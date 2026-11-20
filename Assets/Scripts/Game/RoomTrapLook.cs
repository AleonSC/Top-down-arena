using UnityEngine;

namespace Arena
{
    /// A room's traps on the floor (RoomTraps.cs): a turret in hostile red (a plate, a twin-barrelled head that tracks the nearest player),
    /// a spike plate (a ring of steel points) and a slow field (a violet pool with rings drifting in). Hidden while their room is dark.
    public partial class ArenaApp
    {
        static readonly Rgba RoomTrapRed = new Rgba(1f, 0.3f, 0.34f), RoomTrapSteel = new Rgba(0.82f, 0.85f, 0.92f), RoomTrapViolet = new Rgba(0.62f, 0.42f, 1f);

        void DrawRoomTraps(float t)
        {
            if (!game.ExpandingOn)
                return;
            foreach (var tr in game.RoomTrapList)
            {
                if (game.FogHides(tr.Pos))
                    continue;
                if (tr.Kind == TrapKind.Spike) DrawSpikePlate(tr.Pos, t);
                else if (tr.Kind == TrapKind.Slow) DrawSlowField(tr.Pos, t);
                else DrawTrapTurret(tr.Pos, t);
            }
        }

        void DrawSpikePlate(V2 c, float t)
        {
            float r = RoomTraps.SpikeR;
            Circle(c, r, new Rgba(0.08f, 0.04f, 0.05f, 0.85f), 24);
            Arc(c, r, 2.5f, RoomTrapRed.WithAlpha(0.85f), 24);
            for (int k = 0; k < 8; k++)
            {
                float a = MathX.Tau * k / 8f + 0.2f;
                V2 d = V2.FromAngle(a), s = new V2(-d.Y, d.X);
                Tri3(c + d * (r * 0.78f), c + d * (r * 0.3f) + s * 4.5f, c + d * (r * 0.3f) - s * 4.5f, RoomTrapSteel.WithAlpha(0.9f));
            }
            Tri3(c + new V2(0f, -9f), c + new V2(-5f, 4f), c + new V2(5f, 4f), RoomTrapSteel.WithAlpha(0.95f));
        }

        void DrawSlowField(V2 c, float t)
        {
            float r = RoomTraps.SlowR;
            Circle(c, r, RoomTrapViolet.WithAlpha(0.18f), 28);
            Arc(c, r, 2f, RoomTrapViolet.WithAlpha(0.7f), 28);
            for (int k = 0; k < 3; k++)
            {
                float ph = (t * 0.5f + k / 3f) % 1f;
                Arc(c, r * (1f - ph), 1.5f, RoomTrapViolet.WithAlpha(0.5f * (1f - ph)), 24);
            }
        }

        void DrawTrapTurret(V2 c, float t)
        {
            float aim = t * 0.5f + c.X * 0.01f, best = RoomTraps.TurretRange * RoomTraps.TurretRange;
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
            Arc(c, 25f, 3f, RoomTrapRed.WithAlpha(0.95f), 24);
            var dir = V2.FromAngle(aim);
            var side = new V2(-dir.Y, dir.X);
            for (int s = -1; s <= 1; s += 2)
                Line(c + side * (4.5f * s) + dir * 6f, c + side * (4.5f * s) + dir * 32f, 4f, RoomTrapRed.WithAlpha(0.95f));
            Circle(c, 12f, RoomTrapRed.WithAlpha(0.3f), 16);
            Arc(c, 12f, 2.5f, RoomTrapRed, 16);
            if (locked)
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(t * 20f);
                Circle(c + dir * 34f, 4f + 2f * flash, new Rgba(1f, 0.8f, 0.7f, 0.35f + 0.4f * flash), 10);
            }
        }
    }
}
