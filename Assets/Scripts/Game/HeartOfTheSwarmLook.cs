using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Heart of the Swarm's look (Core/HeartOfTheSwarm.cs; the mockup is the claude.ai artifact "Heart of the Swarm"). Drawing
    /// only, from the replicated Player.HsT and HsPos: the beats follow from HsT (Game.HsBeatAt).
    ///   - under the bodies (DrawSwarmHeartUnder): a faint pool and dashed ring for the reach, a gold arc filling to the next
    ///     beat (yours brighter), a dotted tether from you to the heart, tendrils reaching to the enemies it will pull, and on
    ///     each beat two rings (a pale "lub" and a dark "dub") rolling out to the reach, with veins and drops of blood flying
    ///     to each ally in reach;
    ///   - over the bodies (DrawSwarmHeartOver): the heart itself, squeezing twice on each beat (lub-dub), with its aorta,
    ///     veins and a wet highlight;
    ///   - in the glow pass (DrawSwarmHeartGlow): the heart's glow, flaring on the beat.
    /// With Visual effects off: the reach ring, the beat arc, the rings and a plain heart.
    public partial class ArenaApp
    {
        readonly Dictionary<int, V2> hsDisp = new Dictionary<int, V2>();
        float hsLastT = -1f;
        static readonly Rgba HsGold = new Rgba(0.95f, 0.76f, 0.31f), HsVein = new Rgba(0.2f, 0.01f, 0.03f);

        /// Where this screen draws the heart: eased after the replicated HsPos, once a frame.
        V2 HsAt(Player p) => hsDisp.TryGetValue(p.Id, out var v) ? v : p.HsPos;

        void TickSwarmHeartDisp(float t)
        {
            if (t == hsLastT)
                return;
            float dt = hsLastT < 0f ? 1f : Mathf.Clamp(t - hsLastT, 0f, 0.1f);
            hsLastT = t;
            float k = 1f - Mathf.Exp(-dt * 16f);
            foreach (var p in game.Players.Values)
            {
                if (!Game.HsOn(p))
                {
                    hsDisp.Remove(p.Id);
                    continue;
                }
                hsDisp[p.Id] = hsDisp.TryGetValue(p.Id, out var v) && v.DistanceTo(p.HsPos) < 400f ? v + (p.HsPos - v) * k : p.HsPos;
            }
        }

        /// Seconds since the last beat (large before the first).
        static float HsSince(Player p)
        {
            int n = Game.HsFired(p.HsT);
            return n == 0 ? 9f : p.HsT - Game.HsBeatAt(n - 1);
        }

        /// The squeeze, 0 to 1: a strong "lub" right on the beat and a smaller "dub" 0.18 s after.
        static float HsSqueeze(Player p)
        {
            float a = HsSince(p);
            return Mathf.Exp(-(a - 0.04f) * (a - 0.04f) / 0.003f) + 0.55f * Mathf.Exp(-(a - 0.22f) * (a - 0.22f) / 0.003f);
        }

        /// Toward the next beat, 0 to 1 (the first fills over the wind-up); 1 after the last.
        static float HsCharge(Player p)
        {
            int n = Game.HsFired(p.HsT);
            if (n >= Game.HsBeats)
                return 1f;
            float from = n == 0 ? 0f : Game.HsBeatAt(n - 1), to = Game.HsBeatAt(n);
            return Mathf.Clamp01((p.HsT - from) / (to - from));
        }

        /// The fade in at the cast and out after the last wave.
        static float HsFade(Player p) => Mathf.Clamp01(p.HsT / 0.25f) * Mathf.Clamp01((Game.HsTotal - p.HsT) / 0.3f + 0.2f);

        /// WorldRenderer.DrawPlayerAuras: the reach, the beat arc, the tether, the tendrils, the beat rings and the heal veins.
        void DrawSwarmHeartUnder(Player p, float t)
        {
            TickSwarmHeartDisp(t);
            if (!Game.HsOn(p))
                return;
            V2 at = HsAt(p);
            float fade = HsFade(p), R = Game.HsReach;
            bool mine = p.Id == game.MyId;
            if (fxOn)
                Circle(at, R, Game.HbBlood.WithAlpha(0.05f * fade), 64);
            const int dashes = 44;  // the reach, crawling round
            for (int i = 0; i < dashes; i++)
                Arc(at, R, 1.6f, Game.HbCrest.WithAlpha(0.35f * fade), 3, i * MathX.Tau / dashes - t * 0.08f, MathX.Tau / dashes * 0.5f);
            float ch = HsCharge(p);
            if (Game.HsFired(p.HsT) < Game.HsBeats)
                Arc(at, R + 7f, mine ? 3.2f : 2f, HsGold.WithAlpha((mine ? 0.85f : 0.45f) * fade), 64, -MathX.Tau * 0.25f, ch * MathX.Tau);
            if (fxOn)  // the tether from you to the heart
            {
                V2 d = at - p.Disp;
                float len = d.Length;
                if (len > 30f)
                    for (float s = p.Def.BodyR; s < len - 40f; s += 12f)
                        Line(p.Disp + d * (s / len), p.Disp + d * ((s + 4f) / len), 1.6f, Game.HbBlood.WithAlpha(0.5f * fade));
                int shown = 0;  // tendrils to the enemies the next beat pulls
                foreach (var e in game.Enemies.Values)
                {
                    V2 to = e.Disp - at;
                    float dist = to.Length;
                    if (dist > R + e.Radius || dist < 60f || shown++ >= 28)
                        continue;
                    V2 side = new V2(-to.Y, to.X) * (1f / dist);
                    float wob = Mathf.Sin(t * 3f + e.Id) * 12f;
                    V2 a0 = at + to * 0.2f, a1 = at + to * 0.5f + side * wob, a2 = e.Disp - to * (e.Radius / dist);
                    float a = (0.18f + 0.3f * ch) * fade;
                    Line(a0, a1, 1.4f + ch, Game.HbBlood.WithAlpha(a));
                    Line(a1, a2, 1.0f + ch, Game.HbBlood.WithAlpha(a));
                }
            }
            for (int k = 0; k < Game.HsBeats; k++)  // each beat: the lub and dub rings
            {
                float age = p.HsT - Game.HsBeatAt(k);
                if (age < 0f || age > Game.HsWaveTime + 0.3f)
                    continue;
                float f = Mathf.Clamp01(age / Game.HsWaveTime), f2 = Mathf.Clamp01(age / (Game.HsWaveTime + 0.3f));
                if (f < 1f)
                    Arc(at, R * Mathf.Sqrt(f), 9f * (1f - f) + 2f, Game.HbCrest.WithAlpha(0.9f * (1f - f)), 64);
                float r2 = R * (1f - (1f - f2) * (1f - f2) * (1f - f2)) * 0.98f;
                Arc(at, r2, 18f * (1f - f2) + 1f, Game.HbDark.WithAlpha(0.7f * (1f - f2)), 64);
                if (age > 0.6f)
                    continue;
                float v = age / 0.6f;  // the veins and drops to each ally in reach
                foreach (var q in game.Players.Values)
                {
                    if (q == p || q.Downed || !q.Disp.CloserThan(at, R + q.Def.BodyR))
                        continue;
                    V2 to = q.Disp - at, mid = (at + q.Disp) * 0.5f + new V2(-to.Y, to.X).Normalized() * 24f;
                    Line(at, mid, 3f, Game.HbCrest.WithAlpha(0.7f * (1f - v)));
                    Line(mid, q.Disp, 3f, Game.HbCrest.WithAlpha(0.7f * (1f - v)));
                    if (!fxOn)
                        continue;
                    for (int i = 0; i < 6; i++)
                    {
                        float u = Mathf.Clamp01(v * (1.1f + i * 0.08f)), e2 = u * u, arc = Mathf.Sin(u * Mathf.PI);
                        V2 dp = at + to * e2 + new V2(0f, -arc * 46f) + new V2(-to.Y, to.X).Normalized() * ((i - 2.5f) * 8f * arc);
                        Circle(dp, 3.6f * (1f - 0.4f * u), Game.HbBlood.Mix(Game.HbCrest, 0.5f), 8);
                    }
                    if (v < 0.5f)
                        Arc(q.Disp, q.Def.BodyR + 6f + 14f * v, 2.5f, Game.HbCrest.WithAlpha(0.8f * (1f - 2f * v)), 24);
                }
            }
        }

        /// WorldRenderer, after the bodies: every caster's heart.
        void DrawSwarmHeartOver(float t)
        {
            TickSwarmHeartDisp(t);
            foreach (var p in game.Players.Values)
            {
                if (!Game.HsOn(p))
                    continue;
                float sq = HsSqueeze(p), since = HsSince(p), fade = HsFade(p);
                float swell = since < 0.5f ? Mathf.Sin(since / 0.5f * Mathf.PI) * 0.09f : 0f;
                float s = 36f * fade * (1f + 0.03f * Mathf.Sin(t * 4f) - 0.16f * sq + swell);
                if (s < 1f)
                    continue;
                DrawBigHeart(HsAt(p), s, sq);
            }
        }

        /// The giant heart, `s` px a lobe, `sq` how hard it squeezes (white-hot at the peak).
        void DrawBigHeart(V2 c, float s, float sq)
        {
            if (fxOn)  // the aorta and the vessels out of the top
            {
                V2 b0 = c + new V2(-s * 0.25f, -s * 0.55f), b1 = c + new V2(-s * 0.42f, -s * 1.1f), b2 = c + new V2(-s * 0.08f, -s * 1.35f);
                Line(b0, b1, s * 0.34f, HsVein);
                Line(b1, b2, s * 0.3f, HsVein);
                Line(b0, b1, s * 0.18f, Game.HbDark);
                Line(b1, b2, s * 0.16f, Game.HbDark);
                V2 c0 = c + new V2(s * 0.25f, -s * 0.6f), c1 = c + new V2(s * 0.6f, -s * 1.05f), c2 = c + new V2(s * 0.85f, -s * 0.95f);
                Line(c0, c1, s * 0.26f, HsVein);
                Line(c1, c2, s * 0.22f, HsVein);
                Line(c0, c1, s * 0.12f, Game.HbDark);
            }
            DrawHbHeart(c, s * 1.12f, HsVein);  // the dark rim
            Rgba body = sq > 0.25f ? Game.HbBlood.Mix(Game.HbCrest, Mathf.Clamp01(sq)) : Game.HbBlood;
            DrawHbHeart(c + new V2(0f, -s * 0.03f), s, body);
            if (!fxOn)
                return;
            DrawHbHeart(c + new V2(-s * 0.12f, -s * 0.12f), s * 0.62f, body.Mix(Game.HbCrest, 0.25f + 0.4f * sq).WithAlpha(0.55f));
            Rgba vein = HsVein.WithAlpha(0.7f);  // the veins on its face
            Line(c + new V2(-s * 0.05f, -s * 0.4f), c + new V2(-s * 0.22f, s * 0.05f), 2f, vein);
            Line(c + new V2(-s * 0.22f, s * 0.05f), c + new V2(-s * 0.02f, s * 0.6f), 2f, vein);
            Line(c + new V2(-s * 0.15f, -s * 0.15f), c + new V2(-s * 0.6f, -s * 0.35f), 1.6f, vein);
            Line(c + new V2(-s * 0.1f, s * 0.2f), c + new V2(s * 0.55f, -s * 0.25f), 1.6f, vein);
            Ellipse(c + new V2(-s * 0.55f, -s * 0.5f), s * 0.2f, s * 0.11f, -0.6f, Rgba.White.WithAlpha(0.18f + 0.3f * sq), 12);
        }

        /// Juice.DrawGlowPass: the heart's glow, flaring on each beat.
        void DrawSwarmHeartGlow()
        {
            foreach (var p in game.Players.Values)
            {
                if (!Game.HsOn(p))
                    continue;
                float sq = HsSqueeze(p), fade = HsFade(p);
                GlowDisc(HsAt(p), 90f + 70f * sq, Game.HbBlood, (0.35f + 0.45f * sq) * fade);
            }
        }
    }
}
