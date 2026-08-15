using System;
using System.Collections.Generic;
using nel;
using Polaris.PUI.Wire;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>供 <see cref="PUIHotFixEnabledAttribute"/> 插件使用的 PUI 运行时：首次构建走普通编译路径，收到热重载推送后改用 <see cref="PuiHotReloadBridge"/> 按最新指令重建。</summary>
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

        /// <summary>应用一次热重载推送：先在临时 GameObject 上试跑，失败则丢弃不影响当前 UI；成功后替换旧对象并恢复原状态。</summary>
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
                // 还没显示过：只做校验，构建留给第一次 ShowUI。
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
