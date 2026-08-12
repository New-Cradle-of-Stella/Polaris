import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const dir = "E:/Projects/Polaris/outputs/019ff195-ed27-74d0-bd34-98cdd58d5196";
const inputPath = `${dir}/Polaris-Game-API-Static-Classification.xlsx`;
const outputPath = `${dir}/Polaris-Game-API-Static-Classification-v1-三类API-精简版.xlsx`;

const propertyPurposes = {
  GameFrameCount: "读取游戏当前累计运行帧数。",
  HasFocus: "判断游戏窗口当前是否获得输入焦点。",
  MousePosition: "读取鼠标当前的屏幕坐标。",
  MouseWheelDelta: "读取本帧鼠标滚轮的滚动量。",
  AssetLoadStage: "读取游戏资源当前的加载阶段。",
  IsReady: "判断音频系统是否已经初始化完成。",
  MapKey: "读取当前地图的唯一键名。",
  MapTime: "读取当前地图累计运行的游戏时间。",
  MoverCount: "读取当前地图中的移动对象数量。",
  PlayerCount: "读取当前地图中的玩家对象数量。",
  IsMapDark: "判断当前地图是否属于黑暗区域。",
  NightLevel: "读取当前夜晚等级。",
  WeatherMask: "读取当前启用天气的位掩码。",
  X: "读取角色当前的横向坐标。",
  Y: "读取角色当前的纵向坐标。",
  VelocityX: "读取角色当前的横向速度。",
  VelocityY: "读取角色当前的纵向速度。",
  Width: "读取角色碰撞区域的宽度。",
  Height: "读取角色碰撞区域的高度。",
  Facing: "读取角色当前朝向。",
  Hp: "读取角色当前生命值。",
  MaxHp: "读取角色生命值上限。",
  Mp: "读取角色当前魔力值。",
  MaxMp: "读取角色魔力值上限。",
  IsAlive: "判断角色当前是否存活。",
  State: "读取玩家当前状态。",
  IsChanting: "判断玩家当前是否正在咏唱。",
  EnemyId: "读取敌人的类型编号。",
  EnemyState: "读取敌人当前状态。",
  Key: "读取物品的稳定键名。",
  Id: "读取物品的原版数值编号。",
  Price: "读取物品的基础价格。",
  StackLimit: "读取物品的最大堆叠数量。",
  Category: "读取物品所属分类。",
  Value: "读取物品的原版数值参数。",
  IsUsable: "判断物品是否可以使用。",
  IsPrecious: "判断物品是否属于贵重物品。",
  IsFood: "判断物品是否属于食物。",
  IsTool: "判断物品是否属于工具。",
  IsBomb: "判断物品是否属于炸弹。",
  CapacityRows: "读取存储容器可容纳的行数。",
  SplitsByGrade: "判断存储容器是否按物品等级分组。",
  AcceptsWater: "读取或设置存储容器是否允许存放水类物品。",
  MaxAmount: "读取单种货币允许持有的最大数量。",
  SfxVolume: "读取或设置音效音量。",
  VoiceVolume: "读取或设置语音音量。",
  BgmVolume: "读取或设置背景音乐音量。",
  MasterVolume: "读取或设置总音量。",
  Bpm: "读取当前背景音乐的 BPM。",
  BeatCount: "读取当前背景音乐累计经过的节拍数。",
  IsLooping: "判断该音频播放实例是否循环播放。",
  BaseVolume: "读取该音频播放实例的基础音量。",
  RemainingMilliseconds: "读取该音频播放实例的剩余播放毫秒数。",
  CanHandleInput: "判断游戏菜单当前是否可以处理输入。",
  ShouldQuitCategory: "读取或设置游戏菜单是否应退出当前分类。",
  IsInputHandlingEnabled: "读取或设置游戏菜单的输入处理总开关。",
  IsMessageVisible: "判断事件消息框当前是否可见。",
  SkipMode: "读取或设置事件系统当前的跳过模式。",
  IsSkipDenied: "读取或设置事件系统是否禁止跳过。",
  IsAssetLoading: "判断事件系统是否正在加载资源。",
  IsPrepared: "判断事件系统是否已准备完成。",
  HasCaneQuest: "判断当前是否存在手杖相关任务。",
  NeedsHeadQuestRefresh: "读取或设置任务列表是否需要刷新头部任务。",
};

const methodPurposes = {
  GetCurrentLocale: "获取游戏当前使用的语言区域代码。",
  GetDefaultLocale: "获取游戏默认语言区域代码。",
  ChangeLocale: "切换游戏当前使用的语言。",
  IsCurrentLocale: "判断指定语言是否为当前语言。",
  IsHeld: "判断指定输入动作当前是否持续按下。",
  WasPressed: "判断指定输入动作是否在本帧刚刚按下。",
  WasReleased: "判断指定输入动作是否在本帧刚刚释放。",
  GetDirection: "获取当前方向输入向量。",
  ClearState: "清除指定输入动作的当前输入状态。",
  GetMapTitle: "获取当前地图的显示标题。",
  GetMouseMapPosition: "把鼠标位置转换为当前地图坐标。",
  IsInCamera: "判断指定地图区域是否位于摄像机可见范围内。",
  FindMover: "按键名查找当前地图中的移动对象。",
  CanPlayerAct: "判断玩家当前是否可以执行游戏操作。",
  ChangeMap: "切换到指定地图及入口位置。",
  IsNight: "判断当前是否处于夜晚状态。",
  HasWeather: "判断当前是否启用了指定天气。",
  GetDangerLevel: "获取当前危险等级。",
  GetDangerMeter: "获取当前危险度计量值。",
  GetDangerBonus: "获取当前手动附加的危险度。",
  SetDangerBonus: "设置手动附加的危险度。",
  ClearWeather: "清除当前天气效果。",
  ShuffleWeather: "重新随机选择当前天气。",
  SetBattleCount: "设置夜晚系统记录的战斗次数。",
  Teleport: "把指定角色立即移动到目标坐标。",
  MoveBy: "让指定角色按给定偏移量移动。",
  SetVelocity: "设置指定角色的移动速度。",
  SetFacing: "设置指定角色的朝向。",
  HealHp: "恢复指定角色的生命值。",
  HealMp: "恢复指定角色的魔力值。",
  DamageHp: "扣除指定角色的生命值。",
  DamageMp: "扣除指定角色的魔力值。",
  IsNormalState: "判断玩家是否处于普通状态。",
  IsMagicState: "判断玩家是否处于魔法相关状态。",
  ApplyDamage: "按攻击参数对指定敌人结算一次伤害。",
  AddKnockback: "给指定敌人追加击退速度。",
  Resolve: "按物品键名解析并返回物品句柄。",
  GetLocalizedName: "获取指定物品的本地化显示名称。",
  GetMain: "获取主物品存储容器。",
  GetPrecious: "获取贵重物品存储容器。",
  GetEnhancer: "获取强化物品存储容器。",
  GetHouse: "获取住宅物品存储容器。",
  Count: "统计指定存储容器中的物品数量。",
  CanAdd: "判断指定物品能否加入存储容器。",
  Reduce: "从存储容器中移除指定数量的物品。",
  Clear: "清空指定存储容器中的物品。",
  Use: "使用存储容器中的指定物品。",
  Drop: "在地图中生成指定物品的掉落物。",
  GetAmount: "获取指定货币的当前持有量。",
  Spend: "消耗指定数量的货币。",
  Load: "加载指定背景音乐资源。",
  FadeIn: "让当前背景音乐渐入播放。",
  FadeOut: "让当前背景音乐渐出停止。",
  Replace: "把当前背景音乐替换为指定曲目。",
  GetCurrentTrack: "获取当前前台背景音乐信息。",
  Pause: "暂停指定音频播放实例。",
  SetAisac: "设置指定音频播放实例的 AISAC 控制值。",
  Open: "打开游戏菜单。",
  Close: "关闭游戏菜单。",
  IsClosing: "判断游戏菜单是否正在关闭。",
  IsStoppingWorld: "判断游戏菜单是否正在暂停世界运行。",
  IsBenchMenuActive: "判断当前是否打开了长椅菜单。",
  IsEditingCategory: "判断游戏菜单是否正在编辑指定分类。",
  IsActive: "判断事件系统当前是否正在运行事件。",
  GetCurrent: "获取当前正在执行的事件句柄。",
  Start: "启动指定事件。",
  Change: "把当前事件切换为指定事件。",
  GetContent: "获取指定事件变量的文本内容。",
  SetContent: "设置指定事件变量的文本内容。",
  IsMessageWaiting: "判断事件消息是否正在等待玩家继续。",
  CanProgress: "判断当前事件是否允许继续推进。",
  StopGameLoop: "设置事件系统是否暂停游戏主循环。",
  Get: "按任务键名获取任务句柄。",
  GetProgress: "获取指定任务的当前进度。",
  GetHead: "获取当前任务列表的头部任务进度。",
  Update: "更新指定任务的阶段和显示选项。",
  Remove: "从任务追踪列表中移除指定任务。",
  SetFocused: "设置当前重点追踪的任务。",
  IsTargetItem: "判断指定物品是否是当前任务目标。",
};

const exactMethodPurposes = {
  "PolarisAPI.Game.Player.ChangeState": "切换玩家到指定状态。",
  "PolarisAPI.Game.Enemies.ChangeState": "切换指定敌人到目标状态。",
  "PolarisAPI.Game.Inventory.Add": "向指定存储容器加入物品。",
  "PolarisAPI.Game.Economy.Add": "增加指定货币的持有量。",
  "PolarisAPI.Game.Audio.Bgm.Play": "开始播放已加载的背景音乐。",
  "PolarisAPI.Game.Audio.Play": "播放指定音效并返回播放句柄。",
  "PolarisAPI.Game.Audio.Bgm.Stop": "停止当前背景音乐。",
  "PolarisAPI.Game.Audio.Stop": "停止指定音频播放实例。",
  "PolarisAPI.Game.Events.Stop": "停止当前事件。",
  "PolarisAPI.Game.Audio.Bgm.IsPlaying": "判断背景音乐当前是否正在播放。",
  "PolarisAPI.Game.Audio.IsPlaying": "判断指定音频播放实例是否正在播放。",
};

const callbackPurposes = {
  GameSceneStarted: "在游戏场景完成初始化后触发回调。",
  NewGameStarted: "在新游戏初始化完成后触发回调。",
  SaveLoaded: "在存档成功读取并应用后触发回调。",
  SaveFailed: "在存档读取失败后触发回调。",
  SaveSerialized: "在存档数据完成内存序列化后触发回调。",
  SaveWritten: "在存档文件写入完成后触发回调。",
  AutoSaveCompleted: "在自动保存流程结束后触发回调。",
  LocaleChanged: "在游戏语言切换完成后触发回调。",
  ActionPressed: "在指定输入动作被按下后触发回调。",
  ActionReleased: "在指定输入动作被释放后触发回调。",
  MapChanged: "在当前地图切换完成后触发回调。",
  MapOpened: "在地图打开完成后触发回调。",
  MapClosed: "在地图关闭完成后触发回调。",
  MapActionInitialized: "在地图动作逻辑初始化完成后触发回调。",
  MapActionClosed: "在地图动作逻辑关闭完成后触发回调。",
  DayNightChanged: "在昼夜状态发生变化后触发回调。",
  NightLevelChanged: "在夜晚等级发生变化后触发回调。",
  DangerBonusChanged: "在附加危险度发生变化后触发回调。",
  WeatherChanged: "在当前天气组合发生变化后触发回调。",
  EventOpened: "在事件成功打开后触发回调。",
  EventClosed: "在事件成功关闭后触发回调。",
  PlayerStateChanged: "在玩家状态实际变化后触发回调。",
  EnemyStateChanged: "在敌人状态实际变化后触发回调。",
  PlayerDied: "在玩家首次进入死亡状态后触发回调。",
  PlayerRevived: "在玩家从死亡状态恢复后触发回调。",
  EnemyDied: "在敌人首次进入死亡状态后触发回调。",
  KnockbackApplied: "在角色被施加击退速度后触发回调。",
  StatusAdded: "在角色获得新状态效果后触发回调。",
  StatusRefreshed: "在角色已有状态效果被刷新后触发回调。",
  StatusRemoved: "在角色状态效果被移除后触发回调。",
  DamageApplied: "在一次顶层伤害结算完成后触发回调。",
  HpDamageApplied: "在角色实际损失生命值后触发回调。",
  MpDamageApplied: "在角色实际损失魔力值后触发回调。",
  RecoveryApplied: "在角色实际恢复生命值或魔力值后触发回调。",
  ItemAdded: "在物品实际加入存储容器后触发回调。",
  ItemRemoved: "在物品实际从存储容器移除后触发回调。",
  ItemsTransferred: "在物品完成跨存储容器转移后触发回调。",
  StorageCleared: "在非空存储容器被清空后触发回调。",
  ItemObtained: "在玩家获得物品记录后触发回调。",
  ItemUsed: "在物品实际被使用后触发回调。",
  DropCreated: "在地图掉落物成功创建后触发回调。",
  MoneyChanged: "在任一货币余额实际变化后触发回调。",
  QuestStarted: "在任务首次进入追踪列表后触发回调。",
  QuestUpdated: "在任务阶段发生变化后触发回调。",
  QuestCompleted: "在任务首次进入完成状态后触发回调。",
  QuestRemoved: "在任务从追踪列表移除后触发回调。",
  FocusedQuestChanged: "在重点追踪任务发生变化后触发回调。",
  StoryFlagChanged: "在剧情标记值实际变化后触发回调。",
  GameMenuOpened: "在游戏菜单打开完成后触发回调。",
  GameMenuClosed: "在游戏菜单关闭完成后触发回调。",
  MusicChanged: "在当前背景音乐曲目发生变化后触发回调。",
  MusicPlaybackChanged: "在背景音乐播放或停止状态变化后触发回调。",
  SoundPlayed: "在音效成功开始播放后触发回调。",
  VolumeChanged: "在任一音量通道数值发生变化后触发回调。",
};

function terminalName(api) {
  return String(api).split(".").at(-1);
}

function purposeFor(type, api) {
  const name = terminalName(api);
  if (type === "属性提取") return propertyPurposes[name];
  if (type === "方法调用") return exactMethodPurposes[api] ?? methodPurposes[name];
  if (type === "手动注册回调") return callbackPurposes[name];
  return null;
}

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(inputPath));
const sheet = workbook.worksheets.getItem("API目录_v1");
const overview = workbook.worksheets.getItem("规范总览");
const rows = sheet.getRange("A6:O212").values;
const descriptions = rows.map((row) => {
  const purpose = purposeFor(row[2], row[3]);
  if (!purpose) throw new Error(`缺少功能说明：${row[0]} ${row[2]} ${row[3]}`);
  return [purpose];
});

sheet.getRange("A1:O1").unmerge();
sheet.getRange("A2:O2").unmerge();
sheet.getRange("I3:L3").unmerge();
sheet.getRange("L1:O212").clear({ applyTo: "all" });

sheet.getRange("A1:L1").merge();
sheet.getRange("A1").values = [["Polaris 游戏 API 规范｜v1 三类 API 精简目录"]];
sheet.getRange("A2:L2").merge();
sheet.getRange("A2").values = [["本表只保留三类 API 的必要定义；末尾仅用“功能说明”概括每个 API 的用途，不再保留优先级、风险、错误约定和备注。"]];

sheet.getRange("A3:O3").clear({ applyTo: "all" });
sheet.getRange("A3:H3").values = [["目录条目", null, "属性提取", null, "方法调用", null, "手动注册回调", null]];
sheet.getRange("B3").formulas = [["=COUNTA(A6:A212)"]];
sheet.getRange("D3").formulas = [["=COUNTIF(C6:C212,\"属性提取\")"]];
sheet.getRange("F3").formulas = [["=COUNTIF(C6:C212,\"方法调用\")"]];
sheet.getRange("H3").formulas = [["=COUNTIF(C6:C212,\"手动注册回调\")"]];
sheet.getRange("I3:L3").merge();
sheet.getRange("I3").values = [["精简版｜末尾仅保留功能说明"]];

sheet.getRange("L5").copyFrom(sheet.getRange("I5"), "formats");
sheet.getRange("L5").values = [["功能说明"]];
sheet.getRange("L6:L212").copyFrom(sheet.getRange("I6:I212"), "formats");
sheet.getRange("L6:L212").values = descriptions;
sheet.getRange("L6:L212").format.wrapText = true;
sheet.getRange("L6:L212").format.verticalAlignment = "center";
sheet.getRange("L6:L212").format.horizontalAlignment = "left";

sheet.getRange("A1:L1").format = {
  fill: "#14532D",
  font: { bold: true, color: "#FFFFFF", size: 16 },
  verticalAlignment: "center",
  horizontalAlignment: "left",
};
sheet.getRange("A2:L2").format = {
  fill: "#DCFCE7",
  font: { italic: true, color: "#166534", size: 10 },
  verticalAlignment: "center",
  horizontalAlignment: "left",
  wrapText: true,
};
sheet.getRange("A3:L3").format = {
  fill: "#F1F5F9",
  font: { bold: true, color: "#475569", size: 10 },
  verticalAlignment: "center",
  horizontalAlignment: "center",
  borders: { preset: "inside", style: "thin", color: "#CBD5E1" },
};
for (const cell of ["B3", "D3", "F3", "H3"]) {
  sheet.getRange(cell).format.font = { bold: true, color: "#166534" };
}
sheet.getRange("I3:L3").format = {
  fill: "#ECFDF5",
  font: { bold: true, color: "#166534", size: 10 },
  verticalAlignment: "center",
  horizontalAlignment: "center",
  borders: { preset: "outside", style: "thin", color: "#CBD5E1" },
};

sheet.getRange("A1:L1").format.rowHeight = 30;
sheet.getRange("A2:L2").format.rowHeight = 34;
sheet.getRange("A3:L3").format.rowHeight = 24;
sheet.getRange("A1:A212").format.columnWidth = 7;
sheet.getRange("B1:B212").format.columnWidth = 18;
sheet.getRange("C1:C212").format.columnWidth = 16;
sheet.getRange("D1:D212").format.columnWidth = 39;
sheet.getRange("E1:E212").format.columnWidth = 43;
sheet.getRange("F1:F212").format.columnWidth = 10;
sheet.getRange("G1:G212").format.columnWidth = 22;
sheet.getRange("H1:H212").format.columnWidth = 40;
sheet.getRange("I1:I212").format.columnWidth = 47;
sheet.getRange("J1:J212").format.columnWidth = 14;
sheet.getRange("K1:K212").format.columnWidth = 15;
sheet.getRange("L1:L212").format.columnWidth = 44;

sheet.freezePanes.unfreeze();
sheet.freezePanes.freezeRows(5);
sheet.freezePanes.freezeColumns(3);

// 目录已删除优先级列，总览不再保留失去数据来源的 P0/P2 统计。
overview.getRange("A9:B10").clear({ applyTo: "all" });

const keyInspect = await workbook.inspect({
  kind: "table",
  sheetId: "API目录_v1",
  range: "A1:L16",
  include: "values,formulas",
  tableMaxRows: 16,
  tableMaxCols: 12,
  maxChars: 12000,
});
const tailInspect = await workbook.inspect({
  kind: "table",
  sheetId: "API目录_v1",
  range: "A154:L212",
  include: "values,formulas",
  tableMaxRows: 59,
  tableMaxCols: 12,
  maxChars: 50000,
});
const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
await fs.writeFile(`${dir}/simplify-final-verification.ndjson`, `${keyInspect.ndjson}\n${tailInspect.ndjson}\n${errors.ndjson}\n`, "utf8");

for (const [name, range] of [
  ["simplify-final-start.png", "A1:L35"],
  ["simplify-final-middle.png", "A65:L105"],
  ["simplify-final-callbacks.png", "A154:L185"],
  ["simplify-final-tail.png", "A186:L212"],
]) {
  const preview = await workbook.render({ sheetName: "API目录_v1", range, scale: 1.15, format: "png" });
  await fs.writeFile(`${dir}/${name}`, new Uint8Array(await preview.arrayBuffer()));
}

for (const [sheetName, name, range] of [
  ["规范总览", "simplify-final-overview.png", "A1:H25"],
  ["映射与错误规则", "simplify-final-rules.png", "A1:G25"],
  ["辅助类型", "simplify-final-types.png", "A1:G32"],
]) {
  const preview = await workbook.render({ sheetName, range, scale: 1.15, format: "png" });
  await fs.writeFile(`${dir}/${name}`, new Uint8Array(await preview.arrayBuffer()));
}

await fs.mkdir(dir, { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);

console.log(JSON.stringify({ outputPath, rows: descriptions.length, formulaScan: errors.ndjson }));
