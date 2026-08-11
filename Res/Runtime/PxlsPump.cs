using System;
using System.Collections.Generic;
using Polaris.Res.Loaders;

namespace Polaris.Res.Runtime
{
    /// <summary>
    /// PXLS 复合加载在途列表——PXLS 是唯一天生跨帧的资源种类（游戏自己的
    /// <c>PxlsLoader.loadCharacterASync</c> 内部靠协程，绕不开），这里只是一个专用小列表，
    /// 挂在已有的 <see cref="ResPump.Tick"/> 上推进，不是给其它资源种类复用的通用异步框架。
    /// </summary>
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
            // 每个 job 只是两次布尔属性读取，不需要按 budget 分摊；但每个 in-flight 单独
            // try/catch——一个模组的 Ready/Faulted 回调炸了，不该连累同一帧里其它 PXLS 的收尾。
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
