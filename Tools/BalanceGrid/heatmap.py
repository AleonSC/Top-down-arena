# Draws the balance grid (grid.sh): one row per role, class and weapon, one column per boss. Each cell shows the win
# rate, the average fight time and the tested seat's boss DPS; its colour is METRIC (wins: red 0% -> green 100%; dps or
# time: against the role's median for that boss, red = weak, green = strong). Prints the outliers.
#   heatmap.py <grid.csv> <out.png>
import csv, os, sys, statistics
from collections import defaultdict
from PIL import Image, ImageDraw, ImageFont

FB = ImageFont.truetype('/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf', 15)
FR = ImageFont.truetype('/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf', 13)
METRIC = os.environ.get('METRIC', 'wins')
ROLE_ORDER = {'Tank': 0, 'Dps': 1, 'Healer': 2}

rows = list(csv.DictReader(open(sys.argv[1])))
if not rows: sys.exit('no fights in ' + sys.argv[1])
cells = defaultdict(list)
for r in rows: cells[(r['role'], r['class'], r['weapon'], int(r['wave']))].append(r)
waves = sorted({k[3] for k in cells})
boss = {int(r['wave']): r['boss'] for r in rows}
loadouts = sorted({k[:3] for k in cells}, key=lambda l: (ROLE_ORDER.get(l[0], 9), l[1]))

def stat(rs):
    return {'wins': sum(r['outcome'] == 'Won' for r in rs) / len(rs), 'time': statistics.mean(float(r['time']) for r in rs),
            'dps': statistics.mean(float(r['dps']) for r in rs), 'downs': statistics.mean(int(r['downs']) for r in rs), 'n': len(rs)}
S = {k: stat(v) for k, v in cells.items()}
med = {}  # the role's median DPS and win time for each boss
for role in {l[0] for l in loadouts}:
    for w in waves:
        ss = [S[k] for k in S if k[0] == role and k[3] == w]
        med[(role, w)] = (statistics.median(s['dps'] for s in ss) or 1, statistics.median(s['time'] for s in ss) or 1)

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
RED, YEL, GRN = (150, 45, 50), (150, 125, 40), (40, 125, 70)
def ramp(t):  # 0 = bad, 1 = good
    t = max(0.0, min(1.0, t))
    return lerp(RED, YEL, t * 2) if t < 0.5 else lerp(YEL, GRN, t * 2 - 1)
def colour(k):
    s = S[k]; md, mt = med[(k[0], k[3])]
    if METRIC == 'dps': return ramp(0.5 + (s['dps'] / md - 1) * 1.25)        # +-40% from the median = full green / red
    if METRIC == 'time': return ramp(0.5 - (s['time'] / mt - 1) * 1.25)      # faster than the median = green
    return ramp(s['wins'])

LW, CW, CH, HH = 330, 150, 40, 54
W, H = LW + CW * len(waves) + 16, HH + CH * len(loadouts) + 60
img = Image.new('RGB', (W, H), (20, 22, 30)); d = ImageDraw.Draw(img)
d.text((12, 10), 'Balance grid  ·  colour: %s  ·  %d fights  ·  cell: wins / avg fight / boss DPS' % (METRIC, len(rows)), font=FB, fill=(230, 232, 245))
for j, w in enumerate(waves):
    x = LW + j * CW
    d.text((x + 6, 32), 'W%d %s' % (w, boss[w])[:20], font=FR, fill=(200, 205, 225))
prev = None
for i, l in enumerate(loadouts):
    y = HH + i * CH
    if prev and prev[0] != l[0]: d.line((8, y, W - 8, y), fill=(120, 130, 160), width=2)
    prev = l
    d.text((12, y + 11), '%s  %s  ·  %s' % (l[0].upper(), l[1], l[2]), font=FB if i % 2 == 0 else FB, fill=(220, 225, 240))
    for j, w in enumerate(waves):
        k = l + (w,)
        x = LW + j * CW
        if k not in S: continue
        s = S[k]
        d.rectangle((x + 2, y + 2, x + CW - 2, y + CH - 2), fill=colour(k))
        d.text((x + 8, y + 4), '%d%% won' % round(s['wins'] * 100), font=FB, fill=(255, 255, 255))
        d.text((x + 8, y + 21), '%.0fs · %.0f dps' % (s['time'], s['dps']), font=FR, fill=(235, 238, 245))
d.text((12, H - 40), 'Good bots in the Boss Lab, typical upgrades for the wave.\nColours compare each role with its own median.', font=FR, fill=(150, 155, 175))
img.save(sys.argv[2])
print(os.path.relpath(sys.argv[2]))

# Outliers: DPS 30% off the role's median for that boss, or a win rate far from the role's.
out = []
for k, s in S.items():
    md, _ = med[(k[0], k[3])]
    r = s['dps'] / md
    if r >= 1.3 or r <= 0.7: out.append((abs(r - 1), '%s %s %s vs W%d %s: DPS %.0f (%+.0f%% vs %s median)' % (k[0], k[1], k[2], k[3], boss[k[3]], s['dps'], (r - 1) * 100, k[0])))
for _, line in sorted(out, reverse=True)[:12]: print('  ' + line)
if not out: print('  no outliers (every cell within 30% of its role median DPS)')
