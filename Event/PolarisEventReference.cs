using Polaris.API;

namespace Polaris.Event
{
    /// <summary>强类型事件引用，对应 <c>GeneratedEvents.MuseumEntrance</c> 这类生成代码的调用点。</summary>
    public sealed class PolarisEventReference
    {
        public string Namespace { get; }
        public string LogicalId { get; }

        public PolarisEventReference(string @namespace, string logicalId)
        {
            Namespace = @namespace;
            LogicalId = logicalId;
        }

        public GameEvent Start(params string[] args) => PolarisEvent.StartByKey(Namespace, LogicalId, args);

        public GameEvent Change(params string[] args) => PolarisEvent.ChangeByKey(Namespace, LogicalId, args);
    }
}
