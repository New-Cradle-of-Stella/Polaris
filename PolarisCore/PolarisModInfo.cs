namespace Polaris
{
    /// <summary>
    /// 单个 dll 的模组信息视图：由 <see cref="PolarisModInfoResolver"/> 把
    /// <see cref="PolarisModInfoAttribute"/> 和 BepInEx 插件元数据合并后产出，供日志与管理页展示。
    /// 没有标注特性的 dll 同样会有一条记录，此时 <see cref="HasModInfo"/> 为 <c>false</c>，
    /// 除 <see cref="FileName"/>、<see cref="DisplayName"/> 外的字段都可能为 null。
    /// </summary>
    public sealed class PolarisModInfo
    {
        /// <summary>dll 文件名（不含路径），如 "SomeMod.dll"，也是 <see cref="PolarisModInfoResolver"/> 的检索键。</summary>
        public string FileName { get; internal set; }

        /// <summary>展示名：特性 &gt; BepInEx 插件名 &gt; 去掉扩展名的文件名。</summary>
        public string DisplayName { get; internal set; }

        /// <summary>作者；未标注特性时为 null。</summary>
        public string Author { get; internal set; }

        /// <summary>一句话简介；未标注特性时为 null。</summary>
        public string Description { get; internal set; }

        /// <summary>版本号：特性 &gt; BepInPlugin 的版本；都没有时为 null。</summary>
        public string Version { get; internal set; }

        /// <summary>主页 / 发布页地址；未填写时为 null。</summary>
        public string Url { get; internal set; }

        /// <summary>该 dll 是否标注了 <see cref="PolarisModInfoAttribute"/>。</summary>
        public bool HasModInfo { get; internal set; }
    }
}
