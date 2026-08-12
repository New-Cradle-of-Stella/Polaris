using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.Mono.Bootstrap;

namespace Polaris.Res.Runtime
{
    /// <summary>
    /// 全自动发现：扫描全部已加载的 BepInEx 插件程序集，找到打了 <see cref="PolarisResourceFolderAttribute"/> 的 static 类，
    /// 挂载类特性指定的文件夹并回填打了 <see cref="PolarisResourceAttribute"/> 的静态字段。
    /// 类特性是自动绑定的必要条件，没打特性的类只会收到警告，不会猜测默认文件夹；仍可用 <c>ModResources.MountDefault()</c>/<c>BindStaticFields(Type)</c> 手动绑定。
    /// </summary>
    internal static class AutoBindScanner
    {
        internal static void ScanAll()
        {
            int totalFolders = 0;
            int totalFields = 0;

            // PluginAssemblies 已做去重（同一程序集可能对应多个插件实例）。
            foreach (Assembly assembly in PolarisAPI.Modules.PluginAssemblies)
            {
                try
                {
                    (int folders, int fields) = ScanAssembly(assembly);
                    totalFolders += folders;
                    totalFields += fields;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisRes] Failed to auto-scan assembly {assembly.GetName().Name}: {ex}");
                }
            }

            Plugin.Logger.LogMessage(
                $"[PolarisRes] Automatic resource discovery finished: found {totalFolders} resource folders, bound {totalFields} resource fields.");
        }

        /// <returns>(挂载的不同文件夹数, 成功绑定的字段数)——都只统计这个程序集自己的。</returns>
        private static (int Folders, int Fields) ScanAssembly(Assembly assembly)
        {
            string dllDirectory = Path.GetDirectoryName(assembly.Location);
            string modId = assembly.GetName().Name;

            HashSet<string> mountedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int fieldsBound = 0;

            foreach (Type type in PolarisAPI.Types.Of(assembly))
            {
                var folderAttr = (PolarisResourceFolderAttribute)Attribute.GetCustomAttribute(
                    type, typeof(PolarisResourceFolderAttribute));

                if (folderAttr == null)
                {
                    WarnIfOrphaned(type);
                    continue;
                }

                string absoluteFolder;
                try
                {
                    absoluteFolder = Path.GetFullPath(
                        Path.Combine(string.IsNullOrEmpty(dllDirectory) ? "." : dllDirectory, folderAttr.Folder ?? string.Empty));
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError(
                        $"[PolarisRes] The [PolarisResourceFolder(\"{folderAttr.Folder}\")] path on {type.FullName} is invalid: {ex.Message}");
                    continue;
                }

                // 每个打了 [PolarisResourceFolder] 的类各用独立挂载表（按类型全限定名区分），
                // 避免不同类的同名相对路径撞上共享 ModResources 里的同一缓存条目。
                ModResources classResources = PolarisResAPI.For(modId + "#" + type.FullName);
                classResources.Mount(absoluteFolder);
                mountedFolders.Add(absoluteFolder);
                fieldsBound += classResources.BindStaticFields(type);
            }

            if (mountedFolders.Count > 0)
            {
                Plugin.Logger.LogInfo(
                    $"[PolarisRes] {modId}: found {mountedFolders.Count} resource folders, bound {fieldsBound} resource fields.");
            }

            return (mountedFolders.Count, fieldsBound);
        }

        /// <summary>类里有 <see cref="PolarisResourceAttribute"/> 字段但没打 <see cref="PolarisResourceFolderAttribute"/> 时提示一下。</summary>
        private static void WarnIfOrphaned(Type type)
        {
            int orphanCount = 0;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (Attribute.IsDefined(field, typeof(PolarisResourceAttribute)))
                {
                    orphanCount++;
                }
            }

            if (orphanCount > 0)
            {
                Plugin.Logger.LogWarning(
                    $"[PolarisRes] {type.FullName} has {orphanCount} [PolarisResource] fields but the class is missing " +
                    "[PolarisResourceFolder]; auto-binding skipped.");
            }
        }
    }
}
