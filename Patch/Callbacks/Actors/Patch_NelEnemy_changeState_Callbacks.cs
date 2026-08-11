using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>NelEnemy.changeState(STATE)</c> 在 <c>state == st</c> 时直接原样返回 <c>this</c>，
    /// 同样用前后比较 <c>state</c> 字段的方式，不需要跟着它内部一堆特判分支。
    /// </summary>
    [HarmonyPatch(typeof(NelEnemy), nameof(NelEnemy.changeState), new[] { typeof(NelEnemy.STATE) })]
    [PolarisPatchFeature(GameCallbackKind.EnemyStateChanged)]
    internal static class Patch_NelEnemy_changeState_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(NelEnemy __instance, out NelEnemy.STATE __state) => __state = __instance.state;

        [HarmonyPostfix]
        static void Postfix(NelEnemy __instance, NelEnemy.STATE __state)
        {
            NelEnemy.STATE previous = __state;
            NelEnemy.STATE current = __instance.state;
            if (current == previous)
            {
                return;
            }

            CharacterHandle handle = CharacterGameAPI.HandleOf(__instance);
            ActorCallbacks.PublishEnemyStateChanged(handle, previous.ToString(), current.ToString());
        }
    }
}
