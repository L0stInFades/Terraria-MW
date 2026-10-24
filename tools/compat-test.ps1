$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$engineeringPath = Join-Path (Split-Path -Parent $projectPath) 'ModernEngineering'
. "$PSScriptRoot/tml.ps1"
$loaderPath = Get-TmlDirectory $projectPath
$run = Get-Content -LiteralPath (Join-Path $engineeringPath 'artifacts/current-fresh-run.json') -Raw | ConvertFrom-Json
$sourceWorld = [IO.Path]::GetFullPath($run.world)
if (!(Test-Path -LiteralPath $sourceWorld)) { throw 'Existing engineering world is missing.' }
$beforeHash = (Get-FileHash -LiteralPath $sourceWorld -Algorithm SHA256).Hash
$profilePath = Join-Path $projectPath 'artifacts/CompatProfile'
foreach ($folder in 'Mods','Worlds') { New-Item -ItemType Directory -Path (Join-Path $profilePath $folder) -Force | Out-Null }
$worldPath = Join-Path $profilePath 'Worlds/Engineering.wld'
Copy-Item -LiteralPath $sourceWorld -Destination $worldPath -Force
Copy-Item -LiteralPath ([IO.Path]::ChangeExtension($sourceWorld,'.twld')) -Destination ([IO.Path]::ChangeExtension($worldPath,'.twld')) -Force
Copy-Item -LiteralPath (Join-Path $projectPath 'artifacts/ModernArsenal.tmod') -Destination (Join-Path $profilePath 'Mods/ModernArsenal.tmod') -Force
Copy-Item -LiteralPath (Join-Path $engineeringPath 'artifacts/ModernEngineering.tmod') -Destination (Join-Path $profilePath 'Mods/ModernEngineering.tmod') -Force
[IO.File]::WriteAllText((Join-Path $profilePath 'Mods/enabled.json'),'["ModernEngineering","ModernArsenal"]')
$configPath = Join-Path $profilePath 'serverconfig.txt'
[IO.File]::WriteAllText($configPath,"world=$worldPath`nport=7797`nupnp=0`nlanguage=zh-Hans`n")
$report = Join-Path $projectPath 'artifacts/compat-results.json'
Push-Location $loaderPath
try {
    & (Join-Path $loaderPath 'dotnet/dotnet.exe') 'tModLoader.dll' -server -nosteam -ip 127.0.0.1 -tmlsavedirectory $profilePath -config $configPath -arsenalcompat $report *> (Join-Path $projectPath 'artifacts/compat-console.log')
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $report -ErrorAction SilentlyContinue; throw 'Compatibility test failed.' }
} finally { Pop-Location }
if ((Get-FileHash -LiteralPath $sourceWorld -Algorithm SHA256).Hash -ne $beforeHash) { throw 'Source world changed during the isolated compatibility test.' }
Get-Content -LiteralPath $report
Write-Output 'The original engineering world file is unchanged.'
