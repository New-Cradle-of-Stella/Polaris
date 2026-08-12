using System;
using System.IO;
using HarmonyLib;
using evt;
using Polaris.Event;

namespace Polaris.Patch
{
    /// <summary>
    /// 让 <c>EV.getEventContent</c> 能找到 Polaris 解包到 <c>plugins/Polaris/events/</c> 下的 .cmd 文件：
    /// 命中 <see cref="PolarisEventRuntime"/> 注册的事件名时用 <c>EvReader.parseText</c> 直接灌入并跳过原版，
    /// 否则放行走原版逻辑。参数类型显式列出以避免重载歧义。
    /// </summary>
    [HarmonyPatch(typeof(EV), nameof(EV.getEventContent), new[] { typeof(string), typeof(EvReader) })]
    internal static class Patch_EV_getEventContent
    {
        static bool Prefix(string _name, EvReader ER, ref bool __result)
        {
            if (!PolarisEventRuntime.TryGetFilePath(_name, out string path))
            {
                return true;
            }

            try
            {
                ER.parseText(File.ReadAllText(path));
                __result = true;
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Patch_EV_getEventContent");
                __result = false;
            }

            return false;
        }
    }
}
