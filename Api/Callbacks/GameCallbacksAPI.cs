using System.Collections.Generic;
using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>
    /// 下游模组订阅游戏内回调的统一入口，挂在 <c>PolarisAPI.Game.Callbacks</c> 下。
    /// <para>
    /// 所有真正的状态（<see cref="GameSignal{T}"/> 实例、订阅表）都是各领域门面里的
    /// <c>static readonly</c> 字段，不挂在这个类的实例上——哪怕下游误用 <c>new GameStateAPI()</c>
    /// 造出另一个 <see cref="GameCallbacksAPI"/> 实例，它的领域门面读到的仍然是同一份进程级共享状态，
    /// 不会出现一个不会被 <see cref="Plugin.Update"/> 驱动的"孤立回调中心"。
    /// </para>
    /// </summary>
    public sealed class GameCallbacksAPI
    {
        internal GameCallbacksAPI() { }

        public LifecycleCallbacks Lifecycle { get; } = new();
        public LoopCallbacks Loop { get; } = new();
        public WorldCallbacks World { get; } = new();
        public ActorCallbacks Actors { get; } = new();
        public CombatCallbacks Combat { get; } = new();
        public InventoryCallbacks Inventory { get; } = new();
        public ProgressionCallbacks Progression { get; } = new();
        public InputCallbacks Input { get; } = new();
        public UiCallbacks UI { get; } = new();

        /// <summary>查这条回调本局通不通。</summary>
        public GameCallbackStatus Status(GameCallbackKind kind) => CallbackRegistry.Status(kind);

        /// <summary>诊断页/报告用：目前已登记的每一条回调的状态。</summary>
        public IReadOnlyList<GameCallbackDescriptor> DescribeAll() => CallbackRegistry.DescribeAll();
    }
}
