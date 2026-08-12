using System;
using System.Collections.Generic;
using System.Text;
using nel;
using Polaris.Localization;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>模组管理页右侧的详情浮窗，复刻原版设置页行为：只挂 fnHover 不挂 fnOut，鼠标移开后仍显示最后悬停项，避免闪烁。</summary>
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

        /// <summary>首次调用时建出浮窗并立即隐藏（否则打开管理页时浮窗会先亮着）；重复调用只更新 owner 引用。</summary>
        internal static void Ensure(UiBoxDesignerFamily family, UiBoxDesigner ownerBox)
        {
            owner = ownerBox;

            if (designer != null)
            {
                return;
            }

            // 后 Create 的 designer z 更小，会画在主面板上层。
            designer = family.Create(
                DesignerName, 0f, 0f, PopupW, PopupH,
                -1, 30f, UiBoxDesignerFamily.MASKTYPE.BOX);
            designer.use_scroll = false;
            designer.getBox().frametype = UiBox.FRAMETYPE.MAIN;

            family.setAutoActivate(designer, false);
            designer.Focusable(false); // 不参与焦点，否则会和主列表抢，导致键盘导航失效
            designer.deactivate();

        }

        /// <summary>收起浮窗并忘掉当前项；管理页关闭时必须调用，否则残留状态会让下次打开时误点亮浮窗。</summary>
        internal static void Reset()
        {
            currentKey = null;
            designer?.deactivate();
        }

        /// <summary>悬停某个模组行时调用：换内容并移到该行右侧；<paramref name="targetEnabled"/> 可能只是尚未落盘的缓存状态。</summary>
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

        /// <summary>列表重建后调用：按 <see cref="currentKey"/> 重新查，只换文字不动位置（各行高度统一，重建后原地不动）；查不到则收起浮窗。</summary>
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

        /// <summary>把浮窗摆到 <paramref name="button"/> 所在行的右外侧；y 坐标已扣掉滚动位移，故滚动后依然对齐。</summary>
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

        /// <summary>写入正文；首次 Clear+addP 建块，之后只改文字与行距。</summary>
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

            // html 保持 false：第三方填的文本若含 '<' 会被富文本解析器吃掉。
            // text_auto_wrap 须显式设 true，默认值 TX.isEnglishLang() 在中文环境下为 false。
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
                    text.Append('\n')
                        .Append(ModManagerStrings.Text(ModManagerStrings.DetailAuthor))
                        .Append(Clip(info.Author, FieldMax));
                }

                if (info.Description != null)
                {
                    text.Append('\n')
                        .Append(ModManagerStrings.Text(ModManagerStrings.DetailDescription))
                        .Append(Clip(info.Description, DescriptionMax));
                }

                if (info.Url != null)
                {
                    text.Append('\n')
                        .Append(ModManagerStrings.Text(ModManagerStrings.DetailUrl))
                        .Append(Clip(info.Url, FieldMax));
                }
            }
            else
            {
                text.Append(Clip(record.DisplayName, HeadlineMax));
            }

            if (targetEnabled != record.Enabled)
            {
                // 已改动但未落盘：需说清现状与重启后的结果，避免玩家以为点一下就生效。
                text.Append('\n').Append(ModManagerStrings.Text(record.Enabled
                    ? ModManagerStrings.DetailPendingDisable
                    : ModManagerStrings.DetailPendingEnable));
            }
            else if (!record.Enabled)
            {
                // 无待应用改动，磁盘现状就是本次启动状态，不必再提"重启后生效"。
                text.Append('\n').Append(ModManagerStrings.Text(ModManagerStrings.DetailDisabled));
            }
            else if (!hasModInfo)
            {
                // 覆盖两种情况：模组未标特性；或文件已启用但本次 BepInEx 未加载它。
                text.Append('\n').Append(ModManagerStrings.Text(ModManagerStrings.DetailNoInfo));
            }

            if (hasModInfo)
            {
                text.Append('\n').Append(record.DisplayName);
            }

            if (error != null)
            {
                text.Append('\n')
                    .Append(ModManagerStrings.Text(ModManagerStrings.DetailFailed))
                    .Append(Clip(error, FieldMax));
            }

            return text.ToString();
        }

        /// <summary>截断超长字段，避免第三方长文本把后面的行挤没。</summary>
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
