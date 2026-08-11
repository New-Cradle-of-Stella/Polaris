using System;
using UnityEngine;

namespace Polaris.Res.Runtime
{
    /// <summary>
    /// PolarisRes 唯一的每帧泵。挂在 <see cref="ResHost"/> 的常驻 GameObject 上。
    /// <para>
    /// 不用游戏自己的 <c>LoadTicketManager</c>（它依赖舞台对象、且每帧只处理"当前最低优先级"
    /// 那一批，多帧任务会饿死原版加载），也不把每个加载任务各自建一个 Unity 协程
    /// （协程宿主被销毁/禁用会静默死掉，留下永远不完成也不报错的半初始化状态，
    /// 也无法被统一枚举做诊断、无法施加全局预算）。这里改用显式的 <see cref="Tick"/> 事件，
    /// 各子系统（I/O 完成派发、在途加载任务推进、延迟卸载扫描、热重载防抖、音量桥……）
    /// 在自己的里程碑里订阅它，在同一个 <see cref="FrameBudget"/> 下分摊时间片。
    /// </para>
    /// </summary>
    internal sealed class ResPump : MonoBehaviour
    {
        private readonly FrameBudget budget = new FrameBudget();

        /// <summary>
        /// 每帧触发一次，携带本帧的时间预算。订阅方应在 <see cref="FrameBudget.HasTimeLeft"/>
        /// 为 false 时提前返回，把剩余工作留到下一帧——预算由 <see cref="ResSettings.FrameBudgetMilliseconds"/> 配置。
        /// </summary>
        internal static event Action<FrameBudget> Tick;

        private void Update()
        {
            // 1) 先把后台线程/终结器排过来的动作在主线程上执行掉。
            MainThreadDispatcher.Drain();

            // 2) 开始计时本帧预算，供后续各订阅方共享。
            budget.Begin(ResSettings.FrameBudgetMilliseconds);

            // 3) 其余子系统（M4 起的任务推进、M6 起的延迟卸载、M7 起的统计、
            //    M8 起的热重载防抖、M9 起的音量桥）都挂在这里。
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
