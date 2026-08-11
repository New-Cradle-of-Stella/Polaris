using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary><c>ItemStorage.tranferItems</c> 是 Storage 间转移的入口，避免被误报成一次无关联
    /// 的增加+减少。<c>__result</c> 是实际转移的物品行数。</summary>
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.tranferItems))]
    [PolarisPatchFeature(GameCallbackKind.ItemsTransferred)]
    internal static class Patch_ItemStorage_tranferItems_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(int __result) => InventoryCallbacks.PublishItemsTransferred(__result);
    }
}
