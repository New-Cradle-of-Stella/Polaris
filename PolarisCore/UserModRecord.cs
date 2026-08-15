namespace Polaris
{
    /// <summary>
    /// <c>plugins</c> 根目录下一个可启停管理的 dll 文件的描述对象。启用状态即文件后缀本身：
    /// <see cref="EnabledPath"/>（<c>.dll</c>）存在则视为启用，<see cref="DisabledPath"/>
    /// （<c>.dll.disabled</c>）存在则视为禁用，两者互斥。
    /// </summary>
    internal sealed class UserModRecord
    {
        /// <summary>去掉 <c>.disabled</c> 后缀的文件名，如 "SomeMod.dll"，用于界面展示与 Scan 归并。</summary>
        public string DisplayName { get; set; }

        /// <summary>启用状态下的完整路径。</summary>
        public string EnabledPath { get; set; }

        /// <summary>禁用状态下的完整路径。</summary>
        public string DisabledPath { get; set; }

        /// <summary>当前是否处于启用状态（即磁盘上存在的是 <see cref="EnabledPath"/>）。</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 该 dll 的模组信息（作者、简介等），由 <see cref="PolarisModInfoResolver"/> 解析而来，不为 null。
        /// 处于禁用状态、或没标 <see cref="PolarisModInfoAttribute"/> 的 dll，
        /// 其 <see cref="PolarisModInfo.HasModInfo"/> 为 false。
        /// </summary>
        public PolarisModInfo Info { get; set; }

        /// <summary>上一次 <see cref="UserModToggleManager.SetEnabled"/> 失败时的说明；成功或未操作过时为 null。</summary>
        public string Error { get; set; }
    }
}
