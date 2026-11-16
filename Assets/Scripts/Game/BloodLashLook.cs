using UnityEngine;

namespace Arena
{
    /// The Blood Lash's chains (Core/BloodLash.cs; mockup Docs/Mockups/blood-lash-chains.html). A chain of clotted-blood links runs
    /// from the Hemomancer's whip hand to every enemy in `Player.Chains`: it sags and sways, grows thicker with each link, flickers
    /// when its links are about to drop (the client doesn't know the timers, so it flickers once the enemy is past half the break
    /// distance), and red diamonds over the enemy count its links. Martyr's Leash pulls every chain taut and bright, a pulse runs
    /// out along each, rings beat out from the Hemomancer and a ring round them empties over the Leash's 3 s.
    public partial class ArenaApp
    {
        static readonly Rgba BlDark = new Rgba(0.36f, 0.03f, 0.07f), BlRed = new Rgba(0.85f, 0.1f, 0.18f), BlBright = new Rgba(1f, 0.36f, 0.42f);
        static readonly Rgba BlHot = new Rgba(1f, 0.72f, 0.68f);

        /// Called for each player holding chains or the Leash.
        void DrawLashChains(Player p, float t)
        {
            if (p.Downed)
                return;
            V2 hand = p.Disp + V2.FromAngle(p.Aim) * 14f;
            bool taut = p.LeashOn;
            for (int k = 0; k < p.Chains.Count; k++)
                if (game.Enemies.TryGetValue(p.Chains[k], out var e) && e.Hp > 0f)
                    DrawLashChain(hand, e, p.ChainLinks[k], taut, t, p.LeashT);
            if (!taut)
                return;
            float u = Mathf.Clamp01(p.LeashT / Game.LeashMax);
            for (int q = 0; q < 3; q++)  // rings beating out from you
            {
                float f = MathX.Fmod(t * 1.3f + q / 3f, 1f);
                Arc(p.Disp, p.Def.BodyR + 6f + f * 70f, 2f, BlBright.WithAlpha(0.35f * (1f - f)), 32);
            }
            Arc(p.Disp, p.Def.BodyR + 10f, 3.5f, BlDark.WithAlpha(0.7f), 32);
            Arc(p.Disp, p.Def.BodyR + 10f, 3.5f, BlBright.WithAlpha(0.95f), 32, -MathX.Pi / 2f, MathX.Tau * (1f - u));
        }

        /// A point `u` of the way along a chain from `from` (d: to the enemy), sagging `sag` px to one side and down.
        static V2 BlAt(V2 from, V2 d, V2 nrm, float sag, float u)
        {
            float b = Mathf.Sin(u * MathX.Pi) * sag;
            return from + d * u + nrm * b + new V2(0f, b * 0.4f);
        }

        void DrawLashChain(V2 from, Enemy e, int links, bool taut, float t, float leashT)
        {
            V2 to = e.Disp, d = to - from;
            float len = d.Length;
            if (len < 4f)
                return;
            V2 dir = d / len, nrm = dir.Orthogonal();
            float sag = taut ? 0f : Mathf.Min(26f, len * 0.12f) * (1f + 0.3f * Mathf.Sin(t * 2.2f + e.Id));
            float fade = !taut && len > Game.LashBreak * 0.5f ? (Mathf.Sin(t * 30f) > 0f ? 0.45f : 1f) : 1f;
            int n = Mathf.Max(6, (int)(len / 9f));
            float w = 2.4f + links * 0.7f, lw = 1.5f + links * 0.35f;
            // a glow under the links
            V2 prev = from;
            for (int i = 1; i <= n; i++)
            {
                V2 pt = BlAt(from, d, nrm, sag, (float)i / n);
                Line(prev, pt, 5f + links * 3f, (taut ? BlBright : BlRed).WithAlpha((taut ? 0.3f : 0.16f) * fade));
                prev = pt;
            }
            // the links: an oval, then a bar through the next
            prev = from;
            for (int i = 1; i <= n; i++)
            {
                V2 pt = BlAt(from, d, nrm, sag, (float)i / n), m = (prev + pt) * 0.5f, a = (pt - prev);
                float al = a.Length;
                if (al < 0.5f)
                    continue;
                V2 ad = a / al, an = ad.Orthogonal();
                if (i % 2 == 1)
                {
                    Rgba c = (taut ? BlBright : BlRed).WithAlpha(fade);
                    V2 h = ad * 5.5f, s = an * w;
                    Line(m - h * 0.7f + s, m + h * 0.7f + s, lw, c);
                    Line(m - h * 0.7f - s, m + h * 0.7f - s, lw, c);
                    Line(m - h * 0.7f + s, m - h, lw, c);
                    Line(m - h * 0.7f - s, m - h, lw, c);
                    Line(m + h * 0.7f + s, m + h, lw, c);
                    Line(m + h * 0.7f - s, m + h, lw, c);
                }
                else
                    Line(m - ad * 5.5f, m + ad * 5.5f, lw, BlDark.WithAlpha(fade));
                prev = pt;
            }
            if (taut)  // a pulse running out along the chain
            {
                float u = MathX.Fmod(leashT * 1.6f + e.Id * 0.23f, 1f);
                GlowDisc(BlAt(from, d, nrm, sag, u), 7f, BlHot, 0.85f);
            }
            for (int i = 0; i < links; i++)  // a diamond over the enemy for each link
            {
                V2 c = e.Disp + new V2(-(links - 1) * 5f + i * 10f, -e.Radius - 11f);
                SetColor(BlHot);
                Quad(c + new V2(0f, -4.2f), c + new V2(4.2f, 0f), c + new V2(0f, 4.2f), c + new V2(-4.2f, 0f));
                SetColor(BlRed);
                Quad(c + new V2(0f, -3f), c + new V2(3f, 0f), c + new V2(0f, 3f), c + new V2(-3f, 0f));
            }
        }
    }
}
