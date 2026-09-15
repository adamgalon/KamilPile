<#
    Builds a standalone MetrykiPali.exe that runs offline on any Windows 10/11
    machine - no .NET installation required on the target computer.

    Usage:  powershell -ExecutionPolicy Bypass -File publish.ps1
    Result: publish\MetrykiPali.exe
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\MetrykiPali\MetrykiPali.csproj'
$output = Join-Path $root 'publish'

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $output

Write-Host ''
Write-Host "Gotowe: $(Join-Path $output 'MetrykiPali.exe')" -ForegroundColor Green
