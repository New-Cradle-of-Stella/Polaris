using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.API
{
    /// <summary>
    /// 游戏能力层的总入口，挂在 <c>PolarisAPI.Game</c> 下。<c>Polaris.API</c> 与 <c>Polaris.Patch</c>
    /// 是全库对游戏内部结构的唯一兼容层：任何需要 Publicizer 才能触达的私有字段/方法，都应该集中在
    /// 这里实现一次，再以稳定的公开 API 暴露出去。下游模组不应该自己对 <c>Assembly-CSharp</c>/
    /// <c>unsafeAssem</c>/<c>pixelliner</c> 之类的游戏程序集做 Publicizer——那样每个模组
    /// 都要各自跟踪游戏内部结构，换一次游戏版本就要改所有模组；集中在这里之后，换版本
    /// 只需要改 Polaris 这一处。
    /// <para>
    /// 下面按领域分成若干门面（<see cref="Loop"/>/<see cref="Input"/>/<see cref="World"/>…）。
    /// 它们只是分组，<b>不是</b>需要各自初始化的子系统：整个能力层没有初始化步骤，
    /// 也没有自己的生命周期，第一次访问就能用，用不到的分组不产生任何开销。
    /// </para>
    /// <para>
    /// 三条贯穿全层的约定：
    /// <list type="number">
    /// <item>查询不产生副作用，取不到就返回空值/零值，永远不抛异常给调用方。</item>
    /// <item>动作一律返回 <see cref="GameActionResult"/>，失败有明确原因，没有"静默什么都没发生"。</item>
    /// <item>公开签名里不出现任何游戏类型，收发的是 Polaris 自己的句柄、快照与请求。</item>
    /// </list>
    /// 本局哪些能力真的通、哪些只读、哪些本版本没有入口，查 <see cref="GameCapabilities"/>。
    /// </para>
    /// </summary>
    public sealed class GameStateAPI
    {
        /// <summary>游戏循环：每帧回调、焦点、退出。</summary>
        public GameLoopAPI Loop { get; } = new();

        /// <summary>玩家输入的只读查询（按游戏动作，不按键码）。</summary>
        public InputGameAPI Input { get; } = new();

        /// <summary>世界状态：地图、危险度、日夜与天气。</summary>
        public WorldGameAPI World { get; } = new();

        /// <summary>场上角色的查询与位移。</summary>
        public CharacterGameAPI Characters { get; } = new();

        /// <summary>玩家角色专属的查询与动作。</summary>
        public PlayerGameAPI Player { get; } = new();

        /// <summary>玩家背包。</summary>
        public InventoryGameAPI Inventory { get; } = new();

        /// <summary>金钱。</summary>
        public EconomyGameAPI Economy { get; } = new();

        /// <summary>伤害与恢复。</summary>
        public CombatGameAPI Combat { get; } = new();

        /// <summary>音频。</summary>
        public AudioGameAPI Audio { get; } = new();

        /// <summary>
        /// 下游模组订阅游戏内回调的统一入口：生命周期、循环、世界、角色、输入……
        /// 见 <see cref="GameCallbacksAPI"/>；本局每条回调通不通见 <see cref="GameCallbackStatus"/>。
        /// </summary>
        public GameCallbacksAPI Callbacks { get; } = new();

        /// <summary>
        /// <c>XX.MTRX</c> 是否已经完全就绪：PXL 图标、Shader、私有的 <c>init2()</c>、
        /// 音频 sheet 全部完成。任何 PXLS 解析（<c>PxlCharacter</c>）或图像注册
        /// （<c>MImage</c>/<c>MTRX.assignMI</c>）都依赖 <c>MTRX.OMI</c>/<c>MTRX.OMeshImages</c>
        /// 这两个静态字典，它们只在 <c>MTRX.init1()</c> 内才会被创建，在此之前碰它们会直接
        /// NullReferenceException。
        /// <para>
        /// 反编译确认 <c>MTRX.prepared</c> 这个属性的 getter <b>有副作用</b>：当
        /// <c>loaded == 1</c> 且图标/Shader 就绪时，读取它会把 <c>loaded</c> 推进到 3 并调用
        /// 私有的 <c>init2()</c>。为了不给游戏自身的初始化流程添加任何调用方控制不到的变量，
        /// 这里改为直接读取（经 Publicizer 公开的）<c>MTRX.loaded</c> 字段，只在它已经等于 7
        /// 时才认为"就绪"——纯读取，没有副作用。
        /// </para>
        /// </summary>
        public bool IsMtrxReady => XX.MTRX.loaded == 7;

        /// <summary>
        /// 当前生效的本地化 family key（如 <c>"_"</c>/<c>"en"</c>/<c>"zh-cn"</c>/<c>"ko-kr"</c>），
        /// 直通 <c>XX.TX.getCurrentFamilyName()</c>。
        /// <para>
        /// 已用 ilspycmd 反编译核实：family 集合不是固定枚举，而是
        /// <c>TX.reloadTx()</c> 在启动/切换语言时按 <c>StreamingAssets/localization/</c> 下
        /// 每个 <c>___family*.txt</c> 里的 <c>%DEFAULT_LANGUAGE</c>/<c>%SYSTEM_LANGUAGE</c> 等
        /// 指令动态建出的 <c>TXFamily</c> 表；<c>TX.changeFamily(key)</c> 切换当前 family，
        /// <c>TX.getCurrentFamilyName()</c> 就是当前 family 的 <c>key</c> 字段（游戏自带的
        /// family 目前是 <c>"_"</c> 表示默认语言，其余按实际安装的语言包而定）。这里直通、
        /// 不缓存，随时反映玩家当前选择的语言。<c>Get</c> 本身是 public static，不需要
        /// Publicizer 才能调用。
        /// </para>
        /// </summary>
        public string CurrentLocale => XX.TX.getCurrentFamilyName();

        /// <summary>
        /// 玩家切换游戏语言（当前 family 变化）时触发，参数是新的 family key。
        /// <para>
        /// 游戏没有对外暴露"语言变了"的回调，切换入口也不止一个（选项界面、直接调
        /// <c>TX.changeFamily</c>、启动时按 <c>%SYSTEM_LANGUAGE</c> 自动选），所以这里选择在
        /// <see cref="Pump"/> 里每帧比对 <see cref="CurrentLocale"/>——读一个字符串属性，比给每条
        /// 入口都打 Harmony 补丁便宜，也不用跟着游戏版本追内部调用链。
        /// </para>
        /// <para>
        /// 首次探到语言（启动那一下）只记下来、不触发；只有真正从一种语言变成另一种才算。
        /// 单个订阅者抛异常不会连累其它订阅者。
        /// </para>
        /// <para>
        /// 同一次探测结果也会经 <see cref="Callbacks"/> 的 <see cref="LifecycleCallbacks.LocaleChanged"/>
        /// 发一次：两条路径共享同一次差分，不会各自探测、各发一次。
        /// </para>
        /// </summary>
        public event Action<string> LocaleChanged;

        readonly List<Action> pendingReady = new(4);
        bool loggedReadyOnce;
        string lastLocale;
        bool localeKnown;

        /// <summary>
        /// 注册一个"等 <see cref="IsMtrxReady"/> 之后执行"的回调；此刻已就绪则立即同步执行，
        /// 否则排队，由 <see cref="Plugin"/> 每帧检查，就绪的那一帧统一执行并清空。
        /// <para>
        /// 排队这件事本身与游戏版本无关，但"什么算就绪"是游戏内部结构，两者都归这里：
        /// 每个下游模组各自维护一份等待队列没有意义，资源子系统就曾为此单独写过一个
        /// <c>GameReady</c> 类，还因为和 <c>PolarisResAPI.GameReady</c> 属性重名而不得不到处写全限定名。
        /// </para>
        /// </summary>
        public void WhenReady(Action action)
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

        /// <summary>
        /// 极早期读取游戏内部状态理论上不该抛异常，但防御性地当作"还没好"处理，
        /// 而不是让整条模组初始化链路崩掉。
        /// </summary>
        bool SafeIsReady
        {
            get
            {
                try
                {
                    return IsMtrxReady;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[Polaris] Exception while probing MTRX readiness; treating it as not ready: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>由 <see cref="Plugin.Update"/> 每帧调用。</summary>
        internal void Pump()
        {
            bool ready = SafeIsReady;

            // 只在"首次变为就绪"的那一帧打一次日志，方便验证门控确实在正确的时机放行。
            if (ready && !loggedReadyOnce)
            {
                loggedReadyOnce = true;
                Plugin.Logger.LogMessage(
                    $"[Polaris] MTRX became ready for the first time at frame {UnityEngine.Time.frameCount}.");
                LifecycleCallbacks.PublishReady();
            }

            PumpLocale(ready);

            // 地图代数要先于任何对外回调推进：订阅者在自己的回调里拿句柄时，
            // 该失效的应该已经失效了。
            GameBinding.Pump();
            Audio.Pump();

            // 状态差分探测：都在"入队"这一层完成，真正派发给下游订阅者要等下面的 Drain。
            WorldCallbacks.Pump();
            ActorCallbacks.Pump();
            InputCallbacks.Pump();
            LoopCallbacks.PumpGameFrame();

            // 旧的 Loop.Updating 兼容事件保持原有时机不变。
            Loop.PumpUpdate();

            // 先把这一帧探测到的事件派发出去，再触发新版 Updating 信号——这样"状态差分事件在
            // 普通 Updating 之前可见"，下游在 Updating 回调里读到的状态已经是这一帧的最终结果。
            Infra.CallbackRuntime.Drain();
            LoopCallbacks.RaiseUpdating();

            if (!ready || pendingReady.Count == 0)
            {
                return;
            }

            // 先复制再清空：回调内部可能再次调用 WhenReady（此时会被当成"已就绪"直接同步执行），
            // 不应该让新加入的回调被这一轮的 Clear 误删。
            var toRun = new List<Action>(pendingReady);
            pendingReady.Clear();
            foreach (Action action in toRun)
            {
                try
                {
                    // 面包屑：这些回调是下游模组"游戏就绪后要做的重活"（解析 PXLS、注册图像、
                    // 建图集），是最有可能一去不回的一类代码。卡住时看门狗要能点出是谁。
                    using (Diagnostics.MainThreadBeat.Enter(
                        $"WhenReady callback ({Describe(action)})", action.Method?.DeclaringType?.Assembly))
                    {
                        action();
                    }
                }
                catch (Exception ex)
                {
                    // 责任人就是这个回调委托本身所在的程序集，不必走堆栈推断。
                    PolarisAPI.Errors.Report(ex, "WhenReady callback", action.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError("[Polaris] A WhenReady callback threw an exception; ignored.");
                }
            }
        }

        /// <summary>由 <see cref="Plugin.LateUpdate"/> 每帧调用。</summary>
        internal void PumpLate()
        {
            Loop.PumpLateUpdate();

            // 大多数 Update 阶段产生的事件能在同帧 LateUpdate 收到；Update 里已经排过一轮，
            // 这里再排一轮兜底 Update 之后、LateUpdate 之前发生的任何入队（例如其它插件的
            // Update 比 Polaris 晚跑，在这段时间里触发了 Harmony 补丁）。
            Infra.CallbackRuntime.Drain();
            LoopCallbacks.RaiseLateUpdating();
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

        /// <summary>比对当前 family，变了就通知 <see cref="LocaleChanged"/> 的订阅者。</summary>
        void PumpLocale(bool ready)
        {
            // 就绪之前不问：family 表是 TX.reloadTx() 建的，早期读到的可能是空/半成品，
            // 那会被误判成"语言变了"，白白让下游重建一遍。
            if (!ready)
            {
                return;
            }

            string locale;
            try
            {
                locale = CurrentLocale;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] Exception while reading the current language; skipping this frame: {ex.Message}");
                return;
            }

            if (string.IsNullOrEmpty(locale))
            {
                return;
            }

            if (!localeKnown)
            {
                localeKnown = true;
                lastLocale = locale;
                return;
            }

            if (locale == lastLocale)
            {
                return;
            }

            string previous = lastLocale;
            lastLocale = locale;
            Plugin.Logger.LogMessage($"[Polaris] Game language changed: {previous} -> {locale}.");

            LifecycleCallbacks.PublishLocaleChanged(previous, locale);

            Action<string> handlers = LocaleChanged;
            if (handlers == null)
            {
                return;
            }

            // 逐个调用而不是 handlers(locale)：一个订阅者抛异常不该让后面排队的收不到通知。
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    // 面包屑：切语言会让下游模组重建自己的全部文案与图像（PolarisLang 要重扫语言包、
                    // PolarisUI 要重排控件），和 WhenReady 一样属于"一去不回"的高危回调。
                    using (Diagnostics.MainThreadBeat.Enter(
                        $"LocaleChanged callback ({Describe(handler)})", handler.Method?.DeclaringType?.Assembly))
                    {
                        ((Action<string>)handler)(locale);
                    }
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "LocaleChanged callback", handler.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError("[Polaris] A LocaleChanged callback threw an exception; ignored.");
                }
            }
        }
    }
}
