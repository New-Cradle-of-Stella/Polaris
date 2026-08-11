using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    [HarmonyPatch(typeof(QuestTracker), nameof(QuestTracker.remove), new[] { typeof(string), typeof(bool) })]
    [PolarisPatchFeature(GameCallbackKind.QuestRemoved)]
    internal static class Patch_QuestTracker_remove_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(string k) => ProgressionCallbacks.PublishQuestRemoved(k);
    }
}
