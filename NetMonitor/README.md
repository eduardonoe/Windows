# NetMonitor v1.0.4

Monitor de tráfego de rede ultraleve para a barra de tarefas do **Windows 11**, desenvolvido para o ecossistema de **Eduardo Noé (`eduardonoe`)**.

---

## 📁 Estrutura de Diretórios

```text
NetMonitor/
├── release/                        # 🌟 Pacotes finais para download e distribuição
│   ├── NetMonitor-v1.0.4.zip       # Pacote completo (Instalador Oficial + Versão Portátil)
│   ├── NetMonitor-Portable-v1.0.4.zip # Arquivo .zip avulso da versão portátil
│   ├── NetMonitorSetup.exe         # Instalador oficial autocontido (com UAC e ícone)
│   └── NetMonitor Portable/        # Pasta da versão portátil descompactada
├── bin/                            # Binários compilados da compilação interna
│   ├── NetMonitor.exe              # Executável principal (46 KB, com ícone PE)
│   ├── NetMonitorSetup.exe         # Executável do instalador
│   └── uninstall.exe               # Executável do desinstalador
├── src/                            # Código-fonte em C# Win32 puro
│   ├── app.ico                     # Ícone oficial multi-resolução (16x16 até 256x256)
│   ├── NetMonitor.cs               # Monitor de rede com setas vetoriais sutis
│   ├── Installer.cs                # Assistente de instalação visual com auto-elevação UAC
│   └── Uninstaller.cs              # Assistente de desinstalação
├── docs/                           # Documentação técnica e design
│   ├── comparativo_design.md       # Relatório técnico das opções testadas
│   └── assets/                     # Ícone original e captura de tela
├── scripts/                        # Scripts de automação de build
│   ├── build.ps1                   # Pipeline de compilação oficial
│   ├── install.ps1                 # Script de suporte
│   └── uninstall.ps1               # Script de suporte
├── build.ps1                       # Atalho de build na raiz do projeto
├── CHANGELOG.md                    # Histórico de versões e revisões (SemVer)
└── README.md                       # Este arquivo
```

---

## 🚀 Como Usar

### 1. Pacote de Lançamento (Release):
Na pasta [`release/`](file:///D:/_Antigravity/NetMonitor/release), você encontra:
* **`NetMonitor-v1.0.4.zip`**: Pacote completo contendo tanto o Instalador quanto a Versão Portátil.
* **`NetMonitorSetup.exe`**: Instalador oficial para o Windows 11.
* **`NetMonitor Portable`**: Versão que roda sem instalação direto de qualquer pasta ou pendrive.

### 2. Para Recompilar o Projeto:
Execute no PowerShell:
```powershell
.\build.ps1
```
