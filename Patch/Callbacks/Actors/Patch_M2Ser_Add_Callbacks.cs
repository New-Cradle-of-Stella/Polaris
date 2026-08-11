using HarmonyLib;
using m2d;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>M2Ser.Add</c> 一个方法同时处理"这个状态异常本来没有"（新增）和"已经有，刷新持续时间/层级"
    /// （刷新）两种情况，内部用 <c>Find(ser)</c> 是否为 null 分支。Prefix 提前做同一次查找，
    /// Postfix 据此决定发哪一种事件——不需要跟着它内部的分支逻辑走。
    /// </summary>
    [HarmonyPatch(typeof(M2Ser), nameof(M2Ser.Add))]
    [PolarisPatchFeature(GameCallbackKind.StatusAdded)]
    [PolarisPatchFeature(GameCallbackKind.StatusRefreshed)]
    internal static class Patch_M2Ser_Add_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(M2Ser __instance, SER ser, out bool __state)
            => __state = ActorCallbacks.WantsStatusEvents && __instance.Find(ser) != null;

        [HarmonyPostfix]
        static void Postfix(M2Ser __instance, SER ser, bool __state)
        {
            if (!ActorCallbacks.WantsStatusEvents || __instance.Mv == null)
            {
                return;
            }

            CharacterHandle target = CharacterGameAPI.HandleOf(__instance.Mv);
            if (__state)
            {
                ActorCallbacks.PublishStatusRefreshed(target, (int)ser);
            }
            else
            {
                ActorCallbacks.PublishStatusAdded(target, (int)ser);
            }
        }
    }
}
