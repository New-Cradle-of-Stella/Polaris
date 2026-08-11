using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using nel;
using Polaris.PUI.HotReload;
using UnityEngine;

namespace Polaris.PUI
{
    /// <summary>
    /// 单个已创建 <see cref="IPUI"/> 的运行时实例：持有其专属的 GameObject /
    /// <see cref="UiBoxDesignerFamily"/> / <see cref="UiBoxDesigner"/>，并以状态机驱动
    /// 构建 -&gt; 显示 -&gt; 隐藏 -&gt; 销毁 的生命周期，对应原版 UiBoxDesignerFamily.Create /
    /// activate / deactivate / destruct 的调用约定。
    /// 可直接通过 <see cref="Create"/> 获得并持有，不要求先按名字注册进 <see cref="PUIManager"/>。
    /// </summary>
    public class PUIRuntime
    {
        private static readonly ConditionalWeakTable<IPUI, PUIRuntime> handlerIndex = new ConditionalWeakTable<IPUI, PUIRuntime>();

        // 语言切换后要重建所有已构建的实例，得能把它们枚举出来。handlerIndex 是
        // ConditionalWeakTable（不同 Mono 版本对枚举的支持不一致，不指望它），PUIManager 的名字表
        // 也只覆盖"按名字注册过"的那部分——直接 Create 出来自己持有的、状态机图里创建的都不在里面。
        // 所以这里自己存一份弱引用：不影响 GC 回收，扫描时顺手把死条目清掉。
        private static readonly List<WeakReference<PUIRuntime>> liveInstances = new List<WeakReference<PUIRuntime>>();

        public IPUI Handler { get; }

        public PUIState State { get; private set; } = PUIState.Unbuilt;

        // protected（而不是 private）：仅仅是为了让 PUIHotReloadRuntime 能够
        // override Build()、并在 ApplyHotReload 里直接复用 Teardown/Activate/Deactivate，
        // 不改变这几个成员本身的语义或本类（release 路径）的任何行为。
        protected GameObject host;
        protected UiBoxDesignerFamily family;
        protected UiBoxDesigner window;

        private List<PUISolution> owners;

        public PUIRuntime(IPUI handler)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));

            // 同一个 IPUI 对象只能被包裹一次：Add 在键已存在时会抛异常，天然禁止重复包裹，
            // 这也是 RaiseEvent/Of 能够无歧义地按 IPUI 对象反查 PUIRuntime 的前提。
            handlerIndex.Add(handler, this);
            liveInstances.Add(new WeakReference<PUIRuntime>(this));
        }

        /// <summary>
        /// 唯一推荐的创建入口：按 <paramref name="handler"/> 所在程序集是否标了
        /// <see cref="PUIHotFixEnabledAttribute"/> 自动选型（<see cref="PUIRuntime"/> 或
        /// <see cref="PUIHotReloadRuntime"/>），不接触任何名字索引——是否把结果登记进
        /// <see cref="PUIManager"/> 的名字表由调用方另行决定（见 <see cref="PUIManager.Register"/>）。
        /// </summary>
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

        /// <summary>
        /// 反查某个 <see cref="IPUI"/> 实例对应的 <see cref="PUIRuntime"/>；未被
        /// <see cref="Create"/> 包裹过时为 null。生成代码 / 热重载桥用它把一次按钮点击
        /// 路由回正确的运行时实例，而不必再依赖裸字符串名字。
        /// </summary>
        public static PUIRuntime Of(IPUI handler)
        {
            if (handler == null)
            {
                return null;
            }

            return handlerIndex.TryGetValue(handler, out PUIRuntime runtime) ? runtime : null;
        }

        /// <summary>
        /// 触发一次状态迁移；非法迁移（例如已销毁后继续操作）会抛出异常。
        /// </summary>
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

        /// <summary>
        /// 当前"拥有"本实例、会接收 <see cref="RaiseEvent"/> 路由的 <see cref="PUISolution"/>；
        /// 由 <see cref="PUISolution"/> 在把本节点设为当前节点时设置，离开时若自己是 Controller
        /// 则清空。未加入任何图，或加入的图从未把本节点设为当前节点时为 null。
        /// </summary>
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

        /// <summary>
        /// 供生成代码 / 热重载桥调用：把本 .pui 上配置的一个状态连接点触发键交给"当前拥有我的
        /// 解决方案"处理。路由规则：<see cref="Controller"/> 非空则转给它；否则若本实例只加入了
        /// 唯一一个 <see cref="PUISolution"/> 则转给它；否则（未加入任何图，或同时属于多个图又
        /// 没有当前 Controller）记一次日志并安全地什么都不做——跟今天"没在任何已加载的图里配置
        /// 就什么都不做"的语义一致。
        /// </summary>
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
            Handler.BuildUI(window);
        }

        /// <summary>
        /// 新建一个挂在 <see cref="PUIManager.Root"/> 下的宿主 GameObject（调用方随后自行
        /// AddComponent&lt;UiBoxDesignerFamily&gt;）。所有宿主——正式的与热重载的临时对象——都必须
        /// 走这里：先挂到 Root 下、保持启用状态，不做任何 SetActive(false) 之类的"隐藏起来再建"
        /// 的花活，因为 nel 的 <see cref="UiBoxDesignerFamily"/> 依赖 OnEnable 之类的生命周期回调
        /// 做初始化，提前禁用会导致内部状态没初始化就被 Create() 用到，直接 NullReferenceException。
        /// </summary>
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

        /// <summary>
        /// 游戏语言切换后刷新所有已构建的实例，返回受影响的个数。
        /// <para>
        /// 为什么非重建不可：生成代码里的 <c>XX.TX.Get("key")</c>（即 <c>.pui</c> 里的
        /// <c>&amp;key</c> 语法）写在 <see cref="IPUI.BuildUI"/> 里，而 <see cref="Build"/> 只在第一次
        /// <see cref="Show"/> 时跑一次，之后 Show/Hide 只是 activate/deactivate 同一个窗口——不重建
        /// 的话已经打开过的窗口会一直停在第一次构建时那门语言，关掉重开也没用。
        /// </para>
        /// </summary>
        internal static int RefreshAllForLocaleChange()
        {
            int affected = 0;

            // 倒着走：顺手删掉已经被 GC 回收的死条目，不影响还没遍历到的下标。
            for (int i = liveInstances.Count - 1; i >= 0; i--)
            {
                if (!liveInstances[i].TryGetTarget(out PUIRuntime runtime))
                {
                    liveInstances.RemoveAt(i);
                    continue;
                }

                // 一个 PUI 重建失败（BuildUI 里抛了）不该让其它 PUI 停在旧语言上。
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

        /// <summary>
        /// 显示中的立刻重建并保持显示（连引擎焦点一起恢复）；隐藏的只拆掉、打回
        /// <see cref="PUIState.Unbuilt"/>，下次 <see cref="Show"/> 时自然重建——没必要为看不见的窗口
        /// 立刻付构建开销。未构建/已销毁的不用管。
        /// </summary>
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
