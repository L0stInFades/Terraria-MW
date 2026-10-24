$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/tml.ps1"
$loaderPath = Get-TmlDirectory $projectPath
if ((Split-Path -Leaf $projectPath) -ne 'ModernArsenal') {
    throw 'tModLoader requires the source folder name ModernArsenal. Clone Terraria-MW into a folder named ModernArsenal; see docs/DEVELOPMENT.md.'
}
$profilePath = Join-Path $projectPath 'artifacts/BuildProfile'
New-Item -ItemType Directory -Path $profilePath -Force | Out-Null
Push-Location $loaderPath
try {
    & (Join-Path $loaderPath 'dotnet/dotnet.exe') 'tModLoader.dll' -server -nosteam -tmlsavedirectory $profilePath -build $projectPath
    if ($LASTEXITCODE -ne 0) { throw "tModLoader build failed: $LASTEXITCODE" }
} finally { Pop-Location }
Copy-Item -LiteralPath (Join-Path $profilePath 'Mods/ModernArsenal.tmod') -Destination (Join-Path $projectPath 'artifacts/ModernArsenal.tmod') -Force
