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
                throw new ArgumentException("节点 key 不能为空", nameof(key));
            }

            if (string.IsNullOrEmpty(puiName))
            {
                throw new ArgumentException("PUI 名称不能为空", nameof(puiName));
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
                throw new ArgumentException("源节点 key 不能为空", nameof(sourceKey));
            }

            if (string.IsNullOrEmpty(triggerKey))
            {
                throw new ArgumentException("触发键不能为空", nameof(triggerKey));
            }

            if (string.IsNullOrEmpty(targetKey))
            {
                throw new ArgumentException("目标节点 key 不能为空", nameof(targetKey));
            }

            if (!edgeKeys.Add((sourceKey, triggerKey)))
            {
                throw new InvalidOperationException($"图「{name}」里重复的边：({sourceKey}, {triggerKey})");
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
