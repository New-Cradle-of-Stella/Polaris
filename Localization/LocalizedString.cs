// 这个文件被编进两个 nullable 设置不同的项目（Polaris 关、PolarisTools 开），
// 自己固定语境免得同一份源码在一边干净、在另一边刷警告。
#nullable disable

namespace Polaris.Localization
{
    /// <summary>
    /// "显示用字符串"的本地化键约定——<b>整个 Polaris 系列唯一的一份判定实现</b>。
    /// <para>
    /// 约定：字符串以 <c>&amp;</c> 开头表示"这不是文案本身，而是一个本地化键"，取值时去查表；
    /// 开头写 <c>&amp;&amp;</c> 表示"我要的就是一个字面 <c>&amp;</c>"，脱一层后按普通字面量处理。
    /// 判定<b>只看第 0 个字符</b>，字符串中间的 <c>&amp;</c> 永远原样保留——所以富文本里
    /// <c>A&amp;nbsp;B</c> 是安全的，只有 <c>&amp;nbsp;A</c> 这种开头就是 HTML 实体的才需要
    /// 写成 <c>&amp;&amp;nbsp;A</c>。
    /// </para>
    /// <para>
    /// 用到这份判定的路径全部共用它，不允许各写一套：
    /// 设置项的标签/说明/选项（<see cref="LocalizationAPI.Text"/>）、
    /// <c>.pui</c> 的编译期展开（PolarisTools 的 <c>CSharpTextEmitter</c>）、
    /// 热重载运行期（<c>Polaris.PUI.PuiText</c>）、编辑器预览（<c>PuiPreviewRenderer</c>）。
    /// </para>
    /// <para>
    /// 因此本文件被 PolarisTools 以 <c>&lt;Compile Include Link="..."/&gt;</c> 链接进 net472 的
    /// VSIX 编译，<b>不得引用 UnityEngine / XX / BepInEx 任何类型</b>，也不得引用
    /// Polaris 自己的其它类型——真正去查表的部分在 <see cref="LocalizationAPI.Text"/> 里。
    /// </para>
    /// </summary>
    public static class LocalizedString
    {
        /// <summary>本地化键的前缀字符。</summary>
        public const char Sigil = '&';

        /// <summary>
        /// <paramref name="raw"/> 是本地化键时返回 true，<paramref name="key"/> 为去掉
        /// 开头 <c>&amp;</c> 之后的内容。
        /// <para>
        /// 以下都<b>不是</b>键：null/空串、不以 <c>&amp;</c> 开头、以 <c>&amp;&amp;</c>
        /// 开头（转义），以及只有一个 <c>&amp;</c> 字符（空键查表没有意义，当字面量处理）。
        /// </para>
        /// </summary>
        public static bool TryGetKey(string raw, out string key)
        {
            key = null;

            // 长度 < 2 直接排除：既盖掉 null/空串，也盖掉单独一个 "&"。
            if (raw == null || raw.Length < 2 || raw[0] != Sigil || raw[1] == Sigil)
            {
                return false;
            }

            key = raw.Substring(1);
            return true;
        }

        /// <summary>
        /// 给<b>不是键</b>的字符串脱转义：开头的 <c>&amp;&amp;</c> 去掉一个 <c>&amp;</c>，
        /// 其余原样返回。和 <see cref="TryGetKey"/> 配对使用——先问是不是键，不是再脱转义。
        /// </summary>
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
