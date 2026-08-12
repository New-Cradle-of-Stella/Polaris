using HarmonyLib;
using evt;
using nel.gm;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>纯通知，不改变行为；与世界暂停的 transpiler 补丁类独立叠加在同一方法上。</summary>
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.activate))]
    [PolarisPatchFeature("GameMenuOpened")]
    internal static class Patch_UiGameMenu_activate_Notify
    {
        [HarmonyPostfix]
        static void Postfix(UiGameMenu __instance) => GameCallbackPublishers.GameMenuOpened(__instance);
    }

    /// <summary>菜单关闭。</summary>
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.deactivate))]
    [PolarisPatchFeature("GameMenuClosed")]
    internal static class Patch_UiGameMenu_deactivate_Notify
    {
        [HarmonyPostfix]
        static void Postfix(UiGameMenu __instance) => GameCallbackPublishers.GameMenuClosed(__instance);
    }

    /// <summary>事件压栈的唯一入口；引擎未暴露稳定的"当前栈顶事件"成员，故靠这三个补丁记账。</summary>
    [HarmonyPatch(typeof(EV), nameof(EV.stack))]
    [PolarisPatchFeature("EventOpened")]
    internal static class Patch_EV_stack_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(string _name, object __result)
        {
            if (__result != null)
            {
                GameEventRuntime.OnOpened(_name);
            }
        }
    }

    /// <summary>事件切换：旧的这一层结束，新的顶上来。</summary>
    [HarmonyPatch(typeof(EV), nameof(EV.changeEvent), new[] { typeof(string), typeof(int), typeof(string[]) })]
    [PolarisPatchFeature("EventOpened")]
    internal static class Patch_EV_changeEvent_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(string _event, bool __result)
        {
            if (!__result)
            {
                return;
            }

            // 切换等于"上一层正常演完了，换这一层"，因此先关后开，顺序不能反：
            // 反过来的话新事件会被紧跟着的关闭事件当成已经结束。
            GameEventRuntime.OnClosed(true);
            GameEventRuntime.OnOpened(_event);
        }
    }

    /// <summary><c>EV.evEnd</c> 是事件结束的唯一出口。</summary>
    [HarmonyPatch(typeof(EV), nameof(EV.evEnd))]
    [PolarisPatchFeature("EventClosed")]
    internal static class Patch_EV_evEnd_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(bool _all, bool __result)
        {
            if (__result)
            {
                // _all 为真是"整栈强制收掉"，那不是正常演完。
                GameEventRuntime.OnClosed(!_all);
            }
        }
    }
}
