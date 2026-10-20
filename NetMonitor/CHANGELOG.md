# Changelog — NetMonitor

Todas as alterações notáveis deste projeto seguem o padrão de **Versionamento Semântico (SemVer)**: `MAJOR.MINOR.PATCH`.

---

## [1.0.0] - 2026-10-20

### Adicionado
- **Tema Nativo do Windows 11 no Menu de Contexto (Modo Escuro / Claro Automático):**
  - Implementado `Windows11MenuRenderer` e `Windows11ColorTable` que detectam o tema do sistema em tempo real, aplicando fundo escuro fluente (`#202020`), hover suave (`#323232`), textos brancos e caixas de seleção azuis (`#0078D4`). Em temas claros, adapta-se automaticamente à paleta clara do Windows.
- **Seletor Completo de Tipografia na Interface:**
  - Adicionado seletor de Família da Fonte (`Segoe UI Variable Display`, `Segoe UI`, `Cascadia Code`, `Consolas`, `Arial`, etc.).
  - Seletor de Tamanho da Fonte (de 8.5 pt a 12.0 pt).
  - Seletor de Estilo da Fonte (Regular, Negrito / Bold, Itálico / Italic).
- **Sistema Avançado de Cores (Conjunto e Individual):**
  - Controle de cor das **Setas**: modo unificado (ambas) ou cores individuais para Upload e Download.
  - Controle de cor dos **Textos** (Velocidade e Unidade): modo unificado ou cores individuais para Upload e Download.
  - Paletas rápidas com um clique (*Branco Clássico*, *Verde/Vermelho*, *Ciano/Laranja*, *Azul Fluent*).
  - Suporte a seletor nativo `ColorDialog` com prévia em botões de amostra (*swatches*).
- **Nova Arquitetura de Configurações com Abas e Live Preview Fixo:**
  - Interface organizada em 4 abas limpas e modernas: **Geral**, **Tipografia**, **Cores** e **Ícones & Unicode**.
  - Painel de pré-visualização ao vivo persistente na parte inferior, refletindo em tempo real qualquer mudança de fonte, cor, layout ou caractere.

### Corrigido
- **Invasão do Segundo Monitor em Setups Multi-tela:**
  - Submenus configurados com `DropDownDirection = ToolStripDropDownDirection.Left`, forçando a expansão para a esquerda e mantendo todos os menus rigorosamente contidos nos limites do monitor atual.
- **Fechamento Prematuro de Menus Suspensos / Comboboxes:**
  - O timer Win32 e chamadas de reposicionamento são pausados durante a exibição do diálogo de configurações, eliminando mensagens `WM_CANCELMODE` que fechavam os seletores ao passar o mouse.

---

## [1.0.0] - 2026-10-20

### Adicionado
- **Checkmarks Nítidos nos Menus (✓):**
  - Habilitado `ShowCheckMargin = true` no menu de contexto e em todos os submenus suspensos, garantindo que o usuário visualize com precisão quais opções estão ativas (adaptador de rede, idioma, inicialização, modo de unidade e trava de posição).
- **Setas Posicionadas à Direita por Padrão:**
  - Layout modernizado no formato `[ 823.6 Mbps ▲ ]`, trazendo maior legibilidade e alinhamento visual com a barra de tarefas do Windows 11.
- **Seleção Fixa de Unidades de Medida:**
  - Adicionado seletor de unidade com opções: **Automático**, **Kbps Fixo**, **Mbps Fixo** e **Gbps Fixo**, acessível pelo menu de contexto e pelo modal de configurações.
- **Trava de Posição (Lock Position):**
  - Opção no menu e configurações para travar o widget na barra de tarefas, prevenindo deslocamentos acidentais ao clicar ou arrastar com o mouse.
- **Janela de Personalização e Configurações da UI:**
  - Modal completo com:
    - Alternância de layout (Setas à Direita vs Setas à Esquerda).
    - Templates de ícones (Triângulos Sutis, Chevrons, Clássico, Sólidos).
    - Campo de código Unicode / Mapa de Caracteres para Upload e Download (aceita tanto o caractere direto como códigos hexadecimais `U+25B2`, `0x25B2`, `25B2` ou `\u25b2`).
    - Painel de pré-visualização ao vivo (*live preview*) das configurações antes de aplicar.

### Corrigido
- **Corte da letra 's' em "Mbps":**
  - Ajustado o retângulo de renderização GDI+ da unidade (largura expandida de 35px para 38px) e alinhamento de texto com ClearType, eliminando o corte da última letra em conexões rápidas.

---

## [1.0.0] - 2026-10-20

### Alterado
- **Tipografia Mais Fina:**
  - A fonte dos números de velocidade agora utiliza `FontStyle.Regular` (10.5pt), eliminando o aspecto excessivamente grosso e pesado do negrito anterior.
- **Triângulos Ampliados em 15%:**
  - As setas vetoriais foram ampliadas exatamente em 15% (de 7×4.5px para 8×5.2px), mantendo a suavidade e o acabamento anti-aliased.
- **Aproximação e Redução do Espaço Vazio à Direita (~30%):**
  - Largura do widget enxugada de 120px para **98px**, eliminando o espaço morto à direita da unidade.
  - O widget foi posicionado mais próximo do chevron `^` da bandeja, reduzindo o espaçamento em ~30% exatamente conforme indicado no retângulo azul.
- **Nomenclatura Oficial Portátil:**
  - A pasta e o arquivo compactado agora seguem rigorosamente o nome: **`NetMonitor Portable`** e **`NetMonitor Portable.zip`**.

### Corrigido
- **Abertura do Menu em Múltiplos Monitores Verticais:**
  - O menu de contexto agora calcula a sua altura e é forçado a abrir **rigorosamente para CIMA** da barra de tarefas (`Point(e.X, -menuHeight)`), impedindo que em setups com monitores posicionados abaixo o menu caia na tela secundária.

---

## [1.0.0] - 2026-10-20

### Adicionado
- Ícone oficial do aplicativo (`NetMonitor-Speed-02-Blue.ico` processado em multi-resolução de 16x16 até 256x256).
- Tríade de distribuição obrigatória: Instalador, Portátil e Desinstalador.
- Criação automática da subpasta `\NetMonitor` e auto-elevação UAC ao instalar em `C:\Program Files`.
