namespace Polaris.PUI
{
    /// <summary>
    /// <see cref="PUIGraphDefinition"/> 中的一个节点：图内唯一的 <see cref="Key"/> 对应到一个 PUI 名。同一 PuiName 多次出现时 Key 需各自区分（生成器按 "{PuiName}#{序号}" 去重）。
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
