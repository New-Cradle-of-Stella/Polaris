using System.Collections.Generic;
using Polaris.Event.Compiler.Parsing;

namespace Polaris.Event.Compiler.Lowering
{
    /// <summary>
    /// 收集一个文件内全部合法标签名（含 @if/@else 块内的），供 @goto 做正向引用检查——
    /// 标签作用域是"本文件"，不需要跨文件（PolarisEvent-实现计划.md §4.1 的“事件 ID”才是跨文件/跨模组的）。
    /// </summary>
    static class LabelCollector
    {
        public static HashSet<string> Collect(IReadOnlyList<HxxNode> nodes)
        {
            var labels = new HashSet<string>();
            Walk(nodes, labels);
            return labels;
        }

        static void Walk(IReadOnlyList<HxxNode> nodes, HashSet<string> labels)
        {
            foreach (var node in nodes)
            {
                if (node is LabelNode label)
                {
                    labels.Add(label.Name);
                }
                else if (node is IfNode ifNode)
                {
                    Walk(ifNode.ThenBody, labels);
                    if (ifNode.ElseBody != null)
                    {
                        Walk(ifNode.ElseBody, labels);
                    }
                }
            }
        }
    }
}
