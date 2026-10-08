import os, shutil
from PIL import Image, ImageDraw, ImageFont

d = r'C:\Users\Mo\WorkBuddy\2026-10-08-17-50-32'
prev = os.path.join(d, 'ui-preview')
v3 = os.path.join(d, 'ui3-r5')
os.makedirs(prev, exist_ok=True)

# v3 页面预览
copies = {
    os.path.join(v3, 'home-live.png'): '主页-运行中-v3.png',
    os.path.join(v3, 'home-idle.png'): '主页-空闲-v3.png',
    os.path.join(v3, 'home-error.png'): '主页-错误-v3.png',
    os.path.join(v3, 'settings-dirty.png'): '设置-v3.png',
    os.path.join(v3, 'about.png'): '关于-v3.png',
    os.path.join(v3, 'narrow', 'home-live.png'): '主页-单列-v3.png',
    os.path.join(d, 'ui3-live3.png'): '实机截图-PocketDeck.png',
    os.path.join(d, 'ui3-icon-preview.png'): '新图标.png',
}
for src, dst in copies.items():
    if os.path.exists(src):
        shutil.copyfile(src, os.path.join(prev, dst))
print('copied', len(copies), 'preview images')

font_path = r'C:\Windows\Fonts\msyh.ttc'
head = ImageFont.truetype(font_path, 27)
lab = ImageFont.truetype(font_path, 21)
old = Image.open(os.path.join(d, 'ui-r6', 'home-running.png')).convert('RGB')
new = Image.open(os.path.join(v3, 'home-live.png')).convert('RGB')
w = max(old.size[0], new.size[0]); h = max(old.size[1], new.size[1])
pad, headh, gap = 18, 72, 22
W = pad * 2 + w * 2 + gap
H = pad + headh + h + pad
canvas = Image.new('RGB', (W, H), (10, 10, 13))
dr = ImageDraw.Draw(canvas)
dr.text((pad, pad - 4), 'PocketDeck VR · UI 从零重构：左＝旧界面（v2），右＝全新界面（v3）', font=head, fill=(246, 246, 248))
y = pad + headh
for i, (img, title) in enumerate(((old, '旧 · 卡片式仪表盘（绝对坐标布局）'), (new, '新 · 动作优先 + 信号管线 + 标签导航（流式布局/可缩放）'))):
    x = pad + i * (w + gap)
    canvas.paste(img, (x, y))
    dr.text((x + 4, y - 30), title, font=lab, fill=(160, 160, 172) if i == 0 else (255, 122, 148))
out = os.path.join(prev, 'UI重构-v2对比v3.png')
canvas.save(out)
print('saved', out, canvas.size)
