# Contact sheet for uishots.sh. uisheet.py <dir>                  -> <dir>/sheet.png (every screen, labelled)
#                                uisheet.py <dir> <ref dir> <ref> -> <dir>/compare.png (changed screens: ref | now)
import sys, os
from PIL import Image, ImageDraw, ImageFont, ImageChops
F = ImageFont.truetype('/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf', 18)
def shots(d): return sorted(f[:-4] for f in os.listdir(d) if f.endswith('.png') and f not in ('sheet.png', 'compare.png'))
def thumb(path, w=640): im = Image.open(path).convert('RGB'); return im.resize((w, w * im.height // im.width), Image.LANCZOS)
def grid(cells, cols, out):  # cells: (label, image or None)
    tw, th, lh = 640, 360, 28
    rows = (len(cells) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * (tw + 8) + 8, rows * (th + lh + 8) + 8), (24, 26, 34))
    d = ImageDraw.Draw(sheet)
    for i, (label, im) in enumerate(cells):
        x, y = 8 + (i % cols) * (tw + 8), 8 + (i // cols) * (th + lh + 8)
        d.text((x + 4, y + 4), label, font=F, fill=(220, 225, 240))
        if im is not None: sheet.paste(im, (x, y + lh))
        else: d.text((x + 20, y + lh + 160), "(missing)", font=F, fill=(255, 120, 120))
    sheet.save(out); print(out)
now = sys.argv[1]
if len(sys.argv) == 2:
    grid([(n, thumb(os.path.join(now, n + '.png'))) for n in shots(now)], 3, os.path.join(now, 'sheet.png'))
else:
    ref, refname = sys.argv[2], sys.argv[3]
    names = sorted(set(shots(now)) | set(shots(ref)))
    here = os.path.dirname(os.path.abspath(__file__))
    noisy = set(l.split()[0] for l in open(os.path.join(here, 'ui-screens.txt')) if 'NOISY=1' in l and not l.lstrip().startswith('#'))
    changed, same, rough = [], [], []
    for n in names:
        if n in noisy: rough.append(n); continue
        a, b = os.path.join(ref, n + '.png'), os.path.join(now, n + '.png')
        if os.path.exists(a) and os.path.exists(b):
            ia, ib = Image.open(a).convert('RGB'), Image.open(b).convert('RGB')
            if ia.size == ib.size and ImageChops.difference(ia, ib).getbbox() is None: same.append(n); continue
        changed.append(n)
    print("unchanged: " + (", ".join(same) or "none"))
    print("changed:   " + (", ".join(changed) or "none"))
    if rough: print("check by eye (differ run to run): " + ", ".join(rough))
    changed += rough
    if changed:
        cells = []
        for n in changed:
            for d, tag in ((ref, refname), (now, 'now')):
                p = os.path.join(d, n + '.png')
                cells.append((n + "  (" + tag + (", check by eye" if n in rough else "") + ")", thumb(p) if os.path.exists(p) else None))
        grid(cells, 2, os.path.join(now, 'compare.png'))
