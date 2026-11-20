using UnityEngine;

namespace Arena
{
    /// A room's traps on the floor (RoomTraps.cs): a turret in hostile red (a plate, a twin-barrelled head that tracks the nearest player),
    /// a spike plate (a square with a field of steel points) and a slow field (a violet square with squares drifting in); a shot-up turret is gone. Hidden while their room is dark.
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
            float hp = game.TurretHpFrac(c);  // a damaged turret wears a health bar over it, red on a dark slot, flashing white when just hit
            if (hp < 1f)
            {
                float ago = game.TurretHitAgo(c), flashT = Mathf.Clamp01(1f - ago / 0.15f);
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
