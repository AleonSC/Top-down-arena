using UnityEngine;

namespace Arena
{
    /// The Duelist's faction, the Masked Court (Core/CourtFaction.cs), drawn as in `Docs/Mockups/duelist-faction.html`:
    /// a dark body with a pale rim, a white half-mask toward where it faces, two eye slits, a rapier with a gold guard.
    ///   Fencer (Shiv): small, a foil held out; its line and leap are the Lunger's tell (LungerTell, from the underlay).
    ///   Blade Dancer (Guard): a crimson cape behind and a longer blade, drawn back while its line shows; its line, trail
    ///     and hanging blades are the Duelist's (DrawDuelFloor, DuelistLook.cs).
    ///   Parrier (Riot Shield): a broad body with a gold parrying arc in front that dims as it cracks and flashes before
    ///     the bash; a short main-gauche across its front. Broken, the arc is gone and it reels while it staggers.
    ///   Second (Security Guard): a gold rim, three daggers fanned in its hand; winding up, the hand goes back, the
    ///     daggers glint and five faint lanes fan out where they'll fly. Its daggers (DrawThrownDagger) spin a little.
    ///   Marshal (Gas Grenadier): a gold rim and cape, a baton with a gold knob raised as it winds up; its circle
    ///     (DrawDuelCirclePatch) draws itself round over the warning, then glows gold with blades round the edge and a
    ///     ring that counts down.
    ///   Gallant (Taser Guard): a steel cape and a long rapier; saluting, the sword goes straight up with a gold glint and a
    ///     shrinking ring; parried, it reels with a crimson rim and gold stars over its head (open to more damage).
    ///   Harlequin (Thug): a big body in crimson diamonds under a black mask, a sword out of each side; showing its line it
    ///     crouches with both swords forward; its lines, trail and blades are the Duelist's (DrawDuelFloor).
    ///   Mummer and Mummer Copy (Shiv; drawn the same, on purpose): a violet cape and rim, a pale mask with a painted tear,
    ///     a short blade.
    public partial class ArenaApp
    {
        static readonly Rgba CourtInk = new Rgba(0.04f, 0.045f, 0.07f, 0.95f), CourtBody = new Rgba(0.1f, 0.11f, 0.16f),
            CourtMask = new Rgba(0.95f, 0.93f, 0.9f), CourtSteel = new Rgba(0.81f, 0.84f, 1f), CourtCrimson = new Rgba(1f, 0.3f, 0.42f),
            CourtGold = new Rgba(1f, 0.8f, 0.35f);

        void DrawCourtBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Fencer: DrawFencer(e, pos, r, col, t); break;
                case EnemyType.BladeDancer: DrawBladeDancer(e, pos, r, col, t); break;
                case EnemyType.Parrier: DrawParrier(e, pos, r, col, t); break;
                case EnemyType.Second: DrawSecond(e, pos, r, col, t); break;
                case EnemyType.Marshal: DrawMarshal(e, pos, r, col, t); break;
                case EnemyType.Gallant: DrawGallant(e, pos, r, col, t); break;
                case EnemyType.Harlequin: DrawHarlequin(e, pos, r, col, t); break;
                case EnemyType.Mummer:
                case EnemyType.MummerCopy: DrawMummer(e, pos, r, t); break;
            }
        }

        /// A masked fencer: ink edge, body, rim in `rim`, a half-mask toward `face` with two eye slits.
        void CourtFigure(V2 pos, float r, float face, Rgba rim)
        {
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            Circle(pos, r + 2f, CourtInk, 18);
            Circle(pos, r, rim, 18);
            Circle(pos, r - 2f, CourtBody, 18);
            // The half-mask: a fan of triangles on the front half.
            SetColor(CourtMask);
            V2 mc = pos + f * (r * 0.2f);
            const int n = 7;
            for (int i = 0; i < n; i++)
            {
                float a0 = -MathX.Pi / 2f + MathX.Pi * i / n, a1 = -MathX.Pi / 2f + MathX.Pi * (i + 1) / n;
                Tri(mc, mc + f * (Mathf.Cos(a0) * r * 0.55f) + s * (Mathf.Sin(a0) * r * 0.68f), mc + f * (Mathf.Cos(a1) * r * 0.55f) + s * (Mathf.Sin(a1) * r * 0.68f));
            }
            for (int k = -1; k <= 1; k += 2)
                Line(mc + f * (r * 0.3f) + s * (k * r * 0.18f), mc + f * (r * 0.3f) + s * (k * r * 0.36f), 1.6f, CourtInk);
        }

        /// A rapier from the body's edge at `ang`, `len` long, with a gold cross-guard.
        void CourtBlade(V2 pos, float r, float ang, float len, float width)
        {
            V2 d = V2.FromAngle(ang), s = d.Orthogonal(), hilt = pos + d * (r - 1f);
            Line(hilt, hilt + d * len, width, new Rgba(0.95f, 0.96f, 1f));
            Line(hilt - s * 4.5f, hilt + s * 4.5f, 2.5f, CourtGold);
        }

        void DrawFencer(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float face = e.Facing;
            // Winding up, the foil draws back; leaping, it points straight down the line.
            float back = e.Phase == 1 ? 0.6f : e.Phase == 2 ? 0f : 0.25f;
            CourtBlade(pos, r, face + back, r * 1.6f, 2f);
            CourtFigure(pos, r, face, col);
        }

        void DrawBladeDancer(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float face = e.Facing;
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            // The cape: a crimson fan behind, swinging as it moves.
            float sway = Mathf.Sin(t * 5f + e.Id) * 0.12f;
            V2 back = pos - V2.FromAngle(face + sway) * (r * 2f);
            SetColor(CourtCrimson.WithAlpha(0.55f));
            Tri(pos + s * r * 0.9f - f * r * 0.2f, pos - s * r * 0.9f - f * r * 0.2f, back);
            var d = e.Duel;
            bool warn = d != null && d.Dash != DuelDash.None && d.DashWarn > 0f, dash = d != null && d.Dash != DuelDash.None && d.DashWarn <= 0f;
            float bladeAng = warn ? face + 1.1f : dash ? face : face + 0.3f;
            CourtBlade(pos, r, bladeAng, r * 1.9f, 2.5f);
            CourtFigure(pos, r, face, col);
            if (warn)
                Circle(pos + V2.FromAngle(bladeAng) * (r + r * 1.9f), 3f + 1.5f * Mathf.Sin(t * 30f), CourtCrimson, 8);  // a glint at the tip
        }

        void DrawParrier(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            int crack = Game.RiotCrack(e);
            byte st = Game.RiotState(e);
            bool broken = crack >= Game.RiotCracked;
            float face = e.Facing + (broken && st == Game.RiotStaggered ? Mathf.Sin(t * 30f) * 0.2f : 0f);
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            if (!broken)
            {
                // The parrying arc in front: gold, dimmer with each crack, flashing before the bash, low after it.
                bool low = st == Game.RiotLow;
                float a = (1f - 0.18f * crack) * (low ? 0.4f : 1f);
                if (st == Game.RiotWinding)
                    a = Mathf.Sin(t * 50f) > 0f ? 1f : 0.35f;
                const float half = 1.15f;
                Arc(pos, r + 10f, 9f, CourtGold.WithAlpha(0.22f * a), 20, face - half, half * 2f);
                Arc(pos, r + 10f, 2.5f, CourtGold.WithAlpha(0.95f * a), 20, face - half, half * 2f);
                for (int k = 0; k < crack; k++)  // nicks in the guard
                {
                    V2 at = pos + V2.FromAngle(face - 0.7f + 0.55f * k) * (r + 10f);
                    Line(at - f * 3f, at + f * 4f, 1.6f, CourtInk);
                }
                // The main-gauche, held across the front.
                Line(pos + f * (r * 0.5f) - s * (r * 0.75f), pos + f * (r * 1.05f) + s * (r * 0.25f), 3f, new Rgba(0.95f, 0.96f, 1f));
                Line(pos + f * (r * 0.4f) - s * (r * 0.95f), pos + f * (r * 0.75f) - s * (r * 0.5f), 3f, CourtGold);
            }
            CourtBlade(pos, r, face - 0.9f, r * 1.1f, 2.5f);
            CourtFigure(pos, r, face, col);
        }

        void DrawSecond(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float face = e.Facing;
            float back = e.Winding ? 1.1f : 0.5f;
            if (e.Winding)  // where the fan will fly
                for (int i = 0; i < Game.SecondFan; i++)
                {
                    V2 d = V2.FromAngle(face + (i - (Game.SecondFan - 1) / 2f) * Game.SecondSpread);
                    for (float u = r + 14f; u < 230f; u += 22f)
                        Line(pos + d * u, pos + d * (u + 10f), 1.5f, CourtSteel.WithAlpha(0.28f));
                }
            V2 hand = pos + V2.FromAngle(face - back) * (r + 3f);
            for (int k = -1; k <= 1; k++)
            {
                V2 d = V2.FromAngle(face - back * 0.6f + k * 0.28f);
                Line(hand, hand + d * 11f, 2f, CourtSteel);
                Line(hand - d.Orthogonal() * 2.5f, hand + d.Orthogonal() * 2.5f, 2f, CourtGold);
            }
            CourtFigure(pos, r, face, col);
            if (e.Winding)
                Circle(hand + V2.FromAngle(face - back * 0.6f) * 12f, 3f + 1.5f * Mathf.Sin(t * 30f), CourtGold, 8);
        }

        void DrawMarshal(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float face = e.Facing;
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            SetColor(CourtGold.WithAlpha(0.35f));
            Tri(pos + s * r * 0.9f - f * r * 0.2f, pos - s * r * 0.9f - f * r * 0.2f, pos - f * (r * 1.9f));
            float raise = e.Winding ? 1.3f : 0.2f;
            V2 d = V2.FromAngle(face - raise), from = pos + d * (r * 0.6f), tip = from + d * (r * 1.4f);
            Line(from, tip, 3f, new Rgba(0.85f, 0.75f, 0.54f));
            Circle(tip, 3.5f, CourtGold, 8);
            CourtFigure(pos, r, face, col);
            if (e.Winding)
                Circle(tip, 5f + 2f * Mathf.Sin(t * 30f), CourtGold.WithAlpha(0.5f), 10);
        }

        void DrawGallant(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var d = e.Duel;
            bool salute = d != null && d.SaluteT > 0f, stagger = d != null && d.StaggerT > 0f;
            float face = e.Facing + (stagger ? Mathf.Sin(t * 14f) * 0.35f : 0f);
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            SetColor(CourtSteel.WithAlpha(0.3f));
            Tri(pos + s * r * 0.9f - f * r * 0.2f, pos - s * r * 0.9f - f * r * 0.2f, pos - V2.FromAngle(face + Mathf.Sin(t * 5f + e.Id) * 0.12f) * (r * 2f));
            if (salute)
            {
                float k = Mathf.Clamp01(1f - d.SaluteT / Game.GallantTell);
                Circle(pos, r + 18f, CourtGold.WithAlpha(0.12f + 0.1f * k), 24);
                Arc(pos, r + 26f - 16f * k, 2.5f, CourtGold.WithAlpha(0.5f + 0.4f * Mathf.Sin(t * 30f)), 28);
                V2 hilt = pos + s * (r * 0.5f);
                Line(hilt, hilt + new V2(0f, -r * 2.3f), 3f, new Rgba(0.95f, 0.96f, 1f));
                Line(hilt - new V2(5f, 0f), hilt + new V2(5f, 0f), 2.5f, CourtGold);
                Circle(hilt + new V2(0f, -r * 2.3f), 4f + 2f * Mathf.Sin(t * 40f), new Rgba(1f, 0.96f, 0.8f), 10);
            }
            else
                CourtBlade(pos, r, face + (d != null && d.Dash != DuelDash.None ? 0f : 0.3f), r * 2f, 2.5f);
            CourtFigure(pos, r, face, stagger ? CourtCrimson : col);
            if (stagger)
            {
                Circle(pos, r + 6f, CourtCrimson.WithAlpha(0.2f + 0.1f * Mathf.Sin(t * 10f)), 20);
                for (int k = 0; k < 3; k++)
                {
                    float a = t * 5f + k * MathX.Tau / 3f;
                    Circle(pos + new V2(Mathf.Cos(a) * 18f, -(r + 10f) + Mathf.Sin(a) * 5f), 3f, CourtGold, 8);
                }
            }
        }

        void DrawHarlequin(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var d = e.Duel;
            bool aim = d != null && d.Dash != DuelDash.None && d.DashWarn > 0f;
            float face = e.Facing;
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            // Two swords: out to each side, or both forward while it shows its line.
            float spread = aim ? 0.35f : 1.4f;
            CourtBlade(pos, r, face + spread, r * 1.3f, 3f);
            CourtBlade(pos, r, face - spread, r * 1.3f, 3f);
            Circle(pos, r + 2f, CourtInk, 24);
            Circle(pos, r, CourtCrimson, 24);
            Circle(pos, r - 2f, CourtBody, 24);
            // Diamonds: a lattice of crimson lozenges turned with the body.
            SetColor(CourtCrimson.WithAlpha(0.75f));
            float q = r * 0.3f;
            for (int i = -2; i <= 2; i++)
                for (int j = -2; j <= 2; j++)
                {
                    if (((i + j) & 1) != 0)
                        continue;
                    V2 c = pos + f * (i * q) + s * (j * q);
                    if (c.DistanceTo(pos) > r - q * 0.9f)
                        continue;
                    V2 a1 = c + f * (q * 0.5f), a2 = c + s * (q * 0.5f), a3 = c - f * (q * 0.5f), a4 = c - s * (q * 0.5f);
                    Tri(a1, a2, a3);
                    Tri(a1, a3, a4);
                }
            // A black half-mask with gold eye rims.
            V2 mc = pos + f * (r * 0.25f);
            SetColor(CourtInk);
            const int n = 7;
            for (int i = 0; i < n; i++)
            {
                float a0 = -MathX.Pi / 2f + MathX.Pi * i / n, a1 = -MathX.Pi / 2f + MathX.Pi * (i + 1) / n;
                Tri(mc, mc + f * (Mathf.Cos(a0) * r * 0.5f) + s * (Mathf.Sin(a0) * r * 0.62f), mc + f * (Mathf.Cos(a1) * r * 0.5f) + s * (Mathf.Sin(a1) * r * 0.62f));
            }
            for (int k = -1; k <= 1; k += 2)
                Circle(mc + f * (r * 0.28f) + s * (k * r * 0.26f), 2.2f, CourtGold, 8);
            if (aim)
                Circle(pos, r + 6f + 2f * Mathf.Sin(t * 30f), CourtCrimson.WithAlpha(0.25f), 24);
        }

        void DrawMummer(Enemy e, V2 pos, float r, float t)
        {
            float face = e.Facing;
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            var violet = Game.MummerViolet;
            SetColor(violet.WithAlpha(0.5f));
            Tri(pos + s * r * 0.9f - f * r * 0.2f, pos - s * r * 0.9f - f * r * 0.2f, pos - V2.FromAngle(face + Mathf.Sin(t * 6f + e.Id) * 0.15f) * (r * 2f));
            CourtBlade(pos, r, face + (e.Phase == 1 ? 0.6f : 0.25f), r * 1.4f, 2f);
            CourtFigure(pos, r, face, violet);
            Line(pos + f * (r * 0.45f) + s * (r * 0.27f), pos + f * (r * 0.2f) + s * (r * 0.3f), 1.5f, violet);  // a painted tear
        }

        /// A Second's dagger: a steel blade along its flight with a gold hilt, wobbling a little.
        void DrawThrownDagger(Bullet b, float t)
        {
            V2 d = V2.FromAngle(b.Vel.Angle + Mathf.Sin(t * 22f + b.Id) * 0.15f), s = d.Orthogonal();
            Line(b.Pos - d * 12f, b.Pos - d * 2f, 3f, CourtSteel.WithAlpha(0.18f));  // a faint streak
            Line(b.Pos - d * 3f, b.Pos + d * 7f, 2.5f, new Rgba(0.95f, 0.96f, 1f));
            Line(b.Pos - d * 3f - s * 3.5f, b.Pos - d * 3f + s * 3.5f, 2.5f, CourtGold);
            Line(b.Pos - d * 3f, b.Pos - d * 7f, 2.5f, new Rgba(0.42f, 0.29f, 0.17f));
        }

        /// A Marshal's dueling circle (PathsUi.cs DrawPatch): drawn round over the warning, then a gold ring with blades
        /// pointing in round the edge and an outer ring counting down; it fades at the end.
        void DrawDuelCirclePatch(PathPatch pt, float t)
        {
            float r = pt.R0, a = Mathf.Clamp01((pt.Life - pt.Age) / 0.3f);
            if (!pt.Active)
            {
                float k = Mathf.Clamp01(pt.Age / Mathf.Max(0.01f, pt.Warn));
                Arc(pt.Pos, r, 3f, CourtGold.WithAlpha(0.45f + 0.3f * Mathf.Sin(t * 30f)), 48, -MathX.Pi / 2f, MathX.Tau * k);
                return;
            }
            Circle(pt.Pos, r, CourtGold.WithAlpha(0.04f * a), 48);
            Arc(pt.Pos, r, 12f, CourtGold.WithAlpha(0.14f * a), 48);
            Arc(pt.Pos, r, 2.5f, CourtGold.WithAlpha(0.9f * a), 48);
            int n = 12;
            for (int k = 0; k < n; k++)
            {
                V2 dir = V2.FromAngle(MathX.Tau * k / n + t * 0.3f), at = pt.Pos + dir * (r - 4f);
                Line(at, at - dir * 12f, 2f, (k % 2 == 0 ? CourtSteel : CourtGold).WithAlpha(0.8f * a));
            }
            float left = Mathf.Clamp01((pt.Life - pt.Age) / Mathf.Max(0.01f, pt.Life - pt.Warn));
            Arc(pt.Pos, r + 10f, 2f, CourtGold.WithAlpha(0.45f * a), 48, -MathX.Pi / 2f, MathX.Tau * left);
        }
    }
}
