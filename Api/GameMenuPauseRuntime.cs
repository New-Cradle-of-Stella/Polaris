using nel;
using nel.gm;

namespace Polaris.API
{
    /// <summary>
    /// <see cref="GameMenuAPI.SetWorldPause"/> 的运行时内核：谁真正拥有这次 <c>PauseMem</c>/
    /// <c>ResumeMem</c>，以及四个关键补丁是否都成功应用。只在 Unity 主线程读写。
    /// <para>
    /// 只在这一层跳过 <c>PauseMem()</c> 不够——世界主循环仍会被
    /// <c>NelM2DBase.run()</c>/<c>runPostForDraw()</c> 里的 <c>GM.isStoppingGame()</c> 截断；
    /// 只改那两处调用点又会让 <c>PauseMem</c> 注册的对象继续处于暂停状态。四个补丁必须成对生效，
    /// 见 <see cref="FeatureAvailable"/>。
    /// </para>
    /// </summary>
    internal static class GameMenuPauseRuntime
    {
        internal enum PatchTarget { Activate, Deactivate, Run, RunPostForDraw }

        /// <summary>ESC 菜单打开时是否暂停世界；默认 <c>true</c>，与原版行为一致。</summary>
        internal static bool PauseWorldWhileOpen = true;

        /// <summary>这次菜单打开是否是由 Polaris（经 <see cref="OnMenuPauseMemory"/>）真正执行过 <c>PauseMem</c>。
        /// 只有这个标志为真，关闭/切换时才有资格配对调用 <c>ResumeMem</c>。</summary>
        internal static bool MenuPauseApplied;

        /// <summary>上一次 <see cref="Pump"/> 时，外部暂停（转场/冻结器）是否处于活动状态，用于探测下降沿。</summary>
        internal static bool ExternalStopWasActive;

        static bool activatePatched;
        static bool deactivatePatched;
        static bool runPatched;
        static bool runPostForDrawPatched;

        /// <summary>四个关键补丁是否全部成功应用。任一缺失时，所有 helper 一律原样执行原版行为
        /// （fail-closed），避免"PauseMem 已跳过但世界循环仍停止"之类的半启用状态。</summary>
        internal static bool FeatureAvailable { get; private set; }

        internal static void ReportPatchApplied(PatchTarget target)
        {
            switch (target)
            {
                case PatchTarget.Activate: activatePatched = true; break;
                case PatchTarget.Deactivate: deactivatePatched = true; break;
                case PatchTarget.Run: runPatched = true; break;
                case PatchTarget.RunPostForDraw: runPostForDrawPatched = true; break;
            }

            FeatureAvailable = activatePatched && deactivatePatched && runPatched && runPostForDrawPatched;
        }

        internal static void SetPolicy(bool enabled)
        {
            if (PauseWorldWhileOpen == enabled)
            {
                return;
            }

            PauseWorldWhileOpen = enabled;
            Reconcile();
        }

        /// <summary>替换 <c>UiGameMenu.activate()</c> 里原本的 <c>M2D.PauseMem(true)</c> 调用。</summary>
        internal static void OnMenuPauseMemory(NelM2DBase m2d, bool particleSetterStop)
        {
            if (!FeatureAvailable || PauseWorldWhileOpen)
            {
                m2d.PauseMem(particleSetterStop);
                MenuPauseApplied = FeatureAvailable;
                return;
            }

            MenuPauseApplied = false;
        }

        /// <summary>替换 <c>UiGameMenu.deactivate()</c> 里原本的 <c>M2D.ResumeMem(true)</c> 调用。</summary>
        internal static void OnMenuResumeMemory(NelM2DBase m2d, bool particleSetterResume)
        {
            if (!FeatureAvailable)
            {
                m2d.ResumeMem(particleSetterResume);
                return;
            }

            if (MenuPauseApplied && !IsExternalStopActive())
            {
                m2d.ResumeMem(particleSetterResume);
            }

            MenuPauseApplied = false;
        }

        /// <summary>替换 <c>NelM2DBase.run()</c>/<c>runPostForDraw()</c> 里原本的 <c>GM.isStoppingGame()</c> 调用。</summary>
        internal static bool ShouldStopWorld(UiGameMenu gm)
        {
            bool vanilla = gm.isStoppingGame();
            if (!vanilla || !FeatureAvailable)
            {
                return vanilla;
            }

            // Game Over、读档、退出或退回标题等终止状态，以及 BENCH 之外真正在关闭的普通菜单，
            // 一律照原版停：只解除"菜单打开且策略为 false"这一种情况造成的停止。
            if (gm.isClosingGame() || GAMEOVER.isActive())
            {
                return true;
            }

            return PauseWorldWhileOpen;
        }

        static bool IsExternalStopActive()
        {
            NelM2DBase m2d = GameBinding.NelM2D;
            if (m2d == null)
            {
                return false;
            }

            return m2d.transferring_game_stopping || m2d.Freezer.isPausing();
        }

        /// <summary>由 <see cref="GameMenuAPI.Pump"/> 每帧调用：外部暂停从活动变为不活动时补一次对账，
        /// 让菜单重新拿回本应属于自己的 PauseMem/ResumeMem。不在绘制路径调用，避免产生副作用的时机不可控。</summary>
        internal static void Pump()
        {
            bool externalActive = IsExternalStopActive();
            if (ExternalStopWasActive && !externalActive)
            {
                Reconcile();
            }

            ExternalStopWasActive = externalActive;
        }

        static void Reconcile()
        {
            if (!FeatureAvailable)
            {
                return;
            }

            NelM2DBase m2d = GameBinding.NelM2D;
            UiGameMenu gm = m2d?.GM;
            if (gm == null || !gm.isActive() || IsExternalStopActive())
            {
                return;
            }

            if (PauseWorldWhileOpen && !MenuPauseApplied)
            {
                m2d.PauseMem(true);
                MenuPauseApplied = true;
            }
            else if (!PauseWorldWhileOpen && MenuPauseApplied)
            {
                m2d.ResumeMem(true);
                MenuPauseApplied = false;
            }
        }

        /// <summary>进程退出时调用，只清零本地标志，不触发任何 PauseMem/ResumeMem 副作用。</summary>
        internal static void Reset()
        {
            PauseWorldWhileOpen = true;
            MenuPauseApplied = false;
            ExternalStopWasActive = false;
        }
    }
}
