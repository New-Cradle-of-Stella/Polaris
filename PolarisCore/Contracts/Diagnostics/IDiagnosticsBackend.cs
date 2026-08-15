using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// Core 通过这份契约把基础捕获结果交给当前诊断后端；高级分析、报告与看门狗由 PolarisDiagnostics 实现。
    /// </summary>
    internal interface IDiagnosticsBackend
    {
        void Report(Exception exception, string context, Assembly culprit);
        void ReportLog(string condition, string stackTrace, string context);
        void RecordLoggedErrors(long count);
        void RaiseFatal(FatalError fatal);

        bool IsFatal { get; }
        IReadOnlyList<ErrorIncident> Incidents { get; }
        event Action<ErrorIncident> IncidentRecorded;

        LastSessionInfo LastSession { get; }
        SessionEndKind LastSessionEnd { get; }
        IDisposable ExpectStall(string reason, double seconds);
        IDisposable Activity(string what, Assembly owner);
        event Action<HangReport> HangSuspected;
        double SecondsSinceLastFrame { get; }
        int HangCount { get; }

        FatalError FirstFatal { get; }
        int OtherFatalCount { get; }
        string FatalReportPath { get; }
        string LastWrittenReportPath { get; }

        void Beat(int frameCount);
        void SetPaused(bool paused);
        void RecordCallbackInvocation(string ownerGuid, string context, double millis);
        void RecordCallbackException(string ownerGuid, string context);
        void Stop();
        string Summary();
        void CloseSession();
    }
}
