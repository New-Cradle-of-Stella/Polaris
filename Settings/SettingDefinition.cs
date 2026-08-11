using System;
using BepInEx.Configuration;
using Polaris.Localization;

namespace Polaris.Settings
{
    /// <summary>
    /// 一个设置项的 UI 无关描述。渲染层（<see cref="SettingsRowRenderer"/>）和存储层
    /// （<see cref="SettingsStore"/>）都只认这个模型，不认识 nel/XX 里的任何类型——
    /// 换句话说，把设置项挪到别的界面去渲染不需要改模型。
    /// <para>
    /// 所有给玩家看的字符串（<see cref="Label"/>、<see cref="Description"/>、选项文案）都遵守
    /// <see cref="LocalizedString"/> 的 <c>&amp;</c> 约定：以 <c>&amp;</c> 开头就是本地化键，
    /// 显示时经 <see cref="LocalizationAPI.Text"/> 查表。模型层存的永远是<b>原始串</b>，
    /// 求值一律推迟到 <c>Display*</c> 这几个属性，绝不在注册时算好存下来：设置项是在
    /// <c>Plugin.Start</c> 注册的，那时游戏的语言表未必已经建好，算出来的多半是键本身。
    /// </para>
    /// <para>
    /// 每次界面构造都会重新求值一遍，所以模组自己在运行中改了内置文案、或者语言在设置界面
    /// 构造之前变了，下一次都能取到新的。<b>已经画出来的那一份不会自己变</b>——原版的
    /// <c>UiCFG</c> 只 new 一次之后一直 <c>resume()</c> 复用，它自己的行也是同样的表现，
    /// 这里不额外为此重建控件。
    /// </para>
    /// </summary>
    public abstract class SettingDefinition
    {
        /// <summary>组内唯一，直接作为配置文件里的键。一旦发布就别再改：改了等于把用户的设置重置。</summary>
        public string Id { get; }

        /// <summary>界面上的行标签（原始串，可能是 <c>&amp;</c> 开头的本地化键）。</summary>
        public string Label { get; }

        /// <summary>
        /// 悬停时显示在右侧说明框里的文字（原始串，可能是 <c>&amp;</c> 开头的本地化键）。
        /// 为 null 或空串则说明框收起——与原版没有 <c>Config_desc_*</c> 条目的行表现一致。
        /// </summary>
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
                throw new ArgumentException("设置项 Id 不能为空", nameof(id));
            }

            Id = id;
            Label = string.IsNullOrEmpty(label) ? id : label;
        }

        /// <summary>
        /// 控件在 Designer 里的注册名，同时用作 <c>DsnData.title</c>。
        /// 必须带前缀：原版 <c>UiCFG.changeConfigValue</c> 与 <c>fnShowDesc</c> 都是按
        /// <c>aBtn.title</c> 做 switch 的，不加前缀就有撞上原版分支的风险。
        /// </summary>
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

        /// <summary>
        /// 值的"真身"是模组自己的静态字段，<see cref="Entry"/> 只是它的存档，两边必须同步：
        /// 加载时 Entry→字段，玩家改动时也要写回字段。由 <see cref="SettingsAttributeScanner"/> 挂上。
        /// </summary>
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
                Plugin.Logger.LogWarning($"[Polaris.Settings] 回写 {RowKey} 的字段失败：{e}");
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
                Plugin.Logger.LogWarning($"[Polaris.Settings] {RowKey} 的 Changed 订阅者抛异常：{e}");
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

        /// <summary>
        /// 写进 .cfg 的注释。必须压成一行：BepInEx 是按 <c>## &lt;文本&gt;</c> 逐行写注释的，
        /// 文案里带换行会让第二行往后变成不合法的配置行，重启后解析直接出错。
        /// </summary>
        string ConfigComment
        {
            get
            {
                // 用求值后的文案：手改 .cfg 的玩家看到的应该是"严格模式"，不是"&polarisres.strict"。
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

    /// <summary>
    /// 整数滑条。与 <see cref="SliderSetting"/> 走同一个 meter 控件、<c>valintv</c> 固定为步长，
    /// 原版的"窗口大小"就是这么做的——游戏里没有独立的数字输入框控件值得为此单开一条渲染路径。
    /// </summary>
    public sealed class IntSetting : ValueSettingDefinition<int>
    {
        internal IntSetting(string id, string label, int def) : base(id, label, def) { }

        public int Min { get; internal set; }
        public int Max { get; internal set; } = 100;
        public int Step { get; internal set; } = 1;
    }

    /// <summary>
    /// 多选一设置项的非泛型视角：渲染层只按"选项文案 + 当前下标"操作，不必关心值到底是
    /// 下标本身还是枚举成员。<see cref="EnumSetting{TEnum}"/> 是泛型，<c>switch</c> 里穷举不了，
    /// 靠这个接口才能和 <see cref="ChoiceSetting"/> 走同一条渲染路径。
    /// </summary>
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
            // 存的是枚举本身，界面按下标操作，两边在这里换算。配置文件被手改成
            // 已删除的枚举名时 IndexOf 会给 -1，退回第一项而不是让界面画出空白。
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
