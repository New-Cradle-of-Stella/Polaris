using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一次<b>致命错误</b>的描述：模组环境里出现了一个继续跑下去只会越错越远的问题，
    /// 由发现它的模块构造这个对象交给 <see cref="Infra.ErrorsAPI.Fatal"/>。
    /// <para>
    /// 和普通的 <see cref="ErrorIncident"/> 区别在于因果方向：普通错误是"已经出事了，记一笔、
    /// 告诉玩家该找谁"，致命错误是"环境本身是坏的，这一局不该继续"——所以它不走归因推断
    /// （调用方本来就知道是谁的问题），而是直接点名责任方，并在标题画面拦住玩家、请他退出。
    /// </para>
    /// <para>
    /// 判据是"继续玩下去会得到错误的结果或更难排查的问题"，不是"有个功能坏了"。单个功能坏掉
    /// 应该走 <see cref="Infra.ErrorsAPI.Report(System.Exception, string)"/> 并让游戏照常运行——
    /// 把玩家赶出游戏是很重的处置，Polaris 系列自己也只在极少数情况下这么做。
    /// </para>
    /// </summary>
    public sealed class FatalError
    {
        /// <param name="source">
        /// 报出这条致命错误的模块名，例如 <c>"PolarisLang"</c>。会原样出现在日志、报告和
        /// 告知页上——玩家看到"是谁在拦我"，作者看到"该去问谁"。
        /// </param>
        /// <param name="reason">一句话说清为什么这一局不能继续，给玩家看。</param>
        public FatalError(string source, FatalText reason)
        {
            Source = string.IsNullOrEmpty(source) ? "未知模块" : source;
            Reason = reason;
        }

        /// <summary>报出这条致命错误的模块名。</summary>
        public string Source { get; }

        /// <summary>一句话原因，给玩家看。</summary>
        public FatalText Reason { get; }

        /// <summary>
        /// 玩家该怎么办。留空时报告与告知页给一段通用文案（"关掉相关模组再启动"）。
        /// 调用方知道更具体的做法时一定要填——通用文案对"到底关哪个"帮不上忙。
        /// </summary>
        public FatalText Action { get; set; }

        /// <summary>
        /// 逐条明细，按重要程度排序。<b>刻意是语言中性的</b>：这里该放的是 key 名、dll 文件名、
        /// 数值这类不需要翻译、也不应该被翻译的东西（"<c>mymod.ok</c>：A.dll ↔ B.dll"），
        /// 中英日玩家看到的都是同一份，交给作者时也不会因为语言不同而对不上。
        /// <para>
        /// 告知页只列前几条、其余归到"另有 N 条，见报告"；报告文件里一条不少。
        /// </para>
        /// </summary>
        public List<string> Details { get; } = new();

        /// <summary>
        /// 责任方所在的程序集，可以有多个（"两个模组撞了同一个 key"这类问题本来就没有单一责任人）。
        /// Polaris 会用它们查出模组名、作者与主页，写进报告的"该找谁"那一段。
        /// </summary>
        public List<Assembly> Culprits { get; } = new();
    }
}
