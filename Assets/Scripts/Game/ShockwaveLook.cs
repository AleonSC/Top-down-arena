using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Shockwave ability, look B "Kinetic Burst" of the Shockwave Rework mockup (no floor cracks). One `FxKind.Shockwave`
    /// carries the show, drawn from its age (Core/Shockwave.cs):
    ///   cast   a flash at the caster's feet, a kick of the screen, and a scorch left on the floor that fades over a second
    ///   wave   a thick hot band, ember inside to a white-hot front, with dust thrown off its edge and drifting out
    ///   bullets each enemy shot the front takes shatters into three orange sparks flung outward
    /// The shoved enemies' skid streaks are the usual push look (FxKind.Knock). Visual effects off: a plain gold ring.
    /// Reduced: less dust, no shake.
    public partial class ArenaApp
    {
        static readonly Rgba SwEmber = new Rgba(1f, 0.55f, 0.24f), SwHot = new Rgba(1f, 0.92f, 0.75f), SwDust = new Rgba(0.63f, 0.55f, 0.45f);
        const float SwSparkLife = 0.28f;

        /// One enemy shot the front took: where it was, which way the wave was going, and when (renderer clock).
        struct SwSpark { public V2 Pos, Dir; public float T0, Seed; }
        readonly List<SwSpark> swSparks = new List<SwSpark>();
        Dictionary<int, V2> swPrev = new Dictionary<int, V2>(), swNow = new Dictionary<int, V2>();
        readonly HashSet<Effect> swKicked = new HashSet<Effect>();
        readonly List<Effect> swGone = new List<Effect>();

        void ClearShockwaveLook()
        {
            swSparks.Clear();
            swPrev.Clear();
            swNow.Clear();
            swKicked.Clear();
        }

        /// Each frame: the screen kick, and sparks for the enemy shots that vanished behind a front since the last frame
        /// (the host erases them before the frame is drawn, so the look remembers where they were a frame ago).
        void UpdateShockwaveLook(float t)
        {
            swNow.Clear();
            foreach (var fx in FxOf(FxKind.Shockwave))
            {
                if (fx.Kind != FxKind.Shockwave)
                    continue;
                if (swKicked.Add(fx))
                {
                    var me = game.Me;
                    if (me != null && me.Pos.DistanceTo(fx.Pos) < 1200f)
                        Shake(me.Id == (int)fx.Pos2.X ? 0.34f : 0.2f);
                }
                if (fx.T > Game.ShockwaveSweep + 0.1f)
                    continue;
                float r = Game.ShockwaveR(fx.T, fx.Radius) + 30f;
                foreach (var kv in swPrev)
                    if (!game.Bullets.ContainsKey(kv.Key) && kv.Value.DistanceTo(fx.Pos) <= r && swSparks.Count < 200)
                        swSparks.Add(new SwSpark { Pos = kv.Value, Dir = (kv.Value - fx.Pos).Normalized(), T0 = t, Seed = kv.Key * 0.618f });
                foreach (var b in game.Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(fx.Pos) < fx.Radius + 60f)
                        swNow[b.Id] = b.Pos;
            }
            var swap = swPrev;
            swPrev = swNow;
            swNow = swap;
            swSparks.RemoveAll(s => t - s.T0 > SwSparkLife || t < s.T0);
            if (swKicked.Count > 0)
            {
                swGone.Clear();
                foreach (var k in swKicked)
                    if (!game.Effects.Contains(k))
                        swGone.Add(k);
                foreach (var k in swGone)
                    swKicked.Remove(k);
            }
        }

        /// The scorch under the enemies.
        void DrawShockwaveFloor()
        {
            if (!fxOn)
                return;
            foreach (var fx in FxOf(FxKind.Shockwave))
            {
                if (fx.Kind != FxKind.Shockwave)
                    continue;
                float a = Mathf.Clamp01(1f - (fx.T - 0.6f) / 1f);
                if (a > 0f)
                    FanGradient(fx.Pos, 46f, new Rgba(0.08f, 0.05f, 0.04f, 0.6f * a), new Rgba(0.08f, 0.05f, 0.04f, 0f), 24);
            }
        }

        /// The wave, its flash and dust (the effect layer).
        void DrawShockwave(Effect fx)
        {
            float age = fx.T, R = fx.Radius;
            if (!fxOn)
            {
                float f = age / 0.45f;
                if (f < 1f)
                    Arc(fx.Pos, R * f, 3f, new Rgba(1f, 0.9f, 0.5f, 1f - f), 64);
                return;
            }
            V2 c = fx.Pos;
            if (age < 0.22f)
                GlowDisc(c, 90f, SwHot, 0.75f * (1f - age / 0.22f));
            float r = Game.ShockwaveR(age, R);
            float fade = age < Game.ShockwaveSweep ? 1f : Mathf.Clamp01(1f - (age - Game.ShockwaveSweep) / 0.28f);
            if (fade > 0f && r > 4f)
            {
                float inner = Mathf.Max(0f, r - 46f), span = r - inner;
                Rgba a0 = SwEmber.WithAlpha(0f), a1 = new Rgba(1f, 0.63f, 0.31f, 0.3f * fade), a2 = SwHot.WithAlpha(0.95f * fade), a3 = new Rgba(1f, 1f, 1f, 0f);
                BfBand(c, inner, inner + span * 0.7f, a0, a1, 96);         // (BloodFreezeLook.cs: a ring fading from one colour to the next)
                BfBand(c, inner + span * 0.7f, inner + span * 0.93f, a1, a2, 96);
                BfBand(c, inner + span * 0.93f, r + 4f, a2, a3, 96);
                Arc(c, r, 1.5f, new Rgba(1f, 1f, 1f, 0.55f * fade), 96);
            }
            // dust thrown off the front while it runs, drifting out and fading
            const float rate = 220f, life = 0.7f;
            int step = fxReduced ? 3 : 1;
            int i0 = Mathf.Max(0, Mathf.CeilToInt((age - life) * rate)), i1 = Mathf.FloorToInt(Mathf.Min(age, Game.ShockwaveSweep) * rate);
            for (int i = i0; i <= i1; i += step)
            {
                float ts = i / rate, u = age - ts, k = u / (life * (0.6f + 0.4f * Hash01(i * 2.3f)));
                if (k >= 1f || u < 0f)
                    continue;
                V2 dir = V2.FromAngle(Hash01(i * 1.7f + 0.3f) * MathX.Tau);
                float d = Game.ShockwaveR(ts, R) + 160f / 4f * (1f - Mathf.Exp(-4f * u));
                float sz = (4f + 7f * Hash01(i * 3.1f)) * (1f + k);
                Circle(c + dir * d, sz, SwDust.WithAlpha(0.28f * (1f - k)), 8);
            }
            DrawShockwaveSparks();
        }

        void DrawShockwaveSparks()
        {
            float t = game.AnimT;
            foreach (var s in swSparks)
            {
                float u = t - s.T0, k = u / SwSparkLife;
                if (k < 0f || k >= 1f)
                    continue;
                float ang = s.Dir.Angle;
                Rgba col = new Rgba(1f, 0.75f - 0.3f * k, 0.35f, 1f - k);
                for (int j = 0; j < 3; j++)
                {
                    float a = ang + (Hash01(s.Seed + j * 1.37f) - 0.5f) * 1.1f, sp = 220f + 180f * Hash01(s.Seed * 2.1f + j);
                    V2 d = V2.FromAngle(a);
                    V2 at = s.Pos + d * (sp / 7f * (1f - Mathf.Exp(-7f * u)));
                    Line(at, at - d * (sp * Mathf.Exp(-7f * u) * 0.045f + 2f), 2.5f, col);
                }
            }
        }
    }
}
