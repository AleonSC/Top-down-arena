using UnityEngine;

namespace Arena
{
    /// A room's traps (RoomTraps.cs): a map turret is an embrasure set into its wall (an armoured half dome on a hazard collar, one heavy
    /// barrel through a turning slit, 75 degrees either side of straight out); a Dev Kit turret stands on the floor (a plate, a twin-barrelled head that tracks the nearest player),
    /// a spike plate (a hatched grate whose slots jab steel points on the tick) and a slow field (a violet grid that sags under players on it); a shot-up turret is gone. Hidden while their room is dark.
    public partial class ArenaApp
    {
        static readonly Rgba RoomTrapRed = new Rgba(1f, 0.3f, 0.34f), RoomTrapSteel = new Rgba(0.82f, 0.85f, 0.92f), RoomTrapViolet = new Rgba(0.62f, 0.42f, 1f);

        void DrawRoomTraps(float t)
        {
            if (!game.ExpandingOn && game.DevTrapCount == 0)
                return;
            foreach (var tr in game.RoomTrapList)
            {
                if (game.FogHidesStill(tr.Pos))  // (a trap you've seen stays shown)
                    continue;
                if (tr.Kind == TrapKind.Spike) DrawSpikePlate(tr.Pos, tr.HalfX, tr.HalfY, t);
                else if (tr.Kind == TrapKind.Slow) DrawSlowField(tr.Pos, tr.HalfX, tr.HalfY, t);
                else if (tr.Face.X != 0f || tr.Face.Y != 0f) DrawEmbrasureTurret(tr.Pos, tr.Face, t);
                else DrawTrapTurret(tr.Pos, t);
            }
        }

        /// A recessed grate (a square, or a strip down a corridor), hatched red, with corner brackets; its slots jab steel points on the damage tick.
        void DrawSpikePlate(V2 c, float hx, float hy, float t)
        {
            var hot = new Rgba(1f, 0.36f, 0.3f);
            float cyc = (t / RoomTraps.SpikeTick) % 1f, up = cyc < 0.35f ? Mathf.Sin(cyc / 0.35f * MathX.Pi) : 0f;
            FillRect(c.X - hx, c.Y - hy, hx * 2f, hy * 2f, new Rgba(0.05f, 0.03f, 0.05f, 0.9f));
            HatchRect(c.X - hx, c.Y - hy, hx * 2f, hy * 2f, hot.WithAlpha(0.13f));
            NodeFrame(c.X - hx, c.Y - hy, hx * 2f, hy * 2f, 1f, hot.WithAlpha(0.35f));
            TrapBrackets(c, hx, hy, Mathf.Min(13f, Mathf.Min(hx, hy) * 0.45f), 2f, hot.WithAlpha(0.95f));
            int nx = SpikeSlots(hx), ny = SpikeSlots(hy);  // an nx x ny field of slots
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    V2 p = c + new V2((i + 0.5f - nx * 0.5f) * (hx * 2f / nx), (j + 0.5f - ny * 0.5f) * (hy * 2f / ny));
                    FillRect(p.X - 7f, p.Y - 2.5f, 14f, 5f, new Rgba(0f, 0f, 0f, 0.9f));
                    FillRect(p.X - 7f, p.Y + 1.5f, 14f, 1f, hot.WithAlpha(0.4f + 0.6f * up));
                    float l = 2f + 9f * up;
                    if (l > 2.5f)
                        Tri3(p + new V2(0f, -l), p + new V2(-4f, 2f), p + new V2(4f, 2f), RoomTrapSteel.Mix(hot, 0.25f).WithAlpha(0.95f));
                }
        }

        static int SpikeSlots(float h) => Mathf.Max(2, Mathf.RoundToInt(h / 11f));

        /// A violet gravity well: a flat grid (a square, or a strip) in corner brackets that sags under every player standing on it.
        void DrawSlowField(V2 c, float hx, float hy, float t)
        {
            var vi = RoomTrapViolet;
            int nw = 0;
            foreach (var p in game.Players.Values)
                if (nw < wellAt.Length && RoomTraps.InRect(c, p.Pos, hx + SlowSagR, hy + SlowSagR))
                    wellAt[nw++] = p.Pos;
            FillRect(c.X - hx, c.Y - hy, hx * 2f, hy * 2f, vi.WithAlpha(0.08f));
            const int sub = 4;  // grid lines, each cut in short pieces so it can bend
            int cx = Mathf.Clamp(Mathf.RoundToInt(hx / 8f), 4, 18), cy = Mathf.Clamp(Mathf.RoundToInt(hy / 8f), 4, 18);
            for (int axis = 0; axis < 2; axis++)
            {
                int lines = axis == 0 ? cx : cy, steps = (axis == 0 ? cy : cx) * sub;  // axis 0: lines of constant x
                for (int a = 1; a < lines; a++)
                {
                    float u = -1f + 2f * a / lines;
                    V2 prev = default; float prevSag = 0f;
                    for (int s = 0; s <= steps; s++)
                    {
                        float w = -1f + 2f * s / steps;
                        V2 q = c + (axis == 0 ? new V2(u * hx, w * hy) : new V2(w * hx, u * hy));
                        V2 at = SagPoint(c, hx, hy, q, nw, out float sag);
                        if (s > 0)
                            Line(prev, at, 1.2f, vi.WithAlpha(0.3f + 0.45f * Mathf.Max(sag, prevSag)));
                        prev = at; prevSag = sag;
                    }
                }
            }
            for (int i = 0; i < nw; i++)  // a soft shadow in the dip
                if (RoomTraps.InRect(c, wellAt[i], hx, hy))
                    Circle(wellAt[i], SlowSagR * 0.6f, vi.WithAlpha(0.1f), 16);
            NodeFrame(c.X - hx, c.Y - hy, hx * 2f, hy * 2f, 1f, vi.WithAlpha(0.4f));
            TrapBrackets(c, hx, hy, Mathf.Min(16f, Mathf.Min(hx, hy) * 0.4f), 2f, vi.WithAlpha(0.95f));
        }

        const float SlowSagR = 54f;  // how far round a player the grid sags
        readonly V2[] wellAt = new V2[4];

        /// A grid point pulled toward the players on the well (fading to nothing at its rim, which stays square); sag 0-1 is how deep.
        V2 SagPoint(V2 c, float hx, float hy, V2 q, int nw, out float sag)
        {
            float rim = Mathf.Clamp01((hx - Mathf.Abs(q.X - c.X)) / 14f) * Mathf.Clamp01((hy - Mathf.Abs(q.Y - c.Y)) / 14f);
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
        void TrapBrackets(V2 c, float hx, float hy, float len, float w, Rgba col)
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var k = c + new V2(sx * hx, sy * hy);
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

        /// A map turret in its wall: the hazard collar, the dark half dome and its armour rings, the slit turning with one heavy barrel
        /// (clamped to its arc, locked on the nearest player in it, a thin sight line), then the same damage as the floor turret's.
        void DrawEmbrasureTurret(V2 home, V2 n, float t)
        {
            float hp = game.TurretHpFrac(home), ago = game.TurretHitAgo(home);
            float hit = Mathf.Clamp01(1f - ago / 0.2f);
            V2 shake = new V2(Mathf.Sin(ago * 95f + home.X), Mathf.Cos(ago * 83f + home.Y)) * (2.5f * hit);
            V2 f = home - n * RoomTraps.WallOut, c = f + shake;  // the dome's middle sits on the wall's face
            var tg = new V2(-n.Y, n.X);
            float baseAng = n.Angle;
            const float lim = 75f * Mathf.Deg2Rad;

            float ang = Mathf.Sin(t * 0.8f + home.X * 0.01f) * 0.9f, best = RoomTraps.TurretRange * RoomTraps.TurretRange;
            bool locked = false;
            V2 target = home;
            foreach (var p in game.Players.Values)
            {
                float d = (p.Pos - home).LengthSq;
                if (p.Downed || d >= best || !RoomTraps.InArc(home, n, p.Pos))
                    continue;
                best = d;
                var v = p.Pos - f;
                ang = Mathf.Atan2(v.Dot(tg), v.Dot(n));
                locked = true;
                target = p.Pos;
            }
            ang = Mathf.Clamp(ang, -lim, lim);
            V2 dir = n * Mathf.Cos(ang) + tg * Mathf.Sin(ang), side = new V2(-dir.Y, dir.X);
            var red = RoomTrapRed.Mix(Rgba.White, hit);
            var steel = new Rgba(0.32f, 0.3f, 0.36f, 1f);
            System.Func<float, float, V2> P = (x, y) => f + tg * x + n * y;

            Quad(P(-42f, -8f), P(42f, -8f), P(42f, 3f), P(-42f, 3f), new Rgba(0.18f, 0.05f, 0.06f, 1f));  // the collar, hazard teeth
            for (int k = -3; k <= 3; k++)
                Quad(P(k * 12f - 3f, -8f), P(k * 12f + 3f, -8f), P(k * 12f + 7f, 3f), P(k * 12f + 1f, 3f), RoomTrapRed.WithAlpha(0.55f));
            Arc(c, 17f, 34f, new Rgba(0.1f, 0.03f, 0.04f, 0.96f), 24, baseAng - MathX.Pi / 2f, MathX.Pi);
            Arc(c, 34f, 4f, red.WithAlpha(0.95f), 24, baseAng - MathX.Pi / 2f, MathX.Pi);
            Arc(c, 22f, 1.5f, RoomTrapRed.WithAlpha(0.45f), 20, baseAng - MathX.Pi / 2f, MathX.Pi);
            for (int k = 1; k < 6; k++)
            {
                var r = V2.FromAngle(baseAng - MathX.Pi / 2f + MathX.Pi * k / 6f);
                Line(c + r * 23f, c + r * 32f, 1.5f, RoomTrapRed.WithAlpha(0.35f));
            }
            Arc(c, 17f, 30f, new Rgba(0f, 0f, 0f, 0.9f), 6, ang + baseAng - 0.16f, 0.32f);  // the slit
            Line(c + dir * 10f, c + dir * 48f, 9f, steel);
            Line(c + dir * 10f, c + dir * 48f, 5f, red.WithAlpha(0.95f));
            Line(c + dir * 44f + side * 7f, c + dir * 44f - side * 7f, 5f, red);  // the muzzle brake
            Circle(c, 9f, red.WithAlpha(0.9f), 12);
            if (hit > 0f)
                Arc(c, 17f, 34f, new Rgba(1f, 1f, 1f, 0.55f * hit), 24, baseAng - MathX.Pi / 2f, MathX.Pi);

            V2 dome = c + n * 18f;  // damage shows on the dome: cracks, then smoke, then sparks and a glow of heat
            var crack = new Rgba(1f, 0.7f, 0.5f, 0.85f);
            if (hp < 0.66f)
            {
                Line(P(-26f, 6f), P(-16f, 14f), 1.5f, crack);
                Line(P(-16f, 14f), P(-18f, 24f), 1.5f, crack);
            }
            if (hp < 0.33f)
            {
                Line(P(24f, 8f), P(15f, 16f), 1.5f, crack);
                Line(P(15f, 16f), P(20f, 27f), 1.5f, crack);
                Arc(c, 17f, 34f, new Rgba(1f, 0.35f, 0.1f, 0.12f + 0.1f * Mathf.Sin(t * 12f)), 24, baseAng - MathX.Pi / 2f, MathX.Pi);
            }
            if (hp < 0.5f)
                for (int k = 0; k < 3; k++)  // smoke drifting up
                {
                    float ph = (t * 0.7f + k / 3f) % 1f;
                    Circle(dome + new V2(Mathf.Sin(ph * 6f + k * 2f) * 5f, -8f - ph * 34f), 4f + ph * 8f, new Rgba(0.55f, 0.55f, 0.6f, 0.38f * (1f - ph)), 10);
                }
            if (ago < 0.35f)  // sparks fly off the hit
                for (int k = 0; k < 7; k++)
                {
                    float a = baseAng + Mathf.Sin(home.X * 0.37f + k * 1.9f) * 1.4f;
                    float run = 18f + ago * 190f * (0.6f + 0.15f * (k % 3));
                    Line(dome + V2.FromAngle(a) * run, dome + V2.FromAngle(a) * (run + 8f), 2f, new Rgba(1f, 0.8f, 0.4f, 1f - ago / 0.35f));
                }

            V2 muzzle = c + dir * 50f;
            if (locked)  // a thin sight line to you and a pulsing muzzle
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(t * 20f);
                Line(muzzle, muzzle + dir * Mathf.Min(260f, (target - muzzle).Length), 1f, RoomTrapRed.WithAlpha(0.22f));
                Circle(muzzle, 3.5f + 2f * flash, new Rgba(1f, 0.8f, 0.7f, 0.35f + 0.4f * flash), 10);
            }
            if (hp < 1f)
            {
                float bw = 46f, bh = 6f;
                V2 b = Mathf.Abs(n.Y) > 0.5f ? c + n * 50f : c + n * 30f + new V2(0f, -44f);  // (out past the dome; on a side wall, above it)
                FillRect(b.X - bw / 2f - 1f, b.Y - bh / 2f - 1f, bw + 2f, bh + 2f, new Rgba(0.02f, 0.01f, 0.02f, 0.9f));
                FillRect(b.X - bw / 2f, b.Y - bh / 2f, bw * hp, bh, RoomTrapRed.Mix(Rgba.White, hit).WithAlpha(0.95f));
                NodeFrame(b.X - bw / 2f - 1f, b.Y - bh / 2f - 1f, bw + 2f, bh + 2f, 1.5f, RoomTrapRed.WithAlpha(0.8f));
            }
        }
    }
}
