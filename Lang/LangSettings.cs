using System;
using Polaris.Settings;

namespace Polaris.Lang
{
    /// <summary>
    /// 模组文案的语言。<see cref="Auto"/> 跟随游戏，其余各自钉死在一个语言代码上。
    /// <para>
    /// 刻意只列游戏自带语言包对应的那几门，而不是把 <c>.plang</c> 里出现过的语言代码
    /// 全收集起来动态生成选项：设置项的选项列表在 <c>Plugin.Start</c> 一次性定型，
    /// 玩家换一批模组选项就跟着变的话，存在配置文件里的选择随时可能失效；
    /// 而"游戏支持哪些语言"是稳定的，玩家也是照着这个预期来找的。
    /// </para>
    /// </summary>
    public enum ModTextLanguage
    {
        Auto,
        Japanese,
        English,
        SimplifiedChinese,
        Korean,
    }

    /// <summary>
    /// 本地化子系统暴露给玩家的设置。字段本身就是值的真身，
    /// <c>SettingsAttributeScanner</c> 在 <c>Plugin.Start</c> 阶段把上次存的值写回这里。
    /// </summary>
    [PolarisSettingGroup("polarislang", LangStrings.Group)]
    internal static class LangSettings
    {
        // 选项文案：只有"自动"需要翻译，语言名照惯例用它自己的语言写，翻译反而更难认。
        // 这里必须写 new[]{...} 而不是集合表达式——特性实参只认数组创建表达式。
        [PolarisSetting(LangStrings.Language, Desc = LangStrings.LanguageDesc,
            Choices = new[] { LangStrings.LanguageAuto, "日本語", "English", "简体中文", "한국어" })]
        public static ModTextLanguage Language = ModTextLanguage.Auto;

        /// <summary>
        /// <see cref="PlangRuntime.Get"/> 实际用来查表的语言代码：玩家指定了就用指定的，
        /// 选"自动"就问游戏当前语言。
        /// <para>
        /// 读不到游戏语言时返回 null（而不是随便挑一个）——<see cref="PlangRuntime.Get"/>
        /// 会把它当"未知语言"处理、退回作者写的中性文案，这比让所有模组突然一起变成日文好。
        /// </para>
        /// </summary>
        internal static string EffectiveLocale
        {
            get
            {
                switch (Language)
                {
                    // 这几个代码与 PolarisAPI.Game.CurrentLocale 给出的 family key 对齐；
                    // 日文那一档写 "ja" 而不是游戏的默认 family key "_"，是因为 .plang 作者
                    // 填的是语言代码。PlangRuntime.Get 两边都认，见那里的候选顺序。
                    case ModTextLanguage.Japanese: return "ja";
                    case ModTextLanguage.English: return "en";
                    case ModTextLanguage.SimplifiedChinese: return "zh-cn";
                    case ModTextLanguage.Korean: return "ko-kr";
                }

                try
                {
                    return PolarisAPI.Game.CurrentLocale;
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[PolarisLang] Failed to read the game's current language; treating it as unknown this time: {e.Message}");
                    return null;
                }
            }
        }
    }
}
