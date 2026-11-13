using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Hive Queen's faction (Core/HiveKin.cs), drawn as in `Docs/Mockups/hive-faction.html`: one insect body plan (wings,
    /// abdomen with stripes, thorax, head) that each type fills in with its own sizes and colours, as vector lines with a faint
    /// fill and a soft glow. The mockup's numbers are used as they are, in "canvas units"; `W` turns a canvas unit into world
    /// pixels so that the body is about 1.45 radii from the middle to the head.
    ///   Worker (Rioter): small, wings buzzing.   Soldier (Guard): armoured, jaws that snap near you.
    ///   Comb Shield (Riot Shield): a plate of seven wax cells that breaks cell by cell, flashes before the bash.
    ///   Honey Lobber (Gas Grenadier): legs planted, abdomen glowing as it winds up; honey glob and ring in GasLobLook.cs.
    ///   Stinger (Taser Guard): a venom lance that crackles while it charges; its dart is DrawStingBolt.
    ///   Needler (Security Guard): a needle snout, a magazine of seven pips above it.   Bumble (Thug): fuzz all round.
    ///   Hornet (Shiv): a white lance held out front.
    public partial class ArenaApp
    {
        sealed class HiveSpec
        {
            public Rgba Col;
            public Rgba Stripe, LanceCol, Tip, GlowCol;
            public float S = 1f;                       // the mockup's insect scale
            public float AbdX = 8f, AbdY = 5.5f, Th = 4.5f, Head = 3.4f, WingL = 10f, WingW = 3.4f, Flap = 50f;
            public float Lance, Legs, Mand = -1f;
            public int Stripes = 2, Fuzz;
            public bool Armor, Plate;
            public float BodyHalf => (2f * AbdX + Th * 0.55f + Th * 0.95f + 1.5f * Head) * 0.5f * S;  // canvas units, middle to head
            public float W(float r) => r * 1.45f / BodyHalf;                                         // world px per canvas unit
        }

        static HiveSpec[] hiveSpecs;

        static HiveSpec HiveSpecOf(EnemyType t)
        {
            if (hiveSpecs == null)
            {
                hiveSpecs = new HiveSpec[(int)EnemyType.Hornet + 1];
                hiveSpecs[(int)EnemyType.Worker] = new HiveSpec { Col = Hex(0xffc533), S = 1.15f, AbdX = 6f, AbdY = 4.2f, Th = 3.5f, Head = 2.7f, Stripes = 2, WingL = 8f, WingW = 3f, Flap = 62f };
                hiveSpecs[(int)EnemyType.Soldier] = new HiveSpec { Col = Hex(0xff7a1f), S = 1.45f, AbdX = 9f, AbdY = 6.5f, Th = 5.4f, Head = 4.2f, Stripes = 3, WingL = 10f, WingW = 3.4f, Flap = 34f, Armor = true, Mand = 0.25f };
                hiveSpecs[(int)EnemyType.CombShield] = new HiveSpec { Col = Hex(0xffc533), S = 1.2f, AbdX = 7f, AbdY = 5f, Th = 4.4f, Head = 3.2f, Stripes = 2, WingL = 9f, WingW = 3f, Flap = 40f, Plate = true };
                hiveSpecs[(int)EnemyType.HoneyLobber] = new HiveSpec { Col = Hex(0xffa21a), S = 1.25f, AbdX = 11f, AbdY = 8.5f, Th = 4f, Head = 3f, Stripes = 3, WingL = 7f, WingW = 2.6f, Flap = 20f, Legs = 4.5f, GlowCol = Hex(0xffc533) };
                hiveSpecs[(int)EnemyType.Stinger] = new HiveSpec { Col = Hex(0xffec9a), S = 1.2f, AbdX = 8f, AbdY = 3.6f, Th = 3.6f, Head = 2.8f, Stripes = 3, Stripe = Hex(0xc8ff3d), WingL = 11f, WingW = 2.8f, Flap = 55f, Lance = 15f, LanceCol = Hex(0xc8ff3d), Tip = Hex(0xc8ff3d) };
                hiveSpecs[(int)EnemyType.Needler] = new HiveSpec { Col = Hex(0xffec9a), S = 1.15f, AbdX = 9f, AbdY = 3.2f, Th = 3.4f, Head = 2.6f, Stripes = 2, WingL = 12f, WingW = 2.6f, Flap = 58f, Lance = 20f, LanceCol = Hex(0xfff7e0), Tip = Hex(0xfff7e0) };
                hiveSpecs[(int)EnemyType.Bumble] = new HiveSpec { Col = Hex(0xe8920c), S = 1.9f, AbdX = 8.5f, AbdY = 7f, Th = 6.2f, Head = 3.8f, Stripes = 3, WingL = 7f, WingW = 2.8f, Flap = 26f, Fuzz = 16 };
                hiveSpecs[(int)EnemyType.Hornet] = new HiveSpec { Col = Hex(0xff4a28), S = 1.3f, AbdX = 8f, AbdY = 4.4f, Th = 4.4f, Head = 3.4f, Stripes = 2, WingL = 11f, WingW = 3f, Flap = 52f, Lance = 12f, LanceCol = Hex(0xfff7e0), Tip = Hex(0xfff7e0) };
                foreach (var s in hiveSpecs)
                    if (s != null)
                    {
                        if (s.Stripe.A == 0f) s.Stripe = s.Col;
                        if (s.LanceCol.A == 0f) s.LanceCol = s.Col;
                        if (s.Tip.A == 0f) s.Tip = s.LanceCol;
                        if (s.GlowCol.A == 0f) s.GlowCol = Hex(0xfff7e0);
                    }
            }
            return hiveSpecs[(int)t];
        }

        static readonly Rgba HiveWhite = new Rgba(1f, 0.969f, 0.878f), HiveWax = new Rgba(0.953f, 0.894f, 0.702f);

        // A frame in the bee's own space: x forward, y to its right, `K` world pixels to a unit.
        struct HiveFrame
        {
            public V2 O, F, S;
            public float K;
            public V2 P(float x, float y) => O + F * (x * K) + S * (y * K);
        }

        /// A stroke with a soft glow under it (the mockup's shadow blur).
        void HiveStroke(V2 a, V2 b, float w, Rgba c, float glow)
        {
            if (glow > 0f && c.A > 0f)
                Line(a, b, w + glow * 0.9f, c.WithAlpha(c.A * 0.14f));
            Line(a, b, w, c);
        }

        /// An ellipse (centre, radii, tilt in the bee's frame): a faint fill, then the outline.
        void HiveEllipse(HiveFrame f, float cx, float cy, float rx, float ry, float rot, Rgba c, float fillA, float w, float glow, int segs = 14)
        {
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            System.Func<float, V2> Pt = a => { float x = rx * Mathf.Cos(a), y = ry * Mathf.Sin(a); return f.P(cx + x * cr - y * sr, cy + x * sr + y * cr); };
            if (fillA > 0f)
            {
                SetColor(c.WithAlpha(c.A * fillA));
                V2 mid = f.P(cx, cy), prev = Pt(0f);
                for (int i = 1; i <= segs; i++)
                {
                    V2 next = Pt(MathX.Tau * i / segs);
                    Tri(mid, prev, next);
                    prev = next;
                }
            }
            V2 p0 = Pt(0f);
            for (int i = 1; i <= segs; i++)
            {
                V2 next = Pt(MathX.Tau * i / segs);
                HiveStroke(p0, next, w, c, glow);
                p0 = next;
            }
        }

        void HiveHexRing(HiveFrame f, float cx, float cy, float rad, float rot, Rgba c, float fillA, float w, float glow)
        {
            var pts = new V2[6];
            for (int i = 0; i < 6; i++)
                pts[i] = f.P(cx + Mathf.Cos(rot + i * MathX.Tau / 6f) * rad, cy + Mathf.Sin(rot + i * MathX.Tau / 6f) * rad);
            if (fillA > 0f)
            {
                SetColor(c.WithAlpha(c.A * fillA));
                V2 mid = f.P(cx, cy);
                for (int i = 0; i < 6; i++)
                    Tri(mid, pts[i], pts[(i + 1) % 6]);
            }
            for (int i = 0; i < 6; i++)
                HiveStroke(pts[i], pts[(i + 1) % 6], w, c, glow);
        }

        /// The bee: wings, legs, abdomen and stripes, thorax, fuzz, head, jaws and lance. `a` fades it in as it spawns.
        void DrawHiveInsect(HiveSpec o, V2 pos, float ang, float r, float t, float a, float hit, float mand, float glowAmt, bool plated = false, float scaleBy = 1f)
        {
            float w = o.W(r) * scaleBy;
            var f = new HiveFrame { O = pos, F = V2.FromAngle(ang), S = V2.FromAngle(ang).Orthogonal(), K = w * o.S };
            float lw = Mathf.Max(1.2f, 1.7f * f.K);  // a mockup stroke (about 1.7 in the insect's own units)
            float gl = f.K * 6f;
            Rgba col = o.Col.Mix(Rgba.White, hit).WithAlpha(a);
            Rgba white = HiveWhite.WithAlpha(a);
            float tr = o.Th, hr = o.Head;
            float ax = -(o.AbdX + tr * 0.55f), hx = tr * 0.95f + hr * 0.5f, fl = Mathf.Sin(t * o.Flap + 1.7f * (pos.X + pos.Y) * 0.01f);
            // wings
            for (int sd = -1; sd <= 1; sd += 2)
                HiveEllipse(f, -o.WingL * 0.3f, sd * (tr + o.WingW * 0.55f), o.WingL, o.WingW * (0.55f + 0.45f * Mathf.Abs(fl)), sd * 0.45f, white.WithAlpha(a * 0.5f), 0.12f, Mathf.Max(1f, f.K * 0.9f), 0f, 10);
            // legs
            if (o.Legs > 0f)
                for (int sd = -1; sd <= 1; sd += 2)
                    for (int k = 0; k < 3; k++)
                    {
                        float lx = Mathf.Lerp(-tr * 0.7f, tr * 0.9f, k / 2f);
                        HiveStroke(f.P(lx, sd * tr * 0.6f), f.P(lx + (k - 1) * 3f, sd * (tr + o.Legs)), Mathf.Max(1f, 1.1f * f.K), col, f.K * 3f);
                    }
            // abdomen and its stripes
            HiveEllipse(f, ax, 0f, o.AbdX, o.AbdY, 0f, col, 0.22f, lw, gl);
            var stripe = o.Stripe.Mix(Rgba.White, hit).WithAlpha(a);
            for (int i = 0; i < o.Stripes; i++)
            {
                float xx = ax - o.AbdX * 0.55f + (i + 0.5f) * o.AbdX * 1.1f / o.Stripes;
                float h = o.AbdY * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((xx - ax) / o.AbdX, 2f))) * 0.92f;
                HiveStroke(f.P(xx, -h), f.P(xx, h), Mathf.Max(1.1f, 1.5f * f.K), stripe, f.K * 3f);
            }
            if (glowAmt > 0.01f)  // the lobber's swelling honey
                HiveEllipse(f, ax, 0f, o.AbdX * 0.6f, o.AbdY * 0.6f, 0f, o.GlowCol.WithAlpha(a * glowAmt), 0.7f, Mathf.Max(1f, f.K), f.K * 8f);
            // thorax, armour
            HiveEllipse(f, 0f, 0f, tr, tr, 0f, col, 0.3f, lw, gl, 16);
            if (o.Armor)
            {
                if (plated)  // charging: the plates close up, filled and doubled (the armour that halves the damage)
                {
                    var steel = col.Mix(Rgba.White, 0.55f).WithAlpha(a);
                    HiveHexRing(f, 0f, 0f, tr + 2.6f, 0f, steel, 0.4f, Mathf.Max(1.4f, 1.8f * f.K), f.K * 7f);
                    HiveHexRing(f, 0f, 0f, tr + 5f, MathX.Pi / 6f, steel, 0.1f, Mathf.Max(1.1f, 1.3f * f.K), f.K * 5f);
                }
                else
                    HiveHexRing(f, 0f, 0f, tr + 2.6f, 0f, col, 0f, Mathf.Max(1.1f, 1.4f * f.K), f.K * 5f);
            }
            if (o.Fuzz > 0)
            {
                for (int k = 0; k < o.Fuzz; k++)
                {
                    float an = k * MathX.Tau / o.Fuzz, j = 1f + 0.25f * Mathf.Sin(t * 9f + k * 1.7f), rr = (k % 2 == 1 ? tr + 3.5f : tr + 5f) * j;
                    HiveStroke(f.P(Mathf.Cos(an) * tr * 0.9f, Mathf.Sin(an) * tr * 0.9f), f.P(Mathf.Cos(an) * rr, Mathf.Sin(an) * rr), Mathf.Max(1f, 1.1f * f.K), col, f.K * 2f);
                }
                for (int k = 0; k < o.Fuzz; k++)
                {
                    float an = k * MathX.Tau / o.Fuzz + 0.1f, j = 1f + 0.25f * Mathf.Sin(t * 8f + k * 2.1f);
                    HiveStroke(f.P(ax + Mathf.Cos(an) * o.AbdX * 0.9f, Mathf.Sin(an) * o.AbdY * 0.9f), f.P(ax + Mathf.Cos(an) * (o.AbdX + 2.5f * j), Mathf.Sin(an) * (o.AbdY + 3.5f * j)), Mathf.Max(1f, f.K), col, f.K * 2f);
                }
            }
            // head, eyes, jaws, lance
            HiveEllipse(f, hx, 0f, hr, hr, 0f, col, 0.35f, lw, gl, 14);
            Circle(f.P(hx + hr * 0.45f, -hr * 0.45f), Mathf.Max(0.8f, 0.75f * f.K), white, 6);
            Circle(f.P(hx + hr * 0.45f, hr * 0.45f), Mathf.Max(0.8f, 0.75f * f.K), white, 6);
            if (mand >= 0f)
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    V2 p0 = f.P(hx + hr * 0.8f, sd * hr * 0.35f), p1 = f.P(hx + hr + 5f, sd * (hr * 0.9f + mand * 4.5f)), p2 = f.P(hx + hr + 7f, sd * (1.2f + mand * 3.2f));
                    V2 prev = p0;
                    for (int i = 1; i <= 6; i++)
                    {
                        float u = i / 6f;
                        V2 q = p0 * ((1f - u) * (1f - u)) + p1 * (2f * u * (1f - u)) + p2 * (u * u);
                        HiveStroke(prev, q, Mathf.Max(1.2f, 1.8f * f.K), col, f.K * 3f);
                        prev = q;
                    }
                }
            if (o.Lance > 0f)
            {
                var lc = o.LanceCol.Mix(Rgba.White, hit).WithAlpha(a);
                HiveStroke(f.P(hx + hr, 0f), f.P(hx + hr + o.Lance, 0f), Mathf.Max(1.1f, 1.6f * f.K), lc, f.K * 4f);
                V2 tip = f.P(hx + hr + o.Lance, 0f);
                Circle(tip, Mathf.Max(1.2f, 1.6f * f.K) + 1.5f, o.Tip.WithAlpha(a * 0.25f), 10);
                Circle(tip, Mathf.Max(1.2f, 1.6f * f.K), o.Tip.WithAlpha(a), 8);
            }
        }

        /// Where a lance ends in the world (the Stinger's crackle, its dart's muzzle).
        static V2 HiveLanceTip(HiveSpec o, V2 pos, float ang, float r)
        {
            float k = o.W(r) * o.S, hx = o.Th * 0.95f + o.Head * 0.5f;
            return pos + V2.FromAngle(ang) * ((hx + o.Head + o.Lance) * k);
        }

        float HiveNearestPlayer(V2 pos)
        {
            float best = 1e9f;
            foreach (var p in game.Players.Values)
                if (!p.Downed)
                    best = Mathf.Min(best, p.Disp.DistanceTo(pos));
            return best;
        }

        /// The Hive enemy's body, in place of the usual polygon (DrawEnemy): the bee, and the Comb Shield's plate.
        void DrawHiveBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var o = HiveSpecOf(e.Type);
            float a = col.A, hit = (e.Flash > 0f || JuiceFlash(e)) ? HitFlashMix(e) : 0f;
            float mand = -1f, glow = 0f;
            if (e.Type == EnemyType.Soldier)
            {
                float d = HiveNearestPlayer(pos);
                mand = d < r * 3.4f ? 0.5f + 0.5f * Mathf.Sin(t * 28f) : 0.2f + 0.5f * Mathf.Clamp01(1f - d / 240f);  // jaws gape on the approach, snap at the end
            }
            if (e.Type == EnemyType.HoneyLobber && GasLobOf(e, out float age, out float throwAt, out float flight))
                glow = age < throwAt ? 0.2f + 0.6f * Mathf.Clamp01(age / Mathf.Max(0.01f, throwAt)) : Mathf.Max(0f, 0.8f - (age - throwAt) / Mathf.Max(0.01f, flight) * 2f);
            DrawHiveInsect(o, pos, e.Facing, r, t, a, hit, mand, glow, Game.SoldierCharging(e));
            if (e.Type == EnemyType.CombShield)
                DrawCombPlate(e, o, pos, r, t, a, hit);
        }

        static readonly int[] CombLoss = { 1, 6, 2, 5, 3, 4, 0 };  // the cell facing you goes first, the middle one last

        /// The Comb Shield's plate: seven wax cells in front of it. Each wear step takes two cells (the next one shows cracks), a
        /// bash wind-up flashes it, a lunge pushes it out, held low it's dimmer and off to the side; broken, it's gone.
        void DrawCombPlate(Enemy e, HiveSpec o, V2 pos, float r, float t, float a, float hit)
        {
            byte st = Game.RiotState(e), crack = Game.RiotCrack(e);
            if (crack >= Game.RiotCracked)
                return;
            float W = o.W(r), R = 8f;
            var f = new HiveFrame { O = pos, F = V2.FromAngle(e.Facing), S = V2.FromAngle(e.Facing).Orthogonal(), K = W };
            bool low = st == Game.RiotLow, flash = st == Game.RiotWinding;
            float push = st == Game.RiotLunging ? 14f : 0f, sx = 30f + push, sy = low ? 14f : 0f;
            int lost = Mathf.Min(7, crack * 2);
            float lw = Mathf.Max(1.2f, 1.7f * W);
            for (int i = 0; i < 7; i++)
            {
                int cell = i;  // (order of loss: CombLoss)
                int rank = System.Array.IndexOf(CombLoss, cell);
                if (rank < lost)
                    continue;
                float cx = 0f, cy = 0f;
                if (cell > 0)
                {
                    float an = Mathf.PI / 6f + (cell - 1) * Mathf.PI / 3f;
                    cx = Mathf.Cos(an) * R * 1.74f;
                    cy = Mathf.Sin(an) * R * 1.74f;
                }
                bool cracked = crack > 0 && rank == lost;
                Rgba c = (flash && Mathf.Floor(t * 14f) % 2f == 0f) || st == Game.RiotLunging ? HiveWhite : HiveWax;
                if (hit > 0f)
                    c = c.Mix(Rgba.White, hit);
                c = c.WithAlpha(a * (low ? 0.5f : cracked ? 0.65f : 1f));
                HiveHexRing(f, sx + cx, sy + cy, R - 0.6f, 0f, c, 0.18f + (flash ? 0.1f : 0f), lw, flash ? W * 12f : W * 7f);
                if (cracked)
                {
                    HiveStroke(f.P(sx + cx - 3f, sy + cy - 2f), f.P(sx + cx + 1f, sy + cy + 1f), Mathf.Max(1f, W), c, 0f);
                    HiveStroke(f.P(sx + cx + 1f, sy + cy + 1f), f.P(sx + cx + 4f, sy + cy + 4f), Mathf.Max(1f, W), c, 0f);
                }
            }
        }

        /// Over the body: the Stinger's lance crackling while it charges, the Needler's magazine.
        void DrawHiveDetails(Enemy e, V2 pos, float r, float t)
        {
            var o = HiveSpecOf(e.Type);
            if (e.Type == EnemyType.Stinger && e.Winding)
            {
                V2 tip = HiveLanceTip(o, pos, e.Facing, r);
                V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
                float W = o.W(r);
                for (int k = 0; k < 3; k++)
                {
                    float n = Mathf.Floor(t * 18f) + k * 7f;
                    V2 end = tip + f * ((2f + OrbHash(n) * 8f) * W) + s * ((OrbHash(n + 9f) - 0.5f) * 14f * W);
                    HiveStroke(tip + f * (2f * W), end, Mathf.Max(1f, 1.1f * W), HiveWhite, W * 3f);
                }
            }
            if (e.Type == EnemyType.Needler)
            {
                float W = o.W(r);
                V2 at = pos + new V2(-16f * W, -26f * W);
                for (int i = 0; i < 7; i++)
                {
                    V2 c = at + new V2(i * 5.4f * W, 0f);
                    float rr = Mathf.Max(1.6f, 1.8f * W);
                    var pale = Hex(0xffec9a);
                    if (i < e.Phase)
                    {
                        Circle(c, rr + 1.5f, pale.WithAlpha(0.25f), 8);
                        Circle(c, rr, pale, 8);
                    }
                    else
                        Arc(c, rr, 1f, pale.WithAlpha(0.8f), 8);
                }
            }
        }

        /// The Stinger's venom dart (Defs.StingBolt): a lime zigzag with two white barbs at its tip, flickering sixteen times a second.
        void DrawStingBolt(Bullet b, float t)
        {
            V2 u = b.Vel.Normalized(), n = u.Orthogonal();
            float fz = Mathf.Floor(t * 16f) % 2f == 0f ? 1f : -1f, W = 2.2f;
            System.Func<float, float, V2> Q = (x, y) => b.Pos + u * (x * W) + n * (y * W);
            var lime = Hex(0xc8ff3d);
            V2[] zig = { Q(-14f, 0f), Q(-9f, 4f * fz), Q(-4f, -4f * fz), Q(1f, 3f * fz), Q(8f, 0f) };
            Circle(Q(2f, 0f), 11f, lime.WithAlpha(0.16f), 14);
            for (int i = 0; i < 4; i++)
                HiveStroke(zig[i], zig[i + 1], 2.4f, lime, 10f);
            HiveStroke(zig[4], Q(3f, -5f), 1.6f, HiveWhite, 6f);
            HiveStroke(zig[4], Q(3f, 5f), 1.6f, HiveWhite, 6f);
        }

        /// A Honey Lobber's lob (FxKind.GasLob): a red ring on the floor that fills as the wind-up goes, then a honey glob
        /// flying in an arc to it, with a short trail. Drawn as in the mockup.
        void DrawHoneyLobFx(Enemy e, Effect fx, float age, float throwAt, float flight, V2 land, float R)
        {
            var red = Hex(0xff5a3c);
            var amber = Hex(0xffc533);
            float tellP = throwAt > 0f ? Mathf.Clamp01(age / Mathf.Max(0.01f, throwAt)) : 1f;
            if (age < throwAt + flight)
            {
                Circle(land, R, red.WithAlpha(0.06f), 32);
                Arc(land, R, 2f, red.WithAlpha(0.9f), 32);
                if (age < throwAt)
                    Arc(land, R, 4f, red, Mathf.Max(3, (int)(32 * tellP)), -MathX.Pi / 2f, MathX.Tau * tellP);
            }
            if (age >= throwAt && age < throwAt + flight)
            {
                float u = (age - throwAt) / flight;
                V2 from = e.Disp + V2.FromAngle(e.Facing) * (e.Radius * 1.5f);
                float arc = 46f * 1.4f;
                for (int k = 4; k >= 1; k--)
                {
                    float ut = Mathf.Max(0f, u - k * 0.04f);
                    V2 q = V2.Lerp(from, land, ut) - new V2(0f, arc * Mathf.Sin(Mathf.PI * ut));
                    Circle(q, Mathf.Max(0.8f, 3.5f - k * 0.5f) * 1.4f, Hex(0xffa21a).WithAlpha(0.4f - k * 0.08f), 8);
                }
                V2 c = V2.Lerp(from, land, u) - new V2(0f, arc * Mathf.Sin(Mathf.PI * u));
                Circle(c, 8.5f, amber.WithAlpha(0.2f), 12);
                Circle(c, 6.3f, amber.WithAlpha(0.7f), 12);
                Arc(c, 6.3f, 2f, amber, 12);
            }
        }
    }
}