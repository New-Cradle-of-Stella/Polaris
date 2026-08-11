using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Polaris.Infra
{
    /// <summary>
    /// 全库唯一的类型扫描器，从 <see cref="PolarisAPI.Types"/> 取。
    /// <para>
    /// 之前设置项扫描、资源的自动发现与字段绑定、PUI/图发现各自带了一份
    /// "遍历程序集 + 兜 <see cref="ReflectionTypeLoadException"/>"的实现，
    /// 每加一个扫描器就多一份要跟着维护的反射样板。收在这里之后只剩一处兜底逻辑，
    /// 每个程序集的类型表也只解析一次（见 <see cref="Of"/> 的缓存）。
    /// </para>
    /// <para>
    /// <b>作用域优先选 <see cref="InPlugins"/></b>：它只看 BepInEx 真正加载了的插件程序集。
    /// <see cref="InAppDomain"/> 会把游戏本体那几个大程序集（Assembly-CSharp 有 5MB）也翻一遍，
    /// 只有当要找的类型可能位于"不是 BepInEx 插件的附属程序集"里时才值得付这个代价。
    /// </para>
    /// </summary>
    public sealed class TypesAPI
    {
        internal TypesAPI() { }

        // 程序集一旦加载，它的类型表就不会再变，缓存不存在失效问题。
        readonly Dictionary<Assembly, Type[]> cache = [];

        /// <summary>
        /// 取一个程序集里所有能加载的类型。加载不了的类型（引用了没装的可选依赖很常见）
        /// 会被剔除而不是让整次扫描失败；结果按程序集缓存。
        /// </summary>
        public IReadOnlyList<Type> Of(Assembly assembly)
        {
            if (assembly == null)
            {
                return [];
            }

            if (cache.TryGetValue(assembly, out Type[] cached))
            {
                return cached;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"[Polaris.Types] Failed to read the types of {assembly.GetName().Name}: {e.Message}");
                types = [];
            }

            cache[assembly] = types;
            return types;
        }

        /// <summary>BepInEx 已加载插件所在的程序集里的全部类型。绝大多数扫描都该用这个。</summary>
        public IEnumerable<Type> InPlugins()
        {
            foreach (Assembly assembly in PolarisAPI.Modules.PluginAssemblies)
            {
                foreach (Type type in Of(assembly))
                {
                    yield return type;
                }
            }
        }

        /// <summary>整个 AppDomain 里的全部类型，含游戏本体程序集。慢，除非确有必要否则用 <see cref="InPlugins"/>。</summary>
        public IEnumerable<Type> InAppDomain()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in Of(assembly))
                {
                    yield return type;
                }
            }
        }

        /// <summary>插件程序集里标了 <typeparamref name="TAttr"/> 的类型，连同该特性实例一起给出。</summary>
        public IEnumerable<(Type Type, TAttr Attribute)> InPluginsWith<TAttr>() where TAttr : Attribute
            => WithAttribute<TAttr>(InPlugins());

        /// <summary>整个 AppDomain 里标了 <typeparamref name="TAttr"/> 的类型。</summary>
        public IEnumerable<(Type Type, TAttr Attribute)> InAppDomainWith<TAttr>() where TAttr : Attribute
            => WithAttribute<TAttr>(InAppDomain());

        /// <summary>
        /// 在给定类型集合里挑出标了 <typeparamref name="TAttr"/> 的。读特性本身也可能抛
        /// （特性的构造函数在别的程序集里且加载失败），逐个吞掉并继续，不让一个坏类型
        /// 掀掉整次扫描。
        /// </summary>
        public IEnumerable<(Type Type, TAttr Attribute)> WithAttribute<TAttr>(IEnumerable<Type> types)
            where TAttr : Attribute
        {
            foreach (Type type in types)
            {
                TAttr attribute;
                try
                {
                    attribute = (TAttr)Attribute.GetCustomAttribute(type, typeof(TAttr));
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"[Polaris.Types] Failed to read {typeof(TAttr).Name} on {type.FullName}: {e.Message}");
                    continue;
                }

                if (attribute != null)
                {
                    yield return (type, attribute);
                }
            }
        }
    }
}
