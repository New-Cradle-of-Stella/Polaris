using HarmonyLib;
using nel.title;
using UnityEngine;

namespace Polaris.Patch
{
    /// <summary>
    /// 标题界面每帧驱动入口：当某个按钮的窗口处于打开状态时，推进确定/取消按钮条的
    /// 淡入动画，侦测窗口是否已被自行关闭（自动归位）或玩家按下 ESC/X（请求关闭）。
    /// 同时每帧重新应用顶部按钮换行末行的居中修正——BxTop 激活时会走一次内部的行高/
    /// 布局重算（row_remake_flag 消费、box 尺寸随按钮数变化后触发的重排），会用引擎原版
    /// 公式重新摆一遍按钮位置，把 Patch_SceneTitleTemp_initButtons/fineTexts 里做的居中
    /// 修正冲掉；这个时机在场景私有方法内部，没有单独的 Harmony 挂载点，所以改成每帧
    /// 重新断言一次，而不是找到那个具体触发点单独打补丁。CenterTopRow 内部一开始就判断
    /// 末行是否需要居中（大多数帧都会因为整除而立刻返回），所以每帧调用的开销可以忽略。
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), "runIRD")]
    internal static class Patch_SceneTitleTemp_runIRD
    {
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance)
        {
            MainMenuAPI.CenterTopRow(__instance);

            // 标题告知页（致命错误页 / 模组警示页 / 错误通知页，见 TitleOverlays）的淡入。
            // 放在最前面且不受下面那些 return
            // 影响：这些页面出现时标题状态机停在 TOP、CurrentOpenButton 为空，走到下面就
            // 直接返回了。
            TitleOverlays.AdvanceFade(Time.deltaTime);

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
