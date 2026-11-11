using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Arena;

// Wave Lab: a tiny web server that runs the real game simulation (Assets/Scripts/Core) and streams it to a browser page
// (web/index.html) over a WebSocket, so a wave design can be played by hand (keyboard + mouse, or touch) or by the bot, on
// a phone too. One shared match at a time. Run it with Tools/WaveLab/run.sh [port].
static class Lab
{
    static readonly object L = new object();
    static Session sess;
    static int nextGamePort = 17100;
    static PlayerInput inp;
    static bool god;
    static BotSkill botSkill = BotSkill.Off;
    static string webRoot;
    static readonly List<Client> clients = new List<Client>();

    // ---- JSON out (hand-written: no JSON library in the mono set we build with) ----
    static string N(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "0" : Math.Round(v, 1).ToString("0.#", CultureInfo.InvariantCulture);
    static string S(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s ?? "")
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\r') { }
            else if (c < 32) sb.Append(' ');
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
    static string Col(Rgba c) => "\"#" + ((int)(Clamp(c.R) * 255)).ToString("x2") + ((int)(Clamp(c.G) * 255)).ToString("x2") + ((int)(Clamp(c.B) * 255)).ToString("x2") + "\"";
    static float Clamp(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

    // ---- JSON in: a small recursive parser (objects -> Dictionary, arrays -> List, numbers -> double) ----
    static object Parse(string s) { int i = 0; return Val(s, ref i); }
    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    static object Val(string s, ref int i)
    {
        Ws(s, ref i);
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++;
            for (;;)
            {
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                if (s[i] == ',') { i++; continue; }
                string k = (string)Val(s, ref i); Ws(s, ref i); i++;  // ':'
                d[k] = Val(s, ref i);
            }
        }
        if (c == '[')
        {
            var l = new List<object>(); i++;
            for (;;)
            {
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                if (s[i] == ',') { i++; continue; }
                l.Add(Val(s, ref i));
            }
        }
        if (c == '"')
        {
            var sb = new StringBuilder(); i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i++];
                    if (e == 'n') sb.Append('\n');
                    else if (e == 't') sb.Append('\t');
                    else if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; }
                    else sb.Append(e);
                }
                else sb.Append(s[i++]);
            }
            i++;
            return sb.ToString();
        }
        int st = i;
        while (i < s.Length && ",}] \n\r\t".IndexOf(s[i]) < 0) i++;
        string t = s.Substring(st, i - st);
        if (t == "true") return true;
        if (t == "false") return false;
        if (t == "null") return null;
        return double.Parse(t, CultureInfo.InvariantCulture);
    }
    static double D(Dictionary<string, object> m, string k, double def = 0) => m.TryGetValue(k, out var v) && v is double d ? d : def;
    static bool B(Dictionary<string, object> m, string k) => m.TryGetValue(k, out var v) && v is bool b && b;
    static string Str(Dictionary<string, object> m, string k, string def = "") => m.TryGetValue(k, out var v) && v is string s ? s : def;

    // ---- Static data the page needs once: enemies, squads, built-in sets, classes ----
    static string Meta()
    {
        var sb = new StringBuilder("{\"enemies\":[");
        bool first = true;
        foreach (EnemyType t in Enum.GetValues(typeof(EnemyType)))
        {
            var d = Defs.Get(t);
            sb.Append(first ? "" : ",").Append("{\"id\":").Append((int)t).Append(",\"name\":").Append(S(d.Name)).Append(",\"key\":").Append(S(t.ToString()))
              .Append(",\"hp\":").Append(N(d.Hp)).Append(",\"r\":").Append(N(d.Radius)).Append(",\"speed\":").Append(N(d.Speed)).Append(",\"boss\":").Append(d.IsBoss ? "true" : "false")
              .Append(",\"sides\":").Append(d.Sides).Append(",\"group\":").Append(S(d.Picker.ToString())).Append(",\"color\":").Append(Col(d.Color)).Append(",\"blurb\":").Append(S(d.Blurb)).Append('}');
            first = false;
        }
        sb.Append("],\"squads\":[");
        first = true;
        foreach (var q in WaveSet.AllSquads())
        {
            sb.Append(first ? "" : ",").Append("{\"name\":").Append(S(q.Name)).Append(",\"members\":").Append(S(WaveSet.MembersText(q.Members))).Append('}');
            first = false;
        }
        sb.Append("],\"sets\":[");
        first = true;
        foreach (var n in DesignedWaves.Names)
        {
            sb.Append(first ? "" : ",").Append("{\"name\":").Append(S(n)).Append(",\"text\":").Append(S(DesignedWaves.Get(n).ToText())).Append('}');
            first = false;
        }
        sb.Append("],\"roles\":[");
        string[] roles = { "Tank", "Healer", "DPS", "Wildcard" };
        string[] kits = Enum.GetNames(typeof(Kit));
        for (int r = 0; r < 3; r++)
        {
            sb.Append(r > 0 ? "," : "").Append("{\"name\":").Append(S(roles[r])).Append(",\"kits\":[");
            for (int k = 0; k < kits.Length; k++)
            {
                sb.Append(k > 0 ? "," : "").Append("{\"name\":").Append(S(kits[k])).Append(",\"weapons\":[");
                for (int w = 0; w < 3; w++)
                {
                    string wn;
                    try { wn = Defs.Weapon((Role)r, (Kit)k, w).Name; } catch { wn = "Weapon " + (w + 1); }
                    sb.Append(w > 0 ? "," : "").Append(S(wn));
                }
                sb.Append("],\"abilities\":[");
                for (int a = 0; a < Upgrades.RoleAbilityChoices; a++)
                    sb.Append(a > 0 ? "," : "").Append(S(Upgrades.Ability(Upgrades.RoleAbility((Role)r, (Kit)k, a)).Name));
                sb.Append("],\"ults\":[");
                bool fu = true;
                for (int u = 0; u < Upgrades.UltKeys; u++)
                {
                    var id = Upgrades.Ultimate((Role)r, (Kit)k, u);
                    if (u > 0 && Upgrades.AltUltimate((Role)r, (Kit)k, u) == AbilityId.None) continue;
                    sb.Append(fu ? "" : ",").Append("{\"key\":").Append(u).Append(",\"name\":").Append(S(Upgrades.Ability(id).Name)).Append('}');
                    fu = false;
                }
                sb.Append("]}");
            }
            sb.Append("]}");
        }
        return sb.Append("]}").ToString();
    }

    // Parses a .waves text into JSON the editor can edit.
    static string ParseWaves(string text)
    {
        List<string> problems;
        var set = WaveSet.Parse(text, out problems);
        var sb = new StringBuilder("{\"name\":" + S(set.Name) + ",\"loop\":" + (set.Loop ? "true" : "false") + ",\"points\":[");
        for (int i = 0; i < set.Points.Length; i++)
            sb.Append(i > 0 ? "," : "").Append('[').Append(N(set.Points[i].X)).Append(',').Append(N(set.Points[i].Y)).Append(']');
        sb.Append("],\"waves\":[");
        for (int k = 0; k < set.Waves.Count; k++)
        {
            var w = set.Waves[k];
            sb.Append(k > 0 ? "," : "").Append("{\"level\":").Append(w.Level).Append(",\"groups\":[");
            for (int g = 0; g < w.Groups.Count; g++)
            {
                var gr = w.Groups[g];
                sb.Append(g > 0 ? "," : "").Append("{\"name\":").Append(S(gr.Name)).Append(",\"squad\":").Append(gr.Squad == null ? "null" : S(gr.Squad))
                  .Append(",\"members\":").Append(S(WaveSet.MembersText(gr.Members))).Append(",\"from\":").Append(S(gr.From)).Append(",\"at\":").Append(N(gr.At))
                  .Append(",\"every\":").Append(N(gr.Every)).Append(",\"arrive\":").Append(S(WaveSet.ArriveName(gr.Arrive))).Append('}');
            }
            sb.Append("],\"props\":[");
            for (int p = 0; p < w.Props.Count; p++)
                sb.Append(p > 0 ? "," : "").Append("{\"kind\":").Append(S(WaveSet.PropName(w.Props[p].Kind))).Append(",\"x\":").Append(N(w.Props[p].Pos.X)).Append(",\"y\":").Append(N(w.Props[p].Pos.Y)).Append('}');
            sb.Append("]}");
        }
        sb.Append("],\"problems\":[");
        for (int i = 0; i < problems.Count; i++)
            sb.Append(i > 0 ? "," : "").Append(S(problems[i]));
        return sb.Append("]}").ToString();
    }

    // ---- The match ----
    static string StartMatch(Dictionary<string, object> m)
    {
        List<string> problems;
        var set = WaveSet.Parse(Str(m, "waves"), out problems);
        if (set.Waves.Sum(w => w.EnemyCount) == 0)
            return "Nothing to play: add an enemy group first";
        var old = sess;
        sess = new Session { AutoStart = 1 };
        var role = (Role)(int)D(m, "role", 2);
        var kit = (Kit)(int)D(m, "kit", 0);
        int weapon = (int)D(m, "weapon", 0);
        if (!sess.Host(nextGamePort++, "You", role, weapon, (int)D(m, "ability", 0), kit, 0, (int)D(m, "ult", 0)))
            return "Couldn't start the match: " + sess.Status;
        old?.Leave("");
        sess.Diff.Set(DiffKnob.Campaign, 0f);
        sess.Plan = set.ToPlan();
        sess.DesignTest = set;
        sess.SetMode(true, false);
        sess.StartMatch();
        var g = sess.Game;
        g.PracticeIndex = Math.Max(-1, (int)D(m, "wave", 1) - 2);  // the index of the wave just cleared: the next one is the start wave
        g.SetMyAutoReady(true);
        god = B(m, "god");
        repSet = set;
        rep = new List<Row>(); cur = null; wasFighting = false; repSent = false; repWho = role + " " + kit + ", " + Defs.Weapon(role, kit, weapon).Name;
        botSkill = BotFrom(Str(m, "bot", "off"));
        inp = new PlayerInput { AimAt = new V2(800, 450) };
        return null;
    }

    static BotSkill BotFrom(string s)
    {
        switch (s) { case "casual": return BotSkill.Casual; case "good": return BotSkill.Good; case "expert": return BotSkill.Expert; default: return BotSkill.Off; }
    }

    static void Handle(Dictionary<string, object> m, Client c)
    {
        string t = Str(m, "t");
        lock (L)
        {
            if (t == "in")
            {
                inp.Move = new V2((float)D(m, "mx"), (float)D(m, "my"));
                if (inp.Move.Length > 1f) inp.Move = inp.Move.Normalized();
                inp.AimAt = new V2((float)D(m, "ax", 800), (float)D(m, "ay", 450));
                inp.Fire = B(m, "fire"); inp.Ability = B(m, "ab"); inp.AbilityQ = B(m, "q"); inp.AbilityE = B(m, "e"); inp.AbilityShift = B(m, "sh");
                inp.Ultimate = B(m, "ult"); inp.Parry = B(m, "parry"); inp.Dodge = B(m, "dodge"); inp.WeaponAction = B(m, "alt");
            }
            else if (t == "start") { string err = StartMatch(m); c.Send("{\"t\":\"started\",\"err\":" + (err == null ? "null" : S(err)) + "}"); }
            else if (t == "bot") botSkill = BotFrom(Str(m, "level", "off"));
            else if (t == "god") god = B(m, "on");
            else if (t == "report" && sess != null && rep != null) c.Send(ReportJson(sess.Game));
            else if (t == "stop" && sess != null) { sess.Game.Running = false; sess.Leave(""); sess = null; }
            else if (t == "parse") c.Send("{\"t\":\"parsed\",\"set\":" + ParseWaves(Str(m, "text")) + "}");
            else if (t == "wave" && sess != null) sess.Game.PracticeIndex = Math.Max(-1, (int)D(m, "n", 1) - 2);
        }
    }

    // ---- Run report: one row per wave fought (what a bot run or your own run did), sent when the run ends or on request ----
    class Row { public int Wave, Level, Enemies, Kills, Downs; public float K0, D0, T0, H0; public float StartT, Clear, MinHp = 1f, Dealt, Taken, Healing; public bool Cleared; }
    static List<Row> rep;
    static Row cur;
    static WaveSet repSet;
    static bool wasFighting, repSent, wasDown;
    static string repWho = "";

    static void Track(Game g, Player me)
    {
        if (rep == null || me == null) return;
        bool fighting = g.State == MatchState.Fighting;
        if (fighting && !wasFighting)
        {
            int i = g.PracticeIndex;
            var w = repSet != null && i >= 0 && i < repSet.Waves.Count ? repSet.Waves[i] : null;
            cur = new Row { Wave = i + 1, Level = w != null ? w.Level : 0, Enemies = w != null ? w.EnemyCount : 0, StartT = g.RunTime, K0 = me.Run.Kills, D0 = me.Run.DamageDealt, T0 = me.Run.DamageTaken, H0 = me.Run.HealingDone };
            wasDown = false;
        }
        if (fighting && cur != null)
        {
            cur.MinHp = Math.Min(cur.MinHp, me.MaxHp > 0 ? Math.Max(0f, me.Hp) / me.MaxHp : 1f);
            if (me.Downed && !wasDown) cur.Downs++;
            wasDown = me.Downed;
        }
        if (wasFighting && !fighting && cur != null)
        {
            cur.Clear = g.RunTime - cur.StartT;
            cur.Cleared = g.State == MatchState.Intermission || g.State == MatchState.Victory;
            cur.Kills = (int)(me.Run.Kills - cur.K0); cur.Dealt = me.Run.DamageDealt - cur.D0; cur.Taken = me.Run.DamageTaken - cur.T0; cur.Healing = me.Run.HealingDone - cur.H0;
            rep.Add(cur); cur = null;
        }
        wasFighting = fighting;
        if ((g.State == MatchState.Victory || g.State == MatchState.GameOver) && !repSent)
        {
            repSent = true;
            string r = ReportJson(g);
            foreach (var c in clients.ToArray()) c.Send(r);
        }
    }

    static string ReportJson(Game g)
    {
        var rows = new List<Row>(rep ?? new List<Row>());
        if (cur != null) rows.Add(new Row { Wave = cur.Wave, Level = cur.Level, Enemies = cur.Enemies, StartT = cur.StartT, Clear = g.RunTime - cur.StartT, MinHp = cur.MinHp, Downs = cur.Downs });
        string result = g.State == MatchState.Victory ? "victory" : g.State == MatchState.GameOver ? "wiped" : "stopped";
        var sb = new StringBuilder("{\"t\":\"report\",\"set\":" + S(repSet != null ? repSet.Name : "") + ",\"who\":" + S(repWho) + ",\"bot\":" + S(botSkill.ToString().ToLowerInvariant())
            + ",\"god\":" + (god ? "true" : "false") + ",\"result\":" + S(result) + ",\"time\":" + N(g.RunTime) + ",\"rows\":[");
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            sb.Append(i > 0 ? "," : "").Append("{\"wave\":").Append(r.Wave).Append(",\"level\":").Append(r.Level).Append(",\"enemies\":").Append(r.Enemies).Append(",\"cleared\":").Append(r.Cleared ? "true" : "false")
              .Append(",\"clear\":").Append(N(r.Clear)).Append(",\"kills\":").Append(r.Kills).Append(",\"dealt\":").Append(N(r.Dealt)).Append(",\"taken\":").Append(N(r.Taken))
              .Append(",\"healing\":").Append(N(r.Healing)).Append(",\"minHp\":").Append(N(r.MinHp * 100)).Append(",\"downs\":").Append(r.Downs).Append('}');
        }
        return sb.Append("]}").ToString();
    }

    // One simulation step at 60 Hz; a snapshot goes out every second step.
    static string Snapshot()
    {
        var g = sess.Game;
        var sb = new StringBuilder(4096);
        sb.Append("{\"t\":\"s\",\"state\":").Append((int)g.State).Append(",\"wave\":").Append(g.Wave).Append(",\"idx\":").Append(g.PracticeIndex)
          .Append(",\"left\":").Append(g.Remaining + g.DesignPending).Append(",\"score\":").Append(g.Score).Append(",\"time\":").Append(N(g.RunTime))
          .Append(",\"bot\":").Append(S(botSkill.ToString().ToLowerInvariant())).Append(",\"god\":").Append(god ? "true" : "false").Append(",\"me\":").Append(g.MyId).Append(",\"players\":[");
        bool first = true;
        foreach (var p in g.Players.Values)
        {
            sb.Append(first ? "" : ",").Append("{\"id\":").Append(p.Id).Append(",\"x\":").Append(N(p.Pos.X)).Append(",\"y\":").Append(N(p.Pos.Y)).Append(",\"a\":").Append(N(p.Aim))
              .Append(",\"hp\":").Append(N(p.Hp)).Append(",\"mhp\":").Append(N(p.MaxHp)).Append(",\"r\":").Append(N(p.Def.BodyR)).Append(",\"down\":").Append(p.Downed ? "true" : "false")
              .Append(",\"fire\":").Append(p.FiringSeen ? "true" : "false").Append(",\"inv\":").Append(p.InvulnT > 0 || p.BarrierT > 0 ? "true" : "false").Append(",\"c\":").Append(Col(p.Def.Color)).Append('}');
            first = false;
        }
        sb.Append("],\"enemies\":[");
        first = true;
        foreach (var e in g.Enemies.Values)
        {
            sb.Append(first ? "" : ",").Append("[").Append((int)e.Type).Append(',').Append(N(e.Pos.X)).Append(',').Append(N(e.Pos.Y)).Append(',').Append(N(e.Hp)).Append(',').Append(N(e.MaxHp))
              .Append(',').Append(N(e.Radius)).Append(',').Append(N(e.Facing)).Append(',').Append(e.Elite ? 1 : 0).Append(',').Append(e.SpawnT > 0 ? 1 : 0).Append(']');
            first = false;
        }
        sb.Append("],\"bullets\":[");
        first = true;
        foreach (var b in g.Bullets.Values)
        {
            sb.Append(first ? "" : ",").Append('[').Append(N(b.Pos.X)).Append(',').Append(N(b.Pos.Y)).Append(',').Append(N(b.Radius)).Append(',').Append(b.Owner > 0 ? 1 : 0).Append(',').Append(b.FromBoss ? 1 : 0).Append(']');
            first = false;
        }
        sb.Append("],\"props\":[");
        first = true;
        foreach (var p in g.Props.Values)
        {
            sb.Append(first ? "" : ",").Append('[').Append((int)p.Kind).Append(',').Append(N(p.Pos.X)).Append(',').Append(N(p.Pos.Y)).Append(',').Append(p.Open ? 1 : 0).Append(']');
            first = false;
        }
        sb.Append("],\"zones\":[");
        first = true;
        foreach (var z in g.Zones.Values)
        {
            sb.Append(first ? "" : ",").Append('[').Append((int)z.Kind).Append(',').Append(N(z.Pos.X)).Append(',').Append(N(z.Pos.Y)).Append(',').Append(N(z.Radius)).Append(']');
            first = false;
        }
        sb.Append("],\"orbs\":[");
        first = true;
        foreach (var o in g.Orbs.Values)
        {
            sb.Append(first ? "" : ",").Append('[').Append(N(o.Pos.X)).Append(',').Append(N(o.Pos.Y)).Append(']');
            first = false;
        }
        return sb.Append("]}").ToString();
    }

    static void SimLoop()
    {
        const float dt = 1f / 60f;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        double next = 0;
        long tick = 0;
        while (true)
        {
            double now = clock.Elapsed.TotalSeconds;
            if (now < next) { Thread.Sleep(1); continue; }
            next += dt;
            if (now - next > 0.25) next = now;  // fell far behind: don't spiral
            lock (L)
            {
                if (sess == null || !sess.Game.Running) continue;
                try
                {
                    var g = sess.Game;
                    sess.Poll();
                    g.Bot = botSkill != BotSkill.Off;
                    g.BotLevel = botSkill;
                    g.BotRerolls = false;
                    Player me;
                    if (g.Players.TryGetValue(g.MyId, out me))
                    {
                        me.PendingPicks = me.PendingLegendary = me.PendingMods = 0;  // a wave tester: no upgrade cards, you play the class as it starts
                        me.Offer = null;
                        if (god != me.DevGod) g.DevSetGod(g.MyId, god);
                    }
                    g.FixedStep(dt, inp);
                    g.Update(dt);
                    g.Sounds.Clear();
                    Track(g, me);
                    if (++tick % 2 == 0 && clients.Count > 0)
                    {
                        string snap = Snapshot();
                        foreach (var c in clients.ToArray()) c.Send(snap);
                    }
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); }
            }
        }
    }

    // ---- HTTP + a minimal WebSocket (RFC 6455: text frames, no extensions) ----
    class Client
    {
        public NetworkStream Stream;
        readonly Queue<string> q = new Queue<string>();
        public bool Dead;
        public void Send(string s)
        {
            lock (q) { if (q.Count > 4) q.Dequeue(); q.Enqueue(s); Monitor.Pulse(q); }  // a slow phone drops old frames instead of lagging
        }
        public void Pump()
        {
            while (!Dead)
            {
                string s;
                lock (q) { while (q.Count == 0 && !Dead) Monitor.Wait(q, 500); if (q.Count == 0) continue; s = q.Dequeue(); }
                try { WriteFrame(Stream, Encoding.UTF8.GetBytes(s)); } catch { Dead = true; }
            }
        }
    }

    static void WriteFrame(NetworkStream st, byte[] p)
    {
        var h = new List<byte> { 0x81 };
        if (p.Length < 126) h.Add((byte)p.Length);
        else if (p.Length < 65536) { h.Add(126); h.Add((byte)(p.Length >> 8)); h.Add((byte)p.Length); }
        else { h.Add(127); for (int i = 7; i >= 0; i--) h.Add((byte)((long)p.Length >> (8 * i))); }
        lock (st) { st.Write(h.ToArray(), 0, h.Count); st.Write(p, 0, p.Length); }
    }

    static bool ReadN(Stream st, byte[] b, int n)
    {
        int o = 0;
        while (o < n) { int r = st.Read(b, o, n - o); if (r <= 0) return false; o += r; }
        return true;
    }

    static void Serve(TcpClient tc)
    {
        try
        {
            tc.NoDelay = true;
            var st = tc.GetStream();
            var req = new StringBuilder();
            var one = new byte[1];
            while (!req.ToString().EndsWith("\r\n\r\n")) { if (st.Read(one, 0, 1) <= 0) return; req.Append((char)one[0]); if (req.Length > 16384) return; }
            string[] lines = req.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string[] first = lines[0].Split(' ');
            var hdr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in lines.Skip(1)) { int i = l.IndexOf(':'); if (i > 0) hdr[l.Substring(0, i)] = l.Substring(i + 1).Trim(); }
            string path = first[1].Split('?')[0];
            if (hdr.TryGetValue("Upgrade", out var up) && up.ToLowerInvariant() == "websocket") { Socket(st, hdr); return; }
            if (path == "/api/meta") { Reply(st, "application/json", Encoding.UTF8.GetBytes(Meta())); return; }
            if (path == "/") path = "/index.html";
            string file = Path.GetFullPath(Path.Combine(webRoot, path.TrimStart('/')));
            if (!file.StartsWith(webRoot) || !File.Exists(file)) { Reply(st, "text/plain", Encoding.UTF8.GetBytes("not found"), "404 Not Found"); return; }
            string ext = Path.GetExtension(file);
            Reply(st, ext == ".html" ? "text/html; charset=utf-8" : ext == ".js" ? "text/javascript" : ext == ".css" ? "text/css" : "application/octet-stream", File.ReadAllBytes(file));
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

    static void Socket(NetworkStream st, Dictionary<string, string> hdr)
    {
        string key = hdr["Sec-WebSocket-Key"];
        string acc = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var resp = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + acc + "\r\n\r\n");
        st.Write(resp, 0, resp.Length);
        var c = new Client { Stream = st };
        lock (L) clients.Add(c);
        new Thread(c.Pump) { IsBackground = true }.Start();
        try
        {
            var hb = new byte[8];
            while (true)
            {
                if (!ReadN(st, hb, 2)) break;
                int op = hb[0] & 0x0f;
                long len = hb[1] & 0x7f;
                if (len == 126) { ReadN(st, hb, 2); len = (hb[0] << 8) | hb[1]; }
                else if (len == 127) { var e = new byte[8]; ReadN(st, e, 8); len = 0; for (int i = 0; i < 8; i++) len = (len << 8) | e[i]; }
                var mask = new byte[4];
                if ((hb[1] & 0x80) != 0 || true) ReadN(st, mask, 4);
                var data = new byte[len];
                if (len > 0 && !ReadN(st, data, (int)len)) break;
                for (int i = 0; i < len; i++) data[i] ^= mask[i % 4];
                if (op == 8) break;
                if (op != 1) continue;
                try { Handle((Dictionary<string, object>)Parse(Encoding.UTF8.GetString(data)), c); } catch (Exception ex) { Console.Error.WriteLine(ex.Message); }
            }
        }
        catch { }
        c.Dead = true;
        lock (L) clients.Remove(c);
    }

    static int Main(string[] args)
    {
        int port = args.Length > 0 ? int.Parse(args[0]) : 8080;
        string here = AppDomain.CurrentDomain.BaseDirectory;
        webRoot = Path.GetFullPath(Path.Combine(here, "web"));
        new Thread(SimLoop) { IsBackground = true }.Start();
        var ls = new TcpListener(IPAddress.Any, port);
        ls.Start();
        string lan = null;
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up || ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork && lan == null) lan = ua.Address.ToString();
            }
        }
        catch { }
        Console.WriteLine("\n  Wave Lab is running.\n  This computer:  http://localhost:" + port + "\n  Phone (same Wi-Fi):  http://" + (lan ?? "<this computer's IP>") + ":" + port + "\n  Ctrl+C to stop.\n");
        for (;;)
        {
            var tc = ls.AcceptTcpClient();
            new Thread(() => Serve(tc)) { IsBackground = true }.Start();
        }
    }
}
