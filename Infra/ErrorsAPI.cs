using System;
using System.Collections.Generic;
using System.Reflection;
using Polaris.Diagnostics;

namespace Polaris.Infra
{
    /// <summary>
    /// 错误上报与分析，从 <see cref="PolarisAPI.Errors"/> 取。
    /// <para>
    /// Polaris 会自动兜底捕获（Unity 异常、后台线程未捕获异常、插件的严重错误日志）并判断
    /// 责任方是<b>某个模组 / Polaris 自己 / 原版游戏</b>，写进 BepInEx 日志和
    /// <see cref="PathsAPI.ReportsDir"/> 下的报告文件。这个 API 是给两类调用方用的：
    /// </para>
    /// <list type="bullet">
    /// <item>在 catch 里主动上报，比自己 <c>LogError</c> 多出归因、去重、报告归档；</item>
    /// <item>调用别人的代码（模组注册的回调、事件订阅者）时用 <see cref="Guard(Action, string, Assembly)"/>
    /// 包一层，异常就地上报并吞掉，不让一个人的问题炸穿整条调用链。</item>
    /// </list>
    /// </summary>
    public sealed class ErrorsAPI
    {
        internal ErrorsAPI() { }

        /// <summary>
        /// 上报一个异常，责任方由 Polaris 走堆栈推断。
        /// <paramref name="context"/> 是给人看的一句话，例如 <c>"加载存档缩略图"</c>；
        /// 它会出现在日志和报告里，对定位问题的帮助往往比堆栈还大。
        /// </summary>
        public void Report(Exception exception, string context = null)
        {
            ErrorRegistry.Submit(exception, context, null);
        }

        /// <summary>
        /// 上报一个异常并<b>直接点名责任方</b>，跳过堆栈推断。
        /// <para>
        /// 调用方已经知道是谁的错时一律用这个：正在初始化的是哪个模块、正在调用的是谁的回调，
        /// 这些信息比任何堆栈推断都准，也省掉一次走栈。
        /// </para>
        /// </summary>
        /// <param name="culprit">责任方所在的程序集，通常是 <c>someObject.GetType().Assembly</c>。</param>
        public void Report(Exception exception, string context, Assembly culprit)
        {
            ErrorRegistry.Submit(exception, context, culprit);
        }

        /// <summary>
        /// 安全地执行一段代码：抛异常就上报并吞掉，返回是否执行成功。
        /// <para>
        /// <paramref name="culprit"/> 留空时按 <paramref name="action"/> 自己所在的程序集算账，
        /// 这对"调用别人注册进来的回调"正好合适。但如果这里传进来的是 Polaris 自己写的一个
        /// lambda、而它内部才去调模组的代码，就要显式把模组的程序集传进来，否则锅会记在 Polaris 头上。
        /// </para>
        /// </summary>
        public bool Guard(Action action, string context, Assembly culprit = null)
        {
            if (action == null)
            {
                return true;
            }

            try
            {
                // 顺手留一条面包屑：Guard 包住的正是"别人的代码"，而卡死看门狗要回答的
                // 恰好是"卡住的时候在执行谁的代码"（见 <see cref="Diagnostics.MainThreadBeat"/>）。
                // 责任方原样传下去、不在这里补算 OwnerOf——那是一次反射，只有真出错时才值得付。
                using (Diagnostics.MainThreadBeat.Enter(context, culprit))
                {
                    action();
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorRegistry.Submit(ex, context, culprit ?? OwnerOf(action));
                return false;
            }
        }

        /// <summary>
        /// <see cref="Guard(Action, string, Assembly)"/> 的有返回值版本；出错时返回
        /// <paramref name="fallback"/>。
        /// </summary>
        public T Guard<T>(Func<T> func, T fallback, string context, Assembly culprit = null)
        {
            if (func == null)
            {
                return fallback;
            }

            try
            {
                using (Diagnostics.MainThreadBeat.Enter(context, culprit))
                {
                    return func();
                }
            }
            catch (Exception ex)
            {
                ErrorRegistry.Submit(ex, context, culprit ?? OwnerOf(func));
                return fallback;
            }
        }

        /// <summary>
        /// 报出一个<b>致命错误</b>：模组环境本身坏了，这一局不该继续。Polaris 会立刻把它写进
        /// BepInEx 日志和报告文件，然后在标题画面拦住菜单、把原因摆给玩家看，并只留"退出游戏"
        /// 一个出口（见 <see cref="Diagnostics.FatalError"/> 里关于判据的说明——这是很重的处置，
        /// 单个功能坏掉请用 <see cref="Report(Exception, string)"/>）。
        /// <para>
        /// <b>用在模块初始化阶段。</b>这条路径的设计前提是"标题画面还没起来"：报告与日志在调用的
        /// 那一刻就落好，展示推迟到标题画面（否则玩家只会看到游戏闪一下就没了）。如果玩家已经
        /// 进了游戏，这一页要等他下次回到标题画面才出现——它拦的是标题菜单，拦不住一局进行中的游戏。
        /// </para>
        /// <para>
        /// 本方法只登记，不阻塞、不抛异常、不当场结束进程；调用方在它返回之后仍应正常收尾
        /// （该注册的还是注册、该 return 的 return），把"什么时候退"交给玩家点那个按钮。
        /// </para>
        /// </summary>
        public void Fatal(FatalError fatal)
        {
            FatalRegistry.Raise(fatal);
        }

        /// <summary>本局是否已经报出过致命错误（标题画面会拦住玩家并请他退出）。</summary>
        public bool IsFatal => FatalRegistry.Any;

        /// <summary>
        /// 本局已归档的错误，按首次出现顺序。同一类错误只有一条，
        /// 重复次数看 <see cref="ErrorIncident.Count"/>。
        /// </summary>
        public IReadOnlyList<ErrorIncident> Session => ErrorRegistry.Incidents;

        /// <summary>
        /// 有新错误归档时触发（同一类只触发一次）。订阅者自己抛异常会被吞掉，
        /// 不会连累其它订阅者，也不会反过来再触发一轮归档。
        /// </summary>
        public event Action<ErrorIncident> IncidentRecorded
        {
            add => ErrorRegistry.Recorded += value;
            remove => ErrorRegistry.Recorded -= value;
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
