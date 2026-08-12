using System.Collections.Generic;

namespace Polaris.Event.Compiler.Text
{
    /// <summary>
    /// 一份待编译的文本（<c>.phxx</c> 或别名 <c>.yaml</c>），带上它的路径以便诊断定位。
    /// <para>
    /// <see cref="Path"/> 约定为"项目内的逻辑相对路径"（正斜杠、无前导斜杠），而不是磁盘绝对路径——
    /// <see cref="HppCompiler"/> 会直接拿它（去掉 <c>.phxx</c> 后缀）当作事件的逻辑 ID，调用方
    /// （构建期是 PolarisTools 的 <c>PolarisEventGenerator</c> 单文件生成器，编辑器实时诊断是
    /// <c>HppDiagnosticsService</c>）负责把磁盘路径换算成这个相对路径。
    /// </para>
    /// </summary>
    public sealed class SourceText
    {
        public string Path { get; }
        public string Content { get; }
        public IReadOnlyList<string> Lines { get; }

        public SourceText(string path, string content)
        {
            Path = path ?? "<unknown>";
            Content = content ?? string.Empty;
            Lines = SplitLines(Content);
        }

        static IReadOnlyList<string> SplitLines(string content)
        {
            // 手写切分而不是 string.Split(Environment.NewLine)：.phxx 可能混用 \n / \r\n，
            // 编译器在多个平台上跑（游戏侧 netstandard2.1、VSIX 侧 net472），换行统一交给这里处理，
            // 不依赖运行机器的 Environment.NewLine。
            var lines = new List<string>();
            int start = 0;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '\n' || c == '\r')
                {
                    lines.Add(content.Substring(start, i - start));
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }

                    start = i + 1;
                }
            }

            lines.Add(content.Substring(start));
            return lines;
        }
    }
}
