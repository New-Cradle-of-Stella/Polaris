using nel.title;

namespace Polaris
{
    /// <summary>标题画面里临时接管顶部按钮行的告知页的共用接口；新增一页无需改动补丁代码。</summary>
    internal interface ITitleOverlay
    {
        /// <summary>每帧从原版闸门问一次：返回 true 表示这一页仍要拦住标题菜单。首次调用时建页。</summary>
        bool Gate(SceneTitleTemp scene);

        /// <summary>推进淡入动画；由 <see cref="Patch.Patch_SceneTitleTemp_runIRD"/> 每帧调用。</summary>
        void AdvanceFade(float deltaSeconds);
    }
}
