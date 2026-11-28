using System;
using System.Collections.Generic;

namespace Arena
{
    /// One player's progress through the drill running (Training, Docs/Training.md): the step they're on, their clock, and the
    /// target dummy standing on a shoot step's beacon (only they can hurt it).
    public sealed class DrillRunner
    {
        public int PlayerId;
        public int Step;       // steps done (the drill's Steps: finished)
        public float Time;     // seconds since the drill started, stopped at the finish
        public bool Done;
        public int TargetId;   // the dummy on a shoot step's beacon, or the Shoot drill's target (0: none up)
        public float TargetT;  // host: seconds since the target came up (the Shoot drill's slide)
        public float Flash;    // seconds since the last step was done (the ring's pop)
        public float Wall = float.NaN;  // Dodge: the wall's x (NaN: none up)
        public bool TellUp;    // Read the Tells: a wind-up is showing
        public byte TellKind;  // its kind (Training.TellLine, TellLane, TellCircle)
        public V2 TellFrom, TellAt;  // where it comes from and the spot it's aimed at
        public float TellT;    // seconds into its wind-up
        public float FiredT = 9f;  // seconds since the last one fired (its flash)
        public V2 FiredFrom, FiredAt;  // ...and where it was
        public byte FiredKind;
        public bool ShotUp;    // Parry: a shot is on its way
        public V2 Shot;        // Parry: where it is
        public int Hits;       // Dodge: walls that caught you
        public float HitT = 9f;  // Dodge: seconds since the last hit (the red flash)
        public float SinceDodge = 9f;  // host: seconds since this player last dodged
        public float LastX = float.NaN;  // host: the player's x last tick (a dodge can cross the wall in one)
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
            {
                MakeRecruit(p);
                DrillRunners[p.Id] = new DrillRunner { PlayerId = p.Id };
            }
        }

        /// Every player in a drill is a plain recruit, whatever their loadout: the DPS build with no class look, no abilities,
        /// ultimate or perks, and the Training Pistol. Done on every machine as the Start is read, so nothing new goes over the wire.
        void MakeRecruit(Player p)
        {
            p.Recruit = true;
            p.Role = Role.Dps;
            p.Def = Defs.Get(Role.Dps);
            p.Kit = Kit.Soldier;
            p.Weapon = Training.Pistol;
            p.FirstAbility = AbilityId.None;
            p.WildcardAbility = AbilityId.None;
            for (int k = 0; k < p.CardAbilities.Length; k++)
                p.CardAbilities[k] = AbilityId.None;
            p.Perks = Perks.Empty;
            p.BaseHp = p.Def.Hp;
            Recalc(p);
            p.Hp = p.MaxHp;
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
            DrillClocks(dt);
            if (IsHost)
                foreach (var d in DrillRunners.Values)
                    switch (Training.Drills[Drill].Key)
                    {
                        case "move": TickMoveDrill(d); break;
                        case "dodge": TickDodgeDrill(d, dt); break;
                        case "wallkick": TickKickDrill(d); break;
                        case "shoot": TickShootDrill(d); break;
                        case "parry": TickParryDrill(d, dt); break;
                        case "tells": TickTellsDrill(d); break;
                    }
            Remaining = 0;
            DrillRecordMine();
        }

        /// Every runner's clocks (the host's tick, and a client's view between snapshots).
        void DrillClocks(float dt)
        {
            foreach (var d in DrillRunners.Values)
            {
                d.Flash += dt;
                d.TargetT += dt;
                d.TellT += dt;
                d.FiredT += dt;
                d.HitT += dt;
                d.SinceDodge += dt;
                if (!d.Done)
                    d.Time += dt;
            }
        }

        /// A client, every frame: the clocks and the Dodge walls run on between snapshots, and its own finish is kept.
        void UpdateDrillView(float dt)
        {
            if (IsHost || !DrillOn)
                return;
            DrillClocks(dt);
            if (Training.Drills[Drill].Key == "dodge")
                foreach (var d in DrillRunners.Values)
                    if (!float.IsNaN(d.Wall))
                        d.Wall += Training.WallDir(d.Step) * Training.WallSpeedAt(d.Step) * dt;
            if (Training.Drills[Drill].Key == "parry")
                foreach (var d in DrillRunners.Values)
                    if (d.ShotUp && Players.TryGetValue(d.PlayerId, out var p))
                        MoveDrillShot(d, p.Pos, dt);
            DrillRecordMine();
        }

        /// This machine's player finished: their best time goes into this machine's profile (once).
        void DrillRecordMine()
        {
            var mine = MyDrill;
            if (drillRecorded || mine == null || !mine.Done || Profile == null)
                return;
            drillRecorded = true;
            Profile.DrillDone(Training.Drills[Drill].Key, mine.Time);
            Profile.Save();
        }

        /// Host: Move and Aim (`Training.MoveSteps` beacons). A touch step is reached by standing on its beacon; a shoot step by bringing down your own dummy on it.
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

        /// Host: Dodge. After a short gap the next wall sets off from its side; when it crosses you, a dodge in the last moment
        /// takes you through it, anything else is a hit and the wall starts again.
        void TickDodgeDrill(DrillRunner d, float dt)
        {
            if (d.Done || !Players.TryGetValue(d.PlayerId, out var p))
                return;
            int dir = Training.WallDir(d.Step);
            if (float.IsNaN(d.Wall))
            {
                if (d.Flash >= Training.WallGap && d.HitT >= Training.WallGap)
                    d.Wall = dir > 0 ? 0f : Width;
                return;
            }
            float was = d.Wall, lastX = float.IsNaN(d.LastX) ? p.Pos.X : d.LastX;
            d.Wall += dir * Training.WallSpeedAt(d.Step) * dt;
            d.LastX = p.Pos.X;
            float a = (lastX - was) * dir, b = (p.Pos.X - d.Wall) * dir;  // > 0: you're ahead of the wall
            if (a >= 0f && b < 0f && a < 150f && b > -150f)  // the wall and you passed each other this tick (not a wrap round the edge)
            {
                d.Wall = float.NaN;
                if (d.SinceDodge <= DodgeInvuln + Training.WallSlack)
                    DrillStepDone(d);
                else
                {
                    d.Hits++;
                    d.HitT = 0f;
                }
                return;
            }
            if (d.Wall < -40f || d.Wall > Width + 40f)
                d.Wall = float.NaN;  // off the far side without crossing you (you wrapped round): it comes again
        }

        /// Host: Parry. After a short gap the next shot sets off from its side and homes on you; on reaching you it's blocked if
        /// your shield is up and facing it, else it's a hit and that shot comes again.
        void TickParryDrill(DrillRunner d, float dt)
        {
            if (d.Done || !Players.TryGetValue(d.PlayerId, out var p))
                return;
            if (!d.ShotUp)
            {
                if (d.Flash >= Training.ParryGap && d.HitT >= Training.ParryGap)
                {
                    d.Shot = Training.ParryStart(d.Step, p.Pos);
                    d.ShotUp = true;
                }
                return;
            }
            MoveDrillShot(d, p.Pos, dt);
            if (!d.Shot.CloserThan(p.Pos, Training.ParryHitR))
                return;
            d.ShotUp = false;
            if (p.ParryActive && Facing(p, d.Shot - p.Pos))
                DrillStepDone(d);
            else
            {
                d.Hits++;
                d.HitT = 0f;
            }
        }

        /// Host: Read the Tells. After a short gap the next wind-up shows, aimed where you stand; when it ends it fires: caught is a
        /// hit (and it comes again), clear and it's slipped.
        void TickTellsDrill(DrillRunner d)
        {
            if (d.Done || !Players.TryGetValue(d.PlayerId, out var p))
                return;
            if (!d.TellUp)
            {
                if (d.Flash >= Training.TellGap && d.HitT >= Training.TellGap)
                {
                    d.TellKind = Training.TellKind(d.Step);
                    d.TellAt = p.Pos;
                    d.TellFrom = Training.TellStart(d.Step, p.Pos);
                    d.TellT = 0f;
                    d.TellUp = true;
                }
                return;
            }
            if (d.TellT < Training.TellWindup(d.Step))
                return;
            d.TellUp = false;
            d.FiredT = 0f;
            d.FiredFrom = d.TellFrom;
            d.FiredAt = d.TellAt;
            d.FiredKind = d.TellKind;
            if (Training.TellCatches(d.TellKind, d.TellFrom, d.TellAt, p.Pos))
            {
                d.Hits++;
                d.HitT = 0f;
            }
            else
                DrillStepDone(d);
        }

        /// A Parry shot's step toward `to` (the host's tick, and a client's view between snapshots).
        void MoveDrillShot(DrillRunner d, V2 to, float dt)
        {
            V2 way = to - d.Shot;
            float len = way.Length, step = Training.ParrySpeedAt(d.Step) * dt;
            d.Shot = len <= step ? to : d.Shot + way * (step / len);
        }

        /// Host: Shoot. One target up at a time (your own dummy), sliding along its lane once the first few are down.
        void TickShootDrill(DrillRunner d)
        {
            if (d.Done)
                return;
            if (d.TargetId == 0)
            {
                if (d.Flash < Training.ShootGap)
                    return;
                var e = SpawnEnemyAt(EnemyType.Grunt, Training.ShootSpot(d.Step, Width, Height), 0.2f, 0);
                e.MaxHp = e.Hp = Training.ShootHp;
                d.TargetId = e.Id;
                d.TargetT = 0f;
                return;
            }
            if (!Enemies.TryGetValue(d.TargetId, out var t))
            {
                d.TargetId = 0;
                DrillStepDone(d);
                return;
            }
            t.Pos = Training.ShootAt(d.Step, d.TargetT, Width, Height);
        }

        /// Host: Wall Kick. The kicks come in from each player's own game (DrillKicked); the last step is standing on the flag.
        void TickKickDrill(DrillRunner d)
        {
            if (d.Done || d.Step < Training.KickWalls || !Players.TryGetValue(d.PlayerId, out var p))
                return;
            if (p.Pos.CloserThan(Training.KickFlag(Width, Height), Training.FlagReach))
                DrillStepDone(d);
        }

        /// Host: a player wall-kicked (their own game saw it: Dodge.cs, Msg.DrillKick). Off the lit panel, that step is done.
        public void DrillKicked(int id)
        {
            if (!IsHost || !DrillOn || Training.Drills[Drill].Key != "wallkick" || !DrillRunners.TryGetValue(id, out var d) || d.Done
                || d.Step >= Training.KickWalls || !Players.TryGetValue(id, out var p))
                return;
            if (Training.BoxDistance(Training.KickPanel(d.Step, Width, Height), p.Pos) <= Training.KickNear)
                DrillStepDone(d);
        }

        /// Your own game: you wall-kicked; the host counts it for the Wall Kick drill.
        void DrillNoteKick()
        {
            if (!DrillOn || Training.Drills[Drill].Key != "wallkick")
                return;
            if (IsHost)
                DrillKicked(MyId);
            else
                session.SendToHost(Proto.End(Proto.Begin(Msg.DrillKick)));
        }

        /// Host: a player dodged (Dodge.cs), for the Dodge drill.
        void DrillDodged(int id)
        {
            if (DrillOn && DrillRunners.TryGetValue(id, out var d))
                d.SinceDodge = 0f;
        }

        void DrillStepDone(DrillRunner d)
        {
            d.Step++;
            d.Flash = 0f;
            if (d.Step >= Training.Drills[Drill].Steps)
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

        /// Read the Tells' part of a runner in the snapshot: the wind-up showing and the last one fired.
        static void SyncTell(NetSync s, DrillRunner d)
        {
            s.Bool(ref d.TellUp);
            s.Byte(ref d.TellKind);
            s.Vec(ref d.TellFrom);
            s.Vec(ref d.TellAt);
            s.Float(ref d.TellT);
            s.Float(ref d.FiredT);
            s.Vec(ref d.FiredFrom);
            s.Vec(ref d.FiredAt);
            s.Byte(ref d.FiredKind);
        }

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
                    s.Float(ref d.Wall);
                    int hits = d.Hits;
                    s.Byte(ref hits);
                    if (hits != d.Hits)
                        d.HitT = 0f;
                    d.Hits = hits;
                    s.Bool(ref d.ShotUp);
                    s.Vec(ref d.Shot);
                    SyncTell(s, d);
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
                s.Float(ref d.Wall);
                int hits = Math.Min(255, d.Hits);
                s.Byte(ref hits);
                s.Bool(ref d.ShotUp);
                s.Vec(ref d.Shot);
                SyncTell(s, d);
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
            try { Game.HostStart(); }
            finally { Game.DrillStart = -1; }  // (never left on: the next START would open the drill)
        }
    }
}
