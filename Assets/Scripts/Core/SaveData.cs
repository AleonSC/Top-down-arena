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
        public int Weapon, Ability, Wild;
        public int Ult;  // the Ultimate row's key (LobbyEntry.Ult; 0 the default ultimate)
        public readonly List<string> Taken = new List<string>();   // upgrade names, in the order picked
        public readonly string[] BonusKeys = { "", "", "" };        // abilities on Q, E, Shift (by name)
        public readonly List<string> Perks = new List<string>();  // the perks they brought (by name; a continued run can't change them)
        public int Level = 1, Rerolls, Gold;
        public float Xp, HpFrac = 1f;
        public bool ReviveToken;
        public readonly Dictionary<string, float> Mastery = new Dictionary<string, float>();  // ability name -> XP
        public RunStats Run = new RunStats();
        // The Expanding arena (ExpansionGame.cs): the seat's vectors and its shop room shelf (kept until the next boss cycle).
        public int Vectors = -1, VectorCap = -1;                   // -1: not saved (a save from another arena, or from before)
        public readonly List<string> Shelf = new List<string>();   // upgrade names by slot, "" for an empty slot
        public int ShelfSold, Restocks;
        public readonly int[] Stash = new int[Market.Count];       // Black Market buys not yet placed, by BuildKind

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
        public int Wave;              // waves cleared: resuming starts wave Wave + 1 (or the boss after it, unless BossSlot)
        public bool BossSlot;
        public bool LongRun;          // a 100-wave Campaign (Hunts.cs); saves from before it are the old 50 waves
        public int LivesUsed;         // lives used so far (Checkpoints.cs)
        public int Leg = 1;           // a Campaign: the leg of Wave that was cleared (0 first, 1 second)         // the boss after wave Wave was beaten (Hunts.cs); saves from before the boss slot: a multiple of 5 means yes
        public int Score;
        public RunMod Mods;
        public Difficulty Diff = new Difficulty();  // saves from before difficulties load as Normal
        public bool Endless, DevUsed;
        public float RunTime;
        public PathId CurrentPath, LastPath;
        public int PathFrom, PathTo;
        public readonly List<PathId> Route = new List<PathId>();
        public readonly List<SeatSave> Seats = new List<SeatSave>();

        // The Expanding arena (ExpansionGame.cs): the map the team built. A room's Owner is its drawer's seat + 1 here (0: none),
        // so it can go back to whoever takes that seat.
        public bool HasExpansion;
        public int ExpandSeed, FloatCycle = -1;
        public readonly List<ExpansionRoom> Rooms = new List<ExpansionRoom>();
        public readonly List<WallCut> Cuts = new List<WallCut>();
        public readonly List<MapNode> Nodes = new List<MapNode>();
        public readonly List<PortalLink> Portals = new List<PortalLink>();
        public readonly List<PlacedBuild> Builds = new List<PlacedBuild>();  // the Black Market traps on the map
        public readonly List<RoomToken> Kits = new List<RoomToken>();

        /// "Wave 14 · 2 players · 23 min" for lists.
        public string Summary => (Wave > 0 && Wave % 5 == 0 && !BossSlot && Leg == 1 ? "Boss " + ShownWave : "Wave " + (ShownWave + 1)) + (Endless ? " (endless)" : "") + " · " + Seats.Count + (Seats.Count == 1 ? " player" : " players")
                                 + " · " + (int)(RunTime / 60f) + " min" + LivesText;

        /// " · 2 lives" (the lives left, by the run's difficulty), or nothing for a run with none.
        string LivesText
        {
            get
            {
                int left = Math.Max(0, (int)Math.Round(Diff[DiffKnob.Lives]) - LivesUsed);
                return (int)Math.Round(Diff[DiffKnob.Lives]) <= 0 ? "" : " · " + left + (left == 1 ? " life" : " lives");
            }
        }

        /// The last wave cleared as the player saw it (1-100 in a Campaign).
        public int ShownWave => LongRun && Wave > 0 ? Wave * 2 - 1 + Leg : Wave;

        public static string NewId() => DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);

        public string ToText()
        {
            var sb = new StringBuilder();
            Line(sb, "arena-run", FormatVersion);
            Line(sb, "saved", SavedAt.ToString("o", CultureInfo.InvariantCulture));
            Line(sb, "wave", Wave);
            Line(sb, "slot", BossSlot ? 1 : 0);
            if (LongRun) Line(sb, "campaign", Leg);
            if (LivesUsed > 0) Line(sb, "lives", LivesUsed);
            Line(sb, "score", Score);
            Line(sb, "mods", (int)Mods);
            Line(sb, "difficulty", Diff.ToText());
            Line(sb, "endless", Endless ? 1 : 0);
            Line(sb, "dev", DevUsed ? 1 : 0);
            Line(sb, "time", RunTime);
            Line(sb, "path", CurrentPath + " " + PathFrom + " " + PathTo);
            Line(sb, "lastpath", LastPath);
            Line(sb, "route", string.Join(",", Route));
            if (HasExpansion)
            {
                Line(sb, "expand", ExpandSeed + " " + FloatCycle);
                foreach (var r in Rooms)
                    Line(sb, "room", string.Join(" ", new object[] { r.Parent, r.X, r.Y, r.W, r.H, r.Kind, r.Floor, r.Price, r.Owner, r.Floating ? 1 : 0, r.Leg, r.Surprise }));
                foreach (var c in Cuts)
                    Line(sb, "cut", (c.Vertical ? 1 : 0) + " " + c.At + " " + c.From + " " + c.Till);
                foreach (var l in Portals)
                    Line(sb, "portal", l.AX + " " + l.AY + " " + l.BX + " " + l.BY);
                foreach (var b in Builds)
                    Line(sb, "build", b.Kind + " " + b.X + " " + b.Y);
                foreach (var k in Kits)
                    Line(sb, "kit", k.Kind + " " + k.Floor);
                foreach (var nd in Nodes)
                    Line(sb, "node", nd.Kind + " " + nd.X + " " + nd.Y + " " + (nd.Spent ? 1 : 0));
            }
            foreach (var s in Seats)
            {
                sb.Append("seat\n");
                Line(sb, "name", s.Name);
                Line(sb, "class", s.Role + " " + s.Kit + " " + s.Weapon + " " + s.Ability + " " + s.Wild + " " + s.Ult);
                Line(sb, "level", s.Level);
                Line(sb, "xp", s.Xp);
                Line(sb, "rerolls", s.Rerolls);
                Line(sb, "gold", s.Gold);
                Line(sb, "token", s.ReviveToken ? 1 : 0);
                Line(sb, "hp", s.HpFrac);
                Line(sb, "keys", string.Join(",", s.BonusKeys));
                if (s.Perks.Count > 0) Line(sb, "perks", string.Join(",", s.Perks));
                if (s.Vectors >= 0) Line(sb, "vectors", s.Vectors + " " + s.VectorCap);
                if (Array.Exists(s.Stash, n => n > 0)) Line(sb, "stash", string.Join(" ", s.Stash));
                if (s.Shelf.Count > 0)
                {
                    Line(sb, "shop", s.Restocks + " " + s.ShelfSold);
                    Line(sb, "shelf", string.Join(",", s.Shelf));
                }
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
            bool header = false, sawSlot = false;
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
                        case "wave": save.Wave = I(val); if (!sawSlot) save.BossSlot = save.Wave > 0 && save.Wave % 5 == 0; break;
                        case "slot": save.BossSlot = I(val) != 0; sawSlot = true; break;
                        case "lives": save.LivesUsed = Math.Max(0, I(val)); break;
                        case "campaign": save.LongRun = true; save.Leg = I(val) != 0 ? 1 : 0; break;
                        case "score": save.Score = I(val); break;
                        case "mods": save.Mods = (RunMod)I(val); break;
                        case "difficulty": save.Diff = Difficulty.Parse(val); break;
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
                        case "expand":
                        {
                            var p = val.Split(' ');
                            save.HasExpansion = true;
                            save.ExpandSeed = I(p[0]);
                            save.FloatCycle = I(p[1]);
                            break;
                        }
                        case "room":
                        {
                            var p = val.Split(' ');
                            save.Rooms.Add(new ExpansionRoom { Parent = I(p[0]), X = I(p[1]), Y = I(p[2]), W = I(p[3]), H = I(p[4]), Kind = Enum<RoomKind>(p[5]), Floor = Enum<PathId>(p[6]),
                                Price = I(p[7]), Owner = I(p[8]), Floating = I(p[9]) != 0, Leg = (byte)I(p[10]), Surprise = p.Length > 11 ? (byte)I(p[11]) : (byte)0 });
                            break;
                        }
                        case "node":
                        {
                            var p = val.Split(' ');
                            save.Nodes.Add(new MapNode { Kind = Enum<NodeKind>(p[0]), X = (short)I(p[1]), Y = (short)I(p[2]), Spent = p.Length > 3 && I(p[3]) != 0 });
                            break;
                        }
                        case "cut":
                        {
                            var p = val.Split(' ');
                            save.Cuts.Add(new WallCut { Vertical = I(p[0]) != 0, At = (short)I(p[1]), From = (short)I(p[2]), Till = (short)I(p[3]) });
                            break;
                        }
                        case "portal":
                        {
                            var p = val.Split(' ');
                            save.Portals.Add(new PortalLink { AX = (short)I(p[0]), AY = (short)I(p[1]), BX = (short)I(p[2]), BY = (short)I(p[3]) });
                            break;
                        }
                        case "build":
                        {
                            var p = val.Split(' ');
                            save.Builds.Add(new PlacedBuild { Kind = Enum<BuildKind>(p[0]), X = (short)I(p[1]), Y = (short)I(p[2]) });
                            break;
                        }
                        case "kit":
                        {
                            var p = val.Split(' ');
                            save.Kits.Add(new RoomToken { Kind = Enum<RoomKind>(p[0]), Floor = Enum<PathId>(p[1]) });
                            break;
                        }
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
                    s.Kit = KitFromSave(p[1]);
                    int top = s.Role == Role.Wildcard ? Defs.WildcardChoices : Defs.WeaponsPerRole;  // (a Wildcard picks from every class's three)
                    s.Weapon = Math.Max(0, Math.Min(Defs.WeaponLimit(s.Role, s.Kit) - 1, I(p[2])));
                    s.Ability = Math.Max(0, Math.Min(top - 1, I(p[3])));
                    s.Wild = p.Length > 4 ? Math.Max(0, Math.Min(3 * Defs.WildcardChoices - 1, I(p[4]))) : 0;  // (saves from before Wildcard have no fifth number)
                    s.Ult = p.Length > 5 ? Upgrades.ClampUlt(s.Role, s.Kit, I(p[5])) : 0;  // (saves from before the Ultimate row's picks have no sixth)
                    break;
                }
                case "level": s.Level = Math.Max(1, I(val)); break;
                case "xp": s.Xp = Fl(val); break;
                case "rerolls": s.Rerolls = Math.Max(0, I(val)); break;
                case "gold": s.Gold = Math.Max(0, I(val)); break;
                case "token": s.ReviveToken = I(val) != 0; break;
                case "perks": foreach (var n in val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) s.Perks.Add(n.Trim()); break;
                case "hp": s.HpFrac = Math.Max(0.05f, Math.Min(1f, Fl(val))); break;
                case "keys":
                {
                    var p = val.Split(',');
                    for (int i = 0; i < s.BonusKeys.Length && i < p.Length; i++)
                        s.BonusKeys[i] = p[i];
                    break;
                }
                case "upgrade": s.Taken.Add(val); break;
                case "stash":
                {
                    var p = val.Split(' ');
                    for (int i = 0; i < p.Length && i < Market.Count; i++)
                        s.Stash[i] = Math.Max(0, I(p[i]));
                    break;
                }
                case "vectors":
                {
                    var p = val.Split(' ');
                    s.Vectors = Math.Max(0, I(p[0]));
                    s.VectorCap = Math.Max(0, I(p[1]));
                    break;
                }
                case "shop":
                {
                    var p = val.Split(' ');
                    s.Restocks = Math.Max(0, I(p[0]));
                    s.ShelfSold = I(p[1]);
                    break;
                }
                case "shelf": s.Shelf.Clear(); s.Shelf.AddRange(val.Split(',')); break;
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

        /// A class by its name in a save: older saves call the Elementalist "Mage" and the Hemomancer "BloodMage".
        internal static Kit KitFromSave(string s) => s.Trim() == "Mage" ? Kit.Elementalist : s.Trim() == "BloodMage" ? Kit.Hemomancer : Enum<Kit>(s);
    }

    /// The saved runs on this computer: one file per run in Folder (the front end points it at the game's data
    /// folder; tests use a temp folder). Only the host of a run saves it.
    public static class SaveStore
    {
        public static string Folder;  // null: saving is off

        static string PathOf(string id) => Path.Combine(Folder, id + ".run");
        static string CheckpointOf(string id) => Path.Combine(Folder, id + ".checkpoint");

        /// The run's checkpoint (Checkpoints.cs): the save as it was after the last boss. Only the newest is kept.
        public static bool WriteCheckpoint(RunSave save)
        {
            if (Folder == null || string.IsNullOrEmpty(save.Id))
                return false;
            try
            {
                Directory.CreateDirectory(Folder);
                SafeWrite(CheckpointOf(save.Id), save.ToText());
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        /// The run's checkpoint, or null if it has none.
        public static RunSave ReadCheckpoint(string id)
        {
            if (Folder == null || string.IsNullOrEmpty(id) || !File.Exists(CheckpointOf(id)))
                return null;
            try { return RunSave.Parse(File.ReadAllText(CheckpointOf(id)), id); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

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
        public static void SafeWrite(string path, string text, string backupPath = null)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path))
                File.Replace(tmp, path, backupPath);
            else
                File.Move(tmp, path);
        }

        /// The DEL key in the Continue list: the save moves to Folder/deleted/ instead of being erased, so a mis-click can be
        /// undone by moving the file back. (A run that ends deletes its own save for good with Delete.) Returns false if it
        /// couldn't move it.
        public static bool Trash(string id)
        {
            if (Folder == null || string.IsNullOrEmpty(id) || !File.Exists(PathOf(id)))
                return false;
            try
            {
                string dir = Path.Combine(Folder, "deleted");
                Directory.CreateDirectory(dir);
                string to = Path.Combine(dir, id + ".run");
                if (File.Exists(to))
                    File.Delete(to);
                File.Move(PathOf(id), to);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static void Delete(string id)
        {
            if (Folder == null || string.IsNullOrEmpty(id))
                return;
            try
            {
                if (File.Exists(PathOf(id)))
                    File.Delete(PathOf(id));
                if (File.Exists(CheckpointOf(id)))
                    File.Delete(CheckpointOf(id));  // (its checkpoint goes with it)
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
