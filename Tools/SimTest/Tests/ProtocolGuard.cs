using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Arena;

// The protocol guard (suite `protocol`): Golden/protocol.txt keeps Session.ProtocolVersion and a hash of every line in
// Core that writes or reads the wire (`w.Write(...)`, `r.ReadSingle()`, ...). The suite fails when:
//   - the read/write lines changed but ProtocolVersion didn't: bump it (Docs/ProtocolHistory.md);
//   - ProtocolVersion has no line in Docs/ProtocolHistory.md;
//   - two builds with different ids or cards would get the same Session.IdsHash (it must cover every enum and card).
// New ids and cards need no bump: the ids are kept in name order (Tools/Ids/ids.py), so their numbers move, and
// Session.IdsHash, sent with Hello, tells builds with different ones apart. After a bump, or after a change that sends
// nothing new (a refactor, a save-only field), `PROTOCOL=update run.sh 1 1 protocol` rewrites the golden copy.
static partial class Program
{
    const string ProtocolFile = "Golden/protocol.txt";
    const string CoreDir = "../../Assets/Scripts/Core";

    static readonly Regex WireLine = new Regex(@"\b\w+\.(Write\(|Read(Byte|SByte|Single|Double|Boolean|String|Bytes|Char|Int16|UInt16|Int32|UInt32|Int64|UInt64)\()");

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
        var now = new List<string> { "version " + Session.ProtocolVersion, "format " + format + " (" + lines + " read/write lines)" };
        if (Environment.GetEnvironmentVariable("PROTOCOL") == "update" || !File.Exists(ProtocolFile))
        {
            File.WriteAllLines(ProtocolFile, now);
            Console.WriteLine("  wrote " + ProtocolFile + ": version " + Session.ProtocolVersion + ", " + lines + " read/write lines");
        }
        var golden = File.ReadAllLines(ProtocolFile).ToList();
        int goldenVersion = int.Parse(golden[0].Substring("version ".Length));
        const string fix = " (PROTOCOL=update run.sh 1 1 protocol rewrites Golden/protocol.txt)";

        // Same version: the same read/write lines.
        if (goldenVersion == Session.ProtocolVersion)
            Check(golden.Count > 1 && golden[1] == now[1], "the wire format matches protocol version " + Session.ProtocolVersion
                  + (golden.Count > 1 && golden[1] == now[1] ? "" : ": the read/write lines changed (" + (golden.Count > 1 ? golden[1] : "none") + " -> " + now[1]
                     + "). Bump Session.ProtocolVersion and add its line to Docs/ProtocolHistory.md, then" + fix
                     + "; if nothing sent changed (a refactor, a save-only change), just run that"));
        else
            Check(false, "Golden/protocol.txt records version " + goldenVersion + " but Session.ProtocolVersion is " + Session.ProtocolVersion + fix);

        // The history has the version's line.
        string history = "../../Docs/ProtocolHistory.md";
        Check(File.Exists(history) && File.ReadAllLines(history).Any(l => l.StartsWith("- " + Session.ProtocolVersion + ":")),
            "Docs/ProtocolHistory.md has a line for version " + Session.ProtocolVersion);

        // The ids fingerprint covers the ids and the cards: renaming, adding or renumbering any changes it.
        int ids = Session.IdsHash;
        Check(ids != 0 && Session.VersionText.StartsWith("Version " + Session.ProtocolVersion + "."), "Session.IdsHash is worked out (" + Session.VersionText + ")");
        var field = typeof(Session).GetField("idsHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var card = Upgrades.All[0]; string name = card.Name;
        card.Name = name + " (renamed)"; field.SetValue(null, 0);
        int renamed = Session.IdsHash;
        card.Name = name; field.SetValue(null, 0);
        Check(renamed != ids && Session.IdsHash == ids, "renaming a card changes Session.IdsHash, and it's the same again after");
    }
}
