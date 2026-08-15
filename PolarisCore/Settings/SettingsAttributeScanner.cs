using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Unity.Mono.Bootstrap;

namespace Polaris.Settings
{
    /// <summary>
    /// 扫描已加载插件程序集里标了 <see cref="PolarisSettingGroupAttribute"/> 的类，把 <see cref="PolarisSettingAttribute"/> 字段注册成设置项。
    /// 只看 BepInEx 真正加载的插件程序集（<see cref="Infra.TypesAPI.InPluginsWith{TAttr}"/>），避免遍历整个 AppDomain。
    /// </summary>
    internal static class SettingsAttributeScanner
    {
        static bool scanned;

        /// <summary>在 <c>Plugin.Start</c> 里、所有插件 Awake 与 Polaris 模块 Init 之后调用一次。</summary>
        internal static void ScanAll()
        {
            if (scanned)
            {
                return;
            }

            scanned = true;

            int typeCount = 0;
            foreach ((Type type, PolarisSettingGroupAttribute groupAttr)
                     in PolarisAPI.Types.InPluginsWith<PolarisSettingGroupAttribute>())
            {
                if (ScanType(type, groupAttr))
                {
                    typeCount++;
                }
            }

            // 分区数与类数不一定相等：一个模组的设置项可分散在多个类中。
            if (typeCount > 0)
            {
                Plugin.Logger.LogMessage(
                    $"[Polaris.Settings] Registered {PolarisAPI.Settings.Groups.Count} setting groups from {typeCount} classes.");
            }
        }

        /// <summary>返回是否真的注册了至少一个设置项。</summary>
        static bool ScanType(Type type, PolarisSettingGroupAttribute groupAttr)
        {
            // 按 Order 再按声明顺序排（MetadataToken 同类型内单调递增，代理声明顺序）。
            List<(FieldInfo Field, PolarisSettingAttribute Attr)> fields =
                type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Select(f => (Field: f, Attr: (PolarisSettingAttribute)Attribute.GetCustomAttribute(
                        f, typeof(PolarisSettingAttribute))))
                    .Where(x => x.Attr != null)
                    .OrderBy(x => x.Attr.Order)
                    .ThenBy(x => x.Field.MetadataToken)
                    .ToList();

            if (fields.Count == 0)
            {
                Plugin.Logger.LogWarning(
                    $"[Polaris.Settings] {type.FullName} is marked PolarisSettingGroup but has no PolarisSetting fields.");
                return false;
            }

            SettingsGroupBuilder builder;
            try
            {
                builder = PolarisAPI.Settings.BuildFor(groupAttr.ModId, groupAttr.DisplayName, groupAttr.Order);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris.Settings] The section declaration on {type.FullName} is invalid; the whole group is skipped: {e.Message}");
                return false;
            }

            int added = 0;
            foreach ((FieldInfo field, PolarisSettingAttribute attr) in fields)
            {
                if (field.IsInitOnly || field.IsLiteral)
                {
                    Plugin.Logger.LogWarning(
                        $"[Polaris.Settings] {type.FullName}.{field.Name} is readonly/const and cannot be written back; skipped.");
                    continue;
                }

                ValueSettingDefinition setting;
                try
                {
                    setting = AddField(builder, field, attr);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"[Polaris.Settings] Failed to register {type.FullName}.{field.Name}; skipped: {e.Message}");
                    continue;
                }

                if (setting == null)
                {
                    continue;
                }

                // 须在 Register 前挂好：Register 会立刻绑定配置并靠 FieldSetter 回灌上次存的值（notify: false，不触发 OnChanged）。
                Type fieldType = field.FieldType;
                setting.FieldSetter = v => field.SetValue(null, ConvertTo(v, fieldType));

                if (!string.IsNullOrEmpty(attr.OnChanged))
                {
                    Action<object> handler = ResolveChangeHandler(type, attr.OnChanged, fieldType);
                    if (handler != null)
                    {
                        setting.Changed += handler;
                    }
                }

                added++;
            }

            if (added == 0)
            {
                return false;
            }

            builder.Register();

            // 此时该组的值已全部落进字段，可安全应用到运行状态。
            if (!string.IsNullOrEmpty(groupAttr.OnLoaded))
            {
                InvokeLoaded(type, groupAttr.OnLoaded);
            }

            return true;
        }

        /// <summary>解析 <see cref="PolarisSettingAttribute.OnChanged"/>：优先 <c>M(T value)</c>，否则 <c>M()</c>；找不到只记错误不抛，避免拖垮其它模组注册。</summary>
        static Action<object> ResolveChangeHandler(Type owner, string methodName, Type valueType)
        {
            MethodInfo noArg = null;
            MethodInfo oneArg = null;

            foreach (MethodInfo m in owner.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != methodName)
                {
                    continue;
                }

                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length == 0)
                {
                    noArg ??= m;
                }
                else if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(valueType))
                {
                    oneArg ??= m;
                }
            }

            if (oneArg != null)
            {
                Type paramType = oneArg.GetParameters()[0].ParameterType;
                // 事件带的是存储类型（如 double 字段实际存 float），需按形参类型转一次。
                return v => Invoke(oneArg, [ConvertTo(v, paramType)]);
            }

            if (noArg != null)
            {
                return _ => Invoke(noArg, null);
            }

            Plugin.Logger.LogError(
                $"[Polaris.Settings] Could not find the static method {methodName} named by OnChanged in {owner.FullName}. " +
                "The signature must be static void M() or static void M(T value). The change callback for this entry will not take effect.");
            return null;
        }

        static void InvokeLoaded(Type owner, string methodName)
        {
            MethodInfo m = owner.GetMethod(
                methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null);

            if (m == null)
            {
                Plugin.Logger.LogError(
                    $"[Polaris.Settings] Could not find the static method {methodName}() named by OnLoaded in {owner.FullName}.");
                return;
            }

            Invoke(m, null);
        }

        /// <summary>调模组的回调；反射调用把异常包成 TargetInvocationException，这里拆开再记以保留有效堆栈。</summary>
        static void Invoke(MethodInfo method, object[] args)
        {
            try
            {
                method.Invoke(null, args);
            }
            catch (TargetInvocationException e)
            {
                PolarisAPI.Errors.Report(e.InnerException ?? e, $"calling {method.DeclaringType?.FullName}.{method.Name}", method.DeclaringType?.Assembly);
                Plugin.Logger.LogError(
                    $"[Polaris.Settings] {method.DeclaringType?.FullName}.{method.Name} threw an exception; ignored.");
            }
            catch (Exception e)
            {
                PolarisAPI.Errors.Report(e, $"calling {method.DeclaringType?.FullName}.{method.Name}", method.DeclaringType?.Assembly);
                Plugin.Logger.LogError(
                    $"[Polaris.Settings] Failed to call {method.DeclaringType?.FullName}.{method.Name}; ignored.");
            }
        }

        /// <summary>按字段类型分派到对应的 Builder 方法。字段类型不受支持时记警告并返回 null。</summary>
        static ValueSettingDefinition AddField(SettingsGroupBuilder builder, FieldInfo field, PolarisSettingAttribute attr)
        {
            string id = string.IsNullOrEmpty(attr.Id) ? field.Name : attr.Id;
            string label = string.IsNullOrEmpty(attr.Label) ? field.Name : attr.Label;
            object current = field.GetValue(null);
            Type t = field.FieldType;

            if (t == typeof(bool))
            {
                return builder.Toggle(id, label, (bool)current, attr.Desc,
                                      attr.Choices is { Length: 2 } ? attr.Choices : null);
            }

            if (t.IsEnum)
            {
                return builder.EnumOfType(t, id, label, current, attr.Choices, attr.Desc);
            }

            if (t == typeof(int))
            {
                if (attr.Choices is { Length: > 0 })
                {
                    return builder.Choice(id, label, attr.Choices, (int)current, attr.Desc);
                }

                int max = double.IsNaN(attr.Max) ? 100 : (int)attr.Max;
                int step = double.IsNaN(attr.Step) ? 1 : (int)attr.Step;
                return builder.Int(id, label, (int)attr.Min, max, (int)current, step, attr.Desc);
            }

            if (t == typeof(float) || t == typeof(double))
            {
                float max = double.IsNaN(attr.Max) ? 1f : (float)attr.Max;
                float step = double.IsNaN(attr.Step) ? 0.1f : (float)attr.Step;
                return builder.Slider(id, label, (float)attr.Min, max, Convert.ToSingle(current), step, attr.Desc);
            }

            if (t == typeof(string))
            {
                // 文本行的 DsnDataInput 无 fnHover 字段，说明框弹不出来，须警告避免作者以为 Desc 丢了。
                if (!string.IsNullOrEmpty(attr.Desc))
                {
                    Plugin.Logger.LogWarning(
                        $"[Polaris.Settings] {field.DeclaringType?.FullName}.{field.Name} is a text entry, and " +
                        "the game's input-box control does not support hover descriptions, so Desc will not be shown.");
                }

                return builder.Text(id, label, (string)current ?? "", attr.MaxLength, desc: attr.Desc);
            }

            Plugin.Logger.LogWarning(
                $"[Polaris.Settings] The type {t.Name} of {field.DeclaringType?.FullName}.{field.Name} is not supported; skipped. " +
                "Supported types: bool / int / float / double / string / enum.");
            return null;
        }

        /// <summary>只有 <c>double</c> 需要转换（走 float 滑条，存值窄一档）；其余类型原样回写。</summary>
        static object ConvertTo(object value, Type target)
        {
            if (value == null || target.IsInstanceOfType(value))
            {
                return value;
            }

            return target == typeof(double) ? Convert.ChangeType(value, target) : value;
        }

    }
}
