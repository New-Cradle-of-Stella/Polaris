using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    [HarmonyPatch(typeof(QuestTracker), nameof(QuestTracker.setFocusedQuest))]
    [PolarisPatchFeature(GameCallbackKind.FocusedQuestChanged)]
    internal static class Patch_QuestTracker_setFocusedQuest_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(QuestTracker.QuestProgress Prog) => ProgressionCallbacks.PublishFocusedQuestChanged(Prog?.Q?.key);
    }
}
