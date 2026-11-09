using UnityEngine;

namespace Arena
{
    /// Arc Lightning in style F ("Lichtenberg", Docs/Mockups/arc-lightning-2.html, the owner's pick; it replaced style A
    /// "Thunderhead"), ported from the mockup's numbers. Charging: a violet halo grows round you and static spits off
    /// the staff, more the longer you hold; your own strip is a dashed cone with tendrils of a fractal tree creeping out
    /// along it. Letting go: the whole tree (a trunk with branches three deep, and a branch to each enemy) whips down the
    /// strip in 0.26 s, white-hot, then cools to violet and stays on the ground for a second; each enemy in it crackles
    /// for the stun. The strip is a slight cone (Game.ArcTaper). All of it is light, so it's drawn in the additive glow pass
    /// (DrawArcLightningGlow, from DrawGlowPass), which the lobby's loadout preview runs too (clipped to its window; the
    /// preview's charge is drawn the same way, see DrawShowcase). Drawing only, with Visual effects on; off, the ring and
    /// strip (DrawArcCharge) and the flat strip (DrawArcFieldFx) draw as before.
    public partial class ArenaApp
    {
        const float ThunderLife = 1.4f;  // how long the effect lasts: the tree flashes, cools, and stays as a scar
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

        /// A point in a strip: `along` out from o, `across` sideways measured at the far end (the strip is a slight cone, so it
        /// is scaled in by Game.ArcTaper nearer you).
        static V2 ArcPt(V2 o, V2 dir, V2 side, float len, float along, float across) => o + dir * along + side * (across * Game.ArcTaper(along, len));

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

        // ---- style F: the tree of lightning (built once, for the longest strip) ----
        struct ArcSeg { public float Ax, Ay, Bx, By, D, W; public bool Hit; }
        static System.Collections.Generic.List<ArcSeg> arcTree;
        static readonly Rgba ArcViolet = new Rgba(0.72f, 0.6f, 1f);
        static readonly System.Collections.Generic.List<ArcSeg> arcHit = new System.Collections.Generic.List<ArcSeg>();
        const float TreeGrow = 0.26f;  // seconds for the tree to reach the far end

        static float ArcLcg(ref long s) { s = s * 16807L % 2147483647L; return (s - 1) / 2147483646f; }

        static void ArcGrow(System.Collections.Generic.List<ArcSeg> segs, ref long r, float px, float py, float ang, float len, int depth, float d0)
        {
            float x = px, y = py, d = d0, hw = Game.ArcHalfWidth;
            int steps = Mathf.CeilToInt(len / 9f);
            for (int i = 0; i < steps; i++)
            {
                ang += (ArcLcg(ref r) - 0.5f) * 0.7f;
                float nx = x + Mathf.Cos(ang) * 9f, ny = y + Mathf.Sin(ang) * 9f;
                if (Mathf.Abs(ny) > hw * 1.08f)
                    break;
                d += 9f;
                segs.Add(new ArcSeg { Ax = x, Ay = y, Bx = nx, By = ny, D = d, W = depth == 1 ? 0.6f : 0.4f });
                x = nx; y = ny;
                if (depth < 3 && ArcLcg(ref r) < 0.2f)
                {
                    float sgn = ArcLcg(ref r) < 0.5f ? 1f : -1f;
                    ArcGrow(segs, ref r, x, y, ang + sgn * (0.5f + ArcLcg(ref r) * 0.6f), len * 0.55f, depth + 1, d);
                }
            }
        }

        /// A trunk along the strip's middle with branches off it, three levels deep: the same every time.
        static System.Collections.Generic.List<ArcSeg> ArcTree()
        {
            if (arcTree != null)
                return arcTree;
            var segs = new System.Collections.Generic.List<ArcSeg>();
            float hw = Game.ArcHalfWidth;
            var trunkX = new System.Collections.Generic.List<float> { 0f };
            var trunkY = new System.Collections.Generic.List<float> { 0f };
            long r0 = 4242;
            float x = 0f, y = 0f;
            while (x < Game.Width * 0.5f * 1.3f + 20f)
            {
                x += 16f;
                y = Mathf.Clamp(y + (ArcLcg(ref r0) - 0.5f) * 15f - y * 0.15f, -hw * 0.45f, hw * 0.45f);
                trunkX.Add(x); trunkY.Add(y);
            }
            for (int i = 1; i < trunkX.Count; i++)
                segs.Add(new ArcSeg { Ax = trunkX[i - 1], Ay = trunkY[i - 1], Bx = trunkX[i], By = trunkY[i], D = trunkX[i], W = 1f });
            for (int i = 2; i < trunkX.Count - 2; i++)
            {
                long r = 777 + i * 31;
                if (ArcLcg(ref r) < 0.6f)
                {
                    float sgn = ArcLcg(ref r) < 0.5f ? -1f : 1f;
                    float ang = sgn * (0.55f + ArcLcg(ref r) * 0.75f), l = 36f + ArcLcg(ref r) * 70f;
                    ArcGrow(segs, ref r, trunkX[i], trunkY[i], ang, l, 1, trunkX[i]);
                }
            }
            return arcTree = segs;
        }

        /// Draws the tree's segments out to `reach` (distance along its branches) and `maxX` along the strip. Across is scaled by
        /// the strip's cone (and by scale for the weapon's width), plus shift (Barrage); enemy branches (Hit) are in real pixels.
        void DrawArcSegs(System.Collections.Generic.List<ArcSeg> segs, V2 o, V2 dir, V2 side, float len, float scale, float shift, float reach, float maxX, float wMul, Rgba col)
        {
            foreach (var sg in segs)
            {
                if (sg.D > reach || (!sg.Hit && sg.Bx > maxX))
                    continue;
                float fa = Game.ArcTaper(sg.Ax, len), fb = Game.ArcTaper(sg.Bx, len);
                V2 a = o + dir * sg.Ax + side * (sg.Hit ? sg.Ay : (sg.Ay * scale + shift) * fa);
                V2 b = o + dir * sg.Bx + side * (sg.Hit ? sg.By : (sg.By * scale + shift) * fb);
                Line(a, b, Mathf.Max(0.7f, sg.W * wMul) / ThunderS, col);
            }
        }

        /// The charge: a violet glow and static on the staff and (yours, not an ally's) the strip as a dashed cone with
        /// tendrils of the tree creeping along it, reaching as far as the strip will.
        void DrawThunderCharge(Player p, bool mine, float t)
        {
            float c = game.BowCharge(p, p.DrawT);
            bool full = c >= 1f;
            var col = full ? ArcGold : ArcViolet;
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
            V2 tip = p.Disp + dir * (p.Def.BodyR + 12f);  // the staff's prongs
            GlowDisc(p.Disp, (30f + 46f * c) / ThunderS, ArcViolet, 0.35f + 0.4f * c);
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
            float len = Game.ArcLength(p, c), w = Game.ArcWidth(p), scale = w / Game.ArcHalfWidth;
            float flick = 0.6f + 0.4f * Mathf.Sin(t * 40f), reach = len * 0.92f;
            var tree = ArcTree();
            foreach (float sh in ArcShifts(p))
            {
                V2 a0 = ArcPt(p.Disp, dir, side, len, 0f, sh + w), a1 = ArcPt(p.Disp, dir, side, len, 0f, sh - w), e0 = ArcPt(p.Disp, dir, side, len, len, sh + w), e1 = ArcPt(p.Disp, dir, side, len, len, sh - w);
                var line = ArcViolet.WithAlpha(0.22f + 0.25f * c);
                float off = -t * 40f / ThunderS;
                ThunderDashes(a0, e0, 10f / ThunderS, 8f / ThunderS, off, 2.5f, line);
                ThunderDashes(a1, e1, 10f / ThunderS, 8f / ThunderS, off, 2.5f, line);
                ThunderDashes(a0, a1, 10f / ThunderS, 8f / ThunderS, off, 2.5f, line);
                ThunderDashes(e0, e1, 10f / ThunderS, 8f / ThunderS, off, 2.5f, line);
                DrawArcSegs(tree, p.Disp, dir, side, len, scale, sh, reach, len, 1.2f, ArcViolet.WithAlpha(0.28f * flick + 0.12f));
                GlowDisc(ArcPt(p.Disp, dir, side, len, reach, sh), (14f + 10f * c) / ThunderS, col, 0.5f + 0.3f * flick);  // the tendrils' tips
            }
        }

        /// A strip going off (one per strip): the tree whips down it in 0.26 s, white-hot with a violet glow, a branch to each
        /// enemy in it; it cools to violet and stays as a scar (the effect lasts ThunderLife). Each enemy in it crackles for the stun.
        void DrawThunderStrip(Effect fx)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float len = to.Length, half = fx.Radius, since = fx.T;
            if (len < 1f || since >= ThunderLife)
                return;
            V2 dir = to / len, side = dir.Orthogonal();
            float scale = half / Game.ArcHalfWidth;
            float grow = EaseOut3(Mathf.Clamp01(since / TreeGrow)), reach = grow * (len + 160f);
            float live = since < 0.4f ? 1f : Mathf.Max(0f, 1f - (since - 0.4f) / 0.5f);  // white-hot, then it cools to violet
            // A branch from the trunk to each enemy in the strip, in a few jagged steps, and a glow where it lands.
            arcHit.Clear();
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                V2 rel = e.Pos - fx.Pos;
                float along = rel.Dot(dir), across = rel.Dot(side);
                if (along < -e.Radius || along > len + e.Radius || Mathf.Abs(across) > half * Game.ArcTaper(along, len) + e.Radius)
                    continue;
                along = Mathf.Clamp(along, 0f, len);
                float hitT = along / (len + 160f) * TreeGrow + 0.03f, k = since - hitT;
                uint hs = (uint)(e.Id * 2654435 + 31) | 1u;
                var pts = ThunderJag(new V2(along, 0f), new V2(along, across), 5, 7f, ref hs);
                for (int i = 1; i < pts.Count; i++)
                    arcHit.Add(new ArcSeg { Ax = pts[i - 1].X, Ay = pts[i - 1].Y, Bx = pts[i].X, By = pts[i].Y, D = along + 14f * i, W = 1.3f, Hit = true });
                if (k > 0f)
                    GlowDisc(e.Disp, 22f / ThunderS, ArcViolet, Mathf.Max(0f, 1f - k / 0.5f));
            }
            var tree = ArcTree();
            if (live > 0f)
            {
                DrawArcSegs(tree, fx.Pos, dir, side, len, scale, 0f, reach, len + 10f, 5.5f, ArcViolet.WithAlpha(0.4f * live));
                DrawArcSegs(arcHit, fx.Pos, dir, side, len, scale, 0f, reach, len + 10f, 5.5f, ArcViolet.WithAlpha(0.4f * live));
                DrawArcSegs(tree, fx.Pos, dir, side, len, scale, 0f, reach, len + 10f, 2.2f, Rgba.White.WithAlpha(live));
                DrawArcSegs(arcHit, fx.Pos, dir, side, len, scale, 0f, reach, len + 10f, 2.2f, Rgba.White.WithAlpha(live));
                if (since < 0.3f)
                    GlowDisc(fx.Pos + dir * Mathf.Min(len, reach), 30f / ThunderS, ArcViolet, live);
            }
            float scar = Mathf.Max(0f, 0.4f * (1f - Mathf.Max(0f, since - 0.3f) / 1.1f));  // the cooled tree stays on the ground
            if (scar > 0f && since >= 0.4f)
            {
                DrawArcSegs(tree, fx.Pos, dir, side, len, scale, 0f, reach, len + 10f, 2.2f, ArcViolet.WithAlpha(scar));
                DrawArcSegs(arcHit, fx.Pos, dir, side, len, scale, 0f, reach, len + 10f, 2.2f, ArcViolet.WithAlpha(scar));
            }
            // The stun: violet crackle round each enemy in it (bosses aren't stunned).
            foreach (var e in game.Enemies.Values)
            {
                if (e.Def.IsBoss || e.Hp <= 0f)
                    continue;
                V2 rel = e.Pos - fx.Pos;
                float along = rel.Dot(dir), across = rel.Dot(side);
                if (along < -e.Radius || along > len + e.Radius || Mathf.Abs(across) > half * Game.ArcTaper(along, len) + e.Radius)
                    continue;
                if (since < along / (len + 160f) * TreeGrow + 0.03f || since > Game.ArcStun)
                    continue;  // not before the tree gets there, nor after the stun ends
                uint r = (uint)(e.Id * 7919 + (int)(since * 20f) * 31 + 11) | 1u;
                for (int k = 0; k < 3; k++)
                {
                    float a = Rand01(ref r) * MathX.Tau;
                    var pts = ThunderJag(e.Disp + V2.FromAngle(a) * e.Radius * 0.5f, e.Disp + V2.FromAngle(a + 0.3f) * e.Radius * 2.2f, 3, 3f, ref r);
                    for (int j = 0; j + 1 < pts.Count; j++)
                        Line(pts[j], pts[j + 1], 2f, ArcViolet.WithAlpha(0.9f));
                }
            }
        }

    }
}
