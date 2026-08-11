using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary><c>ItemStorage.Reduce</c> 是"要么按 <c>count</c> 全部扣掉、要么整体失败"的语义，
    /// 不存在部分扣除，所以 <c>__result == true</c> 时直接用请求的 <c>count</c> 当作 Delta。</summary>
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.Reduce), new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool) })]
    [PolarisPatchFeature(GameCallbackKind.ItemRemoved)]
    internal static class Patch_ItemStorage_Reduce_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(ItemStorage __instance, NelItem Itm, int count, int grade, bool __result)
        {
            if (!__result || count == 0)
            {
                return;
            }

            GameStorageKind kind = ReferenceEquals(__instance, GameBinding.Inventory)
                ? GameStorageKind.PlayerInventory
                : GameStorageKind.Unknown;
            InventoryCallbacks.PublishItemRemoved(kind, Itm?.key, grade, -count);
        }
    }
}
