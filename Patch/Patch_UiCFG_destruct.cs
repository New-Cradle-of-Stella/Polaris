using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>UiCFG</c> 被拆掉（标题画面离开设置页时就是这样）：松开搜索过滤对界面对象的引用。
    /// <para>
    /// 不松手的后果不是"内存泄漏"这么客气——静态字段会一直攥着一批已经销毁的 Unity 对象，
    /// 下一次要是有哪条路径先用到过滤、再走 <see cref="PolarisSettingsScreen.Append"/> 重新登记，
    /// 中间那一下就会踩到假 null。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.destruct))]
    internal static class Patch_UiCFG_destruct
    {
        static void Postfix()
        {
            SettingsSearchFilter.Forget();
            SettingsSearchBox.Forget();
        }
    }
}
