"""
그림판 UI 아이콘 생성기 (원본 디자인, 실크로드/고서 느낌의 메달 버튼).
    python tools/make_ui_icons.py
→ unity/Assets/_Project/Art/UI/*.png  (Unity 에서 자동으로 스프라이트로 가져옴)
나중에 디자이너 이미지로 같은 파일 이름으로 덮어쓰면 그대로 교체됩니다.
"""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "unity", "Assets", "_Project", "Art", "UI")
S = 4  # 슈퍼샘플링 배율

GOLD, GOLD_D, RIM = (201, 162, 74), (150, 112, 44), (92, 62, 24)
PARCH, PARCH_D, INK = (238, 222, 184), (214, 192, 146), (70, 42, 18)
SEAL, SEAL_D, SEAL_L = (150, 28, 28), (98, 14, 14), (196, 70, 60)


def canvas(n=256):
    return Image.new("RGBA", (n * S, n * S), (0, 0, 0, 0))


def finish(im, n=256):
    return im.resize((n, n), Image.LANCZOS)


def medallion(seal=False, n=256):
    im = canvas(n); d = ImageDraw.Draw(im); N = n * S; c = N / 2
    def circ(r, fill, outline=None, w=0):
        d.ellipse([c - r, c - r, c + r, c + r], fill=fill, outline=outline, width=w)
    # 그림자
    sh = canvas(n); ImageDraw.Draw(sh).ellipse([c - N*0.46, c - N*0.44, c + N*0.46, c + N*0.48], fill=(0, 0, 0, 110))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(S * 6)))
    if seal:  # 붉은 밀랍 인장 (가장자리 울퉁불퉁)
        pts = []
        rnd = random.Random(7)
        for i in range(72):
            a = i / 72 * math.tau; r = N * (0.445 + rnd.uniform(-0.012, 0.012))
            pts.append((c + r * math.cos(a), c + r * math.sin(a)))
        d.polygon(pts, fill=SEAL_D)
        circ(N * 0.40, SEAL)
        circ(N * 0.33, None, SEAL_D, int(S * 5))
        hl = canvas(n); ImageDraw.Draw(hl).ellipse([c - N*0.30, c - N*0.34, c + N*0.05, c - N*0.10], fill=SEAL_L + (35,)); im.alpha_composite(hl)  # 광택 (덮어쓰지 않고 겹치기)
    else:  # 금테 + 양피지 메달
        circ(N * 0.45, RIM)
        circ(N * 0.43, GOLD)
        for k in range(48):  # 테두리 톱니 장식
            a = k / 48 * math.tau; r1, r2 = N * 0.385, N * 0.425
            d.line([(c + r1*math.cos(a), c + r1*math.sin(a)), (c + r2*math.cos(a), c + r2*math.sin(a))], fill=GOLD_D, width=int(S * 2))
        circ(N * 0.375, RIM)
        circ(N * 0.36, PARCH)
        circ(N * 0.32, None, PARCH_D, int(S * 3))
        hl = canvas(n); ImageDraw.Draw(hl).ellipse([c - N*0.28, c - N*0.33, c + N*0.10, c - N*0.12], fill=(255, 255, 255, 28)); im.alpha_composite(hl)  # 광택 (덮어쓰지 않고 겹치기)
    return im


def icon_layer(n=256):
    return canvas(n)


def compose(base, layer, n=256):
    base.alpha_composite(layer)
    return finish(base, n)


def P(x, y, n=256):  # 0~1 좌표 → 캔버스 좌표
    return (x * n * S, y * n * S)


def draw_brush(d, col):
    # 붓: 대각선 대나무 자루 + 털 끝
    d.line([P(.66, .30), P(.44, .52)], fill=col, width=int(S * 22))
    for t in (.36, .48):
        x = .66 - (.66 - .44) * t; y = .30 + (.52 - .30) * t
        d.line([P(x - .03, y - .03), P(x + .03, y + .03)], fill=PARCH, width=int(S * 4))
    d.polygon([P(.47, .47), P(.40, .55), P(.33, .70), P(.30, .73), P(.36, .67), P(.52, .52)], fill=col)
    d.ellipse([*P(.27, .69), *P(.35, .77)], fill=col)


def draw_eraser(d, col):
    a = math.radians(-35); cx, cy, w, h = .50, .52, .40, .20
    def rot(px, py):
        return P(cx + px * math.cos(a) - py * math.sin(a), cy + px * math.sin(a) + py * math.cos(a))
    d.polygon([rot(-w/2, -h/2), rot(w/2, -h/2), rot(w/2, h/2), rot(-w/2, h/2)], fill=col)
    d.polygon([rot(-w/2, -h/2), rot(-w/2 + .15, -h/2), rot(-w/2 + .15, h/2), rot(-w/2, h/2)], fill=PARCH_D)
    d.line([P(.28, .74), P(.72, .74)], fill=col, width=int(S * 6))


def draw_fill(d, col):
    a = math.radians(-20); cx, cy = .46, .50
    def rot(px, py):
        return P(cx + px * math.cos(a) - py * math.sin(a), cy + px * math.sin(a) + py * math.cos(a))
    d.polygon([rot(-.17, -.15), rot(.17, -.15), rot(.13, .17), rot(-.13, .17)], fill=col)
    d.arc([*rot(-.13, -.30), *rot(.13, -.02)], 180, 360, fill=col, width=int(S * 8))
    d.polygon([rot(.17, -.15), rot(.25, -.05), rot(.24, .02), rot(.15, -.08)], fill=col)
    d.ellipse([*P(.66, .58), *P(.74, .70)], fill=col)
    d.polygon([P(.66, .63), P(.70, .52), P(.74, .63)], fill=col)


def draw_arrow(d, col, mirror=False):
    def m(x):
        return 1 - x if mirror else x
    box = [*P(.30, .32), *P(.72, .72)]
    if mirror:
        box = [*P(.28, .32), *P(.70, .72)]
    d.arc(box, 200 if not mirror else -20, 360 + 20 if not mirror else 160 - 0, fill=col, width=int(S * 14))
    tip = [P(m(.25), .44), P(m(.40), .34), P(m(.38), .52)]
    d.polygon(tip, fill=col)


def draw_send(d, col):
    # 오른쪽 화살표 → (누구나 "다음/보내기"로 이해)
    d.polygon([P(.27, .43), P(.52, .43), P(.52, .29), P(.75, .50), P(.52, .71), P(.52, .57), P(.27, .57)], fill=col)


def draw_clear(d, col):
    w = int(S * 16)
    d.line([P(.34, .34), P(.66, .66)], fill=col, width=w)
    d.line([P(.66, .34), P(.34, .66)], fill=col, width=w)


def draw_home(d, col):
    d.polygon([P(.50, .27), P(.74, .49), P(.67, .49), P(.67, .72), P(.33, .72), P(.33, .49), P(.26, .49)], fill=col)
    d.rectangle([*P(.45, .57), *P(.55, .72)], fill=PARCH)
    d.ellipse([*P(.465, .37), *P(.535, .44)], fill=PARCH)  # 둥근 창 (아치 느낌)


def make_button(name, draw_fn, seal=False, **kw):
    base = medallion(seal)
    lay = icon_layer(); d = ImageDraw.Draw(lay)
    col = (238, 205, 120) if seal else INK
    draw_fn(d, col, **kw)
    compose(base, lay).save(os.path.join(OUT, name))


def make_selected_ring(n=256):
    im = canvas(n); d = ImageDraw.Draw(im); N = n * S; c = N / 2
    for i, a in enumerate((120, 190, 255)):
        r = N * (0.49 - i * 0.012)
        d.ellipse([c - r, c - r, c + r, c + r], outline=(255, 200, 70, a), width=int(S * 7))
    finish(im.filter(ImageFilter.GaussianBlur(S * 2)), n).save(os.path.join(OUT, "ring_selected.png"))


def make_stroke(w=200, h=90):
    """팔레트용 붓 자국 (흰색 → Unity 에서 색을 입힘)"""
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    rnd = random.Random(3); W, H = w * S, h * S
    top, bot = [], []
    for i in range(41):
        t = i / 40; x = W * (0.04 + 0.92 * t)
        thick = H * (0.18 + 0.22 * math.sin(math.pi * min(1, t * 1.15)) ** 0.6)
        mid = H * 0.5 + H * 0.06 * math.sin(t * 5)
        top.append((x, mid - thick + rnd.uniform(-1, 1) * S * 2))
        bot.append((x, mid + thick * 0.9 + rnd.uniform(-1, 1) * S * 2))
    d.polygon(top + bot[::-1], fill=(255, 255, 255, 255))
    d.ellipse([W * 0.0, H * 0.32, W * 0.12, H * 0.70], fill=(255, 255, 255, 255))
    for k in range(9):  # 마른 붓 결
        y = H * (0.30 + k * 0.05)
        d.line([(W * (0.70 + rnd.uniform(0, .1)), y), (W * 0.97, y + rnd.uniform(-3, 3) * S)], fill=(0, 0, 0, 0), width=int(S * 1.5))
    im = im.resize((w, h), Image.LANCZOS)
    im.save(os.path.join(OUT, "palette_stroke.png"))


def make_wedge(w=600, h=90):
    """굵기 슬라이더 배경 '<' 모양: 왼쪽 가늘고 오른쪽 굵게"""
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0)); d = ImageDraw.Draw(im); W, H = w * S, h * S
    d.polygon([(W * 0.02, H * 0.5), (W * 0.98, H * 0.06), (W * 0.98, H * 0.94)], fill=(255, 255, 255, 255))
    d.ellipse([W * 0.0, H * 0.44, W * 0.04, H * 0.56], fill=(255, 255, 255, 255))
    im.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, "size_wedge.png"))


def make_knob(n=96):
    im = canvas(n); d = ImageDraw.Draw(im); N = n * S; c = N / 2
    d.ellipse([c - N*.46, c - N*.46, c + N*.46, c + N*.46], fill=RIM)
    d.ellipse([c - N*.40, c - N*.40, c + N*.40, c + N*.40], fill=GOLD)
    d.ellipse([c - N*.18, c - N*.18, c + N*.18, c + N*.18], fill=RIM)
    finish(im, n).save(os.path.join(OUT, "size_knob.png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    make_button("btn_brush.png", draw_brush)
    make_button("btn_eraser.png", draw_eraser)
    make_button("btn_fill.png", draw_fill)
    make_button("btn_undo.png", draw_arrow, mirror=False)
    make_button("btn_redo.png", draw_arrow, mirror=True)
    make_button("btn_send.png", draw_send, seal=True)
    make_button("btn_clear.png", draw_clear)
    make_button("btn_home.png", draw_home)
    make_selected_ring()
    make_stroke()
    make_wedge()
    make_knob()
    print("생성 완료:", OUT)
