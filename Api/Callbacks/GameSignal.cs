using System;
using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>
    /// 一种可订阅、不可由调用方直接触发的事件。内部用 copy-on-write 快照，派发路径永远不走
    /// <c>Delegate.GetInvocationList()</c>，也不在派发中途反映刚发生的增删——那些从下一次事件才生效。
    /// </summary>
    public sealed class GameSignal<TEvent> where TEvent : class
    {
        sealed class Entry
        {
            internal Action<TEvent> Handler;
            internal GameCallbackOptions Options;
            internal GameSubscription Subscription;
            internal long RegistrationSeq;
            internal volatile bool Active = true;
        }

        readonly object gate = new();
        readonly string kindName;
        Entry[] snapshot = Array.Empty<Entry>();
        long registrationCounter;

        internal GameSignal(GameCallbackKind kind)
        {
            Kind = kind;
            kindName = kind.ToString();
        }

        internal GameCallbackKind Kind { get; }

        /// <summary>是否存在任何仍活跃的订阅者。调用方在构造事件参数前应该先查这个，零订阅时不分配。</summary>
        public bool HasSubscribers => snapshot.Length > 0;

        public GameSubscription Subscribe(Action<TEvent> handler) => Subscribe(handler, GameCallbackOptions.Default);

        public GameSubscription Subscribe(Action<TEvent> handler, GameCallbackOptions options)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            options ??= GameCallbackOptions.Default;

            string ownerGuid = CallbackOwnerResolver.ResolveGuid(handler.Method);
            var entry = new Entry { Handler = handler, Options = options };
            var subscription = new GameSubscription(() => Deactivate(entry), ownerGuid, options.DebugName);
            entry.Subscription = subscription;

            lock (gate)
            {
                entry.RegistrationSeq = ++registrationCounter;
                var next = new Entry[snapshot.Length + 1];
                Array.Copy(snapshot, next, snapshot.Length);
                next[snapshot.Length] = entry;
                Array.Sort(next, CompareEntries);
                snapshot = next;
            }

            return subscription;
        }

        static int CompareEntries(Entry a, Entry b)
        {
            int byPriority = a.Options.Priority.CompareTo(b.Options.Priority);
            return byPriority != 0 ? byPriority : a.RegistrationSeq.CompareTo(b.RegistrationSeq);
        }

        void Deactivate(Entry entry)
        {
            lock (gate)
            {
                int index = Array.IndexOf(snapshot, entry);
                if (index < 0)
                {
                    return;
                }

                var next = new Entry[snapshot.Length - 1];
                for (int i = 0, w = 0; i < snapshot.Length; i++)
                {
                    if (i != index)
                    {
                        next[w++] = snapshot[i];
                    }
                }

                snapshot = next;
            }
        }

        /// <summary>
        /// 由 <see cref="CallbackRuntime.Drain"/> 在主线程上调用。取一份局部引用后再遍历：
        /// 派发过程中新增的订阅者不会在本轮被调用，派发过程中的 Dispose 只是让它在下一轮消失。
        /// </summary>
        internal void RaiseNow(TEvent evt)
        {
            Entry[] current = snapshot;
            for (int i = 0; i < current.Length; i++)
            {
                Entry entry = current[i];
                if (!entry.Active)
                {
                    continue;
                }

                if (entry.Options.Once)
                {
                    // 调用前先标记失效：回调内部触发同类事件也只入队，不会同步递归到这里，
                    // 但仍要在调用前完成标记，防止极端情况下的重入导致同一次事件被算两次。
                    entry.Active = false;
                    entry.Subscription.MarkInactiveOnly();
                    Deactivate(entry);
                }

                CallbackRuntime.Invoke(entry.Handler, evt, entry.Options.DebugName ?? kindName, entry.Subscription.OwnerPluginGuid);
            }
        }

        /// <summary>入队一次派发；调用方已经确认 <see cref="HasSubscribers"/> 且构造好了事件参数。</summary>
        internal void Publish(TEvent evt) => CallbackRuntime.Enqueue(() => RaiseNow(evt));
    }
}
