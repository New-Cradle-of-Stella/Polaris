import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const dir = "E:/Projects/Polaris/outputs/019ff195-ed27-74d0-bd34-98cdd58d5196";
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(`${dir}/user-edited-v2.xlsx`));
const summary = await workbook.inspect({ kind: "sheet", include: "id,name", maxChars: 4000 });
const sheet = workbook.worksheets.getItem("API规范_v2");
const used = sheet.getUsedRange();
const inspect = await workbook.inspect({
  kind: "table",
  sheetId: "API规范_v2",
  range: "A175:C240",
  include: "values,formulas",
  tableMaxRows: 66,
  tableMaxCols: 3,
  tableMaxCellChars: 300,
  maxChars: 30000,
});
const styles = await workbook.inspect({
  kind: "computedStyle",
  sheetId: "API规范_v2",
  range: "A185:C190",
  maxChars: 12000,
});
const preview = await workbook.render({ sheetName: "API规范_v2", range: "A175:C205", scale: 1.3, format: "png" });
await fs.writeFile(`${dir}/user-v2-bottom.png`, new Uint8Array(await preview.arrayBuffer()));
await fs.writeFile(`${dir}/user-v2-inspect.ndjson`, `${summary.ndjson}\n${inspect.ndjson}\n${styles.ndjson}\n`, "utf8");
console.log(JSON.stringify({ usedAddress: used.address, summary: summary.ndjson, inspect: inspect.ndjson, styles: styles.ndjson }));
