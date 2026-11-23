using UnityEngine;

namespace Arena
{
    /// MOCKUP (preview only, WALLTURRET=1-4): room turrets bolted to their nearest wall instead of standing on the floor.
    /// 1 Bracket mount, 2 Embrasure dome, 3 Rail sentry, 4 Hatch port. Each turns within 75 degrees of straight out from the wall.
    public partial class ArenaApp
    {
        internal static int PreviewWallTurret;

        bool NearestWallFace(V2 pos, out V2 face, out V2 n)
        {
            face = pos; n = new V2(0f, 1f);
            var wd = game.CurrentWorld;
            if (wd == null)
                return false;
            float best = float.MaxValue;
            foreach (var s in wd.Walls())
            {
                if (s.Round)
                    continue;
                var q = new V2(Mathf.Clamp(pos.X, s.Pos.X - s.HW, s.Pos.X + s.HW), Mathf.Clamp(pos.Y, s.Pos.Y - s.HH, s.Pos.Y + s.HH));
                var d = pos - q;
                float l = d.LengthSq;
                if (l < 1f || l >= best)
                    continue;
                best = l;
                face = q;
                n = Mathf.Abs(d.X) > Mathf.Abs(d.Y) ? new V2(Mathf.Sign(d.X), 0f) : new V2(0f, Mathf.Sign(d.Y));
            }
            return best < float.MaxValue;
        }

        void DrawWallTurretMock(V2 home, float t, int style)
        {
            if (!NearestWallFace(home, out V2 f, out V2 n))
                return;
            var tg = new V2(-n.Y, n.X);
            float hp = game.TurretHpFrac(home), ago = game.TurretHitAgo(home);
            float hit = Mathf.Clamp01(1f - ago / 0.2f);
            System.Func<float, float, V2> P = (x, y) => f + tg * x + n * y;

            // Aim: the nearest player, clamped to 75 degrees either side of straight out.
            float ang = Mathf.Sin(t * 0.8f + home.X) * 0.9f, best = RoomTraps.TurretRange * RoomTraps.TurretRange;
            bool locked = false;
            V2 target = f;
            foreach (var p in game.Players.Values)
            {
                float d = (p.Pos - f).LengthSq;
                if (!p.Downed && d < best) { best = d; var v = p.Pos - f; ang = Mathf.Atan2(v.Dot(tg), v.Dot(n)); locked = true; target = p.Pos; }
            }
            const float lim = 75f * Mathf.Deg2Rad;
            ang = Mathf.Clamp(ang, -lim, lim);
            V2 dir = n * Mathf.Cos(ang) + tg * Mathf.Sin(ang), side = new V2(-dir.Y, dir.X);
            float baseAng = Mathf.Atan2(n.Y, n.X);

            var red = RoomTrapRed.Mix(Rgba.White, hit);
            var dark = new Rgba(0.1f, 0.03f, 0.04f, 0.96f);
            var steel = new Rgba(0.32f, 0.3f, 0.36f, 1f);
            V2 head = f, muzzle = f;

            if (style == 1)  // Bracket mount: a bolted wall plate, two struts, a ball head with twin barrels
            {
                Arc(f, 70f, 1.5f, RoomTrapRed.WithAlpha(0.18f), 32, baseAng - lim, 2f * lim);
                Quad(P(-32f, -4f), P(32f, -4f), P(32f, 9f), P(-32f, 9f), dark);
                Line(P(-32f, 9f), P(32f, 9f), 2.5f, red.WithAlpha(0.9f));
                Line(P(-32f, -4f), P(-32f, 9f), 2f, red.WithAlpha(0.7f));
                Line(P(32f, -4f), P(32f, 9f), 2f, red.WithAlpha(0.7f));
                for (int k = 0; k < 4; k++)
                    Circle(P(-25f + k * 16.7f, 2.5f), 2.5f, steel.Mix(Rgba.White, 0.4f), 8);
                Line(P(-18f, 9f), P(-7f, 24f), 5f, steel);
                Line(P(18f, 9f), P(7f, 24f), 5f, steel);
                head = P(0f, 28f);
                Circle(head, 16f, dark, 20);
                Arc(head, 16f, 3f, red.WithAlpha(0.95f), 20);
                for (int s = -1; s <= 1; s += 2)
                    Line(head + side * (5f * s) + dir * 6f, head + side * (5f * s) + dir * 34f, 4.5f, red.WithAlpha(0.95f));
                Circle(head, 7f, RoomTrapRed.WithAlpha(0.35f), 12);
                Arc(head, 7f, 2f, red, 12);
                muzzle = head + dir * 36f;
            }
            else if (style == 2)  // Embrasure: an armoured half dome set into the wall, one heavy barrel through a turning slit
            {
                Quad(P(-42f, -8f), P(42f, -8f), P(42f, 3f), P(-42f, 3f), new Rgba(0.18f, 0.05f, 0.06f, 1f));
                for (int k = -3; k <= 3; k++)  // hazard teeth on the wall collar
                    Quad(P(k * 12f - 3f, -8f), P(k * 12f + 3f, -8f), P(k * 12f + 7f, 3f), P(k * 12f + 1f, 3f), RoomTrapRed.WithAlpha(0.55f));
                Arc(f, 17f, 34f, dark, 24, baseAng - MathX.Pi / 2f, MathX.Pi);
                Arc(f, 34f, 4f, red.WithAlpha(0.95f), 24, baseAng - MathX.Pi / 2f, MathX.Pi);
                Arc(f, 22f, 1.5f, RoomTrapRed.WithAlpha(0.45f), 20, baseAng - MathX.Pi / 2f, MathX.Pi);
                for (int k = 1; k < 6; k++)
                {
                    var r = V2.FromAngle(baseAng - MathX.Pi / 2f + MathX.Pi * k / 6f);
                    Line(f + r * 23f, f + r * 32f, 1.5f, RoomTrapRed.WithAlpha(0.35f));
                }
                Arc(f, 17f, 30f, new Rgba(0f, 0f, 0f, 0.9f), 6, ang + baseAng - 0.16f, 0.32f);  // the slit
                Line(f + dir * 10f, f + dir * 48f, 9f, steel);
                Line(f + dir * 10f, f + dir * 48f, 5f, red.WithAlpha(0.95f));
                Line(f + dir * 44f + side * 7f, f + dir * 44f - side * 7f, 5f, red);  // muzzle brake
                Circle(f, 9f, red.WithAlpha(0.9f), 12);
                head = f + n * 18f;
                muzzle = f + dir * 50f;
            }
            else if (style == 3)  // Rail sentry: a carriage that slides along a wall track to keep you in its sights
            {
                float along = locked ? Mathf.Clamp((target - f).Dot(tg), -72f, 72f) : Mathf.Sin(t * 0.7f) * 60f;
                Quad(P(-96f, -2f), P(96f, -2f), P(96f, 12f), P(-96f, 12f), new Rgba(0.06f, 0.02f, 0.03f, 0.95f));
                Line(P(-96f, 3f), P(96f, 3f), 2f, steel.Mix(Rgba.White, 0.3f));
                Line(P(-96f, 9f), P(96f, 9f), 2f, steel.Mix(Rgba.White, 0.3f));
                for (int k = -8; k <= 8; k++)
                    Line(P(k * 11.5f, 1f), P(k * 11.5f, 11f), 1.5f, RoomTrapRed.WithAlpha(0.3f));
                for (int s = -1; s <= 1; s += 2)
                    Quad(P(s * 96f - 5f, -3f), P(s * 96f + 5f, -3f), P(s * 96f + 5f, 15f), P(s * 96f - 5f, 15f), red.WithAlpha(0.9f));
                Quad(P(along - 20f, 0f), P(along + 20f, 0f), P(along + 16f, 20f), P(along - 16f, 20f), dark);
                Line(P(along - 16f, 20f), P(along + 16f, 20f), 2f, red.WithAlpha(0.8f));
                Circle(P(along - 12f, 6f), 3f, red, 8);
                Circle(P(along + 12f, 6f), 3f, red, 8);
                head = P(along, 24f);
                Circle(head, 14f, dark, 18);
                Arc(head, 14f, 3f, red.WithAlpha(0.95f), 18);
                for (int s = -1; s <= 1; s += 2)
                    Line(head + side * (4.5f * s) + dir * 5f, head + side * (4.5f * s) + dir * 30f, 4f, red.WithAlpha(0.95f));
                Circle(head, 6f, RoomTrapRed.WithAlpha(0.4f), 10);
                muzzle = head + dir * 32f;
            }
            else  // Hatch port: two armour doors swung open in the wall, a boxed gun and a sensor eye, cables running off along the wall
            {
                for (int s = -1; s <= 1; s += 2)
                    for (int c = 0; c < 2; c++)
                    {
                        float y = 2f + c * 5f, x0 = s * 34f, x1 = s * 110f;
                        Line(P(x0, y), P(x1, y), 2f, new Rgba(0.25f, 0.08f, 0.1f, 1f));
                        float ph = ((t * 0.9f + c * 0.5f) % 1f);
                        Circle(P(Mathf.Lerp(x1, x0, ph), y), 2f, RoomTrapRed.WithAlpha(0.9f * (1f - ph * 0.5f)), 6);
                    }
                Quad(P(-28f, -10f), P(28f, -10f), P(28f, 2f), P(-28f, 2f), new Rgba(0.02f, 0.01f, 0.02f, 1f));
                for (int k = -2; k <= 2; k++)
                    Line(P(k * 11f - 4f, -9f), P(k * 11f + 4f, 1f), 3f, RoomTrapRed.WithAlpha(0.5f));
                for (int s = -1; s <= 1; s += 2)  // the doors, swung out
                {
                    Line(P(s * 28f, 1f), P(s * 40f, 18f), 7f, steel);
                    Line(P(s * 28f, 1f), P(s * 40f, 18f), 2f, red.WithAlpha(0.7f));
                }
                head = P(0f, 10f);
                Quad(head - side * 13f - dir * 12f, head + side * 13f - dir * 12f, head + side * 13f + dir * 14f, head - side * 13f + dir * 14f, dark);
                Line(head - side * 13f + dir * 14f, head + side * 13f + dir * 14f, 2.5f, red);
                Line(head + dir * 14f, head + dir * 44f, 8f, steel);
                Line(head + dir * 14f, head + dir * 44f, 4f, red.WithAlpha(0.95f));
                float blink = 0.6f + 0.4f * Mathf.Sin(t * (locked ? 14f : 3f));
                Circle(head - side * 6f, 4f, new Rgba(1f, 0.25f, 0.2f, blink), 10);
                Circle(head - side * 6f, 8f, new Rgba(1f, 0.25f, 0.2f, 0.18f * blink), 12);
                muzzle = head + dir * 46f;
            }

            if (locked)  // a thin sight line to you and a pulsing muzzle
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(t * 20f);
                Line(muzzle, muzzle + dir * Mathf.Min(260f, (target - muzzle).Length), 1f, RoomTrapRed.WithAlpha(0.22f));
                Circle(muzzle, 3.5f + 2f * flash, new Rgba(1f, 0.8f, 0.7f, 0.35f + 0.4f * flash), 10);
            }
            if (hp < 1f)
            {
                float bw = 46f, bh = 6f;
                V2 b = head + n * 30f;
                FillRect(b.X - bw / 2f - 1f, b.Y - bh / 2f - 1f, bw + 2f, bh + 2f, new Rgba(0.02f, 0.01f, 0.02f, 0.9f));
                FillRect(b.X - bw / 2f, b.Y - bh / 2f, bw * hp, bh, red.WithAlpha(0.95f));
            }
        }
    }
}
