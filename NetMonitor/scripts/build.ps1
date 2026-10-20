# ==============================================================================
# NetMonitor - Script de Build Automatizado (v1.0.5)
# Gera sempre os pacotes organizados na pasta release\:
#   1. NetMonitorSetup.exe (Instalador Oficial)
#   2. NetMonitor Portable\ (Pasta da versão portátil)
#   3. NetMonitor-Portable-v1.0.5.zip (Arquivo portátil avulso)
#   4. NetMonitor-v1.0.5.zip (Pacote completo com instalador + portátil)
# ==============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$srcDir = Join-Path $projectRoot "src"
$binDir = Join-Path $projectRoot "bin"
$releaseDir = Join-Path $projectRoot "release"
$iconPath = Join-Path $srcDir "app.ico"

# 1. Fechar processos em execução
Get-Process NetMonitor, NetMonitorSetup -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

# 2. Criar diretórios necessários
foreach ($dir in @($binDir, $releaseDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# 3. Compilar NetMonitor.exe com ícone
$netMonitorSrc = Join-Path $srcDir "NetMonitor.cs"
$netMonitorExe = Join-Path $binDir "NetMonitor.exe"

Write-Host "1. Compilando NetMonitor.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32icon:$iconPath `
    /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Core.dll `
    /out:$netMonitorExe $netMonitorSrc

# 4. Compilar uninstall.exe com ícone
$uninstallerSrc = Join-Path $srcDir "Uninstaller.cs"
$uninstallerExe = Join-Path $binDir "uninstall.exe"

Write-Host "2. Compilando uninstall.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32icon:$iconPath `
    /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll `
    /out:$uninstallerExe $uninstallerSrc

# 5. Compilar o Instalador (NetMonitorSetup.exe)
$installerSrc = Join-Path $srcDir "Installer.cs"
$installerExe = Join-Path $binDir "NetMonitorSetup.exe"
$releaseSetup = Join-Path $releaseDir "NetMonitorSetup.exe"

Write-Host "3. Compilando NetMonitorSetup.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32icon:$iconPath `
    /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll,Microsoft.CSharp.dll `
    /res:"$netMonitorExe,NetMonitor.exe" `
    /res:"$uninstallerExe,uninstall.exe" `
    /out:$installerExe $installerSrc

Copy-Item -Path $installerExe -Destination $releaseSetup -Force

# 6. Gerar a Versão Portátil em release\
Write-Host "4. Empacotando versão portátil em release\..." -ForegroundColor Cyan
$portableDir = Join-Path $releaseDir "NetMonitor Portable"
if (-not (Test-Path $portableDir)) { New-Item -ItemType Directory -Path $portableDir -Force | Out-Null }

$portableExe = Join-Path $portableDir "NetMonitor.exe"
Copy-Item -Path $netMonitorExe -Destination $portableExe -Force

$portableReadme = Join-Path $portableDir "LEIA-ME.txt"
$readmeContent = @"
==============================================================================
NetMonitor v1.0.5 - NetMonitor Portable
Autor: Eduardo Noé (eduardonoe)
==============================================================================

[PT-BR]
Esta é a versão portátil do NetMonitor.
- Não requer instalação.
- Pode ser executado diretamente de qualquer pasta ou pendrive.
- Para usar, basta dar um duplo clique em NetMonitor.exe.
- Para configurar idioma, placa de rede ou inicialização com o Windows, clique
  com o botão direito sobre o widget na barra de tarefas.

[EN]
This is the portable version of NetMonitor.
- No installation required.
- Can be run directly from any folder or USB drive.
- To use, simply double-click NetMonitor.exe.
- To configure language, network adapter, or auto-start, right-click the
  widget in the taskbar.
==============================================================================
"@
Set-Content -Path $portableReadme -Value $readmeContent -Encoding UTF8

# Gerar NetMonitor-Portable-v1.0.5.zip
$portableZip = Join-Path $releaseDir "NetMonitor-Portable-v1.0.5.zip"
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
Compress-Archive -Path "$portableDir\*" -DestinationPath $portableZip -Force

# Gerar NetMonitor-v1.0.5.zip (Instalador + Portátil)
$combinedZip = Join-Path $releaseDir "NetMonitor-v1.0.5.zip"
if (Test-Path $combinedZip) { Remove-Item $combinedZip -Force }

# Usando .NET ZipFile para incluir o setup e a pasta portable
[System.Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem") | Out-Null
$zip = [System.IO.Compression.ZipFile]::Open($combinedZip, [System.IO.Compression.ZipArchiveMode]::Create)
[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $releaseSetup, "NetMonitorSetup.exe") | Out-Null
Get-ChildItem -Path $portableDir -Recurse | ForEach-Object {
    if (-not $_.PSIsContainer) {
        $rel = "NetMonitor Portable\" + $_.FullName.Substring($portableDir.Length).TrimStart('\')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $rel) | Out-Null
    }
}
$zip.Dispose()

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host "Build v1.0.5 Concluído e Organizado em release\!" -ForegroundColor Green
Write-Host "  * Pacote Completo: $combinedZip" -ForegroundColor Yellow
Write-Host "  * Instalador:      $releaseSetup" -ForegroundColor Yellow
Write-Host "  * Portátil:        $portableZip" -ForegroundColor Yellow
Write-Host "========================================================`n" -ForegroundColor Green
