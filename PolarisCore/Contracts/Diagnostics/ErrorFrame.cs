namespace Polaris.Diagnostics
{
    /// <summary>一条已标注归属的堆栈帧，无论来源是异常对象还是纯文本堆栈都归一成这个形状。</summary>
    public sealed class ErrorFrame
    {
        internal ErrorFrame() { }

        /// <summary>声明该方法的类型全名。</summary>
        public string TypeName { get; internal set; }

        /// <summary>方法名。</summary>
        public string MethodName { get; internal set; }

        /// <summary>这一帧属于谁。</summary>
        public AssemblyOwner Owner { get; internal set; }

        /// <summary>这个方法是否被 Harmony 补丁改过（见 <see cref="PatchSuspects"/>）。</summary>
        public bool IsPatched { get; internal set; }

        /// <summary>补丁说明，如"被「WeNeedMoreNoels」以 transpiler 改写"；未改过为 null。</summary>
        public string PatchNote { get; internal set; }

        /// <summary>报告里的一行渲染。</summary>
        public string Describe()
        {
            string head = $"[{Owner?.KindLabel ?? "未知"}] {TypeName}.{MethodName}()";
            return PatchNote == null ? head : $"{head}   <- {PatchNote}";
        }

        public override string ToString() => Describe();
    }
}
