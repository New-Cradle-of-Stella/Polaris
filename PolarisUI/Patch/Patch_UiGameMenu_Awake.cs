using HarmonyLib;
using nel.gm;

namespace Polaris.Patch
{
    /// <summary>
    /// 游戏菜单的分类缓存数组 AGmcCache 原版只留了 11 格（够 1 个自定义分类复用原本从未
    /// 使用的第 11 格）；注册的自定义分类超过 1 个时需要放大这个数组，否则
    /// Patch_UiGameMenu_appearCategory 里按 CATEG 下标写入会越界。
    /// </summary>
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.Awake))]
    internal static class Patch_UiGameMenu_Awake
    {
        [HarmonyPostfix]
        static void Postfix(UiGameMenu __instance)
        {
            int required = GameMenuAPI.VanillaCategoryCount + PolarisAPI.GameMenu.Categories.Count + 1;
            if (__instance.AGmcCache.Length < required)
            {
                __instance.AGmcCache = new UiGMC[required];
            }
        }
    }
}
