using System;
using System.Collections.Generic;

namespace Polaris.PUI
{
    /// <summary>构造 <see cref="PUIGraphDefinition"/> 的 fluent builder；生成代码与手写代码共用。</summary>
    public sealed class PUIGraphDefinitionBuilder
    {
        private readonly string name;
        private readonly List<PUINodeDefinition> nodes = new List<PUINodeDefinition>();
        private readonly List<PUIEdge> edges = new List<PUIEdge>();
        private readonly HashSet<(string source, string trigger)> edgeKeys = new HashSet<(string, string)>();
        private string entryNodeKey;

        internal PUIGraphDefinitionBuilder(string name)
        {
            this.name = name;
        }

        public PUIGraphDefinitionBuilder Node(string key, string puiName)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Node key cannot be empty", nameof(key));
            }

            if (string.IsNullOrEmpty(puiName))
            {
                throw new ArgumentException("PUI name cannot be empty", nameof(puiName));
            }

            nodes.Add(new PUINodeDefinition(key, puiName));
            return this;
        }

        public PUIGraphDefinitionBuilder Entry(string nodeKey)
        {
            entryNodeKey = nodeKey;
            return this;
        }

        public PUIGraphDefinitionBuilder Edge(string sourceKey, string triggerKey, string targetKey, bool blocking)
        {
            if (string.IsNullOrEmpty(sourceKey))
            {
                throw new ArgumentException("Source node key cannot be empty", nameof(sourceKey));
            }

            if (string.IsNullOrEmpty(triggerKey))
            {
                throw new ArgumentException("Trigger key cannot be empty", nameof(triggerKey));
            }

            if (string.IsNullOrEmpty(targetKey))
            {
                throw new ArgumentException("Target node key cannot be empty", nameof(targetKey));
            }

            if (!edgeKeys.Add((sourceKey, triggerKey)))
            {
                throw new InvalidOperationException($"Duplicate edge in graph \"{name}\": ({sourceKey}, {triggerKey})");
            }

            edges.Add(new PUIEdge(sourceKey, triggerKey, targetKey, blocking));
            return this;
        }

        /// <summary>
        /// 一条退出边：sourceKey 上名为 triggerKey 的连接点触发时退出整个状态机
        /// （<see cref="PUISolution.Fire"/> 改为调用 <see cref="PUISolution.Stop"/>），而不是跳到某个节点。
        /// 对应 .puisln 里连到固定"出口"节点的连线。退出没有"目标节点"可显示，因此总是阻塞的。
        /// </summary>
        public PUIGraphDefinitionBuilder ExitEdge(string sourceKey, string triggerKey)
            => Edge(sourceKey, triggerKey, PUIEdge.ExitNodeKey, blocking: true);

        /// <summary>构造出的 <see cref="PUIGraphDefinition"/> 会在返回前跑一次 <see cref="PUIGraphDefinition.Validate"/>。</summary>
        public PUIGraphDefinition Build()
        {
            var definition = new PUIGraphDefinition(name, entryNodeKey, nodes, edges);
            definition.Validate();
            return definition;
        }
    }
}
