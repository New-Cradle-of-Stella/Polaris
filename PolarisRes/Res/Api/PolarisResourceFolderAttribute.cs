using System;

namespace Polaris.Res
{
    /// <summary>
    /// 标在 static 类上，声明该类里 <see cref="PolarisResourceAttribute"/> 字段的资源从哪个文件夹读取；
    /// <see cref="Folder"/> 是相对调用方 dll 所在目录的子路径。只有打了此特性的类会被 <see cref="Runtime.AutoBindScanner"/> 自动绑定；
    /// 也可以不用特性，改用 <see cref="ModResources.MountDefault"/>/<see cref="ModResources.BindStaticFields(Type)"/> 手动控制。
    /// </summary>
    /// <example>
    /// <code>
    /// [PolarisResourceFolder("pics")]
    /// static class MyImages
    /// {
    ///     [PolarisResource("preview_noel00")]
    ///     public static Texture2D PreviewNoel;
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PolarisResourceFolderAttribute : Attribute
    {
        public PolarisResourceFolderAttribute(string folder)
        {
            Folder = folder;
        }

        /// <summary>相对调用方 dll 所在目录的子路径。</summary>
        public string Folder { get; }
    }
}
