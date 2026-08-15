using nel;
using UnityEngine;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 标题画面设置页底部的搜索框窗口，因该处无现成位置放置，故自建 designer 填进面板缩短后让出的一条（游戏内 ESC 菜单则用原版"底部子区"机制，不走这条路）。
    /// 已知限制：本窗口不在 <c>BxR</c> 的按钮导航链中，标题画面的搜索框只能用鼠标点开。
    /// </summary>
    internal static class SettingsSearchWindow
    {
        const string DesignerName = "PolarisSettingsSearch";

        /// <summary>出现动画：方向 0 + 位移 50，与原版设置面板 <c>BxR.positionD(-190, cfg_y, 0, 50f)</c> 一致。</summary>
        const int AppearDir = 0;
        const float AppearLen = 50f;

        static GameObject host;
        static UiBoxDesignerFamily family;
        static UiBoxDesigner designer;

        /// <summary>上一次摆放的位置，供 <see cref="Resume"/> 原地亮回来。</summary>
        static float lastX;
        static float lastY;

        /// <summary>标题画面上要不要有搜索框；无模组注册设置项时不出现。</summary>
        internal static bool Wanted(bool isTitle)
            => isTitle && PolarisAPI.Settings.Groups.Count > 0;

        /// <summary>面板一共让出多少：搜索栏本身 + 它和面板之间的留白。</summary>
        static float Take => SettingsSearchBox.StripHeight + SettingsSearchBox.StripGap;

        /// <summary>把原版设置面板从底部缩短，空出搜索栏占用的一条；上边缘不动，中心随高度减少上移一半。</summary>
        internal static void ShrinkPanel(UiBoxDesigner panel)
        {
            float y = panel.getBox().get_deperture_y();

            // 宽度须原样传回：UiBoxDesigner 重写下传 0 会被当成真的宽度 0，而非"不变"。
            panel.WH(panel.w, panel.h - Take);
            panel.positionD(panel.getBox().get_deperture_x(), y + Take / 2f, AppearDir, AppearLen);
        }

        /// <summary>把搜索框摆进 <paramref name="panel"/> 让出的一条里（面板已缩短，按其下边缘算）；由标题画面 <c>UiCFG</c> 构造完成时调用。</summary>
        internal static void ShowUnder(UiBoxDesigner panel)
        {
            float y = panel.getBox().get_deperture_y() - panel.h / 2f
                      - SettingsSearchBox.StripGap - SettingsSearchBox.StripHeight / 2f;

            Show(panel.getBox().get_deperture_x(), y, panel.w);
        }

        static void Show(float x, float y, float width)
        {
            if (!Ensure(width))
            {
                return;
            }

            lastX = x;
            lastY = y;

            designer.Clear();
            designer.init();
            SettingsSearchBox.Build(designer);

            family.activate();
            designer.positionD(x, y, AppearDir, AppearLen);
        }

        /// <summary>从按键设置页退回时用：内容还在，只需把窗口亮回来；无内容可亮则不做任何事。</summary>
        internal static void Resume()
        {
            // designer 为 null 表示这一局从未显示过搜索框（无模组注册设置项）。
            if (designer == null)
            {
                return;
            }

            family.activate();
            designer.positionD(lastX, lastY, AppearDir, AppearLen);
        }

        internal static void Hide()
        {
            family?.deactivate();
        }

        /// <summary>首次调用时建出窗口；建不出来返回 false（调用方据此放弃显示搜索框）。</summary>
        static bool Ensure(float width)
        {
            if (designer != null)
            {
                // 面板宽度原版写死为 630，理论不变；变了就跟上以免对不齐。
                if (designer.w != width)
                {
                    designer.WH(width, SettingsSearchBox.StripHeight);
                }

                return true;
            }

            host = new GameObject("Polaris.SettingsSearch");
            Object.DontDestroyOnLoad(host);
            // 与模组管理页同一层，盖住标题画面常驻 UI 但不越过全屏覆盖层。
            IN.setZ(host.transform, UiDepth.Window);

            family = host.AddComponent<UiBoxDesignerFamily>();
            designer = family.Create(
                DesignerName, 0f, 0f, width, SettingsSearchBox.StripHeight,
                -1, 30f, UiBoxDesignerFamily.MASKTYPE.BOX);

            designer.use_scroll = false;
            designer.getBox().frametype = UiBox.FRAMETYPE.MAIN;
            // 一条扁栏，上下留白要比默认的 11 小，否则 24 高的输入框根本放不下。
            designer.margin_in_tb = 6f;
            designer.margin_in_lr = 24f;
            designer.item_margin_x_px = 6f;

            return true;
        }
    }
}
