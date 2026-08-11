using Polaris.Localization;

namespace Polaris.PUI
{
    /// <summary>
    /// <see cref="LocalizedString"/> 约定的<b>运行期</b>求值：把 <c>.pui</c> 里填的
    /// 原始字符串变成真正要显示的文案。
    /// <para>
    /// 编译期那条路径不经过这里——<c>CSharpTextEmitter</c> 在生成 <c>.g.cs</c> 时字符串
    /// 已经是已知常量，直接静态展开成 <c>global::XX.TX.Get("key")</c> 或普通字面量，不给
    /// 生成代码留任何无谓的运行时调用。<b>只有热重载</b>拿到的是编辑器现发过来的原始串
    /// （VSIX 进程里没有 <c>TX</c>，也不该有），必须在游戏侧解析，所以有了这个类。
    /// </para>
    /// <para>
    /// 公开而不是 internal：手写 code-behind 里想让某段动态拼出来的文案也遵守同一套
    /// <c>&amp;</c> 约定时，调这里即可，不必自己抄一遍判定。
    /// </para>
    /// </summary>
    public static class PuiText
    {
        /// <summary>
        /// <c>&amp;</c> 开头查 <c>XX.TX.Get</c>，<c>&amp;&amp;</c> 开头脱转义，其余原样返回。
        /// <para>
        /// 键查不到时由 <c>TX.Get</c> 自己决定显示什么（先问 Polaris 的 resolver 链，
        /// 全未命中就落回原版查表）——这里不额外兜底，跟 <c>.plang</c> 生成的
        /// <c>Lang.SomeKey</c> 属性行为完全一致。
        /// </para>
        /// <para>
        /// null 返回空串：调用点原本写的是 <c>p.Text ?? ""</c>，语义要原样保住。
        /// </para>
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
        /// <see cref="Resolve"/> 的数组版（ButtonMulti 的 titles、Checks/Radio 的 descs）。
        /// null 进 null 出；<b>返回新数组</b>，绝不原地改传进来的那一份——热重载的
        /// <c>PuiWireCommand</c> 载荷可能被重复 Apply，就地改会让第二次拿到已经解析过的
        /// 文案，键就此丢失。
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
