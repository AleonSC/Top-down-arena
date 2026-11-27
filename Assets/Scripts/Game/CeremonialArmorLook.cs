using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Ceremonial Armor (Core/CeremonialArmor.cs), the "Mandala" look of Docs/Mockups/ceremonial-armor.html at CereK times its
    /// size: lacquered plates (crimson rows, a gold crown facing out, studs, blue silk lacing, a glint) on two dashed gold rings.
    /// A summon draws a gold sigil at your feet and a beam, and the plate drops in spinning, then clicks in with a ring and
    /// sparks; the others ease to their new spacing. Hits flash a plate white; it cracks at 2/3 and 1/3 health and shatters
    /// into shards. All ten up: a pulsing gold aura. This screen runs its own clock, eased toward the host's.
    public partial class ArenaApp
    {
        const float CereK = 2.2f;  // mockup px -> arena px
        static readonly Rgba CereLacquer = C255(194, 54, 43), CereLacquerDk = C255(126, 31, 25), CereGold = C255(217, 164, 65),
            CereGoldLt = C255(243, 213, 138), CereLace = C255(127, 176, 210), CereEdge = C255(27, 15, 14), CereCrack = C255(244, 233, 216);

        class CereView
        {
            public float Clock, Seen = -1f, Age, LandAt = -9f;
            public int N, Was;
            public V2 LandPos;
            public readonly float[] A = new float[Game.CerePlateMax], R = new float[Game.CerePlateMax];
            public readonly float[] OffA = new float[Game.CerePlateMax], OffR = new float[Game.CerePlateMax];
        }

        readonly Dictionary<int, CereView> cereViews = new Dictionary<int, CereView>();

        static float CereBack(float x) { const float c = 1.9f; float u = x - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        CereView CereViewOf(Player p, float dt, float t)
        {
            CereView v;
            if (!cereViews.TryGetValue(p.Id, out v))
                cereViews[p.Id] = v = new CereView { Clock = p.PlateClock, N = p.PlateN, Was = p.PlateNewT > 0f ? 1 : 0 };
            if (p.PlateClock != v.Seen)
            {
                v.Seen = p.PlateClock;
                v.Age = 0f;
            }
            else
                v.Age += dt;
            float est = p.PlateClock + Mathf.Min(v.Age, 0.25f);
            v.Clock = Mathf.Abs(est - v.Clock) > 0.25f ? est : v.Clock + dt + (est - v.Clock) * Mathf.Min(1f, 8f * dt);
            int n = p.PlateN;
            if (n != v.N)  // the count changed: everyone keeps where they're drawn and eases to the new spacing
            {
                for (int i = 0; i < n; i++)
                {
                    float a, r;
                    Game.CereSlot(i, n, v.Clock, out a, out r);
                    bool fresh = i >= v.N;
                    v.OffA[i] = fresh ? 0f : MathX.WrapPi(v.A[i] - a);
                    v.OffR[i] = fresh ? 0f : v.R[i] - r;
                }
                v.N = n;
            }
            float decay = Mathf.Exp(-7f * dt);
            for (int i = 0; i < n; i++)
            {
                float a, r;
                Game.CereSlot(i, n, v.Clock, out a, out r);
                v.OffA[i] *= decay;
                v.OffR[i] *= decay;
                v.A[i] = a + v.OffA[i];
                v.R[i] = r + v.OffR[i];
            }
            int spawning = p.PlateNewT > 0f ? 1 : 0;
            if (v.Was == 1 && spawning == 0 && n > 0)  // the newest plate just landed
            {
                v.LandAt = t;
                v.LandPos = V2.FromAngle(v.A[n - 1]) * v.R[n - 1];
            }
            v.Was = spawning;
            return v;
        }

        /// Under the player model: the aura, the rings and the sigil. Over it: the plates (DrawCeremonialArmor).
        void DrawCeremonialFloor(Player p, float t)
        {
            if (p.PlateN <= 0)
            {
                cereViews.Remove(p.Id);
                return;
            }
            CereViewOf(p, Time.deltaTime, t);  // (keeps the armour's view state ticking)
            V2 c = p.Disp;
            if (Game.CereFull(p))  // full regalia
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
                GlowDisc(c, 52f * CereK, CereGoldLt, 0.22f + pulse * 0.14f);
            }
            int dashes = 28;
            for (int ring = 0; ring < 2; ring++)  // the dashed guides
            {
                if (ring == 1 && p.PlateN <= Game.CereInnerMax)
                    break;
                float rr = ring == 0 ? Game.CereInnerR : Game.CereOuterR;
                for (int k = 0; k < dashes; k++)
                    Arc(c, rr, 1.6f, CereGold.WithAlpha(0.16f), 3, k * MathX.Tau / dashes, MathX.Tau / dashes / 3f);
            }
            if (p.PlateNewT > 0f)  // the summoning sigil
            {
                float since = Game.CereSummonTime - p.PlateNewT, life = 0.75f, k = since / life;
                if (k < 1f)
                    DrawCereSigil(c + new V2(0f, 4f * CereK), k, t);
            }
        }

        void DrawCereSigil(V2 c, float k, float t)
        {
            float r = (6f + CereBack(Mathf.Min(1f, k * 1.6f)) * 14f) * CereK, a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            const float sq = 0.55f;
            for (int ring = 0; ring < 2; ring++)
            {
                float rr = ring == 0 ? r : r * 0.62f;
                V2 prev = c + new V2(rr, 0f);
                for (int i = 1; i <= 28; i++)
                {
                    float an = MathX.Tau * i / 28f;
                    V2 next = c + new V2(Mathf.Cos(an) * rr, Mathf.Sin(an) * rr * sq);
                    Line(prev, next, 2f, CereGold.WithAlpha(a));
                    prev = next;
                }
            }
            for (int i = 0; i < 8; i++)  // the turning spokes
            {
                float an = t * 3f + (i + 1) * MathX.Tau / 8f;
                V2 d = new V2(Mathf.Cos(an), Mathf.Sin(an) * sq);
                Line(c + d * (r * 0.62f), c + d * r, 2f * CereK * 0.9f, (i % 2 == 1 ? CereGoldLt : CereGold).WithAlpha(a));
            }
        }

        /// Over the player model: the plates (back ones first), the drop-in beam, hit flashes, landing sparks.
        void DrawCeremonialArmor(Player p, float t)
        {
            CereView v;
            if (p.PlateN <= 0 || !cereViews.TryGetValue(p.Id, out v))
                return;
            V2 c = p.Disp;
            int n = Mathf.Min(p.PlateN, Game.CerePlateMax);
            var order = cereOrder;
            order.Clear();
            for (int i = 0; i < n; i++)
                order.Add(i);
            cereBackFirst.A = v.A;
            order.Sort(cereBackFirst);
            foreach (int i in order)
            {
                float spawn = i == n - 1 && p.PlateNewT > 0f ? 1f - p.PlateNewT / Game.CereSummonTime : 1f;
                V2 slot = c + V2.FromAngle(v.A[i]) * v.R[i];
                float flash = 0f;
                foreach (var fx in FxOf(FxKind.CereBlock))
                    if (fx.Kind == FxKind.CereBlock && fx.T < 0.12f && (fx.Pos2 - p.Pos + c).DistanceTo(slot) < 28f)
                        flash = Mathf.Max(flash, 1f - fx.T / 0.12f);
                float frac = p.PlateMax > 0f ? p.PlateHp[i] / p.PlateMax : 1f;
                DrawCerePlate(slot, v.A[i], spawn, frac, flash, i * 1.7f + p.Id, t);
            }
            float since = t - v.LandAt;  // the newest plate clicking in
            if (since >= 0f && since < 0.35f)
            {
                float k = since / 0.35f;
                V2 at = c + v.LandPos;
                Arc(at, (3f + k * 10f) * CereK, 2f, CereGoldLt.WithAlpha(1f - k), 20);
                if (!fxReduced)
                    for (int s = 0; s < 8; s++)
                    {
                        float an = SmokeHash(s + v.LandAt) * MathX.Tau, sp = 45f * CereK * (0.4f + SmokeHash(s * 3f + v.LandAt) * 0.8f);
                        V2 q = at + V2.FromAngle(an) * (sp * (1f - Mathf.Exp(-4.3f * since)) / 4.3f);
                        FillRect(q.X - 1.5f, q.Y - 1.5f, 3f, 3f, (s % 2 == 0 ? CereGoldLt : CereGold).WithAlpha(1f - k));
                    }
            }
        }

        readonly List<int> cereOrder = new List<int>();
        readonly PlatesBackFirst cereBackFirst = new PlatesBackFirst();

        /// Orders plate indices back ones first (by the sine of their angle), set to one player's angles before a sort.
        sealed class PlatesBackFirst : IComparer<int>
        {
            public float[] A;
            public int Compare(int x, int y) => Mathf.Sin(A[x]).CompareTo(Mathf.Sin(A[y]));
        }

        /// One plate at `slot`, its crown facing out along `ang`. spawn < 1: still dropping in.
        void DrawCerePlate(V2 slot, float ang, float spawn, float hpFrac, float flash, float seed, float t)
        {
            float e = CereBack(spawn), keep = glAlpha;
            V2 at = slot - new V2(0f, (1f - e) * 46f * CereK);
            if (spawn < 1f)
            {
                FillRect(at.X - 1f * CereK, at.Y - 220f * CereK, 2f * CereK, 220f * CereK, CereGoldLt.WithAlpha(0.25f * (1f - spawn)));  // the beam
                glAlpha = keep * Mathf.Min(1f, spawn * 2.5f);
            }
            float rot = ang + MathX.Pi * 0.5f + (1f - spawn) * MathX.Tau * 1.5f, s = CereK * (1f + (1f - e) * 0.6f);
            V2 ax = V2.FromAngle(rot) * s, ay = V2.FromAngle(rot + MathX.Pi * 0.5f) * s;
            at += ay * (Mathf.Sin(t * 4f + seed) * 0.4f);  // hover
            CereBox(at, ax, ay, -5.5f, -4.5f, 11f, 9f, CereEdge);
            CereBox(at, ax, ay, -4.5f, -3.5f, 9f, 7f, CereLacquer);
            CereBox(at, ax, ay, -4.5f, -1f, 9f, 0.8f, CereLacquerDk);   // the lamellar rows
            CereBox(at, ax, ay, -4.5f, 1.5f, 9f, 0.8f, CereLacquerDk);
            CereBox(at, ax, ay, -4.5f, -3.5f, 9f, 1.4f, CereGold);      // the gold crown, facing out
            CereBox(at, ax, ay, -4.5f, -3.5f, 1.2f, 1.2f, CereGoldLt);  // studs
            CereBox(at, ax, ay, 3.3f, -3.5f, 1.2f, 1.2f, CereGoldLt);
            for (int lx = -3; lx <= 3; lx += 2)  // silk lacing
            {
                CereBox(at, ax, ay, lx - 0.4f, -0.6f, 0.8f, 0.8f, CereLace);
                CereBox(at, ax, ay, lx - 0.4f, 2f, 0.8f, 0.8f, CereLace);
            }
            float g = MathX.Fmod(t * 0.7f + seed, 2.4f) - 0.6f;  // the glint sweeping across
            if (g > -0.5f && g < 0.5f && !fxReduced)
                CereBox(at, ax, ay, g * 10f - 0.5f, -3.5f, 1f, 7f, Rgba.White.WithAlpha(0.55f));
            if (hpFrac < 0.67f)  // cracks
            {
                CereSeg(at, ax, ay, -2f, -3.5f, -0.5f, -0.5f);
                CereSeg(at, ax, ay, -0.5f, -0.5f, -1.8f, 3.5f);
            }
            if (hpFrac < 0.34f)
            {
                CereSeg(at, ax, ay, 2.5f, -3.5f, 1f, 0.5f);
                CereSeg(at, ax, ay, 1f, 0.5f, 3f, 3.5f);
                CereSeg(at, ax, ay, -0.5f, -0.5f, 1f, 0.5f);
            }
            if (flash > 0f)
                CereBox(at, ax, ay, -5.5f, -4.5f, 11f, 9f, Rgba.White.WithAlpha(flash));
            glAlpha = keep;
        }

        void CereBox(V2 o, V2 ax, V2 ay, float x, float y, float w, float h, Rgba col) =>
            Quad(o + ax * x + ay * y, o + ax * (x + w) + ay * y, o + ax * (x + w) + ay * (y + h), o + ax * x + ay * (y + h), col);

        void CereSeg(V2 o, V2 ax, V2 ay, float x0, float y0, float x1, float y1) =>
            Line(o + ax * x0 + ay * y0, o + ax * x1 + ay * y1, 0.6f * CereK, CereCrack);

        /// A plate stopping a shot: gold and white sparks.
        void DrawCereBlockFx(Effect fx)
        {
            if (fxReduced || fx.T > 0.25f)
                return;
            float k = fx.T / 0.25f, seed = fx.Pos.X * 0.13f + fx.Pos.Y * 0.07f;
            for (int s = 0; s < 5; s++)
            {
                float an = SmokeHash(seed + s) * MathX.Tau, sp = 40f * CereK * (0.4f + SmokeHash(seed + s * 3f) * 0.8f);
                V2 q = fx.Pos + V2.FromAngle(an) * (sp * (1f - Mathf.Exp(-4.3f * fx.T)) / 4.3f);
                FillRect(q.X - 1.5f, q.Y - 1.5f, 3f, 3f, (s % 2 == 0 ? CereGoldLt : Rgba.White).WithAlpha(1f - k));
            }
        }

        /// A plate shattering: a white flash, lacquer and gold shards flung outward (and the mastered burst's ring).
        void DrawCereBreakFx(Effect fx)
        {
            float age = fx.T;
            if (age < 0.3f)
            {
                float k = age / 0.3f;
                Circle(fx.Pos, (4f + k * 6f) * CereK, Rgba.White.WithAlpha(0.8f * (1f - k)), 16);
            }
            if (fx.Radius > 0f && age < 0.45f)  // the capstone's burst
            {
                float k = age / 0.45f;
                GlowDisc(fx.Pos, fx.Radius * (0.5f + 0.5f * k), CereGoldLt, 0.5f * (1f - k));
                Arc(fx.Pos, fx.Radius * (0.4f + 0.6f * k), 3f, CereLacquer.WithAlpha(1f - k), 32);
            }
            float baseAng = (fx.Pos2 - fx.Pos).Angle, seed = fx.Pos.X * 0.11f + fx.Pos.Y * 0.05f;
            int shards = fxReduced ? 4 : 7;
            for (int s = 0; s < shards; s++)
            {
                float life = 0.7f + SmokeHash(seed + s * 5f) * 0.4f;
                if (age > life)
                    continue;
                float d = baseAng + (SmokeHash(seed + s) - 0.5f) * 2.2f, sp = (30f + SmokeHash(seed + s * 2f) * 50f) * CereK;
                V2 q = fx.Pos + V2.FromAngle(d) * (sp * (1f - Mathf.Exp(-4.3f * age)) / 4.3f);
                float rot = SmokeHash(seed + s * 3f) * MathX.Tau + (SmokeHash(seed + s * 4f) - 0.5f) * 18f * age;
                V2 ux = V2.FromAngle(rot) * CereK, uy = V2.FromAngle(rot + MathX.Pi * 0.5f) * CereK;
                var col = (s % 3 != 0 ? CereLacquer : CereGold).WithAlpha(1f - age / life);
                SetColor(col);
                Tri(q + ux * -2f + uy * -1.5f, q + ux * 2.5f, q + ux * -1f + uy * 2f);
            }
        }
    }
}
