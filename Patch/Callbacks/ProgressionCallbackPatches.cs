using System.Collections.Generic;
using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary><c>NelM2DBase.setSF</c> 只是转发到这一个方法（已核对），打这一处就够，不会双发。</summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.setSF))]
    [PolarisPatchFeature("StoryFlagChanged")]
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
                GameCallbackPublishers.StoryFlagChanged(key, __state, after);
            }
        }
    }

    /// <summary>
    /// <c>QuestTracker.updateQuest</c> 内部有一堆提前返回分支（阶段没推进、任务已完成、任务不存在……）。
    /// 与其跟着每个分支判断，Prefix/Postfix 各查一次当前进度表，单纯比较 phase 前后值：
    /// 没找到 → 找到是 <c>QuestStarted</c>，phase 变了是 <c>QuestUpdated</c>，
    /// 落进已完成列表再补一条 <c>QuestCompleted</c>。
    /// </summary>
    [HarmonyPatch(typeof(QuestTracker), nameof(QuestTracker.updateQuest))]
    [PolarisPatchFeature("QuestStarted")]
    [PolarisPatchFeature("QuestUpdated")]
    [PolarisPatchFeature("QuestCompleted")]
    internal static class Patch_QuestTracker_updateQuest_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(QuestTracker __instance, string k, out int __state)
            => __state = FindProgress(__instance, k, out _)?.phase ?? -1;

        [HarmonyPostfix]
        static void Postfix(QuestTracker __instance, string k, int __state)
        {
            QuestTracker.QuestProgress prog = FindProgress(__instance, k, out bool finished);
            if (prog == null)
            {
                return;
            }

            int before = __state;
            int after = prog.phase;

            if (before < 0)
            {
                GameCallbackPublishers.QuestStarted(k, after);
            }

            if (after != before || finished)
            {
                GameCallbackPublishers.QuestUpdated(k, before, after, finished);
            }
        }

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

    /// <summary>任务从追踪列表移除。</summary>
    [HarmonyPatch(typeof(QuestTracker), nameof(QuestTracker.remove), new[] { typeof(string), typeof(bool) })]
    [PolarisPatchFeature("QuestRemoved")]
    internal static class Patch_QuestTracker_remove_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(string k, bool consider_finished) => GameCallbackPublishers.QuestRemoved(k, consider_finished);
    }
}
