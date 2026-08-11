namespace Polaris.API
{
    public sealed class GameMenuOpeningEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal GameMenuOpeningEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    public sealed class GameMenuOpenedEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal GameMenuOpenedEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    public sealed class GameMenuClosingEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal GameMenuClosingEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    public sealed class GameMenuClosedEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal GameMenuClosedEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }
}
