import fs from "node:fs/promises";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const dir = "E:/Projects/Polaris/outputs/019ff195-ed27-74d0-bd34-98cdd58d5196";
const outputPath = `${dir}/Polaris-Game-API-Spec-v2-静态与实例模型.xlsx`;

const sections = [];
const addSection = (title, kind, rows) => sections.push({ title, kind, rows });
const S = (owner, method, signature, purpose) => [`[静态｜${owner}] ${method}`, signature, purpose];
const D = (owner, method, signature, purpose) => [`[动态｜${owner}] ${method}`, signature, purpose];

addSection("静态 API｜全局状态与输入", "static", [
  S("PolarisAPI.Game.Loop", "FrameCount", "static int FrameCount { get; }", "读取游戏当前累计运行帧数。"),
  S("PolarisAPI.Game.Loop", "HasFocus", "static bool HasFocus { get; }", "判断游戏窗口当前是否获得输入焦点。"),
  S("PolarisAPI.Game.Input", "MousePosition", "static GameVector2 MousePosition { get; }", "读取鼠标当前的屏幕坐标。"),
  S("PolarisAPI.Game.Input", "MouseWheelDelta", "static GameVector2 MouseWheelDelta { get; }", "读取本帧鼠标滚轮的滚动量。"),
  S("PolarisAPI.Game.Input", "IsHeld", "static bool IsHeld(GameInputAction action)", "判断指定输入动作当前是否持续按下。"),
  S("PolarisAPI.Game.Input", "WasPressed", "static bool WasPressed(GameInputAction action, int bufferFrames = 1)", "判断指定输入动作是否在本帧或缓冲帧内刚刚按下。"),
  S("PolarisAPI.Game.Input", "WasReleased", "static bool WasReleased(GameInputAction action, int heldFrames = 0)", "判断指定输入动作是否在本帧刚刚释放。"),
  S("PolarisAPI.Game.Input", "GetDirection", "static GameVector2 GetDirection()", "获取当前方向输入向量。"),
  S("PolarisAPI.Game.Input", "ClearState", "static void ClearState(string key, bool onlyPressDown = true)", "清除指定输入动作的当前输入状态。"),
  S("PolarisAPI.Game.Assets", "LoadStage", "static int LoadStage { get; }", "读取游戏资源当前的加载阶段。"),
]);

addSection("静态 API｜语言与全局实例入口", "static", [
  S("PolarisAPI.Game.Localization", "CurrentLocale", "static string CurrentLocale { get; }", "获取游戏当前使用的语言区域代码。"),
  S("PolarisAPI.Game.Localization", "DefaultLocale", "static string DefaultLocale { get; }", "获取游戏默认语言区域代码。"),
  S("PolarisAPI.Game.Localization", "Change", "static void Change(string locale)", "切换游戏当前使用的语言。"),
  S("PolarisAPI.Game.Localization", "IsCurrent", "static bool IsCurrent(string locale)", "判断指定语言是否为当前语言。"),
  S("PolarisAPI.Game.World", "CurrentMap", "static GameMap? CurrentMap { get; }", "取得当前地图实例；随后通过 GameMap 的动态 API 读取地图字段或执行地图操作。"),
  S("PolarisAPI.Game.World", "CurrentPlayer", "static GamePlayer? CurrentPlayer { get; }", "取得当前玩家实例；随后通过 GamePlayer 或其基类 GameCharacter 的动态 API 操作玩家。"),
  S("PolarisAPI.Game.World", "ChangeMap", "static GameMap ChangeMap(string mapKey)", "切换到指定地图并返回新的 GameMap 实例。"),
  S("PolarisAPI.Game.Items", "Resolve", "static GameItem? Resolve(string itemKey)", "按物品键名取得 GameItem 实例。"),
  S("PolarisAPI.Game.Inventory", "Main", "static GameStorage? Main { get; }", "取得主物品栏的 GameStorage 实例。"),
  S("PolarisAPI.Game.Inventory", "Precious", "static GameStorage? Precious { get; }", "取得贵重物品栏的 GameStorage 实例。"),
  S("PolarisAPI.Game.Inventory", "Enhancer", "static GameStorage? Enhancer { get; }", "取得强化物品栏的 GameStorage 实例。"),
  S("PolarisAPI.Game.Inventory", "House", "static GameStorage? House { get; }", "取得住宅仓库的 GameStorage 实例。"),
  S("PolarisAPI.Game.Menu", "Current", "static GameMenu? Current { get; }", "取得当前游戏菜单实例；菜单未打开时返回 null。"),
  S("PolarisAPI.Game.Menu", "Open", "static GameMenu Open()", "打开游戏菜单并返回 GameMenu 实例。"),
  S("PolarisAPI.Game.Events", "Current", "static GameEvent? Current { get; }", "取得当前正在执行的 GameEvent 实例。"),
  S("PolarisAPI.Game.Events", "Start", "static GameEvent Start(string eventKey)", "启动指定事件并返回 GameEvent 实例。"),
  S("PolarisAPI.Game.Events", "Change", "static GameEvent Change(string eventKey)", "切换到指定事件并返回新的 GameEvent 实例。"),
  S("PolarisAPI.Game.Quests", "Get", "static GameQuest? Get(string questKey)", "按任务键名取得 GameQuest 实例。"),
  S("PolarisAPI.Game.Quests", "Head", "static GameQuestProgressView? Head { get; }", "获取当前任务列表的头部任务摘要。"),
]);

addSection("静态 API｜世界、经济与音频控制", "static", [
  S("PolarisAPI.Game.World", "IsNight", "static bool IsNight()", "判断当前是否处于夜晚状态。"),
  S("PolarisAPI.Game.World", "HasWeather", "static bool HasWeather(GameWeather weather)", "判断当前是否启用了指定天气。"),
  S("PolarisAPI.Game.World", "DangerLevel", "static float DangerLevel { get; }", "读取当前危险等级。"),
  S("PolarisAPI.Game.World", "DangerMeter", "static int GetDangerMeter(bool real = true, bool raw = false)", "读取当前危险度计量值。"),
  S("PolarisAPI.Game.World", "DangerBonus", "static int DangerBonus { get; set; }", "读取或设置手动附加的危险度。"),
  S("PolarisAPI.Game.World", "ClearWeather", "static void ClearWeather()", "清除当前天气效果。"),
  S("PolarisAPI.Game.World", "ShuffleWeather", "static void ShuffleWeather()", "重新随机选择当前天气。"),
  S("PolarisAPI.Game.World", "BattleCount", "static int BattleCount { set; }", "设置夜晚系统记录的战斗次数。"),
  S("PolarisAPI.Game.Economy", "MaxAmount", "static uint MaxAmount { get; }", "读取单种货币允许持有的最大数量。"),
  S("PolarisAPI.Game.Economy", "GetAmount", "static uint GetAmount(GameCurrency currency)", "获取指定货币的当前持有量。"),
  S("PolarisAPI.Game.Economy", "Add", "static uint Add(GameCurrency currency, int amount)", "增加指定货币并返回变更后的余额。"),
  S("PolarisAPI.Game.Economy", "Spend", "static bool Spend(GameCurrency currency, int amount)", "尝试消耗指定货币并返回是否成功。"),
  S("PolarisAPI.Game.Audio", "IsReady", "static bool IsReady { get; }", "判断音频系统是否已经初始化完成。"),
  S("PolarisAPI.Game.Audio", "SfxVolume", "static int SfxVolume { get; set; }", "读取或设置音效音量。"),
  S("PolarisAPI.Game.Audio", "VoiceVolume", "static int VoiceVolume { get; set; }", "读取或设置语音音量。"),
  S("PolarisAPI.Game.Audio", "BgmVolume", "static int BgmVolume { get; set; }", "读取或设置背景音乐音量。"),
  S("PolarisAPI.Game.Audio", "MasterVolume", "static int MasterVolume { get; set; }", "读取或设置总音量。"),
  S("PolarisAPI.Game.Audio", "Play", "static GameAudioPlayback? Play(string cue, bool loop = false)", "播放指定音效并返回可继续控制的 GameAudioPlayback 实例。"),
  S("PolarisAPI.Game.Audio.Bgm", "Load", "static void Load(string timing, string cue)", "加载指定背景音乐资源。"),
  S("PolarisAPI.Game.Audio.Bgm", "Play", "static void Play()", "开始播放已加载的背景音乐。"),
  S("PolarisAPI.Game.Audio.Bgm", "Stop", "static void Stop()", "停止当前背景音乐。"),
  S("PolarisAPI.Game.Audio.Bgm", "FadeIn", "static void FadeIn(float seconds)", "让当前背景音乐渐入播放。"),
  S("PolarisAPI.Game.Audio.Bgm", "FadeOut", "static void FadeOut(float seconds)", "让当前背景音乐渐出停止。"),
  S("PolarisAPI.Game.Audio.Bgm", "Replace", "static void Replace(string timing, string cue, bool immediate = false)", "把当前背景音乐替换为指定曲目。"),
  S("PolarisAPI.Game.Audio.Bgm", "IsPlaying", "static bool IsPlaying()", "判断背景音乐当前是否正在播放。"),
  S("PolarisAPI.Game.Audio.Bgm", "CurrentTrack", "static GameBgmTrack? CurrentTrack { get; }", "读取当前前台背景音乐的曲目信息。"),
  S("PolarisAPI.Game.Audio.Bgm", "Bpm", "static float Bpm { get; }", "读取当前背景音乐的 BPM。"),
  S("PolarisAPI.Game.Audio.Bgm", "BeatCount", "static int BeatCount { get; }", "读取当前背景音乐累计经过的节拍数。"),
]);

addSection("动态 API｜GameMap 地图实例", "dynamic", [
  D("GameMap", "Key", "string Key { get; }", "读取该地图实例的唯一键名。"),
  D("GameMap", "Time", "float Time { get; }", "读取该地图实例累计运行的游戏时间。"),
  D("GameMap", "MoverCount", "int MoverCount { get; }", "读取该地图中的移动对象数量。"),
  D("GameMap", "PlayerCount", "int PlayerCount { get; }", "读取该地图中的玩家对象数量。"),
  D("GameMap", "IsDark", "bool IsDark { get; }", "判断该地图是否属于黑暗区域。"),
  D("GameMap", "Title", "string? Title { get; }", "读取该地图的显示标题。"),
  D("GameMap", "MousePosition", "GameVector2 MousePosition { get; }", "把当前鼠标位置转换为该地图的坐标。"),
  D("GameMap", "IsInCamera", "bool IsInCamera(float x, float y, float width, float height, float marginPixels = 0)", "判断该地图中的指定区域是否位于摄像机可见范围内。"),
  D("GameMap", "FindCharacter", "GameCharacter? FindCharacter(string key)", "按键名从该地图取得通用 GameCharacter 实例。"),
  D("GameMap", "FindEnemy", "GameEnemy? FindEnemy(string key)", "按键名从该地图取得 GameEnemy 实例，以调用敌人专属动态 API。"),
]);

addSection("动态 API｜GameCharacter 角色基础实例", "dynamic", [
  D("GameCharacter", "X", "float X { get; }", "读取该角色的横向坐标。"),
  D("GameCharacter", "Y", "float Y { get; }", "读取该角色的纵向坐标。"),
  D("GameCharacter", "VelocityX", "float VelocityX { get; }", "读取该角色的横向速度。"),
  D("GameCharacter", "VelocityY", "float VelocityY { get; }", "读取该角色的纵向速度。"),
  D("GameCharacter", "Width", "float Width { get; }", "读取该角色碰撞区域的宽度。"),
  D("GameCharacter", "Height", "float Height { get; }", "读取该角色碰撞区域的高度。"),
  D("GameCharacter", "Facing", "GameFacing Facing { get; }", "读取该角色当前朝向。"),
  D("GameCharacter", "Hp", "int Hp { get; }", "读取该角色当前生命值。"),
  D("GameCharacter", "MaxHp", "int MaxHp { get; }", "读取该角色生命值上限。"),
  D("GameCharacter", "Mp", "int Mp { get; }", "读取该角色当前魔力值。"),
  D("GameCharacter", "MaxMp", "int MaxMp { get; }", "读取该角色魔力值上限。"),
  D("GameCharacter", "IsAlive", "bool IsAlive { get; }", "判断该角色当前是否存活。"),
  D("GameCharacter", "Teleport", "void Teleport(GameVector2 position)", "把该角色立即移动到目标坐标。"),
  D("GameCharacter", "MoveBy", "bool MoveBy(GameVector2 delta, bool checkFoot = true)", "让该角色按给定偏移量移动。"),
  D("GameCharacter", "SetVelocity", "void SetVelocity(GameVector2 velocity)", "设置该角色的移动速度。"),
  D("GameCharacter", "SetFacing", "void SetFacing(GameFacing facing, bool forceSprite = false)", "设置该角色的朝向。"),
  D("GameCharacter", "HealHp", "void HealHp(int amount)", "恢复该角色的生命值。"),
  D("GameCharacter", "HealMp", "void HealMp(int amount)", "恢复该角色的魔力值。"),
  D("GameCharacter", "DamageHp", "int DamageHp(int amount, bool force = false)", "对该角色结算生命值伤害并返回实际伤害。"),
  D("GameCharacter", "DamageMp", "int DamageMp(int amount, bool force = false)", "对该角色结算魔力值伤害并返回实际伤害。"),
]);

addSection("动态 API｜GamePlayer 玩家实例（继承 GameCharacter）", "dynamic", [
  D("GamePlayer", "State", "GamePlayerState State { get; }", "读取该玩家当前状态。"),
  D("GamePlayer", "IsChanting", "bool IsChanting { get; }", "判断该玩家是否正在咏唱。"),
  D("GamePlayer", "CanAct", "bool CanAct()", "判断该玩家当前是否可以执行游戏操作。"),
  D("GamePlayer", "ChangeState", "void ChangeState(GamePlayerState state)", "切换该玩家到指定状态。"),
  D("GamePlayer", "IsNormalState", "bool IsNormalState()", "判断该玩家是否处于普通状态。"),
  D("GamePlayer", "IsMagicState", "bool IsMagicState()", "判断该玩家是否处于魔法相关状态。"),
]);

addSection("动态 API｜GameEnemy 敌人实例（继承 GameCharacter）", "dynamic", [
  D("GameEnemy", "EnemyId", "GameEnemyId EnemyId { get; }", "读取该敌人的类型编号。"),
  D("GameEnemy", "State", "GameEnemyState State { get; }", "读取该敌人当前状态。"),
  D("GameEnemy", "ChangeState", "void ChangeState(GameEnemyState state)", "切换该敌人到目标状态。"),
  D("GameEnemy", "ApplyDamage", "int ApplyDamage(EnemyDamageRequest request)", "按攻击参数对该敌人结算一次伤害。"),
  D("GameEnemy", "AddKnockback", "void AddKnockback(KnockbackRequest request)", "给该敌人追加击退速度。"),
]);

addSection("动态 API｜GameItem 物品实例", "dynamic", [
  D("GameItem", "Key", "string Key { get; }", "读取该物品的稳定键名。"),
  D("GameItem", "Id", "ushort Id { get; }", "读取该物品的原版数值编号。"),
  D("GameItem", "Price", "int Price { get; }", "读取该物品的基础价格。"),
  D("GameItem", "StackLimit", "int StackLimit { get; }", "读取该物品的最大堆叠数量。"),
  D("GameItem", "Category", "GameItemCategory Category { get; }", "读取该物品所属分类。"),
  D("GameItem", "Value", "float Value { get; }", "读取该物品的原版数值参数。"),
  D("GameItem", "IsUsable", "bool IsUsable { get; }", "判断该物品是否可以使用。"),
  D("GameItem", "IsPrecious", "bool IsPrecious { get; }", "判断该物品是否属于贵重物品。"),
  D("GameItem", "IsFood", "bool IsFood { get; }", "判断该物品是否属于食物。"),
  D("GameItem", "IsTool", "bool IsTool { get; }", "判断该物品是否属于工具。"),
  D("GameItem", "IsBomb", "bool IsBomb { get; }", "判断该物品是否属于炸弹。"),
  D("GameItem", "GetLocalizedName", "string GetLocalizedName(int grade = 0)", "获取该物品指定等级的本地化显示名称。"),
]);

addSection("动态 API｜GameStorage 存储实例", "dynamic", [
  D("GameStorage", "CapacityRows", "int CapacityRows { get; }", "读取该存储容器可容纳的行数。"),
  D("GameStorage", "SplitsByGrade", "bool SplitsByGrade { get; }", "判断该存储容器是否按物品等级分组。"),
  D("GameStorage", "AcceptsWater", "bool AcceptsWater { get; set; }", "读取或设置该存储容器是否允许存放水类物品。"),
  D("GameStorage", "Count", "int Count(GameItem item, int grade = -1)", "统计该存储容器中的指定物品数量。"),
  D("GameStorage", "CanAdd", "bool CanAdd(GameItem item, int count = 1, int grade = 0)", "判断指定物品能否加入该存储容器。"),
  D("GameStorage", "Add", "int Add(GameItem item, int count = 1, int grade = 0)", "向该存储容器加入物品并返回实际加入数量。"),
  D("GameStorage", "Reduce", "bool Reduce(GameItem item, int count = 1, int grade = -1)", "从该存储容器中移除指定数量的物品。"),
  D("GameStorage", "Clear", "void Clear(int newCapacity = -1)", "清空该存储容器中的全部物品。"),
  D("GameStorage", "Use", "int Use(GameItem item, int grade = 0)", "使用该存储容器中的指定物品。"),
  D("GameStorage", "Drop", "GameDrop? Drop(GameItem item, int count = 1, int grade = 0)", "从该存储容器取出物品并在当前地图生成掉落物。"),
]);

addSection("动态 API｜GameAudioPlayback 音频播放实例", "dynamic", [
  D("GameAudioPlayback", "IsLooping", "bool IsLooping { get; }", "判断该播放实例是否循环播放。"),
  D("GameAudioPlayback", "BaseVolume", "float BaseVolume { get; }", "读取该播放实例的基础音量。"),
  D("GameAudioPlayback", "RemainingMilliseconds", "long RemainingMilliseconds { get; }", "读取该播放实例的剩余播放毫秒数。"),
  D("GameAudioPlayback", "Stop", "void Stop()", "停止该音频播放实例。"),
  D("GameAudioPlayback", "Pause", "void Pause(bool paused)", "暂停或恢复该音频播放实例。"),
  D("GameAudioPlayback", "IsPlaying", "bool IsPlaying()", "判断该音频播放实例是否正在播放。"),
  D("GameAudioPlayback", "SetAisac", "void SetAisac(string control, float value)", "设置该播放实例的 AISAC 控制值。"),
]);

addSection("动态 API｜GameMenu 菜单实例", "dynamic", [
  D("GameMenu", "CanHandleInput", "bool CanHandleInput { get; }", "判断该菜单当前是否可以处理输入。"),
  D("GameMenu", "ShouldQuitCategory", "bool ShouldQuitCategory { get; set; }", "读取或设置该菜单是否应退出当前分类。"),
  D("GameMenu", "IsInputHandlingEnabled", "bool IsInputHandlingEnabled { get; set; }", "读取或设置该菜单的输入处理开关。"),
  D("GameMenu", "Close", "void Close(bool immediate = false)", "关闭该菜单实例。"),
  D("GameMenu", "IsClosing", "bool IsClosing()", "判断该菜单是否正在关闭。"),
  D("GameMenu", "IsStoppingWorld", "bool IsStoppingWorld()", "判断该菜单是否正在暂停世界运行。"),
  D("GameMenu", "IsBenchMenuActive", "bool IsBenchMenuActive()", "判断该菜单是否处于长椅菜单状态。"),
  D("GameMenu", "IsEditingCategory", "bool IsEditingCategory(string categoryKey)", "判断该菜单是否正在编辑指定分类。"),
]);

addSection("动态 API｜GameEvent 事件实例", "dynamic", [
  D("GameEvent", "Key", "string Key { get; }", "读取该事件的键名。"),
  D("GameEvent", "Stop", "void Stop(bool immediate = false)", "停止该事件实例。"),
  D("GameEvent", "GetContent", "string? GetContent(string key)", "读取该事件中的指定文本变量。"),
  D("GameEvent", "SetContent", "void SetContent(string key, string value)", "设置该事件中的指定文本变量。"),
  D("GameEvent", "IsMessageVisible", "bool IsMessageVisible { get; }", "判断该事件的消息框当前是否可见。"),
  D("GameEvent", "IsMessageWaiting", "bool IsMessageWaiting()", "判断该事件消息是否正在等待玩家继续。"),
  D("GameEvent", "CanProgress", "bool CanProgress()", "判断该事件当前是否允许继续推进。"),
  D("GameEvent", "SkipMode", "int SkipMode { get; set; }", "读取或设置该事件的跳过模式。"),
  D("GameEvent", "IsSkipDenied", "bool IsSkipDenied { get; set; }", "读取或设置该事件是否禁止跳过。"),
]);

addSection("静态 API｜事件系统状态", "static", [
  S("PolarisAPI.Game.Events", "IsActive", "static bool IsActive()", "判断事件系统当前是否正在执行事件。"),
  S("PolarisAPI.Game.Events", "IsAssetLoading", "static bool IsAssetLoading { get; }", "判断事件系统是否正在加载资源。"),
  S("PolarisAPI.Game.Events", "IsPrepared", "static bool IsPrepared { get; }", "判断事件系统是否已准备完成。"),
  S("PolarisAPI.Game.Events", "StopGameLoop", "static void StopGameLoop(bool stop)", "设置事件系统是否暂停游戏主循环。"),
]);

addSection("动态 API｜GameQuest 任务实例", "dynamic", [
  D("GameQuest", "Key", "string Key { get; }", "读取该任务的稳定键名。"),
  D("GameQuest", "GetProgress", "GameQuestProgress? GetProgress(bool includeFinished = true)", "读取该任务当前的追踪进度。"),
  D("GameQuest", "Update", "void Update(int phase, QuestUpdateOptions options = default)", "更新该任务的阶段和显示选项。"),
  D("GameQuest", "Remove", "void Remove(bool considerFinished = true)", "从任务追踪列表中移除该任务。"),
  D("GameQuest", "SetFocused", "void SetFocused()", "把该任务设置为当前重点追踪任务。"),
  D("GameQuest", "IsTargetItem", "bool IsTargetItem(GameItem item, int grade = 0)", "判断指定物品是否是该任务的目标。"),
]);

addSection("静态 API｜手动注册回调（统一入口，按触发领域合并说明）", "callback", [
  S("PolarisAPI.Game.Callbacks", "Register", "static GameCallbackRegistration Register<TData>(GameCallbackKind kind, Action<TData> callback, GameCallbackOptions options = default)", "统一注册原版行为完成后的只读回调。kind 覆盖生命周期与存档、语言与输入、地图与世界、事件与角色状态、战斗、物品与经济、任务与剧情、菜单与音频。"),
]);

const workbook = Workbook.create();
const sheet = workbook.worksheets.add("API规范_v2");
sheet.showGridLines = false;

sheet.getRange("A1:C1").merge();
sheet.getRange("A1").values = [["Polaris 游戏 API 规范｜v2 静态入口与实例模型"]];
sheet.getRange("A2:C2").merge();
sheet.getRange("A2").values = [["静态 API 只负责全局状态、启动流程或取得实例；动态 API 必须由对应实例调用。第一列已直接标明静态/动态及所属实例类。"]];
sheet.getRange("A3:C3").merge();
sheet.getRange("A3").values = [["调用路径示例：PolarisAPI.Game.World.CurrentPlayer → GamePlayer.Hp / GamePlayer.ChangeState(...)；PolarisAPI.Game.Items.Resolve(key) → GameItem.Price"]];

let row = 5;
const outputRows = [];
const sectionRanges = [];
const dataRanges = [];
for (const section of sections) {
  const sectionRow = row++;
  outputRows.push([section.title, null, null]);
  sectionRanges.push({ row: sectionRow, kind: section.kind });
  outputRows.push(["API方法", "签名", "功能说明"]);
  row++;
  const start = row;
  for (const entry of section.rows) {
    outputRows.push(entry);
    row++;
  }
  dataRanges.push({ start, end: row - 1, kind: section.kind });
}

sheet.getRange(`A5:C${row - 1}`).values = outputRows;

sheet.getRange("A1:C1").format = {
  fill: "#14532D",
  font: { bold: true, color: "#FFFFFF", size: 16 },
  verticalAlignment: "center",
  horizontalAlignment: "left",
};
sheet.getRange("A2:C2").format = {
  fill: "#DCFCE7",
  font: { italic: true, color: "#166534", size: 10 },
  verticalAlignment: "center",
  horizontalAlignment: "left",
  wrapText: true,
};
sheet.getRange("A3:C3").format = {
  fill: "#F0FDF4",
  font: { color: "#475569", size: 10 },
  verticalAlignment: "center",
  horizontalAlignment: "left",
  wrapText: true,
  borders: { preset: "outside", style: "thin", color: "#BBF7D0" },
};

for (const section of sectionRanges) {
  const fill = section.kind === "dynamic" ? "#1D4ED8" : section.kind === "callback" ? "#C2410C" : "#15803D";
  sheet.getRange(`A${section.row}:C${section.row}`).merge();
  sheet.getRange(`A${section.row}:C${section.row}`).format = {
    fill,
    font: { bold: true, color: "#FFFFFF", size: 11 },
    verticalAlignment: "center",
    horizontalAlignment: "left",
  };
  sheet.getRange(`A${section.row}:C${section.row}`).format.rowHeight = 24;

  const headerRow = section.row + 1;
  sheet.getRange(`A${headerRow}:C${headerRow}`).format = {
    fill: "#0F172A",
    font: { bold: true, color: "#FFFFFF", size: 10 },
    verticalAlignment: "center",
    horizontalAlignment: "center",
    borders: { preset: "inside", style: "thin", color: "#475569" },
  };
  sheet.getRange(`A${headerRow}:C${headerRow}`).format.rowHeight = 24;
}

for (const range of dataRanges) {
  const fill = range.kind === "dynamic" ? "#EFF6FF" : range.kind === "callback" ? "#FFF7ED" : "#F0FDF4";
  const accent = range.kind === "dynamic" ? "#1D4ED8" : range.kind === "callback" ? "#C2410C" : "#166534";
  sheet.getRange(`A${range.start}:C${range.end}`).format = {
    fill,
    font: { color: "#334155", size: 10 },
    verticalAlignment: "center",
    wrapText: true,
    borders: { insideHorizontal: { style: "thin", color: "#CBD5E1" } },
  };
  sheet.getRange(`A${range.start}:A${range.end}`).format.font = { bold: true, color: accent, size: 10 };
  sheet.getRange(`A${range.start}:C${range.end}`).format.rowHeight = range.kind === "callback" ? 72 : 38;
}

sheet.getRange(`A1:A${row - 1}`).format.columnWidth = 45;
sheet.getRange(`B1:B${row - 1}`).format.columnWidth = 58;
sheet.getRange(`C1:C${row - 1}`).format.columnWidth = 58;
sheet.getRange("A1:C1").format.rowHeight = 32;
sheet.getRange("A2:C2").format.rowHeight = 30;
sheet.getRange("A3:C3").format.rowHeight = 34;
sheet.freezePanes.freezeRows(4);
sheet.freezePanes.freezeColumns(1);

const apiCount = sections.reduce((sum, section) => sum + section.rows.length, 0);
const staticCount = sections.filter((s) => s.kind === "static").reduce((sum, s) => sum + s.rows.length, 0);
const dynamicCount = sections.filter((s) => s.kind === "dynamic").reduce((sum, s) => sum + s.rows.length, 0);
const callbackDocRows = sections.filter((s) => s.kind === "callback").reduce((sum, s) => sum + s.rows.length, 0);

const verify = await workbook.inspect({
  kind: "table",
  sheetId: "API规范_v2",
  range: `A1:C${row - 1}`,
  include: "values,formulas",
  tableMaxRows: row - 1,
  tableMaxCols: 3,
  tableMaxCellChars: 220,
  maxChars: 120000,
});
const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
await fs.writeFile(`${dir}/instance-api-verification.ndjson`, `${verify.ndjson}\n${errors.ndjson}\n`, "utf8");

const previewRanges = [
  ["instance-api-start.png", "A1:C48"],
  ["instance-api-static.png", "A49:C92"],
  ["instance-api-character.png", "A93:C142"],
  ["instance-api-objects.png", `A143:C${row - 1}`],
];
for (const [name, range] of previewRanges) {
  const preview = await workbook.render({ sheetName: "API规范_v2", range, scale: 1.3, format: "png" });
  await fs.writeFile(`${dir}/${name}`, new Uint8Array(await preview.arrayBuffer()));
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({ outputPath, lastRow: row - 1, apiCount, staticCount, dynamicCount, callbackDocRows, formulaScan: errors.ndjson }));
