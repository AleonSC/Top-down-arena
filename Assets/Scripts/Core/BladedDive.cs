using System;
using System.Collections.Generic;

namespace Arena
{
    /// The DPS Knight's Bladed Dive (was Misericorde, and Whirlwind before it). Press the 1st ability:
    /// you rush toward your cursor (up to BladedRange, over BladedTime), untouchable, spinning your blade round you: BladedSlashes
    /// cuts land along the way, each hitting every enemy within BladedSlashR of you for BladedDamage. Kill anything with it
    /// and the cooldown resets. The rush is carried out by the player's own game (Msg.Carry).
    ///   FxKind.DiveRush: the rush (Pos = where from, Pos2 = where to, Radius = the knight's id, Dur = BladedTime + its tail).
    ///   FxKind.DiveHit: an enemy cut (Pos = the enemy, Pos2 = the cut's direction, Radius = the enemy's size,
    ///     Color = its colour, Dur = BladedHitFx).
    ///   FxKind.DiveReset: a kill reset the cooldown (Pos = the enemy killed, Pos2.X = the knight's id, Dur = BladedResetFx).
    /// Drawn in Game/KnightDpsLook.cs.
    public partial class Game
    {
        public const float BladedRange = 240f, BladedTime = 0.32f, BladedSlashR = 64f, BladedDamage = 18f;
        public const int BladedSlashes = 4;
        public const float BladedHeal = 0.05f;
        public const float BladedHitFx = 0.35f, BladedResetFx = 0.9f, BladedTail = 0.45f;

        class BladedRun
        {
            public int Owner, Slashes;
            public float Elapsed, Dur, Power;
            public V2 From, To;
            public bool Mastered, Reset;
        }

        readonly List<BladedRun> bladedRuns = new List<BladedRun>();

        public static float BladedRangeOf(Player p) => BladedRange + p.Stats.BladedReach;

        /// The cut `k` (0 .. BladedSlashes - 1) lands this many seconds into the rush (the first straight away).
        public static float BladedSlashAt(int k) => BladedTime * k / (BladedSlashes - 1);

        /// Host: Bladed Dive was cast with the cursor at `cursor`.
        void CastBladedDive(Player p, V2 cursor, float power, bool mastered)
        {
            V2 off = cursor - p.Pos;
            V2 dir = off.Length < 1f ? V2.FromAngle(p.Aim) : off.Normalized();
            float len = Math.Min(BladedRangeOf(p), Math.Max(off.Length, BladedRangeOf(p) * 0.5f));  // a short click still bladedRuns half the way
            V2 to = ClampToArena(p.Pos + dir * len, p.Def.BodyR);
            bladedRuns.RemoveAll(r => r.Owner == p.Id);
            bladedRuns.Add(new BladedRun { Owner = p.Id, From = p.Pos, To = to, Dur = BladedTime, Power = power, Mastered = mastered });
            p.InvulnT = Math.Max(p.InvulnT, BladedTime + 0.3f);
            CarryPlayer(p, to, BladedTime);
            BroadcastFx(FxKind.DiveRush, p.Pos, to, new Rgba(0.94f, 0.77f, 0.38f), p.Id, BladedTime + BladedTail, Sfx.Blink);
        }

        /// Host, every tick: the cuts landing along the rush, and the reset on a kill.
        void TickBladed(float dt)
        {
            for (int i = bladedRuns.Count - 1; i >= 0; i--)
            {
                var run = bladedRuns[i];
                Player p;
                if (!Players.TryGetValue(run.Owner, out p) || p.Downed)
                {
                    bladedRuns.RemoveAt(i);
                    continue;
                }
                run.Elapsed += dt;
                while (run.Slashes < BladedSlashes && run.Elapsed + 1e-4f >= BladedSlashAt(run.Slashes))
                {
                    float u = Math.Min(1f, BladedSlashAt(run.Slashes) / run.Dur);
                    V2 at = run.From + (run.To - run.From) * u;
                    BladedSlash(p, run, at);
                    run.Slashes++;
                }
                if (run.Slashes >= BladedSlashes)
                    bladedRuns.RemoveAt(i);
            }
        }

        void BladedSlash(Player p, BladedRun run, V2 at)
        {
            float dmg = BladedDamage * p.Stats.DmgMult * run.Power * (1f + p.Stats.BladedDamage);
            bool any = false;
            var hit = new List<Enemy>();
            foreach (var e in FoesInCircle(at, BladedSlashR, by: p.Id))
                hit.Add(e);
            foreach (var e in hit)
            {
                V2 dir = AwayFrom(e.Pos, at, V2.FromAngle(p.Aim));
                HurtEnemy(e, dmg, p.Id);
                any = true;
                BroadcastFx(FxKind.DiveHit, e.Pos, dir, e.Def.Color, e.Radius, BladedHitFx, Sfx.None);
                if (e.Hp > 0f)
                    continue;
                if (run.Mastered)
                    Heal(p, p.MaxHp * BladedHeal, p, "Bladed Dive");
                if (!run.Reset)
                {
                    run.Reset = true;  // one kill is enough: the cooldown comes back
                    CutCooldown(p, AbilityId.BladedDive, 1f);  // ReaperLoot.cs: that player's own game takes it off too
                    BroadcastFx(FxKind.DiveReset, e.Pos, new V2(p.Id, 0f), new Rgba(1f, 0.84f, 0.55f), e.Radius, BladedResetFx, Sfx.Clang);
                }
            }
            Sounds.Add(any ? Sfx.Hit : Sfx.SwingHeavy);
        }
    }
}
