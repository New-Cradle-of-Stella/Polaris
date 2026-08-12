using System;
using System.Collections.Generic;
using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>
    /// v2 回调的进程唯一派发核心：静态回调按种类分组，实例回调按"种类 + 实例编号"分组。
    /// <para>
    /// 派发不在事件发生的那一刻同步进行，而是入队交给
    /// <see cref="CallbackRuntime"/>，由 <see cref="Plugin.Update"/>/<see cref="Plugin.LateUpdate"/>
    /// 统一清空。这样做的原因和 v1 一样：Harmony 补丁是在原版流程<b>中间</b>被调用的，
    /// 在那里直接执行下游回调，等于让任意模组的代码插进原版函数的执行途中。
    /// </para>
    /// <para>
    /// 每个订阅列表都是 copy-on-write 的数组：派发路径只读一次字段引用，
    /// 回调内部的增删要到下一次事件才可见，因此不会出现"边遍历边改集合"。
    /// </para>
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

            // 已经失效的实例上注册：立刻把注册句柄标成非活跃，调用方一问 IsActive 就知道，
            // 而不是留一个永远不会触发、看起来却很正常的注册。
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

            // 稳定排序：优先级相同的按注册先后执行，这是下游能依赖的唯一顺序保证。
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

            // 在锁外改标志：Registration.MarkInactiveOnly 不会回头调用移除逻辑，
            // 但调用方可能在自己的 Dispose 里做别的事，不该把它们圈在本模块的锁里。
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

        /// <summary>
        /// 发布一条静态回调。<paramref name="factory"/> 只在确实有订阅者时才被调用，
        /// 因此"每帧探测但通常没人听"的差分事件不会白白构造负荷对象。
        /// </summary>
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

            TData data;
            try
            {
                data = factory();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, $"Building callback payload for {kind}", typeof(GameCallbackHub).Assembly);
                return;
            }

            if (data == null)
            {
                return;
            }

            string context = kind.ToString();
            CallbackRuntime.Enqueue(() => Dispatch(current, data, context));
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

            TData data;
            try
            {
                data = factory();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, $"Building callback payload for {kind}", typeof(GameCallbackHub).Assembly);
                return;
            }

            if (data == null)
            {
                return;
            }

            string context = kind.ToString();
            CallbackRuntime.Enqueue(() => Dispatch(current, data, context));
        }

        /// <summary>
        /// 真正调用订阅者。取的是发布那一刻的数组快照：这一轮之后新增的订阅者收不到本次事件，
        /// 这一轮之内被 Dispose 的订阅者也不会再被调到（靠 <see cref="Entry.Active"/> 挡住）。
        /// </summary>
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
                    // 调用前就标记失效：回调内部即使触发同类事件，也只会入队而不会同步递归回来，
                    // 但仍然要在调用前完成标记，堵住任何重入路径导致同一次事件执行两遍。
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
