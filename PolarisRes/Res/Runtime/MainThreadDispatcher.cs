using System;
using System.Collections.Concurrent;

namespace Polaris.Res.Runtime
{
    /// <summary>后台线程到主线程的唯一桥梁（终结器、后台 I/O、文件监听回调等排队用）；用 <see cref="ConcurrentQueue{T}"/> 而非锁，因为出队仅在主线程单线程执行。</summary>
    internal static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        /// <summary>从任意线程调用，把一个动作排队到下一次主线程 Drain。</summary>
        internal static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            queue.Enqueue(action);
        }

        /// <summary>只应由 <see cref="ResPump"/> 在主线程 <c>Update()</c> 里调用。</summary>
        internal static void Drain()
        {
            // 用计数上限而非无限循环，防止排队动作又排新动作导致死循环。
            int budget = 4096;
            while (budget-- > 0 && queue.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisRes] An action in the main-thread dispatch queue threw an exception: {ex}");
                }
            }
        }
    }
}
