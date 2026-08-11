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

        /// <summary>
        /// 默认按钮名称与游戏内部按钮键的映射，用于保留原版按钮的原生行为
        /// </summary>
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

        /// <summary>
        /// 将按钮名称解析为实际写入游戏按钮数组、参与本地化查找的键
        /// </summary>
        public static string ResolveKey(string name)
        {
            return reservedKeyMap.TryGetValue(name, out string key) ? key : name;
        }

        /// <summary>
        /// 在初始菜单添加按钮
        /// </summary>
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
                    throw new ArgumentException("非法插入按钮位置", nameof(insertIndex));
                }
                buttonNames.Insert(insertIndex, name);
            }
            callbacks.Add(ResolveKey(name), callback);
        }

        /// <summary>
        /// 在初始菜单删除按钮
        /// </summary>
        /// <param name="name">按钮名称，可用本地序列化键</param>
        /// <returns>返回true则移除成功，false则失败或者按钮不存在</returns>
        public bool RemoveButton(string name)
        {
            return buttonNames.Remove(name) && callbacks.Remove(ResolveKey(name));
        }

        /// <summary>
        /// 返回当前初始菜单按钮
        /// </summary>
        /// <returns>按钮键名字符串</returns>
        public IEnumerable<string> GetCurrentButtonList()
        {
            return buttonNames;
        }

        /// <summary>
        /// 按当前注册顺序构建实际写入游戏按钮数组的键列表
        /// </summary>
        internal string[] BuildButtonKeys()
        {
            return buttonNames.Select(ResolveKey).ToArray();
        }

        // ================== 顶部按钮换行布局 ==================
        // 原版 SceneTitleTemp.initButtons 把顶部按钮硬编码成固定 4 个、单行铺满整个容器宽度；
        // Patch_SceneTitleTemp_initButtons 的 transpiler 把列数/按钮宽度分母改成读这里算出来的
        // 值，超过每行上限后自动换到下一行，而不是无限压窄同一行的按钮。

        /// <summary>顶部按钮单行最多显示的数量，超过后自动换行。</summary>
        internal const int MaxButtonsPerRow = 6;

        // 原版容器高度 54px = 按钮高度 30px（DsnDataRadio.h）+ 上下边距各 12px
        // （BxTop.margin_in_tb）；DsnDataRadio.margin_h = 0，行间无额外间距，故每多一行只需
        // 再加一个按钮高度。
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

        /// <summary>
        /// 顶部按钮容器的纵向定位（原版固定 134px）。Create() 的 pixel_y 是容器中心点：换行后
        /// 容器变高，若中心点不变会向下多占一截、越过下方留给立绘/黑边的空间，这里把中心上移
        /// "增高量的一半"，让容器保持底边位置不变、只向上变高。
        /// </summary>
        internal static float TopRowY(int totalCount)
        {
            return 134f + (ButtonRows(totalCount) - 1) * (TopRowHeightStep / 2f);
        }

        /// <summary>
        /// 修正顶部按钮末行数量不足整行时的位置。原版网格布局（XX.Designer.reboundCarrForBtnMulti
        /// 与 XX.ObjCarrierCon.calcBase，dnSpy 反编译确认）按列索引 0..clms-1 从左至右摆放，
        /// 末行不足 clms 个按钮时只会贴左对齐、右侧留空，不会整体居中。这里创建/重建完成后，
        /// 直接复用 calcBase 同一套公式（conbase_x、bounds_w 是 ObjCarrierCon 上的公开字段，
        /// 经 Krafs.Publicizer 编译期直连）重新摆放末行按钮的横坐标，把列索引整体右移
        /// (clms-rem)/2 实现居中；不改整行按钮的位置，也不触碰引擎自身按需下标计算的选中/
        /// 导航逻辑。由 Patch_SceneTitleTemp_initButtons（首次创建）和
        /// Patch_SceneTitleTemp_fineTexts（语言切换触发的 RemakeT 会用原版公式重新摆一遍，
        /// 需要再修正一次）共同调用；两处都调用、多次调用都是幂等的。
        /// </summary>
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

        /// <summary>
        /// 根据按钮实际键取出注册的回调
        /// </summary>
        internal bool TryGetCallback(string key, out FnBtnBindings callback)
        {
            return callbacks.TryGetValue(key, out callback);
        }

        // ================== 标题状态机集成 ==================
        // 每个"点击后会打开一个窗口"的按钮都会分配一个专属的 SceneTitleTemp.state
        // 哨兵值；点击时切到该值，窗口关闭时切回 TOP，从而借用游戏自身的状态机
        // 阻止标题菜单在窗口打开期间继续响应点击。
        //
        // 状态切换必须走 changeState(STATE) 方法本身，而不是直接写 state 字段：
        // 顶部按钮行的隐藏/显示是 changeState 内部"离开旧状态时的收尾动作"的副作用，
        // 不是每帧被动读取 state 就会自动发生的事情——直接写字段只改了数值，
        // 不会触发这个收尾动作，按钮行自然不会消失（已通过实测确认）。
        //
        // SceneTitleTemp.STATE 是私有嵌套枚举，changeState 是私有方法；Polaris
        // 通过 Krafs.Publicizer 编译期直连游戏内部成员，不需要反射。哨兵值（负数，
        // 不与任何已声明的 STATE 成员重合）直接用 C# 的枚举 cast 构造——C# 允许把
        // 任意 int 转换成枚举类型，不做运行时校验。
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

        /// <summary>
        /// 为一个"点击后会打开窗口"的按钮分配专属状态值；重复调用同一按钮是安全的（幂等）。
        /// </summary>
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

        /// <summary>
        /// 切换到指定按钮的专属状态，并记录为当前打开的按钮；应在按钮回调即将打开窗口时调用。
        /// </summary>
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

        /// <summary>
        /// 走原版"退出游戏"流程：把标题状态机切到 <c>STATE.QUIT</c>，剩下的淡出、
        /// <c>DigestMgvSave()</c> 落盘、30 帧后调用 <c>IN.quitGame()</c> 都由游戏自己完成，
        /// 与玩家点标题菜单"退出"按钮走的是同一条路径（不另起进程，也不绕过存档收尾）。
        /// <para>
        /// 必须先清空 <see cref="CurrentOpenButton"/>：Patch_SceneTitleTemp_runIRD 每帧都在
        /// 检查"当前打开的窗口是否已经自行关闭"，只要该字段还有值，窗口一关它下一帧就会调用
        /// <see cref="ReturnToTop"/> 把状态机拨回 TOP——刚切进去的 QUIT 会被当场覆盖掉，游戏
        /// 也就退不掉了。
        /// </para>
        /// 标题场景尚未初始化（<see cref="Current"/> 为空）或 changeState 抛异常时，
        /// 退化为直接调用 <c>IN.quitGame()</c>，保证"点了确定就一定会退出"。
        /// </summary>
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
            // changeState(TOP) 自己就会做 DsBlack.hide()+SetActive(false) 和把
            // TxOnePoint 刷新回默认提示；这里不能再调用 ApplyCommandButton(false,false,..)
            // 去"隐藏"确定/取消按钮条——那反而会通过 remakeSumitCancelButton 把刚隐藏好
            // 的 DsBlack 重新显示出来（原因见下面 DsBlack 相关的大段注释）。
            TrySetState(TopStateValue);
        }

        /// <summary>
        /// 为指定按钮注册一个"窗口是否仍处于打开状态"的探测函数；未注册时视为窗口始终打开
        /// （即不会自动归位，只能靠手动 <see cref="ReturnToTop"/> 或 ESC）。
        /// </summary>
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

        /// <summary>
        /// 判断玩家本帧是否触发了"取消"输入。标题界面操作提示行实测显示的取消键是 X
        /// （而不是 ESC），但没找到游戏自带的、不受按键重绑定影响的取消输入查询接口，
        /// 这里先把 ESC 和 X 都当作取消键处理；抽出为独立方法方便以后替换成真正的输入
        /// 抽象层，或者按需追加更多按键。
        /// </summary>
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
                Plugin.Logger.LogWarning($"[Polaris] SceneTitleTemp.changeState 调用失败，已忽略：{ex.Message}");
                return false;
            }
        }

        // ================== 确定/取消按钮条 + 操作提示行 ==================
        // 已用 ilspycmd 反编译 SceneTitleTemp（changeState/remakeSumitCancelButton 的
        // 完整方法体）与 XX.Designer（init/activate/deactivate/hide 的完整方法体）
        // 逐行确认，以下均为实测事实，不再是猜测：
        //
        // - 底部黑色按钮条的黑色背景本身是 private Designer DsBlack 字段（一个
        //   XX.Designer 实例，用于放"确定/取消"这两个按钮），不是 SubmitBtn/CancelBtn
        //   自带的。要让它显示：DsBlack.gameObject.SetActive(true) + DsBlack.activate()；
        //   要让它彻底隐藏：DsBlack.hide() + DsBlack.gameObject.SetActive(false)——两者
        //   changeState 里进入/离开 STATE.TOP 时就是这么配对调用的。
        // - private remakeSumitCancelButton(bool use_submit, bool use_cancel,
        //   int shiftx_px = 0) 方法体是：
        //     BtnContainer<aBtn> c = remakeSumitCancelButtonS(DsBlack, ..., use_submit, use_cancel);
        //     其中 remakeSumitCancelButtonS 内部按钮数组是
        //     (use_submit && use_cancel) ? [提交,取消] : (!use_submit ? [取消] : [提交])
        //   —— 传 (false, false) 并不会得到"什么都不显示"，因为 !use_submit 为 true，
        //   会落到"只显示取消"这个分支！而且 remakeSumitCancelButtonS 内部会调用
        //   Ds.init()，Designer.init() 一开始就有
        //   `if (!gameObject.activeSelf) gameObject.SetActive(true)`，也就是说只要调用
        //   remakeSumitCancelButton，不管传什么，都会把已经隐藏的 DsBlack 重新显示出来。
        //   这就是"返回主菜单后按钮/黑底还留着"这个 bug 的真正原因：旧代码在
        //   ReturnToTop 里调用了 changeState(TOP)（会正确隐藏 DsBlack）之后，又调用了
        //   一次 remakeSumitCancelButton(false,false)，把刚隐藏好的东西又弄出来了。
        //   正确做法：真正要隐藏时只调用 DsBlack.hide()+SetActive(false)，绝不能在
        //   "两侧都不显示"的场景下调用 remakeSumitCancelButton。
        // - SubmitBtn/CancelBtn 两个 aBtn 字段是 remakeSumitCancelButton 调用后从
        //   BtnContainer 里取出来缓存的引用，用于之后单独改文案/点击回调。
        // - 下方操作提示行不是由 UiSVD/Designer 承载的——UiSVD.fineSubmitionNavi 只是
        //   焦点导航逻辑，跟这行文字无关。真正的文本来自 SceneTitleTemp 自己的
        //   private TextRenderer TxOnePoint 字段，changeState 里离开/进入状态时会自动
        //   刷新成 TX.Get("KeyHelp_title_" + state.ToString().ToLower())，但仅在
        //   state >= STATE.TOP 时才刷新——我们的自定义哨兵状态是负数、不满足这个条件，
        //   所以这里需要自己写。TextRenderer.text_content 是公开属性，拿到实例后可直接赋值。
        //
        // 全部都是 Assembly-CSharp.dll 里的非公开成员，通过 Krafs.Publicizer 编译期直连；
        // Current 为空（场景尚未初始化）时整体安静跳过（只影响这一层"锦上添花"的联动，
        // 不影响 CurrentOpenButton 那条点击穿透的核心修复）。

        // DsBlack 刚创建时是给一个完全不相关的"first_ask"提示用的（bgcol 是全透明的，
        // 尺寸也不对）；真正的不透明黑底样式（bgcol/WH/margin_in_tb，外加
        // stencil_ref = 70 作为"已经初始化过"的标记）是 private initDsBlackAfter() 这个
        // 方法给的，vanilla 里只有进入 STATE.TOP 且 DsBlack.stencil_ref != 70 时才会调用
        // 一次。如果玩家在游戏本身第一次真正调用 changeState(TOP) 之前就点开了我们的自定义
        // 按钮，DsBlack 还是那个透明的初始样子，看起来就是"按钮有，黑底没有"。这里在显示
        // 按钮条之前主动补一次同样的判断+调用，不依赖玩家有没有先走过一遍原版流程。
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

        /// <summary>
        /// 切换指定按钮已配置的确定/取消按钮条某一侧的显隐，不影响已登记的文案与回调；
        /// 若目标按钮当前正处于打开状态会立即生效。未通过 <see cref="SetCommandButton"/>
        /// 配置过的一侧调用本方法无效果。
        /// </summary>
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

            // remakeSumitCancelButton(false, false) 不等于"都不显示"：它内部的三元表达式
            // 在 use_submit 为 false 时一律落到"只显示取消"分支，而且会通过 Designer.init()
            // 把已经隐藏的 DsBlack 重新 SetActive(true)。两侧都不需要显示时，必须绕开这个
            // 方法，直接把 DsBlack 隐藏掉。
            if (!submit.Visible && !cancel.Visible)
            {
                HideCommandBar();
                return;
            }

            try
            {
                // 顺序不能换：initDsBlackAfter 会先对 DsBlack 做一次 Clear+init 把样式
                // （WH/bgcol/margin_in_tb）定下来，remakeSumitCancelButton 再在这之上
                // 做它自己的 Clear+init+addButtonMultiT 把按钮加进去；反过来的话按钮会
                // 被 initDsBlackAfter 自己的 Clear() 清掉。
                EnsureDsBlackStyled();
                Current.remakeSumitCancelButton(submit.Visible, cancel.Visible);
                ShowCommandBar();
                ApplyButtonSlot(Current.SubmitBtn, submit);
                ApplyButtonSlot(Current.CancelBtn, cancel);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Polaris] remakeSumitCancelButton 调用失败，已忽略：{ex.Message}");
            }
        }

        /// <summary>
        /// 对应 changeState 里 `if (DsBlack.stencil_ref != 70) initDsBlackAfter();` 那一段——
        /// 确保 DsBlack 已经有过一次正确的不透明黑底样式，不依赖玩家是否已经先走过一遍
        /// 原版会触发 changeState(TOP) 的流程。
        /// </summary>
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
                Plugin.Logger.LogWarning($"[Polaris] SceneTitleTemp.initDsBlackAfter 调用失败，已忽略：{ex.Message}");
            }
        }

        // vanilla 的淡入动画是 runIRD 里逐帧算的，且只在
        // `state == STATE.SVD_SELECT || state == STATE.CONFIG` 时才会给 DsBlack.alpha
        // 从 0 淡到 1（用一个专用计时器 t_desc，约 30 帧内线性淡入）；我们的自定义哨兵
        // 状态永远不满足这个判断，vanilla 那段逻辑不会替我们跑。这里不复用 t_desc（它的
        // 归零时机跟真实 STATE 绑死，不方便借用），而是自己起一个计时器，在
        // Patch_SceneTitleTemp_runIRD 里每帧推进，按时间（而不是帧数）线性淡入，效果与
        // 原版一致但不依赖游戏内部帧计数字段。
        const float CommandBarFadeSeconds = 0.3f;
        float commandBarFadeT = CommandBarFadeSeconds;

        /// <summary>
        /// 对应 changeState 进入 STATE.CONFIG/SVD_SELECT 时的
        /// DsBlack.gameObject.SetActive(true) + DsBlack.activate()；alpha 从 0 起，
        /// 交给 <see cref="AdvanceCommandBarFade"/> 逐帧淡入到 1。
        /// </summary>
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

        /// <summary>
        /// 每帧推进确定/取消按钮条的淡入动画；淡入已完成或当前没有打开的按钮窗口时
        /// 什么都不做。由 Patch_SceneTitleTemp_runIRD 每帧调用。
        /// </summary>
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
            // 这里刻意用类型模式而不是 `as` + `!= null`：Designer 是 Unity Component，
            // `!= null` 会走 UnityEngine.Object 的运算符重载，把"已销毁但引用非空"的
            // 对象也判成 null，与原先各处 `is not Designer ds` 的判定并不等价。
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
                // addClickFn 直接把这个委托交给游戏自己的按钮点击分发；点击时是游戏引擎代码
                // 在调用它，不会经过 Polaris 的任何调用点，所以异常隔离必须包在委托本身里，
                // 否则一个 Mod 的确定/取消回调写坏，会直接从游戏的按钮点击处理里抛出去。
                btn.addClickFn(b =>
                {
                    try
                    {
                        return callback(b);
                    }
                    catch (Exception ex)
                    {
                        // 责任人就是这个回调委托本身所在的程序集，不必走堆栈推断。
                        PolarisAPI.Errors.Report(ex, $"确定/取消按钮 \"{config.Label}\" 的回调", callback.Method?.DeclaringType?.Assembly);
                        Plugin.Logger.LogError($"[Polaris] 确定/取消按钮 \"{config.Label}\" 的回调抛出异常，已忽略。");
                        return true;
                    }
                });
            }
        }

        void ApplyOperationHint(string hintText)
        {
            // hintText 为空时保持原样不动：ReturnToTop 已经通过 changeState(TOP) 让游戏
            // 自己把 TxOnePoint 刷新回默认提示，这里没必要（也不应该）再覆盖一次。
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
                Plugin.Logger.LogWarning($"[Polaris] 操作提示行写入失败，已忽略：{ex.Message}");
            }
        }

        static void WarnCommandButtonOnce()
        {
            if (warnedCommandButton)
            {
                return;
            }
            warnedCommandButton = true;
            Plugin.Logger.LogWarning("[Polaris] 标题场景尚未初始化（Current 为空），确定/取消按钮条自定义暂不生效。");
        }

        static void WarnHintOnce()
        {
            if (warnedHint)
            {
                return;
            }
            warnedHint = true;
            Plugin.Logger.LogWarning("[Polaris] 标题场景尚未初始化（Current 为空），操作提示行自定义暂不生效。");
        }
    }
}
