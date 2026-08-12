using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Polaris.API
{
    /// <summary>
    /// "同一个游戏对象永远给出同一个包装器"的实现。每种实例类型持有一张自己的表。
    /// <para>
    /// 键必须按<b>引用相等</b>比较。游戏里的角色和地图都是 <c>UnityEngine.Object</c>，
    /// 它重写过 <c>Equals</c>，把"已销毁"当成 <c>null</c> 来比——于是两个已经被销毁的
    /// 不同对象会互相相等，而 <c>GetHashCode</c> 又还是各自的实例 id。拿它直接当字典键，
    /// 哈希与相等就对不上，表会开始丢条目。这里显式换成引用相等，让表只认"是不是同一个对象"。
    /// </para>
    /// <para>
    /// 表不持有游戏对象的强引用之外的东西，并在 <see cref="Sweep"/> 里成批丢弃已经失效的条目：
    /// 一局游戏下来会经过成千上万个敌人，留着只是在给 GC 添堵。
    /// </para>
    /// </summary>
    internal sealed class InstanceTable<TNative, TWrapper>
        where TNative : class
        where TWrapper : GameInstance
    {
        sealed class ReferenceComparer : IEqualityComparer<TNative>
        {
            internal static readonly ReferenceComparer Instance = new();

            public bool Equals(TNative a, TNative b) => ReferenceEquals(a, b);

            public int GetHashCode(TNative obj) => RuntimeHelpers.GetHashCode(obj);
        }

        readonly Dictionary<TNative, TWrapper> table = new(ReferenceComparer.Instance);
        readonly List<TNative> sweepBuffer = new(8);

        /// <summary>取（或建立）某个游戏对象的包装器。传 <c>null</c> 得到 <c>null</c>。</summary>
        internal TWrapper Get(TNative native, Func<TNative, TWrapper> factory)
        {
            if (native == null)
            {
                return null;
            }

            if (table.TryGetValue(native, out TWrapper existing))
            {
                if (existing.IsValid)
                {
                    return existing;
                }

                // 同一个池对象换了新住客：旧包装器已经失效，重新发一个，而不是把旧的"复活"
                // ——旧包装器上的回调是上一任住客的订阅者注册的，不该转嫁给新住客。
                table.Remove(native);
            }

            TWrapper created = factory(native);
            table[native] = created;
            return created;
        }

        /// <summary>已经建过包装器就返回它，没建过返回 <c>null</c>（不新建）。</summary>
        internal TWrapper Peek(TNative native)
        {
            if (native == null)
            {
                return null;
            }

            return table.TryGetValue(native, out TWrapper existing) ? existing : null;
        }

        /// <summary>让某个游戏对象的包装器失效并移出表。</summary>
        internal void Invalidate(TNative native)
        {
            if (native == null || !table.TryGetValue(native, out TWrapper wrapper))
            {
                return;
            }

            table.Remove(native);
            wrapper.Invalidate();
        }

        /// <summary>整表失效。地图切换这类"上一批全体作废"的时刻用。</summary>
        internal void InvalidateAll()
        {
            if (table.Count == 0)
            {
                return;
            }

            var wrappers = new List<TWrapper>(table.Values);
            table.Clear();

            foreach (TWrapper wrapper in wrappers)
            {
                wrapper.Invalidate();
            }
        }

        /// <summary>
        /// 遍历当前仍有效的包装器。用于每帧的状态差分——只有<b>被人取到过</b>的实例才在表里，
        /// 因此没人关心的敌人不会产生任何轮询开销。
        /// </summary>
        internal void Each(Action<TWrapper> visit)
        {
            if (table.Count == 0)
            {
                return;
            }

            // 先复制再遍历：差分回调可能间接让某个实例失效并改动这张表。
            var snapshot = new List<TWrapper>(table.Values);
            foreach (TWrapper wrapper in snapshot)
            {
                if (wrapper.IsValid)
                {
                    visit(wrapper);
                }
            }
        }

        /// <summary>丢掉已经失效的条目。由每帧的泵低频调用。</summary>
        internal void Sweep()
        {
            if (table.Count == 0)
            {
                return;
            }

            sweepBuffer.Clear();
            foreach (KeyValuePair<TNative, TWrapper> pair in table)
            {
                if (!pair.Value.IsValid)
                {
                    sweepBuffer.Add(pair.Key);
                }
            }

            foreach (TNative key in sweepBuffer)
            {
                table.Remove(key);
            }

            sweepBuffer.Clear();
        }
    }
}
