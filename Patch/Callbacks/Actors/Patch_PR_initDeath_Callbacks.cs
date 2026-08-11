using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>PR.initDeath()</c> 的"这是不是第一次死"判断不能像 <c>NelEnemy.initDeath()</c> 那样看
    /// <c>state == STATE.DIE</c>：调用它的唯一入口（<c>M2Attackable.applyHpDamage</c>）本身就要求
    /// <c>is_alive</c> 已经是 <c>false</c>，玩家侧没有等价的"已经躺尸"标志位可以在调用前读。
    /// 改用一个静态标志：标志翻转到"死了"才发 <see cref="ActorCallbacks.PlayerDied"/>，
    /// 直到 <see cref="Patch_PR_cureHp_Revival_Callbacks"/> 探测到复活才翻回去。
    /// </summary>
    [HarmonyPatch(typeof(PR), nameof(PR.initDeath), new System.Type[0])]
    [PolarisPatchFeature(GameCallbackKind.PlayerDeathStarting)]
    [PolarisPatchFeature(GameCallbackKind.PlayerDied)]
    internal static class Patch_PR_initDeath_Callbacks
    {
        internal static bool PlayerCurrentlyDead;

        [HarmonyPrefix]
        static void Prefix()
        {
            if (!PlayerCurrentlyDead)
            {
                ActorCallbacks.PublishPlayerDeathStarting();
            }
        }

        [HarmonyPostfix]
        static void Postfix(bool __result)
        {
            if (!__result || PlayerCurrentlyDead)
            {
                return;
            }

            PlayerCurrentlyDead = true;
            ActorCallbacks.PublishPlayerDied();
        }
    }
}
