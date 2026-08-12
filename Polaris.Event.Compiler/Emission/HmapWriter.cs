using System.Collections.Generic;
using System.Text;
using Polaris.Event.Compiler.Lowering;

namespace Polaris.Event.Compiler.Emission
{
    /// <summary>
    /// 手写 JSON，不额外引入 System.Text.Json/Newtonsoft 依赖——结构就是一个扁平对象数组，
    /// 手写序列化/反序列化都不复杂，没必要为此换一个依赖。
    /// </summary>
    public static class HmapWriter
    {
        public static string Write(IReadOnlyList<LoweredLine> lines)
        {
            var sb = new StringBuilder();
            sb.Append("[\n");
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                sb.Append("  { \"cmdLine\": ").Append(i + 1)
                  .Append(", \"sourceFile\": \"").Append(Escape(line.SourceFilePath)).Append('"')
                  .Append(", \"sourceLine\": ").Append(line.SourceLine)
                  .Append(", \"sourceColumn\": ").Append(line.SourceColumn)
                  .Append(" }");
                sb.Append(i == lines.Count - 1 ? "\n" : ",\n");
            }

            sb.Append("]\n");
            return sb.ToString();
        }

        static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
