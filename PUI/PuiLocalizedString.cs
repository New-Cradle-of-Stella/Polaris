// 和 PuiWireProtocol.cs 一样，这个文件被编进两个 nullable 设置不同的项目
// （Polaris 关、PolarisTools 开），自己固定语境免得同一份源码在一边干净、
// 在另一边刷警告。
#nullable disable

using Polaris.Localization;

namespace Polaris.PUI
{
    /// <summary>
    /// <c>.pui</c> 里"显示用字符串"的本地化键约定。
    /// <para>
    /// <b>判定本身不在这里</b>——它是整个 Polaris 系列共用的约定（设置项的标签、选项文案
    /// 走的是同一套），唯一的一份实现在 <see cref="LocalizedString"/> 里。
    /// 本类保留下来只是为了让 <c>.pui</c> 这一侧的调用点和文档还能按 PUI 的名字找过来，
    /// 每个成员都直接转发过去，不允许在这里长出第二套规则。
    /// </para>
    /// </summary>
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
