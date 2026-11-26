using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Heartbeat's look (Core/Heartbeat.cs; the mockup is the claude.ai artifact "Hemomancer Heartbeat Ult"). Drawing only,
    /// from the replicated Player.HbT: the beats and waves follow from it (Game.HbBeatAt, Game.HbWaveR).
    ///   - under the body (DrawHeartbeatUnder): veins drawn in toward you before each beat; each wave, a jagged crest of blood
    ///     with a pale edge and a fainter echo ring behind it (the "dub"), fading as it reaches its end; a dashed reach
    ///     ring while it runs; for your own cast, three pips under you, one lit per beat;
    ///   - on the floor (DrawHeartbeatFloor): blood flung out on each beat lands as stains that dry away;
    ///   - over the body (DrawHeartbeatOver): the heart on your chest, swelling (white-hot) on each beat, and the flung drops;
    ///   - in the glow pass (DrawHeartbeatGlow): the heart's glow, brightest on the beat, and a glow on each wave's crest.
    /// With Visual effects off: the waves, the reach ring and the pips only.
    public partial class ArenaApp
    {
        class HbDrop { public V2 Pos, Vel; public float Life, Max, R; }
        class HbStain { public V2 Pos; public float R, Life, Ph; }

        readonly List<HbDrop> hbDrops = new List<HbDrop>();
        readonly List<HbStain> hbStains = new List<HbStain>();
        readonly Dictionary<int, float> hbLastSeen = new Dictionary<int, float>();
        readonly System.Random hbRng = new System.Random(23);
        float hbLastT = -1f;
        const float HbStainLife = 4f;

        float HbRand => (float)hbRng.NextDouble();

        /// How swollen the heart is, 0 to 1: up on each beat, easing off over 0.3 s.
        static float HbSwell(Player p)
        {
            int n = Game.HbFired(p.HbT);
            if (n == 0)
                return 0f;
            float since = p.HbT - Game.HbBeatAt(n - 1);
            return Mathf.Clamp01(1f - since / 0.3f);
        }

        /// Building up to the next beat, 0 to 1 (over the last HbWind s before it); 0 after the last.
        static float HbCharge(Player p)
        {
            int n = Game.HbFired(p.HbT);
            if (n >= Game.HbBeats)
                return 0f;
            float to = Game.HbBeatAt(n) - p.HbT;
            return Mathf.Clamp01(1f - to / Game.HbWind);
        }

        /// The drops' clock, and a spray on every beat each game sees, once a frame.
        void TickHeartbeatBits(float t)
        {
            if (t == hbLastT)
                return;
            float dt = hbLastT < 0f ? 0f : Mathf.Clamp(t - hbLastT, 0f, 0.05f);
            hbLastT = t;
            for (int i = hbDrops.Count - 1; i >= 0; i--)
            {
                var d = hbDrops[i];
                d.Life -= dt;
                d.Pos += d.Vel * dt;
                d.Vel *= Mathf.Pow(0.03f, dt);
                if (d.Life <= 0f)
                {
                    if (HbRand < 0.4f)
                        hbStains.Add(new HbStain { Pos = d.Pos, R = d.R * 1.8f, Life = HbStainLife, Ph = HbRand * 6.28f });
                    hbDrops.RemoveAt(i);
                }
            }
            for (int i = hbStains.Count - 1; i >= 0; i--)
            {
                hbStains[i].Life -= dt;
                if (hbStains[i].Life <= 0f)
                    hbStains.RemoveAt(i);
            }
            foreach (var p in game.Players.Values)
            {
                float seen;
                hbLastSeen.TryGetValue(p.Id, out seen);
                hbLastSeen[p.Id] = p.HbT;
                if (!fxOn || !Game.HbOn(p) || Game.HbFired(p.HbT) <= Game.HbFired(seen) || p.HbT < seen)
                    continue;
                for (int i = 0; i < 40; i++)  // the beat flings blood out of you
                {
                    float a = HbRand * MathX.Tau, v = 220f + HbRand * 520f;
                    hbDrops.Add(new HbDrop { Pos = p.Disp + V2.FromAngle(a) * p.Def.BodyR * 0.6f, Vel = V2.FromAngle(a) * v,
                        Life = 0.3f + 0.4f * HbRand, Max = 0.7f, R = 1.6f + HbRand * 2.6f });
                }
                hbStains.Add(new HbStain { Pos = p.Disp, R = p.Def.BodyR * 1.6f, Life = HbStainLife * 0.6f, Ph = HbRand * 6.28f });
            }
            if (hbDrops.Count > 400)
                hbDrops.RemoveRange(0, hbDrops.Count - 400);
            if (hbStains.Count > 160)
                hbStains.RemoveRange(0, hbStains.Count - 160);
        }

        /// WorldRenderer.DrawArenaContents, before anyone's auras: the stains the beats flung out.
        void DrawHeartbeatFloor(float t)
        {
            TickHeartbeatBits(t);
            if (!fxOn)
                return;
            foreach (var s in hbStains)
            {
                float a = Mathf.Clamp01(s.Life / HbStainLife * 1.6f);
                Ellipse(s.Pos, s.R, s.R * 0.78f, s.Ph, Game.HbDark.WithAlpha(0.55f * a), 12);
            }
        }

        /// WorldRenderer.DrawPlayerAuras: the veins, the waves, the reach ring and your pips.
        void DrawHeartbeatUnder(Player p, float t)
        {
            if (!Game.HbOn(p))
                return;
            V2 at = p.Disp;
            float body = p.Def.BodyR, charge = HbCharge(p);
            DashedRing(at, Game.HbReach, Game.HbBlood.WithAlpha(0.22f));
            if (fxOn && charge > 0f)  // veins pulled in toward the heart before the beat
                for (int i = 0; i < 9; i++)
                {
                    float a = i * MathX.Tau / 9f + t * 0.5f, r = body + 52f - 34f * charge;
                    V2 a0 = at + V2.FromAngle(a) * r, a1 = at + V2.FromAngle(a + 0.18f) * (r + 14f), a2 = at + V2.FromAngle(a + 0.05f) * (r + 26f);
                    Line(a0, a1, 1.8f, Game.HbBlood.WithAlpha(0.75f * charge));
                    Line(a1, a2, 1.2f, Game.HbBlood.WithAlpha(0.5f * charge));
                }
            for (int k = 0; k < Game.HbBeats; k++)
            {
                float age = p.HbT - Game.HbBeatAt(k);
                if (age < 0f || age > Game.HbWaveTime + 0.15f)
                    continue;
                float r = Game.HbWaveR(age), f = Mathf.Clamp01(age / Game.HbWaveTime);
                float fade = age > Game.HbWaveTime ? 1f - (age - Game.HbWaveTime) / 0.15f : 1f - 0.45f * f * f;
                DrawHbCrest(at, r, age, 7f * (1f - 0.5f * f) + 1f, Game.HbBlood.WithAlpha(0.9f * fade));
                DrawHbCrest(at, r, age, 1.4f, Game.HbCrest.WithAlpha(0.5f * fade));
                if (r > 70f)  // the echo behind it
                    Arc(at, r - 44f, 3f, Game.HbBlood.WithAlpha(0.32f * fade), 48);
                if (fxOn)
                    Arc(at, Mathf.Max(0f, r - 16f), 22f, Game.HbBlood.WithAlpha(0.1f * fade), 48);
            }
            if (p.Id == game.MyId)  // a pip a beat, lit as it comes
            {
                int n = Game.HbFired(p.HbT);
                for (int i = 0; i < Game.HbBeats; i++)
                    Circle(at + new V2((i - 1) * 9f, body + 9f), 3f, i < n ? Game.HbBlood : Game.HbCrest.WithAlpha(0.3f), 10);
            }
        }

        /// A wave's crest: a ring of blood with a jagged, rippling edge.
        void DrawHbCrest(V2 at, float r, float age, float width, Rgba c)
        {
            const int n = 72;
            V2 prev = HbCrestPt(at, r, 0f, age);
            for (int i = 1; i <= n; i++)
            {
                V2 cur = HbCrestPt(at, r, i * MathX.Tau / n, age);
                Line(prev, cur, width, c);
                prev = cur;
            }
        }

        static V2 HbCrestPt(V2 at, float r, float a, float age)
        {
            float k = r + Mathf.Sin(a * 9f + age * 9f) * 4f + Mathf.Sin(a * 23f - age * 14f) * 2.5f;
            return at + V2.FromAngle(a) * k;
        }

        /// A thin dashed ring: how far the waves reach.
        void DashedRing(V2 at, float r, Rgba c)
        {
            const int dashes = 40;
            for (int i = 0; i < dashes; i++)
                Arc(at, r, 1.2f, c, 3, i * MathX.Tau / dashes, MathX.Tau / dashes * 0.45f);
        }

        /// WorldRenderer, after the body: the flung drops, and the heart on the chest of everyone casting it.
        void DrawHeartbeatOver()
        {
            if (fxOn)
                foreach (var d in hbDrops)
                {
                    float a = Mathf.Clamp01(d.Life / d.Max);
                    Circle(d.Pos, d.R * (0.5f + 0.5f * a), Game.HbBlood.WithAlpha(a), 6);
                }
            foreach (var p in game.Players.Values)
            {
                if (!Game.HbOn(p))
                    continue;
                float swell = HbSwell(p), s = 5.5f * (1f + 0.45f * swell + 0.15f * HbCharge(p));
                DrawHbHeart(p.Disp + new V2(0f, 1f), s, swell > 0.3f ? Game.HbCrest.Mix(Rgba.White, swell) : Game.HbBlood);
            }
        }

        /// A little heart, `s` px across each lobe.
        void DrawHbHeart(V2 c, float s, Rgba col)
        {
            Circle(c + new V2(-s * 0.5f, -s * 0.3f), s * 0.56f, col, 12);
            Circle(c + new V2(s * 0.5f, -s * 0.3f), s * 0.56f, col, 12);
            SetColor(col);
            Tri(c + new V2(-s * 1.02f, -s * 0.12f), c + new V2(s * 1.02f, -s * 0.12f), c + new V2(0f, s * 1.15f));
        }

        /// Juice.DrawGlowPass: the heart's glow, brightest on the beat, and each wave's crest.
        void DrawHeartbeatGlow()
        {
            foreach (var p in game.Players.Values)
            {
                if (!Game.HbOn(p))
                    continue;
                float swell = HbSwell(p), charge = HbCharge(p);
                GlowDisc(p.Disp, p.Def.BodyR * (2.2f + 1.6f * swell + 0.6f * charge), Game.HbBlood, 0.25f + 0.5f * swell + 0.25f * charge);
                for (int k = 0; k < Game.HbBeats; k++)
                {
                    float age = p.HbT - Game.HbBeatAt(k);
                    if (age < 0f || age > Game.HbWaveTime)
                        continue;
                    float r = Game.HbWaveR(age), f = age / Game.HbWaveTime;
                    for (int i = 0; i < 12; i++)
                        GlowDisc(p.Disp + V2.FromAngle(i * MathX.Tau / 12f + age) * r, 26f, Game.HbBlood, 0.18f * (1f - f));
                }
            }
        }
    }
}
