using System;
using BepInEx.Configuration;
using Polaris.Localization;

namespace Polaris.Settings
{
    /// <summary>
    /// 一个设置项的 UI 无关描述，渲染层和存储层都只认这个模型。
    /// <see cref="Label"/>/<see cref="Description"/> 存的是原始串，遵守 <see cref="LocalizedString"/> 的 <c>&amp;</c> 约定；求值推迟到 <c>Display*</c> 属性，因注册发生在 <c>Plugin.Start</c>，此时语言表未必已建好。
    /// </summary>
    public abstract class SettingDefinition
    {
        /// <summary>组内唯一，直接作为配置文件里的键。一旦发布就别再改：改了等于把用户的设置重置。</summary>
        public string Id { get; }

        /// <summary>界面上的行标签（原始串，可能是 <c>&amp;</c> 开头的本地化键）。</summary>
        public string Label { get; }

        /// <summary>悬停说明框文字（原始串，可能是本地化键）；为 null/空串则说明框收起。</summary>
        public string Description { get; internal set; }

        /// <summary>按当前语言求值之后的行标签。</summary>
        public string DisplayLabel => PolarisAPI.Localization.Text(Label);

        /// <summary>按当前语言求值之后的说明文字；<see cref="Description"/> 为 null 时同样是 null。</summary>
        public string DisplayDescription => PolarisAPI.Localization.Text(Description);

        /// <summary>由 <see cref="SettingGroup.Add"/> 回填。</summary>
        internal SettingGroup Group { get; set; }

        private protected SettingDefinition(string id, string label)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Setting Id cannot be empty", nameof(id));
            }

            Id = id;
            Label = string.IsNullOrEmpty(label) ? id : label;
        }

        /// <summary>控件注册名/<c>DsnData.title</c>；须带前缀以免撞上原版按 <c>aBtn.title</c> 做 switch 的分支。</summary>
        internal string RowKey => "plrs:" + Group?.ModId + ":" + Id;
    }

    /// <summary>带值、需要持久化的设置项。非泛型层供存储/渲染以 <see cref="object"/> 统一处理。</summary>
    public abstract class ValueSettingDefinition : SettingDefinition
    {
        private protected ValueSettingDefinition(string id, string label, object defaultValue)
            : base(id, label)
        {
            DefaultValue = defaultValue;
        }

        public object DefaultValue { get; }

        public abstract Type ValueType { get; }

        /// <summary>值的真身是模组的静态字段，须与 <see cref="Entry"/> 同步；由 <see cref="SettingsAttributeScanner"/> 挂上。</summary>
        internal Action<object> FieldSetter;

        /// <summary>由 <see cref="SettingsStore"/> 在绑定时回填；未绑定时读写退化到默认值。</summary>
        internal ConfigEntryBase Entry;

        object fallback;

        /// <summary>值变化时触发（拖动滑块的每一步都会触发，与原版"改动即时生效"一致）。</summary>
        public event Action<object> Changed;

        public object BoxedValue
        {
            get => Entry != null ? Entry.BoxedValue : (fallback ?? DefaultValue);
            set => Apply(value, notify: true);
        }

        /// <summary>写值并同步到字段。<paramref name="notify"/> 为 false 时不触发 <see cref="Changed"/>（加载时用）。</summary>
        internal void Apply(object value, bool notify)
        {
            if (Entry != null)
            {
                Entry.BoxedValue = value;
            }
            else
            {
                fallback = value;
            }

            try
            {
                FieldSetter?.Invoke(value);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"[Polaris.Settings] Failed to write back the field of {RowKey}: {e}");
            }

            if (!notify)
            {
                return;
            }

            try
            {
                Changed?.Invoke(value);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"[Polaris.Settings] A Changed subscriber of {RowKey} threw an exception: {e}");
            }
        }

        /// <summary>由 <see cref="SettingsStore"/> 调用；子类用自己的具体类型走 <c>ConfigFile.Bind&lt;T&gt;</c>。</summary>
        internal abstract ConfigEntryBase BindTo(ConfigFile file, string section);
    }

    /// <summary>带值设置项的强类型层，给模组作者用。</summary>
    public abstract class ValueSettingDefinition<T> : ValueSettingDefinition
    {
        private protected ValueSettingDefinition(string id, string label, T defaultValue)
            : base(id, label, defaultValue) { }

        public override Type ValueType => typeof(T);

        public T Value
        {
            get => BoxedValue is T typed ? typed : (T)DefaultValue;
            set => BoxedValue = value;
        }

        internal override ConfigEntryBase BindTo(ConfigFile file, string section)
            => file.Bind(section, Id, (T)DefaultValue, ConfigComment);

        /// <summary>写进 .cfg 的注释，须压成一行——带换行会让 BepInEx 逐行写出的注释产生不合法配置行。</summary>
        string ConfigComment
        {
            get
            {
                // 用求值后的文案，手改 .cfg 的玩家应看到"严格模式"而非键名。
                string text = string.IsNullOrEmpty(Description) ? DisplayLabel : DisplayDescription;
                return text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
            }
        }
    }

    /// <summary>开关。渲染成原版的 checkbox 型 meter（<c>checkbox_mode = 1</c> + 两个 desc key）。</summary>
    public sealed class ToggleSetting : ValueSettingDefinition<bool>
    {
        internal ToggleSetting(string id, string label, bool def) : base(id, label, def) { }

        /// <summary>关/开两态的显示文案（原始串），缺省 "OFF"/"ON"。</summary>
        public string[] StateLabels { get; internal set; } = ["OFF", "ON"];

        /// <summary>按当前语言求值之后的两态文案。</summary>
        public string[] DisplayStateLabels => PolarisAPI.Localization.TextAll(StateLabels);
    }

    /// <summary>浮点滑条。原版音量条就是这个形态（只是它用 0..100 的整数刻度）。</summary>
    public sealed class SliderSetting : ValueSettingDefinition<float>
    {
        internal SliderSetting(string id, string label, float def) : base(id, label, def) { }

        public float Min { get; internal set; }
        public float Max { get; internal set; } = 1f;
        public float Step { get; internal set; } = 0.1f;
    }

    /// <summary>整数滑条，与 <see cref="SliderSetting"/> 走同一个 meter 控件，<c>valintv</c> 固定为步长。</summary>
    public sealed class IntSetting : ValueSettingDefinition<int>
    {
        internal IntSetting(string id, string label, int def) : base(id, label, def) { }

        public int Min { get; internal set; }
        public int Max { get; internal set; } = 100;
        public int Step { get; internal set; } = 1;
    }

    /// <summary>多选一设置项的非泛型视角，让渲染层只按"选项文案 + 当前下标"操作，与具体值类型（下标或枚举）无关。</summary>
    internal interface IChoiceSetting
    {
        string[] Choices { get; }

        /// <summary>按当前语言求值之后的选项文案，长度与 <see cref="Choices"/> 一致。</summary>
        string[] DisplayChoices { get; }

        /// <summary>越界的下标一律忽略，调用方不必自己夹取值范围。</summary>
        int SelectedIndex { get; set; }
    }

    /// <summary>多选一，值是选项下标。渲染成原版的左右箭头选择器（<c>Adesc_keys</c> + CtSetterMeter）。</summary>
    public sealed class ChoiceSetting : ValueSettingDefinition<int>, IChoiceSetting
    {
        internal ChoiceSetting(string id, string label, string[] choices, int def)
            : base(id, label, def)
        {
            Choices = choices;
        }

        public string[] Choices { get; }

        string[] IChoiceSetting.DisplayChoices => PolarisAPI.Localization.TextAll(Choices);

        int IChoiceSetting.SelectedIndex
        {
            get => Value;
            set
            {
                if (value >= 0 && value < Choices.Length)
                {
                    Value = value;
                }
            }
        }
    }

    /// <summary>枚举，值就是枚举本身（配置文件里存枚举名，比存下标更抗改动）。</summary>
    public sealed class EnumSetting<TEnum> : ValueSettingDefinition<TEnum>, IChoiceSetting
        where TEnum : struct, Enum
    {
        internal EnumSetting(string id, string label, TEnum def) : base(id, label, def)
        {
            Values = (TEnum[])Enum.GetValues(typeof(TEnum));
            Choices = Enum.GetNames(typeof(TEnum));
        }

        public TEnum[] Values { get; }

        /// <summary>选项显示文案（原始串），缺省是枚举名。</summary>
        public string[] Choices { get; internal set; }

        string[] IChoiceSetting.DisplayChoices => PolarisAPI.Localization.TextAll(Choices);

        int IChoiceSetting.SelectedIndex
        {
            // 手改配置为已删除的枚举名时 IndexOf 给 -1，退回第一项而非空白界面。
            get => Math.Max(0, Array.IndexOf(Values, Value));
            set
            {
                if (value >= 0 && value < Values.Length)
                {
                    Value = Values[value];
                }
            }
        }
    }

    /// <summary>文本输入。</summary>
    public sealed class TextSetting : ValueSettingDefinition<string>
    {
        internal TextSetting(string id, string label, string def) : base(id, label, def ?? "") { }

        public int MaxLength { get; internal set; } = -1;
        public float Width { get; internal set; } = 220f;
    }
}
