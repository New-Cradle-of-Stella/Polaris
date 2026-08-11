using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 归因总入口：解异常链 → 走栈标归属 → 查补丁嫌疑 → 认异常形状 → 出结论。
    /// 纯计算，不写日志、不落盘、不管去重——那些是 <see cref="ErrorRegistry"/> 的事。
    /// </summary>
    internal static class ErrorAnalyzer
    {
        // ================== 入口 ==================

        /// <summary>
        /// 分析一个真实的异常对象。<paramref name="explicitCulprit"/> 由调用方点名——
        /// Polaris 自己那些 catch 点本来就知道是谁的错（正在初始化的是哪个模块、
        /// 正在调用的是谁的回调），这条路完全不必走堆栈推断，又快又准。
        /// </summary>
        internal static ErrorIncident Analyze(Exception exception, string context, Assembly explicitCulprit)
        {
            Exception root = Unwrap(exception);

            List<ErrorFrame> frames = StackAttribution.FromException(root);
            if (frames.Count == 0 && !ReferenceEquals(root, exception))
            {
                // 内层异常没带上堆栈（重新抛出、或者是构造出来没抛过的），退回外层的。
                frames = StackAttribution.FromException(exception);
            }

            string typeName = root?.GetType().FullName;
            string message = root?.Message;

            return Compose(
                typeName,
                message,
                context,
                frames,
                SafeStackText(root) ?? SafeStackText(exception),
                DescribeChain(exception),
                explicitCulprit);
        }

        /// <summary>
        /// 分析只有文本的错误（<c>Application.logMessageReceived</c> 给的就是这个）。
        /// <paramref name="condition"/> 形如 <c>NullReferenceException: Object reference not set…</c>。
        /// </summary>
        internal static ErrorIncident Analyze(string condition, string stackTraceText, string context)
        {
            SplitCondition(condition, out string typeName, out string message);

            return Compose(
                typeName,
                message,
                context,
                StackAttribution.FromText(stackTraceText),
                stackTraceText,
                condition,
                null);
        }

        // ================== 组装 ==================

        static ErrorIncident Compose(
            string exceptionType,
            string message,
            string context,
            List<ErrorFrame> frames,
            string rawStack,
            string chain,
            Assembly explicitCulprit)
        {
            List<ErrorSuspect> suspects = StackAttribution.CollectSuspects(frames);
            ErrorVerdict verdict = Blame(frames, suspects, explicitCulprit);

            ExceptionShapes.Shape shape = ExceptionShapes.Of(exceptionType, message);
            if (shape != null)
            {
                verdict.Diagnosis = shape.Diagnosis;
                verdict.SuggestedAction = shape.Action;
            }

            DateTime now = DateTime.Now;
            return new ErrorIncident
            {
                Fingerprint = ErrorIncident.ComputeFingerprint(exceptionType, frames),
                FirstSeen = now,
                LastSeen = now,
                Count = 1,
                ExceptionType = exceptionType ?? "<unknown exception>",
                Message = message,
                Context = context,
                Verdict = verdict,
                Frames = frames,
                RawStackTrace = rawStack,
                ExceptionChain = chain,
            };
        }

        // ================== 定责 ==================

        /// <summary>
        /// 定责规则，从内向外走：
        /// <list type="number">
        /// <item>调用方点了名 → 直接采信。</item>
        /// <item>跳过运行库帧——<c>ArgumentNullException</c> 的抛出点在 mscorlib 里，
        /// 但责任显然不在 BCL。</item>
        /// <item>第一个属于模组 / Polaris 的帧 = 主责。<b>模组调用原版代码、原版因为参数不合法
        /// 而抛，责任仍在模组</b>，所以是"第一个可定责的帧"而不是"最内层的帧"。</item>
        /// <item>一路没有模组帧，就看沿途原版方法的补丁嫌疑人（transpiler 不留帧，这条是
        /// 它唯一的出路）。</item>
        /// <item>嫌疑人也没有 → 原版游戏。</item>
        /// </list>
        /// </summary>
        static ErrorVerdict Blame(
            IReadOnlyList<ErrorFrame> frames, List<ErrorSuspect> suspects, Assembly explicitCulprit)
        {
            var verdict = new ErrorVerdict { Suspects = suspects };

            if (explicitCulprit != null)
            {
                AssemblyOwner named = AssemblyOwnerIndex.Of(explicitCulprit);
                verdict.Culprit = named;
                verdict.Kind = named.Kind;
                verdict.Confidence = ErrorConfidence.High;
                verdict.Reason = "The exception was caught right where its calling code ran; the culprit needs no inference.";
                return verdict;
            }

            foreach (ErrorFrame frame in frames)
            {
                if (frame.Owner == null
                    || frame.Owner.Kind == OwnerKind.Runtime
                    || frame.Owner.Kind == OwnerKind.Dynamic)
                {
                    continue;
                }

                if (frame.Owner.IsBlamable)
                {
                    verdict.Culprit = frame.Owner;
                    verdict.Kind = frame.Owner.Kind;
                    verdict.Confidence = ErrorConfidence.High;
                    verdict.Reason = $"Its own code appears in the stack ({frame.TypeName}.{frame.MethodName}).";
                    return verdict;
                }
            }

            List<ErrorSuspect> blamable = suspects.Where(s => s.Owner.IsBlamable).ToList();

            if (blamable.Count == 1)
            {
                ErrorSuspect only = blamable[0];
                bool ilRewrite = only.Reason != null && only.Reason.Contains("IL rewrite");

                verdict.Culprit = only.Owner;
                verdict.Kind = only.Owner.Kind;

                // IL 改写意味着它的代码就在那个方法体里，所以证据比"打了个 postfix 但没留下帧"
                // 强一档。后者更可能是补丁跑完就返回了、真正出事的是原版自己。
                verdict.Confidence = ilRewrite ? ErrorConfidence.Medium : ErrorConfidence.Low;
                verdict.Reason = $"None of its frames are in the stack, but it {only.Reason}.";
                return verdict;
            }

            if (blamable.Count > 1)
            {
                verdict.Kind = OwnerKind.Unknown;
                verdict.Confidence = ErrorConfidence.Low;
                verdict.Reason =
                    $"The vanilla method that failed was modified by {blamable.Count} mods at once, so they cannot be"
                    + " told apart -- disable the suspects below one at a time to verify.";
                return verdict;
            }

            if (frames.Any(f => f.Owner != null && f.Owner.Kind == OwnerKind.Vanilla))
            {
                verdict.Kind = OwnerKind.Vanilla;
                verdict.Confidence = ErrorConfidence.Medium;
                verdict.Reason = "The entire stack is vanilla code, and no method along the way was modified by a mod.";
                return verdict;
            }

            if (frames.Any(f => f.Owner != null && f.Owner.Kind == OwnerKind.Framework))
            {
                verdict.Kind = OwnerKind.Framework;
                verdict.Confidence = ErrorConfidence.Low;
                verdict.Reason = "The stack lands only inside BepInEx / Harmony; most likely a patch failed to apply.";
                return verdict;
            }

            verdict.Kind = OwnerKind.Unknown;
            verdict.Confidence = ErrorConfidence.Unknown;
            verdict.Reason = "Not a single frame in the stack maps to a loaded assembly; cannot determine a verdict.";
            return verdict;
        }

        // ================== 异常链 ==================

        /// <summary>
        /// 剥掉只起转发作用的外壳异常，定责要看最里面那个真正出事的。
        /// <c>TargetInvocationException</c> 的剥法在 <c>SettingsAttributeScanner</c> 里早有先例。
        /// </summary>
        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;

            // 有环的异常链是构造得出来的，加个上限免得在错误处理里死循环。
            for (int depth = 0; depth < 16 && current != null; depth++)
            {
                switch (current)
                {
                    case TargetInvocationException invocation when invocation.InnerException != null:
                        current = invocation.InnerException;
                        continue;
                    case TypeInitializationException initialization when initialization.InnerException != null:
                        current = initialization.InnerException;
                        continue;
                    case AggregateException aggregate when aggregate.InnerExceptions.Count == 1:
                        current = aggregate.InnerExceptions[0];
                        continue;
                    default:
                        return current;
                }
            }

            return current ?? exception;
        }

        /// <summary>
        /// 完整异常链的文本。定责只看最内层，但展示要给全：
        /// <c>TypeInitializationException</c> 这类外壳本身就带着"是哪个静态构造函数炸了"的信息。
        /// </summary>
        static string DescribeChain(Exception exception)
        {
            var builder = new StringBuilder();

            Exception current = exception;
            for (int depth = 0; depth < 16 && current != null; depth++)
            {
                if (depth > 0)
                {
                    builder.Append(new string(' ', depth * 2)).Append("+- inner exception: ");
                }

                builder.Append(current.GetType().FullName).Append(": ").AppendLine(current.Message);
                current = current.InnerException;
            }

            return builder.ToString().TrimEnd();
        }

        static string SafeStackText(Exception exception)
        {
            try
            {
                return exception?.StackTrace;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>把 <c>"NullReferenceException: 消息"</c> 拆成类型名与消息。</summary>
        static void SplitCondition(string condition, out string typeName, out string message)
        {
            typeName = null;
            message = condition;

            if (string.IsNullOrEmpty(condition))
            {
                return;
            }

            int separator = condition.IndexOf(": ", StringComparison.Ordinal);
            if (separator <= 0)
            {
                return;
            }

            string head = condition.Substring(0, separator);

            // 只有看起来真像个异常类型名才拆：普通的 Debug.LogError("加载失败: xxx") 也带冒号，
            // 拆错了会把"加载失败"当成异常类型，指纹跟着一起错。
            if (head.EndsWith("Exception", StringComparison.Ordinal)
                || head.EndsWith("Error", StringComparison.Ordinal))
            {
                typeName = head;
                message = condition.Substring(separator + 2);
            }
        }
    }
}
