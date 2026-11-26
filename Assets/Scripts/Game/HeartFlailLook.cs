using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Heart Flail's look (the "Lifeline Heart Flail" mockup): a dark red anatomical heart, aorta stubs and coronary veins,
    /// on a chain of vein links from your hand, beating at Player.HfBpm (a lub-dub swell on every beat, HfBeats counts them):
    ///   - DrawHeartFlail (over the arena, every Heart Flail player): the chain and heart from HfMode/HfT/HfAng/HfPos (the clock
    ///     and the spin run on between snapshots so it moves smoothly), the swing's crescent, the spin's pulse ring, Open Heart's
    ///     veins to the allies it heals, the beat arc round your feet (gold when the next beat is a Cardiac Burst) and Open
    ///     Heart's cooldown;
    ///   - FxKind.HeartDrops: blood drops flying to who they heal; FxKind.HeartPulse: a heartbeat ring (green while spinning, a red
    ///     splat where the heart lands); FxKind.HeartBurst: the Cardiac Burst;
    ///   - DrawHeartGlow (the glow pass): the heart's glow swelling on each beat.
    public partial class ArenaApp
    {
        static readonly Rgba HfOutline = new Rgba(0.16f, 0.01f, 0.04f), HfVein = new Rgba(0.28f, 0.0f, 0.07f), HfIron = new Rgba(0.6f, 0.65f, 0.77f),
            HfLink = new Rgba(0.72f, 0.13f, 0.23f);

        class HfView { public float T, SeenT = -1f, Ang, BeatAge = 9f; public int Beats = -1; public byte Mode = 255; public V2 Pos; }
        readonly Dictionary<int, HfView> hfViews = new Dictionary<int, HfView>();

        /// The unit heart outline (the classic heart curve), apex at -y (it is turned to point away from the hand).
        static readonly V2[] HfShape = BuildHfShape();

        static V2[] BuildHfShape()
        {
            const int N = 28;
            var pts = new V2[N];
            for (int i = 0; i < N; i++)
            {
                float a = i * MathX.Tau / N;
                float s = Mathf.Sin(a), x = 16f * s * s * s, y = 13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f * a) - 2f * Mathf.Cos(3f * a) - Mathf.Cos(4f * a);
                pts[i] = new V2(x / 17f, -(y + 2f) / 17f);  // about -1..1, the apex at y = -1
            }
            return pts;
        }

        /// The swell of one beat, `age` seconds after it: a lub and a smaller dub.
        static float HfPump(float age) => 0.22f * Mathf.Exp(-age * 11f) + (age > 0.14f ? 0.12f * Mathf.Exp(-(age - 0.14f) * 12f) : 0f);

        /// Where this screen draws the heart now (its own clock between snapshots), and how far it has pumped.
        HfView HfViewOf(Player p)
        {
            HfView v;
            if (!hfViews.TryGetValue(p.Id, out v))
                hfViews[p.Id] = v = new HfView { Pos = p.Disp };
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            if (p.HfT != v.SeenT || p.HfMode != v.Mode)
            {
                v.T = p.HfT;
                v.SeenT = p.HfT;
            }
            else
                v.T += dt;
            if (p.HfMode != v.Mode)
            {
                if (p.HfMode == Game.HfSpin)
                    v.Ang = p.HfAng;
                v.Mode = p.HfMode;
            }
            if (p.HfBeats != v.Beats)
            {
                if (v.Beats >= 0)
                    v.BeatAge = 0f;
                v.Beats = p.HfBeats;
            }
            else
                v.BeatAge += dt;
            V2 target;
            switch (p.HfMode)
            {
                case Game.HfSwing:
                    v.Ang = Game.HfSwingAng(p.HfAim0, p.HfSide, v.T / Game.HfSwingDur);
                    target = p.Disp + V2.FromAngle(v.Ang) * game.HfReachOf(p);
                    v.Pos = target;
                    return v;
                case Game.HfSpin:
                    v.Ang += Game.HfSpinOmega * dt;
                    v.Ang += MathX.WrapPi(p.HfAng - v.Ang) * Mathf.Min(1f, dt * 6f);  // (drift back onto the host's angle)
                    v.Pos = p.Disp + V2.FromAngle(v.Ang) * game.HfSpinROf(p);
                    return v;
                case Game.HfRest:
                    target = p.Disp + V2.FromAngle(p.Aim + 2.3f) * 24f;
                    break;
                default:
                    target = p.HfPos;
                    break;
            }
            v.Pos = V2.Lerp(v.Pos, target, Mathf.Min(1f, dt * (p.HfMode == Game.HfRest ? 14f : 22f)));
            return v;
        }

        /// Over the arena: every Heart Flail player's chain and heart, and its reading.
        void DrawHeartFlail(Player p, float t)
        {
            if (p.Downed)
                return;
            var v = HfViewOf(p);
            float bpmK = Mathf.Clamp01((p.HfBpm - Game.HfBpmMin) / (Game.HfBpmMax - Game.HfBpmMin));
            bool primed = p.HfBpm >= Game.HfBpmMax - 0.5f;
            V2 dir = V2.FromAngle(p.Aim), hand = p.Disp + dir * 11f - dir.Orthogonal() * 8f;
            // the swing's crescent
            if (p.HfMode == Game.HfSwing)
            {
                float k = Mathf.Clamp01(v.T / Game.HfSwingDur), start = Game.HfSwingAng(p.HfAim0, p.HfSide, 0f), r = game.HfReachOf(p);
                float span = v.Ang - start, al = 1f - Mathf.Clamp01((v.T - Game.HfSwingDur * 0.7f) / (Game.HfSwingDur * 0.3f));
                const int K = 5;
                for (int i = 0; i < K; i++)
                {
                    float u0 = i / (float)K, u1 = (i + 1f) / K;
                    Arc(p.Disp, r, 2f + 9f * u1, Game.HfBlood.WithAlpha((0.08f + 0.45f * u1) * al), 6, start + span * u0, span * (u1 - u0));
                }
                Arc(p.Disp, r + 5f, 1.4f, Game.HfPink.WithAlpha(0.55f * al * k), 10, start + span * 0.5f, span * 0.5f);
            }
            // the spin's healing ring (your own) and its blur
            if (p.HfMode == Game.HfSpin)
            {
                float sr = game.HfSpinROf(p);
                Arc(p.Disp, sr, 10f, Game.HfBlood.WithAlpha(0.1f), 40);
                Arc(p.Disp, sr, 3f, Game.HfPink.WithAlpha(0.45f), 14, v.Ang - 1.1f, 1.1f);
                if (p.Id == game.MyId)
                {
                    float pr = Game.HfPulseR * Game.AreaMult(p);
                    for (int i = 0; i < 24; i += 2)
                        Arc(p.Disp, pr, 1.4f, Game.HfGreen.WithAlpha(0.35f), 3, i * MathX.Tau / 24f + t * 0.4f, MathX.Tau / 24f);
                }
            }
            // Open Heart's veins
            if (p.HfMode == Game.HfRoot)
            {
                float vr = Game.HfVeinR * Game.AreaMult(p);
                for (int i = 0; i < 30; i += 2)
                    Arc(v.Pos, vr, 1.3f, Game.HfBlood.WithAlpha(0.3f), 3, i * MathX.Tau / 30f - t * 0.3f, MathX.Tau / 30f);
                int n = 0;
                foreach (var q in game.Players.Values)
                {
                    if (q.Id == p.Id || q.Downed || q.Disp.FartherThan(v.Pos, vr))
                        continue;
                    HfVeinTo(v.Pos, q.Disp, t, n++);
                }
            }
            // the chain: vein links from your hand, sagging when slack
            float sag = p.HfMode == Game.HfRest ? 7f : p.HfMode == Game.HfRoot ? 16f : p.HfMode == Game.HfReel ? 10f : 0f;
            HfChain(hand, v.Pos, sag, t, v.BeatAge);
            // the heart: pointing away from your hand
            V2 away = v.Pos - hand;
            float ang = away.IsZero ? p.Aim : away.Angle;
            float size = 15f * (1f + HfPump(v.BeatAge));
            HfHeart(v.Pos, ang, size, primed, t);
            // the beat round your feet: red filling to gold
            float a0 = MathX.Pi / 2f + 0.9f, sweep = MathX.Tau - 1.8f, fr = p.Def.BodyR + 6f;
            Arc(p.Disp, fr, 2.4f, new Rgba(0f, 0f, 0f, 0.35f), 24, a0, sweep);
            if (bpmK > 0.01f)
                Arc(p.Disp, fr, 2.4f, primed ? Game.HfGold.WithAlpha(0.7f + 0.3f * Mathf.Sin(t * 12f)) : Game.HfBlood.Mix(Game.HfGold, bpmK * bpmK).WithAlpha(0.85f), 24, a0, sweep * bpmK);
            // Open Heart's cooldown (your own)
            if (p.Id == game.MyId && p.HfCd > 0f)
            {
                float cd = Mathf.Clamp01(p.HfCd / Game.HfThrowCd);
                Arc(p.Disp, p.Def.BodyR + 11f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                Arc(p.Disp, p.Def.BodyR + 11f, 2f, Game.HfPink.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * (1f - cd));
            }
        }

        /// The chain from hand to heart: a dark vein with a red core, iron rings at each link, and a pulse of blood running out to
        /// the heart after each beat.
        void HfChain(V2 a, V2 b, float sag, float t, float beatAge)
        {
            float len = a.DistanceTo(b);
            if (len < 2f)
                return;
            V2 mid = (a + b) * 0.5f + new V2(0f, sag);
            int n = Mathf.Clamp((int)(len / 9f), 4, 40);
            V2 prev = a;
            float pulse = Mathf.Clamp01(beatAge / 0.25f);
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n, w = 1f - u;
                V2 at = a * (w * w) + mid * (2f * w * u) + b * (u * u);
                Line(prev, at, 5f, HfOutline);
                bool lit = beatAge < 0.3f && Mathf.Abs(u - pulse) < 0.12f;
                Line(prev, at, 2.6f, lit ? Game.HfPink : (i % 2 == 0 ? HfLink : Game.HfBlood));
                if (i < n)
                    Circle(at, 1.4f, HfIron.WithAlpha(0.85f), 6);
                prev = at;
            }
        }

        /// One heart at `c`, its apex along `ang`, `s` px from middle to edge.
        void HfHeart(V2 c, float ang, float s, bool primed, float t)
        {
            float rot = ang - MathX.Pi / 2f;  // (turned so the apex points along `ang`, away from the hand)
            hfC = c; hfRot = rot + MathX.Pi; hfS = s;  // (the details: aorta at the top, between the lobes)
            // the aorta and its branches, behind the heart
            Line(HfPt(-0.18f, 0.45f), HfPt(-0.22f, 1.05f), 4.6f * s / 12f, HfOutline);
            Line(HfPt(0.2f, 0.4f), HfPt(0.26f, 1.15f), 5.4f * s / 12f, HfOutline);
            Line(HfPt(-0.18f, 0.45f), HfPt(-0.22f, 1.0f), 3f * s / 12f, Game.HfDeep);
            Line(HfPt(0.2f, 0.4f), HfPt(0.26f, 1.1f), 3.8f * s / 12f, new Rgba(0.72f, 0.13f, 0.23f));
            Circle(HfPt(0.26f, 1.12f), 1.6f * s / 12f, new Rgba(0.35f, 0.02f, 0.08f), 8);
            // the body: an outline, the muscle, then a lit upper lobe
            HfFill(c, rot, s * 1.12f, HfOutline);
            HfFill(c, rot, s, primed ? Game.HfDeep.Mix(Game.HfGold, 0.15f + 0.1f * Mathf.Sin(t * 12f)) : Game.HfDeep);
            HfFill(c + new V2(0.08f, -0.1f).Rotated(rot) * s, rot, s * 0.84f, Game.HfBlood);
            Circle(HfPt(-0.42f, 0.45f), 0.3f * s, Game.HfPink.WithAlpha(0.55f), 10);
            Circle(HfPt(-0.48f, 0.52f), 0.12f * s, new Rgba(1f, 0.85f, 0.88f, 0.75f), 8);
            // coronary veins
            Line(HfPt(0.02f, 0.62f), HfPt(-0.1f, 0.05f), 1.2f, HfVein.WithAlpha(0.8f));
            Line(HfPt(-0.1f, 0.05f), HfPt(0.05f, -0.6f), 1.2f, HfVein.WithAlpha(0.8f));
            Line(HfPt(-0.1f, 0.2f), HfPt(0.45f, 0.35f), 1f, HfVein.WithAlpha(0.7f));
            // the shackle where the chain meets it
            Arc(HfPt(0f, 1.05f) + (HfPt(0f, 1.05f) - c).Normalized() * 2f, 3f, 1.6f, HfIron, 10);
        }

        V2 hfC;
        float hfRot, hfS;
        V2 HfPt(float x, float y) => hfC + new V2(x, y).Rotated(hfRot) * hfS;  // a point on the heart HfHeart is drawing

        void HfFill(V2 c, float rot, float s, Rgba col)
        {
            SetColor(col);
            V2 prev = c + HfShape[HfShape.Length - 1].Rotated(rot) * s;
            for (int i = 0; i < HfShape.Length; i++)
            {
                V2 next = c + HfShape[i].Rotated(rot) * s;
                Tri(c, prev, next);
                prev = next;
            }
        }

        /// A vein from the rooted heart to an ally, blood beading along it toward them.
        void HfVeinTo(V2 a, V2 b, float t, int seed)
        {
            V2 d = b - a;
            if (d.IsZero)
                return;
            V2 side = d.Orthogonal().Normalized();
            V2 mid = (a + b) * 0.5f + side * (Mathf.Sin(t * 3f + seed) * 18f);
            V2 prev = a;
            const int N = 14;
            for (int i = 1; i <= N; i++)
            {
                float u = i / (float)N, w = 1f - u;
                V2 at = a * (w * w) + mid * (2f * w * u) + b * (u * u);
                Line(prev, at, 3.4f, HfOutline.WithAlpha(0.8f));
                Line(prev, at, 1.8f, Game.HfBlood);
                prev = at;
            }
            for (int k = 0; k < 3; k++)
            {
                float u = (t * 1.2f + k / 3f) % 1f, w = 1f - u;
                Circle(a * (w * w) + mid * (2f * w * u) + b * (u * u), 2.4f, Game.HfPink, 8);
            }
            Arc(b, 16f + 3f * Mathf.Sin(t * 8f), 1.6f, Game.HfGreen.WithAlpha(0.6f), 18);
        }

        /// FxKind.HeartDrops: drops squeezed out of the heart, flying to who they heal (Pos2 = target id, count; Radius = owner).
        void DrawHeartDrops(Effect fx)
        {
            int target = Mathf.RoundToInt(fx.Pos2.X), n = Mathf.Clamp(Mathf.RoundToInt(fx.Pos2.Y), 1, 12);
            Player to = null;
            bool homing = target >= 0 && game.Players.TryGetValue(target, out to);
            float u = Mathf.Clamp01(fx.T / Game.HfDropFlight), seed = fx.Pos.X * 0.31f + fx.Pos.Y * 0.77f;
            for (int i = 0; i < n; i++)
            {
                float a = i * MathX.Tau / n + TtHash(seed) * MathX.Tau;
                V2 burst = fx.Pos + V2.FromAngle(a) * (10f + 34f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.6f)));
                V2 at;
                float al;
                if (homing)
                {
                    float h = Game.HfEase(Mathf.Clamp01((u - 0.2f) / 0.8f));
                    at = V2.Lerp(burst, to.Disp, h);
                    al = fx.T > Game.HfDropFlight ? 1f - (fx.T - Game.HfDropFlight) / 0.15f : 1f;
                }
                else
                {
                    at = fx.Pos + V2.FromAngle(a) * (10f + 40f * u) + new V2(0f, 20f * u * u);
                    al = 1f - u;
                }
                if (al <= 0.01f)
                    continue;
                Circle(at, 3.4f, HfOutline.WithAlpha(0.8f * al), 8);
                Circle(at, 2.6f, Game.HfBlood.WithAlpha(al), 8);
                Circle(at + new V2(-0.8f, -0.8f), 1f, Game.HfPink.WithAlpha(al), 6);
            }
            if (homing && fx.T > Game.HfDropFlight)  // the heal blooming as they land
            {
                float q = (fx.T - Game.HfDropFlight) / 0.15f;
                Arc(to.Disp, to.Def.BodyR + 4f + 10f * q, 2f, Game.HfGreen.WithAlpha(0.8f * (1f - q)), 20);
            }
        }

        /// FxKind.HeartPulse: a heartbeat ring out to Radius (Pos2.Y = 1: the heart landing, a red splat).
        void DrawHeartPulse(Effect fx)
        {
            float k = Mathf.Clamp01(fx.T / fx.Dur), r = fx.Radius * Game.HfEase(k), al = 1f - k;
            bool splat = fx.Pos2.Y > 0.5f;
            Arc(fx.Pos, r, (splat ? 5f : 3f) * al + 1f, fx.Color.WithAlpha(0.8f * al), 36);
            Arc(fx.Pos, r * 0.86f, 1.4f, fx.Color.WithAlpha(0.35f * al), 30);
            if (!splat)
                return;
            float seed = fx.Pos.X * 0.41f + fx.Pos.Y * 0.67f;
            for (int i = 0; i < 12; i++)
            {
                float a = TtHash(seed + i) * MathX.Tau, d = fx.Radius * (0.3f + 0.7f * TtHash(seed + i * 2.3f)) * Game.HfEase(k * 1.6f);
                Circle(fx.Pos + V2.FromAngle(a) * d, (1.6f + 2.4f * TtHash(seed + i * 3.7f)) * (1f - 0.5f * k), Game.HfBlood.WithAlpha(0.9f * al), 6);
            }
        }

        /// FxKind.HeartBurst: the Cardiac Burst, a gold shock ring with a red echo and a heart flashing over you.
        void DrawHeartBurst(Effect fx)
        {
            float k = Mathf.Clamp01(fx.T / fx.Dur), r = fx.Radius * Game.HfEase(k * 1.3f), al = 1f - k;
            Arc(fx.Pos, r, 8f * al + 2f, Game.HfGold.WithAlpha(0.85f * al), 48);
            Arc(fx.Pos, r * 0.82f, 4f * al + 1f, Game.HfBlood.WithAlpha(0.7f * al), 44);
            Arc(fx.Pos, r * 0.6f, 2f, Game.HfPink.WithAlpha(0.4f * al), 36);
            if (k < 0.5f)
            {
                float hk = k / 0.5f;
                HfFill(fx.Pos + new V2(0f, -4f), MathX.Pi, 14f + 30f * hk, Game.HfGold.WithAlpha(0.35f * (1f - hk)));
            }
        }

        /// The glow pass: each heart glows, swelling on every beat; the burst flashes.
        void DrawHeartGlow()
        {
            foreach (var p in game.Players.Values)
            {
                HfView v;
                if (p.Weapon.Id != WeaponId.HeartFlail || p.Downed || !hfViews.TryGetValue(p.Id, out v))
                    continue;
                bool primed = p.HfBpm >= Game.HfBpmMax - 0.5f;
                GlowDisc(v.Pos, 20f + 30f * HfPump(v.BeatAge), primed ? Game.HfGold : Game.HfBlood, 0.22f + 1.2f * HfPump(v.BeatAge));
            }
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.HeartBurst && fx.T < fx.Dur)
                    GlowDisc(fx.Pos, fx.Radius * 0.6f, Game.HfGold, 0.35f * (1f - fx.T / fx.Dur));
        }

        /// In the hand: an iron grip with the chain's first ring (the heart and the chain are DrawHeartFlail's).
        void DrawHeldHeartFlail(Player p, float t, int hold)
        {
            MLine(-3f, 0f, 7f, 0f, 3.6f, Hx(0x2a3550));
            MLine(-2f, 0f, 6f, 0f, 2f, Hx(0x6b7893));
            MArc(9.5f, 0f, 2.6f, 0f, MathX.Tau, 1.4f, Hx(0x9aa7c4));
            MCirc(-3.5f, 0f, 1.8f, Hx(0xc4122e), 6);
        }
    }
}
