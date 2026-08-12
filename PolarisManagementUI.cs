using System;
using System.Collections.Generic;
using nel;
using Polaris.Localization;
using UnityEngine;
using XX;

namespace Polaris
{
    /// <summary>标题菜单"设置"按钮后的"Polaris"模组管理页：列出 plugins 根目录 dll 的启停状态并可切换。启停改动只缓存在内存里，点"确定"才改名落盘并重启游戏；"取消"始终无条件放弃改动。</summary>
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

        /// <summary>同族内每个 <c>Create</c> 之间的 z 间隔；默认值会导致相邻窗口在 z 上打平，拉开到 0.05 让三层各自分明。</summary>
        const float DesignerSlipZ = 0.05f;

        static readonly Color32 TitleColor = new Color32(56, 56, 56, 255);

        static GameObject host;
        static UiBoxDesignerFamily family;
        static UiBoxDesigner designer;
        static bool isOpen;

        /// <summary>本次打开页面期间缓存的启停改动；只记与磁盘现状不同的项，<c>Count == 0</c> 即无待应用改动。</summary>
        static readonly Dictionary<string, bool> pending = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>上一次启停失败的原因；不能直接读 <see cref="UserModRecord.Error"/>，因为每次 Scan 都会 new 出全新记录。</summary>
        static readonly Dictionary<string, string> lastErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>一条模组行：它在行管理器里的显隐开关，以及这一行代表的模组。</summary>
        readonly struct ModRow
        {
            internal ModRow(DesignerRowMem.DsnMem mem, UserModRecord record)
            {
                Mem = mem;
                Record = record;
            }

            internal DesignerRowMem.DsnMem Mem { get; }

            internal UserModRecord Record { get; }
        }

        /// <summary>本次 <see cref="Rebuild"/> 画出来的模组行，供 <see cref="Filter"/> 收放。</summary>
        static readonly List<ModRow> rowMems = [];

        /// <summary>列表上方那条搜索栏。栏本身的画法与设置界面那条共用，见 <see cref="PolarisSearchRow"/>。</summary>
        static readonly PolarisSearchRow search = new PolarisSearchRow(
            "plrs:manager:search", Localization.SearchStrings.HintMods, Filter);

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

                // 确认窗弹着时 ESC 只作用于确认窗本身，不越过它去关页面。
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
                // 文案在点击时（而非注册时）写入，跟随玩家可能已切换的语言；放在 EnterButtonState 之前避免多余重建。
                ApplyCommandButtons();
                PolarisAPI.MainMenu.EnterButtonState(ButtonName);
                Open();
                return true;
            }, insertIndex: 3);
        }

        /// <summary>按当前语言写入底部确定/取消两个按钮的文案与回调。</summary>
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
                UnityEngine.Object.DontDestroyOnLoad(host);
                // 提到盖住标题画面常驻 UI 的层级，否则版本号文本会糊在面板上（同 UiDepth 用于 PUI 窗口）。
                IN.setZ(host.transform, UiDepth.Window);
                family = host.AddComponent<UiBoxDesignerFamily>();
                family.slip_z = DesignerSlipZ; // 必须赶在下面这些 Create 之前设
                designer = family.Create(
                    "PolarisModuleManager", 0f, 0f, WindowW, WindowH,
                    -1, 30f, UiBoxDesignerFamily.MASKTYPE.BOX);
                designer.use_scroll = true;
                designer.getBox().frametype = UiBox.FRAMETYPE.MAIN;

                // 建在主面板之后才会画在其上层；初始隐藏，首次悬停模组行时才亮起。
                PolarisModDetailPopup.Ensure(family, designer);

                // 最后建，拿到这一族最靠前的 z，盖住主面板和详情浮窗。
                PolarisRestartPrompt.Ensure(family);
            }

            // 每次打开都从磁盘现状重新开始缓存，不继承上次被放弃的改动。
            pending.Clear();
            lastErrors.Clear();

            // 按钮条的显隐恢复留给这里的 ApplyCommandButtons（而非 Close），避免关闭前一帧闪现。

            Rebuild();
            family.activate();
            isOpen = true;
        }

        /// <summary>底部"确定"：有缓存改动就弹确认窗，否则直接关页面。</summary>
        static void RequestApply()
        {
            if (pending.Count == 0)
            {
                Close();
                return;
            }

            ShowRestartPrompt();
        }

        /// <summary>底部"取消"（以及取消键）：无条件放弃本次全部改动并关页面，不反问。</summary>
        static void CancelAll()
        {
            pending.Clear();
            lastErrors.Clear();
            Close();
        }

        static void Close()
        {
            PolarisRestartPrompt.Hide();
            // 搜索留到下次打开会让玩家对着半过滤的列表发懵；须在 deactivate 前做，控件还得活着。
            search.Reset();
            // 须先清掉浮窗记住的当前项，否则下次 Open 的 Rebuild 会抢在主面板之前点亮它。
            PolarisModDetailPopup.Reset();
            family?.deactivate();
            isOpen = false;
            // 不手动调用 ReturnToTop：每帧的 SetWindowOpenChecker 探测会在 isOpen 变 false 后自动归位。
        }

        static void Rebuild()
        {
            designer.Clear();
            designer.init();
            rowMems.Clear();

            // 只扫一次磁盘，列表与浮窗共用同一份快照。
            List<UserModRecord> mods = UserModToggleManager.Scan();
            BuildContent(designer, mods);

            // 重建后搜索过滤需要重新过一遍；不走 search.Apply()，避免重复刷新状态文字。
            Filter(search.Query);

            // 重建后按钮全是新实例，旧悬停状态失效，主动刷新一次浮窗内容。
            PolarisModDetailPopup.Refresh(mods, TargetEnabled, lastErrors);
        }

        /// <summary>按查询串收放模组行，返回命中条数。刻意不重建页面（会打断玩家正在输入的搜索框），只就地拨显隐。</summary>
        static int Filter(string query)
        {
            if (designer == null)
            {
                return 0;
            }

            string[] tokens = Settings.SettingsSearchQuery.Tokenize(query);
            int matched = 0;

            try
            {
                foreach (ModRow row in rowMems)
                {
                    PolarisModInfo info = row.Record.Info;

                    // 匹配文件名、展示名、作者、简介——都是原样展示的字面量，搜到什么就能看到什么。
                    bool hit = Settings.SettingsSearchQuery.MatchesAny(
                        tokens, row.Record.DisplayName,
                        info?.DisplayName, info?.Author, info?.Description);

                    if (hit)
                    {
                        matched++;
                    }

                    PolarisSearchRow.SetVisible(row.Mem, hit);
                }

                designer.rowRemakeCheck(force: true);
            }
            catch (Exception e)
            {
                // 从输入框回调里跑，异常不能顺手掀掉整个管理页；最坏是列表停在半过滤状态。
                PolarisAPI.Errors.Report(e, "filtering the mod manager list");
                Plugin.Logger.LogError($"[Polaris] Failed to apply the mod manager search filter \"{query}\".");
            }

            return matched;
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

            // 上一轮失败提示只对那次操作有意义，重新改动后应消失。
            lastErrors.Remove(record.DisplayName);
        }

        static void ShowRestartPrompt()
        {
            // 确认窗弹出期间收起主列表：同族窗口没有遮挡关系，否则鼠标仍能点到下面的按钮。
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
                // 有改名失败的：不能吞掉错误退出游戏，退回列表把失败原因展示出来，让玩家能重试。
                BackToList();
                return;
            }

            Close();
            PolarisAPI.MainMenu.QuitGame();
        }

        /// <summary>确认窗"取消"：只收起确认窗、退回列表，缓存的改动原样留着（放弃改动是底部"取消"按钮的职责）。</summary>
        static void DismissRestartPrompt()
        {
            BackToList();
        }

        /// <summary>收起确认窗、把主列表和底部按钮条放回来。</summary>
        static void BackToList()
        {
            PolarisRestartPrompt.Hide();
            SetPageChromeVisible(true);
            // 先重建再 activate：Clear() 会触发尺寸归零动画，面板已亮着时做会先塌一下再撑开。
            Rebuild();
            designer.activate();
        }

        /// <summary>切换页面自身外壳（确定/取消按钮条与提示行）的显隐；确认窗弹出期间要收起，避免两对按钮并排混淆。</summary>
        static void SetPageChromeVisible(bool visible)
        {
            PolarisAPI.MainMenu.SetCommandButtonVisible(ButtonName, submit: true, visible);
            PolarisAPI.MainMenu.SetCommandButtonVisible(ButtonName, submit: false, visible);
            PolarisAPI.MainMenu.SetOperationHint(ButtonName, visible ? PageHint : PromptHint);
        }

        /// <summary>把缓存的改动逐条落到磁盘，之后 <see cref="pending"/> 里只剩改名失败的项；返回是否全部应用完毕。</summary>
        static bool ApplyPending()
        {
            // 重新扫一次而不是复用界面快照：玩家可能在页面开着期间手动删改了某个 dll。
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

            // 扫不到的（文件已被外部删改）留在缓存里只会让确认窗反复弹，记日志后一并丢掉。
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
                // 一个 dll 都没有时不画搜索栏，摆一个搜不出东西的框只是碍事。
                search.Build(box);

                foreach (UserModRecord record in mods)
                {
                    bool target = TargetEnabled(record);
                    string prefix = target ? "[✓] " : "[ ] ";
                    string dirtyMark = target != record.Enabled ? "  *" : "";
                    lastErrors.TryGetValue(record.DisplayName, out string error);
                    aBtnNel rowButton = box.addButtonT<aBtnNel>(new DsnDataButton
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

                    // 登记这一行的显隐开关；搜索过滤就是拨它，而不是重建页面（理由见 Filter）。
                    rowMems.Add(new ModRow(box.getRowManager().getBlockMemory(rowButton), record));
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
                // 也挂说明，避免鼠标从最后一个模组滑到这里时浮窗僵在上一条上。
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

        /// <summary>页面最上方居中的 Polaris logo；图片取不到就整行跳过。换算交给 <see cref="PUI.PuiImage.Assign"/>，与 PUI 的 Image 元素共用实现。</summary>
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
