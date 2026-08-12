using System;

namespace Polaris
{
    /// <summary>
    /// 模组元信息特性：标在插件主类或程序集上，向 Polaris 声明作者、简介等展示信息（类级优先）。
    /// <code>
    /// [BepInPlugin("com.example.mymod", "MyMod", "1.0.0")]
    /// [PolarisModInfo("某某", "给爱丽丝加了一顶帽子。", Url = "https://example.com")]
    /// public class Plugin : BaseUnityPlugin { }
    /// </code>
    /// 通过反射读取已加载程序集，故被禁用的 dll 读不到信息，只显示文件名。
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
