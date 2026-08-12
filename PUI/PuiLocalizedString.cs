// 被编进两个 nullable 设置不同的项目，固定语境避免一边刷警告。
#nullable disable

using Polaris.Localization;

namespace Polaris.PUI
{
    /// <summary><c>.pui</c> 里"显示用字符串"的本地化键约定；转发至 <see cref="LocalizedString"/>。</summary>
    public static class PuiLocalizedString
    {
        /// <inheritdoc cref="LocalizedString.Sigil"/>
        public const char Sigil = LocalizedString.Sigil;

        /// <inheritdoc cref="LocalizedString.TryGetKey"/>
        public static bool TryGetKey(string raw, out string key) => LocalizedString.TryGetKey(raw, out key);

        /// <inheritdoc cref="LocalizedString.Unescape"/>
        public static string Unescape(string raw) => LocalizedString.Unescape(raw);
    }
}
