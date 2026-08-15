using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Polaris.API;

namespace Polaris.Infra
{
    /// <summary>回调系统的唯一派发核心；<see cref="Enqueue"/> 只在主线程调用，<see cref="Drain"/> 按入队顺序执行以保证跨事件因果顺序。</summary>
    internal static class CallbackRuntime
    {
        static List<Action> pending = new(8);

        /// <summary>排到下一次 Drain 才派发；调用方已在主线程，读写同线程故不加锁。</summary>
        internal static void Enqueue(Action dispatch) => pending.Add(dispatch);

        /// <summary>由 <see cref="Plugin.Update"/>/<see cref="Plugin.LateUpdate"/> 调用，把当前队列清空一次。</summary>
        internal static void Drain()
        {
            if (pending.Count == 0)
            {
                return;
            }

            List<Action> toRun = pending;
            pending = new List<Action>(8);

            for (int i = 0; i < toRun.Count; i++)
            {
                try
                {
                    toRun[i]();
                }
                catch (Exception ex)
                {
                    // 兜底派发本身的 bug；订阅者异常已在 Invoke 里单独隔离。
                    PolarisAPI.Errors.Report(ex, "Callback dispatch", typeof(CallbackRuntime).Assembly);
                }
            }
        }

        /// <summary>执行单个订阅者：统一处理耗时统计与异常隔离，异常只归因该订阅者，不影响其它订阅者。</summary>
        internal static void Invoke<TEvent>(Action<TEvent> handler, TEvent evt, string context, string ownerGuid)
        {
            MethodInfo method = handler.Method;
            Assembly ownerAssembly = method?.DeclaringType?.Assembly;
            var stopwatch = Stopwatch.StartNew();

            using (Diagnostics.DiagnosticsHost.Activity($"Callback: {context}", ownerAssembly))
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, $"Callback: {context}", ownerAssembly);
                    Diagnostics.DiagnosticsHost.RecordCallbackException(ownerGuid, context);
                }
            }

            stopwatch.Stop();
            Diagnostics.DiagnosticsHost.RecordCallbackInvocation(ownerGuid, context, stopwatch.Elapsed.TotalMilliseconds);
        }

    }
}
