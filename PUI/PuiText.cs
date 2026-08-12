using Polaris.Localization;

namespace Polaris.PUI
{
    /// <summary>
    /// <see cref="LocalizedString"/> 约定的运行期求值：把 <c>.pui</c> 里的原始字符串解析成显示文案。
    /// 编译期由 <c>CSharpTextEmitter</c> 静态展开，不走这里；只有热重载场景需要在游戏侧动态解析。
    /// </summary>
    public static class PuiText
    {
        /// <summary>
        /// <c>&amp;</c> 开头查 <c>XX.TX.Get</c>，<c>&amp;&amp;</c> 开头脱转义，其余原样返回；null 返回空串。
        /// </summary>
        public static string Resolve(string raw)
        {
            if (raw == null)
            {
                return "";
            }

            return LocalizedString.TryGetKey(raw, out string key)
                ? XX.TX.Get(key)
                : LocalizedString.Unescape(raw);
        }

        /// <summary>
        /// <see cref="Resolve"/> 的数组版。null 进 null 出；返回新数组，不原地修改（避免重复 Apply 时丢键）。
        /// </summary>
        public static string[] ResolveAll(string[] raw)
        {
            if (raw == null)
            {
                return null;
            }

            var result = new string[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                result[i] = Resolve(raw[i]);
            }

            return result;
        }
    }
}
