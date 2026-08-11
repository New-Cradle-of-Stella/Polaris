namespace Polaris.PUI
{
    /// <summary>
    /// 一份 <see cref="PUIGraphDefinition"/> 里的一个节点：图内唯一的 <see cref="Key"/>，
    /// 以及该节点对应的 PUI 名（<see cref="IPUI.Name"/>，通过 <see cref="PUIManager"/> 的类型
    /// 目录解析出具体类型）。同一个 PuiName 在一张图里出现多次时，Key 需要各自区分
    /// （生成器按 "{PuiName}#{序号}" 规则去重）。
    /// </summary>
    public sealed class PUINodeDefinition
    {
        public string Key { get; }
        public string PuiName { get; }

        public PUINodeDefinition(string key, string puiName)
        {
            Key = key;
            PuiName = puiName;
        }
    }
}
