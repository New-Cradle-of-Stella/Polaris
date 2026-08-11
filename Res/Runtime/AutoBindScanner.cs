using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.Mono.Bootstrap;

namespace Polaris.Res.Runtime
{
    /// <summary>
    /// 全自动发现：扫描全部已加载的 BepInEx 插件程序集，找到打了
    /// <see cref="PolarisResourceFolderAttribute"/> 的 static 类，把类特性指定的文件夹（相对
    /// 调用方 dll 所在目录的子路径，"和 dll 同级"）挂载进该模组的 <see cref="ModResources"/>，
    /// 再把类里标了 <see cref="PolarisResourceAttribute"/> 的 static 字段自动回填。
    /// <para>
    /// **类特性是自动绑定的必要条件**：一个类里有 <see cref="PolarisResourceAttribute"/> 字段，
    /// 但类本身没打 <see cref="PolarisResourceFolderAttribute"/>，不会被这里处理，只会记一条警告
    /// 提示漏加了特性——不会回退到"猜一个默认文件夹"。仍然保留
    /// <c>ModResources.MountDefault()</c>/<c>BindStaticFields(Type)</c> 这一套手动 API，给不想用
    /// 类特性、想按需动态取用资源，或者想用不受"dll 同级"限制的目录的模组用。
    /// </para>
    /// </summary>
    internal static class AutoBindScanner
    {
        internal static void ScanAll()
        {
            int totalFolders = 0;
            int totalFields = 0;

            // PluginAssemblies 已经做了去重（同一个程序集可能对应多个插件实例）。
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

                // 每个打了 [PolarisResourceFolder] 的类各用一张独立的挂载表：如果都合并挂进
                // 同一个 modId 共享的 ModResources，ResourceId 只看 modId+kind+path，不含
                // "挂载自哪个文件夹"，两个类各自的文件夹里只要出现同名相对路径就会撞上同一个
                // 缓存条目——谁先绑定谁的内容被后绑定的顶掉。按类型全限定名单独开一张表，
                // 从根上让"文件夹"成为资源身份的一部分，不需要改 ResourceId 本身，也不影响
                // 模组自己手动 PolarisResAPI.For(modId) 用的那张共享表。
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

        /// <summary>类里有 <see cref="PolarisResourceAttribute"/> 字段、但类本身没打
        /// <see cref="PolarisResourceFolderAttribute"/>：提示一下，免得模组作者以为自动绑定失效了
        /// 却找不到原因。</summary>
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
