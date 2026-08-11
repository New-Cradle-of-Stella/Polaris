using System;
using HarmonyLib;
using nel.title;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// 主菜单按钮被点击后，将点击事件分发给 MainMenuAPI 中对应按钮注册的回调
    /// 未注册回调的按钮（如原版自带按钮）不受影响，仍走游戏原有逻辑
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), "fnChangedTopCateg")]
    internal static class Patch_SceneTitleTemp_fnChangedTopCateg
    {
        [HarmonyPostfix]
        static void Postfix(BtnContainerRadio<aBtn> _B, int cur_value)
        {
            MainMenuAPI mainMenu = PolarisAPI.MainMenu;
            if (cur_value < 0 || mainMenu.CurrentOpenButton != null)
            {
                return;
            }

            aBtn btn = _B.Get(cur_value);
            if (mainMenu.TryGetCallback(btn.title, out FnBtnBindings callback))
            {
                // 这是 Postfix：异常不接住会直接从这个 Harmony 补丁飞出去，扰乱游戏自己
                // fnChangedTopCateg 调用点之后的处理（比如同一次点击里其它按钮状态的收尾）。
                // 一个 Mod 的按钮回调写坏，不该影响主菜单其余按钮的正常响应。
                try
                {
                    callback(btn);
                }
                catch (Exception ex)
                {
                    // 责任人就是这个回调委托本身所在的程序集，不必走堆栈推断。
                    PolarisAPI.Errors.Report(ex, $"the callback of main menu button \"{btn.title}\"", callback.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError($"[Polaris] The callback of main menu button \"{btn.title}\" threw an exception; ignored.");
                }
            }
        }
    }
}
