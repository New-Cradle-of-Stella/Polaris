using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary><c>NelM2DBase.setSF</c> 只是转发到这一个方法（已核对），打这一处就够，不会双发。</summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.setSF))]
    [PolarisPatchFeature(GameCallbackKind.StoryFlagChanged)]
    internal static class Patch_COOK_setSF_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(string key, out int __state) => __state = COOK.getSF(key);

        [HarmonyPostfix]
        static void Postfix(string key, int __state)
        {
            int after = COOK.getSF(key);
            if (after != __state)
            {
                ProgressionCallbacks.PublishStoryFlagChanged(key, __state, after);
            }
        }
    }
}
