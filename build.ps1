# Zarla Browser Build Script
# Usage: .\build.ps1 [-Release] [-Installer]

param(
    [switch]$Release,
    [switch]$Installer,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Zarla Browser Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
    if (Test-Path "src\Zarla.Browser\bin") { Remove-Item -Recurse -Force "src\Zarla.Browser\bin" }
    if (Test-Path "src\Zarla.Browser\obj") { Remove-Item -Recurse -Force "src\Zarla.Browser\obj" }
    if (Test-Path "src\Zarla.Core\bin") { Remove-Item -Recurse -Force "src\Zarla.Core\bin" }
    if (Test-Path "src\Zarla.Core\obj") { Remove-Item -Recurse -Force "src\Zarla.Core\obj" }
    if (Test-Path "publish") { Remove-Item -Recurse -Force "publish" }
    Write-Host "Clean complete!" -ForegroundColor Green
    Write-Host ""
}

# Restore packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
Write-Host "Restore complete!" -ForegroundColor Green
Write-Host ""

# Build
$config = if ($Release) { "Release" } else { "Debug" }
Write-Host "Building Zarla ($config)..." -ForegroundColor Yellow
dotnet build -c $config
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""

# Publish for installer
if ($Release -or $Installer) {
    Write-Host "Publishing self-contained executable..." -ForegroundColor Yellow

    # Windows x64
    dotnet publish src\Zarla.Browser\Zarla.Browser.csproj `
        -c Release `
        -r win-x64 `
        -f net8.0-windows `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o publish\win-x64

    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    Write-Host "Publish complete!" -ForegroundColor Green
    Write-Host ""

    # Output location
    Write-Host "Published to: publish\win-x64\" -ForegroundColor Cyan
    Write-Host ""
}

# Build installer
if ($Installer) {
    Write-Host "Building NSIS installer..." -ForegroundColor Yellow

    # Check if NSIS is installed
    $nsisPath = "${env:ProgramFiles(x86)}\NSIS\makensis.exe"
    if (-not (Test-Path $nsisPath)) {
        $nsisPath = "${env:ProgramFiles}\NSIS\makensis.exe"
    }
    # Also check PATH
    if (-not (Test-Path $nsisPath)) {
        $nsisPath = (Get-Command makensis -ErrorAction SilentlyContinue).Source
    }

    if ($nsisPath -and (Test-Path $nsisPath)) {
        Push-Location installer
        & $nsisPath zarla-installer.nsi
        $nsisResult = $LASTEXITCODE
        Pop-Location

        if ($nsisResult -eq 0) {
            Write-Host "" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Green
            Write-Host "  Installer created successfully!" -ForegroundColor Green
            Write-Host "  Location: installer\ZarlaSetup-1.0.0.exe" -ForegroundColor White
            Write-Host "========================================" -ForegroundColor Green
        } else {
            Write-Host "Installer build failed!" -ForegroundColor Red
        }
    } else {
        Write-Host "" -ForegroundColor Yellow
        Write-Host "NSIS not found!" -ForegroundColor Red
        Write-Host "" -ForegroundColor Yellow
        Write-Host "To install NSIS:" -ForegroundColor White
        Write-Host "  1. Download from: https://nsis.sourceforge.io/Download" -ForegroundColor Gray
        Write-Host "  2. Or use winget: winget install NSIS.NSIS" -ForegroundColor Gray
        Write-Host "  3. Or use choco:  choco install nsis" -ForegroundColor Gray
        Write-Host "" -ForegroundColor Yellow
    }
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run Zarla:" -ForegroundColor White
if ($Release) {
    Write-Host "  .\publish\win-x64\Zarla.exe" -ForegroundColor Gray
} else {
    Write-Host "  dotnet run --project src\Zarla.Browser" -ForegroundColor Gray
}
Write-Host ""
