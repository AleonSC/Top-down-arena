using UnityEngine;

namespace Arena
{
    /// The Healer Soldier's Lifeline with Visual effects on (Docs/Mockups/lifeline.html, look E "Heartbeat Halo"). Drawing only;
    /// off, the plain green ring and beam draw as before (`IsLifelinePlain`). No gameplay number changes.
    ///
    /// The host sends one FxKind.Lifeline per player it reached (Pos = where the caster stood, Pos2 = (that player's id, the
    /// caster's id), Radius = how far they were), lasting LifelineFxTime. Its age is the clock:
    ///   a lub-dub pulse (two rings, 0.22 s apart) rolls out from the caster at 330 px/s (drawn once, by the caster's own effect);
    ///   as it reaches each player a halo drops over them and a column of light holds them for the 3 s of invulnerability,
    ///   blinking over the last 0.6 s; then the halo lifts away and everything fades over 0.7 s.
    public partial class ArenaApp
    {
        const float LifePulseSpeed = 330f, LifePulseReach = 360f;
        static readonly Rgba LifeGreen = new Rgba(0.35f, 1f, 0.55f), LifePale = new Rgba(0.79f, 1f, 0.84f);

        /// Lifeline's plain ring and beam (Ultimates.cs marks them by their durations), which the look replaces.
        bool IsLifelinePlain(Effect fx)
        {
            if (!fxOn)
                return false;
            if (fx.Kind == FxKind.Ring)
                return Mathf.Abs(fx.Dur - Game.LifelinePlainRing) < 0.005f && SameRgb(fx.Color, new Rgba(0.4f, 1f, 0.55f));
            return fx.Kind == FxKind.Link && Mathf.Abs(fx.Dur - Game.LifelinePlainLink) < 0.005f && SameRgb(fx.Color, new Rgba(0.4f, 1f, 0.55f));
        }

        /// WorldEffects.cs: FxKind.Lifeline, for one player.
        void DrawLifelineFx(Effect fx)
        {
            if (!fxOn)
                return;
            Player p;
            int who = (int)fx.Pos2.X, caster = (int)fx.Pos2.Y;
            if (!game.Players.TryGetValue(who, out p) || LaunchHides(p))
                return;
            float a = fx.T, inv = Game.LifelineInvuln;

            if (who == caster)  // the pulse: lub, then dub
                for (int b = 0; b < 2; b++)
                {
                    float r = (a - 0.22f * b) * LifePulseSpeed;
                    if (r <= 0f || r >= LifePulseReach)
                        continue;
                    float u = 1f - r / LifePulseReach;
                    Arc(fx.Pos, r, (b == 0 ? 5f : 3f) * u + 1f, LifeGreen.WithAlpha(u * (b == 0 ? 1f : 0.7f)), 64);
                }

            float arrive = who == caster ? 0.1f : 0.12f + fx.Radius / LifePulseSpeed;  // when the pulse reaches them
            float scale = Mathf.Max(0.5f, p.Def.BodyR / 13f);
            V2 at = p.Disp;
            float fade = a < inv ? 1f : Mathf.Clamp01(1f - (a - inv) / 0.7f);
            float hold = a >= arrive && a <= inv ? Mathf.Clamp01((a - arrive) / 0.25f) : 0f;
            if (a > inv - 0.6f && a <= inv && (int)(a * 10f) % 2 == 1)  // about to end: blink
                hold *= 0.25f;

            if (hold > 0f)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(a * 6f + who);
                float h = 100f * scale, w = 20f * scale, feet = at.Y + 22f * scale;
                const int slices = 12;
                for (int i = 0; i < slices; i++)  // the column, brightest at their feet
                {
                    float u = i / (float)slices;
                    SetColor(LifeGreen.Mix(Rgba.White, 0.3f * u).WithAlpha(0.5f * (1f - u) * hold * fade * pulse));
                    float y0 = feet - h * u, y1 = feet - h * (u + 1f / slices);
                    Quad(new V2(at.X - w, y0), new V2(at.X + w, y0), new V2(at.X + w, y1), new V2(at.X - w, y1));
                }
                Arc(at + new V2(0f, 16f * scale), 26f * scale, 3f, LifeGreen.WithAlpha(0.35f * hold * fade), 28);
                for (int m = 0; m < 5; m++)  // motes sinking down the column
                {
                    float ph = MathX.Fmod(a * 0.5f + Hash01(who * 7f + m), 1f);
                    V2 mote = new V2(at.X + (Hash01(who * 3f + m) - 0.5f) * 34f * scale + Mathf.Sin(a * 3f + m) * 4f, at.Y - 70f * scale + ph * 80f * scale);
                    Circle(mote, 1.8f, LifePale.WithAlpha(hold * fade * (1f - ph) * 0.9f), 6);
                }
            }

            if (a > 0f && a < inv + 0.7f)  // the halo: drops in as the pulse lands, lifts away at the end
            {
                float drop = EaseOut3(Mathf.Clamp01((a - (arrive - 0.1f)) / 0.45f));
                float lift = a > inv ? EaseOut3((a - inv) / 0.7f) : 0f;
                float hy = at.Y + (-30f - (1f - drop) * 55f - lift * 30f) * scale, ha = Mathf.Clamp01(drop * 2f) * (1f - lift);
                const int segs = 20;
                for (int i = 0; i < segs; i++)
                {
                    float t0 = MathX.Tau * i / segs, t1 = MathX.Tau * (i + 1) / segs;
                    V2 p0 = new V2(at.X + Mathf.Cos(t0) * 15f * scale, hy + Mathf.Sin(t0) * 5f * scale);
                    V2 p1 = new V2(at.X + Mathf.Cos(t1) * 15f * scale, hy + Mathf.Sin(t1) * 5f * scale);
                    Line(p0, p1, 8f * scale, LifeGreen.WithAlpha(0.22f * ha));
                    Line(p0, p1, 3f * scale, LifePale.WithAlpha(ha));
                }
            }
        }
    }
}
