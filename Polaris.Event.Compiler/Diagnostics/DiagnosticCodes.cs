namespace Polaris.Event.Compiler.Diagnostics
{
    /// <summary>
    /// 诊断代码分区沿用 PolarisEvent-实现计划.md §9：HPP1xxx 语法/缩进、HPP2xxx 别名与拼写建议、
    /// HPP3xxx 控制流、HPP9xxx 兼容性。这里只列本轮冻结命令集实际会触发的代码，不预先铺满全表。
    /// </summary>
    public static class DiagnosticCodes
    {
        // HPP1xxx：语法和缩进
        public const string UnexpectedIndent = "HPP1001";
        public const string MalformedCommandLine = "HPP1002";
        public const string DanglingElse = "HPP1004";
        public const string AliasParseFailure = "HPP1006";

        // HPP2xxx：别名、参数和拼写建议
        public const string UnknownCommand = "HPP2001";
        public const string UnknownActor = "HPP2101";
        public const string UnknownPose = "HPP2103";
        public const string UnknownPosition = "HPP2201";
        public const string UnknownSfxAlias = "HPP2202";
        public const string UnknownEventAlias = "HPP2203";
        public const string MissingMainArgument = "HPP2301";
        public const string UnknownNamedArgument = "HPP2302";

        // HPP3xxx：控制流和等待
        public const string UnknownLabel = "HPP3001";
        public const string DuplicateLabel = "HPP3002";
        public const string UnreachableCode = "HPP3003";
        public const string MissingReturn = "HPP3004";
        public const string InvalidWaitFrames = "HPP3101";

        // HPP9xxx：@raw 与兼容性
        public const string RawInStrictMode = "HPP9001";

        // HPP9xxx：宿主环境（编辑器/构建）内部故障，不是脚本本身的问题
        public const string InternalError = "HPP9999";
    }
}
