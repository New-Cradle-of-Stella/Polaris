using System;
using nel.gm;

namespace Polaris.API
{
    /// <summary>
    /// 游戏内 ESC 菜单的一次打开。入口是 <c>PolarisAPI.Game.Menu</c> 与
    /// <see cref="GameStaticCallbackKind.GameMenuOpened"/> 回调。
    /// <para>
    /// 注意与 <c>PolarisAPI.GameMenu</c> 区分：那个是 Polaris 自己的<b>菜单分类扩展</b>注册表
    /// （给菜单加一页），而这里是<b>菜单本身这一次打开</b>的实例（关掉它、问它在不在编辑某个分类）。
    /// 两者名字接近但不是一回事。
    /// </para>
    /// </summary>
    public sealed class GameMenu : GameInstance
    {
        static readonly InstanceTable<UiGameMenu, GameMenu> Table = new();

        readonly UiGameMenu menu;

        GameMenu(UiGameMenu menu)
        {
            this.menu = menu;
        }

        internal static GameMenu Wrap(UiGameMenu native) => Table.Get(native, static n => new GameMenu(n));

        internal static GameMenu Peek(UiGameMenu native) => Table.Peek(native);

        internal static void Invalidate(UiGameMenu native) => Table.Invalidate(native);

        internal static void SweepMenus() => Table.Sweep();

        UiGameMenu Native => IsValid ? menu : null;

        private protected override bool IsNativeAlive
        {
            get
            {
                if (menu == null)
                {
                    return false;
                }

                try
                {
                    // OFFLINE 就是"这个菜单已经不在了"。用状态而不是 Unity 的销毁判定：
                    // 菜单对象本身是复用的，销毁判定要等到很久以后才会为真。
                    return menu.state != UiGameMenu.STATE.OFFLINE;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private protected override string Describe() => "GameMenu";

        /// <summary>判断该菜单当前是否可以处理输入。</summary>
        public bool CanHandleInput => Read(static m => m.general_button_handleable, false);

        /// <summary>获取或设置该菜单是否应退出当前分类。</summary>
        public bool ShouldQuitCategory
        {
            get => Read(static m => m.category_to_quit, false);
            set
            {
                EnsureUsable();
                Act("ShouldQuitCategory", m => m.category_to_quit = value);
            }
        }

        /// <summary>
        /// 获取或设置该菜单的输入处理开关。
        /// <para>
        /// 这一项在游戏里是<b>全局</b>的（菜单类型上的一个静态开关），不是每个菜单实例各自一份。
        /// 放在实例上是因为同一时刻只会有一个游戏菜单；关掉之后记得开回来，
        /// 否则下一次打开的菜单也会不响应输入。
        /// </para>
        /// </summary>
        public bool IsInputHandlingEnabled
        {
            get
            {
                try
                {
                    return UiGameMenu.handle;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            set
            {
                EnsureUsable();

                try
                {
                    UiGameMenu.handle = value;
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "GameMenu.IsInputHandlingEnabled");
                }
            }
        }

        /// <summary>关闭该菜单实例。<paramref name="immediate"/> 为真时跳过关闭动画。</summary>
        public void Close(bool immediate = false)
        {
            EnsureUsable();
            Act("Close", m => m.deactivate(immediate));
        }

        /// <summary>判断该菜单是否正在关闭。</summary>
        public bool IsClosing() => Read(static m => m.isClosingGame(), false);

        /// <summary>判断该菜单是否正在暂停世界运行。</summary>
        public bool IsStoppingWorld() => Read(static m => m.isStoppingGame(), false);

        /// <summary>判断该菜单是否处于长椅菜单状态。</summary>
        public bool IsBenchMenuActive() => Read(static m => m.isBenchMenuActive(false), false);

        /// <summary>
        /// 判断该菜单是否正在编辑指定分类。<paramref name="categoryKey"/> 用游戏的分类名
        /// （<c>STAT</c>/<c>ITEM</c>/<c>MAGIC</c>/<c>SKILL</c>/<c>ENHANCER</c>/<c>MAP</c>/
        /// <c>SCENARIO</c>/<c>BENCH</c>/<c>CONFIG</c>），不分大小写；名字不认识时返回 <c>false</c>。
        /// </summary>
        public bool IsEditingCategory(string categoryKey)
        {
            UiGameMenu m = Native;
            if (m == null || string.IsNullOrEmpty(categoryKey))
            {
                return false;
            }

            if (!Enum.TryParse(categoryKey, true, out CATEG category))
            {
                return false;
            }

            try
            {
                return m.isEditState() && m.edit_categ == category;
            }
            catch (Exception)
            {
                return false;
            }
        }

        TValue Read<TValue>(Func<UiGameMenu, TValue> read, TValue fallback)
        {
            UiGameMenu m = Native;
            if (m == null)
            {
                return fallback;
            }

            try
            {
                return read(m);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        void Act(string what, Action<UiGameMenu> action)
        {
            UiGameMenu m = Native;
            if (m == null)
            {
                return;
            }

            try
            {
                action(m);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, $"GameMenu.{what}");
            }
        }
    }
}
