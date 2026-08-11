using System;
using System.Collections.Generic;
using System.Text;
using nel;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>
    /// 模组管理页右侧的详情浮窗，复刻游戏原版设置页 <c>nel.UiCFG.fnShowDesc</c> 的行为：
    /// 一个独立于主面板的 <see cref="UiBoxDesigner"/>，贴在主面板右外侧，x 固定、y 跟随当前悬停行。
    /// <para>
    /// 和原版一样只挂 <c>fnHover</c>、不挂 <c>fnOut</c>——鼠标移开后浮窗保持显示最后悬停的那一项，
    /// 直到悬停到另一项或页面关闭。挂 fnOut 会让鼠标在两行之间掠过时浮窗反复闪烁。
    /// </para>
    /// <para>
    /// 已知行为（与原版一致，不是缺陷）：同一项持续悬停期间滚动列表，行动了而浮窗不动。
    /// 因为 <c>fnHover</c> 不会对同一个按钮重复触发；滚动后鼠标下换成别的按钮时会自动校正。
    /// </para>
    /// </summary>
    internal static class PolarisModDetailPopup
    {
        const string DesignerName = "PolarisModuleDetail";

        /// <summary>正文 <see cref="FillBlock"/> 的检索名：靠它复用已建好的文本块，只换字不重建。</summary>
        const string TextName = "__POLARIS_DETAIL_P";

        const float PopupW = 360f;
        const float PopupH = 160f;
        const float GapX = 4f;      // 与主面板右边缘的间隙，同原版
        const float MarginLR = 20f; // use_w = PopupW - MarginLR * 2 = 320
        const float TextSize = 14f;

        /// <summary>Unity 单位与界面像素的换算：1 单位 = 64 像素。</summary>
        const float UnitPixels = 64f;

        // 出现动画：方向 2、位移 40，同原版 BxDesc。
        const int AppearDir = 2;
        const float AppearLen = 40f;

        // 行数多时收紧行距，避免固定高度里放不下；阈值与原版同思路，按我们更长的正文上调。
        const int DenseLineThreshold = 6;
        const float LineSpacingLoose = 1.15f;
        const float LineSpacingDense = 0.92f;

        // 超长字段截断长度。Description / Url 来自第三方模组作者填写的特性，没有任何长度约束。
        const int HeadlineMax = 40;
        const int FieldMax = 46;
        const int DescriptionMax = 120;

        static readonly Color32 TextColor = new Color32(56, 56, 56, 255);
        static readonly Color32 ErrorColor = new Color32(168, 42, 32, 255);

        static UiBoxDesigner designer;
        static UiBoxDesigner owner;

        /// <summary>当前正在展示的 <see cref="UserModRecord.DisplayName"/>；展示的不是模组（如刷新按钮说明）时为 null。</summary>
        static string currentKey;

        /// <summary>
        /// 首次调用时在 <paramref name="family"/> 里建出浮窗；重复调用只更新 <paramref name="ownerBox"/> 引用。
        /// 建完立刻 <c>deactivate</c>，并从 <c>family.activate()</c> 的自动激活位里摘掉——
        /// 否则一打开管理页，鼠标还没碰任何东西浮窗就已经亮着了。
        /// </summary>
        internal static void Ensure(UiBoxDesignerFamily family, UiBoxDesigner ownerBox)
        {
            owner = ownerBox;

            if (designer != null)
            {
                return;
            }

            // 后 Create 的 designer z 更小（CreateT 里每次 base_z -= slip_z），会画在主面板上层；
            // MASKTYPE.BOX 分到的 stencil 是 70 + ADs.Count，与主面板互不干扰。
            designer = family.Create(
                DesignerName, 0f, 0f, PopupW, PopupH,
                -1, 30f, UiBoxDesignerFamily.MASKTYPE.BOX);
            designer.use_scroll = false;
            designer.getBox().frametype = UiBox.FRAMETYPE.MAIN;

            family.setAutoActivate(designer, false);
            designer.Focusable(false); // 不参与焦点，否则会和主列表抢，导致键盘导航失效
            designer.deactivate();
        }

        /// <summary>
        /// 收起浮窗并忘掉当前项。管理页关闭时必须调用，否则 <see cref="currentKey"/> 残留会让下次打开时
        /// <see cref="Refresh"/> 把浮窗抢在主面板之前点亮。
        /// 刻意不调 <c>Clear()</c>：留住正文 <see cref="FillBlock"/>，下次悬停仍走只换字的快路径。
        /// </summary>
        internal static void Reset()
        {
            currentKey = null;
            designer?.deactivate();
        }

        /// <summary>
        /// 悬停某个模组行时调用：换内容，并把浮窗移到该行右侧。
        /// <paramref name="targetEnabled"/> 是玩家期望的启停状态（可能还只存在于
        /// <see cref="PolarisManagementUI"/> 的缓存里，与磁盘现状不一致）。
        /// </summary>
        internal static void Show(aBtn button, UserModRecord record, bool targetEnabled, string error)
        {
            if (designer == null || owner == null)
            {
                return;
            }

            currentKey = record.DisplayName;
            SetText(Compose(record, targetEnabled, error), error != null);
            MoveTo(button);
        }

        /// <summary>悬停非模组条目（如"刷新列表"按钮）时调用：展示一段固定说明，不记录当前项。</summary>
        internal static void ShowText(aBtn button, string text)
        {
            if (designer == null || owner == null)
            {
                return;
            }

            currentKey = null;
            SetText(text, isError: false);
            MoveTo(button);
        }

        /// <summary>
        /// 列表重建之后调用：按 <see cref="currentKey"/> 在新一批记录里重新查，只换文字、不动位置。
        /// <para>
        /// 不动位置是安全的——每个模组行高度统一 26f，切换启停只改标题里的勾选标记，
        /// <c>OrderBy(DisplayName)</c> 的排序也不变，所以那一行重建后原地不动。
        /// 若将来给某些行加上额外高度，这个前提就破了，届时要改成重新定位。
        /// </para>
        /// 查不到该键（文件被游戏外部删掉或改名）则收起浮窗。
        /// </summary>
        internal static void Refresh(
            IReadOnlyList<UserModRecord> mods,
            Func<UserModRecord, bool> targetEnabled,
            IDictionary<string, string> errors)
        {
            if (designer == null || currentKey == null)
            {
                return;
            }

            foreach (UserModRecord record in mods)
            {
                if (!string.Equals(record.DisplayName, currentKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                errors.TryGetValue(currentKey, out string error);
                SetText(Compose(record, targetEnabled(record), error), error != null);
                return;
            }

            Reset();
        }

        /// <summary>
        /// 把浮窗摆到 <paramref name="button"/> 所在行的右外侧。x 用主面板实际的 <c>swidth</c> 算
        /// （不是 Create 时传入的宽度——框的实际宽度未必等于它，原版用的也是 <c>BxOut.swidth</c>）；
        /// y 取按钮在容器内的位置，<c>getLocalPosFromContainer</c> 内部已扣掉滚动位移，
        /// 返回的永远是视口内坐标，因此滚动后依然对齐，纵向也不会跑出屏幕。
        /// </summary>
        static void MoveTo(aBtn button)
        {
            UiBox ownerBox = owner.getBox();
            float x = owner.swidth / 2f + PopupW / 2f + GapX + ownerBox.get_deperture_x();
            float y = button.get_Skin().getLocalPosFromContainer().y * UnitPixels + ownerBox.get_deperture_y();

            if (designer.isActive())
            {
                designer.position(x, y); // 已经亮着，平滑滑到新行
            }
            else
            {
                designer.activate();
                designer.positionD(x, y, AppearDir, AppearLen); // 首次亮起，播出现动画
            }
        }

        /// <summary>
        /// 写入正文。首次走 <c>Clear + addP</c> 建块，之后只改 <see cref="FillBlock"/> 的文字与行距，
        /// 结构照抄原版 <c>fnShowDesc</c>（原版此处也不调 <c>init()</c>）。
        /// </summary>
        static void SetText(string text, bool isError)
        {
            float lineSpacing = CountLines(text) >= DenseLineThreshold ? LineSpacingDense : LineSpacingLoose;
            Color32 color = isError ? ErrorColor : TextColor;

            if (designer.Get(TextName) is FillBlock block)
            {
                block.lineSpacing = lineSpacing;
                block.TxCol = color;
                block.text_content = text;
                return;
            }

            designer.Clear();
            designer.margin_in_lr = MarginLR;
            designer.margin_in_tb = 0f;
            designer.WH(PopupW, PopupH);
            designer.alignx = ALIGN.LEFT;

            // html 保持 false：正文里的作者、简介、链接都是第三方模组作者填的，出现 '<' 会被富文本解析器吃掉。
            // text_auto_wrap 必须显式设 true：它的默认值是 TX.isEnglishLang()，中文环境下为 false。
            designer.addP(new DsnDataP(text, false)
            {
                name = TextName,
                size = TextSize,
                alignx = ALIGN.LEFT,
                aligny = ALIGNY.MIDDLE,
                TxCol = color,
                swidth = designer.use_w,
                sheight = designer.use_h,
                text_auto_wrap = true,
                lineSpacing = lineSpacing,
            });
        }

        /// <summary>拼出浮窗正文，一条一行。</summary>
        static string Compose(UserModRecord record, bool targetEnabled, string error)
        {
            PolarisModInfo info = record.Info;
            bool hasModInfo = info != null && info.HasModInfo;
            var text = new StringBuilder();

            // 标了特性就用展示名 + 版本当标题，末尾再补一行 dll 文件名；没标就直接拿文件名当标题，不重复。
            if (hasModInfo)
            {
                string headline = info.Version == null ? info.DisplayName : $"{info.DisplayName}  v{info.Version}";
                text.Append(Clip(headline, HeadlineMax));

                if (info.Author != null)
                {
                    text.Append('\n').Append("作者：").Append(Clip(info.Author, FieldMax));
                }

                if (info.Description != null)
                {
                    text.Append('\n').Append("简介：").Append(Clip(info.Description, DescriptionMax));
                }

                if (info.Url != null)
                {
                    text.Append('\n').Append("链接：").Append(Clip(info.Url, FieldMax));
                }
            }
            else
            {
                text.Append(Clip(record.DisplayName, HeadlineMax));
            }

            if (targetEnabled != record.Enabled)
            {
                // 本页里改过、但还没落盘的状态。文案要说清"现在是什么"和"重启后会变成什么"，
                // 别让玩家以为点一下就已经生效了。
                text.Append('\n')
                    .Append(record.Enabled ? "当前已启用，待禁用" : "当前已禁用，待启用")
                    .Append("（关闭本页确认后重启生效）");
            }
            else if (!record.Enabled)
            {
                // 没有待应用的改动时，磁盘现状就是本次启动时的状态，不必再提"重启后生效"。
                text.Append('\n').Append("已禁用");
            }
            else if (!hasModInfo)
            {
                // 两种情况都会走到这里，文案要同时说清：模组根本没标特性；以及文件虽然是启用
                // 状态、但本次启动时 BepInEx 并没有加载过它（比如是在游戏外面手动改名启用的）。
                text.Append('\n').Append("未提供模组信息，或本次启动时未加载。");
            }

            if (hasModInfo)
            {
                text.Append('\n').Append(record.DisplayName);
            }

            if (error != null)
            {
                text.Append('\n').Append("操作失败：").Append(Clip(error, FieldMax));
            }

            return text.ToString();
        }

        /// <summary>截断超长字段。浮窗高度固定，放任第三方填的长文本会把后面的行挤没。</summary>
        static string Clip(string text, int max)
        {
            return text == null || text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }

        static int CountLines(string text)
        {
            int lines = 1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lines++;
                }
            }

            return lines;
        }
    }
}
