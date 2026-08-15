using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 致命错误：模组环境本身已坏、不该继续这一局，由发现它的模块直接点名责任方并拦住标题画面。
    /// 判据是"继续会得到错误结果"，单个功能坏掉应走 <see cref="Infra.ErrorsAPI.Report(System.Exception, string)"/>。
    /// </summary>
    public sealed class FatalError
    {
        /// <param name="source">报出这条致命错误的模块名，原样显示在日志、报告和告知页上。</param>
        /// <param name="reason">一句话说清为什么这一局不能继续，给玩家看。</param>
        public FatalError(string source, FatalText reason)
        {
            Source = string.IsNullOrEmpty(source) ? "unknown module" : source;
            Reason = reason;
        }

        /// <summary>报出这条致命错误的模块名。</summary>
        public string Source { get; }

        /// <summary>一句话原因，给玩家看。</summary>
        public FatalText Reason { get; }

        /// <summary>玩家该怎么办；留空则用通用文案，调用方知道具体做法时应填写。</summary>
        public FatalText Action { get; set; }

        /// <summary>逐条明细，按重要程度排序，内容保持语言中性（key 名、dll 名、数值）以免跨语言失真。</summary>
        public List<string> Details { get; } = new();

        /// <summary>责任方所在的程序集，可以有多个；用于在报告里查出模组名、作者与主页。</summary>
        public List<Assembly> Culprits { get; } = new();
    }
}
