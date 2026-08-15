using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>UiCFG</c> 被拆掉时松开搜索过滤对界面对象的引用，避免静态字段攥着已销毁的 Unity 对象导致假 null。
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.destruct))]
    internal static class Patch_UiCFG_destruct
    {
        static void Postfix()
        {
            SettingsSearchFilter.Forget();
            SettingsSearchBox.Forget();
        }
    }
}
