using System;
using System.Reflection;

namespace Polaris.Lang
{
    /// <summary>
    /// 一次 key 冲突：两个<b>不同</b>的插件程序集注册了同一个 key。冲突不看文案是否相同，
    /// 因为两份文案一样今天无害，改字后就会变成难以追查的错位；同程序集内部重复注册不算冲突。
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

        /// <summary>写进报告与告知页的一行明细，刻意语言中性（只含 key 名和 dll 文件名），方便不同语言玩家截图对照。</summary>
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
            catch (Exception)
            {
                return "?";
            }
        }
    }
}
