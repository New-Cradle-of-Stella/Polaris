using System.Reflection;

namespace Polaris.Lang
{
    /// <summary>
    /// 一次 key 冲突：两个<b>不同</b>的插件程序集注册了同一个 key。
    /// <para>
    /// 冲突不看文案是否相同——两份一模一样的文案今天看着无害，其中一方改了字就变成"界面上
    /// 随机出现另一个模组的文案"，而这种问题从表象上几乎不可能被追回到 key 撞车上。同一个
    /// 程序集内部的重复注册不算冲突（那是同一个作者自己的两份 <c>.plang</c>，见
    /// <see cref="PlangRuntime.Register"/>）。
    /// </para>
    /// </summary>
    internal sealed class PlangConflict
    {
        internal PlangConflict(string key, Assembly kept, Assembly ignored)
        {
            Key = key;
            Kept = kept;
            Ignored = ignored;
        }

        internal string Key { get; }

        /// <summary>先注册、文案被保留的那一方。</summary>
        internal Assembly Kept { get; }

        /// <summary>后注册、文案被丢弃的那一方。</summary>
        internal Assembly Ignored { get; }

        /// <summary>
        /// 写进报告与告知页的一行明细。<b>刻意是语言中性的</b>（见
        /// <c>FatalError.Details</c>）：里面全是 key 名和 dll 文件名，中英日玩家看到的都是同一份，
        /// 截图或复制给作者时不会因为语言不同而对不上。
        /// </summary>
        internal string Describe()
            => $"{Key}  --  {NameOf(Kept)} (used) <-> {NameOf(Ignored)} (ignored)";

        static string NameOf(Assembly assembly)
        {
            if (assembly == null)
            {
                return "?";
            }

            try
            {
                return assembly.GetName().Name;
            }
            catch (System.Exception)
            {
                return "?";
            }
        }
    }
}
