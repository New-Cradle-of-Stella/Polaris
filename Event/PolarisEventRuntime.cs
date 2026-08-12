using System;
using System.Collections.Generic;
using System.IO;

namespace Polaris.Event
{
    /// <summary>
    /// 把已注册的事件定义解包成真实 <c>.cmd</c> 文件，写到 <c>plugins/Polaris/events/</c>
    /// 下——游戏引擎自己的 <c>EV.getEventContent</c> 只认 <c>StreamingAssets/evt</c> 那棵树，
    /// 让它找到这些文件靠 <c>Patch\Patch_EV_getEventContent.cs</c> 那个 Harmony 补丁，这里只负责
    /// 落盘和记账。
    /// <para>
    /// 这一步现在是纯 <see cref="System.IO"/> 操作，不碰 <c>EV</c> 的任何方法，所以不需要像以前
    /// （调用 <c>EV.setEventContent</c> 那版）那样纠结"阶段0还没验证 EV 初始化时机、Plugin.Start()
    /// 阶段调它安不安全"——可以在 <see cref="PolarisEventRegistryScanner.ScanAll"/> 扫描完成的
    /// 同时就把所有事件文件写盘，不用等到真正 <see cref="PolarisEvent.Start"/>/
    /// <see cref="PolarisEvent.Change"/> 被调用。<see cref="EnsureAllInstalled"/> 在
    /// <see cref="PolarisEvent.StartCore"/>/<c>ChangeCore</c> 里仍然保留一次调用，作为"扫描结束后
    /// 才运行时直接调 Register"这种迟到注册场景的兜底——字典判重让重复调用完全免费。
    /// </para>
    /// </summary>
    internal static class PolarisEventRuntime
    {
        static readonly Dictionary<string, string> installed = new Dictionary<string, string>(PolarisEventId.KeyComparer);

        internal static void EnsureAllInstalled()
        {
            foreach (var definition in PolarisEventRegistry.All)
            {
                EnsureInstalled(definition);
            }
        }

        internal static void EnsureInstalled(PolarisEventDefinition definition)
        {
            if (definition == null || installed.ContainsKey(definition.RuntimeKey))
            {
                return;
            }

            try
            {
                string path = ResolveFilePath(definition);
                WriteIfChanged(path, definition.CommandText);
                installed[definition.RuntimeKey] = path;
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "PolarisEvent.EnsureInstalled");
            }
        }

        /// <summary>给 <c>Patch_EV_getEventContent</c> 用：这个运行时键有没有解包出来的文件，在哪。</summary>
        internal static bool TryGetFilePath(string runtimeKey, out string path)
            => installed.TryGetValue(runtimeKey, out path);

        static string ResolveFilePath(PolarisEventDefinition definition)
        {
            string relative = definition.LogicalId.Replace('/', Path.DirectorySeparatorChar) + ".cmd";
            return Path.Combine(PolarisAPI.Paths.EventsDir, definition.Namespace, relative);
        }

        static void WriteIfChanged(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            if (File.Exists(path) && File.ReadAllText(path) == content)
            {
                return; // 内容没变就不摸 mtime，避免每次启动都重写几百个小文件。
            }

            File.WriteAllText(path, content);
        }
    }
}
