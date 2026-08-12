using System;

namespace Polaris.Patch
{
    /// <summary>
    /// 标在一个 Harmony 补丁类上，声明"这个补丁类是某个（或几个）回调的入口"。
    /// <see cref="Plugin.PatchAllIndividually"/> 在补丁应用成功/失败后据此更新
    /// <see cref="Infra.CallbackPatchRegistry"/>，一个补丁坏了只会让对应的那几条回调降级，
    /// 不影响其它回调或其它 Polaris 功能。
    /// <para>
    /// 特性值是<b>字符串</b>而不是枚举，因为一个补丁贡献的既可能是静态回调也可能是实例回调，
    /// 两者是两个不同的枚举；而这里真正需要的只是一个诊断用的稳定名字。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class PolarisPatchFeatureAttribute : Attribute
    {
        internal string Feature { get; }

        internal PolarisPatchFeatureAttribute(string feature) => Feature = feature;
    }
}
