using System.Diagnostics;

namespace Polaris.Res.Runtime
{
    /// <summary>每帧时间预算，<see cref="ResPump"/> 用它节流每帧执行的工作量。</summary>
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
