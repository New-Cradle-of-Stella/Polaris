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

            try
            {
                EV.evEnd(immediate);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameEvent.Stop");
            }
        }

        /// <summary>获取该事件中的指定文本内容；没有这一项时返回 <c>null</c>。</summary>
        public string GetContent(string contentKey)
        {
            if (!IsValid || string.IsNullOrEmpty(contentKey))
            {
                return null;
            }

            try
            {
                // 直接读内容表，而不是 EV.getEventContent——它只返回"查到没有"，值本身拿不到。
                return EV.Oevt_content != null && EV.Oevt_content.TryGetValue(contentKey, out string value)
                    ? value
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>设置该事件中的指定文本内容。</summary>
        public void SetContent(string contentKey, string value)
        {
            EnsureUsable();

            if (string.IsNullOrEmpty(contentKey))
            {
                return;
            }

            try
            {
                EV.setEventContent(contentKey, value);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameEvent.SetContent");
            }
        }

        /// <summary>判断该事件的消息框当前是否可见。</summary>
        public bool IsMessageVisible
        {
            get
            {
                if (!IsValid)
                {
                    return false;
                }

                try
                {
                    return EV.msg_active;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>判断该事件消息是否正在等待玩家继续。</summary>
        public bool IsMessageWaiting()
        {
            if (!IsValid)
            {
                return false;
            }

            try
            {
                return EV.msg_active && EV.canProgress();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>判断该事件当前是否允许继续推进。</summary>
        public bool CanProgress()
        {
            if (!IsValid)
            {
                return false;
            }

            try
            {
                return EV.canProgress();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 获取或设置该事件的跳过模式。0 表示不跳过，其余取值对应游戏自己的跳过档位
        /// （见 <c>EV.SKIP_ESC</c>/<c>EV.SKIP_X</c>）。
        /// </summary>
        public int SkipMode
        {
            get
            {
                if (!IsValid)
                {
                    return 0;
                }

                try
                {
                    return EV.skipping;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            set
            {
                EnsureUsable();

                try
                {
                    EV.skipping = value;
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "GameEvent.SkipMode");
                }
            }
        }

        /// <summary>获取或设置该事件是否禁止跳过。</summary>
        public bool IsSkipDenied
        {
            get
            {
                if (!IsValid)
                {
                    return false;
                }

                try
                {
                    return EV.deny_skip;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            set
            {
                EnsureUsable();

                try
                {
                    EV.deny_skip = value;
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "GameEvent.IsSkipDenied");
                }
            }
        }
    }
}
