using System;
using nel.gm;

namespace Polaris
{
    /// <summary>把 <see cref="UiGMC"/> 子类化的细节封在内部，让 mod 侧只需提供一个 <c>Action&lt;UiBoxDesigner&gt;</c> 即可注册分类。</summary>
    internal sealed class GameMenuCategoryController : UiGMC
    {
        readonly GameMenuAPI.CategoryRegistration reg;

        public GameMenuCategoryController(UiGameMenu gm, CATEG categ, GameMenuAPI.CategoryRegistration reg)
            : base(gm, categ)
        {
            this.reg = reg;
        }

        public override bool initAppearMain()
        {
            base.initAppearMain();
            BxR.Clear();
            BxR.init();

            // Mod 回调不能让异常飞出 initAppearMain，否则会中止游戏菜单本次调用链。
            try
            {
                reg.BuildContent(BxR);
            }
            catch (Exception ex)
            {
                // 直接用回调所在程序集作为责任人，不必走堆栈推断。
                PolarisAPI.Errors.Report(ex, $"building the content of custom category \"{reg.DisplayName}\"", reg.BuildContent.Method?.DeclaringType?.Assembly);
                Plugin.Logger.LogError($"[Polaris] Building the content of custom category \"{reg.DisplayName}\" threw an exception; ignored.");
            }

            return true;
        }

        public override bool canInitEdit() => reg.CanEnter();
        public override void initEdit() { }
        public override void quitEdit() { }
    }
}
