param([switch]$Reload, [switch]$Client)
$ErrorActionPreference = 'Stop'
$ciwsProjectPath = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/tml.ps1"
$ciwsLoaderPath = Get-TmlDirectory $ciwsProjectPath
$ciwsProfilePath = Join-Path $ciwsProjectPath $(if ($Client) { 'artifacts/CIWSClientProfile' } else { 'artifacts/CIWSProfile' })
foreach ($folder in 'Mods','Worlds','Players') { New-Item -ItemType Directory -Path (Join-Path $ciwsProfilePath $folder) -Force | Out-Null }
Copy-Item -LiteralPath (Join-Path $ciwsProjectPath 'artifacts/ModernArsenal.tmod') -Destination (Join-Path $ciwsProfilePath 'Mods/ModernArsenal.tmod') -Force
if ($Client) {
    foreach ($extension in 'wld','twld') { Copy-Item -LiteralPath (Join-Path $ciwsProjectPath "artifacts/CIWSProfile/Worlds/Arsenal.$extension") -Destination (Join-Path $ciwsProfilePath "Worlds/Arsenal.$extension") -Force }
    Get-ChildItem -LiteralPath (Join-Path $ciwsProjectPath 'Play/Players') -File | Where-Object { $_.Extension -in '.plr','.tplr' } | Copy-Item -Destination (Join-Path $ciwsProfilePath 'Players') -Force
    foreach ($name in 'BossRushAPI','ExampleBossRush') { Copy-Item -LiteralPath (Join-Path $ciwsProjectPath "Play/Mods/$name.tmod") -Destination (Join-Path $ciwsProfilePath "Mods/$name.tmod") -Force }
    [IO.File]::WriteAllText((Join-Path $ciwsProfilePath 'Mods/enabled.json'),'["ModernArsenal","BossRushAPI","ExampleBossRush"]')
    [IO.File]::WriteAllText((Join-Path $ciwsProfilePath 'config.json'),'{"Language":"zh-Hans","Fullscreen":false,"WindowBorderless":false,"DisplayWidth":1600,"DisplayHeight":900,"AutoPause":false,"LastLaunchedTModLoaderVersion":"2026.8.3.0"}')
    $ciwsReportPath = Join-Path $ciwsProjectPath 'artifacts/ciws-client-results.json'
    if (Test-Path -LiteralPath $ciwsReportPath) { Remove-Item -LiteralPath $ciwsReportPath }
    $env:PATH = (Join-Path $ciwsLoaderPath 'Libraries/Native/Windows') + ';' + $env:PATH
    $ciwsArguments = @('tModLoader.dll','-nosteam','-skipselect','新兵:现代军械·射击场','-tmlsavedirectory',('"'+$ciwsProfilePath+'"'),'-arsenalciwsclient',('"'+$ciwsReportPath+'"'))
    $ciwsProcess = Start-Process -FilePath (Join-Path $ciwsLoaderPath 'dotnet/dotnet.exe') -ArgumentList $ciwsArguments -WorkingDirectory $ciwsLoaderPath -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $ciwsProjectPath 'artifacts/ciws-client-console.log') -RedirectStandardError (Join-Path $ciwsProjectPath 'artifacts/ciws-client-error.log')
    $ciwsProcess | Select-Object Id,StartTime | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ciwsProjectPath 'artifacts/ciws-client-process.json') -Encoding utf8
    Write-Output "CIWS client verification PID $($ciwsProcess.Id)"
    return
}
[IO.File]::WriteAllText((Join-Path $ciwsProfilePath 'Mods/enabled.json'),'["ModernArsenal"]')
if (!$Reload) {
    if (!(Test-Path -LiteralPath (Join-Path $ciwsProjectPath 'artifacts/TestProfile/Worlds/Arsenal.wld'))) { & "$PSScriptRoot/test.ps1" }
    foreach ($extension in 'wld','twld') { Copy-Item -LiteralPath (Join-Path $ciwsProjectPath "artifacts/TestProfile/Worlds/Arsenal.$extension") -Destination (Join-Path $ciwsProfilePath "Worlds/Arsenal.$extension") -Force }
}
$ciwsConfiguration = Join-Path $ciwsProfilePath 'serverconfig.txt'
[IO.File]::WriteAllText($ciwsConfiguration,("world="+(Join-Path $ciwsProfilePath 'Worlds/Arsenal.wld')+"`nport=7793`nupnp=0`nlanguage=zh-Hans`n"))
$ciwsMode = if ($Reload) { 'ciwsreload' } else { 'ciwstest' }
$ciwsReportPath = Join-Path $ciwsProjectPath "artifacts/$ciwsMode-results.json"
Push-Location $ciwsLoaderPath
try {
    & (Join-Path $ciwsLoaderPath 'dotnet/dotnet.exe') tModLoader.dll -server -nosteam -ip 127.0.0.1 -tmlsavedirectory $ciwsProfilePath -config $ciwsConfiguration "-arsenal$ciwsMode" $ciwsReportPath *> (Join-Path $ciwsProjectPath "artifacts/$ciwsMode-console.log")
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $ciwsReportPath; throw 'CIWS verification failed.' }
} finally { Pop-Location }
Get-Content -LiteralPath $ciwsReportPath
