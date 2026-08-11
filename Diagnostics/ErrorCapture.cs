using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 全局兜底：把游戏里各条错误通道都接过来，交给 <see cref="ErrorRegistry"/>。
    /// 由 <see cref="Plugin.Awake"/> 尽早安装。
    /// <para>
    /// <b>已知的覆盖缺口</b>：BepInEx 不保证插件加载顺序，比 Polaris 更早 <c>Awake</c> 的
    /// 插件在它自己的 <c>Awake</c> 里抛的异常我们抓不到。这个缺口可以接受——BepInEx 自己会
    /// 记录"插件 X 加载失败"，那种情况责任人本来就已经被点名了。
    /// </para>
    /// </summary>
    internal static class ErrorCapture
    {
        /// <summary>
        /// BepInEx 把 Unity 日志转发进自己日志系统时用的 source 名（<c>UnityLogSource</c>，
        /// 默认开启，见 BepInEx.cfg 的 <c>UnityLogListening</c>）。必须认出来并丢掉：
        /// 同一条 Unity 异常我们已经从 <see cref="Application.logMessageReceived"/> 收过一遍了。
        /// </summary>
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

            // 每一步单独 try：三条通道互不依赖，其中一条挂不上不该连累另外两条。
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

        /// <summary>
        /// 主线程版本，不是 <c>logMessageReceivedThreaded</c>。归因要读
        /// <c>UnityChainloader.Instance.Plugins</c>、要写文件、要碰 Unity API，全是主线程的事；
        /// threaded 版本能多抓到后台线程的日志，但代价是整条分析链都要变成线程安全的，
        /// 不划算——后台线程的未捕获异常另有 <see cref="OnUnhandled"/> 兜着。
        /// </summary>
        static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Exception:
                    ErrorRegistry.Submit(condition, stackTrace, null);
                    break;

                case LogType.Error:
                case LogType.Assert:
                    // 不建档：Debug.LogError 太随意，原版自己就在大量使用，按异常对待会把
                    // 报告淹掉。只记一笔，退出时汇总。
                    ErrorRegistry.CountLoggedError();
                    break;
            }
        }

        // ================== AppDomain 未捕获异常 ==================

        /// <summary>
        /// 后台线程里没人接的异常走这里。拿得到真的 <see cref="Exception"/> 对象，
        /// 归因质量是三条通道里最好的。
        /// <para>
        /// 刻意不用 <c>FirstChanceException</c>：那个对<b>每一个</b>被 catch 的异常都触发，
        /// 光是 BCL 内部的控制流异常就足以把它变成噪音发生器。
        /// </para>
        /// </summary>
        static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
            {
                ErrorRegistry.Submit(exception, "后台线程未捕获的异常", null);
            }
        }

        // ================== BepInEx 日志监听 ==================

        /// <summary>
        /// 听 BepInEx 日志的价值在于 <c>LogEventArgs.Source.SourceName</c> ——它就是插件名，
        /// 于是"是谁在报错"不需要任何堆栈解析就有答案。
        /// <para>
        /// 但只认两种情况：<c>Fatal</c>，以及 <c>Data</c> 真的是一个 <see cref="Exception"/>
        /// 对象的 <c>Error</c>。普通的 <c>LogError("读不到配置")</c> 只计数——不少模组把
        /// LogError 当普通提示用，全都建档写报告会让报告彻底失去可信度。
        /// </para>
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

                // BepInEx 转发过来的 Unity 日志：Application.logMessageReceived 已经收过。
                if (source == UnitySourceName)
                {
                    return;
                }

                // 我们自己写的日志。不丢就是死循环：报告写失败 → 记 error → 又被自己听到。
                if (source == MyPluginInfo.PLUGIN_NAME)
                {
                    return;
                }

                bool fatal = (args.Level & LogLevel.Fatal) != 0;
                var exception = args.Data as Exception;

                if (exception != null)
                {
                    ErrorRegistry.Submit(exception, $"{source} 报告的错误", AssemblyOf(source));
                    return;
                }

                if (fatal)
                {
                    ErrorRegistry.Submit(
                        new PluginReportedError(Convert.ToString(args.Data)),
                        $"{source} 报告的严重错误",
                        AssemblyOf(source));
                    return;
                }

                ErrorRegistry.CountLoggedError();
            }

            public void Dispose() { }
        }

        /// <summary>
        /// 把 BepInEx 的 source 名换成插件程序集。source 名就是 <c>BepInPlugin</c> 的插件名
        /// （<c>BaseUnityPlugin</c> 用它建 <c>ManualLogSource</c>）。
        /// </summary>
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

        /// <summary>
        /// 插件用 <c>LogFatal</c> 报出来的、没有异常对象的错误。包一层是为了让下游
        /// （指纹、报告、告知页）不必为"没有异常对象"这种情况另开一条分支。
        /// </summary>
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
                Plugin.Logger.LogWarning($"[Polaris] 错误捕获通道安装失败，这一路的错误本局收不到：{ex.Message}");
            }
        }
    }
}
