using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Arc Knuckles' dash chain (Docs/ArcKnuckles.md step 3; Docs/Mockups/gauntlets.html, "Full dash chain"). A Rocket Dash that
    /// hits something opens a window: press Fire within KnuckleChainWindowTime and the next move follows, each one opening the next
    /// window as it lands:
    ///   1. Flurry, where you landed: eight alternating jabs, then a straight. You're rooted while it runs.
    ///   2. Grab & Throw: grab the Flurry's target (not a boss, and it must be close), swing it round once and throw it along your aim.
    ///      It flies until it hits another enemy (both are hurt, the other one shoved and stunned) or runs out of distance.
    ///   3. Rocket Punch: the lead fist fires off on its chain down a line, hitting everything on it, then reels back.
    /// Player.KnuckleChain holds the stage (replicated: your own game roots you and shows the Fire prompt from it): 0 none, 1 the
    /// Flurry's window, 2 the Flurry, 3 the grab's window, 4 the grab and throw, 5 the Rocket Punch's window, 6 the Rocket Punch.
    /// Missing a window, a press that can't follow (a boss, the target gone), raising the parry or putting the weapon away ends it.
    /// Each move is one effect (FxKind.KnuckleChain, Radius = id * 10 + move: 1 Flurry, 2 Grab & Throw, 3 Rocket Punch), drawn from
    /// its age (Game/ArcKnucklesLook.cs); the grabbed enemy is carried by the host along KnuckleGrabFist, which the look shares.
    public partial class Game
    {
        public const float KnuckleChainWindowTime = 0.35f;
        public const int FlurryJabs = 8;
        public const float FlurryStart = 0.09f, FlurryGap = 0.085f, FlurryStraightAt = 0.79f, FlurryDur = 1.05f;
        public const float FlurryJabMult = 0.5f, FlurryStraightMult = 20f / 12f, FlurryReach = 66f, FlurryWidth = 20f, FlurryStraightReach = 78f, FlurryStraightKnock = 12f;
        public const float GrabRange = 110f, GrabLatch = 0.10f, GrabPull = 0.22f, GrabRelease = 0.74f, GrabDur = 1.15f, GrabHold = 14f;
        public const float KnuckleThrowSpeed = 520f, KnuckleThrowRange = 260f, KnuckleThrowMult = 20f / 12f, KnuckleThrowHitMult = 26f / 12f, KnuckleThrowKnock = 42f, KnuckleThrowStun = 0.5f;
        public const float RocketPunchReach = 210f, RocketPunchWidth = 18f, RocketPunchAt = 0.26f, RocketPunchMult = 28f / 12f, RocketPunchKnock = 18f, RocketPunchDur = 0.9f;

        /// The grabbing fist, `c` seconds into Grab & Throw, in front of the body (x along the aim at the grab, y across): it reaches,
        /// pulls the enemy in, swings it all the way round (accelerating), then thrusts it forward on release.
        public static V2 KnuckleGrabFist(float c)
        {
            if (c < GrabLatch)
            {
                float k = 1f - (1f - c / GrabLatch) * (1f - c / GrabLatch) * (1f - c / GrabLatch);
                return new V2(17f + 33f * k, 13f - 11f * k);
            }
            if (c < GrabPull)
            {
                float k = (c - GrabLatch) / (GrabPull - GrabLatch);
                k = k < 0.5f ? 2f * k * k : 1f - (-2f * k + 2f) * (-2f * k + 2f) / 2f;
                return new V2(50f - 20f * k, 2f + 4f * k);
            }
            if (c < 0.72f)
                return Rot(new V2(30f, 6f), KnuckleGrabSpin(c));
            if (c < 0.80f)
            {
                float k = 1f - (1f - (c - 0.72f) / 0.08f) * (1f - (c - 0.72f) / 0.08f) * (1f - (c - 0.72f) / 0.08f);
                return new V2(30f + 16f * k, 6f - 3f * k);
            }
            float u = Math.Min(1f, (c - 0.80f) / 0.35f);
            u = u < 0.5f ? 2f * u * u : 1f - (-2f * u + 2f) * (-2f * u + 2f) / 2f;
            return new V2(46f - 29f * u, 3f + 10f * u);
        }

        /// How far round the swing has turned, `c` seconds in (0 before it starts, a full turn by 0.72 s).
        public static float KnuckleGrabSpin(float c) => c < GrabPull ? 0f : c >= 0.72f ? -MathX.Tau : -MathX.Tau * (float)Math.Pow((c - GrabPull) / 0.5f, 1.7f);

        static V2 Rot(V2 p, float a) => new V2(p.X * (float)Math.Cos(a) - p.Y * (float)Math.Sin(a), p.X * (float)Math.Sin(a) + p.Y * (float)Math.Cos(a));

        class GrabRun
        {
            public int Owner, Target;
            public float T, Aim;          // seconds since the press; the aim it was grabbed along
            public V2 From;               // where the enemy was when the fist latched
            public bool Flying, Done;
            public V2 Pos, Dir;           // in flight
            public float Flown;
        }

        readonly List<GrabRun> grabRuns = new List<GrabRun>();

        /// Host: a press of the primary while a chain window is open (from FireArcKnuckles). True if it threw the chain's next move.
        bool FireKnuckleChain(Player p, float aim, float dmg)
        {
            var w = p.Weapon;
            switch (p.KnuckleChain)
            {
                case 1:  // the Flurry, where you landed
                {
                    p.KnuckleChain = 2;
                    p.KnuckleChainT = 0f;
                    p.FireCd = FlurryStraightAt;
                    for (int i = 0; i < FlurryJabs; i++)
                    {
                        var s = KnuckleSwing(p, aim, FlurryReach, 0f, w.Damage * dmg * FlurryJabMult, FlurryStart + i * FlurryGap, 8);
                        s.HalfWidth = FlurryWidth;
                        s.Thrust = true;
                        s.Sound = Sfx.KnuckleJab;
                    }
                    var st = KnuckleSwing(p, aim, FlurryStraightReach, 0f, w.Damage * dmg * FlurryStraightMult, FlurryStraightAt, 8);
                    st.HalfWidth = KnuckleUpperWidth;
                    st.Thrust = true;
                    st.Heavy = true;
                    st.Knock = FlurryStraightKnock;
                    st.Sound = Sfx.KnuckleRocketHit;
                    BroadcastFx(FxKind.KnuckleChain, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, FlurryReach), ArcBlue, p.Id * 10 + 1, FlurryDur, Sfx.None);
                    return true;
                }
                case 3:  // Grab & Throw: the Flurry's target, if it's still there, close and not a boss
                {
                    Enemy e;
                    if (!Enemies.TryGetValue(p.KnuckleTarget, out e) || !Targetable(e) || e.Def.IsBoss || e.Pos.DistanceTo(p.Pos) > GrabRange + e.Radius)
                    {
                        EndKnuckleChain(p);
                        return false;  // (the press throws a punch instead)
                    }
                    p.KnuckleChain = 4;
                    p.KnuckleChainT = 0f;
                    p.FireCd = GrabRelease;
                    grabRuns.Add(new GrabRun { Owner = p.Id, Target = e.Id, Aim = aim, From = e.Pos });
                    BroadcastFx(FxKind.KnuckleChain, p.Pos, p.Pos + V2.FromAngle(aim) * 60f, ArcBlue, p.Id * 10 + 2, GrabDur, Sfx.KnuckleGrab);
                    return true;
                }
                case 5:  // the Rocket Punch, down the line
                {
                    p.KnuckleChain = 6;
                    p.KnuckleChainT = 0f;
                    p.FireCd = RocketPunchDur * 0.8f;
                    var s = KnuckleSwing(p, aim, RocketPunchReach, 0f, w.Damage * dmg * RocketPunchMult, RocketPunchAt, 9);
                    s.HalfWidth = RocketPunchWidth;
                    s.Thrust = true;
                    s.Heavy = true;
                    s.Knock = RocketPunchKnock;
                    s.Sound = Sfx.KnuckleRocketHit;
                    BroadcastFx(FxKind.KnuckleChain, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, RocketPunchReach), ArcBlue, p.Id * 10 + 3, RocketPunchDur, Sfx.KnuckleRocket);
                    return true;
                }
            }
            return false;
        }

        /// Host: the Rocket Dash's fist hit `target`: the Flurry's window opens.
        void OpenKnuckleChain(Player p, Enemy target)
        {
            p.KnuckleChain = 1;
            p.KnuckleChainT = 0f;
            p.KnuckleTarget = target.Id;
        }

        /// Host: the chain is over (a window missed, the parry, the weapon put away); a grabbed enemy is let go where it is.
        void EndKnuckleChain(Player p)
        {
            p.KnuckleChain = 0;
            p.KnuckleChainT = 0f;
            foreach (var r in grabRuns)
                if (r.Owner == p.Id)
                    r.Done = true;
        }

        /// Host, every tick (from TickArcKnuckles): the windows close, the Flurry ends, the grabbed enemy is carried and thrown.
        void TickKnuckleChain(Player p, float dt)
        {
            if (p.KnuckleChain == 0)
                return;
            if (p.Weapon.Id != WeaponId.ArcKnuckles || p.Downed || State == MatchState.GameOver)
            {
                EndKnuckleChain(p);
                return;
            }
            p.KnuckleChainT += dt;
            switch (p.KnuckleChain)
            {
                case 1:
                case 3:
                case 5:
                    if (p.KnuckleChainT > KnuckleChainWindowTime + (p.KnuckleChain == 1 ? Math.Max(0f, p.FireCd) : 0f))
                        EndKnuckleChain(p);  // (the Flurry's window counts from when you can press: the dash's flight)
                    break;
                case 2:
                    if (p.KnuckleChainT >= FlurryStraightAt)
                    {
                        p.KnuckleChain = 3;  // the straight landed: the grab's window
                        p.KnuckleChainT = 0f;
                    }
                    break;
                case 6:
                    if (p.KnuckleChainT >= RocketPunchDur)
                        EndKnuckleChain(p);  // the fist is back: the chain is done
                    break;
            }
        }

        /// Host, every tick: grabbed enemies carried round and thrown.
        void TickGrabRuns(float dt)
        {
            for (int i = grabRuns.Count - 1; i >= 0; i--)
            {
                var r = grabRuns[i];
                Player p = null;
                Enemy e = null;
                if (r.Done || !Players.TryGetValue(r.Owner, out p) || !Enemies.TryGetValue(r.Target, out e) || e.Hp <= 0f)
                {
                    if (p != null && p.KnuckleChain == 4)
                        EndKnuckleChain(p);  // (the enemy died in your hands)
                    grabRuns.RemoveAt(i);
                    continue;
                }
                r.T += dt;
                e.FrozenT = Math.Max(e.FrozenT, 0.2f);  // held: it can't move or shoot
                e.KnockLeft = V2.Zero;
                if (!r.Flying)
                {
                    if (r.T < GrabLatch)
                        continue;  // the fist is still reaching
                    V2 hold = KnuckleHoldPoint(p.Pos, r.Aim, r.T);
                    float pull = Math.Min(1f, (r.T - GrabLatch) / (GrabPull - GrabLatch));
                    e.Pos = pull < 1f ? r.From + (hold - r.From) * pull : hold;
                    if (r.T >= GrabRelease)
                    {
                        r.Flying = true;  // let go, along where you aim now
                        r.Pos = e.Pos;
                        r.Dir = V2.FromAngle(p.Aim);
                        BroadcastSound(Sfx.KnuckleThrow);
                    }
                    continue;
                }
                float step = KnuckleThrowSpeed * dt;
                r.Pos += r.Dir * step;
                r.Flown += step;
                V2 kept = ClampToArena(r.Pos, e.Radius);
                bool wall = kept.DistanceTo(r.Pos) > 0.5f;
                e.Pos = kept;
                Enemy into = null;
                foreach (var o in Enemies.Values)
                    if (o != e && Targetable(o) && o.Pos.DistanceTo(e.Pos) <= o.Radius + e.Radius)
                    {
                        into = o;
                        break;
                    }
                if (into == null && !wall && r.Flown < KnuckleThrowRange)
                    continue;
                float dmg = p.Weapon.Damage * p.Stats.DmgMult;
                FangHurt(p, e, dmg * KnuckleThrowMult, e.Pos - r.Dir * 10f);  // (TwinBlades.cs: the hit through the on-hit pipeline)
                if (into != null)
                {
                    FangHurt(p, into, dmg * KnuckleThrowHitMult, e.Pos);
                    if (!Heavy(into))
                        KnockEnemy(into, r.Dir * KnuckleThrowKnock);
                    if (!into.Def.IsBoss)
                        into.FrozenT = Math.Max(into.FrozenT, KnuckleThrowStun);
                }
                e.FrozenT = 0f;
                KnockEnemy(e, (r.Dir * -1f + r.Dir.Orthogonal()) * 10f);  // it bounces off
                BroadcastFx(FxKind.Ring, e.Pos, ArcBlue, e.Radius + 24f, 0.3f, Sfx.KnuckleRocketHit);
                grabRuns.RemoveAt(i);
                if (p.KnuckleChain == 4)
                {
                    p.KnuckleChain = 5;  // it landed: the Rocket Punch's window
                    p.KnuckleChainT = 0f;
                    p.FireCd = 0f;
                }
            }
        }

        /// Where the held enemy is, `c` seconds into Grab & Throw: just beyond the grabbing fist.
        public static V2 KnuckleHoldPoint(V2 body, float aim, float c)
        {
            V2 f = Rot(KnuckleGrabFist(c), aim);
            float len = f.Length;
            return body + (len > 0.01f ? f + f * (GrabHold / len) : f);
        }
    }
}
