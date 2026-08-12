using System.Collections.Generic;
using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler
{
    public sealed class HppProject
    {
        /// <summary>运行时事件命名空间（如 "com.example.mymod"），必填，见实现计划 §4.1。</summary>
        public string Namespace { get; set; }

        /// <summary>宿主 C# 项目的 RootNamespace，用于生成代码所在的 C# 命名空间；可为空。</summary>
        public string RootNamespace { get; set; }

        public string TargetVersion { get; set; } = "0.29j";

        /// <summary>strict 模式下 @raw 会额外报 HPP9001 警告，见 CSV "严格模式产生 HPP9001"。</summary>
        public bool StrictRaw { get; set; }

        /// <summary><c>.phxx</c> 源文件，<see cref="SourceText.Path"/> 必须已经是项目内的逻辑相对路径。</summary>
        public IReadOnlyList<SourceText> Files { get; set; } = new List<SourceText>();

        /// <summary>单一项目级别名文件；为空则视为没有任何别名（几乎所有语句都会解析失败）。</summary>
        public SourceText AliasFile { get; set; }
    }
}
