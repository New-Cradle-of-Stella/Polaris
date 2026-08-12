using System;
using System.Collections.Generic;
using nel;
using UnityEngine;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 把已注册的设置项追加到原版设置界面（<c>nel.UiCFG</c>）主标签页尾部，并负责右侧说明框。
    /// 挂接点是官方 <c>FnCfgTabCreateAfter</c> 委托，不改原版任何 IL。
    /// </summary>
    internal static class PolarisSettingsScreen
    {
        /// <summary>说明框正文的检索名；与原版 <c>__CFG_DESC_P</c> 区分开，因原版块 <c>html = true</c> 会吃掉说明里的 '&lt;'。</summary>
        const string DescBlockName = "__PLRS_CFG_DESC_P";

        /// <summary>说明框尺寸，与原版 <c>UiCFG.desc_w/desc_h</c> 一致。</summary>
        const float DescW = 380f;
        const float DescH = 120f;
        const float DescMarginLR = 40f;
        const float DescTextSize = 16f;

        /// <summary>说明文字缩到放得下为止的下限（12，再小不好认）与每次缩的幅度。</summary>
        const float DescMinTextSize = 12f;
        const float DescSizeStep = 1f;

        /// <summary>行数多到这个程度就收紧行距，抄的原版阈值。</summary>
        const int DenseLineThreshold = 6;
        const float LineSpacingDense = 0.94f;
        const float LineSpacingLoose = 1.16f;

        /// <summary>原版文字色；<c>UiCFG.P</c> 的标签和 <c>fnShowDesc</c> 的说明框用的是同一个值。</summary>
        const uint TextColor = 4283780170u;

        /// <summary>分区分隔线的样式，取值抄自 <c>UiBoxDesigner.Hr</c> 与原来那句 <c>Hr(0.94f, 16f, 8f)</c>。</summary>
        const float HrWidthRatio = 0.94f;
        const float HrMargin = 16f;
        const uint HrColor = 2857717320u;

        /// <summary>Unity 单位与界面像素的换算：1 单位 = 64 像素。</summary>
        const float PixelsPerUnit = 64f;

        /// <summary>当前这个 UiCFG 实例上都画了哪些设置项，供 <see cref="Sync"/> 回拨界面。</summary>
        static readonly List<SettingDefinition> rendered = [];

        /// <summary>在主标签页尾部追加所有已注册的分区；由 <c>UiCFG.createBoxDesignerContentMain</c> 末尾的委托调用。</summary>
        internal static void Append(UiCFG cfg)
        {
            rendered.Clear();
            SettingsSearchFilter.Begin(cfg);
            PolarisAPI.Settings.ScreenBuilt = true;

            IReadOnlyList<SettingGroup> groups = PolarisAPI.Settings.Groups;
            if (groups.Count == 0)
            {
                return;
            }

            UiBoxDesigner box = cfg.BxOut;

            foreach (SettingGroup group in groups)
            {
                try
                {
                    SettingsSearchFilter.GroupRecorder recorder = SettingsSearchFilter.OpenGroup(group);
                    GroupHeader(box, group, recorder);
                    foreach (SettingDefinition setting in group.Settings)
                    {
                        SettingsRowRenderer.Render(cfg, box, setting, recorder.OpenRow(setting));
                        rendered.Add(setting);
                    }
                }
                catch (Exception e)
                {
                    // 一个模组画崩不能连累整个设置界面。
                    PolarisAPI.Errors.Report(e, $"rendering the settings of {group.ModId}");
                    Plugin.Logger.LogError($"[Polaris.Settings] Failed to render the settings of {group.ModId}; ignored.");
                }
            }

            Plugin.Logger.LogInfo($"[Polaris.Settings] Appended {groups.Count} groups and {rendered.Count} settings to the settings screen.");
        }

        /// <summary>
        /// 分区标题：一条分隔线 + 一行居中文字。分隔线照抄 <c>UiBoxDesigner.Hr</c> 而非直接调用，因为搜索过滤需要拿到 <c>addHr</c> 返回的块（<c>Hr()</c> 不返回）。
        /// </summary>
        static void GroupHeader(UiBoxDesigner box, SettingGroup group, SettingsSearchFilter.GroupRecorder recorder)
        {
            box.Br();
            recorder.AddHeader(box.addHr(new DsnDataHr
            {
                draw_width_rate = HrWidthRatio,
                swidth = box.use_w,
                Col = C32.d2c(HrColor),
                margin_t = HrMargin,
                margin_b = HrMargin,
                line_height = 1f,
            }));
            box.Br();

            recorder.AddHeader(Caption(box, group.DisplayTitle, "P_PLRS_GROUP_" + group.ModId, box.use_w));
            box.Br();
        }

        /// <summary>
        /// 一行文字，复刻原版 <c>UiCFG.P()</c> 样式，但不直接调用它——<c>UiCFG.P()</c> 强制把参数当本地化键走 <c>TX.Get</c>，未命中时静默返回空串，会把模组的字面量画成空白。这里收的是已求值的文案。
        /// </summary>
        /// <param name="width">文字块宽度：标签用固定的标签栏宽度，分区标题铺满整行</param>
        /// <returns>画出来的文字块，供 <see cref="SettingsSearchFilter"/> 登记显隐。</returns>
        internal static FillBlock Caption(UiBoxDesigner box, string text, string name, float width)
        {
            return box.Br().addP(new DsnDataP
            {
                text = text,
                name = name,
                size = 18f * (X.ENG_MODE ? 0.7f : 1f),
                alignx = ALIGN.CENTER,
                Col = MTRX.ColTrnsp,
                TxCol = C32.d2c(TextColor),
                swidth = width,
                sheight = 0f,
                text_auto_condense = true,
                text_auto_wrap = false,
                // 恒 false：文案是第三方填的，富文本解析器会吃掉里面的 '<'。
                html = false,
            });
        }

        /// <summary>把设置项当前值推回控件；<c>UiCFG</c> 实例复用，须靠此拨正两次打开之间值可能变化的界面。</summary>
        internal static void Sync(UiCFG cfg)
        {
            foreach (SettingDefinition setting in rendered)
            {
                try
                {
                    SettingsRowRenderer.Sync(cfg, setting);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"[Polaris.Settings] Failed to sync the control display of {setting.RowKey}: {e}");
                }
            }
        }

        /// <summary>右侧说明框，复刻原版 <c>UiCFG.fnShowDesc</c>：无说明则收起，有则挪到当前行；鼠标移开后保留最后悬停的一条。</summary>
        internal static bool ShowDescription(UiCFG cfg, aBtn button, string desc)
        {
            UiBoxDesigner bxDesc = cfg.BxDesc;
            if (bxDesc == null)
            {
                return true;
            }

            if (string.IsNullOrEmpty(desc))
            {
                bxDesc.positionD(bxDesc.getBox().get_deperture_x(), bxDesc.getBox().get_deperture_y(), 2, 40f);
                bxDesc.deactivate();
                return true;
            }

            float lineSpacing = TX.countLine(desc) >= DenseLineThreshold ? LineSpacingDense : LineSpacingLoose;

            FillBlock block = bxDesc.Get(DescBlockName) as FillBlock;
            if (block != null)
            {
                block.lineSpacing = lineSpacing;
                block.text_content = desc;
            }
            else
            {
                bxDesc.Clear();
                bxDesc.margin_in_lr = DescMarginLR;
                bxDesc.margin_in_tb = 0f;
                bxDesc.WH(DescW, DescH);
                bxDesc.alignx = ALIGN.LEFT;
                bxDesc.addP(new DsnDataP(desc, false)
                {
                    name = DescBlockName,
                    size = DescTextSize,
                    alignx = ALIGN.CENTER,
                    aligny = ALIGNY.MIDDLE,
                    Col = MTRX.ColTrnsp,
                    TxCol = C32.d2c(TextColor),
                    swidth = bxDesc.use_w,
                    sheight = bxDesc.use_h,
                    // 默认值中文环境下为 false，须显式打开。
                    text_auto_wrap = true,
                    lineSpacing = lineSpacing,
                });

                block = bxDesc.Get(DescBlockName) as FillBlock;
            }

            FitText(bxDesc, block);

            // x 贴主面板右外侧，y 跟随当前悬停行，与原版算法一致。
            UiBoxDesigner box = cfg.BxOut;
            Vector3 local = button.get_Skin().getLocalPosFromContainer();
            float x = box.swidth / 2f + 190f + 4f + box.getBox().get_deperture_x();
            float y = local.y * PixelsPerUnit + box.getBox().get_deperture_y();

            if (bxDesc.isActive())
            {
                bxDesc.position(x, y);
            }
            else
            {
                bxDesc.activate();
                bxDesc.positionD(x, y, 2, 40f);
            }

            return true;
        }

        /// <summary>
        /// 把说明文字缩到框里；原版没有这一步，因原版说明都很短。模组填的说明长度不受控，放不下时游戏既不裁剪也不缩放，会溢出糊住界面，故只缩字号（不改框，从原版字号重新起步而非累积缩小）。
        /// </summary>
        static void FitText(UiBoxDesigner bxDesc, FillBlock block)
        {
            if (block == null)
            {
                return;
            }

            float available = bxDesc.use_h;
            if (available <= 0f)
            {
                return;
            }

            float size = DescTextSize;
            block.size = size;

            // 缩小字号使高度单调下降，循环必收敛，最多四步。
            while (size > DescMinTextSize && API.TextMetrics.TextHeightOf(block) > available)
            {
                size = Math.Max(DescMinTextSize, size - DescSizeStep);
                block.size = size;
            }
        }
    }
}
