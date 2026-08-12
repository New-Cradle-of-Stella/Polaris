using System;
using System.IO;
using HarmonyLib;
using evt;
using Polaris.Event;

namespace Polaris.Patch
{
    /// <summary>
    /// 让 PolarisEvent 解包到 <c>plugins/Polaris/events/</c> 下的 .cmd 文件能被游戏原生的
    /// <c>EV.getEventContent</c> 找到——原版这个方法只认 <c>StreamingAssets/evt</c> 那棵树，这里在
    /// 原版查表之前先问一遍 <see cref="PolarisEventRuntime"/>：这个事件名是不是我们注册过的，是的话
    /// 直接用游戏自己的 <c>EvReader.parseText</c> 把解包出来的文件内容灌进去、跳过原版
    /// （<c>return false</c>）；不是的话放行走原版逻辑（<c>return true</c>），完全不影响任何原生
    /// 事件。跟 <see cref="Patch_TX_Get"/> 是同一个"自己的解析器优先、没命中就让原版兜底"模式。
    /// <para>
    /// 显式给出两个参数类型，跟 <see cref="Patch_TX_Get"/> 一样防着"其实有个没在文档表格里列出来的
    /// 重载"导致 <c>PatchAll</c> 抛 <c>AmbiguousMatchException</c> 那个坑。
    /// </para>
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
