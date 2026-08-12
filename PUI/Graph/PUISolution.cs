using System;
using System.Collections.Generic;

namespace Polaris.PUI
{
    /// <summary>
    /// <see cref="PUIGraphDefinition"/> 的运行时实例，自行维护当前所在节点；不同实例彼此完全独立。
    /// </summary>
    public sealed class PUISolution
    {
        /// <summary>"取消/ESC"触发的保留 key，须与生成器侧字面一致。</summary>
        public const string CancelTriggerKey = "@Cancel";

        private sealed class Node
        {
            public readonly string Key;
            public readonly PUIRuntime Runtime;
            public readonly Dictionary<string, PUIEdge> Outgoing = new Dictionary<string, PUIEdge>();

            public Node(string key, PUIRuntime runtime)
            {
                Key = key;
                Runtime = runtime;
            }
        }

        private readonly Dictionary<string, Node> nodesByKey = new Dictionary<string, Node>();
        private readonly Dictionary<PUIRuntime, string> runtimeToKey = new Dictionary<PUIRuntime, string>();

        public string Name { get; }
        public PUIGraphDefinition Definition { get; }

        /// <summary>当前所在节点的 key；仅在 Start/Enter/阻塞式跳转时移动，非阻塞浮层不影响它。</summary>
        public string CurrentNodeKey { get; private set; }

        public PUIRuntime Current { get; private set; }

        /// <summary>(from, to) —— from 在 Stop() 时为当前节点、to 为 null。</summary>
        public event Action<string, string> NodeChanged;

        internal PUISolution(string name, PUIGraphDefinition definition)
        {
            Name = name;
            Definition = definition;

            foreach (PUINodeDefinition nodeDef in definition.Nodes)
            {
                PUIRuntime runtime = PUIManager.CreateInstance(nodeDef.PuiName);
                var node = new Node(nodeDef.Key, runtime);
                nodesByKey.Add(nodeDef.Key, node);
                runtimeToKey.Add(runtime, nodeDef.Key);
                runtime.Attach(this);
            }

            foreach (PUIEdge edge in definition.Edges)
            {
                nodesByKey[edge.SourceNodeKey].Outgoing[edge.TriggerKey] = edge;
            }

            PUISolutionPump.Attach(this);
        }

        public bool TryGetNode(string nodeKey, out PUIRuntime runtime)
        {
            if (nodeKey != null && nodesByKey.TryGetValue(nodeKey, out Node node))
            {
                runtime = node.Runtime;
                return true;
            }

            runtime = null;
            return false;
        }

        public IEnumerable<PUIEdge> GetOutgoingEdges(string nodeKey)
        {
            if (nodeKey != null && nodesByKey.TryGetValue(nodeKey, out Node node))
            {
                return node.Outgoing.Values;
            }

            return Array.Empty<PUIEdge>();
        }

        /// <summary>进入 <see cref="PUIGraphDefinition.EntryNodeKey"/>；未设置入口节点时抛出。</summary>
        public void Start()
        {
            if (string.IsNullOrEmpty(Definition.EntryNodeKey))
            {
                throw new InvalidOperationException($"Graph \"{Definition.Name}\" has no entry node set; cannot Start().");
            }

            Enter(Definition.EntryNodeKey);
        }

        /// <summary>隐藏当前节点（如果有），显示并聚焦 <paramref name="nodeKey"/>，使其成为当前节点。</summary>
        public void Enter(string nodeKey)
        {
            if (!nodesByKey.TryGetValue(nodeKey, out Node target))
            {
                throw new ArgumentException($"No such node in graph \"{Definition.Name}\": {nodeKey}", nameof(nodeKey));
            }

            Leave(Current);
            MakeCurrent(target);
        }

        /// <summary>走一条边；找不到匹配的边时安全返回 false，什么都不做。</summary>
        public bool Fire(string sourceNodeKey, string triggerKey)
        {
            if (string.IsNullOrEmpty(sourceNodeKey) || string.IsNullOrEmpty(triggerKey))
            {
                return false;
            }

            if (!nodesByKey.TryGetValue(sourceNodeKey, out Node source))
            {
                return false;
            }

            if (!source.Outgoing.TryGetValue(triggerKey, out PUIEdge edge))
            {
                return false;
            }

            if (edge.IsExit)
            {
                Stop();
                return true;
            }

            Node target = nodesByKey[edge.TargetNodeKey];

            if (edge.Blocking)
            {
                // 阻塞跳转从边的来源节点离开，它不一定是当前节点。
                Leave(source.Runtime);
                MakeCurrent(target);
            }
            else
            {
                target.Runtime.Show();
            }

            return true;
        }

        internal bool Fire(PUIRuntime source, string triggerKey)
        {
            return runtimeToKey.TryGetValue(source, out string key) && Fire(key, triggerKey);
        }

        /// <summary>隐藏所有已显示的节点；CurrentNodeKey 变为 null。不销毁任何节点的窗口。</summary>
        public void Stop()
        {
            string from = CurrentNodeKey;

            foreach (Node n in nodesByKey.Values)
            {
                if (n.Runtime.State == PUIState.Shown)
                {
                    n.Runtime.Hide();
                }

                if (n.Runtime.Controller == this)
                {
                    n.Runtime.Controller = null;
                }
            }

            CurrentNodeKey = null;
            Current = null;

            if (from != null)
            {
                NodeChanged?.Invoke(from, null);
            }
        }

        /// <summary>Stop() 并从每个节点解绑；不销毁任何节点的窗口。</summary>
        public void Dispose()
        {
            Stop();

            foreach (Node n in nodesByKey.Values)
            {
                n.Runtime.Detach(this);
            }

            PUISolutionPump.Detach(this);
        }

        internal void PollCancel()
        {
            foreach (Node n in nodesByKey.Values)
            {
                if (n.Runtime.State == PUIState.Shown && n.Runtime.IsFocused)
                {
                    Fire(n.Key, CancelTriggerKey);
                }
            }
        }

        /// <summary>隐藏一个节点并放弃对它的控制权；<paramref name="runtime"/> 为 null 时什么都不做。</summary>
        private void Leave(PUIRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            runtime.Hide();
            if (runtime.Controller == this)
            {
                runtime.Controller = null;
            }
        }

        /// <summary>显示并聚焦一个节点，接管它的控制权，把它记为当前节点，并广播一次 <see cref="NodeChanged"/>。</summary>
        private void MakeCurrent(Node target)
        {
            string from = CurrentNodeKey;

            target.Runtime.Show();
            target.Runtime.Focus();
            target.Runtime.Controller = this;

            CurrentNodeKey = target.Key;
            Current = target.Runtime;
            NodeChanged?.Invoke(from, target.Key);
        }
    }
}
