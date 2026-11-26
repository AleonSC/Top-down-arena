using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Octopus Familiar's look (Core/OctopusFamiliar.cs; Docs/Mockups/octopus-familiar.html): a violet spirit octopus with a pale
    /// mantle, spots and two big eyes that follow what it reaches for, floating over its shadow beside the player it guards,
    /// a dotted tether to them and a faint ring of its reach round them. Eight tapering arms with pale suckers drift round it;
    /// each `FxKind.OctoSlap` throws its arm (the effect's Radius) out to the enemy and back. Over it: its health bar and a
    /// teal ring of the time it has left; it flashes pale when hurt and blinks out over its last 3 s. The pick (your own
    /// game): reach ring, a ring on every player in reach, a tether to the one under the cursor and its reach to come. The
    /// ink: a splash it pops out of, and a burst of ink blobs as it goes.
    public partial class ArenaApp
    {
        static readonly Rgba OctoBody = new Rgba(0.55f, 0.27f, 0.86f), OctoPale = new Rgba(0.94f, 0.78f, 1f),
            OctoArm = new Rgba(0.62f, 0.33f, 0.91f), OctoSucker = new Rgba(0.97f, 0.85f, 1f), OctoTeal = new Rgba(0.37f, 0.89f, 0.85f),
            OctoInkDark = new Rgba(0.18f, 0.08f, 0.3f);

        readonly Dictionary<int, float> octoFracSeen = new Dictionary<int, float>(), octoHurtAt = new Dictionary<int, float>();
        readonly V2[] octoLeft = new V2[13], octoRight = new V2[13], octoMid = new V2[13];

        /// Where arm i of this octopus joins its body, and which way it points out (the arms drift round slowly).
        static float OctoArmAngle(Minion m, int i, float t) => MathX.Tau * i / Game.OctoArms + t * 0.35f + m.Id;

        void DrawOctopus(Minion m, float t)
        {
            float age = m.Duration - m.T;
            float grow = Mathf.Clamp01(age / 0.45f);
            float scale = grow < 1f ? 1f - Mathf.Pow(1f - grow, 3f) + Mathf.Sin(grow * MathX.Pi) * 0.15f : 1f;  // pops out, a little past full size
            float blink = m.T < 3f && m.T >= 0f ? 0.55f + 0.45f * Mathf.Sin(t * 18f) : 1f;  // blinking out
            float a = blink;
            // Hurt: its health dropped since the last frame.
            float seen;
            if (octoFracSeen.TryGetValue(m.Id, out seen) && m.GuardFrac < seen - 0.001f)
                octoHurtAt[m.Id] = t;
            octoFracSeen[m.Id] = m.GuardFrac;
            float hurtAt, hurt = octoHurtAt.TryGetValue(m.Id, out hurtAt) ? Mathf.Clamp01(1f - (t - hurtAt) / 0.15f) : 0f;

            V2 at = m.Disp + new V2(0f, 3f * Mathf.Sin(t * 2.4f + m.Id));
            Player linked;
            if (game.Players.TryGetValue(m.LinkTo, out linked))
            {
                Arc(linked.Disp, Game.OctoReach, 1.5f, OctoTeal.WithAlpha(0.14f * a), 64);  // its reach round them
                V2 d = linked.Disp - at;
                float len = d.Length;
                if (len > 1f)
                {
                    d /= len;
                    for (float s = 14f - MathX.Fmod(t * 20f, 9f); s < len - linked.Def.BodyR; s += 9f)  // the dotted tether
                        Circle(at + d * s, 1.4f, OctoPale.WithAlpha(0.45f * a), 6);
                }
            }
            Ellipse(m.Disp + new V2(0f, 34f), 15f * scale, 5f * scale, 0f, new Rgba(0f, 0f, 0f, 0.3f * a), 16);  // its shadow on the floor
            Circle(at, 40f * scale, OctoBody.WithAlpha(0.06f * a), 24);  // the spirit glow
            Circle(at, 26f * scale, OctoBody.WithAlpha(0.1f * a), 20);

            // The arms: each strike reaching out to its enemy, the rest drifting.
            float[] strike = octoStrike;
            V2[] goal = octoGoal;
            for (int i = 0; i < Game.OctoArms; i++)
                strike[i] = 0f;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.OctoSlap || fx.Pos.FartherThan(m.Pos, 90f))
                    continue;
                int i = Mathf.Clamp((int)fx.Radius, 0, Game.OctoArms - 1);
                float k = fx.T < Game.OctoLand ? 1f - Mathf.Pow(1f - fx.T / Game.OctoLand, 2f) : 1f - (fx.T - Game.OctoLand) / (Game.OctoStrike - Game.OctoLand);
                strike[i] = Mathf.Clamp01(k);
                goal[i] = fx.Pos2;
            }
            for (int i = 0; i < Game.OctoArms; i++)
            {
                float ang = OctoArmAngle(m, i, t);
                V2 dir = V2.FromAngle(ang);
                V2 root = at + new V2(dir.X * 9f, 6f + dir.Y * 6f) * scale;
                float wave = ang + Mathf.Sin(t * 2.2f + i * 1.7f) * 0.35f, reach = (24f + 4f * Mathf.Sin(t * 3f + i)) * scale;
                V2 idle = root + new V2(Mathf.Cos(wave) * reach, Mathf.Sin(wave) * reach * 0.8f + 6f * scale);
                V2 tip = strike[i] > 0f ? V2.Lerp(idle, goal[i], strike[i]) : idle;
                DrawOctoArm(root, tip, i, strike[i], t, a);
            }

            // The body: the skirt, the mantle (dark under a pale crown), spots and eyes.
            var body = OctoBody.Mix(OctoPale, hurt);
            Ellipse(at + new V2(0f, 5f * scale), 15f * scale, 7f * scale, 0f, body.WithAlpha(a), 18);
            Ellipse(at + new V2(0f, -7f * scale), 14f * scale, 18f * scale, 0f, OctoArm.Mix(OctoPale, Mathf.Min(1f, 0.25f + hurt)).WithAlpha(a), 20);
            Ellipse(at + new V2(-1.5f, -13f * scale), 10f * scale, 11f * scale, 0f, OctoArm.Mix(OctoPale, Mathf.Min(1f, 0.6f + hurt)).WithAlpha(a), 18);
            Ellipse(at + new V2(-4f, -19f * scale), 4f * scale, 3f * scale, -0.5f, new Rgba(1f, 1f, 1f, 0.45f * a), 10);  // the shine
            Circle(at + new V2(-5f, -12f) * scale, 2.2f * scale, OctoBody.WithAlpha(0.7f * a), 8);
            Circle(at + new V2(6f, -17f) * scale, 1.6f * scale, OctoBody.WithAlpha(0.7f * a), 8);
            Circle(at + new V2(3f, -9f) * scale, 1.3f * scale, OctoBody.WithAlpha(0.7f * a), 8);
            V2 look = V2.FromAngle(m.Aim) * 1.8f;
            for (int s = -1; s <= 1; s += 2)
            {
                V2 eye = at + new V2(5.5f * s, 0f) * scale;
                Circle(eye, 4.3f * scale, new Rgba(1f, 1f, 1f, a), 12);
                Circle(eye + look, 2.3f * scale, new Rgba(0.14f, 0.07f, 0.23f, a), 10);
                Circle(eye + look + new V2(-0.8f, -0.9f), 0.8f, new Rgba(1f, 1f, 1f, a), 6);
            }

            // Its time left and its health.
            if (m.T >= 0f && m.Duration > 0f)
                Arc(at + new V2(0f, -4f), 30f, 2f, OctoTeal.WithAlpha(0.65f * a), 40, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
            Bar(at + new V2(-18f, -44f), 36f, 5f, m.GuardFrac, m.GuardFrac > 0.3f ? new Rgba(0.79f, 0.55f, 1f) : new Rgba(1f, 0.42f, 0.49f));
        }

        readonly float[] octoStrike = new float[Game.OctoArms];
        readonly V2[] octoGoal = new V2[Game.OctoArms];

        /// One arm, root to tip: a curve that sways while it rests and straightens as it strikes, thick at the root and thin
        /// at the tip, pale suckers along it; a struck arm's tip swells as it lands.
        void DrawOctoArm(V2 root, V2 tip, int i, float strike, float t, float a)
        {
            V2 d = tip - root;
            float len = Mathf.Max(1f, d.Length);
            V2 n = new V2(-d.Y / len, d.X / len);
            float bend = Mathf.Sin(t * 4f + i * 1.3f) * 8f * (1f - strike) + (i % 2 == 0 ? 1f : -1f) * len * 0.18f * (1f - strike * 0.6f);
            V2 ctrl = root + d * 0.5f + n * bend;
            const int segs = 12;
            for (int k = 0; k <= segs; k++)
            {
                float u = k / (float)segs, w0 = (1f - u) * (1f - u), w1 = 2f * (1f - u) * u, w2 = u * u;
                V2 p = root * w0 + ctrl * w1 + tip * w2;
                V2 g = (ctrl - root) * (2f * (1f - u)) + (tip - ctrl) * (2f * u);
                V2 side = g.IsZero ? n : g.Normalized().Orthogonal();
                float w = Mathf.Lerp(5.2f, 1.1f, u) + (strike > 0.85f && u > 0.9f ? 2f : 0f);
                octoMid[k] = p;
                octoLeft[k] = p + side * w;
                octoRight[k] = p - side * w;
            }
            var col = OctoArm.Mix(OctoPale, strike * 0.5f).WithAlpha(a);
            SetColor(col);
            for (int k = 0; k < segs; k++)
                Quad(octoLeft[k], octoLeft[k + 1], octoRight[k + 1], octoRight[k]);
            Circle(tip, Mathf.Lerp(1.1f, 2.4f, strike), col, 8);
            for (int k = 3; k < segs - 1; k += 2)  // the suckers, on the inside of the curl
                Circle(V2.Lerp(octoMid[k], octoRight[k], 0.55f), Mathf.Max(0.9f, Mathf.Lerp(5.2f, 1.1f, k / (float)segs) * 0.35f), OctoSucker.WithAlpha(0.9f * a), 6);
        }

        /// Your own game, while Octopus Familiar waits for a click: its reach, a ring on every player in it, and on the one
        /// under the cursor a bright turning ring, a dashed tether from you and the reach the octopus will guard.
        void DrawOctoPick(Player me, float t)
        {
            Arc(me.Disp, Game.OctoRange, 1.5f, OctoTeal.WithAlpha(0.18f + 0.06f * Mathf.Sin(t * 3f)), 96);
            var pick = game.OctoPickTarget(me, me.AimAt);
            foreach (var q in game.Players.Values)
            {
                if (q.Pos.FartherThan(me.Pos, Game.OctoRange + q.Def.BodyR))
                    continue;
                float r = q.Def.BodyR + 10f;
                if (q != pick)
                {
                    Arc(q.Disp, r, 2f, Game.OctoColor.WithAlpha(0.6f), 24);
                    continue;
                }
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 9f);
                Arc(q.Disp, Game.OctoReach, 1.5f, OctoTeal.WithAlpha(0.35f), 64);
                Circle(q.Disp, r + 2f * pulse, Game.OctoColor.WithAlpha(0.14f), 28);
                Arc(q.Disp, r + 2f * pulse, 3f, Game.OctoColor.WithAlpha(0.95f), 28);
                for (int k = 0; k < 8; k++)  // eight little arms of light turning round them
                    Arc(q.Disp, r + 7f, 2f, OctoPale.WithAlpha(0.8f), 4, t * 1.5f + k * MathX.Tau / 8f, 0.32f);
                if (q == me)
                    continue;
                V2 d = q.Disp - me.Disp;
                float len = d.Length;
                if (len < 1f)
                    continue;
                d /= len;
                for (float s = me.Def.BodyR + 6f - MathX.Fmod(t * 50f, 16f); s < len - r; s += 16f)  // the tether's dashes creep toward them
                {
                    float a0 = Mathf.Max(me.Def.BodyR + 6f, s), b0 = Mathf.Min(len - r, s + 8f);
                    if (b0 > a0)
                        Line(me.Disp + d * a0, me.Disp + d * b0, 2.5f, Game.OctoColor.WithAlpha(0.85f));
                }
            }
        }

        /// The ink: Radius 0, the splash the octopus pops out of; 1, the burst of blobs as it goes.
        void DrawOctoInkFx(Effect fx, float f)
        {
            if (fx.Radius < 0.5f)
            {
                Arc(fx.Pos, 8f + 40f * f, 3f, Game.OctoColor.WithAlpha(1f - f), 32);
                Circle(fx.Pos, 20f * (1f - f), OctoInkDark.WithAlpha(0.6f * (1f - f)), 18);
                return;
            }
            float seed = fx.Pos.X * 0.37f + fx.Pos.Y * 0.71f;
            for (int i = 0; i < 14; i++)
            {
                float ang = MathX.Tau * i / 14f + Mathf.Sin(seed + i * 3.1f) * 0.4f;
                float dist = (20f + 30f * (0.5f + 0.5f * Mathf.Sin(seed * 1.7f + i))) * (1f - (1f - f) * (1f - f));
                float r = (5f + 5f * (0.5f + 0.5f * Mathf.Sin(seed + i * 5.3f))) * (1f + f * 0.8f);
                Circle(fx.Pos + V2.FromAngle(ang) * dist, r, OctoInkDark.WithAlpha(0.75f * (1f - f)), 12);
            }
            Arc(fx.Pos, 10f + 36f * f, 2f, OctoTeal.WithAlpha(1f - f), 32);
        }
    }
}
