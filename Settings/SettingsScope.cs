using System;

namespace Polaris.Settings
{
    /// <summary>
    /// 单个模组的设置读写作用域，从 <c>PolarisAPI.Settings.For(modId)</c> 取。
    /// <para>
    /// 和 <c>PolarisResAPI.For(modId)</c> 一样先取作用域再操作，而不是每个方法都带一个
    /// <c>modId</c> 参数：绝大多数调用方全程只关心自己那一个模组，取一次拿在手里更顺，
    /// 也避免了"传错 modId 静默拿到别人的值"。
    /// </para>
    /// <para>
    /// 取值有三种形态，全系列统一：<see cref="Get{T}"/> 找不到抛异常、
    /// <see cref="TryGet{T}"/> 返回 bool、<see cref="GetOrDefault{T}"/> 返回兜底值。
    /// </para>
    /// </summary>
    public sealed class SettingsScope
    {
        readonly SettingsAPI owner;

        internal SettingsScope(SettingsAPI owner, string modId)
        {
            this.owner = owner;
            ModId = modId;
        }

        public string ModId { get; }

        /// <summary>这个模组有没有声明过设置项（即有没有任何类标了 <see cref="PolarisSettingGroupAttribute"/>）。</summary>
        public bool Exists => Group != null;

        /// <summary>该模组的设置分区；没声明过设置项时为 null。</summary>
        public SettingGroup Group => owner.FindGroup(ModId);

        /// <summary>读一个设置项的当前值；不存在或类型对不上直接抛。</summary>
        /// <exception cref="ArgumentException">设置项不存在，或实际类型与 <typeparamref name="T"/> 不符</exception>
        public T Get<T>(string id)
        {
            if (!TryGet(id, out T value))
            {
                throw new ArgumentException(
                    $"Mod \"{ModId}\" has no setting \"{id}\" of type {typeof(T).Name}.", nameof(id));
            }

            return value;
        }

        /// <summary>读一个设置项的当前值；不存在或类型对不上返回 false。</summary>
        public bool TryGet<T>(string id, out T value)
        {
            value = default;

            if (Group is not SettingGroup group
                || !group.TryGet(id, out SettingDefinition setting)
                || setting is not ValueSettingDefinition v
                || v.BoxedValue is not T typed)
            {
                return false;
            }

            value = typed;
            return true;
        }

        /// <summary>读一个设置项的当前值；不存在或类型对不上返回 <paramref name="fallback"/>。</summary>
        public T GetOrDefault<T>(string id, T fallback = default)
            => TryGet(id, out T value) ? value : fallback;

        /// <summary>
        /// 写一个设置项的值（等价于玩家在界面上改了它）。改动会在下次提交时落盘，
        /// 也可以调 <c>PolarisAPI.Settings.Save()</c> 立刻写。
        /// </summary>
        /// <returns>设置项不存在或类型不符时返回 false，不抛。</returns>
        public bool Set<T>(string id, T value)
        {
            if (Group is not SettingGroup group
                || !group.TryGet(id, out SettingDefinition setting)
                || setting is not ValueSettingDefinition v
                || !v.ValueType.IsInstanceOfType(value))
            {
                return false;
            }

            v.BoxedValue = value;
            return true;
        }
    }
}
