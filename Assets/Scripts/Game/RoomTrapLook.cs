using UnityEngine;

namespace Arena
{
    /// A room's traps on the floor (RoomTraps.cs): a turret in hostile red (a plate, a twin-barrelled head that tracks the nearest player),
    /// a spike plate (a hatched grate whose slots jab steel points on the tick) and a slow field (a violet grid that sags under players on it); a shot-up turret is gone. Hidden while their room is dark.
    public partial class ArenaApp
    {
        static readonly Rgba RoomTrapRed = new Rgba(1f, 0.3f, 0.34f), RoomTrapSteel = new Rgba(0.82f, 0.85f, 0.92f), RoomTrapViolet = new Rgba(0.62f, 0.42f, 1f);

        void DrawRoomTraps(float t)
        {
            if (!game.ExpandingOn)
                return;
            foreach (var tr in game.RoomTrapList)
            {
                if (game.FogHidesStill(tr.Pos))  // (a trap you've seen stays shown)
                    continue;
                if (tr.Kind == TrapKind.Spike) DrawSpikePlate(tr.Pos, t);
                else if (tr.Kind == TrapKind.Slow) DrawSlowField(tr.Pos, t);
                else DrawTrapTurret(tr.Pos, t);
            }
        }

        /// A recessed grate, hatched red, with corner brackets; its slots jab steel points on the damage tick.
        void DrawSpikePlate(V2 c, float t)
        {
            float h = RoomTraps.SpikeHalf;
            var hot = new Rgba(1f, 0.36f, 0.3f);
            float cyc = (t / RoomTraps.SpikeTick) % 1f, up = cyc < 0.35f ? Mathf.Sin(cyc / 0.35f * MathX.Pi) : 0f;
            FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, new Rgba(0.05f, 0.03f, 0.05f, 0.9f));
            HatchRect(c.X - h, c.Y - h, h * 2f, h * 2f, hot.WithAlpha(0.13f));
            NodeFrame(c.X - h, c.Y - h, h * 2f, h * 2f, 1f, hot.WithAlpha(0.35f));
            TrapBrackets(c, h, 13f, 2f, hot.WithAlpha(0.95f));
            for (int i = -1; i <= 1; i++)  // a 3 x 3 field of slots
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

        /// A violet gravity well: a flat grid in corner brackets that sags under every player standing on it.
        void DrawSlowField(V2 c, float t)
        {
            float h = RoomTraps.SlowHalf;
            var vi = RoomTrapViolet;
            int nw = 0;
            foreach (var p in game.Players.Values)
                if (nw < wellAt.Length && RoomTraps.InSquare(c, p.Pos, h + SlowSagR))
                    wellAt[nw++] = p.Pos;
            FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, vi.WithAlpha(0.08f));
            const int cells = 6, sub = 4;  // grid lines, each cut in short pieces so it can bend
            for (int a = 1; a < cells; a++)
                for (int axis = 0; axis < 2; axis++)
                {
                    float u = -1f + 2f * a / cells;
                    V2 prev = default; float prevSag = 0f;
                    for (int s = 0; s <= cells * sub; s++)
                    {
                        float w = -1f + 2f * s / (cells * sub);
                        V2 q = c + (axis == 0 ? new V2(u, w) : new V2(w, u)) * h;
                        V2 at = SagPoint(c, h, q, nw, out float sag);
                        if (s > 0)
                            Line(prev, at, 1.2f, vi.WithAlpha(0.3f + 0.45f * Mathf.Max(sag, prevSag)));
                        prev = at; prevSag = sag;
                    }
                }
            for (int i = 0; i < nw; i++)  // a soft shadow in the dip
                if (RoomTraps.InSquare(c, wellAt[i], h))
                    Circle(wellAt[i], SlowSagR * 0.6f, vi.WithAlpha(0.1f), 16);
            NodeFrame(c.X - h, c.Y - h, h * 2f, h * 2f, 1f, vi.WithAlpha(0.4f));
            TrapBrackets(c, h, 16f, 2f, vi.WithAlpha(0.95f));
        }

        const float SlowSagR = 54f;  // how far round a player the grid sags
        readonly V2[] wellAt = new V2[4];

        /// A grid point pulled toward the players on the well (fading to nothing at its rim, which stays square); sag 0-1 is how deep.
        V2 SagPoint(V2 c, float h, V2 q, int nw, out float sag)
        {
            float rim = Mathf.Clamp01((h - Mathf.Abs(q.X - c.X)) / 14f) * Mathf.Clamp01((h - Mathf.Abs(q.Y - c.Y)) / 14f);
            V2 off = new V2(0f, 0f);
            sag = 0f;
            for (int i = 0; i < nw; i++)
            {
                V2 d = wellAt[i] - q;
                float k = Mathf.Exp(-d.LengthSq / (SlowSagR * SlowSagR)) * rim;
                off = off + d * (0.5f * k);
                sag = Mathf.Max(sag, k);
            }
            return q + off;
        }

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
    }
}
