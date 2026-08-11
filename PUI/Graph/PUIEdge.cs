namespace Polaris.PUI
{
    /// <summary>
    /// 一份 <see cref="PUIGraphDefinition"/> 里的一条边：从某节点的某个触发键，指向另一个节点，
    /// 以及这次跳转是否阻塞（阻塞 = 迁移“当前节点”并隐藏来源节点；非阻塞 = 仅显示目标节点，
    /// 当前节点不变，对应今天 .puisln 里的“浮层”语义）。
    /// </summary>
    public readonly struct PUIEdge
    {
        /// <summary>
        /// 保留的目标节点 key：表示这条边不指向图里的任何节点，而是退出整个状态机
        /// （<see cref="PUISolution.Fire"/> 命中时改为调用 <see cref="PUISolution.Stop"/>）。
        /// 对应 .puisln 里连到固定"出口"节点的连线，见 <see cref="PUIGraphDefinitionBuilder.ExitEdge"/>。
        /// </summary>
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
