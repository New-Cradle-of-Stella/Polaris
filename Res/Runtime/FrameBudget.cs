using System.Diagnostics;

namespace Polaris.Res.Runtime
{
    /// <summary>
    /// 每帧时间预算。<see cref="ResPump"/> 每帧开始时调用 <see cref="Begin"/>，
    /// 在推进各个 <c>IResourceJob</c> 之间反复查询 <see cref="HasTimeLeft"/>，
    /// 超预算的任务会在 <c>Step()</c> 返回后自然留到下一帧继续——job 本身不知道
    /// "预算"这个概念，只是被 <see cref="ResPump"/> 少调用几次。
    /// 默认预算由 <c>ResSettings.FrameBudgetMilliseconds</c> 配置。
    /// </summary>
    internal sealed class FrameBudget
    {
        private readonly Stopwatch stopwatch = new Stopwatch();
        private double budgetMs;

        internal void Begin(double budgetMilliseconds)
        {
            budgetMs = budgetMilliseconds;
            stopwatch.Restart();
        }

        internal bool HasTimeLeft => stopwatch.Elapsed.TotalMilliseconds < budgetMs;

        internal double ElapsedMilliseconds => stopwatch.Elapsed.TotalMilliseconds;
    }
}
