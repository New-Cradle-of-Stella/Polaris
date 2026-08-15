using System;
using System.Collections.Generic;
using System.Reflection;
using Polaris.Diagnostics;

namespace Polaris.Infra
{
    /// <summary>
    /// 错误上报与分析，从 <see cref="PolarisAPI.Errors"/> 取：主动上报异常，或用
    /// <see cref="Guard(Action, string, Assembly)"/> 包一层调用别人的代码，异常就地上报并吞掉。
    /// </summary>
    public sealed class ErrorsAPI
    {
        internal ErrorsAPI() { }

        /// <summary>上报一个异常，责任方由 Polaris 走堆栈推断；<paramref name="context"/> 是给人看的一句话，会出现在日志和报告里。</summary>
        public void Report(Exception exception, string context = null)
        {
            DiagnosticsHost.Report(exception, context, null);
        }

        /// <summary>上报一个异常并直接点名责任方，跳过堆栈推断——调用方已知是谁的错时用这个。</summary>
        /// <param name="culprit">责任方所在的程序集，通常是 <c>someObject.GetType().Assembly</c>。</param>
        public void Report(Exception exception, string context, Assembly culprit)
        {
            DiagnosticsHost.Report(exception, context, culprit);
        }

        /// <summary>
        /// 安全地执行一段代码：抛异常就上报并吞掉，返回是否执行成功。
        /// <paramref name="culprit"/> 留空时按 <paramref name="action"/> 自己所在的程序集算账；如果传入的是
        /// Polaris 自己的 lambda 而它内部才调模组代码，需显式传入模组程序集，否则锅会记在 Polaris 头上。
        /// </summary>
        public bool Guard(Action action, string context, Assembly culprit = null)
        {
            if (action == null)
            {
                return true;
            }

            try
            {
                // 留一条面包屑，供卡死看门狗回答"卡住时在执行谁的代码"；culprit 原样传下去，不在这里补算 OwnerOf（反射开销只在真出错时才值得付）。
                using (DiagnosticsHost.Activity(context, culprit))
                {
                    action();
                }

                return true;
            }
            catch (Exception ex)
            {
                DiagnosticsHost.Report(ex, context, culprit ?? OwnerOf(action));
                return false;
            }
        }

        /// <summary><see cref="Guard(Action, string, Assembly)"/> 的有返回值版本；出错时返回 <paramref name="fallback"/>。</summary>
        public T Guard<T>(Func<T> func, T fallback, string context, Assembly culprit = null)
        {
            if (func == null)
            {
                return fallback;
            }

            try
            {
                using (DiagnosticsHost.Activity(context, culprit))
                {
                    return func();
                }
            }
            catch (Exception ex)
            {
                DiagnosticsHost.Report(ex, context, culprit ?? OwnerOf(func));
                return fallback;
            }
        }

        /// <summary>
        /// 报出一个致命错误：模组环境坏了、这一局不该继续，Polaris 会写日志与报告并在标题画面拦住玩家、只留"退出游戏"一个出口
        /// （单个功能坏掉请用 <see cref="Report(Exception, string)"/>）。用在模块初始化阶段；本方法只登记，不阻塞、不抛异常、不结束进程。
        /// </summary>
        public void Fatal(FatalError fatal)
        {
            DiagnosticsHost.RaiseFatal(fatal);
        }

        /// <summary>本局是否已经报出过致命错误（标题画面会拦住玩家并请他退出）。</summary>
        public bool IsFatal => DiagnosticsHost.IsFatal;

        /// <summary>本局已归档的错误，按首次出现顺序；同一类只有一条，重复次数看 <see cref="ErrorIncident.Count"/>。</summary>
        public IReadOnlyList<ErrorIncident> Session => DiagnosticsHost.Incidents;

        /// <summary>有新错误归档时触发（同一类只触发一次）；订阅者抛异常会被吞掉，不连累其它订阅者。</summary>
        public event Action<ErrorIncident> IncidentRecorded
        {
            add => DiagnosticsHost.IncidentRecorded += value;
            remove => DiagnosticsHost.IncidentRecorded -= value;
        }

        static Assembly OwnerOf(Delegate action)
        {
            try
            {
                return action.Method?.DeclaringType?.Assembly;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
