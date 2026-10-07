"""アプリアイコン (src/VscodeSelector/app.ico) を生成する。

モチーフ: VS Code ブルーの角丸地に、ウィンドウ一覧を表すタイル 2x2。
選ばれた 1 枚は黄色で顔付き、そこへカーソルが乗っている。
使い方: python tools/make_icon.py
"""
from pathlib import Path

from PIL import Image, ImageDraw

S = 1024  # 下描き解像度。各サイズへ縮小する。
OUT = Path(__file__).resolve().parent.parent / "src" / "VscodeSelector" / "app.ico"

BLUE_TOP = (64, 170, 255)
BLUE_BOTTOM = (0, 110, 200)
TILE = (255, 255, 255)
TILE_BAR = (190, 225, 255)
PICK = (255, 214, 64)
PICK_BAR = (255, 170, 40)
INK = (40, 50, 80)
CHEEK = (255, 140, 140)


def rounded(d, box, r, fill, outline=None, width=0):
    d.rounded_rectangle(box, r, fill=fill, outline=outline, width=width)


def draw():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    # 背景: 縦グラデーションの角丸正方形
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        gd.line([(0, y), (S, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(BLUE_TOP, BLUE_BOTTOM)))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((32, 32, S - 32, S - 32), 220, fill=255)
    img.paste(grad, (0, 0), mask)

    d = ImageDraw.Draw(img)
    pad, gap = 170, 60
    cell = (S - 2 * pad - gap) // 2
    for row in range(2):
        for col in range(2):
            x0 = pad + col * (cell + gap)
            y0 = pad + row * (cell + gap)
            picked = (row, col) == (1, 0)
            body, bar = (PICK, PICK_BAR) if picked else (TILE, TILE_BAR)
            rounded(d, (x0, y0, x0 + cell, y0 + cell), 70, body)
            # タイトルバー風の帯 (上端だけ)
            d.rounded_rectangle((x0, y0, x0 + cell, y0 + 90), 70, fill=bar, corners=(True, True, False, False))
            if picked:
                cx, cy = x0 + cell // 2, y0 + 90 + (cell - 90) // 2
                # 目
                for ex in (cx - 70, cx + 70):
                    d.ellipse((ex - 22, cy - 50, ex + 22, cy + 6), fill=INK)
                # ほっぺ
                for ex in (cx - 115, cx + 115):
                    d.ellipse((ex - 30, cy + 10, ex + 30, cy + 44), fill=CHEEK)
                # にっこり口
                d.arc((cx - 45, cy - 10, cx + 45, cy + 60), 20, 160, fill=INK, width=18)
            else:
                # コード行
                for i, w in enumerate((0.62, 0.42, 0.52)):
                    ly = y0 + 140 + i * 55
                    d.rounded_rectangle((x0 + 45, ly, x0 + 45 + int((cell - 90) * w), ly + 24), 12, fill=TILE_BAR)

    # カーソル (選ばれたタイルの右下に重ねる)
    tip = (pad + cell - 60, pad + cell + gap + cell - 80)
    pts = [(0, 0), (0, 300), (75, 230), (125, 340), (175, 318), (126, 210), (225, 210)]
    k = 0.62
    poly = [(tip[0] + x * k, tip[1] + y * k) for x, y in pts]
    d.polygon(poly, fill=(255, 255, 255), outline=INK, width=12)
    return img


def main():
    big = draw()
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    big.resize((256, 256), Image.LANCZOS).save(OUT, sizes=[(s, s) for s in sizes])
    print(OUT)


if __name__ == "__main__":
    main()
