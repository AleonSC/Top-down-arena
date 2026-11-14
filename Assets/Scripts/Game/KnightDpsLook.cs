using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The DPS Knight's Space abilities, ported from Docs/Mockups/knight-dps-abilities.html (its drawing code, shape for
    /// shape and in world units, the mockup's stage being drawn at about the arena's own scale):
    ///   Caught Steel, look Forge (Core/CaughtSteel.cs): the dashed graze ring, a white-hot flare and sparks where a bullet
    ///     is caught, the shards cooling from white-hot to grey steel as they orbit and dripping sparks, the sweeping cut and
    ///     its ring, each shard drawn into the blade and flying off as a molten streak (the SteelShard bullet), sparks where
    ///     it strikes, shards knocked loose by a hit clattering to the floor, and twelve pips over your own head.
    ///   The Gauntlet, look Heraldic (Core/Gauntlet.cs): the spinning gauntlet and its glow trail, the clang (sparks, ring,
    ///     glint), the crest over the Challenged enemy with its timer ring and the gold ring turning under it, the crimson
    ///     and gold ribbon from you to it, the gauntlet lying where it hit, the charge's afterimages, bow wave and dust, the
    ///     bowled enemies' sparks, the impact, and the gauntlet flying back with the crest breaking and READY.
    ///   Bladed Dive (Core/BladedDive.cs): a gold streak and afterimages along the rush, two crescents whirling round you
    ///     at the cut's reach, a ring snapping out at each of the four cuts, a white slash and sparks across every enemy
    ///     cut, dust where you land, and on a kill a gold burst, a glint and RESET over you.
    public partial class ArenaApp
    {
        // Forge
        static readonly Rgba ForgeCore = C255(255, 226, 170), ForgeEdge = C255(255, 150, 60), ForgeGlow = C255(255, 110, 25), ForgeTrail = C255(255, 150, 60),
            ForgeRing = C255(255, 175, 80), ForgeCool = C255(132, 140, 152), ForgePip = C255(255, 160, 70), ForgeSpark = C255(255, 170, 70);
        // Heraldic
        static readonly Rgba HeraldGlow = C255(255, 200, 90), HeraldKnuckle = C255(216, 173, 78), HeraldAccent = C255(214, 170, 74), HeraldField = C255(142, 27, 40),
            HeraldRibbon = C255(200, 40, 55), HeraldDust = C255(150, 132, 108), HeraldSpark = C255(255, 214, 140);
        // Bladed Dive
        static readonly Rgba DiveGold = C255(240, 190, 90), DiveLight = C255(255, 244, 210);

        static Rgba C255(int r, int g, int b) => new Rgba(r / 255f, g / 255f, b / 255f);
        static float KHash(float n) => SprayHash(n * 0.731f + 3.17f);
        static float KEo(float f) { f = Mathf.Clamp01(f); return 1f - (1f - f) * (1f - f) * (1f - f); }
        static float KSm(float f) { f = Mathf.Clamp01(f); return f * f * (3f - 2f * f); }

        // --- Shared pieces (the mockup's sparks, puffs, shatter, crescent, glint, ghost) ---------------------------------

        /// Decelerating sparks, drawn as short streaks along their paths.
        void KSparks(V2 c, float age, int n, float seed, float ang, float spread, float sp, float dur, Rgba col, float lw = 1.6f, float lenK = 0.035f)
        {
            if (age < 0f || age > dur)
                return;
            for (int i = 0; i < n; i++)
            {
                float h1 = KHash(seed + i * 1.31f), h2 = KHash(seed + i * 2.77f), h3 = KHash(seed + i * 4.13f);
                float a = ang + (h1 - 0.5f) * spread, s = sp * (0.35f + 0.65f * h2), life = dur * (0.45f + 0.55f * h3);
                if (age > life)
                    continue;
                float g = age / life, d = s * life * (g - g * g / 2f), v = s * (1f - g), l = Mathf.Max(1.5f, lenK * v);
                V2 dir = V2.FromAngle(a), at = c + dir * d;
                Line(at - dir * l, at, lw * (1f - g * 0.5f), col.WithAlpha(1f - g));
            }
        }

        /// Soft puffs of dust or smoke drifting out and fading.
        void KPuffs(V2 c, float age, int n, float seed, float ang, float spread, float sp, float dur, Rgba col, float r, float alpha, float rise = 0f)
        {
            if (age < 0f || age > dur)
                return;
            float f = age / dur;
            for (int i = 0; i < n; i++)
            {
                float h1 = KHash(seed + i * 1.7f), h2 = KHash(seed + i * 3.1f), h3 = KHash(seed + i * 5.3f);
                float a = ang + (h1 - 0.5f) * spread, s = sp * (0.4f + 0.6f * h2), d = s * dur * (f - f * f / 2f);
                V2 at = c + V2.FromAngle(a) * d + new V2(0f, -rise * f);
                float rr = r * (0.6f + 0.8f * h3) * (0.45f + f * 1.1f), al = Mathf.Pow(1f - f, 1.4f) * alpha;
                FanGradient(at, rr, col.WithAlpha(al), col.WithAlpha(0f), 12);
            }
        }

        /// A cut's crescent: thick and bright at its head, thinning and fading toward its tail.
        void KCrescent(V2 c, float a0, float a1, float r0, float r1, Rgba col, float alpha)
        {
            if (alpha <= 0.01f)
                return;
            const int n = 22;
            for (int i = 0; i < n; i++)
            {
                float f = (i + 1) / (float)n, aa = Mathf.Lerp(a0, a1, i / (float)n), ab = Mathf.Lerp(a0, a1, f), th = (r1 - r0) * Mathf.Pow(f, 0.8f);
                SetColor(col.WithAlpha(alpha * 0.55f * Mathf.Pow(f, 1.5f)));
                Quad(c + V2.FromAngle(aa) * r1, c + V2.FromAngle(ab) * r1, c + V2.FromAngle(ab) * (r1 - th), c + V2.FromAngle(aa) * (r1 - th));
            }
            float s0 = Mathf.Lerp(a0, a1, 0.35f);
            Arc(c, r1, 2f, Rgba.White.WithAlpha(alpha * 0.85f), 16, s0, a1 - s0);
        }

        /// A four-pointed glint.
        void KStar(V2 c, float r, Rgba col, float alpha, float rot)
        {
            if (alpha <= 0.01f || r <= 0.5f)
                return;
            SetColor(col.WithAlpha(alpha));
            for (int k = 0; k < 4; k++)
            {
                V2 tip = c + V2.FromAngle(rot + k * MathX.Pi / 2f) * r, s1 = c + V2.FromAngle(rot + k * MathX.Pi / 2f + MathX.Pi / 4f) * r * 0.17f, s2 = c + V2.FromAngle(rot + k * MathX.Pi / 2f - MathX.Pi / 4f) * r * 0.17f;
                Tri(tip, s1, s2);
                Tri(s1, s2, c);
            }
        }

        /// A translucent knight silhouette (cape, body, pauldrons) for afterimages.
        void KGhost(V2 at, float ang, Rgba col, float alpha, float lift = 0f)
        {
            if (alpha <= 0.01f)
                return;
            at += new V2(0f, -lift);
            V2 u = V2.FromAngle(ang), v = new V2(-u.Y, u.X);
            System.Func<float, float, V2> P = (x, y) => at + u * x + v * y;
            SetColor(col.WithAlpha(0.55f * alpha));
            Quad(P(-2f, -12f), P(-24f, -13f), P(-24f, 13f), P(-2f, 12f));
            Tri(P(-24f, -13f), P(-30f, 0f), P(-24f, 13f));
            Circle(at, 14f, col.WithAlpha(0.8f * alpha), 16);
            Ellipse(P(-1f, -11.5f), 6.6f, 5.2f, ang, col.WithAlpha(0.8f * alpha), 10);
            Ellipse(P(-1f, 11.5f), 6.6f, 5.2f, ang, col.WithAlpha(0.8f * alpha), 10);
        }

        /// A dashed circle.
        void KDashedRing(V2 c, float r, float w, Rgba col, float dash, float gap, float offset)
        {
            if (r <= 1f)
                return;
            float per = dash + gap, circ = MathX.Tau * r;
            for (float s = -Mathf.Repeat(offset, per); s < circ; s += per)
            {
                float a0 = Mathf.Max(0f, s) / r, a1 = Mathf.Min(circ, s + dash) / r;
                if (a1 > a0)
                    Arc(c, r, w, col, 3, a0, a1 - a0);
            }
        }

        /// Hooked into the arena's drawing (WorldRenderer.cs): under the enemies, over them, and over the players.
        void DrawKnightDpsUnder(float t)
        {
            DrawGauntletGround(t);
            DrawBridgeShake();  // DrawbridgeLook.cs
        }

        void DrawKnightDpsOver(float t)
        {
            DrawBridgesFalling(t);  // DrawbridgeLook.cs: above the enemies it's about to crush
            DrawGauntletCrests(t);
        }

        void DrawKnightDpsTop(float t)
        {
            foreach (var p in game.Players.Values)
            {
                DrawSteel(p, t);
                DrawGauntletLying(p, t);
            }
        }

        /// The DPS Knight's moments on the HUD: READY, RESET, and your own shard pips.
        void DrawKnightDpsPops()
        {
            if (!ReferenceEquals(game, session.Game))
                return;
            foreach (var fx in game.Effects)
            {
                float a = fx.T;
                if (fx.Kind == FxKind.GauntletBack && fx.Color.A > 0.75f && a > Game.GauntletBackTime && a < Game.GauntletBackTime + 0.7f)
                {
                    Player p;
                    if (!game.Players.TryGetValue((int)fx.Radius, out p))
                        continue;
                    float a3 = a - Game.GauntletBackTime;
                    V2 at = ToUi(p.Disp);
                    at.Y -= (p.Def.BodyR + 18f) * UiWorldScale + a3 * 14f;
                    Ui.Shadowed(new Rect(at.X - 40f, at.Y - 10f, 80f, 20f), "READY", 13, UiCol(HeraldSpark, 1f - Mathf.Clamp01((a3 - 0.4f) / 0.3f)));
                }
                if (fx.Kind == FxKind.DiveReset && a < Game.BladedResetFx)
                {
                    Player p;
                    if (!game.Players.TryGetValue((int)fx.Pos2.X, out p))
                        continue;
                    float pop = a < 0.08f ? 1f + 0.5f * (1f - a / 0.08f) : 1f;
                    V2 at = ToUi(p.Disp);
                    at.Y -= (p.Def.BodyR + 46f) * UiWorldScale + a * 16f;
                    Ui.Shadowed(new Rect(at.X - 40f, at.Y - 10f, 80f, 20f), "RESET", Mathf.RoundToInt(13f * pop), UiCol(DiveLight, 1f - Mathf.Clamp01((a - 0.55f) / 0.35f)));
                }
            }
            var me = game.Me;
            if (me != null && !me.Downed && (me.SteelT > 0f || me.Steel > 0))
            {
                int cap = Game.SteelCapOf(me, Game.Mastered(me, AbilityId.BattleFury)), n = me.Steel;
                V2 at = ToUi(me.Disp);
                float y = at.Y - me.Def.BodyR * UiWorldScale - 30f, x0 = at.X - (cap - 1) * 6.5f;
                for (int i = 0; i < cap; i++)
                {
                    bool on = i < n;
                    UiDiamond(x0 + i * 13f, y, 5.1f, on ? new Color(1f, 1f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.25f));
                    UiDiamond(x0 + i * 13f, y, 4.1f, on ? UiCol(ForgePip, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.6f));
                }
            }
        }


        /// A small diamond on the HUD (rows of 1 px fills), centred at (x, y), `r` from the centre to a point.
        static void UiDiamond(float x, float y, float r, Color c)
        {
            for (float dy = -r; dy < r; dy += 1f)
            {
                float w = 2f * (r - Mathf.Abs(dy + 0.5f));
                if (w > 0f)
                    Ui.Fill(new Rect(x - w / 2f, y + dy, w, 1f), c);
            }
        }

        /// Your own screen kicks and holds for the big moments.
        void KnightDpsFeel(Effect fx, Player me)
        {
            if (me == null)
                return;
            switch (fx.Kind)
            {
                case FxKind.SteelRelease:
                    if ((int)fx.Pos2.X == game.MyId)
                        Shake(0.12f);
                    break;
                case FxKind.GauntletImpact:
                    if ((int)fx.Radius == game.MyId)
                    {
                        Hold(0.06f, 0.2f);  // the mockup's hold on the hit
                        Shake(0.25f);
                    }
                    break;
                case FxKind.DiveRush:
                    if ((int)fx.Radius == game.MyId)
                        Shake(0.1f);
                    break;
                case FxKind.DiveReset:
                    if ((int)fx.Pos2.X == game.MyId)
                    {
                        Hold(0.05f, 0.15f);
                        Shake(0.18f);
                    }
                    break;
            }
        }

        // =============================================================================================================
        // Caught Steel (Forge)
        // =============================================================================================================

        class SteelBit
        {
            public Effect Fx;        // the catch that made it (null: made up for one we never saw)
            public float T0;         // AnimT when it was caught
            public float Ang, R;     // where it was caught, round the knight
            public int K;
        }

        readonly Dictionary<int, List<SteelBit>> steelBits = new Dictionary<int, List<SteelBit>>();
        readonly List<SteelBit> steelScratch = new List<SteelBit>();
        const float SteelOmega = 1.8f;

        static float SteelOrbitR(SteelBit b) => 40f + (b.K % 3) * 7f;

        V2 SteelOrbitAt(Player p, SteelBit b, float now)
        {
            float age = now - b.T0, r = Mathf.Lerp(b.R, SteelOrbitR(b), KEo(age / 0.35f)) + Mathf.Sin(now * 3f + b.K) * 1.5f;
            return p.Disp + V2.FromAngle(b.Ang + SteelOmega * age) * r;
        }

        float SteelOrbitAng(SteelBit b, float now) => b.Ang + SteelOmega * (now - b.T0);

        /// One shard: a diamond, white-hot cooling to grey steel (heat 1 to 0), with its glow.
        void DrawSteelShard(V2 at, float ang, float heat, float alpha, float stretch)
        {
            Rgba core = ForgeCool.Mix(ForgeCore, heat), edge = C255(60, 64, 72).Mix(ForgeEdge, heat);
            GlowDisc(at, 6f + 10f * heat, ForgeGlow, (0.12f + 0.4f * heat) * alpha);
            float l = 7f * stretch;
            V2 u = V2.FromAngle(ang), v = new V2(-u.Y, u.X);
            V2 a = at + u * l, b = at + v * -2.5f, c = at - u * (l * 0.8f), d = at + v * 2.5f;
            SetColor(edge.WithAlpha(alpha));
            Quad(a + u * 0.8f, b - v * 0.8f, c - u * 0.8f, d + v * 0.8f);
            SetColor(core.WithAlpha(alpha));
            Quad(a, b, c, d);
            Line(at - u * (l * 0.5f), at + u * (l * 0.8f), 0.8f, Rgba.White.WithAlpha(0.75f * alpha));
        }

        /// A player's shards: caught, orbiting and cooling; drawn into the blade and away when the cut throws them; a few
        /// knocked loose by a hit; and the graze ring while they're catching.
        void DrawSteel(Player p, float t)
        {
            float now = game.AnimT;
            List<SteelBit> bits;
            if (!steelBits.TryGetValue(p.Id, out bits))
                steelBits[p.Id] = bits = new List<SteelBit>();
            Effect release = null, drop = null;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.SteelCatch && (int)fx.Pos2.X == p.Id)
                {
                    bool known = false;
                    foreach (var b in bits)
                        if (ReferenceEquals(b.Fx, fx))
                            known = true;
                    if (!known && bits.Count < Game.SteelCapMastered)
                    {
                        V2 rel = fx.Pos - p.Disp;
                        bits.Add(new SteelBit { Fx = fx, T0 = now - fx.T, Ang = rel.Angle, R = rel.Length, K = bits.Count });
                    }
                    // the flare where it was caught
                    float a = fx.T, f = a / 0.4f;
                    if (a < 0.4f)
                    {
                        GlowDisc(fx.Pos, 22f * (1f - f), C255(255, 230, 190), 0.9f * (1f - f));
                        KSparks(fx.Pos, a, 7, fx.Pos.X * 0.13f + fx.Pos.Y, 0f, MathX.Tau, 150f, 0.35f, ForgeSpark);
                    }
                }
                if (fx.Kind == FxKind.SteelRelease && (int)fx.Pos2.X == p.Id)
                    release = fx;
                if (fx.Kind == FxKind.SteelDrop && (int)fx.Pos2.X == p.Id)
                    drop = fx;
                if (fx.Kind == FxKind.SteelHit)
                {
                    float a = fx.T;
                    if (fx.Radius > 0.5f)
                    {
                        KSparks(fx.Pos, a, 11, fx.Pos.X + fx.Pos.Y * 0.7f, fx.Pos2.Angle + MathX.Pi, 2.4f, 260f, 0.35f, ForgeTrail);
                        GlowDisc(fx.Pos, 18f * (1f - a / 0.25f), ForgeGlow, 0.8f * Mathf.Clamp01(1f - a / 0.25f));
                    }
                    else
                        KSparks(fx.Pos, a, 7, fx.Pos.X * 0.37f + fx.Pos.Y, 0f, MathX.Tau, 180f, 0.3f, C255(255, 140, 150));
                }
            }
            // The graze ring while catching.
            if (p.SteelT > 0f && !p.Downed)
            {
                float ra = Mathf.Clamp01((Game.SteelTime - p.SteelT) / 0.3f) * Mathf.Clamp01(p.SteelT / 0.3f + (release == null ? 1f : 0f));
                float gr = Game.SteelReach(p, 4f);
                GlowDisc(p.Disp, gr + 10f, ForgeGlow, 0.05f * ra);
                KDashedRing(p.Disp, gr, 1.3f, ForgeRing.WithAlpha(0.3f * ra), 3f, 6f, -t * 18f);
            }
            // The cut: its crescent and ring, and each shard drawn into the blade, gone as its bullet leaves.
            if (release != null)
            {
                float a = release.T, aim = release.Radius;
                float sweep = Game.SteelSweep;
                if (a < 0.42f)
                {
                    KCrescent(release.Pos, aim - 1.55f, aim + Mathf.Lerp(-1.55f, 1.5f, KEo(a / sweep)), 20f, 60f, ForgeTrail, 1f - Mathf.Clamp01((a - sweep) / 0.22f));
                    float rf = a / 0.42f;
                    Arc(release.Pos, 20f + 90f * KEo(rf), 2f, ForgeRing.WithAlpha(0.5f * (1f - rf)), 40);
                }
                int n = Mathf.RoundToInt(release.Pos2.Y), cap = Game.SteelCapOf(p, Game.Mastered(p, AbilityId.BattleFury));
                steelScratch.Clear();
                steelScratch.AddRange(bits);
                steelScratch.Sort((x, y) => Mathf.Sin(SteelOrbitAng(x, now - a) - aim).CompareTo(Mathf.Sin(SteelOrbitAng(y, now - a) - aim)));
                for (int i = 0; i < steelScratch.Count && i < n; i++)
                {
                    var b = steelScratch[i];
                    float tl = 0.03f + sweep * 0.85f * (n > 1 ? i / (float)(n - 1) : 0f);
                    if (a >= tl)
                        continue;  // its bullet is flying now
                    int fi = i + (cap - n + 1) / 2;
                    float fa = aim - Game.SteelFan / 2f + Game.SteelFan * fi / (cap - 1);
                    V2 lp = release.Pos + V2.FromAngle(fa) * Game.SteelLaunchFrom;
                    V2 orbit = SteelOrbitAt(p, b, now - a);
                    float k = KEo((a - (tl - 0.06f)) / 0.06f);
                    DrawSteelShard(V2.Lerp(orbit, lp, a > tl - 0.06f ? k : 0f), a > tl - 0.06f ? fa : SteelOrbitAng(b, now) + MathX.Pi / 2f, 1f, 1f, 1f);
                }
                if (a > 0.01f)
                    bits.Clear();
                return;
            }
            // Knocked loose: the last few clatter to the floor, grey, and fade.
            if (drop != null)
            {
                int lost = Mathf.RoundToInt(drop.Pos2.Y);
                float a = drop.T, f = Mathf.Clamp01(a / 0.8f);
                for (int i = 0; i < lost; i++)
                {
                    float h = KHash(drop.Pos.X + i * 3.1f), ang = h * MathX.Tau;
                    V2 at = drop.Pos + V2.FromAngle(ang) * (44f + 26f * KEo(f)) + new V2(0f, 10f * f * f);
                    DrawSteelShard(at, ang + f * 9f * (KHash(i + 1.7f) - 0.5f), 0f, 1f - f, 1f);
                    if (a < 0.2f)
                        KSparks(at, a, 3, i * 7.3f + drop.Pos.Y, 0f, MathX.Tau, 90f, 0.2f, ForgeSpark);
                }
            }
            // Keep the shards in step with the count the host sends (a missed catch, or some knocked loose).
            int want = p.Steel;
            while (bits.Count > want)
                bits.RemoveAt(bits.Count - 1);
            while (bits.Count < want)
                bits.Add(new SteelBit { T0 = now - 1f, Ang = KHash(bits.Count + p.Id * 13f) * MathX.Tau, R = 44f, K = bits.Count });
            foreach (var b in bits)
            {
                float heat = Mathf.Clamp01(1f - (now - b.T0) / 0.9f);
                V2 at = SteelOrbitAt(p, b, now);
                if (heat < 0.6f && KHash(b.K + Mathf.Floor(now * 14f)) > 0.86f)  // a spark drips off a cooling shard
                    Circle(at + new V2(0f, Mathf.Repeat(now * 14f, 1f) * 6f), 1.2f, ForgeSpark.WithAlpha(0.8f), 5);
                DrawSteelShard(at, SteelOrbitAng(b, now) + MathX.Pi / 2f, heat, 1f, 1f);
            }
        }

        /// A thrown shard (Defs.SteelShard): a molten streak and the shard at its head, reheated by the cut.
        void DrawSteelShardShot(Bullet b)
        {
            if (b.Vel.IsZero)
                return;
            V2 d = b.Vel.Normalized();
            float flown = (b.Life0 - b.Life) * Game.SteelSpeed, tl = Mathf.Min(52f, flown);
            V2 tail = b.Pos - d * tl, side = new V2(-d.Y, d.X) * 1.6f;
            VTri(tail + side, ForgeTrail.WithAlpha(0f), b.Pos + side, ForgeTrail.WithAlpha(0.95f), b.Pos - side, ForgeTrail.WithAlpha(0.95f));
            VTri(tail + side, ForgeTrail.WithAlpha(0f), b.Pos - side, ForgeTrail.WithAlpha(0.95f), tail - side, ForgeTrail.WithAlpha(0f));
            DrawSteelShard(b.Pos, d.Angle, 1f, 1f, 1.5f);
        }

        // =============================================================================================================
        // The Gauntlet (Heraldic)
        // =============================================================================================================

        /// The armoured fist, seen from above: cuff, back plate, finger lames (the last one gold) and thumb.
        void DrawGauntletModel(V2 at, float rot, float scale, float glowA)
        {
            if (glowA > 0f)
                GlowDisc(at, 24f * scale, HeraldGlow, 0.55f * glowA);
            V2 u = V2.FromAngle(rot) * scale, v = new V2(-u.Y, u.X);
            System.Func<float, float, V2> P = (x, y) => at + u * x + v * y;
            var ink = C255(38, 43, 51);
            SetColor(ink);
            Quad(P(-16f, -9.5f), P(-4f, -7.5f), P(-4f, 7.5f), P(-16f, 9.5f));
            SetColor(C255(104, 113, 127));
            Quad(P(-15f, -8.5f), P(-5f, -6.5f), P(-5f, 6.5f), P(-15f, 8.5f));
            Line(P(-12f, -7.5f), P(-12f, 7.5f), 1f * scale, Rgba.White.WithAlpha(0.18f));
            SetColor(ink);
            Quad(P(-7f, -7.8f), P(7.5f, -7.8f), P(7.5f, 7.8f), P(-7f, 7.8f));
            SetColor(C255(179, 189, 202));
            Quad(P(-6f, -6.8f), P(4f, -6.8f), P(6.5f, 4.5f), P(-6f, 6.8f));
            Quad(P(4f, -6.8f), P(6.5f, -4.5f), P(6.5f, 4.5f), P(4f, 6.8f));
            Line(P(-3f, -4f), P(-3f, 4f), 1f * scale, new Rgba(0f, 0f, 0f, 0.25f));
            for (int i = 0; i < 3; i++)
            {
                float x = 6.5f + i * 3.3f;
                SetColor(ink);
                Quad(P(x - 0.5f, -6.8f), P(x + 3.4f, -6.1f), P(x + 3.4f, 6.1f), P(x - 0.5f, 6.8f));
                SetColor(i == 2 ? HeraldKnuckle : C255(199, 208, 219));
                Quad(P(x, -6.2f), P(x + 2.9f, -5.6f), P(x + 2.9f, 5.6f), P(x, 6.2f));
            }
            SetColor(ink);
            Quad(P(-2.5f, -7.3f), P(4f, -11.2f), P(8.2f, -9.4f), P(3f, -5.2f));
            SetColor(C255(158, 169, 184));
            Quad(P(-2f, -6.8f), P(4f, -10.5f), P(7.5f, -9f), P(3f, -5.8f));
        }

        /// The crest over a Challenged enemy: a crimson heater shield with a gold chevron, and the Challenge's timer ring.
        void DrawGauntletCrest(V2 at, float s, float timer, float alpha)
        {
            GlowDisc(at, 24f * s, HeraldGlow, 0.35f * alpha);
            System.Func<float, float, V2> P = (x, y) => at + new V2(x * s, y * s);
            V2[] sh = { P(-10f, -12f), P(10f, -12f), P(10f, 0f), P(7f, 8f), P(0f, 14f), P(-7f, 8f), P(-10f, 0f) };
            V2 mid = P(0f, 0f);
            SetColor(HeraldAccent.WithAlpha(alpha));
            for (int i = 0; i < sh.Length; i++)
            {
                V2 a = sh[i], b = sh[(i + 1) % sh.Length];
                Tri(mid + (a - mid) * 1.18f, mid + (b - mid) * 1.18f, mid);
            }
            SetColor(HeraldField.WithAlpha(alpha));
            for (int i = 0; i < sh.Length; i++)
                Tri(sh[i], sh[(i + 1) % sh.Length], mid);
            SetColor(HeraldAccent.WithAlpha(alpha));  // the chevron
            Quad(P(-8f, 2f), P(0f, -5f), P(0f, -1f), P(-8f, 6f));
            Quad(P(0f, -5f), P(8f, 2f), P(8f, 6f), P(0f, -1f));
            Circle(P(0f, -8f), 1.6f * s, HeraldAccent.WithAlpha(alpha), 6);
            if (timer >= 0f)
                Arc(P(0f, 1f), 19f * s, 2f, HeraldAccent.WithAlpha(0.85f * alpha), 24, -MathX.Pi / 2f, MathX.Tau * timer);
        }

        static float ChallengeFull(Enemy e) => e.ChallengeT > Game.ChallengeTime + 0.05f ? Game.ChallengeTimeMastered : Game.ChallengeTime;

        /// Under each Challenged enemy: the gold ring with eight ticks turning; and the dust of the charges.
        void DrawGauntletGround(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (e.ChallengeT <= 0f)
                    continue;
                float age = ChallengeFull(e) - e.ChallengeT, grow = KEo(age / 0.25f), fade = Mathf.Clamp01(e.ChallengeT / 0.3f);
                V2 c = e.Disp + new V2(0f, 2f);
                Arc(c, 26f * grow, 2f, HeraldAccent.WithAlpha(0.8f * fade), 32);
                for (int k = 0; k < 8; k++)
                {
                    V2 d = V2.FromAngle(k / 8f * MathX.Tau + t * 1.2f);
                    Line(c + d * (29f * grow), c + d * (34f * grow), 2f, HeraldAccent.WithAlpha(0.75f * fade));
                }
            }
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.GauntletCharge)
                    continue;
                V2 path = fx.Pos2 - fx.Pos;
                float len = path.Length;
                if (len < 1f)
                    continue;
                V2 d = path / len, side = new V2(-d.Y, d.X);
                int i = 0;
                for (float u = 0f; u < len; u += 20f, i++)
                {
                    float a = fx.T - u / Game.GauntletChargeSpeed;
                    KPuffs(fx.Pos + d * u + side * ((i % 2 == 1 ? 1f : -1f) * 7f), a, 2, u + fx.Pos.X, 0f, MathX.Tau, 22f, 0.7f, HeraldDust, 11f, 0.45f);
                }
            }
        }

        /// The crest over each Challenged enemy (it pops in big and settles), after the enemies are drawn.
        void DrawGauntletCrests(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (e.ChallengeT <= 0f)
                    continue;
                float full = ChallengeFull(e), age = full - e.ChallengeT;
                float s = age < 0.14f ? 1.8f - 0.8f * KEo(age / 0.14f) : 1f;
                DrawGauntletCrest(e.Disp + new V2(0f, -34f), s, e.ChallengeT / full, Mathf.Clamp01(e.ChallengeT / 0.2f));
            }
        }

        /// The gauntlet lying where it hit, its glow pulsing, until it comes back.
        void DrawGauntletLying(Player p, float t)
        {
            if (!p.GauntletDown)
                return;
            foreach (var fx in game.Effects)  // still in the air: the throw draws it
                if (fx.Kind == FxKind.GauntletThrow && (int)(fx.Radius / 10f + 0.001f) == p.Id && fx.T < GauntletFlight(fx))
                    return;
            V2 away = p.GauntletRest - p.Disp;
            DrawGauntletModel(p.GauntletRest, (away.IsZero ? 0f : away.Angle) + MathX.Pi, 0.9f, 0.5f + 0.5f * Mathf.Sin(t * 8f));
        }

        static bool GauntletHit(Effect fx) => fx.Radius - (int)(fx.Radius / 10f + 0.001f) * 10f > 0.5f;

        static float GauntletFlight(Effect fx) => fx.Dur - 0.05f - (GauntletHit(fx) ? Game.GauntletBrace : Game.GauntletBackTime);

        /// The throw: the gauntlet spinning out with a glow trail on a slight curve; the clang where it hits (or its flight
        /// back on a miss); the crimson and gold ribbon from you to the enemy while you brace.
        void DrawGauntletThrowFx(Effect fx, float t)
        {
            int id = (int)(fx.Radius / 10f + 0.001f);
            bool hit = GauntletHit(fx);
            float flight = Mathf.Max(0.01f, GauntletFlight(fx)), a = fx.T;
            V2 from = fx.Pos, to = fx.Pos2, d = (to - from).Normalized(), side = new V2(-d.Y, d.X);
            Player p;
            game.Players.TryGetValue(id, out p);
            if (a < flight)
            {
                float f = a / flight;
                for (int k = 1; k < 5; k++)
                {
                    float f2 = Mathf.Max(0f, f - k * 0.06f);
                    GlowDisc(V2.Lerp(from, to, f2) + side * (-Mathf.Sin(MathX.Pi * f2) * 16f), 10f - k, HeraldGlow, 0.25f * (1f - k / 5f));
                }
                DrawGauntletModel(V2.Lerp(from, to, f) + side * (-Mathf.Sin(MathX.Pi * f) * 16f), d.Angle + t * 18f, 0.9f, 1f);
                return;
            }
            float b = a - flight;
            if (!hit)
            {
                if (p == null || b > Game.GauntletBackTime)
                    return;
                float g = b / Game.GauntletBackTime;
                V2 hand = p.Disp + Game.GauntletHand.Rotated(p.Aim);
                DrawGauntletModel(V2.Lerp(to, hand, KSm(g)) + new V2(-(hand - to).Normalized().Y, (hand - to).Normalized().X) * (Mathf.Sin(MathX.Pi * g) * 22f), t * 20f, 0.9f, 1f);
                return;
            }
            if (b < 0.5f)  // the clang
            {
                KSparks(to, b, 14, to.X * 0.31f, 0f, MathX.Tau, 240f, 0.35f, HeraldSpark);
                Arc(to, 8f + 30f * KEo(b / 0.3f), 2.5f, HeraldSpark.WithAlpha(0.8f * Mathf.Clamp01(1f - b / 0.3f)), 32);
                KStar(to, 26f * (1f - b / 0.2f), C255(255, 245, 220), Mathf.Clamp01(1f - b / 0.2f), 0.4f);
            }
            if (p != null && b < Game.GauntletBrace + 0.05f)  // bracing: the ribbon
                DrawGauntletRibbon(p.Disp + Game.GauntletHand.Rotated(p.Aim), to, Mathf.Clamp01(b / 0.1f), t);
        }

        void DrawGauntletRibbon(V2 from, V2 to, float alpha, float t)
        {
            if (alpha <= 0.01f)
                return;
            V2 path = to - from;
            float len = path.Length;
            if (len < 2f)
                return;
            V2 d = path / len;
            float off = Mathf.Repeat(t * 60f, 17f);
            for (float s = -off; s < len; s += 17f)  // crimson dashes 10 long, 7 apart
            {
                float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len, s + 10f);
                if (s1 > s0)
                    Line(from + d * s0, from + d * s1, 3f, HeraldRibbon.WithAlpha(0.85f * alpha));
            }
            float off2 = Mathf.Repeat(t * 60f, 17f);
            for (float s = -off2; s < len; s += 17f)  // gold dashes 3 long, 14 apart
            {
                float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len, s + 3f);
                if (s1 > s0)
                    Line(from + d * s0, from + d * s1, 2f, HeraldAccent.WithAlpha(0.9f * alpha));
            }
        }

        /// The charge: afterimages behind you, the bow wave at your shoulder, and the ribbon to the enemy.
        void DrawGauntletChargeFx(Effect fx, float t)
        {
            float dur = Mathf.Max(0.01f, fx.Pos.DistanceTo(fx.Pos2) / Game.GauntletChargeSpeed), a = fx.T;
            if (a > dur)
                return;
            Player p;
            if (!game.Players.TryGetValue((int)fx.Radius, out p))
                return;
            V2 d = (fx.Pos2 - fx.Pos).Normalized();
            float ang = d.Angle;
            for (int k = 1; k <= 5; k++)
            {
                float ta = a - k * 0.03f;
                if (ta < 0f)
                    break;
                KGhost(V2.Lerp(fx.Pos, fx.Pos2, ta / dur), ang, HeraldGlow, 0.32f * (1f - k / 6f));
            }
            Arc(p.Disp, 26f, 3f, HeraldSpark.WithAlpha(0.55f), 12, ang - 0.9f, 1.8f);
            GlowDisc(p.Disp + d * 22f, 22f, HeraldGlow, 0.3f);
            DrawGauntletRibbon(p.Disp + Game.GauntletHand.Rotated(ang), fx.Pos2 + d * Game.GauntletStopShort, 1f - Mathf.Clamp01((a - dur + 0.05f) / 0.05f), t);
        }

        void DrawGauntletBowlFx(Effect fx)
        {
            float a = fx.T;
            KPuffs(fx.Pos + fx.Pos2 * Game.GauntletBowlShove * KEo(a / 0.32f), a, 5, fx.Pos.X * 0.7f, 0f, MathX.Tau, 50f, 0.6f, HeraldDust, 9f, 0.5f);
            KSparks(fx.Pos, a, 8, fx.Pos.Y * 0.9f, fx.Pos2.Angle, 1.6f, 220f, 0.3f, HeraldSpark);
        }

        void DrawGauntletImpactFx(Effect fx)
        {
            float a = fx.T;
            if (a > 0.6f)
                return;
            Arc(fx.Pos, 14f + 60f * KEo(a / 0.4f), 3f, HeraldSpark.WithAlpha(0.7f * Mathf.Clamp01(1f - a / 0.4f)), 40);
            KSparks(fx.Pos, a, 18, fx.Pos.X * 0.41f + 5f, fx.Pos2.Angle, 1.8f, 330f, 0.4f, HeraldSpark, 2f);
            KPuffs(fx.Pos, a, 8, fx.Pos.Y * 0.09f, 0f, MathX.Tau, 70f, 0.6f, HeraldDust, 14f, 0.5f);
            GlowDisc(fx.Pos, 46f * (1f - a / 0.35f), HeraldGlow, 0.6f * Mathf.Clamp01(1f - a / 0.35f));
        }

        /// The gauntlet flies back to your hand on a curve, the crest breaking over the enemy; a kill's catch rings out.
        void DrawGauntletBackFx(Effect fx, float t)
        {
            float a = fx.T;
            Player p;
            if (!game.Players.TryGetValue((int)fx.Radius, out p))
                return;
            V2 crest = fx.Pos + new V2(0f, -34f);
            if (a < 0.45f)
            {
                float b = a / 0.45f;
                for (int q = 0; q < 4; q++)
                {
                    float aa = -MathX.Pi / 2f + (q - 1.5f) * 0.7f;
                    V2 at = crest + new V2(Mathf.Cos(aa) * b * 40f, Mathf.Sin(aa) * b * 30f + b * b * 60f);
                    V2 u = V2.FromAngle(b * (q - 1.5f) * 3f), v = new V2(-u.Y, u.X);
                    SetColor((q % 2 == 1 ? HeraldField : HeraldAccent).WithAlpha(1f - b));
                    Quad(at + u * -5f + v * -6f, at + u * 5f + v * -6f, at + u * 4f + v * 5f, at + u * -4f + v * 5f);
                }
            }
            V2 hand = p.Disp + Game.GauntletHand.Rotated(p.Aim);
            if (a < Game.GauntletBackTime)
            {
                float g = a / Game.GauntletBackTime;
                V2 back = (hand - fx.Pos2).Normalized();
                DrawGauntletModel(V2.Lerp(fx.Pos2, hand, KSm(g)) + new V2(-back.Y, back.X) * (Mathf.Sin(MathX.Pi * g) * 22f), t * 20f, 0.9f, 1f);
                return;
            }
            float a3 = a - Game.GauntletBackTime;
            if (fx.Color.A < 0.75f || a3 > 0.7f)
                return;
            Arc(hand, 6f + 24f * KEo(a3 / 0.35f), 2f, HeraldSpark.WithAlpha(0.9f * Mathf.Clamp01(1f - a3 / 0.35f)), 24);
            KStar(hand, 16f * (1f - a3 / 0.3f), C255(255, 250, 235), Mathf.Clamp01(1f - a3 / 0.3f), t);
        }

        // =============================================================================================================
        // Bladed Dive
        // =============================================================================================================

        /// The rush: a gold streak and afterimages along the path, two crescents whirling round you, a ring snapping out at
        /// each cut, and dust where you land.
        void DrawDiveRushFx(Effect fx, float t)
        {
            float a = fx.T, D = Game.BladedTime, u = KSm(a / D);
            V2 from = fx.Pos, to = fx.Pos2, d = (to - from).IsZero ? new V2(1f, 0f) : (to - from).Normalized(), n = new V2(-d.Y, d.X);
            Player p;
            V2 at = V2.Lerp(from, to, u);
            if (a < D && game.Players.TryGetValue((int)fx.Radius, out p) && p.Disp.DistanceTo(at) < 60f)
                at = p.Disp;  // where the knight really is, when it's close (the rush is carried out on their own game)
            float fade = a < D ? 1f : 1f - (a - D) / Game.BladedTail;
            // The streak along the path: a gold band, widest just behind you, and a white core.
            V2 tail = V2.Lerp(from, at, a < D ? 0f : KEo((a - D) / Game.BladedTail));
            if (tail.DistanceTo(at) > 2f)
            {
                SetColor(DiveGold.WithAlpha(0.28f * fade));
                Quad(tail, tail, at + n * 13f, at - n * 13f);
                SetColor(DiveLight.WithAlpha(0.55f * fade));
                Quad(tail, tail, at + n * 4f, at - n * 4f);
                for (int k = 0; k < 5; k++)  // speed lines either side
                {
                    float off = (KHash(fx.Pos.X + k * 3.1f) - 0.5f) * 44f, l0 = 0.15f + 0.6f * KHash(fx.Pos.Y + k * 1.7f);
                    V2 s0 = V2.Lerp(tail, at, l0) + n * off;
                    Line(s0, s0 + d * (26f + 20f * KHash(k + 0.5f)), 1.4f, DiveLight.WithAlpha(0.5f * fade));
                }
            }
            if (a < D)  // afterimages
                for (int k = 1; k <= 4; k++)
                {
                    float ta = a - k * 0.05f;
                    if (ta < 0f)
                        break;
                    KGhost(V2.Lerp(from, to, KSm(ta / D)), d.Angle, DiveGold, 0.36f * (1f - k / 5f));
                }
            // The whirling blade: two crescents round you at the cut's reach, two and a half turns over the rush.
            if (a < D + 0.12f)
            {
                float spin = d.Angle + MathX.Tau * 2.5f * KEo(Mathf.Min(a, D) / D) + MathX.Pi / 2f, al = a < D ? 1f : 1f - (a - D) / 0.12f;
                for (int k = 0; k < 2; k++)
                {
                    float h = spin + k * MathX.Pi;
                    KCrescent(at, h - 2.4f, h, Game.BladedSlashR - 26f, Game.BladedSlashR, DiveGold, al);
                    KCrescent(at, h - 1.2f, h, Game.BladedSlashR - 12f, Game.BladedSlashR - 2f, DiveLight, 0.8f * al);
                }
                Arc(at, Game.BladedSlashR, 1f, DiveGold.WithAlpha(0.18f * al), 48);
            }
            // A ring snapping out at each cut, where it landed.
            for (int k = 0; k < Game.BladedSlashes; k++)
            {
                float sa = a - Game.BladedSlashAt(k);
                if (sa < 0f || sa > 0.22f)
                    continue;
                V2 c = V2.Lerp(from, to, KSm(Game.BladedSlashAt(k) / D));
                Arc(c, Game.BladedSlashR * (0.8f + 0.25f * KEo(sa / 0.22f)), 2f * (1f - sa / 0.22f) + 0.5f, DiveGold.WithAlpha(0.55f * (1f - sa / 0.22f)), 48);
            }
            if (a >= D)  // dust where you land
                KPuffs(to, a - D, 6, fx.Pos.X, d.Angle + MathX.Pi, 2.4f, 70f, Game.BladedTail, HeraldDust, 9f, 0.5f);
        }

        /// An enemy cut: a white slash across it, square to the cut, and sparks thrown along it.
        void DrawDiveHitFx(Effect fx)
        {
            float a = fx.T, f = a / Game.BladedHitFx;
            V2 d = fx.Pos2.IsZero ? new V2(1f, 0f) : fx.Pos2.Normalized(), n = new V2(-d.Y, d.X);
            float half = (fx.Radius + 10f) * KEo(a / 0.06f);
            V2 c = fx.Pos + d * (f * 6f);
            Line(c - n * half - d * 3f, c + n * half + d * 3f, 4f * (1f - f), DiveGold.WithAlpha(0.6f * (1f - f)));
            Line(c - n * half - d * 3f, c + n * half + d * 3f, 1.8f * (1f - f), Rgba.White.WithAlpha(1f - f));
            KSparks(fx.Pos, a, 6, fx.Pos.X + fx.Pos.Y, d.Angle, 1.4f, 260f, Game.BladedHitFx, DiveLight);
        }

        /// A kill reset the cooldown: a gold burst and a glint on the enemy killed (RESET is on the HUD).
        void DrawDiveResetFx(Effect fx, float t)
        {
            float a = fx.T;
            GlowDisc(fx.Pos, 46f * (1f - a / 0.4f), DiveGold, 0.7f * Mathf.Clamp01(1f - a / 0.4f));
            for (int q = 0; q < 2; q++)
            {
                float aa = a - q * 0.1f;
                if (aa < 0f || aa > 0.5f)
                    continue;
                Arc(fx.Pos, fx.Radius + 6f + 52f * KEo(aa / 0.5f), 2.4f - q * 0.8f, DiveGold.WithAlpha(0.85f * (1f - aa / 0.5f)), 40);
            }
            KStar(fx.Pos, 22f * (1f - a / 0.35f), DiveLight, Mathf.Clamp01(1f - a / 0.35f), t * 3f);
            KSparks(fx.Pos, a, 12, fx.Pos.X * 0.7f, 0f, MathX.Tau, 220f, 0.6f, DiveGold);
        }
    }
}
