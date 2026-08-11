using nel.title;
using XX;

namespace Polaris
{
    /// <summary>
    /// 标题告知页（<see cref="TitleOverlays"/>）显示期间，把原版标题画面那几件常驻装饰压住。
    /// <para>
    /// 起因：告知页的底板是 0xF0 的黑（<see cref="PolarisModWarning"/> 留了一丝透明度好透出
    /// 正在淡入的 logo），而原版的语言切换行 <c>DsLang</c>、左下角三个外链按钮 <c>DsLink</c> 都是
    /// <b>按钮</b>——它们仍然隐约可见，更要紧的是仍然可以点。按钮的命中测试走的是
    /// <c>XX.CLICK</c> 自己维护的那份 <c>List&lt;IClickable&gt;</c>，跟 Unity 的层级顺序、
    /// 前后遮挡、z 值统统无关，所以"盖在上面"根本挡不住点击。玩家在告知页上点到右下角，
    /// 会直接把游戏语言换掉；点到左下角，会拿浏览器打开 Discord/Twitter/Bilibili。
    /// </para>
    /// <para>
    /// 压制手段选的是 <c>Designer.alpha = 0</c>，而不是 <c>SetActive(false)</c> 或
    /// <c>Designer.hide()</c>：
    /// <list type="bullet">
    /// <item><c>SetActive(false)</c> 挡不住点击——<c>aBtn.clickable</c> 查的是按钮<b>自己</b>那个
    /// GameObject 的 <c>activeSelf</c>，按钮是 Designer 的子物体，父物体失活时它自己仍然是
    /// activeSelf==true，而 <c>CLICK</c> 的名单又不看层级。</item>
    /// <item><c>Designer.hide()</c> 能挡住（它一路走到 <c>aBtn.btn_enabled = false</c>），但顺带会
    /// 清掉悬停/选中态、把 <c>PreSelected</c> 挪进 <c>OfflineSelected</c>，恢复时的 <c>bind()</c>
    /// 又带着 <c>binding_check</c>／<c>default_focus</c> 那些副作用——为了藏一行装饰去动按钮容器的
    /// 绑定状态，风险和收益不成比例。</item>
    /// <item><c>alpha = 0</c> 一步到位：<c>Designer.alpha</c> 逐块下发到 <c>aBtn.setAlpha</c> →
    /// <c>Skin.alpha</c>，而 <c>aBtn.clickable</c> 里正好有一条 <c>Skin.alpha &gt; 0f</c>。
    /// 于是"看不见"和"点不到"是同一件事，恢复时也只是把这个数写回去，没有别的状态要还原。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 键盘/手柄的 LTab/RTab 换语言不走点击，绕开了上面这条 <c>Skin.alpha</c> 判定
    /// （<c>aBtn.ExecuteOnClick</c> 只看 <c>active &gt; 0</c>），单独由
    /// <see cref="Patch.Patch_SceneTitleTemp_languageShift"/> 拦掉。
    /// </para>
    /// <para>
    /// 必须每帧重设：原版 <c>runIRD</c> 在 <c>STATE.TOP</c> 的前 80 帧里每帧都会重写
    /// <c>DsLang.alpha</c> / <c>DsLink.alpha</c>（淡入），<c>TxOnePoint.alpha</c> 更是只要还没到 1
    /// 就一直重写。所以本类由 <see cref="Patch.Patch_SceneTitleTemp_runIRD"/> 的 Postfix 驱动——
    /// 那是原版这一帧写完之后的最后一句，我们的值才是最终值。
    /// </para>
    /// </summary>
    internal static class TitleChrome
    {
        /// <summary>
        /// 当前压制状态所属的标题场景。回标题会重建一份新的 <c>SceneTitleTemp</c>，那份的
        /// alpha 归原版自己管，旧场景遗留的"压制中"标记不能拿去对新场景做一次恢复。
        /// </summary>
        static SceneTitleTemp trackedScene;

        static bool suppressed;

        /// <summary>
        /// 每帧调一次。<paramref name="suppress"/> 为 true 时把那几件装饰按住不放，
        /// 转为 false 的那一帧恢复一次——恢复只在下降沿做，之后就完全交还给原版。
        /// </summary>
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

            // 恢复成 1 而不是"记下压制前的值再写回去"：告知页出现在 STATE.TOP 的
            // FIRST_LOGO_DELAY 帧之后，那时这几件东西的淡入早就跑完，压制前必然就是 1。
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

            // 左下角 Discord / Twitter / Bilibili 三个外链按钮：点中会拿系统浏览器开网页，
            // 和语言按钮属于同一类"隔着告知页被点到"的问题，一并压掉。
            Designer link = scene.DsLink;
            if (link != null)
            {
                link.alpha = alpha;
            }

            // 底部居中那行按键提示（KeyHelp_title_top，"←↓↑→ 移动　Z 决定"）。它不是按钮，
            // 纯粹是看着碍事：告知页自己底下就有一行按键提示，两行叠在一起会让玩家不知道
            // 该按哪个。
            TextRenderer hint = scene.TxOnePoint;
            if (hint != null)
            {
                hint.alpha = alpha;
            }
        }
    }
}
