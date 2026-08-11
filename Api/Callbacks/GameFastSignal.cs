using System;
using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>
    /// 无参数、不入队、同步派发的信号，专供 <c>Updating</c>/<c>LateUpdating</c>/<c>FixedUpdating</c>
    /// 这类每帧必然触发一次的高频事件——它们没有"事件参数"可言，也不需要跨领域因果排序，
    /// 走 <see cref="GameSignal{T}"/> 的队列反而会在每帧产生一次不必要的分配。
    /// </summary>
    public sealed class GameFastSignal
    {
        sealed class Entry
        {
            internal Action Handler;
            internal GameCallbackOptions Options;
            internal GameSubscription Subscription;
            internal long RegistrationSeq;
            internal volatile bool Active = true;
        }

        readonly object gate = new();
        readonly string kindName;
        Entry[] snapshot = Array.Empty<Entry>();
        long registrationCounter;

        internal GameFastSignal(GameCallbackKind kind) => kindName = kind.ToString();

        public bool HasSubscribers => snapshot.Length > 0;

        public GameSubscription Subscribe(Action handler) => Subscribe(handler, GameCallbackOptions.Default);

        public GameSubscription Subscribe(Action handler, GameCallbackOptions options)
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

        /// <summary>由 <see cref="Plugin.Update"/>/<see cref="Plugin.LateUpdate"/>/<c>FixedUpdate</c> 直接调用。</summary>
        internal void Raise()
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
                    entry.Active = false;
                    entry.Subscription.MarkInactiveOnly();
                    Deactivate(entry);
                }

                CallbackRuntime.Invoke(entry.Handler, entry.Options.DebugName ?? kindName, entry.Subscription.OwnerPluginGuid);
            }
        }
    }
}
