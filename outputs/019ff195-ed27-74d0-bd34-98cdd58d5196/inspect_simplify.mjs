import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const dir = "E:/Projects/Polaris/outputs/019ff195-ed27-74d0-bd34-98cdd58d5196";
const path = `${dir}/Polaris-Game-API-Static-Classification.xlsx`;
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(path));

const inspect = await workbook.inspect({
  kind: "table",
  sheetId: "API目录_v1",
  range: "A1:O212",
  include: "values,formulas",
  tableMaxRows: 212,
  tableMaxCols: 15,
  tableMaxCellChars: 160,
  maxChars: 140000,
});
await fs.writeFile(`${dir}/simplify-before-inspect.ndjson`, inspect.ndjson, "utf8");

const preview = await workbook.render({
  sheetName: "API目录_v1",
  range: "A1:O35",
  scale: 1.2,
  format: "png",
});
await fs.writeFile(`${dir}/simplify-before.png`, new Uint8Array(await preview.arrayBuffer()));

console.log(inspect.ndjson.slice(0, 3500));
