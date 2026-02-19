<#
.SYNOPSIS
  Собирает единый NuGet-пакет TMS.Z.Blazor.Diagrams (включает Core).
.EXAMPLE
  .\build-nuget.ps1
  .\build-nuget.ps1 -Push -ApiKey $env:NUGET_API_KEY -Source https://api.nuget.org/v3/index.json
#>
param(
    [string]$OutputPath = "nupkgs",
    [switch]$Push,
    [string]$ApiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptDir

try {
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

    Write-Host "Building TMS.Z.Blazor.Diagrams (includes Core)..." -ForegroundColor Cyan
    dotnet pack src/Blazor.Diagrams/Blazor.Diagrams.csproj -c Release -o $OutputPath

    Write-Host ("`nPackage created in {0}:" -f $OutputPath) -ForegroundColor Green
    Get-ChildItem $OutputPath -Filter "TMS.Z.Blazor.Diagrams.*.nupkg" | Where-Object { $_.Name -notlike "*.Core.*" } | ForEach-Object { Write-Host "  - $($_.Name)" }

    if ($Push) {
        if (-not $ApiKey) { throw "ApiKey required for -Push" }
        $pkg = Get-ChildItem $OutputPath -Filter "TMS.Z.Blazor.Diagrams.*.nupkg" | Where-Object { $_.Name -notlike "*.Core.*" } | Select-Object -First 1
        if ($pkg) { dotnet nuget push $pkg.FullName -k $ApiKey -s $Source --skip-duplicate }
    }
}
finally {
    Pop-Location
}
