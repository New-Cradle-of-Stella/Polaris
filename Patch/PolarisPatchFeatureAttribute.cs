using System;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// 标在一个 Harmony 补丁类上，声明"这个补丁类是某个（或几个）回调种类的入口"。
    /// <see cref="Plugin.PatchAllIndividually"/> 在补丁应用成功/失败后据此更新
    /// <see cref="Infra.CallbackPatchRegistry"/>，一个补丁坏了只会让对应 Kind 降级，
    /// 不影响其它回调或其它 Polaris 功能。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class PolarisPatchFeatureAttribute : Attribute
    {
        internal GameCallbackKind Kind { get; }

        internal PolarisPatchFeatureAttribute(GameCallbackKind kind) => Kind = kind;
    }
}
