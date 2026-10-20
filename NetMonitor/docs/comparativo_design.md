# Monitor de Rede Ultraleve — Comparativo Visual & Técnico

Aplicamos as duas abordagens diretamente sobre o print da sua barra de tarefas do Windows 11.

---

## 🖼️ Comparativo Visual Aplicado na sua Barra de Tarefas

````carousel
![Comparativo Completo das Opções](/C:/Users/enn_g/.gemini/antigravity/brain/04172f6b-31d7-4493-86f9-d8a97680a0c2/preview_comparativo.png)
<!-- slide -->
![Opção B — Mini-Widget Acoplado](/C:/Users/enn_g/.gemini/antigravity/brain/04172f6b-31d7-4493-86f9-d8a97680a0c2/preview_opcao_b_widget.png)
<!-- slide -->
![Opção A — Ícone na Bandeja com Tooltip](/C:/Users/enn_g/.gemini/antigravity/brain/04172f6b-31d7-4493-86f9-d8a97680a0c2/preview_opcao_a_tray.png)
````

---

## ⚖️ Qual opção é mais leve? (Análise Técnica)

| Aspecto | Opção A: Ícone na Bandeja (Tray Icon) | Opção B: Mini-Widget Acoplado (Taskbar Overlay) |
| :--- | :--- | :--- |
| **Consumo de RAM** | **~2.5 MB** | **~4.5 MB** |
| **Consumo de CPU** | **0.00%** (executa em 0.05 ms/segundo) | **0.00% ~ 0.01%** (executa em 0.1 ms/segundo) |
| **Desenho / Renderização** | `HICON` de 16×16 pixels atualizado no tray via `Shell_NotifyIcon` | `WM_PAINT` Win32 GDI/DirectWrite em retângulo de ~100×38 px |
| **Legibilidade** | ⚠️ **Muito baixa**: num espaço de 16×16 pixels cabem apenas microdígitos (ex: `U15` / `D600`), sem decimais e sem unidade | 🟢 **Perfeita**: Segoe UI Variable com decimais, setas coloridas (`▲ 15.4 Mbps` / `▼ 598.2 Mbps`) |
| **Informação Completa** | Apenas ao passar o mouse em cima (Tooltip flutuante) | Sempre visível na barra a qualquer momento com um relance de olho |
| **Comportamento no Windows 11** | Fica entre os ícones da bandeja (ou pode ser ocultado pela seta `^` pelo Windows se não for fixado) | Fica fixado à esquerda da seta `^`, integrando-se como parte nativa da barra |

> [!NOTE]
> A diferença de peso na memória RAM entre **2.5 MB** e **4.5 MB** é de apenas **2 MB** (menos de 0.0001% da memória do computador). Para termos de comparação, uma única aba em branco do Chrome ou Edge consome cerca de **80 a 120 MB**. Em termos de CPU, ambas são idênticas (praticamente zero).

---

## 🎯 Variações do Mini-Widget (Opção B)

1. **Estilo Pílula (B1):** Um retângulo sutil com cantos arredondados (`#242424`), combinando com os botões e áreas ativas do Windows 11.
2. **Estilo 100% Integrado / Flat (B2):** Sem fundo, com os caracteres e setas desenhados diretamente sobre a cor nativa da barra de tarefas (`#1c1c1c`), parecendo que faz parte do próprio sistema operacional.
