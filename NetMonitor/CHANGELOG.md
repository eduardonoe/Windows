# Changelog — NetMonitor

Todas as alterações notáveis deste projeto seguem o padrão de **Versionamento Semântico (SemVer)**: `MAJOR.MINOR.PATCH`.

---

## [1.0.0] - 2026-10-23

### Adicionado
- **Central de Configurações Moderna com Sidebar Lateral (Windows 11):**
  - Nova interface gráfica estruturada em 4 páginas: **Geral**, **Tipografia**, **Cores** e **Ícones & Setas**.
  - Painel de pré-visualização ao vivo (*live preview*) no rodapé, atualizando tipografia, cores, layout e setas em tempo real.
  - Botões para salvar configurações, restaurar padrões de fábrica ou cancelar (com rollback instantâneo do estado prévio).
- **Integração com ALT + TAB e Barra de Tarefas:**
  - Janela de configurações com `WS_EX_APPWINDOW`, `ShowInTaskbar = true` e ícone oficial multi-resolução, permitindo alternar rapidamente via carrossel do Windows mesmo se encoberta por outras janelas.
- **Card Flutuante (Flyout) do Top 5 Programas por Tráfego de Rede (Hover):**
  - Acionado ao passar o mouse sobre o widget da barra de tarefas.
  - Agrupamento inteligente por aplicativo/software (soma de tráfego de múltiplos subprocessos, abas e workers).
  - Design moderno Windows 11 com cantos arredondados DWM (`DWMWCP_ROUND`), fundo fluente translúcido e sombra sutil.
- **Tema Nativo do Windows 11 (Escuro / Claro Automático):**
  - Menus e janelas sincronizados dinamicamente via `WM_SETTINGCHANGE` e `DwmSetWindowAttribute`.
  - Renderização escura fluente (`#202020`), hover suave (`#323232`), checkmarks azuis (`#0078D4`) e bordas suaves.
- **Suporte Bilingue Nativo (Português do Brasil / Inglês):**
  - Submenu "Idioma" no menu de contexto do widget e seletor na página Geral das configurações.
  - Persistência imediata no Registro do Windows (`HKCU\Software\NetMonitor`).
- **Sistema Avançado de Cores e Tipografia:**
  - Cores unificadas ou individuais para textos de velocidade (Upload / Download), unidades e setas.
  - Paletas rápidas (*Branco Clássico*, *Verde/Vermelho*, *Ciano/Laranja*, *Azul Fluent*) e seletor `ColorDialog` nativo.
  - Seleção de família da fonte (`Segoe UI Variable Display`, `Segoe UI`, `Cascadia Code`, `Consolas`, `Arial`), estilos e tamanhos proporcionais.
- **Proteção e Privacidade:**
  - Ocultação automática durante protetor de tela (incluindo tela cheia / botão "Visualizar" de `desk.cpl`) e tela de bloqueio.
  - Instância única de processo garantida via `Mutex` nomeado global.
- **Identidade Visual e Informações do Desenvolvedor:**
  - Rodapé das configurações e menu de contexto com links clicáveis para **Noé VFX ([noevfx.com](https://noevfx.com))** e `contato@noevfx.com`.
  - Ícone PE multi-resolução oficial embutido em todos os executáveis.

### Corrigido
- **Isolamento de Grupos de RadioButtons na UI:**
  - Mini-painéis para isolar `pnlGrpText`, `pnlGrpUnit` e `pnlGrpArrow`, garantindo persistência da seleção de cores.
- **Layout do Rodapé das Configurações:**
  - Eliminação de qualquer sobreposição de texto sobre o botão "Redefinir Padrões" através de dimensões e bounds fixos.
- **Protetor de Tela em Modo Janela / Preview:**
  - Validação estrita de janela em tela cheia para evitar bloqueio falso no mini-preview de configuração do Windows.
- **Invasão de Monitor Secundário:**
  - Abertura de submenus sempre orientada à esquerda (`ToolStripDropDownDirection.Left`) e para cima da barra de tarefas.
- **Corte de Caracteres na Unidade:**
  - Ajuste de retângulo GDI+ com largura expandida e suavização ClearType.

