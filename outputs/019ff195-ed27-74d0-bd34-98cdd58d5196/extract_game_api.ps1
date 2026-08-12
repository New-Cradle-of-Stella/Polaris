param(
    [string]$GameRoot = 'D:\AliceInCradle Win ver029 - BIE6\AliceInCradle_ver029',
    [string]$OutputPath = '.\game-members.json'
)

Add-Type -Path '.\cecil\Mono.Cecil.dll'

$assemblyPaths = @(
    (Join-Path $GameRoot 'AliceInCradle_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $GameRoot 'AliceInCradle_Data\Managed\unsafeAssem.dll')
)

$wantedTypes = @(
    'XX.IN', 'XX.TX', 'XX.MTRX', 'XX.BGM', 'XX.SND', 'XX.SndPlayer',
    'm2d.M2DBase', 'm2d.Map2d', 'm2d.M2Mover', 'm2d.M2MoverPr', 'm2d.M2Attackable',
    'evt.EV',
    'nel.NelM2DBase', 'nel.PR', 'nel.NelEnemy', 'nel.NightController',
    'nel.ItemStorage', 'nel.NelItem', 'nel.NelItemManager', 'nel.CoinStorage',
    'nel.QuestTracker', 'nel.gm.UiGameMenu'
)

$result = [System.Collections.Generic.List[object]]::new()

foreach ($assemblyPath in $assemblyPaths) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyPath)
    foreach ($type in $assembly.MainModule.Types) {
        if ($wantedTypes -notcontains $type.FullName) { continue }

        $fields = foreach ($field in $type.Fields) {
            [pscustomobject]@{
                kind = 'field'
                name = $field.Name
                signature = $field.FieldType.FullName
                visibility = if ($field.IsPublic) {'public'} elseif ($field.IsFamily) {'protected'} elseif ($field.IsAssembly) {'internal'} else {'private'}
                static = $field.IsStatic
                readonly = $field.IsInitOnly
            }
        }

        $properties = foreach ($property in $type.Properties) {
            $accessor = if ($property.GetMethod) {$property.GetMethod} else {$property.SetMethod}
            [pscustomobject]@{
                kind = 'property'
                name = $property.Name
                signature = $property.PropertyType.FullName
                visibility = if ($accessor.IsPublic) {'public'} elseif ($accessor.IsFamily) {'protected'} elseif ($accessor.IsAssembly) {'internal'} else {'private'}
                static = $accessor.IsStatic
                canRead = $null -ne $property.GetMethod
                canWrite = $null -ne $property.SetMethod
            }
        }

        $methods = foreach ($method in $type.Methods) {
            if ($method.IsConstructor -or $method.IsGetter -or $method.IsSetter -or $method.IsAddOn -or $method.IsRemoveOn) { continue }
            [pscustomobject]@{
                kind = 'method'
                name = $method.Name
                signature = ('{0} {1}({2})' -f $method.ReturnType.FullName, $method.Name, (($method.Parameters | ForEach-Object { '{0} {1}' -f $_.ParameterType.FullName, $_.Name }) -join ', '))
                visibility = if ($method.IsPublic) {'public'} elseif ($method.IsFamily) {'protected'} elseif ($method.IsAssembly) {'internal'} else {'private'}
                static = $method.IsStatic
            }
        }

        $result.Add([pscustomobject]@{
            assembly = [IO.Path]::GetFileName($assemblyPath)
            type = $type.FullName
            baseType = if ($type.BaseType) {$type.BaseType.FullName} else {$null}
            fields = @($fields)
            properties = @($properties)
            methods = @($methods)
        })
    }
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output ("Wrote {0} types to {1}" -f $result.Count, (Resolve-Path $OutputPath))
