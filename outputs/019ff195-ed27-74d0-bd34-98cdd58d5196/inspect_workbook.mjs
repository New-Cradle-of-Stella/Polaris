import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const input = await FileBlob.load("source.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);

const summary = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 12000,
  tableMaxRows: 12,
  tableMaxCols: 20,
  tableMaxCellChars: 180,
});
console.log(summary.ndjson);

const sheets = JSON.parse(`[${(await workbook.inspect({ kind: "sheet", include: "id,name", maxChars: 8000 })).ndjson.split("\n").filter(Boolean).join(",")}]`);
for (const item of sheets) {
  const sheetName = item.name ?? item.sheetName;
  if (!sheetName) continue;
  const region = await workbook.inspect({
    kind: "region",
    sheetId: sheetName,
    range: "A1:Z80",
    maxChars: 12000,
    tableMaxRows: 80,
    tableMaxCols: 26,
    tableMaxCellChars: 220,
  });
  console.log(`\n--- REGION ${sheetName} ---\n${region.ndjson}`);
  const styles = await workbook.inspect({
    kind: "computedStyle",
    sheetId: sheetName,
    range: "A1:Z20",
    maxChars: 5000,
  });
  console.log(`\n--- STYLES ${sheetName} ---\n${styles.ndjson}`);
  const preview = await workbook.render({ sheetName, autoCrop: "all", scale: 1, format: "png" });
  await fs.writeFile(`preview-${sheetName.replace(/[\\/:*?\"<>|]/g, "_")}.png`, new Uint8Array(await preview.arrayBuffer()));
}
