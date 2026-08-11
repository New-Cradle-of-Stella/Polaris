namespace Polaris.API
{
    /// <summary>玩家对象进入可查询状态（引用 <c>null -&gt; PR</c>）。</summary>
    public sealed class PlayerAvailableEvent
    {
        public GameCallbackStamp Stamp { get; }
        public CharacterHandle Player { get; }

        internal PlayerAvailableEvent(GameCallbackStamp stamp, CharacterHandle player)
        {
            Stamp = stamp;
            Player = player;
        }
    }

    /// <summary>玩家对象离开或被重建（引用 <c>PR -&gt; null/other</c>）。</summary>
    public sealed class PlayerUnavailableEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal PlayerUnavailableEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }
}
