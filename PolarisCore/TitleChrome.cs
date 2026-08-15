using nel.title;
using XX;

namespace Polaris
{
    /// <summary>
    /// 标题告知页显示期间，压住原版语言按钮 <c>DsLang</c>、外链按钮 <c>DsLink</c> 与提示文本，
    /// 否则它们隔着告知页仍然可点（点击命中测试与遮挡无关）。用 <c>Designer.alpha = 0</c> 压制，
    /// 因为它同时挡住可见性与点击判定（<c>aBtn.clickable</c> 检查 Skin.alpha），且没有副作用要还原。
    /// 键盘/手柄换语言绕过点击判定，单独由 <see cref="Patch.Patch_SceneTitleTemp_languageShift"/> 拦掉。
    /// 须每帧重设：原版每帧都会重写这些 alpha。
    /// </summary>
    internal static class TitleChrome
    {
        /// <summary>当前压制状态所属的标题场景；场景重建后旧场景的压制标记不能沿用到新场景。</summary>
        static SceneTitleTemp trackedScene;

        static bool suppressed;

        /// <summary>每帧调一次；<paramref name="suppress"/> 为 true 时压住装饰，转为 false 的那一帧恢复一次并交还原版。</summary>
        internal static void Apply(SceneTitleTemp scene, bool suppress)
        {
            if (!ReferenceEquals(scene, trackedScene))
            {
                trackedScene = scene;
                suppressed = false;
            }

            if (suppress)
            {
                suppressed = true;
                SetAlpha(scene, 0f);
                return;
            }

            if (!suppressed)
            {
                return;
            }

            suppressed = false;

            // 恢复成 1 而非记下压制前的值：告知页出现时这些淡入早已跑完，压制前必然是 1。
            SetAlpha(scene, 1f);
        }

        static void SetAlpha(SceneTitleTemp scene, float alpha)
        {
            // 语言切换行（屏幕右下角那排语言旗标按钮）。
            Designer lang = scene.DsLang;
            if (lang != null)
            {
                lang.alpha = alpha;
            }

            // 左下角 Discord / Twitter / Bilibili 三个外链按钮，同样会被隔着告知页点到，一并压掉。
            Designer link = scene.DsLink;
            if (link != null)
            {
                link.alpha = alpha;
            }

            // 底部居中那行按键提示不是按钮，纯粹是看着碍事：与告知页自己的提示行叠在一起会混淆。
            TextRenderer hint = scene.TxOnePoint;
            if (hint != null)
            {
                hint.alpha = alpha;
            }
        }
    }
}
