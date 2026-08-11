using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// 打 <c>Add</c> 的 5 参数重载（没有 <c>out IRow</c>，签名更好写）而不是它转发到的 6 参数版本——
    /// 前者是外层包装，Postfix 在它返回时跑，此时内层版本已经执行完毕，观察到的仍然是最终结果。
    /// <c>__result</c> 是实际加进去的数量；<c>execute == false</c> 时是"预演"，不算真的发生。
    /// </summary>
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.Add), new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature(GameCallbackKind.ItemAdded)]
    internal static class Patch_ItemStorage_Add_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(ItemStorage __instance, NelItem Itm, int grade, bool execute, int __result)
        {
            if (!execute || __result == 0)
            {
                return;
            }

            GameStorageKind kind = ReferenceEquals(__instance, GameBinding.Inventory)
                ? GameStorageKind.PlayerInventory
                : GameStorageKind.Unknown;
            InventoryCallbacks.PublishItemAdded(kind, Itm?.key, grade, __result);
        }
    }
}
