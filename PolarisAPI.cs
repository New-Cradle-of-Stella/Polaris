namespace Polaris
{
    public static partial class PolarisAPI
    {
        /// <summary>主菜单按钮相关 API。</summary>
        public static MainMenuAPI MainMenu { get; } = new();

        /// <summary>游戏内 ESC 菜单分类扩展，以及菜单本身的打开/关闭与世界暂停策略控制。</summary>
        public static GameMenuAPI GameMenu { get; } = new();

        /// <summary>设置项相关 API：声明的设置项会渲染进原版设置界面并自动持久化。</summary>
        public static Settings.SettingsAPI Settings { get; } = new();

        // 游戏能力层入口 PolarisAPI.Game 是一个嵌套静态类，定义在 Api/Game/PolarisGameAPI.cs。

        /// <summary>本地化 resolver 注册表：注册 key→文案回调，供原版 <c>TX.Get</c> 优先采用。</summary>
        public static Localization.LocalizationAPI Localization { get; } = new();

        // ── 以下是全库共用的基础设施，与任何单一子系统的领域无关（领域概念应去 PolarisUIAPI / PolarisResAPI）。

        /// <summary>BepInEx 已加载插件的只读视图；软依赖判断走 <see cref="Infra.ModulesAPI.IsLoaded"/>。</summary>
        public static Infra.ModulesAPI Modules { get; } = new();

        /// <summary>Polaris 系列约定的目录结构。见 <see cref="Infra.PathsAPI"/>。</summary>
        public static Infra.PathsAPI Paths { get; } = new();

        /// <summary>全系列唯一的类型扫描器，带缓存与 <c>ReflectionTypeLoadException</c> 兜底。</summary>
        public static Infra.TypesAPI Types { get; } = new();

        /// <summary>错误上报与归因：判断出问题的是模组、Polaris 还是原版游戏，并写出报告。</summary>
        public static Infra.ErrorsAPI Errors { get; } = new();

        /// <summary>会话级健康状况：上一局是否正常结束、主线程是否仍在动。</summary>
        public static Infra.HealthAPI Health { get; } = new();
    }
}
