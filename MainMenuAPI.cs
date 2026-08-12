using System;
using System.Collections.Generic;
using System.Linq;
using nel.title;
using UnityEngine;
using XX;

namespace Polaris
{
    public class MainMenuAPI
    {
        // 术语约定：name 是调用方给出的按钮名称，key 是 ResolveKey(name) 之后的实际按钮键。
        // 所有以按钮为索引的字典一律用 key 存取，只有 buttonNames 保存原始 name。

        /// <summary>默认按钮名称与游戏内部按钮键的映射，用于保留原版按钮的原生行为。</summary>
        static readonly Dictionary<string, string> reservedKeyMap = new()
        {
            ["startgame"] = "&&btn_new_game",
            ["continue"] = "&&btn_continue",
            ["settings"] = "&&btn_option",
            ["quit"] = "&&btn_quit",
        };

        /// <summary>原版按钮的初始顺序；名称需与 <see cref="reservedKeyMap"/> 的键一致。</summary>
        static readonly string[] defaultButtonNames = ["startgame", "continue", "settings", "quit"];

        readonly List<string> buttonNames = [];
        readonly Dictionary<string, FnBtnBindings> callbacks = [];

        internal MainMenuAPI()
        {
            buttonNames.AddRange(defaultButtonNames);
        }

        /// <summary>将按钮名称解析为实际写入游戏按钮数组、参与本地化查找的键。</summary>
        public static string ResolveKey(string name)
        {
            return reservedKeyMap.TryGetValue(name, out string key) ? key : name;
        }

        /// <summary>在初始菜单添加按钮。</summary>
        /// <param name="name">按钮名称，可用本地序列化键</param>
        /// <param name="callback">点下按钮后的回调</param>
        /// <param name="insertIndex">添加位置，-1为在最后追加（默认为-1）</param>
        /// <exception cref="ArgumentException">当插入位置非法时抛出</exception>
        public void AddButton(string name, FnBtnBindings callback, int insertIndex = -1)
        {
            if (insertIndex == -1)
            {
                buttonNames.Add(name);
            }
            else
            {
                if (insertIndex < 0 || insertIndex > buttonNames.Count)
                {
                    throw new ArgumentException("Illegal button insert position", nameof(insertIndex));
                }
                buttonNames.Insert(insertIndex, name);
            }
            callbacks.Add(ResolveKey(name), callback);
        }

        /// <summary>在初始菜单删除按钮。</summary>
        /// <param name="name">按钮名称，可用本地序列化键</param>
        /// <returns>返回true则移除成功，false则失败或者按钮不存在</returns>
        public bool RemoveButton(string name)
        {
            return buttonNames.Remove(name) && callbacks.Remove(ResolveKey(name));
        }

        /// <summary>返回当前初始菜单按钮。</summary>
        /// <returns>按钮键名字符串</returns>
        public IEnumerable<string> GetCurrentButtonList()
        {
            return buttonNames;
        }

        /// <summary>按当前注册顺序构建实际写入游戏按钮数组的键列表。</summary>
        internal string[] BuildButtonKeys()
        {
            return buttonNames.Select(ResolveKey).ToArray();
        }

        // ================== 顶部按钮换行布局 ==================
        // 原版顶部按钮硬编码固定 4 个单行铺满；transpiler 改为读这里算出的列数，超限自动换行。

        /// <summary>顶部按钮单行最多显示的数量，超过后自动换行。</summary>
        internal const int MaxButtonsPerRow = 6;

        // 原版容器高度 54px = 按钮高度 30px + 上下边距各 12px；行间无额外间距，每多一行加一个按钮高度。
        const float TopRowHeightBase = 54f;
        const float TopRowHeightStep = 30f;

        /// <summary>按钮总数算出实际使用的列数：不超过 <see cref="MaxButtonsPerRow"/>，也不超过总数本身。</summary>
        internal static int ButtonColumns(int totalCount)
        {
            return Math.Min(Math.Max(totalCount, 1), MaxButtonsPerRow);
        }

        /// <summary>按钮换行后的实际行数。</summary>
        internal static int ButtonRows(int totalCount)
        {
            int columns = ButtonColumns(totalCount);
            return (int)Math.Ceiling(Math.Max(totalCount, 1) / (double)columns);
        }

        /// <summary>顶部按钮容器的实际高度（原版固定 54px，现按行数增长）。</summary>
        internal static float TopRowHeight(int totalCount)
        {
            return TopRowHeightBase + (ButtonRows(totalCount) - 1) * TopRowHeightStep;
        }

        /// <summary>顶部按钮容器的纵向定位；中心上移半个增高量，使底边位置不变、只向上变高。</summary>
        internal static float TopRowY(int totalCount)
        {
            return 134f + (ButtonRows(totalCount) - 1) * (TopRowHeightStep / 2f);
        }

        /// <summary>修正末行按钮数量不足整行时贴左对齐的问题，重算横坐标使其居中；幂等，可重复调用。</summary>
        internal static void CenterTopRow(SceneTitleTemp instance)
        {
            BtnContainerRadio<aBtn> con = instance?.BConTop;
            if (con == null)
            {
                return;
            }

            int total = con.Length;
            if (total <= 0)
            {
                return;
            }

            int clms = ButtonColumns(total);
            int rem = total % clms;
            if (rem == 0 || clms <= 1)
            {
                return;
            }

            ObjCarrierCon carrier = con.getBaseCarr();
            if (carrier == null)
            {
                return;
            }

            float colStart = (clms - rem) / 2f;
            for (int k = 0; k < rem; k++)
            {
                aBtn btn = con.Get(total - rem + k);
                if (btn == null)
                {
                    continue;
                }

                float col = colStart + k;
                float x = carrier.conbase_x + (-0.5f + col / (clms - 1)) * carrier.bounds_w;
                Vector3 localPosition = btn.transform.localPosition;
                btn.transform.localPosition = new Vector3(x, localPosition.y, localPosition.z);
            }
        }

        /// <summary>根据按钮实际键取出注册的回调。</summary>
        internal bool TryGetCallback(string key, out FnBtnBindings callback)
        {
            return callbacks.TryGetValue(key, out callback);
        }

        // ================== 标题状态机集成 ==================
        // 每个"打开窗口"的按钮分配一个专属 STATE 哨兵值（负数），借用游戏状态机阻止标题菜单
        // 在窗口打开期间响应点击。切换必须走 changeState 方法本身而非直接写字段，因为按钮行
        // 隐藏/显示是 changeState 收尾动作的副作用。
        static readonly SceneTitleTemp.STATE TopStateValue = SceneTitleTemp.STATE.TOP;

        /// <summary>当前标题场景实例，由 Patch_SceneTitleTemp_initButtons 在场景初始化时写入。</summary>
        internal SceneTitleTemp Current { get; set; }

        readonly Dictionary<string, SceneTitleTemp.STATE> buttonStates = [];
        readonly Dictionary<string, Func<bool>> windowOpenCheckers = [];
        int nextStateSeed = -9000;

        /// <summary>当前处于"打开状态"的按钮解析键；null 表示菜单处于正常（TOP）状态。</summary>
        public string CurrentOpenButton { get; private set; }

        /// <summary>按下 ESC 且存在 <see cref="CurrentOpenButton"/> 时触发，参数为该按钮解析键。</summary>
        public event Action<string> Escaped;

        /// <summary>为一个"打开窗口"的按钮分配专属状态值；重复调用同一按钮是安全的（幂等）。</summary>
        /// <param name="name">按钮名称，规则与 <see cref="AddButton"/> 一致</param>
        public void AllocateButtonState(string name)
        {
            string key = ResolveKey(name);
            if (buttonStates.ContainsKey(key))
            {
                return;
            }

            buttonStates[key] = (SceneTitleTemp.STATE)nextStateSeed--;
        }

        /// <summary>切换到指定按钮的专属状态，并记录为当前打开的按钮；应在按钮回调即将打开窗口时调用。</summary>
        /// <param name="name">按钮名称，规则与 <see cref="AddButton"/> 一致</param>
        public void EnterButtonState(string name)
        {
            string key = ResolveKey(name);
            CurrentOpenButton = key;
            if (buttonStates.TryGetValue(key, out SceneTitleTemp.STATE value))
            {
                TrySetState(value);
            }
            ApplyCommandBarAndHint(key);
        }

        /// <summary>走原版"退出游戏"流程（与点标题菜单退出按钮同路径）。先清空 <see cref="CurrentOpenButton"/>，否则每帧的窗口关闭检查会把状态拨回 TOP、覆盖掉 QUIT；场景未初始化或切换失败时退化为直接调用 <c>IN.quitGame()</c>。</summary>
        public void QuitGame()
        {
            CurrentOpenButton = null;
            if (Current == null || !TrySetState(SceneTitleTemp.STATE.QUIT))
            {
                IN.quitGame();
            }
        }

        /// <summary>把标题状态机切回 TOP 并清空 <see cref="CurrentOpenButton"/>；窗口关闭方可随时调用。</summary>
        public void ReturnToTop()
        {
            CurrentOpenButton = null;
            // changeState(TOP) 已会隐藏 DsBlack 并恢复提示文本；不能再调用 ApplyCommandButton(false,false)，
            // 那会通过 remakeSumitCancelButton 把刚隐藏的 DsBlack 重新显示出来。
            TrySetState(TopStateValue);
        }

        /// <summary>为指定按钮注册"窗口是否仍打开"的探测函数；未注册时视为始终打开（只能靠手动归位或 ESC）。</summary>
        public void SetWindowOpenChecker(string name, Func<bool> isOpen)
        {
            windowOpenCheckers[ResolveKey(name)] = isOpen;
        }

        internal bool IsCurrentWindowStillOpen()
        {
            if (CurrentOpenButton == null)
            {
                return true;
            }

            return !windowOpenCheckers.TryGetValue(CurrentOpenButton, out Func<bool> checker) || checker();
        }

        internal void RaiseEscaped()
        {
            if (CurrentOpenButton != null)
            {
                Escaped?.Invoke(CurrentOpenButton);
            }
        }

        /// <summary>判断玩家本帧是否触发了"取消"输入；暂时把 ESC 和 X 都当作取消键处理。</summary>
        public static bool IsCancelInputPressed()
        {
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X);
        }

        /// <summary>切换标题状态机；返回是否真的切成功（场景未初始化或 changeState 抛异常都算失败）。</summary>
        bool TrySetState(SceneTitleTemp.STATE state)
        {
            if (Current == null)
            {
                return false;
            }

            try
            {
                Current.changeState(state);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] SceneTitleTemp.changeState call failed; ignored: {ex.Message}");
                return false;
            }
        }

        // ================== 确定/取消按钮条 + 操作提示行 ==================
        // DsBlack 是底部黑色按钮条的背景（private Designer 字段），remakeSumitCancelButton(false,false)
        // 并不等于"都不显示"——内部会落到"只显示取消"分支，并把已隐藏的 DsBlack 重新显示出来；
        // 真正要隐藏两侧时必须直接调用 DsBlack.hide()+SetActive(false)。操作提示行的文本来自
        // TxOnePoint 字段，仅在 state >= STATE.TOP 时才自动刷新，我们的负数哨兵状态需要手动写。
        // Current 为空（场景未初始化）时整体安静跳过。

        // DsBlack 初始样式是透明的（给"first_ask"提示用），真正的不透明黑底样式要
        // initDsBlackAfter() 设置一次（用 stencil_ref = 70 标记已初始化）。玩家在原版
        // 首次进入 STATE.TOP 之前就点开自定义按钮时需要主动补一次这个初始化。
        const int DsBlackStyledStencilRef = 70;

        static bool warnedCommandButton;
        static bool warnedHint;

        /// <summary>某个按钮打开窗口期间，确定/取消按钮条一侧的配置；<c>default</c> 表示该侧不显示。</summary>
        readonly struct CommandButtonConfig
        {
            public CommandButtonConfig(string label, FnBtnBindings callback, bool visible)
            {
                Label = label;
                Callback = callback;
                Visible = visible;
            }

            public string Label { get; }
            public FnBtnBindings Callback { get; }
            public bool Visible { get; }
        }

        readonly Dictionary<string, CommandButtonConfig> submitConfigs = [];
        readonly Dictionary<string, CommandButtonConfig> cancelConfigs = [];
        readonly Dictionary<string, string> hintConfigs = [];

        Dictionary<string, CommandButtonConfig> ConfigsFor(bool submit)
        {
            return submit ? submitConfigs : cancelConfigs;
        }

        /// <summary>
        /// 配置指定按钮的窗口打开期间，底部确定/取消按钮条中一侧的文案与点击回调（配置后默认可见）。
        /// </summary>
        /// <param name="name">按钮名称，规则与 <see cref="AddButton"/> 一致</param>
        /// <param name="submit">true 配置"确定"侧，false 配置"取消"侧</param>
        /// <param name="label">按钮文案</param>
        /// <param name="callback">点击回调</param>
        public void SetCommandButton(string name, bool submit, string label, FnBtnBindings callback)
        {
            string key = ResolveKey(name);
            ConfigsFor(submit)[key] = new CommandButtonConfig(label, callback, true);
            RefreshIfCurrent(key);
        }

        /// <summary>切换指定按钮已配置的确定/取消按钮条某一侧的显隐，不影响已登记的文案与回调。</summary>
        public void SetCommandButtonVisible(string name, bool submit, bool visible)
        {
            string key = ResolveKey(name);
            Dictionary<string, CommandButtonConfig> configs = ConfigsFor(submit);
            if (configs.TryGetValue(key, out CommandButtonConfig config))
            {
                configs[key] = new CommandButtonConfig(config.Label, config.Callback, visible);
                RefreshIfCurrent(key);
            }
        }

        /// <summary>移除指定按钮为确定/取消按钮条配置的某一侧内容，恢复为默认隐藏。</summary>
        public void ClearCommandButton(string name, bool submit)
        {
            string key = ResolveKey(name);
            ConfigsFor(submit).Remove(key);
            RefreshIfCurrent(key);
        }

        /// <summary>配置指定按钮的窗口打开期间，底部操作提示行显示的文本（可用 <see cref="KeyHint"/> 拼接键位图标）。</summary>
        public void SetOperationHint(string name, string hintText)
        {
            string key = ResolveKey(name);
            hintConfigs[key] = hintText;
            RefreshIfCurrent(key);
        }

        /// <summary>若指定按钮当前正是打开状态，立即重新应用其确定/取消按钮条与操作提示；否则什么都不做。</summary>
        void RefreshIfCurrent(string key)
        {
            if (CurrentOpenButton == key)
            {
                ApplyCommandBarAndHint(key);
            }
        }

        void ApplyCommandBarAndHint(string key)
        {
            // 未配置过的一侧取到 default，其 Visible 为 false，正好等于"该侧不显示"。
            submitConfigs.TryGetValue(key, out CommandButtonConfig submit);
            cancelConfigs.TryGetValue(key, out CommandButtonConfig cancel);
            ApplyCommandButton(submit, cancel);

            hintConfigs.TryGetValue(key, out string hint);
            ApplyOperationHint(hint);
        }

        void ApplyCommandButton(CommandButtonConfig submit, CommandButtonConfig cancel)
        {
            if (Current == null)
            {
                WarnCommandButtonOnce();
                return;
            }

            // remakeSumitCancelButton(false,false) 不等于"都不显示"，两侧都要隐藏时必须绕开它直接隐藏 DsBlack。
            if (!submit.Visible && !cancel.Visible)
            {
                HideCommandBar();
                return;
            }

            try
            {
                // 顺序不能换：initDsBlackAfter 先定样式，remakeSumitCancelButton 才加按钮；反过来按钮会被清掉。
                EnsureDsBlackStyled();
                Current.remakeSumitCancelButton(submit.Visible, cancel.Visible);
                ShowCommandBar();
                ApplyButtonSlot(Current.SubmitBtn, submit);
                ApplyButtonSlot(Current.CancelBtn, cancel);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] remakeSumitCancelButton call failed; ignored: {ex.Message}");
            }
        }

        /// <summary>确保 DsBlack 已应用过不透明黑底样式，不依赖玩家是否先触发过原版 changeState(TOP)。</summary>
        void EnsureDsBlackStyled()
        {
            if (!TryGetDsBlack(out Designer ds) || ds.stencil_ref == DsBlackStyledStencilRef)
            {
                return;
            }

            try
            {
                Current.initDsBlackAfter();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] SceneTitleTemp.initDsBlackAfter call failed; ignored: {ex.Message}");
            }
        }

        // 原版淡入逻辑只在真实 STATE 下触发，我们的哨兵状态不满足，故自起计时器按时间线性淡入。
        const float CommandBarFadeSeconds = 0.3f;
        float commandBarFadeT = CommandBarFadeSeconds;

        /// <summary>显示确定/取消按钮条；alpha 从 0 起，交给 <see cref="AdvanceCommandBarFade"/> 逐帧淡入到 1。</summary>
        void ShowCommandBar()
        {
            if (TryGetDsBlack(out Designer ds))
            {
                ds.gameObject.SetActive(true);
                ds.activate();
                ds.alpha = 0f;
                commandBarFadeT = 0f;
            }
        }

        /// <summary>对应 changeState 进入 STATE.TOP 时的 DsBlack.hide() + DsBlack.gameObject.SetActive(false)。</summary>
        void HideCommandBar()
        {
            if (TryGetDsBlack(out Designer ds))
            {
                ds.hide();
                ds.gameObject.SetActive(false);
                ds.alpha = 0f;
            }
            else
            {
                WarnCommandButtonOnce();
            }

            commandBarFadeT = CommandBarFadeSeconds;
            ApplyButtonSlot(Current?.SubmitBtn, default);
            ApplyButtonSlot(Current?.CancelBtn, default);
        }

        /// <summary>每帧推进确定/取消按钮条的淡入动画；淡入已完成或无打开窗口时不做任何事。</summary>
        internal void AdvanceCommandBarFade(float deltaSeconds)
        {
            if (CurrentOpenButton == null || commandBarFadeT >= CommandBarFadeSeconds)
            {
                return;
            }

            if (!TryGetDsBlack(out Designer ds))
            {
                return;
            }

            commandBarFadeT = Math.Min(CommandBarFadeSeconds, commandBarFadeT + deltaSeconds);
            ds.alpha = commandBarFadeT / CommandBarFadeSeconds;
        }

        /// <summary>取出当前标题场景的 DsBlack 黑底实例；Current 为空时返回 false。</summary>
        bool TryGetDsBlack(out Designer ds)
        {
            // 用类型模式而非 `!= null`：Unity Object 的运算符重载会把"已销毁但引用非空"也判为 null。
            if (Current?.DsBlack is Designer found)
            {
                ds = found;
                return true;
            }

            ds = null;
            return false;
        }

        void ApplyButtonSlot(aBtn btn, CommandButtonConfig config)
        {
            if (btn == null)
            {
                if (config.Visible)
                {
                    WarnCommandButtonOnce();
                }
                return;
            }

            btn.gameObject.SetActive(config.Visible);
            if (config.Visible)
            {
                btn.title = config.Label;
                FnBtnBindings callback = config.Callback;
                // 点击是引擎代码直接调用委托，异常隔离必须包在委托内部，否则会从按钮点击处理里抛出去。
                btn.addClickFn(b =>
                {
                    try
                    {
                        return callback(b);
                    }
                    catch (Exception ex)
                    {
                        PolarisAPI.Errors.Report(ex, $"the callback of submit/cancel button \"{config.Label}\"", callback.Method?.DeclaringType?.Assembly);
                        Plugin.Logger.LogError($"[Polaris] The callback of submit/cancel button \"{config.Label}\" threw an exception; ignored.");
                        return true;
                    }
                });
            }
        }

        void ApplyOperationHint(string hintText)
        {
            // hintText 为空时保持原样：ReturnToTop 已通过 changeState(TOP) 恢复默认提示。
            if (string.IsNullOrEmpty(hintText))
            {
                return;
            }

            if (Current?.TxOnePoint is not TextRenderer tx)
            {
                WarnHintOnce();
                return;
            }

            try
            {
                tx.text_content = hintText;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] Failed to write the key hint line; ignored: {ex.Message}");
            }
        }

        static void WarnCommandButtonOnce()
        {
            if (warnedCommandButton)
            {
                return;
            }
            warnedCommandButton = true;
            Plugin.Logger.LogWarning("[Polaris] The title scene is not initialized yet (Current is null); submit/cancel button bar customization has no effect for now.");
        }

        static void WarnHintOnce()
        {
            if (warnedHint)
            {
                return;
            }
            warnedHint = true;
            Plugin.Logger.LogWarning("[Polaris] The title scene is not initialized yet (Current is null); key hint line customization has no effect for now.");
        }
    }
}
