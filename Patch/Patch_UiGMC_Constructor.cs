using System;
using HarmonyLib;
using nel;
using nel.gm;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 游戏内 ESC 菜单的"设置"分类：把 <c>subarea_btm_clms/rows</c> 从 0 改成 1，
    /// 借用原版底部子区机制申请一块区域放设置搜索框（与强化/技能分类同一条路）。
    /// 改在基类构造函数上是因为这些字段是 readonly，只有在这里通过 ref 参数才能改动。
    /// </summary>
    [HarmonyPatch(typeof(UiGMC), MethodType.Constructor,
        typeof(UiGameMenu), typeof(CATEG), typeof(bool),
        typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(float), typeof(float))]
    internal static class Patch_UiGMC_Constructor
    {
        static void Prefix(CATEG _categ,
                           ref byte _subarea_btm_clms, ref byte _subarea_btm_rows,
                           ref float _subarea_btm_row_height)
        {
            if (_categ != CATEG.CONFIG)
            {
                return;
            }

            // 没有任何模组注册设置项时没有可搜的东西，不占玩家的地方。
            if (PolarisAPI.Settings.Groups.Count == 0)
            {
                return;
            }

            // 原版传的是 0,0,0,0；如果已有别的模组动过这两个值，让给它，不覆盖。
            if (_subarea_btm_clms != 0 || _subarea_btm_rows != 0)
            {
                Plugin.Logger.LogWarning(
                    "[Polaris.Settings] The in-game settings category already has a bottom subarea; the search box is not added there.");
                return;
            }

            _subarea_btm_clms = 1;
            _subarea_btm_rows = 1;
            _subarea_btm_row_height = SettingsSearchBox.SubareaRowScale;
        }
    }

    /// <summary>
    /// 把搜索栏画进上面申请到的底部子区。挂在基类虚方法上，所以要按 CATEG.CONFIG 过滤
    /// （其他分类也会调到同一方法体）；原方法返回 true 表示内容是暂存恢复的，不能重画。
    /// </summary>
    [HarmonyPatch(typeof(UiGMC), "initAppearSubAreaInner")]
    internal static class Patch_UiGMC_initAppearSubAreaInner
    {
        static void Postfix(UiGMC __instance, UiBoxDesigner Ds, bool is_top, ref bool __result)
        {
            if (__result || is_top || Ds == null || __instance.categ != CATEG.CONFIG)
            {
                return;
            }

            try
            {
                Ds.init();
                SettingsSearchBox.Build(Ds);
                __result = true;
            }
            catch (Exception e)
            {
                // 搜索栏画崩了不能连累整个 ESC 菜单，最坏结果只是底部空着一条。
                PolarisAPI.Errors.Report(e, "building the in-game settings search box");
                Plugin.Logger.LogError("[Polaris.Settings] Failed to build the search box in the in-game settings menu.");
            }
        }
    }
}
