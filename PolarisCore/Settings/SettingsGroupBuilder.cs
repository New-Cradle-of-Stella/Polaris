using System;
using System.Reflection;

namespace Polaris.Settings
{
    /// <summary>
    /// 构造设置项的内部接口，不对模组作者开放（对外唯一途径是 <see cref="PolarisSettingAttribute"/>）。
    /// <see cref="SettingsAttributeScanner"/> 把扫到的字段翻译成对这里的调用；取值范围校验、默认值裁剪集中在这一层。
    /// </summary>
    internal sealed class SettingsGroupBuilder
    {
        readonly SettingGroup group;

        internal SettingsGroupBuilder(SettingGroup group) => this.group = group;

        /// <summary>开关。</summary>
        /// <param name="stateLabels">关/开两态的文案，缺省 ["OFF", "ON"]</param>
        public ToggleSetting Toggle(string id, string label, bool def = false, string desc = null,
                                    string[] stateLabels = null)
        {
            var s = new ToggleSetting(id, label, def);
            if (stateLabels is { Length: 2 })
            {
                s.StateLabels = stateLabels;
            }

            return Add(s, desc);
        }

        /// <summary>浮点滑条。</summary>
        public SliderSetting Slider(string id, string label, float min, float max, float def,
                                    float step = 0.1f, string desc = null)
        {
            return Add(new SliderSetting(id, label, Math.Min(Math.Max(def, min), max))
            {
                Min = min,
                Max = max,
                Step = step <= 0f ? 0.1f : step,
            }, desc);
        }

        /// <summary>整数滑条。</summary>
        public IntSetting Int(string id, string label, int min, int max, int def,
                              int step = 1, string desc = null)
        {
            return Add(new IntSetting(id, label, Math.Min(Math.Max(def, min), max))
            {
                Min = min,
                Max = max,
                Step = step <= 0 ? 1 : step,
            }, desc);
        }

        /// <summary>多选一，值是选项下标。</summary>
        public ChoiceSetting Choice(string id, string label, string[] choices, int def = 0,
                                    string desc = null)
        {
            if (choices == null || choices.Length == 0)
            {
                throw new ArgumentException($"The choice list of setting {id} cannot be empty", nameof(choices));
            }

            return Add(new ChoiceSetting(id, label, choices, Math.Min(Math.Max(def, 0), choices.Length - 1)), desc);
        }

        /// <summary>
        /// 枚举的非泛型入口：扫描器只有 <see cref="Type"/>，而 <see cref="EnumSetting{TEnum}"/> 需要编译期类型实参，靠反射搭桥。
        /// 保持在 <see cref="Enum{TEnum}"/> 旁边，因反射调用的实参数组须与其签名逐字对应。
        /// </summary>
        internal ValueSettingDefinition EnumOfType(Type enumType, string id, string label, object def,
                                                   string[] choices, string desc)
        {
            MethodInfo generic = typeof(SettingsGroupBuilder)
                .GetMethod(nameof(Enum))
                .MakeGenericMethod(enumType);

            try
            {
                return (ValueSettingDefinition)generic.Invoke(this, [id, label, def, choices, desc]);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                // 反射会包一层无信息量的异常，拆开再抛。
                throw e.InnerException;
            }
        }

        /// <summary>枚举，配置文件里存枚举名。</summary>
        /// <param name="choices">选项显示文案，缺省用枚举名；长度必须和枚举成员数一致</param>
        public EnumSetting<TEnum> Enum<TEnum>(string id, string label, TEnum def, string[] choices = null,
                                              string desc = null)
            where TEnum : struct, System.Enum
        {
            var s = new EnumSetting<TEnum>(id, label, def);
            if (s.Values.Length == 0)
            {
                // 空枚举会让 meter 拿到 mx = -1 画坏，提前拦下。
                throw new ArgumentException($"The enum {typeof(TEnum).Name} of setting {id} has no members");
            }

            if (choices != null)
            {
                if (choices.Length == s.Values.Length)
                {
                    s.Choices = choices;
                }
                else
                {
                    Plugin.Logger.LogWarning(
                        $"[Polaris.Settings] The Choices length ({choices.Length}) of {group.ModId}.{id} does not match the enum member count" +
                        $" ({s.Values.Length}); falling back to enum names.");
                }
            }

            return Add(s, desc);
        }

        /// <summary>文本输入。</summary>
        public TextSetting Text(string id, string label, string def = "", int maxLength = -1,
                                float width = 220f, string desc = null)
        {
            return Add(new TextSetting(id, label, def) { MaxLength = maxLength, Width = width }, desc);
        }

        /// <summary>提交注册；返回时字段已回灌为玩家上次退出时的值。</summary>
        public SettingGroup Register() => PolarisAPI.Settings.Register(group);

        T Add<T>(T setting, string desc) where T : ValueSettingDefinition
        {
            setting.Description = desc;
            group.Add(setting);
            return setting;
        }
    }
}
