using System;
using System.Collections.Generic;
using Polaris.Res.Loaders;

namespace Polaris.Res.Runtime
{
    /// <summary>PXLS 复合加载在途列表；PXLS 是唯一天生跨帧的资源种类，挂在 <see cref="ResPump.Tick"/> 上推进，不是通用异步框架。</summary>
    internal static class PxlsPump
    {
        private static readonly List<PxlsLoadOperation> inFlight = new List<PxlsLoadOperation>();
        private static bool subscribed;

        internal static void Enqueue(PxlsLoadOperation operation)
        {
            EnsureSubscribed();
            inFlight.Add(operation);
        }

        private static void EnsureSubscribed()
        {
            if (subscribed)
            {
                return;
            }

            subscribed = true;
            ResPump.Tick += Advance;
        }

        private static void Advance(FrameBudget budget)
        {
            // 每个 in-flight 单独 try/catch，避免一个模组的回调炸了连累其它 PXLS 的收尾。
            for (int i = inFlight.Count - 1; i >= 0; i--)
            {
                PxlsLoadOperation operation = inFlight[i];

                try
                {
                    operation.Tick();
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisRes] PxlsLoadOperation.Tick threw an exception: {ex}");
                }

                if (operation.IsDone)
                {
                    inFlight.RemoveAt(i);
                }
            }
        }
    }
}
