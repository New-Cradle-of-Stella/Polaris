using System;
using HarmonyLib;
using nel;
using nel.gm;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 游戏内 ESC 菜单的"设置"分类：申请一个底部子区，用来放设置搜索框。
    /// <para>
    /// 子区是原版自己的机制，不是我们造出来的：<c>UiGameMenu.BxRRemake</c> 每次都按
    /// <c>bounds_h - 顶部子区高 - 底部子区高</c> 给右侧内容框定高，所以只要把
    /// <c>subarea_btm_clms/rows</c> 从 0 改成 1，设置面板就会自动从底部缩回去一条，
    /// 空出来的地方由 <c>UiGameMenuTopTab</c> 建一个独立的小框占住——正是"面板缩一点、
    /// 下面加一个窗口"要的效果，而且出现/收起动画、层级、导航链全都跟着原版走。
    /// 强化（<c>UiGMCEnhancer</c>）、技能（<c>UiGMCSkill</c>）那几个分类用的就是这条路。
    /// </para>
    /// <para>
    /// 改在<b>基类构造函数</b>上而不是 <c>UiGMCCfg</c> 自己的：那几个值是 <c>readonly</c> 字段，
    /// 在基类构造函数里从参数赋值，子类构造函数里已经写不动了；而参数在这里还是可以 <c>ref</c> 改的。
    /// 顺带也就不必去碰 <c>AEvSubArea</c>——它是同一个构造函数按改后的数量分配的。
    /// </para>
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

            // 一个模组都没注册过设置项：没有可搜的东西，不占玩家的地方。
            // 这时机是安全的——设置项在 Plugin.Start 扫描完毕，而 UiGMCCfg 要等玩家
            // 第一次打开 ESC 菜单的设置分类才构造。
            if (PolarisAPI.Settings.Groups.Count == 0)
            {
                return;
            }

            // 原版设置分类传的是 0,0,0,0（见 UiGMCCfg 的构造函数），这里是从无到有。
            // 真有别的模组也动过这两个值就让给它，不去覆盖。
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
    /// 把搜索栏画进上面申请到的那个底部子区。
    /// <para>
    /// 挂在基类的虚方法上（<c>UiGMCCfg</c> 自己没有重写它），所以要按分类过滤——
    /// 强化/技能/道具那几个分类会 <c>base.initAppearSubAreaInner(...)</c> 调到同一个方法体。
    /// </para>
    /// <para>
    /// 原方法返回 true 表示"内容是从上次的暂存里恢复回来的"（玩家在 ESC 菜单里切走又切回来），
    /// 那种情况下控件原封不动还在，不能再画一遍。
    /// </para>
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
                // 搜索栏画崩了不能连累整个 ESC 菜单——设置界面本身还是能用的，
                // 最坏的结果只是底部空着一条。
                PolarisAPI.Errors.Report(e, "building the in-game settings search box");
                Plugin.Logger.LogError("[Polaris.Settings] Failed to build the search box in the in-game settings menu.");
            }
        }
    }
}
