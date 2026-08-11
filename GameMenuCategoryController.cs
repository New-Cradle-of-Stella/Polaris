using System;
using nel.gm;

namespace Polaris
{
    /// <summary>
    /// 把游戏要求的 <see cref="UiGMC"/> 子类实现细节封在 Polaris 内部：mod 侧通过
    /// <see cref="GameMenuAPI.AddCategory"/> 只需要提供一个 <c>Action&lt;UiBoxDesigner&gt;</c>，
    /// 不需要自己认识 <see cref="UiGMC"/>，也不需要在自己的项目里引用 Krafs.Publicizer
    /// （<see cref="UiGMC"/> 的构造函数是 internal，外部程序集想直接 <c>: UiGMC</c> 派生
    /// 必须先把游戏程序集 publicize 一遍；这件事只在 Polaris 内部做一次）。
    /// </summary>
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

            // BuildContent 是 Mod 自己的代码，跑在游戏的 ESC 菜单调用栈里；写坏了不能让异常
            // 直接飞出 initAppearMain——那会中止游戏菜单本次的界面调用（原版的
            // appearCategory/BxRRemake 调用链之后可能还有别的收尾没做完）。BxR 已经
            // Clear()+init() 过，至少不会残留上一次内容。
            try
            {
                reg.BuildContent(BxR);
            }
            catch (Exception ex)
            {
                // 责任人就是这个回调委托本身所在的程序集，不必走堆栈推断。
                PolarisAPI.Errors.Report(ex, $"自定义分类 \"{reg.DisplayName}\" 的内容构建", reg.BuildContent.Method?.DeclaringType?.Assembly);
                Plugin.Logger.LogError($"[Polaris] 自定义分类 \"{reg.DisplayName}\" 的内容构建抛出异常，已忽略。");
            }

            return true;
        }

        public override bool canInitEdit() => reg.CanEnter();
        public override void initEdit() { }
        public override void quitEdit() { }
    }
}
