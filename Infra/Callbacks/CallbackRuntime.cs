using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Polaris.API;

namespace Polaris.Infra
{
    /// <summary>
    /// 回调系统的进程唯一派发核心。<see cref="Enqueue"/> 只能在主线程调用（Harmony 补丁和状态差分
    /// 探测都在主线程跑），<see cref="Drain"/> 由 <see cref="Plugin.Update"/>/<see cref="Plugin.LateUpdate"/>
    /// 各调一次，按入队顺序（也就是 <see cref="GameCallbackStamp.Sequence"/> 顺序）执行——
    /// 这是"伤害事件先于它引发的死亡事件"之类跨领域因果顺序的唯一保证来源。
    /// </summary>
    internal static class CallbackRuntime
    {
        static long sequenceCounter;
        static List<Action> pending = new(8);

        internal static long NextSequence() => Interlocked.Increment(ref sequenceCounter);

        /// <summary>给一条即将发布的事件盖章。必须在事件真正发生的那一刻调用，不能延迟到 Drain 时才盖。</summary>
        internal static GameCallbackStamp NextStamp(GameCallbackOrigin origin, GameCallbackPrecision precision)
        {
            long seq = NextSequence();
            return new GameCallbackStamp(seq, SafeUnityFrame(), SafeGameFrame(), GameBinding.MapGeneration, origin, precision);
        }

        static int SafeUnityFrame()
        {
            try { return UnityEngine.Time.frameCount; }
            catch (Exception) { return 0; }
        }

        static int SafeGameFrame()
        {
            try { return PolarisAPI.Game.Loop.GameFrameCount; }
            catch (Exception) { return 0; }
        }

        /// <summary>
        /// 排到下一次 Drain 才真正派发给订阅者。调用方（Harmony 补丁/状态差分）自己已经在主线程，
        /// 这里不加锁——写入方和 <see cref="Drain"/> 的读取方是同一个线程。
        /// </summary>
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
                    // 这里只兜底 RaiseNow 自身的 bug——每个订阅者的异常已经在 Invoke 里单独隔离过了。
                    PolarisAPI.Errors.Report(ex, "Callback dispatch", typeof(CallbackRuntime).Assembly);
                }
            }
        }

        /// <summary>
        /// 单个订阅者的执行：面包屑、耗时统计与异常隔离都在这一层完成，调用方（<see cref="GameSignal{T}"/>）
        /// 不需要重复这些逻辑。一个订阅者抛异常只归因到它自己，不影响同一事件的其它订阅者。
        /// </summary>
        internal static void Invoke<TEvent>(Action<TEvent> handler, TEvent evt, string context, string ownerGuid)
        {
            MethodInfo method = handler.Method;
            Assembly ownerAssembly = method?.DeclaringType?.Assembly;
            var stopwatch = Stopwatch.StartNew();

            using (Diagnostics.MainThreadBeat.Enter($"Callback: {context}", ownerAssembly))
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, $"Callback: {context}", ownerAssembly);
                    CallbackDiagnostics.RecordException(ownerGuid, context);
                }
            }

            stopwatch.Stop();
            CallbackDiagnostics.RecordInvocation(ownerGuid, context, stopwatch.Elapsed.TotalMilliseconds);
        }

        /// <summary>无事件参数版本，供 <see cref="GameFastSignal"/>（Updating/LateUpdating/FixedUpdating）使用。</summary>
        internal static void Invoke(Action handler, string context, string ownerGuid)
        {
            MethodInfo method = handler.Method;
            Assembly ownerAssembly = method?.DeclaringType?.Assembly;
            var stopwatch = Stopwatch.StartNew();

            using (Diagnostics.MainThreadBeat.Enter($"Callback: {context}", ownerAssembly))
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, $"Callback: {context}", ownerAssembly);
                    CallbackDiagnostics.RecordException(ownerGuid, context);
                }
            }

            stopwatch.Stop();
            CallbackDiagnostics.RecordInvocation(ownerGuid, context, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
