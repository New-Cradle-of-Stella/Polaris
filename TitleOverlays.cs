using nel.title;

namespace Polaris
{
    /// <summary>
    /// 标题画面一次性告知页的注册表，按优先级顺序问一遍 <see cref="ITitleOverlay"/>。
    /// <para>
    /// 顺序即优先级，写死成数组字面量而不是运行时 <c>Register</c>：这几页都是 Polaris 自己的、
    /// 数量固定、不需要模组扩展，运行时注册反而会让"谁先弹"变得依赖模组加载顺序。
    /// 致命错误（<see cref="PolarisFatalNotice"/>）排在最前：这一局已经判定不能继续，再问玩家
    /// "责任声明看没看过""上一局出了什么问题"都没有意义，而且那两页确认完会放行、这一页不会。
    /// 其后责任告知（<see cref="PolarisModWarning"/>）排在错误通知（<see cref="PolarisErrorNotice"/>）
    /// 前面：一辈子只弹一次的"先把责任讲清楚"应该比"上一局出了什么问题"先看到。
    /// </para>
    /// </summary>
    internal static class TitleOverlays
    {
        static readonly ITitleOverlay[] all =
        [
            PolarisFatalNotice.Overlay,
            PolarisModWarning.Overlay,
            PolarisErrorNotice.Overlay,
        ];

        /// <summary>
        /// 依次问过去，第一个返回 true 的页面独占当前帧——同一时刻只显示一页，
        /// 不会叠在一起。
        /// </summary>
        internal static bool Gate(SceneTitleTemp scene)
        {
            foreach (ITitleOverlay overlay in all)
            {
                if (overlay.Gate(scene))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 全部页面都推进一次淡入动画。没有在展示的页面内部会直接短路返回，
        /// 这里不需要重复判断"当前是哪一页"。
        /// </summary>
        internal static void AdvanceFade(float deltaSeconds)
        {
            foreach (ITitleOverlay overlay in all)
            {
                overlay.AdvanceFade(deltaSeconds);
            }
        }
    }
}
