using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Lockdown Drill (Docs/RunLength.md step 3): the Warden's teaching cycle's optional mid-boss. In a Campaign, the
    /// break after the cycle's fifth wave opens a Data Gate to it (instead of the stretch's random one). Inside, the
    /// Warden's Lockdown gates (Warden.cs: two rows of push bullets with two doors and a gold lock) sweep the room from the
    /// top, then the bottom, while Guards and Rioters come at you: break DrillLocks gold locks before the clock runs out.
    /// Win and the altar gives everyone a prize pick, like any Data Gate. It teaches the one move of the Warden's fight
    /// that no enemy previews. The gates ride in the room's snapshot (SyncRoom).
    public partial class Game
    {
        public const float DrillTime = 45f, DrillGateEvery = 3.2f, DrillFirstGate = 0.6f;
        public const int DrillLocks = 3, DrillGatesASide = 3;

        /// The drill's gates (the Warden's own are in its WardenState).
        public readonly List<WardenGate> DrillGates = new List<WardenGate>();
        float drillGateT;
        int drillSent;

        /// Every gate on the field: the Warden's and the drill's.
        IEnumerable<WardenGate> AllGates()
        {
            var e = TheWarden();
            if (e != null && e.Warden != null)
                foreach (var g in e.Warden.Gates)
                    yield return g;
            foreach (var g in DrillGates)
                yield return g;
        }

        /// The drill runs in this room.
        public bool DrillOn => InRoom && RoomKind == ChallengeKind.Lockdown;

        /// Host, as a wave is cleared: in a Campaign, the break after the fifth wave of a cycle whose boss has a
        /// drill (the Warden) opens its Data Gate. True if it opened (the stretch's random gate is then skipped).
        bool MaybeOpenDrill()
        {
            if (!IsHost || !LongLegs || Room != RoomPhase.None || ShowcaseMode || ApproachBoss(Wave) != EnemyType.Boss)
                return false;
            if (ShownWave % 10 != 5)
                return false;
            OpenPortal(ChallengeKind.Lockdown);
            return true;
        }

        /// A Campaign stretch that has the drill gets no random Data Gate as well.
        bool DrillStretch => LongLegs && ApproachBoss(Wave) == EnemyType.Boss;

        /// Host, entering the room: no gates yet, the first soon.
        void StartDrill()
        {
            DrillGates.Clear();
            drillGateT = DrillFirstGate;
            drillSent = 0;
            RoomGoal = DrillLocks;
            RoomKills = 0;  // (the locks broken)
        }

        /// Host, each tick of the drill: gates on a timer (three from the top, three from the bottom, ...), Guards and
        /// Rioters to keep you busy, and the win (enough locks) or the loss (out of time).
        void TickDrill(float dt)
        {
            drillGateT -= dt;
            if (drillGateT <= 0f)
            {
                float dir = (drillSent / DrillGatesASide) % 2 == 0 ? 1f : -1f;  // top first, then the bottom, and so on
                AddGate(DrillGates, dir);
                drillSent++;
                drillGateT = DrillGateEvery;
                BroadcastSound(drillSent == 1 ? Sfx.BossSiren : Sfx.BossLaserWarn);
            }
            TickGates(DrillGates, dt, "Lockdown Drill (gate)");
            RoomSpawner(dt, 1.6f, 4 + 2 * Players.Count);
            if (RoomKills >= RoomGoal)
                WinRoom();
            else if (RoomClock <= 0f)
                LoseRoom("out of time (" + RoomKills + " of " + RoomGoal + " locks)");
        }

        /// Host: a drill gate's lock was shot out.
        void DrillLockBroken()
        {
            if (Room == RoomPhase.Active && RoomKind == ChallengeKind.Lockdown)
                RoomKills++;
        }

        /// Every game, each frame: clients move the drill's gates between snapshots.
        void UpdateDrillView(float dt)
        {
            if (!IsHost && DrillGates.Count > 0)
                AdvanceGates(DrillGates, dt);
        }
    }
}
