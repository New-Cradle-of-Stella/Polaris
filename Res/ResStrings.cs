using Polaris.Localization;

namespace Polaris.Res
{
    /// <summary>资源子系统设置项文案的内置翻译；写在代码里是因为 <c>Plugin.Awake</c> 绑定配置文件时 <c>.plang</c> 还没注册。</summary>
    internal static class ResStrings
    {
        const string P = "polarisres.settings.";

        internal const string Group = "&" + P + "group";
        internal const string StrictMode = "&" + P + "strict";
        internal const string StrictModeDesc = "&" + P + "strict.desc";
        internal const string FrameBudget = "&" + P + "frame_budget";
        internal const string FrameBudgetDesc = "&" + P + "frame_budget.desc";

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

            loc.Register(P + "group", new LocalizedText("Resources")
            {
                ["zh"] = "资源库",
                ["ja"] = "リソース",
            });

            loc.Register(P + "strict", new LocalizedText("Strict mode")
            {
                ["zh"] = "严格模式",
                ["ja"] = "厳格モード",
            });

            loc.Register(P + "strict.desc", new LocalizedText(
                "Throw an exception when an asset is missing, instead of logging an error and "
                + "using a placeholder.\nFor mod authors — leave it off while playing.")
            {
                ["zh"] = "找不到资源时抛异常，而不是记录错误并用占位对象顶替。\n"
                       + "这是给模组作者用的，平时玩请保持关闭。",
                ["ja"] = "リソースが見つからないとき、エラー記録と代替表示ではなく例外を投げます。\n"
                       + "MOD制作者向けです。通常プレイではオフのままに。",
            });

            loc.Register(P + "frame_budget", new LocalizedText("Loading budget per frame (ms)")
            {
                ["zh"] = "每帧加载预算（毫秒）",
                ["ja"] = "1フレームあたりの読み込み時間（ミリ秒）",
            });

            loc.Register(P + "frame_budget.desc", new LocalizedText(
                "Time spent each frame on background loading (textures, PXLS, …); the rest "
                + "continues next frame.\nHigher loads mod assets sooner but makes the frame "
                + "rate less even.")
            {
                ["zh"] = "每帧用在后台加载（纹理、PXLS 等）上的时间，没做完的留到下一帧。\n"
                       + "调大加载更快，但帧率没那么平稳。",
                ["ja"] = "テクスチャやPXLSなどの読み込みに1フレームで使う時間です。残りは次のフレームへ。\n"
                       + "大きくすると読み込みは早くなりますが、フレームレートは不安定になります。",
            });
        }
    }
}
