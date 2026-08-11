using System;
using System.Collections.Concurrent;

namespace Polaris.Res.Runtime
{
    /// <summary>
    /// 后台线程 → 主线程的唯一桥梁。
    /// <para>
    /// 会往这里排队的调用方：<see cref="Core.Lease{T}"/> 的终结器（绝不能在终结器线程上
    /// 直接触碰引用计数或 Unity 对象）、<c>Runtime.IoScheduler</c> 完成的后台 I/O
    /// 任务、<c>HotReload.FileWatchService</c> 的 <c>FileSystemWatcher</c> 回调。
    /// </para>
    /// <para>
    /// 用 <see cref="ConcurrentQueue{T}"/> 而不是锁：入队方只有"排进去"这一个操作，
    /// 出队只在 <see cref="ResPump"/> 的 <c>Update()</c> 里单线程执行，不需要更重的同步原语。
    /// </para>
    /// </summary>
    internal static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        /// <summary>从任意线程调用，把一个动作排队到下一次主线程 Drain。</summary>
        internal static void Enqueue(Action action)
        {
            if (action == null) { return; }
            queue.Enqueue(action);
        }

        /// <summary>只应由 <see cref="ResPump"/> 在主线程 <c>Update()</c> 里调用。</summary>
        internal static void Drain()
        {
            // 用计数上限而不是"一直 while 到空"：万一某个排队动作又排了新动作
            // （理论上不该发生，但防御一下），也不会在一帧内无限循环。
            int budget = 4096;
            while (budget-- > 0 && queue.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisRes] 主线程派发队列中的动作抛出异常：{ex}");
                }
            }
        }
    }
}
