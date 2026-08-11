using System;

namespace Polaris.Res
{
    /// <summary>
    /// 标在 static 类上，声明这个类里 <see cref="PolarisResourceAttribute"/> 字段的资源从哪个
    /// 文件夹读取。<see cref="Folder"/> 是相对调用方 dll 所在目录的子路径（"和 dll 同级"，
    /// 可以是多级路径，比如 <c>"audio/bgm"</c>），不是绝对路径，也不是相对 <c>BepInEx/plugins</c>
    /// 根目录。
    /// <para>
    /// <see cref="Runtime.AutoBindScanner"/> 只会自动绑定打了这个特性的类——同一个模组可以有
    /// 多个这样的类，各自指向不同子文件夹（比如一个类管图片、一个类管音频）。**没打这个特性的类，
    /// 即使里面有 <see cref="PolarisResourceAttribute"/> 字段，也不会被自动绑定**，扫描时会打一条
    /// 警告日志提示漏加了这个特性。
    /// </para>
    /// <para>
    /// 如果不想用类特性这条全自动路径（比如想按需动态取用资源，或者想用一个不受"dll 同级"
    /// 限制的目录），仍然可以调用 <see cref="ModResources.MountDefault"/>/<see cref="ModResources.Mount"/>
    /// 和 <see cref="ModResources.BindStaticFields(Type)"/> 手动控制，两条路径互不影响。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // 假设这个模组的 dll 是 plugins/WNMN/WeNeedMoreNoels.dll，
    /// // 那么这个类的资源就放在 plugins/WNMN/pics/ 下（比如 pics/preview_noel00.png）。
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
