"""
병원 진료실 그림: 포스터·간판·시력표·키 재기·시계 판·색 견본을 한 장(2048²)에 모은 그림판과 바닥·천장 타일.
사용: python Tools/make_clinic_art.py  → Assets/Texture/Clinic/진료실_그림.png, 진료실_바닥.png, 진료실_천장.png
칸 배치는 JourneyRealism.ClinicDecor.cs 의 Atlas 칸과 같아야 한다 (왼쪽 위 원점, 픽셀).
"""
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets/Texture/Clinic"
CAP = ROOT / "Tools/realism_captures"
FONT = "C:/Windows/Fonts/malgun.ttf"
BOLD = "C:/Windows/Fonts/malgunbd.ttf"

NAVY = (22, 52, 74)
TEAL = (38, 150, 160)
SKY = (127, 227, 255)
PAPER = (246, 244, 238)
INK = (34, 42, 50)


def font(size, bold=False):
    return ImageFont.truetype(BOLD if bold else FONT, size)


def centered(d, box, text, f, fill):
    x0, y0, x1, y1 = box
    l, t, r, b = d.textbbox((0, 0), text, font=f)
    d.text(((x0 + x1 - (r - l)) / 2 - l, (y0 + y1 - (b - t)) / 2 - t), text, font=f, fill=fill)


def cover(img, w, h):
    """사진을 칸 비율에 맞춰 가운데를 잘라 채운다."""
    s = max(w / img.width, h / img.height)
    img = img.resize((math.ceil(img.width * s), math.ceil(img.height * s)), Image.LANCZOS)
    x, y = (img.width - w) // 2, (img.height - h) // 2
    return img.crop((x, y, x + w, y + h))


def organ_poster(n, title, photo, caption, sub, crop=None):
    w, h = 512, 724
    p = Image.new("RGB", (w, h), PAPER)
    d = ImageDraw.Draw(p)
    d.rectangle((0, 0, w, 120), fill=NAVY)
    d.text((28, 16), f"우리 몸 소화 여행  {n}/4", font=font(24), fill=SKY)
    d.text((28, 50), title, font=font(46, True), fill=(255, 255, 255))
    src = Image.open(photo if Path(photo).is_absolute() else CAP / photo).convert("RGB")
    if crop:  # 잠수정 창틀·안내판이 안 들어간 부분만 (비율 0~1)
        src = src.crop((int(crop[0] * src.width), int(crop[1] * src.height), int(crop[2] * src.width), int(crop[3] * src.height)))
    pic = cover(src, w - 48, 380)
    p.paste(pic, (24, 140))
    d.rectangle((24, 140, w - 25, 519), outline=NAVY, width=3)
    d.rounded_rectangle((24, 540, w - 24, 600), radius=14, fill=TEAL)
    centered(d, (24, 540, w - 24, 600), caption, font(28, True), (255, 255, 255))
    y = 616
    for line in sub:
        d.text((30, y), "· " + line, font=font(24), fill=INK)
        y += 34
    return p


def brushing_poster():
    w, h = 512, 724
    p = Image.new("RGB", (w, h), (236, 248, 250))
    d = ImageDraw.Draw(p)
    d.rectangle((0, 0, w, 150), fill=TEAL)
    centered(d, (0, 10, w, 90), "양치 3·3·3", font(64, True), (255, 255, 255))
    centered(d, (0, 92, w, 140), "충치를 막는 세 가지 약속", font(28), (230, 250, 255))
    rows = [("하루", "3번 닦아요"), ("밥 먹고", "3분 안에"), ("한 번에", "3분 동안")]
    for i, (a, b) in enumerate(rows):
        y = 180 + i * 170
        d.ellipse((34, y, 164, y + 130), fill=(255, 255, 255), outline=TEAL, width=6)
        centered(d, (34, y, 164, y + 130), "3", font(86, True), TEAL)
        d.text((190, y + 14), a, font=font(30), fill=INK)
        d.text((190, y + 54), b, font=font(44, True), fill=NAVY)
    return p


def habits_poster():
    w, h = 512, 724
    p = Image.new("RGB", (w, h), PAPER)
    d = ImageDraw.Draw(p)
    d.rectangle((0, 0, w, 130), fill=(64, 140, 90))
    centered(d, (0, 14, w, 80), "튼튼한 소화 습관", font(50, True), (255, 255, 255))
    centered(d, (0, 80, w, 120), "배가 편안해지는 네 가지", font(26), (225, 245, 230))
    items = [((240, 170, 60), "천천히 꼭꼭", "씹어 먹어요"), ((90, 180, 90), "채소와 과일을", "골고루 먹어요"),
             ((80, 160, 220), "물을 자주", "마셔요"), ((230, 110, 110), "밥은 제때", "먹어요")]
    for i, (c, a, b) in enumerate(items):
        y = 156 + i * 140
        d.ellipse((30, y + 8, 130, y + 108), fill=c)
        centered(d, (30, y + 8, 130, y + 108), str(i + 1), font(56, True), (255, 255, 255))
        d.text((156, y + 18), a, font=font(34, True), fill=INK)
        d.text((156, y + 62), b, font=font(30), fill=(80, 90, 100))
    return p


def eye_chart():
    w, h = 384, 724
    p = Image.new("RGB", (w, h), (252, 252, 250))
    d = ImageDraw.Draw(p)
    centered(d, (0, 8, w, 60), "시 력 표", font(38, True), INK)
    d.line((24, 64, w - 24, 64), fill=INK, width=2)
    rng = random.Random(7)
    sizes = [96, 74, 58, 46, 38, 31, 25, 20, 16, 13]
    y = 76
    for i, s in enumerate(sizes):
        n = 1 if i < 2 else 2 if i < 4 else 3 if i < 6 else 4
        txt = "  ".join(str(rng.choice("2345679")) for _ in range(n))
        centered(d, (40, y, w - 10, y + s + 8), txt, font(s, True), INK)
        d.text((8, y + (s + 8) / 2 - 10), f"{(i + 1) / 10:.1f}", font=font(16), fill=(120, 120, 120))
        y += s + 18
    return p


def height_chart():
    w, h = 128, 724  # 1.2m (60cm ~ 180cm)
    p = Image.new("RGB", (w, h), (255, 255, 255))
    d = ImageDraw.Draw(p)
    for k in range(6):  # 칸마다 색
        c = [(255, 205, 120), (255, 170, 150), (190, 220, 140), (140, 210, 230), (180, 170, 235), (250, 190, 220)][k]
        d.rectangle((0, h - (k + 1) * h / 6, 40, h - k * h / 6), fill=c)
    for cm in range(60, 181):
        y = h - (cm - 60) / 120 * h
        long = cm % 10 == 0
        d.line((40, y, 40 + (36 if long else 16 if cm % 5 == 0 else 9), y), fill=INK, width=2 if long else 1)
        if long and 60 < cm < 180:
            d.text((80, y - 13), str(cm), font=font(20, True), fill=INK)
    d.rectangle((0, 0, w - 1, h - 1), outline=(200, 200, 200), width=2)
    return p


def sign(w, h, text, bg=NAVY, fg=(255, 255, 255), size=52):
    p = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(p)
    d.rectangle((3, 3, w - 4, h - 4), outline=SKY if bg == NAVY else fg, width=3)
    centered(d, (0, 0, w, h), text, font(size, True), fg)
    return p


def exit_sign():
    w, h = 256, 96
    p = Image.new("RGB", (w, h), (24, 150, 70))
    d = ImageDraw.Draw(p)
    # 달려 나가는 사람 (단순한 그림)
    x, y = 46, 48
    d.ellipse((x - 7, y - 36, x + 7, y - 22), fill="white")
    d.line((x, y - 20, x - 4, y + 4), fill="white", width=7)
    d.line((x - 4, y + 4, x - 20, y + 30), fill="white", width=6)
    d.line((x - 4, y + 4, x + 14, y + 26), fill="white", width=6)
    d.line((x - 2, y - 14, x + 18, y - 4), fill="white", width=5)
    d.line((x - 2, y - 14, x - 22, y - 2), fill="white", width=5)
    d.rectangle((72, 14, 84, 82), fill="white")  # 문
    centered(d, (88, 0, w, h), "비상구", font(46, True), (255, 255, 255))
    return p


def led_panel():
    w, h = 256, 96
    p = Image.new("RGB", (w, h), (250, 252, 255))
    d = ImageDraw.Draw(p)
    d.rectangle((0, 0, w - 1, h - 1), outline=(200, 205, 212), width=6)
    return p


def mirror():
    w, h = 256, 96
    p = Image.new("RGB", (w, h), (176, 192, 204))
    d = ImageDraw.Draw(p)
    for i in range(w + h):
        t = i / (w + h)
        c = int(170 + 40 * math.sin(t * math.pi))
        d.line((i, 0, i - h, h), fill=(c - 8, c + 4, c + 14))
    for x0, ww in ((60, 26), (110, 10)):  # 비스듬한 빛줄기
        d.polygon([(x0, 0), (x0 + ww, 0), (x0 + ww - h * .6, h), (x0 - h * .6, h)], fill=(232, 240, 246))
    return p.filter(ImageFilter.GaussianBlur(1.2))


def clock_face():
    s = 512
    p = Image.new("RGB", (s, s), (40, 44, 50))
    d = ImageDraw.Draw(p)
    d.ellipse((6, 6, s - 6, s - 6), fill=(250, 250, 248), outline=(60, 66, 72), width=10)
    c = s / 2
    for k in range(60):
        a = k / 60 * math.tau
        r0 = c - (58 if k % 5 == 0 else 40)
        d.line((c + math.sin(a) * r0, c - math.cos(a) * r0, c + math.sin(a) * (c - 28), c - math.cos(a) * (c - 28)),
               fill=INK, width=8 if k % 5 == 0 else 3)
    for k in range(1, 13):
        a = k / 12 * math.tau
        centered(d, (c + math.sin(a) * 160 - 40, c - math.cos(a) * 160 - 40, c + math.sin(a) * 160 + 40, c - math.cos(a) * 160 + 40),
                 str(k), font(54, True), INK)
    return p


SWATCHES = [(244, 246, 248), (196, 202, 208), (78, 86, 94), (52, 140, 150), (150, 205, 196), (160, 196, 230),
            (240, 180, 190), (246, 210, 110), (210, 70, 70), (90, 170, 100), (150, 110, 80), (24, 26, 30)]

BOXES = [("소화제", (52, 140, 150)), ("해열제", (210, 90, 90)), ("비타민", (240, 170, 60)),
         ("연고", (90, 150, 210)), ("밴드", (240, 150, 170)), ("소독약", (90, 170, 100))]


def box_label(text, c):
    w, h = 256, 128
    p = Image.new("RGB", (w, h), (248, 248, 246))
    d = ImageDraw.Draw(p)
    d.rectangle((0, 0, w, 34), fill=c)
    d.rectangle((0, h - 18, w, h), fill=c)
    centered(d, (0, 34, w, h - 18), text, font(40, True), INK)
    return p


def atlas():
    a = Image.new("RGB", (2048, 2048), (128, 128, 128))
    posters = [
        # 사진은 우리가 찍은 장면 (잠수정 창틀·안내판·만화 캐릭터가 안 보이는 것)
        ("입", "01_Mouth_옆.png", "이로 꼭꼭 씹고 침과 섞어요", ["침 속 효소가 녹말을 잘라요", "양치를 안 하면 충치가 생겨요"], None),
        ("위", "03_Stomach_옆.png", "위액이 음식을 죽처럼 녹여요", ["주름진 벽이 꿈틀꿈틀 섞어요", "센 위산도 점액이 막아 줘요"], (0, 0, .82, 1)),
        ("소장", str(ROOT / "Tools/play_captures/04_SmallIntestine_2_벽_혈관_적혈구.png"), "융털이 영양소를 흡수해요", ["작은 융털이 빽빽하게 나 있어요", "영양소는 핏줄로 들어가요"], None),
        ("대장", "05_LargeIntestine_중간_옆.png", "물을 흡수하고 정리해요", ["유익균이 살고 있어요", "식이섬유는 유익균의 먹이예요"], (.2, 0, .7, .5)),
    ]
    for i, (t, photo, cap, sub, crop) in enumerate(posters):
        a.paste(organ_poster(i + 1, t, photo, cap, sub, crop), (i * 512, 0))
    a.paste(brushing_poster(), (0, 724))
    a.paste(habits_poster(), (512, 724))
    a.paste(eye_chart(), (1024, 724))
    a.paste(height_chart(), (1408, 724))
    a.paste(sign(512, 96, "소화기내과 진료실"), (1536, 724))
    a.paste(sign(512, 96, "접수 · 수납"), (1536, 820))
    a.paste(sign(512, 96, "초소형 치료 잠수정 탑승구", size=40), (1536, 916))
    a.paste(exit_sign(), (1536, 1012))
    a.paste(led_panel(), (1792, 1012))
    a.paste(mirror(), (1536, 1108))
    a.paste(clock_face(), (0, 1448))
    for i, c in enumerate(SWATCHES):
        a.paste(Image.new("RGB", (128, 128), c), (512 + i * 128, 1448))
    for i, (t, c) in enumerate(BOXES):
        a.paste(box_label(t, c), (512 + i * 256, 1576))
    return a


def tiles(base, var, grout, gw, speck, seed, size=1024, n=4):
    rng = random.Random(seed)
    img = Image.new("RGB", (size, size), grout)
    d = ImageDraw.Draw(img)
    t = size // n
    for i in range(n):
        for j in range(n):
            k = rng.uniform(-var, var)
            c = tuple(max(0, min(255, int(v + k))) for v in base)
            d.rectangle((i * t + gw, j * t + gw, (i + 1) * t - gw - 1, (j + 1) * t - gw - 1), fill=c)
    px = img.load()
    for _ in range(size * size // 9):  # 잔 무늬
        x, y = rng.randrange(size), rng.randrange(size)
        if (x % t) < gw + 1 or (y % t) < gw + 1:
            continue
        s = rng.uniform(-speck, speck)
        r, g, b = px[x, y]
        px[x, y] = (int(max(0, min(255, r + s))), int(max(0, min(255, g + s))), int(max(0, min(255, b + s))))
    return img.filter(ImageFilter.GaussianBlur(.6))


def ceiling():
    img = tiles((172, 176, 182), 3, (196, 200, 206), 4, 22, 3)
    d = ImageDraw.Draw(img)
    rng = random.Random(5)
    for _ in range(9000):  # 흡음판 구멍
        x, y = rng.randrange(1024), rng.randrange(1024)
        if x % 256 < 8 or y % 256 < 8:
            continue
        d.point((x, y), fill=(138, 142, 148))
    return img


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    atlas().save(OUT / "진료실_그림.png")
    tiles((108, 121, 134), 5, (84, 94, 104), 3, 16, 1).save(OUT / "진료실_바닥.png")
    ceiling().save(OUT / "진료실_천장.png")
    print("ok", OUT)
