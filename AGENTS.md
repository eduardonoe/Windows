# Diretrizes Globais do Workspace & Padrões GitHub (Antigravity)

Este workspace (`D:\_Antigravity`) é a base dedicada de desenvolvimento para os projetos do ecossistema de **Eduardo Noé (`eduardonoe`)**, integrando repositórios e ferramentas de **Motion Design, Automação Adobe (AE, Premiere, Illustrator, Photoshop), 3D (Blender 5.2, C4D) e Câmera Virtual (CompCAM)**.

---

## 📌 1. Hierarquia Mandatória de Execução & Economia de Tokens

Para garantir **máxima velocidade, determinismo e economia de tokens**, o agente deve SEMPRE seguir a seguinte ordem de prioridade de ferramentas:

1. **Prioridade 1 (Máxima) — MCPs & Terminal Direto:**
   - Utilizar os servidores MCP dedicados (After Effects, Premiere, Cinema 4D).
   - Executar operações via PowerShell, CMD, scripts Node.js ou Python (`bpy`).
   - Requisições diretas a APIs REST ou CLIs (`gh`, `git`, `ffmpeg`).
   - *Por que:* consome a quantidade mínima de tokens (texto puro estruturado, 30-150 tokens por comando).

2. **Prioridade 2 (Intermediária) — Tarefas em Background:**
   - Processos de longa duração rodando em segundo plano (jobs, builds, renders, testes).

3. **Prioridade 3 (ÚLTIMO RECURSO / Fallback) — Navegador & Controle Visual:**
   - **Somente acionar quando uma tarefa não puder ser resolvida por terminal, API, MCP ou script.**
   - Perfis autorizados do Microsoft Edge:
     - **Pessoal:** `Default` (`Eduardo Nóe`)
     - **Profissional:** `Profile 4` (`Noe VFX`)
   - O perfil `Profile 1` (`Vero Filmes`) é estritamente isolado e não deve sofrer automações automatizadas.

---

## 📌 2. Regra Fundamental para Todos os Chats

1. **Uso Ativo dos MCPs e Ferramentas:**
   - **After Effects MCP:** Servidor em `D:\_Claude\AE-mcp\ae-mcp-server\dist\index.js` e skill `after_effects_controller`.
   - **Premiere Pro MCP:** Servidor em `C:\Users\enn_g\AppData\Roaming\prmcp\server\index.js` (`premiere-bridge`).
   - **Cinema 4D MCP:** Servidor em `D:\_Claude\C4D-mcp\main.py`.
   - **Blender 5.2:** `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` via skill `blender-automation` (`bpy`).
2. **Organização de Pastas e Isolamento:**
   - Todo código novo, testes, scripts gerados e saídas devem ser salvos em **`D:\_Antigravity\`**.
   - O agente pode consultar referências e assets em `D:\_Claude\` sem modificar os arquivos originais de lá.

---

## 📦 3. Padrões Obrigatórios de Desenvolvimento (Especificações GitHub)

Todos os scripts, extensões e projetos devem seguir rigorosamente as especificações dos repositórios oficiais (`CompCAM`, `Scripts-AE`, `Illustrator`, `Photoshop`, `Premiere`, `MCP`):

### A. Padrão de Versionamento Semântico (SemVer)
- Formato obrigatório: `MAJOR.MINOR.PATCH` (ex: `1.0.1`).
  - **PATCH (`1.0.0` ➔ `1.0.1`)**: Correções de bugs, ajustes pequenos, textos, melhorias internas ou compatibilidade.
  - **MINOR (`1.0.0` ➔ `1.1.0`)**: Novos recursos compatíveis com a versão anterior.
  - **MAJOR (`1.0.0` ➔ `2.0.0`)**: Mudanças que quebram compatibilidade ou alteram o fluxo principal.
- Scripts legados marcados como `Version: 1.0` evoluem como `1.0.0` (próximo patch: `1.0.1`).
- **Cabeçalho obrigatório:** Sempre atualizar a versão no cabeçalho do arquivo `.jsx`, `.js` ou manifest:
  ```javascript
  // Version: 1.0.1
  ```
- **Changelog no arquivo:** Quando a alteração for relevante, adicionar bloco curto de changelog no cabeçalho:
  ```text
  v1.0.1 changelog:
  - Corrige comportamento X.
  - Ajusta compatibilidade com Y.
  ```

### B. Padrão de Mensagens de Commit (Git)
Mensagens claras e objetivas em **Português**, com prefixos semânticos padronizados:
- `fix:` para correções
- `feat:` para novos recursos
- `docs:` para documentação
- `chore:` para manutenção, dependências e organização
- `release:` para preparação e fechamento de versão
*Exemplo:* `fix: corrige divisao de texto por linhas` / `feat: adiciona controle de stagger`

### C. Regras de Código Adobe ExtendScript (After Effects & Illustrator)
- **ExtendScript é ES3 estrito:** Proibido usar `let`, `const`, arrow functions (`() => {}`), métodos ES5/ES6 (`Array.map`, `forEach`, `indexOf`, `includes`), `JSON` nativo ou vírgulas sobrando (*trailing commas*).
- Nunca assumir sucesso de `app.executeMenuCommand` — validar o efeito do comando.
- Preservar o estilo existente do script; manter o escopo pequeno e cirúrgico (sem reformatar arquivos inteiros sem necessidade).
- Nunca modificar o documento original destrutivamente: trabalhar sempre sobre cópias ou confirmar o fluxo.

### D. Especificidades por Ecossistema
- **CEP vs UXP:** Não confundir CEP (HTML/JS com ExtendScript para Illustrator/After Effects) com UXP (Photoshop/Premiere moderno).
- **CompCAM:** O arquivo `ae-script/CompCam.jsx` é gerado; edite apenas `ae-script/src/*.js` e execute `node ae-script/build.js`. A matemática pose AE é espelhada entre `10_math.js` e `Math3.kt`. A ponte Python nunca pode rodar órfã sem o AE.

---

## 🎨 4. Skills Registradas e Disponíveis

- `after_effects_controller`: Controle e automação em tempo real do After Effects via Node / ExtendScript.
- `motion-design`: Diretrizes de easing, timing e princípios de animação clássica.
- `gsap-core`, `gsap-timeline`, `gsap-scrolltrigger`, `gsap-plugins`, `gsap-react`, `gsap-frameworks`, `gsap-performance`, `gsap-utils`.
- `threejs-animation`, `threejs-fundamentals`, `threejs-geometry`, `threejs-interaction`, `threejs-lighting`, `threejs-loaders`, `threejs-materials`, `threejs-postprocessing`, `threejs-shaders`, `threejs-textures`.
- `blender-automation`: Execução e render via Blender 5.2 headless e UI.
- `premiere-bridge`: Automação da timeline do Premiere.
- `graphify`: Mapeamento de repositórios e dependências.

---

## ⚡ 5. Cadência de Resposta & Alinhamento Ágil (Padrão Claude)

Para garantir máxima velocidade perceptível, alinhamento perfeito e evitar desperdício de tokens:

1. **Síntese Imediata & Dúvidas Upfront:**
   - Em novas demandas de implementação, UI/UX, refatoração ou resolução de problemas complexos:
     - Sintetizar rapidamente em tópicos curtos o que foi compreendido.
     - Destacar de imediato eventuais dúvidas, premissas ou caminhos de solução.
     - **Aguardar a validação do usuário ("pode fazer", "é isso", etc.) ANTES de iniciar cadeias pesadas de raciocínio profundo, rodar scripts complexos de teste ou modificar arquivos.**
2. **Execução Direta em Tarefas Operacionais:**
   - Pedidos objetivos e diretos (ex: "faça o build", "publique", "leia o arquivo", "commite", "crie o arquivo X conforme combinamos") devem ser executados imediatamente sem confirmação prévia.
3. **Comunicação Enxuta:**
   - Tom direto, técnico e sem rodeios em português do Brasil.
