import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const root = "E:/Projects/Polaris";
const sourcePath = path.join(root, "Polaris-Game-API-Spec-v2-静态与实例模型.xlsx");
const outputDir = path.join(root, "outputs/019ff477-bd13-7602-b5b2-01314545dcc2");
const outputPath = path.join(outputDir, "Polaris-Game-API-Spec-v2.csv");

function normalizeCell(value) {
  if (value === null || value === undefined) return "";
  if (value instanceof Date) return value.toISOString();
  return String(value);
}

function quoteCsv(value) {
  return `"${normalizeCell(value).replaceAll('"', '""')}"`;
}

function dimensions(matrix) {
  return {
    rows: matrix.length,
    cols: matrix.reduce((max, row) => Math.max(max, row.length), 0),
    nonEmpty: matrix.flat().filter((value) => normalizeCell(value) !== "").length,
  };
}

function matricesEqual(left, right, rows, cols) {
  for (let row = 0; row < rows; row += 1) {
    for (let col = 0; col < cols; col += 1) {
      if (normalizeCell(left[row]?.[col]) !== normalizeCell(right[row]?.[col])) {
        return { equal: false, row: row + 1, col: col + 1 };
      }
    }
  }
  return { equal: true };
}

await fs.mkdir(outputDir, { recursive: true });
const sourceWorkbook = await SpreadsheetFile.importXlsx(await FileBlob.load(sourcePath));
if (sourceWorkbook.worksheets.items.length !== 1) {
  throw new Error(`Expected exactly one worksheet, found ${sourceWorkbook.worksheets.items.length}.`);
}

const sourceSheet = sourceWorkbook.worksheets.getItemAt(0);
const usedRange = sourceSheet.getUsedRange(false);
if (!usedRange || usedRange.address !== "A1:C242") {
  throw new Error(`Unexpected source used range: ${usedRange?.address ?? "none"}.`);
}

const sourceValues = usedRange.values;
const sourceFormulas = usedRange.formulas;
const formulaCells = sourceFormulas.flat().filter((value) => typeof value === "string" && value.startsWith("="));
if (formulaCells.length !== 0) {
  throw new Error(`Source contains ${formulaCells.length} formula cells; a value-only CSV would not be lossless.`);
}

const csvText = sourceValues.map((row) => row.map(quoteCsv).join(",")).join("\r\n") + "\r\n";
await fs.writeFile(outputPath, `\uFEFF${csvText}`, "utf8");

const roundTripWorkbook = await Workbook.fromCSV(csvText, { sheetName: sourceSheet.name });
const roundTripSheet = roundTripWorkbook.worksheets.getItemAt(0);
const roundTripValues = roundTripSheet.getRange("A1:C242").values;
const comparison = matricesEqual(sourceValues, roundTripValues, 242, 3);
if (!comparison.equal) {
  throw new Error(`Round-trip mismatch at row ${comparison.row}, column ${comparison.col}.`);
}

const sourceDims = dimensions(sourceValues);
const roundTripDims = dimensions(roundTripValues);
if (JSON.stringify(sourceDims) !== JSON.stringify(roundTripDims)) {
  throw new Error(`Dimension/count mismatch: ${JSON.stringify({ sourceDims, roundTripDims })}`);
}

const verification = await roundTripWorkbook.inspect({
  kind: "sheet,region,formula",
  sheetId: sourceSheet.name,
  range: "A1:C242",
  maxChars: 6000,
  tableMaxRows: 8,
  tableMaxCols: 3,
  tableMaxCellChars: 240,
  options: { maxResults: 50 },
});
await fs.writeFile(path.join(outputDir, "csv-verification.ndjson"), verification.ndjson, "utf8");

const outputBytes = await fs.readFile(outputPath);
const hasUtf8Bom = outputBytes[0] === 0xef && outputBytes[1] === 0xbb && outputBytes[2] === 0xbf;
if (!hasUtf8Bom) throw new Error("UTF-8 BOM is missing.");

const report = {
  sourceSheet: sourceSheet.name,
  sourceRange: usedRange.address,
  rows: sourceDims.rows,
  columns: sourceDims.cols,
  nonEmptyCells: sourceDims.nonEmpty,
  formulaCells: formulaCells.length,
  utf8Bom: hasUtf8Bom,
  roundTripExact: comparison.equal,
  outputPath,
};
await fs.writeFile(path.join(outputDir, "csv-verification.json"), JSON.stringify(report, null, 2), "utf8");
console.log(JSON.stringify(report, null, 2));
