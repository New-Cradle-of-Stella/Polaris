using System;
using System.Collections.Generic;

namespace Polaris.PUI
{
    /// <summary>
    /// 不可变的图蓝图（节点、边、入口节点 key）；每次 <see cref="CreateSolution"/> 都产生独立的运行时实例。
    /// </summary>
    public sealed class PUIGraphDefinition
    {
        public string Name { get; }
        public string EntryNodeKey { get; }
        public IReadOnlyList<PUINodeDefinition> Nodes { get; }
        public IReadOnlyList<PUIEdge> Edges { get; }

        internal PUIGraphDefinition(string name, string entryNodeKey, List<PUINodeDefinition> nodes, List<PUIEdge> edges)
        {
            Name = name;
            EntryNodeKey = entryNodeKey;
            Nodes = nodes;
            Edges = edges;
        }

        public static PUIGraphDefinitionBuilder CreateBuilder(string name) => new PUIGraphDefinitionBuilder(name);

        /// <summary>
        /// 创建一份独立的运行时实例，各节点的 PUI 均为新建。
        /// </summary>
        public PUISolution CreateSolution(string instanceName = null)
        {
            Validate();
            return new PUISolution(instanceName ?? Name, this);
        }

        /// <summary>
        /// 校验节点 key 唯一、边引用有效、入口节点存在、PuiName 均可解析；失败则抛出异常。
        /// </summary>
        public void Validate()
        {
            var nodeKeys = new HashSet<string>();

            foreach (PUINodeDefinition node in Nodes)
            {
                if (!nodeKeys.Add(node.Key))
                {
                    throw new InvalidOperationException($"Duplicate node key in graph \"{Name}\": {node.Key}");
                }

                if (!PUIManager.IsKnownPuiName(node.PuiName))
                {
                    throw new InvalidOperationException(
                        $"Node \"{node.Key}\" of graph \"{Name}\" references an unknown PUI \"{node.PuiName}\": " +
                        "check that it is marked [PUIAutoRegistration] and that its assembly is loaded.");
                }
            }

            foreach (PUIEdge edge in Edges)
            {
                if (!nodeKeys.Contains(edge.SourceNodeKey))
                {
                    throw new InvalidOperationException($"An edge of graph \"{Name}\" references a source node that does not exist: {edge.SourceNodeKey}");
                }

                if (!edge.IsExit && !nodeKeys.Contains(edge.TargetNodeKey))
                {
                    throw new InvalidOperationException($"An edge of graph \"{Name}\" references a target node that does not exist: {edge.TargetNodeKey}");
                }
            }

            if (EntryNodeKey != null && !nodeKeys.Contains(EntryNodeKey))
            {
                throw new InvalidOperationException($"Entry node \"{EntryNodeKey}\" of graph \"{Name}\" does not exist.");
            }
        }
    }
}
