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

    public static class Unlocks
    {
        /// Every unlock in the game. None yet: add entries here, e.g.
        ///   new UnlockDef { Id = "first-win", Name = "Champion", Desc = "Win a run", Earned = p => p.RunsWon > 0 },
        /// then check Profile.IsUnlocked("first-win") wherever it should matter (a lobby choice, a modifier...).
        public static readonly List<UnlockDef> All = new List<UnlockDef>();

        public static UnlockDef Get(string id) => All.Find(u => u.Id == id);
    }

    /// This computer's player's records across every run (the local player only: in co-op each player's own game
    /// keeps their own). Practice Range matches don't count, and neither does a run where dev tools or the bot
    /// were used. Saved as "key value" lines in Folder/profile.txt.
    public class Profile
    {
        public const int FormatVersion = 1;

        public int RunsStarted, RunsWon, RunsLost;
        public int BestWave, BestScore, BestEndlessWave;
        public float PlayTime;  // seconds, across runs
        public int Kills, BossKills, Downs, Revives;
        public float DamageDealt, HealingDone;
        public readonly Dictionary<string, int> BossesBeaten = new Dictionary<string, int>();  // EnemyType name -> times beaten
        public readonly Dictionary<string, int> WinsAs = new Dictionary<string, int>();        // "Tank Knight" -> wins
        public readonly Dictionary<string, int> RunsAs = new Dictionary<string, int>();        // "Tank Knight" -> runs finished
        public readonly Dictionary<string, int> ModWins = new Dictionary<string, int>();       // RunMod name -> wins with it on
        public readonly HashSet<string> Unlocked = new HashSet<string>();

        public static string Folder;  // null: profiles aren't kept (tests set a temp folder)
        static Profile current;

        /// The profile on this computer, loaded on first use.
        public static Profile Current => current ?? (current = Load());

        /// Tests: forget the loaded profile so the next Current reads Folder again.
        public static void Reload() => current = null;

        public bool IsUnlocked(string id) => Unlocked.Contains(id);

        public static string ClassKey(Role role, Kit kit) => Defs.KitName(kit) + " " + Defs.Get(role).Name;  // "Knight Tank", as the lobby says it

        static int Get(Dictionary<string, int> d, string k) { int v; return d.TryGetValue(k, out v) ? v : 0; }
        static void Bump(Dictionary<string, int> d, string k) => d[k] = Get(d, k) + 1;

        public int Beaten(EnemyType boss) => Get(BossesBeaten, boss.ToString());

        // --- What happened -------------------------------------------------------------------------

        public void RunStarted() => RunsStarted++;

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
            RunSave.Line(sb, "runs", RunsStarted + " " + RunsWon + " " + RunsLost);
            RunSave.Line(sb, "best", BestWave + " " + BestScore + " " + BestEndlessWave);
            RunSave.Line(sb, "time", PlayTime);
            RunSave.Line(sb, "totals", Kills + " " + BossKills + " " + Downs + " " + Revives + " " + RunSave.F(DamageDealt) + " " + RunSave.F(HealingDone));
            foreach (var kv in BossesBeaten) RunSave.Line(sb, "boss", kv.Key + " " + kv.Value);
            foreach (var kv in RunsAs) RunSave.Line(sb, "played", kv.Value + " " + kv.Key);
            foreach (var kv in WinsAs) RunSave.Line(sb, "won", kv.Value + " " + kv.Key);
            foreach (var kv in ModWins) RunSave.Line(sb, "modwin", kv.Key + " " + kv.Value);
            foreach (var u in Unlocked) RunSave.Line(sb, "unlocked", u);
            return sb.ToString();
        }

        public static Profile Parse(string text)
        {
            var p = new Profile();
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
                        case "unlocked": p.Unlocked.Add(val); break;
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is OverflowException)
                {
                    // a damaged line: skip it
                }
            }
            return p;
        }

        static string FilePath => Path.Combine(Folder, "profile.txt");

        static Profile Load()
        {
            if (Folder == null)
                return new Profile();
            try
            {
                return File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath)) : new Profile();
            }
            catch (IOException) { return new Profile(); }
            catch (UnauthorizedAccessException) { return new Profile(); }
        }

        /// Writes it (temp file first, then swapped in, so a crash can't leave half a profile).
        public void Save()
        {
            if (Folder == null)
                return;
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, ToText());
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
