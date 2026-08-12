import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "Polaris-Game-API-Static-Classification.xlsx";
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(inputPath));
const overview = workbook.worksheets.getItem("规范总览");
const catalog = workbook.worksheets.getItem("API目录_v1");
const rules = workbook.worksheets.getItem("映射与错误规则");
const types = workbook.worksheets.getItem("辅助类型");

const callbackRows = [];
const commonError = "callback 为 null 时抛 ArgumentNullException；回调异常隔离并记录";

function addCallback(subsystem, kind, dataType, internalType, internalMember, trigger, lifetime, priority, risk, payload, note = "") {
  const id = 154 + callbackRows.length;
  callbackRows.push([
    id,
    subsystem,
    "手动注册回调",
    `PolarisAPI.Game.Callbacks.${kind}`,
    `Register<${dataType}>(GameCallbackKind.${kind}, callback, options)`,
    "注册/取消",
    internalType,
    internalMember,
    `后置：${trigger}`,
    lifetime,
    "主线程同步",
    priority,
    risk,
    commonError,
    `载荷：${payload}${note ? `；${note}` : ""}`,
  ]);
}

// 生命周期与存读档：只保留原版行为完成后的通知。
addCallback("Callbacks/Lifecycle", "GameSceneStarted", "GameSceneStartedCallbackData", "nel.COOK", "bool initGameScene(NelM2DBase)", "方法返回后派发", "游戏会话", "P0", "低", "Stamp, LoadedExistingSave");
addCallback("Callbacks/Lifecycle", "NewGameStarted", "NewGameStartedCallbackData", "nel.COOK", "void newGame(NelM2DBase, bool)", "新游戏初始化方法返回后派发", "游戏会话", "P0", "低", "Stamp");
addCallback("Callbacks/Save", "SaveLoaded", "SaveLoadedCallbackData", "nel.COOK", "bool readBinaryContent(ByteArray, sFile, NelM2DBase)", "返回 true 后派发", "存档", "P0", "低", "Stamp, SlotIndex");
addCallback("Callbacks/Save", "SaveFailed", "SaveFailedCallbackData", "nel.COOK", "bool readBinaryContent(ByteArray, sFile, NelM2DBase)", "返回 false 后派发", "存档", "P0", "中", "Stamp, SlotIndex, Reason");
addCallback("Callbacks/Save", "SaveSerialized", "SaveSerializedCallbackData", "nel.COOK", "ByteArray createBinary(ByteArray, sFile, NelM2DBase, bool, bool)", "存档二进制生成完成后派发；尚未代表落盘", "存档", "P1", "低", "Stamp, SlotIndex, ByteCount");
addCallback("Callbacks/Save", "SaveWritten", "SaveWrittenCallbackData", "nel.SVD", "string saveBinary(sFile, ByteArray)", "文件写入方法返回后派发", "存档", "P0", "中", "Stamp, SlotIndex, Succeeded, FailureReason");
addCallback("Callbacks/Save", "AutoSaveCompleted", "AutoSaveCompletedCallbackData", "nel.COOK", "UILogRow autoSave(NelM2DBase, bool, bool)", "自动保存流程返回后派发", "存档", "P1", "中", "Stamp, IsBench, Succeeded");
addCallback("Callbacks/Localization", "LocaleChanged", "LocaleChangedCallbackData", "XX.TX", "void changeFamily(string)", "语言 family 实际发生变化后派发", "全局", "P0", "低", "Stamp, PreviousLocale, CurrentLocale");

// 输入：由原版输入系统在当前帧确认边沿后派发。
addCallback("Callbacks/Input", "ActionPressed", "ActionPressedCallbackData", "XX.IN", "Update + is*PD method family", "本帧由原版输入方法确认按下边沿后派发一次", "全局", "P0", "中", "Stamp, Action");
addCallback("Callbacks/Input", "ActionReleased", "ActionReleasedCallbackData", "XX.IN", "Update + is*U method family", "本帧由原版输入方法确认释放边沿后派发一次", "全局", "P0", "中", "Stamp, Action");

// 世界、地图与事件系统。
addCallback("Callbacks/World", "MapChanged", "MapChangedCallbackData", "m2d.M2DBase", "Map2d changeMap(Map2d)", "返回的新当前地图与旧地图不同时派发", "地图", "P0", "低", "Stamp, PreviousMapKey, CurrentMapKey");
addCallback("Callbacks/World", "MapOpened", "MapOpenedCallbackData", "m2d.Map2d", "Map2d open(GameObject, MAPMODE, M2SubMap)", "地图 open 返回后派发", "地图", "P1", "低", "Stamp, MapKey, Mode");
addCallback("Callbacks/World", "MapClosed", "MapClosedCallbackData", "m2d.Map2d", "void close(...)", "地图 close 返回后派发", "地图", "P1", "低", "Stamp, MapKey");
addCallback("Callbacks/World", "MapActionInitialized", "MapActionInitializedCallbackData", "m2d.M2DBase", "void mapActionInitted(Map2d)", "地图动作初始化完成后派发", "地图", "P1", "低", "Stamp, MapKey");
addCallback("Callbacks/World", "MapActionClosed", "MapActionClosedCallbackData", "m2d.M2DBase", "void mapActionClosed(Map2d)", "地图动作关闭完成后派发", "地图", "P1", "低", "Stamp, MapKey");
addCallback("Callbacks/World", "DayNightChanged", "DayNightChangedCallbackData", "nel.NightController", "void fineLevel(bool)", "方法返回且 isNight() 结果变化后派发", "地图", "P0", "低", "Stamp, IsNight");
addCallback("Callbacks/World", "NightLevelChanged", "NightLevelChangedCallbackData", "nel.NightController", "void fineLevel(bool)", "方法返回且 night_level 变化后派发", "地图", "P1", "低", "Stamp, PreviousLevel, CurrentLevel");
addCallback("Callbacks/World", "DangerBonusChanged", "DangerBonusChangedCallbackData", "nel.NightController", "void setAdditionalDangerLevelManual(int)", "方法返回且附加危险度实际变化后派发", "地图", "P1", "低", "Stamp, PreviousValue, CurrentValue");
addCallback("Callbacks/World", "WeatherChanged", "WeatherChangedCallbackData", "nel.NightController", "clearWeather / weatherShuffle / initTemporaryWeather", "任一原版天气变更方法返回且位掩码变化后派发", "地图", "P1", "中", "Stamp, PreviousBits, CurrentBits");
addCallback("Callbacks/Events", "EventOpened", "EventOpenedCallbackData", "evt.EV", "bool listenerOpen(bool)", "返回 true 且当前事件已建立后派发", "事件", "P1", "中", "Stamp, EventKey");
addCallback("Callbacks/Events", "EventClosed", "EventClosedCallbackData", "evt.EV", "bool listenerClose(bool)", "返回 true 且事件关闭完成后派发", "事件", "P1", "中", "Stamp, EventKey");

// 角色、状态与战斗。
addCallback("Callbacks/Actors", "PlayerStateChanged", "PlayerStateChangedCallbackData", "nel.PR", "void changeState(PR.STATE, PR.STATE)", "方法返回且 state 真正变化后派发", "玩家", "P0", "低", "Stamp, PreviousState, CurrentState");
addCallback("Callbacks/Actors", "EnemyStateChanged", "EnemyStateChangedCallbackData", "nel.NelEnemy", "NelEnemy changeState(NelEnemy.STATE)", "方法返回且 state 真正变化后派发", "角色对象", "P0", "低", "Stamp, Enemy, PreviousState, CurrentState");
addCallback("Callbacks/Actors", "PlayerDied", "PlayerDiedCallbackData", "nel.PR", "bool initDeath()", "首次成功进入死亡状态后派发", "玩家", "P0", "中", "Stamp");
addCallback("Callbacks/Actors", "PlayerRevived", "PlayerRevivedCallbackData", "nel.PR", "void cureHp(int)", "方法返回且 is_alive 从 false 变为 true 后派发", "玩家", "P0", "中", "Stamp");
addCallback("Callbacks/Actors", "EnemyDied", "EnemyDiedCallbackData", "nel.NelEnemy", "bool initDeath()", "首次成功进入死亡状态后派发", "角色对象", "P0", "中", "Stamp, Enemy");
addCallback("Callbacks/Combat", "KnockbackApplied", "KnockbackAppliedCallbackData", "nel.PR / nel.NelEnemy", "void addKnockbackVelocity(...)", "击退方法返回后派发", "角色对象", "P1", "中", "Stamp, Target");
addCallback("Callbacks/Actors", "StatusAdded", "StatusChangedCallbackData", "nel.M2Ser", "M2SerItem Add(SER, int, int, bool)", "方法返回且调用前不存在该状态时派发", "角色对象", "P1", "低", "Stamp, Target, StatusId");
addCallback("Callbacks/Actors", "StatusRefreshed", "StatusChangedCallbackData", "nel.M2Ser", "M2SerItem Add(SER, int, int, bool)", "方法返回且调用前已经存在该状态时派发", "角色对象", "P1", "低", "Stamp, Target, StatusId");
addCallback("Callbacks/Actors", "StatusRemoved", "StatusChangedCallbackData", "nel.M2Ser", "void removeBit(SER)", "方法返回且调用前存在该状态时派发", "角色对象", "P1", "低", "Stamp, Target, StatusId");
addCallback("Callbacks/Combat", "DamageApplied", "DamageAppliedCallbackData", "nel.PR / nel.NelEnemy", "applyDamage(...)", "顶层伤害方法返回且实际 HP 减少后派发", "角色对象", "P0", "中", "Stamp, OperationId, Target, ActualHpDamage, WasLethal");
addCallback("Callbacks/Combat", "HpDamageApplied", "HpDamageAppliedCallbackData", "m2d.M2Attackable", "int applyHpDamage(int, bool, AttackInfo)", "返回非零实际伤害后派发", "角色对象", "P1", "中", "Stamp, OperationId, Target, Amount, HpAfter");
addCallback("Callbacks/Combat", "MpDamageApplied", "MpDamageAppliedCallbackData", "m2d.M2Attackable", "int applyMpDamage(int, bool, AttackInfo)", "返回非零实际伤害后派发", "角色对象", "P1", "中", "Stamp, OperationId, Target, Amount, MpAfter");
addCallback("Callbacks/Combat", "RecoveryApplied", "RecoveryAppliedCallbackData", "m2d.M2Attackable", "cureHp(int) / cureMp(int)", "方法返回且 HP 或 MP 实际增加后派发", "角色对象", "P0", "低", "Stamp, Target, HpDelta, MpDelta");

// 背包、掉落与货币。
addCallback("Callbacks/Inventory", "ItemAdded", "InventoryChangedCallbackData", "nel.ItemStorage", "int Add(NelItem, int, int, bool, bool)", "execute=true 且返回的实际加入数量非零时派发", "存档", "P0", "低", "Stamp, Storage, ItemKey, Grade, Delta");
addCallback("Callbacks/Inventory", "ItemRemoved", "InventoryChangedCallbackData", "nel.ItemStorage", "bool Reduce(NelItem, int, int, bool)", "返回 true 且请求数量非零时派发", "存档", "P0", "低", "Stamp, Storage, ItemKey, Grade, Delta");
addCallback("Callbacks/Inventory", "ItemsTransferred", "ItemsTransferredCallbackData", "nel.ItemStorage", "int tranferItems(ItemStorage, List<NelItemEntry>, int)", "返回的实际转移行数大于零时派发", "存档", "P1", "低", "Stamp, SourceStorage, DestinationStorage, ItemCount");
addCallback("Callbacks/Inventory", "StorageCleared", "StorageClearedCallbackData", "nel.ItemStorage", "ItemStorage clearAllItems(int)", "方法返回且调用前库存非空时派发", "存档", "P1", "中", "Stamp, Storage, PreviousItemCount, NewCapacity");
addCallback("Callbacks/Inventory", "ItemObtained", "ItemObtainedCallbackData", "nel.NelItemManager", "int getItem(NelItem, int, int, bool, bool, bool, bool)", "返回的获得记录增量非零时派发", "存档", "P0", "低", "Stamp, ItemKey, Count");
addCallback("Callbacks/Inventory", "ItemUsed", "ItemUsedCallbackData", "nel.NelItem", "int Use(PR, ItemStorage, int, IItemUser)", "物品使用方法返回且结果表示发生使用后派发", "存档", "P1", "中", "Stamp, ItemKey, Grade, ResultCode");
addCallback("Callbacks/Inventory", "DropCreated", "DropCreatedCallbackData", "nel.NelItemManager", "NelItemDrop dropManual(...)", "返回非 null 掉落对象后派发", "地图", "P1", "中", "Stamp, Drop, ItemKey, Count, Grade");
addCallback("Callbacks/Economy", "MoneyChanged", "MoneyChangedCallbackData", "nel.CoinStorage", "addCount / reduceCount", "任一货币变更方法返回且余额实际变化后派发", "存档", "P0", "低", "Stamp, Currency, PreviousValue, CurrentValue, Delta");

// 任务与剧情进度。
addCallback("Callbacks/Progression", "QuestStarted", "QuestChangedCallbackData", "nel.QuestTracker", "void updateQuest(string, int, ...)", "调用前无进度、调用后出现进度时派发", "存档", "P0", "低", "Stamp, QuestKey, PreviousPhase, CurrentPhase");
addCallback("Callbacks/Progression", "QuestUpdated", "QuestChangedCallbackData", "nel.QuestTracker", "void updateQuest(string, int, ...)", "调用前后阶段不同且未转入完成时派发", "存档", "P0", "低", "Stamp, QuestKey, PreviousPhase, CurrentPhase");
addCallback("Callbacks/Progression", "QuestCompleted", "QuestChangedCallbackData", "nel.QuestTracker", "void updateQuest(string, int, ...)", "调用后首次进入完成状态时派发", "存档", "P0", "低", "Stamp, QuestKey, PreviousPhase, CurrentPhase");
addCallback("Callbacks/Progression", "QuestRemoved", "QuestRemovedCallbackData", "nel.QuestTracker", "void remove(string, bool)", "任务确实从追踪表移除后派发", "存档", "P1", "低", "Stamp, QuestKey");
addCallback("Callbacks/Progression", "FocusedQuestChanged", "FocusedQuestChangedCallbackData", "nel.QuestTracker", "void setFocusedQuest(QuestProgress)", "方法返回且焦点任务 key 变化后派发", "存档", "P1", "低", "Stamp, PreviousQuestKey, CurrentQuestKey");
addCallback("Callbacks/Progression", "StoryFlagChanged", "StoryFlagChangedCallbackData", "nel.COOK", "void setSF(string, int)", "方法返回且 flag 值实际变化后派发", "存档", "P1", "中", "Stamp, Key, PreviousValue, CurrentValue", "Key 是原版内部键，不承诺跨版本稳定");

// 游戏内 UI 与音频。
addCallback("Callbacks/UI", "GameMenuOpened", "GameMenuOpenedCallbackData", "nel.gm.UiGameMenu", "UiBoxDesignerFamily activate()", "菜单 activate 返回且处于打开状态后派发", "菜单", "P0", "低", "Stamp");
addCallback("Callbacks/UI", "GameMenuClosed", "GameMenuClosedCallbackData", "nel.gm.UiGameMenu", "UiBoxDesignerFamily deactivate(bool)", "菜单 deactivate 返回且处于关闭状态后派发", "菜单", "P0", "低", "Stamp");
addCallback("Callbacks/Audio", "MusicChanged", "MusicChangedCallbackData", "XX.BGM", "load / replace", "调用返回且前台 BGM timing/cue 发生变化后派发", "全局", "P1", "中", "Stamp, PreviousTrack, CurrentTrack");
addCallback("Callbacks/Audio", "MusicPlaybackChanged", "MusicPlaybackChangedCallbackData", "XX.BGM", "play / stop", "调用返回且 isFrontPlaying() 发生变化后派发", "全局", "P1", "低", "Stamp, IsPlaying");
addCallback("Callbacks/Audio", "SoundPlayed", "SoundPlayedCallbackData", "XX.SndPlayer", "bool play(string, bool)", "返回 true 后派发", "音频对象", "P1", "低", "Stamp, Playback, CueName");
addCallback("Callbacks/Audio", "VolumeChanged", "VolumeChangedCallbackData", "XX.SND", "volume / voice_volume / bgm_volume / master_volume setters", "任一原版音量 setter 返回且值变化后派发", "全局", "P1", "低", "Stamp, Channel, PreviousValue, CurrentValue");

const callbackStartRow = 159;
const callbackEndRow = callbackStartRow + callbackRows.length - 1;
const catalogEndRow = callbackEndRow;

// Append to the existing API table.
const catalogTable = catalog.tables.items[0];
catalogTable.rows.add(null, callbackRows);

catalog.getRange("A1").values = [["Polaris 游戏 API 规范｜v1 三类 API 候选目录"]];
catalog.getRange("A2").values = [["本表只允许三种 API：属性提取、方法调用、手动注册回调。回调均为原版行为完成后的只读通知；不包含 Signal、Capability、自动扫描或实现状态。"]];
catalog.getRange("A3:O3").values = [["目录条目", null, "属性提取", null, "方法调用", null, "手动注册回调", null, "P0", null, "P1", null, "P2", null, "三类 API"]];
catalog.getRange("B3").formulas = [[`=COUNTA(A6:A${catalogEndRow})`]];
catalog.getRange("D3").formulas = [[`=COUNTIF(C6:C${catalogEndRow},\"属性提取\")`]];
catalog.getRange("F3").formulas = [[`=COUNTIF(C6:C${catalogEndRow},\"方法调用\")`]];
catalog.getRange("H3").formulas = [[`=COUNTIF(C6:C${catalogEndRow},\"手动注册回调\")`]];
catalog.getRange("J3").formulas = [[`=COUNTIF(L6:L${catalogEndRow},\"P0\")`]];
catalog.getRange("L3").formulas = [[`=COUNTIF(L6:L${catalogEndRow},\"P1\")`]];
catalog.getRange("N3").formulas = [[`=COUNTIF(L6:L${catalogEndRow},\"P2\")`]];
for (const cell of ["B3", "D3", "F3", "H3", "J3", "L3", "N3"]) {
  catalog.getRange(cell).format = { fill: "#FFFFFF", font: { bold: true, color: "#166534" }, horizontalAlignment: "center" };
}

catalog.getRange(`A${callbackStartRow}:O${callbackEndRow}`).format = {
  fill: "#FFF7ED",
  font: { color: "#1F2937", fontSize: 9 },
  verticalAlignment: "top",
  wrapText: true,
  borders: { insideHorizontal: { style: "thin", color: "#FED7AA" } },
};
catalog.getRange(`A${callbackStartRow}:A${callbackEndRow}`).format.horizontalAlignment = "center";
catalog.getRange(`C${callbackStartRow}:C${callbackEndRow}`).format = { font: { bold: true, color: "#9A3412" }, horizontalAlignment: "center" };
catalog.getRange(`F${callbackStartRow}:M${callbackEndRow}`).format.verticalAlignment = "top";
catalog.getRange(`F${callbackStartRow}:F${callbackEndRow}`).format.horizontalAlignment = "center";
catalog.getRange(`J${callbackStartRow}:M${callbackEndRow}`).format.horizontalAlignment = "center";
catalog.getRange(`${callbackStartRow}:${callbackEndRow}`).format.rowHeight = 44;
catalog.getRange(`M${callbackStartRow}:M${callbackEndRow}`).conditionalFormats.add("containsText", { text: "高", format: { fill: "#FEE2E2", font: { bold: true, color: "#991B1B" } } });
catalog.getRange(`C6:C${catalogEndRow}`).dataValidation = { rule: { type: "list", values: ["属性提取", "方法调用", "手动注册回调"] } };
catalog.getRange(`F6:F${catalogEndRow}`).dataValidation = { rule: { type: "list", values: ["只读", "读写", "调用", "注册/取消"] } };
catalog.getRange(`L6:L${catalogEndRow}`).dataValidation = { rule: { type: "list", values: ["P0", "P1", "P2"] } };
catalog.getRange(`M6:M${catalogEndRow}`).dataValidation = { rule: { type: "list", values: ["低", "中", "高"] } };

// Overview: explicitly show the third category and remove the old callback exclusion.
overview.getRange("A1").values = [["Polaris 游戏 API 规范｜第一版（三类 API）"]];
overview.getRange("A2").values = [["目标：为 Alice In Cradle v0.29 建立稳定、可审阅的公开表面。API 只有“属性提取”“方法调用”“手动注册回调”三类。"]];
overview.getRange("A10:B10").copyFrom(overview.getRange("A9:B9"), "all");
overview.getRange("A4:B10").values = [
  ["指标", "数量"],
  ["目录总条目", null],
  ["属性提取", null],
  ["方法调用", null],
  ["手动注册回调", null],
  ["P0 核心", null],
  ["P2 高权限", null],
];
overview.getRange("B5").formulas = [[`=COUNTA('API目录_v1'!$A$6:$A$${catalogEndRow})`]];
overview.getRange("B6").formulas = [[`=COUNTIF('API目录_v1'!$C$6:$C$${catalogEndRow},\"属性提取\")`]];
overview.getRange("B7").formulas = [[`=COUNTIF('API目录_v1'!$C$6:$C$${catalogEndRow},\"方法调用\")`]];
overview.getRange("B8").formulas = [[`=COUNTIF('API目录_v1'!$C$6:$C$${catalogEndRow},\"手动注册回调\")`]];
overview.getRange("B9").formulas = [[`=COUNTIF('API目录_v1'!$L$6:$L$${catalogEndRow},\"P0\")`]];
overview.getRange("B10").formulas = [[`=COUNTIF('API目录_v1'!$L$6:$L$${catalogEndRow},\"P2\")`]];
overview.getRange("A10:A10").format = { fill: "#F8FAFC", font: { bold: true, color: "#475569" } };
overview.getRange("B10:B10").format = { fill: "#FFFFFF", font: { bold: true, color: "#166534", fontSize: 13 }, horizontalAlignment: "center", numberFormat: "#,##0" };
overview.getRange("D4:H4").unmerge();
overview.getRange("D4:H4").merge();
overview.getRange("D4").values = [["三类 API 的边界"]];
overview.getRange("D5:H9").unmerge();
for (let row = 5; row <= 9; row++) overview.getRange(`E${row}:H${row}`).merge();
overview.getRange("D5:H9").values = [
  ["属性提取", "公开一个原始字段或属性；默认只读；不做聚合，不触发动作。", null, null, null],
  ["方法调用", "调用一个主要原始方法；允许句柄解析和参数/返回值投影，不暗中追加第二个业务动作。", null, null, null],
  ["手动注册回调", "插件显式注册；原版行为完成或状态变化确认后，在主线程派发只读通知。", null, null, null],
  ["辅助类型", "对象壳、枚举、DTO、注册令牌和错误类型只承载参数/返回值，不单独称为 API。", null, null, null],
  ["非目标", "自动扫描、特性标注、Signal 属性、Capability、Snapshot 聚合、实现状态和可跑通性。", null, null, null],
];
overview.getRange("D5:D9").format = { fill: "#F8FAFC", font: { bold: true, color: "#166534" }, verticalAlignment: "top" };
overview.getRange("E5:H9").format = { fill: "#FFFFFF", wrapText: true, verticalAlignment: "top", font: { color: "#1F2937" } };
overview.getRange("D4:H9").format.borders = { preset: "outside", style: "thin", color: "#CBD5E1" };
overview.getRange("9:9").format.rowHeight = 34;
overview.getRange("A20:C20").copyFrom(overview.getRange("A19:C19"), "all");
overview.getRange("A20:C20").values = [[8, "回调只后置", "回调只能观察已完成的原版行为；v1 不支持取消、替换、修改参数或改变原方法返回值。"]];
overview.getRange("20:20").format.rowHeight = 30;

// Callback-specific contract rules.
const callbackRuleRows = [
  ["R-15", "手动注册回调", "显式注册", "只允许调用 Register<TData>(kind, callback, options) 注册；禁止程序集扫描、特性自动注册和 Signal 属性订阅。", "必须", "注册关系可见、可追踪", "所有回调"],
  ["R-16", "手动注册回调", "只做后置通知", "原版行为完成或状态变化确认后才派发；回调不能取消、替换、修改参数或改变原返回值。", "必须", "避免回调侵入原版控制流", "所有回调"],
  ["R-17", "手动注册回调", "主线程同步", "回调在触发行为所在的游戏主线程同步执行；不跨线程、不隐式排队。", "必须", "保持时序和状态一致", "所有回调"],
  ["R-18", "手动注册回调", "异常隔离", "单个 callback 抛出的异常必须捕获、记录并继续派发其余注册项，不得反向破坏原版行为。", "必须", "保护游戏流程和其他模组", "所有回调"],
  ["R-19", "手动注册回调", "确定顺序", "先按 options.Priority 从高到低，再按注册顺序执行；派发开始后使用注册快照。", "必须", "保证可预测性", "同一 kind 多注册项"],
  ["R-20", "手动注册回调", "显式取消", "Register 返回 GameCallbackRegistration；Dispose 立即取消后续派发，模组卸载时统一释放其全部注册。", "必须", "避免悬挂委托和跨模组泄漏", "注册生命周期"],
  ["R-21", "手动注册回调", "载荷不可变", "TData 为只读 callback data；只允许公共枚举、对象壳和基础值，不得暴露原版内部对象引用。", "必须", "防止回调越权修改内部状态", "所有载荷"],
];
const rulesTable = rules.tables.items[0];
rulesTable.rows.add(null, callbackRuleRows);
const rulesStart = 19;
const rulesEnd = rulesStart + callbackRuleRows.length - 1;
rules.getRange(`A${rulesStart}:G${rulesEnd}`).format = {
  wrapText: true,
  verticalAlignment: "top",
  font: { color: "#1F2937", fontSize: 10 },
  borders: { insideHorizontal: { style: "thin", color: "#FED7AA" } },
};
rules.getRange(`A${rulesStart}:A${rulesEnd}`).format = { fill: "#FFEDD5", font: { bold: true, color: "#9A3412" }, horizontalAlignment: "center" };
rules.getRange(`B${rulesStart}:C${rulesEnd}`).format.font = { bold: true, color: "#9A3412" };
rules.getRange(`${rulesStart}:${rulesEnd}`).format.rowHeight = 46;

// Supporting types for explicit callback registration.
const callbackTypeRows = [
  ["GameCallbackKind", "enum", "目录中 54 个固定 kind", "回调目录", "稳定枚举", "只允许尾部追加；不复用既有数值", "P0"],
  ["GameCallbackRegistration", "readonly struct + IDisposable", "long Id; string OwnerKey; GameCallbackKind Kind", "注册表", "取消令牌", "Dispose 可重复调用；模组卸载时自动统一释放", "P0"],
  ["GameCallbackOptions", "readonly struct", "int Priority; bool Once; string? OwnerKey; string? DebugName", "注册参数", "参数 DTO", "Priority 越大越先执行；Once 在首次派发后自动取消", "P0"],
  ["GameCallbackStamp", "readonly record", "long Sequence; int Frame; int GameFrame; int MapGeneration", "派发时刻", "只读 DTO", "每个 callback data 必须包含 Stamp", "P0"],
  ["XxxCallbackData", "readonly record 约定", "GameCallbackStamp Stamp + 目录备注所列字段", "原版触发点", "载荷 DTO", "不得包含 UnityEngine、m2d、nel、XX、evt 对象引用", "P0"],
];
const typesTable = types.tables.items[0];
typesTable.rows.add(null, callbackTypeRows);
const typesStart = 28;
const typesEnd = typesStart + callbackTypeRows.length - 1;
types.getRange(`A${typesStart}:G${typesEnd}`).format = {
  wrapText: true,
  verticalAlignment: "top",
  font: { color: "#1F2937", fontSize: 10 },
  borders: { insideHorizontal: { style: "thin", color: "#FED7AA" } },
};
types.getRange(`A${typesStart}:A${typesEnd}`).format = { fill: "#FFEDD5", font: { bold: true, color: "#9A3412" } };
types.getRange(`${typesStart}:${typesEnd}`).format.rowHeight = 44;

// Compact verification and visual renders.
const inspect = await workbook.inspect({
  kind: "table",
  range: `API目录_v1!A150:O${catalogEndRow}`,
  include: "values,formulas",
  tableMaxRows: 70,
  tableMaxCols: 15,
  tableMaxCellChars: 160,
  maxChars: 22000,
});
const formulaErrors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
await fs.writeFile("callbacks-final-verification.ndjson", `${inspect.ndjson}\n${formulaErrors.ndjson}`, "utf8");

for (const [file, sheetName, range] of [
  ["callbacks-final-overview.png", "规范总览", "A1:H25"],
  ["callbacks-final-catalog-start.png", "API目录_v1", "A1:O30"],
  ["callbacks-final-catalog-cb1.png", "API目录_v1", "A150:O182"],
  ["callbacks-final-catalog-cb2.png", "API目录_v1", `A183:O${catalogEndRow}`],
  ["callbacks-final-rules.png", "映射与错误规则", `A1:G${rulesEnd}`],
  ["callbacks-final-types.png", "辅助类型", `A1:G${typesEnd}`],
]) {
  const preview = await workbook.render({ sheetName, range, scale: 1.1, format: "png" });
  await fs.writeFile(file, new Uint8Array(await preview.arrayBuffer()));
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(inputPath);
console.log(JSON.stringify({ callbackRows: callbackRows.length, totalRows: catalogEndRow - 5, catalogEndRow, rulesEnd, typesEnd }));
