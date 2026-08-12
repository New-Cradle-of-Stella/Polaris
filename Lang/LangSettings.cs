using System;
using Polaris.Settings;

namespace Polaris.Lang
{
    /// <summary>
    /// 模组文案的语言。<see cref="Auto"/> 跟随游戏，其余各自钉死在一个语言代码上。
    /// 只列游戏自带语言包对应的那几门，不随 <c>.plang</c> 里出现的语言代码动态生成选项。
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
    /// 本地化子系统暴露给玩家的设置；<c>SettingsAttributeScanner</c> 在 <c>Plugin.Start</c> 阶段把上次存的值写回这里。
    /// </summary>
    [PolarisSettingGroup("polarislang", LangStrings.Group)]
    internal static class LangSettings
    {
        // 只有"自动"需要翻译，语言名照惯例用它自己的语言写。
        // 特性实参只认数组创建表达式，不能用集合表达式。
        [PolarisSetting(LangStrings.Language, Desc = LangStrings.LanguageDesc,
            Choices = new[] { LangStrings.LanguageAuto, "日本語", "English", "简体中文", "한국어" })]
        public static ModTextLanguage Language = ModTextLanguage.Auto;

        /// <summary>
        /// <see cref="PlangRuntime.Get"/> 实际用来查表的语言代码：玩家指定了就用指定的，选"自动"就问游戏当前语言。
        /// 读不到游戏语言时返回 null，<see cref="PlangRuntime.Get"/> 会当"未知语言"处理并退回中性文案。
        /// </summary>
        internal static string EffectiveLocale
        {
            get
            {
                switch (Language)
                {
                    // 与 CurrentLocale 的 family key 对齐；日文写 "ja" 而非游戏默认的 "_"，因为 .plang 作者填的是语言代码。
                    case ModTextLanguage.Japanese: return "ja";
                    case ModTextLanguage.English: return "en";
                    case ModTextLanguage.SimplifiedChinese: return "zh-cn";
                    case ModTextLanguage.Korean: return "ko-kr";
                }

                try
                {
                    return PolarisAPI.Game.Localization.CurrentLocale;
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
