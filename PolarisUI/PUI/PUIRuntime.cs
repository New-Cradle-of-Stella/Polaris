using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using nel;
using Polaris.PUI.HotReload;
using UnityEngine;

namespace Polaris.PUI
{
    /// <summary>单个 <see cref="IPUI"/> 的运行时实例，持有专属 GameObject 并以状态机驱动构建/显示/隐藏/销毁的生命周期。</summary>
    public class PUIRuntime
    {
        private static readonly ConditionalWeakTable<IPUI, PUIRuntime> handlerIndex = new ConditionalWeakTable<IPUI, PUIRuntime>();

        // 用于语言切换时枚举所有已构建实例；ConditionalWeakTable 不可枚举，故单独维护弱引用列表，扫描时顺手清理死条目。
        private static readonly List<WeakReference<PUIRuntime>> liveInstances = new List<WeakReference<PUIRuntime>>();

        public IPUI Handler { get; }

        public PUIState State { get; private set; } = PUIState.Unbuilt;

        // protected 是为了让 PUIHotReloadRuntime 能 override Build() 并复用 Teardown/Activate/Deactivate。
        protected GameObject host;
        protected UiBoxDesignerFamily family;
        protected UiBoxDesigner window;

        private List<PUISolution> owners;

        public PUIRuntime(IPUI handler)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));

            // Add 在键已存在时会抛异常，天然禁止同一个 IPUI 被重复包裹。
            handlerIndex.Add(handler, this);
            liveInstances.Add(new WeakReference<PUIRuntime>(this));
        }

        /// <summary>推荐的创建入口：按 handler 所在程序集是否启用热重载自动选型运行时类型；不涉及名字表注册。</summary>
        public static PUIRuntime Create(IPUI handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (PUIManager.IsHotReloadEnabled(handler.GetType().Assembly))
            {
                var runtime = new PUIHotReloadRuntime(handler);
                PUIManager.TrackHotReload(runtime);
                PUIManager.EnsureHotReloadServerStarted();
                return runtime;
            }

            return new PUIRuntime(handler);
        }

        /// <summary>反查某个 <see cref="IPUI"/> 对应的 <see cref="PUIRuntime"/>；未被 <see cref="Create"/> 包裹过时为 null。</summary>
        public static PUIRuntime Of(IPUI handler)
        {
            if (handler == null)
            {
                return null;
            }

            return handlerIndex.TryGetValue(handler, out PUIRuntime runtime) ? runtime : null;
        }

        /// <summary>触发一次状态迁移；非法迁移（例如已销毁后继续操作）会抛出异常。</summary>
        public void Show() => Fire(PUITrigger.Show);

        /// <summary>隐藏（不销毁，可再次 Show）。</summary>
        public void Hide() => Fire(PUITrigger.Hide);

        /// <summary>销毁；销毁后不可再显示。</summary>
        public void Destroy() => Fire(PUITrigger.Destroy);

        /// <summary>让这个 PUI 抢占引擎输入焦点。未构建（Unbuilt/Destroyed）时什么都不做。</summary>
        public void Focus()
        {
            if (window == null)
            {
                return;
            }

            window.Focusable();
            window.Focus();
        }

        /// <summary>当前是否拿到引擎焦点；未构建（Unbuilt/Destroyed）时视为未聚焦。</summary>
        public bool IsFocused => window != null && window.isFocused();

        /// <summary>当前"拥有"本实例、接收 <see cref="RaiseEvent"/> 路由的 <see cref="PUISolution"/>；未加入任何图或未被设为当前节点时为 null。</summary>
        internal PUISolution Controller { get; set; }

        internal void Attach(PUISolution solution)
        {
            owners ??= new List<PUISolution>();
            if (!owners.Contains(solution))
            {
                owners.Add(solution);
            }
        }

        internal void Detach(PUISolution solution)
        {
            owners?.Remove(solution);
            if (Controller == solution)
            {
                Controller = null;
            }
        }

        /// <summary>把触发键路由给 <see cref="Controller"/>（若为空则路由给唯一加入的 <see cref="PUISolution"/>）；无法确定归属时记日志并忽略。</summary>
        public void RaiseEvent(string triggerKey)
        {
            if (string.IsNullOrEmpty(triggerKey) || State == PUIState.Destroyed)
            {
                return;
            }

            PUISolution target = Controller;

            if (target == null && owners != null)
            {
                if (owners.Count == 1)
                {
                    target = owners[0];
                }
                else if (owners.Count > 1)
                {
                    Plugin.Logger.LogWarning(
                        $"[PUI] \"{Handler.Name}\" belongs to {owners.Count} PUISolutions at once and has no current " +
                        $"Controller, so there is no way to decide who the trigger key \"{triggerKey}\" should go to; ignored.");
                    return;
                }
            }

            target?.Fire(this, triggerKey);
        }

        internal void Fire(PUITrigger trigger)
        {
            PUIState from = State;
            PUIState to = Transition(from, trigger);
            ApplyEntryAction(from, to);
            State = to;
        }

        private static PUIState Transition(PUIState current, PUITrigger trigger)
        {
            if (current == PUIState.Destroyed)
            {
                throw new InvalidOperationException($"The PUI has been destroyed; cannot run {trigger}");
            }

            switch (trigger)
            {
                case PUITrigger.Show:
                    return PUIState.Shown;
                case PUITrigger.Hide:
                    return current == PUIState.Shown ? PUIState.Hidden : current;
                case PUITrigger.Destroy:
                    return PUIState.Destroyed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null);
            }
        }

        /// <summary>执行迁移到 <paramref name="to"/> 的入口动作；状态没有真正变化时什么都不做。</summary>
        private void ApplyEntryAction(PUIState from, PUIState to)
        {
            if (from == to)
            {
                return;
            }

            switch (to)
            {
                case PUIState.Shown:
                    if (from == PUIState.Unbuilt)
                    {
                        Build();
                    }
                    Activate();
                    break;

                case PUIState.Hidden:
                    if (from == PUIState.Shown)
                    {
                        Deactivate();
                    }
                    break;

                case PUIState.Destroyed:
                    if (from != PUIState.Unbuilt)
                    {
                        Teardown();
                    }
                    break;
            }
        }

        protected virtual void Build()
        {
            host = CreateHostObject($"PUI.{Handler.Name}");
            family = host.AddComponent<UiBoxDesignerFamily>();
            window = Handler.GetUIWindow(family);
            try
            {
                Handler.BuildUI(window);
            }
            catch (Exception)
            {
                Teardown(); //构建异常则直接删除，让调用方抛出异常
            }
        }

        /// <summary>新建一个挂在 <see cref="PUIManager.Root"/> 下的宿主 GameObject；须保持启用状态，因为 <see cref="UiBoxDesignerFamily"/> 依赖 OnEnable 初始化，提前禁用会导致 NullReferenceException。</summary>
        protected static GameObject CreateHostObject(string name)
        {
            var hostObject = new GameObject(name);
            if (PUIManager.Root != null)
            {
                hostObject.transform.SetParent(PUIManager.Root.transform, false);
            }

            return hostObject;
        }

        protected void Activate()
        {
            family.activate();
        }

        protected void Deactivate()
        {
            family.deactivate();
        }

        protected void Teardown()
        {
            family.destruct();
            UnityEngine.Object.Destroy(host);
            host = null;
            family = null;
            window = null;
        }

        /// <summary>语言切换后重建所有已构建实例以刷新词条，返回受影响的个数（因文本只在 <see cref="Build"/> 时求值一次）。</summary>
        internal static int RefreshAllForLocaleChange()
        {
            int affected = 0;

            // 倒着走以便顺手移除已被 GC 回收的死条目。
            for (int i = liveInstances.Count - 1; i >= 0; i--)
            {
                if (!liveInstances[i].TryGetTarget(out PUIRuntime runtime))
                {
                    liveInstances.RemoveAt(i);
                    continue;
                }

                // 单个 PUI 重建失败不应影响其它 PUI。
                try
                {
                    if (runtime.RefreshForLocaleChange())
                    {
                        affected++;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[PolarisUI] Failed to rebuild PUI \"{runtime.Handler.Name}\" after the language change: {ex}");
                }
            }

            return affected;
        }

        /// <summary>Shown 的立刻重建并恢复显示/焦点；Hidden 的只拆掉打回 Unbuilt，下次 Show 时再建；其它状态不处理。</summary>
        private bool RefreshForLocaleChange()
        {
            switch (State)
            {
                case PUIState.Shown:
                    bool wasFocused = IsFocused;
                    Teardown();
                    Build();
                    Activate();
                    if (wasFocused)
                    {
                        Focus();
                    }
                    return true;

                case PUIState.Hidden:
                    Teardown();
                    State = PUIState.Unbuilt;
                    return true;

                default:
                    return false;
            }
        }
    }
}
