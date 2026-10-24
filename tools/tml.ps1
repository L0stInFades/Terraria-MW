function Get-TmlDirectory {
    param([string]$ProjectPath)
    $tmlDirectory = if ($env:TML_PATH) { $env:TML_PATH } else {
        Join-Path (Split-Path -Parent (Split-Path -Parent $ProjectPath)) 'tModLoader'
    }
    $tmlDirectory = [IO.Path]::GetFullPath($tmlDirectory)
    if (!(Test-Path -LiteralPath (Join-Path $tmlDirectory 'tModLoader.dll')) -or
        !(Test-Path -LiteralPath (Join-Path $tmlDirectory 'dotnet/dotnet.exe'))) {
        throw 'Set TML_PATH to your Windows tModLoader installation directory. See docs/DEVELOPMENT.md.'
    }
    return $tmlDirectory
}
