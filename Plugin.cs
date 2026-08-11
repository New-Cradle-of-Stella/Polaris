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

            // 必须是第一行：这一步只记住"主线程是哪一个"并给心跳一个初值，之后所有面包屑埋点
            // 才知道自己在不在主线程上（见 Diagnostics.MainThreadBeat）。
            Diagnostics.MainThreadBeat.Install();

            // 尽早安装：从这一行开始，Unity 异常、后台线程未捕获异常、其它插件报出的严重错误
            // 都会被接住并归因。装在 EnsureDirectories 之前，是为了让下面那一行自己也受保护。
            Diagnostics.ErrorCapture.Install();

            // 目录建不出来（只读安装、权限不足）不该把整个 Awake 掀掉——Awake 抛异常之后
            // Unity 不会再调 Start，Start 里那几个子系统的初始化就一个都不会执行。这正是
            // PatchAllIndividually 当初要解决的那类失败模式，同一个坑不该在这里再踩一次。
            PolarisAPI.Errors.Guard(
                PolarisAPI.Paths.EnsureDirectories,
                "创建 Polaris 目录结构");

            // 崩溃与卡死检测。三步都要在这里、都要在目录建好之后：
            //   1. 阈值：看门狗线程一起跑就得带着它们，等不到 Start 阶段的设置项扫描；
            //   2. 环境信息：Application.version 这类属性只能主线程读，而写卡死报告的是看门狗线程；
            //   3. 哨兵：先读掉上一局留下的标记（那是崩溃的唯一证据），再为本局写一个新的。
            Diagnostics.DiagnosticsConfig.Resolve();
            Diagnostics.ErrorReportWriter.PrimeEnvironment();
            PolarisAPI.Errors.Guard(Diagnostics.SessionSentinel.Install, "登记本局会话哨兵");
            PolarisAPI.Errors.Guard(ReportLastSession, "读取上一局的结束情况");
            Diagnostics.Watchdog.Install();

            // 内置文案表。三张表都必须早于 Start 阶段的设置项扫描：绑定配置文件时要拿说明
            // 文字去写 .cfg 注释，那一步就已经在查表了。放在这里也顺便让告知页之类的早期
            // 界面能用上。
            Localization.PolarisStrings.Register();
            Lang.LangStrings.Register();
            Res.ResStrings.Register();

            harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            PatchAllIndividually();

            Logger.LogMessage(Logo);
        }

        /// <summary>
        /// 上一局没有正常结束时，把结论摊到控制台、写进本局报告、并给标题画面的告知页上膛。
        /// 上一局正常退出时（也就是绝大多数时候）这里一个字都不说。
        /// </summary>
        private static void ReportLastSession()
        {
            Diagnostics.LastSessionInfo last = Diagnostics.SessionSentinel.LastSession;
            if (last == null)
            {
                return;
            }

            Logger.LogWarning($"[Polaris] {last.OneLine()}");
            Logger.LogWarning($"[Polaris] 停在何处：{last.Where()}");

            Diagnostics.ErrorReportWriter.AppendPreviousSession(last);
            PolarisErrorNotice.AdoptLastSession(last);
        }

        /// <summary>
        /// 逐个类应用 Harmony 补丁，而不是一把 <c>harmony.PatchAll()</c>。
        /// <para>
        /// <c>PatchAll</c> 是全有全无的：任何一个补丁类出问题（目标方法有重载没指定参数类型、
        /// 游戏版本更新后方法没了、签名变了……）异常都会冒出 <c>Awake</c>，而 Awake 抛异常
        /// 之后 Unity 不会再调 <c>Start</c>——于是 <see cref="Start"/> 里那几个子系统的初始化
        /// 整个不执行，一个都起不来。一个补丁的问题不该有这么大的杀伤半径。
        /// </para>
        /// <para>
        /// 改成逐类应用之后，坏掉的那个补丁响亮地报错并跳过，其余功能照常。
        /// </para>
        /// </summary>
        private void PatchAllIndividually()
        {
            int applied = 0;

            foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                try
                {
                    // 面包屑：补丁应用是启动阶段少数几个"会执行大量反射与 IL 生成"的地方，
                    // 卡在这里时看门狗要能说出卡在哪一个补丁上。
                    using (Diagnostics.MainThreadBeat.Enter($"应用补丁 {type.Name}", type.Assembly))
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
                    // 责任人直接点名（就是 Polaris 自己——这些补丁类都在本程序集里），
                    // 不必走堆栈推断。归因、去重与报告归档都由 Errors 统一负责，这里只补一句
                    // 玩家最需要知道的后果。
                    PolarisAPI.Errors.Report(ex, $"应用补丁 {type.Name}", type.Assembly);
                    Logger.LogError($"[Polaris] 补丁 {type.Name} 负责的功能本局不可用。");
                }
            }

            Logger.LogMessage($"[Polaris] 已应用 {applied} 个 Harmony 补丁。");
        }

        /// <summary>
        /// 各子系统的初始化统一放在这里，而不是各自找地方自启：到了 Start 阶段所有插件都已
        /// 完成 Awake、程序集也都加载完毕，靠反射扫描其它模组的那几个子系统
        /// （PUI 的自动注册、Res 的资源发现、Lang 的 key 表）才看得到完整的插件名单。
        /// <para>
        /// <b>下面的顺序有硬约束，不能随手调</b>：Res 必须早于 PUI（PUI 的图片控件用 Res 取
        /// 素材）；Lang 的 resolver 必须早于 <see cref="Settings.SettingsAttributeScanner"/>
        /// （设置项的标签与说明支持 <c>&amp;键</c> 本地化写法，扫描时就要能求值）。
        /// </para>
        /// </summary>
        private void Start()
        {
            // 必须最先注册，赶在其它模组注册主菜单按钮之前占住"设置"后面的位置。
            PolarisManagementUI.RegisterButton();

            // 每个子系统各自兜底：一个起不来不该连累另外两个，更不该把异常抛回 Start
            // ——那会让后面的设置项扫描也一起不执行。
            InitSubsystem("资源", Res.Runtime.ResRuntime.Init);

            InitSubsystem("本地化", () =>
            {
                Lang.PlangRegistryScanner.ScanAll();
                PolarisAPI.Localization.RegisterResolver(Lang.PlangRuntime.Get);
            });

            InitSubsystem("PUI", PUI.PUIManager.Init);

            // 必须排在三个子系统之后：Builder 轨的设置项注册可能发生在子系统初始化里，
            // 两轨都到齐了才轮到特性轨扫描，也才能让"注册晚于设置界面"的警告判断准确。
            Settings.SettingsAttributeScanner.ScanAll();
        }

        /// <summary>
        /// 跑一个子系统的初始化。<see cref="PolarisAPI.Errors"/> 的 Guard 已经负责了面包屑、
        /// 归因与报告归档，这里只补一句玩家最需要知道的后果。
        /// </summary>
        private static void InitSubsystem(string name, Action init)
        {
            if (!PolarisAPI.Errors.Guard(init, $"{name}子系统初始化"))
            {
                Logger.LogError($"[Polaris] {name}子系统初始化失败，它的功能本局不可用。");
            }
        }

        /// <summary>
        /// Polaris 自己的每帧泵。目前只驱动 MTRX 就绪门控的等待队列
        /// （见 <see cref="GameApi.GameStateAPI.WhenReady"/>）——这件事所有下游模组都要用，
        /// 放在这里比让每个模组各建一个 MonoBehaviour 轮询划算。
        /// </summary>
        private void Update()
        {
            // 心跳必须是第一行、且在 Pump 之外：Pump 里跑的是下游模组注册进来的回调，
            // 万一其中一个卡住了，这一帧的心跳应该是"已经打过"的——看门狗量的是"帧与帧之间
            // 隔了多久"，把心跳放在回调之后会让卡住的那一帧连带把上一帧也算进停摆时长里。
            Diagnostics.MainThreadBeat.Beat(UnityEngine.Time.frameCount);

            PolarisAPI.Game.Pump();
        }

        /// <summary>
        /// 窗口失焦/回到前台。这是卡死误报的最大来源：<c>Application.runInBackground</c> 为 false 时，
        /// 窗口一失焦 Unity 就不再调 <see cref="Update"/>——主线程完全健康，只是没事干。
        /// 玩家去泡杯茶回来，看门狗已经"发现"了一次五分钟的卡死。
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            Diagnostics.Watchdog.SetPaused(!hasFocus);
        }

        /// <summary>
        /// 被系统挂起/恢复。语义和失焦不同（移动端才是常态，桌面端也会在某些窗口状态下触发），
        /// 但对看门狗的意义一样：这段时间里不推进帧是正常的。
        /// </summary>
        private void OnApplicationPause(bool isPaused)
        {
            Diagnostics.Watchdog.SetPaused(isPaused);
        }

        /// <summary>
        /// 进程退出前的收尾：把本局的错误情况落一份"上一局摘要"，供下次启动时标题画面的
        /// <see cref="PolarisErrorNotice"/> 读取；控制台补一行汇总（没有任何模组相关错误时
        /// <see cref="Diagnostics.ErrorRegistry.Summary"/> 返回 null，这里不吭声——没出错的
        /// 一局，错误系统必须一个字都不说）。
        /// </summary>
        private void OnApplicationQuit()
        {
            // 第一件事：停掉看门狗。这之后 Unity 不再调 Update，而进程还要活一会儿（存档、淡出、
            // 资源释放），不停掉就会把正常的退出过程判成卡死，还顺手给下一局上一发误报。
            Diagnostics.Watchdog.Uninstall();

            string summary = Diagnostics.ErrorRegistry.Summary();
            if (summary != null)
            {
                Logger.LogMessage(summary);
            }

            PolarisAPI.Errors.Guard(PolarisErrorNotice.PersistPending, "保存上一局错误摘要");

            // 最后一件事：删掉会话哨兵，这是"这一局是正常结束的"唯一表达方式。必须排在
            // PersistPending 之后——那一步才是正常退出路径下真正把摘要交给下一局的地方，
            // 在它之前就删掉哨兵，中间万一出事就两边都没留下。
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
                """;
    }
}
