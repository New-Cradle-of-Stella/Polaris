import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const dir = "E:/Projects/Polaris/outputs/019ff195-ed27-74d0-bd34-98cdd58d5196";
const inputPath = `${dir}/user-edited-v2.xlsx`;
const outputPath = `${dir}/Polaris-Game-API-Spec-v2-静态与实例模型-完整回调.xlsx`;

const staticCallbacks = [
  ["GameSceneStarted", "GameSceneStartedCallbackData", "游戏场景完成初始化后触发。"],
  ["NewGameStarted", "NewGameStartedCallbackData", "新游戏初始化完成后触发。"],
  ["SaveLoaded", "SaveLoadedCallbackData", "存档成功读取并应用后触发。"],
  ["SaveFailed", "SaveFailedCallbackData", "存档读取失败后触发。"],
  ["SaveSerialized", "SaveSerializedCallbackData", "存档数据完成内存序列化后触发。"],
  ["SaveWritten", "SaveWrittenCallbackData", "存档文件写入完成后触发。"],
  ["AutoSaveCompleted", "AutoSaveCompletedCallbackData", "自动保存流程结束后触发。"],
  ["LocaleChanged", "LocaleChangedCallbackData", "游戏语言切换完成后触发。"],
  ["ActionPressed", "ActionPressedCallbackData", "任一指定输入动作被按下后触发。"],
  ["ActionReleased", "ActionReleasedCallbackData", "任一指定输入动作被释放后触发。"],
  ["MapChanged", "MapChangedCallbackData", "当前地图切换完成后触发。"],
  ["MapOpened", "MapOpenedCallbackData", "任一地图打开完成后触发；用于取得刚打开的 GameMap 实例。"],
  ["DayNightChanged", "DayNightChangedCallbackData", "昼夜状态发生变化后触发。"],
  ["NightLevelChanged", "NightLevelChangedCallbackData", "夜晚等级发生变化后触发。"],
  ["DangerBonusChanged", "DangerBonusChangedCallbackData", "手动附加危险度发生变化后触发。"],
  ["WeatherChanged", "WeatherChangedCallbackData", "当前天气组合发生变化后触发。"],
  ["EventOpened", "EventOpenedCallbackData", "任一事件成功打开后触发；用于取得刚打开的 GameEvent 实例。"],
  ["ItemObtained", "ItemObtainedCallbackData", "玩家获得物品记录后触发。"],
  ["DropCreated", "DropCreatedCallbackData", "地图掉落物成功创建后触发。"],
  ["MoneyChanged", "MoneyChangedCallbackData", "任一货币余额实际变化后触发。"],
  ["QuestStarted", "QuestChangedCallbackData", "任务首次进入追踪列表后触发；用于取得新的 GameQuest 实例。"],
  ["FocusedQuestChanged", "FocusedQuestChangedCallbackData", "重点追踪任务发生变化后触发。"],
  ["StoryFlagChanged", "StoryFlagChangedCallbackData", "剧情标记值实际变化后触发。"],
  ["GameMenuOpened", "GameMenuOpenedCallbackData", "游戏菜单打开完成后触发；用于取得新的 GameMenu 实例。"],
  ["MusicChanged", "MusicChangedCallbackData", "当前背景音乐曲目发生变化后触发。"],
  ["MusicPlaybackChanged", "MusicPlaybackChangedCallbackData", "背景音乐播放或停止状态变化后触发。"],
  ["SoundPlayed", "SoundPlayedCallbackData", "任一音效成功开始播放后触发；载荷包含 GameAudioPlayback 实例。"],
  ["VolumeChanged", "VolumeChangedCallbackData", "任一音量通道数值发生变化后触发。"],
];

const dynamicCallbacks = [
  ["GameMap", "MapClosed", "MapClosedCallbackData", "该地图实例关闭完成后触发。"],
  ["GameMap", "MapActionInitialized", "MapActionInitializedCallbackData", "该地图实例的动作逻辑初始化完成后触发。"],
  ["GameMap", "MapActionClosed", "MapActionClosedCallbackData", "该地图实例的动作逻辑关闭完成后触发。"],
  ["GameEvent", "EventClosed", "EventClosedCallbackData", "该事件实例成功关闭后触发。"],
  ["GamePlayer", "PlayerStateChanged", "PlayerStateChangedCallbackData", "该玩家实例的状态实际变化后触发。"],
  ["GamePlayer", "PlayerDied", "PlayerDiedCallbackData", "该玩家实例首次进入死亡状态后触发。"],
  ["GamePlayer", "PlayerRevived", "PlayerRevivedCallbackData", "该玩家实例从死亡状态恢复后触发。"],
  ["GameEnemy", "EnemyStateChanged", "EnemyStateChangedCallbackData", "该敌人实例的状态实际变化后触发。"],
  ["GameEnemy", "EnemyDied", "EnemyDiedCallbackData", "该敌人实例首次进入死亡状态后触发。"],
  ["GameCharacter", "KnockbackApplied", "KnockbackAppliedCallbackData", "该角色实例被施加击退速度后触发。"],
  ["GameCharacter", "StatusAdded", "StatusChangedCallbackData", "该角色实例获得新状态效果后触发。"],
  ["GameCharacter", "StatusRefreshed", "StatusChangedCallbackData", "该角色实例已有状态效果被刷新后触发。"],
  ["GameCharacter", "StatusRemoved", "StatusChangedCallbackData", "该角色实例的状态效果被移除后触发。"],
  ["GameCharacter", "DamageApplied", "DamageAppliedCallbackData", "该角色实例的一次顶层伤害结算完成后触发。"],
  ["GameCharacter", "HpDamageApplied", "HpDamageAppliedCallbackData", "该角色实例实际损失生命值后触发。"],
  ["GameCharacter", "MpDamageApplied", "MpDamageAppliedCallbackData", "该角色实例实际损失魔力值后触发。"],
  ["GameCharacter", "RecoveryApplied", "RecoveryAppliedCallbackData", "该角色实例实际恢复生命值或魔力值后触发。"],
  ["GameStorage", "ItemAdded", "InventoryChangedCallbackData", "物品实际加入该存储实例后触发。"],
  ["GameStorage", "ItemRemoved", "InventoryChangedCallbackData", "物品实际从该存储实例移除后触发。"],
  ["GameStorage", "ItemsTransferred", "ItemsTransferredCallbackData", "该存储实例参与的物品转移完成后触发。"],
  ["GameStorage", "StorageCleared", "StorageClearedCallbackData", "该非空存储实例被清空后触发。"],
  ["GameItem", "ItemUsed", "ItemUsedCallbackData", "该物品实例实际被使用后触发。"],
  ["GameQuest", "QuestUpdated", "QuestChangedCallbackData", "该任务实例的阶段发生变化后触发。"],
  ["GameQuest", "QuestCompleted", "QuestChangedCallbackData", "该任务实例首次进入完成状态后触发。"],
  ["GameQuest", "QuestRemoved", "QuestRemovedCallbackData", "该任务实例从追踪列表移除后触发。"],
  ["GameMenu", "GameMenuClosed", "GameMenuClosedCallbackData", "该菜单实例关闭完成后触发。"],
];

if (staticCallbacks.length + dynamicCallbacks.length !== 54) {
  throw new Error(`回调总数应为 54，当前为 ${staticCallbacks.length + dynamicCallbacks.length}`);
}

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(inputPath));
const sheet = workbook.worksheets.getItem("API规范_v2");
const preservedValues = JSON.stringify(sheet.getRange("A1:C182").values);
const preservedFormulas = JSON.stringify(sheet.getRange("A1:C182").formulas);

// 只修改现有 Register 行，并新增动态实例版重载。
sheet.getRange("A183:C183").values = [[
  "[静态｜PolarisAPI.Game.Callbacks] Register",
  "static GameCallbackRegistration Register<TData>(GameStaticCallbackKind kind, Action<TData> callback, GameCallbackOptions options = default)",
  "注册全局静态回调；不依赖具体游戏实例，直到显式 Dispose 或模块卸载时取消。",
]];
sheet.getRange("A184:C184").copyFrom(sheet.getRange("A183:C183"), "formats");
sheet.getRange("A184:C184").values = [[
  "[动态｜实例对象] Register",
  "GameCallbackRegistration Register<TData>(GameInstanceCallbackKind kind, Action<TData> callback, GameCallbackOptions options = default)",
  "在 GameMap、GameCharacter、GamePlayer、GameEnemy、GameStorage、GameItem、GameEvent、GameQuest 或 GameMenu 实例上注册；只接收该实例自身触发的回调。",
]];
sheet.getRange("A184:C184").format = {
  fill: "#EFF6FF",
  font: { color: "#334155", size: 10 },
  verticalAlignment: "center",
  wrapText: true,
  borders: { insideHorizontal: { style: "thin", color: "#CBD5E1" } },
};
sheet.getRange("A184").format.font = { bold: true, color: "#1D4ED8", size: 10 };
sheet.getRange("A183:C184").format.rowHeight = 58;

const staticHeaderRow = 185;
const staticColumnHeaderRow = 186;
const staticStart = 187;
const staticEnd = staticStart + staticCallbacks.length - 1;
const dynamicHeaderRow = staticEnd + 1;
const dynamicColumnHeaderRow = dynamicHeaderRow + 1;
const dynamicStart = dynamicColumnHeaderRow + 1;
const dynamicEnd = dynamicStart + dynamicCallbacks.length - 1;

sheet.getRange(`A${staticHeaderRow}:C${staticHeaderRow}`).merge();
sheet.getRange(`A${staticHeaderRow}`).values = [[`可注册的静态回调｜${staticCallbacks.length} 项（全局触发）`]];
sheet.getRange(`A${staticColumnHeaderRow}:C${staticColumnHeaderRow}`).values = [["回调名称", "注册参数 / 载荷", "触发时机"]];
sheet.getRange(`A${staticStart}:C${staticEnd}`).values = staticCallbacks.map(([name, data, description]) => [
  `[静态回调] ${name}`,
  `GameStaticCallbackKind.${name} → Action<${data}>`,
  description,
]);

sheet.getRange(`A${dynamicHeaderRow}:C${dynamicHeaderRow}`).merge();
sheet.getRange(`A${dynamicHeaderRow}`).values = [[`可注册的动态回调｜${dynamicCallbacks.length} 项（绑定具体实例）`]];
sheet.getRange(`A${dynamicColumnHeaderRow}:C${dynamicColumnHeaderRow}`).values = [["回调名称 / 所属实例类", "注册参数 / 载荷", "触发时机"]];
sheet.getRange(`A${dynamicStart}:C${dynamicEnd}`).values = dynamicCallbacks.map(([owner, name, data, description]) => [
  `[动态回调｜${owner}] ${name}`,
  `GameInstanceCallbackKind.${name} → Action<${data}>`,
  description,
]);

for (const [headerRow, fill] of [[staticHeaderRow, "#C2410C"], [dynamicHeaderRow, "#1D4ED8"]]) {
  sheet.getRange(`A${headerRow}:C${headerRow}`).format = {
    fill,
    font: { bold: true, color: "#FFFFFF", size: 11 },
    verticalAlignment: "center",
    horizontalAlignment: "left",
  };
  sheet.getRange(`A${headerRow}:C${headerRow}`).format.rowHeight = 25;
}

for (const headerRow of [staticColumnHeaderRow, dynamicColumnHeaderRow]) {
  sheet.getRange(`A${headerRow}:C${headerRow}`).format = {
    fill: "#0F172A",
    font: { bold: true, color: "#FFFFFF", size: 10 },
    verticalAlignment: "center",
    horizontalAlignment: "center",
    borders: { preset: "inside", style: "thin", color: "#475569" },
  };
  sheet.getRange(`A${headerRow}:C${headerRow}`).format.rowHeight = 24;
}

sheet.getRange(`A${staticStart}:C${staticEnd}`).format = {
  fill: "#FFF7ED",
  font: { color: "#334155", size: 10 },
  verticalAlignment: "center",
  wrapText: true,
  borders: { insideHorizontal: { style: "thin", color: "#FED7AA" } },
};
sheet.getRange(`A${staticStart}:A${staticEnd}`).format.font = { bold: true, color: "#C2410C", size: 10 };
sheet.getRange(`A${staticStart}:C${staticEnd}`).format.rowHeight = 40;

sheet.getRange(`A${dynamicStart}:C${dynamicEnd}`).format = {
  fill: "#EFF6FF",
  font: { color: "#334155", size: 10 },
  verticalAlignment: "center",
  wrapText: true,
  borders: { insideHorizontal: { style: "thin", color: "#BFDBFE" } },
};
sheet.getRange(`A${dynamicStart}:A${dynamicEnd}`).format.font = { bold: true, color: "#1D4ED8", size: 10 };
sheet.getRange(`A${dynamicStart}:C${dynamicEnd}`).format.rowHeight = 40;

if (JSON.stringify(sheet.getRange("A1:C182").values) !== preservedValues ||
    JSON.stringify(sheet.getRange("A1:C182").formulas) !== preservedFormulas) {
  throw new Error("A1:C182 的既有内容发生变化，已停止导出");
}

const check = await workbook.inspect({
  kind: "table",
  sheetId: "API规范_v2",
  range: `A181:C${dynamicEnd}`,
  include: "values,formulas",
  tableMaxRows: dynamicEnd - 180,
  tableMaxCols: 3,
  tableMaxCellChars: 260,
  maxChars: 70000,
});
const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
await fs.writeFile(`${dir}/callback-catalog-verification.ndjson`, `${check.ndjson}\n${errors.ndjson}\n`, "utf8");

for (const [name, range] of [
  ["callback-catalog-register.png", "A175:C192"],
  ["callback-catalog-static-1.png", `A${staticStart}:C${Math.min(staticEnd, staticStart + 13)}`],
  ["callback-catalog-static-2.png", `A${staticStart + 14}:C${staticEnd}`],
  ["callback-catalog-dynamic-1.png", `A${dynamicHeaderRow}:C${Math.min(dynamicEnd, dynamicStart + 13)}`],
  ["callback-catalog-dynamic-2.png", `A${dynamicStart + 14}:C${dynamicEnd}`],
]) {
  const preview = await workbook.render({ sheetName: "API规范_v2", range, scale: 1.3, format: "png" });
  await fs.writeFile(`${dir}/${name}`, new Uint8Array(await preview.arrayBuffer()));
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({ outputPath, staticCallbacks: staticCallbacks.length, dynamicCallbacks: dynamicCallbacks.length, dynamicEnd, preservedThroughRow: 182, formulaScan: errors.ndjson }));
