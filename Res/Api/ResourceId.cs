using System;

namespace Polaris.Res
{
    /// <summary>
    /// 一个资源的逻辑身份：模组命名空间 + 种类 + 挂载相对路径。
    /// <see cref="Path"/> 构造时会被规范化（斜杠统一、去除多余斜杠、整体转小写），以避免同一物理资源因大小写不同被缓存两次。
    /// 扩展名可省略，省略时由 <see cref="Mounts.ResourceKindExtensions.CandidateExtensions"/> 按 <see cref="Kind"/> 探测。
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
