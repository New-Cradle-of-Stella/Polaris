using System;
using System.Reflection;
using Polaris.Diagnostics;

namespace Polaris.Infra
{
    /// <summary>
    /// 会话级健康状况，从 <see cref="PolarisAPI.Health"/> 取：<b>上一局是怎么结束的</b>，以及
    /// <b>这一局主线程还在不在动</b>。
    /// <para>
    /// 和 <see cref="ErrorsAPI"/> 分开而不是塞进去，是因为两者的因果方向不同。
    /// <c>Errors</c> 处理的是"有一个异常抛出来了"——出事的那一局自己有能力汇报，
    /// 报告、归因、告知页都是当场就能做完的事。而崩溃和卡死是唯一一类<b>出事的那一局
    /// 自己没机会汇报</b>的问题：进程已经没了，或者主线程再也不回来了，
    /// <c>OnApplicationQuit</c> 不会来、异常通道一条都没响。这类问题只能跨进程、跨线程地看，
    /// 用的机制（哨兵文件、后台看门狗线程、面包屑）和错误上报没有一处相同。
    /// </para>
    /// </summary>
    public sealed class HealthAPI
    {
        internal HealthAPI() { }

        /// <summary>
        /// 上一局的结局。<b>上一局正常退出时为 null</b>——只有崩溃、被强杀、或被判定卡死的那一局
        /// 才会在这里留下东西（判定见 <see cref="SessionEndKind"/>）。
        /// <para>
        /// 模组可以用它做一些"上次没能善终"时的自我保护，比如上一局刚好卡在自己的初始化里，
        /// 这一局就退回一个更保守的路径。
        /// </para>
        /// </summary>
        public LastSessionInfo LastSession => SessionSentinel.LastSession;

        /// <summary>
        /// 上一局是怎么结束的。<see cref="LastSession"/> 只在出事时才非 null，分不出"正常退出"
        /// 和"我们没看成"；要区分这两者的用这个（判据见 <see cref="SessionEndKind"/>）。
        /// <para>
        /// 注意 <see cref="SessionEndKind.Clean"/> 也包含"这是第一次装 Polaris"——第一次运行和
        /// 上一局正常退出留下的东西完全一样，都是什么都没有。
        /// </para>
        /// </summary>
        public SessionEndKind LastSessionEnd => SessionSentinel.LastEnd;

        /// <summary>上一局是不是没能正常结束。</summary>
        public bool LastSessionEndedBadly => SessionSentinel.LastSession != null;

        /// <summary>
        /// 声明"接下来这段时间不推进帧是正常的"，让卡死看门狗在此期间闭嘴。
        /// 读一个很大的存档、同步切场景、一次性解析上千张图这类操作都该包一层。
        /// <para>
        /// <paramref name="seconds"/> 是硬上限：即使返回的对象没被释放（比如那段代码抛了异常），
        /// 超过它之后看门狗也会恢复工作，所以给一个宽松但有限的估计就好。
        /// </para>
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
        /// 留一条面包屑：告诉 Polaris"我现在开始执行这件事"。真的卡死时，报告和下一局的告知页
        /// 就能直接说出卡在哪一步、该找谁，而不是只报一句"游戏不动了"。
        /// <para>
        /// Polaris 自己已经在几个转发模组代码的关口埋好了（模块初始化、补丁应用、
        /// <see cref="API.GameSessionRuntime.WhenReady"/> 与 <c>LocaleChanged</c> 的回调、
        /// <see cref="ErrorsAPI.Guard"/>），所以多数模组不需要手动调它。值得自己埋的是那种
        /// <b>耗时长、又不经 Polaris 转发</b>的活儿：自己的 <c>Update</c> 里的重计算、
        /// 自己起的协程、自己挂的 Harmony 补丁里的循环。
        /// </para>
        /// <para>
        /// 只在主线程上有效（面包屑栈是主线程独占的，见 <see cref="Diagnostics.MainThreadBeat"/>）；
        /// 从后台线程调进来会拿到一个什么都不做的对象，不会出错也不会记错。
        /// </para>
        /// </summary>
        /// <param name="what">给人看的一句话，例如 <c>"重建服装图集"</c>。</param>
        /// <param name="owner">
        /// 责任程序集，通常是 <c>GetType().Assembly</c>。给了它，卡死报告就能直接点名模组、
        /// 连带作者与主页一起写出来。
        /// </param>
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
        /// 判定疑似卡死时触发。
        /// <para>
        /// <b>在后台线程上触发，而且触发的那一刻主线程正卡着。</b>订阅者在这里碰任何 Unity API
        /// 都是错的（Unity 的对象模型只允许主线程访问，何况那个线程此刻根本不动），
        /// 能做的只有记日志、写文件这类与引擎无关的事。单个订阅者抛异常会被吞掉。
        /// </para>
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

        /// <summary>
        /// 面包屑作用域。用类而不是 <c>struct</c>：公开 API 交出去的是 <see cref="IDisposable"/>，
        /// 调用方重复 <c>Dispose</c> 是完全可能发生的事，而面包屑栈弹多了就会错位。
        /// （Polaris 内部的埋点走 <see cref="MainThreadBeat.Enter"/> 那条不装箱的 struct 路径。）
        /// </summary>
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
