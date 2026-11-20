# Script de compilacion y empaquetado para AddOnAutoLotes (SAP Business One 10.0 x64)
# Uso: .\build.ps1 -Version 1.0.3
param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "1. Compilando AddOnAutoLotes.exe en x64 Release..." -ForegroundColor Cyan

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$outDir = Join-Path $scriptDir "bin\x64\Release"
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$exePath = Join-Path $outDir "AddOnAutoLotes.exe"
$sdkDll = "C:\Program Files (x86)\SAP\SAP Business One SDK\Lib\SAPBusinessOneSDK.dll"

& $csc /target:winexe /platform:x64 /optimize+ `
    /r:$sdkDll /r:System.dll /r:System.Configuration.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /out:$exePath `
    (Join-Path $scriptDir "Program.cs") `
    (Join-Path $scriptDir "BatchAutoAssigner.cs") `
    (Join-Path $scriptDir "Properties\AssemblyInfo.cs")

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la compilacion de AddOnAutoLotes.exe"
    exit $LASTEXITCODE
}

Write-Host "2. Preparando archivos de ejecucion en X64Client..." -ForegroundColor Cyan
Copy-Item (Join-Path $scriptDir "App.config") (Join-Path $outDir "AddOnAutoLotes.exe.config") -Force

$x64Dir = Join-Path $scriptDir "X64Client"
if (!(Test-Path $x64Dir)) { New-Item -ItemType Directory -Path $x64Dir -Force | Out-Null }
Copy-Item $exePath (Join-Path $x64Dir "AddOnAutoLotes.exe") -Force
Copy-Item (Join-Path $outDir "AddOnAutoLotes.exe.config") (Join-Path $x64Dir "AddOnAutoLotes.exe.config") -Force

Write-Host "3. Calculando hash MD5 del ejecutable..." -ForegroundColor Cyan
$hash = (Get-FileHash -Path $exePath -Algorithm MD5).Hash
Write-Host "   Hash MD5: $hash" -ForegroundColor Green

Write-Host "4. Actualizando AddOnAutoLotes.ard con el hash..." -ForegroundColor Cyan
$ardPath = Join-Path $scriptDir "AddOnAutoLotes.ard"
$ardContent = Get-Content $ardPath -Raw -Encoding utf8
$ardContent = [regex]::Replace($ardContent, 'AddonSig="[A-Fa-f0-9]{32}"', "AddonSig=""$hash""")
$ardContent = [regex]::Replace($ardContent, 'ExtVersion="[^"]*"', "ExtVersion=""$Version""")
[System.IO.File]::WriteAllText($ardPath, $ardContent, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "5. Compilando instalador ejecutable (AddOnAutoLotesInstaller.exe)..." -ForegroundColor Cyan
$installerPath = Join-Path $scriptDir "AddOnAutoLotesInstaller.exe"
$installerCs = Join-Path $scriptDir "Installer.cs"
$cfgPath = Join-Path $x64Dir "AddOnAutoLotes.exe.config"

& $csc /target:winexe /platform:x64 /optimize+ `
    /r:System.dll /r:System.Windows.Forms.dll `
    /resource:"$exePath,AddOnAutoLotes.exe" `
    /resource:"$cfgPath,AddOnAutoLotes.exe.config" `
    /out:$installerPath `
    $installerCs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la compilacion del instalador."
    exit $LASTEXITCODE
}

Write-Host "6. Empaquetando Extension ZIP $Version para Extension Manager..." -ForegroundColor Cyan
# Misma estructura que genera el asistente oficial: .ard + X64Client/
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zipPath = Join-Path $scriptDir "AddOnAutoLotes_$Version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
try {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $ardPath, "AddOnAutoLotes.ard") | Out-Null
    $zip.CreateEntry("X64Client/") | Out-Null
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $x64Dir "AddOnAutoLotes.exe"), "X64Client/AddOnAutoLotes.exe") | Out-Null
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $x64Dir "AddOnAutoLotes.exe.config"), "X64Client/AddOnAutoLotes.exe.config") | Out-Null
} finally {
    $zip.Dispose()
}

Write-Host "`nListo! Archivos generados exitosamente:" -ForegroundColor Green
Write-Host "   Instalador EXE: $installerPath" -ForegroundColor Yellow
Write-Host "   Paquete ZIP:    $zipPath" -ForegroundColor Yellow


