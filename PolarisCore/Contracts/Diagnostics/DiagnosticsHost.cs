using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// Core 基础诊断与高级诊断模块之间的接线点。两者一起打包发行，视为始终存在；
    /// 这里只留一个什么都不做、绝不抛异常的空实现，兜住 <c>Plugin.Awake</c> 里
    /// <c>CoreErrorCapture.Install()</c> 到 PolarisDiagnostics 完成 <see cref="Register"/>
    /// 之间那一小段时序缺口。
    /// </summary>
    internal static class DiagnosticsHost
    {
        static IDiagnosticsBackend backend = NullDiagnosticsBackend.Instance;
        static bool registered;

        internal static void Register(IDiagnosticsBackend implementation)
        {
            if (implementation == null)
            {
                throw new ArgumentNullException(nameof(implementation));
            }

            if (registered)
            {
                throw new InvalidOperationException("A diagnostics backend has already been registered.");
            }

            backend = implementation;
            registered = true;
        }

        internal static void Report(Exception exception, string context, Assembly culprit)
            => backend.Report(exception, context, culprit);

        internal static void ReportLog(string condition, string stackTrace, string context)
            => backend.ReportLog(condition, stackTrace, context);

        internal static void CountLoggedError() => backend.RecordLoggedErrors(1);
        internal static void RaiseFatal(FatalError fatal) => backend.RaiseFatal(fatal);
        internal static bool IsFatal => backend.IsFatal;
        internal static IReadOnlyList<ErrorIncident> Incidents => backend.Incidents;
        internal static LastSessionInfo LastSession => backend.LastSession;
        internal static SessionEndKind LastSessionEnd => backend.LastSessionEnd;
        internal static FatalError FirstFatal => backend.FirstFatal;
        internal static int OtherFatalCount => backend.OtherFatalCount;
        internal static string FatalReportPath => backend.FatalReportPath;
        internal static string LastWrittenReportPath => backend.LastWrittenReportPath;
        internal static double SecondsSinceLastFrame => backend.SecondsSinceLastFrame;
        internal static int HangCount => backend.HangCount;

        internal static event Action<ErrorIncident> IncidentRecorded
        {
            add => backend.IncidentRecorded += value;
            remove => backend.IncidentRecorded -= value;
        }

        internal static event Action<HangReport> HangSuspected
        {
            add => backend.HangSuspected += value;
            remove => backend.HangSuspected -= value;
        }

        internal static IDisposable ExpectStall(string reason, double seconds)
            => backend.ExpectStall(reason, seconds);

        internal static IDisposable Activity(string what, Assembly owner = null)
            => backend.Activity(what, owner);

        internal static void Beat(int frameCount) => backend.Beat(frameCount);
        internal static void SetPaused(bool paused) => backend.SetPaused(paused);
        internal static void RecordCallbackInvocation(string ownerGuid, string context, double millis)
            => backend.RecordCallbackInvocation(ownerGuid, context, millis);
        internal static void RecordCallbackException(string ownerGuid, string context)
            => backend.RecordCallbackException(ownerGuid, context);
        internal static void Stop() => backend.Stop();
        internal static string Summary() => backend.Summary();
        internal static void CloseSession() => backend.CloseSession();

        /// <summary>Register 之前的占位实现：只把异常/致命错误直接记到 BepInEx 日志，不缓冲、不重放。</summary>
        sealed class NullDiagnosticsBackend : IDiagnosticsBackend
        {
            internal static readonly NullDiagnosticsBackend Instance = new();
            static readonly IDisposable Noop = new NoopDisposable();

            public bool IsFatal => false;
            public IReadOnlyList<ErrorIncident> Incidents => Array.Empty<ErrorIncident>();
            public LastSessionInfo LastSession => null;
            public SessionEndKind LastSessionEnd => SessionEndKind.Clean;
            public FatalError FirstFatal => null;
            public int OtherFatalCount => 0;
            public string FatalReportPath => null;
            public string LastWrittenReportPath => null;
            public double SecondsSinceLastFrame => 0d;
            public int HangCount => 0;

            public event Action<ErrorIncident> IncidentRecorded { add { } remove { } }
            public event Action<HangReport> HangSuspected { add { } remove { } }

            public void Report(Exception exception, string context, Assembly culprit)
            {
                if (exception != null)
                {
                    Plugin.Logger?.LogError($"[PolarisCore] {context ?? "captured exception"}: {exception}");
                }
            }

            public void ReportLog(string condition, string stackTrace, string context)
            {
                if (!string.IsNullOrEmpty(condition))
                {
                    Plugin.Logger?.LogError($"[PolarisCore] {context ?? "captured error"}: {condition}\n{stackTrace}");
                }
            }

            public void RecordLoggedErrors(long count) { }

            public void RaiseFatal(FatalError fatal)
            {
                if (fatal != null)
                {
                    Plugin.Logger?.LogError(
                        $"[PolarisCore] Fatal error reported by {fatal.Source}: {fatal.Reason?.ForReport}");
                }
            }

            public IDisposable ExpectStall(string reason, double seconds) => Noop;
            public IDisposable Activity(string what, Assembly owner) => Noop;
            public void Beat(int frameCount) { }
            public void SetPaused(bool paused) { }
            public void RecordCallbackInvocation(string ownerGuid, string context, double millis) { }
            public void RecordCallbackException(string ownerGuid, string context) { }
            public void Stop() { }
            public string Summary() => null;
            public void CloseSession() { }

            sealed class NoopDisposable : IDisposable
            {
                public void Dispose() { }
            }
        }
    }
}
