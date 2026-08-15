// 本文件被链接进 nullable 设置不同的两个项目，固定语境以避免一边刷警告。
#nullable disable

namespace Polaris.Localization
{
    /// <summary>
    /// "显示用字符串"本地化键约定的唯一判定实现：<c>&amp;</c> 开头表示本地化键，<c>&amp;&amp;</c> 开头表示转义的字面 <c>&amp;</c>，只看第 0 个字符。
    /// 被多个模块（设置项文案、.pui 编译期展开、热重载、编辑器预览）共用；因链接进 net472 VSIX 编译，不得引用 UnityEngine / XX / BepInEx 或 Polaris 其它类型。
    /// </summary>
    public static class LocalizedString
    {
        /// <summary>本地化键的前缀字符。</summary>
        public const char Sigil = '&';

        /// <summary><paramref name="raw"/> 是本地化键（<c>&amp;</c> 开头，非转义，非空键）时返回 true，<paramref name="key"/> 为去掉前缀后的内容。</summary>
        public static bool TryGetKey(string raw, out string key)
        {
            key = null;

            // 长度 < 2 排除 null/空串及单独一个 "&"。
            if (raw == null || raw.Length < 2 || raw[0] != Sigil || raw[1] == Sigil)
            {
                return false;
            }

            key = raw.Substring(1);
            return true;
        }

        /// <summary>对非键字符串脱转义：开头 <c>&amp;&amp;</c> 去掉一个 <c>&amp;</c>，其余原样返回。与 <see cref="TryGetKey"/> 配对使用。</summary>
        public static string Unescape(string raw)
        {
            if (raw == null || raw.Length < 2 || raw[0] != Sigil || raw[1] != Sigil)
            {
                return raw;
            }

            return raw.Substring(1);
        }
    }
}
