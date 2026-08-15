using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// Core 自带的全局兜底：接住 Unity、AppDomain 与 BepInEx 错误通道。
    /// 高级诊断模块存在时转交给其分析；不存在时仍由 Core 记录并保留有限的早期错误。
    /// 已知缺口：比 Polaris 更早 <c>Awake</c> 的插件自身抛的异常抓不到，但 BepInEx 自己会记录那类失败。
    /// </summary>
    internal static class CoreErrorCapture
    {
        /// <summary>BepInEx 转发 Unity 日志时用的 source 名；须过滤掉，避免与 Unity 日志回调重复收录。</summary>
        const string UnitySourceName = "Unity Log";

        static bool installed;
        static PolarisLogListener listener;

        internal static void Install()
        {
            if (installed)
            {
                return;
            }

            installed = true;

            // 每一步单独 try：三条通道互不依赖，一条挂不上不该连累其他。
            Try(() => Application.logMessageReceived += OnUnityLog);
            Try(() => AppDomain.CurrentDomain.UnhandledException += OnUnhandled);
            Try(() =>
            {
                listener = new PolarisLogListener();
                BepInEx.Logging.Logger.Listeners.Add(listener);
            });
        }

        internal static void Uninstall()
        {
            if (!installed)
            {
                return;
            }

            installed = false;

            Try(() => Application.logMessageReceived -= OnUnityLog);
            Try(() => AppDomain.CurrentDomain.UnhandledException -= OnUnhandled);
            Try(() =>
            {
                if (listener != null)
                {
                    BepInEx.Logging.Logger.Listeners.Remove(listener);
                    listener = null;
                }
            });
        }

        // ================== Unity 日志回调 ==================

        /// <summary>主线程版本（非 threaded），因为归因链要读 Unity API、写文件，都得在主线程做。</summary>
        static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Exception:
                    DiagnosticsHost.ReportLog(condition, stackTrace, null);
                    break;

                case LogType.Error:
                case LogType.Assert:
                    // 不建档，Debug.LogError 太常见；只计数，退出时汇总。
                    DiagnosticsHost.CountLoggedError();
                    break;
            }
        }

        // ================== AppDomain 未捕获异常 ==================

        /// <summary>后台线程未捕获异常，拿到真实 <see cref="Exception"/> 对象，归因质量最好。</summary>
        static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
            {
                DiagnosticsHost.Report(exception, "unhandled exception on a background thread", null);
            }
        }

        // ================== BepInEx 日志监听 ==================

        /// <summary>
        /// 监听 BepInEx 日志：<c>SourceName</c> 直接给出插件名。仅对 <c>Fatal</c> 或带
        /// <see cref="Exception"/> 对象的 <c>Error</c> 建档，普通 <c>LogError</c> 只计数。
        /// </summary>
        sealed class PolarisLogListener : ILogListener
        {
            public LogLevel LogLevelFilter => LogLevel.Error | LogLevel.Fatal;

            public void LogEvent(object sender, LogEventArgs args)
            {
                string source = args?.Source?.SourceName;
                if (source == null)
                {
                    return;
                }

                // Unity 日志已经从别的通道收过，跳过避免重复。
                if (source == UnitySourceName)
                {
                    return;
                }

                // 自己写的日志不能听，否则报告写失败会触发死循环。
                if (source == MyPluginInfo.PLUGIN_NAME)
                {
                    return;
                }

                bool fatal = (args.Level & LogLevel.Fatal) != 0;
                var exception = args.Data as Exception;

                if (exception != null)
                {
                    DiagnosticsHost.Report(exception, $"an error reported by {source}", AssemblyOf(source));
                    return;
                }

                if (fatal)
                {
                    DiagnosticsHost.Report(
                        new PluginReportedError(Convert.ToString(args.Data)),
                        $"a severe error reported by {source}",
                        AssemblyOf(source));
                    return;
                }

                DiagnosticsHost.CountLoggedError();
            }

            public void Dispose() { }
        }

        /// <summary>把 BepInEx 的 source 名（即插件名）换成对应的插件程序集。</summary>
        static Assembly AssemblyOf(string sourceName)
        {
            try
            {
                foreach (BepInEx.PluginInfo info in PolarisAPI.Modules.Plugins)
                {
                    if (string.Equals(info.Metadata?.Name, sourceName, StringComparison.Ordinal))
                    {
                        return info.Instance?.GetType().Assembly;
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>包装插件用 <c>LogFatal</c> 报出的无异常对象错误，让下游按统一路径处理。</summary>
        sealed class PluginReportedError : Exception
        {
            internal PluginReportedError(string message) : base(message) { }
        }

        static void Try(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] Failed to install an error capture channel; errors on that path will not be seen this session: {ex.Message}");
            }
        }
    }
}
