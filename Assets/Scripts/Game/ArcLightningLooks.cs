using UnityEngine;

namespace Arena
{
    /// Arc Lightning in style A ("Thunderhead", Docs/Mockups/arc-lightning.html, the owner's pick), ported from the
    /// mockup's numbers. Charging: a halo grows round you and static spits off the staff, more the longer you hold; your
    /// own strip is a dashed ground outline with a pulse running out to a crackling marker at its far end. Letting go:
    /// one white-hot bolt races the strip's length in 80 ms, throwing forks to its edges, with a flash at the end; each
    /// enemy in it crackles for the stun. All of it is light, so it's drawn in the additive glow pass
    /// (DrawArcLightningGlow, from DrawGlowPass), which the lobby's loadout preview runs too (clipped to its window; the
    /// preview's charge is drawn the same way, see DrawShowcase). Drawing only, with Visual effects on; off, the ring and
    /// strip (DrawArcCharge) and the flat strip (DrawArcFieldFx) draw as before.
    public partial class ArenaApp
    {
        const float ThunderRun = 0.08f, ThunderLife = 0.4f;  // the bolt racing out; how long the effect (and the stun crackle) lasts
        // The mockup is drawn at 0.55 of the game's scale; its pixel sizes are divided by this to be arena pixels.
        const float ThunderS = 0.55f;

        static readonly Rgba ArcGold = new Rgba(1f, 0.82f, 0.3f);

        /// DrawArcFieldFx / DrawArcCharge: with Visual effects on, the strip's release and the charge are drawn in the
        /// glow pass instead (true: draw nothing here). Also lets the release last as long as the crackle does.
        bool ArcLightningDrawnAsThunder(Effect fx)
        {
            if (!fxOn)
                return false;
            if (fx.Dur < ThunderLife)
                fx.Dur = ThunderLife;  // (front end only: the effect is drawing, nothing reads it)
            return true;
        }

        /// The glow pass: every Arc Lightning charge being held, and every strip going off.
        void DrawArcLightningGlow()
        {
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.ArcLightning && p.DrawT > 0f && !p.Downed)
                    DrawThunderCharge(p, p.Id == game.MyId, game.AnimT);
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.ArcField)
                    DrawThunderStrip(fx);
        }

        /// Where each strip's middle sits sideways from you: Barrage's extra strips alternate sides (Game.ArcField).
        static float[] ArcShifts(Player p)
        {
            int n = 1 + p.Stats.Fan;
            var s = new float[n];
            float half = Game.ArcWidth(p);
            for (int v = 1; v < n; v++)
                s[v] = (v % 2 == 1 ? 1f : -1f) * 2f * half * ((v + 1) / 2);
            return s;
        }

        void ThunderBolt(System.Collections.Generic.List<V2> pts, float w, float a, Rgba col)
        {
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                SoftLine(pts[i], pts[i + 1], w * 3.2f, col.WithAlpha(0.35f * a));
                Line(pts[i], pts[i + 1], w, Rgba.White.WithAlpha(a));
            }
        }

        /// A jagged run from a to b: each middle point is thrown sideways by up to amp, most in the middle.
        static System.Collections.Generic.List<V2> ThunderJag(V2 a, V2 b, int segs, float amp, ref uint s)
        {
            var pts = new System.Collections.Generic.List<V2> { a };
            V2 n = (b - a).Normalized().Orthogonal();
            for (int i = 1; i < segs; i++)
            {
                float f = (float)i / segs, j = (Rand01(ref s) - 0.5f) * 2f * amp * Mathf.Sin(f * Mathf.PI);
                pts.Add(a + (b - a) * f + n * j);
            }
            pts.Add(b);
            return pts;
        }

        void ThunderDashes(V2 a, V2 b, float dash, float gap, float offset, float w, Rgba col)
        {
            float len = (b - a).Length;
            if (len < 1f)
                return;
            V2 d = (b - a) / len;
            float period = dash + gap, start = -MathX.Fmod(offset, period);
            for (float x = start; x < len; x += period)
            {
                float x0 = Mathf.Max(0f, x), x1 = Mathf.Min(len, x + dash);
                if (x1 > x0)
                    Line(a + d * x0, a + d * x1, w, col);
            }
        }

        /// The charge: the halo, the sparks off the staff, and (yours, not an ally's) the dashed strip with its pulse and marker.
        void DrawThunderCharge(Player p, bool mine, float t)
        {
            float c = game.BowCharge(p, p.DrawT);
            bool full = c >= 1f;
            var col = full ? ArcGold : Defs.BulletColor(Defs.BoltShot);
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
            V2 tip = p.Disp + dir * (p.Def.BodyR + 12f);  // the staff's prongs
            GlowDisc(p.Disp, (30f + 46f * c) / ThunderS, col, 0.35f + 0.4f * c);
            uint s = (uint)(31 + (int)(t * 16f) + p.Id * 977);
            int n = 2 + (int)(c * 8f);
            for (int i = 0; i < n; i++)  // static leaping off it, more as it builds
            {
                float a = Rand01(ref s) * MathX.Tau, d = (16f + Rand01(ref s) * (22f + 30f * c)) / ThunderS;
                var pts = ThunderJag(tip, tip + V2.FromAngle(a) * d, 3, 4f / ThunderS, ref s);
                for (int k = 0; k + 1 < pts.Count; k++)
                    Line(pts[k], pts[k + 1], 1.8f, Rgba.White.WithAlpha(0.8f));
            }
            if (!mine)
                return;
            float len = Game.ArcLength(p, c), w = Game.ArcWidth(p);
            foreach (float sh in ArcShifts(p))
            {
                V2 o = p.Disp + side * sh;
                V2 a0 = o + side * w, a1 = o - side * w, e0 = a0 + dir * len, e1 = a1 + dir * len;
                var line = col.WithAlpha(0.25f + 0.4f * c);
                float off = -t * 40f / ThunderS;
                ThunderDashes(a0, e0, 10f / ThunderS, 8f / ThunderS, off, 2.5f, line);
                ThunderDashes(a1, e1, 10f / ThunderS, 8f / ThunderS, off, 2.5f, line);
                ThunderDashes(a0, a1, 10f / ThunderS, 8f / ThunderS, off, 2.5f, line);
                ThunderDashes(e0, e1, 10f / ThunderS, 8f / ThunderS, off, 2.5f, line);
                GlowDisc(o + dir * (MathX.Fmod(t * 1.6f, 1f) * len), 22f / ThunderS, col, 0.25f + 0.3f * c);  // the pulse running out
                float tick = Mathf.Sin(t * 30f) * 0.5f + 0.5f;  // the crackling marker at the far end
                Line(o + dir * (len + 3f) + side * (12f / ThunderS), o + dir * (len + 3f) - side * (12f / ThunderS), 2.5f, col.WithAlpha(0.4f + 0.5f * tick));
            }
        }

        /// A strip going off (one per strip): the spine bolt with its forks and flash, and the stun crackle on each enemy in it.
        void DrawThunderStrip(Effect fx)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float len = to.Length, half = fx.Radius, since = fx.T;
            if (len < 1f || since >= ThunderLife)
                return;
            V2 dir = to / len, side = dir.Orthogonal();
            var col = fx.Color;
            float run = EaseOut3(Mathf.Clamp01(since / ThunderRun)), fade = Mathf.Clamp01(1f - (since - 0.06f) / 0.34f), f = since / ThunderLife;
            V2 mid = fx.Pos + dir * (len / 2f), reach = fx.Pos + dir * (len * run);
            GlowDisc(mid, len / 2f + 40f / ThunderS, col, 0.28f * fade);
            SetColor(col.WithAlpha(0.14f * fade));
            Quad(fx.Pos + side * half, fx.Pos + dir * len + side * half, fx.Pos + dir * len - side * half, fx.Pos - side * half);
            uint s = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663 ^ (int)(since * 30f) * 83492791) | 1u;  // re-rolls as it fades; the same on every screen
            var spine = ThunderJag(fx.Pos, reach, Mathf.Max(6, (int)(len / 28f)), 28f, ref s);
            ThunderBolt(spine, 5f, fade, col);
            if (since < 0.2f)
                for (int i = 1; i < spine.Count - 1; i += 2)  // forks off it to the strip's edges
                {
                    float sd = Rand01(ref s) < 0.5f ? -1f : 1f;
                    V2 end = spine[i] + dir * ((16f + Rand01(ref s) * 18f) / ThunderS) + side * (sd * half * (0.6f + Rand01(ref s) * 0.5f) - (spine[i] - fx.Pos).Dot(side));
                    ThunderBolt(ThunderJag(spine[i], end, 4, 9f, ref s), 2f, fade * 0.9f, col);
                }
            GlowDisc(reach, 26f / ThunderS, Rgba.White, 0.8f * fade);  // the head of the bolt
            if (run >= 1f)
                GlowDisc(fx.Pos + dir * len, 40f / ThunderS * (1f + f), col, 0.7f * (1f - f));
            // The stun: a crackle round each enemy in it (bosses aren't stunned).
            foreach (var e in game.Enemies.Values)
            {
                if (e.Def.IsBoss || e.Hp <= 0f)
                    continue;
                V2 rel = e.Pos - fx.Pos;
                float along = rel.Dot(dir), across = rel.Dot(side);
                if (along < -e.Radius || along > len + e.Radius || Mathf.Abs(across) > half + e.Radius)
                    continue;
                if (since < ThunderRun * (along / len) || since > Game.ArcStun)
                    continue;  // not before the bolt gets there, nor after the stun ends
                uint r = (uint)(e.Id * 7919 + (int)(since * 20f) * 31 + 11) | 1u;
                for (int k = 0; k < 2; k++)
                {
                    float a = Rand01(ref r) * MathX.Tau, b = a + 0.7f + Rand01(ref r);
                    var pts = ThunderJag(e.Disp + V2.FromAngle(a) * e.Radius * 1.5f, e.Disp + V2.FromAngle(b) * e.Radius * 1.5f, 3, 4f, ref r);
                    for (int j = 0; j + 1 < pts.Count; j++)
                        Line(pts[j], pts[j + 1], 1.8f, Rgba.White.WithAlpha(0.8f * fade));
                }
            }
        }

    }
}
