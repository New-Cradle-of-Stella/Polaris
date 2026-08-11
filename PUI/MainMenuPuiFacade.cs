using System;
using System.Collections.Generic;
using XX;

namespace Polaris.PUI
{
    /// <summary>
    /// 二级抽象：把 Polaris 提供的主菜单按钮 API（<see cref="PolarisAPI.MainMenu"/>，
    /// 直接修改游戏底层、随游戏版本演进的兼容层）与 PUI 窗口（<see cref="PUIRuntime"/>）或
    /// PUI 状态机（<see cref="PUISolution"/>）绑定起来，让业务代码可以直接「注册一个主菜单
    /// 按钮 -&gt; 点击后打开一个 PUI / 一张图」，而不必自己处理按钮回调与显示/隐藏的接线。
    ///
    /// 同时负责把每个按钮接入 <see cref="PolarisAPI.MainMenu"/> 的标题状态机联动：
    /// 点击时切到该按钮专属的状态（阻止标题菜单在窗口打开期间继续响应点击），
    /// 窗口被自身关闭按钮关掉或玩家按 ESC 时自动切回 TOP。
    /// </summary>
    public sealed class MainMenuPuiFacade
    {
        /// <summary>按钮 key -&gt; ESC/X 时的关闭动作。只有单个 PUI（<see cref="PUIRuntime"/>）会在这里登记
        /// 「直接 Hide」；PUI 状态机（<see cref="PUISolution"/>）不登记——它自己的 <see cref="PUISolution.CancelTriggerKey"/>
        /// 边已经由 <see cref="PUISolutionPump"/> 每帧驱动，这里再额外处理一遍会导致同一次按键被处理两次
        /// （见 <see cref="AddButton(string, PUISolution, int, string, FnBtnBindings, string, FnBtnBindings, string)"/>）。</summary>
        internal MainMenuPuiFacade() { }

        private readonly Dictionary<string, Action> buttonToClose = new Dictionary<string, Action>();
        private bool hooked;

        private void EnsureHooked()
        {
            if (hooked)
            {
                return;
            }

            hooked = true;
            PolarisAPI.MainMenu.Escaped += key =>
            {
                if (buttonToClose.TryGetValue(key, out Action close))
                {
                    close();
                }
            };
        }

        /// <summary>
        /// 在主菜单添加一个按钮，点击后显示指定名称的 PUI，或驱动指定名称的 PUI 状态机（图）。
        /// 先按普通 PUI 名解析（<see cref="PuiRegistry.TryGet"/>）；解析不到再按 .puisln 图名解析
        /// （<see cref="PuiRegistry.TryGetGraph"/>），命中图名时使用该图 <see cref="PuiRegistry.Init"/>
        /// 时自动创建的默认共享 <see cref="PUISolution"/>（<see cref="PuiRegistry.GetDefaultSolution"/>）。
        /// 两者都没有则抛出。
        /// </summary>
        /// <param name="name">按钮名称，规则与 <see cref="MainMenuAPI.AddButton"/> 一致</param>
        /// <param name="puiOrGraphName">目标 PUI 的 <see cref="IPUI.Name"/>，或目标图（.puisln）的名字；需已完成注册</param>
        /// <param name="insertIndex">添加位置，-1为在最后追加（默认为-1）</param>
        /// <param name="submitLabel">底部确定按钮文案；为 null（默认）则不显示确定按钮</param>
        /// <param name="onSubmit">确定按钮点击回调，仅在 <paramref name="submitLabel"/> 非 null 时使用</param>
        /// <param name="cancelLabel">底部取消按钮文案；默认"キャンセル"，传 null 则不显示取消按钮</param>
        /// <param name="onCancel">取消按钮点击回调；为 null（默认）则关闭当前 PUI 窗口 / 状态机</param>
        /// <param name="hint">底部操作提示行文本；为 null（默认）则按是否配置了确定/取消给出对应默认提示</param>
        public void AddButton(
            string name,
            string puiOrGraphName,
            int insertIndex = -1,
            string submitLabel = null,
            FnBtnBindings onSubmit = null,
            string cancelLabel = "キャンセル",
            FnBtnBindings onCancel = null,
            string hint = null)
        {
            if (string.IsNullOrEmpty(puiOrGraphName))
            {
                throw new ArgumentException("PUI / state machine name cannot be empty", nameof(puiOrGraphName));
            }

            if (PolarisUIAPI.Pui.TryGet(puiOrGraphName, out PUIRuntime pui))
            {
                AddButton(name, pui, insertIndex, submitLabel, onSubmit, cancelLabel, onCancel, hint);
                return;
            }

            if (PolarisUIAPI.Pui.TryGetGraph(puiOrGraphName, out _))
            {
                AddButton(name, PolarisUIAPI.Pui.GetDefaultSolution(puiOrGraphName), insertIndex, submitLabel, onSubmit, cancelLabel, onCancel, hint);
                return;
            }

            throw new ArgumentException($"\"{puiOrGraphName}\" is neither a registered PUI nor a registered PUI state machine (graph)", nameof(puiOrGraphName));
        }

        /// <summary>
        /// 在主菜单添加一个按钮，点击后显示指定的 PUI 运行时实例——可以是按名字共享的实例
        /// （<see cref="PuiRegistry.Get"/>），也可以是某个 <see cref="PUISolution"/> 节点的实例
        /// （<see cref="PUISolution.TryGetNode"/>），或任何 <see cref="PUIRuntime.Create"/> 直接
        /// 创建出来的独立实例。
        /// </summary>
        /// <param name="name">按钮名称，规则与 <see cref="MainMenuAPI.AddButton"/> 一致</param>
        /// <param name="pui">要展示的 PUI 运行时实例</param>
        /// <param name="insertIndex">添加位置，-1为在最后追加（默认为-1）</param>
        /// <param name="submitLabel">底部确定按钮文案；为 null（默认）则不显示确定按钮</param>
        /// <param name="onSubmit">确定按钮点击回调，仅在 <paramref name="submitLabel"/> 非 null 时使用</param>
        /// <param name="cancelLabel">底部取消按钮文案；默认"キャンセル"，传 null 则不显示取消按钮</param>
        /// <param name="onCancel">取消按钮点击回调；为 null（默认）则关闭当前 PUI 窗口</param>
        /// <param name="hint">底部操作提示行文本；为 null（默认）则按是否配置了确定/取消给出对应默认提示</param>
        public void AddButton(
            string name,
            PUIRuntime pui,
            int insertIndex = -1,
            string submitLabel = null,
            FnBtnBindings onSubmit = null,
            string cancelLabel = "キャンセル",
            FnBtnBindings onCancel = null,
            string hint = null)
        {
            if (pui == null)
            {
                throw new ArgumentNullException(nameof(pui));
            }

            AddButtonCore(
                name,
                isShown: () => pui.State == PUIState.Shown,
                show: () => pui.Show(),
                onEscape: () => pui.Hide(),
                defaultCancelAction: () => pui.Hide(),
                insertIndex, submitLabel, onSubmit, cancelLabel, onCancel, hint);
        }

        /// <summary>
        /// 在主菜单添加一个按钮，点击后驱动指定的 PUI 状态机（图）：打开即
        /// <see cref="PUISolution.Start"/>（进入入口节点）。「是否已打开」以
        /// <see cref="PUISolution.Current"/> 是否处于 <see cref="PUIState.Shown"/> 判定。
        ///
        /// 关闭/取消**不会**无条件 <see cref="PUISolution.Stop"/> 整张图：ESC/X 完全交给
        /// <see cref="PUISolutionPump"/> 每帧按当前节点自己 .pui 里配置的 Cancel 边处理——可能是
        /// 退到上一级节点，也可能是真正退出（<c>ExitEdge</c>），由图的作者决定，本方法不重复处理，
        /// 避免同一次按键被外层和图内部各触发一次、把"退一级"错误升级成"整图直接关掉"。
        /// 底部"取消"按钮（玩家显式点击，不受这个"同一帧按键读两次"的问题影响）默认走的是同一套
        /// 图内 Cancel 逻辑（<see cref="PUISolution.Fire(string, string)"/> + <see cref="PUISolution.CancelTriggerKey"/>），
        /// 而不是强制 Stop，与 ESC/X 的行为保持一致；如果图当前节点没有配置 Cancel 边，则安全地什么都不做。
        /// 若确实需要一个"无论在哪个节点，点了就整图退出"的强制关闭按钮，可自行传入
        /// <paramref name="onCancel"/>（比如 <c>(_) => { solution.Stop(); return true; }</c>）覆盖默认行为。
        /// </summary>
        /// <param name="name">按钮名称，规则与 <see cref="MainMenuAPI.AddButton"/> 一致</param>
        /// <param name="solution">要驱动的 PUI 状态机实例</param>
        /// <param name="insertIndex">添加位置，-1为在最后追加（默认为-1）</param>
        /// <param name="submitLabel">底部确定按钮文案；为 null（默认）则不显示确定按钮</param>
        /// <param name="onSubmit">确定按钮点击回调，仅在 <paramref name="submitLabel"/> 非 null 时使用</param>
        /// <param name="cancelLabel">底部取消按钮文案；默认"キャンセル"，传 null 则不显示取消按钮</param>
        /// <param name="onCancel">取消按钮点击回调；为 null（默认）则在当前节点触发一次 <see cref="PUISolution.CancelTriggerKey"/></param>
        /// <param name="hint">底部操作提示行文本；为 null（默认）则按是否配置了确定/取消给出对应默认提示</param>
        public void AddButton(
            string name,
            PUISolution solution,
            int insertIndex = -1,
            string submitLabel = null,
            FnBtnBindings onSubmit = null,
            string cancelLabel = "キャンセル",
            FnBtnBindings onCancel = null,
            string hint = null)
        {
            if (solution == null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            AddButtonCore(
                name,
                isShown: () => solution.Current != null && solution.Current.State == PUIState.Shown,
                show: () => solution.Start(),
                onEscape: null,
                defaultCancelAction: () => solution.Fire(solution.CurrentNodeKey, PUISolution.CancelTriggerKey),
                insertIndex, submitLabel, onSubmit, cancelLabel, onCancel, hint);
        }

        /// <param name="onEscape">
        /// ESC/X 时要执行的关闭动作；传 null 表示这个按钮打开的东西自己会处理 ESC/X
        /// （目前即 <see cref="PUISolution"/>，由 <see cref="PUISolutionPump"/> 负责），
        /// 这里不重复登记，避免同一次按键被处理两次。
        /// </param>
        private void AddButtonCore(
            string name,
            Func<bool> isShown,
            Action show,
            Action onEscape,
            Action defaultCancelAction,
            int insertIndex,
            string submitLabel,
            FnBtnBindings onSubmit,
            string cancelLabel,
            FnBtnBindings onCancel,
            string hint)
        {
            EnsureHooked();

            string key = MainMenuAPI.ResolveKey(name);
            if (onEscape != null)
            {
                buttonToClose[key] = onEscape;
            }
            else
            {
                buttonToClose.Remove(key);
            }

            PolarisAPI.MainMenu.AllocateButtonState(name);
            PolarisAPI.MainMenu.SetWindowOpenChecker(name, isShown);

            // 内联声明这个窗口打开期间要显示哪些确定/取消按钮和提示行；调用方注册后
            // 仍可用 SetCommandButton/SetOperationHint/SetCommandButtonVisible 覆盖或动态调整。
            if (submitLabel != null)
            {
                PolarisAPI.MainMenu.SetCommandButton(name, submit: true, submitLabel, onSubmit);
            }
            if (cancelLabel != null)
            {
                PolarisAPI.MainMenu.SetCommandButton(name, submit: false, cancelLabel, onCancel ?? (_ =>
                {
                    defaultCancelAction();
                    return true;
                }));
            }
            PolarisAPI.MainMenu.SetOperationHint(name, hint ?? DefaultHint(submitLabel, cancelLabel));

            PolarisAPI.MainMenu.AddButton(name, _ =>
            {
                PolarisAPI.MainMenu.EnterButtonState(name);
                show();
                return true;
            }, insertIndex);
        }

        private static string DefaultHint(string submitLabel, string cancelLabel)
        {
            string submitHint = submitLabel != null ? $"{KeyHint.Submit} {submitLabel}   " : "";
            string cancelHint = cancelLabel != null ? $"{KeyHint.Cancel} {cancelLabel}" : "";
            return submitHint + cancelHint;
        }

        // SetCommandButton / SetCommandButtonVisible / SetOperationHint / RemoveButton
        // 不在这里重复暴露：它们和 PUI 没有关系，是 Polaris 主菜单能力的原样转发。
        // 直接用 PolarisAPI.MainMenu.*。见 CLAUDE.md 的门面契约第 3 条。

        /// <summary>
        /// 在主菜单添加一个按钮，点击后显示指定的 PUI 实例；若该实例尚未按名字注册会自动注册。
        /// </summary>
        /// <param name="name">按钮名称，规则与 <see cref="MainMenuAPI.AddButton"/> 一致</param>
        /// <param name="pui">要展示的 PUI 实例</param>
        /// <param name="insertIndex">添加位置，-1为在最后追加（默认为-1）</param>
        /// <returns>该 PUI 对应的运行时实例</returns>
        public PUIRuntime AddButton(string name, IPUI pui, int insertIndex = -1)
        {
            if (pui == null)
            {
                throw new ArgumentNullException(nameof(pui));
            }

            PUIRuntime runtime = PolarisUIAPI.Pui.IsRegistered(pui.Name)
                ? PolarisUIAPI.Pui.Get(pui.Name)
                : PolarisUIAPI.Pui.Register(pui);

            AddButton(name, runtime, insertIndex);
            return runtime;
        }
    }
}
