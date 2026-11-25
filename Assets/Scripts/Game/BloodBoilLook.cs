using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Blood Boil's look (Core/BloodBoil.cs; the mockup is the claude.ai artifact "Blood Boil"). Drawing only, from the
    /// replicated Player.BoilT and health (Game.BoilLevel) and FxKind.BoilEnd. Everything grows with the boil:
    ///   - under the body (DrawBoilUnder): a hot glow, three wobbling heat rings that swell on each heartbeat (faster as
    ///     it boils), and for your own boil a thin arc of the time left;
    ///   - on the floor (DrawBoilFloor): boiling blood left where you ran, each puddle popping a bubble now and then;
    ///   - over the body (DrawBoilOver): bubbles rising off you and popping, and wisps of steam;
    ///   - in the glow pass (DrawBoilGlow): the body's heat, brighter on each beat;
    ///   - the end (FxKind.BoilEnd): a burst of steam rolling out of you and a pale ring.
    /// With Visual effects off: only the heat rings and the time arc.
    public partial class ArenaApp
    {
        class BoilBit { public V2 Pos, Vel; public float Life, Max, R; public byte Kind; }  // Kind 0 a bubble, 1 steam
        class BoilPool { public V2 Pos; public float R, Life, Ph; }

        readonly List<BoilBit> boilBits = new List<BoilBit>();
        readonly List<BoilPool> boilPools = new List<BoilPool>();
        readonly System.Random boilRng = new System.Random(11);
        float boilLastT = -1f;
        const float BoilPoolLife = 2.6f;

        float BoilRand => (float)boilRng.NextDouble();

        /// The heartbeat, 0 to 1, a sharp swell once a beat: about 66 a minute cold, 220 at full boil.
        static float BoilBeat(Player p, float t, float boil)
        {
            float rate = 1.1f + 2.6f * boil;
            float ph = (t * rate + p.Id * 0.37f) % 1f;
            return Mathf.Pow(Mathf.Max(0f, Mathf.Sin(ph * Mathf.PI)), 10f);
        }

        /// The particles' clock and the spawning, once a frame.
        void TickBoilBits(float t)
        {
            if (t == boilLastT)
                return;
            float dt = boilLastT < 0f ? 0f : Mathf.Clamp(t - boilLastT, 0f, 0.05f);
            boilLastT = t;
            for (int i = boilBits.Count - 1; i >= 0; i--)
            {
                var b = boilBits[i];
                b.Life -= dt;
                b.Pos += b.Vel * dt;
                if (b.Kind == 1)
                {
                    b.R += dt * 16f;
                    b.Vel *= Mathf.Pow(0.4f, dt);
                }
                if (b.Life <= 0f)
                    boilBits.RemoveAt(i);
            }
            for (int i = boilPools.Count - 1; i >= 0; i--)
            {
                boilPools[i].Life -= dt;
                if (boilPools[i].Life <= 0f)
                    boilPools.RemoveAt(i);
            }
            if (!fxOn || dt <= 0f)
                return;
            foreach (var p in game.Players.Values)
            {
                if (!Game.BoilOn(p))
                    continue;
                float boil = Game.BoilLevel(p);
                V2 at = p.Disp;
                float body = p.Def.BodyR;
                if (BoilRand < dt * (14f + 46f * boil))  // a bubble off the skin
                {
                    float a = BoilRand * MathX.Tau, r = BoilRand * body;
                    boilBits.Add(new BoilBit { Kind = 0, Pos = at + V2.FromAngle(a) * r, Vel = new V2((BoilRand - 0.5f) * 20f, -30f - 60f * BoilRand - 60f * boil),
                        Life = 0.5f + 0.5f * BoilRand, Max = 1f, R = 1.6f + BoilRand * 2.2f + boil * 2.4f });
                }
                if (BoilRand < dt * (3f + 9f * boil))  // a wisp of steam
                    boilBits.Add(new BoilBit { Kind = 1, Pos = at + new V2((BoilRand - 0.5f) * body, -body * 0.5f), Vel = new V2((BoilRand - 0.5f) * 18f, -36f - 30f * boil),
                        Life = 1.3f, Max = 1.3f, R = 6f + 8f * boil });
                if (BoilRand < dt * (5f + 16f * boil))  // boiling blood left on the floor
                {
                    boilPools.Add(new BoilPool { Pos = at + new V2((BoilRand - 0.5f) * 8f, (BoilRand - 0.5f) * 8f), R = 5f + BoilRand * 6f + boil * 5f, Life = BoilPoolLife, Ph = BoilRand * 6.28f });
                }
            }
            if (boilBits.Count > 500)
                boilBits.RemoveRange(0, boilBits.Count - 500);
            if (boilPools.Count > 140)
                boilPools.RemoveRange(0, boilPools.Count - 140);
        }

        /// WorldRenderer.DrawArenaContents, before anyone's auras: the boiling puddles.
        void DrawBoilFloor(float t)
        {
            TickBoilBits(t);
            if (!fxOn)
                return;
            foreach (var pool in boilPools)
            {
                float a = pool.Life / BoilPoolLife;
                Ellipse(pool.Pos, pool.R, pool.R * 0.75f, pool.Ph, Game.BoilBlood.WithAlpha(0.42f * a), 12);
                float pop = Mathf.Sin(t * 9f + pool.Ph * 3f);
                if (pop > 0.55f)
                    Arc(pool.Pos + V2.FromAngle(pool.Ph) * pool.R * 0.35f, 1.5f + (pop - 0.55f) * 7f, 1f, Game.BoilHot.WithAlpha(0.6f * a), 10);
            }
        }

        /// WorldRenderer.DrawPlayerAuras: the glow, the heat rings and your time arc.
        void DrawBoilUnder(Player p, float t)
        {
            if (!Game.BoilOn(p))
                return;
            float boil = Game.BoilLevel(p), beat = BoilBeat(p, t, boil), body = p.Def.BodyR;
            V2 at = p.Disp;
            if (fxOn)
                SoftGlow(at, body * (3.4f + 2.4f * boil) + beat * 8f, Game.BoilHot, 0.22f + 0.3f * boil, 5);
            for (int k = 0; k < 3; k++)  // the heat rings: wobbling, rolling outward, swelling on the beat
            {
                float roll = (t * (10f + 14f * boil)) % 10f;
                float r = body + 8f + k * 10f + beat * 7f + roll;
                float wob = 1.5f + 4f * boil, ph = t * (6f + 10f * boil) + k;
                Rgba c = Game.BoilBlood.Mix(Game.BoilHot, 0.4f + 0.6f * boil).WithAlpha((0.42f - k * 0.12f) * (0.55f + 0.45f * boil) * (1f - roll / 14f));
                const int n = 28;
                V2 prev = at + new V2(r + Mathf.Sin(ph) * wob, 0f);
                for (int i = 1; i <= n; i++)
                {
                    float a = i * MathX.Tau / n, rr = r + Mathf.Sin(a * 7f + ph) * wob;
                    V2 cur = at + V2.FromAngle(a) * rr;
                    Line(prev, cur, 1.6f, c);
                    prev = cur;
                }
            }
            if (p.Id == game.MyId && p.BoilLen > 0f)  // the time left, a thin arc just outside the body
            {
                float left = Mathf.Clamp01(1f - p.BoilT / p.BoilLen);
                Arc(at, body + 5f, 2f, Game.BoilSteam.WithAlpha(0.15f), 32);
                Arc(at, body + 5f, 2f, Game.BoilSteam.WithAlpha(0.75f), 32, -MathX.Pi / 2f, MathX.Tau * left);
            }
        }

        /// WorldRenderer, after the body: the bubbles and steam over everyone boiling.
        void DrawBoilOver()
        {
            if (!fxOn)
                return;
            foreach (var b in boilBits)
            {
                float a = Mathf.Clamp01(b.Life / b.Max);
                if (b.Kind == 1)
                    Circle(b.Pos, b.R, Game.BoilSteam.WithAlpha(0.1f * a), 14);
                else
                {
                    float r = b.R * (1.35f - 0.35f * a);
                    Arc(b.Pos, r, 1.2f, Game.BoilSteam.Mix(Game.BoilHot, a).WithAlpha(a), 10);
                    Circle(b.Pos + new V2(-r * 0.35f, -r * 0.4f), 0.8f, Game.BoilSteam.WithAlpha(0.8f * a), 4);
                }
            }
        }

        /// Juice.DrawGlowPass: the heat in the body, brighter on each beat.
        void DrawBoilGlow()
        {
            float t = game.AnimT;
            foreach (var p in game.Players.Values)
            {
                if (!Game.BoilOn(p))
                    continue;
                float boil = Game.BoilLevel(p), beat = BoilBeat(p, t, boil);
                GlowDisc(p.Disp, p.Def.BodyR * (2.2f + 1.4f * boil), Game.BoilHot, 0.25f + 0.35f * boil + 0.25f * beat);
            }
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.BoilEnd && fx.Dur > 0f)
                {
                    float k = Mathf.Clamp01(fx.T / fx.Dur);
                    GlowDisc(fx.Pos, 40f + 60f * k, Game.BoilSteam, 0.35f * (1f - k));
                }
        }

        /// FxKind.BoilEnd: steam rolling out of you and a pale ring, from the effect's age (`f` 0 to 1).
        void DrawBoilEndFx(Effect fx, float f)
        {
            float k = Mathf.Clamp01(f), fade = 1f - k;
            Arc(fx.Pos, 20f + fx.Radius * 1.6f * Mathf.Sqrt(k), 2.5f * fade + 0.5f, Game.BoilSteam.WithAlpha(0.7f * fade), 36);
            if (!fxOn)
                return;
            for (int i = 0; i < 14; i++)
            {
                float h = i * 1.618f + fx.Pos.X * 0.013f, a = (h % 1f) * MathX.Tau + i * 0.45f;
                float dist = (18f + 70f * ((h * 7.3f) % 1f)) * (1f - (1f - k) * (1f - k));
                V2 at = fx.Pos + V2.FromAngle(a) * dist + new V2(0f, -28f * k);
                Circle(at, 10f + 16f * k + 6f * ((h * 3.1f) % 1f), Game.BoilSteam.WithAlpha(0.14f * fade), 16);
            }
        }
    }
}
