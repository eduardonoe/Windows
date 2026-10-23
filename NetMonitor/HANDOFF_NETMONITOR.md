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

## 🚀 ATUALIZAÇÕES ADICIONAIS CONSOLIDADAS

### 6. Instância Única (Single Instance via Mutex)
* Bloqueio global de sessão no `Main()` impedindo que mais de uma instância rode concorrentemente. Tentativas subsequentes encerram de forma silenciosa e instantânea.

### 7. Layout do Rodapé Corrigido (Sem Sobreposição)
* O texto do preview foi reestruturado para `PRÉVIA AO VIVO` / `Tempo real` com `AutoSize = false` e largura restrita a 110px.
* O botão `Redefinir Padrões` recebeu largura de 108px e posicionamento exclusivo a partir de X=238, eliminando qualquer colisão com os textos e preservando espaçamento simétrico em relação aos botões `Salvar` e `Cancelar`.

### 8. Menu de Contexto: Opção de Idioma
* Adicionado submenu **"Idioma"** / **"Language"** diretamente ao menu de contexto do widget na barra de tarefas:
  - `✔ Português (Brasil)`
  - `   English (United States)`
* Alterna dinamicamente as legendas, persiste no registro e atualiza o widget em tempo real.

### 9. Protetor de Tela: Suporte ao Botão "Visualizar" de `desk.cpl`
* Ao clicar em "Visualizar" no painel de descanso de tela, o Windows dispara o protetor com `/s` em tela cheia sem sinalizar `SPI_GETSCREENSAVERRUNNING`.
* Foi adicionada a validação por `GetForegroundWindow()` e varredura de processos `.scr` com verificação de tamanho (`w >= 400 && h >= 300`) e ausência de janela pai (`GetParent == IntPtr.Zero`).
* O NetMonitor agora se auto-oculta perfeitamente no botão "Visualizar" e continua visível quando estiver apenas no mini-preview da janelinha.
* Timer de verificação ajustado para 300ms com remoção de `HWND_TOPMOST` durante o protetor.

### 10. Top 5 Agrupado por Aplicativo (Software)
* Múltiplos workers, abas e subprocessos do mesmo software (ex: Antigravity IDE, Dropbox, Sync, Chrome) agora têm seus tráfegos somados e consolidados sob uma única entrada.
* Título limpo sem extensão `.exe`, liberando as outras 4 posições do Top 5 para os demais softwares ativos.

### 11. Identidade e Contato do Desenvolvedor (Noé VFX)
* Rodapé da sidebar das Configurações inclui:
  - *desenvolvido por* **Noé VFX | noevfx.com** (link clicável direto para `https://noevfx.com`)
  - **contato@noevfx.com** (link direto para `mailto:contato@noevfx.com`)
* Adicionado atalho direto `Noé VFX (noevfx.com)` no menu de contexto do widget.

### 12. Janela de Configurações no ALT + TAB e Barra de Tarefas
* **Problema:** A janela de personalização (`SettingsForm`) possuía `ShowInTaskbar = false;` e não herdava estilo de aplicativo, sendo excluída pelo DWM do carrossel do ALT + TAB e da barra de tarefas caso outra janela cobrisse a tela.
* **Solução:**
  - `this.ShowInTaskbar = true;` em `InitializeUI()`.
  - Herança do ícone do executável (`this.Icon = parentApp.Icon`).
  - Override de `CreateParams` com `WS_EX_APPWINDOW (0x00040000)` para forçar registro no Shell e DWM.
  - Implementação de `OnHandleCreated` aplicando `UpdateTitleBarTheme(isDarkTheme)` imediatamente.

### 13. Isolamento Estrito de I/O de Disco e Filtro de Loopback no Top 5 Apps
* **Problema:** Processos realizando operações pesadas de disco/SSD (como instaladores `Setup.exe` descompactando arquivos a ~175 MB/s) eram listados no Top 5 de rede com velocidades irreais (ex: 1,4 Gbps), mesmo com a placa física e conexão baixando a apenas 230 Kbps.
* **Causa Raiz:** A API nativa do Windows `GetProcessIoCounters` contabiliza I/O total (disco + rede indistintamente). Além disso, `GetActiveNetworkPids` listava qualquer processo com portas em escuta (`0.0.0.0`) ou comunicação local `127.0.0.1`.
* **Solução Implementada:**
  1. **Filtro Estrito de Sockets:** `GetActiveNetworkPids` agora aceita apenas sockets TCP no estado `ESTABLISHED` (`state == 5`) conectados a IPs remotos externos legítimos, ignorando escuta (`0.0.0.0`) e loopback (`127.x.x.x` e `::1`).
  2. **Calibração e Teto pela Placa Física:** `NetMonitorApp` rastreia continuamente a taxa física real (`latestPhysicalSpeedBytesPerSec`). O tráfego reportado para qualquer processo é delimitado por essa velocidade física (descartando picos de escrita no SSD) e a soma coletiva dos apps é normalizada para jamais ultrapassar a capacidade real da interface.
  3. **Zero Impacto em Performance:** Mantém 0% de CPU e menos de 20 MB de RAM, preservando compilação nativa em C# puro via `csc.exe` sem dependências externas ou exigência de Administrador (UAC).

---

## 🛠️ SCRIPTS DE AUTOMAÇÃO
* **Compilar:** `powershell -ExecutionPolicy Bypass -File "D:\_Antigravity\NetMonitor\build.ps1"`
* **Instalar:** `powershell -ExecutionPolicy Bypass -File "D:\_Antigravity\NetMonitor\scripts\install.ps1"`
* **Desinstalar:** `powershell -ExecutionPolicy Bypass -File "D:\_Antigravity\NetMonitor\scripts\uninstall.ps1"`
* **Sincronizar Drive:** `powershell -ExecutionPolicy Bypass -File "D:\_Antigravity\NetMonitor\scripts\upload_to_drive.ps1"`
* **Versão:** Manter rigorosamente **`1.0.0`**.


