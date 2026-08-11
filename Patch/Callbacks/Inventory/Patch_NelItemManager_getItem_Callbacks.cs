using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary><c>NelItemManager.getItem</c> 是玩家"获得记录"增加的入口；<c>__result</c> 是实际
    /// 记为获得的数量。这与某个 Storage 是否真的多了这件物品是两件事，各打各的补丁。</summary>
    [HarmonyPatch(typeof(NelItemManager), nameof(NelItemManager.getItem),
        new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature(GameCallbackKind.ItemObtained)]
    internal static class Patch_NelItemManager_getItem_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(NelItem Itm, int __result)
        {
            if (__result != 0)
            {
                InventoryCallbacks.PublishItemObtained(Itm?.key, __result);
            }
        }
    }
}
