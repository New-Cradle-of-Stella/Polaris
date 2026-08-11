using System;

namespace Polaris.Settings
{
    /// <summary>
    /// 标在静态字段上，把它变成一个设置项。字段本身就是值的真身：Polaris 在游戏启动时
    /// 把上次存的值写回字段，玩家在设置界面改动时也直接写字段，模组代码照常读字段即可。
    /// <para>
    /// 必须配合类上的 <see cref="PolarisSettingGroupAttribute"/> 使用。
    /// 控件类型由字段类型推断：<c>bool</c>→开关，<c>float</c>/<c>double</c>→滑条，
    /// <c>int</c>→整数滑条（给了 <see cref="Choices"/> 则是多选一），
    /// <c>enum</c>→枚举选择器，<c>string</c>→文本输入。
    /// </para>
    /// <para>
    /// <b>本地化</b>：<see cref="Label"/>、<see cref="Desc"/>、<see cref="Choices"/> 以及分区的
    /// <see cref="PolarisSettingGroupAttribute.DisplayName"/> 都遵守 PUI 那套 <c>&amp;</c> 约定
    /// （见 <see cref="Localization.LocalizedString"/>）：以 <c>&amp;</c> 开头就当成本地化键去查表，
    /// 想显示字面的 <c>&amp;</c> 开头就写 <c>&amp;&amp;</c>。文案来源是 <c>.plang</c>、
    /// <see cref="Localization.LocalizationAPI.Register(string, Localization.LocalizedText)"/>
    /// 登记的内置表、或者游戏自带的 key，三者共用同一条查询链。
    /// <b>写死的字面量照旧能用</b>，只是那样玩家换语言时它不会跟着变。
    /// </para>
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

        /// <summary>
        /// 选项文案（逐条 <c>&amp;</c> 开头视为本地化键）。<c>int</c> 字段给了它就变成多选一
        /// （值是下标）；<c>enum</c> 字段给了它就用它替换枚举名，长度必须和枚举成员数一致；
        /// <c>bool</c> 字段给两条时用作关/开两态的文案，缺省是 "OFF"/"ON"。
        /// </summary>
        public string[] Choices { get; set; }

        /// <summary>文本型的最大长度，-1 为不限。</summary>
        public int MaxLength { get; set; } = -1;

        /// <summary>组内排序权重，小的在前；相同则按字段在类里的声明顺序。</summary>
        public int Order { get; set; }

        /// <summary>
        /// 值变化后要调的静态方法名（同一个类里找，public/private 都行）。用 <c>nameof</c> 写以免打错。
        /// <para>
        /// 两种签名都认：<c>static void M()</c>（自己读字段）或 <c>static void M(T value)</c>
        /// （<c>T</c> 与字段类型兼容）。调用时字段已经是新值了。
        /// </para>
        /// <para>
        /// 触发时机：玩家在设置界面改动的每一步（含拖动滑块的中间值），以及点"取消"回滚时。
        /// <b>游戏启动加载配置时不触发</b>——那是初始化，用
        /// <see cref="PolarisSettingGroupAttribute.OnLoaded"/>。
        /// </para>
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
        /// 这一组的值全部从配置文件加载完之后要调的静态方法名（签名 <c>static void M()</c>，
        /// 同一个类里找，public/private 都行）。用 <c>nameof</c> 写以免打错。
        /// <para>
        /// 这是模组的设置初始化点：调到它的时候所有标了 <see cref="PolarisSettingAttribute"/>
        /// 的字段都已经是上次退出时的值，可以放心地把它们应用到运行状态（挂补丁、建 UI、改参数）。
        /// 调用发生在 <c>Plugin.Start</c> 阶段，此时所有插件的 <c>Awake</c> 都已完成。
        /// </para>
        /// </summary>
        public string OnLoaded { get; set; }
    }
}
