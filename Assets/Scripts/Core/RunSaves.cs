using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Saved runs and the player's profile, on the game's side (SaveData.cs and Profile.cs hold the data).
    ///   Saving: the host writes the run to its save slot as each wave starts (everyone's picks made, the path
    ///     chosen, the shop closed), and when the match stops between waves. A run that ends (won or lost) loses
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
        public Profile ProfileOverride;   // tests: a profile per game instead of the computer's
        public bool Unrecorded;           // a Boss Lab fight: never saved, and not in anyone's records (BossLab.cs)
        public Profile Profile => ProfileOverride ?? Profile.Current;
        public readonly List<UnlockToast> UnlockToasts = new List<UnlockToast>();  // the front end draws and ages them

        enum RecordKind : byte { Boss, End, Resume }

        bool Saveable => IsHost && Practice == null && !ShowcaseMode && !Unrecorded && RunId != null;

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
                Id = RunId, SavedAt = DateTime.UtcNow, Wave = Wave, Score = Score, Mods = Mods, Endless = Endless, DevUsed = devTouched,
                RunTime = RunTime, CurrentPath = CurrentPath, PathFrom = PathFrom, PathTo = PathTo, LastPath = lastPath,
            };
            save.Route.AddRange(Route);
            foreach (var p in Players.Values)
            {
                LobbyEntry e;
                session.Lobby.TryGetValue(p.Id, out e);
                var seat = new SeatSave
                {
                    Name = p.Name, Role = p.Role, Kit = p.Kit, Weapon = e != null ? e.Weapon : 0, Ability = e != null ? e.Ability : 0,
                    Level = p.Level, Xp = p.Xp, Rerolls = p.Rerolls, Gold = p.Gold, ReviveToken = p.ReviveToken,
                    HpFrac = p.Downed || p.MaxHp <= 0f ? 0.4f : p.Hp / p.MaxHp, Run = p.Run,
                };
                foreach (int t in p.Taken)
                    seat.Taken.Add(Upgrades.All[t].Name);
                for (int k = 0; k < p.BonusKeys.Length; k++)
                    seat.BonusKeys[k] = p.BonusKeys[k] == AbilityId.None ? "" : p.BonusKeys[k].ToString();
                foreach (var kv in p.MasteryXp)
                    if (kv.Key != AbilityId.None && kv.Value > 0f)
                        seat.Mastery[kv.Key.ToString()] = kv.Value;
                save.Seats.Add(seat);
            }
            bool ok = SaveStore.Write(save);
            if (ok)
                DoLog("run saved before wave " + (Wave + 1));
            return ok;
        }

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
                    int u = Array.FindIndex(Upgrades.All, x => x.Name == name);
                    if (u >= 0)
                        p.Taken.Add(u);
                }
                for (int k = 0; k < p.BonusKeys.Length; k++)
                {
                    AbilityId id;
                    p.BonusKeys[k] = Enum.TryParse(s.BonusKeys[k], out id) ? id : AbilityId.None;
                }
                p.MasteryXp.Clear();
                foreach (var kv in s.Mastery)
                {
                    AbilityId id;
                    if (Enum.TryParse(kv.Key, out id))
                        p.MasteryXp[id] = kv.Value;
                }
                p.Level = s.Level;
                p.Xp = s.Xp;
                p.Rerolls = s.Rerolls;
                p.Gold = s.Gold;
                p.ReviveToken = s.ReviveToken;
                p.Run = s.Run;
                p.PendingPicks = 0;
                p.LegendaryQueue.Clear();
                p.PendingMods = 0;
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
            Endless = save.Endless;
            RunTime = save.RunTime;
            devTouched = save.DevUsed;
            CurrentPath = save.CurrentPath;
            PathFrom = save.PathFrom;
            PathTo = save.PathTo;
            lastPath = save.LastPath;
            Route.Clear();
            Route.AddRange(save.Route);
            var w = BeginRecord(RecordKind.Resume);
            w.Write(RunTime);
            w.Write((byte)CurrentPath);
            w.Write(PathFrom);
            w.Write(PathTo);
            w.Write((byte)Route.Count);
            foreach (var r in Route)
                w.Write((byte)r);
            session.Broadcast(Proto.End(w));
            DoLog("resumed a saved run at wave " + (Wave + 1));
            SendSnapshot();
        }

        /// Every game, as a match starts: a fresh run counts as started (a resumed one already did).
        void BeginRunRecord(bool resumed)
        {
            Resumed = resumed;
            botTouched = Bot;
            runRecorded = false;
            if (IsHost)
            {
                devTouched = false;
                RunId = !RecordsRuns ? null : resumed && ResumeFrom != null ? ResumeFrom.Id : RunSave.NewId();
            }
            if (!resumed && RecordsRuns && Me != null)
            {
                Profile.RunStarted();
                Profile.Save();
            }
        }

        /// Host: the run ended (won or lost); its save goes, and everyone's profile hears about it.
        void RunOver(bool won)
        {
            if (!IsHost)
                return;
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
            var msg = Proto.End(w);
            session.Broadcast(msg);
            ReadRecordFrom(msg);
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
                    if (me == null || dev || botTouched)
                        return;
                    Profile.BossBeaten(boss, wave);
                    AddUnlockToasts(Profile.CheckUnlocks());
                    Profile.Save();
                    return;
                }
                case RecordKind.End:
                {
                    bool won = r.ReadBoolean();
                    int wave = r.ReadInt32(), score = r.ReadInt32();
                    bool endless = r.ReadBoolean(), dev = r.ReadBoolean();
                    float time = r.ReadSingle();
                    if (me == null || dev || botTouched || runRecorded)
                        return;
                    runRecorded = true;
                    AddUnlockToasts(Profile.RunEnded(won, wave, score, endless, Mods, me.Role, me.Kit, me.Run, time));
                    Profile.Save();
                    return;
                }
            }
        }

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
    }
}
