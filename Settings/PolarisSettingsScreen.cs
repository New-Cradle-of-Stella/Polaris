using System;
using System.Collections.Generic;
using nel;
using UnityEngine;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 把已注册的设置项追加到原版设置界面（<c>nel.UiCFG</c>）主标签页的尾部，
    /// 并负责右侧说明框。挂接点是 <c>UiCFG</c> 官方留的 <c>FnCfgTabCreateAfter</c> 委托，
    /// 由 <see cref="Patch_UiCFG_Constructor"/> 链上去——不是 transpiler，也不改原版任何一行 IL。
    /// </summary>
    internal static class PolarisSettingsScreen
    {
        /// <summary>
        /// 说明框正文的检索名。刻意与原版的 <c>__CFG_DESC_P</c> 区分开：原版那个块建的时候
        /// <c>html = true</c>，复用它就意味着模组填的说明里出现 '&lt;' 会被富文本解析器吃掉。
        /// 用自己的名字，代价只是在原版行与 Polaris 行之间来回悬停时各重建一次文本块（很便宜）。
        /// </summary>
        const string DescBlockName = "__PLRS_CFG_DESC_P";

        /// <summary>说明框尺寸，与原版 <c>UiCFG.desc_w/desc_h</c> 一致。</summary>
        const float DescW = 380f;
        const float DescH = 120f;
        const float DescMarginLR = 40f;
        const float DescTextSize = 16f;

        /// <summary>
        /// 说明文字缩到放得下为止的下限与每次缩的幅度。
        /// <para>
        /// 12 这个下限是权衡出来的：再小就不好认了，而放不下也总比看不清强——真到了下限还溢出，
        /// 那份说明本来就该写短一点（原版自己的说明都在两三行以内）。
        /// </para>
        /// </summary>
        const float DescMinTextSize = 12f;
        const float DescSizeStep = 1f;

        /// <summary>行数多到这个程度就收紧行距，抄的原版阈值。</summary>
        const int DenseLineThreshold = 6;
        const float LineSpacingDense = 0.94f;
        const float LineSpacingLoose = 1.16f;

        /// <summary>原版文字色；<c>UiCFG.P</c> 的标签和 <c>fnShowDesc</c> 的说明框用的是同一个值。</summary>
        const uint TextColor = 4283780170u;

        /// <summary>Unity 单位与界面像素的换算：1 单位 = 64 像素。</summary>
        const float PixelsPerUnit = 64f;

        /// <summary>当前这个 UiCFG 实例上都画了哪些设置项，供 <see cref="Sync"/> 回拨界面。</summary>
        static readonly List<SettingDefinition> rendered = [];

        /// <summary>
        /// 在主标签页尾部追加所有已注册的分区。由 <c>UiCFG.createBoxDesignerContentMain</c>
        /// 末尾的委托调用，此时 <c>BxOut</c> 还处在 addTab/endTab 之间，往它上面加就是往主标签页加。
        /// </summary>
        internal static void Append(UiCFG cfg)
        {
            rendered.Clear();
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
                    GroupHeader(box, group);
                    foreach (SettingDefinition setting in group.Settings)
                    {
                        SettingsRowRenderer.Render(cfg, box, setting);
                        rendered.Add(setting);
                    }
                }
                catch (Exception e)
                {
                    // 一个模组的设置项画崩了不能连累整个设置界面——原版行已经画完了，
                    // 这里抛出去会让 createBoxDesignerContentMain 半途夭折。
                    // SettingGroup 不携带程序集信息（ModId 只是个字符串），责任方交给堆栈推断。
                    PolarisAPI.Errors.Report(e, $"渲染 {group.ModId} 的设置项");
                    Plugin.Logger.LogError($"[Polaris.Settings] 渲染 {group.ModId} 的设置项失败，已忽略。");
                }
            }

            Plugin.Logger.LogInfo($"[Polaris.Settings] 已向设置界面追加 {groups.Count} 组、{rendered.Count} 个设置项。");
        }

        /// <summary>分区标题：一条分隔线 + 一行居中文字，与原版的行样式同色系。</summary>
        static void GroupHeader(UiBoxDesigner box, SettingGroup group)
        {
            box.Hr(0.94f, 16f, 8f);
            Caption(box, group.DisplayTitle, "P_PLRS_GROUP_" + group.ModId, box.use_w);
            box.Br();
        }

        /// <summary>
        /// 一行文字，逐字段复刻原版 <c>UiCFG.P()</c> 的样式；分区标题和设置行标签共用。
        /// <para>
        /// 不能直接调 <c>UiCFG.P()</c>：它强制把参数当本地化键走 <c>TX.Get</c>，
        /// 而 <c>TX.Get</c> 未命中是静默返回空串的，模组给的字面量会画成一片空白。
        /// 唯一的差异就在取文案这一步——这里收的是已经求过值的文案（是不是本地化键、查不到时
        /// 显示什么，都由 <see cref="Localization.LocalizationAPI.Text"/> 决定）。
        /// </para>
        /// </summary>
        /// <param name="width">文字块宽度：标签用固定的标签栏宽度，分区标题铺满整行</param>
        internal static void Caption(UiBoxDesigner box, string text, string name, float width)
        {
            box.Br().addP(new DsnDataP
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
                // 原版这里是 (auto_wrap || X.ENG_MODE)；我们恒 false，因为文案是第三方填的，
                // 富文本解析器会把里面的 '<' 吃掉（同 PolarisModDetailPopup 的处理）。
                html = false,
            });
        }

        /// <summary>
        /// 把设置项的当前值推回控件。<c>UiCFG</c> 实例是复用的，两次打开之间模组自己改了值
        /// （或者上次是"取消"退出的）都要靠这个把界面拨正。
        /// </summary>
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
                    Plugin.Logger.LogWarning($"[Polaris.Settings] 同步 {setting.RowKey} 的控件显示失败：{e}");
                }
            }
        }

        /// <summary>
        /// 右侧说明框，复刻原版 <c>UiCFG.fnShowDesc</c>：没有说明就收起，有就把文本块挪到当前行的高度。
        /// 和原版一样只挂 <c>fnHover</c> 不挂 <c>fnOut</c>——鼠标移开后保留最后悬停的那条。
        /// </summary>
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
                    // 默认值是 TX.isEnglishLang()，中文环境下为 false，必须显式打开。
                    text_auto_wrap = true,
                    lineSpacing = lineSpacing,
                });

                block = bxDesc.Get(DescBlockName) as FillBlock;
            }

            FitText(bxDesc, block);

            // x 固定贴在主面板右外侧，y 跟随当前悬停的那一行——与原版 fnShowDesc 的算法一致。
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
        /// 把说明文字缩到框里。<b>原版没有这一步</b>——它的说明都是自己写的、都在两三行以内，
        /// 塞不下这件事不会发生。模组填的说明长度不受任何人控制，而文本块放不下时游戏既不裁剪
        /// 也不缩放：<c>aligny = MIDDLE</c> 让它以框的中线为准往上下两头一起溢出，糊在设置界面
        /// 和标题画面上。宁可字小一点，也不能让一个模组的说明糊住半个屏幕。
        /// <para>
        /// 只动字号、不动框：说明框的位置是贴着当前悬停那一行算出来的（见 <see cref="ShowDescription"/>
        /// 末尾），把框改高就得连同这个位置一起重算，还要防着算到屏幕外面去；而字号是
        /// <c>FillBlock</c> 的公开属性，改完自己会重新排版换行。
        /// </para>
        /// <para>
        /// 每次都从原版字号重新起步，不是在上一条的结果上接着缩——否则悬停过一条长说明之后，
        /// 后面每一条短说明都会跟着一直小下去。
        /// </para>
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

            // 缩小字号会让每行装下更多字、总行数变少，高度只会跟着降，所以这个循环一定收敛；
            // 从 16 缩到 12 最多走四步，一次悬停付得起。
            while (size > DescMinTextSize && API.TextMetrics.TextHeightOf(block) > available)
            {
                size = Math.Max(DescMinTextSize, size - DescSizeStep);
                block.size = size;
            }
        }
    }
}
