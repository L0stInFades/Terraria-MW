$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/tml.ps1"
$loaderPath = Get-TmlDirectory $projectPath
$profilePath = Join-Path $projectPath 'artifacts/PresentationProfile'
foreach ($folder in 'Mods','Worlds','Players') { New-Item -ItemType Directory -Path (Join-Path $profilePath $folder) -Force | Out-Null }
foreach ($folder in 'Worlds','Players') {
    Get-ChildItem -LiteralPath (Join-Path $projectPath "Play/$folder") -File | Where-Object { $_.Extension -in '.wld','.twld','.plr','.tplr' } | Copy-Item -Destination (Join-Path $profilePath $folder) -Force
}
Copy-Item -LiteralPath (Join-Path $projectPath 'artifacts/ModernArsenal.tmod') -Destination (Join-Path $profilePath 'Mods/ModernArsenal.tmod') -Force
[IO.File]::WriteAllText((Join-Path $profilePath 'Mods/enabled.json'),'["ModernArsenal"]')
[IO.File]::WriteAllText((Join-Path $profilePath 'config.json'),'{"Language":"zh-Hans","Fullscreen":false,"DisplayWidth":1600,"DisplayHeight":900,"PauseWhenInactive":false,"LastLaunchedTModLoaderVersion":"2026.8.3.0"}')
$reportPath = Join-Path $projectPath 'artifacts/presentation-results.json'
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath }
$env:PATH = (Join-Path $loaderPath 'Libraries/Native/Windows') + ';' + $env:PATH
$arguments = @('tModLoader.dll','-nosteam','-skipselect','新兵:现代军械·射击场','-tmlsavedirectory',('"'+$profilePath+'"'),'-arsenalpresentation',('"'+$reportPath+'"'))
$client = Start-Process -FilePath (Join-Path $loaderPath 'dotnet/dotnet.exe') -ArgumentList $arguments -WorkingDirectory $loaderPath -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $projectPath 'artifacts/presentation-console.log') -RedirectStandardError (Join-Path $projectPath 'artifacts/presentation-error.log')
$client | Select-Object Id,StartTime | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectPath 'artifacts/presentation-process.json')
Write-Output "Presentation test PID $($client.Id)"
