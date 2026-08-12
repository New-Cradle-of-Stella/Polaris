using System;
using System.Collections.Generic;
using System.Reflection;
using Polaris.Patch;

namespace Polaris.Infra
{
    /// <summary>记录各回调补丁本局是否真的接上；一条回调若有任一候选补丁失败就整体判定不可用。</summary>
    internal static class CallbackPatchRegistry
    {
        [Flags]
        enum PatchState { None = 0, Applied = 1, Failed = 2 }

        static readonly Dictionary<string, PatchState> state = new();

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
            }
        }

        static void Merge(string feature, PatchState flag)
        {
            state.TryGetValue(feature, out PatchState existing);
            state[feature] = existing | flag;
        }
    }
}
