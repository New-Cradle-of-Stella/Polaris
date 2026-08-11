using System;
using System.Collections.Generic;
using nel;
using Polaris.Localization;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>
    /// 标题菜单"设置"按钮后面的"Polaris"按钮：打开一个不经过 PUI、直接用游戏原生
    /// <c>Designer</c>/<c>DsnData*</c> API 手搓的模组管理页。内容是
    /// <see cref="UserModToggleManager"/> 管理的 plugins 根目录 dll 启停列表，
    /// 每条是一个 <see cref="DsnDataButton"/> 按钮，点击切换启停、悬停在右侧弹出
    /// <see cref="PolarisModDetailPopup"/> 展示作者与简介。
    /// 面板本身比内容矮，靠 <c>use_scroll</c> 滚动查看。
    /// <para>
    /// 启停改动不当场落盘：页面打开期间只记在 <see cref="pending"/> 里，点底部"确定"时若有
    /// 改动就弹 <see cref="PolarisRestartPrompt"/> 确认，确认后才改名并关闭游戏（改名后的 dll
    /// 要下次启动 BepInEx 扫描插件目录时才会被发现，本次进程里怎么改都不会生效）。
    /// </para>
    /// <para>
    /// 底部两个按钮的分工是刻意的、也和游戏别处的确定/取消一致：<b>"取消"永远无条件</b>——
    /// 一按就丢掉本次全部改动并退回标题菜单，不再反问一句。会反问的只有"确定"，因为它真正
    /// 要做的事（改 dll 文件名 + 关掉游戏）既动磁盘又打断游戏，那一步才值得拦一道。
    /// 反过来做（取消时问"要保存吗"）会让玩家在最想干脆退出的时候还得多读一屏字。
    /// </para>
    /// </summary>
    internal static class PolarisManagementUI
    {
        const string ButtonName = "Polaris";

        /// <summary>页面自身的操作提示行。每次用时现拼——玩家随时可能在标题画面换语言。</summary>
        static string PageHint =>
            $"{KeyHint.Submit} {ModManagerStrings.Text(ModManagerStrings.CmdSubmit)}"
            + $"    {KeyHint.Cancel} {ModManagerStrings.Text(ModManagerStrings.CmdCancel)}";

        /// <summary>确认窗弹着时的操作提示行：此刻取消键的含义是"退回列表"。</summary>
        static string PromptHint =>
            $"{KeyHint.Cancel} {ModManagerStrings.Text(ModManagerStrings.CmdBack)}";

        const float WindowW = 500f;
        const float WindowH = 320f; // 视口高度，比内容矮，靠滚动查看

        /// <summary>
        /// 同族内每个 <c>Create</c> 之间的 z 间隔。默认值 0.008 恰好等于 Designer 底板自身的
        /// +0.008 偏移，也就是说后一个窗口的底板会和前一个窗口的正文落在同一个 z 上——平局时
        /// 谁在前完全看运气。拉开到 0.05，让"主面板 / 详情浮窗 / 重启确认窗"三层各自分明。
        /// </summary>
        const float DesignerSlipZ = 0.05f;

        static readonly Color32 TitleColor = new Color32(56, 56, 56, 255);

        static GameObject host;
        static UiBoxDesignerFamily family;
        static UiBoxDesigner designer;
        static bool isOpen;

        /// <summary>
        /// 本次打开页面期间缓存的启停改动，键是 <see cref="UserModRecord.DisplayName"/>，
        /// 值是玩家期望的启停状态。只记与磁盘现状不同的项——改回原样会被直接移除，
        /// 所以 <c>Count == 0</c> 就等于"没有任何待应用的改动"。
        /// </summary>
        static readonly Dictionary<string, bool> pending = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 上一次启停失败的原因，键是 <see cref="UserModRecord.DisplayName"/>。
        /// 不能直接读 <see cref="UserModRecord.Error"/>：<see cref="UserModToggleManager.Scan"/> 每次
        /// 重建列表都会 new 出一批全新记录，<c>Error</c> 恒为 null，写在旧实例上的失败原因会立刻丢掉。
        /// </summary>
        static readonly Dictionary<string, string> lastErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>必须在 Plugin.Start() 最开始调用，赶在其它模组注册按钮之前占住"设置"后面的位置。</summary>
        internal static void RegisterButton()
        {
            ModManagerStrings.Register();

            PolarisAPI.MainMenu.AllocateButtonState(ButtonName);
            PolarisAPI.MainMenu.SetWindowOpenChecker(ButtonName, () => isOpen);
            PolarisAPI.MainMenu.Escaped += key =>
            {
                if (key != MainMenuAPI.ResolveKey(ButtonName))
                {
                    return;
                }

                // 确认窗弹着的时候 ESC 只作用于确认窗本身（等同于点它自己的"取消"），
                // 不越过它去关页面。
                if (PolarisRestartPrompt.IsOpen)
                {
                    DismissRestartPrompt();
                    return;
                }

                // 取消键 == 底部的"取消"按钮：无条件放弃。
                CancelAll();
            };

            PolarisAPI.MainMenu.AddButton(ButtonName, _ =>
            {
                // 按钮文案与提示行在这里（而不是注册时）写入：它们是本地化文本，玩家在标题画面
                // 换过语言之后必须跟着变，而注册只发生在启动时那一次。放在 EnterButtonState
                // 之前是为了少一次重建——那时本按钮还不是"当前打开的按钮"，
                // SetCommandButton 内部的 RefreshIfCurrent 是空操作。
                ApplyCommandButtons();
                PolarisAPI.MainMenu.EnterButtonState(ButtonName);
                Open();
                return true;
            }, insertIndex: 3);
        }

        /// <summary>
        /// 按当前语言写入底部确定/取消两个按钮的文案与回调。
        /// <para>
        /// "确定"走 <see cref="RequestApply"/>（有改动时弹确认窗），"取消"走
        /// <see cref="CancelAll"/>（无条件放弃）。
        /// </para>
        /// </summary>
        static void ApplyCommandButtons()
        {
            PolarisAPI.MainMenu.SetCommandButton(
                ButtonName, submit: true, ModManagerStrings.Text(ModManagerStrings.CmdSubmit), _ =>
                {
                    RequestApply();
                    return true;
                });

            PolarisAPI.MainMenu.SetCommandButton(
                ButtonName, submit: false, ModManagerStrings.Text(ModManagerStrings.CmdCancel), _ =>
                {
                    CancelAll();
                    return true;
                });

            PolarisAPI.MainMenu.SetOperationHint(ButtonName, PageHint);
        }

        static void Open()
        {
            if (host == null)
            {
                host = new GameObject("Polaris.ModuleManager");
                UnityEngine.Object.DontDestroyOnLoad(host); // 加了 using System 后 Object 会和 System.Object 撞名
                // 往前提到"盖住标题画面常驻 UI"那一层，否则版本号文本会糊在面板上，
                // 取值与理由见 UiDepth（PUI 窗口用的是同一个常量）。
                IN.setZ(host.transform, UiDepth.Window);
                family = host.AddComponent<UiBoxDesignerFamily>();
                family.slip_z = DesignerSlipZ; // 必须赶在下面这些 Create 之前设，逐个 Create 时才用得上
                designer = family.Create(
                    "PolarisModuleManager", 0f, 0f, WindowW, WindowH,
                    -1, 30f, UiBoxDesignerFamily.MASKTYPE.BOX);
                designer.use_scroll = true;
                designer.getBox().frametype = UiBox.FRAMETYPE.MAIN;

                // 必须在主面板之后建，才会画在它上层；建完是隐藏的，第一次悬停模组行时才亮起。
                PolarisModDetailPopup.Ensure(family, designer);

                // 再往后建，拿到这一族最靠前的 z：确认窗要盖住主面板和详情浮窗两者。
                PolarisRestartPrompt.Ensure(family);
            }

            // 每次打开都从磁盘现状重新开始缓存，不继承上一次没应用（被放弃）的改动。
            pending.Clear();
            lastErrors.Clear();

            // 上次是在确认窗里关掉页面的话，按钮条还停在"隐藏"的配置上。这里不必再恢复一次：
            // 每次打开都会先走一遍 ApplyCommandButtons，SetCommandButton 本身就把两侧重新置为
            // 可见（见 CommandButtonConfig 的构造）。在 Close() 里恢复反而会在关闭前一帧把按钮条
            // 又亮出来，那正是当初把恢复挪到打开时做的原因。

            Rebuild();
            family.activate();
            isOpen = true;
        }

        /// <summary>
        /// 底部"确定"：有缓存改动就弹确认窗问一句（应用要改 dll 文件名并关掉游戏，值得拦一道），
        /// 什么都没改过就直接关页面。
        /// </summary>
        static void RequestApply()
        {
            if (pending.Count == 0)
            {
                Close();
                return;
            }

            ShowRestartPrompt();
        }

        /// <summary>
        /// 底部"取消"（以及取消键）：无条件放弃本次全部改动并关页面，不反问。磁盘上什么都没
        /// 动过——改动一直只记在 <see cref="pending"/> 里，丢掉即可。
        /// </summary>
        static void CancelAll()
        {
            pending.Clear();
            lastErrors.Clear();
            Close();
        }

        static void Close()
        {
            PolarisRestartPrompt.Hide();
            // 必须先清掉浮窗记住的当前项，否则下次 Open 的 Rebuild 会把它抢在主面板之前点亮。
            PolarisModDetailPopup.Reset();
            family?.deactivate();
            isOpen = false;
            // 不在这里手动调用 PolarisAPI.MainMenu.ReturnToTop()：Patch_SceneTitleTemp_runIRD
            // 每帧会检查 SetWindowOpenChecker 注册的探测函数，发现 isOpen 变 false 后自动归位，
            // 和 MainMenuPUI 的 PUIRuntime.Hide() 走的是同一套机制。
        }

        static void Rebuild()
        {
            designer.Clear();
            designer.init();

            // 只扫一次磁盘，列表与浮窗共用同一份快照，避免两边看到不一致的结果。
            List<UserModRecord> mods = UserModToggleManager.Scan();
            BuildContent(designer, mods);

            // 重建后按钮全是新实例，旧的悬停状态失效；主动按记住的键刷新一次浮窗内容。
            PolarisModDetailPopup.Refresh(mods, TargetEnabled, lastErrors);
        }

        // ================== 启停改动的缓存与应用 ==================

        /// <summary>玩家期望的启停状态：有缓存改动就用缓存值，否则就是磁盘现状。</summary>
        static bool TargetEnabled(UserModRecord record)
        {
            return pending.TryGetValue(record.DisplayName, out bool target) ? target : record.Enabled;
        }

        /// <summary>翻转一条记录的期望状态；翻回磁盘现状时把这条改动从缓存里撤销掉。</summary>
        static void Toggle(UserModRecord record)
        {
            bool target = !TargetEnabled(record);
            if (target == record.Enabled)
            {
                pending.Remove(record.DisplayName);
            }
            else
            {
                pending[record.DisplayName] = target;
            }

            // 上一轮应用失败的提示只对当时那次操作有意义，玩家重新改动后就该消失。
            lastErrors.Remove(record.DisplayName);
        }

        static void ShowRestartPrompt()
        {
            // 确认窗弹出期间收起主列表：同族窗口之间没有遮挡关系，列表留着的话鼠标照样点得到
            // 下面的启停按钮，确认窗就不是模态的了。
            PolarisModDetailPopup.Reset();
            designer.deactivate();
            SetPageChromeVisible(false);

            PolarisRestartPrompt.Show(
                string.Format(ModManagerStrings.Text(ModManagerStrings.PromptMessage), pending.Count),
                ConfirmRestartPrompt,
                DismissRestartPrompt);
        }

        /// <summary>确认窗"确定"：把缓存的改动落到磁盘，成功就关页面并退出游戏。</summary>
        static void ConfirmRestartPrompt()
        {
            if (!ApplyPending())
            {
                // 有改名失败的（文件被占用、权限不足……）：不能就这么退出游戏把错误吞掉，
                // 退回列表把失败原因摆出来，让玩家看得见、能重试。
                BackToList();
                return;
            }

            Close();
            PolarisAPI.MainMenu.QuitGame();
        }

        /// <summary>
        /// 确认窗"取消"（以及它弹着时的取消键）：只收起确认窗、退回列表，缓存的改动原样留着。
        /// <para>
        /// 不在这里放弃改动：放弃是底部"取消"按钮的职责，而且是无条件的。玩家点"确定"看到
        /// "要关掉游戏"之后改主意，多半是想回去再改两笔，不是想把刚才勾的全丢掉。
        /// </para>
        /// </summary>
        static void DismissRestartPrompt()
        {
            BackToList();
        }

        /// <summary>收起确认窗、把主列表和底部按钮条放回来。</summary>
        static void BackToList()
        {
            PolarisRestartPrompt.Hide();
            SetPageChromeVisible(true);
            // 先重建再 activate，和 Open() 的顺序保持一致：Clear() 会触发一次尺寸归零动画，
            // 放在面板已经亮着的时候做会看到面板先塌一下再撑开。
            Rebuild();
            designer.activate();
        }

        /// <summary>
        /// 切换页面自身那套"外壳"（底部确定/取消按钮条与操作提示行）的显隐。确认窗弹出期间要
        /// 收起来：页面这条按钮条上也是一对确定/取消，跟确认窗里的那对并排出现，玩家根本分不出
        /// 该点哪个。
        /// </summary>
        static void SetPageChromeVisible(bool visible)
        {
            PolarisAPI.MainMenu.SetCommandButtonVisible(ButtonName, submit: true, visible);
            PolarisAPI.MainMenu.SetCommandButtonVisible(ButtonName, submit: false, visible);
            PolarisAPI.MainMenu.SetOperationHint(ButtonName, visible ? PageHint : PromptHint);
        }

        /// <summary>
        /// 把缓存的改动逐条落到磁盘，之后 <see cref="pending"/> 里只剩改名失败的那些
        /// （成功的、以及这次已经扫不到的都会出局）；返回是否已全部应用完毕。
        /// </summary>
        static bool ApplyPending()
        {
            // 重新扫一次而不是复用界面上那份快照：页面开着的这段时间里，玩家完全可能在
            // 游戏外面手动删掉或改名了某个 dll。
            var failed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var scanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (UserModRecord record in UserModToggleManager.Scan())
            {
                if (!pending.TryGetValue(record.DisplayName, out bool target))
                {
                    continue;
                }

                scanned.Add(record.DisplayName);

                if (UserModToggleManager.SetEnabled(record, target))
                {
                    lastErrors.Remove(record.DisplayName);
                }
                else
                {
                    failed[record.DisplayName] = target;
                    lastErrors[record.DisplayName] = record.Error;
                }
            }

            // 扫不到的（文件已被外部删掉或改名）连改都没得改，留在缓存里只会让确认窗反复弹；
            // 记一笔日志后一并丢掉。
            foreach (string displayName in pending.Keys)
            {
                if (!scanned.Contains(displayName))
                {
                    lastErrors.Remove(displayName);
                    Plugin.Logger.LogWarning($"[Polaris] Mod \"{displayName}\" is no longer in the plugins directory; skipping its enable/disable change.");
                }
            }

            pending.Clear();
            foreach (KeyValuePair<string, bool> entry in failed)
            {
                pending[entry.Key] = entry.Value;
            }

            return pending.Count == 0;
        }

        static void BuildContent(UiBoxDesigner box, List<UserModRecord> mods)
        {
            Logo(box);
            Title(box, ModManagerStrings.Text(ModManagerStrings.Title));
            HrGap(box, 6f, 6f);

            Section(box, ModManagerStrings.Text(ModManagerStrings.SectionMods));
            if (mods.Count == 0)
            {
                Muted(box, ModManagerStrings.Text(ModManagerStrings.Empty));
            }
            else
            {
                foreach (UserModRecord record in mods)
                {
                    bool target = TargetEnabled(record);
                    string prefix = target ? "[✓] " : "[ ] ";
                    string dirtyMark = target != record.Enabled ? "  *" : "";
                    lastErrors.TryGetValue(record.DisplayName, out string error);
                    box.addButtonT<aBtnNel>(new DsnDataButton
                    {
                        name = record.DisplayName,
                        title = prefix + Headline(record.Info, record.DisplayName) + dirtyMark
                                + (error != null ? ModManagerStrings.Text(ModManagerStrings.RowFailed) : ""),
                        w = box.use_w,
                        h = 26f,
                        fnClick = _ =>
                        {
                            Toggle(record);
                            Rebuild();
                            return true;
                        },
                        fnHover = button =>
                        {
                            lastErrors.TryGetValue(record.DisplayName, out string hoverError);
                            PolarisModDetailPopup.Show(button, record, TargetEnabled(record), hoverError);
                            return true;
                        }
                    });
                }
            }

            if (pending.Count > 0)
            {
                Muted(box, string.Format(
                    ModManagerStrings.Text(ModManagerStrings.PendingNote), pending.Count));
            }

            HrGap(box, 6f, 6f);

            box.alignx = ALIGN.CENTER;
            box.addButtonT<aBtnNel>(new DsnDataButton
            {
                name = "refresh",
                title = ModManagerStrings.Text(ModManagerStrings.Refresh),
                w = 160f,
                h = 28f,
                fnClick = _ =>
                {
                    Rebuild();
                    return true;
                },
                // 也挂说明，否则鼠标从最后一个模组滑到这里时浮窗会僵在上一条上，看着像卡住了。
                fnHover = button =>
                {
                    PolarisModDetailPopup.ShowText(
                        button, ModManagerStrings.Text(ModManagerStrings.RefreshDesc));
                    return true;
                }
            });
            box.Br();
            box.alignx = ALIGN.LEFT;
        }

        /// <summary>
        /// 页面最上方居中的 Polaris logo（<see cref="PolarisBrandImages.Logo"/>，硬编码在
        /// <c>plugins/Polaris/</c> 下的 png）。图片取不到就整行跳过，页面其余部分照常显示。
        /// <para>
        /// 图块铺满整行、图片画在块的正中（<c>FillImageBlock</c> 就是把图画在块中心），所以看到的
        /// 是"整行居中的一张方图"。<c>UvRect</c>/<c>scale</c> 的换算交给
        /// <see cref="PUI.PuiImage.Assign"/>——那两个字段的语义反直觉（前者是纹理像素矩形，
        /// 绘制尺寸只由它的尺寸 × <c>scale</c> 决定，跟 <c>swidth</c>/<c>sheight</c> 无关），
        /// 和 PUI 的 Image 元素共用同一份实现，不在这里再手算一遍。
        /// </para>
        /// </summary>
        static float Logo(UiBoxDesigner box)
        {
            MImage image = PolarisBrandImages.Logo;
            if (image == null)
            {
                return 0f;
            }

            const float sheight = 72f;
            var data = new DsnDataImg
            {
                name = "logo",
                swidth = box.use_w,
                sheight = sheight,
            };
            PUI.PuiImage.Assign(data, image, 0f, 0f, 1f, 1f, sheight, sheight, 1f);

            box.addImg(data);
            box.Br();
            return sheight;
        }

        // 标题：整行居中、字号加大。返回占用高度供滚动范围累加。
        static float Title(UiBoxDesigner box, string text)
        {
            const float sheight = 30f;
            box.addP(new DsnDataP(text, false)
            {
                swidth = box.use_w,
                sheight = sheight,
                size = 20f,
                alignx = ALIGN.CENTER,
                TxCol = TitleColor,
            });
            box.Br();
            return sheight;
        }

        // 分区小标题：整行底色条 + 不透明文字，保证在任意面板背景上都看得清。
        static float Section(UiBoxDesigner box, string text)
        {
            const float sheight = 24f;
            box.addP(new DsnDataP(text, false)
            {
                swidth = box.use_w,
                sheight = sheight,
                size = 16f,
                radius = 4f,
                TxCol = TitleColor,
                text_margin_x = 12f,
            });
            box.Br();
            return sheight + 4f;
        }

        // 标题行文字：标了 PolarisModInfo 的用其展示名（带版本），否则退回文件名。
        static string Headline(PolarisModInfo info, string fallback)
        {
            if (info == null || !info.HasModInfo)
            {
                return fallback;
            }

            return info.Version == null ? info.DisplayName : $"{info.DisplayName}  v{info.Version}";
        }

        // 只读说明文字：缩进 + 淡底色条，和分区标题、可交互勾选行区分层级。
        static float Muted(UiBoxDesigner box, string text)
        {
            const float sheight = 20f;
            box.addP(new DsnDataP(text, false)
            {
                swidth = box.use_w,
                sheight = sheight,
                size = 13f,
                radius = 3f,
                TxCol = TitleColor,
                text_margin_x = 18f,
            });
            box.Br();
            return sheight + 2f;
        }

        // 纯留白间隔（可选带一条分隔线），返回占用高度供滚动范围累加。
        static float HrGap(UiBoxDesigner box, float marginT, float marginB, float widthRatio = 0.94f)
        {
            box.Hr(widthRatio, marginT, marginB);
            return marginT + marginB + 1f;
        }
    }
}
