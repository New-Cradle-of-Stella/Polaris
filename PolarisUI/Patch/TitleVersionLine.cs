using nel.title;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// 标题画面右下角版本号下面那行 <c>Polaris vX.Y.Z</c> 的唯一出处，供 <c>initTitleLogo</c> 和
    /// <c>fineTexts</c> 两处在游戏重置 <c>TxVer.text_content</c> 后调用补回。调用点紧跟在原版整块
    /// 赋值之后，文本总是刚重置的干净版本号，直接 <c>+=</c> 不会叠加多行。
    /// </summary>
    internal static class TitleVersionLine
    {
        internal static void Append(SceneTitleTemp instance)
        {
            // 玩家关闭该功能时直接不追加，无需擦除已有文本。
            if (!Settings.PolarisSettings.ShowTitleVersionLine)
            {
                return;
            }

            TextRenderer tx = instance?.TxVer;
            if (tx == null)
            {
                return;
            }

            tx.text_content += $"\n<font size=\"10\">Polaris v{MyPluginInfo.PLUGIN_VERSION}</font>";
        }
    }
}
