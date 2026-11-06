using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arena
{
    /// Something a profile earns for good once its condition is met (Unlocks.All). Earned is checked against the
    /// profile after every run and boss; what an unlock actually opens up is up to the code that asks IsUnlocked.
    public class UnlockDef
    {
        public string Id;          // stored in the profile; never change it once players have it
        public string Name, Desc;  // shown in the Records screen and the "Unlocked" toast
        public Func<Profile, bool> Earned;
    }

    /// One finished run's XP, kept for the Records screen (Profile.Recent, newest first).
    public class RankLog
    {
        public int Wave, Result, Xp, MultPct;   // Result: 0 lost, 1 won, 2 hunt lost, 3 hunt won, 4 left (quit) before it ended
        public string Mods = "";               // modifier names, '|' between
        // Recent runs (RecentRuns.md): what the strip shows. Older log lines have none of these.
        public string Class = "", Mode = "", File = "";  // File: the run's curve in Folder/runs (RunTimeline.cs), "" if none was kept
        public float Seconds, Dealt, Taken, Heal;
        public int Players;
    }

    public static class Unlocks
    {
        /// Every unlock in the game. Add entries here, e.g.
        ///   new UnlockDef { Id = "first-win", Name = "Champion", Desc = "Win a run", Earned = p => p.RunsWon > 0 },
        /// then check Profile.IsUnlocked("first-win") wherever it should matter (a lobby choice, a modifier...).
        public static readonly List<UnlockDef> All = new List<UnlockDef>
        {
            new UnlockDef { Id = "hunt-warden", Name = "Warden Hunt", Desc = "Beat the Warden in a run: hunt it any time from the lobby's Boss Hunt", Earned = p => p.Beaten(EnemyType.Boss) > 0 },
        };

        public static UnlockDef Get(string id) => All.Find(u => u.Id == id);
    }

    /// This computer's player's records across every run (the local player only: in co-op each player's own game
    /// keeps their own). Practice Range matches don't count, and neither does a run where dev tools or the bot
    /// were used. Saved as "key value" lines in Folder/profile.txt.
    public class Profile
    {
        /// 2: adds the closing "end" line, so a cut-off file is told apart from a whole one. Version 1 files (no "end") still load.
        /// 3: adds the "name" line (the profile's name, ProfileStore.cs). Older files load with no name.
        /// 4: adds the "perks" line (the perks equipped, Perks.cs). Older files load with none.
        /// 5: adds the "hint" line (how many launches the menu has pointed at the player card, and whether the profile was ever opened). Older files load with none.
        public const int FormatVersion = 5;

        /// The profile's name (also the lobby name). "" until ProfileStore gives it one.
        public string Name = "";

        /// Set by Parse/Load: the file's version, whether it read as a whole profile, and how many lines were skipped.
        public int LoadedVersion;
        public bool Valid;
        public int SkippedLines;
        /// What Load did about a bad file, for the log ("" none): it fell back to the backup, or set the file aside.
        public static string LoadNote = "";
        /// True when the last Save could not write (the front end warns the player).
        public static bool LastSaveFailed;

        public int RunsStarted, RunsWon, RunsLost;
        public int BestWave, BestScore, BestEndlessWave;
        public float PlayTime;  // seconds, across runs
        public int Kills, BossKills, Downs, Revives;
        public float DamageDealt, HealingDone;
        public readonly Dictionary<string, int> BossesBeaten = new Dictionary<string, int>();  // EnemyType name -> times beaten
        public readonly Dictionary<string, int> WinsAs = new Dictionary<string, int>();        // "Tank Knight" -> wins
        public readonly Dictionary<string, int> RunsAs = new Dictionary<string, int>();        // "Tank Knight" -> runs finished
        public readonly Dictionary<string, int> ModWins = new Dictionary<string, int>();       // RunMod name -> wins with it on
        public readonly Dictionary<string, int> RoomsPlayed = new Dictionary<string, int>();   // ChallengeKind name -> rooms played
        public readonly Dictionary<string, int> RoomsWon = new Dictionary<string, int>();      // ChallengeKind name -> rooms won
        public readonly Dictionary<string, int> HuntsPlayed = new Dictionary<string, int>();   // EnemyType name -> hunts finished
        public readonly Dictionary<string, int> HuntsWon = new Dictionary<string, int>();      // EnemyType name -> hunts won
        public readonly Dictionary<string, int> HuntsWonHard = new Dictionary<string, int>();  // EnemyType name -> hunts won on the Hard or Nightmare preset (a perk's second unlock, Perks.cs)
        public readonly Dictionary<string, float> HuntBest = new Dictionary<string, float>();  // EnemyType name -> fastest win (seconds)
        public readonly HashSet<string> Unlocked = new HashSet<string>();
        /// The perks equipped, by PerkId name, in slot order (Perks.cs). Perks.Clean keeps it to what is unlocked.
        public readonly List<string> PerkNames = new List<string>();

        // Rank (Rank.cs, Docs/Rank.md): total XP, the cosmetics equipped (ids from Rank.Cosmetics; "" none) and the last few runs' XP.
        public long Xp;
        public string Frame = "", Trail = "", Background = "", CardBg = "", NameFx = "", AimLine = "", Cursor = "";
        public readonly List<RankLog> Recent = new List<RankLog>();
        public const int RecentKept = 20;  // (the Rank page lists the newest RankPageRuns)
        public const int RankPageRuns = 6;

        // The main menu's "your profile" nudge beside the player card: shown until the profile has been opened once, for at most HintMaxLaunches launches.
        public int HintLaunches;
        public bool ProfileOpened;
        public const int HintMaxLaunches = 5;
        public bool ShowProfileHint => !ProfileOpened && HintLaunches <= HintMaxLaunches;
        /// The menu came up for this launch (counted once per launch by the front end).
        public void NoteMenuLaunch() { if (HintLaunches < 1000) HintLaunches++; }

        public static string Folder;  // null: profiles aren't kept (tests set a temp folder)
        static Profile current;

        /// The profile on this computer, loaded on first use.
        public static Profile Current => current ?? (current = Load());

        /// Tests: forget the loaded profile so the next Current reads Folder again.
        public static void Reload() => current = null;

        public bool IsUnlocked(string id) => Unlocked.Contains(id);

        public int RankNow => Rank.Of(Xp).Rank;

        /// Adds a finished run's or hunt's XP, logs it, and says what it unlocked. result: 0 lost, 1 won, 2 hunt lost, 3 hunt won.
        public RankGain AwardXp(XpAward a, int wave, int result)
        {
            var gain = new RankGain { Award = a, Before = Xp, RankBefore = Rank.Of(Xp).Rank };
            Xp += a.Total;
            gain.After = Xp;
            gain.RankAfter = Rank.Of(Xp).Rank;
            for (int r = gain.RankBefore + 1; r <= gain.RankAfter; r++)
                gain.Unlocked.AddRange(Rank.RewardsAt(r));
            Recent.Insert(0, new RankLog { Wave = wave, Result = result, Xp = a.Total, MultPct = (int)Math.Round(a.ModMult * a.DiffMult * 100f), Mods = a.Mods });
            if (Recent.Count > RecentKept)
                Recent.RemoveRange(RecentKept, Recent.Count - RecentKept);
            return gain;
        }

        /// A run you left before it ended (no XP): it still shows in Recent runs, result 4. Returns the log line to fill in.
        public RankLog AddQuitLog(int wave, string mods)
        {
            var log = new RankLog { Wave = wave, Result = 4, MultPct = 100, Mods = mods ?? "" };
            Recent.Insert(0, log);
            if (Recent.Count > RecentKept)
                Recent.RemoveRange(RecentKept, Recent.Count - RecentKept);
            return log;
        }

        /// Equips a cosmetic you've reached the rank for (id "" takes the slot off). False: not yours yet, or not that kind.
        public bool Equip(CosmeticKind kind, string id)
        {
            if (kind == CosmeticKind.Emote)
                return false;  // emotes aren't equipped: all the ones your rank has are on the emote bar
            if (id != "")
            {
                var c = Rank.Cosmetic(id);
                if (c == null || c.Kind != kind || !Rank.Owns(RankNow, id))
                    return false;
            }
            if (kind == CosmeticKind.Frame) Frame = id;
            else if (kind == CosmeticKind.Trail) Trail = id;
            else if (kind == CosmeticKind.CardBackground) CardBg = id;
            else if (kind == CosmeticKind.NameEffect) NameFx = id;
            else if (kind == CosmeticKind.AimLine) AimLine = id;
            else if (kind == CosmeticKind.Cursor) Cursor = id;
            else Background = id;
            return true;
        }

        public string Equipped(CosmeticKind kind) => kind == CosmeticKind.Emote ? "" : kind == CosmeticKind.Frame ? Frame : kind == CosmeticKind.Trail ? Trail : kind == CosmeticKind.CardBackground ? CardBg : kind == CosmeticKind.NameEffect ? NameFx : kind == CosmeticKind.AimLine ? AimLine : kind == CosmeticKind.Cursor ? Cursor : Background;

        public static string ClassKey(Role role, Kit kit) => Defs.KitName(kit) + " " + Defs.Get(role).Name;  // "Knight Tank", as the lobby says it

        static int Get(Dictionary<string, int> d, string k) { int v; return d.TryGetValue(k, out v) ? v : 0; }
        static void Bump(Dictionary<string, int> d, string k) => d[k] = Get(d, k) + 1;

        public int HuntWins(EnemyType boss) => Get(HuntsWon, boss.ToString());
        public int HuntWinsHard(EnemyType boss) => Get(HuntsWonHard, boss.ToString());

        /// How many different bosses' hunts have been won.
        public int HuntsWonCount { get { int n = 0; foreach (var kv in HuntsWon) if (kv.Value > 0) n++; return n; } }

        public int Beaten(EnemyType boss) => Get(BossesBeaten, boss.ToString());
        public int RoomPlayed(ChallengeKind k) => Get(RoomsPlayed, k.ToString());
        public int RoomWon(ChallengeKind k) => Get(RoomsWon, k.ToString());
        public int HuntPlayed(EnemyType boss) => Get(HuntsPlayed, boss.ToString());
        public int HuntWon(EnemyType boss) => Get(HuntsWon, boss.ToString());
        public float HuntBestTime(EnemyType boss) { float v; return HuntBest.TryGetValue(boss.ToString(), out v) ? v : 0f; }

        /// A Boss Hunt finished (won: the boss beaten). Hunts aren't runs: they don't count towards the run records.
        public List<UnlockDef> HuntEnded(EnemyType boss, bool won, float time, bool hard = false)
        {
            string k = boss.ToString();
            Bump(HuntsPlayed, k);
            if (won)
            {
                Bump(HuntsWon, k);
                if (hard)
                    Bump(HuntsWonHard, k);  // won on Hard or Nightmare
                float best = HuntBestTime(boss);
                if (best <= 0f || time < best)
                    HuntBest[k] = time;
            }
            PlayTime += time;
            return CheckUnlocks();
        }

        /// A challenge room ended (Challenges.cs).
        public void RoomEnded(ChallengeKind k, bool won)
        {
            Bump(RoomsPlayed, k.ToString());
            if (won)
                Bump(RoomsWon, k.ToString());
        }

        // --- What happened -------------------------------------------------------------------------

        public void RunStarted() => RunsStarted++;

        /// Wins (wave 50) with a role, over every class.
        public int WinsWithRole(Role role)
        {
            int n = 0;
            foreach (var kv in WinsAs)
                if (kv.Key.EndsWith(" " + Defs.Get(role).Name, StringComparison.Ordinal))  // (keys read "Knight Tank", ClassKey)
                    n += kv.Value;
            return n;
        }

        /// Wildcard unlocks once you have won a run as a Tank, a Healer and a DPS (a co-op win counts). It stays out of the lobby, the
        /// Practice Range and the loadout pickers until then (Docs/Wildcard.md).
        public bool WildcardUnlocked => WinsWithRole(Role.Tank) > 0 && WinsWithRole(Role.Healer) > 0 && WinsWithRole(Role.Dps) > 0;

        public void BossBeaten(EnemyType boss, int wave)
        {
            Bump(BossesBeaten, boss.ToString());
            BestWave = Math.Max(BestWave, wave);
        }

        /// A run finished: won (beat wave 50) or lost (everyone down). Returns unlocks newly earned.
        public List<UnlockDef> RunEnded(bool won, int wave, int score, bool endless, RunMod mods, Role role, Kit kit, RunStats stats, float time)
        {
            if (won) RunsWon++;
            else RunsLost++;
            if (endless)
                BestEndlessWave = Math.Max(BestEndlessWave, wave);
            BestWave = Math.Max(BestWave, wave);
            BestScore = Math.Max(BestScore, score);
            PlayTime += time;
            string cls = ClassKey(role, kit);
            Bump(RunsAs, cls);
            if (won)
            {
                Bump(WinsAs, cls);
                foreach (RunMod m in Enum.GetValues(typeof(RunMod)))
                    if (m != RunMod.None && (mods & m) == m && CountBits((int)m) == 1)
                        Bump(ModWins, m.ToString());
            }
            if (stats != null)
            {
                Kills += stats.Kills;
                BossKills += stats.BossKills;
                Downs += stats.Downs;
                Revives += stats.Revives;
                DamageDealt += stats.DamageDealt;
                HealingDone += stats.HealingDone;
            }
            return CheckUnlocks();
        }

        static int CountBits(int v) { int n = 0; while (v != 0) { v &= v - 1; n++; } return n; }

        /// Marks every unlock whose condition is now met; returns the ones just earned.
        public List<UnlockDef> CheckUnlocks()
        {
            var fresh = new List<UnlockDef>();
            foreach (var u in Unlocks.All)
                if (!Unlocked.Contains(u.Id) && u.Earned != null && u.Earned(this))
                {
                    Unlocked.Add(u.Id);
                    fresh.Add(u);
                }
            return fresh;
        }

        // --- Saving ----------------------------------------------------------------------------------

        public string ToText()
        {
            var sb = new StringBuilder();
            RunSave.Line(sb, "arena-profile", FormatVersion);
            if (Name.Length > 0) RunSave.Line(sb, "name", Name);
            RunSave.Line(sb, "runs", RunsStarted + " " + RunsWon + " " + RunsLost);
            RunSave.Line(sb, "best", BestWave + " " + BestScore + " " + BestEndlessWave);
            RunSave.Line(sb, "time", PlayTime);
            RunSave.Line(sb, "totals", Kills + " " + BossKills + " " + Downs + " " + Revives + " " + RunSave.F(DamageDealt) + " " + RunSave.F(HealingDone));
            foreach (var kv in BossesBeaten) RunSave.Line(sb, "boss", kv.Key + " " + kv.Value);
            foreach (var kv in RunsAs) RunSave.Line(sb, "played", kv.Value + " " + kv.Key);
            foreach (var kv in WinsAs) RunSave.Line(sb, "won", kv.Value + " " + kv.Key);
            foreach (var kv in ModWins) RunSave.Line(sb, "modwin", kv.Key + " " + kv.Value);
            foreach (var kv in RoomsPlayed) RunSave.Line(sb, "room", kv.Key + " " + kv.Value + " " + Get(RoomsWon, kv.Key));
            foreach (var kv in HuntsPlayed) RunSave.Line(sb, "hunt", kv.Key + " " + kv.Value + " " + Get(HuntsWon, kv.Key) + " " + RunSave.F(HuntBest.ContainsKey(kv.Key) ? HuntBest[kv.Key] : 0f) + " " + Get(HuntsWonHard, kv.Key));
            foreach (var u in Unlocked) RunSave.Line(sb, "unlocked", u);
            if (PerkNames.Count > 0) RunSave.Line(sb, "perks", string.Join(",", PerkNames));
            if (HintLaunches > 0 || ProfileOpened) RunSave.Line(sb, "hint", HintLaunches + " " + (ProfileOpened ? 1 : 0));
            RunSave.Line(sb, "xp", Xp);
            RunSave.Line(sb, "equip", Dash(Frame) + " " + Dash(Trail) + " " + Dash(Background) + " " + Dash(CardBg) + " " + Dash(NameFx) + " " + Dash(AimLine) + " " + Dash(Cursor));
            foreach (var l in Recent) RunSave.Line(sb, "rlog", l.Wave + " " + l.Result + " " + l.Xp + " " + l.MultPct + " " + Dash(l.Mods.Replace(' ', '_')) + " " + Dash(l.Class.Replace(' ', '_')) + " " + Dash(l.Mode) + " " + RunSave.F(l.Seconds) + " " + RunSave.F(l.Dealt) + " " + RunSave.F(l.Taken) + " " + RunSave.F(l.Heal) + " " + l.Players + " " + Dash(l.File));
            RunSave.Line(sb, "end", 1);
            return sb.ToString();
        }

        static string Dash(string s) => string.IsNullOrEmpty(s) ? "-" : s;
        static string UnDash(string s) => s == "-" ? "" : s;

        public static Profile Parse(string text)
        {
            var p = new Profile();
            bool sawHeader = false, sawEnd = false;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                int sp = line.IndexOf(' ');
                if (sp < 0)
                    continue;
                string key = line.Substring(0, sp), val = line.Substring(sp + 1).Trim();
                try
                {
                    var f = val.Split(' ');
                    switch (key)
                    {
                        case "arena-profile": p.LoadedVersion = RunSave.I(f[0]); sawHeader = true; break;
                        case "end": sawEnd = true; break;
                        case "name": p.Name = val.Trim(); break;
                        case "runs": p.RunsStarted = RunSave.I(f[0]); p.RunsWon = RunSave.I(f[1]); p.RunsLost = RunSave.I(f[2]); break;
                        case "best": p.BestWave = RunSave.I(f[0]); p.BestScore = RunSave.I(f[1]); p.BestEndlessWave = RunSave.I(f[2]); break;
                        case "time": p.PlayTime = RunSave.Fl(val); break;
                        case "totals":
                            p.Kills = RunSave.I(f[0]); p.BossKills = RunSave.I(f[1]); p.Downs = RunSave.I(f[2]); p.Revives = RunSave.I(f[3]);
                            p.DamageDealt = RunSave.Fl(f[4]); p.HealingDone = RunSave.Fl(f[5]);
                            break;
                        case "boss": p.BossesBeaten[f[0]] = RunSave.I(f[1]); break;
                        case "played": p.RunsAs[val.Substring(val.IndexOf(' ') + 1)] = RunSave.I(f[0]); break;
                        case "won": p.WinsAs[val.Substring(val.IndexOf(' ') + 1)] = RunSave.I(f[0]); break;
                        case "modwin": p.ModWins[f[0]] = RunSave.I(f[1]); break;
                        case "room": p.RoomsPlayed[f[0]] = RunSave.I(f[1]); p.RoomsWon[f[0]] = RunSave.I(f[2]); break;
                        case "hunt":
                            p.HuntsPlayed[f[0]] = RunSave.I(f[1]); p.HuntsWon[f[0]] = RunSave.I(f[2]);
                            if (RunSave.Fl(f[3]) > 0f) p.HuntBest[f[0]] = RunSave.Fl(f[3]);
                            if (f.Length > 4) p.HuntsWonHard[f[0]] = RunSave.I(f[4]);  // (older files have four fields)
                            break;
                        case "unlocked": p.Unlocked.Add(val); break;
                        case "perks": foreach (var n in val.Split(',')) if (n.Length > 0) p.PerkNames.Add(n.Trim()); break;
                        case "hint": p.HintLaunches = Math.Max(0, RunSave.I(f[0])); p.ProfileOpened = f.Length > 1 && RunSave.I(f[1]) != 0; break;
                        case "xp": p.Xp = Math.Max(0, long.Parse(f[0], System.Globalization.CultureInfo.InvariantCulture)); break;
                        case "equip": p.Frame = UnDash(f[0]); p.Trail = UnDash(f[1]); p.Background = UnDash(f[2]); if (f.Length > 3) p.CardBg = UnDash(f[3]); if (f.Length > 4) p.NameFx = UnDash(f[4]); if (f.Length > 5) p.AimLine = UnDash(f[5]); if (f.Length > 6) p.Cursor = UnDash(f[6]); break;
                        case "rlog":
                            if (p.Recent.Count < RecentKept)
                            {
                                var log = new RankLog { Wave = RunSave.I(f[0]), Result = RunSave.I(f[1]), Xp = RunSave.I(f[2]), MultPct = RunSave.I(f[3]), Mods = f.Length > 4 ? UnDash(f[4]).Replace('_', ' ') : "" };
                                if (f.Length > 11)  // (the Recent runs fields; older lines stop after the modifiers)
                                {
                                    log.Class = UnDash(f[5]).Replace('_', ' '); log.Mode = UnDash(f[6]); log.Seconds = RunSave.Fl(f[7]);
                                    log.Dealt = RunSave.Fl(f[8]); log.Taken = RunSave.Fl(f[9]); log.Heal = RunSave.Fl(f[10]); log.Players = RunSave.I(f[11]);
                                    log.File = f.Length > 12 ? UnDash(f[12]) : "";
                                }
                                p.Recent.Add(log);
                            }
                            break;
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is OverflowException)
                {
                    p.SkippedLines++;  // a damaged line: skip it (Load keeps a copy of the file before it is overwritten)
                }
            }
            p.Valid = sawHeader && (p.LoadedVersion < 2 || sawEnd);
            if (p.Valid)
                Migrate(p);
            return p;
        }

        /// Brings an older file's data up to FormatVersion. Add a step per version when the format changes.
        static void Migrate(Profile p)
        {
            // 1 -> 2 only added the "end" line, 2 -> 3 the name, 3 -> 4 the perks, 4 -> 5 the hint line: nothing to convert.
            Perks.Clean(p);  // drop a perk that isn't unlocked (a hand-edited file) or no longer fits the slots
            p.LoadedVersion = FormatVersion;
        }

        static string FilePath => Path.Combine(Folder, "profile.txt");
        static string BackupPath => FilePath + ".bak";

        /// Reads one file: null if it is missing, unreadable, or doesn't read as a whole profile.
        static Profile TryRead(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var p = Parse(File.ReadAllText(path));
                return p.Valid ? p : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        /// Moves a bad file out of the way (never overwritten or deleted), so the next Save can't destroy it.
        static void SetAside(string path, string label)
        {
            try
            {
                if (File.Exists(path))
                    File.Copy(path, Path.Combine(Folder, "profile." + label + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt"), true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static Profile Load()
        {
            LoadNote = "";
            if (Folder == null)
                return new Profile();
            bool mainExists = false;
            try { mainExists = File.Exists(FilePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            var p = TryRead(FilePath);
            if (p != null)
            {
                if (p.SkippedLines > 0)
                {
                    SetAside(FilePath, "damaged");  // keep the lines that were dropped
                    LoadNote = "profile.txt had " + p.SkippedLines + " damaged line(s); a copy was kept";
                }
                return p;
            }
            // Missing, cut off or unreadable: try the backup from the save before last.
            var b = TryRead(BackupPath);
            if (mainExists)
                SetAside(FilePath, "corrupt");
            if (b != null)
            {
                LoadNote = mainExists ? "profile.txt was damaged; restored from profile.txt.bak" : "profile.txt was missing; restored from profile.txt.bak";
                return b;
            }
            if (mainExists)
                LoadNote = "profile.txt was damaged and there is no backup; a copy was kept and a new profile started";
            return new Profile();
        }

        /// Writes it (temp file first, then swapped in with the old file kept as profile.txt.bak, so a crash can't
        /// leave half a profile or none). False if it couldn't write; LastSaveFailed says so for the front end.
        public bool Save()
        {
            if (Folder == null)
                return true;
            bool ok = SaveTo(Folder);
            LastSaveFailed = !ok;
            return ok;
        }

        /// Writes it into any profile folder (ProfileStore uses this for profiles that aren't the active one).
        public bool SaveTo(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                SaveStore.SafeWrite(Path.Combine(folder, "profile.txt"), ToText(), Path.Combine(folder, "profile.txt.bak"));
                return true;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return false;
        }
    }
}
