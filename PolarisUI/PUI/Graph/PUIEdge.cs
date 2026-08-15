namespace Polaris.PUI
{
    /// <summary>
    /// <see cref="PUIGraphDefinition"/> 中的一条边：某节点的某个触发键指向另一节点；<see cref="Blocking"/> 决定是切换当前节点还是仅显示浮层。
    /// </summary>
    public readonly struct PUIEdge
    {
        /// <summary>保留的目标 key，表示退出整个状态机（<see cref="PUISolution.Fire"/> 命中时改为调用 <see cref="PUISolution.Stop"/>）。</summary>
        public const string ExitNodeKey = "@Exit";

        public string SourceNodeKey { get; }
        public string TriggerKey { get; }
        public string TargetNodeKey { get; }
        public bool Blocking { get; }

        /// <summary>true 表示 <see cref="TargetNodeKey"/> 是 <see cref="ExitNodeKey"/>，这条边代表退出整个状态机。</summary>
        public bool IsExit => TargetNodeKey == ExitNodeKey;

        public PUIEdge(string sourceNodeKey, string triggerKey, string targetNodeKey, bool blocking)
        {
            SourceNodeKey = sourceNodeKey;
            TriggerKey = triggerKey;
            TargetNodeKey = targetNodeKey;
            Blocking = blocking;
        }
    }
}
