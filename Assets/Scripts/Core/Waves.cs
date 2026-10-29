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
            foreach (var p in Players.Values)
                if (!p.Downed || p.ReviveToken)
                    return;  // someone's standing, or will get up on their own
            State = MatchState.GameOver;
            spawnQueue.Clear();
            foreach (var p in Players.Values)
            {
                p.Offer = null;
                p.LegendaryQueue.Clear();
                p.PendingLegendary = 0;
                p.PendingMods = 0;
                p.OfferMod = false;
            }
            BroadcastRunStats();
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
            switch (State)
            {
                case MatchState.Intermission:
                    // The countdown waits while anyone is still choosing upgrades, for the host to pick a path, and for the shop.
                    if (PathOffer != null && Bot)
                        ChoosePath(rng.Next(2));  // a bot host picks for itself
                    TickShop(dt);  // the Bazaar's shop closes when everyone is done (Shop.cs)
                    if (!AnyonePicking() && PathOffer == null && !ShopOpen)
                        StateTimer -= dt;
                    if (StateTimer <= 0f)
                    {
                        State = MatchState.Fighting;
                        spawnTimer = 0.5f;
                        string bossName;
                        if (Practice != null && Practice.Waves.Count > 0)
                        {
                            // Practice range: the next wave of the plan (wrapping round), at its level.
                            PracticeIndex = (PracticeIndex + 1) % Practice.Waves.Count;
                            var pw = Practice.Waves[PracticeIndex];
                            Wave = Math.Max(1, pw.Level);
                            BuildPracticeWave(pw);
                            var pm = Proto.Begin(Msg.PracticeWave);  // so everyone's HUD shows which practice wave this is
                            pm.Write(PracticeIndex);
                            session.Broadcast(Proto.End(pm));
                            var boss = Array.Find(PracticePlan.Choices, t => Defs.Get(t).IsBoss && pw.Counts.ContainsKey(t) && pw.Counts[t] > 0);
                            bossName = Defs.Get(boss).IsBoss ? PracticePlan.NameOf(boss) : null;
                        }
                        else
                        {
                            Wave++;
                            BuildWave(Wave);
                            bossName = Wave % 5 == 0 ? Defs.Get(BossForWave(Wave)).Name : null;
                        }
                        Recorder?.WaveStart(this, Wave, bossName);
                        DoLog("wave " + Wave + ": " + string.Join(",", spawnQueue));
                    }
                    break;
                case MatchState.Fighting:
                    if (spawnQueue.Count > 0)
                    {
                        spawnTimer -= dt;
                        if (spawnTimer <= 0f)
                        {
                            SpawnEnemy(spawnQueue[0]);
                            spawnQueue.RemoveAt(0);
                            spawnTimer = Math.Max(0.3f, 1.1f - Wave * 0.05f);
                        }
                    }
                    else if (Enemies.Count == 0)
                    {
                        if (Practice != null)
                        {
                            if (PracticeIndex >= Practice.Waves.Count - 1 && !Practice.Loop)
                                Win();  // practice complete
                            else
                                WaveCleared();
                        }
                        else if (Wave >= MaxWave && !Endless)
                            Win();
                        else
                            WaveCleared();
                    }
                    break;
            }
            Remaining = spawnQueue.Count + Enemies.Count;
        }

        void WaveCleared()
        {
            Recorder?.WaveEnd(this);
            ResetBossState();
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            State = MatchState.Intermission;
            StateTimer = IntermissionTime;
            if (CurrentPath != PathId.None && Wave >= PathTo)
                FinishPath();  // its prize, then the plain arena for the boss (Paths.cs)
            if (Has(RunMod.GoldRush) && (Wave + 1) % 5 == 0 && !ShopOpen && Practice == null && (Wave < MaxWave || Endless))
                OpenShop();  // Gold Rush: a shop before every boss
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    RevivePlayer(p, 0.4f);
                else
                    p.Hp = Math.Min(p.MaxHp, p.Hp + p.MaxHp * 0.3f);
                if (p.Offer == null)
                    NextOffer(p);
            }
            if (Wave % 5 == 0)
                OfferPaths();  // after a boss: two paths for the next stretch
            RemoveTemporaryMinions();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            DoLog("wave " + Wave + " cleared");
        }
    }
}
