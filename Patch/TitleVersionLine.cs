using nel.title;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// 标题画面右下角版本号下面那行 <c>Polaris vX.Y.Z</c> 的唯一出处。
    /// <para>
    /// 游戏有两处会把 <c>TxVer.text_content</c> 整块重置成 <c>NEL.version</c>：
    /// <c>initTitleLogo</c>（开场 logo 动画播到 10 帧、<c>MdLogo == null</c> 时只跑这一次）
    /// 和 <c>fineTexts</c>（<c>fnClickLang</c> 换语言后会调用，把 <c>TxVer</c>、顶部按钮、
    /// 字体等一起按新语言重做）。只在前者追加会导致玩家一切语言就把这行冲掉，所以两处
    /// 重置之后都要补回来，共用这里这一份文本。
    /// </para>
    /// <para>
    /// 调用点都紧跟在游戏自己的整块赋值之后，每次追加前文本都是刚重置过的干净版本号，
    /// 因此直接 <c>+=</c> 不会叠加成多行，无需再做去重判断。
    /// </para>
    /// </summary>
    internal static class TitleVersionLine
    {
        internal static void Append(SceneTitleTemp instance)
        {
            // 玩家关掉了就当这个功能不存在。两个调用点都是"原版刚把整块文本重置过"，
            // 直接不追加即可，不需要再去擦掉什么。
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
