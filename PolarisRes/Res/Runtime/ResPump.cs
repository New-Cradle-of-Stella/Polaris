using System;
using UnityEngine;

namespace Polaris.Res.Runtime
{
    /// <summary>
    /// PolarisRes 唯一的每帧泵，挂在 <see cref="ResHost"/> 的常驻 GameObject 上。
    /// 不用游戏的 <c>LoadTicketManager</c> 或各自建协程（两者都有诊断/预算方面的缺陷），改用显式 <see cref="Tick"/> 事件，各子系统订阅并共享同一个 <see cref="FrameBudget"/>。
    /// </summary>
    internal sealed class ResPump : MonoBehaviour
    {
        private readonly FrameBudget budget = new FrameBudget();

        /// <summary>每帧触发一次，携带本帧时间预算；订阅方应在 <see cref="FrameBudget.HasTimeLeft"/> 为 false 时提前返回。</summary>
        internal static event Action<FrameBudget> Tick;

        private void Update()
        {
            // 1) 先把后台线程/终结器排过来的动作在主线程上执行掉。
            MainThreadDispatcher.Drain();

            // 2) 开始计时本帧预算，供后续各订阅方共享。
            budget.Begin(ResSettings.FrameBudgetMilliseconds);

            try
            {
                Tick?.Invoke(budget);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[PolarisRes] A ResPump.Tick subscriber threw an exception: {ex}");
            }
        }
    }
}
