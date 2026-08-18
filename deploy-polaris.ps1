<#
.SYNOPSIS
    构建、部署或打包 PolarisCore 与十二个普通 DLL 组件。

.DESCRIPTION
    PolarisCore.dll 是唯一带 BepInPlugin 的插件，放在 BepInEx\plugins\Polaris\。
    PolarisUI/Res/Lang/Magic/Addons/Map/Diagnostics/Save/Event/Particles/AI 都是普通类库，
    放在 BepInEx\plugins\Polaris\libs\，由 PolarisCore 的组件宿主发现并驱动。

    游戏根目录（含 AliceInCradle_Data 的那一层）默认从仓库根的 aic_path.txt 读取——
    那是整套构建流程唯一的路径配置，MSBuild 侧的 Directory.Build.props 读的也是它。
    -AicPath 只是临时覆盖它，用来往另一份游戏安装上部署。该路径同时用于编译期游戏程序集
    引用；非 Package 模式下也作为部署目标。

.EXAMPLE
    .\deploy-polaris.ps1
    .\deploy-polaris.ps1 -Configuration Release
    .\deploy-polaris.ps1 -Package
    .\deploy-polaris.ps1 -AicPath 'D:\Games\AliceInCradle'
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $AicPath,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $Package
)

if ($Package) { $Configuration = 'Release' }
$ErrorActionPreference = 'Stop'

$RepoRoot = $PSScriptRoot
$AicPathFile = Join-Path $RepoRoot 'aic_path.txt'

if (-not $AicPath) {
    if (-not (Test-Path -LiteralPath $AicPathFile -PathType Leaf)) {
        throw "aic_path.txt not found: $AicPathFile`nCreate it with a single line holding the AliceInCradle install directory (the one that contains AliceInCradle_Data), or pass -AicPath to override it."
    }
    $AicPath = (Get-Content -LiteralPath $AicPathFile -Raw).Trim().Trim('"')
    if (-not $AicPath) { throw "aic_path.txt is empty: $AicPathFile" }
}

$AicPath = (Resolve-Path -LiteralPath $AicPath -ErrorAction Stop).Path
$ManagedDir = Join-Path $AicPath 'AliceInCradle_Data\Managed'
$Solution = Join-Path $RepoRoot 'Polaris.slnx'
$CoreProject = Join-Path $RepoRoot 'PolarisCore\PolarisCore.csproj'
$CoreAssembly = 'PolarisCore'
$Projects = @(
    @{ Name = 'PolarisCore'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisUI'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisRes'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisLang'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisMagic'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisAddons'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisMap'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisDiagnostics'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisSave'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisEvent'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisParticles'; Tfm = 'netstandard2.1' },
    @{ Name = 'PolarisAI'; Tfm = 'netstandard2.1' }
)

$PluginsDir = Join-Path $AicPath 'BepInEx\plugins'
$PolarisDir = Join-Path $PluginsDir 'Polaris'
$LibsDir = Join-Path $PolarisDir 'libs'
$DistDir = Join-Path $RepoRoot 'dist'
$StageRoot = Join-Path $DistDir 'stage\Polaris'
$StageLibs = Join-Path $StageRoot 'libs'
$AssetFiles = @(@{ Source = 'PolarisCore\polaris_icon.png'; Name = 'polaris_icon.png' })

function Write-Step([string] $Text) { Write-Host "=== $Text ===" -ForegroundColor Cyan }

function Invoke-Build {
    & dotnet build $Solution -c $Configuration --nologo "-p:AicGameDir=$AicPath"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $Solution" }
}

function Get-ProjectOutput([hashtable] $Project) {
    Join-Path $RepoRoot "$($Project.Name)\bin\$Configuration\$($Project.Tfm)"
}

function Copy-Unique([object[]] $Files, [string] $Destination) {
    if (-not (Test-Path -LiteralPath $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }
    $Files | Group-Object Name | ForEach-Object {
        Copy-Item -LiteralPath $_.Group[0].FullName -Destination $Destination -Force
    }
}

function Copy-Outputs([string] $Destination, [string] $LibsDestination, [switch] $IncludeSymbols) {
    $patterns = @('*.dll')
    if ($IncludeSymbols) { $patterns += '*.pdb' }
    $files = @()
    foreach ($project in $Projects) {
        $output = Get-ProjectOutput $project
        if (-not (Test-Path -LiteralPath $output)) { throw "Build output directory not found: $output" }
        $files += Get-ChildItem -Path (Join-Path $output '*') -File -Include $patterns
    }
    Copy-Unique @($files | Where-Object { $_.BaseName -eq $CoreAssembly }) $Destination
    Copy-Unique @($files | Where-Object { $_.BaseName -ne $CoreAssembly }) $LibsDestination
}

function Copy-Assets([string] $Destination) {
    foreach ($asset in $AssetFiles) {
        $source = Join-Path $RepoRoot $asset.Source
        if (-not (Test-Path -LiteralPath $source)) { throw "Bundled asset not found: $source" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $Destination $asset.Name) -Force
    }
}

function Get-Version {
    ([xml](Get-Content -LiteralPath $CoreProject -Raw)).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}

try {
    if (-not (Test-Path -LiteralPath $ManagedDir -PathType Container)) {
        throw "AliceInCradle_Data\Managed was not found under AicPath: $AicPath"
    }
    if (-not $Package -and -not (Test-Path -LiteralPath $PluginsDir)) {
        throw "BepInEx plugins directory not found under AicPath: $PluginsDir"
    }
    Write-Step "[1/2] Building Polaris ($Configuration)"
    Invoke-Build
    if ($Package) {
        Write-Step '[2/2] Packaging release zip'
        if (Test-Path -LiteralPath $DistDir) { Remove-Item -LiteralPath $DistDir -Recurse -Force }
        New-Item -ItemType Directory -Path $StageLibs -Force | Out-Null
        Copy-Outputs -Destination $StageRoot -LibsDestination $StageLibs
        Copy-Assets -Destination $StageRoot
        $version = Get-Version
        if (-not $version) { $version = 'unversioned' }
        $zipPath = Join-Path $DistDir "Polaris-v$version.zip"
        Compress-Archive -Path $StageRoot -DestinationPath $zipPath
        Remove-Item -LiteralPath (Join-Path $DistDir 'stage') -Recurse -Force
        Write-Host "Packaged: $zipPath" -ForegroundColor Green
        exit 0
    }
    Write-Step '[2/2] Deploying PolarisCore and component DLLs'
    Copy-Outputs -Destination $PolarisDir -LibsDestination $LibsDir -IncludeSymbols
    Copy-Assets -Destination $PolarisDir
    Write-Host "Deployed: $PolarisDir" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
