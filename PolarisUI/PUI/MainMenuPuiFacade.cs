using System;
using System.Collections.Generic;
using XX;

namespace Polaris.PUI
{
    /// <summary>
    /// 把主菜单按钮 API（<see cref="PolarisAPI.MainMenu"/>）与 PUI 窗口/状态机绑定起来，
    /// 让业务代码直接「注册按钮 -&gt; 点击后打开 PUI」，并联动标题状态机的进入/退出。
    /// </summary>
    public sealed class MainMenuPuiFacade
    {
        /// <summary>按钮 key -&gt; ESC/X 时的关闭动作；PUI 状态机不登记，避免与其自身 ESC 处理重复。</summary>
        private readonly Dictionary<string, Action> buttonToClose = new Dictionary<string, Action>();

        private bool hooked;

        internal MainMenuPuiFacade() { }

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
        /// 在主菜单添加一个按钮，点击后显示指定名称的 PUI，或驱动指定名称的 PUI 状态机（图）；
        /// 先按 PUI 名解析，再按图名解析，两者都没有则抛出。
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

        /// <summary>在主菜单添加一个按钮，点击后显示指定的 PUI 运行时实例。</summary>
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
        /// 在主菜单添加一个按钮，点击后驱动指定的 PUI 状态机（图）：打开即 <see cref="PUISolution.Start"/>。
        /// 关闭/取消不会强制 <see cref="PUISolution.Stop"/> 整张图，而是走图内 Cancel 边，交由图自身决定
        /// 退一级还是退出；需要强制整图退出可自行传入 <paramref name="onCancel"/> 覆盖默认行为。
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

        /// <param name="onEscape">ESC/X 时的关闭动作；传 null 表示打开的东西自己处理 ESC/X，这里不重复登记。</param>
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

            // 声明窗口打开期间显示的确定/取消按钮和提示行；调用方可后续覆盖。
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

        // SetCommandButton 等与 PUI 无关的能力不在这里重复暴露，直接用 PolarisAPI.MainMenu.*。

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
