using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.API
{
    /// <summary>
    /// 游戏层的会话级服务：就绪门控、语言变化广播，以及由 <see cref="Plugin"/> 驱动的每帧泵。
    /// 服务的是 Polaris 自身子系统（资源、本地化、PUI），不属于下游模组的公开 API。
    /// </summary>
    internal static class GameSessionRuntime
    {
        /// <summary><c>MTRX.loaded</c> 等于 7 才算全部就绪：图标、Shader、私有初始化与音频 sheet 都完成。</summary>
        const int ReadyStage = 7;

        static readonly List<Action> pendingReady = new(4);

        static bool loggedReadyOnce;

        /// <summary>游戏资源是否已完全就绪；此之前碰 <c>MTRX.OMI</c>/<c>OMeshImages</c> 会 NullReferenceException。</summary>
        internal static bool IsReady => PolarisAPI.Game.Assets.LoadStage == ReadyStage;

        /// <summary>玩家切换游戏语言时触发，参数是 (旧语言, 新语言)。供 Polaris 自身子系统使用。</summary>
        internal static event Action<string, string> LocaleChanged;

        /// <summary>注册一个"等就绪后执行"的回调；已就绪则立即执行，否则排队等就绪那一帧统一执行。</summary>
        internal static void WhenReady(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (SafeIsReady)
            {
                action();
                return;
            }

            pendingReady.Add(action);
        }

        /// <summary>防御性读取：极早期访问游戏内部状态若抛异常，当作"还没好"处理，避免拖垮整条初始化链路。</summary>
        static bool SafeIsReady
        {
            get
            {
                try
                {
                    return IsReady;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning(
                        $"[Polaris] Exception while probing asset readiness; treating it as not ready: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>由 <see cref="Plugin.Update"/> 每帧调用。</summary>
        internal static void Pump()
        {
            bool ready = SafeIsReady;

            // 只在首次变为就绪的那一帧打日志。
            if (ready && !loggedReadyOnce)
            {
                loggedReadyOnce = true;
                Plugin.Logger.LogMessage(
                    $"[Polaris] Game assets became ready for the first time at frame {UnityEngine.Time.frameCount}.");
            }

            GameBinding.Pump();
            GameRuntime.Pump();

            // 派发这一帧探测到的事件，确保订阅者读到的状态是本帧最终结果。
            Infra.CallbackRuntime.Drain();

            DrainReady(ready);
        }

        /// <summary>由 <see cref="Plugin.LateUpdate"/> 每帧调用。</summary>
        internal static void PumpLate()
        {
            // 兜底 Update 之后、LateUpdate 之前发生的任何入队。
            Infra.CallbackRuntime.Drain();

            // 这一帧的回调都发完了，此刻清理失效的包装器表条目才安全。
            GameRuntime.Sweep();
        }

        /// <summary>由 <see cref="GameRuntime"/> 在探到语言变化时调用。两条路径共享同一次差分。</summary>
        internal static void NotifyLocaleChanged(string previous, string current)
        {
            Action<string, string> handlers = LocaleChanged;
            if (handlers == null)
            {
                return;
            }

            // 逐个调用而不是 handlers(...)：一个订阅者抛异常不该让后面的收不到通知。
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    // 面包屑：切语言回调可能重建大量文案/图像，属于高危的"一去不回"回调。
                    using (PolarisAPI.Health.Activity(
                        $"LocaleChanged callback ({Describe(handler)})", handler.Method?.DeclaringType?.Assembly))
                    {
                        ((Action<string, string>)handler)(previous, current);
                    }
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "LocaleChanged callback", handler.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError("[Polaris] A LocaleChanged callback threw an exception; ignored.");
                }
            }
        }

        /// <summary>世界卸载/回到标题：让全部游戏实例作废。</summary>
        internal static void ResetWorld() => GameRuntime.ResetWorld();

        static void DrainReady(bool ready)
        {
            if (!ready || pendingReady.Count == 0)
            {
                return;
            }

            // 先复制再清空：避免回调内部再次调用 WhenReady 新加入的项被这轮 Clear 误删。
            var toRun = new List<Action>(pendingReady);
            pendingReady.Clear();

            foreach (Action action in toRun)
            {
                try
                {
                    // 面包屑：下游模组"就绪后要做的重活"，最容易卡住，方便看门狗定位。
                    using (PolarisAPI.Health.Activity(
                        $"WhenReady callback ({Describe(action)})", action.Method?.DeclaringType?.Assembly))
                    {
                        action();
                    }
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "WhenReady callback", action.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError("[Polaris] A WhenReady callback threw an exception; ignored.");
                }
            }
        }

        /// <summary>面包屑用的一句话："类型.方法"；委托本身没有方法信息时给个占位。</summary>
        static string Describe(Delegate callback)
        {
            MethodInfo method = callback?.Method;
            if (method == null)
            {
                return "?";
            }

            string owner = method.DeclaringType?.Name ?? method.DeclaringType?.FullName;
            return owner != null ? $"{owner}.{method.Name}" : method.Name;
        }
    }
}
