namespace Polaris.Localization
{
    /// <summary>
    /// 模组管理页（<see cref="PolarisManagementUI"/>、<see cref="PolarisModDetailPopup"/>、<see cref="PolarisRestartPrompt"/>）全部界面文案的内置翻译。
    /// 走内置表而非 <c>.plang</c>，确保玩家在此关闭出问题的模组时该页文案不会被那个模组顶掉；取值一律走 <see cref="Text"/> 现查，不缓存，以跟随玩家实时切换语言。
    /// </summary>
    internal static class ModManagerStrings
    {
        /// <summary>key 前缀，与设置项的 <c>polaris.settings.</c> 分开。</summary>
        const string P = "polaris.manager.";

        // ---- 页面骨架 ----
        internal const string Title = "title";
        internal const string SectionMods = "section_mods";
        internal const string Empty = "empty";
        internal const string Refresh = "refresh";
        internal const string RefreshDesc = "refresh_desc";
        internal const string RowFailed = "row_failed";

        /// <summary>列表底部那行"有 N 项没应用"的提醒，<c>{0}</c> 是条数。</summary>
        internal const string PendingNote = "pending_note";

        // ---- 底部确定/取消按钮条 ----
        internal const string CmdSubmit = "cmd_submit";
        internal const string CmdCancel = "cmd_cancel";
        internal const string CmdBack = "cmd_back";

        // ---- 重启确认窗 ----
        /// <summary>确认窗正文，<c>{0}</c> 是待应用的条数。</summary>
        internal const string PromptMessage = "prompt_message";
        internal const string PromptConfirm = "prompt_confirm";
        internal const string PromptCancel = "prompt_cancel";

        // ---- 右侧详情浮窗 ----
        internal const string DetailAuthor = "detail_author";
        internal const string DetailDescription = "detail_description";
        internal const string DetailUrl = "detail_url";
        internal const string DetailPendingDisable = "detail_pending_disable";
        internal const string DetailPendingEnable = "detail_pending_enable";
        internal const string DetailDisabled = "detail_disabled";
        internal const string DetailNoInfo = "detail_no_info";
        internal const string DetailFailed = "detail_failed";

        static bool registered;

        /// <summary>查一条本页文案；<paramref name="key"/> 请用本类常量，不要写字面量。</summary>
        internal static string Text(string key)
        {
            return PolarisAPI.Localization.Text(LocalizedString.Sigil + P + key);
        }

        /// <summary>由 <see cref="PolarisManagementUI.RegisterButton"/> 调一次；不必赶在 <c>Plugin.Awake</c>，玩家打开此页时才首次查到。</summary>
        internal static void Register()
        {
            if (registered)
            {
                return;
            }

            registered = true;

            LocalizationAPI loc = PolarisAPI.Localization;

            loc.Register(P + Title, new LocalizedText("Polaris Mod Manager")
            {
                ["zh"] = "Polaris 模组管理",
                ["ja"] = "Polaris MOD管理",
            });

            loc.Register(P + SectionMods, new LocalizedText("Mod list")
            {
                ["zh"] = "模组列表",
                ["ja"] = "MOD一覧",
            });

            loc.Register(P + Empty, new LocalizedText("(none found)")
            {
                ["zh"] = "（未检测到）",
                ["ja"] = "（検出されませんでした）",
            });

            loc.Register(P + Refresh, new LocalizedText("Refresh list")
            {
                ["zh"] = "刷新列表",
                ["ja"] = "一覧を更新",
            });

            loc.Register(P + RefreshDesc, new LocalizedText(
                "Refresh list\n"
                + "Rescan the plugins folder and re-read each mod's enabled state.\n"
                + "Changes you made here but have not applied yet are kept.")
            {
                ["zh"] = "刷新列表\n"
                       + "重新扫描 plugins 目录，读取模组的启停状态。\n"
                       + "已勾选但尚未应用的修改会保留。",
                ["ja"] = "一覧を更新\n"
                       + "plugins フォルダーを再スキャンし、各MODの有効/無効を読み直します。\n"
                       + "まだ適用していない変更はそのまま保持されます。",
            });

            // 前导两个空格是排版所需，翻译时请保留。
            loc.Register(P + RowFailed, new LocalizedText("  (failed)")
            {
                ["zh"] = "  (操作失败)",
                ["ja"] = "  (失敗)",
            });

            // 不自动换行、高度固定，三种语言译文都须压在一行内（面板宽约 460px）。
            loc.Register(P + PendingNote, new LocalizedText(
                "*  {0} change(s) pending — OK applies them on restart, Cancel discards them.")
            {
                ["zh"] = "*  有 {0} 项修改尚未应用（确定后重启生效，取消则全部放弃）",
                ["ja"] = "*  未適用の変更が {0} 件（決定で再起動して反映、キャンセルで破棄）",
            });

            loc.Register(P + CmdSubmit, new LocalizedText("OK")
            {
                ["zh"] = "确定",
                ["ja"] = "決定",
            });

            loc.Register(P + CmdCancel, new LocalizedText("Cancel")
            {
                ["zh"] = "取消",
                ["ja"] = "キャンセル",
            });

            loc.Register(P + CmdBack, new LocalizedText("Back")
            {
                ["zh"] = "返回",
                ["ja"] = "戻る",
            });

            // 窗口高度 PolarisRestartPrompt.PromptH 按此处最长译文定的，大幅加长需同步调整。
            loc.Register(P + PromptMessage, new LocalizedText(
                "{0} mod enable/disable change(s) are not applied yet.\n"
                + "They take effect only when BepInEx rescans the plugins folder on the next "
                + "launch — they cannot be applied while the game is running.\n\n"
                + "OK: save them and quit the game, then start it again yourself.\n"
                + "Cancel: go back to the list, keeping the changes.")
            {
                ["zh"] = "有 {0} 项模组启停修改尚未应用。\n"
                       + "这类修改要等下次启动时 BepInEx 重新扫描插件目录才会生效，本次游戏内无法热更。\n\n"
                       + "确定：保存修改并关闭游戏，之后请手动重新启动。\n"
                       + "取消：返回列表，修改仍然保留。",
                ["ja"] = "MODの有効/無効の変更が {0} 件、まだ適用されていません。\n"
                       + "この種の変更は、次回起動時に BepInEx がプラグインフォルダーを読み直したときに反映されます。"
                       + "ゲーム中に反映することはできません。\n\n"
                       + "決定：変更を保存してゲームを終了します。そのあと手動で起動し直してください。\n"
                       + "キャンセル：一覧に戻ります。変更はそのまま保持されます。",
            });

            loc.Register(P + PromptConfirm, new LocalizedText("OK (quit game)")
            {
                ["zh"] = "确定（关闭游戏）",
                ["ja"] = "決定（ゲームを終了）",
            });

            loc.Register(P + PromptCancel, new LocalizedText("Cancel (back to list)")
            {
                ["zh"] = "取消（返回列表）",
                ["ja"] = "キャンセル（一覧に戻る）",
            });

            loc.Register(P + DetailAuthor, new LocalizedText("Author: ")
            {
                ["zh"] = "作者：",
                ["ja"] = "作者：",
            });

            loc.Register(P + DetailDescription, new LocalizedText("About: ")
            {
                ["zh"] = "简介：",
                ["ja"] = "概要：",
            });

            loc.Register(P + DetailUrl, new LocalizedText("Link: ")
            {
                ["zh"] = "链接：",
                ["ja"] = "リンク：",
            });

            loc.Register(P + DetailPendingDisable, new LocalizedText(
                "Enabled now, will be disabled after restart")
            {
                ["zh"] = "当前已启用，待禁用（确定后重启生效）",
                ["ja"] = "現在は有効、無効化予定（決定後に反映）",
            });

            loc.Register(P + DetailPendingEnable, new LocalizedText(
                "Disabled now, will be enabled after restart")
            {
                ["zh"] = "当前已禁用，待启用（确定后重启生效）",
                ["ja"] = "現在は無効、有効化予定（決定後に反映）",
            });

            loc.Register(P + DetailDisabled, new LocalizedText("Disabled")
            {
                ["zh"] = "已禁用",
                ["ja"] = "無効",
            });

            loc.Register(P + DetailNoInfo, new LocalizedText(
                "No mod info provided, or not loaded this session.")
            {
                ["zh"] = "未提供模组信息，或本次启动时未加载。",
                ["ja"] = "MOD情報が未提供、または今回の起動では読み込まれていません。",
            });

            loc.Register(P + DetailFailed, new LocalizedText("Failed: ")
            {
                ["zh"] = "操作失败：",
                ["ja"] = "失敗：",
            });
        }
    }
}
