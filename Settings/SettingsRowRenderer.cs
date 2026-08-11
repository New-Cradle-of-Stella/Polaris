using System;
using nel;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 把一个 <see cref="SettingDefinition"/> 画成原版设置界面里的一行。
    /// 一行 = 一个标签 <see cref="DsnDataP"/> + 一个控件，与原版
    /// <c>UiCFG.createBoxDesignerContentMain</c> 里 <c>P("Config_Xxx").addSliderCT(...)</c> 同构。
    /// <para>
    /// 控件全部加在 <c>cfg.BxOut</c> 上而不是委托参数给的那个 tab Designer：委托是在
    /// <c>BxOut.addTab(...)</c> 与 <c>BxOut.endTab()</c> 之间跑的，此时往 BxOut 上加就是往主标签页里加，
    /// 而且名字会注册到 BxOut 的检索表里——<see cref="Sync"/> 要靠 <c>BxOut.Get(name)</c> 找回控件。
    /// </para>
    /// </summary>
    internal static class SettingsRowRenderer
    {
        /// <summary>原版标签宽度；标签与控件同处一行，靠这个宽度对齐。</summary>
        const float LabelWidth = 140f;

        /// <summary>
        /// 原版设置行的 setter（值显示区）宽度，抄自 <c>createBoxDesignerContentMain</c>。
        /// 原版按行的形态分成三档，控件本体的宽度也跟着走（见 <see cref="Meter"/>）：
        /// checkbox（"窗口模式"）214、左右箭头选项（"窗口大小"）154、纯数值（音量条）用默认的 114。
        /// 三档不能混：<c>addSliderCT</c> 发现"控件宽 + setter 宽"超出可用宽度会自己插一个
        /// <c>Br()</c> 把 setter 挤到下一行，看起来就是那一行的排版塌了。
        /// </summary>
        const float SetterWidthCheckbox = 214f;
        const float SetterWidthChoices = 154f;
        const float SetterWidthNumeric = 114f;

        /// <summary>超过这个选项数就从 checkbox 形态换成左右箭头形态（原版"窗口大小"就是后者）。</summary>
        const int CheckboxMaxChoices = 2;

        /// <param name="row">
        /// 本行画出来的每一个块都要登记进去，搜索过滤靠它整行收放；见 <see cref="SettingsSearchFilter"/>。
        /// </param>
        internal static void Render(UiCFG cfg, UiBoxDesigner box, SettingDefinition setting,
                                    SettingsSearchFilter.RowRecorder row)
        {
            switch (setting)
            {
                case ToggleSetting s:
                    Label(box, s, row);
                    Meter(cfg, box, s, row, s.Value ? 1f : 0f, 0f, 1f, 1f,
                          checkbox: true, keys: s.DisplayStateLabels,
                          onChanged: cur => s.Value = cur >= 0.5f);
                    break;

                case SliderSetting s:
                    Label(box, s, row);
                    Meter(cfg, box, s, row, s.Value, s.Min, s.Max, s.Step,
                          checkbox: false, keys: null,
                          onChanged: cur => s.Value = cur);
                    break;

                case IntSetting s:
                    Label(box, s, row);
                    Meter(cfg, box, s, row, s.Value, s.Min, s.Max, s.Step,
                          checkbox: false, keys: null,
                          onChanged: cur => s.Value = (int)Math.Round(cur));
                    break;

                // ChoiceSetting 与 EnumSetting<T> 共用这一条：选项少时用 checkbox 形态，
                // 多时用原版"窗口大小"那种左右箭头形态。
                case IChoiceSetting c:
                    Label(box, setting, row);
                    bool useCheckbox = c.Choices.Length <= CheckboxMaxChoices;
                    Meter(cfg, box, setting, row, c.SelectedIndex, 0f, c.Choices.Length - 1, 1f,
                          checkbox: useCheckbox, keys: c.DisplayChoices,
                          onChanged: cur => c.SelectedIndex = (int)Math.Round(cur));
                    break;

                case TextSetting s:
                    TextField(box, s, row);
                    break;

                default:
                    Plugin.Logger.LogWarning($"[Polaris.Settings] Unrecognized setting type {setting.GetType().Name}; skipped.");
                    break;
            }
        }

        /// <summary>所有带数值的行最终都落到这一个原版 meter 控件上，区别只在 checkbox_mode 与宽度。</summary>
        static void Meter(UiCFG cfg, UiBoxDesigner box, SettingDefinition s,
                          SettingsSearchFilter.RowRecorder row,
                          float current, float min, float max, float step,
                          bool checkbox, string[] keys, Action<float> onChanged)
        {
            // 三档宽度必须成对取，理由见 SetterWidthCheckbox 的注释。
            (float width, float setter) = checkbox
                ? (cfg.sliderw_sml, SetterWidthCheckbox)
                : keys != null
                    ? (cfg.sliderw_middle, SetterWidthChoices)
                    : (cfg.sliderw, SetterWidthNumeric);

            // 一行数值控件其实是两个块：meter 本体 + 右侧的值显示区（CtSetterMeter），
            // 搜索过滤要两个一起收，少登记一个就会在过滤后剩下半行。
            aBtnMeterNel meter = box.addSliderCT(new DsnDataSlider
            {
                name = s.RowKey,
                title = s.RowKey,
                skin_title = "",
                checkbox_mode = (byte)(checkbox ? 1 : 0),
                def = current,
                mn = min,
                mx = max,
                valintv = step,
                w = width,
                Adesc_keys = keys,
                fnChanged = (_, _, cur) =>
                {
                    onChanged(cur);
                    return true;
                },
                fnHover = button => PolarisSettingsScreen.ShowDescription(cfg, button, s.DisplayDescription),
            }, setter);

            row.Add(meter);
            row.Add(meter.getCtSetter());
        }

        static void TextField(UiBoxDesigner box, TextSetting s, SettingsSearchFilter.RowRecorder row)
        {
            Label(box, s, row);
            // DsnDataInput 没有 fnHover 字段，所以文本行不会弹右侧说明框——
            // 说明只能写进标签里，或者靠上方的分区标题交代。
            row.Add(box.addInput(new DsnDataInput
            {
                name = s.RowKey,
                label = "",
                def = s.Value ?? "",
                w = s.Width,
                max_len = s.MaxLength,
                fnChangedDelay = fld =>
                {
                    s.Value = fld.text;
                    return true;
                },
            }));
        }

        /// <summary>行标签。名字沿用原版 "P_Config_" + 控件名 的约定，这样原版 <c>setMeterEnable</c> 能连标签一起置灰。</summary>
        static void Label(UiBoxDesigner box, SettingDefinition s, SettingsSearchFilter.RowRecorder row)
        {
            row.Add(PolarisSettingsScreen.Caption(box, s.DisplayLabel, "P_Config_" + s.RowKey, LabelWidth));
        }

        /// <summary>
        /// 把设置项的当前值推回控件显示。原版的 <c>UiCFG</c> 只 new 一次、之后一直 <c>resume()</c> 复用，
        /// 所以两次打开设置界面之间模组自己改了值的话，只能靠这个把界面拨正。
        /// 用 <c>setValue</c> 而不是 <c>setValueAndCallFunc</c>：这是"同步显示"，不是"玩家改了值"。
        /// </summary>
        internal static void Sync(UiCFG cfg, SettingDefinition setting)
        {
            IVariableObject widget = cfg.BxOut.Get(setting.RowKey);

            switch (setting)
            {
                case ToggleSetting s when widget is aBtnMeter m:
                    m.setValue(s.Value ? 1f : 0f);
                    break;
                case IChoiceSetting c when widget is aBtnMeter m:
                    m.setValue(c.SelectedIndex);
                    break;
                case SliderSetting s when widget is aBtnMeter m:
                    m.setValue(s.Value);
                    break;
                case IntSetting s when widget is aBtnMeter m:
                    m.setValue(s.Value);
                    break;
                case TextSetting s when widget is LabeledInputField f:
                    f.setValue(s.Value ?? "", call_changed_delay: false);
                    break;
            }
        }
    }
}
