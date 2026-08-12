using System;
using System.Reflection;
using Polaris.Diagnostics;

namespace Polaris.Infra
{
    /// <summary>
    /// 会话级健康状况，从 <see cref="PolarisAPI.Health"/> 取：上一局是怎么结束的，以及这一局主线程还在不在动。
    /// 与 <see cref="ErrorsAPI"/> 分开是因为崩溃/卡死时那一局自己没机会汇报，只能跨进程、跨线程地看（哨兵文件、看门狗线程、面包屑）。
    /// </summary>
    public sealed class HealthAPI
    {
        internal HealthAPI() { }

        /// <summary>
        /// 上一局的结局；正常退出时为 null，只有崩溃、被强杀、或判定卡死的那一局才留下东西（判定见 <see cref="SessionEndKind"/>）。
        /// 模组可用它在"上次没能善终"时退回更保守的路径。
        /// </summary>
        public LastSessionInfo LastSession => SessionSentinel.LastSession;

        /// <summary>
        /// 上一局是怎么结束的（判据见 <see cref="SessionEndKind"/>），区分"正常退出"与"我们没看成"，这点 <see cref="LastSession"/> 做不到。
        /// 注意 <see cref="SessionEndKind.Clean"/> 也包含"第一次装 Polaris"的情况。
        /// </summary>
        public SessionEndKind LastSessionEnd => SessionSentinel.LastEnd;

        /// <summary>上一局是不是没能正常结束。</summary>
        public bool LastSessionEndedBadly => SessionSentinel.LastSession != null;

        /// <summary>
        /// 声明"接下来这段时间不推进帧是正常的"，让卡死看门狗在此期间闭嘴；用于读大存档、同步切场景等长耗时操作。
        /// <paramref name="seconds"/> 是硬上限，超过后看门狗恢复工作（即使对象未被释放），给一个宽松但有限的估计即可。
        /// </summary>
        /// <example>
        /// <code>
        /// using (PolarisAPI.Health.ExpectStall("读取存档缩略图", 30))
        /// {
        ///     LoadEveryThumbnail();
        /// }
        /// </code>
        /// </example>
        public IDisposable ExpectStall(string reason, double seconds = 60d)
            => Watchdog.ExpectStall(reason, seconds);

        /// <summary>
        /// 留一条面包屑，告诉 Polaris"我现在开始执行这件事"，卡死时报告能直接说出卡在哪一步。
        /// Polaris 已在多数转发模组代码的关口（模块初始化、补丁应用等）自动埋点，值得手动调的是耗时长又不经 Polaris 转发的活儿（自己的 Update、协程、Harmony 补丁循环）。
        /// 只在主线程有效；后台线程调用会拿到一个空操作对象。
        /// </summary>
        /// <param name="what">给人看的一句话，例如 <c>"重建服装图集"</c>。</param>
        /// <param name="owner">责任程序集，通常是 <c>GetType().Assembly</c>；给了它卡死报告能直接点名模组。</param>
        public IDisposable Activity(string what, Assembly owner = null)
        {
            if (string.IsNullOrEmpty(what) || !MainThreadBeat.OnMainThread)
            {
                return Noop;
            }

            MainThreadBeat.Push(what, owner);
            return new ActivityToken();
        }

        /// <summary>
        /// 判定疑似卡死时触发；在后台线程上触发且主线程正卡着，订阅者不能碰任何 Unity API，只能记日志、写文件。单个订阅者抛异常会被吞掉。
        /// </summary>
        public event Action<HangReport> HangSuspected
        {
            add => Watchdog.HangSuspected += value;
            remove => Watchdog.HangSuspected -= value;
        }

        /// <summary>主线程上一次推进到现在过了几秒。正常游玩时是一帧的长度。</summary>
        public double SecondsSinceLastFrame => MainThreadBeat.SecondsSinceBeat;

        /// <summary>本局判定过几次疑似卡死（判过之后主线程仍可能恢复，所以这个数可以大于 0 而游戏照常跑）。</summary>
        public int HangCount => Watchdog.HangCount;

        static readonly IDisposable Noop = new NoopToken();

        /// <summary>面包屑作用域；用类而非 struct，因为调用方可能重复 Dispose，重复弹栈会错位。</summary>
        sealed class ActivityToken : IDisposable
        {
            bool popped;

            public void Dispose()
            {
                if (popped)
                {
                    return;
                }

                popped = true;
                MainThreadBeat.Pop();
            }
        }

        sealed class NoopToken : IDisposable
        {
            public void Dispose() { }
        }
    }
}
