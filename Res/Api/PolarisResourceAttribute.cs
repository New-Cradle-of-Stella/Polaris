using System;

namespace Polaris.Res
{
    /// <summary>
    /// 标在 static 字段上，声明"资源加载完成后自动填入这个字段"。字段类型决定资源种类：
    /// <c>byte[]</c> → 原始字节，<c>UnityEngine.Texture2D</c> → 纹理，<c>XX.MImage</c> → 图像
    /// （游戏自己的封装），<c>PxlsCharacterHandle</c> → PixelLiner 角色，
    /// <c>UnityEngine.AudioClip</c> → wav/ogg 音频，<c>VideoHandle</c> → mp4 视频路径句柄。
    /// 图像用游戏内部已有的封装（<c>XX.MImage</c>）；游戏没有"原始音频/视频"这类封装
    /// （音频走 CRIWARE cue sheet，视频走 AssetBundle 里的 <c>VideoClip</c>），所以音频/视频
    /// 是 PolarisRes 自己定义的字段类型。
    /// <para>
    /// **不需要模组自己写任何初始化代码，但类本身必须先打
    /// <see cref="PolarisResourceFolderAttribute"/>。** <see cref="Runtime.AutoBindScanner"/>
    /// 会在 PolarisRes 启动时自动扫描全部已加载的插件程序集，找到打了
    /// <see cref="PolarisResourceFolderAttribute"/> 的类，把特性里指定的文件夹（相对调用方
    /// dll 所在目录的子路径，"和 dll 同级"）自动挂载，再把这个类里的 <c>[PolarisResource]</c>
    /// 字段自动回填。没打 <see cref="PolarisResourceFolderAttribute"/> 的类，即使字段打了
    /// <c>[PolarisResource]</c> 也不会被自动绑定，只会记一条警告。
    /// </para>
    /// <para>
    /// 如果想按需动态取用资源而不是绑定到 static 字段，或者想用不受"dll 同级"限制的目录，
    /// 仍然可以调用 <see cref="ModResources.MountDefault"/>/<see cref="ModResources.Mount"/>
    /// 和 <see cref="ModResources.BindStaticFields(System.Type)"/> 手动控制——<c>MountTable</c>
    /// 对同一物理目录的重复挂载是幂等的，和自动路径不会冲突。
    /// </para>
    /// <para>
    /// 填入方式等价于 <see cref="ModResources.Own"/>：一次性获取、永不释放、按路径去重，
    /// 生命周期与模组本身绑定，不需要（也不应该）手动 Dispose。
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
