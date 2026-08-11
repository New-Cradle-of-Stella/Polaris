using Polaris.PUI.Wire;
using System;
using System.Collections.Generic;
using nel;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>
    /// 仅供 <see cref="PUIHotFixEnabledAttribute"/> 标注的插件使用的 PUI 运行时：
    /// 首次构建（还没收到过任何热重载推送时）跟普通的 <see cref="PUIRuntime"/> 完全一样，
    /// 调用编译好的 <see cref="IPUI.BuildUI"/>；只有收到过热重载推送之后，才改用
    /// <see cref="PuiHotReloadBridge"/> 按最近一次收到的指令重建。这两条路径除了共用
    /// GameObject 生命周期管理（<see cref="PUIRuntime"/> 的 Teardown/Activate/Deactivate）
    /// 之外互不干扰：不开启热重载的插件永远只会走 <see cref="PUIRuntime"/>，不会实例化本类。
    /// </summary>
    internal sealed class PUIHotReloadRuntime : PUIRuntime
    {
        private List<PuiWireCommand> pendingCommands;

        public PUIHotReloadRuntime(IPUI handler) : base(handler)
        {
        }

        protected override void Build()
        {
            if (pendingCommands == null)
            {
                base.Build();
                return;
            }

            host = CreateHostObject($"PUI.{Handler.Name}");
            family = host.AddComponent<UiBoxDesignerFamily>();
            window = PuiHotReloadBridge.Apply(family, pendingCommands, Handler);
        }

        /// <summary>
        /// 应用一次热重载推送。先在一个临时 GameObject 上完整跑一遍 <see cref="PuiHotReloadBridge"/>
        /// （宿主经 <see cref="PUIRuntime.CreateHostObject"/> 创建，挂载方式与
        /// <see cref="PUIRuntime.Build"/> 完全一致）：
        /// 失败（比如引用了不存在的回调方法）就销毁临时对象、原样返回错误，当前正在显示的 UI
        /// （如果有）完全不受影响；成功后再销毁旧的、把临时对象转正，并按之前的状态决定是否要重新 activate。
        /// </summary>
        public (bool ok, string error) ApplyHotReload(List<PuiWireCommand> commands)
        {
            GameObject stagingHost = CreateHostObject($"PUI.{Handler.Name}.__staging");
            UiBoxDesignerFamily stagingFamily = stagingHost.AddComponent<UiBoxDesignerFamily>();

            UiBoxDesigner stagingWindow;
            try
            {
                stagingWindow = PuiHotReloadBridge.Apply(stagingFamily, commands, Handler);
            }
            catch (Exception ex)
            {
                UnityEngine.Object.Destroy(stagingHost);
                return (false, ex.Message);
            }

            PUIState previousState = State;

            if (previousState == PUIState.Unbuilt)
            {
                // 还没被显示过：这次推送只做校验，真正的构建留给以后第一次 ShowUI。
                UnityEngine.Object.Destroy(stagingHost);
                pendingCommands = commands;
                return (true, null);
            }

            Teardown();

            stagingHost.name = $"PUI.{Handler.Name}";

            host = stagingHost;
            family = stagingFamily;
            window = stagingWindow;
            pendingCommands = commands;

            if (previousState == PUIState.Shown)
            {
                Activate();
            }

            return (true, null);
        }
    }
}
