# ==============================================================================
# NetMonitor - Cópia e Sincronização para o Google Drive
# ==============================================================================

$target = "G:\Meu Drive\NetMonitor"
if (-not (Test-Path $target)) {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}

Write-Host "Copiando arquivos da release para $target..." -ForegroundColor Cyan
Copy-Item -Path "D:\_Antigravity\NetMonitor\release\*" -Destination $target -Recurse -Force
Copy-Item -Path "D:\_Antigravity\NetMonitor\README.md" -Destination $target -Force
Copy-Item -Path "D:\_Antigravity\NetMonitor\CHANGELOG.md" -Destination $target -Force

Write-Host "`nArquivos copiados com sucesso para o Google Drive:" -ForegroundColor Green
Get-ChildItem -Path $target | Select-Object Name, Length, LastWriteTime
