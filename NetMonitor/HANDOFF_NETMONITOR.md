# 📋 HANDOFF TÉCNICO: NetMonitor v1.0.0
**Data:** 23/10/2026  
**Repositório:** `eduardonoe/windows` (pasta `D:\_Antigravity\NetMonitor`)  
**Status Atual:** Compilado, instalado e rodando (PID ativo via `install.ps1`).

---

## 🔍 DIAGNÓSTICO DOS PROBLEMAS IDENTIFICADOS (Pontuação Cirúrgica)

### 1. Colisão de RadioButtons em `card` (Causa Raiz da Pré-visualização de Cores)
* **O que acontece:** No WinForms, todos os `RadioButton` adicionados diretamente ao mesmo container compartilham o mesmo grupo de exclusão mútua.
* **No arquivo `NetMonitor.cs` (em `BuildPageCores`):**
  - Seção 1 tem `rbTextUnified` e `rbTextIndividual`.
  - Seção 2 tem `rbUnitInherit`, `rbUnitUnified` e `rbUnitIndividual`.
  - Seção 3 tem `rbArrowUnified` e `rbArrowIndividual`.
  Como todos foram adicionados direto em `card.Controls.Add(...)`, ao marcar `rbUnitInherit = true`, o WinForms **desmarca automaticamente** `rbTextIndividual` e `rbTextUnified`!
* **Consequência visível:** No print do usuário, ambos os círculos de "1. Números da Velocidade" aparecem desmarcados (vazios).
* **Impacto na Live Preview e na Barra:**
  - `PnlLivePreview_Paint`:
    ```csharp
    Color cNumUp = rbTextIndividual.Checked ? btnColorTextUp.BackColor : btnColorTextBoth.BackColor;
    ```
    Como `rbTextIndividual.Checked` é falso, ele cai no fallback `btnColorTextBoth.BackColor` (branco `#FFFFFF`), ignorando as cores individuais do Upload (verde) e Download (vermelho).
  - `TriggerLiveTuning`: Envia `SelectedTextColorMode = "Unified"`, fazendo o widget da barra forçar cor unificada branca e não aplicar as cores dos presets.
* **Solução:** Isolar cada par/trio de RadioButtons em seu próprio mini-painel invisível ou `FlowLayoutPanel` (ex: `pnlGroupText`, `pnlGroupUnit`, `pnlGroupArrow`), garantindo independência total de grupos.

---

### 2. Texto Escuro/Ilegível na Aba "Ícones & Setas" (Tema Escuro)
* **O que acontece:**
  - Em `BuildPageIcones`:
    ```csharp
    bool isCustom = (idx == 5);
    lblUp.Enabled = isCustom;
    lblDown.Enabled = isCustom;
    ```
  - No WinForms, desabilitar um `Label` (`Enabled = false`) força a renderização nativa de texto cinza-escuro/preto do sistema (`SystemColors.GrayText`), que sobre o fundo escuro do card `#2A2A2E` fica quase invisível (como visto no segundo print).
  - Além disso, no título `"Modelos de Ícones & Setas"`, o caractere `&` age como tecla de atalho de mnemônico no WinForms, ocultando o `&` se `UseMnemonic = false` não for especificado no Label.
* **Solução:**
  - Nunca desabilitar o `Label` (`lblUp.Enabled = true; lblDown.Enabled = true;`). Em vez disso, apenas esmaecer sua cor para `clrTextSub` quando inativo.
  - Desabilitar apenas os `TextBox` (`txtUp.Enabled = isCustom; txtDown.Enabled = isCustom;`).
  - Definir `UseMnemonic = false` nos labels que contêm `&`.

---

### 3. Preset "Padrão Win11" Bugado
* **O que acontece:**
  - Ao clicar em "Padrão Win11", ele deve definir:
    - Modo de texto: Unificado (`#FFFFFF`).
    - Unidades: Herdar (`Inherit`).
    - Setas: Unificado (`#EBEBEB`).
  - Pela colisão de containers de RadioButtons citada no item 1, a seleção do modo unificado se desfaz e não atualiza corretamente as propriedades `SelectedTextColorMode` e `SelectedTextColorUnified`.
* **Solução:** Com o isolamento dos grupos de RadioButtons, o preset aciona `rbTextUnified.Checked = true`, `rbUnitInherit.Checked = true` e `rbArrowUnified.Checked = true` sem que um desmarque o outro.

---

### 4. Botão "Redefinir Padrões" (Reset de Fábrica) Solicitado
* **O que foi solicitado:** Um botão explícito para resetar para o padrão de fábrica da instalação.
* **Onde adicionar:** Na barra inferior (`pnlBottom`) do `SettingsForm`, ao lado de Cancelar/Salvar:
  - Botão: `btnResetDefaults` ("Redefinir" / "Reset").
  - Ação ao clicar: Restaura todos os valores de fábrica originais:
    - Idioma: `pt` (ou sistema)
    - Formato: `Bits` (Mbps / Kbps)
    - Unidade: `Auto`
    - Posição Setas: À Direita (`ArrowOnRight = true`)
    - Ícones: `VectorTriangle`
    - Fonte: `Segoe UI Variable Display`, 9.5 pt, Bold
    - Cores de Texto: `Unified`, `#FFFFFF`
    - Unidades: `Inherit`
    - Setas: `Unified`, `#EBEBEB`
    - Atualiza os controles na tela, chama `pnlLivePreview.Invalidate()` e `TriggerLiveTuning()`.

---

## 🛠️ ARQUIVOS E SCRIPTS RELEVANTES
* **Código Principal:** [`src/NetMonitor.cs`](file:///D:/_Antigravity/NetMonitor/src/NetMonitor.cs)
* **Compilação:** [`build.ps1`](file:///D:/_Antigravity/NetMonitor/build.ps1)
* **Instalação:** [`scripts/install.ps1`](file:///D:/_Antigravity/NetMonitor/scripts/install.ps1)
* **Desinstalação:** [`scripts/uninstall.ps1`](file:///D:/_Antigravity/NetMonitor/scripts/uninstall.ps1)
* **Upload Drive:** [`scripts/upload_to_drive.ps1`](file:///D:/_Antigravity/NetMonitor/scripts/upload_to_drive.ps1)
* **Versão Trava:** Estritamente **`1.0.0`** (SemVer Sem Alteração de Versão).
