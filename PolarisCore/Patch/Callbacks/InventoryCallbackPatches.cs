using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>打 <c>Add</c> 的 5 参数外层重载；<c>__result</c> 是实际加入数量，<c>execute == false</c> 时只是预演不算发生。</summary>
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.Add),
        new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool), typeof(bool) })]
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

    /// <summary><c>Reduce</c> 全扣或整体失败、无部分扣除，<c>__result == true</c> 时直接用请求的 <c>count</c> 当变化量。</summary>
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.Reduce),
        new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool) })]
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

    /// <summary>Storage 间转移入口，单独发事件避免误报为无关的增加+减少；<c>__result</c> 为 0 表示未转移。形参名 <c>Dest</c> 必须与游戏签名一致，否则 Harmony 注入时会抛 "Parameter not found"。</summary>
    [HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.tranferItems))]
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

    /// <summary>玩家"获得记录"入口；与 Storage 是否真的增加物品是两件事，各打各的补丁。</summary>
    [HarmonyPatch(typeof(NelItemManager), nameof(NelItemManager.getItem),
        new[] { typeof(NelItem), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
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

    /// <summary>物品使用的唯一入口（菜单/快捷栏/事件脚本均经此）；<c>__result</c> 为结果码，0 表示未发生任何效果。</summary>
    [HarmonyPatch(typeof(NelItem), nameof(NelItem.Use))]
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
