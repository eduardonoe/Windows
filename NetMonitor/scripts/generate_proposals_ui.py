import os
from PIL import Image, ImageDraw, ImageFont

artifact_dir = r"C:\Users\enn_g\.gemini\antigravity\brain\04172f6b-31d7-4493-86f9-d8a97680a0c2"

# Fonts
font_title = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 13)
font_bold = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 12)
font_regular = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 12)
font_small = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 10)
font_mono = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 11)
font_icon = ImageFont.truetype("C:/Windows/Fonts/segmdl2.ttf", 13)

def draw_window_frame(draw, w, h, title, bg_col, title_col, border_col, is_dark=True):
    # Background and border
    draw.rounded_rectangle([0, 0, w-1, h-1], radius=8, fill=bg_col, outline=border_col, width=1)
    
    # Titlebar
    draw.rectangle([1, 1, w-2, 34], fill=bg_col)
    
    # App icon (little blue circle/wifi)
    draw.ellipse([14, 11, 24, 21], fill=(0, 120, 212))
    draw.text((32, 10), title, font=font_title, fill=title_col)
    
    # Caption buttons (minimize, maximize, close)
    btn_col = (160, 160, 160) if is_dark else (90, 90, 90)
    # close 'X'
    cx = w - 24
    draw.line([cx-4, 13, cx+4, 21], fill=btn_col, width=1)
    draw.line([cx+4, 13, cx-4, 21], fill=btn_col, width=1)
    # maximize
    mx = w - 50
    draw.rectangle([mx-4, 13, mx+4, 21], outline=btn_col, width=1)
    # minimize
    lx = w - 76
    draw.line([lx-4, 17, lx+4, 17], fill=btn_col, width=1)

def draw_live_preview(draw, x, y, bg_col, text_col, arrow_col, arrow_type="▲", unit="Mbps"):
    # Live preview container
    draw.rounded_rectangle([x, y, x+110, y+42], radius=4, fill=(24, 24, 24), outline=(50, 50, 50), width=1)
    # Upload
    draw.text((x+8, y+5), "823.6", font=font_bold, fill=text_col)
    draw.text((x+50, y+6), unit, font=font_regular, fill=(180, 180, 180))
    draw.text((x+90, y+5), "▲" if arrow_type=="▲" else arrow_type[0], font=font_bold, fill=arrow_col)
    # Download
    draw.text((x+8, y+23), "142.0", font=font_bold, fill=text_col)
    draw.text((x+50, y+24), unit, font=font_regular, fill=(180, 180, 180))
    draw.text((x+90, y+23), "▼" if arrow_type=="▲" else arrow_type[1], font=font_bold, fill=arrow_col)


# ==============================================================================
# PROPOSTA A: Fluent Windows 11 (Segmented Controls no Topo)
# ==============================================================================
def render_proposta_a():
    W, H = 580, 520
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    
    # Dark Theme base
    draw_window_frame(draw, W, H, "Personalização do NetMonitor v1.0.0", (32, 32, 35), (255, 255, 255), (60, 60, 65), True)
    
    # 1. Segmented Control Bar (Top)
    bar_x, bar_y, bar_w, bar_h = 24, 48, W - 48, 36
    draw.rounded_rectangle([bar_x, bar_y, bar_x+bar_w, bar_y+bar_h], radius=6, fill=(24, 24, 26))
    
    tabs = ["Geral", "Tipografia", "Cores", "Ícones & Setas"]
    tab_w = bar_w // 4
    for i, t in enumerate(tabs):
        tx = bar_x + i * tab_w
        if i == 0: # Active
            draw.rounded_rectangle([tx+3, bar_y+3, tx+tab_w-3, bar_y+bar_h-3], radius=5, fill=(0, 120, 212))
            draw.text((tx + tab_w//2 - 16, bar_y+10), t, font=font_bold, fill=(255, 255, 255))
        else:
            draw.text((tx + tab_w//2 - 24 if len(t)>6 else tx + tab_w//2 - 14, bar_y+10), t, font=font_regular, fill=(180, 180, 185))
            
    # 2. Main Content Card
    card_x, card_y, card_w, card_h = 24, 98, W - 48, 280
    draw.rounded_rectangle([card_x, card_y, card_x+card_w, card_y+card_h], radius=6, fill=(40, 40, 44), outline=(55, 55, 60), width=1)
    
    # Setting Rows
    # Row 1: Idioma
    draw.text((card_x+18, card_y+18), "Idioma da Interface:", font=font_regular, fill=(230, 230, 230))
    draw.rounded_rectangle([card_x+220, card_y+14, card_x+460, card_y+40], radius=4, fill=(28, 28, 30), outline=(65, 65, 70), width=1)
    draw.text((card_x+232, card_y+20), "Português (Brasil)", font=font_regular, fill=(255, 255, 255))
    draw.text((card_x+442, card_y+21), "⌄", font=font_bold, fill=(180, 180, 180))
    
    # Row 2: Formato de Medição (Bits vs Bytes - NOVA OPÇÃO!)
    draw.text((card_x+18, card_y+58), "Formato de Medição:", font=font_regular, fill=(230, 230, 230))
    draw.rounded_rectangle([card_x+220, card_y+54, card_x+460, card_y+80], radius=4, fill=(28, 28, 30), outline=(65, 65, 70), width=1)
    draw.text((card_x+232, card_y+60), "Bits (Mbps / Kbps) ⬌ Bytes (MB/s)", font=font_regular, fill=(0, 150, 255))
    draw.text((card_x+442, card_y+61), "⌄", font=font_bold, fill=(180, 180, 180))
    
    # Row 3: Unidade Específica
    draw.text((card_x+18, card_y+98), "Escala de Velocidade:", font=font_regular, fill=(230, 230, 230))
    draw.rounded_rectangle([card_x+220, card_y+94, card_x+460, card_y+120], radius=4, fill=(28, 28, 30), outline=(65, 65, 70), width=1)
    draw.text((card_x+232, card_y+100), "Automático (Kbps / Mbps / Gbps)", font=font_regular, fill=(255, 255, 255))
    draw.text((card_x+442, card_y+101), "⌄", font=font_bold, fill=(180, 180, 180))
    
    # Separator line
    draw.line([card_x+18, card_y+136, card_x+card_w-18, card_y+136], fill=(55, 55, 60), width=1)
    
    # Row 4: Posição das Setas
    draw.text((card_x+18, card_y+152), "Posicionamento das Setas:", font=font_regular, fill=(230, 230, 230))
    draw.ellipse([card_x+220, card_y+154, card_x+232, card_y+166], outline=(0, 120, 212), width=2)
    draw.ellipse([card_x+223, card_y+157, card_x+229, card_y+163], fill=(0, 120, 212))
    draw.text((card_x+240, card_y+152), "Setas à Direita: [ 823.6 Mbps ▲ ] (Padrão)", font=font_regular, fill=(255, 255, 255))
    
    draw.ellipse([card_x+220, card_y+184, card_x+232, card_y+196], outline=(100, 100, 100), width=2)
    draw.text((card_x+240, card_y+182), "Setas à Esquerda: [ ▲ 823.6 Mbps ]", font=font_regular, fill=(190, 190, 190))
    
    # Separator line
    draw.line([card_x+18, card_y+214, card_x+card_w-18, card_y+214], fill=(55, 55, 60), width=1)
    
    # Row 5: Travar e Proteção Screensaver
    draw.rounded_rectangle([card_x+20, card_y+228, card_x+34, card_y+242], radius=3, fill=(0, 120, 212))
    draw.text((card_x+23, card_y+226), "✓", font=font_bold, fill=(255, 255, 255))
    draw.text((card_x+44, card_y+227), "Travar posição na barra de tarefas (bloqueia arrasto)", font=font_regular, fill=(240, 240, 240))

    draw.rounded_rectangle([card_x+20, card_y+252, card_x+34, card_y+266], radius=3, fill=(0, 120, 212))
    draw.text((card_x+23, card_y+250), "✓", font=font_bold, fill=(255, 255, 255))
    draw.text((card_x+44, card_y+251), "Ocultar automaticamente no Screensaver e Lockscreen", font=font_regular, fill=(0, 200, 255))
    
    # 3. Bottom Area: Live Preview + Action buttons
    prev_box_y = 392
    draw.rounded_rectangle([24, prev_box_y, W-24, prev_box_y+58], radius=6, fill=(38, 38, 42), outline=(55, 55, 60), width=1)
    draw_live_preview(draw, 34, prev_box_y+8, (20, 20, 20), (255, 255, 255), (225, 225, 225))
    
    draw.text((158, prev_box_y+14), "Pré-visualização em Tempo Real (Live Preview)", font=font_bold, fill=(255, 255, 255))
    draw.text((158, prev_box_y+32), "Reflete imediatamente qualquer alteração de cor, fonte ou template.", font=font_small, fill=(160, 160, 165))
    
    # Action Buttons
    btn_y = 466
    draw.rounded_rectangle([W-230, btn_y, W-130, btn_y+36], radius=4, fill=(0, 120, 212))
    draw.text((W-195, btn_y+10), "Salvar", font=font_bold, fill=(255, 255, 255))
    
    draw.rounded_rectangle([W-120, btn_y, W-24, btn_y+36], radius=4, fill=(50, 50, 54), outline=(70, 70, 75), width=1)
    draw.text((W-88, btn_y+10), "Cancelar", font=font_regular, fill=(230, 230, 230))
    
    path = os.path.join(artifact_dir, "proposta_a_fluent.png")
    im.save(path)
    return path


# ==============================================================================
# PROPOSTA B: Sidebar Lateral (Estilo Windows 11 Settings)
# ==============================================================================
def render_proposta_b():
    W, H = 640, 500
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    
    # Modern Mica Dark Frame
    draw_window_frame(draw, W, H, "Personalização do NetMonitor v1.0.0", (28, 28, 30), (255, 255, 255), (55, 55, 60), True)
    
    # 1. Left Sidebar
    sb_w = 160
    draw.rectangle([1, 35, sb_w, H-2], fill=(22, 22, 24))
    draw.line([sb_w, 35, sb_w, H-2], fill=(45, 45, 50), width=1)
    
    items = [
        ("⚙ Geral", True),
        ("🔤 Tipografia", False),
        ("🎨 Cores", False),
        ("🔣 Ícones & Setas", False),
        ("🛡 Sistema", False)
    ]
    
    sy = 50
    for name, active in items:
        if active:
            draw.rounded_rectangle([8, sy, sb_w-8, sy+34], radius=5, fill=(42, 42, 46))
            draw.rounded_rectangle([8, sy+6, 11, sy+28], radius=2, fill=(0, 120, 212)) # Blue accent indicator
            draw.text((22, sy+9), name, font=font_bold, fill=(255, 255, 255))
        else:
            draw.text((22, sy+9), name, font=font_regular, fill=(170, 170, 175))
        sy += 42
        
    # Version badge at bottom of sidebar
    draw.text((20, H-30), "Versão: 1.0.0 (Oficial)", font=font_small, fill=(110, 110, 115))
    
    # 2. Right Content Area (Cards)
    rx = sb_w + 20
    rw = W - rx - 20
    
    draw.text((rx, 50), "Configurações Gerais & Formato", font=font_title, fill=(255, 255, 255))
    draw.text((rx, 70), "Personalize a exibição na barra de tarefas e unidades de tráfego.", font=font_small, fill=(150, 150, 155))
    
    # Setting Card 1
    cy = 96
    draw.rounded_rectangle([rx, cy, rx+rw, cy+60], radius=6, fill=(35, 35, 38), outline=(50, 50, 55), width=1)
    draw.text((rx+16, cy+14), "Formato de Tráfego de Rede", font=font_bold, fill=(255, 255, 255))
    draw.text((rx+16, cy+32), "Alterne entre medição de velocidade (Mbps) e taxa de arquivo (MB/s)", font=font_small, fill=(160, 160, 165))
    draw.rounded_rectangle([rx+rw-150, cy+16, rx+rw-16, cy+44], radius=4, fill=(24, 24, 26), outline=(65, 65, 70), width=1)
    draw.text((rx+rw-138, cy+23), "Mbps (Velocidade)", font=font_regular, fill=(0, 150, 255))
    draw.text((rx+rw-32, cy+24), "⌄", font=font_bold, fill=(180, 180, 180))
    
    # Setting Card 2
    cy += 70
    draw.rounded_rectangle([rx, cy, rx+rw, cy+60], radius=6, fill=(35, 35, 38), outline=(50, 50, 55), width=1)
    draw.text((rx+16, cy+14), "Ordem & Alinhamento dos Elementos", font=font_bold, fill=(255, 255, 255))
    draw.text((rx+16, cy+32), "Posição das setas de upload/download em relação aos números", font=font_small, fill=(160, 160, 165))
    draw.rounded_rectangle([rx+rw-150, cy+16, rx+rw-16, cy+44], radius=4, fill=(24, 24, 26), outline=(65, 65, 70), width=1)
    draw.text((rx+rw-138, cy+23), "Setas à Direita", font=font_regular, fill=(255, 255, 255))
    draw.text((rx+rw-32, cy+24), "⌄", font=font_bold, fill=(180, 180, 180))
    
    # Setting Card 3 (Toggles)
    cy += 70
    draw.rounded_rectangle([rx, cy, rx+rw, cy+78], radius=6, fill=(35, 35, 38), outline=(50, 50, 55), width=1)
    # Toggle 1
    draw.rounded_rectangle([rx+16, cy+14, rx+48, cy+30], radius=8, fill=(0, 120, 212))
    draw.ellipse([rx+32, cy+15, rx+46, cy+29], fill=(255, 255, 255))
    draw.text((rx+58, cy+15), "Travar posição do widget na barra de tarefas", font=font_regular, fill=(240, 240, 240))
    # Toggle 2
    draw.rounded_rectangle([rx+16, cy+46, rx+48, cy+62], radius=8, fill=(0, 120, 212))
    draw.ellipse([rx+32, cy+47, rx+46, cy+61], fill=(255, 255, 255))
    draw.text((rx+58, cy+47), "Ocultar quando o Screensaver ou Tela de Bloqueio ativar", font=font_regular, fill=(0, 200, 255))
    
    # Bottom Live Preview & Action bar
    by = H - 75
    draw.line([rx, by-10, rx+rw, by-10], fill=(50, 50, 55), width=1)
    draw_live_preview(draw, rx, by, (20, 20, 20), (255, 255, 255), (225, 225, 225))
    
    draw.rounded_rectangle([W-210, by+6, W-120, by+40], radius=4, fill=(0, 120, 212))
    draw.text((W-180, by+15), "Salvar", font=font_bold, fill=(255, 255, 255))
    
    draw.rounded_rectangle([W-110, by+6, W-20, by+40], radius=4, fill=(45, 45, 48), outline=(65, 65, 70), width=1)
    draw.text((W-80, by+15), "Cancelar", font=font_regular, fill=(220, 220, 220))

    path = os.path.join(artifact_dir, "proposta_b_sidebar.png")
    im.save(path)
    return path


# ==============================================================================
# PROPOSTA C: Modern Dark-First (Estúdio Pro & Hardware VST)
# ==============================================================================
def render_proposta_c():
    W, H = 580, 530
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    
    # Ultra-deep Onyx Frame
    draw_window_frame(draw, W, H, "Personalização do NetMonitor v1.0.0", (18, 18, 20), (255, 255, 255), (45, 45, 50), True)
    
    # 1. Studio Header Banner
    draw.text((24, 46), "PAINEL DE CALIBRAÇÃO & DESIGN", font=font_mono, fill=(0, 120, 212))
    draw.text((24, 64), "Ajuste milimétrico de tipografia, cores, ícones e unidades.", font=font_small, fill=(140, 140, 145))
    
    # 2. Studio Tabs (Clean minimalist underline)
    ty = 90
    draw.text((24, ty), "GERAL", font=font_bold, fill=(0, 120, 212))
    draw.line([24, ty+20, 75, ty+20], fill=(0, 120, 212), width=2)
    
    draw.text((110, ty), "TIPOGRAFIA", font=font_regular, fill=(160, 160, 165))
    draw.text((215, ty), "CORES", font=font_regular, fill=(160, 160, 165))
    draw.text((290, ty), "ÍCONES & TEMPLATES", font=font_regular, fill=(160, 160, 165))
    draw.line([24, ty+20, W-24, ty+20], fill=(40, 40, 45), width=1)
    
    # 3. Two-Column Studio Layout
    cy = 125
    col_w = (W - 60) // 2
    
    # Left Column: Modos & Unidades
    draw.rounded_rectangle([24, cy, 24+col_w, cy+180], radius=6, fill=(25, 25, 28), outline=(42, 42, 48), width=1)
    draw.text((36, cy+14), "MODO DE TRÁFEGO", font=font_mono, fill=(180, 180, 185))
    
    # Bits vs Bytes Pills
    draw.rounded_rectangle([36, cy+38, 36+col_w-24, cy+68], radius=4, fill=(18, 18, 20), outline=(50, 50, 55), width=1)
    draw.rounded_rectangle([38, cy+40, 38+(col_w-28)//2, cy+66], radius=3, fill=(0, 120, 212))
    draw.text((58, cy+48), "Bits (Mbps)", font=font_bold, fill=(255, 255, 255))
    draw.text((160, cy+48), "Bytes (MB/s)", font=font_regular, fill=(150, 150, 155))
    
    draw.text((36, cy+84), "LAYOUT DAS SETAS", font=font_mono, fill=(180, 180, 185))
    draw.rounded_rectangle([36, cy+104, 36+col_w-24, cy+134], radius=4, fill=(18, 18, 20), outline=(50, 50, 55), width=1)
    draw.rounded_rectangle([38, cy+106, 38+(col_w-28)//2, cy+132], radius=3, fill=(45, 45, 50))
    draw.text((48, cy+114), "À Direita ▲", font=font_bold, fill=(255, 255, 255))
    draw.text((155, cy+114), "▲ À Esquerda", font=font_regular, fill=(150, 150, 155))
    
    draw.text((36, cy+150), "ESCALA: Automático (Auto-Range)", font=font_small, fill=(0, 190, 255))

    # Right Column: Novos Templates de Setas em Destaque!
    rx = 24 + col_w + 12
    draw.rounded_rectangle([rx, cy, rx+col_w, cy+180], radius=6, fill=(25, 25, 28), outline=(42, 42, 48), width=1)
    draw.text((rx+14, cy+14), "TEMPLATES DE SETAS", font=font_mono, fill=(180, 180, 185))
    
    tpls = [
        ("1. Triângulos Vetoriais (Nativo)", "▲ / ▼"),
        ("2. Chevrons Fluentes Windows", "∧ / ∨"),
        ("3. NOVO: Dual Speed Booster", "▲▲ / ▼▼"),
        ("4. NOVO: Minimalist Stream Tech", "↗ / ↘")
    ]
    ty_sub = cy + 36
    for idx, (tname, tchar) in enumerate(tpls):
        is_sel = (idx == 2) # Highlighting new dual booster
        box_bg = (35, 35, 42) if is_sel else (18, 18, 20)
        border_c = (0, 120, 212) if is_sel else (40, 40, 45)
        draw.rounded_rectangle([rx+12, ty_sub, rx+col_w-12, ty_sub+30], radius=4, fill=box_bg, outline=border_c, width=1)
        draw.text((rx+20, ty_sub+7), tname, font=font_small, fill=(255, 255, 255) if is_sel else (170, 170, 175))
        draw.text((rx+col_w-55, ty_sub+7), tchar, font=font_bold, fill=(0, 200, 255) if is_sel else (200, 200, 200))
        ty_sub += 34

    # 4. System Toggles Bar
    sy = cy + 195
    draw.rounded_rectangle([24, sy, W-24, sy+54], radius=6, fill=(25, 25, 28), outline=(42, 42, 48), width=1)
    
    # Check 1
    draw.rounded_rectangle([36, sy+18, 50, sy+32], radius=3, fill=(0, 120, 212))
    draw.text((39, sy+17), "✓", font=font_bold, fill=(255, 255, 255))
    draw.text((58, sy+18), "Travar posição na barra de tarefas", font=font_regular, fill=(240, 240, 240))
    
    # Check 2
    draw.rounded_rectangle([300, sy+18, 314, sy+32], radius=3, fill=(0, 120, 212))
    draw.text((303, sy+17), "✓", font=font_bold, fill=(255, 255, 255))
    draw.text((322, sy+18), "Ocultar no Screensaver / Bloqueio", font=font_regular, fill=(0, 210, 255))

    # 5. Bottom Live Preview & Action bar
    by = H - 75
    draw.line([24, by-10, W-24, by-10], fill=(42, 42, 48), width=1)
    draw_live_preview(draw, 24, by, (14, 14, 16), (255, 255, 255), (0, 210, 255), arrow_type=("▲▲", "▼▼"))
    
    draw.text((148, by+8), "MONITOR DE TRÁFEGO ATIVO (LIVE)", font=font_mono, fill=(0, 120, 212))
    draw.text((148, by+24), "Renderização idêntica à taskbar com os novos ícones calibrados.", font=font_small, fill=(150, 150, 155))
    
    draw.rounded_rectangle([W-210, by+6, W-120, by+40], radius=4, fill=(0, 120, 212))
    draw.text((W-180, by+15), "Salvar", font=font_bold, fill=(255, 255, 255))
    
    draw.rounded_rectangle([W-110, by+6, W-24, by+40], radius=4, fill=(35, 35, 40), outline=(55, 55, 60), width=1)
    draw.text((W-84, by+15), "Cancelar", font=font_regular, fill=(210, 210, 215))

    path = os.path.join(artifact_dir, "proposta_c_dark_studio.png")
    im.save(path)
    return path


# ==============================================================================
# COMBINADO: Comparativo Lado a Lado das 3 Propostas
# ==============================================================================
def render_comparativo(path_a, path_b, path_c):
    im_a = Image.open(path_a)
    im_b = Image.open(path_b)
    im_c = Image.open(path_c)
    
    # Scale each to height 480
    def scale_to_h(im, target_h):
        w, h = im.size
        new_w = int(w * (target_h / h))
        return im.resize((new_w, target_h), Image.Resampling.LANCZOS)
    
    target_h = 420
    s_a = scale_to_h(im_a, target_h)
    s_b = scale_to_h(im_b, target_h)
    s_c = scale_to_h(im_c, target_h)
    
    total_w = s_a.width + s_b.width + s_c.width + 80
    total_h = target_h + 120
    
    board = Image.new("RGB", (total_w, total_h), (18, 18, 20))
    draw = ImageDraw.Draw(board)
    
    # Header title
    font_main = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 20)
    font_sub = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 13)
    draw.text((30, 20), "NetMonitor v1.0.0 — Comparativo de Design & Layout (Propostas A, B e C)", font=font_main, fill=(255, 255, 255))
    draw.text((30, 48), "Escolha o layout ideal: Fluent Top Bar, Sidebar Lateral (Configurações Win11) ou Studio Dark Pro.", font=font_sub, fill=(170, 170, 175))
    
    # Paste images
    curr_x = 30
    draw.text((curr_x, 82), "PROPOSTA A: Fluent Top Bar", font=font_bold, fill=(0, 150, 255))
    board.paste(s_a, (curr_x, 105), s_a)
    
    curr_x += s_a.width + 25
    draw.text((curr_x, 82), "PROPOSTA B: Sidebar Lateral Win11", font=font_bold, fill=(0, 220, 130))
    board.paste(s_b, (curr_x, 105), s_b)
    
    curr_x += s_b.width + 25
    draw.text((curr_x, 82), "PROPOSTA C: Dark Pro Studio", font=font_bold, fill=(255, 170, 0))
    board.paste(s_c, (curr_x, 105), s_c)
    
    comp_path = os.path.join(artifact_dir, "comparativo_propostas_ui.png")
    board.save(comp_path)
    print("Salvo com sucesso em:", comp_path)

p_a = render_proposta_a()
p_b = render_proposta_b()
p_c = render_proposta_c()
render_comparativo(p_a, p_b, p_c)
