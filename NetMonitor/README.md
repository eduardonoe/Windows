# NetMonitor v1.0.0

Monitor de tráfego de rede ultraleve para a barra de tarefas do **Windows 11**, desenvolvido para o ecossistema de **Eduardo Noé (`eduardonoe`)**.

---

## ✨ Recursos Principais (v1.0.0)

- **Tema Nativo do Windows 11 (Dark / Light):** Menus de contexto que respeitam o tema do sistema operacional, com renderização escura elegante (`#202020`), hover suave e checkmarks em azul fluente.
- **Isolamento de Monitores (Multi-display Fix):** Submenus que nunca invadem a segunda tela em setups multi-monitor, expandindo sempre para a esquerda.
- **Controle de Tipografia Completo:**
  - Escolha de família da fonte (`Segoe UI Variable`, `Segoe UI`, `Cascadia Code`, `Consolas`, `Arial`, etc.).
  - Ajuste de tamanho de fonte (8.5 pt a 12.0 pt) com escala proporcional de unidades.
  - Estilos: Regular, Negrito (Bold) e Itálico.
- **Sistema Avançado de Cores:**
  - Cores unificadas ou individuais para setas (Upload / Download).
  - Cores unificadas ou individuais para textos de velocidade e unidade.
  - Seletor nativo `ColorDialog` e paletas rápidas de cores.
- **Janela de Configurações Moderna com Abas:** Abas para *Geral*, *Tipografia*, *Cores* e *Ícones & Unicode*, com pré-visualização ao vivo persistente na parte inferior.
- **Estabilidade de Interface:** Correção definitiva de fechamento precoce de menus suspensos e comboboxes.
- **Layout Moderno com Setas à Direita:** Leitura clara e natural `[ 823.6 Mbps ▲ ]` perfeitamente alinhada com a barra de tarefas do Windows 11.
- **Sem Dependências Externas:** Escrito em C# Win32 puro, consumindo ~10 MB de RAM e 0% de CPU.

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
├── scripts/                           # Scripts de automação de build
│   ├── build.ps1                      # Pipeline de compilação oficial
│   ├── install.ps1                    # Script de suporte
│   └── uninstall.ps1                  # Script de suporte
├── build.ps1                          # Atalho de build na raiz do projeto
├── CHANGELOG.md                       # Histórico de versões e revisões (SemVer)
└── README.md                          # Este arquivo
```

---

## 🚀 Como Usar

### 1. Pacote de Lançamento (Release):
Na pasta [`release/`](file:///D:/_Antigravity/NetMonitor/release), você encontra:
* **`NetMonitor-v1.0.0.zip`**: Pacote completo contendo tanto o Instalador quanto a Versão Portátil.
* **`NetMonitorSetup.exe`**: Instalador oficial para o Windows 11.
* **`NetMonitor Portable`**: Versão que roda sem instalação direto de qualquer pasta ou pendrive.

### 2. Para Recompilar o Projeto:
Execute no PowerShell:
```powershell
.\build.ps1
```
