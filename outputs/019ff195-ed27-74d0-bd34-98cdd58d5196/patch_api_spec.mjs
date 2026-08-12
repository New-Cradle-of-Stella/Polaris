import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const path = "Polaris-Game-API-Static-Classification.xlsx";
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(path));
const catalog = workbook.worksheets.getItem("API目录_v1");
const rules = workbook.worksheets.getItem("映射与错误规则");
const types = workbook.worksheets.getItem("辅助类型");

const replacements = new Map([
  ["CharacterHandle", "GameCharacter"],
  ["ItemHandle", "GameItem"],
  ["StorageHandle", "GameStorage"],
  ["AudioHandle", "GameAudioPlayback"],
  ["EventHandle", "GameEvent"],
  ["QuestProgressHandle", "GameQuestProgress"],
  ["QuestHandle", "GameQuest"],
  ["DropHandle", "GameDrop"],
  ["MapHandle", "GameMap"],
]);

function replaceMatrix(matrix) {
  return matrix.map(row => row.map(value => {
    if (typeof value !== "string") return value;
    let result = value;
    for (const [from, to] of replacements) result = result.split(from).join(to);
    return result;
  }));
}

const catalogRange = catalog.getRange("A1:O158");
catalogRange.values = replaceMatrix(catalogRange.values);
const typeRange = types.getRange("A1:G27");
typeRange.values = replaceMatrix(typeRange.values);

const propertyFixes = new Map([
  [19, ["GameCharacter.X", "float X { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [20, ["GameCharacter.Y", "float Y { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [21, ["GameCharacter.VelocityX", "float VelocityX { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [22, ["GameCharacter.VelocityY", "float VelocityY { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [23, ["GameCharacter.Width", "float Width { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [24, ["GameCharacter.Height", "float Height { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [25, ["GameCharacter.Facing", "GameFacing Facing { get; }", "过期对象壳抛 InvalidGameHandleException；未知原枚举值映射 Unknown"]],
  [26, ["GameCharacter.Hp", "int Hp { get; }", "对象不是 Attackable 或对象壳过期时抛 InvalidGameHandleException"]],
  [27, ["GameCharacter.MaxHp", "int MaxHp { get; }", "对象不是 Attackable 或对象壳过期时抛 InvalidGameHandleException"]],
  [28, ["GameCharacter.Mp", "int Mp { get; }", "对象不是 Attackable 或对象壳过期时抛 InvalidGameHandleException"]],
  [29, ["GameCharacter.MaxMp", "int MaxMp { get; }", "对象不是 Attackable 或对象壳过期时抛 InvalidGameHandleException"]],
  [30, ["GameCharacter.IsAlive", "bool IsAlive { get; }", "对象不是 Attackable 或对象壳过期时抛 InvalidGameHandleException"]],
  [33, ["GameCharacter.EnemyId", "GameEnemyId? EnemyId { get; }", "对象不是 NelEnemy 时返回 null；对象壳过期时抛 InvalidGameHandleException"]],
  [34, ["GameCharacter.EnemyState", "GameEnemyState? EnemyState { get; }", "对象不是 NelEnemy 时返回 null；对象壳过期时抛 InvalidGameHandleException"]],
  [35, ["GameItem.Key", "string Key { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [36, ["GameItem.Id", "ushort Id { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [37, ["GameItem.Price", "int Price { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [38, ["GameItem.StackLimit", "int StackLimit { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [39, ["GameItem.Category", "GameItemCategory Category { get; }", "过期对象壳抛 InvalidGameHandleException；未知值映射 Unknown"]],
  [40, ["GameItem.Value", "float Value { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [41, ["GameItem.IsUsable", "bool IsUsable { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [42, ["GameItem.IsPrecious", "bool IsPrecious { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [43, ["GameItem.IsFood", "bool IsFood { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [44, ["GameItem.IsTool", "bool IsTool { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [45, ["GameItem.IsBomb", "bool IsBomb { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [46, ["GameStorage.CapacityRows", "int CapacityRows { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [47, ["GameStorage.SplitsByGrade", "bool SplitsByGrade { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [48, ["GameStorage.AcceptsWater", "bool AcceptsWater { get; set; }", "过期对象壳抛 InvalidGameHandleException"]],
  [56, ["GameAudioPlayback.IsLooping", "bool IsLooping { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [57, ["GameAudioPlayback.BaseVolume", "float BaseVolume { get; }", "过期对象壳抛 InvalidGameHandleException"]],
  [58, ["GameAudioPlayback.RemainingMilliseconds", "long RemainingMilliseconds { get; }", "过期对象壳抛 InvalidGameHandleException"]],
]);

for (const [row, [apiPath, signature, errorRule]] of propertyFixes) {
  catalog.getRange(`D${row}:E${row}`).values = [[apiPath, signature]];
  catalog.getRange(`N${row}`).values = [[errorRule]];
}

rules.getRange("B10:G10").values = [[
  "对象壳",
  "代数校验",
  "GameCharacter、GameItem、GameStorage、GameAudioPlayback 等公开对象壳只保存 ID 与代数；其实例属性仍是本目录中的“属性提取”API，不持有内部游戏对象引用。",
  "必须",
  "保证属性语法合法并避免悬空内部引用",
  "角色/物品/存储/音频/事件",
]];

const typeAdjustments = [
  [6, "GameCharacter", "readonly struct facade", "地图切换或对象销毁后失效；实例属性列于 API 目录"],
  [7, "GameMap", "readonly struct facade", "不得持有 Map2d 公共引用"],
  [8, "GameItem", "readonly struct facade", "可跨地图；解析失败即过期"],
  [9, "GameStorage", "readonly struct facade", "换档/读档后失效"],
  [10, "GameAudioPlayback", "readonly struct facade", "播放停止并回收后失效；实例属性列于 API 目录"],
  [11, "GameEvent", "readonly struct facade", "事件结束/换事件后失效"],
  [12, "GameQuest", "readonly struct facade", "任务脚本重载后重新解析"],
  [13, "GameQuestProgress", "readonly struct facade", "任务列表更新或读档后可能失效"],
  [14, "GameDrop", "readonly struct facade", "掉落被拾取/销毁后失效"],
];
for (const [row, name, shape, constraint] of typeAdjustments) {
  types.getRange(`A${row}:B${row}`).values = [[name, shape]];
  types.getRange(`F${row}`).values = [[constraint]];
}

const invalidPropertySignatures = [];
for (let row = 6; row <= 158; row++) {
  const apiType = catalog.getRange(`C${row}`).values[0][0];
  const signature = catalog.getRange(`E${row}`).values[0][0];
  if (apiType === "属性提取" && typeof signature === "string" && /\([^)]*\)\s*\{\s*get/.test(signature)) {
    invalidPropertySignatures.push({ row, signature });
  }
}
if (invalidPropertySignatures.length) {
  throw new Error(`Invalid property signatures remain: ${JSON.stringify(invalidPropertySignatures)}`);
}

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
await fs.writeFile("verification-final.ndjson", errors.ndjson, "utf8");

for (const [file, sheetName, range] of [
  ["verified-overview.png", "规范总览", "A1:H25"],
  ["verified-catalog-properties.png", "API目录_v1", "A1:O58"],
  ["verified-catalog-methods-1.png", "API目录_v1", "A59:O110"],
  ["verified-catalog-methods-2.png", "API目录_v1", "A111:O158"],
  ["verified-rules.png", "映射与错误规则", "A1:G18"],
  ["verified-types.png", "辅助类型", "A1:G27"],
]) {
  const preview = await workbook.render({ sheetName, range, scale: 1.15, format: "png" });
  await fs.writeFile(file, new Uint8Array(await preview.arrayBuffer()));
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(path);
console.log(JSON.stringify({ patched: true, invalidPropertySignatures: invalidPropertySignatures.length }));
