using System;

namespace Polaris.Lang
{
    /// <summary>
    /// 扫描已加载插件程序集里标了 <see cref="PlangAutoRegistrationAttribute"/> 的类，逐个构造实例并调用
    /// <see cref="IPlangRegistrar.Register"/>；Key/文案现已编译期生成进代码，取代旧版运行时目录扫描。
    /// 扫描同时是 key 冲突判定现场：每个注册类调用前先点名所属程序集（<see cref="PlangConflictGuard.CurrentSource"/>），
    /// 全部结束后由 <see cref="PlangConflictGuard.Seal"/> 汇总处置。
    /// </summary>
    internal static class PlangRegistryScanner
    {
        static bool scanned;

        /// <summary>在 <c>Plugin.Init</c> 里调用一次。</summary>
        internal static void ScanAll()
        {
            if (scanned)
            {
                return;
            }

            scanned = true;

            int count = 0;
            foreach ((Type type, _) in PolarisAPI.Types.InPluginsWith<PlangAutoRegistrationAttribute>())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(IPlangRegistrar).IsAssignableFrom(type))
                {
                    continue;
                }

                // 一个模组的注册类写坏不该连累其它模组，捕获异常避免中止整次扫描。
                try
                {
                    // 点名当前注册方供 PlangRuntime.Register 判断 key 冲突；finally 清空以免影响扫描之后的直接调用。
                    PlangConflictGuard.CurrentSource = type.Assembly;
                    ((IPlangRegistrar)Activator.CreateInstance(type)).Register();
                    count++;
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"[PolarisLang] Failed to auto-register {type.FullName}; skipped: {e}");
                }
                finally
                {
                    PlangConflictGuard.CurrentSource = null;
                }
            }

            Plugin.Logger.LogMessage($"[PolarisLang] Registered localization text from {count} generated classes.");

            // 所有注册都到齐了才处置冲突：一次启动只报一条致命错误，列全所有撞车的 key。
            PlangConflictGuard.Seal();
        }
    }
}
