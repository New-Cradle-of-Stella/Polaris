using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Unity.Mono.Bootstrap;

namespace Polaris.Settings
{
    /// <summary>
    /// 扫描已加载插件程序集里标了 <see cref="PolarisSettingGroupAttribute"/> 的类，
    /// 把其中 <see cref="PolarisSettingAttribute"/> 字段注册成设置项。
    /// 作用域用 <see cref="Infra.TypesAPI.InPluginsWith{TAttr}"/>（只看 BepInEx 真正加载了的
    /// 插件程序集）——遍历整个 AppDomain 意味着要把 5MB 的 Assembly-CSharp 也翻一遍，不值得。
    /// <para>
    /// 这里只做"字段 → <see cref="SettingsGroupBuilder"/> 调用"的翻译，外加挂上字段读写委托，
    /// 让模组的静态字段成为值的真身；取值范围校验之类的规则一概留在 Builder 那一层。
    /// </para>
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

            // 分区数与类数不一定相等：一个模组可以把设置项分散在几个类里，界面上还是一个分区。
            if (typeCount > 0)
            {
                Plugin.Logger.LogMessage(
                    $"[Polaris.Settings] 从 {typeCount} 个类注册了 {PolarisAPI.Settings.Groups.Count} 组设置项。");
            }
        }

        /// <summary>返回是否真的注册了至少一个设置项。</summary>
        static bool ScanType(Type type, PolarisSettingGroupAttribute groupAttr)
        {
            // 按 Order 再按声明顺序排；MetadataToken 在同一个类型里单调递增，是声明顺序的可靠代理。
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
                    $"[Polaris.Settings] {type.FullName} 标了 PolarisSettingGroup 但没有任何 PolarisSetting 字段。");
                return false;
            }

            SettingsGroupBuilder builder;
            try
            {
                builder = PolarisAPI.Settings.BuildFor(groupAttr.ModId, groupAttr.DisplayName, groupAttr.Order);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[Polaris.Settings] {type.FullName} 的分区声明非法，整组跳过：{e.Message}");
                return false;
            }

            int added = 0;
            foreach ((FieldInfo field, PolarisSettingAttribute attr) in fields)
            {
                if (field.IsInitOnly || field.IsLiteral)
                {
                    Plugin.Logger.LogWarning(
                        $"[Polaris.Settings] {type.FullName}.{field.Name} 是 readonly/const，无法回写，跳过。");
                    continue;
                }

                ValueSettingDefinition setting;
                try
                {
                    setting = AddField(builder, field, attr);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"[Polaris.Settings] 注册 {type.FullName}.{field.Name} 失败，跳过：{e.Message}");
                    continue;
                }

                if (setting == null)
                {
                    continue;
                }

                // 字段读写委托与变更回调都必须在 Register 之前挂上：Register 会立刻绑定配置文件
                // 并把上次存的值回灌回来，那一步就要靠 FieldSetter 写进模组的静态字段。
                // （回灌走的是 notify: false，所以不会误触发 OnChanged——初始化是 OnLoaded 的活。）
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

            // 到这里这一组的值已经全部落进字段了，模组可以放心地把它们应用到运行状态。
            if (!string.IsNullOrEmpty(groupAttr.OnLoaded))
            {
                InvokeLoaded(type, groupAttr.OnLoaded);
            }

            return true;
        }

        /// <summary>
        /// 解析 <see cref="PolarisSettingAttribute.OnChanged"/>：优先取 <c>M(T value)</c>，
        /// 没有就退回 <c>M()</c>（模组自己读字段）。找不到只记错误不抛——
        /// 一个模组把方法名打错了不该让其余模组的设置项一起注册不上。
        /// </summary>
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
                // 事件带的是"存储类型"（double 字段实际存的是 float），按形参类型转一次再传。
                return v => Invoke(oneArg, [ConvertTo(v, paramType)]);
            }

            if (noArg != null)
            {
                return _ => Invoke(noArg, null);
            }

            Plugin.Logger.LogError(
                $"[Polaris.Settings] {owner.FullName} 里找不到 OnChanged 指定的静态方法 {methodName}，" +
                "签名应为 static void M() 或 static void M(T value)。该项的变更回调将不生效。");
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
                    $"[Polaris.Settings] {owner.FullName} 里找不到 OnLoaded 指定的静态方法 {methodName}()。");
                return;
            }

            Invoke(m, null);
        }

        /// <summary>
        /// 调模组的回调。反射调用会把异常包成 TargetInvocationException，这里拆开再记，
        /// 否则日志里只看得到一层没有信息量的包装。
        /// </summary>
        static void Invoke(MethodInfo method, object[] args)
        {
            try
            {
                method.Invoke(null, args);
            }
            catch (TargetInvocationException e)
            {
                // 责任人就是这个方法本身所在的程序集，不必走堆栈推断；拆开包装是为了不让
                // 报告里的堆栈只剩一层没有信息量的 TargetInvocationException。
                PolarisAPI.Errors.Report(e.InnerException ?? e, $"调用 {method.DeclaringType?.FullName}.{method.Name}", method.DeclaringType?.Assembly);
                Plugin.Logger.LogError(
                    $"[Polaris.Settings] {method.DeclaringType?.FullName}.{method.Name} 抛异常，已忽略。");
            }
            catch (Exception e)
            {
                PolarisAPI.Errors.Report(e, $"调用 {method.DeclaringType?.FullName}.{method.Name}", method.DeclaringType?.Assembly);
                Plugin.Logger.LogError(
                    $"[Polaris.Settings] 调用 {method.DeclaringType?.FullName}.{method.Name} 失败，已忽略。");
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
                // 文本行用的 DsnDataInput 没有 fnHover 字段，说明框永远弹不出来。
                // 不吭声的话模组作者会以为自己写的 Desc 丢了。
                if (!string.IsNullOrEmpty(attr.Desc))
                {
                    Plugin.Logger.LogWarning(
                        $"[Polaris.Settings] {field.DeclaringType?.FullName}.{field.Name} 是文本项，" +
                        "游戏的输入框控件不支持悬停说明，Desc 不会显示。");
                }

                return builder.Text(id, label, (string)current ?? "", attr.MaxLength, desc: attr.Desc);
            }

            Plugin.Logger.LogWarning(
                $"[Polaris.Settings] {field.DeclaringType?.FullName}.{field.Name} 的类型 {t.Name} 不受支持，跳过。" +
                "支持的类型：bool / int / float / double / string / enum。");
            return null;
        }

        /// <summary>
        /// 只有 <c>double</c> 需要转：它走的是 float 滑条，存进去的值比字段窄一档。
        /// 其余类型（bool / int / string / enum）存的就是字段自己的类型，原样回写。
        /// </summary>
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
