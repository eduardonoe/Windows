# Token & Context Usage Inspector for Antigravity
param (
    [string]$ConversationId
)

$geminiPath = "$env:USERPROFILE\.gemini\antigravity\brain"

if (-not $ConversationId) {
    # Get latest active conversation that has transcript.jsonl
    $latest = Get-ChildItem -Path $geminiPath -Directory -ErrorAction SilentlyContinue | 
              Where-Object { Test-Path "$($_.FullName)\.system_generated\logs\transcript.jsonl" } |
              Sort-Object LastWriteTime -Descending | 
              Select-Object -First 1
    if ($latest) {
        $ConversationId = $latest.Name
    }
}

$logFile = "$geminiPath\$ConversationId\.system_generated\logs\transcript.jsonl"

if (-not (Test-Path $logFile)) {
    Write-Host "Arquivo de log não encontrado para a conversa: $ConversationId" -ForegroundColor Red
    exit 1
}

$lines = Get-Content $logFile
$totalInput = 0
$totalOutput = 0
$totalCache = 0
$lastContext = 0
$turns = 0

foreach ($line in $lines) {
    if (-not $line.Trim()) { continue }
    try {
        $entry = $line | ConvertFrom-Json
        if ($entry.type -eq "PLANNER_RESPONSE" -and $entry.input_tokens -ne $null) {
            $totalInput += [int64]$entry.input_tokens
            $totalOutput += [int64]$entry.output_tokens
            $totalCache += [int64]$entry.cache_read_tokens
            $lastContext = [int64]$entry.input_tokens + [int64]$entry.cache_read_tokens
            $turns++
        }
    } catch {}
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "        PAINEL DE USO DE TOKENS (ANTIGRAVITY)            " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ID da Sessão:      $ConversationId" -ForegroundColor Gray
Write-Host " Total de Turnos:   $turns interações"
Write-Host " Janela Atual:      $([math]::Round($lastContext / 1000, 1))k tokens (Capacidade: 1.000k - 2.000k)" -ForegroundColor Green
Write-Host " Tokens de Saída:   $([math]::Round($totalOutput / 1000, 1))k tokens gerados"
Write-Host " Prompt Cache:      $([math]::Round($totalCache / 1000000, 2))M tokens economizados via Cache" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""
