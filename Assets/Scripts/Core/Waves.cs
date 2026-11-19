using System;
using System.Collections.Generic;

namespace Arena
{
    /// Wave flow: the run states between waves, clearing a wave and game over.
    public partial class Game
    {
        void CheckGameOver()
        {
            if (State == MatchState.GameOver || State == MatchState.Idle || State == MatchState.Victory || Players.Count == 0)
                return;
            if (Practice != null)
                return;  // the Practice Range never ends in a wipe: a downed player respawns (PracticeRespawn.cs)
            if (InRoom)
            {
                foreach (var p in Players.Values)
                    if (!p.Downed)
                        return;
                RoomCatchesWipe();  // a challenge room: everyone down just loses the challenge (Challenges.cs)
                return;
            }
            foreach (var p in Players.Values)
                if (!p.Downed || p.ReviveToken || p.SecondWind)
                    return;  // someone's standing, or will get up on their own
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.Phoenix)
                    return;  // a phoenix brings someone back on its next beat (Minions.cs)
            State = MatchState.GameOver;
            TestRunEnded("everyone went down");  // BalanceTest.cs
            spawnQueue.Clear();
            squadQueue.Clear();
            foreach (var p in Players.Values)
            {
                p.Offer = null;
                p.LegendaryQueue.Clear();
                p.PendingLegendary = 0;
                p.PendingMods = 0;
                p.OfferMod = false;
                p.PendingCards = 0;
                p.OfferCards = false;
                p.WavePicks = 0;
                p.OfferWave = false;
                p.PendingMilestones = 0;
                p.OfferMilestone = false;
            }
            BroadcastRunStats();
            RunOver(false);  // its save goes; profiles hear about it (RunSaves.cs)
            Recorder?.End(this, "game over");
            DoLog("game over at wave " + Wave + ", score " + Score);
        }

        void TickWaves(float dt)
        {
            if (ShowcaseMode)
            {
                TickShowcase(dt);  // the loadout preview's target dummies instead of waves (Showcase.cs)
                return;
            }
            if (InRoom)
            {
                TickRoom(dt);  // a challenge room instead of a wave (Challenges.cs)
                Remaining = Enemies.Count;
                return;
            }
            switch (State)
            {
                case MatchState.Intermission:
                    if (Surv != null && SurvStarted)
                    {
                        TickSurvivalPicks();  // Survival: the clock is frozen while someone picks a level-up card (SurvivalGame.cs)
                        break;
                    }
                    if (Room == RoomPhase.Portal)
                        TickPortal(dt);  // a challenge room's portal is open (Challenges.cs)
                    // The countdown waits while anyone is still choosing upgrades, for the host to pick a path, and for the shop.
                    if (PathOffer != null && Bot)
                        ChoosePath(rng.Next(2));  // a bot host picks for itself
                    TickShop(dt);  // the Bazaar's shop closes when everyone is done (Shop.cs)
                    TickBreakStatus();  // each player's status chip: picking, building, shopping, out looting, ready (WaveReady.cs)
                    TickHuntCheckpoint();  // the step-10 save, before the vote can pick a node (HuntMapGame.cs)
                    TickHuntMap(dt);  // the hunt map's vote on the next node (HuntMapGame.cs)
                    TickDuelRooms(dt);  // a mini-boss room can be taken on between waves too (GatekeeperRoom.cs)
                    TickKeepsake(dt);  // the vote on the room to keep through the map reset (MapReset.cs)
                    TickMysteryDoors();  // a door whose room no longer fits is re-planned or goes (MysteryDoors.cs)
                    if (RoomsOn)
                        TickStashClaims();  // a breach Stash's cache can be walked into between waves too (BreachEvent.cs)
                    if (!AnyonePicking() && PathOffer == null && !ShopBlocksWave && !HuntMapBlocksWave && !KeepsakeOpen && Room == RoomPhase.None)  // (and while a portal is open)
                    {
                        if (!TickBossGate(ref StateTimer, dt))  // before a boss: everyone in the boss gate instead (BossGate.cs)
                            TickReadyTimer(ref StateTimer, dt);  // ...and until everyone is ready (WaveReady.cs)
                    }
                    if (StateTimer <= 0f && BossBeamHold(dt))
                        break;  // everyone outside the hub is beamed into it before the boss wave (BossBeam.cs)
                    if (StateTimer <= 0f)
                    {
                        if (ShopOpen && ServiceRooms)
                            CloseShop();  // the room shops close as the wave starts (gold and shelf are kept)
                        CloseBossGate();  // BossGate.cs
                        ResolveBuildRound();  // the building round closes as the wave starts: the winning room is built (ExpansionGame.cs)
                        SweepFloorLoot();  // each player's own coins, vector pickups and chest cards left lying about go to them (Coins.cs)
                        SaveRun();  // everyone has picked: the save slot holds the run from here (RunSaves.cs)
                        State = MatchState.Fighting;
                        SpringPendingAmbushes();  // ambush rooms opened between waves (MysteryDoors.cs)
                        spawnTimer = WaveNumbers.OpeningDelay;
                        string bossName;
                        if (SurvivalOn)
                        {
                            BeginSurvival();  // no wave to build: the clock starts (SurvivalGame.cs)
                            bossName = null;
                        }
                        else if (Practice != null && Practice.Waves.Count > 0)
                        {
                            // Practice range: the next wave of the plan (wrapping round), at its level.
                            PracticeIndex = (PracticeIndex + 1) % Practice.Waves.Count;
                            var pw = Practice.Waves[PracticeIndex];
                            Wave = Math.Max(1, pw.Level);
                            if (Design != null && PracticeIndex < Design.Waves.Count)
                                BuildDesignWave(Design.Waves[PracticeIndex]);  // the F1 wave editor's design (WaveDesign.cs)
                            else
                                BuildPracticeWave(pw);
                            var pm = Proto.Begin(Msg.PracticeWave);  // so everyone's HUD shows which practice wave this is
                            pm.Write(PracticeIndex);
                            session.Broadcast(Proto.End(pm));
                            var boss = Array.Find(PracticePlan.Choices, t => Defs.Get(t).IsBoss && pw.Counts.ContainsKey(t) && pw.Counts[t] > 0);
                            bossName = Defs.Get(boss).IsBoss ? PracticePlan.NameOf(boss) : null;
                            BossSlot = bossName != null;
                        }
                        else
                        {
                            if (BossStartsNext)
                                BossSlot = true;  // the boss after every fifth regular wave: the wave number stays (Hunts.cs)
                            else if (LongLegs && Wave > 0 && !BossSlot && Leg == 0)
                                Leg = 1;  // a Campaign: the same track wave again, its second leg (Hunts.cs)
                            else
                            {
                                Leg = 0;
                                BossSlot = false;
                                Wave++;
                            }
                            PlaceFloatingRooms();  // a new boss cycle: its floating shop, vendor and Gatekeeper rooms (ExpansionGame.cs)
                            PickRooms();   // the Expanding arena's walled world, on a regular wave (Rooms.cs)
                            PickLayout();  // this cycle's floor plan, or the open arena for a boss (LayoutRules.cs)
                            BuildWave(Wave);
                            if (BreachesOn)
                            {
                                FloodBreachWave();  // a breach wave: three times the enemies (Breaches.cs)
                                spawnTimer = Math.Max(spawnTimer, ModeRules.Extras ? Breaches.WarnTime(ActiveBreaches.Length) : Breaches.Telegraph);  // the cameras tour the breaches first (Game/BreachTour.cs)
                            }
                            CapSentries();  // at most 3 Beam Sentries and 3 Pulsars (Variety.cs)
                            WaveProps();  // crates, barrels and now and then a chest (Props.cs)
                            bossName = BossNow ? Defs.Get(BossAt(Wave)).Name : null;
                            BeginTimedWave();  // the wave's enemies arrive over a window (TimedWaves.cs)
                            HuntNodeWaveStart();  // an Ambush node: fewer, all at once (HuntMapGame.cs)
                        }
                        ModsWaveStart();  // Hunted's quarry, Second Wind back each boss cycle (Modifiers.cs)
                        MarkWaveStart();  // the wave's numbers start from here (WaveReady.cs)
                        Recorder?.WaveStart(this, Wave, bossName);
                        DoLog("wave " + Wave + ": " + string.Join(",", spawnQueue));
                    }
                    break;
                case MatchState.Fighting:
                    if (Surv != null)
                    {
                        TickSurvival(dt);  // Survival: top the field up to the budget; no wave ever clears (SurvivalGame.cs)
                        break;
                    }
                    if (DevArena)
                    {
                        spawnQueue.Clear();  // the Dev Arena: only what you spawn by hand, and the wave never clears (DevArena.cs)
                        squadQueue.Clear();
                        break;
                    }
                    if (DevFreeze)
                        break;  // F1 dev tool: nothing new spawns and the wave doesn't clear (DevTools.cs)
                    TickRisers(dt);  // Zombie Infection's dead getting back up (Zombies.cs)
                    TickDesign(dt);  // a designed wave's groups arriving on their timers (WaveDesign.cs)
                    TickRoomsWave(dt);  // the Expanding arena's rooms (Rooms.cs)
                    if (TickSpawning(dt))
                    {
                        // (the wave's queue is still coming in: Spawner.cs)
                    }
                    else if (Foes() == 0 && PropsHoldWave())
                        HoldForTreasure();  // an unopened chest or an item left: the wave waits for it (Props.cs)
                    else if (Foes() == 0 && !ZombiesRising && DesignPending == 0)
                    {
                        if (Practice != null)
                        {
                            if (PracticeIndex >= Practice.Waves.Count - 1 && !Practice.Loop)
                                Win();  // practice complete
                            else
                                WaveCleared();
                        }
                        else if (Hunt != null && Wave >= HuntLength)
                            Win();  // a Boss Hunt ends with its boss (Hunts.cs)
                        else if (Wave >= LastWave && BossSlot && !Endless)
                            Win();  // the last boss is beaten (Quick Play: the first, after wave 10) (the last regular wave only leads up to it)
                        else
                            WaveCleared();
                    }
                    break;
            }
            Remaining = PendingEnemies();
        }

        void WaveCleared()
        {
            PayVeins();  // every claimed gold vein pays out (NodeScouting.cs)
            ClearRuleRooms();  // a claimed rule room has been through the wave: its prize (ExpansionGame.cs)
            EndRoomsWave();  // an unbeaten Gatekeeper duel ends with the wave (Rooms.cs)
            Recorder?.WaveEnd(this);
            TestWaveCleared();  // BalanceTest.cs
            SummariseWave();  // clear time, each player's numbers and awards (WaveReady.cs)
            ResetBossState();
            ResetVariety(false);  // pending Volatile bursts and Mirrored shots die with the wave (Variety.cs)
            ClearPods();  // a Turnkey's traps and doors left over go (NonShooters.cs)
            ClearRisers();
            FoldAllCovers();  // Deployable Covers fold away with a look rather than vanishing (DeployableCover.cs)
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            State = MatchState.Intermission;
            StateTimer = IntermissionTime;
            HuntMapNodeCleared();  // the hunt map moves on (HuntMapGame.cs)
            if (CurrentPath != PathId.None && Wave >= PathTo && LegDone)
                FinishPath();  // its prize, then the plain arena for the boss (Paths.cs)
            if (Has(RunMod.GoldRush) && BossNext && !ShopOpen && ModeRules.Extras)
                OpenShop();  // Gold Rush: a shop before every boss
            foreach (var p in Players.Values)
            {
                p.Wound = 0f;  // wounds heal between waves (Combat.cs)
                if (p.Downed)
                    RevivePlayer(p, 0.4f);
                else
                    p.Hp = Math.Min(p.MaxHp, p.Hp + p.MaxHp * 0.3f);
                if (p.Offer == null)
                    NextOffer(p);
            }
            if (BossSlot && Hunt == null)
            {
                if (!ExpandingOn)
                {
                    if (ModeRules.Paths)
                        OfferPaths();  // after a boss: two paths for the next stretch (not in Quick Play; the Expanding arena has none)
                }
                else if (!MapResetOn)
                    GiveSurpriseRoom();  // ...but a free, well-shaped room that closes a loop (ExpansionGame.cs, Docs/SurpriseRooms.md; the Campaign resets the map instead)
            }
            else if (ServiceRooms && RoomsOn && !ShopOpen && ModeRules.RunEvents)
                OpenShopRoom(Wave % Expansion.Milestone == 1 && FirstLeg);  // a connected shop or vendor room: open each intermission (walk in); the shelf is new once a boss cycle
            RevealBreachRooms();  // every breach that is left opens onto a small room (BreachEvent.cs, Docs/BreachRooms.md)
            RevealBreachEvent();  // a breach event the wave ended before it opened: the room opens now (BreachEvent.cs)
            GrantVectorsForWave();  // vectors for everyone (ExpansionGame.cs)
            if (IsHost)
                AgeTurrets();  // a wave fewer for every turret (Turret.cs)
            if (!(BossSlot && Hunt == null && MapResetOn && OpenKeepsake()))  // the Campaign: a vote on the room to keep, then a fresh map with half the spent vectors back (MapReset.cs)
            {
                OpenBuildRound();  // a cleared wave opens a round of room proposals and a vote (ExpansionGame.cs)
                RollMysteryDoors();  // last round's unsaved doors expire, 1-2 new sealed doors appear (MysteryDoors.cs)
            }
            RemoveTemporaryMinions();
            MaybeOpenPortal();  // once every five waves: a challenge room (Challenges.cs)
            HostClearEnemyBullets();
            ClearReady();  // nobody is ready yet (WaveReady.cs)
            DoLog("wave " + Wave + " cleared");
        }
    }
}
