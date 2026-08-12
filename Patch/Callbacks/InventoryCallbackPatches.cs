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
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.Add),
        new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature("ItemAdded")]
    internal static class Patch_ItemStorage_Add_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(ItemStorage __instance, NelItem Itm, int grade, bool execute, int __result)
        {
            if (!execute || __result == 0)
            {
                return;
            }

            GameCallbackPublishers.ItemAdded(__instance, Itm, __result, grade);
        }
    }

    /// <summary><c>ItemStorage.Reduce</c> 是"要么按 <c>count</c> 全部扣掉、要么整体失败"的语义，
    /// 不存在部分扣除，所以 <c>__result == true</c> 时直接用请求的 <c>count</c> 当作变化量。</summary>
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.Reduce),
        new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool) })]
    [PolarisPatchFeature("ItemRemoved")]
    internal static class Patch_ItemStorage_Reduce_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(ItemStorage __instance, NelItem Itm, int count, int grade, bool __result)
        {
            if (!__result || count == 0)
            {
                return;
            }

            GameCallbackPublishers.ItemRemoved(__instance, Itm, count, grade);
        }
    }

    /// <summary>
    /// <c>ItemStorage.tranferItems</c> 是 Storage 间转移的入口，单独发一条事件，
    /// 避免被误报成一次无关联的增加 + 减少。转移方向是"从 <c>__instance</c> 到 <c>Dest</c>"；
    /// <c>__result</c> 是实际转移的物品行数，为 0 表示什么都没动。
    /// <para>
    /// 形参名必须与游戏一致（<c>Dest</c>）：Harmony 是<b>按名字</b>把原方法的实参注进来的，
    /// 名字对不上会在应用补丁时直接抛 "Parameter not found"，而不是安静地注入 null。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.tranferItems))]
    [PolarisPatchFeature("ItemsTransferred")]
    internal static class Patch_ItemStorage_tranferItems_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(ItemStorage __instance, ItemStorage Dest, int __result)
        {
            if (__result == 0)
            {
                return;
            }

            GameCallbackPublishers.ItemsTransferred(__instance, Dest);
        }
    }

    /// <summary><c>NelItemManager.getItem</c> 是玩家"获得记录"增加的入口；<c>__result</c> 是实际
    /// 记为获得的数量。这与某个 Storage 是否真的多了这件物品是两件事，各打各的补丁。</summary>
    [HarmonyPatch(typeof(NelItemManager), nameof(NelItemManager.getItem),
        new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature("ItemObtained")]
    internal static class Patch_NelItemManager_getItem_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(NelItem Itm, int grade, int __result)
        {
            if (__result != 0)
            {
                GameCallbackPublishers.ItemObtained(Itm, __result, grade);
            }
        }
    }

    /// <summary><c>NelItemManager.dropManual</c> 是地图上生成掉落物的入口。</summary>
    [HarmonyPatch(typeof(NelItemManager), nameof(NelItemManager.dropManual))]
    [PolarisPatchFeature("DropCreated")]
    internal static class Patch_NelItemManager_dropManual_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(NelItem Itm, int count, int grade, float mapx, float mapy, object __result)
        {
            if (__result == null)
            {
                return;
            }

            GameCallbackPublishers.DropCreated(Itm, count, grade, mapx, mapy);
        }
    }

    /// <summary>
    /// <c>NelItem.Use</c> 是物品使用的唯一真实入口（菜单、快捷栏与事件脚本都走它）。
    /// <c>__result</c> 是游戏给出的使用结果码，0 一般表示什么也没发生。
    /// </summary>
    [HarmonyPatch(typeof(NelItem), nameof(NelItem.Use))]
    [PolarisPatchFeature("ItemUsed")]
    internal static class Patch_NelItem_Use_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(NelItem __instance, int grade, int __result)
        {
            if (__result != 0)
            {
                GameCallbackPublishers.ItemUsed(__instance, grade, __result);
            }
        }
    }
}
