using System;
using System.Collections.Generic;

namespace Arena
{
    /// Wave flow: the match states between waves, clearing a wave and game over.
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
                    if (Room == RoomPhase.Portal)
                        TickPortal(dt);  // a challenge room's portal is open (Challenges.cs)
                    // The countdown waits while anyone is still choosing upgrades, for the host to pick a path, and for the shop.
                    if (PathOffer != null && Bot)
                        ChoosePath(rng.Next(2));  // a bot host picks for itself
                    TickShop(dt);  // the Bazaar's shop closes when everyone is done (Shop.cs)
                    if (!AnyonePicking() && PathOffer == null && !ShopBlocksWave && Room == RoomPhase.None)  // (and while a portal is open)
                        TickReadyTimer(ref StateTimer, dt);  // ...and until everyone is ready (WaveReady.cs)
                    if (StateTimer <= 0f)
                    {
                        if (ShopOpen && ServiceRooms)
                            CloseShop();  // the room shops close as the wave starts (gold and shelf are kept)
                        ResolveBuildRound();  // the building round closes as the wave starts: the winning room is built (ExpansionGame.cs)
                        SaveRun();  // everyone has picked: the save slot holds the run from here (RunSaves.cs)
                        State = MatchState.Fighting;
                        spawnTimer = 0.5f;
                        string bossName;
                        if (Practice != null && Practice.Waves.Count > 0)
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
                            else
                            {
                                BossSlot = false;
                                Wave++;
                            }
                            PlaceFloatingRooms();  // a new boss cycle: its floating shop, vendor and Gatekeeper rooms (ExpansionGame.cs)
                            PickRooms();   // the side rooms and hallways, on a regular wave with the Rooms arena (Rooms.cs)
                            PickLayout();  // this cycle's floor plan, or the open arena for a boss (LayoutRules.cs)
                            BuildWave(Wave);
                            StartRoomsWave();  // the rooms' nests and the vault's mini-boss (Rooms.cs)
                            if (BreachesOn)
                                spawnTimer = Math.Max(spawnTimer, Breaches.Telegraph);  // the breaches' barriers flicker up first (Breaches.cs)
                            CapSentries();  // at most 3 Beam Sentries (Variety.cs)
                            WaveProps();  // crates, pots, barrels and now and then a chest (Props.cs)
                            bossName = BossNow ? Defs.Get(BossAt(Wave)).Name : null;
                        }
                        ModsWaveStart();  // Hunted's quarry, Second Wind back each boss cycle (Modifiers.cs)
                        MarkWaveStart();  // the wave's numbers start from here (WaveReady.cs)
                        Recorder?.WaveStart(this, Wave, bossName);
                        DoLog("wave " + Wave + ": " + string.Join(",", spawnQueue));
                    }
                    break;
                case MatchState.Fighting:
                    if (DevFreeze)
                        break;  // F1 dev tool: nothing new spawns and the wave doesn't clear (DevTools.cs)
                    TickRisers(dt);  // Zombie Infection's dead getting back up (Zombies.cs)
                    TickDesign(dt);  // a designed wave's groups arriving on their timers (WaveDesign.cs)
                    TickRoomsWave(dt);  // the Rooms arena's nests, spills and cleared rooms (Rooms.cs)
                    if ((spawnQueue.Count > 0 || squadQueue.Count > 0) && RoomsOn && Rooms.RoomByRoom && !RoomsSpawnReady())
                        spawnTimer = Math.Max(spawnTimer, 0.3f);  // every room full (or every nest down: the rest is called off)
                    else if (spawnQueue.Count > 0 || squadQueue.Count > 0)
                    {
                        spawnTimer -= dt;
                        float next = Math.Max(0.3f, 1.1f - Wave * 0.05f);
                        if (spawnTimer <= 0f && ZombieSpawnReady(ref next))  // (a horde comes faster, up to its cap)
                        {
                            spawnTimer = next;
                            if (SpawnNextSquad())  // a squad comes after every few single enemies (Variety.cs)
                                spawnTimer *= 3f;
                            else
                            {
                                SpawnEnemy(spawnQueue[0]);
                                spawnQueue.RemoveAt(0);
                                singlesSinceSquad++;
                            }
                        }
                    }
                    else if (FoesOutsideVault() == 0 && PropsHoldWave())
                        HoldForTreasure();  // an unopened chest or an item left: the wave waits for it (Props.cs)
                    else if (FoesOutsideVault() == 0 && !ZombiesRising && DesignPending == 0)  // (the vault is optional: Rooms.cs)
                    {
                        if (Practice != null)
                        {
                            if (PracticeIndex >= Practice.Waves.Count - 1 && !Practice.Loop)
                                Win();  // practice complete
                            else
                                WaveCleared();
                        }
                        else if (Hunt != null && Wave >= Hunts.Length)
                            Win();  // a Boss Hunt ends with its boss (Hunts.cs)
                        else if (Wave >= MaxWave && BossSlot && !Endless)
                            Win();  // the last boss is beaten (the last regular wave only leads up to it)
                        else
                            WaveCleared();
                    }
                    break;
            }
            Remaining = spawnQueue.Count + Foes() + risers.Count + DesignPending;
            foreach (var sq in squadQueue)
                Remaining += sq.Members.Length;
        }

        void WaveCleared()
        {
            EndRoomsWave();  // the vault closes with the wave (Rooms.cs)
            Recorder?.WaveEnd(this);
            TestWaveCleared();  // BalanceTest.cs
            SummariseWave();  // clear time, each player's numbers and awards (WaveReady.cs)
            ResetBossState();
            ResetVariety(false);  // pending Volatile bursts and Mirrored shots die with the wave (Variety.cs)
            ClearPods();  // a Sapper's pods left over fizzle out (NonShooters.cs)
            ClearRisers();
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            State = MatchState.Intermission;
            StateTimer = IntermissionTime;
            if (CurrentPath != PathId.None && Wave >= PathTo)
                FinishPath();  // its prize, then the plain arena for the boss (Paths.cs)
            if (Has(RunMod.GoldRush) && BossNext && !ShopOpen && Practice == null)
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
                    OfferPaths();  // after a boss: two paths for the next stretch (the Expanding arena has no paths)
            }
            else if (ServiceRooms && RoomsOn && !ShopOpen && Practice == null && Hunt == null)
                OpenShopRoom(Wave % Expansion.Milestone == 1);  // a connected shop or vendor room: open each intermission (walk in); the shelf is new once a boss cycle
            GrantVectorsForWave();  // vectors for everyone (ExpansionGame.cs)
            OpenBuildRound();  // a cleared wave opens a round of room proposals and a vote (ExpansionGame.cs)
            RemoveTemporaryMinions();
            MaybeOpenPortal();  // once every five waves: a challenge room (Challenges.cs)
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            ClearReady();  // nobody is ready yet (WaveReady.cs)
            DoLog("wave " + Wave + " cleared");
        }
    }
}
