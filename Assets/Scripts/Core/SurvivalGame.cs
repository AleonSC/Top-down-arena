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
        float survPropT;
        // What the camera shows round a player (half width and height at the default zoom, plus a margin): enemies appear outside it.
        const float ViewHalfW = 880f, ViewHalfH = 500f;
        const int CrateGoal = 120, PotGoal = 40, BarrelGoal = 8;
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

        /// A Survival run ended (on every machine, for the local player): its records, XP for the waves of clock, a Recent runs card.
        void SurvivalRecord(Player me, int found)
        {
            if (runRecorded)
                return;
            runRecorded = true;
            float time = SurvTime;
            int wave = Survival.WaveAt(time);
            AddUnlockToasts(Profile.SurvivalEnded(Arena.Profile.ClassKey(me.Role, me.Kit), time, SurvPhase + 1));
            LastGain = Profile.AwardXp(Rank.Award(false, Math.Max(0, wave - 1), 0, me.Run.Revives, Mods, Diff.Level), wave, 0);
            PayCores(Variants.RunCores(false, 0, false), found);
            var logged = Profile.Recent[0];
            logged.Class = Arena.Profile.ClassKey(me.Role, me.Kit);
            logged.Mode = "Survival";
            logged.Seconds = time;
            logged.Dealt = me.Run.DamageDealt; logged.Taken = me.Run.DamageTaken; logged.Heal = me.Run.HealingDone;
            logged.Players = Players.Count;
            logged.File = FinishTimeline(false, wave, false, time, logged.Class, logged.Mods, logged.Mode);  // RunTimeline.cs
            SaveProfile();
        }

        /// Host: the countdown ended: the clock starts. Wave 1 scaling to begin with (the wave number follows the clock).
        void BeginSurvival()
        {
            Surv = new Survival(survSeed, Math.Max(1, Players.Count));
            SurvStarted = true;
            SurvTime = 0f;
            Wave = Survival.WaveAt(0f);
            survSquad = null;
            // The big room (SurvivalWorld.cs): up from the first tick, the plain floor plan inside it, everyone near the middle, clear of its walls.
            RoomsUp = true;
            Layout = LayoutId.Open;
            LayoutAge = 0f;
            int i = 0;
            foreach (var p in Players.Values)
            {
                MovePlayer(p, new V2(SurvivalRoom.W / 2f - 300f + 90f * i, SurvivalRoom.H / 2f - 200f));  // (every screen moves with them: Ninja.cs)
                i++;
            }
            ClearProps(false);
            survPropT = 0f;
            SurvivalProps(true);
        }

        /// Host: crates (often two side by side: a little wall), pots and a few barrels scattered over the whole room. At the start
        /// everywhere; after that a few at a time, out of everyone's view, as they get smashed.
        void SurvivalProps(bool all)
        {
            if (!PropsOn)
                return;
            int crates = 0, pots = 0, barrels = 0;
            foreach (var p in Props.Values)
            {
                if (IsCrate(p)) crates++;
                else if (p.Kind == PropKind.Pot) pots++;
                else if (p.Kind == PropKind.Barrel) barrels++;
            }
            int addCrates = all ? CrateGoal : 3, addPots = all ? PotGoal : 1, addBarrels = all ? BarrelGoal : 1;
            for (int n = 0, tries = 0; crates < CrateGoal && n < addCrates && tries < 400; tries++)
            {
                var kind = rng.NextDouble() < 0.3 ? PropKind.HeavyCrate : PropKind.Crate;
                V2 at;
                if (!SurvivalPropSpot(HalfW(kind), all, out at))
                    continue;
                Landed(AddProp(kind, at)); crates++; n++;
                if (crates < CrateGoal && rng.NextDouble() < 0.45)
                {
                    float step = HalfW(kind) * 2f + 2f;
                    V2 next = at + (rng.Next(2) == 0 ? new V2(rng.Next(2) == 0 ? step : -step, 0f) : new V2(0f, rng.Next(2) == 0 ? step : -step));
                    if (SpotClear(next, HalfW(kind), 0f)) { Landed(AddProp(kind, next)); crates++; n++; }
                }
            }
            for (int n = 0, tries = 0; pots < PotGoal && n < addPots && tries < 200; tries++)
            {
                V2 at;
                if (SurvivalPropSpot(PotR, all, out at)) { Landed(AddProp(PropKind.Pot, at)); pots++; n++; }
            }
            for (int n = 0, tries = 0; barrels < BarrelGoal && n < addBarrels && tries < 200; tries++)
            {
                V2 at;
                if (SurvivalPropSpot(PotR, all, out at)) { Landed(AddProp(PropKind.Barrel, at)); barrels++; n++; }
            }
        }

        static Prop Landed(Prop p) { p.Age = 1f; return p; }  // no drop-in animation (and no 220 puffs of dust at once)

        bool SurvivalPropSpot(float size, bool anywhere, out V2 at)
        {
            at = RandomInArena(110f, 110f);
            if (!SpotClear(at, size, 170f))
                return false;
            return anywhere || OutsideViews(at) >= 0f;
        }

        /// How far outside every player's view a point is (negative: inside someone's).
        float OutsideViews(V2 at)
        {
            float outside = float.MaxValue;
            foreach (var p in Players.Values)
                outside = Math.Min(outside, Math.Max(Math.Abs(at.X - p.Pos.X) - ViewHalfW, Math.Abs(at.Y - p.Pos.Y) - ViewHalfH));
            return outside;
        }

        /// Where a Survival enemy comes in: somewhere in the room outside every player's view, clear of the walls, and (of
        /// the spots that qualify) the one nearest a player, so it doesn't have to cross the room.
        V2 SurvivalSpot(float radius)
        {
            V2 best = new V2(SurvivalRoom.W / 2f, SurvivalRoom.H / 2f), fallback = best;
            float bestD = float.MaxValue, fallbackOut = float.MinValue;
            int found = 0;
            for (int tries = 0; tries < 80 && found < 6; tries++)
            {
                V2 at = new V2(Rand(radius + 40f, SurvivalRoom.W - radius - 40f), Rand(radius + 40f, SurvivalRoom.H - radius - 40f));
                if (NearSolid(at, radius + 8f))
                    continue;
                float outside = OutsideViews(at);
                if (outside > fallbackOut) { fallbackOut = outside; fallback = at; }
                if (outside < 40f)
                    continue;
                found++;
                float nearest = float.MaxValue;
                foreach (var p in Players.Values)
                    nearest = Math.Min(nearest, p.Pos.DistanceTo(at));
                if (nearest < bestD) { bestD = nearest; best = at; }
            }
            return found > 0 ? best : fallback;
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
            survPropT += dt;
            if (survPropT >= 15f)
            {
                survPropT = 0f;
                SurvivalProps(false);  // smashed props come back, out of view
            }
        }

        /// Host: one enemy of the spawner's. A squad's members come in together at one spot.
        void SpawnSurvivor(SurvivalSpawn sp)
        {
            var d = Defs.Get(sp.Type);
            V2 pos;
            if (sp.Squad != null && sp.Squad == survSquad)
                pos = survSquadPos + new V2(Rand(-40f, 40f), Rand(-40f, 40f));
            else
            {
                pos = SurvivalSpot(d.Radius);
                survSquad = sp.Squad;
                survSquadPos = pos;
            }
            spawnHome = -1;
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
