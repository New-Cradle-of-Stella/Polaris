using System.Collections.Generic;
using Polaris.API;

namespace Polaris.Infra
{
    /// <summary>
    /// 每个 <see cref="GameCallbackKind"/> 当前的可用状态。领域门面的静态构造器在创建
    /// <see cref="GameSignal{T}"/> 时调用 <see cref="Declare"/> 登记初始状态；依赖 Harmony 补丁的
    /// 种类由 <see cref="CallbackPatchRegistry"/> 在补丁应用结果出来后调用 <see cref="Update"/> 修正。
    /// 未登记的种类一律视为"这条回调本 Polaris 构建里还没实现"。
    /// </summary>
    internal static class CallbackRegistry
    {
        sealed class Entry
        {
            public GameCallbackAvailability Availability;
            public GameCallbackPrecision Precision;
            public string Reason;
        }

        static readonly Dictionary<GameCallbackKind, Entry> entries = new();

        internal static void Declare(GameCallbackKind kind, GameCallbackAvailability availability,
            GameCallbackPrecision precision, string reason = null)
        {
            entries[kind] = new Entry { Availability = availability, Precision = precision, Reason = reason };
        }

        internal static void Update(GameCallbackKind kind, GameCallbackAvailability availability, string reason)
        {
            if (entries.TryGetValue(kind, out Entry entry))
            {
                entry.Availability = availability;
                entry.Reason = reason;
                return;
            }

            entries[kind] = new Entry { Availability = availability, Precision = GameCallbackPrecision.Exact, Reason = reason };
        }

        internal static GameCallbackStatus Status(GameCallbackKind kind)
        {
            if (entries.TryGetValue(kind, out Entry entry))
            {
                return new GameCallbackStatus(kind, entry.Availability, entry.Precision, entry.Reason);
            }

            return new GameCallbackStatus(kind, GameCallbackAvailability.Unsupported, GameCallbackPrecision.Exact,
                "This callback is not implemented in this Polaris build yet.");
        }

        internal static IReadOnlyList<GameCallbackDescriptor> DescribeAll()
        {
            var list = new List<GameCallbackDescriptor>(entries.Count);
            foreach (KeyValuePair<GameCallbackKind, Entry> kv in entries)
            {
                list.Add(new GameCallbackDescriptor(kv.Key, kv.Value.Availability, kv.Value.Precision, kv.Value.Reason));
            }

            return list;
        }
    }
}
