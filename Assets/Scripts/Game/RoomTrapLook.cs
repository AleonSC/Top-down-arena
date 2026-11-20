using UnityEngine;

namespace Arena
{
    /// A room's traps on the floor (RoomTraps.cs): a turret in hostile red (a plate, a twin-barrelled head that tracks the nearest player),
    /// a spike plate (a square with a field of steel points) and a slow field (a violet square with squares drifting in); a shot-up turret is gone. Hidden while their room is dark.
    public partial class ArenaApp
    {
        static readonly Rgba RoomTrapRed = new Rgba(1f, 0.3f, 0.34f), RoomTrapSteel = new Rgba(0.82f, 0.85f, 0.92f), RoomTrapViolet = new Rgba(0.62f, 0.42f, 1f);

        public static int PreviewTrapLook;  // the preview's TRAPLOOK=<n>: 0 the current look, 1-3 the rework mockups
        public static float PreviewTrapPhase;  // TRAPPH=<s>: added to the mockups' clock

        void DrawRoomTraps(float t)
        {
            if (!game.ExpandingOn)
                return;
            foreach (var tr in game.RoomTrapList)
            {
                if (game.FogHidesStill(tr.Pos))  // (a trap you've seen stays shown)
                    continue;
                if (tr.Kind == TrapKind.Spike) { if (PreviewTrapLook > 0) DrawSpikeMock(tr.Pos, t, PreviewTrapLook); else DrawSpikePlate(tr.Pos, t); }
                else if (tr.Kind == TrapKind.Slow) { if (PreviewTrapLook > 0) DrawSlowMock(tr.Pos, t, PreviewTrapLook); else DrawSlowField(tr.Pos, t); }
                else DrawTrapTurret(tr.Pos, t);
            }
        }

        void DrawSpikePlate(V2 c, float t)
        {
            float h = RoomTraps.SpikeHalf;
            FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, new Rgba(0.08f, 0.04f, 0.05f, 0.88f));
            NodeFrame(c.X - h, c.Y - h, h * 2f, h * 2f, 2.5f, RoomTrapRed.WithAlpha(0.85f));
            for (int i = -1; i <= 1; i++)  // a 3 x 3 field of steel points
                for (int j = -1; j <= 1; j++)
                {
                    V2 p = c + new V2(i, j) * (h * 0.58f);
                    Tri3(p + new V2(0f, -9f), p + new V2(-6f, 6f), p + new V2(6f, 6f), RoomTrapSteel.WithAlpha(0.9f));
                }
        }

        void DrawSlowField(V2 c, float t)
        {
            float h = RoomTraps.SlowHalf;
            FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, RoomTrapViolet.WithAlpha(0.18f));
            NodeFrame(c.X - h, c.Y - h, h * 2f, h * 2f, 2f, RoomTrapViolet.WithAlpha(0.7f));
            for (int k = 0; k < 3; k++)  // squares drifting inward
            {
                float ph = (t * 0.5f + k / 3f) % 1f, q = h * (1f - ph);
                NodeFrame(c.X - q, c.Y - q, q * 2f, q * 2f, 1.5f, RoomTrapViolet.WithAlpha(0.5f * (1f - ph)));
            }
        }

        void DrawTrapTurret(V2 home, float t)
        {
            float hp = game.TurretHpFrac(home), ago = game.TurretHitAgo(home);
            float hit = Mathf.Clamp01(1f - ago / 0.2f);  // 1 at the moment of a hit, gone in 0.2 s
            V2 c = home + new V2(Mathf.Sin(ago * 95f + home.X), Mathf.Cos(ago * 83f + home.Y)) * (3.5f * hit);  // it shudders
            float aim = t * 0.5f + home.X * 0.01f, best = RoomTraps.TurretRange * RoomTraps.TurretRange;
            bool locked = false;
            foreach (var p in game.Players.Values)
            {
                float d = (p.Pos - c).LengthSq;
                if (!p.Downed && d < best) { best = d; aim = (p.Pos - c).Angle; locked = true; }
            }
            for (int k = 0; k < 4; k++)
            {
                var f = c + V2.FromAngle(MathX.Tau / 8f + k * MathX.Tau / 4f) * 27f;
                FillRect(f.X - 6f, f.Y - 6f, 12f, 12f, RoomTrapRed.WithAlpha(0.3f));
                NodeFrame(f.X - 6f, f.Y - 6f, 12f, 12f, 2f, RoomTrapRed.WithAlpha(0.8f));
            }
            Circle(c, 25f, new Rgba(0.1f, 0.03f, 0.04f, 0.95f), 24);
            Arc(c, 25f, 3f, RoomTrapRed.Mix(new Rgba(1f, 1f, 1f), hit).WithAlpha(0.95f), 24);
            var dir = V2.FromAngle(aim);
            var side = new V2(-dir.Y, dir.X);
            for (int s = -1; s <= 1; s += 2)
                Line(c + side * (4.5f * s) + dir * 6f, c + side * (4.5f * s) + dir * 32f, 4f, RoomTrapRed.WithAlpha(0.95f));
            Circle(c, 12f, RoomTrapRed.WithAlpha(0.3f), 16);
            Arc(c, 12f, 2.5f, RoomTrapRed, 16);
            if (hit > 0f)  // a white flash over the plate
                Circle(c, 25f, new Rgba(1f, 1f, 1f, 0.55f * hit), 24);

            // Damage shows on the plate: cracks, then smoke, then sparks and a glow of heat.
            if (hp < 0.66f)
            {
                Line(c + new V2(-14f, -9f), c + new V2(-3f, 1f), 1.5f, new Rgba(1f, 0.7f, 0.5f, 0.85f));
                Line(c + new V2(-3f, 1f), c + new V2(2f, 12f), 1.5f, new Rgba(1f, 0.7f, 0.5f, 0.85f));
            }
            if (hp < 0.33f)
            {
                Line(c + new V2(13f, -10f), c + new V2(5f, -1f), 1.5f, new Rgba(1f, 0.7f, 0.5f, 0.85f));
                Line(c + new V2(5f, -1f), c + new V2(9f, 11f), 1.5f, new Rgba(1f, 0.7f, 0.5f, 0.85f));
                Circle(c, 25f, new Rgba(1f, 0.35f, 0.1f, 0.12f + 0.1f * Mathf.Sin(t * 12f)), 24);
            }
            if (hp < 0.5f)
                for (int k = 0; k < 3; k++)  // smoke drifting up
                {
                    float ph = (t * 0.7f + k / 3f) % 1f;
                    Circle(c + new V2(Mathf.Sin(ph * 6f + k * 2f) * 5f, -20f - ph * 34f), 4f + ph * 8f, new Rgba(0.55f, 0.55f, 0.6f, 0.38f * (1f - ph)), 10);
                }
            if (ago < 0.35f)  // sparks fly off the hit
                for (int k = 0; k < 7; k++)
                {
                    float a = (home.X * 0.37f + k * 0.9f + (k % 2) * 2.1f) * 1.7f;
                    float run = 18f + ago * 190f * (0.6f + 0.15f * (k % 3));
                    Line(c + V2.FromAngle(a) * run, c + V2.FromAngle(a) * (run + 8f), 2f, new Rgba(1f, 0.8f, 0.4f, 1f - ago / 0.35f));
                }

            if (hp < 1f)
            {
                float flashT = hit;
                float bw = 46f, bh = 6f, bx = c.X - bw / 2f, by = c.Y - 46f;
                FillRect(bx - 1f, by - 1f, bw + 2f, bh + 2f, new Rgba(0.02f, 0.01f, 0.02f, 0.9f));
                FillRect(bx, by, bw * hp, bh, RoomTrapRed.Mix(new Rgba(1f, 1f, 1f), flashT).WithAlpha(0.95f));
                NodeFrame(bx - 1f, by - 1f, bw + 2f, bh + 2f, 1.5f, RoomTrapRed.WithAlpha(0.8f));
            }
            if (locked)
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(t * 20f);
                Circle(c + dir * 34f, 4f + 2f * flash, new Rgba(1f, 0.8f, 0.7f, 0.35f + 0.4f * flash), 10);
            }
        }

        // --- Rework mockups (TRAPLOOK=1-3) ---------------------------------------------------------------------------

        /// Corner brackets round a square: four glowing L's.
        void TrapBrackets(V2 c, float h, float len, float w, Rgba col)
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var k = c + new V2(sx * h, sy * h);
                    GlowLine(k, k + new V2(-sx * len, 0f), w, col);
                    GlowLine(k, k + new V2(0f, -sy * len), w, col);
                }
        }

        void DrawSpikeMock(V2 c, float t, int v)
        {
            t += PreviewTrapPhase;
            float h = RoomTraps.SpikeHalf;
            var hot = new Rgba(1f, 0.36f, 0.3f);
            float cyc = (t / RoomTraps.SpikeTick) % 1f, up = cyc < 0.35f ? Mathf.Sin(cyc / 0.35f * MathX.Pi) : 0f;  // the spikes jab on the tick
            if (v == 1)  // Vent: a recessed red-hatched grate, slots that jab steel points on the tick
            {
                FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, new Rgba(0.05f, 0.03f, 0.05f, 0.9f));
                HatchRect(c.X - h, c.Y - h, h * 2f, h * 2f, hot.WithAlpha(0.13f));
                NodeFrame(c.X - h, c.Y - h, h * 2f, h * 2f, 1f, hot.WithAlpha(0.35f));
                TrapBrackets(c, h, 13f, 2f, hot.WithAlpha(0.95f));
                for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                    {
                        V2 p = c + new V2(i, j) * (h * 0.56f);
                        FillRect(p.X - 7f, p.Y - 2.5f, 14f, 5f, new Rgba(0f, 0f, 0f, 0.9f));
                        FillRect(p.X - 7f, p.Y + 1.5f, 14f, 1f, hot.WithAlpha(0.4f + 0.6f * up));
                        float l = 2f + 9f * up;
                        if (l > 2.5f)
                            Tri3(p + new V2(0f, -l), p + new V2(-4f, 2f), p + new V2(4f, 2f), RoomTrapSteel.Mix(hot, 0.25f).WithAlpha(0.95f));
                    }
            }
            else if (v == 2)  // Wire: neon outlined spikes on a dark plate, glowing hotter on the tick
            {
                FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, hot.WithAlpha(0.07f + 0.06f * up));
                NeonLine(new V2(c.X - h, c.Y - h), new V2(c.X + h, c.Y - h), 1.5f, hot.WithAlpha(0.8f));
                NeonLine(new V2(c.X + h, c.Y - h), new V2(c.X + h, c.Y + h), 1.5f, hot.WithAlpha(0.8f));
                NeonLine(new V2(c.X + h, c.Y + h), new V2(c.X - h, c.Y + h), 1.5f, hot.WithAlpha(0.8f));
                NeonLine(new V2(c.X - h, c.Y + h), new V2(c.X - h, c.Y - h), 1.5f, hot.WithAlpha(0.8f));
                var ink = hot.Mix(new Rgba(1f, 0.85f, 0.75f), 0.3f * up).WithAlpha(0.6f + 0.4f * up);
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                    {
                        V2 p = c + new V2(-h + 8f + i * (h * 2f - 16f) / 3f, -h + 9f + j * (h * 2f - 18f) / 3f);
                        float l = 6f + 3f * up;
                        V2 a = p + new V2(0f, -l), b = p + new V2(-4.5f, 4f), d = p + new V2(4.5f, 4f);
                        GlowLine(a, b, 1.3f, ink); GlowLine(a, d, 1.3f, ink); GlowLine(b, d, 1.3f, ink);
                    }
            }
            else  // Warning plate: a hazard-striped rim, a core of studs that light in a wave toward the tick
            {
                float band = 9f;
                FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, new Rgba(0.05f, 0.03f, 0.05f, 0.9f));
                HatchRect(c.X - h, c.Y - h, h * 2f, band, hot.WithAlpha(0.55f));
                HatchRect(c.X - h, c.Y + h - band, h * 2f, band, hot.WithAlpha(0.55f));
                HatchRect(c.X - h, c.Y - h + band, band, h * 2f - band * 2f, hot.WithAlpha(0.55f));
                HatchRect(c.X + h - band, c.Y - h + band, band, h * 2f - band * 2f, hot.WithAlpha(0.55f));
                NodeFrame(c.X - h, c.Y - h, h * 2f, h * 2f, 1.5f, hot.WithAlpha(0.9f));
                NodeFrame(c.X - h + band, c.Y - h + band, h * 2f - band * 2f, h * 2f - band * 2f, 1f, hot.WithAlpha(0.45f));
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        V2 p = c + new V2(i - 1, j - 1) * 13f;
                        float lit = Mathf.Clamp01(1f - Mathf.Abs(cyc * 3f - (i + j) * 0.5f));
                        float r = 3.5f + 1.5f * lit;
                        var col = RoomTrapSteel.Mix(hot, 0.6f * lit).WithAlpha(0.55f + 0.45f * lit);
                        Tri3(p + new V2(0f, -r), p + new V2(r, 0f), p + new V2(0f, r), col);
                        Tri3(p + new V2(0f, -r), p + new V2(-r, 0f), p + new V2(0f, r), col);
                    }
            }
        }

        void DrawSlowMock(V2 c, float t, int v)
        {
            t += PreviewTrapPhase;
            float h = RoomTraps.SlowHalf;
            var vi = RoomTrapViolet;
            if (v == 1)  // Gravity well: a grid sagging into the centre, squares sinking in
            {
                FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, vi.WithAlpha(0.08f));
                const int n = 6;
                for (int a = 1; a < n; a++)
                    for (int s = 0; s < n; s++)
                        for (int axis = 0; axis < 2; axis++)
                        {
                            float u = -1f + 2f * a / n, w0 = -1f + 2f * s / n, w1 = -1f + 2f * (s + 1) / n;
                            V2 p0 = Sag(axis == 0 ? new V2(u, w0) : new V2(w0, u)), p1 = Sag(axis == 0 ? new V2(u, w1) : new V2(w1, u));
                            Line(c + p0 * h, c + p1 * h, 1.2f, vi.WithAlpha(0.35f));
                        }
                for (int k = 0; k < 3; k++)
                {
                    float ph = (t * 0.35f + k / 3f) % 1f, q = h * (1f - ph) * 0.9f;
                    var ca = vi.WithAlpha(0.7f * Mathf.Sin(ph * MathX.Pi));
                    NodeFrame(c.X - q, c.Y - q, q * 2f, q * 2f, 1.5f, ca);
                }
                NodeFrame(c.X - h, c.Y - h, h * 2f, h * 2f, 1f, vi.WithAlpha(0.4f));
                TrapBrackets(c, h, 16f, 2f, vi.WithAlpha(0.95f));
                Circle(c, 4f, vi.Mix(new Rgba(1f, 1f, 1f), 0.5f).WithAlpha(0.8f), 10);
            }
            else if (v == 2)  // Static field: scanlines crawling down, chevrons on each side pointing in
            {
                FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, vi.WithAlpha(0.12f));
                float off = (t * 6f) % 4f;
                for (float y = -h + off; y < h; y += 4f)
                    FillRect(c.X - h, c.Y + y, h * 2f, 1f, vi.WithAlpha(0.16f));
                NeonLine(new V2(c.X - h, c.Y - h), new V2(c.X + h, c.Y - h), 1.2f, vi.WithAlpha(0.7f));
                NeonLine(new V2(c.X + h, c.Y - h), new V2(c.X + h, c.Y + h), 1.2f, vi.WithAlpha(0.7f));
                NeonLine(new V2(c.X + h, c.Y + h), new V2(c.X - h, c.Y + h), 1.2f, vi.WithAlpha(0.7f));
                NeonLine(new V2(c.X - h, c.Y + h), new V2(c.X - h, c.Y - h), 1.2f, vi.WithAlpha(0.7f));
                for (int sd = 0; sd < 4; sd++)
                {
                    V2 dir = V2.FromAngle(sd * MathX.Tau / 4f), side = new V2(-dir.Y, dir.X);
                    for (int k = 0; k < 2; k++)
                    {
                        float ph = (t * 0.8f + k * 0.5f) % 1f;
                        V2 tip = c - dir * (h - 8f - ph * 18f);
                        var col = vi.WithAlpha(0.9f * Mathf.Sin(ph * MathX.Pi));
                        GlowLine(tip - dir * 6f + side * 7f, tip, 1.8f, col);
                        GlowLine(tip - dir * 6f - side * 7f, tip, 1.8f, col);
                    }
                }
            }
            else  // Ripple: slow rings spreading out like tar, a bright core, corner brackets
            {
                FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, vi.WithAlpha(0.1f));
                for (int k = 0; k < 3; k++)
                {
                    float ph = (t * 0.3f + k / 3f) % 1f, r = 6f + ph * (h - 8f);
                    GlowArc(c, r, 1.5f, vi.WithAlpha(0.75f * (1f - ph)), 32);
                }
                GlowArc(c, 5f, 1.5f, vi.Mix(new Rgba(1f, 1f, 1f), 0.4f), 14);
                NodeFrame(c.X - h, c.Y - h, h * 2f, h * 2f, 1f, vi.WithAlpha(0.3f));
                TrapBrackets(c, h, 14f, 2f, vi.WithAlpha(0.95f));
            }
        }

        /// A unit grid point pulled toward the centre (the well's sag).
        static V2 Sag(V2 p) { float d = Mathf.Sqrt(p.X * p.X + p.Y * p.Y); return p * (0.55f + 0.45f * Mathf.Min(1f, d)); }
    }
}
