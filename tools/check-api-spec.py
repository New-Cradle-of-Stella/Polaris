#!/usr/bin/env python3
"""把编译产物 Polaris.dll 的公共面与 v2 API 规范表格逐条对照。

用法：
    python tools/check-api-spec.py [--config Release]

它做两件事，缺一不可：

  1. 规范里的每一条契约，程序集里都要有对应的公开成员（双向差集的"缺失"一侧）。
  2. `PolarisAPI.Game` 门面与十个实例类型上，不能出现规范之外的公开成员
     （双向差集的"多余"一侧）——否则清理就成了"加了新的、旧的也还在"。

表格曾有两处笔误（第 42 行 SetWeather 的签名误写成 HasWeather、第 44 行标签写作
DangerMeter 而签名是 GetDangerMeter），已在表格里改正，因此这里不再需要任何豁免：
标签与程序集成员名一一对应，对不上就是真的对不上。
"""

import argparse
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SPEC = REPO / "Polaris-Game-API-Spec-v2-静态与实例模型.xlsx"
CECIL = REPO / "outputs" / "019ff195-ed27-74d0-bd34-98cdd58d5196" / "cecil" / "Mono.Cecil.dll"

NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
M = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"

SEP = "｜"          # 全角竖线，表格里 "静态｜PolarisAPI.Game.Loop" 的分隔符
STATIC = "静态"
INSTANCE = "动态"
STATIC_CB = "静态回调"
INSTANCE_CB = "动态回调"
INSTANCE_OWNER = "实例对象"

# 规范没有单独列行、但被签名引用的支撑类型，允许公开。
SIGNATURE_CLOSURE = {
    "Polaris.API.GameVector2",
    "Polaris.API.GameInputAction",
    "Polaris.API.GameWeather",
    "Polaris.API.GameCurrency",
    "Polaris.API.GameFacing",
    "Polaris.API.GamePlayerState",
    "Polaris.API.GameEnemyState",
    "Polaris.API.GameEnemyId",
    "Polaris.API.GameItemCategory",
    "Polaris.API.GameVolumeChannel",
    "Polaris.API.EnemyDamageRequest",
    "Polaris.API.KnockbackRequest",
    "Polaris.API.QuestUpdateOptions",
    "Polaris.API.GameQuestProgress",
    "Polaris.API.GameQuestProgressView",
    "Polaris.API.GameBgmTrack",
    "Polaris.API.GameDrop",
    "Polaris.API.GameCallbackOptions",
    "Polaris.API.GameCallbackRegistration",
    "Polaris.API.GameCallbackData",
    "Polaris.API.GameStaticCallbackKind",
    "Polaris.API.GameInstanceCallbackKind",
    "Polaris.API.InvalidGameInstanceException",
}

# 实例基类上的成员是规范"实例注册 + 生命周期"那两条的实现，不算多余公开面。
INSTANCE_BASE_MEMBERS = {"Register", "IsValid", "ToString", "Equals", "GetHashCode"}

# 受检的实例类型：规范给它们逐条列了成员，因此"多余成员"这一侧也要管。
INSTANCE_TYPES = [
    "GameMap", "GameCharacter", "GamePlayer", "GameEnemy", "GameItem",
    "GameStorage", "GameAudioPlayback", "GameMenu", "GameEvent", "GameQuest",
]


def read_spec():
    """读出表格里所有带方括号的契约行。"""
    with zipfile.ZipFile(SPEC) as z:
        shared = [
            "".join(t.text or "" for t in si.iter(M + "t"))
            for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall("m:si", NS)
        ]
        sheet = ET.fromstring(z.read("xl/worksheets/sheet1.xml"))

    rows = []
    for row in sheet.iter(M + "row"):
        cells = {}
        for cell in row.findall("m:c", NS):
            col = "".join(ch for ch in cell.get("r") if ch.isalpha())
            v = cell.find("m:v", NS)
            if v is None:
                cells[col] = ""
            elif cell.get("t") == "s":
                cells[col] = shared[int(v.text)]
            else:
                cells[col] = v.text
        rows.append((cells.get("A", ""), cells.get("B", ""), cells.get("C", "")))

    return [r for r in rows if r[0].startswith("[")]


def dump_assembly(dll):
    """用 Mono.Cecil 把程序集的公开面导出成行。放在 PowerShell 里是因为 Cecil 是 .NET 库。"""
    script = f"""
Add-Type -Path '{CECIL}'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('{dll}')
$out = New-Object System.Collections.Generic.List[string]
function Walk($t, $prefix) {{
  if (-not ($t.IsPublic -or $t.IsNestedPublic)) {{ return }}
  $name = if ($prefix) {{ "$prefix.$($t.Name)" }} else {{ $t.FullName }}
  $out.Add("TYPE`t$name")
  foreach ($p in $t.Properties) {{
    $g = $p.GetMethod; $s = $p.SetMethod
    if (-not ((($g -and $g.IsPublic)) -or (($s -and $s.IsPublic)))) {{ continue }}
    $acc = @(); if ($g -and $g.IsPublic) {{ $acc += 'get' }}; if ($s -and $s.IsPublic) {{ $acc += 'set' }}
    $st = if (($g -and $g.IsStatic) -or ($s -and $s.IsStatic)) {{ 'static ' }} else {{ '' }}
    $out.Add("PROP`t$name`t$st$($p.PropertyType.Name) $($p.Name) {{ $($acc -join '; ') }}")
  }}
  foreach ($m in $t.Methods) {{
    if (-not $m.IsPublic) {{ continue }}
    if ($m.IsGetter -or $m.IsSetter -or $m.IsAddOn -or $m.IsRemoveOn) {{ continue }}
    $ps = ($m.Parameters | ForEach-Object {{ "$($_.ParameterType.Name) $($_.Name)" }}) -join ', '
    $st = if ($m.IsStatic) {{ 'static ' }} else {{ '' }}
    $out.Add("METH`t$name`t$st$($m.ReturnType.Name) $($m.Name)($ps)")
  }}
  foreach ($f in $t.Fields) {{
    if (-not $f.IsPublic) {{ continue }}
    if ($t.IsEnum -and $f.Name -eq 'value__') {{ continue }}
    $out.Add("FLD`t$name`t$($f.FieldType.Name) $($f.Name)")
  }}
  foreach ($n in $t.NestedTypes) {{ Walk $n $name }}
}}
foreach ($t in $asm.MainModule.Types) {{ Walk $t $null }}
$out -join "`n"
"""
    result = subprocess.run(
        ["powershell", "-NoProfile", "-Command", script],
        capture_output=True, text=True, encoding="utf-8",
    )
    if result.returncode != 0:
        sys.exit(f"failed to read {dll}:\n{result.stderr}")
    return result.stdout.splitlines()


def index(lines):
    types, members = set(), {}
    for line in lines:
        parts = line.split("\t")
        if parts[0] == "TYPE":
            types.add(parts[1])
            members.setdefault(parts[1], set())
        elif parts[0] in ("PROP", "METH", "FLD") and len(parts) >= 3:
            sig = parts[2]
            m = re.search(r"([A-Za-z_]\w*)\s*[({]", sig)
            name = m.group(1) if m else sig.split()[-1]
            members.setdefault(parts[1], set()).add(name)
    return types, members


def resolve(tag, label):
    kind, _, owner = tag.partition(SEP)
    if kind == STATIC_CB:
        return "Polaris.API.GameStaticCallbackKind", label
    if kind == INSTANCE_CB:
        return "Polaris.API.GameInstanceCallbackKind", label
    if kind == INSTANCE and owner == INSTANCE_OWNER:
        return "Polaris.API.GameInstance", "Register"
    if kind in (STATIC, INSTANCE):
        prefix = "Polaris." if owner.startswith("PolarisAPI.Game") else "Polaris.API."
        return prefix + owner, label
    return None, None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", default="Release")
    args = ap.parse_args()

    dll = REPO / "bin" / args.config / "netstandard2.1" / "Polaris.dll"
    if not dll.exists():
        sys.exit(f"not built: {dll}\nrun: dotnet build Polaris.csproj -c {args.config}")

    contracts = read_spec()
    types, members = index(dump_assembly(dll))

    missing = []
    covered = {}
    for tag_cell, _sig, _desc in contracts:
        tag = tag_cell[1:tag_cell.index("]")]
        label = tag_cell[tag_cell.index("]") + 1:].strip()
        owner, name = resolve(tag, label)
        if owner is None:
            missing.append((tag_cell, "unrecognised contract tag"))
            continue
        if owner not in types:
            missing.append((tag_cell, f"type not public: {owner}"))
            continue
        if name not in members.get(owner, ()):
            missing.append((tag_cell, f"member not public: {owner}.{name}"))
            continue
        covered.setdefault(owner, set()).add(name)

    # 反方向：受检类型上不能有规范之外的公开成员。
    extra = []
    checked = ["Polaris.PolarisAPI.Game." + g for g in (
        "Loop", "Input", "Assets", "Localization", "World", "Items",
        "Inventory", "Menu", "Events", "Quests", "Economy", "Audio", "Callbacks",
    )] + ["Polaris.PolarisAPI.Game.Audio.Bgm"] + ["Polaris.API." + t for t in INSTANCE_TYPES]

    for owner in checked:
        for name in sorted(members.get(owner, ())):
            if name in covered.get(owner, ()):
                continue
            if name in INSTANCE_BASE_MEMBERS:
                continue
            extra.append(f"{owner}.{name}")

    total = len(contracts)
    print(f"spec contracts : {total}")
    print(f"matched        : {total - len(missing)}")
    print(f"missing        : {len(missing)}")
    print(f"unexpected     : {len(extra)}")

    for tag_cell, why in missing:
        print(f"  MISSING  {tag_cell}  -> {why}")
    for name in extra:
        print(f"  EXTRA    {name}")

    return 1 if (missing or extra) else 0


if __name__ == "__main__":
    sys.exit(main())
