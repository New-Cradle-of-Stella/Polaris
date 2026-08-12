using System;
using evt;

namespace Polaris.API
{
    /// <summary>
    /// 一次正在执行的游戏事件（剧情演出）。入口是 <c>PolarisAPI.Game.Events</c> 与
    /// <see cref="GameStaticCallbackKind.EventOpened"/> 回调。
    /// <para>
    /// 游戏的事件系统是一个<b>栈</b>：事件里可以再压入事件。这里的实例代表"某个 key 的事件
    /// 这一次在栈上的存在"，因此 <see cref="GameInstance.IsValid"/> 问的是"它还在栈顶吗"，
    /// 而不是"事件系统还在跑吗"。
    /// </para>
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
                    // check_only_front: true——只有在栈顶才算"这一次执行"，
                    // 被别的事件压住的那一层不接受控制操作。
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

        /// <summary>
        /// 停止该事件实例。<paramref name="immediate"/> 为真时连同压在它下面的整个事件栈一起收掉，
        /// 否则只结束当前这一层。
        /// </summary>
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
                // 直接读事件系统的内容表，而不是 EV.getEventContent——后者返回的是"查到没有"，
                // 内容被写进它收到的那个 EvReader 里，拿不到值本身。
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
