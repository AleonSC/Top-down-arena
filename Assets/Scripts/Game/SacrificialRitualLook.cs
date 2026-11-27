using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Sacrificial Ritual's look (Core/SacrificialRitual.cs; Docs/Mockups/sacrificial-ritual.html, look Bloodmist, claws B Scythe),
    /// drawn from the replicated Player.SacT, SacAge, SacBody and SacKills, and FxKind.SacScythe:
    ///   - under the bodies (DrawSacrificeUnder): the pentagram bleeding onto the floor, the five spikes closing in and the death's
    ///     flash; then the sigil held faint under your body (grey, fallen in a pool), a thin cord to the spirit, a ring of the time
    ///     left and ten pips that light as kills store health;
    ///   - the spirit, drawn in place of the model (DrawSacSpirit): a swirl of blood drops trailing behind as it moves, round a
    ///     small beating heart;
    ///   - a swing (DrawSacScytheFx): one wide crescent of blood sweeping across the aim, pale on its edge.
    /// With Visual effects off: the sigil, the body, the time ring, the pips and the heart only.
    public partial class ArenaApp
    {
        static readonly Rgba SacBlood = new Rgba(0.83f, 0.14f, 0.24f), SacDeep = new Rgba(0.31f, 0.03f, 0.07f), SacPale = new Rgba(1f, 0.82f, 0.86f);
        static readonly Rgba SacHusk = new Rgba(0.23f, 0.17f, 0.18f), SacHuskRim = new Rgba(0.42f, 0.32f, 0.34f);
        const float SacSigilR = 46f;

        static float SacH(float n) { float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }
        static float SacEase(float k) { k = Mathf.Clamp01(k); return k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f; }

        readonly Dictionary<int, V2> sacLast = new Dictionary<int, V2>(), sacVel = new Dictionary<int, V2>();
        float sacClock = -1f;

        /// Out as the spirit: drawn instead of the player's model.
        static bool SacSpiritShown(Player p) => Game.SacSpirit(p);

        /// The sigil, the spikes and the flash in the rite; the body, the cord, the time ring and the pips while you're the spirit.
        void DrawSacrificeUnder(Player p, float t)
        {
            if (!Game.SacOn(p))
                return;
            float age = p.SacAge;
            bool rite = age < Game.SacRite;
            V2 at = rite ? p.Disp : p.SacBody;
            float draw = Mathf.Clamp01(age / 0.6f);
            float a = rite ? 1f : 0.45f + 0.1f * Mathf.Sin(t * 3f);
            if (fxOn)
                GlowDisc(at, SacSigilR * 1.35f, SacBlood, rite ? 0.3f * draw : 0.16f);
            // the circle, then the star drawn point to point
            Arc(at, SacSigilR, 2.4f, SacBlood.WithAlpha(a), 48, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(draw * 2f));
            float seg = Mathf.Clamp01(draw * 2f - 1f) * 5f;
            for (int j = 1; j <= 5; j++)
            {
                float k = Mathf.Clamp01(seg - (j - 1));
                if (k <= 0f)
                    break;
                V2 from = at + V2.FromAngle(-MathX.Pi / 2f + ((j - 1) * 2 % 5) * MathX.Tau / 5f) * SacSigilR;
                V2 to = at + V2.FromAngle(-MathX.Pi / 2f + (j * 2 % 5) * MathX.Tau / 5f) * SacSigilR;
                Line(from, V2.Lerp(from, to, k), 2.2f, SacBlood.WithAlpha(a));
            }
            if (rite)
            {
                // five spikes close in from the star's points
                float k = Mathf.Clamp01((age - 0.6f) / (Game.SacRite - 0.6f));
                if (k > 0f)
                {
                    SetColor(new Rgba(0.76f, 0.07f, 0.18f));
                    for (int j = 0; j < 5; j++)
                    {
                        float ang = -MathX.Pi / 2f + j * MathX.Tau / 5f;
                        float tip = Mathf.Lerp(SacSigilR, 6f, SacEase(k));
                        Tri(at + V2.FromAngle(ang) * tip, at + V2.FromAngle(ang + 0.12f) * SacSigilR, at + V2.FromAngle(ang - 0.12f) * SacSigilR);
                    }
                }
                return;
            }
            // the death's flash
            float since = age - Game.SacRite;
            if (since < 0.5f)
            {
                float k = since / 0.5f;
                Arc(at, SacSigilR * 1.5f * (0.3f + k), 1f + 3f * (1f - k), SacPale.WithAlpha(1f - k), 40);
            }
            // the body, fallen in its blood
            if (fxOn)
                GlowDisc(at + new V2(4f, 6f), 30f, SacDeep, 0.7f);
            float r = p.Def.BodyR;
            Circle(at, r, SacHusk, 24);
            Arc(at, r, 2.2f, SacHuskRim, 24);
            SetColor(new Rgba(0.16f, 0.11f, 0.12f));
            Tri(at + new V2(-r * 0.7f, -r * 0.35f), at + new V2(r * 0.7f, -r * 0.35f), at + new V2(0f, -r * 1.3f));
            // the cord to the spirit
            if (fxOn && p.Disp.FartherThan(at, r * 2f))
            {
                V2 d = p.Disp - at;
                int n = Mathf.Clamp((int)(d.Length / 14f), 2, 60);
                for (int i = 0; i < n; i += 2)
                    Line(at + d * (i / (float)n), at + d * ((i + 1) / (float)n), 1.4f, SacBlood.WithAlpha(0.28f));
            }
            // the time left
            float total = Mathf.Max(0.01f, since + p.SacT), left = Mathf.Clamp01(p.SacT / total);
            float ringR = SacSigilR + 14f;
            Arc(at, ringR, 3.5f, new Rgba(1f, 1f, 1f, 0.08f), 48);
            Arc(at, ringR, 3.5f, left < 0.3f ? Rgba.Gold : SacBlood, 48, -MathX.Pi / 2f, MathX.Tau * left);
            // a pip a kill, all ten gold once full health is stored
            int kills = Mathf.Min(10, p.SacKills);
            for (int i = 0; i < 10; i++)
            {
                V2 pip = at + V2.FromAngle(-MathX.Pi / 2f + (i - 4.5f) * 0.2f) * (ringR + 11f);
                Circle(pip, 3f, i < kills ? (kills >= 10 ? Rgba.Gold : SacBlood) : new Rgba(1f, 1f, 1f, 0.12f), 10);
            }
        }

        /// The blood spirit, in place of the model: drops swirling round a beating heart, trailing as it moves.
        void DrawSacSpirit(Player p, float t, float alpha)
        {
            if (t != sacClock)  // the spirits' velocities, once a frame (the trail)
            {
                float dt = sacClock < 0f ? 0f : Mathf.Clamp(t - sacClock, 0f, 0.05f);
                sacClock = t;
                foreach (var q in game.Players.Values)
                {
                    V2 last;
                    if (!sacLast.TryGetValue(q.Id, out last) || dt <= 0f)
                        last = q.Disp;
                    V2 v = dt > 0f ? (q.Disp - last) / dt : V2.Zero, was;
                    sacVel.TryGetValue(q.Id, out was);
                    sacVel[q.Id] = was + (v - was) * Mathf.Clamp01(dt * 10f);
                    sacLast[q.Id] = q.Disp;
                }
            }
            V2 vel;
            sacVel.TryGetValue(p.Id, out vel);
            if (vel.Length > 600f)
                vel = vel.Normalized() * 600f;
            float rise = Mathf.Clamp01((p.SacAge - Game.SacRite) / 0.4f);
            V2 c = p.Disp + new V2(0f, 10f * (1f - rise));
            float a = alpha * rise;
            if (fxOn)
            {
                GlowDisc(c, 34f, SacBlood, 0.32f * a);
                for (int d = 0; d < 30; d++)
                {
                    float r = 10f + SacH(d) * 20f, spin = (1.5f + SacH(d + 9) * 2f) * (d % 2 == 1 ? 1f : -1f);
                    float ang = t * spin + d, lag = 0.06f * SacH(d + 4);
                    V2 at = c + new V2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * 0.8f) - vel * lag;
                    var col = new Rgba(0.7f + SacH(d + 2) * 0.28f, 0.08f, 0.18f, (0.5f + SacH(d + 1) * 0.5f) * a);
                    Circle(at, 1.5f + SacH(d + 3) * 2.5f, col, 8);
                }
            }
            float beat = 1f + 0.18f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 9f)), 8f);
            float s = 1.25f * beat;
            var heart = new Rgba(1f, 0.2f, 0.33f, a);
            Circle(c + new V2(-3.4f, -2f) * s, 4.3f * s, heart, 12);
            Circle(c + new V2(3.4f, -2f) * s, 4.3f * s, heart, 12);
            SetColor(heart);
            Tri(c + new V2(-7.5f, -0.6f) * s, c + new V2(7.5f, -0.6f) * s, c + new V2(0f, 7.5f) * s);
        }

        /// FxKind.SacScythe: Pos where the spirit swung, Pos2 (aim, owner), Radius the reach. One crescent sweeping across the aim.
        void DrawSacScytheFx(Effect fx)
        {
            float age = fx.T, swing = 0.16f;
            float k = SacEase(age / swing), fade = 1f - Mathf.Clamp01((age - swing) / Mathf.Max(0.01f, fx.Dur - swing));
            if (fade <= 0f || k <= 0f)
                return;
            float ang = fx.Pos2.X, half = Game.SacClawHalf, reach = fx.Radius;
            float a0 = ang - half, a1 = a0 + 2f * half * k;
            V2 c = fx.Pos, inner = c + V2.FromAngle(ang) * reach * 0.13f;
            float ro = reach, ri = reach * 0.62f;
            const int n = 18;
            var fill = new Rgba(1f, 0.24f, 0.36f, 0.8f * fade);
            for (int i = 0; i < n; i++)
            {
                float u0 = a0 + (a1 - a0) * i / n, u1 = a0 + (a1 - a0) * (i + 1) / n;
                V2 o0 = c + V2.FromAngle(u0) * ro, o1 = c + V2.FromAngle(u1) * ro;
                V2 i0 = inner + V2.FromAngle(u0) * ri, i1 = inner + V2.FromAngle(u1) * ri;
                SetColor(fill);
                Quad(o0, o1, i1, i0);
                Line(o0, o1, 2.6f, SacPale.WithAlpha(fade));
            }
            if (fxOn)
                GlowDisc(c + V2.FromAngle(ang) * reach * 0.8f, reach * 0.55f, SacBlood, 0.3f * fade);
        }
    }
}
