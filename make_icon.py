import os
from PIL import Image, ImageDraw, ImageFilter

OUT = r'C:\Users\Mo\WorkBuddy\2026-10-08-17-50-32\src\VRPhoneScreenOverlay\app.ico'
PREVIEW = r'C:\Users\Mo\WorkBuddy\2026-10-08-17-50-32\ui3-icon-preview.png'
S = 1024

def rounded_mask(size, radius):
    m = Image.new('L', (size, size), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return m

def vertical_gradient(size, top, bottom):
    img = Image.new('RGB', (size, size))
    d = ImageDraw.Draw(img)
    for y in range(size):
        t = y / max(1, size - 1)
        d.line([(0, y), (size, y)], fill=(
            int(top[0] + (bottom[0] - top[0]) * t),
            int(top[1] + (bottom[1] - top[1]) * t),
            int(top[2] + (bottom[2] - top[2]) * t)))
    return img

def build():
    base = vertical_gradient(S, (255, 106, 138), (214, 40, 82)).convert('RGBA')
    # 斜向高光
    glow = Image.new('L', (S, S), 0)
    gd = ImageDraw.Draw(glow)
    gd.ellipse([-S * 0.35, -S * 0.75, S * 0.95, S * 0.55], fill=90)
    glow = glow.filter(ImageFilter.GaussianBlur(S * 0.06))
    base = Image.composite(Image.new('RGBA', (S, S), (255, 255, 255, 255)), base, glow.point(lambda v: int(v * 0.55)))

    layer = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    # 悬浮的手机（白色圆角矩形 + 屏幕内嵌）
    pw, ph = int(S * 0.38), int(S * 0.48)
    px, py = (S - pw) // 2, int(S * 0.10)
    d.rounded_rectangle([px, py, px + pw, py + ph], radius=int(S * 0.075), fill=(255, 255, 255, 255))
    inset = int(S * 0.035)
    d.rounded_rectangle([px + inset, py + inset * 2, px + pw - inset, py + ph - inset * 2], radius=int(S * 0.045), fill=(226, 44, 88, 255))
    # 听筒
    d.rounded_rectangle([S // 2 - int(S * 0.05), py + int(S * 0.045), S // 2 + int(S * 0.05), py + int(S * 0.065)], radius=int(S * 0.012), fill=(255, 255, 255, 235))

    # 悬浮阴影（体现“悬浮”）
    shadow = Image.new('L', (S, S), 0)
    sd = ImageDraw.Draw(shadow)
    sd.ellipse([px - int(S * 0.04), py + ph + int(S * 0.02), px + pw + int(S * 0.04), py + ph + int(S * 0.075)], fill=130)
    shadow = shadow.filter(ImageFilter.GaussianBlur(S * 0.022))
    layer = Image.alpha_composite(Image.merge('RGBA', (Image.new('L', (S, S), 20),) * 3 + (shadow,)), layer)

    d = ImageDraw.Draw(layer)  # 合成后重新取画笔，否则头显画在被丢弃的图层上

    # VR 头显剪影（镜框 + 双镜片）
    vy = py + ph + int(S * 0.085)
    vw, vh = int(S * 0.62), int(S * 0.15)
    vx = (S - vw) // 2
    d.rounded_rectangle([vx, vy, vx + vw, vy + vh], radius=int(vh * 0.5), outline=(255, 255, 255, 255), width=int(S * 0.030))
    r = int(vh * 0.26)
    cy = vy + vh // 2
    for cx in (vx + int(vw * 0.30), vx + int(vw * 0.70)):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 255, 255, 255))

    out = Image.alpha_composite(base, layer)
    out.putalpha(rounded_mask(S, int(S * 0.22)))
    return out

icon = build()
sizes = [256, 128, 64, 48, 32, 24, 16]
icon.save(OUT, format='ICO', sizes=[(s, s) for s in sizes])
print('saved', OUT, os.path.getsize(OUT), 'bytes')

# 预览图：各尺寸 + 深色底
pad = 18
sheet = Image.new('RGB', (pad + sum(s + pad for s in sizes), 256 + pad * 2 + 20), (12, 12, 15))
x = pad
for s in sizes:
    sheet.paste(icon.resize((s, s), Image.LANCZOS), (x, pad), icon.resize((s, s), Image.LANCZOS))
    x += s + pad
sheet.save(PREVIEW)
print('saved', PREVIEW, sheet.size)
