using System;
using System.Collections.Generic;

namespace Arena
{
    /// How a boss goes into a stage: a Timed change (it can't be hurt while it changes: `StartXTransition`), Instant
    /// (`EnterXStage` straight away, as the last stands do) or a Stagger you break (the Colossus: `StartColStagger`).
    public enum StageEntry : byte { Timed, Instant, Stagger }

    /// One row of a boss's stage table (Docs/DifficultyTiers.md step 2): the stage (its `XStage` value), the health share it
    /// starts at (the first row: 1) and how it comes in. Min and Max are the mechanics tiers it's in (Easy..Nightmare = always):
    /// a row only Hard has adds a stage on Hard; a row from Normal leaves it out on Easy. Two rows for one stage with different
    /// tiers move its threshold on some tiers.
    public struct BossStage
    {
        public byte Stage;
        public float At;
        public StageEntry How;
        public MechTier Min, Max;

        public BossStage(byte stage, float at, StageEntry how = StageEntry.Timed, MechTier min = MechTier.Easy, MechTier max = MechTier.Nightmare)
        {
            Stage = stage;
            At = at;
            How = how;
            Min = min;
            Max = max;
        }
    }

    public partial class Game
    {
        /// Every boss's stages, in the order it goes through them, on every tier (one row per stage per tier range). Normal is the
        /// fight as balanced; Easy has no last stand (the owner's pick: lower tiers can take mechanics away). New stages go at the
        /// end of the boss's `XStage` enum (the value rides the snapshot), with their row here.
        static readonly Dictionary<EnemyType, BossStage[]> StageRows = new Dictionary<EnemyType, BossStage[]>
        {
            { EnemyType.Warden, new[] {
                new BossStage((byte)WardenStage.Count, 1f), new BossStage((byte)WardenStage.Lockdown, LockdownAt), new BossStage((byte)WardenStage.Riot, RiotAt),
                new BossStage((byte)WardenStage.LastStand, LastStandAt, StageEntry.Timed, MechTier.Normal) } },
            { EnemyType.HiveQueen, new[] {
                new BossStage((byte)QueenStage.Swarm, 1f), new BossStage((byte)QueenStage.Honeycomb, HoneycombAt), new BossStage((byte)QueenStage.Frenzy, FrenzyAt),
                new BossStage((byte)QueenStage.LastStand, QueenLastStandAt, StageEntry.Instant, MechTier.Normal) } },
            { EnemyType.ReaperOfDusk, new[] {  // (both Reapers share one fight: its health is both bodies', or the Dual Reaper's)
                new BossStage((byte)ReaperStage.Twilight, 1f), new BossStage((byte)ReaperStage.Dual, FuseAt), new BossStage((byte)ReaperStage.Sundered, SplitAt) } },
            { EnemyType.Singularity, new[] {
                new BossStage((byte)SingStage.EventHorizon, 1f), new BossStage((byte)SingStage.Accretion, SingLockdownAt), new BossStage((byte)SingStage.Spaghettify, SingSpaghettiAt),
                new BossStage((byte)SingStage.LastStand, SingLastAt, StageEntry.Instant, MechTier.Normal) } },
            { EnemyType.Colossus, new[] {
                new BossStage((byte)ColStage.Awakening, 1f), new BossStage((byte)ColStage.Crumbling, ColCrumbleAt, StageEntry.Stagger),
                new BossStage((byte)ColStage.MoltenCore, ColMoltenAt, StageEntry.Stagger), new BossStage((byte)ColStage.LastStand, ColLastAt, StageEntry.Stagger, MechTier.Normal) } },
            { EnemyType.Duelist, new[] {
                new BossStage((byte)DuelStage.EnGarde, 1f), new BossStage((byte)DuelStage.Masquerade, DuelMasqueradeAt), new BossStage((byte)DuelStage.ToTheDeath, DuelDeathAt),
                new BossStage((byte)DuelStage.LastStand, DuelLastAt, StageEntry.Instant, MechTier.Normal) } },
            { EnemyType.Lich, new[] {
                new BossStage((byte)LichStage.Crypt, 1f), new BossStage((byte)LichStage.Harvest, LichHarvestAt), new BossStage((byte)LichStage.Undeath, LichUndeathAt),
                new BossStage((byte)LichStage.LastStand, LichLastAt, StageEntry.Instant, MechTier.Normal) } },
            { EnemyType.StormHerald, new[] {
                new BossStage((byte)HeraldStage.Gathering, 1f), new BossStage((byte)HeraldStage.Gale, HeraldGaleAt), new BossStage((byte)HeraldStage.Supercell, HeraldSupercellAt),
                new BossStage((byte)HeraldStage.LastStand, HeraldLastAt, StageEntry.Instant, MechTier.Normal) } },
            { EnemyType.Devourer, new[] {
                new BossStage((byte)DevourStage.Appetite, 1f), new BossStage((byte)DevourStage.Gorging, DevourGorgeAt), new BossStage((byte)DevourStage.Ravenous, DevourRavenAt),
                new BossStage((byte)DevourStage.LastStand, DevourLastAt, StageEntry.Instant, MechTier.Normal) } },
            { EnemyType.Eclipse, new[] {
                new BossStage((byte)EclipseStage.Corona, 1f), new BossStage((byte)EclipseStage.Penumbra, EclipsePenumbraAt), new BossStage((byte)EclipseStage.Umbra, EclipseUmbraAt),
                new BossStage((byte)EclipseStage.Echoes, EclipseEchoesAt), new BossStage((byte)EclipseStage.LastLight, EclipseLastLightAt),
                new BossStage((byte)EclipseStage.Totality, EclipseTotalityAt, StageEntry.Instant, MechTier.Normal) } },
        };

        static readonly Dictionary<int, BossStage[]> stageCache = new Dictionary<int, BossStage[]>();
        static readonly BossStage[] NoStages = new BossStage[0];

        /// A boss's stages on a tier, in order (the rows whose tiers include it). Empty for anything without a table.
        public static BossStage[] StagesFor(EnemyType boss, MechTier tier)
        {
            if (boss == EnemyType.ReaperOfDawn)
                boss = EnemyType.ReaperOfDusk;
            int key = (int)boss * 4 + (int)tier;
            BossStage[] list;
            lock (stageCache)
            {
                if (stageCache.TryGetValue(key, out list))
                    return list;
                BossStage[] rows;
                if (!StageRows.TryGetValue(boss, out rows))
                    list = NoStages;
                else
                {
                    var l = new List<BossStage>();
                    foreach (var r in rows)
                        if (tier >= r.Min && tier <= r.Max)
                            l.Add(r);
                    list = l.ToArray();
                }
                stageCache[key] = list;
            }
            return list;
        }

        /// This run's stages for a boss (its mechanics tier).
        public BossStage[] StagesOf(EnemyType boss) => StagesFor(boss, MechTier);

        /// Where a stage sits in a list (-1: not on this tier).
        public static int StageIndex(BossStage[] list, byte stage)
        {
            for (int i = 0; i < list.Length; i++)
                if (list[i].Stage == stage)
                    return i;
            return -1;
        }

        /// The health share a stage starts at on a tier (-1: that tier has no such stage). For tests and the wiki.
        public static float StageAt(EnemyType boss, byte stage, MechTier tier)
        {
            var list = StagesFor(boss, tier);
            int i = StageIndex(list, stage);
            return i < 0 ? -1f : list[i].At;
        }

        /// Host, each tick: the stage a boss in `stage` at `frac` of its health moves on to now (false: it stays). Each boss
        /// checks its own conditions on the row (a charge or a dive finishes first) and goes in the row's way (`How`).
        public bool NextStage(EnemyType boss, byte stage, float frac, out BossStage next)
        {
            var list = StagesOf(boss);
            int i = StageIndex(list, stage);
            next = default(BossStage);
            if (i < 0 || i + 1 >= list.Length)
                return false;
            next = list[i + 1];
            return frac <= next.At;
        }
    }
}
