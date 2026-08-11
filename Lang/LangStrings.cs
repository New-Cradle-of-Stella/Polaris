using Polaris.Localization;

namespace Polaris.Lang
{
    /// <summary>
    /// 本地化子系统设置项文案的内置翻译。
    /// <para>
    /// 这里有点自指：本子系统正是 <c>.plang</c> 的运行时，自己的设置却写死在代码里。
    /// 理由和 <c>Polaris.Localization.PolarisStrings</c> 一样——设置项要在
    /// <c>Plugin.Start</c> 绑定配置文件时就能查到文案，而那时 <c>.plang</c> 的注册与
    /// resolver 挂载才刚跑完；更要紧的是，让"选哪种语言"这一行本身依赖那套还没生效的机制，
    /// 坏起来会连玩家改回去的入口一起坏掉。
    /// </para>
    /// </summary>
    internal static class LangStrings
    {
        const string P = "polarislang.settings.";

        internal const string Group = "&" + P + "group";
        internal const string Language = "&" + P + "language";
        internal const string LanguageDesc = "&" + P + "language.desc";
        internal const string LanguageAuto = "&" + P + "language.auto";

        static bool registered;

        /// <summary>由 <c>Plugin.Awake</c> 调一次，早于 Start 阶段的设置项扫描。</summary>
        internal static void Register()
        {
            if (registered)
            {
                return;
            }

            registered = true;

            LocalizationAPI loc = PolarisAPI.Localization;

            loc.Register(P + "group", new LocalizedText("Mod text")
            {
                ["zh"] = "模组文案",
                ["ja"] = "MODのテキスト",
            });

            loc.Register(P + "language", new LocalizedText("Language")
            {
                ["zh"] = "语言",
                ["ja"] = "言語",
            });

            loc.Register(P + "language.auto", new LocalizedText("Auto")
            {
                ["zh"] = "自动",
                ["ja"] = "自動",
            });

            loc.Register(P + "language.desc", new LocalizedText(
                "Which language mods' own text uses; \"Auto\" follows the game's language.\n"
                + "Pick one you can read when a mod has no translation for your game language. "
                + "Open windows change the next time they are rebuilt.")
            {
                ["zh"] = "模组自带文案用哪种语言，\"自动\" 跟随游戏语言。\n"
                       + "某个模组没有你游戏语言的翻译时，可以指定一门看得懂的。"
                       + "已打开的界面下次重建才会换。",
                ["ja"] = "MOD自身のテキストの言語です。「自動」はゲームの言語に従います。\n"
                       + "ゲーム言語の翻訳が無いMODには、読める言語を指定できます。"
                       + "表示中の画面は次回の再構築時に切り替わります。",
            });
        }
    }
}
