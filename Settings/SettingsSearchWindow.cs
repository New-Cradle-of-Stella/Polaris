using nel;
using UnityEngine;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 标题画面设置页底部的搜索框窗口。
    /// <para>
    /// 标题画面这边没有现成的地方放它：原版设置面板就是 <c>SceneTitleTemp.BxR</c> 一个框，
    /// 底下那段空白归"确定/取消"按钮条（<c>DsBlack</c>，摆在 <c>-IN.hh + 140</c>）。所以这里自建
    /// 一个 designer，位置由 <see cref="Patch.Patch_UiCFG_Constructor"/> 按缩短后的面板算出来
    /// （面板从底部让出 <see cref="SettingsSearchBox.StripHeight"/> + <see cref="SettingsSearchBox.StripGap"/>，
    /// 这个窗口正好填进让出来的那一条）。
    /// </para>
    /// <para>
    /// 游戏内不走这条路：ESC 菜单有原版自己的"底部子区"机制（<c>UiGMC.subarea_btm_*</c>），
    /// 它会连带把右侧内容框自动缩短、跟着菜单一起做出现/收起动画，比自己摆一个框稳当得多。
    /// </para>
    /// <para>
    /// <b>已知限制</b>：本窗口不属于 <c>BxR</c> 的按钮导航链，所以标题画面上的搜索框只能用鼠标点开。
    /// 让它可被键盘聚焦得把它设成 focusable，而 <c>BxR</c> 是以 <c>_click_focusflag: false</c> 建的
    /// ——焦点一旦被抢走就没法靠点回设置面板还回去，那比"要用鼠标点一下"糟得多。
    /// 游戏内的子区由原版 <c>initAppearWhole</c> 挂进导航链，没有这个问题。
    /// </para>
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

        /// <summary>标题画面上要不要有搜索框。一个模组都没注册过设置项时整条栏都不出现。</summary>
        internal static bool Wanted(bool isTitle)
            => isTitle && PolarisAPI.Settings.Groups.Count > 0;

        /// <summary>面板一共让出多少：搜索栏本身 + 它和面板之间的留白。</summary>
        static float Take => SettingsSearchBox.StripHeight + SettingsSearchBox.StripGap;

        /// <summary>
        /// 把原版设置面板从底部缩短，空出搜索栏要占的那一条。
        /// 上边缘钉住不动，所以高度减多少、中心就往上挪一半；
        /// <c>positionD</c> 的方向与位移抄原版那一句（<c>positionD(-190, cfg_y, 0, 50f)</c>），
        /// 出现动画看起来才和没改过一样。
        /// </summary>
        internal static void ShrinkPanel(UiBoxDesigner panel)
        {
            float y = panel.getBox().get_deperture_y();

            // 宽度必须原样传回去，不能传 0 指望"保持不变"：Designer.WH 那一层确实把非正值
            // 当成"不变"，但 UiBoxDesigner 的重写会把同一个 0 直接交给 MsgBox.swh，
            // 而那边只把负值当成"不变"——传 0 的结果是面板宽度真的变成 0。
            panel.WH(panel.w, panel.h - Take);
            panel.positionD(panel.getBox().get_deperture_x(), y + Take / 2f, AppearDir, AppearLen);
        }

        /// <summary>
        /// 把搜索框摆进 <paramref name="panel"/> 让出来的那一条里（面板此时已经缩过了，
        /// 所以是按缩短后的下边缘往下算）。由标题画面的 <c>UiCFG</c> 构造完成时调用——
        /// 那时设置项刚画完，<see cref="SettingsSearchFilter"/> 的登记表是新鲜的。
        /// </summary>
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

        /// <summary>
        /// 从按键设置页退回来时用：内容还在（<c>UiCFG</c> 没被 destruct），只要把窗口亮回来。
        /// 没有内容可亮就什么都不做。
        /// </summary>
        internal static void Resume()
        {
            // designer 为 null 就是"这一局从来没显示过搜索框"（没有任何模组注册设置项）。
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
                // 面板宽度在原版里是写死的 630，理论上不会变；真变了就跟上，
                // 免得搜索栏和设置面板对不齐。
                if (designer.w != width)
                {
                    designer.WH(width, SettingsSearchBox.StripHeight);
                }

                return true;
            }

            host = new GameObject("Polaris.SettingsSearch");
            Object.DontDestroyOnLoad(host);
            // 与模组管理页同一层：稳稳盖住标题画面的常驻 UI，又不越过全屏覆盖层。见 UiDepth。
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
