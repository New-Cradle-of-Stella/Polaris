using nel.title;

namespace Polaris
{
    /// <summary>
    /// 标题画面里"暂时接管顶部按钮行"的一次性告知页共用的最小接口。
    /// <para>
    /// 借用的是原版自己的闸门 <c>SceneTitleTemp.errorAnnounceBox</c>：返回 true 就不激活
    /// 顶部按钮行，玩家碰不到"开始游戏/读取"那一排按钮。<see cref="TitleOverlays"/> 按优先级
    /// 顺序依次问过去，第一个答"是"的页面独占当前帧；这个接口存在的唯一理由，就是让
    /// "加一页新告知"不必再把 <c>Patch_SceneTitleTemp_errorAnnounceBox</c> 和
    /// <c>Patch_SceneTitleTemp_runIRD</c> 两个补丁都改一遍。
    /// </para>
    /// </summary>
    internal interface ITitleOverlay
    {
        /// <summary>每帧从原版闸门问一次：返回 true 表示这一页仍要拦住标题菜单。首次调用时建页。</summary>
        bool Gate(SceneTitleTemp scene);

        /// <summary>推进淡入动画；由 <see cref="Patch.Patch_SceneTitleTemp_runIRD"/> 每帧调用。</summary>
        void AdvanceFade(float deltaSeconds);
    }
}
