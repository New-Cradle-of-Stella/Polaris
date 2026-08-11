<#
.SYNOPSIS
    编译 Polaris，部署进本机游戏，或打包成给玩家的发布 zip。

.DESCRIPTION
    Polaris 是单个 BepInEx 插件（Polaris.dll），一次构建即可。产物落地分两处：

      BepInEx\plugins\Polaris\Polaris.dll   插件本身
      BepInEx\plugins\Polaris\*.png         插件自带的图片（logo，见 PolarisBrandImages）
      BepInEx\plugins\Polaris\libs\*.dll    随包分发的第三方依赖

    分开是为了让"哪个是插件、哪些只是它的依赖"一眼可辨——目前 libs\ 里是 NVorbis
    （.ogg 解码）以及它拉进来的 System.Memory / System.Buffers / System.Numerics.Vectors /
    System.Runtime.CompilerServices.Unsafe。Polaris 的错误归因也靠这个位置把它们判成
    "模组依赖"而不是可定责的插件，见 PathsAPI.LibsDir。放在子目录里不影响加载：BepInEx 的
    运行时程序集解析对 plugins\ 是连同全部子目录递归查找的（Mono 版 LocalResolve →
    Utility.TryResolveDllAssembly）。

    "整个 bin\ 目录里的 dll 都跟着拷"是安全的，不需要另外维护一份白名单：Polaris.csproj 已经
    确保那里只含真正该分发的东西——游戏/Unity 自带程序集（Assembly-CSharp / unsafeAssem /
    pixelliner / Newtonsoft.Json）全部标了 Private=false 不会出现；而 NuGet 包依赖靠
    CopyLocalLockFileAssemblies 才会真的落地到 bin\ 而不是只停留在 .deps.json 里。

    两种用法二选一或都要：
      - 默认（不加 -Package）：部署进 -DeployDir 指向的、装了 BepInEx 的本机游戏安装，
        用于开发内循环——和 csproj 编译期引用的"干净"安装（Directory.Build.props 里的
        AicGameDir）刻意分开，否则"引用的程序集"和"运行时加载的程序集"会来自同一份被模组
        污染过的安装。
      - 加 -Package：不需要本机装游戏，强制用 Release 配置构建，把产物整理成玩家可以直接
        解压进 BepInEx\plugins\ 的目录结构，打成 dist\Polaris-v<版本号>.zip。
        版本号取 Polaris.csproj 的 <Version>。
    PolarisTools（VSIX）不在这个脚本管的范围内：面向的是模组开发者而不是玩家，分发渠道
    是 VSIX 文件本身（或 VS Marketplace），装法和 BepInEx 插件完全不是一回事。

.PARAMETER DeployDir
    装了 BepInEx 的游戏根目录。缺省读环境变量 AIC_DEPLOY_DIR，再缺省用下面的默认值。
    仅在不加 -Package 时使用。

.PARAMETER Configuration
    构建配置，默认 Debug；加 -Package 时忽略此参数，强制用 Release。

.PARAMETER Package
    打一份可以直接发给玩家的发布 zip 到 .\dist\，不部署到本机游戏。

.EXAMPLE
    .\deploy-polaris.ps1
    .\deploy-polaris.ps1 -Configuration Release
    .\deploy-polaris.ps1 -Package
#>
[CmdletBinding()]
param(
    [string] $DeployDir = $(if ($env:AIC_DEPLOY_DIR) { $env:AIC_DEPLOY_DIR }
                            else { 'D:\AliceInCradle Win ver029 - BIE6\AliceInCradle_ver029' }),
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $Package
)

if ($Package) {
    # 发布包必须是 Release：调用方就算显式传了 -Configuration Debug 也不能悄悄打出一份
    # 带调试符号/未优化代码的"发布"zip 给玩家。
    $Configuration = 'Release'
}

$ErrorActionPreference = 'Stop'

$Root        = $PSScriptRoot
$Project     = Join-Path $Root 'Polaris.csproj'
$AssemblyName = 'Polaris'
$Tfm         = 'netstandard2.1'

$PluginsDir = Join-Path $DeployDir 'BepInEx\plugins'
$PolarisDir = Join-Path $PluginsDir 'Polaris'
$LibsDir    = Join-Path $PolarisDir 'libs'

# 随插件一起分发的图片资源（相对仓库根）。只列真正被代码用到的：另外三张 *_icon.png 是
# README 里的插图，不进包。
$AssetFiles = @('polaris_icon.png')

$DistDir      = Join-Path $Root 'dist'
$PackageStage = Join-Path $DistDir 'stage\Polaris'
$PackageLibs  = Join-Path $PackageStage 'libs'

function Write-Step([string] $Text) {
    Write-Host "=== $Text ===" -ForegroundColor Cyan
}

# dotnet 失败时只有 $LASTEXITCODE 会变，不会抛异常，所以每次都要显式检查。
function Invoke-Build([string] $ProjectPath) {
    & dotnet build $ProjectPath -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $ProjectPath"
    }
}

function Get-OutputDir {
    Join-Path $Root "bin\$Configuration\$Tfm"
}

# 取 csproj 里的 <Version>，用于给发布 zip 命名。用不着 Import-Module 引完整 MSBuild，
# 项目文件本身就是合法 XML，直接当 XML 读就够了。
function Get-ProjectVersion([string] $CsprojPath) {
    ([xml](Get-Content -LiteralPath $CsprojPath -Raw)).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}

function Copy-Into([object[]] $Items, [string] $Destination) {
    if ($Items.Count -eq 0) {
        return
    }
    if (-not (Test-Path -LiteralPath $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }
    $Items | Copy-Item -Destination $Destination -Force
}

# 把 bin\ 里"实际存在的每一个 dll"都拷走，主产物与第三方依赖分到两个目的地
# （理由见文件头 .DESCRIPTION）。
function Copy-Output([string] $Destination, [string] $LibsDestination, [switch] $IncludeSymbols) {
    $srcDir = Get-OutputDir
    if (-not (Test-Path -LiteralPath $srcDir)) {
        throw "Build output directory not found: $srcDir"
    }

    $mainDll = Join-Path $srcDir "$AssemblyName.dll"
    if (-not (Test-Path -LiteralPath $mainDll)) {
        throw "Build artifact not found: $mainDll"
    }

    $patterns = @('*.dll')
    if ($IncludeSymbols) {
        $patterns += '*.pdb'
    }
    # -Include 只有配合带通配符的 -Path 才会真正生效；-LiteralPath + -Include 会被
    # PowerShell 静默忽略，结果是不管三七二十一把目录里所有文件（含 .deps.json/.pdb）
    # 都拷过去——这个坑已经用实际输出验证过，不是理论风险。
    $files = @(Get-ChildItem -Path (Join-Path $srcDir '*') -File -Include $patterns)

    # BaseName 只剥最后一级扩展名，所以 System.Runtime.CompilerServices.Unsafe.dll 也能正确
    # 和主程序集名比较；Polaris.dll / Polaris.pdb 则一起归到"主产物"这一侧。
    $mainFiles = @($files | Where-Object { $_.BaseName -eq $AssemblyName })
    $depFiles  = @($files | Where-Object { $_.BaseName -ne $AssemblyName })

    Copy-Into $mainFiles $Destination
    Copy-Into $depFiles  $LibsDestination
}

# 插件自带的图片：PolarisBrandImages 按硬编码的文件名从 plugins\Polaris\ 直接取，
# 所以它必须和 Polaris.dll 同级躺在那里。不走 bin\（那里只有编译产物），直接从仓库根拷。
# 缺文件就报错而不是静默跳过：那会做出一个 logo 消失的包，等玩家反馈才发现。
function Copy-Assets([string] $Destination) {
    $missing = @($AssetFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $Root $_)) })
    if ($missing.Count -gt 0) {
        throw "Bundled asset(s) not found in the repository root: $($missing -join ', ')"
    }

    Copy-Into @($AssetFiles | ForEach-Object { Get-Item -LiteralPath (Join-Path $Root $_) }) $Destination
}

try {
    if (-not $Package -and -not (Test-Path -LiteralPath $PluginsDir)) {
        throw "BepInEx plugins directory not found: $PluginsDir`nPass -DeployDir, or set the AIC_DEPLOY_DIR environment variable to the game root that has BepInEx installed, or use -Package to build a release zip instead (no local game install needed)."
    }

    Write-Step "[1/2] Building Polaris ($Configuration)"
    Invoke-Build $Project

    if ($Package) {
        Write-Step '[2/2] Packaging release zip'

        if (Test-Path -LiteralPath $DistDir) {
            Remove-Item -LiteralPath $DistDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $PackageStage -Force | Out-Null

        Copy-Output -Destination $PackageStage -LibsDestination $PackageLibs
        Copy-Assets -Destination $PackageStage

        $version = Get-ProjectVersion $Project
        if (-not $version) {
            $version = 'unversioned'
        }
        $zipPath = Join-Path $DistDir "Polaris-v$version.zip"
        Compress-Archive -Path (Join-Path (Join-Path $DistDir 'stage') 'Polaris') -DestinationPath $zipPath
        Remove-Item -LiteralPath (Join-Path $DistDir 'stage') -Recurse -Force

        Write-Host ''
        Write-Host "Packaged: $zipPath" -ForegroundColor Green
        Write-Host 'Players extract the whole Polaris\ directory from the zip into BepInEx\plugins\.' -ForegroundColor Green
        exit 0
    }

    Write-Step '[2/2] Deploying to the game at BepInEx\plugins\Polaris\'
    Copy-Output -Destination $PolarisDir -LibsDestination $LibsDir -IncludeSymbols
    Copy-Assets -Destination $PolarisDir

    Write-Host ''
    Write-Host "Deployed: $PolarisDir" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ''
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
