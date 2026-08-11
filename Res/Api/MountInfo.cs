namespace Polaris.Res
{
    /// <summary>
    /// <see cref="ModResources.Mounts"/> 对外暴露的只读挂载点信息。
    /// 真正的 <c>Mounts.DirectoryMount</c>（含大小写索引等实现细节）是 internal，不对外暴露。
    /// </summary>
    public readonly struct MountInfo
    {
        public string RootPath { get; }
        public int Priority { get; }

        internal MountInfo(string rootPath, int priority)
        {
            RootPath = rootPath;
            Priority = priority;
        }
    }
}
