[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Path,

    [string[]]$Extensions = @(
        '.cs', '.csx', '.fs', '.fsx', '.vb',
        '.ps1', '.psm1', '.psd1', '.py', '.rb', '.go', '.rs',
        '.c', '.h', '.cc', '.cpp', '.cxx', '.hpp',
        '.java', '.kt', '.kts', '.swift',
        '.js', '.jsx', '.mjs', '.cjs', '.ts', '.tsx', '.vue', '.svelte',
        '.html', '.htm', '.css', '.scss', '.sass', '.less',
        '.xml', '.xaml', '.csproj', '.fsproj', '.vbproj', '.props', '.targets',
        '.slnx', '.json', '.jsonc', '.yaml', '.yml', '.toml',
        '.sh', '.bash', '.zsh', '.sql'
    ),

    [string[]]$ExcludeDirectories = @(
        '.git', '.vs', '.idea', '.vscode', '.claude', '.codex', '.codex-work', '.agents',
        'bin', 'obj', 'node_modules', 'packages',
        'TestResults', 'coverage', '.coverage', '__pycache__'
    ),

    [switch]$Json,

    [switch]$NoProgress
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Path)) {
    if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $Path = (Get-Location).Path
    }
    else {
        $Path = $PSScriptRoot
    }
}

function Test-CommentOnlyLine {
    param(
        [Parameter(Mandatory)]
        [string]$Line,

        [Parameter(Mandatory)]
        [string]$Extension,

        [Parameter(Mandatory)]
        [ref]$InBlockComment
    )

    $trimmed = $Line.Trim()
    if ($trimmed.Length -eq 0) {
        return $false
    }

    $xmlExtensions = @('.xml', '.xaml', '.csproj', '.fsproj', '.vbproj', '.props', '.targets', '.slnx', '.html', '.htm', '.vue', '.svelte')
    $hashExtensions = @('.ps1', '.psm1', '.psd1', '.py', '.rb', '.sh', '.bash', '.zsh', '.yaml', '.yml', '.toml')
    $sqlExtensions = @('.sql')

    if ($xmlExtensions -contains $Extension) {
        $blockStart = '<!--'
        $blockEnd = '-->'
        $linePrefix = $null
    }
    elseif ($hashExtensions -contains $Extension) {
        if ($Extension -in @('.ps1', '.psm1', '.psd1')) {
            $blockStart = '<#'
            $blockEnd = '#>'
        }
        else {
            $blockStart = $null
            $blockEnd = $null
        }
        $linePrefix = '#'
    }
    elseif ($sqlExtensions -contains $Extension) {
        $blockStart = '/*'
        $blockEnd = '*/'
        $linePrefix = '--'
    }
    elseif ($Extension -eq '.json') {
        return $false
    }
    else {
        $blockStart = '/*'
        $blockEnd = '*/'
        $linePrefix = '//'
    }

    $remaining = $trimmed
    $sawComment = $false

    while ($remaining.Length -gt 0) {
        if ($InBlockComment.Value) {
            $sawComment = $true
            $endIndex = $remaining.IndexOf($blockEnd, [StringComparison]::Ordinal)
            if ($endIndex -lt 0) {
                return $true
            }

            $remaining = $remaining.Substring($endIndex + $blockEnd.Length).TrimStart()
            $InBlockComment.Value = $false
            continue
        }

        if ($linePrefix -and $remaining.StartsWith($linePrefix, [StringComparison]::Ordinal)) {
            return $true
        }

        if ($blockStart -and $remaining.StartsWith($blockStart, [StringComparison]::Ordinal)) {
            $sawComment = $true
            $InBlockComment.Value = $true
            $remaining = $remaining.Substring($blockStart.Length)
            continue
        }

        return $false
    }

    return $sawComment
}

$root = (Resolve-Path -LiteralPath $Path).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Path is not a directory: $root"
}
$pathSeparators = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

$extensionSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($extension in $Extensions) {
    $normalized = if ($extension.StartsWith('.')) { $extension } else { ".$extension" }
    [void]$extensionSet.Add($normalized)
}

$excludedSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($directory in $ExcludeDirectories) {
    [void]$excludedSet.Add($directory)
}

if (-not $NoProgress) {
    Write-Progress -Id 1 -Activity 'Count code lines' -Status 'Scanning files...'
}

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    if (-not $extensionSet.Contains($_.Extension)) {
        return $false
    }

    $relativePath = $_.FullName.Substring($root.Length).TrimStart($pathSeparators)
    foreach ($segment in $relativePath.Split([System.IO.Path]::DirectorySeparatorChar)) {
        if ($excludedSet.Contains($segment)) {
            return $false
        }
    }
    return $true
})

$processedFiles = 0
$results = @(foreach ($file in $files) {
    $processedFiles++
    if (-not $NoProgress) {
        $percentComplete = if ($files.Count -eq 0) { 100 } else { [math]::Floor($processedFiles * 100 / $files.Count) }
        $relativeFile = $file.FullName.Substring($root.Length).TrimStart($pathSeparators)
        Write-Progress -Id 1 -Activity 'Count code lines' `
            -Status "Processing $processedFiles / $($files.Count)" `
            -CurrentOperation $relativeFile `
            -PercentComplete $percentComplete
    }

    $total = 0
    $blank = 0
    $comment = 0
    $inBlockComment = $false
    $extension = $file.Extension.ToLowerInvariant()

    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $total++
        if ([string]::IsNullOrWhiteSpace($line)) {
            $blank++
        }
        elseif (Test-CommentOnlyLine -Line $line -Extension $extension -InBlockComment ([ref]$inBlockComment)) {
            $comment++
        }
    }

    [pscustomobject]@{
        Extension = $extension
        Total     = $total
        Blank     = $blank
        Comment   = $comment
        Code      = $total - $blank - $comment
    }
})

if (-not $NoProgress) {
    Write-Progress -Id 1 -Activity 'Count code lines' -Completed
}

$byExtension = @($results | Group-Object Extension | ForEach-Object {
    [pscustomobject]@{
        Extension = $_.Name
        Files     = $_.Count
        Total     = [long]($_.Group | Measure-Object Total -Sum).Sum
        Blank     = [long]($_.Group | Measure-Object Blank -Sum).Sum
        Comment   = [long]($_.Group | Measure-Object Comment -Sum).Sum
        Code      = [long]($_.Group | Measure-Object Code -Sum).Sum
    }
} | Sort-Object Code -Descending)

$summary = [pscustomobject]@{
    Path      = $root
    Files     = $files.Count
    Total     = [long]($results | Measure-Object Total -Sum).Sum
    Blank     = [long]($results | Measure-Object Blank -Sum).Sum
    Comment   = [long]($results | Measure-Object Comment -Sum).Sum
    Code      = [long]($results | Measure-Object Code -Sum).Sum
    Languages = $byExtension
}

if ($Json) {
    $summary | ConvertTo-Json -Depth 4
    return
}

Write-Host "Root: $($summary.Path)"
Write-Host ""
$byExtension | Format-Table -AutoSize
Write-Host ""
[pscustomobject]@{
    Files   = $summary.Files
    Total   = $summary.Total
    Blank   = $summary.Blank
    Comment = $summary.Comment
    Code    = $summary.Code
} | Format-Table -AutoSize
