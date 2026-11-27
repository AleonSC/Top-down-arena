using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Shadow Clone Medics' look (Core/ShadowCloneMedics.cs; Docs/Mockups/shadow-clone-medics.html): a shadow clone of the
    /// Ninja in violet ink with a jade rim and headband and a pale medic cross, over a faint ink pool and its shadow, a dotted
    /// jade chakra thread running to its player. It pops up out of its smoke over its first 0.35 s, sends a jade ring over its
    /// player with each heal, flashes pale when it stops a shot, and flickers over its last second. A violet ring round it is
    /// its health. The puffs (`FxKind.CloneMedicPuff`): ink smoke it steps out of, a bigger puff and a falling jade leaf as it
    /// goes, and a small ink ripple where it stopped a shot.
    public partial class ArenaApp
    {
        static readonly Rgba ScmRobe = new Rgba(0.17f, 0.10f, 0.30f), ScmHead = new Rgba(0.11f, 0.07f, 0.2f), ScmPale = new Rgba(0.93f, 0.97f, 0.95f),
            ScmSmoke = new Rgba(0.52f, 0.45f, 0.70f);

        readonly Dictionary<int, float> scmFracSeen = new Dictionary<int, float>(), scmHurtAt = new Dictionary<int, float>();

        void DrawCloneMedic(Minion m, float t)
        {
            float age = m.Duration - m.T;
            float grow = Mathf.Clamp01(age / 0.35f);
            float scale = 1f - (1f - grow) * (1f - grow) * (1f - grow) + Mathf.Sin(grow * MathX.Pi) * 0.12f;
            float a = m.T >= 0f && m.T < 1f ? (Mathf.Sin(t * 38f + m.Id) > 0f ? 0.4f : 0.85f) : 0.92f;  // flickering out
            float seen;
            if (scmFracSeen.TryGetValue(m.Id, out seen) && m.GuardFrac < seen - 0.001f)
                scmHurtAt[m.Id] = t;
            scmFracSeen[m.Id] = m.GuardFrac;
            float hurtAt, hurt = scmHurtAt.TryGetValue(m.Id, out hurtAt) ? Mathf.Clamp01(1f - (t - hurtAt) / 0.18f) : 0f;
            float life = 0.55f + 0.45f * Mathf.Clamp01(m.GuardFrac);  // fainter as its health drops

            V2 c = m.Disp, dir = V2.FromAngle(m.Aim);
            Player linked;
            if (game.Players.TryGetValue(m.LinkTo, out linked))
            {
                V2 d = linked.Disp - c;
                float len = d.Length;
                if (len > 1f)
                {
                    d /= len;
                    for (float s = 12f - MathX.Fmod(t * 24f, 8f); s < len - linked.Def.BodyR + 2f; s += 8f)  // the chakra thread
                        Circle(c + d * s, 1.7f, Game.ScmJade.WithAlpha(0.75f * a), 6);
                }
                float pulse = MathX.Fmod(age, Game.ScmHealEvery) / Game.ScmHealEvery;  // each heal's ring over its player
                if (age > Game.ScmHealEvery && !linked.Downed)
                    Arc(linked.Disp, linked.Def.BodyR + 3f + 12f * pulse, 1.5f, Game.ScmJade.WithAlpha(0.45f * (1f - pulse) * a), 28);
            }
            Ellipse(c + new V2(0f, 14f), 12f * scale, 4f * scale, 0f, new Rgba(0f, 0f, 0f, 0.3f * a), 14);  // shadow
            Circle(c, 24f * scale, ScmSmoke.WithAlpha(0.10f * a), 22);  // the ink pool
            Circle(c, 18f * scale, Game.ScmInk.WithAlpha(0.12f * a), 20);
            float r = 12f * scale;
            Circle(c, r, ScmRobe.Mix(ScmPale, hurt * 0.7f).WithAlpha(a * life + (1f - life) * 0.35f), 20);  // robe
            Arc(c, r, 2f, Game.ScmJade.Mix(ScmPale, hurt).WithAlpha(a), 24);  // the jade rim
            Circle(c + dir * 3.5f * scale, 7.5f * scale, ScmHead.WithAlpha(a), 16);  // head
            Arc(c + dir * 3.5f * scale, 7.5f * scale, 2.5f, Game.ScmJade.WithAlpha(a), 14, m.Aim - 1.1f, 2.2f);  // the headband
            V2 back = c - dir * 9f * scale, side = new V2(-dir.Y, dir.X);
            float wave = Mathf.Sin(t * 9f + m.Id) * 3f;
            Line(back, back - dir * 7f + side * (2f + wave), 1.6f, Game.ScmJade.WithAlpha(0.8f * a));  // its tails
            Line(back, back - dir * 6f - side * (2f - wave), 1.6f, Game.ScmJade.WithAlpha(0.8f * a));
            V2 chest = c - dir * 3f * scale;  // the medic cross
            Line(chest - side * 3f, chest + side * 3f, 1.8f, ScmPale.WithAlpha(0.9f * a));
            Line(chest - dir * 3f, chest + dir * 3f, 1.8f, ScmPale.WithAlpha(0.9f * a));
            float f = Mathf.Clamp01(m.GuardFrac);
            Arc(c, r + 5f, 1.2f, Game.ScmInk.WithAlpha(0.18f * a), 28);
            if (f > 0f)
                Arc(c, r + 5f, 2f, (f > 0.35f ? Game.ScmInk : new Rgba(1f, 0.42f, 0.5f)).WithAlpha(0.9f * a), 28, -MathX.Pi / 2f, MathX.Tau * f);  // its health
            if (hurt > 0f)
                Arc(c, r + 4f + (1f - hurt) * 9f, 2f, ScmPale.WithAlpha(hurt * 0.8f), 24);
        }

        void DrawCloneMedicPuffFx(Effect fx, float f)
        {
            if (fx.Radius > 1.5f)  // a shot it stopped
            {
                Arc(fx.Pos, 4f + 14f * f, 2f, Game.ScmInk.WithAlpha(1f - f), 20);
                Circle(fx.Pos, 5f * (1f - f), ScmRobe.WithAlpha(0.7f * (1f - f)), 10);
                return;
            }
            bool going = fx.Radius > 0.5f;
            float seed = fx.Pos.X * 0.31f + fx.Pos.Y * 0.57f;
            int n = going ? 12 : 9;
            float out1 = 1f - (1f - f) * (1f - f);
            for (int i = 0; i < n; i++)
            {
                float ang = MathX.Tau * i / n + Mathf.Sin(seed + i * 2.3f) * 0.5f;
                float dist = (going ? 30f : 24f) * (0.35f + 0.65f * out1) * (0.6f + 0.4f * Mathf.Sin(seed * 1.3f + i));
                float rr = (4f + 3.5f * (0.5f + 0.5f * Mathf.Sin(seed + i * 4.1f))) * (1f + 0.8f * f);
                Circle(fx.Pos + V2.FromAngle(ang) * dist + new V2(0f, -8f * f), rr, ScmSmoke.WithAlpha(0.3f * (1f - f)), 12);
            }
            Arc(fx.Pos, 8f + (going ? 26f : 20f) * f, 1.8f, Game.ScmJade.WithAlpha(0.8f * (1f - f)), 28);
            if (going)  // a jade leaf drifting down
            {
                V2 at = fx.Pos + new V2(Mathf.Sin(f * 8f + seed) * 10f, -6f + 30f * f);
                float rot = Mathf.Sin(f * 6f + seed) * 0.9f;
                Ellipse(at, 6f, 2.5f, rot, Game.ScmJade.WithAlpha(1f - f), 10);
                Line(at - V2.FromAngle(rot) * 6f, at + V2.FromAngle(rot) * 6f, 0.8f, ScmRobe.WithAlpha(0.6f * (1f - f)));
            }
        }
    }
}
