using System;
using System.Collections.Generic;

namespace Arena
{
    /// One player's progress through the drill running (Training, Docs/Training.md): the step they're on, their clock, and the
    /// target dummy standing on a shoot step's beacon (only they can hurt it).
    public sealed class DrillRunner
    {
        public int PlayerId;
        public int Step;       // beacons done (Training.MoveSteps: finished)
        public float Time;     // seconds since the drill started, stopped at the finish
        public bool Done;
        public int TargetId;   // the dummy on a shoot step's beacon (0: none up)
        public float Flash;    // seconds since the last beacon was reached (the ring's pop)
    }

    /// Training's drill run (`GameMode.Training`): the real match on the classic arena with no waves, held in a fight with no
    /// enemies but each player's own beacons. The host starts it from the Training page (`Session.StartDrill`, the drill's index
    /// on `Msg.Start`); every machine runs the same clock, the host decides when a beacon is reached and the snapshot carries each
    /// player's step (`SyncDrill`). Each machine keeps its own player's best time when they finish (`Profile.DrillDone`). Nobody
    /// takes damage, levels up or waits on a cooldown; nothing is saved or recorded. Retry restarts it, Back ends it to the lobby.
    public partial class Game
    {
        /// The drill this run is (an index into Training.Drills; -1: not a drill).
        public int Drill = -1;
        public bool DrillOn => Drill >= 0;

        /// Host: the `Msg.Start` about to go out is this drill (set by Session.StartDrill around HostStart; -1: none).
        public int DrillStart = -1;

        /// Each player's run through the drill, by player id.
        public readonly Dictionary<int, DrillRunner> DrillRunners = new Dictionary<int, DrillRunner>();

        bool drillRecorded;  // this machine's player's finish has gone into its profile

        /// Host and clients, as the Start is read.
        void DrillRead(int drill)
        {
            Drill = drill >= 0 && drill < Training.Drills.Length ? drill : -1;
            DrillRunners.Clear();
            drillRecorded = false;
            if (!DrillOn)
                return;
            Hunt = null;
            HuntMapOn = false;
            foreach (var p in Players.Values)
                DrillRunners[p.Id] = new DrillRunner { PlayerId = p.Id };
        }

        /// This machine's player's runner (null: not in a drill).
        public DrillRunner MyDrill => DrillOn && DrillRunners.TryGetValue(MyId, out var d) ? d : null;

        /// Instead of waves: a fight with no enemies but the shoot steps' dummies, every clock running until its player is done.
        void TickDrill(float dt)
        {
            State = MatchState.Fighting;
            StateTimer = 0f;
            HoldControls = false;
            Orbs.Clear();
            foreach (var p in Players.Values)
            {
                p.Offer = null;
                p.PendingPicks = 0;
                p.InvulnT = Math.Max(p.InvulnT, 0.5f);  // (nothing in a drill hurts)
                p.Hp = p.MaxHp;
            }
            foreach (var d in DrillRunners.Values)
            {
                d.Flash += dt;
                if (!d.Done)
                    d.Time += dt;
            }
            if (IsHost)
                foreach (var d in DrillRunners.Values)
                    TickMoveDrill(d);
            Remaining = 0;
            if (!drillRecorded && MyDrill != null && MyDrill.Done && Profile != null)
            {
                drillRecorded = true;
                Profile.DrillDone(Training.Drills[Drill].Key, MyDrill.Time);
                Profile.Save();
            }
        }

        /// Host: Move and Aim. A touch step is reached by standing on its beacon; a shoot step by bringing down your own dummy on it.
        void TickMoveDrill(DrillRunner d)
        {
            if (d.Done || !Players.TryGetValue(d.PlayerId, out var p))
                return;
            var spot = Training.MoveBeacon(d.Step, Width, Height);
            if (!Training.MoveShootStep(d.Step))
            {
                if (p.Pos.CloserThan(spot, Training.BeaconReach))
                    DrillStepDone(d);
                return;
            }
            if (d.TargetId == 0)
            {
                var e = SpawnEnemyAt(EnemyType.Grunt, spot, 0.3f, 0);
                e.MaxHp = e.Hp = Training.TargetHp;
                d.TargetId = e.Id;
                return;
            }
            if (!Enemies.ContainsKey(d.TargetId))
            {
                d.TargetId = 0;
                DrillStepDone(d);
            }
        }

        void DrillStepDone(DrillRunner d)
        {
            d.Step++;
            d.Flash = 0f;
            if (d.Step >= Training.MoveSteps)
                d.Done = true;
        }

        /// A drill's dummy that belongs to another player: their hits don't count (HurtEnemy), and it isn't drawn for anyone else.
        public bool DrillTargetOf(Enemy e, out int owner)
        {
            owner = 0;
            if (!DrillOn)
                return false;
            foreach (var d in DrillRunners.Values)
                if (d.TargetId == e.Id)
                {
                    owner = d.PlayerId;
                    return true;
                }
            return false;
        }

        /// A drill's dummy hit by someone who isn't its player: no damage.
        bool DrillShieldsHit(Enemy e, int attacker) => DrillTargetOf(e, out int owner) && owner != attacker;

        /// Every player in the drill is done.
        public bool DrillAllDone
        {
            get
            {
                if (!DrillOn || DrillRunners.Count == 0)
                    return false;
                foreach (var d in DrillRunners.Values)
                    if (!d.Done)
                        return false;
                return true;
            }
        }

        /// The snapshot's part: each runner's step, clock, finish and dummy.
        void SyncDrill(NetSync s)
        {
            int n = DrillRunners.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                for (int i = 0; i < n; i++)
                {
                    int id = 0;
                    s.Int(ref id);
                    if (!DrillRunners.TryGetValue(id, out var d))
                        DrillRunners[id] = d = new DrillRunner { PlayerId = id };
                    int step = d.Step;
                    s.Byte(ref step);
                    if (step != d.Step)
                        d.Flash = 0f;
                    d.Step = step;
                    s.Float(ref d.Time);
                    s.Bool(ref d.Done);
                    s.Int(ref d.TargetId);
                }
                return;
            }
            foreach (var d in DrillRunners.Values)
            {
                int id = d.PlayerId, step = d.Step;
                s.Int(ref id);
                s.Byte(ref step);
                s.Float(ref d.Time);
                s.Bool(ref d.Done);
                s.Int(ref d.TargetId);
            }
        }
    }

    public partial class Session
    {
        /// Host: start drill i of Training for the whole party (from the Training page's START DRILL, or Retry in the drill).
        public void StartDrill(int i)
        {
            if (!IsHost || !TrainingMode || i < 0 || i >= Training.Drills.Length || !Training.Drills[i].Built)
                return;
            if (Game.InRun && !Game.DrillOn)
                return;
            Game.SettleRetry();
            ApplyMatchSetup();
            Game.DrillStart = i;
            Game.HostStart();
            Game.DrillStart = -1;
        }
    }
}
