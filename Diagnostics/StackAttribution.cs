using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 走堆栈，把每一帧标上归属，顺带查出沿途的原版方法被谁改过。
    /// <para>
    /// 两条来路都要支持，而且<b>字符串那条是主路不是兜底</b>：
    /// <c>Application.logMessageReceived</c> 的回调签名是
    /// <c>(string condition, string stackTrace, LogType type)</c>——根本没有
    /// <see cref="Exception"/> 对象。全局兜底抓到的绝大多数异常都只有字符串堆栈。
    /// </para>
    /// </summary>
    internal static class StackAttribution
    {
        /// <summary>堆栈再深也没有分析价值，超过这个数就截断，免得报告变成裹脚布。</summary>
        const int MaxFrames = 48;

        // ================== 有 Exception 对象的那条路 ==================

        /// <summary>
        /// 从异常对象走栈。信息最全：拿得到 <see cref="MethodBase"/>，能直接做补丁反查，
        /// 也能让 Harmony 把 DMD 帧还原成原始方法。
        /// </summary>
        internal static List<ErrorFrame> FromException(Exception exception)
        {
            var frames = new List<ErrorFrame>();
            if (exception == null)
            {
                return frames;
            }

            StackFrame[] raw;
            try
            {
                raw = new StackTrace(exception, false).GetFrames();
            }
            catch (Exception)
            {
                return frames;
            }

            if (raw == null)
            {
                return frames;
            }

            foreach (StackFrame frame in raw)
            {
                if (frames.Count >= MaxFrames)
                {
                    break;
                }

                MethodBase method = ResolveMethod(frame);
                if (method == null)
                {
                    continue;
                }

                Type declaring = SafeDeclaringType(method);
                var entry = new ErrorFrame
                {
                    TypeName = declaring?.FullName ?? "<未知类型>",
                    MethodName = method.Name,
                    Owner = AssemblyOwnerIndex.Of(declaring?.Assembly),
                };

                Annotate(entry, method);
                frames.Add(entry);
            }

            return frames;
        }

        /// <summary>
        /// 把一帧解析成方法。Harmony 打过补丁的方法在栈上是一个动态方法（DMD），
        /// 直接 <c>frame.GetMethod()</c> 拿到的是个匿名玩意儿，归属只会是"动态生成"——
        /// 必须先请 Harmony 还原回原始方法，否则整条堆栈上被补丁过的原版方法全都不可读。
        /// </summary>
        static MethodBase ResolveMethod(StackFrame frame)
        {
            try
            {
                MethodBase original = Harmony.GetOriginalMethodFromStackframe(frame);
                if (original != null)
                {
                    return original;
                }
            }
            catch (Exception)
            {
                // 还原失败就用原始帧，能读多少读多少。
            }

            try
            {
                return frame.GetMethod();
            }
            catch (Exception)
            {
                return null;
            }
        }

        static Type SafeDeclaringType(MethodBase method)
        {
            try
            {
                return method.DeclaringType;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ================== 只有字符串的那条路 ==================

        /// <summary>
        /// 解析 Unity / Mono 给的字符串堆栈。两种常见形状都要认：
        /// <code>
        ///   at nel.title.SceneTitleTemp.initButtons () [0x00000] in &lt;hash&gt;:0
        ///   Foo.Bar () (at &lt;hash&gt;:0)
        ///   UnityEngine.Debug:Log(Object)
        /// </code>
        /// 拿不到 <see cref="MethodBase"/>，所以归属只能靠命名空间反查
        /// （<see cref="AssemblyOwnerIndex.OfTypeName"/>），补丁反查也退化成按名字找
        /// （<see cref="PatchSuspects.FindPatched"/>）。
        /// </summary>
        internal static List<ErrorFrame> FromText(string stackTrace)
        {
            var frames = new List<ErrorFrame>();
            if (string.IsNullOrEmpty(stackTrace))
            {
                return frames;
            }

            foreach (string line in stackTrace.Split('\n'))
            {
                if (frames.Count >= MaxFrames)
                {
                    break;
                }

                ErrorFrame frame = ParseLine(line);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }

        static ErrorFrame ParseLine(string line)
        {
            string text = line.Trim();
            if (text.Length == 0)
            {
                return null;
            }

            if (text.StartsWith("at ", StringComparison.Ordinal))
            {
                text = text.Substring(3).TrimStart();
            }

            // 签名 = 第一个左括号之前的部分。泛型参数里也可能有括号，但方法名之前不会有，
            // 取第一个就够。
            int paren = text.IndexOf('(');
            string signature = (paren > 0 ? text.Substring(0, paren) : text).Trim();
            if (signature.Length == 0)
            {
                return null;
            }

            string typeName;
            string methodName;

            // "UnityEngine.Debug:Log" 这种冒号形式是 Unity 自己拼的，和 Mono 的点号形式混在一起出现。
            int colon = signature.LastIndexOf(':');
            int dot = signature.LastIndexOf('.');

            if (colon > 0 && colon > dot)
            {
                typeName = signature.Substring(0, colon);
                methodName = signature.Substring(colon + 1);
            }
            else if (dot > 0)
            {
                typeName = signature.Substring(0, dot);
                methodName = signature.Substring(dot + 1);
            }
            else
            {
                return null;
            }

            var entry = new ErrorFrame
            {
                TypeName = typeName,
                MethodName = methodName,
                Owner = AssemblyOwnerIndex.OfTypeName(typeName),
            };

            if (entry.Owner.Kind == OwnerKind.Vanilla)
            {
                Annotate(entry, PatchSuspects.FindPatched(typeName, methodName));
            }

            return entry;
        }

        // ================== 补丁标注 ==================

        /// <summary>
        /// 只给原版帧做补丁反查。模组自己的方法被别的模组打补丁也可能发生，但那属于
        /// 模组之间的事；这里要解决的是"原版方法被改过，所以看起来像原版的锅"这个特定误判。
        /// </summary>
        static void Annotate(ErrorFrame frame, MethodBase method)
        {
            if (method == null || frame.Owner.Kind != OwnerKind.Vanilla)
            {
                return;
            }

            PatchSuspects.Scan scan = PatchSuspects.Of(method, $"{frame.TypeName}.{frame.MethodName}");
            if (!scan.Any)
            {
                return;
            }

            frame.IsPatched = true;
            frame.PatchNote = scan.Note;
        }

        /// <summary>
        /// 收集整条堆栈上的补丁嫌疑人。<see cref="Annotate"/> 只在帧上留了一句说明，
        /// 定责需要的是去重之后的完整名单，所以这里再走一遍。
        /// </summary>
        internal static List<ErrorSuspect> CollectSuspects(IReadOnlyList<ErrorFrame> frames)
        {
            var result = new List<ErrorSuspect>();
            var seen = new HashSet<AssemblyOwner>();

            foreach (ErrorFrame frame in frames)
            {
                if (!frame.IsPatched)
                {
                    continue;
                }

                MethodBase method = PatchSuspects.FindPatched(frame.TypeName, frame.MethodName);
                PatchSuspects.Scan scan = PatchSuspects.Of(method, $"{frame.TypeName}.{frame.MethodName}");

                foreach (ErrorSuspect suspect in scan.Suspects)
                {
                    if (seen.Add(suspect.Owner))
                    {
                        result.Add(suspect);
                    }
                }
            }

            return result;
        }
    }
}
