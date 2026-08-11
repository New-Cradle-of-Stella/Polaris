namespace Polaris.Res.Runtime
{
    /// <summary>
    /// 资源子系统的初始化编排入口，由 <c>Polaris.Plugin.Start()</c> 调用——此时所有插件的
    /// <c>Awake()</c> 都已跑完，程序集也都已加载完毕，这里可以安全地扫描模组、建常驻宿主。
    /// </summary>
    internal static class ResRuntime
    {
        private static bool initialized;

        internal static void Init()
        {
            if (initialized)
            {
                Plugin.Logger.LogWarning("[PolarisRes] ResRuntime.Init 被重复调用，已忽略。");
                return;
            }

            initialized = true;

            ResHost.EnsureCreated();

            // 全自动发现：扫描所有已加载插件程序集里的 [PolarisResource] 静态字段，
            // 按"与 dll 同名的文件夹"约定自动挂载 + 回填。这里是 Polaris 自己的 Start()，
            // BepInEx 早已在这之前完成了所有插件的 Awake()，
            // UnityChainloader.Instance.Plugins 已经是全量的。
            AutoBindScanner.ScanAll();

            Plugin.Logger.LogInfo("[PolarisRes] 资源库运行时已初始化。");
        }
    }
}
