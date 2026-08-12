namespace Polaris.Res.Runtime
{
    /// <summary>资源子系统的初始化编排入口，由 <c>Polaris.Plugin.Start()</c> 调用（此时全部插件已 Awake）。</summary>
    internal static class ResRuntime
    {
        private static bool initialized;

        internal static void Init()
        {
            if (initialized)
            {
                Plugin.Logger.LogWarning("[PolarisRes] ResRuntime.Init was called more than once; ignored.");
                return;
            }
            initialized = true;
            ResHost.EnsureCreated();
            AutoBindScanner.ScanAll();
            Plugin.Logger.LogInfo("[PolarisRes] Resource runtime initialized.");
        }
    }
}
