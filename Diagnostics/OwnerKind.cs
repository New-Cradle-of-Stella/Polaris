namespace Polaris.Diagnostics
{
    /// <summary>一个程序集在"出了事该找谁"上的归属分类，是归因引擎全部结论的落点。</summary>
    public enum OwnerKind
    {
        /// <summary>判不出来。宁可留白，也不要猜一个责任人出来冤枉谁。</summary>
        Unknown = 0,

        /// <summary>.NET 基础类库与 Unity 引擎程序集。永远不是责任人，走栈时直接跳过。</summary>
        Runtime,

        /// <summary>原版游戏本体及其随包分发的第三方程序集（都在游戏的 Managed 目录下）。</summary>
        Vanilla,

        /// <summary>BepInEx / HarmonyX / MonoMod / Cecil 这些加载器与补丁框架本身。</summary>
        Framework,

        /// <summary>Polaris 自己。</summary>
        Polaris,

        /// <summary>第三方 BepInEx 插件，也就是玩家眼里的"模组"。</summary>
        Mod,

        /// <summary>plugins 目录下但本身不是插件的附属程序集，能定位到文件但没有作者信息。</summary>
        ModLibrary,

        /// <summary>运行期生成、没有落盘位置的程序集（Harmony DMD、反射 Emit）。</summary>
        Dynamic,
    }
}
