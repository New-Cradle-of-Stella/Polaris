import fs from "node:fs/promises";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const outputPath = "Polaris-Game-API-Static-Classification.xlsx";
const workbook = Workbook.create();

const overview = workbook.worksheets.add("瑙勮寖鎬昏");
const catalog = workbook.worksheets.add("API鐩綍_v1");
const rules = workbook.worksheets.add("鏄犲皠涓庨敊璇鍒?);
const types = workbook.worksheets.add("杈呭姪绫诲瀷");

const C = {
  title: "#14532D",
  title2: "#166534",
  header: "#15803D",
  headerText: "#FFFFFF",
  property: "#ECFDF5",
  method: "#EFF6FF",
  propertyText: "#166534",
  methodText: "#1E40AF",
  section: "#DCFCE7",
  neutral: "#F8FAFC",
  border: "#CBD5E1",
  text: "#1F2937",
  muted: "#475569",
  warn: "#FFF7ED",
  high: "#FEE2E2",
};

function titleBand(sheet, title, subtitle, endColumn) {
  sheet.mergeCells(`A1:${endColumn}1`);
  sheet.getRange("A1").values = [[title]];
  sheet.getRange(`A1:${endColumn}1`).format = {
    fill: C.title,
    font: { bold: true, color: C.headerText, fontSize: 16 },
    verticalAlignment: "center",
  };
  sheet.getRange("1:1").format.rowHeight = 30;
  sheet.mergeCells(`A2:${endColumn}2`);
  sheet.getRange("A2").values = [[subtitle]];
  sheet.getRange(`A2:${endColumn}2`).format = {
    fill: C.section,
    font: { italic: true, color: C.title2, fontSize: 10 },
    wrapText: true,
    verticalAlignment: "center",
  };
  sheet.getRange("2:2").format.rowHeight = 32;
  sheet.showGridLines = false;
}

function addRow(rows, subsystem, apiType, api, signature, access, internalType, internalMember, mapping, lifetime, priority, risk, errorRule, note = "") {
  rows.push([
    rows.length + 1,
    subsystem,
    apiType,
    api,
    signature,
    access,
    internalType,
    internalMember,
    mapping,
    lifetime,
    "涓荤嚎绋?,
    priority,
    risk,
    errorRule,
    note,
  ]);
}

const apiRows = [];

// 灞炴€ф彁鍙栵細鍙搴斾竴涓師濮嬪瓧娈垫垨灞炴€э紱榛樿鍙銆?addRow(apiRows, "Loop", "灞炴€ф彁鍙?, "PolarisAPI.Game.Loop.GameFrameCount", "int GameFrameCount { get; }", "鍙", "XX.IN", "static int totalframe", "鍘熷€兼姇褰憋紱涓嶆崲绠楁椂闂村崟浣?, "鍏ㄥ眬", "P0", "浣?, "娓告垙杈撳叆绯荤粺鏈垵濮嬪寲鏃舵姏 GameApiNotReadyException");
addRow(apiRows, "Loop", "灞炴€ф彁鍙?, "PolarisAPI.Game.Loop.HasFocus", "bool HasFocus { get; }", "鍙", "XX.IN", "static bool application_focus", "鍘熷€兼姇褰?, "鍏ㄥ眬", "P0", "浣?, "鏈垵濮嬪寲鏃惰繑鍥?false");
addRow(apiRows, "Input", "灞炴€ф彁鍙?, "PolarisAPI.Game.Input.MousePosition", "GameVector2 MousePosition { get; }", "鍙", "XX.IN", "static Vector2 Mouse", "浠呭皢 Unity Vector2 鎶曞奖涓?GameVector2", "鍏ㄥ眬", "P0", "浣?, "鏈垵濮嬪寲鏃惰繑鍥?GameVector2.Zero");
addRow(apiRows, "Input", "灞炴€ф彁鍙?, "PolarisAPI.Game.Input.MouseWheelDelta", "GameVector2 MouseWheelDelta { get; }", "鍙", "XX.IN", "static Vector2 MouseWheel", "浠呭仛绫诲瀷鎶曞奖", "鍏ㄥ眬", "P1", "浣?, "鏈垵濮嬪寲鏃惰繑鍥?GameVector2.Zero");
addRow(apiRows, "Core", "灞炴€ф彁鍙?, "PolarisAPI.Game.AssetLoadStage", "int AssetLoadStage { get; }", "鍙", "XX.MTRX", "private static int loaded", "鍏紑鍘熷鍔犺浇闃舵鍊硷紝涓嶆敼鍐欏叾璇箟", "鍏ㄥ眬", "P1", "涓?, "鎴愬憳涓嶅彲璇绘椂鎶?GameMemberUnavailableException", "璇ュ瓧娈典负闈炲叕寮€鎴愬憳锛岀増鏈彉鍔ㄦ晱鎰?);
addRow(apiRows, "Audio", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.IsReady", "bool IsReady { get; }", "鍙", "XX.SND", "static bool loaded", "鍘熷睘鎬ф姇褰?, "鍏ㄥ眬", "P0", "浣?, "闊抽绯荤粺灏氭湭寤虹珛鏃惰繑鍥?false");

addRow(apiRows, "World", "灞炴€ф彁鍙?, "PolarisAPI.Game.World.MapKey", "string? MapKey { get; }", "鍙", "m2d.Map2d", "string key", "璇诲彇褰撳墠 Map2d 瀹炰緥鐨?key", "鍦板浘", "P0", "浣?, "鏃犲綋鍓嶅湴鍥炬椂杩斿洖 null");
addRow(apiRows, "World", "灞炴€ф彁鍙?, "PolarisAPI.Game.World.MapTime", "float? MapTime { get; }", "鍙", "m2d.Map2d", "float floort", "鍘熷€兼姇褰憋紝鍗曚綅淇濇寔涓烘父鎴忓抚鏃堕棿", "鍦板浘", "P1", "浣?, "鏃犲綋鍓嶅湴鍥炬椂杩斿洖 null");
addRow(apiRows, "World", "灞炴€ф彁鍙?, "PolarisAPI.Game.World.MoverCount", "int? MoverCount { get; }", "鍙", "m2d.Map2d", "int count_movers { get; }", "鍘熷睘鎬ф姇褰?, "鍦板浘", "P1", "浣?, "鏃犲綋鍓嶅湴鍥炬椂杩斿洖 null");
addRow(apiRows, "World", "灞炴€ф彁鍙?, "PolarisAPI.Game.World.PlayerCount", "int? PlayerCount { get; }", "鍙", "m2d.Map2d", "int count_players { get; }", "鍘熷睘鎬ф姇褰?, "鍦板浘", "P1", "浣?, "鏃犲綋鍓嶅湴鍥炬椂杩斿洖 null");
addRow(apiRows, "World", "灞炴€ф彁鍙?, "PolarisAPI.Game.World.IsMapDark", "bool? IsMapDark { get; }", "鍙", "m2d.M2DBase", "bool map_dark_area { get; set; }", "v1 浠呭叕寮€ getter", "鍦板浘", "P1", "涓?, "鏃犱笘鐣屽疄渚嬫椂杩斿洖 null");
addRow(apiRows, "World", "灞炴€ф彁鍙?, "PolarisAPI.Game.World.NightLevel", "float? NightLevel { get; }", "鍙", "nel.NightController", "float night_level", "鍘熷€兼姇褰?, "鍦板浘", "P0", "浣?, "NightController 涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "World", "灞炴€ф彁鍙?, "PolarisAPI.Game.World.WeatherMask", "int? WeatherMask { get; }", "鍙", "nel.NightController", "int current_weather_bit { get; }", "淇濈暀鍘熶綅鎺╃爜锛屼笉鍦ㄥ睘鎬т腑灞曞紑澶╂皵鍒楄〃", "鍦板浘", "P0", "浣?, "NightController 涓嶅瓨鍦ㄦ椂杩斿洖 null");

addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.X", "float? X(GameCharacter target) { get; }", "鍙", "m2d.M2Mover", "float x { get; }", "鍏堣В鏋愬彞鏌勶紝鍐嶈鍙栧崟涓€灞炴€?, "瑙掕壊鍙ユ焺", "P0", "浣?, "鏃犳晥/杩囨湡鍙ユ焺杩斿洖 null");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.Y", "float? Y(GameCharacter target) { get; }", "鍙", "m2d.M2Mover", "float y { get; }", "鍏堣В鏋愬彞鏌勶紝鍐嶈鍙栧崟涓€灞炴€?, "瑙掕壊鍙ユ焺", "P0", "浣?, "鏃犳晥/杩囨湡鍙ユ焺杩斿洖 null");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.VelocityX", "float? VelocityX(GameCharacter target) { get; }", "鍙", "m2d.M2Mover", "float vx { get; }", "鍘熷睘鎬ф姇褰?, "瑙掕壊鍙ユ焺", "P0", "浣?, "鏃犳晥/杩囨湡鍙ユ焺杩斿洖 null");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.VelocityY", "float? VelocityY(GameCharacter target) { get; }", "鍙", "m2d.M2Mover", "float vy { get; }", "鍘熷睘鎬ф姇褰?, "瑙掕壊鍙ユ焺", "P0", "浣?, "鏃犳晥/杩囨湡鍙ユ焺杩斿洖 null");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.Width", "float? Width(GameCharacter target) { get; }", "鍙", "m2d.M2Mover", "float sizex", "鍘熷瓧娈垫姇褰?, "瑙掕壊鍙ユ焺", "P1", "浣?, "鏃犳晥/杩囨湡鍙ユ焺杩斿洖 null");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.Height", "float? Height(GameCharacter target) { get; }", "鍙", "m2d.M2Mover", "float sizey", "鍘熷瓧娈垫姇褰?, "瑙掕壊鍙ユ焺", "P1", "浣?, "鏃犳晥/杩囨湡鍙ユ焺杩斿洖 null");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.Facing", "GameFacing? Facing(GameCharacter target) { get; }", "鍙", "m2d.M2Mover", "XX.AIM aim", "鍘熸灇涓炬槧灏勪负绋冲畾鍏叡鏋氫妇", "瑙掕壊鍙ユ焺", "P0", "涓?, "鏈煡鍘熸灇涓惧€兼槧灏勪负 GameFacing.Unknown");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.Hp", "int? Hp(GameCharacter target) { get; }", "鍙", "m2d.M2Attackable", "protected int hp", "璇诲彇鍩虹被瀛楁", "瑙掕壊鍙ユ焺", "P0", "涓?, "闈?Attackable 鎴栧彞鏌勫け鏁堟椂杩斿洖 null", "闈炲叕寮€鎴愬憳");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.MaxHp", "int? MaxHp(GameCharacter target) { get; }", "鍙", "m2d.M2Attackable", "protected int maxhp", "璇诲彇鍩虹被瀛楁", "瑙掕壊鍙ユ焺", "P0", "涓?, "闈?Attackable 鎴栧彞鏌勫け鏁堟椂杩斿洖 null", "闈炲叕寮€鎴愬憳");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.Mp", "int? Mp(GameCharacter target) { get; }", "鍙", "m2d.M2Attackable", "protected int mp", "璇诲彇鍩虹被瀛楁", "瑙掕壊鍙ユ焺", "P0", "涓?, "闈?Attackable 鎴栧彞鏌勫け鏁堟椂杩斿洖 null", "闈炲叕寮€鎴愬憳");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.MaxMp", "int? MaxMp(GameCharacter target) { get; }", "鍙", "m2d.M2Attackable", "protected int maxmp", "璇诲彇鍩虹被瀛楁", "瑙掕壊鍙ユ焺", "P0", "涓?, "闈?Attackable 鎴栧彞鏌勫け鏁堟椂杩斿洖 null", "闈炲叕寮€鎴愬憳");
addRow(apiRows, "Characters", "灞炴€ф彁鍙?, "PolarisAPI.Game.Characters.IsAlive", "bool? IsAlive(GameCharacter target) { get; }", "鍙", "m2d.M2Attackable", "bool is_alive { get; }", "鍘熷睘鎬ф姇褰?, "瑙掕壊鍙ユ焺", "P0", "浣?, "闈?Attackable 鎴栧彞鏌勫け鏁堟椂杩斿洖 null");
addRow(apiRows, "Player", "灞炴€ф彁鍙?, "PolarisAPI.Game.Player.State", "GamePlayerState? State { get; }", "鍙", "nel.PR", "protected PR.STATE state", "鍘熸灇涓炬槧灏勪负绋冲畾鍏叡鏋氫妇", "鐜╁", "P0", "涓?, "鐜╁涓嶅瓨鍦ㄦ椂杩斿洖 null锛涙湭鐭ュ€兼槧灏?Unknown", "闈炲叕寮€鎴愬憳");
addRow(apiRows, "Player", "灞炴€ф彁鍙?, "PolarisAPI.Game.Player.IsChanting", "bool? IsChanting { get; }", "鍙", "nel.PR", "bool magic_chanting { get; }", "鍘熷睘鎬ф姇褰?, "鐜╁", "P0", "浣?, "鐜╁涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "Enemies", "灞炴€ф彁鍙?, "PolarisAPI.Game.Enemies.Id", "GameEnemyId? Id(GameCharacter target) { get; }", "鍙", "nel.NelEnemy", "ENEMYID id", "鍘熸灇涓炬姇褰?, "瑙掕壊鍙ユ焺", "P1", "涓?, "鐩爣涓嶆槸 NelEnemy 鎴栧彞鏌勫け鏁堟椂杩斿洖 null");
addRow(apiRows, "Enemies", "灞炴€ф彁鍙?, "PolarisAPI.Game.Enemies.State", "GameEnemyState? State(GameCharacter target) { get; }", "鍙", "nel.NelEnemy", "protected NelEnemy.STATE state", "鍘熸灇涓炬姇褰?, "瑙掕壊鍙ユ焺", "P1", "涓?, "鐩爣涓嶆槸 NelEnemy 鏃惰繑鍥?null", "闈炲叕寮€鎴愬憳");

addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.Key", "string Key(GameItem item) { get; }", "鍙", "nel.NelItem", "readonly string key", "鐢?GameItem 瑙ｆ瀽鍚庤鍙?, "鐗╁搧鍙ユ焺", "P0", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.Id", "ushort Id(GameItem item) { get; }", "鍙", "nel.NelItem", "ushort id", "鍘熷瓧娈垫姇褰?, "鐗╁搧鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.Price", "int Price(GameItem item) { get; }", "鍙", "nel.NelItem", "int price", "鍘熷瓧娈垫姇褰?, "鐗╁搧鍙ユ焺", "P0", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.StackLimit", "int StackLimit(GameItem item) { get; }", "鍙", "nel.NelItem", "int stock", "瀵瑰鍛藉悕璇存槑璇箟锛屽€间笉鍙?, "鐗╁搧鍙ユ焺", "P0", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.Category", "GameItemCategory Category(GameItem item) { get; }", "鍙", "nel.NelItem", "NelItem.CATEG category", "鍘熸灇涓炬姇褰?, "鐗╁搧鍙ユ焺", "P0", "涓?, "鏈煡鍊兼槧灏?Unknown");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.Value", "float Value(GameItem item) { get; }", "鍙", "nel.NelItem", "float value", "鍘熷瓧娈垫姇褰?, "鐗╁搧鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.IsUsable", "bool IsUsable(GameItem item) { get; }", "鍙", "nel.NelItem", "bool useable { get; }", "鍘熷睘鎬ф姇褰?, "鐗╁搧鍙ユ焺", "P0", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.IsPrecious", "bool IsPrecious(GameItem item) { get; }", "鍙", "nel.NelItem", "bool is_precious { get; }", "鍘熷睘鎬ф姇褰?, "鐗╁搧鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.IsFood", "bool IsFood(GameItem item) { get; }", "鍙", "nel.NelItem", "bool is_food { get; }", "鍘熷睘鎬ф姇褰?, "鐗╁搧鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.IsTool", "bool IsTool(GameItem item) { get; }", "鍙", "nel.NelItem", "bool is_tool { get; }", "鍘熷睘鎬ф姇褰?, "鐗╁搧鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Items", "灞炴€ф彁鍙?, "PolarisAPI.Game.Items.IsBomb", "bool IsBomb(GameItem item) { get; }", "鍙", "nel.NelItem", "bool is_bomb { get; }", "鍘熷睘鎬ф姇褰?, "鐗╁搧鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Inventory", "灞炴€ф彁鍙?, "PolarisAPI.Game.Inventory.CapacityRows", "int CapacityRows(GameStorage storage) { get; }", "鍙", "nel.ItemStorage", "int row_max", "鍘熷瓧娈垫姇褰?, "瀛樺偍鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Inventory", "灞炴€ф彁鍙?, "PolarisAPI.Game.Inventory.SplitsByGrade", "bool SplitsByGrade(GameStorage storage) { get; }", "鍙", "nel.ItemStorage", "bool grade_split", "鍘熷瓧娈垫姇褰?, "瀛樺偍鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Inventory", "灞炴€ф彁鍙?, "PolarisAPI.Game.Inventory.AcceptsWater", "bool AcceptsWater(GameStorage storage) { get; set; }", "璇诲啓", "nel.ItemStorage", "bool water_stockable { get; set; }", "鐩存帴 getter/setter锛涗笉闄勫姞搴撳瓨鏁寸悊閫昏緫", "瀛樺偍鍙ユ焺", "P2", "楂?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException", "棣栫増涓敮涓€寤鸿寮€鏀剧殑搴撳瓨鍐欏睘鎬?);
addRow(apiRows, "Economy", "灞炴€ф彁鍙?, "PolarisAPI.Game.Economy.MaxAmount", "uint MaxAmount { get; }", "鍙", "nel.CoinStorage", "static uint MAX_COUNT", "鍘熷父閲忓瓧娈垫姇褰?, "鍏ㄥ眬", "P1", "浣?, "濮嬬粓鍙");

addRow(apiRows, "Audio", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.SfxVolume", "int SfxVolume { get; set; }", "璇诲啓", "XX.SND", "static int volume { get; set; }", "鐩存帴 getter/setter", "鍏ㄥ眬", "P0", "涓?, "璁剧疆鏃舵寜鍘熷睘鎬ц鍒欐埅鏂?闄愬埗");
addRow(apiRows, "Audio", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.VoiceVolume", "int VoiceVolume { get; set; }", "璇诲啓", "XX.SND", "static int voice_volume { get; set; }", "鐩存帴 getter/setter", "鍏ㄥ眬", "P0", "涓?, "璁剧疆鏃舵寜鍘熷睘鎬ц鍒欐埅鏂?闄愬埗");
addRow(apiRows, "Audio", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.BgmVolume", "int BgmVolume { get; set; }", "璇诲啓", "XX.SND", "static int bgm_volume { get; set; }", "鐩存帴 getter/setter", "鍏ㄥ眬", "P0", "涓?, "璁剧疆鏃舵寜鍘熷睘鎬ц鍒欐埅鏂?闄愬埗");
addRow(apiRows, "Audio", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.MasterVolume", "int MasterVolume { get; set; }", "璇诲啓", "XX.SND", "static int master_volume { get; set; }", "鐩存帴 getter/setter", "鍏ㄥ眬", "P0", "涓?, "璁剧疆鏃舵寜鍘熷睘鎬ц鍒欐埅鏂?闄愬埗");
addRow(apiRows, "Audio/BGM", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.Bgm.Bpm", "float Bpm { get; }", "鍙", "XX.BGM", "static float cur_bpm", "鍘熷瓧娈垫姇褰?, "鍏ㄥ眬", "P1", "浣?, "鏃犳椿鍔?BGM 鏃惰繑鍥?0");
addRow(apiRows, "Audio/BGM", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.Bgm.BeatCount", "int BeatCount { get; }", "鍙", "XX.BGM", "static int beatcount", "鍘熷瓧娈垫姇褰?, "鍏ㄥ眬", "P1", "浣?, "鏃犳椿鍔?BGM 鏃惰繑鍥?0");
addRow(apiRows, "Audio/SFX", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.IsLooping", "bool? IsLooping(GameAudioPlayback audio) { get; }", "鍙", "XX.SndPlayer", "byte is_loop", "0/闈? 鏄犲皠涓?bool", "闊抽鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺杩斿洖 null");
addRow(apiRows, "Audio/SFX", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.BaseVolume", "float? BaseVolume(GameAudioPlayback audio) { get; }", "鍙", "XX.SndPlayer", "float base_volume { get; }", "鍘熷睘鎬ф姇褰?, "闊抽鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺杩斿洖 null");
addRow(apiRows, "Audio/SFX", "灞炴€ф彁鍙?, "PolarisAPI.Game.Audio.RemainingMilliseconds", "long? RemainingMilliseconds(GameAudioPlayback audio) { get; }", "鍙", "XX.SndPlayer", "long rest_duration_milisecond { get; }", "鍘熷睘鎬ф姇褰?, "闊抽鍙ユ焺", "P1", "浣?, "鏃犳晥鍙ユ焺杩斿洖 null");

addRow(apiRows, "GameMenu", "灞炴€ф彁鍙?, "PolarisAPI.Game.GameMenu.CanHandleInput", "bool? CanHandleInput { get; }", "鍙", "nel.gm.UiGameMenu", "bool general_button_handleable { get; }", "鍘熷睘鎬ф姇褰?, "鑿滃崟", "P1", "浣?, "鑿滃崟瀹炰緥涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "GameMenu", "灞炴€ф彁鍙?, "PolarisAPI.Game.GameMenu.ShouldQuitCategory", "bool? ShouldQuitCategory { get; set; }", "璇诲啓", "nel.gm.UiGameMenu", "bool category_to_quit { get; set; }", "鐩存帴 getter/setter", "鑿滃崟", "P2", "楂?, "鑿滃崟瀹炰緥涓嶅瓨鍦ㄦ椂 getter 杩斿洖 null锛宻etter 鎶?GameStateUnavailableException");
addRow(apiRows, "GameMenu", "灞炴€ф彁鍙?, "PolarisAPI.Game.GameMenu.IsInputHandlingEnabled", "bool IsInputHandlingEnabled { get; set; }", "璇诲啓", "nel.gm.UiGameMenu", "static bool handle", "鐩存帴瀛楁璇诲啓", "鍏ㄥ眬", "P2", "楂?, "浠呭厑璁镐富绾跨▼鍐欏叆");

addRow(apiRows, "Events", "灞炴€ф彁鍙?, "PolarisAPI.Game.Events.IsMessageVisible", "bool IsMessageVisible { get; }", "鍙", "evt.EV", "static bool msg_active", "鍘熷瓧娈垫姇褰?, "鍏ㄥ眬", "P1", "浣?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃惰繑鍥?false");
addRow(apiRows, "Events", "灞炴€ф彁鍙?, "PolarisAPI.Game.Events.SkipMode", "int SkipMode { get; set; }", "璇诲啓", "evt.EV", "static int skipping", "鐩存帴瀛楁璇诲啓锛屼笉瑙ｉ噴浣?绛夌骇鍚箟", "鍏ㄥ眬", "P2", "楂?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃跺啓鍏ユ姏 GameApiNotReadyException");
addRow(apiRows, "Events", "灞炴€ф彁鍙?, "PolarisAPI.Game.Events.IsSkipDenied", "bool IsSkipDenied { get; set; }", "璇诲啓", "evt.EV", "static bool deny_skip", "鐩存帴瀛楁璇诲啓", "鍏ㄥ眬", "P2", "楂?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃跺啓鍏ユ姏 GameApiNotReadyException");
addRow(apiRows, "Events", "灞炴€ф彁鍙?, "PolarisAPI.Game.Events.IsAssetLoading", "bool IsAssetLoading { get; }", "鍙", "evt.EV", "static bool active_load", "鍘熷瓧娈垫姇褰?, "鍏ㄥ眬", "P1", "浣?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃惰繑鍥?false");
addRow(apiRows, "Events", "灞炴€ф彁鍙?, "PolarisAPI.Game.Events.IsPrepared", "bool IsPrepared { get; }", "鍙", "evt.EV", "static bool ev_prepared { get; }", "鍘熷睘鎬ф姇褰?, "鍏ㄥ眬", "P1", "浣?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃惰繑鍥?false");
addRow(apiRows, "Quests", "灞炴€ф彁鍙?, "PolarisAPI.Game.Quests.HasCaneQuest", "bool? HasCaneQuest { get; }", "鍙", "nel.QuestTracker", "bool has_cane_quest { get; }", "鍘熷睘鎬ф姇褰?, "瀛樻。", "P1", "浣?, "QuestTracker 涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "Quests", "灞炴€ф彁鍙?, "PolarisAPI.Game.Quests.NeedsHeadQuestRefresh", "bool? NeedsHeadQuestRefresh { get; set; }", "璇诲啓", "nel.QuestTracker", "bool need_fine_head_quest { get; set; }", "鐩存帴 getter/setter", "瀛樻。", "P2", "楂?, "QuestTracker 涓嶅瓨鍦ㄦ椂 setter 鎶?GameStateUnavailableException");

// 鏂规硶璋冪敤锛氬厑璁稿弬鏁?杩斿洖鍊奸€傞厤锛屼絾姣忎釜 API 鍙０鏄庝竴涓富瑕佸師濮嬭皟鐢ㄣ€?addRow(apiRows, "Localization", "鏂规硶璋冪敤", "PolarisAPI.Game.Localization.GetCurrentLocale", "string GetCurrentLocale()", "璋冪敤", "XX.TX", "static string getCurrentFamilyName()", "鍘熻繑鍥炲€肩洿鎺ヤ綔涓?locale code", "鍏ㄥ眬", "P0", "浣?, "鏂囨湰绯荤粺鏈垵濮嬪寲鏃舵姏 GameApiNotReadyException");
addRow(apiRows, "Localization", "鏂规硶璋冪敤", "PolarisAPI.Game.Localization.GetDefaultLocale", "string GetDefaultLocale()", "璋冪敤", "XX.TX", "static TXFamily getDefaultFamily()", "鍙姇褰?family key锛屼笉鏆撮湶 TXFamily", "鍏ㄥ眬", "P1", "浣?, "鏂囨湰绯荤粺鏈垵濮嬪寲鏃舵姏 GameApiNotReadyException");
addRow(apiRows, "Localization", "鏂规硶璋冪敤", "PolarisAPI.Game.Localization.ChangeLocale", "void ChangeLocale(string locale)", "璋冪敤", "XX.TX", "static void changeFamily(string fam)", "鍙傛暟鍘熸牱浼犲叆锛涗笉鑷姩淇濆瓨璁剧疆", "鍏ㄥ眬", "P1", "涓?, "绌哄瓧绗︿覆鎶?ArgumentException锛涙湭鐭?locale 娌跨敤鍘熸柟娉曡涓?);
addRow(apiRows, "Localization", "鏂规硶璋冪敤", "PolarisAPI.Game.Localization.IsCurrentLocale", "bool IsCurrentLocale(string locale)", "璋冪敤", "XX.TX", "static bool familyIs(string s)", "鍙傛暟鍘熸牱浼犲叆", "鍏ㄥ眬", "P1", "浣?, "null 鎶?ArgumentNullException");

addRow(apiRows, "Input", "鏂规硶璋冪敤", "PolarisAPI.Game.Input.IsHeld", "bool IsHeld(GameInputAction action)", "璋冪敤", "XX.IN", "is* / is*O method family", "GameInputAction 浠呴€夋嫨瀵瑰簲鐨勫師濮嬫煡璇㈡柟娉?, "鍏ㄥ眬", "P0", "涓?, "鏈煡 action 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Input", "鏂规硶璋冪敤", "PolarisAPI.Game.Input.WasPressed", "bool WasPressed(GameInputAction action, int bufferFrames = 1)", "璋冪敤", "XX.IN", "is*PD(int alloc_frame) method family", "鍙傛暟 action 閫夋嫨鍘熷 PD 鏂规硶锛沚ufferFrames 鐩存帴浼犲叆", "鍏ㄥ眬", "P0", "涓?, "bufferFrames < 0 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Input", "鏂规硶璋冪敤", "PolarisAPI.Game.Input.WasReleased", "bool WasReleased(GameInputAction action, int heldFrames = 0)", "璋冪敤", "XX.IN", "is*U(...) method family", "鍙傛暟 action 閫夋嫨鍘熷 U 鏂规硶锛涗笉鍚堟垚棰濆杈撳叆鐘舵€?, "鍏ㄥ眬", "P0", "涓?, "涓嶆敮鎸侀噴鏀炬煡璇㈢殑 action 杩斿洖 false");
addRow(apiRows, "Input", "鏂规硶璋冪敤", "PolarisAPI.Game.Input.GetDirection", "GameVector2 GetDirection()", "璋冪敤", "XX.IN", "static Vector2 getArrowHold()", "鍙仛 Vector2 绫诲瀷鎶曞奖", "鍏ㄥ眬", "P0", "浣?, "杈撳叆绯荤粺鏈垵濮嬪寲鏃惰繑鍥?GameVector2.Zero");
addRow(apiRows, "Input", "鏂规硶璋冪敤", "PolarisAPI.Game.Input.ClearState", "void ClearState(string key, bool onlyPressDown = true)", "璋冪敤", "XX.IN", "static void clearKeyState(string t, bool only_pushdown_clear)", "鍙傛暟鍘熸牱浼犲叆", "鍏ㄥ眬", "P2", "楂?, "绌?key 鎶?ArgumentException");

addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.GetMapTitle", "string? GetMapTitle()", "璋冪敤", "m2d.M2DBase", "string getMapTitle(Map2d map)", "褰撳墠鍦板浘瀹炰緥浣滀负鍙傛暟锛涜繑鍥炲€煎師鏍锋姇褰?, "鍦板浘", "P1", "浣?, "鏃犲綋鍓嶅湴鍥炬椂杩斿洖 null");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.GetMouseMapPosition", "GameVector2? GetMouseMapPosition()", "璋冪敤", "m2d.M2DBase", "Vector2 getMousePosToMapPos()", "鍙仛 Vector2 绫诲瀷鎶曞奖", "鍦板浘", "P1", "浣?, "鏃犲綋鍓嶅湴鍥炬椂杩斿洖 null");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.IsInCamera", "bool IsInCamera(float x, float y, float width, float height, float marginPixels = 0)", "璋冪敤", "m2d.Map2d", "bool isinCamera(float mapx, float mapy, float mapw, float maph, float extend_pixel)", "鍙傛暟涓€涓€瀵瑰簲", "鍦板浘", "P1", "浣?, "鏃犲綋鍓嶅湴鍥炬椂杩斿洖 false");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.FindMover", "GameCharacter? FindMover(string key)", "璋冪敤", "m2d.Map2d", "M2Mover getMoverByName(string k, bool no_error)", "鍥哄畾 no_error=true锛涜繑鍥炲璞＄櫥璁颁负鍙ユ焺", "鍦板浘", "P0", "浣?, "鎵句笉鍒版椂杩斿洖 null");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.CanPlayerAct", "bool CanPlayerAct()", "璋冪敤", "m2d.Map2d", "bool playerActionUseable()", "鍘熻繑鍥炲€兼姇褰?, "鍦板浘", "P0", "浣?, "鏃犲綋鍓嶅湴鍥炬椂杩斿洖 false");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.ChangeMap", "GameMap? ChangeMap(string mapKey)", "璋冪敤", "m2d.M2DBase", "Map2d changeMap(Map2d newMap)", "mapKey 鍙礋璐ｈВ鏋愬弬鏁帮紱涓昏璋冪敤涓?changeMap", "鍦板浘", "P2", "楂?, "鏈煡鍦板浘杩斿洖 null锛涗笉鑷姩澶勭悊浼犻€佺偣/浜嬩欢闃熷垪", "楂橀闄╋細璋冪敤鑰呰礋璐ｅ師鐗堟墍闇€鍓嶇疆鐘舵€?);
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.IsNight", "bool? IsNight()", "璋冪敤", "nel.NightController", "bool isNight()", "鍘熻繑鍥炲€兼姇褰?, "鍦板浘", "P0", "浣?, "NightController 涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.HasWeather", "bool? HasWeather(GameWeather weather)", "璋冪敤", "nel.NightController", "bool hasWeather(WeatherItem.WEATHER w)", "鍏叡鏋氫妇鏄犲皠鍒板師鏋氫妇", "鍦板浘", "P0", "涓?, "鏈煡鍏叡鏋氫妇鎶?ArgumentOutOfRangeException");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.GetDangerLevel", "float? GetDangerLevel()", "璋冪敤", "nel.NightController", "float getDangerLevel()", "鍘熻繑鍥炲€兼姇褰?, "鍦板浘", "P0", "浣?, "NightController 涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.GetDangerMeter", "int? GetDangerMeter(bool real = true, bool raw = false)", "璋冪敤", "nel.NightController", "int getDangerMeterVal(bool real, bool raw)", "鍙傛暟涓庤繑鍥炲€煎師鏍锋姇褰?, "鍦板浘", "P1", "浣?, "NightController 涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.GetDangerBonus", "int? GetDangerBonus()", "璋冪敤", "nel.NightController", "int getDangerAddedVal()", "鍘熻繑鍥炲€兼姇褰?, "鍦板浘", "P0", "浣?, "NightController 涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.SetDangerBonus", "void SetDangerBonus(int value)", "璋冪敤", "nel.NightController", "void setAdditionalDangerLevelManual(int v)", "鍙傛暟鍘熸牱浼犲叆", "鍦板浘", "P1", "楂?, "NightController 涓嶅瓨鍦ㄦ椂鎶?GameStateUnavailableException");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.ClearWeather", "void ClearWeather()", "璋冪敤", "nel.NightController", "void clearWeather()", "鐩存帴璋冪敤", "鍦板浘", "P2", "楂?, "NightController 涓嶅瓨鍦ㄦ椂鎶?GameStateUnavailableException");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.ShuffleWeather", "void ShuffleWeather()", "璋冪敤", "nel.NightController", "void weatherShuffle()", "鐩存帴璋冪敤", "鍦板浘", "P2", "楂?, "NightController 涓嶅瓨鍦ㄦ椂鎶?GameStateUnavailableException");
addRow(apiRows, "World", "鏂规硶璋冪敤", "PolarisAPI.Game.World.SetBattleCount", "void SetBattleCount(int value)", "璋冪敤", "nel.NightController", "void setBattleCount(int v)", "鍙傛暟鍘熸牱浼犲叆", "鍦板浘", "P2", "楂?, "value < 0 鎶?ArgumentOutOfRangeException");

addRow(apiRows, "Characters", "鏂规硶璋冪敤", "PolarisAPI.Game.Characters.Teleport", "GameCharacter Teleport(GameCharacter target, GameVector2 position)", "璋冪敤", "m2d.M2Mover", "M2Mover setTo(float x, float y)", "浣嶇疆鍒嗛噺鐩存帴浼犲叆锛涜繑鍥炲師瀵硅薄瀵瑰簲鍙ユ焺", "瑙掕壊鍙ユ焺", "P0", "楂?, "鏃犳晥/杩囨湡鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Characters", "鏂规硶璋冪敤", "PolarisAPI.Game.Characters.MoveBy", "bool MoveBy(GameCharacter target, GameVector2 delta, bool recheckFoot = true)", "璋冪敤", "m2d.M2Mover", "bool moveBy(float map_dx, float map_dy, bool recheck_foot)", "鍙傛暟涓€涓€瀵瑰簲", "瑙掕壊鍙ユ焺", "P1", "涓?, "鏃犳晥/杩囨湡鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Characters", "鏂规硶璋冪敤", "PolarisAPI.Game.Characters.SetVelocity", "void SetVelocity(GameCharacter target, GameVector2 velocity)", "璋冪敤", "m2d.M2Mover", "void setVelocityForce(float vx, float vy)", "閫熷害鍒嗛噺鐩存帴浼犲叆", "瑙掕壊鍙ユ焺", "P1", "楂?, "鏃犳晥/杩囨湡鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Characters", "鏂规硶璋冪敤", "PolarisAPI.Game.Characters.SetFacing", "GameCharacter SetFacing(GameCharacter target, GameFacing facing, bool forceSprite = false)", "璋冪敤", "m2d.M2Mover", "M2Mover setAim(AIM n, bool sprite_force_aim_set)", "鍏叡鏋氫妇鏄犲皠鍒?AIM", "瑙掕壊鍙ユ焺", "P1", "涓?, "Unknown 涓嶅厑璁镐紶鍏?);
addRow(apiRows, "Combat", "鏂规硶璋冪敤", "PolarisAPI.Game.Combat.HealHp", "void HealHp(GameCharacter target, int amount)", "璋冪敤", "m2d.M2Attackable", "void cureHp(int val)", "amount 鍘熸牱浼犲叆", "瑙掕壊鍙ユ焺", "P0", "涓?, "amount < 0 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Combat", "鏂规硶璋冪敤", "PolarisAPI.Game.Combat.HealMp", "void HealMp(GameCharacter target, int amount)", "璋冪敤", "m2d.M2Attackable", "void cureMp(int val)", "amount 鍘熸牱浼犲叆", "瑙掕壊鍙ユ焺", "P0", "涓?, "amount < 0 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Combat", "鏂规硶璋冪敤", "PolarisAPI.Game.Combat.DamageHp", "int DamageHp(GameCharacter target, int amount, bool force = false)", "璋冪敤", "m2d.M2Attackable", "int applyHpDamage(int val, bool force, AttackInfo atk)", "v1 鍥哄畾 atk=null锛涜繑鍥炲疄闄呯粨鏋?, "瑙掕壊鍙ユ焺", "P1", "楂?, "闈?Attackable 鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Combat", "鏂规硶璋冪敤", "PolarisAPI.Game.Combat.DamageMp", "int DamageMp(GameCharacter target, int amount, bool force = false)", "璋冪敤", "m2d.M2Attackable", "int applyMpDamage(int val, bool force, AttackInfo atk)", "v1 鍥哄畾 atk=null锛涜繑鍥炲疄闄呯粨鏋?, "瑙掕壊鍙ユ焺", "P1", "楂?, "闈?Attackable 鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Player", "鏂规硶璋冪敤", "PolarisAPI.Game.Player.ChangeState", "void ChangeState(GamePlayerState state)", "璋冪敤", "nel.PR", "void changeState(PR.STATE state)", "鍏叡鏋氫妇鏄犲皠鍒板師鏋氫妇", "鐜╁", "P2", "楂?, "鐜╁涓嶅瓨鍦ㄦ垨 Unknown 鐘舵€佹椂鎶涘紓甯?);
addRow(apiRows, "Player", "鏂规硶璋冪敤", "PolarisAPI.Game.Player.IsNormalState", "bool? IsNormalState()", "璋冪敤", "nel.PR", "bool isNormalState()", "鍘熻繑鍥炲€兼姇褰?, "鐜╁", "P0", "浣?, "鐜╁涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "Player", "鏂规硶璋冪敤", "PolarisAPI.Game.Player.IsMagicState", "bool? IsMagicState()", "璋冪敤", "nel.PR", "bool isMagicState()", "鍘熻繑鍥炲€兼姇褰?, "鐜╁", "P1", "浣?, "鐜╁涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "Enemies", "鏂规硶璋冪敤", "PolarisAPI.Game.Enemies.ChangeState", "GameCharacter ChangeState(GameCharacter target, GameEnemyState state)", "璋冪敤", "nel.NelEnemy", "NelEnemy changeState(NelEnemy.STATE st)", "鍏叡鏋氫妇鏄犲皠锛涜繑鍥炲悓涓€瀵硅薄鍙ユ焺", "瑙掕壊鍙ユ焺", "P2", "楂?, "鐩爣涓嶆槸 NelEnemy 鎴?Unknown 鐘舵€佹椂鎶涘紓甯?);
addRow(apiRows, "Enemies", "鏂规硶璋冪敤", "PolarisAPI.Game.Enemies.ApplyDamage", "int ApplyDamage(GameCharacter target, EnemyDamageRequest request)", "璋冪敤", "nel.NelEnemy", "int applyDamage(NelAttackInfo atk, bool force)", "鍏叡璇锋眰瀵硅薄鎶曞奖涓?NelAttackInfo", "瑙掕壊鍙ユ焺", "P2", "楂?, "鏃犳硶鏋勯€犲悎娉曟敾鍑讳俊鎭椂鎶?GameArgumentMappingException");
addRow(apiRows, "Enemies", "鏂规硶璋冪敤", "PolarisAPI.Game.Enemies.AddKnockback", "void AddKnockback(GameCharacter target, float velocity, KnockbackRequest request)", "璋冪敤", "nel.NelEnemy", "void addKnockbackVelocity(float v0, AttackInfo atk, M2Attackable another, FOC_TYPE type)", "璇锋眰瀵硅薄浠呰礋璐ｅ弬鏁版槧灏?, "瑙掕壊鍙ユ焺", "P2", "楂?, "璇锋眰瀛楁涓嶅畬鏁存椂鎶?GameArgumentMappingException");

addRow(apiRows, "Items", "鏂规硶璋冪敤", "PolarisAPI.Game.Items.Resolve", "GameItem? Resolve(string itemKey)", "璋冪敤", "nel.NelItem", "static NelItem GetById(string item_key, bool no_error)", "鍥哄畾 no_error=true锛涜繑鍥炲璞＄櫥璁颁负鍙ユ焺", "鍏ㄥ眬", "P0", "浣?, "鎵句笉鍒版椂杩斿洖 null");
addRow(apiRows, "Items", "鏂规硶璋冪敤", "PolarisAPI.Game.Items.GetLocalizedName", "string GetLocalizedName(GameItem item, int grade = 0)", "璋冪敤", "nel.NelItem", "string getLocalizedName(int grade)", "grade 鐩存帴浼犲叆", "鐗╁搧鍙ユ焺", "P0", "浣?, "grade 瓒呭嚭鍏叡鑼冨洿鏃舵姏 ArgumentOutOfRangeException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.GetMain", "GameStorage GetMain()", "璋冪敤", "nel.NelItemManager", "ItemStorage getInventory()", "杩斿洖瀵硅薄鐧昏涓哄瓨鍌ㄥ彞鏌?, "瀛樻。", "P0", "浣?, "鐗╁搧绠＄悊鍣ㄤ笉瀛樺湪鏃舵姏 GameStateUnavailableException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.GetPrecious", "GameStorage GetPrecious()", "璋冪敤", "nel.NelItemManager", "ItemStorage getInventoryPrecious()", "杩斿洖瀵硅薄鐧昏涓哄瓨鍌ㄥ彞鏌?, "瀛樻。", "P1", "浣?, "鐗╁搧绠＄悊鍣ㄤ笉瀛樺湪鏃舵姏 GameStateUnavailableException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.GetEnhancer", "GameStorage GetEnhancer()", "璋冪敤", "nel.NelItemManager", "ItemStorage getInventoryEnhancer()", "杩斿洖瀵硅薄鐧昏涓哄瓨鍌ㄥ彞鏌?, "瀛樻。", "P1", "浣?, "鐗╁搧绠＄悊鍣ㄤ笉瀛樺湪鏃舵姏 GameStateUnavailableException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.GetHouse", "GameStorage GetHouse()", "璋冪敤", "nel.NelItemManager", "ItemStorage getHouseInventory()", "杩斿洖瀵硅薄鐧昏涓哄瓨鍌ㄥ彞鏌?, "瀛樻。", "P1", "浣?, "鐗╁搧绠＄悊鍣ㄤ笉瀛樺湪鏃舵姏 GameStateUnavailableException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.Count", "int Count(GameStorage storage, GameItem item, int grade = -1)", "璋冪敤", "nel.ItemStorage", "int getCount(NelItem item, int grade)", "鍙ユ焺瑙ｆ瀽鍚庡弬鏁颁竴涓€瀵瑰簲", "瀛樺偍鍙ユ焺", "P0", "浣?, "grade < -1 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.CanAdd", "int CanAdd(GameItem item, int count, bool addRow = true)", "璋冪敤", "nel.NelItemManager", "int canAddItem(NelItem item, int count, bool add_row)", "杩斿洖鍙姞鍏ユ暟閲?, "瀛樻。", "P0", "浣?, "count < 0 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.Add", "int Add(GameStorage storage, GameItem item, int count, int grade = 0, bool execute = true)", "璋冪敤", "nel.ItemStorage", "int Add(NelItem item, int count, int grade, bool add_row, bool execute)", "v1 鍥哄畾 add_row=true锛涘叾浣欏弬鏁扮洿鎺ヤ紶鍏?, "瀛樺偍鍙ユ焺", "P0", "涓?, "璐熸暟閲忔垨鏃犳晥鍙ユ焺鎶涘紓甯?);
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.Reduce", "bool Reduce(GameStorage storage, GameItem item, int count, int grade = 0)", "璋冪敤", "nel.ItemStorage", "bool Reduce(NelItem item, int count, int grade, bool fine_row)", "v1 鍥哄畾 fine_row=true", "瀛樺偍鍙ユ焺", "P0", "涓?, "璐熸暟閲忔垨鏃犳晥鍙ユ焺鎶涘紓甯?);
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.Clear", "void Clear(GameStorage storage, int newCapacity)", "璋冪敤", "nel.ItemStorage", "ItemStorage clearAllItems(int max)", "newCapacity 鐩存帴浼犲叆锛涘拷鐣ヨ繑鍥炵殑鍚屼竴瀵硅薄", "瀛樺偍鍙ユ焺", "P2", "楂?, "newCapacity < 0 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.Use", "int Use(GameItem item, GameCharacter user, GameStorage storage, int grade = 0)", "璋冪敤", "nel.NelItem", "int Use(PR user, ItemStorage storage, int grade, IItemUser context)", "v1 鍥哄畾 context=null锛涗粎鍏佽 PR 浣滀负 user", "瀛樻。", "P2", "楂?, "闈炵帺瀹?user 鎴栫墿鍝佷笉鍙敤鏃舵姏 GameCallRejectedException");
addRow(apiRows, "Inventory", "鏂规硶璋冪敤", "PolarisAPI.Game.Inventory.Drop", "GameDrop Drop(GameItem item, int count, int grade, GameVector2 position, GameVector2 velocity)", "璋冪敤", "nel.NelItemManager", "NelItemDrop dropManual(...)", "浣嶇疆/閫熷害鍒嗛噺鏄犲皠锛涘叾浠栧師鍙傛暟浣跨敤瑙勮寖榛樿鍊?, "鍦板浘", "P2", "楂?, "鏃犲綋鍓嶅湴鍥炬垨鏁伴噺闈炴硶鏃舵姏寮傚父");

addRow(apiRows, "Economy", "鏂规硶璋冪敤", "PolarisAPI.Game.Economy.GetAmount", "uint GetAmount(GameCurrency currency = GameCurrency.Gold)", "璋冪敤", "nel.CoinStorage", "static uint getCount(CTYPE currency)", "鍏叡鏋氫妇鏄犲皠鍒?CTYPE", "瀛樻。", "P0", "浣?, "Unknown currency 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Economy", "鏂规硶璋冪敤", "PolarisAPI.Game.Economy.Add", "void Add(int amount, GameCurrency currency = GameCurrency.Gold)", "璋冪敤", "nel.CoinStorage", "static void addCount(int value, CTYPE currency, bool callBinder)", "v1 鍥哄畾 callBinder=true", "瀛樻。", "P1", "涓?, "amount < 0 鎶?ArgumentOutOfRangeException");
addRow(apiRows, "Economy", "鏂规硶璋冪敤", "PolarisAPI.Game.Economy.Spend", "void Spend(int amount, GameCurrency currency = GameCurrency.Gold)", "璋冪敤", "nel.CoinStorage", "static void reduceCount(int value, CTYPE currency, bool callBinder)", "v1 鍥哄畾 callBinder=true锛涗笉棰勬鏌ヤ綑棰?, "瀛樻。", "P1", "楂?, "amount < 0 鎶涘紓甯革紱浣欓涓嶈冻娌跨敤鍘熸柟娉曡涓?);

addRow(apiRows, "Audio/BGM", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Bgm.Load", "bool Load(string timing, string cue, bool suppressOverloadError = false)", "璋冪敤", "XX.BGM", "static bool load(string timing, string cue_key, bool no_overload_error)", "鍙傛暟涓€涓€瀵瑰簲", "鍏ㄥ眬", "P1", "涓?, "绌?cue 鎶?ArgumentException");
addRow(apiRows, "Audio/BGM", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Bgm.Play", "void Play(float fadeInFrames = 0)", "璋冪敤", "XX.BGM", "static void play(float fadein_maxt)", "甯ф暟鍘熸牱浼犲叆", "鍏ㄥ眬", "P0", "浣?, "璐熷抚鏁版姏 ArgumentOutOfRangeException");
addRow(apiRows, "Audio/BGM", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Bgm.Stop", "void Stop(bool temporary = false, bool immediate = false)", "璋冪敤", "XX.BGM", "static void stop(bool temporary, bool immediate_run)", "鍙傛暟涓€涓€瀵瑰簲", "鍏ㄥ眬", "P0", "涓?, "闊抽绯荤粺鏈氨缁椂鎶?GameApiNotReadyException");
addRow(apiRows, "Audio/BGM", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Bgm.FadeIn", "void FadeIn(float target = 1, float frames = 120)", "璋冪敤", "XX.BGM", "static void fadein(float dep_to, float maxt)", "鍙傛暟涓€涓€瀵瑰簲", "鍏ㄥ眬", "P1", "浣?, "target 涓嶅湪 0..1 鎴?frames < 0 鏃舵姏寮傚父");
addRow(apiRows, "Audio/BGM", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Bgm.FadeOut", "void FadeOut(float target = 0, float frames = 120, bool autoUnload = false)", "璋冪敤", "XX.BGM", "static void fadeout(float dep_to, float maxt, bool auto_unload)", "鍙傛暟涓€涓€瀵瑰簲", "鍏ㄥ眬", "P1", "浣?, "target 涓嶅湪 0..1 鎴?frames < 0 鏃舵姏寮傚父");
addRow(apiRows, "Audio/BGM", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Bgm.Replace", "void Replace(float fadeOutFrames, float fadeInFrames, bool autoUnload = true, bool suppressError = false)", "璋冪敤", "XX.BGM", "static void replace(float fadeout_time, float fadein_time, bool auto_unload, bool no_error)", "鍙傛暟涓€涓€瀵瑰簲", "鍏ㄥ眬", "P2", "涓?, "璐熷抚鏁版姏 ArgumentOutOfRangeException");
addRow(apiRows, "Audio/BGM", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Bgm.IsPlaying", "bool IsPlaying()", "璋冪敤", "XX.BGM", "static bool isFrontPlaying()", "鍘熻繑鍥炲€兼姇褰?, "鍏ㄥ眬", "P0", "浣?, "闊抽绯荤粺鏈氨缁椂杩斿洖 false");
addRow(apiRows, "Audio/BGM", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Bgm.GetCurrentTrack", "GameBgmTrack? GetCurrentTrack()", "璋冪敤", "XX.BGM", "static void getFrontBgm(out string timing, out string cue)", "涓や釜 out 鍙傛暟鎶曞奖涓哄彧璇?DTO", "鍏ㄥ眬", "P1", "浣?, "鏃犳椿鍔?BGM 鏃惰繑鍥?null");
addRow(apiRows, "Audio/SFX", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Play", "GameAudioPlayback? Play(string cue, bool force = false)", "璋冪敤", "XX.SndPlayer", "bool play(string cue_name, bool force)", "API 灞傚垎閰嶆挱鏀惧櫒骞朵互鍙ユ焺鎸佹湁锛涗富瑕佽皟鐢ㄤ负 play", "鍏ㄥ眬", "P0", "涓?, "鍘熸柟娉曡繑鍥?false 鏃惰繑鍥?null");
addRow(apiRows, "Audio/SFX", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Stop", "void Stop(GameAudioPlayback audio)", "璋冪敤", "XX.SndPlayer", "void Stop()", "鍙ユ焺瑙ｆ瀽鍚庣洿鎺ヨ皟鐢?, "闊抽鍙ユ焺", "P0", "浣?, "鏃犳晥/杩囨湡鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Audio/SFX", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.Pause", "void Pause(GameAudioPlayback audio)", "璋冪敤", "XX.SndPlayer", "void Pause()", "鍙ユ焺瑙ｆ瀽鍚庣洿鎺ヨ皟鐢?, "闊抽鍙ユ焺", "P1", "浣?, "鏃犳晥/杩囨湡鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Audio/SFX", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.IsPlaying", "bool? IsPlaying(GameAudioPlayback audio)", "璋冪敤", "XX.SndPlayer", "bool isPlaying()", "鍘熻繑鍥炲€兼姇褰?, "闊抽鍙ユ焺", "P0", "浣?, "鏃犳晥/杩囨湡鍙ユ焺杩斿洖 null");
addRow(apiRows, "Audio/SFX", "鏂规硶璋冪敤", "PolarisAPI.Game.Audio.SetAisac", "void SetAisac(GameAudioPlayback audio, string control, float value)", "璋冪敤", "XX.SndPlayer", "void SetAisacControl(string controlName, float value)", "鍙傛暟鍘熸牱浼犲叆", "闊抽鍙ユ焺", "P2", "涓?, "绌?control 鎴栨棤鏁堝彞鏌勬姏寮傚父");

addRow(apiRows, "GameMenu", "鏂规硶璋冪敤", "PolarisAPI.Game.GameMenu.Open", "void Open()", "璋冪敤", "nel.gm.UiGameMenu", "UiBoxDesignerFamily activate()", "蹇界暐鍐呴儴 UI 杩斿洖瀵硅薄", "鑿滃崟", "P0", "涓?, "鑿滃崟瀹炰緥涓嶅瓨鍦ㄦ椂鎶?GameStateUnavailableException");
addRow(apiRows, "GameMenu", "鏂规硶璋冪敤", "PolarisAPI.Game.GameMenu.Close", "void Close(bool immediate = false)", "璋冪敤", "nel.gm.UiGameMenu", "UiBoxDesignerFamily deactivate(bool immediate)", "鍙傛暟鐩存帴浼犲叆锛涘拷鐣ュ唴閮?UI 杩斿洖瀵硅薄", "鑿滃崟", "P0", "涓?, "鑿滃崟瀹炰緥涓嶅瓨鍦ㄦ椂鎶?GameStateUnavailableException");
addRow(apiRows, "GameMenu", "鏂规硶璋冪敤", "PolarisAPI.Game.GameMenu.IsClosing", "bool? IsClosing()", "璋冪敤", "nel.gm.UiGameMenu", "bool isClosingGame()", "鍘熻繑鍥炲€兼姇褰?, "鑿滃崟", "P1", "浣?, "鑿滃崟瀹炰緥涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "GameMenu", "鏂规硶璋冪敤", "PolarisAPI.Game.GameMenu.IsStoppingWorld", "bool? IsStoppingWorld()", "璋冪敤", "nel.gm.UiGameMenu", "bool isStoppingGame()", "鍘熻繑鍥炲€兼姇褰?, "鑿滃崟", "P1", "浣?, "鑿滃崟瀹炰緥涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "GameMenu", "鏂规硶璋冪敤", "PolarisAPI.Game.GameMenu.IsBenchMenuActive", "bool? IsBenchMenuActive(bool ignoreTemporaryWait = true)", "璋冪敤", "nel.gm.UiGameMenu", "bool isBenchMenuActive(bool not_temporary_waiting)", "鍙傛暟涓€涓€瀵瑰簲", "鑿滃崟", "P1", "浣?, "鑿滃崟瀹炰緥涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "GameMenu", "鏂规硶璋冪敤", "PolarisAPI.Game.GameMenu.IsEditingCategory", "bool? IsEditingCategory()", "璋冪敤", "nel.gm.UiGameMenu", "bool isEditState()", "鍘熻繑鍥炲€兼姇褰?, "鑿滃崟", "P1", "浣?, "鑿滃崟瀹炰緥涓嶅瓨鍦ㄦ椂杩斿洖 null");

addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.IsActive", "bool IsActive(bool includeLoading = true)", "璋冪敤", "evt.EV", "static bool isActive(bool no_consider_loading)", "浼犲叆 !includeLoading", "鍏ㄥ眬", "P0", "浣?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃惰繑鍥?false");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.GetCurrent", "GameEvent? GetCurrent()", "璋冪敤", "evt.EV", "static EvReader getCurrentEvent()", "杩斿洖瀵硅薄鐧昏涓轰簨浠跺彞鏌?, "浜嬩欢", "P1", "浣?, "鏃犲綋鍓嶄簨浠舵椂杩斿洖 null");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.Start", "GameEvent? Start(string name, int startLine = 0, int position = -1, IReadOnlyList<string>? variables = null)", "璋冪敤", "evt.EV", "static EvReader stack(string name, int start_line, int push_to, string[] variables, EvReader cloneFrom)", "鍥哄畾 cloneFrom=null锛涘叾浣欏弬鏁版槧灏?, "浜嬩欢", "P2", "楂?, "浜嬩欢涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.Change", "bool Change(string name, int startLine = 0, IReadOnlyList<string>? variables = null)", "璋冪敤", "evt.EV", "static bool changeEvent(string eventName, int startLine, string[] variables)", "鍙傛暟鏄犲皠", "浜嬩欢", "P2", "楂?, "绌?name 鎶?ArgumentException");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.Stop", "bool Stop(bool all = false)", "璋冪敤", "evt.EV", "static bool evEnd(bool all)", "鍙傛暟鐩存帴浼犲叆", "浜嬩欢", "P2", "楂?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃惰繑鍥?false");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.GetContent", "string? GetContent(string name)", "璋冪敤", "evt.EV", "static bool getEventContent(string name, EvReader reader)", "API 灞傚垱寤轰复鏃?reader 骞惰鍙栧叾鍐呭锛涗富瑕佽皟鐢ㄤ繚鎸佷笉鍙?, "鍏ㄥ眬", "P2", "涓?, "涓嶅瓨鍦ㄦ椂杩斿洖 null");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.SetContent", "void SetContent(string name, string content)", "璋冪敤", "evt.EV", "static void setEventContent(string name, string text)", "鍙傛暟鍘熸牱浼犲叆", "鍏ㄥ眬", "P2", "楂?, "绌?name 鎶?ArgumentException");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.IsMessageWaiting", "bool IsMessageWaiting()", "璋冪敤", "evt.EV", "static bool isMessageWaiting()", "鍘熻繑鍥炲€兼姇褰?, "浜嬩欢", "P1", "浣?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃惰繑鍥?false");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.CanProgress", "bool CanProgress()", "璋冪敤", "evt.EV", "static bool canProgress()", "鍘熻繑鍥炲€兼姇褰?, "浜嬩欢", "P1", "浣?, "浜嬩欢绯荤粺鏈垵濮嬪寲鏃惰繑鍥?false");
addRow(apiRows, "Events", "鏂规硶璋冪敤", "PolarisAPI.Game.Events.StopGameLoop", "void StopGameLoop(bool stop)", "璋冪敤", "evt.EV", "static void stopGMain(bool flag)", "鍙傛暟鐩存帴浼犲叆", "鍏ㄥ眬", "P2", "楂?, "浠呭厑璁镐富绾跨▼璋冪敤锛涜皟鐢ㄨ€呭繀椤昏礋璐ｆ仮澶?);

addRow(apiRows, "Quests", "鏂规硶璋冪敤", "PolarisAPI.Game.Quests.Get", "GameQuest? Get(string questKey)", "璋冪敤", "nel.QuestTracker", "Quest Get(string key, bool no_error)", "鍥哄畾 no_error=true锛涜繑鍥炲璞＄櫥璁颁负鍙ユ焺", "瀛樻。", "P1", "浣?, "鎵句笉鍒版椂杩斿洖 null");
addRow(apiRows, "Quests", "鏂规硶璋冪敤", "PolarisAPI.Game.Quests.GetProgress", "int? GetProgress(string questKey, bool includeFinished = true)", "璋冪敤", "nel.QuestTracker", "int getProgress(string quest_key, bool include_finished)", "鍙傛暟涓€涓€瀵瑰簲", "瀛樻。", "P1", "浣?, "QuestTracker 涓嶅瓨鍦ㄦ垨浠诲姟鏈煡鏃惰繑鍥?null");
addRow(apiRows, "Quests", "鏂规硶璋冪敤", "PolarisAPI.Game.Quests.GetHead", "QuestProgressView? GetHead()", "璋冪敤", "nel.QuestTracker", "QuestProgress getHeadQuest()", "杩斿洖鍐呴儴瀵硅薄鐨勫彧璇?DTO 鎶曞奖", "瀛樻。", "P1", "涓?, "鏃犻瑕佷换鍔℃椂杩斿洖 null");
addRow(apiRows, "Quests", "鏂规硶璋冪敤", "PolarisAPI.Game.Quests.Update", "void Update(string questKey, int phase, QuestUpdateOptions options = default)", "璋冪敤", "nel.QuestTracker", "void updateQuest(string key, int phase, bool hidden, bool fillTarget, bool focus, bool fixPhase, bool progressTask)", "options 瀛楁涓€涓€鏄犲皠", "瀛樻。", "P2", "楂?, "鏈煡浠诲姟鎴?phase 闈炴硶鏃舵姏 GameCallRejectedException");
addRow(apiRows, "Quests", "鏂规硶璋冪敤", "PolarisAPI.Game.Quests.Remove", "void Remove(string questKey, bool considerFinished = true)", "璋冪敤", "nel.QuestTracker", "void remove(string key, bool consider_finished)", "鍙傛暟涓€涓€瀵瑰簲", "瀛樻。", "P2", "楂?, "绌?key 鎶?ArgumentException");
addRow(apiRows, "Quests", "鏂规硶璋冪敤", "PolarisAPI.Game.Quests.SetFocused", "void SetFocused(GameQuestProgress progress)", "璋冪敤", "nel.QuestTracker", "void setFocusedQuest(QuestProgress progress)", "鍙ユ焺瑙ｆ瀽鍚庣洿鎺ヤ紶鍏?, "瀛樻。", "P2", "楂?, "鏃犳晥鍙ユ焺鎶?InvalidGameHandleException");
addRow(apiRows, "Quests", "鏂规硶璋冪敤", "PolarisAPI.Game.Quests.IsTargetItem", "bool IsTargetItem(GameItem item, int grade = 0)", "璋冪敤", "nel.QuestTracker", "bool isQuestTargetItem(NelItem item, int grade)", "鍙ユ焺瑙ｆ瀽鍚庡弬鏁颁竴涓€瀵瑰簲", "瀛樻。", "P1", "浣?, "QuestTracker 涓嶅瓨鍦ㄦ椂杩斿洖 false");

titleBand(catalog, "Polaris 娓告垙 API 瑙勮寖锝渧1 鍊欓€夌洰褰?, "鏈〃鍙厑璁镐袱绉?API锛氬睘鎬ф彁鍙栦笌鏂规硶璋冪敤銆傝繖閲屾槸璁捐瑙勮寖锛屼笉鍖呭惈瀹炵幇鐘舵€併€佸彲璺戦€氭€с€佸洖璋冦€丼ignal 鎴?Capability銆?, "O");

catalog.getRange("A3:O3").values = [["鐩綍鏉＄洰", apiRows.length, "灞炴€ф彁鍙?, null, "鏂规硶璋冪敤", null, "P0", null, "P1", null, "P2", null, "涓荤嚎绋嬬害鏉?, "鍏ㄩ儴", null]];
catalog.getRange("B3").formulas = [[`=COUNTA(A6:A${apiRows.length + 5})`]];
catalog.getRange("D3").formulas = [[`=COUNTIF(C6:C${apiRows.length + 5},\"灞炴€ф彁鍙朶")`]];
catalog.getRange("F3").formulas = [[`=COUNTIF(C6:C${apiRows.length + 5},\"鏂规硶璋冪敤\")`]];
catalog.getRange("H3").formulas = [[`=COUNTIF(L6:L${apiRows.length + 5},\"P0\")`]];
catalog.getRange("J3").formulas = [[`=COUNTIF(L6:L${apiRows.length + 5},\"P1\")`]];
catalog.getRange("L3").formulas = [[`=COUNTIF(L6:L${apiRows.length + 5},\"P2\")`]];
catalog.getRange("A3:O3").format = {
  fill: C.neutral,
  font: { bold: true, color: C.muted },
  borders: { preset: "outside", style: "thin", color: C.border },
  verticalAlignment: "center",
};
for (const cell of ["B3", "D3", "F3", "H3", "J3", "L3", "N3"]) {
  catalog.getRange(cell).format = { fill: "#FFFFFF", font: { bold: true, color: C.title2 }, horizontalAlignment: "center" };
}
catalog.getRange("3:3").format.rowHeight = 24;

const headers = [["缂栧彿", "瀛愮郴缁?, "API绫诲瀷", "鍏叡API", "鍏叡绛惧悕", "璁块棶", "鍐呴儴绫诲瀷", "鍐呴儴鎴愬憳", "鏄犲皠瑙勫垯", "鐢熷懡鍛ㄦ湡", "绾跨▼", "浼樺厛绾?, "椋庨櫓", "閿欒 / 绌哄€肩害瀹?, "澶囨敞"]];
catalog.getRange("A5:O5").values = headers;
catalog.getRange(`A6:O${apiRows.length + 5}`).values = apiRows;
catalog.getRange("A5:O5").format = {
  fill: C.header,
  font: { bold: true, color: C.headerText },
  horizontalAlignment: "center",
  verticalAlignment: "center",
  wrapText: true,
  borders: { preset: "outside", style: "thin", color: C.title2 },
};
catalog.getRange("5:5").format.rowHeight = 32;
catalog.getRange(`A6:O${apiRows.length + 5}`).format = {
  font: { color: C.text, fontSize: 9 },
  verticalAlignment: "top",
  wrapText: true,
  borders: { insideHorizontal: { style: "thin", color: "#E2E8F0" } },
};
catalog.getRange(`A6:A${apiRows.length + 5}`).format.horizontalAlignment = "center";
catalog.getRange(`C6:C${apiRows.length + 5}`).format.font = { bold: true };
catalog.getRange(`F6:F${apiRows.length + 5}`).format.horizontalAlignment = "center";
catalog.getRange(`J6:M${apiRows.length + 5}`).format.horizontalAlignment = "center";

catalog.getRange(`A6:O${apiRows.length + 5}`).conditionalFormats.addCustom('=$C6="灞炴€ф彁鍙?', { fill: C.property });
catalog.getRange(`A6:O${apiRows.length + 5}`).conditionalFormats.addCustom('=$C6="鏂规硶璋冪敤"', { fill: C.method });
catalog.getRange(`M6:M${apiRows.length + 5}`).conditionalFormats.add("containsText", { text: "楂?, format: { fill: C.high, font: { bold: true, color: "#991B1B" } } });
catalog.getRange(`C6:C${apiRows.length + 5}`).dataValidation = { rule: { type: "list", values: ["灞炴€ф彁鍙?, "鏂规硶璋冪敤"] } };
catalog.getRange(`F6:F${apiRows.length + 5}`).dataValidation = { rule: { type: "list", values: ["鍙", "璇诲啓", "璋冪敤"] } };
catalog.getRange(`L6:L${apiRows.length + 5}`).dataValidation = { rule: { type: "list", values: ["P0", "P1", "P2"] } };
catalog.getRange(`M6:M${apiRows.length + 5}`).dataValidation = { rule: { type: "list", values: ["浣?, "涓?, "楂?] } };

const catalogTable = catalog.tables.add(`A5:O${apiRows.length + 5}`, true, "ApiCatalogV1");
catalogTable.style = "TableStyleMedium4";
catalogTable.showBandedRows = false;
catalogTable.showFilterButton = true;
catalog.freezePanes.freezeRows(5);
catalog.freezePanes.freezeColumns(4);

const widths = { A: 7, B: 15, C: 12, D: 38, E: 46, F: 9, G: 22, H: 44, I: 44, J: 11, K: 9, L: 9, M: 8, N: 44, O: 34 };
for (const [col, width] of Object.entries(widths)) catalog.getRange(`${col}:${col}`).format.columnWidth = width;
catalog.getRange(`6:${apiRows.length + 5}`).format.rowHeight = 38;

// 瑙勮寖鎬昏
titleBand(overview, "Polaris 娓告垙 API 瑙勮寖锝滅涓€鐗?, "鐩爣锛氫负 Alice In Cradle v0.29 寤虹珛绋冲畾銆佸彲瀹￠槄鐨勫叕寮€琛ㄩ潰銆侫PI 鍙湁鈥滃睘鎬ф彁鍙栤€濆拰鈥滄柟娉曡皟鐢ㄢ€濅袱绫汇€?, "H");
overview.getRange("A4:B9").values = [
  ["鎸囨爣", "鏁伴噺"],
  ["鐩綍鎬绘潯鐩?, null],
  ["灞炴€ф彁鍙?, null],
  ["鏂规硶璋冪敤", null],
  ["P0 鏍稿績", null],
  ["P2 楂樻潈闄?, null],
];
overview.getRange("B5").formulas = [["=COUNTA('API鐩綍_v1'!$A$6:$A$205)"]];
overview.getRange("B6").formulas = [["=COUNTIF('API鐩綍_v1'!$C$6:$C$205,\"灞炴€ф彁鍙朶")"]];
overview.getRange("B7").formulas = [["=COUNTIF('API鐩綍_v1'!$C$6:$C$205,\"鏂规硶璋冪敤\")"]];
overview.getRange("B8").formulas = [["=COUNTIF('API鐩綍_v1'!$L$6:$L$205,\"P0\")"]];
overview.getRange("B9").formulas = [["=COUNTIF('API鐩綍_v1'!$L$6:$L$205,\"P2\")"]];
overview.getRange("A4:B4").format = { fill: C.header, font: { bold: true, color: C.headerText }, horizontalAlignment: "center" };
overview.getRange("A5:A9").format = { fill: C.neutral, font: { bold: true, color: C.muted } };
overview.getRange("B5:B9").format = { fill: "#FFFFFF", font: { bold: true, color: C.title2, fontSize: 13 }, horizontalAlignment: "center", numberFormat: "#,##0" };
overview.getRange("A4:B9").format.borders = { preset: "outside", style: "thin", color: C.border };

overview.mergeCells("D4:H4");
overview.getRange("D4").values = [["涓ょ被 API 鐨勮竟鐣?]];
overview.getRange("D4:H4").format = { fill: C.header, font: { bold: true, color: C.headerText }, horizontalAlignment: "center" };
overview.getRange("D5:H8").values = [
  ["灞炴€ф彁鍙?, "鍏紑涓€涓師濮嬪瓧娈垫垨灞炴€э紱榛樿鍙锛涗笉鍋氳仛鍚堛€佷笉瑙﹀彂鍔ㄤ綔銆佷笉浼鏂规硶璋冪敤銆?, null, null, null],
  ["鏂规硶璋冪敤", "璋冪敤涓€涓富瑕佸師濮嬫柟娉曪紱鍏佽鍙傛暟鏋氫妇鏄犲皠銆佸彞鏌勮В鏋愬拰杩斿洖鍊兼姇褰憋紝浣嗕笉寰楁倓鎮勬墽琛岀浜屼釜涓氬姟鍔ㄤ綔銆?, null, null, null],
  ["杈呭姪绫诲瀷", "鍙ユ焺銆佹灇涓俱€丏TO銆侀敊璇被鍨嬪彧鐢ㄤ簬鎵胯浇鍙傛暟/杩斿洖鍊硷紝涓嶅崟鐙О涓?API銆?, null, null, null],
  ["闈炵洰鏍?, "浜嬩欢銆佸洖璋冦€丼ignal銆丆apability銆丼napshot 鑱氬悎銆佸疄鐜扮姸鎬佸拰鍙窇閫氭€у潎涓嶅湪鏈鑼冨唴銆?, null, null, null],
];
for (let row = 5; row <= 8; row++) overview.mergeCells(`E${row}:H${row}`);
overview.getRange("D5:D8").format = { fill: C.neutral, font: { bold: true, color: C.title2 }, verticalAlignment: "top" };
overview.getRange("E5:H8").format = { fill: "#FFFFFF", wrapText: true, verticalAlignment: "top", font: { color: C.text } };
overview.getRange("D4:H8").format.borders = { preset: "outside", style: "thin", color: C.border };
overview.getRange("5:8").format.rowHeight = 34;

overview.mergeCells("A12:H12");
overview.getRange("A12").values = [["v1 鍐崇瓥鍘熷垯"]];
overview.getRange("A12:H12").format = { fill: C.header, font: { bold: true, color: C.headerText }, horizontalAlignment: "center" };
const decisions = [
  ["1", "鍏叡鍛藉悕", "鍏叡 API 浣跨敤 PascalCase锛涘睘鎬х敤鍚嶈瘝/鐘舵€侊紝鏂规硶鐢ㄥ姩璇嶏紱涓嶄繚鐣欏師濮嬫垚鍛樼殑灏忓啓/缂╁啓椋庢牸銆?],
  ["2", "绫诲瀷闅旂", "涓嶅悜鎻掍欢鍏紑 Map2d銆丳R銆丯elItem銆丼ndPlayer 绛夊唴閮ㄥ璞★紱浣跨敤鍙ユ焺銆佸叕鍏辨灇涓惧拰鍙 DTO銆?],
  ["3", "灞炴€ч粯璁ゅ彧璇?, "鍙湁鍘熸垚鍛樺叿鏈夋槑纭?setter 涓斿壇浣滅敤鍙鏈熸椂鎵嶅紑鏀惧啓鍏ワ紱棣栫増鍐欏睘鎬у潎鏍?P2 鎴栭厤缃瀷 P0銆?],
  ["4", "涓荤嚎绋?, "鎵€鏈夋垚鍛樿闂潎瑕佹眰娓告垙涓荤嚎绋嬶紱v1 涓嶅仛闅愬紡鎺掗槦锛屼篃涓嶈繑鍥?Task銆?],
  ["5", "鐢熷懡鍛ㄦ湡", "鍦板浘銆佺帺瀹躲€佽彍鍗曘€佷簨浠躲€侀煶棰戝彞鏌勯兘鍙兘澶辨晥锛涙煡璇紭鍏堣繑鍥?nullable锛屽姩浣滃澶辨晥鍙ユ焺鎶涙槑纭紓甯搞€?],
  ["6", "鐗堟湰绋冲畾", "鍏叡璇箟绋冲畾锛屽唴閮ㄦ槧灏勫彲闅忔父鎴忕増鏈皟鏁达紱P2 鍏佽鍦?1.0 鍓嶆敹绱ф垨绉婚櫎銆?],
  ["7", "涓嶅仛瀹炵幇鍒ゅ畾", "浼樺厛绾с€侀闄╁拰鐢熷懡鍛ㄦ湡鏄璁′俊鎭紝涓嶄唬琛ㄥ綋鍓?Polaris 鏄惁宸茬粡瀹炵幇銆?],
];
overview.getRange("A13:C19").values = decisions;
overview.getRange("A13:A19").format = { fill: C.section, font: { bold: true, color: C.title2 }, horizontalAlignment: "center" };
overview.getRange("B13:B19").format = { fill: C.neutral, font: { bold: true, color: C.muted } };
overview.getRange("C13:C19").format = { wrapText: true, verticalAlignment: "top", font: { color: C.text } };
overview.getRange("A13:C19").format.borders = { insideHorizontal: { style: "thin", color: "#E2E8F0" }, outside: { style: "thin", color: C.border } };
overview.getRange("13:19").format.rowHeight = 30;

overview.mergeCells("A22:H22");
overview.getRange("A22").values = [["鍏冩暟鎹潵婧愶紙浠呯敤浜庡埗瀹氭槧灏勶紝涓嶇敤浜庡垽鏂疄鐜扮姸鎬侊級"]];
overview.getRange("A22:H22").format = { fill: C.header, font: { bold: true, color: C.headerText }, horizontalAlignment: "center" };
overview.getRange("A23:H25").values = [
  ["娓告垙鐩綍", "D:\\AliceInCradle Win ver029 - BIE6\\AliceInCradle_ver029", null, null, null, null, null, null],
  ["涓昏绋嬪簭闆?, "AliceInCradle_Data\\Managed\\Assembly-CSharp.dll", null, null, null, null, null, null],
  ["鍩虹绋嬪簭闆?, "AliceInCradle_Data\\Managed\\unsafeAssem.dll", null, null, null, null, null, null],
];
for (let row = 23; row <= 25; row++) overview.mergeCells(`B${row}:H${row}`);
overview.getRange("A23:A25").format = { fill: C.neutral, font: { bold: true, color: C.muted } };
overview.getRange("B23:H25").format = { font: { color: C.text }, wrapText: true };
overview.getRange("A23:H25").format.borders = { preset: "outside", style: "thin", color: C.border };
overview.getRange("A:A").format.columnWidth = 15;
overview.getRange("B:B").format.columnWidth = 16;
overview.getRange("C:C").format.columnWidth = 58;
overview.getRange("D:D").format.columnWidth = 18;
overview.getRange("E:H").format.columnWidth = 18;
overview.freezePanes.freezeRows(2);

// 鏄犲皠涓庨敊璇鍒?titleBand(rules, "Polaris 娓告垙 API锝滄槧灏勪笌閿欒瑙勫垯", "杩欎簺瑙勫垯绾︽潫鎵€鏈夌洰褰曟垚鍛橈紱鑻ュ崟涓?API 琛屾湁鏇村叿浣撶害瀹氾紝浠ョ洰褰曡涓哄噯銆?, "G");
const ruleRows = [
  ["R-01", "灞炴€ф彁鍙?, "鍗曚竴鏉ユ簮", "涓€涓睘鎬?API 蹇呴』瀵瑰簲涓€涓師濮嬪瓧娈垫垨灞炴€с€傚厑璁稿彞鏌勮В鏋愬拰绫诲瀷鎶曞奖锛屼笉鍏佽璇诲彇澶氫釜鎴愬憳鍚庢嫾瑁呫€?, "蹇呴』", "閬垮厤 Snapshot/娲剧敓鐘舵€佷吉瑁呮垚灞炴€?, "鎵€鏈夊睘鎬?],
  ["R-02", "灞炴€ф彁鍙?, "榛樿鍙", "闄ら潪鍘熸垚鍛樻湰韬湁绋冲畾 setter 涓斿啓鍏ヨ涔夋竻妤氾紝鍚﹀垯鍙毚闇?getter銆?, "蹇呴』", "闄嶄綆璺ㄧ増鏈壇浣滅敤", "璁块棶=鍙"],
  ["R-03", "鏂规硶璋冪敤", "鍗曚竴涓昏璋冪敤", "姣忎釜鏂规硶 API 鍙０鏄庝竴涓富瑕佸師濮嬫柟娉曘€傝В鏋?key銆佸彞鏌勩€佹灇涓惧拰 DTO 涓嶇畻棰濆涓氬姟鍔ㄤ綔銆?, "蹇呴』", "浣胯皟鐢ㄦ晥鏋滃彲棰勬祴", "鎵€鏈夋柟娉?],
  ["R-04", "鏂规硶璋冪敤", "涓嶉殣寮忚ˉ鍋?, "涓嶈嚜鍔ㄩ噸璇曘€佷笉鑷姩淇濆瓨銆佷笉鑷姩鎭㈠鏆傚仠銆佷笉鑷姩鍒囧浘鍚庝紶閫併€?, "蹇呴』", "閬垮厤闅愯棌鐘舵€佹満", "P1/P2 鍔ㄤ綔"],
  ["R-05", "鍏叡绫诲瀷", "鍐呴儴绫诲瀷闅旂", "涓嶅緱鍦ㄥ叕鍏辩鍚嶄腑鍑虹幇 UnityEngine銆乵2d銆乶el銆乆X銆乪vt 绫诲瀷銆?, "蹇呴』", "闃叉鎻掍欢缁戝畾娓告垙鍐呴儴 ABI", "鎵€鏈夋垚鍛?],
  ["R-06", "鍙ユ焺", "浠ｆ暟鏍￠獙", "鍦板浘鍒囨崲銆佸璞￠攢姣佹垨閲嶆柊鍔犺浇鍚庯紝鏃у彞鏌勫繀椤诲垽瀹氫负杩囨湡銆?, "蹇呴』", "閬垮厤澶嶇敤鎮┖鍐呴儴瀵硅薄", "瑙掕壊/闊抽/浜嬩欢/瀛樺偍"],
  ["R-07", "绌哄€?, "鏌ヨ浼樺厛 nullable", "渚濊禆杩愯鏃跺璞＄殑鏌ヨ鍦ㄥ璞′笉瀛樺湪鏃惰繑鍥?null锛涘叏灞€甯冨皵鏌ヨ鍙寜鐩綍绾﹀畾杩斿洖 false銆?, "榛樿", "渚夸簬鎺㈡祴鐢熷懡鍛ㄦ湡", "灞炴€т笌鏃犲壇浣滅敤鏌ヨ"],
  ["R-08", "寮傚父", "璋冪敤澶辫触鍒嗙被", "鍙傛暟閿欒鐢?Argument*锛涘彞鏌勯敊璇敤 InvalidGameHandleException锛涚姸鎬佺己澶辩敤 GameStateUnavailableException锛涘師璋冪敤鎷掔粷鐢?GameCallRejectedException銆?, "蹇呴』", "璋冪敤鏂瑰彲绋冲畾澶勭悊", "鏂规硶璋冪敤"],
  ["R-09", "绾跨▼", "浠呬富绾跨▼", "v1 鎵€鏈?API 浠呭厑璁镐粠 Unity 娓告垙涓荤嚎绋嬭闂紱绾跨▼閿欒鎶?GameThreadViolationException銆?, "蹇呴』", "閬垮厤 Unity/鍘熺増鐘舵€佺珵鎬?, "鎵€鏈夋垚鍛?],
  ["R-10", "璋冨害", "涓嶉殣寮忓紓姝?, "v1 涓嶅湪鍚庡彴绾跨▼鏇胯皟鐢ㄦ柟鎺掗槦锛屼笉杩斿洖 Task锛屼篃涓嶅悶鎺夎法绾跨▼璋冪敤銆?, "蹇呴』", "淇濇寔鏃跺簭鏄庣‘", "鎵€鏈夋垚鍛?],
  ["R-11", "鏋氫妇", "Unknown 淇濆簳", "浠庡唴閮ㄦ灇涓捐鍙栨湭鐭ュ€兼椂鏄犲皠涓?Unknown锛涘悜鍐呴儴鍐欏叆/璋冪敤鏃剁姝紶鍏?Unknown銆?, "蹇呴』", "鍏煎鏂板鍘熷鏋氫妇鍊?, "鏋氫妇鍙傛暟/杩斿洖鍊?],
  ["R-12", "閿欒閫忔槑", "涓嶅悶鍘熷紓甯?, "鍘熷璋冪敤寮傚父鍖呰涓?GameInvocationException锛屽苟淇濈暀 InnerException銆佸唴閮ㄧ被鍨嬪拰鎴愬憳鍚嶃€?, "蹇呴』", "渚夸簬妯＄粍璇婃柇", "鏂规硶璋冪敤"],
  ["R-13", "鐗堟湰", "鏄犲皠鍙彉锛岃涔夌ǔ瀹?, "娓告垙鐗堟湰鍗囩骇鏃跺厑璁告敼鍙樺唴閮ㄦ垚鍛樻槧灏勶紝浣嗕笉寰楅潤榛樻敼鍙樺叕鍏卞弬鏁般€佽繑鍥炲€兼垨鍓綔鐢ㄨ涔夈€?, "蹇呴』", "鍏叡濂戠害涓庢父鎴?ABI 瑙ｈ€?, "P0/P1"],
  ["R-14", "鍒嗙骇", "P0/P1/P2", "P0=甯哥敤鏍稿績锛汸1=鎵╁睍浣嗗彲鎺э紱P2=楂樻潈闄?楂樼姸鎬佹満鑰﹀悎銆傚垎绾т笉浠ｈ〃瀹炵幇鐘舵€併€?, "璇存槑", "鏀寔閫愭寮€鏀?, "鐩綍浼樺厛绾?],
];
rules.getRange("A4:G4").values = [["瑙勫垯ID", "鑼冨洿", "涓婚", "瑙勮寖", "绾︽潫绾у埆", "鐞嗙敱", "閫傜敤瀵硅薄"]];
rules.getRange(`A5:G${ruleRows.length + 4}`).values = ruleRows;
rules.getRange("A4:G4").format = { fill: C.header, font: { bold: true, color: C.headerText }, horizontalAlignment: "center", wrapText: true };
rules.getRange(`A5:G${ruleRows.length + 4}`).format = { wrapText: true, verticalAlignment: "top", font: { color: C.text, fontSize: 10 }, borders: { insideHorizontal: { style: "thin", color: "#E2E8F0" } } };
rules.getRange(`A5:A${ruleRows.length + 4}`).format = { fill: C.section, font: { bold: true, color: C.title2 }, horizontalAlignment: "center" };
rules.getRange(`B5:C${ruleRows.length + 4}`).format.font = { bold: true, color: C.muted };
rules.getRange(`E5:E${ruleRows.length + 4}`).format.horizontalAlignment = "center";
const ruleTable = rules.tables.add(`A4:G${ruleRows.length + 4}`, true, "ApiRules");
ruleTable.style = "TableStyleMedium4";
ruleTable.showBandedRows = true;
rules.freezePanes.freezeRows(4);
const ruleWidths = [10, 14, 18, 68, 12, 34, 28];
for (let i = 0; i < ruleWidths.length; i++) rules.getRange(`${String.fromCharCode(65 + i)}:${String.fromCharCode(65 + i)}`).format.columnWidth = ruleWidths[i];
rules.getRange(`5:${ruleRows.length + 4}`).format.rowHeight = 44;

// 杈呭姪绫诲瀷
titleBand(types, "Polaris 娓告垙 API锝滆緟鍔╃被鍨?, "杩欎簺鏄弬鏁颁笌杩斿洖鍊艰浇浣擄紝涓嶅崟鐙О涓?API锛涗换浣曡緟鍔╃被鍨嬮兘涓嶅緱鎸佹湁鍙緵鎻掍欢鐩存帴璋冪敤鐨勫唴閮ㄦ父鎴忓璞°€?, "G");
const typeRows = [
  ["GameVector2", "readonly struct", "float X; float Y", "UnityEngine.Vector2", "鍊兼嫹璐?, "鍙壙杞藉潗鏍?閫熷害锛屼笉鏆撮湶 Unity 绫诲瀷", "P0"],
  ["GameCharacter", "readonly struct", "long Id; int Generation; string? MapKey", "M2Mover / PR / NelEnemy", "浠ｆ暟鍙ユ焺", "鍦板浘鍒囨崲鎴栧璞￠攢姣佸悗澶辨晥", "P0"],
  ["GameMap", "readonly struct", "string Key; int Generation", "Map2d", "浠ｆ暟鍙ユ焺", "涓嶅緱鎸佹湁 Map2d 鍏叡寮曠敤", "P1"],
  ["GameItem", "readonly struct", "string Key", "NelItem", "绋冲畾閿彞鏌?, "鍙法鍦板浘锛涜В鏋愬け璐ュ嵆鏃犳晥", "P0"],
  ["GameStorage", "readonly struct", "long Id; int SaveGeneration", "ItemStorage", "浠ｆ暟鍙ユ焺", "鎹㈡。/璇绘。鍚庡け鏁?, "P0"],
  ["GameAudioPlayback", "readonly struct", "long Id; int Generation", "SndPlayer", "浠ｆ暟鍙ユ焺", "鎾斁鍋滄骞跺洖鏀跺悗澶辨晥", "P0"],
  ["GameEvent", "readonly struct", "long Id; int Generation", "EvReader", "浠ｆ暟鍙ユ焺", "浜嬩欢缁撴潫/鎹簨浠跺悗澶辨晥", "P1"],
  ["GameQuest", "readonly struct", "string Key", "Quest", "绋冲畾閿彞鏌?, "浠诲姟鑴氭湰閲嶈浇鍚庨噸鏂拌В鏋?, "P1"],
  ["GameQuestProgress", "readonly struct", "long Id; int SaveGeneration", "QuestProgress", "浠ｆ暟鍙ユ焺", "浠诲姟鍒楄〃鏇存柊鎴栬妗ｅ悗鍙兘澶辨晥", "P2"],
  ["GameDrop", "readonly struct", "long Id; int MapGeneration", "NelItemDrop", "浠ｆ暟鍙ユ焺", "鎺夎惤琚嬀鍙?閿€姣佸悗澶辨晥", "P2"],
  ["GameInputAction", "enum", "Left, Right, Up, Down, Submit, Cancel, Jump, Attack, Magic, Menu, Map, Run, Target", "XX.IN is* 鏂规硶鏃?, "鏋氫妇鏄犲皠", "璇诲彇鏈煡鍊间笉閫傜敤锛涗紶鍏?Unknown 绂佹", "P0"],
  ["GameFacing", "enum", "Unknown, Left, Right, Up, Down", "XX.AIM", "鏋氫妇鏄犲皠", "璇诲彇鏈煡鍊兼槧灏?Unknown", "P0"],
  ["GameWeather", "enum", "Unknown + 鍘熺増绋冲畾澶╂皵闆嗗悎", "WeatherItem.WEATHER", "鏋氫妇鏄犲皠", "棣栫増涓嶆壙璇轰綅鍊间笌鍘熸灇涓剧浉鍚?, "P0"],
  ["GameCurrency", "enum", "Gold + 鍏朵粬纭鍚庣殑 CTYPE", "CoinStorage.CTYPE", "鏋氫妇鏄犲皠", "Unknown 涓嶈兘鐢ㄤ簬璋冪敤", "P0"],
  ["GamePlayerState", "enum", "Unknown + 瀹℃牳鍚庣殑 PR.STATE 瀛愰泦", "PR.STATE", "鏋氫妇鏄犲皠", "P0 鍙鍏ㄩ噺鍚嶇О锛汸2 鍐欏叆鍙紑鏀剧櫧鍚嶅崟", "P0/P2"],
  ["GameEnemyState", "enum", "Unknown + 瀹℃牳鍚庣殑 NelEnemy.STATE 瀛愰泦", "NelEnemy.STATE", "鏋氫妇鏄犲皠", "鍐欏叆鍙紑鏀剧櫧鍚嶅崟", "P1/P2"],
  ["GameEnemyId", "enum/value", "Unknown + 鍘?ID 鏁板€?, "ENEMYID", "鍊兼槧灏?, "淇濈暀鏁板€间究浜庢湭鏉ョ増鏈吋瀹?, "P1"],
  ["GameItemCategory", "enum", "Unknown + 鍘熺墿鍝佸垎绫?, "NelItem.CATEG", "鏋氫妇鏄犲皠", "涓嶆壙璇烘暟鍊间笌鍘熸灇涓句竴鑷?, "P0"],
  ["GameBgmTrack", "readonly record", "string Timing; string Cue", "BGM.getFrontBgm out 鍙傛暟", "DTO 鎶曞奖", "鍙鏁版嵁锛屼笉鍚挱鏀惧櫒寮曠敤", "P1"],
  ["QuestProgressView", "readonly record", "string QuestKey; int Phase; bool Finished", "QuestProgress", "DTO 鎶曞奖", "浠呭垪棣栫増绋冲畾瀛楁", "P1"],
  ["QuestUpdateOptions", "readonly struct", "Hidden, FillTarget, Focus, FixPhase, ProgressTask", "updateQuest 鍙傛暟", "鍙傛暟 DTO", "瀛楁涓庡師鏂规硶甯冨皵鍙傛暟涓€涓€瀵瑰簲", "P2"],
  ["EnemyDamageRequest", "readonly record", "鍏叡鏀诲嚮鍙傛暟瀛愰泦", "NelAttackInfo", "鍙傛暟 DTO", "缂哄け蹇呴渶瀛楁鏃舵嫆缁濊皟鐢?, "P2"],
  ["KnockbackRequest", "readonly record", "鏀诲嚮鏉ユ簮銆佺洰鏍囥€佸姏绫诲瀷", "AttackInfo + FOC_TYPE", "鍙傛暟 DTO", "涓嶅緱鏆撮湶鍘熷璞★紱鏉ユ簮浣跨敤 GameCharacter", "P2"],
];
types.getRange("A4:G4").values = [["绫诲瀷", "褰㈡€?, "寤鸿瀛楁 / 鍊?, "鍐呴儴鏉ユ簮", "杞崲鏂瑰紡", "绾︽潫", "绾у埆"]];
types.getRange(`A5:G${typeRows.length + 4}`).values = typeRows;
types.getRange("A4:G4").format = { fill: C.header, font: { bold: true, color: C.headerText }, horizontalAlignment: "center", wrapText: true };
types.getRange(`A5:G${typeRows.length + 4}`).format = { wrapText: true, verticalAlignment: "top", font: { color: C.text, fontSize: 10 }, borders: { insideHorizontal: { style: "thin", color: "#E2E8F0" } } };
types.getRange(`A5:A${typeRows.length + 4}`).format = { fill: C.section, font: { bold: true, color: C.title2 } };
types.getRange(`G5:G${typeRows.length + 4}`).format.horizontalAlignment = "center";
const typesTable = types.tables.add(`A4:G${typeRows.length + 4}`, true, "ApiSupportingTypes");
typesTable.style = "TableStyleMedium4";
typesTable.showBandedRows = true;
types.freezePanes.freezeRows(4);
const typeWidths = [24, 18, 54, 32, 18, 48, 12];
for (let i = 0; i < typeWidths.length; i++) types.getRange(`${String.fromCharCode(65 + i)}:${String.fromCharCode(65 + i)}`).format.columnWidth = typeWidths[i];
types.getRange(`5:${typeRows.length + 4}`).format.rowHeight = 42;

// Compact verification before export.
const checks = [];
checks.push((await workbook.inspect({ kind: "table", range: `API鐩綍_v1!A1:O${apiRows.length + 5}`, include: "values,formulas", tableMaxRows: 12, tableMaxCols: 15, maxChars: 12000 })).ndjson);
checks.push((await workbook.inspect({ kind: "match", searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A", options: { useRegex: true, maxResults: 300 }, summary: "final formula error scan" })).ndjson);
await fs.writeFile("verification.ndjson", checks.join("\n"), "utf8");

for (const [sheetName, range] of [
  ["瑙勮寖鎬昏", "A1:H25"],
  ["API鐩綍_v1", `A1:O${Math.min(apiRows.length + 5, 55)}`],
  ["鏄犲皠涓庨敊璇鍒?, `A1:G${ruleRows.length + 4}`],
  ["杈呭姪绫诲瀷", `A1:G${typeRows.length + 4}`],
]) {
  const preview = await workbook.render({ sheetName, range, scale: 1.2, format: "png" });
  await fs.writeFile(`final-${sheetName}.png`, new Uint8Array(await preview.arrayBuffer()));
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({ outputPath, apiRows: apiRows.length, properties: apiRows.filter(r => r[2] === "灞炴€ф彁鍙?).length, methods: apiRows.filter(r => r[2] === "鏂规硶璋冪敤").length }));

