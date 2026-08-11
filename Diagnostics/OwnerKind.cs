namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一个程序集在"出了事该找谁"这件事上的归属。归因引擎的全部结论最终都落回这个枚举。
    /// <para>
    /// 顺序有意义：<see cref="AssemblyOwnerIndex"/> 的判定表按"路径优先于名字"逐条往下走，
    /// 而 <see cref="StackAttribution"/> 走栈时把 <see cref="Runtime"/> 当作透明的
    /// （<c>ArgumentNullException</c> 的抛出点在 mscorlib 里，责任显然不在 BCL），
    /// 把 <see cref="Mod"/>/<see cref="Polaris"/> 当作可定责的。
    /// </para>
    /// </summary>
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

        /// <summary>
        /// plugins 目录下但本身不是插件的附属程序集（模组随包分发的依赖、
        /// <see cref="Infra.PathsAPI.LibsDir"/> 里的那些）。能定位到文件，但没有作者信息。
        /// </summary>
        ModLibrary,

        /// <summary>
        /// 运行期生成、没有落盘位置的程序集：Harmony 的 DMD、反射 Emit 出来的动态程序集。
        /// 遇到这种帧要先用 <c>Harmony.GetOriginalMethodFromStackframe</c> 还原回原始方法。
        /// </summary>
        Dynamic,
    }
}
