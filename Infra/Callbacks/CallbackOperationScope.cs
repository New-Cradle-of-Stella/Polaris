using System;
using System.Threading;

namespace Polaris.Infra
{
    /// <summary>
    /// 顶层语义操作的 Id：伤害、恢复这类调用链里外层方法（<c>PR.applyDamage</c>）
    /// 会往下调用细分方法（<c>M2Attackable.applyHpDamage</c>）。外层 Enter 时分配一个新 Id，
    /// 内层再 Enter 时发现已经在一个作用域里，直接复用同一个 Id——这样细分事件和高层事件能用
    /// 同一个 <c>OperationId</c> 关联，不需要每层都各自猜"这是不是同一次攻击"。
    /// <para>
    /// 只在主线程使用（回调系统本来就只在主线程派发），用一个整数深度计数器 + 当前 Id 即可，
    /// 不需要真正的栈：细分方法不会在完全跑完外层方法之前把 Id 让位给另一个不相关的操作。
    /// </para>
    /// </summary>
    internal static class CallbackOperationScope
    {
        static long counter;
        static long currentId;
        static int depth;

        internal static long CurrentId => depth > 0 ? currentId : 0;

        internal static bool IsInsideOperation => depth > 0;

        /// <summary>
        /// 进入一次顶层操作；已经在另一个操作内部时只增加深度，复用现有 Id。
        /// <para>
        /// 提供 <see cref="Scope"/>（配合 <c>using</c>）给普通 C# 调用方；Harmony 补丁的
        /// Prefix/Postfix/Finalizer 分处三个不同方法、无法共享一个词法作用域，那边直接配对调用
        /// <see cref="Enter"/> 和 <see cref="Exit"/>。
        /// </para>
        /// </summary>
        internal static Scope Enter()
        {
            if (depth == 0)
            {
                currentId = Interlocked.Increment(ref counter);
            }

            depth++;
            return new Scope();
        }

        /// <summary>与一次 <see cref="Enter"/> 配对；给 Harmony Finalizer 用，Dispose 语义完全一致。</summary>
        internal static void Exit()
        {
            if (depth > 0)
            {
                depth--;
            }
        }

        internal readonly struct Scope : IDisposable
        {
            public void Dispose() => Exit();
        }
    }
}
