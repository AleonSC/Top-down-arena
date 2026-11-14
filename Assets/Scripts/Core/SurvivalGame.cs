using System;
using System.IO;

namespace Arena
{
    /// Survival mode played in a match (Docs/Survival.md, Survival.cs): one clock instead of waves. The host's Survival
    /// spawner tops the field up each tick; a level-up pick freezes the world until everyone has chosen (the match sits in
    /// an Intermission meanwhile, so the normal pick, reroll and gold-instead handling all work). Nothing ever clears or
    /// wins: the run ends when everyone is down (CheckGameOver). Not saved to continue.
    public partial class Game
    {
        /// This run is a Survival run (set by the start message, on every machine).
        public bool SurvivalOn;
        /// The host's spawner (null until the clock starts).
        public Survival Surv;
        /// The clock in seconds, from the snapshot on clients.
        public float SurvTime;
        /// The clock has started: the match goes back and forth between Fighting and a frozen pick Intermission.
        public bool SurvStarted;
        /// The world is held for a pick: enemies and every shot stand still (WorldRate).
        public bool SurvFrozen => SurvivalOn && SurvStarted && State == MatchState.Intermission;
        /// The faction phase the clock is in (0: the basic faction alone).
        public int SurvPhase => Survival.PhaseAt(SurvTime);
        /// 5:12 for 312 seconds.
        public static string SurvClock(float t) { int s = (int)Math.Max(0f, t); return s / 60 + ":" + (s % 60).ToString("00"); }
        int survSeed;
        string survSquad;
        V2 survSquadPos;

        void WriteSurvivalStart(BinaryWriter w, bool quick)
        {
            bool on = session.SurvivalMode && !session.PracticeMode && session.LobbyHunt == null && ResumeFrom == null && !quick;
            w.Write(on);
            if (on)
                w.Write(new Random().Next(1, 1 << 30));
        }

        void ReadSurvivalStart(BinaryReader r)
        {
            SurvivalOn = r.ReadBoolean();
            survSeed = SurvivalOn ? r.ReadInt32() : 0;
            Surv = null;
            SurvStarted = false;
            SurvTime = 0f;
        }

        /// Host: the countdown ended: the clock starts. Wave 1 scaling to begin with (the wave number follows the clock).
        void BeginSurvival()
        {
            Surv = new Survival(survSeed, Math.Max(1, Players.Count));
            SurvStarted = true;
            SurvTime = 0f;
            Wave = Survival.WaveAt(0f);
            survSquad = null;
        }

        /// Host: one Fighting tick. A pending pick freezes the world; otherwise the spawner tops the field up.
        void TickSurvival(float dt)
        {
            if (AnyonePicking())
            {
                foreach (var p in Players.Values)
                    if (p.Offer == null)
                        NextOffer(p);
                State = MatchState.Intermission;
                StateTimer = 1f;
                return;
            }
            float threat = 0f;
            int live = 0;
            foreach (var e in Enemies.Values)
                if (!e.Def.IsBoss && e.Hp > 0f)
                {
                    threat += Survival.Cost(e.Type);
                    live++;
                }
            foreach (var sp in Surv.Step(dt, threat, live))
                SpawnSurvivor(sp);
            SurvTime = Surv.Time;
            Wave = Survival.WaveAt(Surv.Time);
        }

        /// Host: one enemy of the spawner's. A squad's members come in together at one spot.
        void SpawnSurvivor(SurvivalSpawn sp)
        {
            var d = Defs.Get(sp.Type);
            int home;
            V2 pos;
            if (sp.Squad != null && sp.Squad == survSquad)
                pos = survSquadPos + new V2(Rand(-40f, 40f), Rand(-40f, 40f));
            else
            {
                pos = SpawnSpot(d.Radius, !d.IsBoss, out home);
                survSquad = sp.Squad;
                survSquadPos = pos;
            }
            home = -1;
            spawnHome = home;
            SpawnEnemyAt(sp.Type, pos, 0.9f, 0, sp.Elite);
            spawnHome = -1;
        }

        /// Host: the frozen Intermission: offers go out, and once nobody has a pick left the clock runs again.
        void TickSurvivalPicks()
        {
            foreach (var p in Players.Values)
                if (p.Offer == null && (p.PendingPicks > 0 || p.PendingLegendary > 0 || p.PendingMods > 0 || p.PendingMilestones > 0 || p.PrizePicks > 0))
                    NextOffer(p);
            if (!AnyonePicking())
                State = MatchState.Fighting;
        }
    }
}
