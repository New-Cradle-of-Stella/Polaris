using System;

namespace Polaris.Lang
{
    /// <summary>
    /// 扫描已加载插件程序集里标了 <see cref="PlangAutoRegistrationAttribute"/> 的类，逐个构造
    /// 实例并调用 <see cref="IPlangRegistrar.Register"/>，取代旧版 <c>LangLoader.LoadAll</c>
    /// 的运行时目录扫描——现在 Key/文案是编译期生成进代码里的，不再有 <c>.plang</c> 数据文件
    /// 需要在运行时读盘。
    /// <para>
    /// 作用域用 <see cref="Infra.TypesAPI.InPluginsWith{TAttr}"/> 而不是 <c>InAppDomain</c>：
    /// <c>.plang</c> 生成的注册类必然就在模组自己的插件程序集里（源文件由 PolarisTools 的
    /// 单文件生成器直接产出进同一个项目），不会像 PUI 实现那样可能被拆到附属 dll，没必要
    /// 付扫游戏本体程序集的代价。
    /// </para>
    /// <para>
    /// 扫描同时是 key 冲突的判定现场：每个注册类被调用前都会点名它所属的程序集
    /// （<see cref="PlangConflictGuard.CurrentSource"/>），于是"同一个 key 被两个模组注册"能被
    /// 当场认出来；全部注册结束后 <see cref="PlangConflictGuard.Seal"/> 汇总处置——只要有一个
    /// 冲突，这一局就会被判定为致命错误（理由见 <see cref="PlangConflictGuard"/>）。
    /// </para>
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

                // 一个模组的注册类写坏（比如撞了个奇怪的构造函数）不该连累其它模组——
                // 这里是遍历中间，异常不接住会中止整次扫描，后面排队的模组一个都注册不上。
                try
                {
                    // 点名当前注册方，供 PlangRuntime.Register 判断 key 冲突（见
                    // PlangConflictGuard.CurrentSource）。finally 清空：留着会让扫描之后
                    // 别处直接调 Register 时把账记到最后一个被扫到的模组头上。
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

            // 所有注册都到齐了才处置冲突：一次启动只报一条致命错误，里面列全所有撞车的 key，
            // 而不是发现一个报一条。
            PlangConflictGuard.Seal();
        }
    }
}
