using System.Collections.Generic;
using System.Text;
using Polaris.Event.Compiler.Lowering;

namespace Polaris.Event.Compiler.Emission
{
    public static class CmdEmitter
    {
        public static string Emit(IReadOnlyList<LoweredLine> lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                sb.Append(line.Text);
                sb.Append('\n');
            }

            return sb.ToString();
        }
    }
}
