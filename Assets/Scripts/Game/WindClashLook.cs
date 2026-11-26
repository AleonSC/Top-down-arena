using UnityEngine;

namespace Arena
{
    /// Wind Clash's look, Look A "Gale crescents" of Docs/Mockups/wind-clash.html: layered crescents of pale cyan air (a wide
    /// deep band, a mid band, a bright leading edge) with speed streaks trailing inside, a tapered gale lance with a small
    /// crescent riding its head, a clash ring with streaks wheeling inside it, and the Tempest: rotating bands of wind round a
    /// calm eye, two bright arcs racing round its rim. Caught enemy shots spin round you and burst.
    /// Drawing only: every wave comes from its FxKind.WindWave (shape from Game.WcArc / WcLance / WcRing, the same the hits use),
    /// the storm from the replicated WcTempestT, the catches from FxKind.WindCatch.
    public partial class ArenaApp
    {
        static readonly Rgba WcCore = new Rgba(0.95f, 0.99f, 1f), WcMid = new Rgba(0.56f, 0.89f, 0.95f), WcDeep = new Rgba(0.18f, 0.62f, 0.75f);
        static readonly Rgba WcShot = new Rgba(1f, 0.82f, 0.63f);

        static float WcHash(float x) { float s = Mathf.Sin(x * 12.9898f) * 43758.5453f; return s - Mathf.Floor(s); }
        static float WcSeed(Effect fx) => fx.Pos.X * 0.731f + fx.Pos.Y * 1.379f + fx.Pos2.X * 3.1f;

        /// A crescent band along the circle of radius r from `start` through `span` radians: th thick at its middle, tapering
        /// to points at both ends; `lead` of it lies outside r.
        void WcBand(V2 c, float r, float start, float span, float th, float lead, Rgba col)
        {
            const int N = 24;
            SetColor(col);
            V2 po = c, pi = c;
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, a = start + span * u, tp = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.7f);
                V2 d = V2.FromAngle(a), o = c + d * (r + th * lead * tp), n = c + d * Mathf.Max(0f, r - th * (1f - lead) * tp);
                if (i > 0)
                    Quad(po, o, n, pi);
                po = o;
                pi = n;
            }
        }

        /// A tapered strip of wind from t0 to t1 along `dir` from o, widest at its head, with a rounded point on the head.
        void WcStrip(V2 o, V2 dir, float t0, float t1, float w, float tip, Rgba col)
        {
            if (t1 - t0 <= 1f)
                return;
            const int N = 12;
            V2 nrm = dir.Orthogonal();
            SetColor(col);
            V2 pl = o, pr = o;
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, h = w * (0.18f + 0.82f * Mathf.Pow(u, 0.6f)) / 2f;
                V2 m = o + dir * (t0 + (t1 - t0) * u), l = m + nrm * h, r = m - nrm * h;
                if (i > 0)
                    Quad(pl, l, r, pr);
                pl = l;
                pr = r;
            }
            V2 hd = o + dir * t1, pt = hd + dir * (w * tip), sl = hd + dir * (w * tip * 0.6f) + nrm * (w * 0.25f), sr = hd + dir * (w * tip * 0.6f) - nrm * (w * 0.25f);
            Tri(hd, pl, sl); Tri(hd, sl, pt); Tri(hd, pt, sr); Tri(hd, sr, pr);
        }

        /// FxKind.WindWave: a crescent, the gale lance or the clash ring, `fx.T` into its life.
        void DrawWindWave(Effect fx)
        {
            int step = System.Math.Abs((int)fx.Pos2.Y) % 4;
            float aim = fx.Pos2.X, age = fx.T, scale = fx.Radius, seed = WcSeed(fx);
            V2 c = fx.Pos;
            if (step < 2)
            {
                float r, sweep, al, s, span;
                Game.WcArc(age, scale, out r, out sweep, out al);
                Game.WcArcSpan(aim, step, sweep, out s, out span);
                if (Mathf.Abs(span) < 0.02f || al <= 0f)
                    return;
                WcBand(c, r - 6f, s, span, 30f, 0.3f, WcDeep.WithAlpha(0.22f * al));
                WcBand(c, r - 2f, s, span, 15f, 0.35f, WcMid.WithAlpha(0.5f * al));
                WcBand(c, r, s, span, 5f, 0.5f, WcCore.WithAlpha(al));
                for (int k = 1; k <= 4; k++)
                {
                    float sh = 0.08f * k;
                    Arc(c, r - 10f - k * 8f, 2f - k * 0.3f, WcMid.WithAlpha((0.75f - k * 0.14f) * al), 20, s + span * sh, span * (1f - sh * 1.6f));
                }
                if (!fxReduced)
                    WcWaveStreaks(fx, c, seed, age, Game.WcArcDur, r, s, span);
            }
            else if (step == 2)
            {
                float head, tail, al;
                Game.WcLance(age, scale, out head, out tail, out al);
                if (head - tail < 2f || al <= 0f)
                    return;
                V2.Axes(aim, out V2 dir, out V2 nrm);
                float w = Game.WcLanceW * scale;
                WcStrip(c, dir, tail, head, w * 1.5f, 0.5f, WcDeep.WithAlpha(0.22f * al));
                WcStrip(c, dir, tail, head, w * 0.8f, 0.5f, WcMid.WithAlpha(0.5f * al));
                WcStrip(c, dir, tail, head, w * 0.28f, 0.6f, WcCore.WithAlpha(al));
                for (int k = 0; k < 4; k++)
                {
                    float o = k < 2 ? 0.62f : 0.95f, f = k == 0 ? 0.55f : k == 1 ? 0.6f : k == 2 ? 0.35f : 0.4f, sg = k % 2 == 0 ? 1f : -1f;
                    float t1 = head - 8f, t0 = t1 - (head - tail) * f;
                    if (t1 > t0)
                        Line(c + dir * t0 + nrm * (w * o * sg), c + dir * t1 + nrm * (w * o * sg), 1.4f, WcMid.WithAlpha((0.9f - o * 0.45f) * al));
                }
                WcBand(c + dir * (head - 14f), 18f, aim - 1.1f, 2.2f, 5f, 0.5f, WcCore.WithAlpha(al));  // the crescent riding the head
                if (!fxReduced)
                    for (int k = 0; k < 10; k++)
                    {
                        float born = WcHash(seed + k) * Game.WcLanceDur * 0.6f, life = 0.2f, tau = age - born;
                        if (tau < 0f || tau > life)
                            continue;
                        float h0, t0, a0;
                        Game.WcLance(born, scale, out h0, out t0, out a0);
                        float d = t0 + (h0 - t0) * WcHash(seed + k * 3.7f), side = (WcHash(seed + k * 5.3f) - 0.5f) * w * 1.4f;
                        float moved = 380f / 5f * (1f - Mathf.Exp(-5f * tau)), v = 380f * Mathf.Exp(-5f * tau);
                        V2 at = c + dir * (d + moved) + nrm * side;
                        Line(at - dir * Mathf.Min(16f, v * 0.05f), at, 1.4f, WcMid.WithAlpha(0.8f * (1f - tau / life)));
                    }
            }
            else
            {
                float r, al;
                Game.WcRing(age, scale, out r, out al);
                if (al <= 0f)
                    return;
                float p = Mathf.Clamp01(age / Game.WcRingDur), spin = WcHash(seed) * MathX.Tau;
                Arc(c, r - 6f, 18f, WcDeep.WithAlpha(0.22f * al), 48);
                Arc(c, r - 2f, 8f, WcMid.WithAlpha(0.5f * al), 48);
                Arc(c, r, 2.6f, WcCore.WithAlpha(al), 48);
                for (int i = 0; i < 10; i++)
                    Arc(c, Mathf.Max(4f, r - 14f - (i % 3) * 5f), 1.3f, WcMid.WithAlpha(0.6f * al), 8, i * MathX.Tau / 10f + spin + p * 1.2f, 0.42f);
                if (!fxReduced && age < 0.3f)
                    for (int k = 0; k < 22; k++)
                    {
                        float b = k * MathX.Tau / 22f + WcHash(seed + k) * 0.2f, f = 1f - age / 0.3f;
                        float d = 20f + 320f / 5f * (1f - Mathf.Exp(-5f * age)), v = 320f * Mathf.Exp(-5f * age);
                        V2 dir = V2.FromAngle(b);
                        Line(c + dir * (d - Mathf.Min(16f, v * 0.05f)), c + dir * d, 1.4f, WcMid.WithAlpha(0.8f * f));
                    }
            }
        }

        /// Streaks of air flung off a crescent's front as it flies: each leaves the front at its own moment and slows.
        void WcWaveStreaks(Effect fx, V2 c, float seed, float age, float dur, float rNow, float s, float span)
        {
            for (int k = 0; k < 12; k++)
            {
                float born = WcHash(seed + k) * dur * 0.7f, life = 0.22f, tau = age - born;
                if (tau < 0f || tau > life)
                    continue;
                float r0, sw0, a0, s0, span0;
                Game.WcArc(born, fx.Radius, out r0, out sw0, out a0);
                Game.WcArcSpan(fx.Pos2.X, System.Math.Abs((int)fx.Pos2.Y) % 4, sw0, out s0, out span0);
                float ang = s0 + span0 * WcHash(seed + k * 2.3f);
                float d = r0 + 260f / 5f * (1f - Mathf.Exp(-5f * tau)), v = 260f * Mathf.Exp(-5f * tau);
                V2 dir = V2.FromAngle(ang);
                Line(c + dir * (d - Mathf.Min(16f, v * 0.05f)), c + dir * d, 1.4f, WcMid.WithAlpha(0.8f * (1f - tau / life)));
            }
        }

        /// FxKind.WindCatch: an enemy shot caught by a Tempest spins round its player, shrinking, then bursts into a puff.
        void DrawWindCatch(Effect fx)
        {
            Player p;
            if (!game.Players.TryGetValue((int)fx.Pos2.X, out p))
                return;
            V2 c = p.Disp, rel = fx.Pos - p.Pos;
            float tau = fx.T, a = Mathf.Atan2(rel.Y, rel.X) + 6f * tau + 7f * tau * tau, r = Mathf.Max(18f, rel.Length - 70f * tau);
            V2 at = c + V2.FromAngle(a) * r;
            float f = Mathf.Clamp01(tau / Game.WindCatchTime);
            if (f < 0.85f)
                Circle(at, 3.5f * (1f - f * 0.8f), WcShot, 10);
            if (f > 0.6f)
            {
                float q = (f - 0.6f) / 0.4f;
                for (int k = 0; k < 6; k++)
                {
                    V2 d = V2.FromAngle(k * MathX.Tau / 6f + WcHash(fx.Pos.X + k));
                    Circle(at + d * (q * 14f), 2f + q * 4f, WcCore.WithAlpha(0.5f * (1f - q)), 8);
                }
            }
        }

        /// Under a player with Wind Clash's Tempest up: rotating bands of wind round a calm eye, bright arcs racing round the rim.
        void DrawTempest(Player p, float t)
        {
            if (p.WcTempestT <= 0f || p.Weapon.Id != WeaponId.WindClash)
                return;
            float age = Game.TempestTime - p.WcTempestT, A = Mathf.Clamp01(p.WcTempestT / 0.3f);
            float r = Game.TempestRadiusOf(p) * Game.WcEase(age / Game.TempestSpinUp);
            if (r < 2f)
                return;
            V2 c = p.Disp;
            // the field: clear in the middle, deeper toward the rim
            const int N = 40;
            Rgba rim = WcDeep.WithAlpha(0.28f * A), mid = WcDeep.WithAlpha(0f);
            for (int i = 0; i < N; i++)
            {
                V2 d0 = V2.FromAngle(MathX.Tau * i / N), d1 = V2.FromAngle(MathX.Tau * (i + 1) / N);
                VTri(c + d0 * (r * 0.25f), mid, c + d0 * r, rim, c + d1 * r, rim);
                VTri(c + d0 * (r * 0.25f), mid, c + d1 * r, rim, c + d1 * (r * 0.25f), mid);
            }
            // three bands of wind going round, the middle one the other way
            for (int i = 0; i < 22; i++)
            {
                int band = i % 3;
                float rr = r * (0.42f + band * 0.2f + ((i * 37) % 10) / 100f), sp = (2.2f + band * 1.3f) * (band == 1 ? -1f : 1f);
                float s = i * 1.7f + t * sp, len = 0.5f + ((i * 13) % 7) / 8f;
                Arc(c, rr, 1f + band * 0.6f, (band == 2 ? WcCore : WcMid).WithAlpha((0.35f + band * 0.18f) * A), 16, s, len);
            }
            // the rim
            Arc(c, r - 3f, 6f, WcMid.WithAlpha(0.35f * A), 48);
            Rgba edge = WcCore.WithAlpha((0.7f + 0.2f * Mathf.Sin(t * 30f)) * A);
            Arc(c, r, 2f, edge, 24, t * 5f, MathX.Tau * 0.42f);
            Arc(c, r, 2f, edge, 24, t * 5f + MathX.Pi, MathX.Tau * 0.42f);
            // streaks blown round it
            if (!fxReduced)
                for (int k = 0; k < 14; k++)
                {
                    float life = 0.25f, ph = (t / life + WcHash(k + 0.5f)) % 1f, cyc = Mathf.Floor(t / life + WcHash(k + 0.5f));
                    float a0 = WcHash(k * 7.1f + cyc) * MathX.Tau, rr = r * (0.5f + 0.5f * WcHash(k * 3.3f + cyc));
                    float a = a0 + 260f / rr * ph * life;
                    V2 at = c + V2.FromAngle(a) * rr, tang = V2.FromAngle(a + MathX.Pi / 2f);
                    Line(at - tang * 12f, at, 1.4f, WcMid.WithAlpha(0.8f * (1f - ph) * A));
                }
        }

        /// The glow pass: a soft light along each wave's bright edge and in the Tempest's eye.
        void DrawWindClashGlow()
        {
            foreach (var fx in FxOf(FxKind.WindWave))
            {
                if (fx.Kind != FxKind.WindWave)
                    continue;
                int step = System.Math.Abs((int)fx.Pos2.Y) % 4;
                if (step < 2)
                {
                    float r, sw, al, s, span;
                    Game.WcArc(fx.T, fx.Radius, out r, out sw, out al);
                    Game.WcArcSpan(fx.Pos2.X, step, sw, out s, out span);
                    for (int k = 0; k < 5; k++)
                        GlowDisc(fx.Pos + V2.FromAngle(s + span * (0.1f + 0.2f * k)) * r, 18f, WcMid, 0.16f * al);
                }
                else if (step == 2)
                {
                    float h, tl, al;
                    Game.WcLance(fx.T, fx.Radius, out h, out tl, out al);
                    GlowDisc(fx.Pos + V2.FromAngle(fx.Pos2.X) * h, 26f, WcMid, 0.25f * al);
                }
                else
                {
                    float r, al;
                    Game.WcRing(fx.T, fx.Radius, out r, out al);
                    for (int k = 0; k < 8; k++)
                        GlowDisc(fx.Pos + V2.FromAngle(k * MathX.Tau / 8f) * r, 16f, WcMid, 0.14f * al);
                }
            }
            foreach (var p in game.Players.Values)
                if (p.WcTempestT > 0f && p.Weapon.Id == WeaponId.WindClash && !p.Downed)
                    GlowDisc(p.Disp, Game.TempestRadiusOf(p) * 0.8f, WcMid, 0.12f * Mathf.Clamp01(p.WcTempestT / 0.3f));
        }

        /// Wind Clash's hands (model frame: +x your aim): the casting hand sweeps across the front with each crescent, both
        /// thrust forward for the lance and fling out wide for the ring, then ease back. From this player's newest wave.
        void WcHandPose(Player p, ref float rhx, ref float rhy, ref float lhx, ref float lhy)
        {
            Effect last = null;
            foreach (var fx in FxOf(FxKind.WindWave))
                if (fx.Kind == FxKind.WindWave && (int)fx.Pos2.Y / 4 == p.Id && (last == null || fx.T < last.T))
                    last = fx;
            if (last == null || last.T > 0.5f)
                return;
            int step = System.Math.Abs((int)last.Pos2.Y) % 4;
            float back = Mathf.Clamp01((last.T - 0.18f) / 0.3f), k = Game.WcEase(last.T / 0.14f), mix = 1f - back * back * (3f - 2f * back);
            float ax, ay, bx, by;
            if (step < 2)
            {
                float side = step == 0 ? 1f : -1f, a = side * 1.4f - side * 2.8f * k;
                ax = Mathf.Cos(a) * 19f; ay = Mathf.Sin(a) * 19f;
                bx = lhx; by = lhy;
            }
            else if (step == 2)
            {
                float q = Mathf.Sin(Mathf.PI * Mathf.Clamp01(last.T / 0.25f)), r = 19f + 8f * q;
                ax = Mathf.Cos(0.15f) * r; ay = Mathf.Sin(0.15f) * r;
                bx = Mathf.Cos(-0.15f) * r; by = Mathf.Sin(-0.15f) * r;
            }
            else
            {
                float q = Mathf.Sin(Mathf.PI * Mathf.Clamp01(last.T / 0.22f)), r = 19f + 4f * q;
                ax = Mathf.Cos(1.6f) * r; ay = Mathf.Sin(1.6f) * r;
                bx = Mathf.Cos(-1.6f) * r; by = Mathf.Sin(-1.6f) * r;
            }
            rhx += (ax - rhx) * mix; rhy += (ay - rhy) * mix;
            lhx += (bx - lhx) * mix; lhy += (by - lhy) * mix;
        }
    }
}
