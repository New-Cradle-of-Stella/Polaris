using System;

namespace Polaris.Res
{
    /// <summary>
    /// 一个资源的逻辑身份：模组命名空间 + 种类 + 挂载相对路径。
    /// <para>
    /// <see cref="Path"/> 在构造时就会被规范化：反斜杠转正斜杠、去掉首尾多余的斜杠、
    /// 折叠连续斜杠，并整体转小写。转小写是刻意的——原版 <c>XX.MTI</c> 的构造函数只把
    /// 路径最后一段转小写，在大小写敏感的文件系统上会因为字典键大小写不同而把同一个
    /// 物理资源缓存两次；<see cref="ResourceId"/> 作为 <c>ResourceCache</c> 的字典键，
    /// 从根上避免这个问题。真实磁盘上的大小写由 <c>Mounts.DirectoryMount</c> 的大小写
    /// 不敏感索引兜底解析，与这里的比较语义无关。
    /// </para>
    /// <para>
    /// 扩展名可写可不写：不写时由 <see cref="Mounts.ResourceKindExtensions.CandidateExtensions"/>
    /// 按 <see cref="Kind"/> 探测候选扩展名。
    /// </para>
    /// </summary>
    public readonly struct ResourceId : IEquatable<ResourceId>
    {
        public string ModId { get; }
        public ResourceKind Kind { get; }
        public string Path { get; }

        public ResourceId(string modId, ResourceKind kind, string path)
        {
            if (string.IsNullOrEmpty(modId))
            {
                throw new ArgumentException("modId cannot be empty.", nameof(modId));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path cannot be empty.", nameof(path));
            }

            ModId = modId;
            Kind = kind;
            Path = Normalize(path);
        }

        private static string Normalize(string path)
        {
            string p = path.Replace('\\', '/').Trim().Trim('/');
            while (p.Contains("//"))
            {
                p = p.Replace("//", "/");
            }

            return p.ToLowerInvariant();
        }

        public bool Equals(ResourceId other) =>
            Kind == other.Kind
            && string.Equals(ModId, other.ModId, StringComparison.Ordinal)
            && string.Equals(Path, other.Path, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ResourceId other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ModId, Kind, Path);

        public override string ToString() => $"{ModId}:{Kind}:{Path}";

        public static bool operator ==(ResourceId left, ResourceId right) => left.Equals(right);
        public static bool operator !=(ResourceId left, ResourceId right) => !left.Equals(right);
    }
}
