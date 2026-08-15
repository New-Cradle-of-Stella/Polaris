using nel.title;

namespace Polaris
{
    /// <summary>标题画面一次性告知页的注册表，按固定优先级顺序问一遍：致命错误 > 责任告知 > 上一局错误通知。</summary>
    internal static class TitleOverlays
    {
        static readonly ITitleOverlay[] all =
        [
            PolarisFatalNotice.Overlay,
            PolarisModWarning.Overlay,
            PolarisErrorNotice.Overlay,
        ];

        /// <summary>当前是否有告知页正占着标题画面；由 <see cref="Gate"/> 每次被问到时更新，不会有残留的过期 true。</summary>
        internal static bool IsShowing { get; private set; }

        /// <summary>依次问过去，第一个返回 true 的页面独占当前帧，同一时刻只显示一页。</summary>
        internal static bool Gate(SceneTitleTemp scene)
        {
            foreach (ITitleOverlay overlay in all)
            {
                if (overlay.Gate(scene))
                {
                    IsShowing = true;
                    return true;
                }
            }

            IsShowing = false;
            return false;
        }

        /// <summary>全部页面都推进一次淡入动画；未展示的页面内部会直接短路返回。</summary>
        internal static void AdvanceFade(float deltaSeconds)
        {
            foreach (ITitleOverlay overlay in all)
            {
                overlay.AdvanceFade(deltaSeconds);
            }
        }
    }
}
