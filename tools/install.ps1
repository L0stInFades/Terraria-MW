$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$packagePath = Join-Path $projectPath 'artifacts/ModernArsenal.tmod'
if (!(Test-Path -LiteralPath $packagePath)) { & "$PSScriptRoot/build.ps1" }
$profiles = @((Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games/Terraria/tModLoader'))
$runPath = Join-Path (Split-Path -Parent $projectPath) 'ModernEngineering/artifacts/current-fresh-run.json'
if (Test-Path -LiteralPath $runPath) {
    $run = Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json
    if (Test-Path -LiteralPath $run.world) { $profiles += $run.profile }
}
$installed = @()
foreach ($profile in $profiles | Select-Object -Unique) {
    $modsPath = Join-Path $profile 'Mods'
    New-Item -ItemType Directory -Path $modsPath -Force | Out-Null
    $enabledPath = Join-Path $modsPath 'enabled.json'
    $enabled = New-Object 'System.Collections.Generic.List[string]'
    if (Test-Path -LiteralPath $enabledPath) {
        $parsed = Get-Content -LiteralPath $enabledPath -Raw | ConvertFrom-Json
        foreach ($name in $parsed) {
            if (![string]::IsNullOrWhiteSpace($name) -and !$enabled.Contains($name)) { $enabled.Add($name) }
        }
    }
    if (!$enabled.Contains('ModernArsenal')) {
        if ((Test-Path -LiteralPath $enabledPath) -and !(Test-Path -LiteralPath ($enabledPath + '.before-ModernArsenal'))) {
            Copy-Item -LiteralPath $enabledPath -Destination ($enabledPath + '.before-ModernArsenal')
        }
        $enabled.Add('ModernArsenal')
    }
    [IO.File]::WriteAllText($enabledPath,(ConvertTo-Json -InputObject $enabled.ToArray()))
    Copy-Item -LiteralPath $packagePath -Destination (Join-Path $modsPath 'ModernArsenal.tmod') -Force
    $saved = Get-Content -LiteralPath $enabledPath -Raw
    $restored = $saved | ConvertFrom-Json
    if (!$saved.TrimStart().StartsWith('[') -or 'ModernArsenal' -notin $restored -or ($enabled | Where-Object { $_ -notin $restored }).Count) { throw 'Mod enable-list verification failed.' }
    $installed += [PSCustomObject]@{ profile=$profile; package=(Join-Path $modsPath 'ModernArsenal.tmod'); sha256=(Get-FileHash -LiteralPath (Join-Path $modsPath 'ModernArsenal.tmod') -Algorithm SHA256).Hash }
}
$installed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectPath 'artifacts/installed.json') -Encoding utf8
$installed | Format-Table -AutoSize
