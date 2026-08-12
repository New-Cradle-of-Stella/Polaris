using HarmonyLib;
using nel.title;
using UnityEngine;

namespace Polaris.Patch
{
    /// <summary>
    /// 标题界面每帧驱动入口：推进按钮窗口的淡入动画并侦测其关闭；每帧重新应用顶部按钮居中修正
    /// （内部布局重算无独立挂载点会冲掉该修正，故逐帧重新断言，CenterTopRow 本身开销可忽略）。
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), "runIRD")]
    internal static class Patch_SceneTitleTemp_runIRD
    {
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance)
        {
            MainMenuAPI.CenterTopRow(__instance);

            // 标题告知页（见 TitleOverlays）的淡入，须放在下面的 return 之前才能生效。
            TitleOverlays.AdvanceFade(Time.deltaTime);

            // 告知页显示期间压住语言切换行/外链按钮/底部按键提示；必须放在 Postfix 里，
            // 因为原版 runIRD 每帧都会重写这些 alpha，只有跑在它之后写的值才是最终值。
            TitleChrome.Apply(__instance, TitleOverlays.IsShowing);

            MainMenuAPI mainMenu = PolarisAPI.MainMenu;
            if (mainMenu.CurrentOpenButton == null)
            {
                return;
            }

            mainMenu.AdvanceCommandBarFade(Time.deltaTime);

            if (!mainMenu.IsCurrentWindowStillOpen())
            {
                mainMenu.ReturnToTop();
                return;
            }

            if (MainMenuAPI.IsCancelInputPressed())
            {
                mainMenu.RaiseEscaped();
            }
        }
    }
}
