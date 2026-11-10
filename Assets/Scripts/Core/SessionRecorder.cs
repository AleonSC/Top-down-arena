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
    public partial class SessionRecorder
    {
        public const int FormatVersion = 4;  // 4: balance test runs ("test", damage by source, shots, enemy health); 3: per-player "near" in the samples (Docs/Rooms.md); 2: mode, endless, paths, dev tools, healing received, final loadouts
        public readonly string Path;
        public bool Ended { get; private set; }

        class PlayerInfo
        {
            public int Id, Level;
            public string Name, Role, Kit, Weapon, Ability, Ultimate;
            public readonly Dictionary<string, int> Mastery = new Dictionary<string, int>();  // the abilities they ended with, and their levels
        }

        class PlayerWave
        {
            public float DmgDealt, DmgToBosses, DmgTaken, Healing, OrbHealing, LowHpTime, HpStart, HpEnd, MaxHp;
            public float ParryBlocked, HealingReceived;
            public readonly Dictionary<string, float> HealedBy = new Dictionary<string, float>();
            public int Downs, Revives, Kills, ParryPerfect, ParryBreaks, LevelEnd, Orbs;
            public readonly Dictionary<string, float> TakenBy = new Dictionary<string, float>();
            public readonly Dictionary<string, int> Abilities = new Dictionary<string, int>();
            public readonly Dictionary<string, float> Src = new Dictionary<string, float>();  // damage to enemies by source (format 4)
            public readonly Dictionary<string, int> SrcHits = new Dictionary<string, int>();
            public int ShotsFired, ShotsHit, InstantHits;
        }

        class WaveRec
        {
            public int Wave;
            public string Boss;
            public string Path;  // the divergent path this wave was played on (null on the plain arena)
            public string Approach;  // the boss this wave leads up to, when its faction is in it (Approaches.cs)
            public float ApproachShare;  // the themed share of the wave (0 to 1)
            public float Start, End = -1f;
            public int Elites;
            public float HazardDamage;  // damage the floor did to enemies (Burning Grounds' fire)
            public readonly Dictionary<string, int> Spawned = new Dictionary<string, int>(), Killed = new Dictionary<string, int>();
            public readonly Dictionary<int, PlayerWave> Players = new Dictionary<int, PlayerWave>();
        }

        class BossRec
        {
            public int Id, Wave;
            public string Name;
            public float MaxHp, Spawned, Died = -1f, Merged = -1f, PlayerDmgTaken, PowerScale = 1f;  // Merged: left the fight without dying (the Twin Reapers fusing / splitting)
            public int Downs;
            public readonly Dictionary<int, float> DmgBy = new Dictionary<int, float>();
        }

        class EnemyAgg { public int Spawned, Killed, Elites; public float Lifetime, DmgToPlayers, HpSum; }

        class PickRec { public int Player, Wave, Level, RerollsLeft; public string Kind, Picked; public string[] Offered; }
        class DownRec { public int Player, Wave; public float Time; public string Source; }
        class PathRec { public int AfterWave; public string[] Offered; public string Chosen, Prize; }
        class DevRec { public float Time; public int Wave; public string What; }
        class Sample
        {
            public float Time; public int Wave, Enemies, Bullets;
            public Dictionary<int, float> Hp = new Dictionary<int, float>();
            public readonly Dictionary<int, Near> Near = new Dictionary<int, Near>();  // per player, not downed (format 3)
        }

        /// How close the fight is to one player (Docs/Rooms.md, phase 0): the nearest enemy, enemies within 300 px, enemies
        /// and enemy shots inside the view at the scroll wheel's full 2x zoom (800 x 450, held inside the arena like the camera).
        class Near { public float Nearest = -1f; public int Within300, InView2x, ShotsInView2x; }

        public const float NearRange = 300f, View2xHalfW = Game.Width / 4f, View2xHalfH = Game.Height / 4f;

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
        readonly List<PathRec> paths = new List<PathRec>();
        readonly List<DevRec> devUses = new List<DevRec>();
        string mode = "run";
        bool endless, fighting, lastBot;
        readonly DateTime started = DateTime.Now;
        int playerCount, startWave;
        string modifiers = "", difficulty = "Normal", arena = "Classic";
        float sampleT, saveT = AutosaveEvery, now;
        const float AutosaveEvery = 10f;  // a run in progress is written this often, so a crash or a closed window keeps nearly all of it
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
            mode = g.Practice != null ? "practice" : g.Hunt != null ? "hunt: " + g.Hunt.Name : "run";
            lastBot = g.Bot;
            if (g.Bot)
                Dev(g, "bot plays for the host: on");
            modifiers = string.Join(", ", Modifiers.Names(g.Mods).ToArray());
            arena = g.ArenaKind == ArenaLayout.Expanding ? "Expanding" : g.RoomsArena ? "Rooms" : "Classic";  // the Arena setting (Rooms.cs)
            difficulty = g.Diff.Level == DifficultyLevel.Custom ? "Custom (" + string.Join(", ", g.Diff.Changes().ToArray()) + ")" : g.Diff.Name;
            playerCount = g.Players.Count;
            foreach (var p in g.Players.Values)
                players.Add(new PlayerInfo
                {
                    Id = p.Id, Name = p.Name, Role = p.Def.Name, Kit = Defs.KitName(p.Kit), Weapon = p.Weapon.Name,
                    Ability = Upgrades.Ability(p.RoleAbility).Name, Ultimate = Upgrades.Ability(Upgrades.Ultimate(p.Role, p.UltKit, p.UltPick)).Name,
                });
            Save();
        }

        public void Tick(Game g, float dt)
        {
            now = g.RunTime;
            if (g.Bot != lastBot)
            {
                lastBot = g.Bot;
                Dev(g, "bot plays for the host: " + (g.Bot ? "on" : "off"));
            }
            saveT -= dt;
            if (saveT <= 0f && !Ended)
            {
                saveT = AutosaveEvery;
                endWave = g.Wave;
                endTime = now;
                endScore = g.Score;
                UpdateLoadouts(g);
                Save();
            }
            fighting = g.State == MatchState.Fighting;
            if (!fighting)
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
            sampleT = SampleEvery;
            var s = new Sample { Time = now, Wave = g.Wave, Enemies = g.Enemies.Count, Bullets = g.Bullets.Count };
            foreach (var p in g.Players.Values)
            {
                s.Hp[p.Id] = p.Downed ? 0f : p.Hp / Math.Max(1f, p.MaxHp);
                if (!p.Downed)
                    s.Near[p.Id] = MeasureNear(g, p.Pos);
            }
            samples.Add(s);
        }

        /// The Near numbers for a player standing at pos. Distances are straight lines (no wrap), like the zoomed camera.
        static Near MeasureNear(Game g, V2 pos)
        {
            var n = new Near();
            float cx = Math.Max(View2xHalfW, Math.Min(Game.Width - View2xHalfW, pos.X));
            float cy = Math.Max(View2xHalfH, Math.Min(Game.Height - View2xHalfH, pos.Y));
            if (g.RoomsOn)  // the Rooms arena's camera frames the room or hallway you're in (Rooms.cs)
            {
                V2 c = g.CurrentWorld.ViewCentre(pos, View2xHalfW, View2xHalfH, g.VaultOn);
                cx = c.X;
                cy = c.Y;
            }
            float best = float.MaxValue;
            foreach (var e in g.Enemies.Values)
            {
                float d = (float)Math.Sqrt(e.Pos.DistanceSq(pos));
                if (d < best) best = d;
                if (d <= NearRange) n.Within300++;
                if (Math.Abs(e.Pos.X - cx) <= View2xHalfW && Math.Abs(e.Pos.Y - cy) <= View2xHalfH) n.InView2x++;
            }
            if (best < float.MaxValue) n.Nearest = best;
            foreach (var b in g.Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && Math.Abs(b.Pos.X - cx) <= View2xHalfW && Math.Abs(b.Pos.Y - cy) <= View2xHalfH)
                    n.ShotsInView2x++;
            return n;
        }

        public void WaveStart(Game g, int wave, string boss)
        {
            var w = new WaveRec { Wave = wave, Boss = boss, Start = g.RunTime, Path = g.OnPath && !g.IsBossWave(wave) ? Paths.Get(g.CurrentPath).Name : null };
            w.ApproachShare = g.ApproachShare(wave);
            w.Approach = w.ApproachShare > 0f ? Defs.Get(g.ApproachBossAt(wave)).Name : null;
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
            UpdateLoadouts(g);
            Save();
        }

        /// Each player's level and abilities (with mastery) as they stand, so a crash still keeps the latest.
        void UpdateLoadouts(Game g)
        {
            foreach (var info in players)
            {
                Player p;
                if (!g.Players.TryGetValue(info.Id, out p))
                    continue;
                info.Level = p.Level;
                info.Mastery.Clear();
                foreach (var slot in p.Slots)
                    if (slot.Id != AbilityId.None)
                        info.Mastery[Upgrades.Ability(slot.Id).Name] = Game.MasteryLevel(p, slot.Id);
            }
        }

        /// The host kept going past wave 50: the run is open again, so its real end gets recorded.
        public void ContinueEndless()
        {
            Ended = false;
            endless = true;
            outcome = "in progress";
        }

        public void End(Game g, string how)
        {
            if (Ended)
                return;
            WaveEnd(g);
            Ended = true;
            outcome = endless ? how + " (endless)" : how;
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
            a.HpSum += e.MaxHp;
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
                var b = new BossRec { Id = e.Id, Name = e.Def.Name, Wave = w != null ? w.Wave : 0, MaxHp = e.MaxHp, Spawned = now, PowerScale = e.PowerScale };
                bosses[e.Id] = b;
                bossOrder.Add(b);
            }
        }

        /// A boss that leaves the fight without dying (the twins fusing into the Dual Reaper, which splits back): its
        /// record closes, so later damage and downs aren't put on it.
        public void BossMerged(Enemy e)
        {
            BossRec b;
            if (bosses.TryGetValue(e.Id, out b) && b.Died < 0f)
                b.Merged = now;
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

        public void DamageDealt(int attacker, Enemy e, float amount, string src = null)
        {
            if (amount <= 0f || attacker == 0)
                return;
            var pw = Pw(attacker);
            if (pw != null)
            {
                pw.DmgDealt += amount;
                if (e.Def.IsBoss)
                    pw.DmgToBosses += amount;
                string key = SourceName(attacker, src);
                Add(pw.Src, key, amount);
                Add(pw.SrcHits, key, 1);
            }
            BossRec b;
            if (bosses.TryGetValue(e.Id, out b))
                Add(b.DmgBy, attacker, amount);
        }

        /// "weapon" is the player's gun, "status" burning, bleeding and poison, "shot" a bullet an ability or summon fired, "ability:Name" a cast's own hit; nothing known is the rest (zones, minions, delayed hits).
        string SourceName(int attacker, string src)
        {
            if (src == "weapon")
                foreach (var pi in players)
                    if (pi.Id == attacker)
                        return "Weapon: " + pi.Weapon;
            if (src == "status") return "Burn, bleed and poison";
            if (src == "shot") return "Ability and summon shots";
            if (src != null && src.StartsWith("ability:", StringComparison.Ordinal)) return "Ability: " + src.Substring(8);
            return "Other (zones, minions, delayed hits)";
        }

        public void ShotFired(Player p)
        {
            var pw = Pw(p.Id);
            if (pw != null)
                pw.ShotsFired++;
        }

        public void ShotHit(Player p)
        {
            var pw = Pw(p.Id);
            if (pw != null)
                pw.ShotsHit++;
        }

        /// How many enemies a cast's own hit reached the moment it was cast (lasting abilities do their work later).
        public void CastHits(Player p, int hits)
        {
            var pw = Pw(p.Id);
            if (pw != null)
                pw.InstantHits += hits;
        }

        /// Marks this recording as a balance test run (Core/BalanceTest.cs): one-second samples on request.
        public void MarkTest(int firstWave, int waveCount, bool bot, bool fineSamples)
        {
            IsTest = true;
            testFirst = firstWave;
            testWaves = waveCount;
            testBot = bot;
            SampleEvery = fineSamples ? 1f : 5f;
        }

        public bool IsTest { get; private set; }
        int testFirst, testWaves;
        bool testBot;
        public float SampleEvery = 5f;

        public void HazardDamage(float amount)
        {
            if (Current != null && amount > 0f)
                Current.HazardDamage += amount;
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
                if (b.Died < 0f && b.Merged < 0f && b.Spawned <= now)
                    b.PlayerDmgTaken += amount;
        }

        public void Downed(Game g, Player p, string source)
        {
            downs.Add(new DownRec { Player = p.Id, Wave = g.Wave, Time = g.RunTime, Source = source });
            var pw = Pw(p.Id);
            if (pw != null)
                pw.Downs++;
            foreach (var b in bossOrder)
                if (b.Died < 0f && b.Merged < 0f)
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
            HealReceived(p, healed, "orbs");
        }

        /// Healing a player got during a wave, by where it came from (allies, regeneration, lifesteal, a path...).
        public void HealReceived(Player p, float amount, string source)
        {
            if (!fighting || amount <= 0f)
                return;
            var pw = Pw(p.Id);
            if (pw == null)
                return;
            pw.HealingReceived += amount;
            Add(pw.HealedBy, source, amount);
        }

        public void PathOffered(Game g, PathId[] offer) =>
            paths.Add(new PathRec { AfterWave = g.Wave, Offered = new[] { Game.PathName(offer[0]), Game.PathName(offer[1]) } });

        public void PathChosen(PathId id)
        {
            if (paths.Count > 0 && paths[paths.Count - 1].Chosen == null)
                paths[paths.Count - 1].Chosen = Game.PathName(id);
        }

        public void PathFinished(string prize)
        {
            if (paths.Count > 0)
                paths[paths.Count - 1].Prize = prize;
        }

        /// A dev tool was used (god mode, stat edits, wave jumps, the bot...): the numbers after it aren't plain play.
        public void Dev(Game g, string what)
        {
            // Typing a stat in digit by digit (or holding +) edits the same entry rather than adding one per change.
            int cut = what.IndexOf(" set to ", StringComparison.Ordinal);
            var last = devUses.Count > 0 ? devUses[devUses.Count - 1] : null;
            if (cut > 0 && last != null && g.RunTime - last.Time < 5f && last.What.StartsWith(what.Substring(0, cut + 8), StringComparison.Ordinal))
            {
                last.What = what;
                last.Time = g.RunTime;
                return;
            }
            devUses.Add(new DevRec { Time = g.RunTime, Wave = g.Wave, What = what });
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
            j.Field("mode", mode);
            j.Field("endless", endless);
            j.Field("modifiers", modifiers);
            j.Field("difficulty", difficulty);
            j.Field("arena", arena);
            if (IsTest)
            {
                j.Key("test"); j.Open();
                j.Field("kind", "balance-test"); j.Field("firstWave", testFirst); j.Field("waves", testWaves); j.Field("bot", testBot);
                j.Field("sampleEverySeconds", SampleEvery);
                j.Close();
            }
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
                j.Field("levelEnd", p.Level);
                j.Key("abilityMastery"); j.Map(p.Mastery);
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
                j.Field("approach", w.Approach);
                j.Field("approachShare", w.ApproachShare);
                j.Field("start", w.Start);
                j.Field("duration", w.End >= 0f ? w.End - w.Start : -1f);
                j.Field("elites", w.Elites);
                j.Field("hazardDamageToEnemies", w.HazardDamage);
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
                    j.Field("healingReceived", pw.HealingReceived); j.Key("healingReceivedFrom"); j.Map(pw.HealedBy);
                    j.Field("downs", pw.Downs); j.Field("revives", pw.Revives); j.Field("lowHpSeconds", pw.LowHpTime);
                    j.Field("hpStart", pw.HpStart); j.Field("hpEnd", pw.HpEnd); j.Field("maxHp", pw.MaxHp); j.Field("levelEnd", pw.LevelEnd);
                    j.Key("abilitiesUsed"); j.Map(pw.Abilities);
                    j.Key("damageBySource"); j.Map(pw.Src); j.Key("hitsBySource"); j.Map(pw.SrcHits);
                    j.Field("shotsFired", pw.ShotsFired); j.Field("shotsHit", pw.ShotsHit); j.Field("castInstantHits", pw.InstantHits);
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
                j.Field("name", b.Name); j.Field("wave", b.Wave); j.Field("maxHp", b.MaxHp); j.Field("powerScale", b.PowerScale);
                j.Field("spawnedAt", b.Spawned); j.Field("killed", b.Died >= 0f); j.Field("merged", b.Merged >= 0f);
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
                j.Field("avgMaxHp", a.Spawned > 0 ? a.HpSum / a.Spawned : 0f);
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

            j.Key("paths"); j.OpenList();
            foreach (var pr in paths)
            {
                j.Open();
                j.Field("afterWave", pr.AfterWave);
                j.Key("offered"); j.OpenList();
                foreach (var o in pr.Offered)
                    j.Value(o);
                j.CloseList();
                j.Field("chosen", pr.Chosen); j.Field("prize", pr.Prize);
                j.Close();
            }
            j.CloseList();

            j.Key("devTools"); j.OpenList();
            foreach (var d in devUses)
            {
                j.Open();
                j.Field("time", d.Time); j.Field("wave", d.Wave); j.Field("what", d.What);
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
                j.Key("near"); j.Open();
                foreach (var kv in s.Near)
                {
                    j.Key(kv.Key.ToString()); j.Open();
                    j.Field("nearest", kv.Value.Nearest); j.Field("within300", kv.Value.Within300);
                    j.Field("inView2x", kv.Value.InView2x); j.Field("shotsInView2x", kv.Value.ShotsInView2x);
                    j.Close();
                }
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
