# Renders a scene several times with the real draw code and puts the results together (variants.sh, clip.sh).
#   sweep.py variants <name> <mode> [wave secs players] VAR=a,b,c [VAR2=x,y]  -> out/<name>.png  (labelled grid, every combination)
#   sweep.py clip     <name> <mode> [wave secs players] [VAR=from:to:step]     -> out/<name>.gif  (and out/<name>-strip.png: six frames)
# A value list splits on '|' when it has one (MOUSE=100,200|300,400), else on ','. WAVE, SECS and PLAYERS sweep the
# positional numbers. clip without VAR= records the live match: the game keeps running from the posed shot
# (FRAMES=30, CLIPSTEP=3 fixed steps a frame = 20 fps; CLIPHURT=1 lets players take damage).
# Env: CROP=x,y,w,h (a part of the 1280x720 screen), SCALE (default 1 with CROP, else 0.5 for clips and grids),
# COLS (grid columns), FPS (GIF speed, default 20), JOBS (parallel renders). Other env flags pass through to every shot.
import os, sys, subprocess, itertools, shutil
from concurrent.futures import ThreadPoolExecutor
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, '..', '..', 'Assets', 'Scripts')
F = ImageFont.truetype('/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf', 17)
JOBS = int(os.environ.get('JOBS', os.cpu_count() or 2))
POS = {'WAVE': 0, 'SECS': 1, 'PLAYERS': 2}

def die(msg): print(msg, file=sys.stderr); sys.exit(1)

def values(spec):
    if '|' in spec: return spec.split('|')
    if spec.count(':') == 2 and ',' not in spec:  # from:to:step
        a, b, s = (float(x) for x in spec.split(':'))
        out, i = [], 0
        while (s > 0 and a + i * s <= b + 1e-6) or (s < 0 and a + i * s >= b - 1e-6):
            out.append(('%.3f' % (a + i * s)).rstrip('0').rstrip('.')); i += 1
        return out
    return spec.split(',')

def compile_to(work):
    os.makedirs(work, exist_ok=True)
    srcs = ['Stubs.cs'] + sorted(f for f in os.listdir(HERE) if f.startswith('Shot') and f.endswith('.cs'))
    cmd = ['mcs', '-langversion:7.2', '-nowarn:414,169,649,219,618', '-out:' + os.path.join(work, 'shot.exe')] + srcs
    for d in ('Core', 'Game'):
        cmd += sorted(os.path.join(SRC, d, f) for f in os.listdir(os.path.join(SRC, d)) if f.endswith('.cs'))
    r = subprocess.run(cmd, cwd=HERE, capture_output=True, text=True)
    if r.returncode: die('compile failed:\n' + '\n'.join(l for l in r.stdout.splitlines() if 'error' in l)[:3000])

def shoot(work, tag, mode, nums, env):
    tmp = os.path.join(work, 'tmp-' + tag); os.makedirs(tmp, exist_ok=True)
    e = dict(os.environ, TMPDIR=tmp, **env)
    for _ in range(2):  # (once more on a failure: under load the preview match can miss its start)
        r = subprocess.run(['mono', 'shot.exe', tag] + nums + [mode], cwd=work, env=e, capture_output=True, text=True)
        if r.returncode == 0: break
    shutil.rmtree(tmp, ignore_errors=True)
    if r.returncode: die('render %s failed:\n%s' % (tag, (r.stderr or r.stdout)[-1500:]))

def raster(work, base):
    r = subprocess.run(['python3', os.path.join(HERE, 'shot.py'), base, base + '.png'], cwd=work, capture_output=True, text=True)
    if r.returncode: die('raster %s failed:\n%s' % (base, r.stderr[-1500:]))
    for ext in ('.tris', '.ui'):
        try: os.remove(os.path.join(work, base + ext))
        except OSError: pass
    return os.path.join(work, base + '.png')

def look(path, default_scale):
    im = Image.open(path).convert('RGB')
    crop = os.environ.get('CROP')
    if crop:
        x, y, w, h = (int(v) for v in crop.split(','))
        im = im.crop((x, y, x + w, y + h))
    s = float(os.environ.get('SCALE', '1' if crop else str(default_scale)))
    return im if s == 1 else im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)

def grid(cells, cols, out):
    tw, th = cells[0][1].size; lh = 26
    rows = (len(cells) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * (tw + 8) + 8, rows * (th + lh + 8) + 8), (24, 26, 34))
    d = ImageDraw.Draw(sheet)
    for i, (label, im) in enumerate(cells):
        x, y = 8 + (i % cols) * (tw + 8), 8 + (i // cols) * (th + lh + 8)
        d.text((x + 4, y + 4), label, font=F, fill=(220, 225, 240))
        sheet.paste(im, (x, y + lh))
    sheet.save(out)

def main():
    if len(sys.argv) < 4 or sys.argv[1] not in ('variants', 'clip'): die(open(__file__).read().split('\nimport')[0])
    kind, name, mode = sys.argv[1:4]
    nums, sweeps = ['6', '12.2', '1'], []
    rest = sys.argv[4:]
    i = 0
    for a in rest:
        if '=' in a: k, v = a.split('=', 1); sweeps.append((k, values(v)))
        else: nums[i] = a; i += 1
    if kind == 'variants' and not sweeps: die('variants needs at least one VAR=a,b,c')
    out = os.path.join(HERE, 'out'); work = os.path.join(out, 'sweep-' + name)
    shutil.rmtree(work, ignore_errors=True)
    compile_to(work)
    combos = list(itertools.product(*[[(k, v) for v in vs] for k, vs in sweeps])) if sweeps else []
    def job(ci, combo):
        n, env = list(nums), {}
        for k, v in combo:
            if k in POS: n[POS[k]] = v
            else: env[k] = v
        tag = 'v%03d' % ci
        shoot(work, tag, mode, n, env)
        return raster(work, tag)
    if combos:
        print('%d renders...' % len(combos), flush=True)
        with ThreadPoolExecutor(JOBS) as ex: pngs = list(ex.map(lambda c: job(*c), enumerate(combos)))
        labels = ['  '.join('%s=%s' % kv for kv in c) for c in combos]
    else:  # a live clip: one run that keeps the match going and writes every frame
        frames = int(os.environ.get('FRAMES', '30'))
        print('%d frames...' % frames, flush=True)
        shoot(work, 'live', mode, nums, {'CLIP': str(frames + 1)})  # (the posed shot is drawn a little differently: the clip starts after it)
        bases = ['live.f%03d' % k for k in range(1, frames + 1)]
        with ThreadPoolExecutor(JOBS) as ex: pngs = list(ex.map(lambda b: raster(work, b), bases))
        labels = ['frame %d' % k for k in range(frames)]
    os.makedirs(out, exist_ok=True)
    if kind == 'variants':
        ims = [look(p, 0.5) for p in pngs]
        cols = int(os.environ.get('COLS', '0')) or (2 if len(ims) in (2, 4) else min(len(ims), 3 if ims[0].width > 500 else 4))
        dest = os.path.join(out, name + '.png'); grid(list(zip(labels, ims)), cols, dest)
    else:
        ims = [look(p, 0.5) for p in pngs]
        dest = os.path.join(out, name + '.gif')
        ims[0].save(dest, save_all=True, append_images=ims[1:], duration=int(1000 / float(os.environ.get('FPS', '20'))), loop=0, optimize=True)
        pick = [ims[round(k * (len(ims) - 1) / 5)] for k in range(6)] if len(ims) >= 6 else ims  # six evenly spaced frames
        grid([('frame %d' % (round(k * (len(ims) - 1) / 5) + 1), p) for k, p in enumerate(pick)], 3, os.path.join(out, name + '-strip.png'))
    shutil.rmtree(work, ignore_errors=True)
    print(os.path.relpath(dest, os.path.join(HERE, '..', '..')))

main()
