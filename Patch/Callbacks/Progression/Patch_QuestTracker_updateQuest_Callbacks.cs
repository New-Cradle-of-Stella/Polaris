using System.Collections.Generic;
using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>QuestTracker.updateQuest</c> 内部有一堆提前返回分支（阶段没推进、任务已完成、任务不存在……）。
    /// 与其跟着每个分支判断，Prefix/Postfix 各查一次当前进度表，单纯比较 phase 前后值：
    /// 没找到 -&gt; 找到是 <c>QuestStarted</c>，phase 变了是 <c>QuestUpdated</c>，
    /// 到达 <c>end_phase</c> 再补一条 <c>QuestCompleted</c>。
    /// </summary>
    [HarmonyPatch(typeof(QuestTracker), nameof(QuestTracker.updateQuest))]
    [PolarisPatchFeature(GameCallbackKind.QuestStarted)]
    [PolarisPatchFeature(GameCallbackKind.QuestUpdated)]
    [PolarisPatchFeature(GameCallbackKind.QuestCompleted)]
    internal static class Patch_QuestTracker_updateQuest_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(QuestTracker __instance, string k, out int __state)
            => __state = ActorCallbacksGate() ? FindPhase(__instance, k) : int.MinValue;

        [HarmonyPostfix]
        static void Postfix(QuestTracker __instance, string k, int __state)
        {
            if (__state == int.MinValue)
            {
                return;
            }

            QuestTracker.QuestProgress prog = FindProgress(__instance, k, out bool finished);
            if (prog == null)
            {
                return;
            }

            int before = __state;
            int after = prog.phase;

            if (before < 0)
            {
                ProgressionCallbacks.PublishQuestStarted(k, after);
            }
            else if (after != before)
            {
                ProgressionCallbacks.PublishQuestUpdated(k, before, after);
            }

            if (finished && (before < 0 || before < prog.Q.end_phase))
            {
                ProgressionCallbacks.PublishQuestCompleted(k, after);
            }
        }

        static bool ActorCallbacksGate() => ProgressionCallbacks.WantsQuestEvents;

        static int FindPhase(QuestTracker qt, string k) => FindProgress(qt, k, out _)?.phase ?? -1;

        static QuestTracker.QuestProgress FindProgress(QuestTracker qt, string k, out bool finished)
        {
            List<QuestTracker.QuestProgress> active = qt.AProg;
            if (active != null)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i].Q?.key == k)
                    {
                        finished = false;
                        return active[i];
                    }
                }
            }

            List<QuestTracker.QuestProgress> done = qt.AProgFinished;
            if (done != null)
            {
                for (int i = 0; i < done.Count; i++)
                {
                    if (done[i].Q?.key == k)
                    {
                        finished = true;
                        return done[i];
                    }
                }
            }

            finished = false;
            return null;
        }
    }
}
