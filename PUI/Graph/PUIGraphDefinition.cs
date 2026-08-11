using System;
using System.Collections.Generic;

namespace Polaris.PUI
{
    /// <summary>
    /// 一份不可变的图蓝图：节点（key + PUI 名）、边（source, trigger, target, blocking）、
    /// 入口节点 key。一份 .puisln 编译对应一份 Definition；多次 <see cref="CreateSolution"/>
    /// 会各自得到完全独立的运行时实例（各自的 PUI 副本、各自的当前节点）——这就是"真正的多实例"。
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
        /// 创建一份完全独立的运行时实例：每个节点都通过 <see cref="PUIManager"/> 的类型目录
        /// 新建一份对应的 <see cref="IPUI"/>，再用 <see cref="PUIRuntime.Create"/> 包一层。
        /// 多次调用互不干扰，各自拥有独立的 GameObject 与当前节点状态。
        /// </summary>
        public PUISolution CreateSolution(string instanceName = null)
        {
            Validate();
            return new PUISolution(instanceName ?? Name, this);
        }

        /// <summary>
        /// 校验：节点 key 唯一、所有边两端引用的节点存在、入口节点存在、每个节点的 PuiName 能在
        /// <see cref="PUIManager"/> 的类型目录里解析到。失败即抛出，供 <see cref="CreateSolution"/>
        /// 与生成代码的静态初始化尽早发现配置错误。
        /// </summary>
        public void Validate()
        {
            var nodeKeys = new HashSet<string>();

            foreach (PUINodeDefinition node in Nodes)
            {
                if (!nodeKeys.Add(node.Key))
                {
                    throw new InvalidOperationException($"图「{Name}」里重复的节点 key：{node.Key}");
                }

                if (!PUIManager.IsKnownPuiName(node.PuiName))
                {
                    throw new InvalidOperationException(
                        $"图「{Name}」的节点「{node.Key}」引用了未知的 PUI「{node.PuiName}」：" +
                        "请确认它标了 [PUIAutoRegistration] 且所在程序集已加载。");
                }
            }

            foreach (PUIEdge edge in Edges)
            {
                if (!nodeKeys.Contains(edge.SourceNodeKey))
                {
                    throw new InvalidOperationException($"图「{Name}」的边引用了不存在的源节点：{edge.SourceNodeKey}");
                }

                if (!edge.IsExit && !nodeKeys.Contains(edge.TargetNodeKey))
                {
                    throw new InvalidOperationException($"图「{Name}」的边引用了不存在的目标节点：{edge.TargetNodeKey}");
                }
            }

            if (EntryNodeKey != null && !nodeKeys.Contains(EntryNodeKey))
            {
                throw new InvalidOperationException($"图「{Name}」的入口节点「{EntryNodeKey}」不存在。");
            }
        }
    }
}
