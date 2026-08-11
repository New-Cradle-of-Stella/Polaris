using System;
using System.IO;
using Polaris.Res;
using XX;

namespace Polaris
{
    /// <summary>
    /// Polaris 随包分发的自带图片。文件<b>硬编码</b>在 <c>plugins/Polaris/</c> 下、和
    /// <c>Polaris.dll</c> 同级（<see cref="Infra.PathsAPI.PolarisRoot"/>），由
    /// <c>deploy-polaris.ps1</c> 与 <c>Polaris.csproj</c> 一起部署/打包。
    /// <para>
    /// 取用走自己的资源子系统（<see cref="PolarisResAPI"/>）而不是自己 <c>File.ReadAllBytes</c> +
    /// <c>new Texture2D</c>：挂载、扩展名探测、材质缓存、按路径去重都是现成的，顺便也是给自己
    /// 开的一剂真实用量。<c>modId</c> 用 <c>"Polaris"</c>，和 <c>AutoBindScanner</c> 拿
    /// <c>assembly.GetName().Name</c> 当 modId 的约定一致；<c>Own</c> 的语义（一次性获取、
    /// 永不释放、按路径去重）正好符合"整个进程就用这一张图"。
    /// </para>
    /// </summary>
    internal static class PolarisBrandImages
    {
        /// <summary>logo 的文件名（不含扩展名，探测规则同 <c>[PolarisResource]</c>）。</summary>
        const string LogoName = "polaris_icon";

        static bool logoResolved;
        static MImage logo;

        /// <summary>
        /// Polaris 的 logo。图片不在（玩家删了、或者从旧版本升级过来时没带上）就返回 null，
        /// 调用方跳过绘制即可——它纯粹是装饰，不该为它记 error，更不该画出资源子系统那个
        /// 品红占位块。
        /// </summary>
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

            // 先自己确认文件在：Own.Image 在找不到文件时会记一条 error 再返回品红占位纹理
            // （对"必需资源"是对的行为，对装饰图就成了噪音 + 一块显眼的品红）。
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
