# 📋 HANDOFF TÉCNICO COMPLETO: NetMonitor v1.0.0
**Data:** 23/10/2026  
**Repositório:** `eduardonoe/windows`  
**Pasta Local:** `D:\_Antigravity\NetMonitor`  
**Status:** Compilado e instalado em `%LOCALAPPDATA%\Programs\NetMonitor` (v1.0.0 travada).

---

## 🔍 DIAGNÓSTICO DOS 5 PROBLEMAS PONTUADOS

### 1. Pré-visualização de Cores não atualiza (Live Preview & Barra de Tarefas)
* **Causa Raiz:** No WinForms, todos os `RadioButton` adicionados diretamente a um mesmo container (`card.Controls.Add`) pertencem ao mesmo grupo de exclusão mútua.
* **O que acontecia:**
  - Seção 1 (Números): `rbTextUnified` e `rbTextIndividual`.
  - Seção 2 (Unidades): `rbUnitInherit`, `rbUnitUnified` e `rbUnitIndividual`.
  - Seção 3 (Setas): `rbArrowUnified` e `rbArrowIndividual`.
  Ao selecionar qualquer opção de Unidade (ex: `rbUnitInherit = true`), o WinForms **desmarcava os botões de números** (`rbTextIndividual` e `rbTextUnified`).
* **Consequência:**
  - `rbTextIndividual.Checked` tornava-se `false`.
  - `PnlLivePreview_Paint` caía no fallback de `btnColorTextBoth.BackColor` (branco `#FFFFFF`).
  - `TriggerLiveTuning()` enviava modo `"Unified"` para a barra de tarefas, ignorando as cores dos botões de Upload e Download.
* **Solução:** Isolar cada grupo em um mini-painel invisível (`pnlGrpText`, `pnlGrpUnit`, `pnlGrpArrow`) dentro do card.

---

### 2. Protetor de Tela: "Buga e não aparece mais depois do Preview"
* **Causa Raiz:**
  - Ao abrir as Configurações de Proteção de Tela do Windows (`control desk.cpl,,@screensaver`), o Windows roda o protetor em modo mini-preview (ex: `Ribbons.scr`).
  - Nossa rotina `IsAnyScreensaverActive()` verificava todos os processos do sistema buscando `.scr` ativo.
  - Como `Ribbons.scr` fica rodando em segundo plano continuamente enquanto a janela de proteção de tela estiver aberta na máquina, `IsAnyScreensaverActive()` retornava `true` ininterruptamente!
  - O NetMonitor assumia que o descanso de tela estava ativo de forma permanente e mantinha o widget invisível (`this.Visible = false; return;`).
* **Solução:**
  - Remover a varredura Toolhelp32 de processos `.scr` que captura previews indevidos.
  - Utilizar a API oficial do Windows: `SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, ref ss, 0)`.
  - Essa API retorna `true` **APENAS** quando o protetor de tela em tela cheia está de fato ativado e cobrindo o desktop, e volta para `false` no instante em que o usuário toca no mouse.

---

### 3. Preset "Padrão Win11"
* **Causa Raiz:** Efeito cascata da colisão de RadioButtons do item 1. Ao tentar ativar `rbTextUnified.Checked = true`, `rbUnitInherit.Checked = true` e `rbArrowUnified.Checked = true`, um desmarcava o outro sucessivamente.
* **Solução:** Resolvido com os painéis isolados de cada seção.

---

### 4. Texto Escuro na Aba "Ícones & Setas" (Tema Escuro)
* **Causa Raiz:**
  - Em `BuildPageIcones`, o código executava `lblUp.Enabled = isCustom; lblDown.Enabled = isCustom;`.
  - No WinForms, um `Label` desabilitado pinta o texto com a cor nativa `SystemColors.GrayText` (cinza escuro opaco), ficando praticamente preto e ilegível sobre o fundo escuro `#2A2A2E`.
  - O título `Modelos de Ícones Setas` perdeu o `&` porque no WinForms o caractere `&` vira tecla de atalho de mnemônico por falta de `UseMnemonic = false`.
* **Solução:**
  - Manter os labels sempre habilitados (`Enabled = true`), apenas mudando a cor para `clrTextSub` quando o campo estiver desativado.
  - Desabilitar apenas os campos de entrada `TextBox` (`txtUp.Enabled = isCustom; txtDown.Enabled = isCustom;`).
  - Ativar `UseMnemonic = false` nos labels que possuem o caractere `&`.

---

### 5. Botão "Redefinir Padrões" (Reset para Padrão de Fábrica)
* **Demanda:** O usuário pediu um botão para restaurar instantaneamente as configurações para o padrão original da instalação.
* **Onde adicionar:** No rodapé `pnlBottom` (ou na aba Geral), ao lado de Cancelar:
  - Botão `btnResetDefaults` ("Redefinir Padrões" / "Reset").
  - Restaura:
    - Idioma: `pt`
    - Formato: `Bits` (Mbps / Kbps)
    - Unidade: `Auto`
    - Posição Setas: À Direita (`true`)
    - Ícones: `VectorTriangle`
    - Tipografia: `Segoe UI Variable Display`, 9.5 pt, Bold
    - Cores de Texto: `Unified`, `#FFFFFF`
    - Unidades: `Inherit`
    - Setas: `Unified`, `#EBEBEB`
  - Atualiza todos os controles na tela, chama `pnlLivePreview.Invalidate()` e `TriggerLiveTuning()`.

---

## 🛠️ SCRIPTS DE AUTOMAÇÃO
* **Compilar:** `powershell -ExecutionPolicy Bypass -File "D:\_Antigravity\NetMonitor\build.ps1"`
* **Instalar:** `powershell -ExecutionPolicy Bypass -File "D:\_Antigravity\NetMonitor\scripts\install.ps1"`
* **Desinstalar:** `powershell -ExecutionPolicy Bypass -File "D:\_Antigravity\NetMonitor\scripts\uninstall.ps1"`
* **Sincronizar Drive:** `powershell -ExecutionPolicy Bypass -File "D:\_Antigravity\NetMonitor\scripts\upload_to_drive.ps1"`
* **Versão:** Manter rigorosamente **`1.0.0`**.
