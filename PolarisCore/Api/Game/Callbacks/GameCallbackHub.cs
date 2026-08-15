using System;
using System.Collections.Generic;
using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>
    /// v2 回调的进程唯一派发核心：静态回调按种类分组，实例回调按"种类 + 实例编号"分组。
    /// 派发经 <see cref="CallbackRuntime"/> 入队延迟执行（避免在 Harmony 补丁中间同步触发下游代码），
    /// 订阅列表用 copy-on-write 数组保证派发时不受并发增删影响。
    /// </summary>
    internal static class GameCallbackHub
    {
        sealed class Entry
        {
            internal Delegate Handler;
            internal GameCallbackOptions Options;
            internal GameCallbackRegistration Registration;
            internal long Sequence;
            internal volatile bool Active = true;
        }

        readonly struct InstanceKey : IEquatable<InstanceKey>
        {
            internal InstanceKey(GameInstanceCallbackKind kind, long instanceId)
            {
                Kind = kind;
                InstanceId = instanceId;
            }

            internal GameInstanceCallbackKind Kind { get; }

            internal long InstanceId { get; }

            public bool Equals(InstanceKey other) => Kind == other.Kind && InstanceId == other.InstanceId;

            public override bool Equals(object obj) => obj is InstanceKey other && Equals(other);

            public override int GetHashCode() => ((int)Kind * 397) ^ InstanceId.GetHashCode();
        }

        static readonly object gate = new();
        static readonly Dictionary<GameStaticCallbackKind, Entry[]> statics = new();
        static readonly Dictionary<InstanceKey, Entry[]> instances = new();

        /// <summary>某个实例编号上挂了哪些键，用于实例失效时一次性摘干净。</summary>
        static readonly Dictionary<long, List<InstanceKey>> instanceKeys = new();

        static readonly Entry[] Empty = Array.Empty<Entry>();

        static long sequenceCounter;

        // ── 注册 ───────────────────────────────────────────────────────────────

        internal static GameCallbackRegistration RegisterStatic<TData>(
            GameStaticCallbackKind kind, Action<TData> callback, GameCallbackOptions options)
            where TData : GameCallbackData
        {
            options ??= GameCallbackOptions.Default;
            var entry = new Entry { Handler = callback, Options = options };
            var registration = new GameCallbackRegistration(
                () => RemoveStatic(kind, entry),
                CallbackOwnerResolver.ResolveGuid(callback.Method),
                options.DebugName);
            entry.Registration = registration;

            lock (gate)
            {
                entry.Sequence = ++sequenceCounter;
                statics[kind] = Insert(statics.TryGetValue(kind, out Entry[] current) ? current : Empty, entry);
            }

            return registration;
        }

        internal static GameCallbackRegistration RegisterInstance<TData>(
            GameInstanceCallbackKind kind, GameInstance owner, Action<TData> callback, GameCallbackOptions options)
            where TData : GameCallbackData
        {
            options ??= GameCallbackOptions.Default;
            var key = new InstanceKey(kind, owner.InstanceId);
            var entry = new Entry { Handler = callback, Options = options };
            var registration = new GameCallbackRegistration(
                () => RemoveInstance(key, entry),
                CallbackOwnerResolver.ResolveGuid(callback.Method),
                options.DebugName);
            entry.Registration = registration;

            lock (gate)
            {
                entry.Sequence = ++sequenceCounter;
                instances[key] = Insert(instances.TryGetValue(key, out Entry[] current) ? current : Empty, entry);

                if (!instanceKeys.TryGetValue(owner.InstanceId, out List<InstanceKey> keys))
                {
                    keys = new List<InstanceKey>(2);
                    instanceKeys[owner.InstanceId] = keys;
                }

                if (!keys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            // 在已失效的实例上注册：立即标记为非活跃，避免留下一个永远不会触发的“正常”注册。
            if (!owner.IsValid)
            {
                registration.Dispose();
            }

            return registration;
        }

        static Entry[] Insert(Entry[] current, Entry entry)
        {
            var next = new Entry[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = entry;

            // 稳定排序：优先级相同则按注册顺序执行。
            Array.Sort(next, static (a, b) =>
            {
                int byPriority = a.Options.Priority.CompareTo(b.Options.Priority);
                return byPriority != 0 ? byPriority : a.Sequence.CompareTo(b.Sequence);
            });

            return next;
        }

        static Entry[] Remove(Entry[] current, Entry entry)
        {
            int index = Array.IndexOf(current, entry);
            if (index < 0)
            {
                return current;
            }

            if (current.Length == 1)
            {
                return Empty;
            }

            var next = new Entry[current.Length - 1];
            for (int i = 0, w = 0; i < current.Length; i++)
            {
                if (i != index)
                {
                    next[w++] = current[i];
                }
            }

            return next;
        }

        static void RemoveStatic(GameStaticCallbackKind kind, Entry entry)
        {
            lock (gate)
            {
                if (statics.TryGetValue(kind, out Entry[] current))
                {
                    statics[kind] = Remove(current, entry);
                }
            }
        }

        static void RemoveInstance(InstanceKey key, Entry entry)
        {
            lock (gate)
            {
                if (instances.TryGetValue(key, out Entry[] current))
                {
                    Entry[] next = Remove(current, entry);
                    if (next.Length == 0)
                    {
                        instances.Remove(key);
                    }
                    else
                    {
                        instances[key] = next;
                    }
                }
            }
        }

        /// <summary>实例失效：把挂在它上面的全部注册摘掉并标记为非活跃。</summary>
        internal static void ReleaseInstance(long instanceId)
        {
            List<InstanceKey> keys;
            var orphaned = new List<Entry>(4);

            lock (gate)
            {
                if (!instanceKeys.TryGetValue(instanceId, out keys))
                {
                    return;
                }

                instanceKeys.Remove(instanceId);

                foreach (InstanceKey key in keys)
                {
                    if (instances.TryGetValue(key, out Entry[] current))
                    {
                        orphaned.AddRange(current);
                        instances.Remove(key);
                    }
                }
            }

            // 在锁外标记非活跃，避免调用方 Dispose 中的其他逻辑被锁住。
            foreach (Entry entry in orphaned)
            {
                entry.Active = false;
                entry.Registration.MarkInactiveOnly();
            }
        }

        // ── 发布 ───────────────────────────────────────────────────────────────

        /// <summary>有没有人在听这条静态回调。发布方在<b>构造负荷之前</b>先问一句，零订阅时不分配。</summary>
        internal static bool HasStatic(GameStaticCallbackKind kind)
        {
            lock (gate)
            {
                return statics.TryGetValue(kind, out Entry[] current) && current.Length > 0;
            }
        }

        internal static bool HasInstance(GameInstanceCallbackKind kind, GameInstance owner)
        {
            if (owner == null)
            {
                return false;
            }

            lock (gate)
            {
                return instances.TryGetValue(new InstanceKey(kind, owner.InstanceId), out Entry[] current)
                    && current.Length > 0;
            }
        }

        /// <summary>发布一条静态回调；<paramref name="factory"/> 只在有订阅者时才被调用，避免无人监听时白白构造负荷。</summary>
        internal static void PublishStatic<TData>(GameStaticCallbackKind kind, Func<TData> factory)
            where TData : GameCallbackData
        {
            Entry[] current;
            lock (gate)
            {
                if (!statics.TryGetValue(kind, out current) || current.Length == 0)
                {
                    return;
                }
            }

            Publish(current, kind.ToString(), factory);
        }

        internal static void PublishInstance<TData>(
            GameInstanceCallbackKind kind, GameInstance owner, Func<TData> factory)
            where TData : GameCallbackData
        {
            if (owner == null)
            {
                return;
            }

            var key = new InstanceKey(kind, owner.InstanceId);
            Entry[] current;
            lock (gate)
            {
                if (!instances.TryGetValue(key, out current) || current.Length == 0)
                {
                    return;
                }
            }

            Publish(current, kind.ToString(), factory);
        }

        /// <summary>确认有订阅者之后的公共尾段：构造负荷（失败就放弃这一条）并按发布顺序入队。</summary>
        static void Publish<TData>(Entry[] entries, string context, Func<TData> factory)
            where TData : GameCallbackData
        {
            TData data;
            try
            {
                data = factory();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, $"Building callback payload for {context}", typeof(GameCallbackHub).Assembly);
                return;
            }

            if (data == null)
            {
                return;
            }

            CallbackRuntime.Enqueue(() => Dispatch(entries, data, context));
        }

        /// <summary>真正调用订阅者，使用发布那一刻的数组快照；本轮新增的订阅者不会收到，本轮被 Dispose 的靠 <see cref="Entry.Active"/> 挡住。</summary>
        static void Dispatch<TData>(Entry[] entries, TData data, string context) where TData : GameCallbackData
        {
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (!entry.Active)
                {
                    continue;
                }

                if (entry.Options.Once)
                {
                    // 调用前先标记失效，防止重入路径下同一事件被执行两遍。
                    entry.Active = false;
                    entry.Registration.Dispose();
                }

                CallbackRuntime.Invoke(
                    (Action<TData>)entry.Handler,
                    data,
                    entry.Options.DebugName ?? context,
                    entry.Registration.OwnerPluginGuid);
            }
        }
    }
}
