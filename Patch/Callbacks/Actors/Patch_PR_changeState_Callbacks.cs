using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>PR.changeState(STATE, STATE)</c> 有好几处前置条件会直接 <c>return</c>，不落到真正赋值
    /// <c>state</c> 的那一行——包括它的公开单参数重载 <c>changeState(STATE)</c>，内部也是转发到这个
    /// 两参数版本。与其在每个提前返回分支各自判断，这里统一在 Prefix/Postfix 比较 <c>state</c>
    /// 字段前后值：只要值真的变了就算一次变化，不管是从哪条分支落地的。
    /// </summary>
    [HarmonyPatch(typeof(PR), "changeState", new[] { typeof(PR.STATE), typeof(PR.STATE) })]
    [PolarisPatchFeature(GameCallbackKind.PlayerStateChanged)]
    internal static class Patch_PR_changeState_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(PR __instance, out PR.STATE __state) => __state = __instance.state;

        [HarmonyPostfix]
        static void Postfix(PR __instance, PR.STATE __state)
        {
            PR.STATE previous = __state;
            PR.STATE current = __instance.state;
            if (current == previous)
            {
                return;
            }

            ActorCallbacks.PublishPlayerStateChanged(previous.ToString(), current.ToString());
        }
    }
}
