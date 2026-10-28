using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Arena
{
    /// Dev tool: records a run on the host for balancing (turn it on in Settings or with --record).
    /// Everything happens on the host, which decides every hit, so the numbers cover all players.
    /// The file is JSON, rewritten after every wave (so a crash keeps what came before) and at
    /// the end. Tools/SessionReport/report.py summarizes one or many of them.
    public class SessionRecorder
    {
        public const int FormatVersion = 1;
        public readonly string Path;
        public bool Ended { get; private set; }

        class PlayerInfo { public int Id; public string Name, Role, Kit, Weapon, Ability, Ultimate; }

        class PlayerWave
        {
            public float DmgDealt, DmgToBosses, DmgTaken, Healing, OrbHealing, LowHpTime, HpStart, HpEnd, MaxHp;
            public float ParryBlocked;
            public int Downs, Revives, Kills, ParryPerfect, ParryBreaks, LevelEnd, Orbs;
            public readonly Dictionary<string, float> TakenBy = new Dictionary<string, float>();
            public readonly Dictionary<string, int> Abilities = new Dictionary<string, int>();
        }

        class WaveRec
        {
            public int Wave;
            public string Boss;
            public string Path;  // the divergent path this wave was played on (null on the plain arena)
            public float Start, End = -1f;
            public int Elites;
            public readonly Dictionary<string, int> Spawned = new Dictionary<string, int>(), Killed = new Dictionary<string, int>();
            public readonly Dictionary<int, PlayerWave> Players = new Dictionary<int, PlayerWave>();
        }

        class BossRec
        {
            public int Id, Wave;
            public string Name;
            public float MaxHp, Spawned, Died = -1f, PlayerDmgTaken;
            public int Downs;
            public readonly Dictionary<int, float> DmgBy = new Dictionary<int, float>();
        }

        class EnemyAgg { public int Spawned, Killed, Elites; public float Lifetime, DmgToPlayers; }

        class PickRec { public int Player, Wave, Level, RerollsLeft; public string Kind, Picked; public string[] Offered; }
        class DownRec { public int Player, Wave; public float Time; public string Source; }
        class Sample { public float Time; public int Wave, Enemies, Bullets; public Dictionary<int, float> Hp = new Dictionary<int, float>(); }

        readonly List<PlayerInfo> players = new List<PlayerInfo>();
        readonly List<WaveRec> waves = new List<WaveRec>();
        readonly Dictionary<int, BossRec> bosses = new Dictionary<int, BossRec>();
        readonly List<BossRec> bossOrder = new List<BossRec>();
        readonly Dictionary<string, EnemyAgg> enemyTypes = new Dictionary<string, EnemyAgg>();
        readonly Dictionary<int, float> enemyBorn = new Dictionary<int, float>();
        readonly List<PickRec> picks = new List<PickRec>();
        readonly List<DownRec> downs = new List<DownRec>();
        readonly List<Sample> samples = new List<Sample>();
        readonly Dictionary<int, int> rerolls = new Dictionary<int, int>();
        readonly DateTime started = DateTime.Now;
        int playerCount, startWave;
        string modifiers = "";
        float sampleT, now;
        string outcome = "in progress";
        int endWave, endScore;
        float endTime;

        public SessionRecorder(string folder)
        {
            Directory.CreateDirectory(folder);
            Path = System.IO.Path.Combine(folder, "session-" + started.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
        }

        WaveRec Current => waves.Count > 0 ? waves[waves.Count - 1] : null;

        PlayerWave Pw(int id)
        {
            var w = Current;
            if (w == null)
                return null;
            PlayerWave pw;
            if (!w.Players.TryGetValue(id, out pw))
                w.Players[id] = pw = new PlayerWave();
            return pw;
        }

        static void Add<K>(Dictionary<K, float> d, K k, float v) => d[k] = (d.ContainsKey(k) ? d[k] : 0f) + v;
        static void Add<K>(Dictionary<K, int> d, K k, int v) => d[k] = (d.ContainsKey(k) ? d[k] : 0) + v;

        EnemyAgg Agg(string type)
        {
            EnemyAgg a;
            if (!enemyTypes.TryGetValue(type, out a))
                enemyTypes[type] = a = new EnemyAgg();
            return a;
        }

        // --- Hooks (called by Game on the host) ---------------------------------------

        public void Begin(Game g, int startWaveNumber)
        {
            startWave = startWaveNumber;
            modifiers = string.Join(", ", Modifiers.Names(g.Mods).ToArray());
            playerCount = g.Players.Count;
            foreach (var p in g.Players.Values)
                players.Add(new PlayerInfo
                {
                    Id = p.Id, Name = p.Name, Role = p.Def.Name, Kit = Defs.KitName(p.Kit), Weapon = p.Weapon.Name,
                    Ability = Upgrades.Ability(p.RoleAbility).Name, Ultimate = Upgrades.Ability(Upgrades.Ultimate(p.Role, p.Kit)).Name,
                });
            Save();
        }

        public void Tick(Game g, float dt)
        {
            now = g.RunTime;
            if (g.State != MatchState.Fighting)
                return;
            foreach (var p in g.Players.Values)
                if (!p.Downed && p.Hp < p.MaxHp * 0.25f)
                {
                    var pw = Pw(p.Id);
                    if (pw != null)
                        pw.LowHpTime += dt;
                }
            sampleT -= dt;
            if (sampleT > 0f)
                return;
            sampleT = 5f;
            var s = new Sample { Time = now, Wave = g.Wave, Enemies = g.Enemies.Count, Bullets = g.Bullets.Count };
            foreach (var p in g.Players.Values)
                s.Hp[p.Id] = p.Downed ? 0f : p.Hp / Math.Max(1f, p.MaxHp);
            samples.Add(s);
        }

        public void WaveStart(Game g, int wave, string boss)
        {
            var w = new WaveRec { Wave = wave, Boss = boss, Start = g.RunTime, Path = g.OnPath && wave % 5 != 0 ? Paths.Get(g.CurrentPath).Name : null };
            waves.Add(w);
            foreach (var p in g.Players.Values)
            {
                var pw = Pw(p.Id);
                pw.HpStart = p.Hp;
                pw.MaxHp = p.MaxHp;
            }
        }

        public void WaveEnd(Game g)
        {
            var w = Current;
            if (w == null || w.End >= 0f)
                return;
            w.End = g.RunTime;
            foreach (var p in g.Players.Values)
            {
                var pw = Pw(p.Id);
                pw.HpEnd = p.Downed ? 0f : p.Hp;
                pw.MaxHp = p.MaxHp;
                pw.LevelEnd = p.Level;
            }
            Save();
        }

        public void End(Game g, string how)
        {
            if (Ended)
                return;
            WaveEnd(g);
            Ended = true;
            outcome = how;
            endWave = g.Wave;
            endTime = g.RunTime;
            endScore = g.Score;
            Save();
        }

        public void EnemySpawned(Enemy e)
        {
            enemyBorn[e.Id] = now;
            var a = Agg(e.Def.Name);
            a.Spawned++;
            if (e.Elite)
                a.Elites++;
            var w = Current;
            if (w != null)
            {
                Add(w.Spawned, e.Def.Name, 1);
                if (e.Elite)
                    w.Elites++;
            }
            if (e.Def.IsBoss)
            {
                var b = new BossRec { Id = e.Id, Name = e.Def.Name, Wave = w != null ? w.Wave : 0, MaxHp = e.MaxHp, Spawned = now };
                bosses[e.Id] = b;
                bossOrder.Add(b);
            }
        }

        public void EnemyKilled(Enemy e)
        {
            var a = Agg(e.Def.Name);
            a.Killed++;
            float born;
            if (enemyBorn.TryGetValue(e.Id, out born))
            {
                a.Lifetime += now - born;
                enemyBorn.Remove(e.Id);
            }
            var w = Current;
            if (w != null)
                Add(w.Killed, e.Def.Name, 1);
            BossRec b;
            if (bosses.TryGetValue(e.Id, out b))
                b.Died = now;
            if (e.LastHitBy != 0)
            {
                var pw = Pw(e.LastHitBy);
                if (pw != null)
                    pw.Kills++;
            }
        }

        public void DamageDealt(int attacker, Enemy e, float amount)
        {
            if (amount <= 0f || attacker == 0)
                return;
            var pw = Pw(attacker);
            if (pw != null)
            {
                pw.DmgDealt += amount;
                if (e.Def.IsBoss)
                    pw.DmgToBosses += amount;
            }
            BossRec b;
            if (bosses.TryGetValue(e.Id, out b))
                Add(b.DmgBy, attacker, amount);
        }

        public void DamageTaken(Player p, float amount, string source)
        {
            if (amount <= 0f)
                return;
            var pw = Pw(p.Id);
            if (pw != null)
            {
                pw.DmgTaken += amount;
                Add(pw.TakenBy, source, amount);
            }
            // Credit the enemy type, e.g. "Brute (contact)" -> "Brute".
            int paren = source.IndexOf(" (", StringComparison.Ordinal);
            string type = paren > 0 ? source.Substring(0, paren) : source;
            if (enemyTypes.ContainsKey(type))
                enemyTypes[type].DmgToPlayers += amount;
            foreach (var b in bossOrder)
                if (b.Died < 0f && b.Spawned <= now)
                    b.PlayerDmgTaken += amount;
        }

        public void Downed(Game g, Player p, string source)
        {
            downs.Add(new DownRec { Player = p.Id, Wave = g.Wave, Time = g.RunTime, Source = source });
            var pw = Pw(p.Id);
            if (pw != null)
                pw.Downs++;
            foreach (var b in bossOrder)
                if (b.Died < 0f)
                    b.Downs++;
        }

        public void Revived(Player p, Player by)
        {
            if (by != null && by != p)
            {
                var pw = Pw(by.Id);
                if (pw != null)
                    pw.Revives++;
            }
        }

        public void Healed(Player source, float amount)
        {
            if (source == null || amount <= 0f)
                return;
            var pw = Pw(source.Id);
            if (pw != null)
                pw.Healing += amount;
        }

        public void Orb(Player p, float healed)
        {
            var pw = Pw(p.Id);
            if (pw == null)
                return;
            pw.Orbs++;
            pw.OrbHealing += healed;
        }

        public void AbilityUsed(Player p, AbilityId id)
        {
            var pw = Pw(p.Id);
            if (pw != null)
                Add(pw.Abilities, Upgrades.Ability(id).Name, 1);
        }

        public void Parry(Player p, float dmg, bool perfect, bool broke)
        {
            var pw = Pw(p.Id);
            if (pw == null)
                return;
            if (perfect)
                pw.ParryPerfect++;
            else
                pw.ParryBlocked += dmg;
            if (broke)
                pw.ParryBreaks++;
        }

        public void Picked(Game g, Player p, int[] offer, int picked, string kind)
        {
            var names = new string[offer.Length];
            for (int i = 0; i < offer.Length; i++)
                names[i] = Upgrades.All[offer[i]].Name;
            picks.Add(new PickRec { Player = p.Id, Wave = g.Wave, Level = p.Level, Kind = kind, Offered = names, Picked = Upgrades.All[picked].Name, RerollsLeft = p.Rerolls });
        }

        public void Rerolled(Player p) => Add(rerolls, p.Id, 1);

        // --- Writing --------------------------------------------------------------------

        public void Save()
        {
            try
            {
                File.WriteAllText(Path, ToJson());
            }
            catch (Exception)
            {
                // A recording problem should never break the game.
            }
        }

        string ToJson()
        {
            var j = new Json();
            j.Open();
            j.Field("format", FormatVersion);
            j.Field("gameVersion", Session.ProtocolVersion);
            j.Field("started", started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            j.Field("playerCount", playerCount);
            j.Field("startWave", startWave);
            j.Field("modifiers", modifiers);
            j.Key("end"); j.Open();
            j.Field("outcome", outcome); j.Field("wave", endWave); j.Field("time", endTime); j.Field("score", endScore);
            j.Close();

            j.Key("players"); j.OpenList();
            foreach (var p in players)
            {
                j.Open();
                j.Field("id", p.Id); j.Field("name", p.Name); j.Field("role", p.Role); j.Field("class", p.Kit);
                j.Field("weapon", p.Weapon); j.Field("ability", p.Ability); j.Field("ultimate", p.Ultimate);
                j.Field("rerolls", rerolls.ContainsKey(p.Id) ? rerolls[p.Id] : 0);
                j.Close();
            }
            j.CloseList();

            j.Key("waves"); j.OpenList();
            foreach (var w in waves)
            {
                j.Open();
                j.Field("wave", w.Wave);
                j.Field("boss", w.Boss);
                j.Field("path", w.Path);
                j.Field("start", w.Start);
                j.Field("duration", w.End >= 0f ? w.End - w.Start : -1f);
                j.Field("elites", w.Elites);
                j.Key("spawned"); j.Map(w.Spawned);
                j.Key("killed"); j.Map(w.Killed);
                j.Key("players"); j.Open();
                foreach (var kv in w.Players)
                {
                    var pw = kv.Value;
                    j.Key(kv.Key.ToString()); j.Open();
                    j.Field("damageDealt", pw.DmgDealt); j.Field("damageToBosses", pw.DmgToBosses); j.Field("kills", pw.Kills);
                    j.Field("damageTaken", pw.DmgTaken); j.Key("damageTakenFrom"); j.Map(pw.TakenBy);
                    j.Field("healingDone", pw.Healing); j.Field("orbs", pw.Orbs); j.Field("orbHealing", pw.OrbHealing);
                    j.Field("downs", pw.Downs); j.Field("revives", pw.Revives); j.Field("lowHpSeconds", pw.LowHpTime);
                    j.Field("hpStart", pw.HpStart); j.Field("hpEnd", pw.HpEnd); j.Field("maxHp", pw.MaxHp); j.Field("levelEnd", pw.LevelEnd);
                    j.Key("abilitiesUsed"); j.Map(pw.Abilities);
                    j.Field("parryBlocked", pw.ParryBlocked); j.Field("perfectParries", pw.ParryPerfect); j.Field("parryBreaks", pw.ParryBreaks);
                    j.Close();
                }
                j.Close();
                j.Close();
            }
            j.CloseList();

            j.Key("bosses"); j.OpenList();
            foreach (var b in bossOrder)
            {
                j.Open();
                j.Field("name", b.Name); j.Field("wave", b.Wave); j.Field("maxHp", b.MaxHp);
                j.Field("spawnedAt", b.Spawned); j.Field("killed", b.Died >= 0f);
                j.Field("timeToKill", b.Died >= 0f ? b.Died - b.Spawned : -1f);
                j.Field("playerDamageTaken", b.PlayerDmgTaken); j.Field("downs", b.Downs);
                j.Key("damageBy"); j.Open();
                foreach (var kv in b.DmgBy)
                    j.Field(kv.Key.ToString(), kv.Value);
                j.Close();
                j.Close();
            }
            j.CloseList();

            j.Key("enemyTypes"); j.Open();
            foreach (var kv in enemyTypes)
            {
                var a = kv.Value;
                j.Key(kv.Key); j.Open();
                j.Field("spawned", a.Spawned); j.Field("killed", a.Killed); j.Field("elites", a.Elites);
                j.Field("avgLifetime", a.Killed > 0 ? a.Lifetime / a.Killed : 0f);
                j.Field("damageToPlayers", a.DmgToPlayers);
                j.Field("damagePerSpawn", a.Spawned > 0 ? a.DmgToPlayers / a.Spawned : 0f);
                j.Close();
            }
            j.Close();

            j.Key("downs"); j.OpenList();
            foreach (var d in downs)
            {
                j.Open();
                j.Field("player", d.Player); j.Field("wave", d.Wave); j.Field("time", d.Time); j.Field("source", d.Source);
                j.Close();
            }
            j.CloseList();

            j.Key("picks"); j.OpenList();
            foreach (var p in picks)
            {
                j.Open();
                j.Field("player", p.Player); j.Field("wave", p.Wave); j.Field("level", p.Level); j.Field("kind", p.Kind);
                j.Key("offered"); j.OpenList();
                foreach (var o in p.Offered)
                    j.Value(o);
                j.CloseList();
                j.Field("picked", p.Picked); j.Field("rerollsLeft", p.RerollsLeft);
                j.Close();
            }
            j.CloseList();

            j.Key("samples"); j.OpenList();
            foreach (var s in samples)
            {
                j.Open();
                j.Field("time", s.Time); j.Field("wave", s.Wave); j.Field("enemies", s.Enemies); j.Field("bullets", s.Bullets);
                j.Key("hp"); j.Open();
                foreach (var kv in s.Hp)
                    j.Field(kv.Key.ToString(), kv.Value);
                j.Close();
                j.Close();
            }
            j.CloseList();
            j.Close();
            return j.ToString();
        }

        /// A tiny JSON writer (the project has no dependencies).
        class Json
        {
            readonly StringBuilder sb = new StringBuilder();
            bool first = true;
            int depth;

            void Sep()
            {
                if (!first)
                    sb.Append(',');
                sb.Append('\n').Append(' ', depth * 2);
                first = false;
            }

            public void Open() { if (!keyed) Sep(); keyed = false; sb.Append('{'); depth++; first = true; }
            public void Close() { depth--; sb.Append('\n').Append(' ', depth * 2).Append('}'); first = false; }
            public void OpenList() { if (!keyed) Sep(); keyed = false; sb.Append('['); depth++; first = true; }
            public void CloseList() { depth--; sb.Append('\n').Append(' ', depth * 2).Append(']'); first = false; }

            bool keyed;
            public void Key(string k) { Sep(); Str(k); sb.Append(": "); keyed = true; }

            public void Value(string v) { Sep(); Str(v); }
            public void Field(string k, string v) { Key(k); keyed = false; if (v == null) sb.Append("null"); else Str(v); }
            public void Field(string k, int v) { Key(k); keyed = false; sb.Append(v.ToString(CultureInfo.InvariantCulture)); }
            public void Field(string k, float v) { Key(k); keyed = false; sb.Append(Num(v)); }
            public void Field(string k, bool v) { Key(k); keyed = false; sb.Append(v ? "true" : "false"); }

            public void Map(Dictionary<string, int> d)
            {
                Open();
                foreach (var kv in d)
                    Field(kv.Key, kv.Value);
                Close();
            }

            public void Map(Dictionary<string, float> d)
            {
                Open();
                foreach (var kv in d)
                    Field(kv.Key, kv.Value);
                Close();
            }

            static string Num(float v) =>
                float.IsNaN(v) || float.IsInfinity(v) ? "0" : Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

            void Str(string s)
            {
                sb.Append('"');
                foreach (char c in s)
                {
                    if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                    else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                }
                sb.Append('"');
            }

            public override string ToString() => sb.ToString() + "\n";
        }
    }
}
