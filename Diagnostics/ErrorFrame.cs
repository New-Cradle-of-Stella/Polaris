namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一条已经标注好归属的堆栈帧。报告文件里每一行堆栈前面的方括号标签就是它渲染出来的。
    /// <para>
    /// 无论异常是带着 <c>Exception</c> 对象来的（能拿到 <c>MethodBase</c>）还是只带着
    /// Unity 给的字符串堆栈来的，最终都归一成这个形状，下游不必再关心来源。
    /// </para>
    /// </summary>
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

        /// <summary>
        /// 补丁说明，例如 <c>被「WeNeedMoreNoels」以 transpiler 改写</c>。没被改过时为 null。
        /// 这一行是 transpiler 类问题唯一的可见线索——IL 改写不留堆栈帧，不写在这里就彻底没人知道。
        /// </summary>
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
