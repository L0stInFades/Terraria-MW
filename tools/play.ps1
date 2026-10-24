param([switch]$Pause, [switch]$Ciws, [switch]$Flight)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/tml.ps1"
$loaderPath = Get-TmlDirectory $projectPath
$playPath = Join-Path $projectPath 'Play'
$running = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine -like "*$playPath*" -and $_.CommandLine -notlike '*-server*' }
if ($running) { Write-Output "Modern Arsenal is already running as PID $($running.ProcessId)."; return }
if (!(Test-Path -LiteralPath (Join-Path $projectPath 'artifacts/ModernArsenal.tmod'))) { & "$PSScriptRoot/build.ps1" }
if (!(Test-Path -LiteralPath (Join-Path $playPath 'Worlds/Arsenal.wld')) -or !(Test-Path -LiteralPath (Join-Path $playPath 'Players/新兵.plr'))) { & "$PSScriptRoot/test.ps1" -Prepare }
Copy-Item -LiteralPath (Join-Path $projectPath 'artifacts/ModernArsenal.tmod') -Destination (Join-Path $playPath 'Mods/ModernArsenal.tmod') -Force
$configPath = Join-Path $playPath 'config.json'
if (!(Test-Path -LiteralPath $configPath)) {
    [IO.File]::WriteAllText($configPath,'{"Language":"zh-Hans","Fullscreen":false,"DisplayWidth":1600,"DisplayHeight":900,"PauseWhenInactive":false,"LastLaunchedTModLoaderVersion":"2026.8.3.0"}')
}
$env:PATH = (Join-Path $loaderPath 'Libraries/Native/Windows') + ';' + $env:PATH
$arguments = @('tModLoader.dll','-nosteam','-skipselect','新兵:现代军械·射击场','-tmlsavedirectory',('"'+$playPath+'"'))
if ($Flight) { $arguments += @('-arsenalflight',('"'+(Join-Path $projectPath 'artifacts/flight-results.json')+'"')) }
elseif ($Ciws) { $arguments += @('-arsenalciwssetup',('"'+(Join-Path $projectPath 'artifacts/ciws-setup-results.json')+'"')) }
elseif ($Pause) { $arguments += @('-arsenalhandoff',('"'+(Join-Path $projectPath 'artifacts/handoff-results.json')+'"')) }
Start-Process -FilePath (Join-Path $loaderPath 'dotnet/dotnet.exe') -ArgumentList $arguments -WorkingDirectory $loaderPath -WindowStyle Hidden | Out-Null
