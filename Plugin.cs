using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;

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

            // 必须是第一行：记住主线程身份并给心跳一个初值，供之后所有埋点判断是否在主线程上。
            Diagnostics.MainThreadBeat.Install();

            // 尽早安装：从这里开始接住并归因 Unity 异常、后台线程未捕获异常、其它插件的严重错误。
            Diagnostics.ErrorCapture.Install();

            // 目录建不出来不该把整个 Awake 掀掉，否则 Unity 不会再调 Start，子系统全部起不来。
            PolarisAPI.Errors.Guard(
                PolarisAPI.Paths.EnsureDirectories,
                "creating the Polaris directory structure");

            // 崩溃/卡死检测：先读上一局留下的哨兵标记，再为本局写一个新的。
            Diagnostics.DiagnosticsConfig.Resolve();
            Diagnostics.ErrorReportWriter.PrimeEnvironment();
            PolarisAPI.Errors.Guard(Diagnostics.SessionSentinel.Install, "registering this session's sentinel");
            PolarisAPI.Errors.Guard(ReportLastSession, "reading how the previous session ended");
            Diagnostics.Watchdog.Install();

            // 内置文案表必须早于 Start 阶段的设置项扫描，绑定配置时要用说明文字查表。
            Localization.PolarisStrings.Register();
            Lang.LangStrings.Register();
            Res.ResStrings.Register();

            harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            PatchAllIndividually();

            Logger.LogMessage(Logo);
        }

        /// <summary>上一局非正常结束时，把结论摊到控制台、写进本局报告、给告知页上膛；正常退出时不吭声。</summary>
        private static void ReportLastSession()
        {
            Diagnostics.LastSessionInfo last = Diagnostics.SessionSentinel.LastSession;
            if (last == null)
            {
                return;
            }

            Logger.LogWarning($"[Polaris] {last.OneLine()}");
            Logger.LogWarning($"[Polaris] Stalled at: {last.Where()}");

            Diagnostics.ErrorReportWriter.AppendPreviousSession(last);
            PolarisErrorNotice.AdoptLastSession(last);
        }

        /// <summary>逐个类应用 Harmony 补丁而非一把 <c>PatchAll()</c>：后者全有全无，一个补丁坏了会连累其它子系统全不起来；逐类应用则坏一个报错跳过，其余照常。</summary>
        private void PatchAllIndividually()
        {
            int applied = 0;

            foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                try
                {
                    // 面包屑：补丁应用涉及大量反射与 IL 生成，卡住时看门狗要能说出卡在哪个补丁上。
                    using (Diagnostics.MainThreadBeat.Enter($"applying patch {type.Name}", type.Assembly))
                    {
                        // 没标 [HarmonyPatch] 的类型，Patch() 是空操作。
                        if (harmony.CreateClassProcessor(type).Patch() != null)
                        {
                            applied++;
                            Infra.CallbackPatchRegistry.ReportApplied(type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 责任人直接点名（这些补丁类都在本程序集里）；归因与报告归档由 Errors 统一负责。
                    PolarisAPI.Errors.Report(ex, $"applying patch {type.Name}", type.Assembly);
                    Logger.LogError($"[Polaris] The feature owned by patch {type.Name} is unavailable this session.");
                    Infra.CallbackPatchRegistry.ReportFailed(type, ex);
                }
            }

            Logger.LogMessage($"[Polaris] Applied {applied} Harmony patches.");
        }

        /// <summary>各子系统初始化统一放在 Start（此时所有插件已完成 Awake，反射扫描才看得到完整插件名单）。下面顺序有硬约束：Res 须早于 PUI，Lang resolver 须早于设置项扫描。</summary>
        private void Start()
        {
            // 必须最先注册，赶在其它模组注册主菜单按钮之前占住"设置"后面的位置。
            PolarisManagementUI.RegisterButton();

            // 每个子系统各自兜底：一个起不来不该连累另外两个，也不该把异常抛回 Start。
            InitSubsystem("resource", Res.Runtime.ResRuntime.Init);

            InitSubsystem("localization", () =>
            {
                Lang.PlangRegistryScanner.ScanAll();
                PolarisAPI.Localization.RegisterResolver(Lang.PlangRuntime.Get);
            });

            InitSubsystem("PUI", PUI.PUIManager.Init);

            // 扫描登记生成类后顺带把所有事件解包成 plugins/Polaris/events/ 下的 .cmd 文件。

            // 须排在三个子系统之后：Builder 轨的设置项注册可能发生在子系统初始化里。
            Settings.SettingsAttributeScanner.ScanAll();
        }

        /// <summary>跑一个子系统的初始化；Guard 已负责面包屑、归因与报告归档，这里只补一句后果说明。</summary>
        private static void InitSubsystem(string name, Action init)
        {
            if (!PolarisAPI.Errors.Guard(init, $"{name} subsystem initialization"))
            {
                Logger.LogError($"[Polaris] The {name} subsystem failed to initialize; its features are unavailable this session.");
            }
        }

        /// <summary>Polaris 自己的每帧泵：驱动就绪门控、语言变更探测、地图代数推进及能力层回调，供所有下游模组共用。</summary>
        private void Update()
        {
            // 心跳必须是第一行且在 Pump 之外：Pump 里的回调若卡住，这一帧的心跳也要算已打过。
            Diagnostics.MainThreadBeat.Beat(UnityEngine.Time.frameCount);

            API.GameSessionRuntime.Pump();
            PolarisAPI.GameMenu.Pump();
        }

        /// <summary>所有 Update 跑完之后再泵一次，此时读相机位置、角色坐标等"别人算完的结果"才准。</summary>
        private void LateUpdate()
        {
            API.GameSessionRuntime.PumpLate();
        }

        /// <summary>窗口失焦/回到前台；失焦时 Unity 不再调 Update，须暂停看门狗以免误报卡死。</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            Diagnostics.Watchdog.SetPaused(!hasFocus);
        }

        /// <summary>被系统挂起/恢复；对看门狗的意义与失焦相同，这段时间不推进帧属于正常。</summary>
        private void OnApplicationPause(bool isPaused)
        {
            Diagnostics.Watchdog.SetPaused(isPaused);
        }


        /// <summary>进程退出前的收尾：落一份"上一局摘要"供下次启动读取，控制台补一行汇总（无错误时不吭声）。</summary>
        private void OnApplicationQuit()
        {
            // 只清零本地标志，不主动恢复世界：进程都要退出了没必要。
            API.GameMenuPauseRuntime.Reset();

            // 先停看门狗：退出过程还要活一会儿（存档、淡出），不停会把它误判成卡死。
            Diagnostics.Watchdog.Uninstall();

            string summary = Diagnostics.ErrorRegistry.Summary();
            if (summary != null)
            {
                Logger.LogMessage(summary);
            }

            PolarisAPI.Errors.Guard(PolarisErrorNotice.PersistPending, "saving the previous session's error summary");

            // 最后删掉会话哨兵，这是"正常结束"的唯一表达方式；须排在 PersistPending 之后。
            Diagnostics.SessionSentinel.Close();
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
