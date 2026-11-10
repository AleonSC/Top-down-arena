using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Arena
{
    /// What the balance test shows when it ends: tiles, tables and a few automatic findings, all worked out from the
    /// session recorder's own numbers (so the file and the screen agree). Core/BalanceTest.cs runs the test.
    public class BalanceReport
    {
        public class Tile { public string Label, Value, Sub; }
        public class Table { public string Name; public string[] Head; public List<string[]> Rows = new List<string[]>(); }

        public string Title = "", Path = "";
        public bool Cleared;
        public readonly List<Tile> Tiles = new List<Tile>();
        public readonly List<Table> Tables = new List<Table>();
        public readonly List<string> Findings = new List<string>();

        /// The summary as plain text, for the clipboard.
        public string AsText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Title);
            foreach (var t in Tiles)
                sb.AppendLine(t.Label + ": " + t.Value + (t.Sub.Length > 0 ? " (" + t.Sub + ")" : ""));
            foreach (var t in Tables)
            {
                sb.AppendLine().AppendLine("# " + t.Name).AppendLine(string.Join(" | ", t.Head));
                foreach (var r in t.Rows)
                    sb.AppendLine(string.Join(" | ", r));
            }
            if (Findings.Count > 0)
            {
                sb.AppendLine().AppendLine("# Findings");
                foreach (var f in Findings)
                    sb.AppendLine("- " + f);
            }
            return sb.ToString();
        }
    }

    public partial class SessionRecorder
    {
        static string N(float v) => Math.Round(v).ToString("N0", CultureInfo.InvariantCulture);
        static string D1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Pct(float v) => Math.Round(v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

        /// Worked out from what has been recorded so far; call it when the test ends.
        public BalanceReport BuildReport(bool cleared, string how)
        {
            var rep = new BalanceReport { Cleared = cleared, Path = Path };
            var done = waves.Where(w => w.End >= 0f).ToList();
            float time = done.Sum(w => w.End - w.Start);
            float dealt = 0f, taken = 0f, lowHp = 0f, shots = 0f, hits = 0f;
            int downs = 0;
            var bySrc = new Dictionary<string, float>();
            var hitsSrc = new Dictionary<string, int>();
            var takenBy = new Dictionary<string, float>();
            foreach (var w in waves)
                foreach (var pw in w.Players.Values)
                {
                    dealt += pw.DmgDealt; taken += pw.DmgTaken; lowHp += pw.LowHpTime; downs += pw.Downs;
                    shots += pw.ShotsFired; hits += pw.ShotsHit;
                    foreach (var kv in pw.Src) Add(bySrc, kv.Key, kv.Value);
                    foreach (var kv in pw.SrcHits) Add(hitsSrc, kv.Key, kv.Value);
                    foreach (var kv in pw.TakenBy) Add(takenBy, kv.Key, kv.Value);
                }
            string first = waves.Count > 0 ? waves[0].Wave.ToString() : "-", last = waves.Count > 0 ? waves[waves.Count - 1].Wave.ToString() : "-";
            rep.Title = (cleared ? "Test cleared" : "Test ended: " + how) + "  ·  waves " + first + " to " + last;
            rep.Tiles.Add(new BalanceReport.Tile { Label = "Result", Value = cleared ? "Cleared" : "Ended", Sub = done.Count + " of " + testWaves + " waves" });
            rep.Tiles.Add(new BalanceReport.Tile { Label = "Time", Value = D1(time) + " s", Sub = done.Count > 0 ? "avg " + N(time / done.Count) + " s a wave" : "" });
            rep.Tiles.Add(new BalanceReport.Tile { Label = "Damage dealt", Value = N(dealt), Sub = time > 0f ? "avg DPS " + N(dealt / time) : "" });
            rep.Tiles.Add(new BalanceReport.Tile { Label = "Damage taken", Value = N(taken), Sub = time > 0f ? N(taken / time * 60f) + " a minute" : "" });
            rep.Tiles.Add(new BalanceReport.Tile { Label = "Downs", Value = downs.ToString(), Sub = "low health " + D1(lowHp) + " s" });
            rep.Tiles.Add(new BalanceReport.Tile { Label = "Accuracy", Value = shots > 0f ? Pct(hits / shots) : "-", Sub = N(shots) + " shots" });

            // Waves.
            var wt = new BalanceReport.Table { Name = "Waves", Head = new[] { "Wave", "Clear time", "Enemies", "Elites", "Damage dealt", "DPS", "Taken", "Lowest HP" } };
            foreach (var w in waves)
            {
                float dur = w.End >= 0f ? w.End - w.Start : now - w.Start, d = 0f, t = 0f, minHp = 1f;
                foreach (var pw in w.Players.Values)
                {
                    d += pw.DmgDealt; t += pw.DmgTaken;
                }
                foreach (var s in samples)
                    if (s.Wave == w.Wave)
                        foreach (var kv in s.Hp) minHp = Math.Min(minHp, kv.Value);
                wt.Rows.Add(new[] { w.Wave + (w.Boss != null ? " (" + w.Boss + ")" : ""), D1(dur) + " s" + (w.End < 0f ? " (unfinished)" : ""), w.Spawned.Values.Sum().ToString(), w.Elites.ToString(),
                    N(d), N(dur > 0f ? d / dur : 0f), N(t), samples.Any(s => s.Wave == w.Wave) ? Pct(minHp) : "-" });
            }
            rep.Tables.Add(wt);

            // Weapons and abilities.
            var st = new BalanceReport.Table { Name = "Weapons and abilities", Head = new[] { "Source", "Hits", "Avg hit", "Damage", "Share", "Damage / s" } };
            foreach (var kv in bySrc.OrderByDescending(k => k.Value))
            {
                int h; hitsSrc.TryGetValue(kv.Key, out h);
                st.Rows.Add(new[] { kv.Key, h.ToString("N0", CultureInfo.InvariantCulture), h > 0 ? D1(kv.Value / h) : "-", N(kv.Value), Pct(dealt > 0f ? kv.Value / dealt : 0f), time > 0f ? D1(kv.Value / time) : "-" });
            }
            rep.Tables.Add(st);
            var ut = new BalanceReport.Table { Name = "Ability use", Head = new[] { "Ability", "Casts", "Cooldown (s)", "Casts / min", "Hit on cast" } };
            var casts = new Dictionary<string, int>();
            int instant = 0;
            foreach (var w in waves)
                foreach (var pw in w.Players.Values)
                {
                    foreach (var kv in pw.Abilities) Add(casts, kv.Key, kv.Value);
                    instant += pw.InstantHits;
                }
            foreach (var kv in casts.OrderByDescending(k => k.Value))
            {
                var def = Upgrades.Abilities.FirstOrDefault(a => a != null && a.Name == kv.Key);
                ut.Rows.Add(new[] { kv.Key, kv.Value.ToString(), def != null ? D1(def.Cooldown) : "-", time > 0f ? D1(kv.Value / time * 60f) : "-", "" });
            }
            if (ut.Rows.Count > 0) ut.Rows[0][4] = instant + " enemies in total";
            rep.Tables.Add(ut);

            // Enemies.
            var et = new BalanceReport.Table { Name = "Enemies", Head = new[] { "Enemy", "Spawned", "Killed", "Avg health", "Time alive", "Damage to you", "Share", "Per spawn" } };
            foreach (var kv in enemyTypes.OrderByDescending(k => k.Value.DmgToPlayers))
            {
                var a = kv.Value;
                et.Rows.Add(new[] { kv.Key, a.Spawned.ToString(), a.Killed.ToString(), a.Spawned > 0 ? N(a.HpSum / a.Spawned) : "-", a.Killed > 0 ? D1(a.Lifetime / a.Killed) + " s" : "-",
                    N(a.DmgToPlayers), Pct(taken > 0f ? a.DmgToPlayers / taken : 0f), a.Spawned > 0 ? D1(a.DmgToPlayers / a.Spawned) : "-" });
            }
            rep.Tables.Add(et);

            // Damage taken.
            var tt = new BalanceReport.Table { Name = "Damage taken", Head = new[] { "Source", "Damage", "Share" } };
            foreach (var kv in takenBy.OrderByDescending(k => k.Value))
                tt.Rows.Add(new[] { kv.Key, N(kv.Value), Pct(taken > 0f ? kv.Value / taken : 0f) });
            rep.Tables.Add(tt);

            // Picks.
            var pt = new BalanceReport.Table { Name = "Picks", Head = new[] { "Wave", "Level", "Kind", "Offered", "Picked" } };
            foreach (var pk in picks)
                pt.Rows.Add(new[] { pk.Wave.ToString(), pk.Level.ToString(), pk.Kind ?? "", pk.Offered != null ? string.Join(", ", pk.Offered) : "", pk.Picked ?? "" });
            rep.Tables.Add(pt);

            // Findings: simple rules, so the outliers show up without reading the tables.
            foreach (var kv in bySrc)
                if (dealt > 0f && kv.Value / dealt > 0.45f && bySrc.Count > 1 && !kv.Key.StartsWith("Other", StringComparison.Ordinal))
                    rep.Findings.Add(kv.Key + " did " + Pct(kv.Value / dealt) + " of all damage.");
            if (dealt > 0f && bySrc.ContainsKey("Other (zones, minions, delayed hits)") && bySrc["Other (zones, minions, delayed hits)"] / dealt > 0.4f)
                rep.Findings.Add(Pct(bySrc["Other (zones, minions, delayed hits)"] / dealt) + " of the damage is not credited to a weapon or ability cast (zones, minions, delayed hits). Read it as the lasting abilities' share.");
            int spawnedAll = enemyTypes.Values.Sum(a => a.Spawned);
            foreach (var kv in enemyTypes)
            {
                var a = kv.Value;
                float spawnShare = spawnedAll > 0 ? (float)a.Spawned / spawnedAll : 0f, hurtShare = taken > 0f ? a.DmgToPlayers / taken : 0f;
                if (a.Spawned >= 3 && hurtShare > 0.25f && hurtShare > spawnShare * 3f)
                    rep.Findings.Add(kv.Key + " caused " + Pct(hurtShare) + " of the damage you took from " + Pct(spawnShare) + " of the enemies.");
            }
            var lives = enemyTypes.Where(k => k.Value.Killed >= 3).Select(k => k.Value.Lifetime / k.Value.Killed).ToList();
            if (lives.Count >= 3)
            {
                float avg = lives.Average();
                foreach (var kv in enemyTypes)
                    if (kv.Value.Killed >= 3 && kv.Value.Lifetime / kv.Value.Killed > avg * 2.5f)
                        rep.Findings.Add(kv.Key + " lived " + D1(kv.Value.Lifetime / kv.Value.Killed) + " s on average, against " + D1(avg) + " s for the others.");
            }
            if (shots >= 50f && hits / shots < 0.4f)
                rep.Findings.Add("Weapon accuracy was " + Pct(hits / shots) + ".");
            if (downs > 0)
                rep.Findings.Add("Downed " + downs + " time" + (downs == 1 ? "" : "s") + ".");
            foreach (var w in done)
            {
                float t = 0f;
                foreach (var pw in w.Players.Values) t += pw.DmgTaken;
                if (taken > 0f && t / taken > 0.5f && done.Count > 2)
                    rep.Findings.Add("Wave " + w.Wave + " cost " + Pct(t / taken) + " of the damage you took.");
            }
            if (rep.Findings.Count == 0)
                rep.Findings.Add("Nothing stands out.");
            return rep;
        }
    }
}
