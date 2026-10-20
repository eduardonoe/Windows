# ==============================================================================
# NetMonitor - Script de Desinstalação Oficial do Windows 11
# ==============================================================================

Write-Host "Iniciando desinstalação do NetMonitor..." -ForegroundColor Yellow

# 1. Fechar processos em execução
Get-Process NetMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

# 2. Remover Registro do Boot
$runReg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
if (Get-ItemProperty -Path $runReg -Name "NetMonitor" -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $runReg -Name "NetMonitor" -ErrorAction SilentlyContinue
    Write-Host "Removido do Boot do Windows." -ForegroundColor Green
}

# 3. Remover Registro do Adicionar/Remover Programas
$uninstallReg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NetMonitor"
if (Test-Path $uninstallReg) {
    Remove-Item -Path $uninstallReg -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Removido das Configurações do Windows." -ForegroundColor Green
}

# 4. Remover Chaves de Configuração do App
$appReg = "HKCU:\Software\NetMonitor"
if (Test-Path $appReg) {
    Remove-Item -Path $appReg -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Configurações limpas." -ForegroundColor Green
}

# 5. Remover Atalho do Menu Iniciar
$shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\NetMonitor.lnk"
if (Test-Path $shortcutPath) {
    Remove-Item -Path $shortcutPath -Force -ErrorAction SilentlyContinue
    Write-Host "Atalho do Menu Iniciar removido." -ForegroundColor Green
}

# 6. Remover Diretório de Instalação
$installDir = Join-Path $env:LOCALAPPDATA "Programs\NetMonitor"
if (Test-Path $installDir) {
    Start-Sleep -Milliseconds 500
    Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Arquivos de instalação removidos." -ForegroundColor Green
}

Write-Host "`nNetMonitor desinstalado com sucesso e 100% limpo do sistema!" -ForegroundColor Green
