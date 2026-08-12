using HarmonyLib;
using evt;
using nel.gm;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// 纯通知，不改变任何行为——与 <c>Patch_UiGameMenu_activate_WorldPause</c>（世界暂停 transpiler）
    /// 是两个独立的补丁类，Harmony 允许同一方法叠加多个 Prefix/Postfix/Transpiler。
    /// </summary>
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

    /// <summary>
    /// <c>EV.stack</c> 是事件被压入执行栈的唯一入口。事件系统没有把"栈顶事件的名字"暴露成
    /// 稳定的公开成员，所以当前事件由这三个补丁记账，而不是每帧去猜。
    /// </summary>
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
