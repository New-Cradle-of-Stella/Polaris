using System;

namespace Polaris.Res
{
    /// <summary>
    /// 标在 static 字段上，声明"资源加载完成后自动填入这个字段"；字段类型决定资源种类（<c>byte[]</c>/<c>Texture2D</c>/<c>XX.MImage</c>/<c>PxlsCharacterHandle</c>/<c>AudioClip</c>/<c>VideoHandle</c>）。
    /// 类本身必须先打 <see cref="PolarisResourceFolderAttribute"/>，否则不会被 <see cref="Runtime.AutoBindScanner"/> 自动绑定，只会记警告。
    /// 填入方式等价于 <see cref="ModResources.Own"/>：一次性获取、永不释放、生命周期与模组绑定。
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
    ///
    ///     [PolarisResource("multiplayer")]
    ///     public static XX.MImage MultiplayerImage;
    /// }
    ///
    /// // 另一组资源放在 plugins/WNMN/audio/ 下（比如 audio/hit.wav）。
    /// [PolarisResourceFolder("audio")]
    /// static class MySounds
    /// {
    ///     [PolarisResource("hit")]
    ///     public static AudioClip HitSfx;
    /// }
    ///
    /// // 不需要写 Plugin.Awake()/Init() 里的任何代码——PolarisRes 启动时自动填好，
    /// // 随时可以直接用 MyImages.PreviewNoel / MySounds.HitSfx。
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PolarisResourceAttribute : Attribute
    {
        public PolarisResourceAttribute(string path)
        {
            Path = path;
        }

        /// <summary>挂载相对路径，扩展名可省略（按字段类型对应的 Kind 探测）。</summary>
        public string Path { get; }
    }
}
