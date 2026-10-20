# NetMonitor v1.0.5

Monitor de tráfego de rede ultraleve para a barra de tarefas do **Windows 11**, desenvolvido para o ecossistema de **Eduardo Noé (`eduardonoe`)**.

---

## ✨ Recursos Principais (v1.0.5)

- **Layout Moderno com Setas à Direita:** Leitura clara e natural `[ 823.6 Mbps ▲ ]` perfeitamente alinhada com a barra de tarefas do Windows 11.
- **Personalização Visual Completa (Settings):**
  - Alternância entre setas à direita ou à esquerda.
  - Templates visuais: Triângulos Sutis, Chevrons, Clássico e Sólidos.
  - **Suporte a Códigos Unicode:** Campo customizado para upload e download aceitando caracteres diretos ou códigos hexadecimais (ex: `U+25B2`, `0x25B2`, `25B2`).
  - Pré-visualização ao vivo das mudanças.
- **Seleção de Unidades:** Escolha entre **Automático**, **Kbps Fixo**, **Mbps Fixo** e **Gbps Fixo**.
- **Trava de Posição:** Habilite ou desabilite o arraste com o mouse para evitar deslocamentos indesejados.
- **Checkmarks Nítidos no Menu (✓):** Identificação visual imediata de itens ativos (adaptador, idioma, boot, unidades e trava).
- **Sem Dependências Externas:** Escrito em C# Win32 puro, consumindo ~10 MB de RAM e 0% de CPU.

---

## 📁 Estrutura de Diretórios

```text
NetMonitor/
├── release/                           # 🌟 Pacotes finais para download e distribuição
│   ├── NetMonitor-v1.0.5.zip          # Pacote completo (Instalador Oficial + Versão Portátil)
│   ├── NetMonitor-Portable-v1.0.5.zip # Arquivo .zip avulso da versão portátil
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
* **`NetMonitor-v1.0.5.zip`**: Pacote completo contendo tanto o Instalador quanto a Versão Portátil.
* **`NetMonitorSetup.exe`**: Instalador oficial para o Windows 11.
* **`NetMonitor Portable`**: Versão que roda sem instalação direto de qualquer pasta ou pendrive.

### 2. Para Recompilar o Projeto:
Execute no PowerShell:
```powershell
.\build.ps1
```
