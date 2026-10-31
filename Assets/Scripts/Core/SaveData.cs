using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Arena
{
    /// One player's seat in a saved run: their loadout and everything they've earned so far.
    public class SeatSave
    {
        public string Name = "Player";
        public Role Role;
        public Kit Kit;
        public int Weapon, Ability;
        public readonly List<string> Taken = new List<string>();   // upgrade names, in the order picked
        public readonly string[] BonusKeys = { "", "", "" };        // abilities on Q, E, Shift (by name)
        public int Level = 1, Rerolls, Gold;
        public float Xp, HpFrac = 1f;
        public bool ReviveToken;
        public readonly Dictionary<string, float> Mastery = new Dictionary<string, float>();  // ability name -> XP
        public RunStats Run = new RunStats();

        /// "Tank Knight, level 12" for lists.
        public string Summary => Role + " " + Defs.KitName(Kit) + ", level " + Level;
    }

    /// A run saved between waves (the host's copy): resuming starts the next wave with everyone's upgrades, levels,
    /// mastery and gold back. Written as plain "key value" lines, with upgrades and abilities by name, so a save
    /// keeps working when the game's lists grow or get reordered (anything no longer in the game is skipped).
    public class RunSave
    {
        public const int FormatVersion = 1;

        public string Id = "";        // its file name (without .run); a run keeps it from start to finish
        public DateTime SavedAt = DateTime.UtcNow;
        public int Wave;              // waves cleared: resuming starts wave Wave + 1
        public int Score;
        public RunMod Mods;
        public bool Endless, DevUsed;
        public float RunTime;
        public PathId CurrentPath, LastPath;
        public int PathFrom, PathTo;
        public readonly List<PathId> Route = new List<PathId>();
        public readonly List<SeatSave> Seats = new List<SeatSave>();

        /// "Wave 14 · 2 players · 23 min" for lists.
        public string Summary => "Wave " + (Wave + 1) + (Endless ? " (endless)" : "") + " · " + Seats.Count + (Seats.Count == 1 ? " player" : " players")
                                 + " · " + (int)(RunTime / 60f) + " min";

        public static string NewId() => DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);

        public string ToText()
        {
            var sb = new StringBuilder();
            Line(sb, "arena-run", FormatVersion);
            Line(sb, "saved", SavedAt.ToString("o", CultureInfo.InvariantCulture));
            Line(sb, "wave", Wave);
            Line(sb, "score", Score);
            Line(sb, "mods", (int)Mods);
            Line(sb, "endless", Endless ? 1 : 0);
            Line(sb, "dev", DevUsed ? 1 : 0);
            Line(sb, "time", RunTime);
            Line(sb, "path", CurrentPath + " " + PathFrom + " " + PathTo);
            Line(sb, "lastpath", LastPath);
            Line(sb, "route", string.Join(",", Route));
            foreach (var s in Seats)
            {
                sb.Append("seat\n");
                Line(sb, "name", s.Name);
                Line(sb, "class", s.Role + " " + s.Kit + " " + s.Weapon + " " + s.Ability);
                Line(sb, "level", s.Level);
                Line(sb, "xp", s.Xp);
                Line(sb, "rerolls", s.Rerolls);
                Line(sb, "gold", s.Gold);
                Line(sb, "token", s.ReviveToken ? 1 : 0);
                Line(sb, "hp", s.HpFrac);
                Line(sb, "keys", string.Join(",", s.BonusKeys));
                foreach (var t in s.Taken)
                    Line(sb, "upgrade", t);
                foreach (var kv in s.Mastery)
                    Line(sb, "mastery", kv.Key + " " + F(kv.Value));
                var r = s.Run;
                Line(sb, "stats", string.Join(" ", new[] { F(r.DamageDealt), F(r.DamageTaken), F(r.HealingDone), F(r.BiggestHit),
                    r.Kills.ToString(CultureInfo.InvariantCulture), r.EliteKills.ToString(CultureInfo.InvariantCulture), r.BossKills.ToString(CultureInfo.InvariantCulture),
                    r.Downs.ToString(CultureInfo.InvariantCulture), r.Revives.ToString(CultureInfo.InvariantCulture), r.ShotsFired.ToString(CultureInfo.InvariantCulture),
                    r.ShotsHit.ToString(CultureInfo.InvariantCulture), r.AbilitiesUsed.ToString(CultureInfo.InvariantCulture) }));
            }
            return sb.ToString();
        }

        /// Reads a save; null if it isn't one. Unknown keys and values are skipped, so newer saves still mostly load.
        public static RunSave Parse(string text, string id = "")
        {
            var save = new RunSave { Id = id };
            SeatSave seat = null;
            bool header = false;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                int sp = line.IndexOf(' ');
                string key = sp < 0 ? line : line.Substring(0, sp), val = sp < 0 ? "" : line.Substring(sp + 1).Trim();
                try
                {
                    switch (key)
                    {
                        case "arena-run": header = true; break;
                        case "saved": save.SavedAt = DateTime.Parse(val, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind); break;
                        case "wave": save.Wave = I(val); break;
                        case "score": save.Score = I(val); break;
                        case "mods": save.Mods = (RunMod)I(val); break;
                        case "endless": save.Endless = I(val) != 0; break;
                        case "dev": save.DevUsed = I(val) != 0; break;
                        case "time": save.RunTime = Fl(val); break;
                        case "path":
                        {
                            var p = val.Split(' ');
                            save.CurrentPath = Enum<PathId>(p[0]);
                            save.PathFrom = I(p[1]);
                            save.PathTo = I(p[2]);
                            break;
                        }
                        case "lastpath": save.LastPath = Enum<PathId>(val); break;
                        case "route":
                            foreach (var p in val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                                save.Route.Add(Enum<PathId>(p));
                            break;
                        case "seat": seat = new SeatSave(); save.Seats.Add(seat); break;
                        default:
                            if (seat != null)
                                ReadSeat(seat, key, val);
                            break;
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is OverflowException || e is ArgumentException)
                {
                    // a damaged line: skip it
                }
            }
            return header && save.Seats.Count > 0 ? save : null;
        }

        static void ReadSeat(SeatSave s, string key, string val)
        {
            switch (key)
            {
                case "name": s.Name = Session.CleanName(val); break;
                case "class":
                {
                    var p = val.Split(' ');
                    s.Role = Enum<Role>(p[0]);
                    s.Kit = Enum<Kit>(p[1]);
                    s.Weapon = Math.Max(0, Math.Min(Defs.WeaponsPerRole - 1, I(p[2])));
                    s.Ability = Math.Max(0, Math.Min(Upgrades.RoleAbilityChoices - 1, I(p[3])));
                    break;
                }
                case "level": s.Level = Math.Max(1, I(val)); break;
                case "xp": s.Xp = Fl(val); break;
                case "rerolls": s.Rerolls = Math.Max(0, I(val)); break;
                case "gold": s.Gold = Math.Max(0, I(val)); break;
                case "token": s.ReviveToken = I(val) != 0; break;
                case "hp": s.HpFrac = Math.Max(0.05f, Math.Min(1f, Fl(val))); break;
                case "keys":
                {
                    var p = val.Split(',');
                    for (int i = 0; i < s.BonusKeys.Length && i < p.Length; i++)
                        s.BonusKeys[i] = p[i];
                    break;
                }
                case "upgrade": s.Taken.Add(val); break;
                case "mastery":
                {
                    int sp = val.LastIndexOf(' ');
                    s.Mastery[val.Substring(0, sp)] = Fl(val.Substring(sp + 1));
                    break;
                }
                case "stats":
                {
                    var p = val.Split(' ');
                    var r = s.Run;
                    r.DamageDealt = Fl(p[0]); r.DamageTaken = Fl(p[1]); r.HealingDone = Fl(p[2]); r.BiggestHit = Fl(p[3]);
                    r.Kills = I(p[4]); r.EliteKills = I(p[5]); r.BossKills = I(p[6]); r.Downs = I(p[7]); r.Revives = I(p[8]);
                    r.ShotsFired = I(p[9]); r.ShotsHit = I(p[10]); r.AbilitiesUsed = I(p[11]);
                    break;
                }
            }
        }

        // --- Text helpers (shared with Profile) ---------------------------------------------------

        internal static void Line(StringBuilder sb, string key, object val)
        {
            sb.Append(key).Append(' ');
            sb.Append(val is float f ? F(f) : Convert.ToString(val, CultureInfo.InvariantCulture));
            sb.Append('\n');
        }

        internal static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        internal static int I(string s) => int.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
        internal static float Fl(string s) => float.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        internal static T Enum<T>(string s) where T : struct => (T)System.Enum.Parse(typeof(T), s.Trim());
    }

    /// The saved runs on this computer: one file per run in Folder (the front end points it at the game's data
    /// folder; tests use a temp folder). Only the host of a run saves it.
    public static class SaveStore
    {
        public static string Folder;  // null: saving is off

        static string PathOf(string id) => Path.Combine(Folder, id + ".run");

        /// Every saved run, newest first (files that don't read as saves are left alone and not listed).
        public static List<RunSave> List()
        {
            var all = new List<RunSave>();
            if (Folder == null || !Directory.Exists(Folder))
                return all;
            foreach (var path in Directory.GetFiles(Folder, "*.run"))
            {
                try
                {
                    var s = RunSave.Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
                    if (s != null)
                        all.Add(s);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            all.Sort((a, b) => b.SavedAt.CompareTo(a.SavedAt));
            return all;
        }

        /// Writes it (to a temp file first, so a crash mid-write can't leave half a save). False if it couldn't.
        public static bool Write(RunSave save)
        {
            if (Folder == null || string.IsNullOrEmpty(save.Id))
                return false;
            try
            {
                Directory.CreateDirectory(Folder);
                SafeWrite(PathOf(save.Id), save.ToText());
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        /// Writes a file through a temp file that is then swapped in (File.Replace when one is already there), so
        /// there's never a moment with only the half-written temp: a crash leaves the old file or the new one.
        public static void SafeWrite(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }

        public static void Delete(string id)
        {
            if (Folder == null || string.IsNullOrEmpty(id))
                return;
            try
            {
                if (File.Exists(PathOf(id)))
                    File.Delete(PathOf(id));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
