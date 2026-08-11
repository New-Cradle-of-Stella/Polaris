using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>PR.cureHp(int)</c> 自己就在检测"这次治疗是不是把玩家从死亡里拉回来"（原版内部据此调用
    /// <c>recoverFromGameOver</c>）。这里复用同一次前后比较，翻回
    /// <see cref="Patch_PR_initDeath_Callbacks.PlayerCurrentlyDead"/>，让下一次真正死亡还能再发一次事件。
    /// </summary>
    [HarmonyPatch(typeof(PR), nameof(PR.cureHp), new[] { typeof(int) })]
    [PolarisPatchFeature(GameCallbackKind.PlayerRevived)]
    internal static class Patch_PR_cureHp_Revival_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(PR __instance, out bool __state) => __state = __instance.is_alive;

        [HarmonyPostfix]
        static void Postfix(PR __instance, bool __state)
        {
            if (__state || !__instance.is_alive)
            {
                return;
            }

            Patch_PR_initDeath_Callbacks.PlayerCurrentlyDead = false;
            ActorCallbacks.PublishPlayerRevived();
        }
    }
}
