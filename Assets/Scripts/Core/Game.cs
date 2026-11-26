using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// The match. The host simulates enemies, bullets, damage, XP and waves; every
    /// peer moves its own player locally and renders the shared state.
    ///
    /// Network traffic:
    ///   client -> host  Input (every tick: position/aim/firing), Ability, Pick
    ///   host -> all     Spawn / Remove (bullets fly deterministically, so only
    ///                   spawns and hits are sent), Snapshot (20 Hz: players,
    ///                   enemies, turrets and wave state), Upgrades (a player's
    ///                   picks, after each pick), Fx, ClearEnemyBullets
    public partial class Game
    {
        public const float Width = 1600f, Height = 900f;
        public static readonly V2 ArenaMid = new V2(Width / 2f, Height / 2f);  // the arena's centre

        const int SnapshotEvery = 3;  // ticks
        float shoveT;
        V2 shoveDir;

        readonly Session session;
        readonly Random rng = NewRng();

        // Tests (Tools/SimTest) can make every game's random rolls repeatable: SeedForTests gives each game
        // made after it a seed drawn from one sequence. The real game never calls it, so it stays random.
        static Random testSeeds;
        static int sideRoot, sideCount;

        public static void SeedForTests(int seed)
        {
            testSeeds = new Random(seed);
            sideRoot = seed;
            sideCount = 0;
        }

        static Random NewRng() => testSeeds != null ? new Random(testSeeds.Next()) : new Random();

        /// A Random for rolls kept off a game's rng (asking that would shift every other random choice of the run):
        /// the Expanding map's seed, the Warden's orbs, the party bots' picks. Fresh in the real game; under
        /// SeedForTests the n-th one made is the same every run, from its own sequence so no game's seed moves.
        public static Random NewSideRandom()
        {
            if (testSeeds == null)
                return new Random();
            uint h = (uint)sideRoot * 2654435761u ^ (uint)(++sideCount) * 40503u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return new Random((int)(h & 0x7fffffff));
        }

        readonly Random sideRng = NewSideRandom();  // this game's side rolls (NewSideRandom)

        public bool Running;
        public MatchState State = MatchState.Idle;
        public int Wave, Remaining, Score;
        public float StateTimer;

        public readonly SortedDictionary<int, Player> Players = new SortedDictionary<int, Player>();
        public readonly Dictionary<int, Enemy> Enemies = new Dictionary<int, Enemy>();
        public readonly Dictionary<int, Bullet> Bullets = new Dictionary<int, Bullet>();
        public readonly Dictionary<int, Minion> Minions = new Dictionary<int, Minion>();
        public readonly Dictionary<int, Orb> Orbs = new Dictionary<int, Orb>();
        public readonly Dictionary<int, Zone> Zones = new Dictionary<int, Zone>();
        public readonly List<Effect> Effects = new List<Effect>();
        public readonly List<SoundCue> Sounds = new List<SoundCue>();  // drained by the front end each frame

        public float DashT, AnimT;
        float dashSpeed = DashSpeed;
        public V2 DashDir = new V2(1f, 0f);
        readonly bool[] abilityWasDown = new bool[Upgrades.SlotCount];
        bool awaitingPick;  // client sent a pick; ignore offers until the host confirms it
        int rerollsAtRequest = -1;  // client sent a reroll; block picks until new cards arrive

        public bool RerollPending => rerollsAtRequest >= 0;

        public bool Bot;  // drive the local player automatically (testing)
        V2 iceVel;        // your walking velocity: eased (WalkStartTime/WalkStopTime), and sliding on the Frozen Lake path (Paths.cs)
        public bool HoldParry;  // keep the parry up (testing)

        /// Round trip to the host in milliseconds (0 when you're the host), measured four times a second (Game.PingEvery).
        public float PingMs;
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float pingT;

        /// The direction of your most recent shot, as the host fired it (the aim test checks it).
        public int LastShotId;
        public float LastShotAngle, LastShotAim;
        public V2 LastShotFrom;
        long lastShotTicks;

        /// Keep the shot from the newest volley that's closest to where you're aiming (spreads fire several).
        void NoteShot(Bullet b)
        {
            var me = Me;
            if (me == null)
                return;
            float off = Math.Abs(MathX.WrapPi(b.Vel.Angle - me.Aim));
            float prevOff = Math.Abs(MathX.WrapPi(LastShotAngle - me.Aim));
            bool newVolley = clock.ElapsedTicks - lastShotTicks > System.Diagnostics.Stopwatch.Frequency / 25;
            if (newVolley || off < prevOff)
            {
                LastShotId = b.Id;
                LastShotAngle = b.Vel.Angle;
                LastShotAim = me.Aim;  // what you were aiming at when it showed up
                LastShotFrom = b.Pos;
            }
            lastShotTicks = clock.ElapsedTicks;
        }

        public Action<Player, float, string> DamageEvent;  // host: every hit a player takes (Boss Lab)
        public bool BotRerolls = true;
        int botRerolledAt = -1;
        public Action<string> Log;
        float botT;

        // Previous values, to raise sounds when things change.
        MatchState lastState;
        readonly Dictionary<int, bool> lastDowned = new Dictionary<int, bool>();
        readonly Dictionary<int, int> lastLevel = new Dictionary<int, int>();
        float lastMyHp;

        // Host-only simulation state.
        int nextBulletId = 1, nextEnemyId = 1, nextMinionId = 1, nextOrbId = 1, nextZoneId = 1, tick;
        readonly List<EnemyType> spawnQueue = new List<EnemyType>();
        float spawnTimer;
        // Bullets spawned this tick, recorded as they were at spawn time (the host
        // moves them once before the batch is sent).
        readonly List<Bullet> pendingSpawns = new List<Bullet>();
        readonly List<int> pendingRemoved = new List<int>();

        public Game(Session session)
        {
            this.session = session;
        }

        public int MyId => session.MyId;
        bool IsHost => session.IsHost;

        public Player Me
        {
            get
            {
                Player p;
                Players.TryGetValue(MyId, out p);
                return p;
            }
        }


        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float RandAngle() => Rand(0f, MathX.Tau);           // a random heading
        V2 RandDir() => V2.FromAngle(Rand(0f, MathX.Tau));  // a random unit direction

        // --- Lifecycle ---------------------------------------------------------

        public void HostStart()
        {
            var w = Proto.Begin(Msg.Start);
            w.Write(session.StartWave);
            // Quick Play (QuickPlay.cs): its boss order, drawn here so every machine plays the same bosses.
            bool quick = session.QuickMode && session.LobbyHunt == null && ResumeFrom == null && !HangoutStart && DrillStart < 0;
            w.Write(quick);
            if (quick)
            {
                var order = QuickPlay.OrderOverride ?? QuickPlay.MakeOrder(sideRng, Profile == null || Profile.QuickPlays == 0);
                w.Write((byte)order.Length);
                foreach (var b in order)
                    w.Write((byte)b);
            }
            w.Write((byte)session.Lobby.Count);
            foreach (var e in session.Lobby.Values)
                WriteStartPlayer(w, e);
            w.Write(ResumeFrom != null);  // continuing a saved run (RunSaves.cs)
            w.Write(ResumeFrom != null ? ResumeFrom.LongRun : LongRunDefault && !quick && !HangoutStart && DrillStart < 0);  // a 100-wave Campaign (Hunts.cs); a saved run keeps its length
            WriteSurvivalStart(w, quick || HangoutStart || DrillStart >= 0);  // Survival mode: the flag and its seed (SurvivalGame.cs)
            w.Write(HangoutStart);  // the Hangout (HangoutGame.cs)
            w.Write((sbyte)DrillStart);  // a Training drill (DrillGame.cs)
            var msg = Proto.End(w);
            session.Broadcast(msg);
            var r = new BinaryReader(new MemoryStream(msg));
            r.ReadByte();
            ReadStart(r);
            if (ResumeFrom != null)
                ApplyResume(ResumeFrom);
            ResumeFrom = null;
            ResumeSeats.Clear();
        }

        /// One player's loadout in `Msg.Start` (and `Msg.PracticePlayer`, HangoutGame.cs): id, name, class, weapon, abilities, ultimate,
        /// perks and Vowguard chains.
        void WriteStartPlayer(BinaryWriter w, LobbyEntry e)
        {
            w.Write(e.Id);
            w.Write(e.Name);
            w.Write((byte)e.Role);
            w.Write((byte)e.Kit);
            w.Write(e.Weapon);
            w.Write(e.Ability);
            w.Write(e.Wild);
            w.Write(e.Ult);
            var perks = PerksFor(e);
            w.Write((byte)perks.Length);
            foreach (var id in perks)
                w.Write((byte)id);
            Session.WriteVgChain(w, e.Chain);  // their Vowguard chains (Vowguard.cs)
        }

        /// Reads WriteStartPlayer's record into a fresh player at full health, its stats and perks worked out (not yet placed).
        Player ReadStartPlayer(BinaryReader r)
        {
            int id = r.ReadInt32();
            string name = r.ReadString();
            var role = (Role)r.ReadByte();
            var kit = (Kit)r.ReadByte();
            int weapon = r.ReadByte();
            int ability = r.ReadByte();
            int wild = r.ReadByte();  // a Wildcard ability (Defs.WildSecond)
            int ult = r.ReadByte();   // the Ultimate row's key (LobbyEntry.Ult)
            var perkBytes = new List<byte>();
            for (int pn = r.ReadByte(); pn > 0; pn--)
                perkBytes.Add(r.ReadByte());
            var chain = Session.ReadVgChain(r);
            var def = Defs.Get(role);
            var p = new Player { Id = id, Role = role, Def = def, Name = name, Hp = def.Hp, MaxHp = def.Hp, Aim = -MathX.Pi / 2f, ScatShells = ScatShellMax };
            p.Kit = kit;
            p.VgChain = chain;
            p.Weapon = Defs.Weapon(role, kit, weapon < Defs.WeaponLimit(role, kit) ? weapon : 0);
            p.FirstAbility = Upgrades.FirstAbility(role, kit, ability);
            p.UltPick = (byte)Upgrades.ClampUlt(role, kit, ult);
            if (role == Role.Wildcard)
            {
                p.WildKit = Upgrades.UltKitOf(role, kit, p.UltPick);  // the class its Ultimate pick is from
                p.WildcardAbility = Upgrades.FirstAbility(role, kit, Defs.WildSecond(wild));
                p.CardAbilities[0] = p.WildcardAbility;  // on the 2nd ability key
            }
            p.BaseHp = def.Hp * Defs.KitHpMult(kit);
            p.MaxHp = p.Hp = p.BaseHp;
            Recalc(p);
            GivePerks(p, Perks.Sanitize(perkBytes));
            return p;
        }

        void ReadStart(BinaryReader r)
        {
            Stop();
            ResetPaths();
            ResetHuntMap();  // HuntMapGame.cs
            ResetExpansion();  // ExpansionGame.cs
            ResetRoom();  // Challenges.cs
            int startWave = r.ReadInt32();
            Quick = r.ReadBoolean();
            QuickOrder = null;
            QuickBossesBeaten = 0;
            if (Quick)
            {
                var order = new EnemyType[r.ReadByte()];
                for (int i = 0; i < order.Length; i++)
                    order[i] = (EnemyType)r.ReadByte();
                QuickOrder = order.Length > 0 ? order : new[] { EnemyType.Warden };
            }
            int n = r.ReadByte();
            var entries = new List<Player>();
            for (int i = 0; i < n; i++)
            {
                var p = ReadStartPlayer(r);
                p.Pos = new V2(Width / 2f + (i - (n - 1) / 2f) * 90f, Height / 2f + 140f);
                p.Disp = p.Pos;
                if (Has(RunMod.Lucky))
                    p.Rerolls += Modifiers.LuckyRerolls;
                entries.Add(p);
                if (Has(RunMod.HeadStart) && ModeRules.Extras && !(IsHost && ResumeFrom != null))
                {
                    // Head Start: the picks of the first levels, and a legendary from the first boss's pool.
                    p.Level = Modifiers.HeadStartLevel;
                    if (IsHost)
                    {
                        p.PendingPicks += Modifiers.HeadStartLevel - 1;
                        var first = BossAt(Bosses.FirstBossWave);
                        p.LegendaryQueue.Add(IsReaper(first) ? EnemyType.ReaperOfDusk : first);
                        NextOffer(p);
                    }
                }
                Players[p.Id] = p;
                lastDowned[p.Id] = false;
                lastLevel[p.Id] = p.Level;
            }
            bool resumed = r.ReadBoolean();
            LongRun = r.ReadBoolean();  // a 100-wave Campaign (Hunts.cs)
            ReadSurvivalStart(r);  // SurvivalGame.cs
            HangoutRead(r.ReadBoolean());  // HangoutGame.cs
            DrillRead(r.ReadSByte());  // DrillGame.cs
            if (resumed && !IsHost)
                foreach (var p in entries)
                    p.Level = 1;  // a continued run: the host sends everyone's levels and picks next (Head Start doesn't apply again)
            if (startWave < 0)
            {
                Wave = -startWave;  // a negative start is the boss after that wave (the Boss Lab, Hunts.cs)
                BossSlot = false;
                startWave = Wave;
            }
            else
            {
                Wave = Math.Max(0, startWave - 1);
                BossSlot = IsBossWave(Wave);  // starting after a boss: its wave is done (a saved run says so itself: ApplyResume)
            }
            if (HangoutOn || DrillOn)
            {
                Wave = 1;  // (no wave counts in the Hangout or a drill)
                BossSlot = false;
            }
            retrying = false;  // (Stop above has let the retried run go without recording it: Checkpoints.cs)
            LivesUsed = 0;  // (a continued run gets its own back: ApplyResume)
            RetryReady = false;
            Leg = Wave > 0 ? 1 : 0;  // starting after a wave: it's done, both legs (a saved run says where it was: ApplyResume)
            State = MatchState.Intermission;
            lastState = State;
            StateTimer = StartDelay;
            Running = true;
            lastMyHp = Me != null ? Me.Hp : 0f;
            if (!HangoutOn)
                session.Screen = AppScreen.Game;  // (Practice plays inside the lobby's window: the screen stays the lobby, HangoutGame.cs)
            BeginRunRecord(resumed);  // RunSaves.cs
            Tutorial.Begin(this);  // a profile's first Quick Play: the guided prompts (Tutorial.cs)
            DoLog("game started with " + Players.Count + " player(s)");
        }

        /// Ends the run; `how` is what its recording says happened ("left", or "quit" when the game is closed).
        public void Stop(string how = "left")
        {
            if (Running)
                LogQuitRun();  // (not while a retry is on offer or starting: the run isn't over)
            SettleRetry(how);  // a retry left untaken ends the run for good (Checkpoints.cs)  // RunSaves.cs (the run you walk away from still shows in Recent runs)
            if (Running)
                SaveRun();  // between waves: keep the run to continue later (RunSaves.cs)
            Running = false;
            HangoutOn = false;  // HangoutGame.cs
            Drill = -1;  // DrillGame.cs
            DrillRunners.Clear();
            State = MatchState.Idle;
            HuntedId = 0;
            Wave = 0;
            Leg = 0;
            Score = 0;
            WaveReport = null;  // (WaveReady.cs)
            Remaining = 0;
            Players.Clear();
            Enemies.Clear();
            Bullets.Clear();
            Minions.Clear();
            Orbs.Clear();
            Coins.Clear();
            ForgetSentCoins();  // (Coins.cs: the next snapshot sends the full list)
            Gadgets.Clear();
            DevSpawners.Clear();
            Zones.Clear();
            Effects.Clear();
            Sounds.Clear();
            ResetMeleeSounds();  // MeleeSounds.cs
            spawnQueue.Clear();
            squadQueue.Clear();
            DevFreeze = false;  // (DevTools.cs)
            ClearRisers();
            pendingSpawns.Clear();
            pendingRemoved.Clear();
            lastDowned.Clear();
            lastLevel.Clear();
            nextBulletId = 1;
            nextEnemyId = 1;
            nextMinionId = 1;
            nextOrbId = 1;
            nextCoinId = 1;
            nextGadgetId = 1;
            nextZoneId = 1;
            ResetBossState();
            ResetVariety(true);
            DashT = 0f;
            SprintT = 0f;
            Paused = false;
            PausedBy = 0;
            Endless = false;
            RunTime = 0f;
            RunStatsReady = false;
            Hazards.Clear();
            Cages.Clear();
            ResetProps();  // Props.cs
            ResetLayout();  // LayoutRules.cs
            ResetRunLists();  // RunReset.cs
            awaitingPick = false;
            rerollsAtRequest = -1;
            tick = 0;
        }

        public void OnPeerLeft(int id)
        {
            Players.Remove(id);
            if (MapVotes.Remove(id) | MapLocked.Remove(id))
                BroadcastHuntMap();  // a hunt-map vote from someone who left no longer counts (HuntMapGame.cs)
        }

        // --- Upgrades ----------------------------------------------------------

        /// Recompute a player's stats and ability slots from their picks. Runs on
        /// every peer, so all derived values agree without being sent.
        void Recalc(Player p)
        {
            p.Stats = Upgrades.Compute(p.Taken);
            if (p.Role == Role.Wildcard)
            {
                p.Stats.DmgMult *= WildcardDmgMult;  // the price of mixing classes
                p.Stats.CdMult *= WildcardCdMult;
            }
            Relics.Apply(p.Relic, p.Stats);  // Relics.cs
            ApplyStatOverrides(p);
            p.VectorCap = VectorCapBase + p.Stats.VectorCap;  // the Expanding arena's vector cap (ExpansionGame.cs)
            float oldMax = p.MaxHp;
            p.MaxHp = p.BaseHp + p.Stats.BonusHp;
            if (p.MaxHp > oldMax && !p.Downed)
                p.Hp += p.MaxHp - oldMax;

            var ids = new AbilityId[Upgrades.SlotCount];
            ids[0] = p.FirstAbility;
            for (int i = 1; i < ids.Length; i++)
                ids[i] = AbilityId.None;
            ids[Upgrades.UltSlot] = Upgrades.Ultimate(p.Role, p.UltKit, p.UltPick);  // the lobby's Ultimate row pick (0: the default one)
            if (p.Recruit)
                ids[0] = ids[Upgrades.UltSlot] = AbilityId.None;  // a Training recruit has no abilities (DrillGame.cs)
            // 2nd–4th abilities go on the keys their owner chose; any others take the first free key.
            var granted = new List<AbilityId>();
            if (p.WildcardAbility != AbilityId.None)
                granted.Add(p.WildcardAbility);  // a Wildcard ability counts as granted from the start
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Grants != AbilityId.None && granted.Count < Upgrades.CardAbilitySlots && !granted.Contains(Upgrades.All[t].Grants))
                    granted.Add(Upgrades.All[t].Grants);
            for (int k = 0; k < Upgrades.CardAbilitySlots; k++)
                if (granted.Contains(p.CardAbilities[k]) && Array.IndexOf(ids, p.CardAbilities[k]) < 0)
                    ids[1 + k] = p.CardAbilities[k];
            foreach (var g in granted)
                if (Array.IndexOf(ids, g) < 0)
                    for (int k = 1; k <= Upgrades.CardAbilitySlots; k++)
                        if (ids[k] == AbilityId.None)
                        {
                            ids[k] = g;
                            break;
                        }
            for (int k = 0; k < Upgrades.CardAbilitySlots; k++)
                p.CardAbilities[k] = ids[1 + k];

            // Charges and cooldowns follow an ability when it moves to another key.
            var moved = new Dictionary<AbilityId, AbilitySlot>();
            for (int i = 1; i <= Upgrades.CardAbilitySlots; i++)
                if (p.Slots[i].Id != AbilityId.None)
                    moved[p.Slots[i].Id] = new AbilitySlot { Id = p.Slots[i].Id, Charges = p.Slots[i].Charges, MaxCharges = p.Slots[i].MaxCharges, Timer = p.Slots[i].Timer };

            for (int i = 0; i < ids.Length; i++)
            {
                var slot = p.Slots[i];
                var def = ids[i] == AbilityId.None ? null : Upgrades.Ability(ids[i]);
                int max = def == null ? 0 : i == Upgrades.UltSlot ? 1 : 1 + p.Stats.UseBonus[i]
                    + def.ExtraCharges + (def.CapstoneCharge && Mastered(p, def.Id) ? 1 : 0);
                AbilitySlot was;
                if (slot.Id != ids[i] && ids[i] != AbilityId.None && moved.TryGetValue(ids[i], out was))
                {
                    slot.Id = ids[i];
                    slot.Charges = was.Charges;
                    slot.MaxCharges = was.MaxCharges;
                    slot.Timer = was.Timer;
                }
                if (slot.Id != ids[i])
                {
                    slot.Id = ids[i];
                    slot.Charges = max;
                    slot.Timer = 0f;
                    if (i == Upgrades.UltSlot && ids[i] != AbilityId.None)  // (a Training recruit has none)
                    {
                        // The ultimate starts half charged.
                        slot.Charges = 0;
                        slot.Timer = Upgrades.Ability(ids[i]).Cooldown * 0.5f;
                    }
                }
                else if (max > slot.MaxCharges)
                    slot.Charges += max - slot.MaxCharges;
                slot.MaxCharges = max;
                slot.Charges = Math.Min(slot.Charges, max);
            }
        }

        public float Cooldown(Player p, int slot)
        {
            var id = p.Slots[slot].Id;
            if (id == AbilityId.None)
                return 1f;
            if (slot == Upgrades.UltSlot)
                return Upgrades.Ability(id).Cooldown * MasteryCooldown(p, id) * (p.Role == Role.Wildcard ? WildcardCdMult : 1f);  // ultimates ignore cooldown upgrades
            float cd = Upgrades.Ability(id).Cooldown * p.Stats.CdMult * MasteryCooldown(p, id) * (slot >= 1 && slot <= Upgrades.CardAbilitySlots ? p.Stats.BonusCdMult : 1f);  // Quartermaster: 2nd, 3rd, 4th
            if (id == AbilityId.DeployableCover && Mastered(p, id))
                cd *= CoverCdMastered;  // its capstone (DeployableCover.cs)
            return cd;
        }

        void TickSlots(Player p, float dt)
        {
            for (int i = 0; i < p.Slots.Length; i++)
            {
                var s = p.Slots[i];
                if (s.Id == AbilityId.None || s.Charges >= s.MaxCharges || StunHeld(p, s) || s.Id == AbilityId.BloodShell && p.ShellOn || s.Id == AbilityId.ArterialSpray && p.SprayOn || s.Id == AbilityId.BloodWall && p.BloodWallOn || s.Id == AbilityId.LastStand && p.StandOn || s.Id == AbilityId.TrojanHorse && HorseOut(p) || s.Id == AbilityId.CaltropRun && p.CaltropT > 0f)
                    continue;  // (the Stun Gun's cooldown waits until its shots are spent)
                if (HangoutOn)
                {
                    s.Charges = s.MaxCharges;  // the Hangout (HangoutGame.cs): no cooldowns, every ability and the ultimate are back at once
                    s.Timer = 0f;
                    continue;
                }
                s.Timer -= (OverclockOn(p) ? dt * OverclockMult : dt) * (i != Upgrades.UltSlot && Supplied(p) ? SupplyCdMult : 1f);  // (an Overclock Chip: Gadgets.cs; a Supply Drop, not the ultimate: SupplyDrop.cs)
                if (s.Timer <= 0f)
                {
                    s.Charges++;
                    s.Timer = s.Charges < s.MaxCharges ? Cooldown(p, i) : 0f;
                }
            }
        }

        /// Spend a charge. lenient: accept presses that arrive slightly early (the
        /// owner tracks its own cooldowns, so the host's copy can lag a little).
        bool Consume(Player p, int slot, bool lenient)
        {
            var s = p.Slots[slot];
            if (s.Id == AbilityId.None)
                return false;
            if (s.Charges <= 0)
            {
                if (!lenient || s.Timer > 0.5f)
                    return false;
                s.Charges = 1;
                s.Timer = 0f;
            }
            s.Charges--;
            if (s.Timer <= 0f)
                s.Timer = Cooldown(p, slot);
            return true;
        }

        void GrantXp(float amount)
        {
            if (Quiet)
                return;  // no levelling up in the loadout preview or the Hangout
            foreach (var p in Players.Values)
                GrantXpTo(p, amount);
        }

        /// Every 10 levels is a milestone (its own pick, from the milestone pool); Second Helping can add a normal pick,
        /// and the Milestone Compass can add a lucky milestone pick.
        public const int MilestoneEvery = 10;

        void GrantXpTo(Player p, float amount)
        {
            p.Xp += amount * p.Stats.XpMult * Pace;  // Scholar; half in a Campaign (twice the waves, Hunts.cs)
            while (p.Xp >= Defs.XpToNext(p.Level))
            {
                p.Xp -= Defs.XpToNext(p.Level);
                LevelUpPlayer(p);
            }
        }

        /// One level: a normal pick, plus a milestone pick every 10 levels (or by Milestone Compass luck) and Second Helping's extra pick.
        void LevelUpPlayer(Player p)
        {
            p.Level++;
            p.PendingPicks++;
            if (p.Level % MilestoneEvery == 0 || p.Stats.MilestoneChance > 0f && rng.NextDouble() < p.Stats.MilestoneChance)
                p.PendingMilestones = Math.Min(255, p.PendingMilestones + 1);
            if (p.Stats.ExtraPickChance > 0f && rng.NextDouble() < p.Stats.ExtraPickChance)
                p.PendingPicks++;
            if (State == MatchState.Intermission && p.Offer == null)
                NextOffer(p);  // a level gained in the break (a dormant group woken between waves, DormantGroups.cs): its pick is offered now, or the countdown would wait on it
        }

        /// Called by the front end when the local player clicks an upgrade card.
        public void PickUpgrade(int index, int key = 0)
        {
            var me = Me;
            if (me == null || me.Offer == null || index < 0 || index >= me.Offer.Length || RerollPending)
                return;
            Sounds.Add(Sfx.Pick);
            if (IsHost)
            {
                HostPick(MyId, index, key);
            }
            else
            {
                var w = Proto.Begin(Msg.Pick);
                w.Write((byte)index);
                w.Write((byte)key);
                session.SendToHost(Proto.End(w));
                me.Offer = null;  // hide the cards until the host confirms
                awaitingPick = true;
            }
        }

        /// Called by the front end when the local player takes gold instead of a card.
        public void TakeGoldInstead()
        {
            var me = Me;
            if (!CanGoldInstead(me) || RerollPending)
                return;
            Sounds.Add(Sfx.Pick);
            if (IsHost)
            {
                HostPick(MyId, GoldInsteadIndex);
            }
            else
            {
                var w = Proto.Begin(Msg.Pick);
                w.Write((byte)GoldInsteadIndex);
                w.Write((byte)0);
                session.SendToHost(Proto.End(w));
                me.Offer = null;
                awaitingPick = true;
            }
        }

        /// Called by the front end when the local player rerolls their cards.
        public void RerollOffer()
        {
            var me = Me;
            if (me == null || me.Offer == null || me.OfferLegendary || me.OfferMod || me.OfferMilestone || me.OfferPrize || me.OfferWave || me.Rerolls <= 0 || RerollPending)  // (a boss card pick can be rerolled, a wave card pick can't: the host ignores it, so a client waiting on the reply would be stuck)
                return;
            Sounds.Add(Sfx.Pick);
            if (IsHost)
            {
                HostReroll(MyId);
            }
            else
            {
                rerollsAtRequest = me.Rerolls;
                session.SendToHost(Proto.End(Proto.Begin(Msg.Reroll)));
            }
        }

        public void HostReroll(int id)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Offer == null || p.OfferLegendary || p.OfferMod || p.OfferMilestone || p.OfferPrize || p.OfferWave || p.Rerolls <= 0 || State != MatchState.Intermission)
                return;
            p.Rerolls--;
            p.Offer = p.OfferCards ? BossCardOffer(p) : Upgrades.RollOffer(rng, OfferFor(p), p.Level);
            DoLog(p.Role + " rerolled (" + p.Rerolls + " left)");
            SendSnapshot();
        }

        public void OnPick(int peer, BinaryReader r)
        {
            int index = r.ReadByte();
            HostPick(peer, index, r.ReadByte());
        }

        /// key: for a card that grants a new ability, the key to put it on (1 = the 2nd ability, 2 = the 3rd, 3 = the 4th; 0 = first free).
        void HostPick(int id, int index, int key = 0)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Offer == null || State != MatchState.Intermission)
                return;
            if (index == GoldInsteadIndex)  // gold instead of a card: the pick is used up, nothing is granted
            {
                if (!CanGoldInstead(p))
                    return;
                int gold = GoldInsteadAmount(Wave);
                p.Gold += gold;
                p.PendingPicks = Math.Max(0, p.PendingPicks - 1);
                NextOffer(p);
                DoLog(p.Role + " took " + gold + " gold instead of a card");
                BroadcastUpgrades(p);  // (tells a client its pick landed, so it takes offers again: no card was taken to say so)
                SendSnapshot();
                return;
            }
            if (index < 0 || index >= p.Offer.Length)
                return;
            if (p.OfferCards)  // a boss card pick (BossCards.cs): to the hand or the team's kits
            {
                int code = p.Offer[index];
                if (!BossCardUsable(p, code))
                {
                    if (!AnyBossCardUsable(p, p.Offer))  // (the hand filled while the offer was up: nothing in it can be taken, so the pick is skipped, as NextOffer would)
                    {
                        UseCardPick(p);
                        NextOffer(p);
                        BroadcastUpgrades(p);
                        SendSnapshot();
                    }
                    return;
                }
                if (p.OfferWave)
                    GiveCard(p, BossCards.CardOf(code), "wave pick");
                else
                    GrantBossCard(p, code);
                UseCardPick(p);
                NextOffer(p);
                DoLog(p.Role + " picked a " + BossCards.Name(code));
                BroadcastUpgrades(p);  // (tells a client its pick landed, so it takes offers again: a boss card adds no upgrade to say so)
                SendSnapshot();
                return;
            }
            int upgrade = p.Offer[index];
            if (p.OfferLegendary)
                p.LegendaryQueue.RemoveAt(0);
            else if (p.OfferMod)
                p.PendingMods = Math.Max(0, p.PendingMods - 1);
            else if (p.OfferMilestone)
                p.PendingMilestones = Math.Max(0, p.PendingMilestones - 1);
            else if (p.OfferPrize)
                p.PrizePicks = Math.Max(0, p.PrizePicks - 1);
            else
                p.PendingPicks = Math.Max(0, p.PendingPicks - 1);
            var keysBefore = (AbilityId[])p.CardAbilities.Clone();
            PlaceOnKey(p.CardAbilities, Upgrades.All[upgrade].Grants, key);
            FollowExtraUses(p, keysBefore);
            GrantUpgrade(id, upgrade, key);
            p.Rerolls += Upgrades.All[upgrade].RerollsNow;  // Reroll Cache
            NextOffer(p);
            DoLog(p.Role + " picked " + Upgrades.All[upgrade].Name);
            SendSnapshot();  // show the next offer right away
        }

        /// Boss rewards come first (legendaries, then weapon mods or the boss card pick), then regular level-up picks.
        /// Who this player's card offers are for (role, class, 1st ability, picks, weapon, the Expanding arena):
        /// every offer and Upgrades.Available take one (Docs/UpgradePools.md step 2).
        public OfferCtx OfferFor(Player p) => OfferCtx.Of(p, ExpandingOn);

        void NextOffer(Player p)
        {
            p.PendingLegendary = p.LegendaryQueue.Count;
            p.OfferLegendary = p.LegendaryQueue.Count > 0;
            p.OfferMod = false;
            p.OfferCards = false;
            p.OfferWave = false;
            p.WavePicks = Math.Min(p.WavePicks, p.PendingCards);
            p.OfferMilestone = false;
            p.OfferPrize = false;
            int[] mods = null, cards = null;
            if (!p.OfferLegendary && p.PendingMods > 0)
            {
                mods = Upgrades.ModOffer(OfferFor(p));
                if (mods == null)
                    p.PendingMods = 0;  // every mod maxed out
            }
            if (!p.OfferLegendary && mods == null && p.PendingCards > 0)
            {
                bool wave = p.PendingCards <= p.WavePicks;  // the boss's picks first, then the wave's (Hand.cs)
                cards = wave ? WaveCardOffer(p) : BossCardOffer(p);
                if (!AnyBossCardUsable(p, cards))
                {
                    p.PendingCards = p.WavePicks = 0;  // a full hand and full kits: nothing to take
                    cards = null;
                }
                else
                    p.OfferWave = wave;
            }
            if (p.OfferLegendary)
                p.Offer = Upgrades.LegendaryOffer(rng, p.LegendaryQueue[0], OfferFor(p));
            else if (mods != null)
            {
                p.Offer = mods;
                p.OfferMod = true;
            }
            else if (cards != null)
            {
                p.Offer = cards;
                p.OfferCards = true;
            }
            else if (p.PrizePicks > 0)
            {
                p.Offer = Upgrades.PrizeOffer(rng, OfferFor(p));  // a challenge room's prize (Challenges.cs)
                p.OfferPrize = true;
            }
            else if (p.PendingMilestones > 0 && (p.Offer = Upgrades.MilestoneOffer(rng, OfferFor(p))) != null)
                p.OfferMilestone = true;  // its own pool, before the level's normal pick
            else if (p.PendingPicks > 0)
            {
                p.PendingMilestones = 0;  // (only reached with the milestone pool used up)
                p.Offer = Upgrades.RollOffer(rng, OfferFor(p), p.Level);
            }
            else
                p.Offer = null;
        }

        /// Host only: give a player an upgrade and tell everyone.
        /// key: for the Extra Use card, the key it goes on (1-4 the 1st-4th ability; 0 the 1st).
        public void GrantUpgrade(int id, int upgrade, int key = 0)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
            if (Upgrades.All[upgrade].PicksSlot)
                upgrade = Upgrades.UseCard(Math.Max(0, Math.Min(Upgrades.UseSlots - 1, key - 1)));  // it is stored as the key's own card
            p.Taken.Add(upgrade);
            Recalc(p);
            if (ExpandingOn)
                AddVectors(p, Upgrades.All[upgrade].VectorsNow);  // Architect: vectors now, up to the new cap
            BroadcastUpgrades(p);
        }

        void BroadcastUpgrades(Player p)
        {
            var w = Proto.Begin(Msg.Upgrades);
            w.Write(p.Id);
            w.Write((short)p.Taken.Count);
            foreach (int t in p.Taken)
                w.Write((short)t);  // card ids are 2 bytes (Docs/UpgradePools.md step 1)
            foreach (var k in p.CardAbilities)
                w.Write((byte)k);
            session.Broadcast(Proto.End(w));
        }

        /// Extra Use cards are stored per key; when abilities change keys, their extra uses move with them.
        void FollowExtraUses(Player p, AbilityId[] before)
        {
            var old = new int[Upgrades.UseSlots];
            for (int k = 0; k < old.Length; k++)
                old[k] = Upgrades.Count(p.Taken, Upgrades.UseCard(k));
            var now = (int[])old.Clone();
            bool changed = false;
            for (int j = 1; j <= Upgrades.CardAbilitySlots; j++)
            {
                if (p.CardAbilities[j - 1] == before[j - 1])
                    continue;
                int from = p.CardAbilities[j - 1] == AbilityId.None ? -1 : Array.IndexOf(before, p.CardAbilities[j - 1]);
                now[j] = from >= 0 ? old[from + 1] : 0;
                changed = true;
            }
            if (!changed)
                return;
            for (int k = 0; k < now.Length; k++)
            {
                int card = Upgrades.UseCard(k);
                p.Taken.RemoveAll(t => t == card);
                for (int n = 0; n < now[k]; n++)
                    p.Taken.Add(card);
            }
        }

        /// Put a 2nd–4th ability on a key (1 = the 2nd ability, 2 = the 3rd, 3 = the 4th). Whatever was there swaps to its old key.
        public static void PlaceOnKey(AbilityId[] k, AbilityId id, int key)
        {
            if (key < 1 || key > Upgrades.CardAbilitySlots || id == AbilityId.None)
                return;
            int from = Array.IndexOf(k, id);
            var displaced = k[key - 1];
            k[key - 1] = id;
            if (from >= 0 && from != key - 1)
                k[from] = displaced;  // a swap
            else if (from < 0 && displaced != AbilityId.None)
            {
                int free = Array.IndexOf(k, AbilityId.None);  // a new ability: the old one moves to a free key
                if (free >= 0)
                    k[free] = displaced;
            }
        }

        /// The local player moves a 2nd–4th ability to another key (the Tab menu).
        public void SetAbilityKey(AbilityId id, int key)
        {
            var me = Me;
            if (me == null)
                return;
            if (IsHost)
            {
                HostSetKey(MyId, id, key);
                return;
            }
            var w = Proto.Begin(Msg.SetKey);
            w.Write((byte)id);
            w.Write((byte)key);
            session.SendToHost(Proto.End(w));
        }

        public void OnSetKey(int peer, BinaryReader r)
        {
            var id = (AbilityId)r.ReadByte();
            HostSetKey(peer, id, r.ReadByte());
        }

        void HostSetKey(int playerId, AbilityId id, int key)
        {
            Player p;
            if (!Players.TryGetValue(playerId, out p) || Array.IndexOf(p.CardAbilities, id) < 0)
                return;  // you can only move abilities you have
            var before = (AbilityId[])p.CardAbilities.Clone();
            PlaceOnKey(p.CardAbilities, id, key);
            FollowExtraUses(p, before);
            Recalc(p);
            BroadcastUpgrades(p);
        }

        /// How close an awake enemy holds back your level-up cards between waves (about a screen).
        public const float PickHoldReach = 900f;

        /// Between waves the run can't pause (co-op), so a level-up won in a fight (a woken dormant group, a duel, an extra
        /// room) keeps its cards hidden while an awake enemy is near you; they come up once it's clear. The pick stays pending
        /// (the countdown waits on it as before). Both sides work it out from the replicated enemies: no message.
        public bool PickHeld(Player p)
        {
            if (p == null || p.Offer == null || State != MatchState.Intermission || SurvivalOn)
                return false;  // (Survival freezes the world for a pick instead: SurvivalGame.cs)
            foreach (var e in Enemies.Values)
                if (!e.Dormant && e.Hp > 0f && !IsPlanted(e.Type) && e.Pos.CloserThan(p.Pos, PickHoldReach))
                    return true;
            return false;
        }

        public bool AnyonePicking()
        {
            foreach (var p in Players.Values)
                if (p.PendingPicks > 0 || p.PendingLegendary > 0 || p.PendingMods > 0 || p.PendingCards > 0 || p.PendingMilestones > 0 || p.PrizePicks > 0)  // (a challenge room's prize too: the break used to run out under it)
                    return true;
            return false;
        }

        // --- Frame loop --------------------------------------------------------

        /// Fixed-rate simulation step (60 Hz).
        public void FixedStep(float dt, PlayerInput input)
        {
            TickLag(dt);  // the lag chart (LagNet.cs)
            if (!Running)
                return;
            if (Paused)
            {
                // Frozen: the host keeps sending snapshots so clients stay up to date.
                if (IsHost && ++tick % SnapshotEvery == 0)
                    SendSnapshot();
                return;
            }
            if (BreachWarnOn)
                input = new PlayerInput { AimAt = input.AimAt };  // the breach warning: everyone stands still while the cameras tour (Breaches.cs)
            LocalControl(dt, input);
            TickTutorial(dt, input);  // the first-run tutorial's prompts (Tutorial.cs)
            if (Bot)
                botTouched = true;  // the bot played: this run doesn't count for your profile (RunSaves.cs)
            if (!IsHost && (pingT -= dt) <= 0f)
            {
                pingT = PingEvery;
                var pw = Proto.Begin(Msg.Ping);
                pw.Write(clock.ElapsedTicks);
                pw.Write((ushort)Math.Min(65535f, Math.Max(0f, (float)Math.Round(PingMs))));  // for the host to share (LagNet.cs)
                session.SendToHost(Proto.End(pw));
            }
            NoteDuelBetweenWaves();  // GatekeeperRoom.cs (every game: enemies and kits fight in a duel between waves)
            StaleEnemyIndex();  // EnemyIndex.cs: regather the enemies by type this tick
            if (IsHost)
            {
                hostClock += dt;  // (every host tick, whatever the state: Dodge.cs times clients' dodges by it)
                if (State == MatchState.Fighting || State == MatchState.Intermission)
                    RunTime += dt;
                HostTick(dt);
            }
            else
            {
                MoveBullets(dt * WorldRate);  // (a perfect parry slows every shot on every screen: Parry.cs)
                tick++;
                if (tick % 120 == 0 && Log != null)  // (no formatting when nothing reads it)
                    DoLog(string.Format("wave {0} state {1} enemies {2} bullets {3} hp {4}", Wave, State, Enemies.Count, Bullets.Count, HpSummary()));
            }
            WorldSlowT = Math.Max(0f, WorldSlowT - dt);  // Parry.cs
            AgePatches(dt);  // Paths.cs (every game, so the floors look the same everywhere)
            TickLayout(dt);  // LayoutRules.cs (every game: a joiner's walls fade in off this clock, or they stay invisible)
            TickRunTimeline(dt);  // RunTimeline.cs (every game: the Recent runs graphs)
            TickArenaRules(dt);
            foreach (var e in Enemies.Values)
            {
                // Heat: full after about 0.4 s of steady hits, gone about 0.5 s after they stop (drawing only).
                e.FlashHeat = e.Flash > 0f ? Math.Min(1f, e.FlashHeat + dt * FlashHeatUp) : Math.Max(0f, e.FlashHeat - dt * FlashHeatDown);
                e.Flash = Math.Max(0f, e.Flash - dt);
            }
            foreach (var p in Players.Values)
            {
                p.Flash = Math.Max(0f, p.Flash - dt);
                p.LevelFlash = Math.Max(0f, p.LevelFlash - dt);
            }
        }

        /// Per-frame visual update: smoothing, effects and sound cues.
        public void Update(float dt)
        {
            if (!Running)
                return;
            AnimT += dt;
            TickShopNotices(dt);
            TickExpansion(dt);     // a bot player's vote (ExpansionGame.cs)
            TickHuntMapView(dt);   // the hunt map's vote timer on a client, a bot's vote (HuntMapGame.cs)
            TickKeepsakeView(dt);  // the keepsake vote's timer on a client, a bot's vote (MapReset.cs)
            TickGates(dt);         // the shutter gates' clocks (Gates.cs)
            UpdatePropView(dt);    // crates, barrels, chests and their items (Props.cs)
            UpdateInkView(dt);     // Chalk.cs: the chalk's tip easing, a client's ink ageing
            UpdateHiveView(dt);    // HiveCenser.cs: the swarms and pour points easing on screen
            UpdateWardenView(dt);  // Warden.cs
            UpdateHoldingCellView(dt);  // HoldingCell.cs
            UpdateDrillView(dt);   // DrillGame.cs: a client's drill clocks and walls, and its own finish
            UpdateSingView(dt);    // Singularity.cs
            UpdateHeraldView(dt);  // Herald.cs
            UpdateEclipseView(dt); // Eclipse.cs
            UpdateDevourView(dt);  // Devourer.cs
            UpdateDogmaView(dt);   // DragonsDogma.cs: the dragon's trail on screen
            if (IsHost)
                LetGoOfSwallowed();  // Devourer.cs
            UpdateDuelView(dt);    // Duelist.cs
            UpdateLichView(dt);    // Lich.cs
            UpdateColossusView(dt);  // Colossus.cs
            UpdateStompRings(dt);    // Stompers' rings (QuarryFaction.cs)
            UpdateCourt(dt);         // Parriers' parried shots (CourtFaction.cs)
            UpdateQueenView(dt);   // Queen.cs
            UpdateReaperView(dt);  // Reapers.cs
            StepLightDisplay(dt);  // Searchlights (Stun.cs)
            StepRadiantDisplay(dt);    // Radiant Orbs (RadiantOrb.cs)
            foreach (var p in Players.Values)
                p.StunT = Math.Max(0f, p.StunT - dt);
            foreach (var p in Players.Values)
            {
                p.WarpT = Math.Max(0f, p.WarpT - dt);  // (BossBeam.cs)
                p.PvpSlowT = Math.Max(0f, p.PvpSlowT - dt);  // (Pvp.cs)
            }
            float k = 1f - (float)Math.Exp(-18.0 * dt);
            foreach (var p in Players.Values)
                // Snap instead of sliding across the screen when the player wraps around an edge.
                p.Disp = p.Id == MyId || Math.Abs(p.Disp.X - p.Pos.X) > Width / 2f || Math.Abs(p.Disp.Y - p.Pos.Y) > Height / 2f
                    ? p.Pos : V2.Lerp(p.Disp, p.Pos, k);
            foreach (var e in Enemies.Values)
            {
                e.Disp = e.Disp.FartherThan(e.Pos, 600f) ? e.Pos : V2.Lerp(e.Disp, e.Pos, k);  // (a jump through a Rooms portal: snap, don't slide)
                if (IsSwordsman(e.Type) || IsSecurity(e.Type) || IsInfall(e.Type) || IsPenumbral(e.Type) || IsSquall(e.Type))  // (the Security Guard's, the Infall's and the Court's looks are timed the same way)
                {
                    if (e.Phase != e.SeenPhase)
                    {
                        e.SeenPhase = e.Phase;
                        e.SeenT = 0f;
                    }
                    else
                        e.SeenT += dt;  // clients only get PhaseT every snapshot, so the swing is timed from here
                }
            }
            if (!Paused)  // (silent while paused: the bite and step timers would keep firing)
                UpdateMeleeSounds(dt);  // the melee rushers' cues, quieter with distance (MeleeSounds.cs)
            foreach (var o in Orbs.Values)
                o.Disp = IsHost ? o.Pos : V2.Lerp(o.Disp, o.Pos, k);
            foreach (var c in Coins.Values)
                c.Disp = IsHost ? c.Pos : V2.Lerp(c.Disp, c.Pos, k);
            foreach (var g in Gadgets.Values)
                g.Disp = IsHost || g.Flying ? g.Pos : V2.Lerp(g.Disp, g.Pos, k);
            foreach (var m in Minions.Values)
                m.Disp = IsHost ? m.Pos : V2.Lerp(m.Disp, m.Pos, k);
            BreachAge += dt;
            // One compacting pass that keeps the order (RemoveAt shifted the list once per expiry). Effects are not
            // pooled: the looks keep references across frames and test them with Effects.Contains.
            int keep = 0;
            for (int i = 0; i < Effects.Count; i++)
            {
                var fx = Effects[i];
                fx.T += dt;
                if (fx.T < fx.Dur)
                    Effects[keep++] = fx;
            }
            if (keep < Effects.Count)
                Effects.RemoveRange(keep, Effects.Count - keep);

            if (State != lastState)
            {
                if (State == MatchState.Fighting)
                {
                    Sounds.Add(Sfx.WaveStart);
                    BreachAge = 0f;  // this wave's breaches open (Breaches.cs)
                }
                else if (State == MatchState.Intermission && lastState == MatchState.Fighting)
                    Sounds.Add(Sfx.WaveClear);
                else if (State == MatchState.GameOver)
                    Sounds.Add(Sfx.GameOver);
                else if (State == MatchState.Victory)
                    Sounds.Add(Sfx.Victory);
                lastState = State;
            }
            foreach (var p in Players.Values)
            {
                bool wasDown;
                if (lastDowned.TryGetValue(p.Id, out wasDown) && wasDown != p.Downed)
                    Sounds.Add(p.Downed ? Sfx.Downed : Sfx.Revive);
                lastDowned[p.Id] = p.Downed;
                int lvl;
                if (lastLevel.TryGetValue(p.Id, out lvl) && p.Level > lvl)
                {
                    p.LevelFlash = 1.5f;
                    if (p.Id == MyId)
                        Sounds.Add(Sfx.LevelUp);
                }
                lastLevel[p.Id] = p.Level;
            }
            var me = Me;
            if (me != null)
            {
                if (me.Hp < lastMyHp - 0.5f && !me.Downed)
                    Sounds.Add(Sfx.Hurt);
                lastMyHp = me.Hp;
            }
        }

        readonly bool[] heldScratch = new bool[5];  // LocalControl's ability keys this tick

        /// Set by the front end each frame: your controls are held (a run launching: you can't move, dash, shoot, parry or
        /// use abilities until the fight starts; LaunchIntro.cs). Your own game only.
        public bool HoldControls;

        void LocalControl(float dt, PlayerInput input)
        {
            var me = Me;
            if (me == null)
                return;
            if (!IsHost)
                TickSlots(me, dt);  // the host ticks everyone's slots in TickPlayers
            V2 move = input.Move.LimitLength(1f);
            V2 aimAt = input.AimAt;
            V2 before = me.Pos;
            me.Aim = (aimAt - me.Pos).Angle;
            bool parry = input.Parry || HoldParry, weaponAction = input.WeaponAction;  // (the smart bot sets its own: BotBrain.cs)
            bool dodgePress = input.Dodge, dodgeHeld = input.DodgeHeld;  // (and its own dodge: BotDodge.cs)
            me.Parrying = parry && !me.Downed;
            bool firing = input.Fire && !me.Downed && (!me.ParryActive || FiresBehindShield(me));  // can't shoot behind the shield (but Fan the Hammer can: PistolWhip.cs)
            var held = heldScratch;  // (one array, filled each tick)
            held[0] = input.Ability; held[1] = input.AbilityQ; held[2] = input.AbilityE; held[3] = input.AbilityShift; held[4] = input.Ultimate;

            if (Bot && BotLevel != BotSkill.Off)
            {
                botT += dt;
                SmartBot(me, dt, ref move, ref aimAt, ref firing, held, ref weaponAction, ref parry, ref dodgePress, ref dodgeHeld);  // BotBrain.cs
                me.Parrying = parry && !me.Downed;
                if (me.ParryActive && !FiresBehindShield(me))
                    firing = false;
            }
            else if (Bot)
            {
                botT += dt;
                move = V2.FromAngle(botT * 0.9f + MyId);
                Enemy nearest = null;
                foreach (var e in Enemies.Values)
                    if (!SoulBound(e) && (nearest == null || e.Pos.DistanceSq(me.Pos) < nearest.Pos.DistanceSq(me.Pos)))
                        nearest = e;
                if (nearest != null)
                {
                    aimAt = nearest.Pos;
                    me.Aim = (aimAt - me.Pos).Angle;
                }
                firing = !me.Downed && (!ChargeWeapon(me.Weapon.Id) || MathX.Fmod(botT, 1.2f) < 1f);  // bow and Warhammer: charge, then let go
                for (int i = 0; i < held.Length; i++)
                    held[i] = MathX.Fmod(botT + i * 1.3f, 4f) < 0.1f;
            }
            if (Bot && HoldingCells.Count > 0)
                CellBot(me, ref move, ref aimAt, ref firing, held);  // in a Holding Cell, or a teammate in one (HoldingCell.cs)
            if (Bot)
            {
                // Both bots: level-up picks (reroll once per pick while rerolls last, then take the best-scored card: BotPicks.cs) and the Bazaar.
                if (me.Offer != null && MathX.Fmod(botT, 0.5f) < dt)
                {
                    if (BotRerolls && me.Rerolls > 0 && botRerolledAt != me.Taken.Count)
                    {
                        botRerolledAt = me.Taken.Count;
                        RerollOffer();
                    }
                    else
                        PickUpgrade(me.OfferCards ? BotBossCard(me) : BotPickUpgrade(me));  // BotPicks.cs
                }
                else if (me.Offer == null && ShopOpen && !me.ShopDone && MathX.Fmod(botT, 0.5f) < dt)
                    BotShop(me);  // Shop.cs
                BotGate(me, ref move);      // into the boss gate (BossGate.cs)
                BotHorse(me, ref move, dt); // climb into a Trojan Horse (TrojanHorse.cs)
                BotMysteryDoor(me, ref move);  // open a mystery door it can pay for, between waves (MysteryDoors.cs)
                BotTreasure(me, ref move);  // open the chest and take your item (Props.cs)
                BotRoom(me, ref move);      // a portal, a zone, the crystal, the altar (Challenges.cs)
            }

            if (HoldControls)
            {
                move = V2.Zero;
                firing = false;
                me.Parrying = false;
                for (int i = 0; i < held.Length; i++)
                    held[i] = false;
                DashT = 0f;
                iceVel = V2.Zero;
            }
            if (KatanaStriking && DashT <= 0f)
            {
                move = V2.Zero;  // the Katana's Momentum Burst: planted for a moment (Katana.cs)
                firing = false;
            }
            if (BellRooted(me))
                move = V2.Zero;  // braced under your Siege Bell while it rings (SiegeBell.cs)
            if (Riding(me))
            {
                move = V2.Zero;  // inside a Trojan Horse: it carries you (TrojanHorse.cs)
                DashT = 0f;
            }
            if (Seated(me))
            {
                move = V2.Zero;  // sitting in your Deployable Turret: rooted, and it fires instead of your weapon (DeployableTurret.cs)
                DashT = 0f;
                var ba = BallistaOf(me);
                if (ba != null)
                    me.Pos = BaSeat(ba);  // on a Ballista: in its seat, behind the bow (Ballista.cs)
            }
            if (me.Weapon.Id == WeaponId.ArcKnuckles && (me.KnuckleChain == 2 || me.KnuckleChain == 4 || me.KnuckleRooted) && DashT <= 0f)
                move = V2.Zero;  // the Arc Knuckles' Flurry, Grab & Throw and Barrage: rooted while they run (ArcKnucklesChain.cs, ArcKnucklesBrawl.cs)
            bool oathClick = LocalOathPick(me, aimAt, input.Fire && !Bot, ref firing);  // Guardian's Oath: Fire picks the ally instead of shooting (GuardiansOath.cs)
            bool hospClick = LocalHospPick(me, aimAt, input.Fire && !Bot, ref firing);  // Field Hospital: Fire picks the door instead of shooting (FieldHospital.cs)
            float bowRelease = LocalBow(me, firing && !Riding(me), dt);  // (in a Trojan Horse: arrows, no charge: TrojanHorse.cs)
            if (me.Downed)
                DashT = 0f;
            else if (DashT > 0f)
            {
                KickReachOn(me, dt);  // a Dodge ending just short of a wall reaches on to it (Dodge.cs)
                DashT -= dt;
                me.Pos += DashDir * dashSpeed * dt;
                bool iceDash = OnPathOf(PathId.FrozenLake) || FloorAt(me.Pos) == PathId.FrozenLake;
                iceVel = DashDir * me.Speed * (iceDash ? Paths.IceTopSpeed : 1f);  // you come out of a dash moving (on ice, sliding)
            }
            else if (ArmOn(me))
                LocalArmRoll(me, move, aimAt, firing && !HoldControls, dt);  // Iron Armadillo: rolling, not walking (IronArmadillo.cs)
            else
            {
                V2 want = move * me.Speed * SprintMult * (me.ParryActive && !me.Stats.ParryNoSlow ? ParrySlow : 1f) * (bowDraw >= 0f ? BowDrawSlow : 1f) * HoneySlowAt(me.Pos) * CrowdSlow(me) * RelicSpeedAt(me);  // CrowdSlow: Rioters piled on you (RiotBrains.cs)
                if (OnPathOf(PathId.FrozenLake) || FloorAt(me.Pos) == PathId.FrozenLake)
                {
                    // Frozen Lake (Paths.cs): speed builds toward where you're heading and carries on when you let go.
                    iceVel += (want * Paths.IceTopSpeed - iceVel) * Math.Min(1f, Paths.IceGrip * dt);
                    me.Pos += iceVel * dt;
                }
                else
                {
                    // A little weight (WalkStartTime, WalkStopTime in Balance.cs): speed builds and falls fast, but not at once.
                    bool braking = want.IsZero || want.Dot(iceVel) < 0f;
                    float top = Math.Max(me.Speed, want.Length);
                    iceVel += (want - iceVel).LimitLength(top * dt / (braking ? WalkStopTime : WalkStartTime));
                    me.Pos += iceVel * dt;
                }
            }
            if (!ArmOn(me))
                LocalArmRoll(me, move, aimAt, false, dt);  // (resets the roll)
            else
                firing = false;  // curled up: Fire revs the roll, the weapon is away
            if (me.Downed)
                iceVel = V2.Zero;
            if (PullStrength > 0f && !me.Downed && DashT <= 0f)
                me.Pos += (PullCenter - me.Pos).Normalized() * PullStrength * dt;
            if (!me.Downed && DashT <= 0f)
                me.Pos += WellDriftAt(me.Pos) * dt;  // the Singularity's corner wells (Singularity.cs)
            if (!me.Downed && DashT <= 0f)
                me.Pos += HeraldWind(WindPush) * dt;  // the Storm Herald's Gale (Herald.cs)
            if (!me.Downed && DashT <= 0f)
                me.Pos += DevourPull(me.Pos) * dt;  // the Devourer breathing in (Devourer.cs)
            if ((me.StunT > 0f || me.StandOn || me.WarpT > 0f) && !me.Downed)
            {
                me.Pos = before;  // stunned by the Warden, or planted behind Last Stand: stuck where you are
                DashT = 0f;
            }
            WardenGatePush(me, before);  // the Warden's gates shove you back (Warden.cs)
            ColossusPush(me, before);    // the Colossus's stomp rings, plates and pillars (Colossus.cs)
            StomperPush(me);             // a Stomper's small rings (QuarryFaction.cs)
            if (IsHost && Props.Count > 0 && !me.Downed)
                ShoveProps(me.Pos, me.Def.HitR + PropPad, PushSpeed(me), dt, 0f, V2.Zero);  // leaning on a crate or barrel slides it (Props.cs)
            if (LayoutOn && !me.Downed)
            {
                V2 free = me.Pos;
                me.Pos = OneWayStep(before, me.Pos, me.Def.HitR + PropPad);  // a One-way piece's doors: no dashing through the wrong way (OneWayDoors.cs)
                if (me.Pos.FartherThan(free, 0.25f))
                {
                    OneWayPushes++;  // (your screen plays the bump: OneWayDoorLook.cs)
                    OneWayPushAt = me.Pos;
                }
            }
            if ((Props.Count > 0 || LayoutOn) && !me.Downed)
            {
                V2 kickWant = me.Pos;
                me.Pos = PushOutOfProps(me.Pos, me.Def.HitR + PropPad);  // crates, barrels and a closed chest are solid (Props.cs)
                WallKick(me, kickWant);  // a Dodge into it bounces off (Dodge.cs)
            }
            // Touching a boss shoves you back out (its body doesn't hurt: TickEnemies).
            if (!me.Downed && shoveT <= 0f)
                foreach (var e in Enemies.Values)
                    if (e.Def.IsBoss && !IsSpawning(e) && !QueenAirborne(e) && !DevourUnder(e) && !DevourSwallowing(e) && me.Pos.CloserThan(e.Pos, e.Radius + me.Def.HitR + 2f))
                    {
                        V2 away = me.Pos - e.Pos;
                        shoveDir = away.IsZero ? V2.FromAngle(me.Aim + MathX.Pi) : away.Normalized();
                        shoveT = BossShoveTime;
                        break;
                    }
            if (shoveT > 0f)
            {
                shoveT -= dt;
                me.Pos += shoveDir * BossShoveSpeed * dt;
            }
            ApplyKnock(me, dt);  // a Riot Shield's bash (WardenFaction.cs)
            if (PinSwallowed(me))  // inside the Devourer you go where it goes (Devourer.cs)
            {
                DashT = 0f;
                shoveT = 0f;
                knockLeft = 0f;
            }
            else
                PitLocal(me, before);  // over a pit: fall, climb out on the rim (Pits.cs)
            var myHorse = HorseOf(me);
            if (myHorse != null)
            {
                me.Pos = myHorse.Pos;  // inside a Trojan Horse: you go where it goes (TrojanHorse.cs)
                DashT = 0f;
                shoveT = 0f;
                knockLeft = 0f;
            }

            for (int slot = 0; slot < held.Length; slot++)
            {
                bool pressed = held[slot] && !abilityWasDown[slot];
                abilityWasDown[slot] = held[slot];
                if (OathPickSlot == slot)
                {
                    if (pressed)
                    {
                        OathPickSlot = -1;  // the key again: never mind
                        continue;
                    }
                    pressed = oathClick;  // the click on an ally is the cast
                }
                else if (HospPickSlot == slot)
                {
                    if (pressed)
                    {
                        HospPickSlot = -1;  // the key again: never mind
                        continue;
                    }
                    pressed = hospClick;  // the click on a lit door is the cast
                }
                else if (pressed && !Bot && me.Slots[slot].Id == AbilityId.FieldHospital && !me.Downed && HospOnGrid)
                {
                    if (me.Slots[slot].Charges > 0 && AnyHospSpot(me))
                        HospPickSlot = slot;  // now click a lit door (no free door in reach: nothing happens, nothing spent)
                    continue;
                }
                else if (pressed && !Bot && me.Slots[slot].Id == AbilityId.GuardiansOath && !me.Downed)
                {
                    if (me.Slots[slot].Charges > 0 && OathTarget(me, aimAt, OathRangeFor(me)) != null)
                        OathPickSlot = slot;  // now click the ally to grab (nobody in reach: nothing happens, no cooldown spent)
                    continue;
                }
                if (!pressed || me.Downed || State == MatchState.GameOver)
                    continue;
                if (me.Slots[slot].Id == AbilityId.WaterWard && WwFull(me))
                    continue;  // every drop already out: don't waste the cooldown (WaterWard.cs)
                if (me.Slots[slot].Id == AbilityId.CeremonialArmor && me.PlateN >= CerePlateMax)
                    continue;  // Ceremonial Armor with all 10 plates up: don't waste the cooldown
                if (me.Slots[slot].Id == AbilityId.SenbonSeekers && SbFull(me))
                    continue;  // Senbon Seekers holding all its needles (SenbonSeekers.cs)
                if ((me.StandOn || Seated(me)) && MovesYou(me.Slots[slot].Id))
                    continue;
                if (TankOn(me) || ArmOn(me) || Riding(me))
                    continue;  // curled up in Iron Armadillo (IronArmadillo.cs), or in A Literal Tank: no abilities (LiteralTank.cs)  // planted: nothing that would carry you off (LastStand.cs)
                bool orbitOut = OrbitRelease(me, slot);  // Blade Fan pressed again: the host throws the kunai (Ninja.cs)
                bool harvestOut = HarvestPress(me, slot);  // Crimson Harvest pressed again: the host throws the ball (CrimsonHarvest.cs)
                bool standOut = StandRelease(me, slot);  // Last Stand pressed again: the host brings it down (LastStand.cs)
                bool shellOut = ShellRelease(me, slot);  // Blood Shell pressed again: the host brings it down (BloodShell.cs)
                bool rainOut = RainPress(me, slot);  // Sanguine Rain pressed again: the host cancels it (SanguineRain.cs)
                bool boilOut = BoilPress(me, slot);  // Blood Boil pressed again: the host ends it (BloodBoil.cs)
                bool bathOut = BathPress(me, slot);  // Bloodbath pressed again: the host ends the channel (Bloodbath.cs)
                bool sprayOut = SprayRelease(me, slot);  // Arterial Spray pressed again: the host closes the vein (ArterialSpray.cs)
                bool wallOut = WallRelease(me, slot);  // Blood Wall pressed again: the host stops it (BloodWall.cs)
                bool galeOut = GalePress(me, slot);  // Gale Wall pressed again: the host unleashes it (GaleWall.cs)
                bool turretOut = TurretPress(me, slot);  // Deployable Turret pressed again: the host lets you out (DeployableTurret.cs)
                bool graspOut = GraspRelease(me, slot);  // Kinetic Grasp pressed again: the host throws the held enemy (Arcanist.cs)
                if (!graspOut && !turretOut && !galeOut && !orbitOut && !standOut && !harvestOut && !rainOut && !boilOut && !bathOut && !shellOut && !sprayOut && !wallOut && !Consume(me, slot, false))
                    continue;
                V2 target = aimAt, castFrom = me.Pos;  // (where you stood, for the Shadow Step's look)
                switch (me.Slots[slot].Id)
                {
                    case AbilityId.LeapSlam:
                    {
                        // Leap to the cursor (up to 320 px); the host lands the slam there.
                        target = ClampToArena(me.Pos + (aimAt - me.Pos).LimitLength(LeapRange), me.Def.BodyR);
                        float dist = target.DistanceTo(me.Pos);
                        DashDir = dist > 1f ? (target - me.Pos) / dist : V2.Zero;
                        DashT = LeapTime;
                        dashSpeed = dist / LeapTime;
                        break;
                    }
                    case AbilityId.GuardiansOath:
                        target = LocalOathCharge(me, aimAt, Bot ? OathTarget(me, aimAt, OathRangeFor(me)) : OathPickTarget(me, aimAt));  // GuardiansOath.cs: charge to the ally you clicked
                        OathPickSlot = -1;
                        break;
                    case AbilityId.FieldHospital:
                    {
                        var spot = Bot || HospPickSlot < 0 ? (HospSpot?)null : HospPickTarget(me, aimAt);
                        target = spot.HasValue ? spot.Value.Door : Bot ? HospBotTarget(me) : aimAt;  // FieldHospital.cs: the door you clicked (outside a Grid run: the tent goes at the cursor)
                        HospPickSlot = -1;
                        break;
                    }
                    case AbilityId.RoyalDive:
                    {
                        // The Hive Queen's dive: carried to the cursor (up to 380 px); the host lands the crash there.
                        target = ClampToArena(me.Pos + (aimAt - me.Pos).LimitLength(RoyalDiveRange), me.Def.BodyR);
                        float dist = target.DistanceTo(me.Pos);
                        DashDir = dist > 1f ? (target - me.Pos) / dist : V2.Zero;
                        DashT = RoyalDiveTime;
                        dashSpeed = dist / RoyalDiveTime;
                        break;
                    }
                    case AbilityId.VeinRush:
                    {
                        // Vein Rush: carried to the cursor (up to 280 px) as a streak of blood; the host cuts along the way.
                        target = VeinRushLanding(me, aimAt);
                        float dist = target.DistanceTo(me.Pos);
                        DashDir = dist > 1f ? (target - me.Pos) / dist : V2.Zero;
                        DashT = VeinRushTime;
                        dashSpeed = dist / VeinRushTime;
                        break;
                    }
                    case AbilityId.Bulldoze:
                        DashDir = V2.FromAngle(me.Aim);
                        DashT = BulldozeTime;
                        dashSpeed = BulldozeSpeed;
                        target = ClampToArena(me.Pos + DashDir * BulldozeSpeed * BulldozeTime, me.Def.BodyR);  // where the charge ends
                        break;
                    case AbilityId.Airstrike:
                        target = ClampToArena(aimAt, AirEdgeInset);  // the flare lands anywhere in the arena (Airstrike.cs)
                        break;
                    case AbilityId.FlameWall:
                    case AbilityId.Meteor:
                    case AbilityId.Blizzard:
                        target = ClampToArena(me.Pos + (aimAt - me.Pos).LimitLength(SpellRange), 10f);
                        break;
                    case AbilityId.ShadowStep:
                        target = ShadowStepLanding(me, aimAt);  // Ninja.cs: behind the enemy nearest your cursor
                        AddFx(FxKind.Ring, me.Pos, new Rgba(0.45f, 0.4f, 0.65f), 26f, 0.3f);
                        me.Pos = target;
                        DashT = 0f;
                        break;
                    case AbilityId.Blink:
                        target = TeleportLanding(me.Pos, me.Pos + (aimAt - me.Pos).LimitLength(BlinkRange), me.Def.BodyR);  // (Ninja.cs)
                        AddFx(FxKind.Ring, me.Pos, new Rgba(0.7f, 0.6f, 1f), 30f, 0.3f);
                        me.Pos = target;
                        DashT = 0f;
                        break;
                }
                if (IsHost)
                {
                    StepFrom = castFrom;
                    HostAbility(MyId, slot, target, true);
                    StepFrom = null;
                }
                else
                {
                    var w = Proto.Begin(Msg.Ability);
                    w.Write((byte)slot);
                    w.Write(target);
                    w.Write(castFrom);  // (only the Shadow Step reads it)
                    session.SendToHost(Proto.End(w));
                }
            }

            LocalDodge(me, move, dodgePress, dodgeHeld, dt);  // the tiny dash with invulnerability (Dodge.cs)
            me.Sprint = (byte)Math.Round(SprintFade * 255f);  // for everyone else's wake (sent in Msg.Input, then the snapshot)
            LocalWeaponDash(me, move, aimAt, weaponAction && !me.ParryActive && !TankOn(me) && !ArmOn(me) && !Riding(me) && !RocketOn(me), dt);  // weapon secondaries on their own key (Ninja.cs)
            if (LocalLivingBolt(me, firing && !HoldControls, aimAt, dt))
                firing = false;  // electrified: Fire is the dash (LivingBolt.cs)
            if (LocalRavager(me, firing && !HoldControls, dt))
                firing = false;  // the blood legs are out: Fire lashes (BloodRavager.cs)
            if (LocalGreatStar(me, firing && !HoldControls, aimAt, dt))
                firing = false;  // the Great Star stance: Fire draws the route and throws instead (GreatStar.cs)
            V2 edgeWant = RoomsHold(before, me.Pos, me.Def.BodyR);  // (a room locks behind you: Rooms.cs)
            me.Pos = KeepInArena(edgeWant, me.Def.BodyR);
            if (Walled)
                WallKick(me, edgeWant);  // a Dodge into the arena's wall bounces off (Dodge.cs)
            me.Pos = KeepInMarshalCircles(before, me.Pos, me.Def.BodyR);  // a Marshal's circle holds you (CourtFaction.cs)
            me.Pos = RoomsPortal(me.Pos, ref me.PortalT, dt, me.Def.BodyR);  // wraparound portals (Rooms.cs)
            if (me.Pos.FartherThan(before, 600f))
            {
                me.Disp = me.Pos;
                steerMem.Remove(-me.Id);  // (the bot's route: fresh from the far side)
            }
            me.FiringSeen = firing;
            me.AimAt = aimAt;
            me.KnuckleSteer = input.Steer;
            me.IsBot = Bot;
            if (IsHost)
            {
                me.Firing = firing;
                if (bowRelease > 0f)
                {
                    me.BowRelease = bowRelease;
                    me.BowFocus = bowFocusOut;  // timed in the Focus bar's zone (BowFocus.cs)
                }
            }
            else
            {
                var w = Proto.Begin(Msg.Input);
                w.Write(me.Pos);
                w.Write(me.Aim);
                w.Write(me.AimAt);
                w.Write(firing);
                w.Write(me.Parrying);
                w.Write(me.DrawT);
                w.Write(bowRelease);
                w.Write(bowFocusOut);  // the release was a Focus shot (BowFocus.cs)
                w.Write(me.Sprint);  // the Dodge's sprint, for the others' wake (Dodge.cs)
                w.Write(Bot);
                w.Write(me.KnuckleSteer);  // the Arc Knuckles' click wheel (ArcKnucklesBrawl.cs)
                session.SendToHost(Proto.End(w));
            }
        }

        bool AnyTarget()
        {
            foreach (var e in Enemies.Values)
                if (!IsSpawning(e))
                    return true;
            return false;
        }

        V2 ClampToArena(V2 pos, float r)
        {
            int cell = CellSlotAt(pos, CellZone);
            if (cell >= 0)
                return ClampToCell(cell, pos, r);  // in a Holding Cell: its bars are the walls (HoldingCell.cs)
            return pos.Clamp(ArenaMin + new V2(r, r), ArenaMax - new V2(r, r));
        }

        /// Players wrap around: walking off one edge brings you in on the opposite side.
        public static V2 WrapArena(V2 pos) => new V2(MathX.Fmod(pos.X, Width), MathX.Fmod(pos.Y, Height));

        // --- Host simulation ---------------------------------------------------

        void HostTick(float dt)
        {
            tickDt = dt;
            TickLiveStats(dt);  // RunStats.cs
            TickPlayers(dt);
            TickKnights(dt);  // Knight.cs
            TickDives(dt);    // Royal Dive landings (QueenLoot.cs)
            TickScytheClock(dt);  // Twin Scythes' cut timers (ReaperLoot.cs)
            foreach (var tick in Classes.TickHooks)  // each class's KitDef.Tick: TickNinjas (Ninja.cs), TickHemomancers (Hemomancer.cs)
                tick(this, dt);
            TickTsunami(dt);  // Tsunami.cs: waves rolling out
            TickHolyCross(dt);  // HolyCross.cs: Holy Crosses flying
            TickCavalry(dt);    // CavalryCall.cs: lancers riding
            TickVolleys(dt);    // WarhornVolley.cs: the arrows' front crossing the field
            TickCannons(dt);    // CannonCall.cs: the iron balls dropping on their marks
            TickOrbitals(dt);   // OrbitalLaser.cs: the beam's kills and the scorch
            TickAirstrikes(dt);  // Airstrike.cs: jets bombing their lanes, the lanes burning
            TickJugg(dt);       // Juggernaut.cs: the suit's time, and taking it off
            TickTanks(dt);      // LiteralTank.cs: the tank's time, running enemies over, getting out
            TickRocketLaunchers(dt);  // RocketDrop.cs: the launcher's time, dropping the tube
            TickHorseRiders(dt);  // TrojanHorse.cs: riders of a horse that's gone, Sally Forth's time
            TickArmadillo(dt);  // IronArmadillo.cs: the ball's time, its speed and hits, the uncurl slam
            TickColossus(dt);   // StoneColossus.cs: the golem's time, feet, slams and crumbling boulders
            TickGolem(dt);      // BloodGolem.cs: the gather, the golem's swings, shedding, clots and collapse
            TickSpirit(dt);     // SpiritAvatar.cs: the spirit's time, Moon Cleaves and swallowed shots
            TickGaleWalls(dt);  // GaleWall.cs: lenses run down and unleash
            TickSanctumGlass(dt);  // SanctumGlass.cs: panes run down and shatter, motes land and heal
            TickWaterWards(dt);    // WaterWard.cs: the drops circle, land, spread and splash
            TickHarvest(dt);    // CrimsonHarvest.cs: blood torn out, the ball held and thrown
            TickBloodFreeze(dt);  // BloodFreeze.cs: the shockwave freezing what it reaches
            TickShockwaves(dt);   // Shockwave.cs: the Shockwave ability's wave running out
            TickShocks(dt);   // KitWeapons.cs: the Storm Hammer's electrified enemies
            TickIonTrails(dt);  // KitWeapons.cs: the Railgun's charged paths
            TickPack(dt);     // KitWeapons.cs: the Wolf Whistle's prey
            string tickSrc = CreditTo("weapon");  // (these are the weapons' own systems: the session recording credits them)
            TickOrbiters(dt);  // KitWeapons.cs: Ice Shards (WeaponId.IceShards) launching
            TickTwinBlades(dt);  // TwinBlades.cs: the chain forgetting its place, Twin Fang's sabres flying
            TickLightbringer(dt);  // Lightbringer.cs: Mercy's wards fading, Sunlances flying
            TickVowguard(dt);  // Vowguard.cs: steps, timed heals, Aegis Domes, Cover Ally
            TickHalberd(dt);  // Halberd.cs: the set pike turning and poking
            TickBulwark(dt);  // BulwarkBarrier.cs: the Brace held or dropped, Ram draining, a full bash's shards
            TickLance(dt);  // Lance.cs: Hook & Haul's cooldown and hooks landing
            TickCenser(dt);      // Censer.cs: the rite forgetting its place, Censer Cast landing
            TickKatanaLater(dt);  // Katana.cs: the Katana's timed cuts, the Momentum Burst, the caltrops
            TickHammerThrows(dt);   // StormHammer.cs: throws leaving the hand
            TickHammerSlams(dt);    // StormHammer.cs: slams landing and their shockwaves
            TickBoShoves(dt);   // BoStaff.cs: the Bo Staff's shoves landing
            TickStaff(dt);      // Quarterstaff.cs: the jab's cooldown, stacks dropped with the staff
            TickScatter(dt);    // BowFocus.cs: the Mender's Bow's Alt cooling down
            TickBowWind(dt);    // BowFocus.cs: Focus arrows' wind shoving what they pass
            TickBallistaBolts(dt);  // Ballista.cs: its bolts knocking what they pass and the shots in their path
            dmgSrc = tickSrc;
            TickRepulsion(dt);  // Repulsion.cs: the Undertow pulling and draining its meter
            TickTwinRifle(dt);  // TwinGrenades.cs: the grenade tubes' cooldown, emptied with the rifle
            TickTankAlts(dt);   // TankAlts.cs: Double Barrel, Proximity Shells, Tear Gas
            TickSmokeShells(dt);  // GrenadeLauncher.cs: smoke clouds; the launcher refills when put away
            TickWindWaves(dt);  // WindClash.cs: Wind Clash's waves and Tempests
            TickArcBalls(dt);   // StaticShock.cs: Arc Cannon balls and bursts
            TickTessenPetals(dt);  // TwinTessen.cs: the Petal Spin's petals landing
            TickHeartDrops(dt);  // HeartFlail.cs: the Heart Flail's blood drops landing
            TickHive(dt);       // HiveCenser.cs: the Hive Censer's insects settling, swarms hunting and wrapping, chitin
            TickBloodBlast(dt);  // BloodBlast.cs: Blood Blast's returning blood
            TickTide(dt);       // BloodBlast.cs: Crimson Tides gathering and rolling out
            TickFrostStrikes(dt);  // FrostLance.cs: the Frost Lance's chain, chill and Absolute Zero
            TickBoneGuard(dt);  // BoneGuard.cs: marrow threads running out, Ossuary Walls landing
            TickGreat(dt);      // Greatsword.cs: the chain forgetting its place
            TickArcKnuckles(dt);  // ArcKnuckles.cs: an unused Counter Hammer fading
            TickSteelWalls(dt);  // Knight.cs: Greatsword crescents stopping bullets
            TickStuckStars(dt);  // Ninja.cs: Shuriken stars falling out
            TickVines(dt);    // KitWeapons.cs: the Treant Rod's vine
            TickHealerKit(dt);   // KitWeapons.cs: Blast Ofudas, seeded enemies
            TickNimbus(dt);      // Nimbus.cs: rain under each cloud heals allies in it
            TickChalk(dt);       // Chalk.cs: drawing, and what the ink does
            TickLarderHost(dt);     // LarderFaction.cs: loose bile pools, morsels stepped on with no Devourer about
            TickWardenFaction(dt);  // WardenFaction.cs: tear gas with no Warden about, the Riot Shields' bash grace
            TickHoldingCells(dt);   // HoldingCell.cs: the Warden's net and cells
            TickPenumbral(dt);      // Penumbral.cs: Dusk Lobbers' pools drain
            TickReaperFaction(dt);  // ReaperFaction.cs: the Bound Wraiths' tethers, the Scythe Wraiths' reaps
            TickWaves(dt);
            TickPaths(dt);    // Paths.cs: hazard floors
            if (!SurvFrozen)  // (Survival holds the whole world while someone picks a card: no moves, no contact hits: SurvivalGame.cs)
            {
                TickEnemies(dt * WorldRate);  // (slowed for a moment by a perfect parry: Parry.cs)
                if (!ArenaHeld)  // (the fight paused round a Holding Cell: HoldingCell.cs)
                    TickRecycling(dt * WorldRate);  // stragglers far from everyone come back in near the party (Recycling.cs)
            }
            KeepOutOfDomes();  // Fortress's dome (Dome.cs)
            KeepOutOfAegis(dt);  // Aeroaegis's vortex (Aegis.cs)
            TickLivingBolt(dt);  // Living Bolt: its clock, the discharge and the residue's damage (LivingBolt.cs)
            TickRavager(dt);  // Blood Ravager: its clock, the legs' walk and stomps, the ram and the lashes (BloodRavager.cs)
            TickBoil(dt);  // Blood Boil: its clock and the drain (BloodBoil.cs)
            TickForbiddenPath(dt);  // Forbidden Path: the beast's clock, drain, lock-on and combo stages (ForbiddenPath.cs)
            TickAstral(dt);  // Astral Projection: the spirit's clock, its host, lean and heal (AstralProjection.cs)
            TickRodentMinigun(dt);  // Rodent Minigun: the portal's clock, its turn after the aim and the rodents it spits (RodentMinigun.cs)
            TickHeartbeat(dt);  // Heartbeat: its clock, the beats' health and the waves' hits (Heartbeat.cs)
            TickStaticBubble(dt);  // Static Bubble: its clock, growth, enemies put out, shots eaten, the burst (StaticBubble.cs)
            TickVortex(dt);  // the Vortex ultimate: its clock, swirl, damage, swept shots and fling (Vortex.cs)
            KeepOutOfShell(dt);  // Blood Shell (BloodShell.cs)
            KeepOffBridges(dt);    // the Drawbridge (Drawbridge.cs)
            KeepOffBloodWalls(dt);  // Blood Wall (BloodWall.cs): pushes enemies back, bleeds those against it
            KeepOffCovers();  // Deployable Cover (DeployableCover.cs): enemies can't walk through its plates
            KeepOffGroveFences();  // Bamboo Grove's fence (BambooGrove.cs): enemies stay outside it
            KeepOffOssuaryWalls(dt);  // the Bone Guard's Ossuary Wall (BoneGuard.cs): pushes enemies back, darts those against it
            KeepOffPhalanx();    // the Bone Phalanx's shields (BonePhalanx.cs)
            TickMinions(dt);
            TickTurrets(dt);  // Turret.cs: the Black Market's turrets shoot
            TickRoomTraps(dt);  // RoomTraps.cs: a room's spike plates and turrets
            TickRelicGauntlets(dt);  // Gauntlet.cs: the Relic Gauntlet's lasers
            TickSearchlights(dt);  // Stun.cs
            TickOrbs(dt);
            TickCoins(dt);  // Coins.cs
            TickGadgets(dt);  // Gadgets.cs
            TickDevSpawners(dt);  // DevSpawners.cs: the F1 dock's enemy spawners
            TickBossBarrels(dt);  // Props.cs: health barrels dropping into boss fights
            TickProps(dt);    // Props.cs: fuses, smashed health barrels, opened chests, breaks
            MoveBullets(dt * WorldRate);
            Collide();
            FirePendingClaps();  // Squall.cs: Grounders' Thunderclaps charged by this tick's hits
            SendBounces();    // Relics.cs: shots that bounced off a wall this tick
            TickRods(dt);     // LightningRod.cs: the charge running down, and the bolts this tick's hits called
            TickHelis(dt);    // AttackHelicopter.cs: the gunship's targets and rounds
            TickDogmas(dt);   // DragonsDogma.cs: the dragon's flight and bites
            EnforcePens();    // Chalk.cs: enemies penned in by an ink loop stay inside it
            CheckGameOver();

            if (pendingSpawns.Count > 0)
            {
                var w = Proto.Begin(Msg.Spawn);
                w.Write(pendingSpawns.Count);
                foreach (var b in pendingSpawns)
                {
                    w.Write(b.Id);
                    w.Write(b.Kind);
                    w.Write(b.Owner);
                    w.Write(b.Pos);
                    w.Write(b.Vel);
                    w.Write(b.AngVel);
                    w.Write(b.Life);
                    w.Write((byte)((b.FromBoss ? 1 : 0) | (b.Homing > 0f ? 2 : 0) | ((int)b.Look << 2)));
                    if (b.Homing > 0f)
                        w.Write(b.Homing);
                }
                session.Broadcast(Proto.End(w));
                pendingSpawns.Clear();
            }
            if (pendingRemoved.Count > 0)
            {
                var w = Proto.Begin(Msg.Remove);
                w.Write(pendingRemoved.Count);
                foreach (int id in pendingRemoved)
                    w.Write(id);
                session.Broadcast(Proto.End(w));
                pendingRemoved.Clear();
            }
            tick++;
            if (tick % SnapshotEvery == 0)
            {
                SendSnapshot();
                SendSteer();
            }
            if (tick % 120 == 0 && Log != null)  // (no formatting when nothing reads it)
                DoLog(string.Format("wave {0} state {1} enemies {2} bullets {3} score {4} hp {5}", Wave, State, Enemies.Count, Bullets.Count, Score, HpSummary()));
        }

        void TickPlayers(float dt)
        {
            foreach (var p in Players.Values)
            {
                TrackMotion(p, dt);  // Variety.cs
                TickSlots(p, dt);
                TickGraze(p, dt);  // Graze.cs
                p.BarrierT = Math.Max(0f, p.BarrierT - dt);
                p.InvulnT = Math.Max(0f, p.InvulnT - dt);
                TickRiot(p, dt);
                p.OverdriveT = Math.Max(0f, p.OverdriveT - dt);
                p.AdrenalineT = Math.Max(0f, p.AdrenalineT - dt);
                p.OverchargeT = Math.Max(0f, p.OverchargeT - dt);  // StaticShock.cs
                p.CrSurgeT = Math.Max(0f, p.CrSurgeT - dt);  // a Chakra Surge (ChakraReserves.cs)
                p.RushT = Math.Max(0f, p.RushT - dt);  // Static Rush (StaticBubble.cs)
                p.SupplyT = Math.Max(0f, p.SupplyT - dt);  // SupplyDrop.cs
                TickGustTimers(p, dt);  // GustGate.cs
                p.SurgeT = Math.Max(0f, p.SurgeT - dt);  // Arcane Surge
                p.GhostT = Math.Max(0f, p.GhostT - dt);  // Ghost Step
                TickMomentum(p, dt);  // Momentum.cs
                TickIronhide(p, dt);  // Ironhide.cs
                p.SwiftT = Math.Max(0f, p.SwiftT - dt);
                p.DashT = Math.Max(0f, p.DashT - dt);
                p.TauntT = Math.Max(0f, p.TauntT - dt);
                p.AegisT = Math.Max(0f, p.AegisT - dt);
                p.FrenzyT = Math.Max(0f, p.FrenzyT - dt);
                p.LegionT = Math.Max(0f, p.LegionT - dt);
                p.ScentT = Math.Max(0f, p.ScentT - dt);
                p.BoltCd -= dt;  // Ricochet's bounce bolts (Combat.cs)
                p.BroodCd -= dt;  // Brood Mother's boss turrets
                for (int i = 0; i < p.BroodDue; i++)
                    SpawnMinion(MinionKind.Turret, p, ClampToArena(p.Pos + V2.FromAngle(p.Aim + MathX.Pi + (i - (p.BroodDue - 1) / 2f) * 0.8f) * 40f, 13f), BroodTurretTime);
                p.BroodDue = 0;
                p.UltT = p.Downed ? 0f : Math.Max(0f, p.UltT - dt);
                p.DomeT = Math.Max(0f, p.DomeT - dt);  // Fortress's dome stands on even if its owner goes down (Dome.cs)
                TickParry(p, dt);
                TickBloodShell(p, dt);  // BloodShell.cs
                p.ShieldT = Math.Max(0f, p.ShieldT - dt);
                p.WardT = Math.Max(0f, p.WardT - dt);
                if (p.ShieldT <= 0f)
                    p.ShieldHp = 0f;
                p.LifestealPool = Math.Min(10f, p.LifestealPool + 10f * dt);
                if (p.Downed)
                    p.TauntT = 0f;
                p.FireCd -= dt;
                if (p.Weapon.Id != WeaponId.TwinRifleAuto)
                    p.TwinLock = 0;  // (TwinAuto.cs)
                if (p.Weapon.Id != WeaponId.OutOfAmmo)
                    p.AmmoMove = p.KnifeMove = 0;  // (OutOfAmmo.cs)
                if (p.Weapon.Id != WeaponId.OutOfAmmo)
                    p.AmmoSpin = 0f;
                if ((p.Weapon.Id != WeaponId.HoneybeeStaff || p.Downed) && p.HoneyOn)
                    ClearHoneybee(p);  // (HoneybeeStaff.cs)
                if (p.Weapon.Id == WeaponId.HoneybeeStaff && !p.Downed)
                    TickHoneybee(p, dt);  // the honey spray and its meter; Fire is the usual trigger below (HoneybeeStaff.cs)
                if ((p.Weapon.Id != WeaponId.WyvernStaff || p.Downed) && p.WyvTick != 0f)
                    ClearWyvernStaff(p);  // (WyvernStaff.cs)
                if ((p.Weapon.Id != WeaponId.Stonespear || p.Downed) && (p.SsMask != 0 || p.SsWindT > 0f || p.SsQueue.Count > 0))
                    ClearStonespear(p);  // (Stonespear.cs)
                if ((p.Weapon.Id != WeaponId.EmbolusSpears || p.Downed) && (p.EsSt[0] != EsInHand || p.EsSt[1] != EsInHand))
                    ClearEmbolus(p);  // (EmbolusSpears.cs)
                if ((p.Weapon.Id != WeaponId.NeedleSwarm || p.Downed) && NsAny(p))
                    ClearNeedleSwarm(p);  // (NeedleSwarm.cs)
                if ((p.Weapon.Id != WeaponId.BloodBlast || p.Downed) && p.BlastStep != 0)
                    ClearBloodBlast(p);  // (BloodBlast.cs)
                if ((p.Weapon.Id != WeaponId.WindClash || p.Downed) && (p.WcStep != 0 || p.WcTempestT > 0f))
                    ClearWindClash(p);  // (WindClash.cs)
                if ((p.Weapon.Id != WeaponId.FrostLance || p.Downed) && (p.FlStep != 0 || p.FlZeroT > 0f || p.FlRime))
                    ClearFrostLance(p);  // (FrostLance.cs)
                TickArcLock(p, dt);  // Arc Lightning's tags, chain and Forked Volley (ArcLock.cs)
                TickSpinUp(p, dt);  // the Burst SMG (Weapons.cs)
                TickRapier(p, dt);  // the Rapier's Tempo and Flourish (Rapier.cs)
                TickNunchaku(p, dt);  // the Nunchaku's guard: its discs swat shots back (Nunchaku.cs)
                TickNodachi(p, dt);  // the Nodachi's step clock and the Guard's cone (Nodachi.cs)
                TickTwinPistols(p, dt);  // Laser Sights running down, its key cooling down (TwinPistols.cs)
                TickTessen(p, dt);  // the Twin Tessen's gust charge and cooldown, Tailwind (TwinTessen.cs)
                TickHeartFlail(p, dt);  // the Heart Flail's swing, spin, thrown heart and beat (HeartFlail.cs)
                p.DumpCd = Math.Max(0f, p.DumpCd - dt);  // the Railgun's Heat Dump (RailBeam.cs)
                if (p.Weapon.Id != WeaponId.StaticShock && p.StaticOn)
                    ClearStaticShock(p);  // (StaticShock.cs)
                TickHiveCall(p, dt);  // Hive Call's clocks (HiveCenser.cs)
                if (p.Weapon.Id != WeaponId.HiveCenser && p.HiveOn)
                    ClearHivePour(p);
                if (p.Weapon.Id == WeaponId.HiveCenser)
                    TickHivePour(p, dt);  // held, not fired: insects pour from the cursor (HiveCenser.cs)
                if (p.Weapon.Id == WeaponId.StaticShock)
                {
                    string ssSrc = CreditTo("weapon");
                    TickStaticShock(p, dt);  // held, not fired (StaticShock.cs); it stops when you're down
                    dmgSrc = ssSrc;
                }
                if (p.Weapon.Id == WeaponId.RadiantOrb)
                {
                    string orbSrc = CreditTo("weapon");
                    TickRadiantOrb(p, dt);  // held, not fired (RadiantOrb.cs); it lets go when you're down
                    dmgSrc = orbSrc;
                }
                if (p.Downed || p.SwallowT > 0f || p.WarpT > 0f)
                    continue;  // (nothing to shoot from inside the Devourer, or mid beam-in)
                if (Seated(p))
                {
                    // sitting in a Deployable Turret or riding a Ballista: it fires (TickDeployTurret, TickBallista), not your weapon
                }
                else if (Riding(p))
                    TickHorseBow(p, dt);  // in a Trojan Horse: arrows out of its slits, not your weapon (TrojanHorse.cs)
                else if (TankOn(p))
                    TickTankGun(p, dt);
                else if (RocketOn(p))
                    TickRocketGun(p, dt);  // Rocket Drop: the launcher, not your weapon (RocketDrop.cs)
                else if (ArmOn(p))
                {
                    // curled up in Iron Armadillo: the weapon is away, Fire revs the roll (IronArmadillo.cs)
                }  // in A Literal Tank: its main gun, not your weapon (LiteralTank.cs)
                else if (BoltOn(p))
                {
                    // electrified: the weapon is put away, Fire is the dash (sent by the player's own game: LivingBolt.cs)
                }
                else if (RavagerOn(p))
                {
                    // the blood legs are out: the weapon is put away, Fire lashes (sent by the player's own game: BloodRavager.cs)
                }
                else if (ApOn(p))
                    AstralTrigger(p);  // Astral Projection: the weapon is put away, Fire shoots from the spirit (AstralProjection.cs)
                else if (FpOn(p))
                    FpTrigger(p);  // Forbidden Path: the weapon is put away, Fire runs the combo (ForbiddenPath.cs)
                else if (RodentOn(p))
                {
                    // the Rodent Minigun's portal is open: the weapon is put away (RodentMinigun.cs)
                }
                else if (ColOn(p))
                    ColossusTrigger(p);  // the golem: Fire slams, the weapon is put away (StoneColossus.cs)
                else if (GolOn(p))
                    GolemTrigger(p);  // Blood Golem: Fire punches, the weapon is put away (BloodGolem.cs)
                else if (SpOn(p))
                    SpiritTrigger(p);  // Spirit Avatar: Fire is the Moon Cleave, the weapon is put away (SpiritAvatar.cs)
                else if (p.StunShots > 0)
                    TickStunGun(p);  // the Stun Gun is out instead of your weapon (Stun.cs)
                else if (p.Weapon.Id == WeaponId.Revolver)
                    RevolverTrigger(p, dt);  // a single-action gun, or the Fan stance's whip and fan (Weapons.cs, PistolWhip.cs)
                else if (p.Weapon.Id == WeaponId.DoubleBarrel)
                    TickDoubleBarrel(p, dt);
                else if (p.Weapon.Id == WeaponId.HeartFlail)
                    HfTrigger(p, dt);  // a click swings, held spins; never a volley (HeartFlail.cs)
                else if (p.Weapon.Id == WeaponId.GrenadeLauncher)
                    TickGrenadeLauncher(p, dt);  // the drums, the reload, the smoke (GrenadeLauncher.cs)  // two shells, a break-open reload (DoubleBarrel.cs)
                else if (p.Weapon.Id == WeaponId.Stonespear)
                    TickStonespear(p, dt);  // the slam, the hovering spears, the volley (Stonespear.cs)
                else if (p.Weapon.Id == WeaponId.WindClash)
                    TickWindClash(p, dt);  // (WindClash.cs)
                else if (p.Weapon.Id == WeaponId.WyvernStaff)
                    TickWyvernStaff(p, dt);  // the gale, the Cinder meter, the breath; Fire is held, never a volley (WyvernStaff.cs)
                else if (p.Weapon.Id == WeaponId.FrostLance)
                    TickFrostLance(p, dt);  // the five-step chain (FrostLance.cs)
                else if (p.Weapon.Id == WeaponId.NeedleSwarm)
                    TickNeedleSwarm(p, dt);  // the siphon, the tendrils and the wrapped enemies; Fire is held, never a volley (NeedleSwarm.cs)
                else if (p.Weapon.Id == WeaponId.EmbolusSpears)
                    TickEmbolus(p, dt);  // the spears thrown, stuck, recalled and hardening; Fire throws or recalls (EmbolusSpears.cs)  // the four-step combo of air waves (WindClash.cs)
                else if (ChargeWeapon(p.Weapon.Id))
                    TickBow(p);  // the bow and the Warhammer fire when you let go
                else if (p.Weapon.Id == WeaponId.Katana || p.Weapon.Id == WeaponId.BoStaff || p.Weapon.Id == WeaponId.Nodachi)
                    TickKatanaFire(p);  // the Katana's dash or swipe, or the Bo Staff's slash, jab, spin, with the next one queued if you tap ahead (Katana.cs)
                else if (p.Weapon.Id == WeaponId.OutOfAmmo)
                    TickOutOfAmmo(p, dt);  // the swats, the knife chain and Alt's spin, no trigger (OutOfAmmo.cs)
                else if (IsBotWeapon(p.Weapon.Id))
                    TickBotWeapon(p, dt);  // summon the robot, send it, the basic attack (EngineerBots.cs)
                else if (p.Weapon.Id == WeaponId.BumbleRemote)
                    TickBumbleRemote(p, dt);  // you never shoot: the Bumble Bot reads Fire (BumbleRemote.cs)
                else if (p.Weapon.Id == WeaponId.TwinRifleAuto)
                    TickTwinAuto(p, dt);  // the guns aim and fire on their own, no trigger (TwinAuto.cs)
                else if (p.Weapon.Id == WeaponId.ArcKnuckles)
                    TickKnuckleFire(p, dt);  // a click is a move, held repeats, the Quake Punch charges while held (ArcKnucklesBrawl.cs)
                else if (p.Weapon.Id != WeaponId.RadiantOrb && p.Weapon.Id != WeaponId.StaticShock && p.Weapon.Id != WeaponId.HiveCenser && !InGreatStar(p) && p.Firing && !p.ParryActive && !p.RapierGuard && !p.HbSet && !p.RepelPull && !p.KusariWhirl && p.FireCd <= 0f && State != MatchState.GameOver && ImplodeReady(p))
                {
                    p.FireCd = p.Weapon.FireInterval / FireRate(p);
                    FirePlayer(p);
                }
                // Riot Repeater: the rest of the burst.
                if (p.EchoLeft > 0)
                {
                    if (p.ParryActive)
                        p.EchoLeft = 0;
                    for (p.EchoT -= dt; p.EchoLeft > 0 && p.EchoT <= 0f; p.EchoT += p.EchoGap)
                    {
                        p.EchoLeft--;
                        FireFanned(p, p.Aim, 0);
                    }
                }
                if (!OnPathOf(PathId.BloodMoon) && FloorAt(p.Pos) != PathId.BloodMoon && !Has(RunMod.Bloodlust))  // Blood Moon (Paths.cs) and Bloodlust: no regeneration
                {
                    p.Hp = Math.Max(p.Hp, Math.Min(p.HealCap, p.Hp + (PassiveRegen + p.Stats.Regen) * dt));
                }
                if (p.Role == Role.Healer)
                {
                    // The aura heals you too: 1 HP/s, +1 per Sanctuary (its heal multiplier is 1 + stacks).
                    if (!Has(RunMod.Bloodlust))
                        Heal(p, AuraSelfHeal * p.Stats.AuraHealMult * dt, p, AuraSource);
                    foreach (var q in Players.Values)
                        if (q != p && q.Pos.CloserThan(p.Pos, p.AuraRange))
                            Heal(q, AuraHealAt(p, q.Pos) * dt, p, AuraSource);
                }
            }

            TickZones(dt);

            // Standing next to a downed ally slowly brings them back.
            foreach (var p in Players.Values)
            {
                TickReviveToken(p, dt);  // Shop.cs
                if (!p.Downed)
                    continue;
                Player helper = null;
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed && q.Pos.CloserThan(p.Pos, ReviveRange))
                        helper = q;
                if (helper != null)
                {
                    p.Revive += dt / D(DiffKnob.ReviveTime);
                    p.ReviveBy = helper.Id;
                }
                else if (p.ReviveBy == 0)
                    p.Revive = Math.Max(0f, p.Revive - dt * 0.25f);
                // A zone with a revive boost (ExtraRevive: the Censer's cloud, a ward) can finish the revive on its own (it sets ReviveBy to its owner).
                if (p.Revive >= 1f)
                    RevivePlayer(p, 0.35f, PlayerOrNull(p.ReviveBy));
                p.ReviveBy = 0;
            }
        }

        // --- Helpers -----------------------------------------------------------

        /// HP/s a Healer's aura gives an ally standing at `at`: AuraHealNear next to them, AuraHealRim at the rim,
        /// 0 outside (x the Sanctuary multiplier).
        public static float AuraHealAt(Player healer, V2 at)
        {
            float r = healer.AuraRange, d = (at - healer.Pos).Length;
            if (d >= r)
                return 0f;
            return (AuraHealNear + (AuraHealRim - AuraHealNear) * d / r) * healer.Stats.AuraHealMult;
        }

        public bool IsSpawning(Enemy e) => IsHost ? e.SpawnT > 0f : e.Spawning;

        void AddFx(FxKind kind, V2 pos, Rgba color, float radius, float dur)
        {
            Effects.Add(new Effect { Kind = kind, Pos = pos, Color = color, Radius = radius, Dur = dur });
        }

        public string HpSummary()
        {
            var parts = new List<string>();
            foreach (var p in Players.Values)
                parts.Add(p.Role + ":" + (int)p.Hp + (p.Downed ? "(down)" : "") + "/L" + p.Level);
            return string.Join(" ", parts);
        }

        void DoLog(string msg)
        {
            if (Log != null)
                Log("[" + (IsHost ? "host" : "client") + "] " + msg);
        }
    }
}
