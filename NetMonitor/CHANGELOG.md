# Changelog — NetMonitor

Todas as alterações notáveis deste projeto seguem o padrão de **Versionamento Semântico (SemVer)**: `MAJOR.MINOR.PATCH`.

---

## [1.0.4] - 2026-10-20

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

## [1.0.3] - 2026-10-20

### Adicionado
- Ícone oficial do aplicativo (`NetMonitor-Speed-02-Blue.ico` processado em multi-resolução de 16x16 até 256x256).
- Tríade de distribuição obrigatória: Instalador, Portátil e Desinstalador.
- Criação automática da subpasta `\NetMonitor` e auto-elevação UAC ao instalar em `C:\Program Files`.
