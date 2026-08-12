using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.API
{
    /// <summary>
    /// 游戏层的会话级服务：就绪门控、语言变化广播，以及由 <see cref="Plugin"/> 驱动的每帧泵。
    /// <para>
    /// 这些能力<b>不在</b> v2 公开规范里，因为它们服务的是 Polaris 自己的各个子系统
    /// （资源、本地化、PUI），不是下游内容模组的游戏 API。放在这里而不是各子系统自己实现一份：
    /// "什么算就绪"是游戏内部结构，只应该有一处知道。
    /// </para>
    /// </summary>
    internal static class GameSessionRuntime
    {
        /// <summary><c>MTRX.loaded</c> 等于 7 才算全部就绪：图标、Shader、私有初始化与音频 sheet 都完成。</summary>
        const int ReadyStage = 7;

        static readonly List<Action> pendingReady = new(4);

        static bool loggedReadyOnce;

        /// <summary>
        /// 游戏资源是否已经完全就绪。任何 PXLS 解析或图像注册都依赖它——
        /// 在此之前碰 <c>MTRX.OMI</c>/<c>OMeshImages</c> 会直接 NullReferenceException。
        /// </summary>
        internal static bool IsReady => PolarisAPI.Game.Assets.LoadStage == ReadyStage;

        /// <summary>玩家切换游戏语言时触发，参数是 (旧语言, 新语言)。供 Polaris 自身子系统使用。</summary>
        internal static event Action<string, string> LocaleChanged;

        /// <summary>
        /// 注册一个"等就绪之后执行"的回调；此刻已就绪则立即同步执行，否则排队，
        /// 由每帧的泵在就绪的那一帧统一执行并清空。
        /// </summary>
        internal static void WhenReady(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (SafeIsReady)
            {
                action();
                return;
            }

            pendingReady.Add(action);
        }

        /// <summary>
        /// 极早期读取游戏内部状态理论上不该抛异常，但防御性地当作"还没好"处理，
        /// 而不是让整条模组初始化链路崩掉。
        /// </summary>
        static bool SafeIsReady
        {
            get
            {
                try
                {
                    return IsReady;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning(
                        $"[Polaris] Exception while probing asset readiness; treating it as not ready: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>由 <see cref="Plugin.Update"/> 每帧调用。</summary>
        internal static void Pump()
        {
            bool ready = SafeIsReady;

            // 只在"首次变为就绪"的那一帧打一次日志，方便验证门控确实在正确的时机放行。
            if (ready && !loggedReadyOnce)
            {
                loggedReadyOnce = true;
                Plugin.Logger.LogMessage(
                    $"[Polaris] Game assets became ready for the first time at frame {UnityEngine.Time.frameCount}.");
            }

            GameBinding.Pump();
            GameRuntime.Pump();

            // 先把这一帧探测到的事件派发出去，订阅者读到的状态就已经是这一帧的最终结果。
            Infra.CallbackRuntime.Drain();

            DrainReady(ready);
        }

        /// <summary>由 <see cref="Plugin.LateUpdate"/> 每帧调用。</summary>
        internal static void PumpLate()
        {
            // Update 里已经排过一轮，这里再排一轮兜底 Update 之后、LateUpdate 之前发生的任何入队
            // （例如其它插件的 Update 比 Polaris 晚跑，在这段时间里触发了 Harmony 补丁）。
            Infra.CallbackRuntime.Drain();

            // 包装器表的清理放在 LateUpdate：这一帧的回调都发完了，此刻失效的条目确实没人再用。
            GameRuntime.Sweep();
        }

        /// <summary>由 <see cref="GameRuntime"/> 在探到语言变化时调用。两条路径共享同一次差分。</summary>
        internal static void NotifyLocaleChanged(string previous, string current)
        {
            Action<string, string> handlers = LocaleChanged;
            if (handlers == null)
            {
                return;
            }

            // 逐个调用而不是 handlers(...)：一个订阅者抛异常不该让后面排队的收不到通知。
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    // 面包屑：切语言会让下游重建自己的全部文案与图像（PolarisLang 要重扫语言包、
                    // PolarisUI 要重排控件），属于"一去不回"的高危回调。
                    using (Diagnostics.MainThreadBeat.Enter(
                        $"LocaleChanged callback ({Describe(handler)})", handler.Method?.DeclaringType?.Assembly))
                    {
                        ((Action<string, string>)handler)(previous, current);
                    }
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "LocaleChanged callback", handler.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError("[Polaris] A LocaleChanged callback threw an exception; ignored.");
                }
            }
        }

        /// <summary>世界卸载/回到标题：让全部游戏实例作废。</summary>
        internal static void ResetWorld() => GameRuntime.ResetWorld();

        static void DrainReady(bool ready)
        {
            if (!ready || pendingReady.Count == 0)
            {
                return;
            }

            // 先复制再清空：回调内部可能再次调用 WhenReady（此时会被当成"已就绪"直接同步执行），
            // 不应该让新加入的回调被这一轮的 Clear 误删。
            var toRun = new List<Action>(pendingReady);
            pendingReady.Clear();

            foreach (Action action in toRun)
            {
                try
                {
                    // 面包屑：这些回调是下游模组"游戏就绪后要做的重活"（解析 PXLS、注册图像、
                    // 建图集），是最有可能一去不回的一类代码。卡住时看门狗要能点出是谁。
                    using (Diagnostics.MainThreadBeat.Enter(
                        $"WhenReady callback ({Describe(action)})", action.Method?.DeclaringType?.Assembly))
                    {
                        action();
                    }
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "WhenReady callback", action.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError("[Polaris] A WhenReady callback threw an exception; ignored.");
                }
            }
        }

        /// <summary>面包屑用的一句话："类型.方法"；委托本身没有方法信息时给个占位。</summary>
        static string Describe(Delegate callback)
        {
            MethodInfo method = callback?.Method;
            if (method == null)
            {
                return "?";
            }

            string owner = method.DeclaringType?.Name ?? method.DeclaringType?.FullName;
            return owner != null ? $"{owner}.{method.Name}" : method.Name;
        }
    }
}
