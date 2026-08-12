using System;
using System.IO;
using Polaris.Res;
using XX;

namespace Polaris
{
    /// <summary>Polaris 随包分发的自带图片；硬编码在 <see cref="Infra.PathsAPI.PolarisRoot"/> 下，通过自身资源子系统（<see cref="PolarisResAPI"/>）加载。</summary>
    internal static class PolarisBrandImages
    {
        /// <summary>logo 的文件名（不含扩展名，探测规则同 <c>[PolarisResource]</c>）。</summary>
        const string LogoName = "polaris_icon";

        static bool logoResolved;
        static MImage logo;

        /// <summary>Polaris 的 logo；图片缺失时返回 null（纯装饰，不记 error，不画品红占位块）。</summary>
        internal static MImage Logo
        {
            get
            {
                if (!logoResolved)
                {
                    logoResolved = true;
                    logo = Load(LogoName);
                }
                return logo;
            }
        }

        static MImage Load(string name)
        {
            string root = PolarisAPI.Paths.PolarisRoot;

            // 先自己确认文件在：Own.Image 找不到文件时会记 error 并返回品红占位纹理，对装饰图是噪音。
            if (!File.Exists(Path.Combine(root, name + ".png")))
            {
                Plugin.Logger.LogInfo(
                    $"[Polaris] Bundled image {name}.png was not found in {root}; the UI that uses it is skipped.");
                return null;
            }

            try
            {
                return PolarisResAPI.For("Polaris").Mount(root).Own.Image(name);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] Failed to load the bundled image {name}.png: {ex.Message}");
                return null;
            }
        }
    }
}
