import sys, random, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter
out = sys.argv[1]
BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
ink = (22, 22, 24, 255)

def label(name, seed, qr):
    random.seed(seed)
    W, H = 400, 600
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W - 1, H - 1], fill=(247, 245, 239, 255))
    d.rectangle([14, 14, W - 15, H - 15], outline=ink, width=3)
    y = 34
    for w in (220, 300, 180, 260):
        d.rectangle([34, y, 34 + w, y + 14], fill=(60, 60, 64, 255)); y += 28
    d.line([14, y + 6, W - 15, y + 6], fill=ink, width=3)
    y += 26
    for w in (140, 240, 200):
        d.rectangle([34, y, 34 + w, y + 22], fill=ink); y += 40
    d.line([14, y, W - 15, y], fill=ink, width=3)
    x, y0, y1 = 34, y + 24, y + 170
    while x < W - 40:
        bw = random.choice([3, 3, 5, 8]); d.rectangle([x, y0, x + bw - 1, y1], fill=ink); x += bw + random.choice([3, 4, 6])
    if qr:
        q0, s = W - 120, 7
        for i in range(12):
            for j in range(12):
                if random.random() < 0.5: d.rectangle([q0 + i * s, 40 + j * s, q0 + i * s + s - 1, 40 + j * s + s - 1], fill=ink)
    im.save(os.path.join(out, name))

def stamp(name, text, color, size, w, h, box=True):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    f = ImageFont.truetype(BOLD, size)
    if box: d.rounded_rectangle([6, 6, w - 7, h - 7], radius=10, outline=color, width=8)
    d.text((w / 2, h / 2), text, font=f, fill=color, anchor="mm")
    # Worn ink: knock out speckles so it reads as printed on cardboard.
    random.seed(len(text))
    px = im.load()
    for _ in range(w * h // 9):
        x, y = random.randrange(w), random.randrange(h)
        r, g, b, a = px[x, y]; px[x, y] = (r, g, b, int(a * random.uniform(0.2, 0.7)))
    im.filter(ImageFilter.GaussianBlur(0.6)).save(os.path.join(out, name))

def arrows(name):
    W, H = 300, 260
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    c = (105, 64, 34, 235)
    for ox in (90, 210):
        d.polygon([(ox, 20), (ox - 46, 100), (ox + 46, 100)], fill=c)
        d.rectangle([ox - 16, 100, ox + 16, 200], fill=c)
    d.rectangle([30, 220, 270, 236], fill=c)
    im.filter(ImageFilter.GaussianBlur(0.8)).save(os.path.join(out, name))

label("label_a.png", 11, False)
label("label_b.png", 23, True)
stamp("fragile.png", "FRAGILE", (196, 32, 32, 230), 92, 520, 160)
stamp("lot.png", "LOT 0742", (40, 40, 44, 215), 70, 460, 120, box=False)
arrows("arrows.png")
print(sorted(os.listdir(out)))
