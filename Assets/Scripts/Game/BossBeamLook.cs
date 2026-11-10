using UnityEngine;

namespace Arena
{
    /// The boss wave's beam-in (Core/BossBeam.cs; Docs/Mockups/boss-teleport.html, option A "Beam up"), in the game's own pillar of
    /// light (LaunchIntro.cs, Recall.cs). One FxKind.BossBeam per player outside the hub; `fx.T` is its age:
    ///   0.0 s   a landing ring starts pulsing in the hub, and a pillar of their colour comes down onto them while two halos close in
    ///   0.8 s   they go up it: a ring, a flash and a streak up the beam; the body is hidden (`BeamHides`)
    ///   1.1 s   a second pillar comes down onto the landing ring
    ///   1.35 s  they re-form there, stretching down out of the light, with a shock ring and sparks
    ///   1.9 s   it's over (the host starts the wave)
    public partial class ArenaApp
    {
        const float BeamLiftAt = 0.8f, BeamDropAt = 1.1f, BeamLandAt = 1.35f;

        /// Gone up the pillar, not landed yet (the real match only).
        bool BeamHides(Player p)
        {
            if (!ReferenceEquals(game, session.Game))
                return false;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.BossBeam && (int)(fx.Radius + 0.5f) == p.Id && fx.T >= BeamLiftAt && fx.T < BeamLandAt)
                    return true;
            return false;
        }

        /// WorldEffects.cs: FxKind.BossBeam.
        void DrawBossBeamFx(Effect fx)
        {
            float a = fx.T, r = 16f;
            Player p;
            if (game.Players.TryGetValue((int)(fx.Radius + 0.5f), out p))
                r = p.Def.BodyR;
            Rgba col = fx.Color, hot = col.Mix(Rgba.White, 0.35f);
            V2 from = fx.Pos, to = fx.Pos2;

            // The landing ring in the hub: dashed, pulsing, brighter as the second pillar nears.
            if (a < BeamLandAt)
            {
                float near = Seg(a, 0f, BeamDropAt), pulse = 0.5f + 0.5f * Mathf.Sin(a * 9f);
                float rr = r + 8f + 2f * pulse;
                for (int i = 0; i < 12; i++)
                    Arc(to, rr, 2f, col.WithAlpha(0.35f + 0.4f * near), 6, (i * 30f + a * 40f) * Mathf.Deg2Rad, 16f * Mathf.Deg2Rad);
                Arc(to, rr + 9f, 1.2f, hot.WithAlpha(0.2f + 0.25f * near), 40);
                Circle(to, rr - 2f, col.WithAlpha(0.06f + 0.1f * near), 24);
            }

            // Where they stood: the pillar comes down, the halos close in, then they go up it.
            if (a < BeamLiftAt + 0.35f)
            {
                float up = Seg(a, 0f, BeamLiftAt), fade = 1f - Seg(a, BeamLiftAt, BeamLiftAt + 0.35f);
                float inten = a < BeamLiftAt ? 0.25f + 0.75f * EaseOut(up) : fade;
                float w = 40f * (0.5f + 0.5f * up) * (a < BeamLiftAt ? 1f : 0.2f + 0.8f * fade);
                DrawBeamAt(from, r, col, inten, w);
                if (a < BeamLiftAt)
                    for (int h = 0; h < 2; h++)  // two halos, the second a beat behind
                    {
                        float g = Mathf.Clamp01(up * 1.25f - h * 0.25f);
                        Arc(from, r + 34f * (1f - g) + 4f, 2.5f - h, hot.WithAlpha(0.9f * g * (1f - 0.35f * h)), 32);
                    }
                // sparks rising up the beam
                for (int i = 0; i < 9; i++)
                {
                    float f = (a * 1.4f + i * 0.111f) % 1f, side = ((i * 37) % 13 - 6f) * 2.2f * (1f - f * 0.5f);
                    float sa = Mathf.Sin(Mathf.PI * f) * inten;
                    V2 sp = from + new V2(side, -f * 170f);
                    Line(sp, sp + new V2(0f, -8f), 2f, hot.WithAlpha(0.8f * sa));
                }
            }
            float lk = Seg(a, BeamLiftAt, BeamLiftAt + 0.55f);
            if (lk > 0f && lk < 1f)  // gone: a ring out, a flash and a streak up the beam
            {
                Arc(from, 20f + 70f * lk, 4f * (1f - lk) + 1f, col.WithAlpha(1f - lk), 40);
                Circle(from, 30f * (1f - lk), Rgba.White.WithAlpha(0.6f * (1f - lk)), 20);
                float upv = 600f * EaseOut(lk);
                Line(from - new V2(0f, upv), from - new V2(0f, upv + 90f), 6f * (1f - lk), Rgba.White.WithAlpha(0.8f * (1f - lk)));
            }

            // The hub: a pillar down onto the ring, then they re-form out of the light with a shock ring.
            if (a >= BeamDropAt && a < fx.Dur + 0.1f)
            {
                float inten = a < BeamLandAt ? Seg(a, BeamDropAt, BeamLandAt) : 1f - Seg(a, BeamLandAt, BeamLandAt + 0.55f);
                float w = 40f * (a < BeamLandAt ? 1f : 1f - 0.7f * Seg(a, BeamLandAt, BeamLandAt + 0.55f));
                if (inten > 0f)
                    DrawBeamAt(to, r, col, inten, w);
            }
            float lk2 = Seg(a, BeamLandAt, fx.Dur);
            if (a >= BeamLandAt && lk2 < 1f)
            {
                float e = EaseOut(lk2);
                Arc(to, 14f + 56f * e, 4f * (1f - lk2) + 1f, hot.WithAlpha(1f - lk2), 40);
                Arc(to, 8f + 34f * e, 2f * (1f - lk2) + 0.5f, col.WithAlpha(0.8f * (1f - lk2)), 32);
                Circle(to, 26f * (1f - lk2), Rgba.White.WithAlpha(0.5f * (1f - lk2)), 20);
                float sk = Seg(a, BeamLandAt, BeamLandAt + 0.3f);  // the stretch: a streak of light shrinking into them
                if (sk < 1f)
                    Line(to - new V2(0f, 70f * (1f - sk)), to, (r * 1.2f) * (1f - sk) + 2f, Rgba.White.WithAlpha(0.7f * (1f - sk)));
                for (int i = 0; i < 10; i++)  // sparks flying off the landing
                {
                    float ang = i * 0.628f + 0.3f, d = r + 10f + 46f * e;
                    V2 d0 = to + V2.FromAngle(ang) * d, d1 = to + V2.FromAngle(ang) * (d + 9f * (1f - lk2));
                    Line(d0, d1, 2f, hot.WithAlpha(0.9f * (1f - lk2)));
                }
            }
        }
    }
}
