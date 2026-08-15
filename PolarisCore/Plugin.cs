using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Polaris.Components;

namespace Polaris
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private Harmony harmony;

        private void Awake()
        {
            Logger = base.Logger;

            // Core 自带的基础捕获先于任何组件安装；高级诊断缺失或加载失败时仍能留下错误日志。
            Diagnostics.CoreErrorCapture.Install();

            // 先发现组件并执行极早期注册；Diagnostics 在这里向 Core 的契约注册实现。
            ComponentHost.Discover();
            ComponentHost.Bootstrap();

            // 目录建不出来不该把整个 Awake 掀掉，否则 Unity 不会再调 Start，子系统全部起不来。
            PolarisAPI.Errors.Guard(
                PolarisAPI.Paths.EnsureDirectories,
                "creating the Polaris directory structure");

            PolarisAPI.Errors.Guard(ReportLastSession, "reading how the previous session ended");

            // 内置文案表必须早于 Start 阶段的设置项扫描，绑定配置时要用说明文字查表。
            Localization.PolarisStrings.Register();

            // 组件是普通 DLL，不带 BepInPlugin；由唯一的 PolarisCore 插件发现并驱动。
            ComponentHost.Awake();

            harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            PatchAllIndividually();

            Logger.LogMessage(Logo);
        }

        /// <summary>上一局非正常结束时，把结论摊到控制台、写进本局报告、给告知页上膛；正常退出时不吭声。</summary>
        private static void ReportLastSession()
        {
            Diagnostics.LastSessionInfo last = PolarisAPI.Health.LastSession;
            if (last == null)
            {
                return;
            }

            Logger.LogWarning($"[Polaris] {last.OneLine()}");
            Logger.LogWarning($"[Polaris] Stalled at: {last.Where()}");

            PolarisErrorNotice.AdoptLastSession(last);
        }

        /// <summary>逐个类应用 Harmony 补丁而非一把 <c>PatchAll()</c>：后者全有全无，一个补丁坏了会连累其它子系统全不起来；逐类应用则坏一个报错跳过，其余照常。</summary>
        private void PatchAllIndividually()
        {
            int applied = 0;

            foreach (Assembly assembly in ComponentHost.Assemblies)
            {
                foreach (Type type in AccessTools.GetTypesFromAssembly(assembly))
                {
                    try
                    {
                        // 面包屑：补丁应用涉及大量反射与 IL 生成，卡住时看门狗要能说出卡在哪个补丁上。
                        using (PolarisAPI.Health.Activity($"applying patch {type.Name}", type.Assembly))
                        {
                            // 没标 [HarmonyPatch] 的类型，Patch() 是空操作。
                            if (harmony.CreateClassProcessor(type).Patch() != null)
                            {
                                applied++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PolarisAPI.Errors.Report(ex, $"applying patch {type.Name}", type.Assembly);
                        Logger.LogError($"[Polaris] The feature owned by patch {type.Name} is unavailable this session.");
                    }
                }
            }

            Logger.LogMessage($"[Polaris] Applied {applied} Harmony patches.");
        }

        /// <summary>各子系统初始化统一放在 Start（此时所有插件已完成 Awake，反射扫描才看得到完整插件名单）。下面顺序有硬约束：Res 须早于 PUI，Lang resolver 须早于设置项扫描。</summary>
        private void Start()
        {
            ComponentHost.Start();
        }

        /// <summary>Polaris 自己的每帧泵：驱动就绪门控、语言变更探测、地图代数推进及能力层回调，供所有下游模组共用。</summary>
        private void Update()
        {
            // 心跳必须是第一行且在 Pump 之外：Pump 里的回调若卡住，这一帧的心跳也要算已打过。
            Diagnostics.DiagnosticsHost.Beat(UnityEngine.Time.frameCount);

            API.GameSessionRuntime.Pump();
            PolarisAPI.GameMenu.Pump();
            ComponentHost.Update();
        }

        /// <summary>所有 Update 跑完之后再泵一次，此时读相机位置、角色坐标等"别人算完的结果"才准。</summary>
        private void LateUpdate()
        {
            API.GameSessionRuntime.PumpLate();
            ComponentHost.LateUpdate();
        }

        /// <summary>窗口失焦/回到前台；失焦时 Unity 不再调 Update，须暂停看门狗以免误报卡死。</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            Diagnostics.DiagnosticsHost.SetPaused(!hasFocus);
        }

        /// <summary>被系统挂起/恢复；对看门狗的意义与失焦相同，这段时间不推进帧属于正常。</summary>
        private void OnApplicationPause(bool isPaused)
        {
            Diagnostics.DiagnosticsHost.SetPaused(isPaused);
        }


        /// <summary>进程退出前的收尾：落一份"上一局摘要"供下次启动读取，控制台补一行汇总（无错误时不吭声）。</summary>
        private void OnApplicationQuit()
        {
            // 只清零本地标志，不主动恢复世界：进程都要退出了没必要。
            API.GameMenuPauseRuntime.Reset();

            ComponentHost.Shutdown();

            // 先停看门狗：退出过程还要活一会儿（存档、淡出），不停会把它误判成卡死。
            Diagnostics.DiagnosticsHost.Stop();

            string summary = Diagnostics.DiagnosticsHost.Summary();
            if (summary != null)
            {
                Logger.LogMessage(summary);
            }

            PolarisAPI.Errors.Guard(PolarisErrorNotice.PersistPending, "saving the previous session's error summary");

            // 最后删掉会话哨兵，这是"正常结束"的唯一表达方式；须排在 PersistPending 之后。
            Diagnostics.DiagnosticsHost.CloseSession();
            Diagnostics.CoreErrorCapture.Uninstall();
        }

        private const string Logo = """

                :=.                                   ..              .-:                                
                                         .            :.                                                 
                                                     .--.                                                
                                                     :++.                                        ..      
                                                    .:*+..                                      .-:      
                                                   ..:**:.                                               
                              ...                  .-=**--.                  ..                          
                              .--+=                .-=#*--.              ..-=::                          
                 ..            .:+++*:..           :-=##--.           ..-*+==.                           
                 ::              :-##+==..       ..:-=##=-.         .:==*##:.                            
                                  .-:*%%==:..    .:-++%#+=-:.     .-=+%%*::                              
                                   . -==%%*=-..  ::=+*%%++-:.  ..-=*%#-=:                                
                                     .::++#@*=-:.::=+*@%++-::..=+#@*+=::.                                
                                        --++#@#++--=**@%++=:-++#@#+=-:                                   
                                        ..:-+*#%#+=+*#@%*++++%%#*=-:..                                   
                                        ..::--+#####*#@@**#%###=::::..                                   
                                     ...---:---==%@%#%@@##%@%==-:::--:..                                 
                        .......::::-----==++++*****#%%@@%%#**++++++===-----::::...                       
                 .. :===-===+++****#####%%%%%%%%%%%@@@@@@@%%%%%%%%%%%######****+==----===. .             
                    .-------==+++++********########%@@@@@@%########********++++===-------.               
                        .......:::::::::---====++###%%@@%%##*++======----::::::.......                   
                               .........:--::-=++%@%*#@@#*%@%++=:::--:.......                            
                                        ..:-==*@%*+**#@@***+*%%*=--:..                                   
                                        ::-=*%#**=-+*#@@**=-=**#%*=-::                                   
                                       .==*%##+--::=+*@%*+-::-=*###+=-                                   
                -=:                  .:-*###+-:..::=+*%%++-:..:--*%#*+::.                 .:             
                 .                 . -++%%+-:..  ::=+*%%++-:.  .:--*@#++- .               :-.            
                                   -:*@%--:.     .:-++##++-:.     .--=%@*:-                              
                                 .-##+==...      ..:-=##=-:.       ...==+##:.                            
                                .==+*:..           :-=**--.           ..-*+==.                           
                               .:=-                .--**--.              ..-=::                          
                               ....                .--**--.                  :-.                         
                                                   .:-++-:.                                              
                                                    .:++..                                               
                                                     .+=.                                                
                                                     .--                                                 
                                                      ..                                                 
                                                 
                                                  AIC-Polaris
                                       by Alon_ · github.com/AAAA9731
                """;
    }
}
