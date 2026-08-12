namespace Polaris
{
    public static partial class PolarisAPI
    {
        /// <summary>
        /// 主菜单按钮相关API
        /// </summary>
        public static MainMenuAPI MainMenu { get; } = new();

        /// <summary>
        /// 游戏内 ESC 菜单分类扩展，以及菜单本身的打开/关闭与世界暂停策略控制
        /// </summary>
        public static GameMenuAPI GameMenu { get; } = new();

        /// <summary>
        /// 设置项相关 API：声明的设置项会渲染进原版设置界面并自动持久化
        /// </summary>
        public static Settings.SettingsAPI Settings { get; } = new();

        // 游戏能力层入口 PolarisAPI.Game 是一个嵌套静态类，定义在 Api/Game/PolarisGameAPI.cs。

        /// <summary>
        /// 本地化 resolver 注册表：本地化子系统与下游模组在这里注册 key→文案的查询回调，
        /// 之后游戏原生 <c>TX.Get</c> 查到这些 key 时会优先采用。见 <see cref="Localization.LocalizationAPI"/>。
        /// </summary>
        public static Localization.LocalizationAPI Localization { get; } = new();

        // ── 以下是全库共用的基础设施：各子系统都要用，且与任何单一子系统的领域无关。
        //    准入判据就是这条分层规则——"加一个 PUI 按钮""加载一张 png"这类属于某个
        //    子系统自己的领域概念，永远不进 PolarisAPI，去 PolarisUIAPI / PolarisResAPI。

        /// <summary>
        /// BepInEx 已加载插件的只读视图；软依赖判断走
        /// <see cref="Infra.ModulesAPI.IsLoaded"/>。
        /// </summary>
        public static Infra.ModulesAPI Modules { get; } = new();

        /// <summary>Polaris 系列约定的目录结构。见 <see cref="Infra.PathsAPI"/>。</summary>
        public static Infra.PathsAPI Paths { get; } = new();

        /// <summary>
        /// 全系列唯一的类型扫描器：带缓存、只有一处 <c>ReflectionTypeLoadException</c> 兜底。
        /// 见 <see cref="Infra.TypesAPI"/>。
        /// </summary>
        public static Infra.TypesAPI Types { get; } = new();

        /// <summary>
        /// 错误上报与归因：判断出问题的是<b>模组 / Polaris / 原版游戏</b>，并写出报告。
        /// 见 <see cref="Infra.ErrorsAPI"/>。
        /// </summary>
        public static Infra.ErrorsAPI Errors { get; } = new();

        /// <summary>
        /// 会话级健康状况：上一局是不是正常结束的（崩溃/卡死检测），以及这一局主线程还在不在动。
        /// 见 <see cref="Infra.HealthAPI"/>。
        /// </summary>
        public static Infra.HealthAPI Health { get; } = new();
    }
}
