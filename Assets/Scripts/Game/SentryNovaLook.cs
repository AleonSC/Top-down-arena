using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Turret and Nova abilities (Core/SentryNova.cs).
    /// Sentry, the Turret: a steel pod drops in and lands with a thud and a dust ring, three legs and the head unfold, the
    ///   eye boots up; the head eases round to its target and its twin barrels fire in turn, each kicking back with a cyan
    ///   flash and a wisp of smoke; idle, it sweeps a faint scan cone; ten segments round its feet count its time down and
    ///   blink amber in its last 1.5 s; when it runs out (`FxKind.Sentry` mode 1) the eye dies, the legs fold, the ring
    ///   collapses and it settles into a puff of smoke. Shots: short cyan tracers.
    /// Nova: a white-hot flash at the caster, a ring running out with the shots, a spoke of light behind each one and a
    ///   twelve-point sigil fading on the floor; the shots are lilac comets.
    /// Visual effects off: the plain hexagon, ring and dots. Sounds: SentryNovaSoundBank.cs, played here as each effect arrives.
    public partial class ArenaApp
    {
        static readonly Rgba SnSteel0 = new Rgba(0.12f, 0.14f, 0.18f), SnSteel1 = new Rgba(0.27f, 0.31f, 0.37f), SnSteel2 = new Rgba(0.5f, 0.56f, 0.63f);
        static readonly Rgba SnAmber = new Rgba(1f, 0.68f, 0.28f), SnDust = new Rgba(0.6f, 0.62f, 0.66f), SnHot = new Rgba(0.85f, 1f, 1f);
        static readonly Rgba NovaHot = new Rgba(1f, 0.93f, 1f);

        /// One turret as drawn: its head's angle (eased toward its aim), its last shot (renderer clock) and which barrel fired.
        class SentrySeen { public float Ang, LastShot = -10f; public int Barrel, Frame; }
        readonly Dictionary<int, SentrySeen> sentries = new Dictionary<int, SentrySeen>();
        readonly HashSet<int> sentryShots = new HashSet<int>();
        readonly List<int> sentryGone = new List<int>();
        readonly HashSet<Effect> snHeard = new HashSet<Effect>();
        readonly List<Effect> snHeardGone = new List<Effect>();
        float snLastT;
        int snFrame;

        void ClearSentryNovaLook()
        {
            sentries.Clear();
            sentryShots.Clear();
            snHeard.Clear();
        }

        /// Each frame: ease each turret's head, catch its new shots (recoil, flash, smoke), and the sound, sparks and kick of
        /// each Nova and Sentry effect as it arrives.
        void UpdateSentryNovaLook(float t)
        {
            float dt = Mathf.Clamp(t - snLastT, 0f, 0.1f);
            snLastT = t;
            snFrame++;
            foreach (var m in game.Minions.Values)
            {
                if (m.Kind != MinionKind.Turret)
                    continue;
                SentrySeen s;
                if (!sentries.TryGetValue(m.Id, out s))
                    sentries[m.Id] = s = new SentrySeen { Ang = m.Aim };
                s.Frame = snFrame;
                float age = SentryAge(m), aim = m.Aim;
                if (age > Game.SentryDeploy && t - s.LastShot > 1f)
                    aim += 0.55f * Mathf.Sin(t * 1.4f + m.Id);  // idle: sweeping for something to shoot
                float turn = MathX.Fmod(aim - s.Ang + MathX.Pi, MathX.Tau) - MathX.Pi;  // the short way round
                s.Ang += turn * (1f - Mathf.Exp(-14f * dt));
            }
            sentryGone.Clear();
            foreach (var kv in sentries)
                if (kv.Value.Frame != snFrame)
                    sentryGone.Add(kv.Key);
            foreach (int id in sentryGone)
                sentries.Remove(id);

            if (sentryShots.Count > 400)
                sentryShots.RemoveWhere(id => !game.Bullets.ContainsKey(id));
            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind != Defs.TurretShot || !sentryShots.Add(b.Id))
                    continue;
                Minion from = null;
                float best = 44f * 44f;
                foreach (var m in game.Minions.Values)
                    if (m.Kind == MinionKind.Turret && m.Pos.DistanceSq(b.Pos) < best)
                    {
                        best = m.Pos.DistanceSq(b.Pos);
                        from = m;
                    }
                SentrySeen s;
                if (from == null || !sentries.TryGetValue(from.Id, out s))
                    continue;
                s.LastShot = t;
                s.Barrel ^= 1;
                if (!b.Vel.IsZero)
                    s.Ang = b.Vel.Angle;
                if (fxOn && !fxReduced)
                {
                    V2 d = V2.FromAngle(s.Ang), tip = from.Disp + d * 20f + d.Orthogonal() * (s.Barrel == 0 ? -3.2f : 3.2f);
                    AddPart(new Part { Kind = PDust, Pos = tip, Vel = d * Rnd(14f, 30f) + d.Orthogonal() * Rnd(-8f, 8f), Life = Rnd(0.25f, 0.4f), Max = 0.4f, Size = Rnd(1.5f, 2.4f), Col = SnDust });
                }
            }

            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.Nova && fx.Kind != FxKind.Sentry || !snHeard.Add(fx))
                    continue;
                int owner = fx.Kind == FxKind.Nova ? (int)fx.Pos2.X : (int)fx.Radius;
                bool mine = owner == game.MyId;
                float quiet = mine ? 0f : game.QuietAt(fx.Pos);
                float now = Time.unscaledTime;
                if (fx.Kind == FxKind.Nova)
                {
                    sounds?.Play(new SoundCue { Sfx = Sfx.NovaBurst, Ally = !mine, Quiet = quiet }, now);
                    if (mine)
                        Shake(0.16f);
                    if (fxOn)
                    {
                        int n = Mathf.Max(1, (int)fx.Pos2.Y), step = fxReduced ? 4 : 2;
                        for (int i = 0; i < n; i += step)
                        {
                            V2 d = V2.FromAngle(fx.Radius + MathX.Tau * (i + 0.5f) / n);
                            AddPart(new Part { Kind = PSpark, Pos = fx.Pos + d * 20f, Vel = d * Rnd(300f, 460f), Life = Rnd(0.18f, 0.28f), Max = 0.28f, Size = 3.5f, Col = Game.NovaColor });
                        }
                    }
                }
                else if (fx.Pos2.X == 1f)
                    sounds?.Play(new SoundCue { Sfx = Sfx.TurretFold, Ally = !mine, Quiet = quiet }, now);
                else if (fx.Pos2.X == 0f)
                    sounds?.Play(new SoundCue { Sfx = Sfx.TurretDeploy, Ally = !mine, Quiet = quiet }, now);
            }
            if (snHeard.Count > 0)
            {
                snHeardGone.Clear();
                foreach (var k in snHeard)
                    if (!game.Effects.Contains(k))
                        snHeardGone.Add(k);
                foreach (var k in snHeardGone)
                    snHeard.Remove(k);
            }
        }

        static float SentryAge(Minion m) => m.Duration > 0f && m.T >= 0f ? m.Duration - m.T : 99f;

        static float SnEaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

        static float SnEaseBack(float k)
        {
            k = Mathf.Clamp01(k);
            const float s = 1.9f;
            float u = k - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }

        /// A turret on the field (WorldEnemies.cs DrawMinion, Visual effects on).
        void DrawSentry(Minion m, float t)
        {
            V2 c = m.Disp;
            float age = SentryAge(m);
            if (age < Game.SentryLand)
            {
                DrawSentryPod(c, age / Game.SentryLand, 1f, m.Id);
                return;
            }
            SentrySeen s;
            sentries.TryGetValue(m.Id, out s);
            float ang = s != null ? s.Ang : m.Aim, shotAge = s != null ? t - s.LastShot : 10f;
            float u = (age - Game.SentryLand) / (Game.SentryDeploy - Game.SentryLand);
            float open = SnEaseBack(u), head = 0.55f + 0.45f * SnEaseBack(u * 1.25f - 0.25f);
            float eye = age < Game.SentryDeploy ? (u > 0.55f && Mathf.Sin(age * 90f) > -0.2f ? 1f : 0.15f) : 1f;
            bool warn = m.T >= 0f && m.T < 1.5f;
            float frac = m.Duration > 0f && m.T >= 0f ? MathX.Clamp01(m.T / m.Duration) : 1f;
            bool idle = age > Game.SentryDeploy && shotAge > 1f;
            DrawSentryBody(c, ang, open, head, frac, warn, eye, s != null ? s.Barrel : 0, shotAge, idle, m.Id, t);
        }

        /// The closed pod: dropping in (k 0..1, big and faint as it falls, a shadow gathering under it) or settled (k 1).
        void DrawSentryPod(V2 c, float k, float alpha, int seed)
        {
            float fall = (1f - k) * (1f - k), sc = 1f + 1.6f * fall;
            Circle(c + new V2(2f, 3f), 12f * (0.45f + 0.55f * k), new Rgba(0f, 0f, 0f, 0.35f * k * alpha), 18);
            float a = alpha * (0.35f + 0.65f * k);
            Circle(c, 10f * sc, SnSteel0.WithAlpha(a), 20);
            Circle(c, 8.5f * sc, SnSteel1.WithAlpha(a), 20);
            for (int i = 0; i < 3; i++)
            {
                V2 d = V2.FromAngle(seed * 1.9f + i * MathX.Tau / 3f);
                Line(c + d * 3f * sc, c + d * 8.5f * sc, 1.2f * sc, SnSteel0.WithAlpha(a));
            }
            Arc(c, 9.5f * sc, 1.6f * sc, Game.SentryColor.WithAlpha(0.7f * a), 20);
            Circle(c, 2.2f * sc, Game.SentryColor.WithAlpha(a), 10);
        }

        /// The turret itself: legs (open 0..1), base and timer ring (frac of its time left), the head (scale `head`, turned to
        /// `ang`) with its twin barrels, the last-fired one kicked back and flashing for `shotAge` seconds, the eye and, idle,
        /// its scan cone.
        void DrawSentryBody(V2 c, float ang, float open, float head, float frac, bool warn, float eye, int barrel, float shotAge, bool idle, int seed, float t, float alpha = 1f)
        {
            Rgba cy = Game.SentryColor;
            bool blink = warn && MathX.Fmod(t, 0.24f) < 0.12f;
            Circle(c + new V2(2f, 3f), 17f * Mathf.Max(0.6f, open), new Rgba(0f, 0f, 0f, 0.3f * alpha), 20);
            if (idle && open >= 0.99f)
            {
                Wedge(c, 88f, ang, 0.28f, cy.WithAlpha(0.06f * alpha), 12);
                for (int e = -1; e <= 1; e += 2)
                {
                    V2 d = V2.FromAngle(ang + 0.28f * e);
                    Line(c + d * 22f, c + d * 88f, 1f, cy.WithAlpha(0.16f * alpha));
                }
            }
            for (int k = 0; k < 3; k++)
            {
                V2 d = V2.FromAngle(seed * 1.9f + k * MathX.Tau / 3f);
                V2 foot = c + d * (5f + 13f * open), knee = c + d * (5f + 7f * open);
                Line(c, foot, 4.6f, SnSteel0.WithAlpha(alpha));
                Line(c, foot, 2.4f, SnSteel1.WithAlpha(alpha));
                Circle(foot, 0.6f + 2.9f * Mathf.Clamp01(open), SnSteel1.WithAlpha(alpha), 10);
                Circle(foot, 1.5f, SnSteel2.WithAlpha(alpha), 6);
                Circle(knee, 1.3f, cy.WithAlpha(0.7f * Mathf.Clamp01(open) * alpha), 6);
            }
            Circle(c, 10.5f, SnSteel0.WithAlpha(alpha), 22);
            Arc(c, 10.5f, 1.5f, SnSteel1.WithAlpha(alpha), 22);
            float ring = Mathf.Clamp01(open);
            if (ring > 0.05f)
            {
                const int segs = 10;
                int lit = Mathf.CeilToInt(frac * segs);
                for (int j = 0; j < segs; j++)
                {
                    Rgba col = j < lit ? (warn ? (blink ? SnAmber : SnAmber.WithAlpha(0.35f)) : cy.WithAlpha(0.8f)) : SnSteel1.WithAlpha(0.45f);
                    Arc(c, 21f * (0.7f + 0.3f * ring), 2f, col.WithAlpha(col.A * ring * alpha), 4, -MathX.Pi / 2f + j * MathX.Tau / segs + 0.07f, MathX.Tau / segs - 0.14f);
                }
            }
            // the head
            V2 f = V2.FromAngle(ang) * head, sd = f.Orthogonal();
            System.Func<float, float, V2> P = (x, y) => c + f * x + sd * y;
            Quad(P(-12f, -5f), P(-8f, -5f), P(-8f, 5f), P(-12f, 5f), SnSteel0.WithAlpha(alpha));  // counterweight
            float kick = Mathf.Max(0f, 1f - shotAge / 0.12f) * 4f;
            for (int b = 0; b < 2; b++)
            {
                float y = b == 0 ? -3.2f : 3.2f, r = b == barrel ? kick : 0f;
                Quad(P(4f - r, y - 1.6f), P(19f - r, y - 1.6f), P(19f - r, y + 1.6f), P(4f - r, y + 1.6f), SnSteel0.WithAlpha(alpha));
                Line(P(5f - r, y - 0.6f), P(17f - r, y - 0.6f), 0.8f * head, SnSteel2.WithAlpha(0.6f * alpha));
                Quad(P(17f - r, y - 2.1f), P(20f - r, y - 2.1f), P(20f - r, y + 2.1f), P(17f - r, y + 2.1f), SnSteel1.WithAlpha(alpha));
            }
            Quad(P(-8f, -7f), P(5f, -7f), P(5f, 7f), P(-8f, 7f), SnSteel1.WithAlpha(alpha));
            Quad(P(5f, -7f), P(8.5f, -4.5f), P(8.5f, 4.5f), P(5f, 7f), SnSteel1.WithAlpha(alpha));
            Quad(P(-6f, -5f), P(3f, -5f), P(3f, 5f), P(-6f, 5f), SnSteel2.WithAlpha(0.3f * alpha));
            Line(P(-6f, -7f), P(3f, -7f), 1.4f * head, cy.WithAlpha(0.8f * alpha));
            Line(P(-6f, 7f), P(3f, 7f), 1.4f * head, cy.WithAlpha(0.8f * alpha));
            Circle(P(-2.5f, 0f), 1.8f * head, SnSteel0.WithAlpha(alpha), 8);
            Rgba eyeCol = warn ? (blink ? SnAmber : SnAmber.Darkened(0.5f)) : cy;
            if (eye > 0.2f)
                GlowDisc(P(6f, 0f), 10f, eyeCol, 0.45f * eye * alpha);
            Circle(P(6f, 0f), 2.4f * head, eyeCol.WithAlpha((0.25f + 0.75f * eye) * alpha), 10);
            if (shotAge < 0.07f)
            {
                float k = shotAge / 0.07f;
                float y = barrel == 0 ? -3.2f : 3.2f;
                V2 tip = P(20.5f - kick, y), fd = f.Normalized();
                GlowDisc(tip, 14f, SnHot, 0.9f * (1f - k) * alpha);
                Line(tip, tip + fd * (4f + 10f * (1f - k)), 1f + 3f * (1f - k), Rgba.White.WithAlpha((1f - k) * alpha));
                Line(tip - fd.Orthogonal() * 4.5f * (1f - k), tip + fd.Orthogonal() * 4.5f * (1f - k), 1.5f, cy.WithAlpha(0.7f * (1f - k) * alpha));
            }
        }

        /// FxKind.Sentry: the landing (dust ring, flash) or the power-down (eye dies, legs fold, ring collapses, smoke).
        void DrawSentryFx(Effect fx)
        {
            V2 c = fx.Pos;
            float age = fx.T;
            int seed = 0;
            if (fx.Pos2.X != 1f)
            {
                if (!fxOn || age < Game.SentryLand)
                    return;
                float u = age - Game.SentryLand, k = u / 0.45f;
                if (u < 0.15f)
                    GlowDisc(c, 32f, Game.SentryColor, 0.55f * (1f - u / 0.15f));
                if (k < 1f)
                {
                    float e = SnEaseOut(k);
                    Arc(c, 14f + 24f * e, 1f + 2f * (1f - k), SnDust.WithAlpha(0.35f * (1f - k)), 32);
                    int n = fxReduced ? 4 : 8;
                    for (int i = 0; i < n; i++)
                    {
                        V2 d = V2.FromAngle(i * MathX.Tau / n + 0.4f + 0.3f * Mathf.Sin(i * 7.1f));
                        Circle(c + d * (13f + 20f * e), (2.2f + 1.4f * Mathf.Abs(Mathf.Sin(i * 3.3f))) * (1f + 0.6f * k), SnDust.WithAlpha(0.24f * (1f - k)), 8);
                    }
                }
                return;
            }
            float aim = fx.Pos2.Y;
            if (!fxOn)
            {
                float f = age / fx.Dur;
                Arc(c, 13f * (1f - f), 2f, Game.SentryColor.WithAlpha(1f - f), 16);
                return;
            }
            const float folded = 0.38f;
            float fk = SnEaseOut(age / folded);
            if (age < folded)
            {
                float eye = age < 0.14f && Mathf.Sin(age * 120f) > 0f ? 0.8f : 0f;
                DrawSentryBody(c, aim, 1f - fk, 1f - 0.45f * fk, 0f, false, eye, 0, 10f, false, seed, game.AnimT);
                Arc(c, 21f * (1f - fk) + 2f, 1.8f, Game.SentryColor.WithAlpha(0.8f * (1f - fk)), 28);
            }
            else
            {
                float k = (age - folded) / (fx.Dur - folded);
                DrawSentryPod(c, 1f, 1f - k, seed);
            }
            if (age > 0.3f)
            {
                float k = (age - 0.3f) / (fx.Dur - 0.3f);
                for (int i = 0; i < 5; i++)
                {
                    V2 d = V2.FromAngle(i * 1.257f + 0.6f);
                    Circle(c + d * (4f + 10f * SnEaseOut(k)) + new V2(0f, -8f * k), (2.5f + 1.2f * (i % 2)) * (1f + 0.8f * k), SnDust.WithAlpha(0.26f * (1f - k)), 10);
                }
            }
            if (age > 0.12f && age < 0.3f)
            {
                float k = (age - 0.12f) / 0.18f;
                V2 eyeAt = c + V2.FromAngle(aim) * 6f * (1f - 0.45f * fk);
                GlowDisc(eyeAt, 8f * (1f - k), Game.SentryColor, 0.6f * (1f - k));
            }
        }

        /// FxKind.Nova: the flash, the ring running out with the shots, their spokes and the floor sigil.
        void DrawNovaFx(Effect fx)
        {
            V2 c = fx.Pos;
            float age = fx.T, aim = fx.Radius;
            Rgba nc = Game.NovaColor;
            float front = 18f + Game.NovaShotSpeed * age;
            if (!fxOn)
            {
                float f = age / fx.Dur;
                Arc(c, 50f * (0.5f + f), 3f, nc.WithAlpha(1f - f), 32);
                return;
            }
            int n = Mathf.Max(1, (int)fx.Pos2.Y);
            float sig = 1f - age / fx.Dur;
            if (sig > 0f)
            {
                float rot = aim + age * 0.8f, r = 36f + 10f * age;
                const int pts = 12;
                for (int i = 0; i < pts; i++)
                    Line(c + V2.FromAngle(rot + MathX.Tau * i / pts) * r, c + V2.FromAngle(rot + MathX.Tau * (i + 5) / pts) * r, 1.4f, nc.WithAlpha(0.5f * sig * sig));
                Arc(c, r + 6f, 1.5f, nc.WithAlpha(0.4f * sig), 48);
            }
            if (age < 0.22f)
            {
                float k = age / 0.22f;
                GlowDisc(c, 70f + 60f * k, NovaHot, 0.7f * (1f - k));
                Circle(c, 2f + 16f * (1f - k), Rgba.White.WithAlpha(0.85f * (1f - k)), 20);
            }
            if (age < 0.4f)
            {
                float k = age / 0.4f, inner = Mathf.Max(18f, front - 90f);
                for (int i = 0; i < n; i++)
                {
                    V2 d = V2.FromAngle(aim + MathX.Tau * i / n);
                    Line(c + d * inner, c + d * (front - 7f), 1.6f, nc.WithAlpha(0.45f * (1f - k)));
                }
            }
            float rk = age / 0.5f;
            if (rk < 1f)
            {
                Arc(c, front - 6f, 9f, nc.WithAlpha(0.16f * (1f - rk)), 96);
                Arc(c, front, 1f + 2.5f * (1f - rk), Rgba.White.WithAlpha(0.7f * (1f - rk)), 96);
            }
        }

        /// Nova's lilac comets and the turret's cyan tracers (WorldRenderer.cs DrawBigShotLook, Visual effects on).
        bool DrawAbilityShot(Bullet b, float t)
        {
            if (!fxOn || b.Kind != Defs.NovaShot && b.Kind != Defs.TurretShot)
                return false;
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized();
            if (b.Kind == Defs.NovaShot)
            {
                Rgba nc = Game.NovaColor;
                TaperTrail(b, 9, b.Radius * 1.5f, nc, 0.55f);
                Circle(b.Pos, b.Radius * 2.1f, nc.WithAlpha(0.18f), 14);
                Circle(b.Pos, b.Radius, nc, 12);
                Circle(b.Pos - dir, b.Radius * 0.5f, Rgba.White, 8);
                return true;
            }
            TaperTrail(b, 5, 2.6f, Game.SentryColor, 0.85f);
            Circle(b.Pos, b.Radius * 0.9f, Game.SentryColor, 8);
            Circle(b.Pos, b.Radius * 0.5f, Rgba.White, 6);
            return true;
        }
    }
}
