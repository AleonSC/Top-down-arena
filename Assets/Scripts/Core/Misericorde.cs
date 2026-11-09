using System;
using System.Collections.Generic;

namespace Arena
{
    /// The DPS Knight's Misericorde (`AbilityId.Whirlwind` kept; was Whirlwind), the Last Rites look of
    /// Docs/Mockups/knight-dps-abilities.html. Wounded enemies (under MercyBelow of their health, from anyone's damage)
    /// show a small dagger. Press Space: you leap onto the wounded enemy nearest your cursor within MercyReach and drive
    /// the mercy dagger home, executing it whatever health it has left; then you leap on to the next wounded enemy
    /// within MercyReach of where you landed, and the next, until none are left in reach. Untouchable while it runs.
    /// A wounded boss isn't executed: it takes MercyBossStab and the chain ends there. With nobody wounded in reach the
    /// press does nothing and costs nothing (your own game checks first). The leaps are carried out by the player's own
    /// game (Msg.Carry).
    ///   FxKind.MercyLeap: a leap (Pos = where from: you, or the last enemy executed; Pos2 = the enemy leapt at,
    ///     Radius = the knight's id * 100 + the link 0, 1, 2..., Dur = MercyLeapTime + MercyStabAt + its tail).
    ///   FxKind.MercyKill: an execution (Pos = the enemy, Pos2 = (the knight's id, the health it had left),
    ///     Radius = the enemy's size, Color = its colour, Color.A = 0.5 for a boss's stab, Dur = MercyKillFx).
    ///   FxKind.MercyEnd: the chain ends (Pos = where you landed, Pos2.X = the knight's id, Radius = the reach).
    /// Drawn in Game/MisericordeLook.cs.
    public partial class Game
    {
        public const float MercyBelow = 0.25f, MercyReach = 200f;
        public const float MercyLeapTime = 0.2f, MercyStabAt = 0.05f, MercyGap = 0.16f;
        public const float MercyLandShort = 17f;      // you land this far short of the enemy's centre
        public const float MercyBossStab = 120f, MercyHeal = 0.05f;
        public const float MercyKillFx = 3.2f, MercyEndFx = 0.9f;
        const int MercyMaxLinks = 12;

        class MercyRun
        {
            public int Owner, Target, Link;
            public int Phase;     // 0 leaping, 1 the gap before the next
            public bool Ending;   // a boss stabbed, or the most links: no more leaps
            public float T;
            public V2 Landing, KillPos;
            public float Power;
            public bool Mastered;
            public readonly List<int> Done = new List<int>();
        }

        readonly List<MercyRun> mercyRuns = new List<MercyRun>();

        public static float MercyBelowOf(Player p) => MercyBelow + p.Stats.MercyBelow;
        public static float MercyReachOf(Player p) => MercyReach + p.Stats.MercyReach;

        /// Is this enemy wounded enough for the dagger (for drawing the marks too)?
        public static bool MercyWounded(Enemy e, float below) => e.Hp > 0f && e.MaxHp > 0f && e.Hp <= e.MaxHp * below && !e.Spawning && e.SpawnT <= 0f;

        /// The wounded enemy the dagger goes to next: from `from`, within reach; the first one is the nearest to the
        /// cursor, the rest the nearest to where you landed. Null when there's none.
        Enemy MercyPick(V2 from, V2 cursor, float below, float reach, List<int> skip, bool first)
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!MercyWounded(e, below) || skip != null && skip.Contains(e.Id) || IsSpawning(e))
                    continue;
                if (e.Pos.DistanceTo(from) > reach + e.Radius)
                    continue;
                float d = e.Pos.DistanceTo(first ? cursor : from);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// The wounded enemy a press would leap to first (your own game checks before spending the cooldown, and the
        /// look brackets it).
        public Enemy MercyFirst(Player p, V2 cursor) => MercyPick(p.Pos, cursor, MercyBelowOf(p), MercyReachOf(p), null, true);

        /// Host: Misericorde was cast with the cursor at `cursor`.
        void CastMisericorde(Player p, V2 cursor, float power, bool mastered)
        {
            var e = MercyFirst(p, cursor);
            if (e == null)
            {
                BroadcastFx(FxKind.MercyEnd, p.Pos, new V2(p.Id, 0f), new Rgba(0.67f, 0.68f, 0.71f), MercyReachOf(p), MercyEndFx, Sfx.None);
                return;
            }
            mercyRuns.RemoveAll(r => r.Owner == p.Id);
            var run = new MercyRun { Owner = p.Id, Landing = p.Pos, KillPos = p.Pos, Power = power, Mastered = mastered };
            mercyRuns.Add(run);
            MercyLeap(p, run, e, p.Pos);
        }

        void MercyLeap(Player p, MercyRun run, Enemy e, V2 from)
        {
            V2 off = e.Pos - run.Landing;
            V2 dir = off.IsZero ? V2.FromAngle(p.Aim) : off.Normalized();
            V2 land = off.Length > MercyLandShort ? ClampToArena(e.Pos - dir * MercyLandShort, p.Def.BodyR) : run.Landing;
            run.Target = e.Id;
            run.Phase = 0;
            run.T = MercyLeapTime + MercyStabAt;
            p.InvulnT = Math.Max(p.InvulnT, MercyLeapTime + MercyStabAt + MercyGap + 0.15f);
            CarryPlayer(p, land, MercyLeapTime);
            BroadcastFx(FxKind.MercyLeap, from, e.Pos, new Rgba(0.94f, 0.77f, 0.38f), p.Id * 100f + run.Link, MercyLeapTime + MercyStabAt + 0.5f, Sfx.Blink);
            run.Landing = land;
            run.Link++;
        }

        /// Host, every tick: the stabs landing and the leaps on to the next.
        void TickMercy(float dt)
        {
            for (int i = mercyRuns.Count - 1; i >= 0; i--)
            {
                var run = mercyRuns[i];
                Player p;
                if (!Players.TryGetValue(run.Owner, out p) || p.Downed)
                {
                    mercyRuns.RemoveAt(i);
                    continue;
                }
                run.T -= dt;
                if (run.T > 0f)
                    continue;
                if (run.Phase == 0)
                {
                    Enemy e;
                    bool end = false;
                    run.Done.Add(run.Target);
                    if (Enemies.TryGetValue(run.Target, out e) && e.Hp > 0f)
                    {
                        float left = e.Hp;
                        run.KillPos = e.Pos;
                        var col = e.Def.Color;
                        if (e.Def.IsBoss)
                        {
                            HurtEnemy(e, MercyBossStab * p.Stats.DmgMult * run.Power, p.Id);  // a boss isn't executed: a heavy stab, and the chain ends
                            end = true;
                            col = col.WithAlpha(0.5f);
                        }
                        else
                            HurtEnemy(e, e.Hp + e.MaxHp, p.Id);
                        if (run.Mastered)
                            Heal(p, p.MaxHp * MercyHeal, p, "Misericorde");
                        BroadcastFx(FxKind.MercyKill, e.Pos, new V2(p.Id, (float)Math.Round(left)), col, e.Radius, MercyKillFx, Sfx.Clang);
                    }
                    run.Phase = 1;
                    run.T = MercyGap;
                    run.Ending = end || run.Link >= MercyMaxLinks;
                    if (!run.Ending)
                        continue;
                }
                var next = run.Ending ? null : MercyPick(run.Landing, run.Landing, MercyBelowOf(p), MercyReachOf(p), run.Done, false);
                V2 from = run.KillPos;
                if (next == null)
                {
                    mercyRuns.RemoveAt(i);
                    BroadcastFx(FxKind.MercyEnd, run.Landing, new V2(p.Id, 1f), new Rgba(0.67f, 0.68f, 0.71f), MercyReachOf(p), MercyEndFx, Sfx.None);
                    continue;
                }
                MercyLeap(p, run, next, from);
            }
        }
    }
}
