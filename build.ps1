# Zarla Browser Build Script
# Usage: .\build.ps1 [-Version "1.0.2"] [-Clean] [-DebugOnly]
# By default, this script builds in Release mode and creates the installer.

param(
    [switch]$Clean,
    [switch]$DebugOnly,  # Use this flag to only do a debug build (no installer)
    [string]$Version,
    [string]$EncryptKey
)

# Configuration - Edit these to customize
$BrowserName = "Zarla"
$InstallerName = "ZarlaSetup"

$ErrorActionPreference = "Stop"

# Function to encrypt API key for embedding in config
function Get-EncryptedApiKey {
    param([string]$ApiKey)
    
    # Use the same encryption as SecureStorage.EncryptPortable()
    $keyString = "ZarlaBrowser-Embedded-Key-v1-Zarla.Core"
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $aesKey = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($keyString))
    
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $aesIv = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("Zarla-IV-2024"))
    
    $aes = [System.Security.Cryptography.Aes]::Create()
    $aes.Key = $aesKey
    $aes.IV = $aesIv
    $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
    
    $encryptor = $aes.CreateEncryptor()
    $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($ApiKey)
    $encryptedBytes = $encryptor.TransformFinalBlock($plainBytes, 0, $plainBytes.Length)
    
    return [Convert]::ToBase64String($encryptedBytes)
}

# If EncryptKey parameter is provided, just encrypt and exit
if ($EncryptKey) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  API Key Encryption Tool" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    $encrypted = Get-EncryptedApiKey -ApiKey $EncryptKey
    
    Write-Host "Your encrypted API key:" -ForegroundColor Green
    Write-Host ""
    Write-Host $encrypted -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Add this to zarla-config.json:" -ForegroundColor White
    Write-Host '  "encryptedAIApiKey": "' -NoNewline -ForegroundColor Gray
    Write-Host $encrypted -NoNewline -ForegroundColor Yellow
    Write-Host '"' -ForegroundColor Gray
    Write-Host ""
    Write-Host "Users will NOT need to set any environment variables!" -ForegroundColor Green
    Write-Host ""
    exit 0
}

# Function to encrypt API key in config if present
function Encrypt-ApiKeyInConfig {
    if (Test-Path $configPath) {
        $configContent = Get-Content $configPath -Raw | ConvertFrom-Json
        
        # Check if there's a plain API key that needs encrypting
        if ($configContent.aiApiKey -and $configContent.aiApiKey -ne "") {
            Write-Host "Found API key in config - encrypting..." -ForegroundColor Yellow
            
            $encrypted = Get-EncryptedApiKey -ApiKey $configContent.aiApiKey
            
            # Update the JSON - set encrypted key and clear plain key
            $jsonContent = Get-Content $configPath -Raw
            $jsonContent = $jsonContent -replace '"aiApiKey"\s*:\s*"[^"]*"', '"aiApiKey": ""'
            $jsonContent = $jsonContent -replace '"encryptedAIApiKey"\s*:\s*(?:null|"[^"]*")', "`"encryptedAIApiKey`": `"$encrypted`""
            Set-Content $configPath $jsonContent -NoNewline
            
            Write-Host "API key encrypted and embedded in config!" -ForegroundColor Green
            Write-Host "Users will NOT need to set any environment variables." -ForegroundColor Green
            Write-Host ""
        }
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Zarla Browser Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Read version from zarla-config.json if not provided
$configPath = "zarla-config.json"
if (-not $Version) {
    if (Test-Path $configPath) {
        $config = Get-Content $configPath | ConvertFrom-Json
        $Version = $config.version
        Write-Host "Using version from config: $Version" -ForegroundColor Cyan
    } else {
        $Version = "1.0.0"
        Write-Host "Using default version: $Version" -ForegroundColor Yellow
    }
}

# Update version in zarla-config.json
function Update-ConfigVersion {
    param([string]$NewVersion)
    
    if (Test-Path $configPath) {
        $jsonContent = Get-Content $configPath -Raw
        # Use regex to update just the version field while preserving formatting
        $updatedJson = $jsonContent -replace '"version"\s*:\s*"[^"]*"', "`"version`": `"$NewVersion`""
        Set-Content $configPath $updatedJson -NoNewline
        Write-Host "Updated zarla-config.json version to $NewVersion" -ForegroundColor Green
    }
}

# Update version in NSIS installer script
function Update-InstallerVersion {
    param([string]$NewVersion)
    
    $nsisScript = "installer\zarla-installer.nsi"
    if (Test-Path $nsisScript) {
        $content = Get-Content $nsisScript -Raw
        $content = $content -replace '!define PRODUCT_VERSION "[^"]*"', "!define PRODUCT_VERSION `"$NewVersion`""
        Set-Content $nsisScript $content
        Write-Host "Updated NSIS installer version to $NewVersion" -ForegroundColor Green
    }
}

# Update versions in both config and installer
Update-ConfigVersion -NewVersion $Version
Update-InstallerVersion -NewVersion $Version

# Encrypt API key if present in config
Encrypt-ApiKeyInConfig

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
$config = if ($DebugOnly) { "Debug" } else { "Release" }
Write-Host "Building Zarla ($config)..." -ForegroundColor Yellow
dotnet build -c $config
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""

# Publish for installer (always do this unless DebugOnly)
if (-not $DebugOnly) {
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

    # Build installer (always build installer in default mode)
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
            Write-Host "  Location: installer\$InstallerName-$Version.exe" -ForegroundColor White
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
if ($DebugOnly) {
    Write-Host "  dotnet run --project src\Zarla.Browser" -ForegroundColor Gray
} else {
    Write-Host "  .\publish\win-x64\Zarla.exe" -ForegroundColor Gray
}
Write-Host ""
