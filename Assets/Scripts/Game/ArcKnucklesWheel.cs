using UnityEngine;

namespace Arena
{
    /// The Arc Knuckles' click wheel (Brawl stance; Core/ArcKnucklesBrawl.cs, Docs/Mockups/arc-knuckles-stances.html, wheel steering).
    /// Your own game only: a Pull Punch that lands (Player.KnuckleMove goes past 0) opens a four-way wheel fixed on the screen where
    /// you clicked. From then on, the side of the wheel the cursor is on is the way your punches send enemies and your dashes go:
    /// it rides with every input as PlayerInput.Steer (an angle in the arena, so a turned camera still means "that way on screen");
    /// the centre is no steer. The wheel closes when the combo starts over or you leave the stance.
    public partial class ArenaApp
    {
        const float KnWheelR = 62f, KnWheelHole = 16f;  // the wheel's size and its centre's, in screen pixels at the UI's scale
        V2 knWheelAt, knPullClick;  // screen pixels (y up, as Input.mousePosition)
        bool knWheelOpen, knFireWas, knPullSet;

        static V2 MouseScreen => new V2(Input.mousePosition.x, Input.mousePosition.y);

        /// The point in the arena under a point on the screen (the cursor's math, MouseArena).
        V2 ScreenToArena(V2 m)
        {
            float ws = scale * WorldToUi;
            V2 s = new V2((m.X - offX) / ws, (UnityEngine.Screen.height - m.Y - offY) / ws) + lean;
            return ChargeCamUnpunch(CamZoomed ? UnzoomPoint(s) : s);
        }

        /// The side of the open wheel the cursor is on (screen axes, y up), or none in its centre.
        bool KnuckleWheelSide(out V2 side)
        {
            V2 d = MouseScreen - knWheelAt;
            side = V2.Zero;
            if (d.Length < KnWheelHole * scale)
                return false;
            side = Mathf.Abs(d.X) > Mathf.Abs(d.Y) ? new V2(Mathf.Sign(d.X), 0f) : new V2(0f, Mathf.Sign(d.Y));
            return true;
        }

        /// The arena direction of a side of the wheel (as the screen shows it now).
        V2 KnuckleWheelDir(V2 side)
        {
            V2 c = ScreenToArena(knWheelAt), a = ScreenToArena(knWheelAt + side * (40f * scale));
            return (a - c).Normalized();
        }

        /// Every input (FixedUpdate): opens and closes the wheel with the combo, and returns the steer it sends.
        byte KnuckleWheelSteer(bool fire)
        {
            var me = game.Me;
            bool click = fire && !knFireWas;
            knFireWas = fire;
            if (me == null || me.Weapon.Id != WeaponId.ArcKnuckles || me.KnuckleStance != 0 || me.Downed)
            {
                knWheelOpen = false;
                return 0;
            }
            if (click && me.KnuckleMove == 0)
            {
                knPullClick = MouseScreen;  // where a Pull Punch was clicked: the wheel opens there if it lands
                knPullSet = true;
            }
            if (me.KnuckleMove == 0)
                knWheelOpen = false;
            else if (!knWheelOpen)
            {
                knWheelOpen = true;
                V2 at = knPullSet ? knPullClick : MouseScreen;
                float m = (KnWheelR + 8f) * scale;  // (kept whole on the screen)
                knWheelAt = new V2(Mathf.Clamp(at.X, m, UnityEngine.Screen.width - m), Mathf.Clamp(at.Y, m, UnityEngine.Screen.height - m));
            }
            if (!knWheelOpen || !KnuckleWheelSide(out V2 side))
                return 0;
            V2 dir = KnuckleWheelDir(side);
            return dir.IsZero ? (byte)0 : Game.KnuckleSteerCode(dir.Angle);
        }

        /// The wheel, in the arena under the screen spot it's fixed to (drawn with the players: WorldRenderer.cs): four sides, the one
        /// under the cursor lit, a chevron on each, the centre with the move number.
        void DrawKnuckleWheel(Player p, float t)
        {
            if (!knWheelOpen || p.Id != game.MyId || p.Weapon.Id != WeaponId.ArcKnuckles || p.KnuckleStance != 0 || p.KnuckleMove == 0)
                return;
            V2 c = ScreenToArena(knWheelAt);
            float px = ScreenToArena(knWheelAt + new V2(10f * scale, 0f)).DistanceTo(c) / 10f;  // arena units per screen pixel at the UI's scale
            float r = KnWheelR * px, hole = KnWheelHole * px;
            bool any = KnuckleWheelSide(out V2 hov);
            bool dash = p.KnuckleMove == 2 || p.KnuckleMove == 4;
            V2[] sides = { new V2(1f, 0f), new V2(0f, -1f), new V2(-1f, 0f), new V2(0f, 1f) };
            foreach (var sd in sides)
            {
                V2 d = KnuckleWheelDir(sd);
                float a = d.Angle;
                bool lit = any && sd.X == hov.X && sd.Y == hov.Y;
                Wedge(c, r, a, MathX.Pi / 4f - 0.05f, lit ? KnArc.WithAlpha(0.32f) : new Rgba(0.05f, 0.08f, 0.14f, 0.5f), 14);
                Arc(c, r, 1.6f * px, (lit ? KnArcHi : KnArc).WithAlpha(lit ? 0.9f : 0.4f), 14, a - MathX.Pi / 4f + 0.05f, MathX.Pi / 2f - 0.1f);
                V2 side = d.Orthogonal(), m = c + d * ((r + hole) / 2f);
                float s = 7f * px * (lit ? 1.15f : 1f);
                SetColor((lit ? KnArcHi : KnBand).WithAlpha(lit ? 1f : 0.75f));
                Tri(m + d * s * 1.2f, m - d * s * 0.6f + side * s, m - d * s * 0.6f - side * s);
            }
            for (int i = 0; i < 4; i++)  // the gaps between the sides
            {
                V2 d = KnuckleWheelDir(sides[i]);
                V2 g = V2.FromAngle(d.Angle + MathX.Pi / 4f);
                Line(c + g * (hole + 2f * px), c + g * r, 2f * px, new Rgba(0.02f, 0.03f, 0.05f, 0.8f));
            }
            Circle(c, hole, new Rgba(0.03f, 0.05f, 0.09f, 0.9f), 20);
            Arc(c, hole, 1.4f * px, (any ? KnArc.WithAlpha(0.5f) : KnArcHi.WithAlpha(0.9f)), 20);
            // the next move's kind in the centre: a dash's arrow or a punch's fist
            if (dash)
            {
                SetColor(KnArcHi);
                Tri(c + new V2(0f, -6f * px), c + new V2(5f * px, 3f * px), c + new V2(-5f * px, 3f * px));
            }
            else
                Circle(c, 4.5f * px, p.KnuckleMove >= 5 ? KnGold : KnBand, 12);
        }

        /// The wheel's label (the HUD, over the world): the next move's name under it.
        void DrawKnuckleWheelLabel()
        {
            var me = game.Me;
            if (!knWheelOpen || me == null || me.Weapon.Id != WeaponId.ArcKnuckles || me.KnuckleStance != 0 || me.KnuckleMove == 0 || me.KnuckleMove >= Game.KnuckleBrawlMoves)
                return;
            float x = (knWheelAt.X - offX) / scale, y = (UnityEngine.Screen.height - knWheelAt.Y - offY) / scale + KnWheelR + 6f;
            string name = Game.KnuckleBrawlNames[me.KnuckleMove] + (me.KnuckleMove == 7 ? "  (hold)" : "");
            Ui.Shadowed(new Rect(x - 90f, y, 180f, 18f), name, 13, me.KnuckleMove >= 5 ? CrtStyle.Gold : CrtStyle.Ink);
        }
    }
}
