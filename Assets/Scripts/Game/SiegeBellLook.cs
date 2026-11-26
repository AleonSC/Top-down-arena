using UnityEngine;

namespace Arena
{
    /// The Tank Knight's Siege Bell (Core/SiegeBell.cs), look C "Cathedral" of the Siege Bell mock-up (claude.ai artifact
    /// "Siege Bell"), drawn from the one `FxKind.SiegeBell` effect. A ghostly teal bell is lowered on a shaft of light over its
    /// spot and swings on each toll. Under it: a dashed rune ring marks the shockwave's reach and brightens with each toll; tolls
    /// 1 and 2 send a pair of rune rings out to 120 px; toll 3 flashes white and throws the shockwave (a filled wash, a thick
    /// front with rune marks, a pale inner ring). Then the bell rises and fades. Every enemy the wave stunned wears a small
    /// teal ring that drains with the stun, with sound rippling off its sides (the daze stars are over its head).
    ///
    /// The floor (rings, wash, flash) goes under everything (`DrawSiegeBellFloor`), the bell and the stun marks after the
    /// enemies (`DrawSiegeBellOver`), the halo in the glow pass (`DrawSiegeBellGlow`, from Juice.cs).
    public partial class ArenaApp
    {
        static readonly Rgba BellGhost = Rgb(127, 240, 224), BellHi = Rgb(224, 255, 251), BellDeep = Rgb(40, 120, 130);

        /// The bell's profile: (how far down from the crown, half its width), crown to lip.
        static readonly Vector2[] BellProfile =
        {
            new Vector2(0f, 6f), new Vector2(4f, 12f), new Vector2(14f, 16f), new Vector2(26f, 17f),
            new Vector2(36f, 19f), new Vector2(43f, 23f), new Vector2(50f, 30f),
        };

        static float BellEase(float k) => 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);

        /// How many tolls have rung `since` the cast.
        static int BellRung(float since)
        {
            int n = 0;
            foreach (float t in Game.BellTolls)
                if (since >= t)
                    n++;
            return n;
        }

        /// The rune ring: `n` small diamonds round a circle of radius `r`.
        void BellRunes(V2 c, float r, int n, float size, Rgba col, float turn)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (float)i / n * MathX.Tau + turn;
                Polygon(c + V2.FromAngle(a) * r, size, 4, a, col);
            }
        }

        void BellDashed(V2 c, float r, float w, Rgba col, int dashes)
        {
            for (int i = 0; i < dashes; i++)
            {
                float a = (float)i / dashes * MathX.Tau;
                Arc(c, r, w, col, 3, a, MathX.Tau / dashes * 0.55f);
            }
        }

        /// Under everything: the reach ring, the tolls' rings, the shockwave and its flash.
        void DrawSiegeBellFloor(float t)
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.SiegeBell)
                    DrawSbFloor(fx);
        }

        void DrawSbFloor(Effect fx)
        {
            float since = fx.T, R = fx.Radius, t3 = Game.BellTolls[2];
            V2 c = fx.Pos;
            // The reach: a dashed rune ring that brightens with each toll, then fades after the wave.
            float hold = since < t3 ? 1f : Mathf.Max(0f, 1f - (since - t3) / 1.4f);
            if (hold > 0f)
            {
                float a = (0.14f + 0.1f * BellRung(since)) * hold * Mathf.Clamp01(since / 0.25f);
                BellDashed(c, R, 1.5f, BellGhost.WithAlpha(a), 36);
                Circle(c, 26f, BellGhost.WithAlpha(0.12f * hold), 24);  // the bell's pool of light
            }
            // Tolls 1 and 2: a pair of rune rings out to 120 px.
            for (int i = 0; i < 2; i++)
            {
                float a = since - Game.BellTolls[i];
                if (a < 0f || a > Game.BellRingT)
                    continue;
                float k = a / Game.BellRingT, r = Game.BellRing * BellEase(k);
                Arc(c, r, 6f * (1f - k) + 1f, BellGhost.WithAlpha(0.9f * (1f - k)), 40);
                Arc(c, r * 0.6f, 3f * (1f - k) + 1f, BellGhost.WithAlpha(0.5f * (1f - k)), 32);
                BellRunes(c, r, 16, 2.4f, BellGhost.WithAlpha(0.9f * (1f - k)), r * 0.004f);
            }
            // Toll 3: the shockwave.
            float w = since - t3;
            if (w < 0f)
                return;
            if (w < Game.BellWave + 0.5f)
            {
                float r = Game.BellFrontAt(since, R), k = Mathf.Clamp01(w / Game.BellWave);
                float fade = w < Game.BellWave ? 1f : 1f - (w - Game.BellWave) / 0.5f;
                FanGradient(c, r, BellGhost.WithAlpha(0.04f * fade), BellGhost.WithAlpha(0.16f * fade), 48);
                Arc(c, r, 14f * (1f - k) + 3f, BellGhost.WithAlpha(0.95f * fade), 64);
                Arc(c, Mathf.Max(0f, r - 16f), 3f, BellHi.WithAlpha(0.45f * fade), 64);
                Arc(c, r * 0.7f, 2f, BellGhost.WithAlpha(0.3f * fade), 48);
                BellRunes(c, r, 24, 3f, BellHi.WithAlpha(0.8f * fade), r * 0.004f);
            }
            if (w < 0.14f)  // the crack of the third toll
                Circle(c, 40f + w * 300f, BellHi.WithAlpha(0.55f * (1f - w / 0.14f)), 32);
        }

        /// After the enemies: the bell on its shaft of light, and the stun marks on the enemies it stunned.
        void DrawSiegeBellOver(float t)
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.SiegeBell)
                {
                    DrawSbBell(fx);
                    DrawSbStuns(fx);
                }
        }

        /// How the bell hangs `since` the cast: its alpha, lift above its rest and swing; false when it's gone.
        static bool BellPose(float since, out float alpha, out float lift, out float swing)
        {
            float t3 = Game.BellTolls[2];
            float appear = Mathf.Clamp01(since / 0.22f), gone = Mathf.Clamp01((since - (t3 + 0.7f)) / 0.45f);
            alpha = appear <= 0f ? 0f : 1f - gone;
            lift = (1f - BellEase(appear)) * 140f + gone * 40f;
            swing = 0f;
            for (int i = 0; i < Game.BellTolls.Length; i++)
            {
                float a = since - Game.BellTolls[i];
                if (a > 0f)
                    swing += Mathf.Sin(a * 22f) * Mathf.Exp(-a * 5f) * (i == 2 ? 0.35f : 0.22f);
            }
            return alpha > 0f;
        }

        void DrawSbBell(Effect fx)
        {
            float since = fx.T, alpha, lift, swing;
            if (!BellPose(since, out alpha, out lift, out swing))
                return;
            V2 up = UnrotateView(new V2(0f, -1f)), right = new V2(-up.Y, up.X);
            V2 pivot = fx.Pos + up * (64f + lift);
            Line(pivot + up * 130f, pivot, 1.5f, BellGhost.WithAlpha(0.5f * alpha));  // the shaft it hangs from
            // Down and across the bell, turned by the swing.
            V2 down = (-up).Rotated(swing), across = right.Rotated(swing);
            float flash = 0f;
            foreach (float tt in Game.BellTolls)
            {
                float a = since - tt;
                if (a >= 0f && a < 0.12f)
                    flash = Mathf.Max(flash, 1f - a / 0.12f);
            }
            const int strips = 4;  // shading across: deep edge, ghost, bright, deep
            for (int j = 0; j + 1 < BellProfile.Length; j++)
            {
                Vector2 p0 = BellProfile[j], p1 = BellProfile[j + 1];
                for (int k = 0; k < strips; k++)
                {
                    float u0 = (float)k / strips, u1 = (float)(k + 1) / strips, uc = (u0 + u1) * 0.5f;
                    Rgba col = uc < 0.4f ? BellDeep.Mix(BellGhost, uc / 0.4f) : uc < 0.6f ? BellGhost.Mix(BellHi, (uc - 0.4f) / 0.2f) : BellHi.Mix(BellDeep, (uc - 0.6f) / 0.4f);
                    col = col.Mix(BellHi, flash).WithAlpha(0.6f * alpha);
                    V2 a0 = pivot + down * p0.x + across * (p0.y * (2f * u0 - 1f)), b0 = pivot + down * p0.x + across * (p0.y * (2f * u1 - 1f));
                    V2 a1 = pivot + down * p1.x + across * (p1.y * (2f * u0 - 1f)), b1 = pivot + down * p1.x + across * (p1.y * (2f * u1 - 1f));
                    Quad(a0, b0, b1, a1, col);
                }
            }
            Line(pivot + down * 50f - across * 30f, pivot + down * 50f + across * 30f, 3f, BellHi.WithAlpha(0.8f * alpha));  // the lip
            Line(pivot + down * 22f - across * 17f, pivot + down * 22f + across * 17f, 1.5f, BellHi.WithAlpha(0.5f * alpha));  // a band
            Polygon(pivot - down * 3f, 4f, 4, 0f, BellHi.WithAlpha(0.8f * alpha));  // the crown
            Circle(pivot + down * 48f + across * (Mathf.Sin(-swing * 2.2f) * 14f), 4.5f, BellDeep.WithAlpha(0.8f * alpha), 12);  // the clapper
        }

        /// A ring draining with the stun round each enemy the wave stunned, sound rippling off its sides.
        void DrawSbStuns(Effect fx)
        {
            float since = fx.T, stun = fx.Pos2.Y, R = fx.Radius;
            if (since < Game.BellTolls[2] || stun <= 0f)
                return;
            V2 up = UnrotateView(new V2(0f, -1f)), right = new V2(-up.Y, up.X);
            foreach (var e in game.Enemies.Values)
            {
                if (!(e.Dazed || e.StunT > 0f) || e.Def.IsBoss)
                    continue;
                float d = e.Disp.DistanceTo(fx.Pos);
                if (d > R + e.Radius + Game.BellShove)
                    continue;
                float hit = Game.BellReachT(Mathf.Max(0f, d - Game.BellShove), R);
                if (since < hit)
                    continue;
                float left = 1f - (since - hit) / stun;
                if (left <= 0f)
                    continue;
                Arc(e.Disp, e.Radius + 6f, 2.5f, BellGhost.WithAlpha(0.9f), 24, -MathX.Tau / 4f, MathX.Tau * left);
                float k = Mathf.Repeat(since * 2f + e.Id * 0.37f, 1f), rr = e.Radius + 10f + k * 8f;  // sound rippling off it
                float side = Mathf.Atan2(right.Y, right.X);
                Arc(e.Disp, rr, 1.5f, BellGhost.WithAlpha(0.8f * (1f - k)), 6, side - 0.5f, 1f);
                Arc(e.Disp, rr, 1.5f, BellGhost.WithAlpha(0.8f * (1f - k)), 6, side + MathX.Tau / 2f - 0.5f, 1f);
            }
        }

        /// The glow pass: the bell's halo, brighter as it tolls, and the third toll's burst.
        void DrawSiegeBellGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.SiegeBell)
                    continue;
                float since = fx.T, alpha, lift, swing;
                if (BellPose(since, out alpha, out lift, out swing))
                {
                    V2 up = UnrotateView(new V2(0f, -1f));
                    float ring = 0f;
                    foreach (float tt in Game.BellTolls)
                    {
                        float a = since - tt;
                        if (a >= 0f && a < 0.4f)
                            ring = Mathf.Max(ring, 1f - a / 0.4f);
                    }
                    GlowDisc(fx.Pos + up * (90f + lift), 60f + 30f * ring, BellGhost, (0.3f + 0.3f * ring) * alpha);
                }
                float w = since - Game.BellTolls[2];
                if (w >= 0f && w < Game.BellWave)
                    GlowDisc(fx.Pos, Game.BellFrontAt(since, fx.Radius), BellGhost, 0.25f * (1f - w / Game.BellWave));
            }
        }
    }
}
