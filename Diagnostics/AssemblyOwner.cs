using System.Reflection;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一个程序集的归属结论，由 <see cref="AssemblyOwnerIndex"/> 产出并长期缓存
    /// （程序集一旦加载，它是谁的就不会再变，和 <see cref="Infra.TypesAPI"/> 缓存类型表的理由一样）。
    /// <para>
    /// 这个类型是整套错误分析的"身份证"：日志里的一行结论、报告文件里每一帧前面的方括号标注、
    /// 告知页上的模组名，全都从这里取值，不再各自拼一遍字符串。
    /// </para>
    /// </summary>
    public sealed class AssemblyOwner
    {
        internal AssemblyOwner() { }

        /// <summary>归属类别。</summary>
        public OwnerKind Kind { get; internal set; }

        /// <summary>对应的程序集；字符串堆栈解析出来的归属可能没有实例，此时为 null。</summary>
        public Assembly Assembly { get; internal set; }

        /// <summary>
        /// 展示名。模组优先用 <see cref="PolarisModInfo.DisplayName"/>（即作者声明的名字或
        /// BepInPlugin 名），其余情况用程序集名。
        /// </summary>
        public string DisplayName { get; internal set; }

        /// <summary>dll 文件名（不含路径）；<see cref="OwnerKind.Dynamic"/> 时为 null。</summary>
        public string FileName { get; internal set; }

        /// <summary>dll 完整路径；<see cref="OwnerKind.Dynamic"/> 时为 null。</summary>
        public string FullPath { get; internal set; }

        /// <summary>BepInEx 插件 GUID；不是插件时为 null。Harmony 的 patch owner 也是这个值。</summary>
        public string PluginGuid { get; internal set; }

        /// <summary>
        /// 模组元信息（作者、主页、简介）。只有 <see cref="OwnerKind.Mod"/> 才有，
        /// 是报告尾部"该找谁"那一段的唯一数据来源。
        /// </summary>
        public PolarisModInfo ModInfo { get; internal set; }

        /// <summary>
        /// 这个归属能不能被当成责任人。原版、BCL、框架都不行——不是因为它们不会有 bug，
        /// 而是因为在一份装了模组的游戏里，把锅甩给它们对玩家没有任何指导意义
        /// （这也正是 <see cref="PolarisModWarning"/> 那一页在讲的事）。
        /// </summary>
        public bool IsBlamable
            => Kind == OwnerKind.Mod || Kind == OwnerKind.Polaris;

        /// <summary>报告与日志里用的短标签，例如 <c>[模组]</c> 里的那两个字。</summary>
        public string KindLabel
        {
            get
            {
                switch (Kind)
                {
                    case OwnerKind.Runtime: return "runtime";
                    case OwnerKind.Vanilla: return "vanilla";
                    case OwnerKind.Framework: return "framework";
                    case OwnerKind.Polaris: return "Polaris";
                    case OwnerKind.Mod: return "mod";
                    case OwnerKind.ModLibrary: return "mod dependency";
                    case OwnerKind.Dynamic: return "dynamic";
                    default: return "unknown";
                }
            }
        }

        /// <summary>
        /// 一行式描述，形如 <c>模组「WeNeedMoreNoels」（作者：某某）</c>。
        /// 作者未声明时不硬凑括号——留白比写"作者：未知"干净。
        /// </summary>
        public string Describe()
        {
            string name = string.IsNullOrEmpty(DisplayName) ? FileName ?? "unknown assembly" : DisplayName;

            if (Kind == OwnerKind.Polaris)
            {
                return "Polaris itself";
            }

            string author = ModInfo?.Author;
            return author == null
                ? $"{KindLabel} \"{name}\""
                : $"{KindLabel} \"{name}\" (author: {author})";
        }

        public override string ToString() => Describe();
    }
}
