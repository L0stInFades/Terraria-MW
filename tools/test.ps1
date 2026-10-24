param([switch]$Reload, [switch]$Prepare)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/tml.ps1"
$loaderPath = Get-TmlDirectory $projectPath
$profilePath = Join-Path $projectPath $(if ($Prepare) { 'Play' } else { 'artifacts/TestProfile' })
foreach ($folder in 'Mods','Worlds','Players') { New-Item -ItemType Directory -Path (Join-Path $profilePath $folder) -Force | Out-Null }
Copy-Item -LiteralPath (Join-Path $projectPath 'artifacts/ModernArsenal.tmod') -Destination (Join-Path $profilePath 'Mods/ModernArsenal.tmod') -Force
[IO.File]::WriteAllText((Join-Path $profilePath 'Mods/enabled.json'), '["ModernArsenal"]')
$worldPath = Join-Path $profilePath 'Worlds/Arsenal.wld'
if ($Reload -and !(Test-Path -LiteralPath $worldPath)) { throw 'Run the first test before the reload test.' }
$configuration = @"
world=$worldPath
autocreate=1
seed=Modern-Arsenal-20261024
worldname=现代军械·射击场
difficulty=0
maxplayers=2
port=7796
upnp=0
language=zh-Hans
"@
$configPath = Join-Path $profilePath 'serverconfig.txt'
[IO.File]::WriteAllText($configPath, $configuration)
$mode = if ($Prepare) { 'prepare' } elseif ($Reload) { 'reload' } else { 'test' }
$report = Join-Path $projectPath "artifacts/$mode-results.json"
Push-Location $loaderPath
try {
    & (Join-Path $loaderPath 'dotnet/dotnet.exe') 'tModLoader.dll' -server -nosteam -ip 127.0.0.1 -tmlsavedirectory $profilePath -config $configPath "-arsenal$mode" $report *> (Join-Path $projectPath "artifacts/$mode-console.log")
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $report -ErrorAction SilentlyContinue; throw "Arsenal $mode failed." }
} finally { Pop-Location }
Get-Content -LiteralPath $report
