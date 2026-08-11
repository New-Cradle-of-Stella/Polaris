using System;

namespace Polaris
{
    /// <summary>
    /// 模组元信息特性：模组标注它来向 Polaris 声明作者、简介等展示用信息。
    /// 可以标在 BepInEx 插件主类（<c>BaseUnityPlugin</c> 派生类）上：
    /// <code>
    /// [BepInPlugin("com.example.mymod", "MyMod", "1.0.0")]
    /// [PolarisModInfo("某某", "给爱丽丝加了一顶帽子。", Url = "https://example.com")]
    /// public class Plugin : BaseUnityPlugin { }
    /// </code>
    /// 也可以直接标在程序集上：<c>[assembly: PolarisModInfo("某某", "……")]</c>。
    /// 两者同时存在时以类级为准。
    /// <para>
    /// Polaris 在扫描 <c>plugins</c> 根目录时，
    /// 会由 <see cref="PolarisModInfoResolver"/> 从已加载的插件上读取该特性，合成
    /// <see cref="PolarisModInfo"/> 展示在模组管理页里。读取走的是已加载程序集的反射，
    /// 因此被禁用（<c>.dll.disabled</c>）而没进入本次游戏的 dll 读不到信息，只会显示文件名。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class PolarisModInfoAttribute : Attribute
    {
        /// <param name="author">作者。</param>
        /// <param name="description">一句话简介。</param>
        public PolarisModInfoAttribute(string author, string description)
        {
            Author = author;
            Description = description;
        }

        /// <summary>作者。</summary>
        public string Author { get; }

        /// <summary>一句话简介。</summary>
        public string Description { get; }

        /// <summary>展示名；留空则回退到 <c>BepInPlugin</c> 的插件名，再回退到 dll 文件名。</summary>
        public string DisplayName { get; set; }

        /// <summary>版本号；留空则回退到 <c>BepInPlugin</c> 的版本。</summary>
        public string Version { get; set; }

        /// <summary>主页 / 发布页地址，可空。</summary>
        public string Url { get; set; }
    }
}
