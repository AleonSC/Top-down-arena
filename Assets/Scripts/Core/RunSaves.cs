using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Saved runs and the player's profile, on the game's side (SaveData.cs and Profile.cs hold the data).
    ///   Saving: the host writes the run to its save slot as each wave starts (everyone's picks made, the path
    ///     chosen, the shop closed), and when the match stops between waves. The Expanding arena's map, kits, vectors and shop
    ///     shelves go in too (ExpansionGame.cs SaveExpansion / ResumeExpansion). A run that ends (won or lost) loses
    ///     its save. Practice Range matches and the lobby preview are never saved.
    ///   Resuming: the host's lobby loads a save (Session.Resume); each player takes a seat; the match starts at
    ///     the next wave and ApplyResume gives every seat back its upgrades, levels, mastery and gold.
    ///   Profile: the host tells everyone when a boss falls and when the run ends (Msg.Record); each game adds it
    ///     to its own player's profile, unless dev tools or the bot were used in the run.
    public partial class Game
    {
        public string RunId;              // host: this run's save slot (null: not saved)
        public RunSave ResumeFrom;        // host, before HostStart: the save being continued
        public readonly Dictionary<int, int> ResumeSeats = new Dictionary<int, int>();  // host: player id -> seat in ResumeFrom
        public bool Resumed;              // this run was continued from a save (every game knows)
        bool devTouched;                  // host: a dev tool changed this run (its save says so; nobody's profile counts it)
        bool botTouched;                  // this game's bot played at some point (this player's profile doesn't count it)
        bool runRecorded;                 // this game has added the run's end to the profile
        int quickCreditedWaves, quickCreditedBosses;  // Quick Play: what the profile already got (its win, before an endless stretch)
        float quickCreditedTime;
        int endlessFromWave, endlessFromCores;      // the Campaign: the waves cleared and Cores found when its win was recorded (an endless fall adds only what came after)
        float endlessFromTime;
        bool endlessRecorded;                       // the Campaign's endless stretch after the win is on the profile (fallen or quit)

        /// The rank XP this player's profile just earned from the run or hunt that ended (null until then; the results screen shows it).
        public RankGain LastGain;
        int runLastBossWave;              // the wave of the last boss this player's profile heard beaten this run (a boss down on wave 10 means wave 10 is cleared)
        public Profile ProfileOverride;   // tests: a profile per game instead of the computer's
        public bool Unrecorded;           // a Boss Lab fight: never saved, and not in anyone's records (BossLab.cs)
        public Profile Profile => ProfileOverride ?? Profile.Current;
        public readonly List<UnlockToast> UnlockToasts = new List<UnlockToast>();  // the front end draws and ages them

        enum RecordKind : byte { Boss, End, Resume, Room }

        bool Saveable => IsHost && !SurvivalOn && Practice == null && Hunt == null && !Quick && !ShowcaseMode && !Unrecorded && RunId != null;

        /// Counts towards records at all (a real run, not practice, the lobby preview or the Boss Lab).
        bool RecordsRuns => Practice == null && !ShowcaseMode && !Unrecorded;

        /// A dev tool was used (god mode, stat edits, wave jumps...): recorded, and the run no longer counts.
        void DevUsed(string what)
        {
            devTouched = true;
            Recorder?.Dev(this, what);
        }

        /// Host: write the run to its slot, if it's between waves with nothing left to choose.
        public bool SaveRun()
        {
            if (!Saveable || State != MatchState.Intermission || AnyonePicking() || PathOffer != null || ShopOpen || Players.Count == 0)
                return false;
            var save = new RunSave
            {
                Id = RunId, SavedAt = DateTime.UtcNow, Wave = Wave, BossSlot = BossSlot, LongRun = LongLegs, Leg = Leg, LivesUsed = LivesUsed, Score = Score, Mods = Mods, Diff = Diff.Copy(), Endless = Endless, DevUsed = devTouched,
                RunTime = RunTime, CurrentPath = CurrentPath, PathFrom = PathFrom, PathTo = PathTo, LastPath = lastPath,
            };
            save.Route.AddRange(Route);
            var seatOf = new Dictionary<int, int>();  // player id -> seat (the Expanding arena's room owners)
            foreach (var p in Players.Values)
            {
                seatOf[p.Id] = save.Seats.Count;
                LobbyEntry e;
                session.Lobby.TryGetValue(p.Id, out e);
                var seat = new SeatSave
                {
                    Name = p.Name, Role = p.Role, Kit = p.Kit, Weapon = e != null ? e.Weapon : 0, Ability = e != null ? e.Ability : 0, Wild = e != null ? e.Wild : 0, Ult = p.UltPick,
                    Level = p.Level, Xp = p.Xp, Rerolls = p.Rerolls, Gold = p.Gold, Relic = p.Relic, ReviveToken = p.ReviveToken,
                    HpFrac = p.Downed || p.MaxHp <= 0f ? 0.4f : p.Hp / p.MaxHp, Run = p.Run,
                };
                foreach (var pk in p.Perks)
                    seat.Perks.Add(pk.ToString());
                foreach (int t in p.Taken)
                    seat.Taken.Add(Upgrades.All[t].Name);
                for (int k = 0; k < p.BonusKeys.Length; k++)
                    seat.BonusKeys[k] = p.BonusKeys[k] == AbilityId.None ? "" : p.BonusKeys[k].ToString();
                foreach (var kv in p.MasteryXp)
                    if (kv.Key != AbilityId.None && kv.Value > 0f)
                        seat.Mastery[kv.Key.ToString()] = kv.Value;
                if (ExpandingOn)
                    SaveExpansionSeat(p, seat);
                save.Seats.Add(seat);
            }
            if (ExpandingOn)
                SaveExpansion(save, seatOf);
            bool ok = SaveStore.Write(save);
            if (ok && BossSlot && Hunt == null)
                SaveStore.WriteCheckpoint(save);  // a boss just fell: this is the run's checkpoint (Checkpoints.cs)
            if (ok)
                DoLog("run saved before wave " + (ShownWave + 1));
            return ok;
        }

        /// An ability's name in an older save, as it is called now (the Cluster Grenade became Airstrike).
        static string OldAbilityName(string name) => name == "Grenade" ? nameof(AbilityId.Airstrike) : name;

        /// Host, just after the match starts from a save: everyone's earnings back, and the run's own state.
        void ApplyResume(RunSave save)
        {
            foreach (var p in Players.Values)
            {
                int idx;
                if (!ResumeSeats.TryGetValue(p.Id, out idx) || idx < 0 || idx >= save.Seats.Count)
                    continue;
                var s = save.Seats[idx];
                p.Taken.Clear();
                foreach (var name in s.Taken)
                {
                    string card = name == "Cluster Grenade" ? "Airstrike" : name;  // (the Cluster Grenade became Airstrike)
                    int u = Array.FindIndex(Upgrades.All, x => x.Name == card);
                    if (u >= 0)
                        p.Taken.Add(u);
                }
                for (int k = 0; k < p.BonusKeys.Length; k++)
                {
                    AbilityId id;
                    p.BonusKeys[k] = Enum.TryParse(OldAbilityName(s.BonusKeys[k]), out id) ? id : AbilityId.None;
                }
                p.MasteryXp.Clear();
                foreach (var kv in s.Mastery)
                {
                    AbilityId id;
                    if (Enum.TryParse(OldAbilityName(kv.Key), out id))
                        p.MasteryXp[id] = kv.Value;
                }
                p.Level = s.Level;
                p.Xp = s.Xp;
                p.Rerolls = s.Rerolls;
                p.Gold = s.Gold;
                p.Relic = s.Relic;
                p.ReviveToken = s.ReviveToken;
                p.Run = s.Run;
                p.PendingPicks = 0;
                p.LegendaryQueue.Clear();
                p.PendingMods = 0;
                p.PendingMilestones = 0;
                p.Offer = null;
                NextOffer(p);
                Recalc(p);
                p.Hp = p.MaxHp * s.HpFrac;
                for (int k = 0; k < p.HpHistory.Length; k++)
                    p.HpHistory[k] = p.Hp;
                lastLevel[p.Id] = p.Level;
                BroadcastUpgrades(p);
            }
            Score = save.Score;
            BossSlot = save.BossSlot;
            LivesUsed = save.LivesUsed;  // Checkpoints.cs
            Leg = save.LongRun ? save.Leg : 1;
            Endless = save.Endless;
            RunTime = save.RunTime;
            devTouched = save.DevUsed;
            CurrentPath = save.CurrentPath;
            PathFrom = save.PathFrom;
            PathTo = save.PathTo;
            lastPath = save.LastPath;
            Route.Clear();
            Route.AddRange(save.Route);
            if (ExpandingOn && save.HasExpansion)
                ResumeExpansion(save);  // ExpansionGame.cs
            var w = BeginRecord(RecordKind.Resume);
            w.Write(RunTime);
            w.Write((byte)CurrentPath);
            w.Write(PathFrom);
            w.Write(PathTo);
            w.Write((byte)Route.Count);
            foreach (var r in Route)
                w.Write((byte)r);
            session.Broadcast(Proto.End(w));
            DoLog("resumed a saved run at wave " + (ShownWave + 1));
            SendSnapshot();
        }

        /// Every game, as a match starts: a fresh run counts as started (a resumed one already did).
        void BeginRunRecord(bool resumed)
        {
            Resumed = resumed;
            botTouched = Bot;
            runRecorded = false;
            quickCreditedWaves = quickCreditedBosses = 0;
            quickCreditedTime = 0f;
            endlessFromWave = endlessFromCores = 0;
            endlessFromTime = 0f;
            endlessRecorded = false;
            ResetTimeline();  // RunTimeline.cs
            LastGain = null;
            runLastBossWave = 0;
            if (IsHost)
            {
                devTouched = false;
                RunId = !RecordsRuns ? null : resumed && ResumeFrom != null ? ResumeFrom.Id : RunSave.NewId();
            }
            if (!resumed && RecordsRuns && Me != null && Hunt == null && !Quick)  // (a Boss Hunt or a Quick Play isn't a run: its own records at the end)
            {
                Profile.RunStarted();
                SaveProfile();
            }
        }

        /// Host: the run ended (won or lost); its save goes, and everyone's profile hears about it.
        void RunOver(bool won)
        {
            if (!IsHost)
                return;
            if (!won && OfferRetry())
                return;  // a life left: the game over screen offers a retry from the checkpoint (Checkpoints.cs)
            EndRun(won);
        }

        /// Host: the run is over for good: its save (and checkpoint) go, and everyone's profile hears about it.
        void EndRun(bool won)
        {
            if (RunId != null)
                SaveStore.Delete(RunId);
            if (!RecordsRuns)
                return;
            var w = BeginRecord(RecordKind.End);
            w.Write(won);
            w.Write(Wave);
            w.Write(Score);
            w.Write(Endless);
            w.Write(devTouched);
            w.Write(RunTime);
            w.Write(won && Deathless);  // won without using a life (Checkpoints.cs)
            w.Write(RecordWave);  // the wave as records count it (Hunts.cs: 1-100 in a Campaign)
            w.Write(CoresFound);  // Cores the run's carriers dropped (CoreDrops.cs)
            var msg = Proto.End(w);
            session.Broadcast(msg);
            ReadRecordFrom(msg);
        }

        /// This game is being stopped mid-run (quit or left): the run still goes in Recent runs, as "quit", with no XP.
        /// Only your own profile, and only if a curve was started (so not the lobby or a match that never began).
        void LogQuitRun()
        {
            bool endlessStretch = Endless && runRecorded && !endlessRecorded;  // quitting the endless stretch after a win (Quick Play or Campaign): the win is logged, the stretch since gets its own card
            if ((runRecorded && !endlessStretch) || RetryReady || retrying || timeline == null || Me == null || !TimelineOn || Hunt != null)  // (a quit Boss Hunt isn't logged)
                return;
            runRecorded = true;
            endlessRecorded = Endless;
            var logged = Profile.AddQuitLog(RecordWave, Rank.Award(false, 0, 0, 0, Mods, Diff.Level).Mods);  // (no XP: only the modifier names are used)
            logged.Class = Arena.Profile.ClassKey(Me.Role, Me.Kit);
            logged.Mode = Quick ? (Endless ? "Quick Play · Endless" : "Quick Play") : Endless ? "Endless" : "Campaign";  // (a Quick Play quit before its first record, or in its endless stretch: QuickRecord)
            logged.Seconds = RunTime;
            logged.Dealt = Me.Run.DamageDealt; logged.Taken = Me.Run.DamageTaken; logged.Heal = Me.Run.HealingDone;
            logged.Players = Players.Count;
            logged.File = FinishTimeline(false, RecordWave, Endless, RunTime, logged.Class, logged.Mods, Quick ? logged.Mode : null);  // (FinishTimeline drops the curve: a second Stop logs nothing)
            SaveProfile();
        }

        /// Host: a boss was beaten (the Twin Reapers count once, as TwinA).
        void BossRecord(Enemy e)
        {
            if (!IsHost || !RecordsRuns)
                return;
            var w = BeginRecord(RecordKind.Boss);
            w.Write((byte)(IsReaper(e.Type) ? EnemyType.TwinA : e.Type));
            w.Write(Wave);
            w.Write(devTouched);
            w.Write(RecordWave);  // the wave as records count it (Hunts.cs: 1-100 in a Campaign)
            var msg = Proto.End(w);
            session.Broadcast(msg);
            ReadRecordFrom(msg);
        }

        /// Host: a challenge room ended (Challenges.cs): everyone's profile counts it.
        void RoomRecord(ChallengeKind kind, bool won)
        {
            if (!IsHost || !RecordsRuns)
                return;
            var w = BeginRecord(RecordKind.Room);
            w.Write((byte)kind);
            w.Write(won);
            w.Write(devTouched);
            var msg = Proto.End(w);
            session.Broadcast(msg);
            ReadRecordFrom(msg);
        }

        static BinaryWriter BeginRecord(RecordKind kind)
        {
            var w = Proto.Begin(Msg.Record);
            w.Write((byte)kind);
            return w;
        }

        void ReadRecordFrom(byte[] msg)
        {
            var r = new BinaryReader(new MemoryStream(msg));
            r.ReadByte();
            ReadRecord(r);
        }

        /// Every game: add the host's news to this player's profile (or, for a resume, catch up on the run's state).
        void ReadRecord(BinaryReader r)
        {
            var kind = (RecordKind)r.ReadByte();
            var me = Me;
            switch (kind)
            {
                case RecordKind.Resume:
                {
                    float time = r.ReadSingle();
                    var path = (PathId)r.ReadByte();
                    int from = r.ReadInt32(), to = r.ReadInt32();
                    int n = r.ReadByte();
                    var route = new List<PathId>();
                    for (int i = 0; i < n; i++)
                        route.Add((PathId)r.ReadByte());
                    if (IsHost)
                        return;
                    RunTime = time;
                    CurrentPath = path;
                    PathFrom = from;
                    PathTo = to;
                    Route.Clear();
                    Route.AddRange(route);
                    return;
                }
                case RecordKind.Boss:
                {
                    var boss = (EnemyType)r.ReadByte();
                    int wave = r.ReadInt32();
                    bool dev = r.ReadBoolean();
                    int recorded = r.ReadInt32();
                    if (me == null || dev || botTouched)
                        return;
                    Profile.BossBeaten(boss, Quick ? 0 : recorded);  // (Quick Play keeps its own best wave: QuickEnded)
                    if (Quick)
                        QuickBossesBeaten++;
                    runLastBossWave = Math.Max(runLastBossWave, wave);
                    AddUnlockToasts(Profile.CheckUnlocks());
                    SaveProfile();
                    return;
                }
                case RecordKind.Room:
                {
                    var room = (ChallengeKind)r.ReadByte();
                    bool won = r.ReadBoolean(), dev = r.ReadBoolean();
                    if (me == null || dev || botTouched)
                        return;
                    Profile.RoomEnded(room, won);
                    SaveProfile();
                    return;
                }
                case RecordKind.End:
                {
                    bool won = r.ReadBoolean();
                    int wave = r.ReadInt32(), score = r.ReadInt32();
                    bool endless = r.ReadBoolean(), dev = r.ReadBoolean();
                    float time = r.ReadSingle();
                    bool deathless = r.ReadBoolean();
                    int recorded = r.ReadInt32();  // (best wave, Recent runs and the curve; Rank XP keeps counting the track's waves)
                    int found = Math.Max(0, r.ReadInt32());
                    if (Quick)
                    {
                        if (me == null || dev || botTouched)
                            return;
                        QuickRecord(me, won, wave, time, found);
                        return;
                    }
                    if (me == null || dev || botTouched)
                        return;
                    if (runRecorded)
                    {
                        if (!won && Endless && Hunt == null)
                            EndlessRecord(me, wave, recorded, time, found);  // the Campaign's endless stretch after its win, fallen
                        return;
                    }
                    runRecorded = true;
                    if (Hunt != null)
                    {
                        AddUnlockToasts(Profile.HuntEnded(Hunt.Boss, won, time, Diff.Level == DifficultyLevel.Hard || Diff.Level == DifficultyLevel.Nightmare));  // Hunts.cs
                        int cleared = won ? Hunts.Length : Math.Max(0, HuntWave - 1);
                        LastGain = Profile.AwardXp(Rank.Award(false, cleared, won ? 1 : 0, me.Run.Revives, Mods, Diff.Level), HuntWave, won ? 3 : 2);
                        PayCores(Variants.HuntCores(won), found);
                        var hunted = Profile.Recent[0];  // (the hunt just added by AwardXp: its curve for the results screen and Recent runs)
                        hunted.Class = Arena.Profile.ClassKey(me.Role, me.Kit);
                        hunted.Mode = "Hunt";
                        hunted.Seconds = time;
                        hunted.Dealt = me.Run.DamageDealt; hunted.Taken = me.Run.DamageTaken; hunted.Heal = me.Run.HealingDone;
                        hunted.Players = Players.Count;
                        hunted.File = FinishTimeline(won, HuntWave, false, time, hunted.Class, hunted.Mods, "Hunt");  // RunTimeline.cs
                        SaveProfile();
                        return;
                    }
                    AddUnlockToasts(Profile.RunEnded(won, recorded, score, endless, Mods, me.Role, me.Kit, me.Run, time));
                    int wavesCleared = won || runLastBossWave >= wave ? wave : Math.Max(0, wave - 1);
                    LastGain = Profile.AwardXp(Rank.Award(won, wavesCleared, Rank.BossesIn(wavesCleared), me.Run.Revives, Mods, Diff.Level), recorded, won ? 1 : 0);
                    PayCores(Variants.RunCores(won, Rank.BossesIn(wavesCleared), Endless), found);
                    var logged = Profile.Recent[0];  // (the run just added by AwardXp)
                    logged.Class = Arena.Profile.ClassKey(me.Role, me.Kit);
                    logged.Mode = Endless ? "Endless" : deathless ? "Campaign · Deathless" : "Campaign";
                    logged.Seconds = time;
                    logged.Dealt = me.Run.DamageDealt; logged.Taken = me.Run.DamageTaken; logged.Heal = me.Run.HealingDone;
                    logged.Players = Players.Count;
                    logged.File = FinishTimeline(won, recorded, Endless, time, logged.Class, logged.Mods);  // RunTimeline.cs
                    endlessFromWave = wavesCleared;
                    endlessFromTime = time;
                    endlessFromCores = found;
                    SaveProfile();
                    return;
                }
            }
        }

        /// The Campaign's endless stretch after its win ended with the party down: the best endless wave, XP for the waves and
        /// bosses since the win, the Cores found since, and its own Recent runs card with the stretch's curve.
        void EndlessRecord(Player me, int wave, int recorded, float time, int found)
        {
            if (endlessRecorded)
                return;
            endlessRecorded = true;
            Profile.BestEndlessWave = Math.Max(Profile.BestEndlessWave, recorded);
            Profile.BestWave = Math.Max(Profile.BestWave, recorded);
            Profile.PlayTime += Math.Max(0f, time - endlessFromTime);
            int cleared = runLastBossWave >= wave ? wave : Math.Max(0, wave - 1);
            int newWaves = Math.Max(0, cleared - endlessFromWave);
            int newBosses = Math.Max(0, Rank.BossesIn(cleared) - Rank.BossesIn(endlessFromWave));
            LastGain = Profile.AwardXp(Rank.Award(false, newWaves, newBosses, 0, Mods, Diff.Level), recorded, 0);
            PayCores(Variants.RunCores(false, newBosses, true), Math.Max(0, found - endlessFromCores));
            var logged = Profile.Recent[0];
            logged.Class = Arena.Profile.ClassKey(me.Role, me.Kit);
            logged.Mode = "Endless";
            logged.Seconds = time;
            logged.Dealt = me.Run.DamageDealt; logged.Taken = me.Run.DamageTaken; logged.Heal = me.Run.HealingDone;
            logged.Players = Players.Count;
            logged.File = FinishTimeline(false, recorded, true, time, logged.Class, logged.Mods);  // RunTimeline.cs (the stretch since the win)
            AddUnlockToasts(Profile.CheckUnlocks());
            SaveProfile();
        }

        /// A Quick Play's end on this player's profile (QuickPlay.cs). It is recorded when its first boss falls (won) or the
        /// party does, and again if an endless stretch after the win ends: that one adds only the waves, bosses and time since.
        void QuickRecord(Player me, bool won, int wave, float time, int found)
        {
            bool first = !runRecorded;
            Tutorial.Active = false;
            FinishTutorial();  // (a first Quick Play recorded: the tutorial is over too)
            int cleared = won || runLastBossWave >= wave ? wave : Math.Max(0, wave - 1);
            int newWaves = cleared - quickCreditedWaves, newBosses = QuickBossesBeaten - quickCreditedBosses;
            if (!first && newWaves <= 0 && newBosses <= 0)
                return;
            runRecorded = true;
            bool beatFirst = QuickBossesBeaten > 0;
            AddUnlockToasts(Profile.QuickEnded(first, beatFirst, wave, newBosses, time - quickCreditedTime));
            LastGain = Profile.AwardXp(Rank.Award(first && won, Math.Max(0, newWaves), Math.Max(0, newBosses), first ? me.Run.Revives : 0, Mods, Diff.Level), wave, beatFirst ? 1 : 0);
            PayCores(Variants.RunCores(first && won, Math.Max(0, newBosses), false), first ? found : 0);
            var logged = Profile.Recent[0];
            logged.Class = Arena.Profile.ClassKey(me.Role, me.Kit);
            logged.Mode = Endless ? "Quick Play · Endless" : "Quick Play";
            logged.Seconds = time;
            logged.Dealt = me.Run.DamageDealt; logged.Taken = me.Run.DamageTaken; logged.Heal = me.Run.HealingDone;
            logged.Players = Players.Count;
            logged.File = FinishTimeline(won, wave, Endless, time, logged.Class, logged.Mods, logged.Mode);  // RunTimeline.cs (an endless stretch's entry: its own curve, since the win)
            quickCreditedWaves = Math.Max(quickCreditedWaves, cleared);
            quickCreditedBosses = QuickBossesBeaten;
            quickCreditedTime = time;
            SaveProfile();
        }

        /// Adds a finished run's Cores (its payout plus the drops found) to your profile and notes them on the results (LastGain).
        void PayCores(int paid, int found)
        {
            Profile.AddCores(paid + found);
            LastGain.Cores = paid + found;
            LastGain.CoresFound = found;
        }

        /// Saves the profile; if the disk refuses, warns the player once (the next successful save clears it).
        void SaveProfile()
        {
            if (Profile.Save())
            {
                profileWarned = false;
                return;
            }
            if (profileWarned)
                return;
            profileWarned = true;
            UnlockToasts.Add(new UnlockToast { Warning = true, T = 8f, Unlock = new UnlockDef { Id = "", Name = "Profile not saved", Desc = "Couldn't write profile.txt: check disk space and permissions" } });
        }
        bool profileWarned;

        void AddUnlockToasts(List<UnlockDef> fresh)
        {
            foreach (var u in fresh)
                UnlockToasts.Add(new UnlockToast { Unlock = u, T = 4f });
        }

        /// The saved seat that goes with this name (-1: none).
        public static int SeatFor(RunSave save, string name)
        {
            if (save == null)
                return -1;
            return save.Seats.FindIndex(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// "Unlocked: ..." popups (the front end draws and ages them).
    public class UnlockToast
    {
        public UnlockDef Unlock;
        public float T;
        public bool Warning;   // a "couldn't save" warning, drawn in red instead of the gold "UNLOCKED"
    }
}
