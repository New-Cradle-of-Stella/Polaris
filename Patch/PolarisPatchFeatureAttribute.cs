using System;

namespace Polaris.Patch
{
    /// <summary>
    /// 标在 Harmony 补丁类上，声明它是某个（或几个）回调的入口；<see cref="Plugin.PatchAllIndividually"/>
    /// 据此更新 <see cref="Infra.CallbackPatchRegistry"/>，使单个补丁失败只降级对应回调。
    /// 特性值用字符串而非枚举，因为静态/实例回调各有各的枚举，这里只需要一个稳定的诊断名字。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class PolarisPatchFeatureAttribute : Attribute
    {
        internal string Feature { get; }

        internal PolarisPatchFeatureAttribute(string feature) => Feature = feature;
    }
}
