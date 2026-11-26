using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Arena;

// The protocol guard (suite `protocol`): Golden/protocol.txt keeps Session.ProtocolVersion, every enum in Core with
// its values (Msg, EnemyType, AbilityId, FxKind, ... go over the wire as numbers) and a hash of every line in Core that
// writes or reads the wire (`w.Write(...)`, `r.ReadSingle()`, ...). The suite fails when:
//   - the enums or the read/write lines changed but ProtocolVersion didn't: bump it (Docs/ProtocolHistory.md);
//   - an existing enum value got a new number (new values go at the end, before None);
//   - ProtocolVersion has no line in Docs/ProtocolHistory.md.
// After a bump, or after a change that sends nothing new (a refactor, a save-only field), `PROTOCOL=update run.sh 1 1
// protocol` rewrites the golden copy.
static partial class Program
{
    const string ProtocolFile = "Golden/protocol.txt";
    const string CoreDir = "../../Assets/Scripts/Core";

    static readonly Regex WireLine = new Regex(@"\b\w+\.(Write\(|Read(Byte|SByte|Single|Double|Boolean|String|Bytes|Char|Int16|UInt16|Int32|UInt32|Int64|UInt64)\()");

    // Every enum of the game's code (namespace Arena), "Type: Name=value ..." in name order.
    static List<string> WireEnums()
    {
        return typeof(Game).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == "Arena")
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => "enum " + t.FullName.Substring("Arena.".Length).Replace('+', '.') + ": "
                         + string.Join(" ", Enum.GetNames(t).Select(n => n + "=" + Convert.ToInt64(Enum.Parse(t, n)))))
            .ToList();
    }

    // A hash of every wire read/write line in Core (comments and spacing ignored), file by file in name order.
    static string WireFormatHash(out int lines)
    {
        var sb = new System.Text.StringBuilder();
        lines = 0;
        foreach (var f in Directory.GetFiles(CoreDir, "*.cs").OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal))
            foreach (var raw in File.ReadAllLines(f))
            {
                string line = raw;
                int c = line.IndexOf("//", StringComparison.Ordinal);
                if (c >= 0) line = line.Substring(0, c);
                if (!WireLine.IsMatch(line)) continue;
                sb.Append(Regex.Replace(line, @"\s+", "")).Append('\n');
                lines++;
            }
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
    }

    static void ProtocolGuardTest()
    {
        int lines;
        string format = WireFormatHash(out lines);
        var enums = WireEnums();
        var now = new List<string> { "version " + Session.ProtocolVersion, "format " + format + " (" + lines + " read/write lines)" };
        now.AddRange(enums);
        if (Environment.GetEnvironmentVariable("PROTOCOL") == "update" || !File.Exists(ProtocolFile))
        {
            File.WriteAllLines(ProtocolFile, now);
            Console.WriteLine("  wrote " + ProtocolFile + ": version " + Session.ProtocolVersion + ", " + enums.Count + " enums, " + lines + " read/write lines");
        }
        var golden = File.ReadAllLines(ProtocolFile).ToList();
        int goldenVersion = int.Parse(golden[0].Substring("version ".Length));
        const string fix = " (PROTOCOL=update run.sh 1 1 protocol rewrites Golden/protocol.txt)";

        // Existing enum values keep their numbers: old saves and other players' games read them as numbers.
        var moved = new List<string>();
        var before = golden.Where(l => l.StartsWith("enum ")).ToDictionary(l => l.Substring(5, l.IndexOf(':') - 5),
            l => l.Substring(l.IndexOf(':') + 1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=')).ToDictionary(p => p[0], p => long.Parse(p[1])));
        foreach (var l in enums)
        {
            string type = l.Substring(5, l.IndexOf(':') - 5);
            Dictionary<string, long> old;
            if (!before.TryGetValue(type, out old)) continue;
            foreach (var p in l.Substring(l.IndexOf(':') + 1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=')))
            {
                long was;
                if (p[0] != "None" && old.TryGetValue(p[0], out was) && was != long.Parse(p[1]))  // (None moves on: new values go before it)
                    moved.Add(type + "." + p[0] + " " + was + " -> " + p[1]);
            }
        }
        Check(moved.Count == 0, "existing enum values keep their numbers (new values go at the end, before None)"
              + (moved.Count > 0 ? ": " + string.Join(", ", moved.Take(8)) + (moved.Count > 8 ? ", ..." : "") + fix : ""));

        // Same version: the same enums and the same read/write lines.
        var changed = new List<string>();
        if (golden.Count < 2 || golden[1] != now[1]) changed.Add("read/write lines (" + (golden.Count > 1 ? golden[1] : "none") + " -> " + now[1] + ")");
        var goldenEnums = new HashSet<string>(golden.Where(l => l.StartsWith("enum ")));
        var diff = enums.Where(l => !goldenEnums.Contains(l)).Select(l => l.Substring(5, l.IndexOf(':') - 5))
            .Concat(goldenEnums.Where(l => !enums.Contains(l)).Select(l => l.Substring(5, l.IndexOf(':') - 5))).Distinct().ToList();
        if (diff.Count > 0) changed.Add("enums " + string.Join(", ", diff.Take(8)) + (diff.Count > 8 ? ", ..." : ""));
        if (goldenVersion == Session.ProtocolVersion)
            Check(changed.Count == 0, "the wire format matches protocol version " + Session.ProtocolVersion
                  + (changed.Count > 0 ? ": changed " + string.Join("; ", changed) + ". Bump Session.ProtocolVersion and add its line to Docs/ProtocolHistory.md, then"
                     + fix + "; if nothing sent changed (a refactor, a save-only change), just run that" : ""));
        else
            Check(false, "Golden/protocol.txt records version " + goldenVersion + " but Session.ProtocolVersion is " + Session.ProtocolVersion + fix);

        // The history has the version's line.
        string history = "../../Docs/ProtocolHistory.md";
        Check(File.Exists(history) && File.ReadAllLines(history).Any(l => l.StartsWith("- " + Session.ProtocolVersion + ":")),
            "Docs/ProtocolHistory.md has a line for version " + Session.ProtocolVersion);
    }
}
