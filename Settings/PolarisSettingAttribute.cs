using System;

namespace Polaris.Settings
{
    /// <summary>
    /// 标在静态字段上，把它变成一个设置项；字段本身就是值的真身，须配合类上的 <see cref="PolarisSettingGroupAttribute"/> 使用。
    /// 控件类型由字段类型推断：<c>bool</c>→开关，<c>float</c>/<c>double</c>→滑条，<c>int</c>→整数滑条（有 <see cref="Choices"/> 则多选一），<c>enum</c>→选择器，<c>string</c>→文本输入。
    /// <see cref="Label"/>/<see cref="Desc"/>/<see cref="Choices"/> 遵守 <c>&amp;</c> 本地化键约定（见 <see cref="Localization.LocalizedString"/>）。
    /// </summary>
    /// <example>
    /// <code>
    /// [PolarisSettingGroup("mymod", "&amp;mymod.settings.group", OnLoaded = nameof(Apply))]
    /// static class MyConfig
    /// {
    ///     [PolarisSetting("&amp;mymod.settings.show_hud", Desc = "&amp;mymod.settings.show_hud.desc",
    ///         OnChanged = nameof(Apply))]
    ///     public static bool ShowHud = true;
    ///
    ///     // 不打算做多语言时直接写字面量也行
    ///     [PolarisSetting("不透明度", Min = 0, Max = 1, Step = 0.05)]
    ///     public static float Opacity = 0.8f;
    ///
    ///     // 启动加载完、以及玩家每次改动之后都会走到这里
    ///     static void Apply() => MyHud.SetVisible(ShowHud);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PolarisSettingAttribute : Attribute
    {
        public PolarisSettingAttribute(string label) => Label = label;

        /// <summary>界面上显示的名字；<c>&amp;</c> 开头视为本地化键。</summary>
        public string Label { get; }

        /// <summary>持久化用的键；缺省取字段名。一旦发布就别改，改了等于重置玩家的设置。</summary>
        public string Id { get; set; }

        /// <summary>悬停时右侧说明框的文字；<c>&amp;</c> 开头视为本地化键。</summary>
        public string Desc { get; set; }

        /// <summary>数值型的下界，缺省 0。</summary>
        public double Min { get; set; }

        /// <summary>数值型的上界，缺省：浮点 1，整数 100。</summary>
        public double Max { get; set; } = double.NaN;

        /// <summary>数值型的步长，缺省：浮点 0.1，整数 1。</summary>
        public double Step { get; set; } = double.NaN;

        /// <summary>选项文案。<c>int</c> 字段变多选一（值为下标）；<c>enum</c> 字段替换枚举名（长度须与成员数一致）；<c>bool</c> 字段用作关/开两态文案。</summary>
        public string[] Choices { get; set; }

        /// <summary>文本型的最大长度，-1 为不限。</summary>
        public int MaxLength { get; set; } = -1;

        /// <summary>组内排序权重，小的在前；相同则按字段在类里的声明顺序。</summary>
        public int Order { get; set; }

        /// <summary>
        /// 值变化后调用的静态方法名（同类中查找，签名 <c>static void M()</c> 或 <c>static void M(T value)</c>）。
        /// 触发于玩家改动的每一步及取消回滚，但启动加载配置时不触发——那用 <see cref="PolarisSettingGroupAttribute.OnLoaded"/>。
        /// </summary>
        public string OnChanged { get; set; }
    }

    /// <summary>
    /// 标在静态类上，声明这个类里所有 <see cref="PolarisSettingAttribute"/> 字段属于哪个模组分区。
    /// <see cref="SettingsAttributeScanner"/> 会在 <c>Plugin.Start</c> 阶段自动扫描并注册。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PolarisSettingGroupAttribute : Attribute
    {
        public PolarisSettingGroupAttribute(string modId, string displayName = null)
        {
            ModId = modId;
            DisplayName = displayName;
        }

        /// <summary>模组标识，直接用作配置文件名。</summary>
        public string ModId { get; }

        /// <summary>分区标题；<c>&amp;</c> 开头视为本地化键，缺省用 <see cref="ModId"/>。</summary>
        public string DisplayName { get; }

        /// <summary>分区排序权重，小的在前。</summary>
        public int Order { get; set; }

        /// <summary>
        /// 该组值全部从配置文件加载完后调用的静态方法名（签名 <c>static void M()</c>）；调用时机在 <c>Plugin.Start</c>，此时所有字段已是上次退出时的值，可安全应用到运行状态。
        /// </summary>
        public string OnLoaded { get; set; }
    }
}
