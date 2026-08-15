using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Polaris.API
{
    /// <summary>
    /// "同一个游戏对象永远给出同一个包装器"的实现，每种实例类型持有一张自己的表。
    /// 键按引用相等比较（<c>UnityEngine.Object</c> 的 <c>Equals</c> 把"已销毁"当 null 比，直接当字典键会丢条目），
    /// <see cref="Sweep"/> 定期批量清理已失效的条目以免堆积。
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

                // 池对象换了新住客：旧包装器已失效，重新发一个，不复用（避免旧回调转嫁给新住客）。
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

        /// <summary>遍历当前仍有效的包装器，用于每帧状态差分；只有被取到过的实例才在表里。</summary>
        internal void Each(Action<TWrapper> visit)
        {
            if (table.Count == 0)
            {
                return;
            }

            // 先复制再遍历：回调可能间接让实例失效并改动这张表。
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
