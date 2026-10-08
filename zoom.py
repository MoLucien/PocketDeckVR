import sys
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
# regions: name,x,y,w,h,zoom
regions = []
for spec in sys.argv[3:]:
    name, x, y, w, h, z = spec.split(',')
    regions.append((name, int(x), int(y), int(w), int(h), int(z)))

im = Image.open(src).convert('RGB')
tiles = []
for name, x, y, w, h, z in regions:
    crop = im.crop((x, y, x + w, y + h)).resize((w * z, h * z), Image.NEAREST)
    tiles.append((name, crop))

gap = 12
width = max(t.width for _, t in tiles) + gap * 2
height = sum(t.height + gap + 18 for _, t in tiles) + gap
out = Image.new('RGB', (width, height), (10, 10, 12))
from PIL import ImageDraw
d = ImageDraw.Draw(out)
yy = gap
for name, t in tiles:
    d.text((gap, yy), name, fill=(255, 210, 120))
    yy += 18
    out.paste(t, (gap, yy))
    yy += t.height + gap
out.save(dst)
print('saved', dst, out.size)
