import sys, random
from PIL import Image, ImageFilter
# One tile is 0.5 m of belt (U) by 0.5 m of width (V). Fine, low-contrast cross texture so motion reads
# without looking like slats; a faint splice line once per tile helps too.
size, ribs = 256, 32
random.seed(7)
img = Image.new("L", (size, size))
period = size // ribs
for x in range(size):
    rib = 4 if (x % period) < 2 else 0
    splice = 9 if x in (0, 1) else 0
    for y in range(size):
        img.putpixel((x, y), int(26 + rib + splice + random.uniform(-3, 3)))
img = img.filter(ImageFilter.GaussianBlur(0.5)).convert("RGB")
img.save(sys.argv[1])
