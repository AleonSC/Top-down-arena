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

        const int SnapshotEvery = 3;  // ticks
        float shoveT;
        V2 shoveDir;

        readonly Session session;
        readonly Random rng = NewRng();

        // Tests (Tools/SimTest) can make every game's random rolls repeatable: SeedForTests gives each game
        // made after it a seed drawn from one sequence. The real game never calls it, so it stays random.
        static Random testSeeds;

        public static void SeedForTests(int seed) => testSeeds = new Random(seed);

        static Random NewRng() => testSeeds != null ? new Random(testSeeds.Next()) : new Random();

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
        V2 iceVel;        // your sliding velocity on the Frozen Lake path (Paths.cs)
        public bool HoldParry;  // keep the parry up (testing)

        /// Round trip to the host in milliseconds (0 when you're the host), measured four times a second (Game.PingEvery).
        public float PingMs;
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float pingT;

        /// For the aim debug overlay: the direction of your most recent shot, as the host fired it.
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
            float off = Math.Abs(MathX.Fmod(b.Vel.Angle - me.Aim + MathX.Pi, MathX.Tau) - MathX.Pi);
            float prevOff = Math.Abs(MathX.Fmod(LastShotAngle - me.Aim + MathX.Pi, MathX.Tau) - MathX.Pi);
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

        /// Dev: when set (on the host), each match is recorded to a new file in this folder.
        public string RecordFolder;
        public SessionRecorder Recorder;
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
                Players.TryGetValue(Replaying ? ViewId : MyId, out p);
                return p;
            }
        }

        // Replays (Replay.cs): a replay watches the recorded match as a spectator (nobody is "you"), seeing the player ViewId's screen.
        public bool Replaying;
        public int ViewId;
        public ReplayRecorder Replay;  // recording this match (HostStart sets it when ReplayFolder is)
        public string ReplayFolder;    // dev: where to save replays of the matches this game hosts (null = don't record)

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // --- Lifecycle ---------------------------------------------------------

        public void HostStart()
        {
            var w = Proto.Begin(Msg.Start);
            w.Write(session.StartWave);
            // Quick Play (QuickPlay.cs): its boss order, drawn here so every machine plays the same bosses.
            bool quick = session.QuickMode && !session.PracticeMode && session.LobbyHunt == null && ResumeFrom == null;
            w.Write(quick);
            if (quick)
            {
                var order = QuickPlay.MakeOrder(new Random(), Profile == null || Profile.QuickPlays == 0);
                w.Write((byte)order.Length);
                foreach (var b in order)
                    w.Write((byte)b);
            }
            w.Write((byte)session.Lobby.Count);
            foreach (var e in session.Lobby.Values)
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
            }
            w.Write(ResumeFrom != null);  // continuing a saved run (RunSaves.cs)
            w.Write(ResumeFrom != null ? ResumeFrom.LongRun : LongRunDefault && !quick);  // a 100-wave Campaign (Hunts.cs); a saved run keeps its length
            var msg = Proto.End(w);
            session.Broadcast(msg);
            var r = new BinaryReader(new MemoryStream(msg));
            r.ReadByte();
            ReadStart(r);
            if (ReplayFolder != null && !ShowcaseMode && !Unrecorded && !Replaying)
            {
                Replay = new ReplayRecorder(this, session, ReplayFolder);
                Replay.Capture(msg);  // (the broadcast went out before there was a recorder)
                Replay.SetParty();
            }
            if (ResumeFrom != null)
                ApplyResume(ResumeFrom);
            ResumeFrom = null;
            ResumeSeats.Clear();
        }

        void ReadStart(BinaryReader r)
        {
            Stop();
            PracticeIndex = -1;
            ResetPaths();
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
                QuickOrder = order.Length > 0 ? order : new[] { EnemyType.Boss };
            }
            int n = r.ReadByte();
            var entries = new List<Player>();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                string name = r.ReadString();
                var role = (Role)r.ReadByte();
                var kit = (Kit)r.ReadByte();
                int weapon = r.ReadByte();
                int ability = r.ReadByte();
                int wild = r.ReadByte();  // a Wildcard's second ability and its key (Defs.WildSecond / WildKey)
                int ult = r.ReadByte();   // the Ultimate row's key (LobbyEntry.Ult)
                var perkBytes = new List<byte>();
                for (int pn = r.ReadByte(); pn > 0; pn--)
                    perkBytes.Add(r.ReadByte());
                var def = Defs.Get(role);
                var p = new Player { Id = id, Role = role, Def = def, Name = name, Hp = def.Hp, MaxHp = def.Hp, Aim = -MathX.Pi / 2f, ScatShells = ScatShellMax };
                p.Kit = kit;
                p.Weapon = Defs.Weapon(role, kit, weapon < Defs.WeaponLimit(role, kit) ? weapon : 0);
                p.RoleAbility = Upgrades.RoleAbility(role, kit, ability);
                p.UltPick = (byte)Upgrades.ClampUlt(role, kit, ult);
                if (role == Role.Wildcard)
                {
                    p.WildKit = Upgrades.UltKitOf(role, kit, p.UltPick);  // the class its Ultimate pick is from
                    p.StartBonus = Upgrades.RoleAbility(role, kit, Defs.WildSecond(wild));
                    p.BonusKeys[Defs.WildKey(wild) - 1] = p.StartBonus;
                }
                p.BaseHp = def.Hp * Defs.KitHpMult(kit);
                p.MaxHp = p.Hp = p.BaseHp;
                for (int k = 0; k < p.HpHistory.Length; k++)
                    p.HpHistory[k] = p.Hp;
                p.Pos = new V2(Width / 2f + (i - (n - 1) / 2f) * 90f, Height / 2f + 140f);
                p.Disp = p.Pos;
                Recalc(p);
                GivePerks(p, Perks.Sanitize(perkBytes));
                if (Has(RunMod.Lucky))
                    p.Rerolls += Modifiers.LuckyRerolls;
                entries.Add(p);
                if (Has(RunMod.HeadStart) && Practice == null && !(IsHost && ResumeFrom != null))
                {
                    // Head Start: the picks of the first levels, and a legendary from the first boss's pool.
                    p.Level = Modifiers.HeadStartLevel;
                    if (IsHost)
                    {
                        p.PendingPicks += Modifiers.HeadStartLevel - 1;
                        var first = BossAt(Bosses.FirstBossWave);
                        p.LegendaryQueue.Add(IsReaper(first) ? EnemyType.TwinA : first);
                        NextOffer(p);
                    }
                }
                Players[id] = p;
                lastDowned[id] = false;
                lastLevel[id] = p.Level;
            }
            bool resumed = r.ReadBoolean();
            LongRun = r.ReadBoolean();  // a 100-wave Campaign (Hunts.cs)
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
            retrying = false;  // (Stop above has let the retried run go without recording it: Checkpoints.cs)
            LivesUsed = 0;  // (a continued run gets its own back: ApplyResume)
            RetryReady = false;
            Leg = Wave > 0 ? 1 : 0;  // starting after a wave: it's done, both legs (a saved run says where it was: ApplyResume)
            State = MatchState.Intermission;
            lastState = State;
            StateTimer = StartDelay;
            Running = true;
            lastMyHp = Me != null ? Me.Hp : 0f;
            session.Screen = AppScreen.Game;
            if (Recorder != null && !Recorder.Ended)
                Recorder.End(this, "restarted");  // a new match started over one still going: finish its file first
            Recorder = null;
            if (IsHost && RecordFolder != null)
            {
                Recorder = new SessionRecorder(RecordFolder);
                Recorder.Begin(this, startWave);
            }
            BeginRunRecord(resumed);  // RunSaves.cs
            DoLog("game started with " + Players.Count + " player(s)");
        }

        /// Ends the match; `how` is what its recording says happened ("left", or "quit" when the game is closed).
        public void Stop(string how = "left")
        {
            if (Replay != null)
            {
                Replay.Finish();
                Replay = null;
            }
            if (Running)
                LogQuitRun();  // (not while a retry is on offer or starting: the run isn't over)
            SettleRetry(how);  // a retry left untaken ends the run for good (Checkpoints.cs)  // RunSaves.cs (the run you walk away from still shows in Recent runs)
            if (Running)
                SaveRun();  // between waves: keep the run to continue later (RunSaves.cs)
            if (TestRunning)
                TestRunEnded("left the match");
            if (Recorder != null && Running)
                Recorder.End(this, how);
            Running = false;
            Test = null;
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
            Zones.Clear();
            Effects.Clear();
            Sounds.Clear();
            ResetMeleeSounds();  // MeleeSounds.cs
            spawnQueue.Clear();
            squadQueue.Clear();
            DevFreeze = false;  // (DevTools.cs)
            ClearRisers();
            designQueue.Clear();
            pendingSpawns.Clear();
            pendingRemoved.Clear();
            lastDowned.Clear();
            lastLevel.Clear();
            nextBulletId = 1;
            nextEnemyId = 1;
            nextMinionId = 1;
            nextOrbId = 1;
            nextCoinId = 1;
            nextZoneId = 1;
            ResetBossState();
            ResetVariety(true);
            DashT = 0f;
            Paused = false;
            PausedBy = 0;
            Endless = false;
            RunTime = 0f;
            RunStatsReady = false;
            Hazards.Clear();
            Cages.Clear();
            ResetProps();  // Props.cs
            ResetLayout();  // LayoutRules.cs
            awaitingPick = false;
            rerollsAtRequest = -1;
            tick = 0;
        }

        public void OnPeerLeft(int id)
        {
            Players.Remove(id);
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
            ApplyStatOverrides(p);
            p.VectorCap = VectorCapBase + p.Stats.VectorCap;  // the Expanding arena's vector cap (ExpansionGame.cs)
            float oldMax = p.MaxHp;
            p.MaxHp = p.BaseHp + p.Stats.BonusHp;
            if (p.MaxHp > oldMax && !p.Downed)
                p.Hp += p.MaxHp - oldMax;

            var ids = new AbilityId[Upgrades.SlotCount];
            ids[0] = p.RoleAbility;
            for (int i = 1; i < ids.Length; i++)
                ids[i] = AbilityId.None;
            ids[Upgrades.UltSlot] = Upgrades.Ultimate(p.Role, p.UltKit, p.UltPick);  // the lobby's Ultimate row pick (0: the default one)
            // Bonus abilities go on the keys their owner chose; any others take the first free key.
            var granted = new List<AbilityId>();
            if (p.StartBonus != AbilityId.None)
                granted.Add(p.StartBonus);  // a Wildcard's second ability counts as granted from the start
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Grants != AbilityId.None && granted.Count < Upgrades.BonusSlots && !granted.Contains(Upgrades.All[t].Grants))
                    granted.Add(Upgrades.All[t].Grants);
            for (int k = 0; k < Upgrades.BonusSlots; k++)
                if (granted.Contains(p.BonusKeys[k]) && Array.IndexOf(ids, p.BonusKeys[k]) < 0)
                    ids[1 + k] = p.BonusKeys[k];
            foreach (var g in granted)
                if (Array.IndexOf(ids, g) < 0)
                    for (int k = 1; k <= Upgrades.BonusSlots; k++)
                        if (ids[k] == AbilityId.None)
                        {
                            ids[k] = g;
                            break;
                        }
            for (int k = 0; k < Upgrades.BonusSlots; k++)
                p.BonusKeys[k] = ids[1 + k];

            // Charges and cooldowns follow an ability when it moves to another key.
            var moved = new Dictionary<AbilityId, AbilitySlot>();
            for (int i = 1; i <= Upgrades.BonusSlots; i++)
                if (p.Slots[i].Id != AbilityId.None)
                    moved[p.Slots[i].Id] = new AbilitySlot { Id = p.Slots[i].Id, Charges = p.Slots[i].Charges, MaxCharges = p.Slots[i].MaxCharges, Timer = p.Slots[i].Timer };

            for (int i = 0; i < ids.Length; i++)
            {
                var slot = p.Slots[i];
                var def = ids[i] == AbilityId.None ? null : Upgrades.Ability(ids[i]);
                int max = def == null ? 0 : i == Upgrades.UltSlot ? 1 : 1 + (i == 0 ? p.Stats.RoleCharges : p.Stats.ExtraCharges)
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
                    if (i == Upgrades.UltSlot)
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
            float cd = Upgrades.Ability(id).Cooldown * p.Stats.CdMult * MasteryCooldown(p, id);
            return cd;
        }

        void TickSlots(Player p, float dt)
        {
            for (int i = 0; i < p.Slots.Length; i++)
            {
                var s = p.Slots[i];
                if (s.Id == AbilityId.None || s.Charges >= s.MaxCharges || StunHeld(p, s) || s.Id == AbilityId.BloodShell && p.ShellOn || s.Id == AbilityId.BloodBoil && p.SprayOn || s.Id == AbilityId.CrimsonPact && p.BloodWallOn || s.Id == AbilityId.LastStand && p.StandOn || s.Id == AbilityId.BattleFury && p.SteelT > 0f)
                    continue;  // (the Stun Gun's cooldown waits until its shots are spent)
                s.Timer -= dt;
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
            if (ShowcaseMode)
                return;  // no levelling up in the loadout preview
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

        /// Called by the front end when the local player rerolls their cards.
        public void RerollOffer()
        {
            var me = Me;
            if (me == null || me.Offer == null || me.OfferLegendary || me.OfferMod || me.OfferMilestone || me.OfferPrize || me.Rerolls <= 0 || RerollPending)
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
            if (!Players.TryGetValue(id, out p) || p.Offer == null || p.OfferLegendary || p.OfferMod || p.OfferMilestone || p.OfferPrize || p.Rerolls <= 0 || State != MatchState.Intermission)
                return;
            p.Rerolls--;
            Recorder?.Rerolled(p);
            p.Offer = Upgrades.RollOffer(rng, p.Role, p.RoleAbility, p.Taken, p.Level, p.Weapon.Id, ExpandingOn);
            DoLog(p.Role + " rerolled (" + p.Rerolls + " left)");
            SendSnapshot();
        }

        public void OnPick(int peer, BinaryReader r)
        {
            int index = r.ReadByte();
            HostPick(peer, index, r.ReadByte());
        }

        /// key: for a card that grants a new ability, the key to put it on (1 = Q, 2 = E, 3 = Shift; 0 = first free).
        void HostPick(int id, int index, int key = 0)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Offer == null || index < 0 || index >= p.Offer.Length || State != MatchState.Intermission)
                return;
            int upgrade = p.Offer[index];
            Recorder?.Picked(this, p, p.Offer, upgrade, p.OfferLegendary ? "legendary" : p.OfferMod ? "mod" : p.OfferMilestone ? "milestone" : p.OfferPrize ? "prize" : "level");
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
            PlaceOnKey(p.BonusKeys, Upgrades.All[upgrade].Grants, key);
            GrantUpgrade(id, upgrade);
            p.Rerolls += Upgrades.All[upgrade].RerollsNow;  // Reroll Cache
            NextOffer(p);
            DoLog(p.Role + " picked " + Upgrades.All[upgrade].Name);
            SendSnapshot();  // show the next offer right away
        }

        /// Boss rewards come first (legendaries, then weapon mods), then regular level-up picks.
        void NextOffer(Player p)
        {
            p.PendingLegendary = p.LegendaryQueue.Count;
            p.OfferLegendary = p.LegendaryQueue.Count > 0;
            p.OfferMod = false;
            p.OfferMilestone = false;
            p.OfferPrize = false;
            int[] mods = null;
            if (!p.OfferLegendary && p.PendingMods > 0)
            {
                mods = Upgrades.ModOffer(p.Role, p.RoleAbility, p.Taken);
                if (mods == null)
                    p.PendingMods = 0;  // every mod maxed out
            }
            if (p.OfferLegendary)
                p.Offer = Upgrades.LegendaryOffer(rng, p.LegendaryQueue[0], p.Role, p.RoleAbility, p.Taken);
            else if (mods != null)
            {
                p.Offer = mods;
                p.OfferMod = true;
            }
            else if (p.PrizePicks > 0)
            {
                p.Offer = Upgrades.PrizeOffer(rng, p.Role, p.RoleAbility, p.Taken, p.Weapon.Id, ExpandingOn);  // a challenge room's prize (Challenges.cs)
                p.OfferPrize = true;
            }
            else if (p.PendingMilestones > 0 && (p.Offer = Upgrades.MilestoneOffer(rng, p.Role, p.RoleAbility, p.Taken, ExpandingOn)) != null)
                p.OfferMilestone = true;  // its own pool, before the level's normal pick
            else if (p.PendingPicks > 0)
            {
                p.PendingMilestones = 0;  // (only reached with the milestone pool used up)
                p.Offer = Upgrades.RollOffer(rng, p.Role, p.RoleAbility, p.Taken, p.Level, p.Weapon.Id, ExpandingOn);
            }
            else
                p.Offer = null;
        }

        /// Host only: give a player an upgrade and tell everyone.
        public void GrantUpgrade(int id, int upgrade)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
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
            w.Write((byte)p.Taken.Count);
            foreach (int t in p.Taken)
                w.Write((byte)t);
            foreach (var k in p.BonusKeys)
                w.Write((byte)k);
            session.Broadcast(Proto.End(w));
        }

        /// Put a bonus ability on a key (1 = Q, 2 = E, 3 = Shift). Whatever was there swaps to its old key.
        public static void PlaceOnKey(AbilityId[] k, AbilityId id, int key)
        {
            if (key < 1 || key > Upgrades.BonusSlots || id == AbilityId.None)
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

        /// The local player moves a bonus ability to another key (the Tab menu).
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
            if (!Players.TryGetValue(playerId, out p) || Array.IndexOf(p.BonusKeys, id) < 0)
                return;  // you can only move abilities you have
            PlaceOnKey(p.BonusKeys, id, key);
            Recalc(p);
            BroadcastUpgrades(p);
        }

        public bool AnyonePicking()
        {
            foreach (var p in Players.Values)
                if (p.PendingPicks > 0 || p.PendingLegendary > 0 || p.PendingMods > 0 || p.PendingMilestones > 0 || p.PrizePicks > 0)  // (a challenge room's prize too: the break used to run out under it)
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
            if (IsHost)
                Replay?.Tick(dt);
            if (BreachWarnOn)
                input = new PlayerInput { AimAt = input.AimAt };  // the breach warning: everyone stands still while the cameras tour (Breaches.cs)
            if (!Replaying)
                LocalControl(dt, input);  // (a replay has nobody at the controls)
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
            if (IsHost)
            {
                if (State == MatchState.Fighting || State == MatchState.Intermission)
                    RunTime += dt;
                HostTick(dt);
            }
            else
            {
                MoveBullets(dt * WorldRate);  // (a perfect parry slows every shot on every screen: Parry.cs)
                tick++;
                if (tick % 120 == 0)
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
            TickGates(dt);         // the shutter gates' clocks (Gates.cs)
            UpdatePropView(dt);    // crates, barrels, chests and their items (Props.cs)
            UpdateInkView(dt);     // Chalk.cs: the chalk's tip easing, a client's ink ageing
            UpdateWardenView(dt);  // Warden.cs
            UpdateSingView(dt);    // Singularity.cs
            UpdateHeraldView(dt);  // Herald.cs
            UpdateEclipseView(dt); // Eclipse.cs
            UpdateDevourView(dt);  // Devourer.cs
            if (IsHost)
                LetGoOfSwallowed();  // Devourer.cs
            UpdateDuelView(dt);    // Duelist.cs
            UpdateLichView(dt);    // Lich.cs
            UpdateColossusView(dt);  // Colossus.cs
            UpdateQueenView(dt);   // Queen.cs
            UpdateReaperView(dt);  // Reapers.cs
            StepLightDisplay(dt);  // Searchlights (Stun.cs)
            StepRadiantDisplay(dt);    // Radiant Orbs (RadiantOrb.cs)
            foreach (var p in Players.Values)
                p.StunT = Math.Max(0f, p.StunT - dt);
            foreach (var p in Players.Values)
                p.WarpT = Math.Max(0f, p.WarpT - dt);  // (BossBeam.cs)
            float k = 1f - (float)Math.Exp(-18.0 * dt);
            foreach (var p in Players.Values)
                // Snap instead of sliding across the screen when the player wraps around an edge.
                p.Disp = p.Id == MyId || Math.Abs(p.Disp.X - p.Pos.X) > Width / 2f || Math.Abs(p.Disp.Y - p.Pos.Y) > Height / 2f
                    ? p.Pos : V2.Lerp(p.Disp, p.Pos, k);
            foreach (var e in Enemies.Values)
            {
                e.Disp = e.Disp.DistanceTo(e.Pos) > 600f ? e.Pos : V2.Lerp(e.Disp, e.Pos, k);  // (a jump through a Rooms portal: snap, don't slide)
                if (IsSwordsman(e.Type) || e.Type == EnemyType.Warder || e.Type == EnemyType.SecurityGuard)  // (the Warder's and the Security Guard's charge look is timed the same way)
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
            UpdateMeleeSounds(dt);  // the melee rushers' cues, quieter with distance (MeleeSounds.cs)
            foreach (var o in Orbs.Values)
                o.Disp = IsHost ? o.Pos : V2.Lerp(o.Disp, o.Pos, k);
            foreach (var c in Coins.Values)
                c.Disp = IsHost ? c.Pos : V2.Lerp(c.Disp, c.Pos, k);
            foreach (var m in Minions.Values)
                m.Disp = IsHost ? m.Pos : V2.Lerp(m.Disp, m.Pos, k);
            BreachAge += dt;
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                Effects[i].T += dt;
                if (Effects[i].T >= Effects[i].Dur)
                    Effects.RemoveAt(i);
            }

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
            me.Parrying = (input.Parry || HoldParry) && !me.Downed;
            bool firing = input.Fire && !me.Downed && !me.ParryActive;  // can't shoot behind the shield
            bool[] held = { input.Ability, input.AbilityQ, input.AbilityE, input.AbilityShift, input.Ultimate };

            if (Bot && BotLevel != BotSkill.Off)
            {
                botT += dt;
                SmartBot(me, dt, ref move, ref aimAt, ref firing, held);  // BotBrain.cs
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
            if (Bot)
            {
                // Both bots: level-up picks (reroll once per pick while rerolls last, then pick at random) and the Bazaar.
                if (me.Offer != null && MathX.Fmod(botT, 0.5f) < dt)
                {
                    if (BotRerolls && me.Rerolls > 0 && botRerolledAt != me.Taken.Count)
                    {
                        botRerolledAt = me.Taken.Count;
                        RerollOffer();
                    }
                    else
                        PickUpgrade(rng.Next(me.Offer.Length));
                }
                else if (me.Offer == null && ShopOpen && !me.ShopDone && MathX.Fmod(botT, 0.5f) < dt)
                    BotShop(me);  // Shop.cs
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
            if (me.Weapon.Id == WeaponId.ArcKnuckles && (me.KnuckleChain == 2 || me.KnuckleChain == 4) && DashT <= 0f)
                move = V2.Zero;  // the Arc Knuckles' Flurry and Grab & Throw: rooted while they run (ArcKnucklesChain.cs)
            float bowRelease = LocalBow(me, firing, dt);
            if (me.Downed)
                DashT = 0f;
            else if (DashT > 0f)
            {
                DashT -= dt;
                me.Pos += DashDir * dashSpeed * dt;
                iceVel = DashDir * me.Speed * Paths.IceTopSpeed;  // on ice you slide out of a dash
            }
            else
            {
                V2 want = move * me.Speed * (me.ParryActive && !me.Stats.ParryNoSlow ? ParrySlow : 1f) * (bowDraw >= 0f ? BowDrawSlow : 1f) * HoneySlowAt(me.Pos);
                if (OnPathOf(PathId.FrozenLake) || FloorAt(me.Pos) == PathId.FrozenLake)
                {
                    // Frozen Lake (Paths.cs): speed builds toward where you're heading and carries on when you let go.
                    iceVel += (want * Paths.IceTopSpeed - iceVel) * Math.Min(1f, Paths.IceGrip * dt);
                    me.Pos += iceVel * dt;
                }
                else
                {
                    iceVel = want;
                    me.Pos += want * dt;
                }
            }
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
            if (IsHost && Props.Count > 0 && !me.Downed)
                ShoveProps(me.Pos, me.Def.HitR + PropPad, PushSpeed(me), dt, 0f, V2.Zero);  // leaning on a crate or barrel slides it (Props.cs)
            if ((Props.Count > 0 || LayoutOn) && !me.Downed)
                me.Pos = PushOutOfProps(me.Pos, me.Def.HitR + PropPad);  // crates, barrels and a closed chest are solid (Props.cs)
            // Touching a boss shoves you back out (it only hurts once a second; see TickEnemies).
            if (!me.Downed && shoveT <= 0f)
                foreach (var e in Enemies.Values)
                    if (e.Def.IsBoss && !IsSpawning(e) && !QueenAirborne(e) && !DevourUnder(e) && !DevourSwallowing(e) && me.Pos.DistanceTo(e.Pos) < e.Radius + me.Def.HitR + 2f)
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

            for (int slot = 0; slot < held.Length; slot++)
            {
                bool pressed = held[slot] && !abilityWasDown[slot];
                abilityWasDown[slot] = held[slot];
                if (!pressed || me.Downed || State == MatchState.GameOver)
                    continue;
                if (me.Slots[slot].Id == AbilityId.Mark && !AnyTarget())
                    continue;  // nothing to mark: don't waste the cooldown
                if (me.StandOn && MovesYou(me.Slots[slot].Id))
                    continue;  // planted: nothing that would carry you off (LastStand.cs)
                bool orbitOut = OrbitRelease(me, slot);  // Blade Fan pressed again: the host throws the kunai (Ninja.cs)
                bool harvestOut = HarvestPress(me, slot);  // Crimson Harvest pressed again: the host throws the ball (CrimsonHarvest.cs)
                bool standOut = StandRelease(me, slot);  // Last Stand pressed again: the host brings it down (LastStand.cs)
                bool shellOut = ShellRelease(me, slot);  // Blood Shell pressed again: the host brings it down (BloodShell.cs)
                bool rainOut = RainPress(me, slot);  // Sanguine Rain pressed again: the host cancels it (SanguineRain.cs)
                bool bathOut = BathPress(me, slot);  // Bloodbath pressed again: the host ends the channel (Bloodbath.cs)
                bool sprayOut = SprayRelease(me, slot);  // Arterial Spray pressed again: the host closes the vein (ArterialSpray.cs)
                bool steelOut = SteelRelease(me, slot);  // Caught Steel pressed again: the host throws the shards (CaughtSteel.cs)
                bool wallOut = WallRelease(me, slot);  // Blood Wall pressed again: the host stops it (BloodWall.cs)
                if (me.Slots[slot].Id == AbilityId.Whirlwind && MercyFirst(me, aimAt) == null)
                    continue;  // Misericorde with nobody wounded in reach: don't waste the cooldown (Misericorde.cs)
                if (!orbitOut && !standOut && !harvestOut && !rainOut && !bathOut && !shellOut && !sprayOut && !steelOut && !wallOut && !Consume(me, slot, false))
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
                        target = LocalOathCharge(me, aimAt);  // GuardiansOath.cs: charge to the player nearest your cursor
                        break;
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
                    case AbilityId.HemoNova:
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
                        target = me.Pos + (aimAt - me.Pos).LimitLength(BlinkRange);
                        target = ClampToArena(target, me.Def.BodyR);
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

            LocalDodge(me, move, input.Dodge, dt);  // the tiny dash with invulnerability (Dodge.cs)
            LocalWeaponDash(me, move, aimAt, input.WeaponAction && !me.ParryActive, dt);  // weapon secondaries on their own key (Ninja.cs)
            me.Pos = KeepInArena(RoomsHold(before, me.Pos, me.Def.BodyR), me.Def.BodyR);  // (a room locks behind you: Rooms.cs)
            me.Pos = RoomsPortal(me.Pos, ref me.PortalT, dt);  // wraparound portals (Rooms.cs)
            if (me.Pos.DistanceTo(before) > 600f)
            {
                me.Disp = me.Pos;
                steerMem.Remove(-me.Id);  // (the bot's route: fresh from the far side)
            }
            me.FiringSeen = firing;
            me.AimAt = aimAt;
            if (IsHost)
            {
                me.Firing = firing;
                if (bowRelease > 0f)
                    me.BowRelease = bowRelease;
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

        V2 ClampToArena(V2 pos, float r) => pos.Clamp(ArenaMin + new V2(r, r), ArenaMax - new V2(r, r));

        /// Players wrap around: walking off one edge brings you in on the opposite side.
        public static V2 WrapArena(V2 pos) => new V2(MathX.Fmod(pos.X, Width), MathX.Fmod(pos.Y, Height));

        // --- Host simulation ---------------------------------------------------

        void HostTick(float dt)
        {
            tickDt = dt;
            TickLiveStats(dt);  // RunStats.cs
            Recorder?.Tick(this, dt);
            TickPlayers(dt);
            TickKnights(dt);  // Knight.cs
            TickDives(dt);    // Royal Dive landings (QueenLoot.cs)
            TickScytheClock(dt);  // Twin Scythes' cut timers (ReaperLoot.cs)
            foreach (var tick in Classes.TickHooks)  // each class's KitDef.Tick: TickNinjas (Ninja.cs), TickBloodMages (BloodMage.cs)
                tick(this, dt);
            TickTsunami(dt);  // Tsunami.cs: waves rolling out
            TickHolyCross(dt);  // HolyCross.cs: Holy Crosses flying
            TickCavalry(dt);    // CavalryCall.cs: lancers riding
            TickAirstrikes(dt);  // Airstrike.cs: jets bombing their lanes, the lanes burning
            TickJugg(dt);       // Juggernaut.cs: the suit's time, and taking it off
            TickHarvest(dt);    // CrimsonHarvest.cs: blood torn out, the ball held and thrown
            TickBloodFreeze(dt);  // BloodFreeze.cs: the shockwave freezing what it reaches
            TickShocks(dt);   // KitWeapons.cs: the Storm Hammer's electrified enemies
            TickIonTrails(dt);  // KitWeapons.cs: the Railgun's charged paths
            TickPack(dt);     // KitWeapons.cs: the Wolf Whistle's prey
            TickOrbiters(dt);  // KitWeapons.cs: Ice Shards (WeaponId.ArcaneMissiles) launching
            TickTwinBlades(dt);  // TwinBlades.cs: the chain forgetting its place, Twin Fang's sabres flying
            TickLightbringer(dt);  // Lightbringer.cs: Mercy's wards fading, Sunlances flying
            TickLance(dt);  // Lance.cs: Hook & Haul's cooldown and hooks landing
            TickCenser(dt);      // Censer.cs: the rite forgetting its place, Censer Cast landing
            TickKatanaLater(dt);  // Katana.cs: the Katana's timed cuts, the Momentum Burst, the caltrops
            TickHammerThrows(dt);   // StormHammer.cs: throws leaving the hand
            TickHammerSlams(dt);    // StormHammer.cs: slams landing and their shockwaves
            TickBoShoves(dt);   // BoStaff.cs: the Bo Staff's shoves landing
            TickStaff(dt);      // Quarterstaff.cs: the jab's cooldown, stacks dropped with the staff
            TickRepulsion(dt);  // Repulsion.cs: the Undertow pulling and draining its meter
            TickTwinRifle(dt);  // TwinGrenades.cs: the grenade tubes' cooldown, emptied with the rifle
            TickTankAlts(dt);   // TankAlts.cs: Double Barrel, Proximity Shells, Tear Gas
            TickGreat(dt);      // Greatsword.cs: the chain forgetting its place, the guard arc stopping bullets
            TickArcKnuckles(dt);  // ArcKnuckles.cs: an unused Counter Hammer fading
            TickSteelWalls(dt);  // Knight.cs: Greatsword crescents stopping bullets
            TickStuckStars(dt);  // Ninja.cs: Shuriken stars falling out
            TickVines(dt);    // KitWeapons.cs: the Treant Rod's vine
            TickSpearPickups();  // BloodMage.cs: Bone Spears walked over
            TickHealerKit(dt);   // KitWeapons.cs: Ofuda wards, seeded enemies
            TickNimbus(dt);      // Nimbus.cs: rain under each cloud heals allies in it
            TickChalk(dt);       // Chalk.cs: drawing, and what the ink does
            TickWardenFaction(dt);  // WardenFaction.cs: tear gas with no Warden about, the Riot Shields' bash grace
            TickWaves(dt);
            TickPaths(dt);    // Paths.cs: hazard floors
            TickEnemies(dt * WorldRate);  // (slowed for a moment by a perfect parry: Parry.cs)
            KeepOutOfDomes();  // Fortress's dome (Dome.cs)
            KeepOutOfAegis(dt);  // Aeroaegis's vortex (Aegis.cs)
            KeepOutOfShell(dt);  // Blood Shell (BloodShell.cs)
            KeepOffBridges(dt);    // the Drawbridge (Drawbridge.cs)
            KeepOffBloodWalls(dt);  // Blood Wall (BloodWall.cs): pushes enemies back, bleeds those against it
            KeepOffPhalanx();    // the Bone Phalanx's shields (BonePhalanx.cs)
            TickMinions(dt);
            TickSearchlights(dt);  // Stun.cs
            TickOrbs(dt);
            TickCoins(dt);  // Coins.cs
            TickBossBarrels(dt);  // Props.cs: health barrels dropping into boss fights
            TickProps(dt);    // Props.cs: fuses, smashed pots, opened chests, breaks
            MoveBullets(dt * WorldRate);
            Collide();
            TickRods(dt);     // LightningRod.cs: the charge running down, and the bolts this tick's hits called
            EnforcePens();    // Chalk.cs: enemies penned in by an ink loop stay inside it
            TickDps(dt);      // DpsTools.cs: the DPS meter reads this tick's damage
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
            if (tick % 120 == 0)
                DoLog(string.Format("wave {0} state {1} enemies {2} bullets {3} score {4} hp {5}", Wave, State, Enemies.Count, Bullets.Count, Score, HpSummary()));
        }

        void TickPlayers(float dt)
        {
            foreach (var p in Players.Values)
            {
                TrackMotion(p, dt);  // Variety.cs
                TickSlots(p, dt);
                p.BarrierT = Math.Max(0f, p.BarrierT - dt);
                p.InvulnT = Math.Max(0f, p.InvulnT - dt);
                TickRiot(p, dt);
                p.OverdriveT = Math.Max(0f, p.OverdriveT - dt);
                p.AdrenalineT = Math.Max(0f, p.AdrenalineT - dt);
                TickMomentum(p, dt);  // Momentum.cs
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
                if (p.EnergyOn && p.ShieldHp <= 0f)
                    EnergyPayback(p);  // the Energy Ward ran out: what it soaked comes back as healing (Minions.cs)
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
                TickSpinUp(p, dt);  // the Burst SMG (Weapons.cs)
                TickRapier(p, dt);  // the Rapier's Tempo and Flourish (Rapier.cs)
                if (p.Weapon.Id == WeaponId.RadiantOrb)
                    TickRadiantOrb(p, dt);  // held, not fired (RadiantOrb.cs); it lets go when you're down
                if (p.Downed || p.SwallowT > 0f || p.WarpT > 0f)
                    continue;  // (nothing to shoot from inside the Devourer, or mid beam-in)
                if (p.StunShots > 0)
                    TickStunGun(p);  // the Stun Gun is out instead of your weapon (Stun.cs)
                else if (p.Weapon.Id == WeaponId.Pistol)
                {
                    TickRevolver(p, dt);
                    RevolverTrigger(p, dt);
                }
                else if (ChargeWeapon(p.Weapon.Id))
                    TickBow(p);  // the bow and the Warhammer fire when you let go
                else if (p.Weapon.Id == WeaponId.Katana || p.Weapon.Id == WeaponId.BoStaff)
                    TickKatanaFire(p);  // the Katana's dash or swipe, or the Bo Staff's slash, jab, spin, with the next one queued if you tap ahead (Katana.cs)
                else if (p.Weapon.Id == WeaponId.OutOfAmmo)
                    TickOutOfAmmo(p, dt);  // the swats, the knife chain and Alt's spin, no trigger (OutOfAmmo.cs)
                else if (p.Weapon.Id == WeaponId.TwinRifleAuto)
                    TickTwinAuto(p, dt);  // the guns aim and fire on their own, no trigger (TwinAuto.cs)
                else if (p.Weapon.Id != WeaponId.RadiantOrb && p.Firing && !p.ParryActive && !p.GreatGuard && !p.RapierGuard && !p.RepelPull && !p.KusariWhirl && p.FireCd <= 0f && State != MatchState.GameOver && ImplodeReady(p))
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
                    Recorder?.HealReceived(p, Math.Max(0f, Math.Min((PassiveRegen + p.Stats.Regen) * dt, p.HealCap - p.Hp)), "regeneration");
                    p.Hp = Math.Max(p.Hp, Math.Min(p.HealCap, p.Hp + (PassiveRegen + p.Stats.Regen) * dt));
                }
                if (p.Role == Role.Healer)
                {
                    // The aura heals you too: 1 HP/s, +1 per Sanctuary (its heal multiplier is 1 + stacks).
                    if (!Has(RunMod.Bloodlust))
                        Heal(p, AuraSelfHeal * p.Stats.AuraHealMult * dt, p, AuraSource);
                    foreach (var q in Players.Values)
                        if (q != p && q.Pos.DistanceTo(p.Pos) < p.AuraRange)
                            Heal(q, AuraHeal * p.Stats.AuraHealMult * dt, p, AuraSource);
                }
            }

            TickZones(dt);
            RecordHistory(dt);

            // Standing next to a downed ally slowly brings them back.
            foreach (var p in Players.Values)
            {
                TickReviveToken(p, dt);  // Shop.cs
                if (!p.Downed)
                    continue;
                Player helper = null;
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed && q.Pos.DistanceTo(p.Pos) < ReviveRange)
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
