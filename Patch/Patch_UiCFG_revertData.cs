using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 设置界面"取消"时回滚 Polaris 的设置项，对应原版 <c>revertData</c> 从
    /// <c>BaRevertData</c> 快照恢复 <c>CFG</c> 的那一步。
    /// 回滚只改内存并通知模组（好让运行中的效果一起撤销），不写盘——磁盘上还是上次提交的内容。
    /// <para>
    /// 控件显示不在这里拨正：原版的 <c>UiCFG</c> 取消之后整个界面就收起了，
    /// 下次打开走 <c>resume()</c>，由 <see cref="Patch_UiCFG_resume"/> 统一同步。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.revertData))]
    internal static class Patch_UiCFG_revertData
    {
        static void Postfix() => SettingsStore.Revert();
    }
}
