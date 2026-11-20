import sys
from PIL import Image, ImageDraw
# Face of a 0.56 x 0.40 m sign. Header band is the top 0.11 m; text is drawn live in Unity.
W, H = 1024, 732
SS = 2
img = Image.new("RGB", (W * SS, H * SS), (242, 194, 0))
d = ImageDraw.Draw(img)
s = lambda v: int(v * SS)
black = (24, 24, 26)
band = int(H * 0.11 / 0.40)
d.rectangle([0, 0, s(W), s(band)], fill=black)
m = 14
d.rounded_rectangle([s(m), s(band + m), s(W - m), s(H - m)], radius=s(18), outline=black, width=s(6))
# ANSI safety alert symbol: yellow triangle, black exclamation mark, left of the signal word.
cx, cy, r = 150, band / 2 + 6, 70
tri = [(s(cx), s(cy - r)), (s(cx - r * 1.1), s(cy + r * 0.82)), (s(cx + r * 1.1), s(cy + r * 0.82))]
d.polygon(tri, fill=(242, 194, 0))
d.rounded_rectangle([s(cx - 9), s(cy - 34), s(cx + 9), s(cy + 22)], radius=s(6), fill=black)
d.ellipse([s(cx - 10), s(cy + 32), s(cx + 10), s(cy + 52)], fill=black)
img.resize((W, H), Image.LANCZOS).save(sys.argv[1])
