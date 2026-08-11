using System;
using System.Diagnostics;
using System.Threading;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 卡死看门狗：一条后台线程，盯着主线程还在不在推进帧（<see cref="MainThreadBeat"/>）。
    /// <para>
    /// 这是错误捕获那三条通道完全覆盖不到的一类问题。死循环、死锁、无限递归的等待——
    /// 主线程再也不回来，于是<b>没有异常、没有日志、也不会有 <c>OnApplicationQuit</c></b>，
    /// 三条通道一条都不会响。要发现它，只能有一个不在主线程上的东西负责数着秒。
    /// </para>
    /// <para>
    /// 两级升级：到 <see cref="DiagnosticsConfig.WarnSeconds"/> 只在控制台记一行（很可能只是一次
    /// 长加载，不值得为它写文件、更不值得下一局去打扰玩家）；到
    /// <see cref="DiagnosticsConfig.ReportSeconds"/> 才写报告、并通过 <see cref="SessionSentinel"/>
    /// 给下一局的标题画面上膛——那之后即使玩家是自己去任务管理器把游戏结束掉的，
    /// 下次启动仍然说得清"上一局卡在哪"。
    /// </para>
    /// <para>
    /// <b>默认不杀进程</b>（<see cref="DiagnosticsConfig.KillOnHang"/>）。一次误判就会让玩家丢掉
    /// 这一局的进度，那比卡在那里更糟；而报告在判定的那一刻就已经落盘了，杀不杀进程都不影响
    /// 玩家事后能拿到什么。
    /// </para>
    /// <para>
    /// 这条线程<b>绝对不碰任何 Unity API</b>。Unity 的对象模型只允许主线程访问，而主线程此刻
    /// 恰恰是卡住的那一个——在这里读一个 <c>Time.frameCount</c> 就足以把"发现卡死"变成
    /// "跟着一起死"。所有 Unity 侧的量都由主线程采样进 <see cref="MainThreadBeat"/>，这里只读它。
    /// </para>
    /// </summary>
    internal static class Watchdog
    {
        /// <summary>轮询间隔。判定阈值是十几秒到几十秒，一秒一次的精度远远够用。</summary>
        const int PollMillis = 1000;

        /// <summary>心跳落盘间隔。</summary>
        const int FlushMillis = 5000;

        /// <summary>
        /// <b>自己被饿到这个程度就不作判断。</b>这一轮循环距上一轮超过了它，说明卡住的不只是
        /// 主线程——系统休眠/唤醒、整机换页、调试器断点、虚拟机被挂起都会让所有线程一起停摆。
        /// 那种情况下"主线程停了 40 秒"是真的，但结论"游戏卡死了"是假的。
        /// </summary>
        const int SelfStarvationMillis = 5000;

        /// <summary>一局最多写几份卡死报告。反复卡住又反复恢复的情况下，第五份开始就没有新信息了。</summary>
        const int MaxHangReports = 4;

        /// <summary><see cref="ExpectStall"/> 允许声明的最长时间，防止一个笔误把看门狗关掉一整局。</summary>
        const double MaxExpectSeconds = 600d;

        static Thread thread;
        static bool installed;
        static volatile bool stopping;

        /// <summary>窗口失焦/被系统挂起期间为 true。见 <see cref="SetPaused"/>。</summary>
        static volatile bool paused;

        static int activeStalls;
        static long stallDeadlineMillis;
        static volatile string stallReason;
        static bool warnedAboutLeak;

        // 当前这一轮"停摆事件"的状态。主线程一恢复就整体清零。
        static bool warned;
        static bool reported;
        static double episodePeakSeconds;

        static int hangReports;

        /// <summary>
        /// 判定疑似卡死时触发。<b>在后台线程上触发</b>，而且触发的那一刻主线程正卡着——
        /// 订阅者在这里碰任何 Unity API 都是错的，能做的只有记日志、写文件、发网络请求这类事。
        /// </summary>
        internal static event Action<HangReport> HangSuspected;

        internal static void Install()
        {
            if (installed)
            {
                return;
            }

            installed = true;

            try
            {
                thread = new Thread(Loop)
                {
                    // 后台线程：进程退出时不会因为它还在跑而卡住。看门狗的价值全在"发现问题"，
                    // 不值得为它多留一个能拖住退出流程的东西。
                    IsBackground = true,
                    Priority = ThreadPriority.BelowNormal,
                    Name = "Polaris.Watchdog",
                };
                thread.Start();
            }
            catch (Exception e)
            {
                installed = false;
                thread = null;
                Plugin.Logger.LogWarning($"[Polaris] 卡死检测线程启动失败，本局不做卡死判定：{e.Message}");
            }
        }

        /// <summary>
        /// 停掉看门狗。<b>退出流程一开始就要调</b>：<c>OnApplicationQuit</c> 之后 Unity 不再调
        /// <c>Update</c>，而进程还要活一会儿（存档、淡出、资源释放），不停掉就会把正常的退出
        /// 过程判成卡死，还顺手给下一局上一发误报。
        /// </summary>
        internal static void Uninstall()
        {
            if (!installed)
            {
                return;
            }

            installed = false;
            stopping = true;

            try
            {
                thread?.Interrupt();
            }
            catch (Exception)
            {
                // 打断失败也无所谓：IsBackground 的线程不会拖住进程退出。
            }

            thread = null;
        }

        /// <summary>
        /// 由 <c>Plugin.OnApplicationFocus</c>/<c>OnApplicationPause</c> 调用。
        /// <para>
        /// 这是最大的一个误报来源：<c>Application.runInBackground</c> 为 false 时，窗口一失焦
        /// Unity 就不再调 <c>Update</c>——主线程完全健康，只是没事干。玩家去泡杯茶回来，
        /// 看门狗已经"发现"了一次五分钟的卡死。
        /// </para>
        /// <para>
        /// 恢复时顺手把心跳基线抹平：这个回调在主线程上、且发生在同一帧的 <c>Update</c> 之前，
        /// 不抹的话中间那一小段仍然带着失焦期间攒下的陈旧读数。
        /// </para>
        /// </summary>
        internal static void SetPaused(bool value)
        {
            if (!value)
            {
                MainThreadBeat.ResetBaseline();
            }

            paused = value;
        }

        /// <summary>
        /// 声明"接下来这段时间不推进帧是正常的"（读大存档、切场景、一次性解析一堆资源）。
        /// 返回的对象释放时结束声明；<paramref name="seconds"/> 是硬上限，忘记释放也不会把看门狗
        /// 永久关掉。
        /// </summary>
        internal static IDisposable ExpectStall(string reason, double seconds)
        {
            double clamped = seconds > 0d ? Math.Min(seconds, MaxExpectSeconds) : 1d;
            long deadline = MainThreadBeat.ElapsedMillis + (long)(clamped * 1000d);

            stallReason = reason;
            Interlocked.Increment(ref activeStalls);

            // 多个声明同时存在时取最晚的那个截止时间。
            while (true)
            {
                long current = Volatile.Read(ref stallDeadlineMillis);
                if (deadline <= current)
                {
                    break;
                }

                if (Interlocked.CompareExchange(ref stallDeadlineMillis, deadline, current) == current)
                {
                    break;
                }
            }

            return new StallToken();
        }

        /// <summary>本局判定过几次卡死。</summary>
        internal static int HangCount => hangReports;

        // ================== 主循环 ==================

        static void Loop()
        {
            long previous = MainThreadBeat.ElapsedMillis;
            long lastFlush = previous;

            while (!stopping)
            {
                try
                {
                    Thread.Sleep(PollMillis);
                }
                catch (ThreadInterruptedException)
                {
                    return;
                }

                if (stopping)
                {
                    return;
                }

                try
                {
                    long now = MainThreadBeat.ElapsedMillis;
                    long sinceLastPoll = now - previous;
                    previous = now;

                    // 心跳照刷：哪怕这一轮不做判定，"上一局最后活到什么时候"也得记下来，
                    // 崩溃检测靠的就是它。
                    if (now - lastFlush >= FlushMillis)
                    {
                        lastFlush = now;
                        SessionSentinel.Flush();
                    }

                    if (sinceLastPoll > SelfStarvationMillis)
                    {
                        // 连我们自己都被停了这么久，这一轮的读数不能当证据。
                        ResetEpisode(silent: true);
                        continue;
                    }

                    if (!Judgeable(now))
                    {
                        ResetEpisode(silent: true);
                        continue;
                    }

                    Judge(now);
                }
                catch (Exception)
                {
                    // 看门狗自己抛异常绝对不能让线程死掉——它一死，之后真的卡住了也没人看着了。
                    // 这里同样不记日志：能走到这一步的异常多半会每轮都复现，一秒一行足以把日志埋掉。
                }
            }
        }

        /// <summary>这一轮该不该作判断。</summary>
        static bool Judgeable(long now)
        {
            if (paused || !DiagnosticsConfig.WatchdogEnabled)
            {
                return false;
            }

            // 挂着调试器时"主线程停了两分钟"通常意味着有人正在看某一行代码。
            if (Debugger.IsAttached)
            {
                return false;
            }

            return !Suppressed(now);
        }

        /// <summary>调用方有没有声明"这段时间不推进帧是正常的"。</summary>
        static bool Suppressed(long now)
        {
            if (Volatile.Read(ref activeStalls) <= 0)
            {
                warnedAboutLeak = false;
                return false;
            }

            if (now < Volatile.Read(ref stallDeadlineMillis))
            {
                return true;
            }

            // 声明超时还没释放。多半是调用方在那段代码里抛了异常、把 Dispose 跳过去了。
            // 不再顺着它——否则一次泄漏就等于把这一局的卡死检测整个关掉。
            if (!warnedAboutLeak)
            {
                warnedAboutLeak = true;
                Plugin.Logger.LogWarning(
                    $"[Polaris] 有一个「预期卡顿」声明（{stallReason ?? "未命名"}）超过了自己声明的时限还没释放，"
                    + "卡死检测继续正常工作。");
            }

            return false;
        }

        static void Judge(long now)
        {
            double stall = MainThreadBeat.SecondsSinceBeat;
            bool boot = !MainThreadBeat.HasBeaten;

            double reportAt = boot ? DiagnosticsConfig.BootReportSeconds : DiagnosticsConfig.ReportSeconds;

            // 启动阶段的警告线跟着报告线一起抬。用游戏中的 10 秒去量启动，等于每局都在控制台
            // 抱怨一次"启动慢"——而启动慢本来就是这游戏的常态。
            double warnAt = boot
                ? Math.Max(DiagnosticsConfig.WarnSeconds, reportAt / 3d)
                : DiagnosticsConfig.WarnSeconds;

            if (stall < warnAt)
            {
                ResetEpisode(silent: false);
                return;
            }

            if (stall > episodePeakSeconds)
            {
                episodePeakSeconds = stall;
            }

            if (!warned)
            {
                warned = true;
                Plugin.Logger.LogWarning(
                    $"[Polaris] 主线程已经 {stall:0} 秒没有推进"
                    + (boot ? "（还在启动阶段）" : $"（frame {MainThreadBeat.LastFrame}）")
                    + $"。当前在执行：{MainThreadBeat.ActivityChain() ?? "（不在任何 Polaris 埋点里）"}。"
                    + $"超过 {reportAt:0} 秒会按卡死记录一份报告。");

                // 立刻落一次盘：这一刻的面包屑是最接近病根的，等下一次周期性心跳可能就没机会了。
                SessionSentinel.Flush();
            }

            if (reported || stall < reportAt)
            {
                return;
            }

            reported = true;
            Report(stall, boot);
        }

        /// <summary>主线程恢复了（或这一轮不作判断）：清掉本轮停摆事件的状态。</summary>
        static void ResetEpisode(bool silent)
        {
            if (warned && !silent)
            {
                Plugin.Logger.LogMessage(
                    $"[Polaris] 主线程已恢复（这次一共停了约 {episodePeakSeconds:0} 秒）。");
            }

            warned = false;
            reported = false;
            episodePeakSeconds = 0d;
        }

        static void Report(double stall, bool boot)
        {
            hangReports++;

            var report = new HangReport
            {
                DetectedAt = DateTime.Now,
                StallSeconds = stall,
                LastFrame = MainThreadBeat.LastFrame,
                Scene = MainThreadBeat.SceneName,
                Activity = MainThreadBeat.ActivityChain(),
                Culprit = MainThreadBeat.CurrentOwner(),
                Index = hangReports,
                DuringBoot = boot,
            };

            // 顺序和 ErrorRegistry / FatalRegistry 一致：先落盘，再打日志——日志最后一行要报出
            // 报告文件的位置，写失败时不能对着玩家撒谎说"已写入"。
            if (hangReports <= MaxHangReports)
            {
                ErrorReportWriter.AppendHang(report);
            }

            // 哨兵必须无条件更新，哪怕报告已经不写了：它是下一局唯一的信息来源。
            SessionSentinel.MarkHung(report);

            if (hangReports <= MaxHangReports)
            {
                Log(report);
            }

            Raise(report);

            if (DiagnosticsConfig.KillOnHang)
            {
                Kill();
            }
        }

        static void Log(HangReport report)
        {
            Plugin.Logger.LogError($"[Polaris] 疑似卡死：{report.OneLine()}");

            if (report.Culprit != null)
            {
                string owner;
                try
                {
                    owner = AssemblyOwnerIndex.Of(report.Culprit)?.Describe() ?? report.Culprit.GetName().Name;
                }
                catch (Exception)
                {
                    owner = "（查不出归属）";
                }

                Plugin.Logger.LogError($"[Polaris] 停止响应时正在执行的代码属于：{owner}");
            }

            string path = ErrorReportWriter.LastWrittenPath;
            Plugin.Logger.LogError(path != null
                ? $"[Polaris] 卡死报告：{path}"
                : "[Polaris] 报告文件写入失败，线索只能从本日志里找。");

            Plugin.Logger.LogError(
                "[Polaris] 游戏可能已经无法操作。下次启动时标题画面会再提醒一次这件事。"
                + (DiagnosticsConfig.KillOnHang ? "" : "（Polaris 不会自动结束游戏，见 _polaris_diagnostics.cfg）"));
        }

        static void Raise(HangReport report)
        {
            Action<HangReport> handlers = HangSuspected;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<HangReport>)handler)(report);
                }
                catch (Exception)
                {
                    // 一个订阅者写坏了不该连累其它订阅者，更不该把看门狗线程带走。
                }
            }
        }

        /// <summary>
        /// 结束进程。走不了 <c>Application.Quit</c>——那是 Unity API，只能在主线程调，
        /// 而主线程正是卡住的那一个；也不走 <c>Environment.Exit</c>，它要跑终结器和
        /// AppDomain 卸载，那些同样可能要等主线程。剩下唯一确定能生效的就是直接杀自己。
        /// </summary>
        static void Kill()
        {
            Plugin.Logger.LogError("[Polaris] KillOnHang 已开启，正在结束游戏进程。");

            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris] 结束进程失败，请手动关闭游戏：{e.Message}");
            }
        }

        sealed class StallToken : IDisposable
        {
            bool released;

            public void Dispose()
            {
                if (released)
                {
                    return;
                }

                released = true;
                Interlocked.Decrement(ref activeStalls);
            }
        }
    }
}
