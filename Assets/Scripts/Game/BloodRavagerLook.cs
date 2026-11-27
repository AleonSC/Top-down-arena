using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Blood Ravager's look (Core/BloodRavager.cs; the mockup is the claude.ai artifact "Hemomancer Blood Legs", variant 4
    /// Ravager). Drawing only, from the replicated Player.RvT / RvLen and FxKind.RavagerLash:
    ///   - the walk: this screen keeps its own RavagerGait per player, fed the drawn position, so the eight legs step the same
    ///     way everywhere without sending a foot;
    ///   - each leg (DrawRavagerLeg): a thin two-part blood limb from a hip on the body to a planted claw, the knee bent out and
    ///     lifted toward the camera (more while it steps), a dark rim, a wet highlight, two barbs on the thigh, a knob at the
    ///     knee, a pale claw tip, and a shadow under the foot that shrinks as it lifts;
    ///   - under the body (DrawRavagerUnder): the back four legs, a pulsing red glow, blood stains where feet landed, drops off
    ///     the knees and, for your own legs, the dashed reach ring with a ring round each enemy in reach;
    ///   - over the body (DrawRavagerOver): the front four legs and three pips under you, lit for each leg lashing (yours only);
    ///   - a lash (from FxKind.RavagerLash): the leg's claw goes out to the enemy and back along a fading streak, following it if
    ///     it moves, with a spray of drops where it lands.
    /// With Visual effects off: plain lines for the legs and no stains, drops or glow.
    public partial class ArenaApp
    {
        static readonly Rgba RvClaw = new Rgba(1f, 0.84f, 0.85f), RvSheen = new Rgba(1f, 0.47f, 0.51f), RvShadow = new Rgba(0f, 0f, 0f, 0.45f);
        static readonly Rgba RvBodyGlow = new Rgba(0.78f, 0.08f, 0.16f);

        class RvSeen { public RavagerGait Gait = new RavagerGait(); public float LastT = -1f; public int Frame; }
        class RvDrop { public V2 Pos, Vel; public float Life, R; }
        class RvStain { public V2 Pos; public float R, A, Rot; }

        readonly Dictionary<int, RvSeen> rvSeen = new Dictionary<int, RvSeen>();
        readonly List<RvDrop> rvDrops = new List<RvDrop>();
        readonly List<RvStain> rvStains = new List<RvStain>();
        readonly HashSet<Effect> rvSplashed = new HashSet<Effect>();
        readonly List<int> rvGone = new List<int>();
        readonly System.Random rvRng = new System.Random(7);
        int rvFrame;
        float rvLastT = -1f;

        float RvRand => (float)rvRng.NextDouble();

        /// The lash a leg of `p`'s is making now, if any.
        Effect RavagerLashOf(Player p, int leg)
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.RavagerLash && Mathf.RoundToInt(fx.Radius) == p.Id && Mathf.RoundToInt(fx.Pos2.X) == leg && fx.T < fx.Dur)
                    return fx;
            return null;
        }

        /// The particles' clock: once a frame, whoever draws first.
        void TickRavagerBits(float t)
        {
            if (t == rvLastT)
                return;
            float dt = rvLastT < 0f ? 0f : Mathf.Clamp(t - rvLastT, 0f, 0.05f);
            rvLastT = t;
            rvFrame++;
            for (int i = rvDrops.Count - 1; i >= 0; i--)
            {
                var d = rvDrops[i];
                d.Life -= dt;
                d.Pos += d.Vel * dt;
                d.Vel *= Mathf.Pow(0.08f, dt);
                if (d.Life <= 0f)
                    rvDrops.RemoveAt(i);
            }
            for (int i = rvStains.Count - 1; i >= 0; i--)
            {
                rvStains[i].A -= dt * 0.06f;
                if (rvStains[i].A <= 0f)
                    rvStains.RemoveAt(i);
            }
            if (rvStains.Count > 160)
                rvStains.RemoveRange(0, rvStains.Count - 160);
            rvGone.Clear();
            foreach (var kv in rvSeen)
                if (rvFrame - kv.Value.Frame > 30)
                    rvGone.Add(kv.Key);
            foreach (var id in rvGone)
                rvSeen.Remove(id);
            rvSplashed.RemoveWhere(fx => !game.Effects.Contains(fx));
        }

        void RvBurst(V2 at, int n, float speed)
        {
            if (!fxOn)
                return;
            for (int i = 0; i < n; i++)
            {
                float a = RvRand * MathX.Tau, v = (40f + RvRand * 140f) * speed * 0.5f;
                rvDrops.Add(new RvDrop { Pos = at, Vel = V2.FromAngle(a) * v, Life = 0.4f + RvRand * 0.5f, R = 1.5f + RvRand * 2.5f });
            }
        }

        /// The walk, stepped to this frame (once a frame per player).
        RvSeen RavagerWalk(Player p, float t)
        {
            RvSeen s;
            if (!rvSeen.TryGetValue(p.Id, out s))
                rvSeen[p.Id] = s = new RvSeen();
            if (s.LastT == t)
                return s;
            float dt = s.LastT < 0f ? 0f : Mathf.Clamp(t - s.LastT, 0f, 0.05f);
            if (s.LastT < 0f || rvFrame - s.Frame > 2)
                s.Gait.Started = false;  // (new, or not drawn for a while: the legs burst out of the body again)
            s.LastT = t;
            s.Frame = rvFrame;
            for (int i = 0; i < RavagerGait.Legs; i++)
                s.Gait.Busy[i] = RavagerLashOf(p, i) != null;
            s.Gait.Update(p.Disp, p.Aim, dt);
            for (int i = 0; i < RavagerGait.Legs; i++)
                if ((s.Gait.Landed & (1 << i)) != 0 && fxOn)
                {
                    RvBurst(s.Gait.Foot[i], 3, 1.2f);
                    rvStains.Add(new RvStain { Pos = s.Gait.Foot[i], R = 7f + RvRand * 5f, A = 0.35f, Rot = RvRand * 6f });
                }
            if (fxOn && dt > 0f && RvRand < dt * 20f)  // a drip off a knee
            {
                int leg = rvRng.Next(RavagerGait.Legs);
                var j = RavagerJoints(p, s.Gait, leg);
                rvDrops.Add(new RvDrop { Pos = j.Knee, Vel = new V2((RvRand - 0.5f) * 20f, 20f), Life = 0.5f, R = 1.5f });
            }
            return s;
        }

        struct RvJoints { public V2 Hip, Knee, Foot, Ground; public float Lift; public Effect Lash; }

        /// Hip, knee and claw of leg i (a two-bone reach, the knee bent out from the body and lifted).
        RvJoints RavagerJoints(Player p, RavagerGait g, int i)
        {
            V2 foot = g.Foot[i];
            float lift = g.StepT[i] < 1f ? Mathf.Sin(g.StepT[i] * Mathf.PI) : 0f;
            var lash = RavagerLashOf(p, i);
            if (lash != null)
            {
                float k = Mathf.Clamp01(lash.T / lash.Dur), ext = Mathf.Sin(k * Mathf.PI);
                Enemy e;
                V2 at = game.Enemies.TryGetValue(Mathf.RoundToInt(lash.Pos2.Y), out e) ? e.Disp : lash.Pos;
                foot = V2.Lerp(lash.Pos, at, ext);
                lift = 0.35f * ext;
                if (k >= Game.RvLashHitAt && rvSplashed.Add(lash))
                    RvBurst(at, 5, 1.4f);
            }
            V2 hip = g.Hip(i), off = foot - hip;
            float d = Mathf.Clamp(off.Length, 1f, RavagerGait.L1 + RavagerGait.L2 - 1f), a = off.Angle;
            float c = (RavagerGait.L1 * RavagerGait.L1 + d * d - RavagerGait.L2 * RavagerGait.L2) / (2f * RavagerGait.L1 * d);
            float side = Mathf.Sign(MathX.WrapPi(a - g.Face));
            if (side == 0f)
                side = 1f;
            float ka = a - side * Mathf.Acos(Mathf.Clamp(c, -1f, 1f));
            V2 knee = hip + V2.FromAngle(ka) * RavagerGait.L1 - new V2(0f, 10f + lift * 14f);
            return new RvJoints { Hip = hip, Knee = knee, Foot = foot - new V2(0f, lift * 16f), Ground = foot, Lift = lift, Lash = lash };
        }

        /// A tapered limb segment: half-width w1 at a, w2 at b.
        void RvSeg(V2 a, V2 b, float w1, float w2, Rgba col)
        {
            V2 n = (b - a).Normalized().Orthogonal();
            if (n.IsZero)
                return;
            Quad(a + n * w1, b + n * w2, b - n * w2, a - n * w1, col);
        }

        void DrawRavagerLeg(Player p, RavagerGait g, int i)
        {
            var j = RavagerJoints(p, g, i);
            if (!fxOn)
            {
                Line(j.Hip, j.Knee, 3f, Game.RvBlood);
                Line(j.Knee, j.Foot, 2f, Game.RvBlood);
                return;
            }
            Ellipse(j.Ground, 10f - j.Lift * 3f, 5f - j.Lift * 1.5f, RvShadow);
            RvSeg(j.Hip, j.Knee, 5f, 3.5f, Game.RvBloodDk);  // the rim
            RvSeg(j.Knee, j.Foot, 4f, 1f, Game.RvBloodDk);
            RvSeg(j.Hip, j.Knee, 3f, 1.5f, Game.RvBlood);  // the limb
            RvSeg(j.Knee, j.Foot, 2f, 0.01f, Game.RvBlood);
            RvSeg(j.Hip, j.Knee, 1.8f, 1.3f, RvSheen.WithAlpha(0.55f));  // the wet sheen
            RvSeg(j.Knee, j.Foot, 1.4f, 0.3f, RvSheen.WithAlpha(0.5f));
            float a = (j.Knee - j.Hip).Angle;
            for (int s = 0; s < 2; s++)  // two barbs on the thigh
            {
                V2 at = V2.Lerp(j.Hip, j.Knee, s == 0 ? 0.4f : 0.75f);
                RvSeg(at, at + V2.FromAngle(a - 1.6f) * 6f, 1.5f, 0.01f, Game.RvBloodDk);
            }
            Circle(j.Knee, 3.5f, Game.RvBlood, 10);
            Circle(j.Knee - new V2(1f, 1f), 1.2f, Game.RvBloodHi, 6);
            Circle(j.Foot, 1.2f, RvClaw, 6);
            if (j.Lash != null)  // the streak it leaves
            {
                float k = Mathf.Clamp01(j.Lash.T / j.Lash.Dur);
                Line(j.Lash.Pos, j.Foot, 2f, Game.RvBloodHi.WithAlpha((1f - k) * 0.4f));
            }
        }

        /// WorldRenderer.DrawPlayerAuras: under a player whose legs are out: stains, glow, the reach ring, the back four legs.
        void DrawRavagerUnder(Player p, float t)
        {
            if (!Game.RavagerOn(p))
                return;
            var seen = RavagerWalk(p, t);
            if (p.Id == game.MyId)
            {
                Rgba ring = Game.RvBloodHi.WithAlpha(0.2f);
                for (int k = 0; k < 40; k++)  // the dashed reach ring
                    Arc(p.Disp, Game.RvLashR, 1f, ring, 2, k * MathX.Tau / 40f, MathX.Tau / 40f * 0.45f);
                foreach (var e in game.Enemies.Values)
                    if (e.Hp > 0f && e.SpawnT <= 0f && e.Disp.CloserThan(p.Disp, Game.RvLashR + e.Radius))
                        Arc(e.Disp, e.Radius + 5f, 1f, ring, 16);
            }
            if (fxOn)
            {
                float pulse = 1f + Mathf.Sin(t * 8.3f) * 0.08f;
                Circle(p.Disp, 24f * pulse, RvBodyGlow.WithAlpha(0.25f), 24);
            }
            for (int i = 0; i < RavagerGait.Legs; i++)
                if (Mathf.Abs(RavagerGait.LegAng[i]) > 1.6f)
                    DrawRavagerLeg(p, seen.Gait, i);
        }

        /// WorldRenderer, after the body: the front four legs and your lash pips.
        void DrawRavagerOver(Player p, float t)
        {
            if (!Game.RavagerOn(p))
                return;
            var seen = RavagerWalk(p, t);
            for (int i = 0; i < RavagerGait.Legs; i++)
                if (Mathf.Abs(RavagerGait.LegAng[i]) <= 1.6f)
                    DrawRavagerLeg(p, seen.Gait, i);
            if (p.Id == game.MyId)
            {
                int busy = 0;
                for (int i = 0; i < RavagerGait.Legs; i++)
                    if (RavagerLashOf(p, i) != null)
                        busy++;
                for (int k = 0; k < Game.RvLashMax; k++)  // a pip a leg that can lash at once, lit while it does
                {
                    V2 at = p.Disp + new V2((k - 1) * 9f, p.Def.BodyR + 12f);
                    Polygon(at, 3.6f, 4, MathX.Pi / 4f, CrtStyle.Ink.ToRgba(1f));
                    Polygon(at, 2.6f, 4, MathX.Pi / 4f, k < busy ? Game.RvBloodHi : new Rgba(0.03f, 0.07f, 0.19f));
                }
            }
        }

        /// WorldRenderer.DrawArenaContents, before anyone's auras: the particles' clock and the stains where feet landed.
        void DrawRavagerStains(float t)
        {
            TickRavagerBits(t);
            if (fxOn)
                foreach (var s in rvStains)
                    Ellipse(s.Pos, s.R, s.R * 0.7f, s.Rot, Game.RvBlood.WithAlpha(Mathf.Max(0f, s.A)), 12);
        }

        /// WorldRenderer, over the arena's contents: the drops off the legs.
        void DrawRavagerDrops()
        {
            if (fxOn)
                foreach (var d in rvDrops)
                    Circle(d.Pos, d.R, Game.RvBloodHi.WithAlpha(Mathf.Min(1f, d.Life * 2f)), 8);
        }
    }
}
