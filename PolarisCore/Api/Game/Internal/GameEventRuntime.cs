namespace Polaris.API
{
    /// <summary>
    /// 当前事件的记账；由 EV 补丁推送状态而非轮询，补丁未生效时 <see cref="Current"/> 恒为 null。
    /// </summary>
    internal static class GameEventRuntime
    {
        static string currentKey;

        /// <summary>当前正在执行的事件实例；没有事件在跑时为 <c>null</c>。</summary>
        internal static GameEvent Current
        {
            get
            {
                if (string.IsNullOrEmpty(currentKey))
                {
                    return null;
                }

                GameEvent current = GameEvent.Wrap(currentKey);
                return current != null && current.IsValid ? current : null;
            }
        }

        /// <summary>由事件补丁调用：一个事件被压栈或切换过来了。</summary>
        internal static void OnOpened(string eventKey)
        {
            if (string.IsNullOrEmpty(eventKey))
            {
                return;
            }

            currentKey = eventKey;

            GameEvent opened = GameEvent.Wrap(eventKey);
            if (opened == null)
            {
                return;
            }

            GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.EventOpened, () => new EventOpenedCallbackData(opened));
        }

        /// <summary>由事件补丁调用：当前事件结束了。</summary>
        internal static void OnClosed(bool completed)
        {
            string key = currentKey;
            currentKey = null;

            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            // 用 Peek 而非 Wrap：事件已结束，没必要为其新建一个立刻失效的包装器。
            GameEvent closed = GameEvent.Peek(key);
            if (closed == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.EventClosed, closed, () => new EventClosedCallbackData(closed, completed));

            closed.Invalidate();
        }

        /// <summary>世界卸载时清账。</summary>
        internal static void Reset()
        {
            currentKey = null;
            GameEvent.InvalidateAllEvents();
        }
    }
}
