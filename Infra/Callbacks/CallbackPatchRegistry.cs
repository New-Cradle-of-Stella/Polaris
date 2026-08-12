using System;
using System.Collections.Generic;
using System.Reflection;
using Polaris.Patch;

namespace Polaris.Infra
{
    /// <summary>
    /// <see cref="Plugin.PatchAllIndividually"/> 在每个补丁类应用成功/失败之后调用这里，
    /// 记下每一条回调本局到底有没有真的接上。
    /// <para>
    /// 一条回调可以有多个候选补丁（例如玩家侧和敌人侧各一个）；只要有<b>任何一个</b>失败过就不算
    /// 可用——即便另一个成功了，语义上大概率是"半个入口"，宁可报不可用也不要装作完好。
    /// </para>
    /// </summary>
    internal static class CallbackPatchRegistry
    {
        [Flags]
        enum PatchState { None = 0, Applied = 1, Failed = 2 }

        static readonly Dictionary<string, PatchState> state = new();
        static readonly Dictionary<string, string> reasons = new();

        internal static void ReportApplied(Type patchType)
        {
            foreach (PolarisPatchFeatureAttribute feature in patchType.GetCustomAttributes<PolarisPatchFeatureAttribute>())
            {
                Merge(feature.Feature, PatchState.Applied);
            }
        }

        internal static void ReportFailed(Type patchType, Exception ex)
        {
            foreach (PolarisPatchFeatureAttribute feature in patchType.GetCustomAttributes<PolarisPatchFeatureAttribute>())
            {
                Merge(feature.Feature, PatchState.Failed);
                reasons[feature.Feature] = $"Patch {patchType.Name} failed to apply this session: {ex.Message}";
            }
        }

        /// <summary>这条回调本局是不是完好接上了。没登记过的一律当作没接上。</summary>
        internal static bool IsAvailable(string feature)
            => state.TryGetValue(feature, out PatchState current)
               && (current & PatchState.Applied) != 0
               && (current & PatchState.Failed) == 0;

        /// <summary>诊断页/报告用：每条回调的可用性与失败原因。</summary>
        internal static IReadOnlyDictionary<string, string> Failures() => reasons;

        static void Merge(string feature, PatchState flag)
        {
            state.TryGetValue(feature, out PatchState existing);
            state[feature] = existing | flag;
        }
    }
}
