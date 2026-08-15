using System;
using nel;
using Polaris.Localization;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>模组管理页的"需要重启"确认窗，独立摆在屏幕中央；仅拦在"确定"这条路上，"取消"无条件绕过它。管理页需把主列表整个 deactivate 才能让本窗真正模态。</summary>
    internal static class PolarisRestartPrompt
    {
        const string DesignerName = "PolarisRestartPrompt";

        const float PromptW = 480f;

        /// <summary>窗口高度按最长语言版本（英文，约 8 行）定；本窗不滚动，排不下会把按钮顶出框外点不到。</summary>
        const float PromptH = 300f;

        /// <summary>正文占用的高度：面板内高（PromptH - margin_in_tb * 2 = 240）减去按钮行与留白。</summary>
        const float TextH = 170f;

        const float ButtonW = 180f;
        const float ButtonH = 30f;
        const float TextSize = 15f;

        // 出现动画：方向 2（自下而上）、位移 40，与详情浮窗保持一致。
        const int AppearDir = 2;
        const float AppearLen = 40f;

        static readonly Color32 TextColor = new Color32(56, 56, 56, 255);

        static UiBoxDesigner designer;
        static Action onConfirm;
        static Action onCancel;

        /// <summary>确认窗当前是否正显示；管理页据此把 ESC/关闭改派给本窗处理。</summary>
        internal static bool IsOpen { get; private set; }

        /// <summary>首次调用时建出确认窗，须在主面板与详情浮窗之后建才能拿到最靠前的 z；建完立即隐藏。</summary>
        internal static void Ensure(UiBoxDesignerFamily family)
        {
            if (designer != null)
            {
                return;
            }

            designer = family.Create(
                DesignerName, 0f, 0f, PromptW, PromptH,
                -1, 30f, UiBoxDesignerFamily.MASKTYPE.BOX);
            designer.use_scroll = false;
            designer.getBox().frametype = UiBox.FRAMETYPE.MAIN;

            family.setAutoActivate(designer, false);
            designer.deactivate();
        }

        /// <summary>弹出确认窗；回调触发前本窗已自行收起，回调里可直接接着关页面/退游戏。</summary>
        internal static void Show(string message, Action confirm, Action cancel)
        {
            if (designer == null)
            {
                // 建不出窗就不把玩家卡在中间态，直接当作确认处理。
                confirm?.Invoke();
                return;
            }

            onConfirm = confirm;
            onCancel = cancel;

            designer.Clear();
            designer.init();
            Build(message);

            IsOpen = true;
            designer.activate();
            designer.positionD(0f, 0f, AppearDir, AppearLen);
        }

        /// <summary>收起确认窗并丢掉回调；管理页关闭时也要调用，避免回调跨次打开残留。</summary>
        internal static void Hide()
        {
            IsOpen = false;
            onConfirm = null;
            onCancel = null;
            designer?.deactivate();
        }

        static void Build(string message)
        {
            designer.alignx = ALIGN.CENTER;

            // html 保持 false：第三方文件名若带 '<' 会被富文本解析器吃掉；text_auto_wrap 须显式设 true。
            designer.addP(new DsnDataP(message, false)
            {
                size = TextSize,
                alignx = ALIGN.LEFT,
                aligny = ALIGNY.MIDDLE,
                TxCol = TextColor,
                swidth = designer.use_w,
                sheight = TextH,
                text_auto_wrap = true,
                lineSpacing = 1.15f,
            });
            designer.Br();

            designer.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = "restart_confirm",
                title = ModManagerStrings.Text(ModManagerStrings.PromptConfirm),
                w = ButtonW,
                h = ButtonH,
                fnClick = _ =>
                {
                    Action confirm = onConfirm;
                    Hide();
                    confirm?.Invoke();
                    return true;
                }
            });

            designer.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = "restart_cancel",
                title = ModManagerStrings.Text(ModManagerStrings.PromptCancel),
                w = ButtonW,
                h = ButtonH,
                fnClick = _ =>
                {
                    Action cancel = onCancel;
                    Hide();
                    cancel?.Invoke();
                    return true;
                }
            });

            designer.Br();
            designer.alignx = ALIGN.LEFT;
        }
    }
}
