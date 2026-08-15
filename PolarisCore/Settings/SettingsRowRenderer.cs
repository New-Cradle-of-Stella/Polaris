using System;
using nel;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 把一个 <see cref="SettingDefinition"/> 画成原版设置界面里的一行：标签 + 控件。
    /// 控件全部加在 <c>cfg.BxOut</c> 上，因委托运行期间它等同主标签页，且名字须注册进检索表供 <see cref="Sync"/> 用 <c>Get(name)</c> 找回。
    /// </summary>
    internal static class SettingsRowRenderer
    {
        /// <summary>原版标签宽度；标签与控件同处一行，靠这个宽度对齐。</summary>
        const float LabelWidth = 140f;

        /// <summary>
        /// 原版设置行 setter（值显示区）宽度，按形态分三档（见 <see cref="Meter"/>），不能混用——超宽会被 <c>addSliderCT</c> 自动换行，塌了排版。
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

                // ChoiceSetting 与 EnumSetting<T> 共用：选项少用 checkbox，多用左右箭头形态。
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
            // 三档宽度须成对取，见 SetterWidthCheckbox。
            (float width, float setter) = checkbox
                ? (cfg.sliderw_sml, SetterWidthCheckbox)
                : keys != null
                    ? (cfg.sliderw_middle, SetterWidthChoices)
                    : (cfg.sliderw, SetterWidthNumeric);

            // 一行数值控件是两个块（meter 本体 + CtSetterMeter），须一起登记，否则过滤后剩半行。
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
            // DsnDataInput 无 fnHover 字段，文本行不会弹右侧说明框。
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

        /// <summary>把设置项当前值推回控件显示；用 <c>setValue</c> 而非 <c>setValueAndCallFunc</c>，因这是同步显示不是玩家改值。</summary>
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
