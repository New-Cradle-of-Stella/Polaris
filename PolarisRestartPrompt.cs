using System;
using nel;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>
    /// 模组管理页的"需要重启"确认窗：一个与主面板同族、但独立摆在屏幕中央的
    /// <see cref="UiBoxDesigner"/>，正文说明缓存的启停修改必须重启才会生效，下方两个按钮
    /// 分别对应"保存并关闭游戏"与"放弃修改"。
    /// <para>
    /// 弹出期间 <see cref="PolarisManagementUI"/> 会把主列表整个 deactivate 掉——这一族里的
    /// 按钮不存在"被上层窗口挡住就点不到"的说法，鼠标射线照样能打到列表上的启停按钮；
    /// 只有把列表收起来，这个确认窗才是真正的模态。
    /// </para>
    /// </summary>
    internal static class PolarisRestartPrompt
    {
        const string DesignerName = "PolarisRestartPrompt";

        const float PromptW = 480f;
        const float PromptH = 240f;

        /// <summary>正文占用的高度：面板内高（PromptH - margin_in_tb * 2 = 180）减去按钮行与留白。</summary>
        const float TextH = 110f;

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

        /// <summary>
        /// 首次调用时在 <paramref name="family"/> 里建出确认窗。必须在主面板与详情浮窗之后建，
        /// 才会拿到这一族里最靠前的 z（<c>CreateT</c> 每建一个就把 <c>base_z</c> 往前推
        /// 一个 <c>slip_z</c>）；建完立刻从自动激活位里摘掉并 deactivate，否则一打开管理页
        /// 它就跟着亮起来了。
        /// </summary>
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

        /// <summary>
        /// 弹出确认窗。<paramref name="confirm"/> 与 <paramref name="cancel"/> 在按钮点下时调用，
        /// 调用前本窗已自行收起，回调里可以直接接着做关页面/退游戏这类动作。
        /// </summary>
        internal static void Show(string message, Action confirm, Action cancel)
        {
            if (designer == null)
            {
                // 建不出窗就别把玩家卡在"改了但没提示"的中间态，直接当作确认处理。
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

            // html 保持 false：正文里会拼进模组文件名，第三方文件名带 '<' 会被富文本解析器吃掉。
            // text_auto_wrap 必须显式设 true，它的默认值是 TX.isEnglishLang()，中文环境下为 false。
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
                title = "确定（关闭游戏）",
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
                title = "取消（放弃修改）",
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
