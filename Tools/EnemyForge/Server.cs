using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Arena;

// Enemy Forge: design a new enemy from scratch (stats, look, movement, attacks, traits) with a live preview, then save it or
// copy a brief for Claude to implement. A tiny localhost web server: it serves the page (web/index.html, or the copy inside
// the single-file EnemyForge.exe), the real roster from Defs.Enemies (/api/meta) and the designs folder (/api/list, load,
// save). It opens the page in its own window and quits a few seconds after the window is closed. Docs/EnemyForge.md.
static class Forge
{
    static readonly object L = new object();
    static string webRoot, designDir;
    static double lastPing = -1;
    static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    static string N(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "0" : Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
    static string S(string s)
    {
        if (s == null) return "\"\"";
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '\n') sb.Append("\\n");
            else if (c < ' ') sb.Append(' ');
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
    static string Col(Rgba c) => S("#" + ((int)Math.Round(Clamp01(c.R) * 255)).ToString("x2") + ((int)Math.Round(Clamp01(c.G) * 255)).ToString("x2") + ((int)Math.Round(Clamp01(c.B) * 255)).ToString("x2"));
    static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

    // The real enemies and roles, for the "plays like" list, the size line-up and the stat comparisons.
    static string Meta()
    {
        var sb = new StringBuilder("{\"folder\":" + S(designDir) + ",\"enemies\":[");
        bool first = true;
        foreach (EnemyType t in Enum.GetValues(typeof(EnemyType)))
        {
            var d = Defs.Get(t);
            if (d == null) continue;
            sb.Append(first ? "" : ",").Append("{\"key\":").Append(S(t.ToString())).Append(",\"name\":").Append(S(d.Name))
              .Append(",\"hp\":").Append(N(d.Hp)).Append(",\"r\":").Append(N(d.Radius)).Append(",\"speed\":").Append(N(d.Speed))
              .Append(",\"score\":").Append(d.Score).Append(",\"xp\":").Append(d.Xp).Append(",\"sides\":").Append(d.Sides)
              .Append(",\"contact\":").Append(N(d.Contact)).Append(",\"boss\":").Append(d.IsBoss ? "true" : "false")
              .Append(",\"melee\":").Append(d.Melee ? "true" : "false").Append(",\"heavy\":").Append(d.Heavy ? "true" : "false")
              .Append(",\"group\":").Append(S(d.Picker.ToString())).Append(",\"color\":").Append(Col(d.Color)).Append(",\"blurb\":").Append(S(d.Blurb)).Append('}');
            first = false;
        }
        sb.Append("],\"roles\":[");
        for (int r = 0; r < 3; r++)
        {
            var d = Defs.Roles[r];
            sb.Append(r == 0 ? "" : ",").Append("{\"name\":").Append(S(d.Name)).Append(",\"hp\":").Append(N(d.Hp)).Append(",\"speed\":").Append(N(d.Speed))
              .Append(",\"r\":").Append(N(d.BodyR)).Append(",\"hitR\":").Append(N(d.HitR)).Append(",\"fire\":").Append(N(d.FireInterval)).Append(",\"color\":").Append(Col(d.Color)).Append('}');
        }
        sb.Append("],\"bulletMult\":").Append(N(Defs.EnemyBulletMult)).Append('}');
        return sb.ToString();
    }

    // A design's file name: letters, digits, spaces, dashes; never a path.
    static string SafeName(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in Uri.UnescapeDataString(s ?? "").Trim())
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_') sb.Append(c);
        string n = sb.ToString().Trim();
        if (n.Length > 60) n = n.Substring(0, 60);
        return n.Length == 0 ? "Untitled" : n;
    }

    static string Query(string target, string key)
    {
        int q = target.IndexOf('?');
        if (q < 0) return null;
        foreach (var part in target.Substring(q + 1).Split('&'))
        {
            int e = part.IndexOf('=');
            if (e > 0 && part.Substring(0, e) == key) return part.Substring(e + 1).Replace('+', ' ');
        }
        return null;
    }

    static string List()
    {
        var sb = new StringBuilder("[");
        bool first = true;
        if (Directory.Exists(designDir))
            foreach (var f in new DirectoryInfo(designDir).GetFiles("*.enemy.json").OrderByDescending(f => f.LastWriteTimeUtc))
            {
                string n = f.Name.Substring(0, f.Name.Length - ".enemy.json".Length);
                sb.Append(first ? "" : ",").Append("{\"name\":").Append(S(n)).Append(",\"when\":").Append(S(f.LastWriteTime.ToString("d MMM HH:mm", CultureInfo.InvariantCulture))).Append('}');
                first = false;
            }
        return sb.Append(']').ToString();
    }

    static void Serve(TcpClient tc)
    {
        try
        {
            var st = tc.GetStream();
            var req = new StringBuilder();
            var one = new byte[1];
            while (!req.ToString().EndsWith("\r\n\r\n")) { if (st.Read(one, 0, 1) <= 0) return; req.Append((char)one[0]); if (req.Length > 16384) return; }
            string[] lines = req.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string[] first = lines[0].Split(' ');
            string method = first[0], target = first[1], path = target.Split('?')[0];
            int len = 0;
            foreach (var l in lines.Skip(1)) { int i = l.IndexOf(':'); if (i > 0 && l.Substring(0, i).Trim().ToLowerInvariant() == "content-length") int.TryParse(l.Substring(i + 1).Trim(), out len); }
            byte[] body = new byte[Math.Max(0, Math.Min(len, 1 << 20))];
            for (int got = 0; got < body.Length;) { int n = st.Read(body, got, body.Length - got); if (n <= 0) break; got += n; }

            if (path == "/api/ping") { lock (L) lastPing = clock.Elapsed.TotalSeconds; Reply(st, "text/plain", Encoding.UTF8.GetBytes("ok")); return; }
            if (path == "/api/meta") { Reply(st, "application/json", Encoding.UTF8.GetBytes(Meta())); return; }
            if (path == "/api/list") { Reply(st, "application/json", Encoding.UTF8.GetBytes(List())); return; }
            if (path == "/api/load")
            {
                string f = Path.Combine(designDir, SafeName(Query(target, "name")) + ".enemy.json");
                if (!File.Exists(f)) { Reply(st, "text/plain", Encoding.UTF8.GetBytes("not found"), "404 Not Found"); return; }
                Reply(st, "application/json", File.ReadAllBytes(f)); return;
            }
            if (path == "/api/save" && method == "POST")
            {
                string n = SafeName(Query(target, "name"));
                Directory.CreateDirectory(designDir);
                File.WriteAllBytes(Path.Combine(designDir, n + ".enemy.json"), body);
                Reply(st, "application/json", Encoding.UTF8.GetBytes("{\"saved\":" + S(n) + "}")); return;
            }
            if (path == "/api/folder")
            {
                Directory.CreateDirectory(designDir);
                try
                {
                    if (Path.DirectorySeparatorChar == '\\') System.Diagnostics.Process.Start("explorer.exe", "\"" + designDir + "\"");
                    else System.Diagnostics.Process.Start(Directory.Exists("/Applications") ? "open" : "xdg-open", designDir);
                }
                catch { }
                Reply(st, "text/plain", Encoding.UTF8.GetBytes("ok")); return;
            }
            if (path != "/" && path != "/index.html") { Reply(st, "text/plain", Encoding.UTF8.GetBytes("not found"), "404 Not Found"); return; }
            string file = Path.Combine(webRoot, "index.html");
            if (File.Exists(file)) { Reply(st, "text/html; charset=utf-8", File.ReadAllBytes(file)); return; }
            using (var rs = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html"))  // the single-file build
                if (rs != null) { var ms = new MemoryStream(); rs.CopyTo(ms); Reply(st, "text/html; charset=utf-8", ms.ToArray()); return; }
            Reply(st, "text/plain", Encoding.UTF8.GetBytes("page missing"), "404 Not Found");
        }
        catch { }
        finally { try { tc.Close(); } catch { } }
    }

    static void Reply(NetworkStream st, string type, byte[] body, string status = "200 OK")
    {
        var h = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
        st.Write(h, 0, h.Length);
        st.Write(body, 0, body.Length);
    }

    // Opens the page in its own window: Edge or Chrome in app mode (no tabs or address bar), else the default browser.
    static void OpenWindow(string url)
    {
        var cands = new List<string>();
        string pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? "", pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "", la = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
        if (Path.DirectorySeparatorChar == '\\')
        {
            cands.Add(Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe")); cands.Add(Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe"));
            cands.Add(Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe")); cands.Add(Path.Combine(pf86, "Google", "Chrome", "Application", "chrome.exe")); cands.Add(Path.Combine(la, "Google", "Chrome", "Application", "chrome.exe"));
        }
        else if (Directory.Exists("/Applications"))
        {
            cands.Add("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"); cands.Add("/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"); cands.Add("/Applications/Chromium.app/Contents/MacOS/Chromium");
        }
        else cands.AddRange(new[] { "google-chrome", "chromium", "chromium-browser", "microsoft-edge", "google-chrome-stable" });
        string prof = Path.Combine(Path.GetTempPath(), "enemyforge-window");
        foreach (var c in cands)
        {
            try
            {
                if (c.Contains("/") || c.Contains("\\")) { if (!File.Exists(c)) continue; }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(c, "--app=" + url + " --window-size=1440,880 --user-data-dir=\"" + prof + "\" --no-first-run") { UseShellExecute = false });
                return;
            }
            catch { }
        }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { try { System.Diagnostics.Process.Start(Directory.Exists("/Applications") ? "open" : "xdg-open", url); } catch { } }
    }

    // Designs go in an "Enemy Forge" folder next to the exe; if that can't be written, in Documents.
    static string PickDesignDir(string here)
    {
        string a = Path.Combine(here, "Enemy Forge");
        try { Directory.CreateDirectory(a); string probe = Path.Combine(a, ".probe"); File.WriteAllText(probe, ""); File.Delete(probe); return a; }
        catch { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Enemy Forge"); }
    }

    static int Main(string[] args)
    {
        int port = 8190;
        string webArg = null;
        bool serveOnly = false;
        foreach (var a in args)
        {
            if (a == "--serve") serveOnly = true;
            else if (a.StartsWith("--web=")) webArg = a.Substring(6);
            else if (a == "--meta") { designDir = ""; Console.WriteLine(Meta()); return 0; }
            else int.TryParse(a, out port);
        }
        string here = AppDomain.CurrentDomain.BaseDirectory;
        webRoot = Path.GetFullPath(webArg ?? Path.Combine(here, "web"));
        designDir = PickDesignDir(here);
        TcpListener ls = null;
        for (int tryPort = port; tryPort < port + 20 && ls == null; tryPort++)
        {
            // Loopback only: nothing on the network can reach it, and Windows asks no firewall question.
            try { var l = new TcpListener(IPAddress.Loopback, tryPort); l.Start(); ls = l; port = tryPort; } catch (SocketException) { }
        }
        if (ls == null) { Console.Error.WriteLine("No free port near " + port); return 1; }
        Console.WriteLine("\n  Enemy Forge is running:  http://localhost:" + port + "\n  Designs folder:  " + designDir + "\n  Ctrl+C to stop.\n");
        if (!serveOnly)
        {
            // App mode: quit a few seconds after the window stops pinging (closed), or if it never opens.
            OpenWindow("http://localhost:" + port);
            new Thread(() =>
            {
                for (;;)
                {
                    Thread.Sleep(1000);
                    double t = clock.Elapsed.TotalSeconds, p;
                    lock (L) p = lastPing;
                    if (p < 0 ? t > 90 : t - p > 8) Environment.Exit(0);
                }
            }) { IsBackground = true }.Start();
        }
        for (;;)
        {
            var tc = ls.AcceptTcpClient();
            new Thread(() => Serve(tc)) { IsBackground = true }.Start();
        }
    }
}
