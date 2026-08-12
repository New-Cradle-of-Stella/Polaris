namespace Polaris.API
{
    /// <summary>
    /// 当前事件的记账。
    /// <para>
    /// 游戏的事件系统是一个 <c>EvReader</c> 栈，栈顶那一层的名字没有稳定的公开读取口，
    /// 所以这里不轮询，而是由 <c>EV.stack</c>/<c>EV.changeEvent</c>/<c>EV.evEnd</c>
    /// 三个补丁把"现在在演哪一个"报进来。补丁没能应用时，
    /// <see cref="Current"/> 恒为 <c>null</c>，而不是给出一个可能过期的答案。
    /// </para>
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

            // 用 Peek 而不是 Wrap：事件已经结束了，为一个没人拿过的 key 新建包装器毫无意义，
            // 而且新建出来的那个立刻就是失效状态。
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
