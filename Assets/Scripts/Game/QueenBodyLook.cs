using UnityEngine;

namespace Arena
{
    /// The Hive Queen's body, the Brood Mother (`Docs/Mockups/hive-queen-rework.html`, direction B): a small head and thorax
    /// dragging a huge see-through egg sac full of eggs, with stubby wings and a gold tiara. She faces her nearest player
    /// (or her nectar spray). Her phases show on the sac: Swarm plain, Honeycomb wrapped in a wax lattice, Frenzy glowing
    /// red with the eggs boiling and her wings torn (embers rising), Last Stand half-empty, cracked and leaking honey.
    /// Drawn with the Hive's helpers (HiveLook.cs) in the mockup's units; the rings round her stay in QueenLook.cs.
    public partial class ArenaApp
    {
        static readonly Rgba QAmber = Hex(0xffc533), QFrenzy = Hex(0xff6e45), QRed = Hex(0xff5a3c), QHoney = Hex(0xffa21a),
            QGold = Hex(0xffd75a), QOrange = Hex(0xff7a1f), QDark = Hex(0x1a1206);

        static float QRnd(float n)
        {
            float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        /// Which way she looks: along her nectar spray while it sweeps, else at the nearest standing player (down with none).
        float QueenFacing(Enemy e, V2 pos)
        {
            var q = e.Queen;
            if (q != null && q.TransT <= 0f && q.Turn == QueenTurn.Spray)
                return (20f + 140f * q.Aux) * MathX.Pi / 180f;
            if (q != null && Game.QueenAir(q) > 0f && (q.Shadow - pos).Length > 4f)  // diving: at where she'll land
                return Mathf.Atan2(q.Shadow.Y - pos.Y, q.Shadow.X - pos.X);
            float best = 1e9f;
            V2 to = new V2(0f, 1f);
            foreach (var p in game.Players.Values)
                if (!p.Downed && p.Disp.DistanceTo(pos) < best)
                {
                    best = p.Disp.DistanceTo(pos);
                    to = p.Disp - pos;
                }
            return Mathf.Atan2(to.Y, to.X);
        }

        void DrawQueenBody(Enemy e, V2 pos, float r, Rgba col0, float t)
        {
            var q = e.Queen;
            int stage = q == null ? 0 : (int)q.Stage;
            bool frz = stage >= (int)QueenStage.Frenzy, last = stage == (int)QueenStage.LastStand;
            float air = q != null ? Game.QueenAir(q) : 0f, swell = Game.QueenSwell(q);
            float squash = QueenSquash(q);
            bool laying = swell > 0f && q != null && q.TransT <= 0f && q.Turn == QueenTurn.Eggs;  // squeezing her eggs out
            float a = col0.A, hit = (e.Flash > 0f || JuiceFlash(e)) ? HitFlashMix(e) : 0f;
            hiveCrowd = game.Enemies.Count > HiveCrowd;
            float ang = QueenFacing(e, pos);
            V2 F = V2.FromAngle(ang);
            float K = r * 1.45f / 50f * (1f + 0.18f * air);
            var f = new HiveFrame { O = pos + F * (29f * K), F = F, S = F.Orthogonal(), K = K };  // her middle (the sac's front) on pos
            float lw = Mathf.Max(1.2f, 2f * K), thin = Mathf.Max(1f, 1.2f * K), gl = K * 8f;
            Rgba col = (frz ? QFrenzy : QAmber).Mix(Rgba.White, hit).WithAlpha(a);
            Rgba wax = HiveWax.WithAlpha(a), white = HiveWhite.WithAlpha(a);
            const float tr = 10f, hr = 7f;
            // the sac: pulsing, swelling before she lays (throbbing faster as it fills), tucked in and narrowed in a dive
            float pulse = 1f + (0.045f + 0.035f * swell) * Mathf.Sin(t * (stage == (int)QueenStage.Frenzy ? 7f : 2.6f + 9f * swell * swell));
            // (landing from a dive: squashed flat and wide, wobbling back)
            float rx = 34f * pulse * (1f + 0.22f * swell) * (1f - 0.2f * air) * (1f - 0.2f * squash), ry = (last ? 18f : 25f) * pulse * (1f + 0.3f * swell) * (1f - 0.22f * air) * (1f + 0.32f * squash);
            float ax = -(tr * 0.55f + rx - 2f);
            float fl = Mathf.Sin(t * (air > 0f ? 80f : 52f)), legIn = 1f - 0.65f * air + 0.45f * Mathf.Max(0f, squash);  // (splayed as she lands)

            // legs
            for (int sd = -1; sd <= 1; sd += 2)
                for (int k = 0; k < 3; k++)
                {
                    float lx = Mathf.Lerp(-tr * 0.6f, tr * 0.7f, k / 2f), sw = Mathf.Sin(t * 5f + k + sd) * 1.5f;
                    V2 p1 = f.P(lx + (k - 1) * 4f * legIn + 3f * air, sd * (tr + 5f * legIn));  // in a dive: folded in under her, forward
                    HiveStroke(f.P(lx, sd * tr * 0.7f), p1, thin, col, K * 3f);
                    HiveStroke(p1, f.P(lx + (k - 1) * 7f * legIn + sw * legIn + 6f * air, sd * (tr + 9f * legIn)), thin, col, K * 3f);
                }

            // the sac: a see-through fill (red in the Frenzy), its outline and segment arcs
            HiveEllipse(f, ax, 0f, rx, ry, 0f, (frz ? QRed : HiveWax).WithAlpha(a), frz ? 0.32f : 0.16f, 0f, 0f, 24);
            HiveEllipse(f, ax, 0f, rx, ry, 0f, col.Mix(HiveWhite.WithAlpha(a), 0.35f * swell), 0f, lw * (1f + 0.4f * swell), gl * (0.7f + 0.8f * swell), 28);
            for (int i = 1; i <= 4; i++)
            {
                float xx = ax + rx - i * rx * 0.42f, h = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((xx - ax) / rx, 2f))) * 0.95f;
                V2 prev = f.P(xx, -h);
                for (int s = 1; s <= 6; s++)
                {
                    float u = s / 6f, x = (1f - u) * (1f - u) * xx + 2f * u * (1f - u) * (xx - 6f) + u * u * xx, y = -h + 2f * h * u;
                    V2 nx = f.P(x, y);
                    HiveStroke(prev, nx, thin, col, K * 3f);
                    prev = nx;
                }
            }
            // the eggs inside, each with a larva showing; they boil in the Frenzy, two left in the Last Stand
            // (swelling: packed to the rim and glowing; just laid: three left, refilling)
            int eggs = last ? 2 : q != null && q.TransT <= 0f && q.Turn == QueenTurn.Eggs && swell < 0.5f ? 3 : 7;
            for (int i = 0; i < eggs; i++)
            {
                float ea = i * 2.4f + 0.4f, er = i == 0 ? 0f : (0.35f + 0.35f * QRnd(i)) * (1f + 0.25f * swell);
                float ex = ax + Mathf.Cos(ea) * rx * er * 0.8f, ey = Mathf.Sin(ea) * ry * er * 0.75f;
                float jit = stage == (int)QueenStage.Frenzy ? Mathf.Sin(t * 22f + i) * 1.4f : Mathf.Sin(t * 2f + i) * 0.6f;
                HiveEllipse(f, ex + jit, ey, 5.2f * (1f + 0.2f * swell), 3.6f * (1f + 0.2f * swell), ea, wax, 0.5f + 0.3f * swell, Mathf.Max(1f, K), K * (frz ? 10f : 5f + 6f * swell), 10);
                Circle(f.P(ex + jit + 1f, ey), Mathf.Max(1f, 1.5f * K), (frz ? QRed : QHoney).WithAlpha(a), 6);
            }
            if (stage == (int)QueenStage.Honeycomb)  // wrapped in a wax lattice
            {
                var lat = HiveWax.WithAlpha(a * 0.55f);
                for (int gx = -4; gx <= 4; gx++)
                    for (int gy = -3; gy <= 3; gy++)
                    {
                        float cx = ax + gx * 10.5f, cy = gy * 12f + ((gx & 1) == 1 ? 6f : 0f);
                        float u = (cx - ax) / (rx - 6f), v = cy / (ry - 6f);
                        if (u * u + v * v <= 1f)
                            HiveHexRing(f, cx, cy, 6.6f, 0f, lat, 0f, Mathf.Max(1f, K), K * 3f);
                    }
            }
            if (last)  // cracked, and leaking honey behind her
            {
                var dk = QDark.WithAlpha(a);
                HiveStroke(f.P(ax + 10f, -ry * 0.6f), f.P(ax - 4f, -ry * 0.1f), Mathf.Max(1f, 1.4f * K), dk, 0f);
                HiveStroke(f.P(ax - 4f, -ry * 0.1f), f.P(ax - 14f, ry * 0.3f), Mathf.Max(1f, 1.2f * K), dk, 0f);
                for (int i = 0; i < 6; i++)
                {
                    float p = (t * 0.5f + i / 6f) % 1f;
                    Circle(f.P(ax - rx + 4f - p * 26f - i * 2f, 4f + Mathf.Sin(i * 2.1f) * 5f + p * 4f), (2.4f - 1.4f * p) * K, QHoney.WithAlpha(a * (1f - p)), 8);
                }
            }
            queenTip = f.P(ax - rx - 8f, 0f);
            // the ovipositor (flaring as she lays, with a spurt of honey behind)
            if (laying)
            {
                Circle(f.P(ax - rx - 6f, 0f), (6f + 6f * swell) * K, QHoney.WithAlpha(a * 0.35f * swell), 12);
                for (int i = 0; i < 4; i++)
                {
                    float p = 1f - swell;
                    Circle(f.P(ax - rx - 8f - p * 30f, (i - 1.5f) * 6f * p), (2.2f - p) * K + 0.6f, QHoney.WithAlpha(a * swell), 8);
                }
            }
            {
                V2 o0 = f.P(ax - rx + 2f, -3f), o1 = f.P(ax - rx - 8f, 0f), o2 = f.P(ax - rx + 2f, 3f);
                SetColor(col.WithAlpha(a * 0.5f));
                Tri(o0, o1, o2);
                HiveStroke(o0, o1, thin, col, K * 4f);
                HiveStroke(o1, o2, thin, col, K * 4f);
            }

            // stubby wings (torn from the Frenzy on), then the thorax and its wax gem
            // (in a dive: spread wide and long, beating hard, with a ghost of the last beat behind each)
            float wl = Mathf.Lerp(24f, 42f, air), ww = Mathf.Lerp(8f, 13f, air), wa = Mathf.Lerp(0.95f, 0.5f, air);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                if (air > 0.05f)
                    QueenWing(f, tr * 0.1f, sd * tr * 0.6f, wl, ww, MathX.Pi + sd * (wa + 0.45f * air), -fl, frz, a * 0.22f * air);
                QueenWing(f, tr * 0.1f, sd * tr * 0.6f, wl, ww, MathX.Pi + sd * (wa + 0.15f * fl), fl, frz, a * Mathf.Lerp(0.6f, 0.45f, air));
            }
            HiveEllipse(f, 0f, 0f, tr, tr, 0f, col, 0.34f, lw, gl, 16);
            if (!last)
                HiveHexRing(f, 0f, 0f, tr * 0.38f, MathX.Pi / 6f, (frz ? QRed : HiveWax).WithAlpha(a), 0.6f, thin, K * 10f);

            // the head: antennae, eyes, jaws and the tiara
            float hx = tr * 0.95f + hr * 0.55f, flick = Mathf.Sin(t * 3f) * 0.12f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                V2 b0 = f.P(hx + hr * 0.6f, sd * hr * 0.45f);
                float ex = hx + hr * 0.6f + hr * 1.1f, ey = sd * (hr + 2f) + sd * flick * 6f;
                V2 b1 = f.P(ex, ey), b2 = f.P(ex + hr * 0.9f, ey + sd * hr * 0.45f + sd * flick * 4f);
                HiveStroke(b0, b1, Mathf.Max(1f, 1.4f * K), col, K * 4f);
                HiveStroke(b1, b2, Mathf.Max(1f, 1.4f * K), col, K * 4f);
                Circle(b2, Mathf.Max(1f, 1.4f * K), col, 6);
            }
            HiveEllipse(f, hx, 0f, hr, hr, 0f, col, 0.35f, lw, gl, 16);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                HiveEllipse(f, hx + hr * 0.15f, sd * hr * 0.58f, hr * 0.48f, hr * 0.3f, sd * 0.35f, QDark.WithAlpha(a), 0.9f, 0f, 0f, 10);
                HiveEllipse(f, hx + hr * 0.15f, sd * hr * 0.58f, hr * 0.48f, hr * 0.3f, sd * 0.35f, col, 0f, Mathf.Max(1f, K), K * 3f, 10);
                Circle(f.P(hx + hr * 0.3f, sd * hr * 0.52f), Mathf.Max(0.8f, 0.65f * K), frz ? QRed.WithAlpha(a) : white, 6);
                V2 m0 = f.P(hx + hr * 0.85f, sd * hr * 0.25f), m1 = f.P(hx + hr * 1.35f, sd * hr * 0.5f), m2 = f.P(hx + hr * 1.45f, sd * hr * 0.12f), prev = m0;
                for (int s = 1; s <= 4; s++)
                {
                    float u = s / 4f;
                    V2 nx = m0 * ((1f - u) * (1f - u)) + m1 * (2f * u * (1f - u)) + m2 * (u * u);
                    HiveStroke(prev, nx, Mathf.Max(1f, 1.4f * K), col, K * 4f);
                    prev = nx;
                }
            }
            var crown = (frz ? QOrange : QGold).Mix(Rgba.White, hit).WithAlpha(a);
            for (int k = 0; k < 5; k++)
            {
                float an = MathX.Pi + (k - 2) * 0.42f, r0 = hr * 0.95f, r1 = hr * (k == 2 ? 1.75f : 1.45f);
                V2 c0 = f.P(hx + Mathf.Cos(an - 0.15f) * r0, Mathf.Sin(an - 0.15f) * r0), c1 = f.P(hx + Mathf.Cos(an) * r1, Mathf.Sin(an) * r1), c2 = f.P(hx + Mathf.Cos(an + 0.15f) * r0, Mathf.Sin(an + 0.15f) * r0);
                SetColor(crown.WithAlpha(a * 0.55f));
                Tri(c0, c1, c2);
                HiveStroke(c0, c1, thin, crown, K * 6f);
                HiveStroke(c1, c2, thin, crown, K * 6f);
            }
            for (int s = 0; s < 6; s++)
            {
                float a0 = MathX.Pi - 1f + 2f * s / 6f, a1 = MathX.Pi - 1f + 2f * (s + 1) / 6f;
                HiveStroke(f.P(hx + Mathf.Cos(a0) * hr * 0.98f, Mathf.Sin(a0) * hr * 0.98f), f.P(hx + Mathf.Cos(a1) * hr * 0.98f, Mathf.Sin(a1) * hr * 0.98f), thin, crown, K * 5f);
            }

            if (stage == (int)QueenStage.Frenzy)  // embers rising off her
                for (int i = 0; i < 14; i++)
                {
                    float p = (t * 0.6f + QRnd(i)) % 1f, an = QRnd(i + 9) * MathX.Tau, rr = 46f * K * (0.3f + 0.7f * QRnd(i + 3));
                    Circle(pos + new V2(Mathf.Cos(an) * rr + Mathf.Sin(t * 2f + i) * 4f, Mathf.Sin(an) * rr - p * 36f), 1.6f, QOrange.WithAlpha(a * 0.9f * (1f - p)), 5);
                }
        }

        static V2 QW(HiveFrame f, float x0, float y0, float cs, float sn, float x, float y) => f.P(x0 + x * cs - y * sn, y0 + x * sn + y * cs);

        V2 queenTip;  // where her ovipositor is this frame (her eggs fly from it)

        /// The landing from a royal dive, 1 at the hit wobbling down to 0 over 0.45 s (negative on the rebound).
        static float QueenSquash(QueenState q)
        {
            if (q == null || q.TransT > 0f || q.Turn != QueenTurn.Dive || q.DiveT < Game.DiveLand)
                return 0f;
            float k = (q.DiveT - Game.DiveLand) / 0.45f;
            return k >= 1f ? 0f : (1f - k) * (1f - k) * Mathf.Cos(k * MathX.Pi * 3f);
        }

        /// Her eggs, as she lays them: each arcs from her ovipositor to where it lands, with a fading trail.
        void DrawEggFlights(Enemy queen, float t)
        {
            var q = queen.Queen;
            if (q.TransT > 0f || q.Turn != QueenTurn.Eggs || q.TurnT >= Game.EggFlight)
                return;
            float p = q.TurnT / Game.EggFlight;
            foreach (var egg in game.Enemies.Values)
            {
                if (egg.Type != EnemyType.HiveEgg)
                    continue;
                for (int k = 3; k >= 0; k--)
                {
                    float u = Mathf.Max(0f, p - k * 0.06f), h = 90f * Mathf.Sin(MathX.Pi * u);
                    V2 at = V2.Lerp(queenTip, egg.Disp, u) + new V2(0f, -h);
                    if (k > 0)
                        Circle(at, 6f - k * 1.3f, QHoney.WithAlpha(0.45f - k * 0.12f), 8);
                    else
                    {
                        Circle(at, 12f, HiveWax.WithAlpha(0.25f), 12);
                        Circle(at, 8.5f, HiveWax, 12);
                        Circle(at + new V2(2f, 0f), 3f, QHoney, 8);
                    }
                }
            }
        }

        /// On the floor where she lands from a dive: wax cracks shooting out, honey splashed round, dust rolling out and fading.
        void DrawDiveLanding(Enemy e, QueenState q, float t)
        {
            if (q.DiveT < Game.DiveLand || q.DiveT >= Game.DiveLand + 0.7f)
                return;
            float k = (q.DiveT - Game.DiveLand) / 0.7f, ek = 1f - (1f - k) * (1f - k), R = Game.DiveRadius;
            V2 at = q.Shadow;
            for (int i = 0; i < 7; i++)  // cracks
            {
                float an = i * MathX.Tau / 7f + QRnd(i + q.Dives * 7) * 0.5f, len = R * (0.9f + 0.5f * QRnd(i + 20)) * Mathf.Min(1f, k * 5f);
                V2 d = V2.FromAngle(an), mid = at + d * len * 0.55f + V2.FromAngle(an + 1.4f) * 8f;
                Line(at + d * 18f, mid, 2.5f, QDark.WithAlpha(0.7f * (1f - k)));
                Line(mid, at + d * len, 1.6f, QDark.WithAlpha(0.6f * (1f - k)));
            }
            for (int i = 0; i < 9; i++)  // honey splashed
            {
                float an = i * MathX.Tau / 9f + QRnd(i + 40), d = R * (0.35f + 0.5f * QRnd(i + 50)) * Mathf.Min(1f, k * 6f);
                Circle(at + V2.FromAngle(an) * d, (5f + 6f * QRnd(i + 60)) * (1f - 0.3f * k), QHoney.WithAlpha(0.55f * Mathf.Sqrt(1f - k)), 10);
            }
            for (int i = 0; i < 14; i++)  // dust
            {
                float an = i * MathX.Tau / 14f + QRnd(i + 70) * 0.4f, d = R * (0.4f + 0.8f * ek) * (0.85f + 0.3f * QRnd(i + 80));
                Circle(at + V2.FromAngle(an) * d, 7f + 12f * k, HiveWax.WithAlpha(0.3f * (1f - k)), 10);
            }
        }

        /// A wing from its root (x, y in the frame), swept back along `ang`: a faint wax fill, its rim (notched when torn) and veins.
        void QueenWing(HiveFrame f, float x0, float y0, float len, float wid, float ang, float flap, bool torn, float alpha)
        {
            const int N = 10;
            float w = wid * (0.6f + 0.4f * Mathf.Abs(flap)), cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            var rim = HiveWax.WithAlpha(alpha);
            var fillC = HiveWax.WithAlpha(alpha * 0.14f);
            V2 root = QW(f, x0, y0, cs, sn, 0f, 0f), up = root, lo = root;
            for (int i = 1; i <= N; i++)
            {
                float u = i / (float)N, y = Mathf.Sin(MathX.Pi * u) * w * (1f - 0.35f * u);
                if (torn && i > 3 && i < N - 1 && QRnd(i * 7.3f + len) < 0.45f)
                    y *= 0.45f;
                V2 nu = QW(f, x0, y0, cs, sn, -len * u, y), nl = QW(f, x0, y0, cs, sn, -len * u, -Mathf.Sin(MathX.Pi * u) * w * 0.25f);
                SetColor(fillC);
                Tri(root, up, nu);
                Tri(root, lo, nl);
                HiveStroke(up, nu, Mathf.Max(1f, 1.1f * f.K), rim, f.K * 4f);
                HiveStroke(lo, nl, Mathf.Max(1f, 1.1f * f.K), rim, 0f);
                up = nu;
                lo = nl;
            }
            var vein = HiveWax.WithAlpha(alpha * 0.35f);
            HiveStroke(QW(f, x0, y0, cs, sn, 0f, 0f), QW(f, x0, y0, cs, sn, -len * 0.92f, w * 0.18f), Mathf.Max(0.8f, 0.8f * f.K), vein, 0f);
            HiveStroke(QW(f, x0, y0, cs, sn, -len * 0.35f, w * 0.12f), QW(f, x0, y0, cs, sn, -len * 0.7f, w * 0.7f), Mathf.Max(0.8f, 0.7f * f.K), vein, 0f);
        }
    }
}
