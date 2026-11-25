using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Astral Projection's look (Core/AstralProjection.cs; from its mockup, the artifact "Ninja Astral Projection"). Drawing only,
    /// from the replicated ApT, ApHost, ApPos and ApLinkT.
    ///   - under the players (DrawAstralUnder): the gold hex ward turning round the body left behind, the silver cord (a dotted
    ///     curve) from the body to the spirit, the host's dashed tether ring and green mending aura, and the tether itself, a
    ///     pale cyan beam from the host to the spirit, swaying and pulsing;
    ///   - on top (DrawAstralTop): the spirit, a pale glowing ninja bobbing at ApPos with its headband tails and eye slit, and the
    ///     time left as an arc round it; green motes rising off the host;
    ///   - in the glow pass (AstralGlow): a soft cyan light round the spirit.
    public partial class ArenaApp
    {
        readonly Dictionary<int, V2> apDisp = new Dictionary<int, V2>();  // each spirit where this screen draws it (eased after ApPos)

        V2 ApSpiritDisp(Player p)
        {
            V2 at;
            if (!apDisp.TryGetValue(p.Id, out at) || (at - p.ApPos).Length > 400f)
                at = p.ApPos;
            at += (p.ApPos - at) * Mathf.Min(1f, Time.unscaledDeltaTime * 18f);
            apDisp[p.Id] = at;
            return at;
        }

        /// The host's position on this screen (the caster's own body when it rides nobody).
        V2 ApHostDisp(Player p)
        {
            Player h;
            return game.Players.TryGetValue(p.ApHost, out h) ? h.Disp : p.Disp;
        }

        void DrawAstralUnder(Player p, float t)
        {
            if (!Game.ApOn(p))
                return;
            V2 spirit = apDisp.TryGetValue(p.Id, out var sd) ? sd : p.ApPos;
            V2 host = ApHostDisp(p);
            float fade = Mathf.Clamp01(p.ApT / 0.3f), pulse = 0.6f + 0.4f * Mathf.Sin(t * 10f);
            float r = p.Def.BodyR;
            if (Game.ApAway(p))
            {
                // the body's ward: a slow gold hexagon
                float rot = t * 0.8f, wr = r + 11f, wa = (0.45f + 0.35f * Mathf.Sin(t * 6f)) * fade;
                for (int i = 0; i < 6; i++)
                {
                    V2 a = p.Disp + V2.FromAngle(rot + i * MathX.Pi / 3f) * wr, b = p.Disp + V2.FromAngle(rot + (i + 1) * MathX.Pi / 3f) * wr;
                    Line(a, b, 2f, Game.ApWard.WithAlpha(wa));
                }
                Circle(p.Disp, r + 4f, Game.ApSpirit.WithAlpha(0.12f * fade), 20);
                // the silver cord, a dotted arc from the body up to the spirit
                V2 mid = (p.Disp + spirit) * 0.5f + new V2(0f, -60f);
                float shift = Mathf.Repeat(t * 0.6f, 1f / 24f);
                for (float k = shift; k < 1f; k += 1f / 24f)
                {
                    V2 a = ApQuad(p.Disp, mid, spirit, k), b = ApQuad(p.Disp, mid, spirit, Mathf.Min(1f, k + 0.018f));
                    Line(a, b, 1.5f, Game.ApSpirit.WithAlpha(0.35f * fade));
                }
            }
            // the tether ring round the host, dashed and turning
            float ringRot = t * 0.4f;
            for (int i = 0; i < 28; i++)
                Arc(host, Game.ApTether, 1.5f, Game.ApViolet.WithAlpha(0.35f * fade), 4, ringRot + i * MathX.Tau / 28f, MathX.Tau / 28f * 0.5f);
            // the host's mending aura
            Player h;
            float hr = game.Players.TryGetValue(p.ApHost, out h) ? h.Def.BodyR : r;
            Arc(host, hr + 7f + pulse * 2f, 2f, Game.ApMend.WithAlpha((0.4f + 0.3f * pulse) * fade), 28);
            // the tether: a swaying beam, wide and faint under a bright core
            V2 sway = (host + spirit) * 0.5f + new V2(Mathf.Sin(t * 7f), Mathf.Cos(t * 7f)) * 6f;
            const int segs = 14;
            V2 prev = host;
            for (int i = 1; i <= segs; i++)
            {
                V2 q = ApQuad(host, sway, spirit, i / (float)segs);
                Line(prev, q, 7f + pulse * 3f, Game.ApSpirit.WithAlpha(0.14f * fade));
                Line(prev, q, 2.5f + pulse, Game.ApSpirit.WithAlpha((0.55f + 0.3f * pulse) * fade));
                prev = q;
            }
        }

        static V2 ApQuad(V2 a, V2 m, V2 b, float k) => a * ((1f - k) * (1f - k)) + m * (2f * k * (1f - k)) + b * (k * k);

        /// The spirits and the motes off their hosts, over everything in the arena.
        void DrawAstralTop(float t)
        {
            foreach (var p in game.Players.Values)
            {
                if (!Game.ApOn(p))
                    continue;
                V2 s = ApSpiritDisp(p) + new V2(0f, Mathf.Sin(t * 5f + p.Id) * 2f);
                float fade = Mathf.Clamp01(p.ApT / 0.3f), r = p.Def.BodyR + 1f;
                float aim = (p.AimAt - s).IsZero ? p.Aim : (p.AimAt - s).Angle;
                V2 fwd = V2.FromAngle(aim), side = fwd.Orthogonal();
                // the body of light
                Circle(s, r, Game.ApSpirit.WithAlpha(0.28f * fade), 22);
                Arc(s, r, 2f, Game.ApSpirit.WithAlpha(0.9f * fade), 22);
                // headband tails streaming back
                V2 knot = s - fwd * (r * 0.55f);
                for (int k = -1; k <= 1; k += 2)
                {
                    V2 tip = knot - fwd * 11f + side * (k * 5f + Mathf.Sin(t * 9f + k) * 3f);
                    Line(knot, tip, 2f, Game.ApSpirit.WithAlpha(0.85f * fade));
                }
                // eye slit
                Line(s + fwd * (r * 0.35f) - side * 4f, s + fwd * (r * 0.35f) + side * 4f, 2f, Rgba.White.WithAlpha(fade));
                // the time left
                float total = p.ApMastered ? Game.ApTimeMastered : Game.ApTime;
                Arc(s, r + 6f, 1.5f, Game.ApViolet.WithAlpha(0.8f * fade), 24, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.ApT / Mathf.Max(total, p.ApT)));
                // green motes rising off the host
                V2 host = ApHostDisp(p);
                for (int k = 0; k < 4; k++)
                {
                    float u = Mathf.Repeat(t * 0.9f + k * 0.25f, 1f);
                    V2 m = host + new V2(Mathf.Sin(k * 2.3f + p.Id) * 12f, -u * 26f);
                    Rgba c = Game.ApMend.WithAlpha((1f - u) * 0.9f * fade);
                    Line(m - new V2(3f, 0f), m + new V2(3f, 0f), 1.6f, c);
                    Line(m - new V2(0f, 3f), m + new V2(0f, 3f), 1.6f, c);
                }
            }
        }

        /// The glow pass: a soft light round each spirit.
        void AstralGlow(Player p)
        {
            if (!Game.ApOn(p))
                return;
            V2 s = apDisp.TryGetValue(p.Id, out var sd) ? sd : p.ApPos;
            GlowDisc(s, 46f, Game.ApSpirit, 0.35f * Mathf.Clamp01(p.ApT / 0.3f));
        }
    }
}
