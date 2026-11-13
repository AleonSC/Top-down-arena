using System;
using System.Collections.Generic;

namespace Arena
{
    /// Earthquake (Tank Summoner; Docs/Mockups/burrower-visuals.html, look A "dirt mound"): you slam the ground (25 damage,
    /// 140 px, slows), and half a second later a Burrower breaks out at your cursor. It dives and tunnels for BurrowerRun
    /// seconds at BurrowerSpeed, steering at the nearest enemy it hasn't bitten, and hurts each enemy it passes under once
    /// (30). A capstone Earthquake also freezes whatever it bites. It blocks nothing and can't be hurt. When its run is over it shrinks into
    /// the ground and stays (unseen) for BurrowerSink seconds so the client can fade the scar it left.
    /// Clients draw it from the minion's age (telegraph, breach, tunnel, sink), Aim (heading) and Disp; see Game/BurrowerLook.cs.
    public partial class Game
    {
        public const float BurrowerTelegraph = 0.5f, BurrowerSurface = 1.1f, BurrowerRun = 3f, BurrowerSink = 2.2f;
        public const float BurrowerTime = BurrowerSurface + BurrowerRun + BurrowerSink;
        public const float BurrowerTurn = 3.2f, BurrowerReach = 650f, BurrowerSeek = 700f;
        const float QuakeSlamDamage = 25f, QuakeSlamRadius = 140f, QuakeSlamSlow = 2f;

        /// Host: the slam, then the Burrower at the cursor (at most BurrowerReach from you).
        void CastEarthquake(Player p, V2 target, bool cap, float dmg)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) >= QuakeSlamRadius + e.Radius)
                    continue;
                HurtEnemy(e, QuakeSlamDamage * dmg, p.Id);
                Slow(e, QuakeSlamSlow, 0.5f);
            }
            BroadcastFx(FxKind.Boom, p.Pos, new Rgba(0.7f, 0.6f, 0.45f), QuakeSlamRadius, 0.4f, Sfx.Explode);

            foreach (var old in Minions.Values)
                if (old.Owner == p.Id && old.Kind == MinionKind.Burrower)
                    old.T = Math.Min(old.T, BurrowerSink - 0.3f);  // a new one replaces the last: it sinks at once
            V2 to = target - p.Pos;
            if (to.Length > BurrowerReach)
                target = p.Pos + to.Normalized() * BurrowerReach;
            var m = SpawnMinion(MinionKind.Burrower, p, target, BurrowerTime);
            m.Aim = to.Length > 40f ? to.Angle : p.Aim;
            m.Capstone = cap;
            m.Burrowed = new HashSet<int>();
            Sounds.Add(Sfx.Summon);
        }

        /// Host, each tick: nothing until it breaks out, then it runs and bites.
        void TickBurrower(Minion m, Player owner, float dt, float dmg)
        {
            float age = m.Duration - m.T;
            if (!m.Stood && age >= BurrowerTelegraph)
            {
                m.Stood = true;
                Sounds.Add(Sfx.BigExplode);  // the breach
            }
            if (age < BurrowerSurface || State != MatchState.Fighting)
                return;
            if (m.T <= BurrowerSink)  // its run is over: it sinks where it is
            {
                if (!m.SlamHit)
                {
                    m.SlamHit = true;
                    Sounds.Add(Sfx.Shockwave);
                }
                return;
            }
            var d = m.Def;
            if (m.Burrowed == null)
                m.Burrowed = new HashSet<int>();
            Enemy goal = null;
            float best = BurrowerSeek;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || m.Burrowed.Contains(e.Id))
                    continue;
                float dist = e.Pos.DistanceTo(m.Pos);
                if (dist < best)
                {
                    best = dist;
                    goal = e;
                }
            }
            if (goal != null)
            {
                float da = (goal.Pos - m.Pos).Angle - m.Aim;
                while (da > MathX.Pi) da -= MathX.Tau;
                while (da < -MathX.Pi) da += MathX.Tau;
                m.Aim += Math.Max(-BurrowerTurn * dt, Math.Min(BurrowerTurn * dt, da));
            }
            m.Pos = ClampToArena(m.Pos + V2.FromAngle(m.Aim) * d.Speed * dt, 30f);
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || m.Burrowed.Contains(e.Id) || e.Pos.DistanceTo(m.Pos) >= d.Range + e.Radius)
                    continue;
                m.Burrowed.Add(e.Id);
                HurtEnemy(e, dmg, owner.Id);
                if (m.Capstone)
                    Freeze(e.Pos, 0f, 1f);
                BroadcastFx(FxKind.BurrowSnap, e.Pos, m.Pos, d.Color, e.Radius, 0.7f, Sfx.Hit);
            }
        }
    }
}
