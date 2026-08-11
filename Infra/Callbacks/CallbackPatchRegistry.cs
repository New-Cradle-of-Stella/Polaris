using System;
using System.Reflection;
using Polaris.API;
using Polaris.Patch;

namespace Polaris.Infra
{
    /// <summary>
    /// <see cref="Plugin.PatchAllIndividually"/> 在每个补丁类应用成功/失败之后调用这里，
    /// 把结果转译成对应 <see cref="GameCallbackKind"/> 的 <see cref="CallbackRegistry"/> 状态。
    /// 一个 Kind 可以有多个候选补丁（例如同一 Kind 由 Prefix 类和 Postfix 类分别贡献），
    /// 全部成功才算 <see cref="GameCallbackAvailability.Available"/>。
    /// </summary>
    internal static class CallbackPatchRegistry
    {
        [Flags]
        enum PatchState { None = 0, Applied = 1, Failed = 2 }

        static readonly System.Collections.Generic.Dictionary<GameCallbackKind, PatchState> state = new();

        internal static void ReportApplied(Type patchType)
        {
            foreach (PolarisPatchFeatureAttribute feature in patchType.GetCustomAttributes<PolarisPatchFeatureAttribute>())
            {
                Merge(feature.Kind, PatchState.Applied);
                RefreshRegistry(feature.Kind);
            }
        }

        internal static void ReportFailed(Type patchType, Exception ex)
        {
            foreach (PolarisPatchFeatureAttribute feature in patchType.GetCustomAttributes<PolarisPatchFeatureAttribute>())
            {
                Merge(feature.Kind, PatchState.Failed);
                CallbackRegistry.Update(feature.Kind, GameCallbackAvailability.Unsupported,
                    $"Patch {patchType.Name} failed to apply this session: {ex.Message}");
            }
        }

        static void Merge(GameCallbackKind kind, PatchState flag)
        {
            state.TryGetValue(kind, out PatchState existing);
            state[kind] = existing | flag;
        }

        static void RefreshRegistry(GameCallbackKind kind)
        {
            if (!state.TryGetValue(kind, out PatchState current))
            {
                return;
            }

            // 同一个 Kind 只要有任何一个候选补丁失败过，就不能标 Available——即便另一个候选
            // 补丁成功了，语义上大概率是"半个入口"，宁可报 Unsupported 也不要装作完好。
            if ((current & PatchState.Failed) == 0)
            {
                CallbackRegistry.Update(kind, GameCallbackAvailability.Available, null);
            }
        }
    }
}
