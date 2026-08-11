using System;
using System.Reflection;

namespace Polaris.Settings
{
    /// <summary>
    /// 构造设置项的内部接口。<b>不对模组作者开放</b>——对外只有
    /// <see cref="PolarisSettingAttribute"/> 一条声明途径，好处是注册时机由 Polaris 统一掌控，
    /// 模组不可能因为注册晚于设置界面而踩空（见 <see cref="SettingsAPI"/> 的说明）。
    /// <see cref="SettingsAttributeScanner"/> 把扫到的字段翻译成对这里的调用，
    /// 取值范围校验、默认值裁剪都集中在这一层。
    /// <para>
    /// 各方法返回刚建好的设置项，调用方拿去挂字段读写委托与变更回调。
    /// </para>
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
                throw new ArgumentException($"设置项 {id} 的选项列表不能为空", nameof(choices));
            }

            return Add(new ChoiceSetting(id, label, choices, Math.Min(Math.Max(def, 0), choices.Length - 1)), desc);
        }

        /// <summary>
        /// 枚举的非泛型入口：扫描器只拿得到 <see cref="Type"/>，而 <see cref="EnumSetting{TEnum}"/>
        /// 要求编译期类型实参，中间只能靠反射搭一次桥。
        /// <para>
        /// 这层桥刻意放在 <see cref="Enum{TEnum}"/> 旁边而不是扫描器里：实参数组和被调方法的签名
        /// 必须逐字对应，隔着文件放的话签名一改另一边就对不上——编译器看不见，
        /// 只有运行时才炸成 "Number of parameters specified does not match the expected number."
        /// </para>
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
                // 校验异常经反射会被包一层，而包装层的 Message 没有任何信息量
                // （"Exception has been thrown by the target of an invocation."），拆开再抛。
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
                // 成员数为 0 的枚举会让 meter 拿到 mx = -1，画出来是坏的；和 Choice 一样提前拦下。
                throw new ArgumentException($"设置项 {id} 的枚举 {typeof(TEnum).Name} 没有任何成员");
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
                        $"[Polaris.Settings] {group.ModId}.{id} 的 Choices 长度({choices.Length})与枚举成员数" +
                        $"({s.Values.Length})不符，改用枚举名。");
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

        /// <summary>
        /// 提交注册。此时会立刻绑定配置文件并把上次存的值回灌回来——
        /// 也就是说 <see cref="Register"/> 一返回，字段就已经是玩家上次退出时的值了。
        /// </summary>
        public SettingGroup Register() => PolarisAPI.Settings.Register(group);

        T Add<T>(T setting, string desc) where T : ValueSettingDefinition
        {
            setting.Description = desc;
            group.Add(setting);
            return setting;
        }
    }
}
