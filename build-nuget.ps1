<#
.SYNOPSIS
  Собирает единый NuGet-пакет TMS.Z.Blazor.Diagrams (включает Core).
.EXAMPLE
  .\build-nuget.ps1
  .\build-nuget.ps1 -Push -ApiKey $env:NUGET_API_KEY -Source https://api.nuget.org/v3/index.json
#>
param(
    [string]$OutputPath = "nupkgs",
    [switch]$CleanOutput,
    [switch]$Push,
    [string]$ApiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptDir

try {
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

    # По умолчанию включаем безопасный режим: удаляем старые nupkg этого пакета,
    # чтобы не было риска взять "предыдущую" сборку.
    if (-not $PSBoundParameters.ContainsKey("CleanOutput")) {
        $CleanOutput = $true
    }
    if ($CleanOutput) {
        Get-ChildItem $OutputPath -Filter "TMS.Z.Blazor.Diagrams.*.nupkg" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notlike "*.Core.*" } |
            Remove-Item -Force
    }

    $projectPath = "src/Blazor.Diagrams/Blazor.Diagrams.csproj"
    [xml]$projectXml = Get-Content $projectPath -Raw
    $packageVersion = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($packageVersion)) {
        throw "Version не задан в $projectPath"
    }

    Write-Host "Building TMS.Z.Blazor.Diagrams (includes Core)..." -ForegroundColor Cyan
    dotnet pack $projectPath -c Release -o $OutputPath

    Write-Host ("`nPackage created in {0}:" -f $OutputPath) -ForegroundColor Green
    $packages = Get-ChildItem $OutputPath -Filter "TMS.Z.Blazor.Diagrams.*.nupkg" |
        Where-Object { $_.Name -notlike "*.Core.*" } |
        Sort-Object LastWriteTimeUtc -Descending
    $packages | ForEach-Object { Write-Host "  - $($_.Name)" }

    $latestPackage = $packages | Select-Object -First 1
    if (-not $latestPackage) {
        throw "Пакет не создан. Проверьте вывод dotnet pack."
    }
    if ($latestPackage.Name -notmatch [regex]::Escape($packageVersion)) {
        throw "Найденный пакет '$($latestPackage.Name)' не содержит ожидаемую версию '$packageVersion'."
    }

    if ($Push) {
        if (-not $ApiKey) { throw "ApiKey required for -Push" }
        dotnet nuget push $latestPackage.FullName -k $ApiKey -s $Source --skip-duplicate
    }
}
finally {
    Pop-Location
}
