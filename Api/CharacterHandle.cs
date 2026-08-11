using System.Collections.Generic;
using m2d;

namespace Polaris.API
{
    /// <summary>
    /// 指向某个角色（玩家、敌人、NPC）的稳定引用。<b>不要长期持有游戏对象本身</b>：游戏的 mover
    /// 是对象池复用的，切图之后同一个实例会变成另一个角色，持有引用的代码会在毫不知情的情况下
    /// 对着新住客发号施令。
    /// <para>
    /// 句柄由 <see cref="StableId"/> 和 <see cref="Generation"/> 两段组成：前者认对象，后者认地图。
    /// 地图一换，上一张图发出去的所有句柄整体失效，解析时直接判为过期。
    /// </para>
    /// </summary>
    public readonly struct CharacterHandle
    {
        public long StableId { get; }

        /// <summary>发出这个句柄时的地图代数，见 <c>GameBinding.MapGeneration</c>。</summary>
        public int Generation { get; }

        internal CharacterHandle(long stableId, int generation)
        {
            StableId = stableId;
            Generation = generation;
        }

        /// <summary>空句柄，指向"没有目标"，永远解析失败。</summary>
        public static CharacterHandle None => default;

        /// <summary>只判断句柄本身是不是空的；<b>不代表目标还活着</b>，那要靠解析。</summary>
        public bool IsEmpty => StableId == 0;

        public override string ToString() => IsEmpty ? "CharacterHandle(空)" : $"CharacterHandle({StableId}#{Generation})";
    }

    /// <summary>
    /// 句柄注册表。只在本层内部使用：公开 API 收发的是 <see cref="CharacterHandle"/>，
    /// 游戏对象不出现在任何公开签名里。
    /// </summary>
    internal static class CharacterRegistry
    {
        /// <summary>
        /// 角色对象是 <c>MonoBehaviour</c>，也就是 <c>UnityEngine.Object</c>——它重写过
        /// <c>Equals</c>，把"已销毁"当成 null 来比，于是<b>两个已经被销毁的不同对象会相等</b>，
        /// 而 <c>GetHashCode</c> 又还是各自的实例 id。拿它直接当字典键，哈希与相等就对不上了。
        /// 这里显式改用引用相等，让表只认"是不是同一个对象"这一件事。
        /// </summary>
        sealed class ReferenceComparer : IEqualityComparer<M2Attackable>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(M2Attackable a, M2Attackable b) => ReferenceEquals(a, b);

            public int GetHashCode(M2Attackable obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        static readonly Dictionary<M2Attackable, long> Ids =
            new Dictionary<M2Attackable, long>(ReferenceComparer.Instance);

        static readonly Dictionary<long, M2Attackable> Targets = new Dictionary<long, M2Attackable>();

        static long nextId = 1;
        static int tableGeneration = -1;

        /// <summary>为一个游戏角色取（或分配）句柄。传 <c>null</c> 得到 <see cref="CharacterHandle.None"/>。</summary>
        internal static CharacterHandle Handle(M2Attackable target)
        {
            if (target == null)
            {
                return CharacterHandle.None;
            }

            SyncGeneration();

            if (!Ids.TryGetValue(target, out long id))
            {
                id = nextId++;
                Ids[target] = id;
                Targets[id] = target;
            }

            return new CharacterHandle(id, GameBinding.MapGeneration);
        }

        /// <summary>解析句柄。目标已失效（换图、被销毁、离场）时返回 <c>null</c>。</summary>
        internal static M2Attackable Resolve(CharacterHandle handle)
        {
            if (handle.IsEmpty)
            {
                return null;
            }

            SyncGeneration();

            if (handle.Generation != GameBinding.MapGeneration)
            {
                return null;
            }

            if (!Targets.TryGetValue(handle.StableId, out M2Attackable target))
            {
                return null;
            }

            // Unity 对象被销毁之后 == null 为真而引用本身不为 null，这里必须用 Unity 的相等语义。
            return target == null ? null : target;
        }

        /// <summary>
        /// 地图换了就把整张表清掉。不做逐个存活检查，也不用弱引用：地图代数已经让旧句柄全部失效，
        /// 表里剩下的条目既解析不出来也没人再查，留着只是在给 GC 添堵。
        /// </summary>
        static void SyncGeneration()
        {
            if (tableGeneration == GameBinding.MapGeneration)
            {
                return;
            }

            tableGeneration = GameBinding.MapGeneration;
            Ids.Clear();
            Targets.Clear();
        }
    }
}
