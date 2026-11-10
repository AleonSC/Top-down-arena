using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Arena
{
    /// One profile as the "Who's playing?" screen lists it.
    public class ProfileInfo
    {
        public string Id;      // the folder's name under Root
        public string Name;
        public int Rank, Wins, Runs;
        public string Disk = "";  // the easter-egg disk look, "" for the usual one (ProfileStore.DiskLookFor)
    }

    /// Several profiles on one computer (Docs/Profiles.md). Each is a folder under Root: profile.txt (records, rank,
    /// cosmetics, the name) and saves/ (its saved runs). active.txt holds the id of the one in use; Profile.Folder and
    /// SaveStore.Folder point into it, so the rest of the game doesn't know there are several.
    public static class ProfileStore
    {
        public const int MaxProfiles = 3, MaxNameLength = 16;

        public static string Root;  // null: profiles are off (tests set a temp folder)

        static string PathOf(string id) => Path.Combine(Root, id);
        static string ActiveFile => Path.Combine(Root, "active.txt");

        /// The id of the profile in use ("" none yet).
        public static string ActiveId = "";

        /// Start of the game: finds the profiles under dataFolder, moving a pre-profiles install in on the first run
        /// (copied, not moved: the old files stay put), and makes the last one used the active one. legacyName names
        /// the migrated profile. Returns true if a profile is active.
        public static bool Init(string dataFolder, string legacyName)
        {
            Root = Path.Combine(dataFolder, "profiles");
            ActiveId = "";
            try
            {
                if (!Directory.Exists(Root))
                {
                    Directory.CreateDirectory(Root);
                    Migrate(dataFolder, legacyName);
                }
                string saved = File.Exists(ActiveFile) ? File.ReadAllText(ActiveFile).Trim() : "";
                var all = List();
                string pick = all.Any(p => p.Id == saved) ? saved : all.Count > 0 ? all[0].Id : "";
                return pick.Length > 0 && SetActive(pick);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            ApplyFolders();
            return false;
        }

        /// Copies a single-profile install (data folder's profile.txt and saves/*.run) into its own profile.
        static void Migrate(string dataFolder, string legacyName)
        {
            string oldProfile = Path.Combine(dataFolder, "profile.txt"), oldSaves = Path.Combine(dataFolder, "saves");
            bool hasProfile = File.Exists(oldProfile) || File.Exists(oldProfile + ".bak");
            bool hasSaves = Directory.Exists(oldSaves) && Directory.GetFiles(oldSaves, "*.run").Length > 0;
            if (!hasProfile && !hasSaves)
                return;
            string name = Clean(legacyName);
            if (name.Length == 0) name = "Player";
            string id = NewId(name), dir = PathOf(id);
            Directory.CreateDirectory(Path.Combine(dir, "saves"));
            if (File.Exists(oldProfile)) File.Copy(oldProfile, Path.Combine(dir, "profile.txt"), true);
            if (File.Exists(oldProfile + ".bak")) File.Copy(oldProfile + ".bak", Path.Combine(dir, "profile.txt.bak"), true);
            if (hasSaves)
                foreach (var f in Directory.GetFiles(oldSaves, "*.run"))
                    File.Copy(f, Path.Combine(dir, "saves", Path.GetFileName(f)), true);
            // Give it its name (the old file has none), keeping everything else.
            var p = ReadProfile(dir) ?? new Profile();
            p.Name = name;
            p.SaveTo(dir);
            File.WriteAllText(Path.Combine(dataFolder, "migrated.txt"), "Copied into profiles/" + id + " on " + DateTime.UtcNow.ToString("u") + ". The old files here are no longer used.\n");
        }

        /// Reads a profile folder's profile.txt (or its backup); null if there is none to read.
        static Profile ReadProfile(string dir)
        {
            foreach (var f in new[] { "profile.txt", "profile.txt.bak" })
            {
                try
                {
                    string path = Path.Combine(dir, f);
                    if (!File.Exists(path)) continue;
                    var p = Profile.Parse(File.ReadAllText(path));
                    if (p.Valid) return p;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return null;
        }

        /// Every profile, by name. A folder with nothing readable is still listed.
        public static List<ProfileInfo> List()
        {
            var all = new List<ProfileInfo>();
            if (Root == null || !Directory.Exists(Root))
                return all;
            foreach (var d in Directory.GetDirectories(Root))
            {
                string id = Path.GetFileName(d);
                var p = id == ActiveId ? Profile.Current : ReadProfile(d);
                all.Add(new ProfileInfo
                {
                    Id = id,
                    Name = p != null && p.Name.Length > 0 ? p.Name : id,
                    Rank = p != null ? p.RankNow : 1,
                    Wins = p != null ? p.RunsWon : 0,
                    Runs = p != null ? p.RunsWon + p.RunsLost : 0,
                    Disk = p == null ? "" : p.DiskLook.Length > 0 ? p.DiskLook : DiskLookFor(p.Name),  // (a profile named before the look was kept still gets it)
                });
            }
            all.Sort((a, b) => { int c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id); });
            return all;
        }

        /// Makes a profile active: its folders become Profile.Folder and SaveStore.Folder and its records load.
        public static bool SetActive(string id)
        {
            if (Root == null || id == null || !Directory.Exists(PathOf(id)))
                return false;
            ActiveId = id;
            try { File.WriteAllText(ActiveFile, id); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            ApplyFolders();
            return true;
        }

        static void ApplyFolders()
        {
            Profile.Folder = ActiveId.Length > 0 ? PathOf(ActiveId) : null;
            SaveStore.Folder = ActiveId.Length > 0 ? Path.Combine(PathOf(ActiveId), "saves") : null;
            Profile.Reload();
        }

        /// A name as it's kept: trimmed, one line, at most MaxNameLength characters, no control characters.
        public static string Clean(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
                if (!char.IsControl(c))
                    sb.Append(c);
            string s = sb.ToString().Trim();
            return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength).TrimEnd() : s;
        }

        /// Why a name can't be used ("" if it can). exceptId: the profile being renamed.
        public static string NameProblem(string name, string exceptId = null)
        {
            string n = Clean(name);
            if (n.Length == 0) return "Type a name.";
            foreach (var p in List())
                if (p.Id != exceptId && string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase))
                    return "Someone already has that name.";
            return "";
        }

        /// A folder name for a profile: letters and digits of the name (lower case), "-" for anything else, and a
        /// number if taken. Never the raw name: no slashes, dots or reserved names get through.
        public static string NewId(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in Clean(name).ToLowerInvariant())
                sb.Append(c < 128 && char.IsLetterOrDigit(c) ? c : '-');
            string slug = sb.ToString().Trim('-');
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            if (slug.Length == 0) slug = "player";
            if (slug.Length > 24) slug = slug.Substring(0, 24).TrimEnd('-');
            if (new[] { "con", "prn", "aux", "nul" }.Contains(slug) || (slug.Length == 4 && (slug.StartsWith("com") || slug.StartsWith("lpt")) && char.IsDigit(slug[3])))
                slug += "-p";
            string id = slug;
            for (int i = 2; Root != null && Directory.Exists(PathOf(id)); i++)
                id = slug + "-" + i;
            return id;
        }

        /// Easter egg: the disk look a name earns, "" for none. "sa": San Andreas, SanAndreas, GTA:SA, GTASA or SA; "doom": any name with
        /// Doom in it; "mi": "win95": Windows 95, Win95; "mi": Mission Impossible, MI, MI1, MI2, Ethan Hunt, NOC list (case, spaces and punctuation ignored).
        public static string DiskLookFor(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            var sb = new StringBuilder();
            foreach (char ch in name)
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            string n = sb.ToString();
            if (n == "mi" || n == "mi1" || n == "mi2" || n.Contains("missionimpossible") || n.Contains("ethanhunt") || n.Contains("noclist"))
                return "mi";
            if (n.Contains("windows95") || n.Contains("win95") || n == "w95")
                return "win95";
            if (n.Contains("doom"))
                return "doom";
            if (n == "sa" || n == "gtasa" || n.Contains("sanandreas"))
                return "sa";
            return "";
        }

        /// Makes a profile and returns its id (null: not made, see NameProblem; the limit is MaxProfiles).
        public static string Create(string name)
        {
            if (Root == null || NameProblem(name).Length > 0 || List().Count >= MaxProfiles)
                return null;
            string n = Clean(name), id = NewId(n);
            try
            {
                Directory.CreateDirectory(Path.Combine(PathOf(id), "saves"));
                if (!new Profile { Name = n, DiskLook = DiskLookFor(n) }.SaveTo(PathOf(id)))
                    return null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            return id;
        }

        public static bool Rename(string id, string name)
        {
            if (Root == null || NameProblem(name, id).Length > 0 || !Directory.Exists(PathOf(id)))
                return false;
            var p = id == ActiveId ? Profile.Current : ReadProfile(PathOf(id)) ?? new Profile();
            if (p.DiskLook.Length == 0)
                p.DiskLook = DiskLookFor(p.Name);   // the old name's look is kept
            p.Name = Clean(name);
            string look = DiskLookFor(p.Name);
            if (look.Length > 0)
                p.DiskLook = look;                   // a new name that earns another look swaps it; a plain name keeps the one it has
            return id == ActiveId ? p.Save() : p.SaveTo(PathOf(id));
        }

        /// Deletes a profile and everything in it. The last one can't be deleted. Deleting the active one makes the
        /// first of the others active.
        public static bool Delete(string id)
        {
            if (Root == null || !Directory.Exists(PathOf(id)))
                return false;
            var all = List();
            if (all.Count < 2)
                return false;
            try { Directory.Delete(PathOf(id), true); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            if (id == ActiveId)
            {
                ActiveId = "";
                SetActive(all.First(p => p.Id != id).Id);
            }
            return true;
        }
    }
}
