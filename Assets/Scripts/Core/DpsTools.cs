using System;
using System.Collections.Generic;

namespace Arena
{
    /// One player's damage over time, for the DPS meter: a ring of 0.1 s slices (10 s of history) plus running totals.
    public class DpsTrack
    {
        public const float Slice = 0.1f, IdleGap = 2f;
        public const int Slices = 100;
        readonly float[] ring = new float[Slices];
        int head;
        float headT;

        public float Total;        // everything dealt since the last reset
        public float OverTime;     // of Total, what burning, bleeding, poison and vines did
        public float Peak;         // the best 1 s window
        public float CombatTime;   // seconds spent dealing damage (a gap longer than IdleGap stops the clock)
        public float SinceHit = 99f;
        float gap;                 // time since the last hit, kept to be counted as fighting if another hit follows within IdleGap
        public float Biggest;      // the biggest single tick of damage seen (a hit, or one tick of a burn)

        /// Damage dealt this tick, and how much of it was over time.
        public void Add(float dmg, float overTime)
        {
            if (dmg <= 0f)
                return;
            ring[head] += dmg;
            Total += dmg;
            OverTime += Math.Min(dmg, overTime);
            Biggest = Math.Max(Biggest, dmg);
            if (SinceHit < IdleGap)
                CombatTime += gap;  // the lull between two hits counts as fighting; a long one, or time after the last hit, doesn't
            gap = 0f;
            SinceHit = 0f;
        }

        public void Advance(float dt)
        {
            SinceHit += dt;
            if (SinceHit < IdleGap)
                gap += dt;
            headT += dt;
            while (headT >= Slice)
            {
                headT -= Slice;
                head = (head + 1) % Slices;
                ring[head] = 0f;
            }
            Peak = Math.Max(Peak, Window(1f));
        }

        /// Damage a second over the last `seconds` (up to 9 s).
        public float Window(float seconds)
        {
            int n = Math.Max(1, Math.Min(Slices - 1, (int)Math.Round(seconds / Slice)));
            float sum = 0f;
            for (int i = 0; i <= n; i++)
                sum += ring[(head - i + Slices) % Slices];
            return sum / (n * Slice + headT);
        }

        /// Damage a second over the time spent fighting (the lulls between hits count; walking about doesn't).
        public float Average => Total / Math.Max(1f, CombatTime);

        public bool Idle => SinceHit >= IdleGap;
    }

    /// Developer tools for balancing: a DPS meter (F2 in a match) and target dummies. Host only; nothing is sent over
    /// the network, so the meter shows the host's view of every player.
    public partial class Game
    {
        public const int MaxDummies = 4;
        const float LabDummyHp = 1e6f, DummyPatrol = 160f, DummyPatrolSpeed = 1.05f;

        /// Host: each player's damage record, by player id.
        public readonly Dictionary<int, DpsTrack> Dps = new Dictionary<int, DpsTrack>();
        readonly Dictionary<int, float> dpsSeen = new Dictionary<int, float>();  // each player's Run.DamageDealt when last looked at
        readonly Dictionary<int, float> dpsOverTime = new Dictionary<int, float>();  // damage over time dealt since the last look

        /// Host: the dummies strafe side to side instead of standing still.
        public bool DummyMoves;

        void DpsDot(int by, float amount)
        {
            float so;
            dpsOverTime.TryGetValue(by, out so);
            dpsOverTime[by] = so + amount;
        }

        /// Called every host tick: reads what each player's damage tally gained since last tick, so every source counts.
        void TickDps(float dt)
        {
            foreach (var p in Players.Values)
            {
                DpsTrack t;
                if (!Dps.TryGetValue(p.Id, out t))
                    Dps[p.Id] = t = new DpsTrack();
                float seen, dot;
                if (!dpsSeen.TryGetValue(p.Id, out seen))
                    seen = p.Run.DamageDealt;
                dpsOverTime.TryGetValue(p.Id, out dot);
                dpsSeen[p.Id] = p.Run.DamageDealt;
                dpsOverTime[p.Id] = 0f;
                t.Add(Math.Max(0f, p.Run.DamageDealt - seen), dot);
                t.Advance(dt);
            }
        }

        /// Ignore damage credited outside the fight (Kill all): look at the tallies without counting the change.
        void DpsSkip()
        {
            foreach (var p in Players.Values)
                dpsSeen[p.Id] = p.Run.DamageDealt;
        }

        /// Start the meter over: every player's record, and what the dummies have taken.
        public void DevResetDps()
        {
            if (!IsHost)
                return;
            foreach (int id in new List<int>(Dps.Keys))
                Dps[id] = new DpsTrack();
            DpsSkip();
            dpsOverTime.Clear();
            foreach (var e in Enemies.Values)
                if (e.Dummy)
                    e.DummyTaken = 0f;
        }

        // --- Dummies ----------------------------------------------------------------------

        /// Live enemies that count for the wave: everything but the dummies.
        public int Foes()
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (!e.Dummy)
                    n++;
            return n;
        }

        public int DummyCount()
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (e.Dummy)
                    n++;
            return n;
        }

        /// Everything the dummies have taken since the last reset.
        public float DummyDamage()
        {
            float sum = 0f;
            foreach (var e in Enemies.Values)
                if (e.Dummy)
                    sum += e.DummyTaken;
            return sum;
        }

        /// Put a target dummy in front of this player: a Juggernaut that never moves (unless DummyMoves), never shoots,
        /// never hurts, never dies, and counts the damage it takes. It doesn't hold up the wave.
        public Enemy DevSpawnDummy(int id)
        {
            Player p;
            if (!IsHost || State == MatchState.Idle || !Players.TryGetValue(id, out p) || DummyCount() >= MaxDummies)
                return null;
            int n = DummyCount();
            V2 fwd = V2.FromAngle(p.Aim);
            V2 side = new V2(-fwd.Y, fwd.X);
            float shift = ((n + 1) / 2) * 90f * (n % 2 == 1 ? 1f : -1f);
            var e = SpawnEnemyAt(EnemyType.Juggernaut, p.Pos + fwd * 260f + side * shift, 0f, 0);
            e.Dummy = true;
            e.Elite = false;
            e.MaxHp = e.Hp = LabDummyHp;
            e.ShieldHp = e.BubbleHp = 0f;
            e.DummyHome = e.Pos;
            DevUsed(p.Name + ": target dummy");
            return e;
        }

        public void DevClearDummies()
        {
            if (!IsHost)
                return;
            var gone = new List<int>();
            foreach (var e in Enemies.Values)
                if (e.Dummy)
                    gone.Add(e.Id);
            foreach (int eid in gone)
                Enemies.Remove(eid);
            if (gone.Count > 0)
                DevUsed("target dummies removed");
        }

        /// A dummy's turn: heal back to full (counting what it took), stay put or patrol, and shrug off pushes.
        void TickDummy(Enemy e, float dt)
        {
            e.DummyTaken += Math.Max(0f, e.MaxHp - e.Hp);
            e.Hp = e.MaxHp;
            e.ShieldHp = e.BubbleHp = 0f;
            e.KnockLeft = V2.Zero;
            e.FrozenT = e.GraspT = e.CageT = 0f;  // nothing pins or cages it: the meter should see what the weapon does
            if (DummyMoves)
                e.Pos = ClampToArena(new V2(e.DummyHome.X + (float)Math.Sin(e.T * DummyPatrolSpeed) * DummyPatrol, e.DummyHome.Y), e.Radius);
            else
                e.Pos = e.DummyHome;
        }
    }
}
