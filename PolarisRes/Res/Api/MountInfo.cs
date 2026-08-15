namespace Polaris.Res
{
    /// <summary><see cref="ModResources.Mounts"/> 对外暴露的只读挂载点信息。</summary>
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
