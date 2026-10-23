# NetMonitor v1.0.0

Monitor de tráfego de rede ultraleve para a barra de tarefas do **Windows 11**, desenvolvido por **Noé VFX ([noevfx.com](https://noevfx.com))** para o ecossistema de **Eduardo Noé (`eduardonoe`)**.

---

## ✨ Recursos Principais (v1.0.0)

- **Central de Personalização Moderna (Windows 11 Settings):**
  - Interface fluente com sidebar lateral e navegação rápida entre 4 páginas: **Geral**, **Tipografia**, **Cores** e **Ícones & Setas**.
  - **Pré-visualização Ao Vivo Persistente:** Painel no rodapé que reflete imediatamente qualquer alteração em tempo real.
  - **Suporte Completo ao ALT + TAB e Barra de Tarefas:** Integração total ao carrossel de janelas e barra de tarefas do Windows para alternância instantânea.
- **Card Flutuante (Flyout) do Top 5 Programas ao Passar o Mouse (Hover):**
  - Design moderno com cantos arredondados DWM e sombra suave.
  - Tráfego de rede agrupado de forma inteligente por **aplicativo/software** (soma dados de múltiplos subprocessos e workers, ex: Antigravity IDE, Dropbox, Chrome, Sync).
- **Tema Nativo Fluente (Dark / Light):**
  - Menus e janelas que respeitam o tema do sistema operacional, com renderização escura elegante (`#202020`), hover suave e checkmarks em azul fluente.
- **Suporte Bilingue Nativo (Português / Inglês):**
  - Alternância instantânea de idioma com um clique nas configurações ou diretamente pelo menu de contexto na barra de tarefas.
- **Controle de Tipografia Completo:**
  - Escolha de família da fonte (`Segoe UI Variable Display`, `Segoe UI`, `Cascadia Code`, `Consolas`, `Arial`, etc.).
  - Ajuste de tamanho de fonte (8.0 pt a 12.0 pt) com escala proporcional de unidades.
  - Estilos: Regular, Negrito (Bold) e Itálico.
- **Sistema Avançado de Cores:**
  - Cores unificadas ou individuais para setas (Upload / Download).
  - Cores unificadas ou individuais para textos de velocidade e unidade.
  - Seletor nativo `ColorDialog` e paletas rápidas de cores (*Branco Clássico*, *Verde/Vermelho*, *Ciano/Laranja*, *Azul Fluent*).
- **Layout Moderno e Ícones Customizados:**
  - Setas à direita por padrão: leitura clara `[ 823.6 Mbps ▲ ]` perfeitamente alinhada com a bandeja do Windows 11.
  - Templates de ícones prontos (Triângulos Sutis, Chevrons, Clássico, Sólidos, Minimalista) ou caracteres Unicode customizados.
- **Privacidade e Ocultação Inteligente:**
  - Oculta automaticamente durante o descanso de tela (incluindo tela cheia e modo "Visualizar" de `desk.cpl`) e tela de bloqueio do Windows.
- **Instância Única Garantida (Mutex Global):**
  - Previne múltiplas instâncias concorrentes em execução.
- **Sem Dependências Externas (Ultraleve):**
  - Escrito em C# Win32 puro (.NET Framework nativo do Windows), consumindo ~10 MB de RAM e 0% de CPU.

---

## 📁 Estrutura de Diretórios

```text
NetMonitor/
├── release/                           # 🌟 Pacotes finais para download e distribuição
│   ├── NetMonitor-v1.0.0.zip          # Pacote completo (Instalador Oficial + Versão Portátil)
│   ├── NetMonitor-Portable-v1.0.0.zip # Arquivo .zip avulso da versão portátil
│   ├── NetMonitorSetup.exe            # Instalador oficial autocontido (com UAC e ícone)
│   └── NetMonitor Portable/           # Pasta da versão portátil descompactada
├── bin/                               # Binários compilados da compilação interna
│   ├── NetMonitor.exe                 # Executável principal (com ícone PE)
│   ├── NetMonitorSetup.exe            # Executável do instalador
│   └── uninstall.exe                  # Executável do desinstalador
├── src/                               # Código-fonte em C# Win32 puro
│   ├── app.ico                        # Ícone oficial multi-resolução (16x16 até 256x256)
│   ├── NetMonitor.cs                  # Monitor de rede com personalização e UI
│   ├── Installer.cs                   # Assistente de instalação visual com auto-elevação UAC
│   └── Uninstaller.cs                 # Assistente de desinstalação
├── docs/                              # Documentação técnica e design
│   ├── comparativo_design.md          # Relatório técnico das opções testadas
│   └── assets/                        # Ícone original e captura de tela
├── scripts/                           # Scripts de automação de build e deploy
│   ├── build.ps1                      # Pipeline de compilação oficial
│   ├── install.ps1                    # Script de instalação local
│   ├── uninstall.ps1                  # Script de desinstalação
│   └── upload_to_drive.ps1            # Sincronização automática com o Google Drive
├── build.ps1                          # Atalho de build na raiz do projeto
├── CHANGELOG.md                       # Histórico de versões e revisões (SemVer)
├── HANDOFF_NETMONITOR.md              # Documento oficial de passagem de bastão técnica
└── README.md                          # Este arquivo
```

---

## 🚀 Como Usar

### 1. Pacote de Lançamento (Release):
Na pasta [`release/`](file:///D:/_Antigravity/NetMonitor/release), você encontra:
* **`NetMonitorSetup.exe`**: Instalador oficial para o Windows 11.
* **`NetMonitor-Portable-v1.0.0.zip`**: Versão portátil avulsa (roda sem instalação direto de qualquer pasta).
* **`NetMonitor-v1.0.0.zip`**: Pacote completo contendo o instalador e a versão portátil.

### 2. Para Recompilar o Projeto:
Execute no PowerShell:
```powershell
.\build.ps1
```

---

## 👨‍💻 Desenvolvedor & Suporte

* **Desenvolvido por:** Noé VFX ([noevfx.com](https://noevfx.com))
* **Contato:** [contato@noevfx.com](mailto:contato@noevfx.com)
* **Repositório:** [eduardonoe/windows](https://github.com/eduardonoe/windows)

