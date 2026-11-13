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
    }
}
