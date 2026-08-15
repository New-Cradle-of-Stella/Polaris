using System;
using evt;

namespace Polaris.API
{
    /// <summary>
    /// 一次正在执行的游戏事件（剧情演出）。事件系统是<b>栈</b>式的，实例代表某 key 的事件
    /// 在栈上的这一次存在，<see cref="GameInstance.IsValid"/> 问的是"还在栈顶吗"。
    /// </summary>
    public sealed class GameEvent : GameInstance
    {
        static readonly InstanceTable<string, GameEvent> Table = new();

        readonly string key;

        GameEvent(string key)
        {
            this.key = key;
        }

        internal static GameEvent Wrap(string eventKey)
            => string.IsNullOrEmpty(eventKey) ? null : Table.Get(string.Intern(eventKey), static k => new GameEvent(k));

        internal static GameEvent Peek(string eventKey)
            => string.IsNullOrEmpty(eventKey) ? null : Table.Peek(string.Intern(eventKey));

        internal static void SweepEvents() => Table.Sweep();

        internal static void InvalidateAllEvents() => Table.InvalidateAll();

        private protected override bool IsNativeAlive
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                {
                    return false;
                }

                try
                {
                    // 只有栈顶才算"这一次执行"，被压住的那一层不接受控制操作。
                    return EV.isActive(key, true);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private protected override string Describe() => $"GameEvent({key})";

        /// <summary>获取该事件的键名。</summary>
        public string Key => key;

        /// <summary>停止该事件实例；<paramref name="immediate"/> 为真时连同下方整个事件栈一起收掉，否则只结束当前层。</summary>
        public void Stop(bool immediate = false)
        {
            EnsureUsable();
            Act("Stop", () => EV.evEnd(immediate));
        }

        /// <summary>获取该事件中的指定文本内容；没有这一项时返回 <c>null</c>。</summary>
        public string GetContent(string contentKey)
        {
            if (string.IsNullOrEmpty(contentKey))
            {
                return null;
            }

            // 直接读内容表，而不是 EV.getEventContent——它只返回"查到没有"，值本身拿不到。
            return Read(
                () => EV.Oevt_content != null && EV.Oevt_content.TryGetValue(contentKey, out string value) ? value : null,
                null);
        }

        /// <summary>设置该事件中的指定文本内容。</summary>
        public void SetContent(string contentKey, string value)
        {
            EnsureUsable();

            if (!string.IsNullOrEmpty(contentKey))
            {
                Act("SetContent", () => EV.setEventContent(contentKey, value));
            }
        }

        /// <summary>判断该事件的消息框当前是否可见。</summary>
        public bool IsMessageVisible => Read(static () => EV.msg_active, false);

        /// <summary>判断该事件消息是否正在等待玩家继续。</summary>
        public bool IsMessageWaiting() => Read(static () => EV.msg_active && EV.canProgress(), false);

        /// <summary>判断该事件当前是否允许继续推进。</summary>
        public bool CanProgress() => Read(static () => EV.canProgress(), false);

        /// <summary>
        /// 获取或设置该事件的跳过模式。0 表示不跳过，其余取值对应游戏自己的跳过档位
        /// （见 <c>EV.SKIP_ESC</c>/<c>EV.SKIP_X</c>）。
        /// </summary>
        public int SkipMode
        {
            get => Read(static () => EV.skipping, 0);
            set
            {
                EnsureUsable();
                Act("SkipMode", () => EV.skipping = value);
            }
        }

        /// <summary>获取或设置该事件是否禁止跳过。</summary>
        public bool IsSkipDenied
        {
            get => Read(static () => EV.deny_skip, false);
            set
            {
                EnsureUsable();
                Act("IsSkipDenied", () => EV.deny_skip = value);
            }
        }

        // ── 内部工具 ──────────────────
        // 事件状态挂在静态的 EV 上而非某个实例对象，故这两个包装不取 native 引用，
        // 只负责"失效就给回退值"与"异常就上报"。

        /// <summary>只读访问的统一包装：实例已失效或读取抛异常时给回退值，不把异常丢给调用方。</summary>
        TValue Read<TValue>(Func<TValue> read, TValue fallback)
        {
            if (!IsValid)
            {
                return fallback;
            }

            try
            {
                return read();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>写操作的统一包装：调用方已经过 <see cref="GameInstance.EnsureUsable"/>。</summary>
        static void Act(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, $"GameEvent.{what}");
            }
        }
    }
}
