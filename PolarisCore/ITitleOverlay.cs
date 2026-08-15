using System;
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

    /// <summary>把两个方法转发出去的适配器：各告知页都是全静态类，用它把静态方法接到接口上，省得每页各写一份同样的壳。</summary>
    internal sealed class TitleOverlay : ITitleOverlay
    {
        readonly Func<SceneTitleTemp, bool> gate;
        readonly Action<float> advanceFade;

        internal TitleOverlay(Func<SceneTitleTemp, bool> gate, Action<float> advanceFade)
        {
            this.gate = gate;
            this.advanceFade = advanceFade;
        }

        public bool Gate(SceneTitleTemp scene) => gate(scene);

        public void AdvanceFade(float deltaSeconds) => advanceFade(deltaSeconds);
    }
}
