import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const root = "E:/Projects/Polaris";
const sourcePath = path.join(root, "Polaris-Game-API-Spec-v2-静态与实例模型.xlsx");
const outputDir = path.join(root, "outputs/019ff477-bd13-7602-b5b2-01314545dcc2");

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(sourcePath));
const summary = await workbook.inspect({
  kind: "workbook,sheet,table,drawing,definedName,thread",
  maxChars: 20000,
  tableMaxRows: 8,
  tableMaxCols: 12,
  tableMaxCellChars: 160,
});
await fs.writeFile(path.join(outputDir, "source-summary.ndjson"), summary.ndjson, "utf8");

const sheetInfo = [];
for (const sheet of workbook.worksheets.items) {
  const used = sheet.getUsedRange(false);
  const address = used?.address ?? null;
  const entry = { name: sheet.name, address };
  if (address) {
    const region = await workbook.inspect({
      kind: "region",
      sheetId: sheet.name,
      range: address,
      maxChars: 12000,
      tableMaxRows: 20,
      tableMaxCols: 20,
      tableMaxCellChars: 240,
    });
    await fs.writeFile(path.join(outputDir, `source-${sheetInfo.length + 1}-region.ndjson`), region.ndjson, "utf8");
    const preview = await workbook.render({ sheetName: sheet.name, autoCrop: "all", scale: 1, format: "png" });
    const previewPath = path.join(outputDir, `source-${sheetInfo.length + 1}.png`);
    await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
    entry.previewPath = previewPath;
  }
  sheetInfo.push(entry);
}

const csvHelp = workbook.help("exportCsv", { include: "index,examples,notes", maxChars: 4000 });
await fs.writeFile(path.join(outputDir, "csv-help.ndjson"), csvHelp.ndjson, "utf8");
await fs.writeFile(path.join(outputDir, "sheet-info.json"), JSON.stringify(sheetInfo, null, 2), "utf8");
console.log(JSON.stringify(sheetInfo, null, 2));
