using HarmonyLib;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// 让通过 <see cref="PolarisAPI.Localization"/> 注册的本地化 key 能直接从游戏原生 <c>TX.Get</c>
    /// 管线里查到文案：命中 resolver 就写入 <c>__result</c> 并跳过原版，否则放行走原版查表/回退逻辑。
    /// <c>TX.Get</c> 有两个重载，必须显式给出参数类型，否则 <c>PatchAll</c> 会抛 <c>AmbiguousMatchException</c>
    /// 并中断 <c>Plugin.Awake</c>。
    /// </summary>
    [HarmonyPatch(typeof(TX), nameof(TX.Get), new[] { typeof(string), typeof(string) })]
    internal static class Patch_TX_Get
    {
        static bool Prefix(string title, ref string __result)
        {
            string resolved = PolarisAPI.Localization.Resolve(title);
            if (resolved == null)
            {
                return true;
            }

            __result = resolved;
            return false;
        }
    }
}
