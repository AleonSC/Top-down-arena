using System;
using System.Collections.Generic;

namespace Arena
{
    /// Challenge rooms. Once in every five waves (after one of the first three regular waves of each stretch; in the
    /// very first stretch after wave 2 or 3), a portal opens during the break. Everyone alive standing in it fills it
    /// (faster the more of you stand in it) and the party goes through to a room for one challenge: defend a crystal,
    /// duel a mini-boss, a kill rush, hold three zones in turn, or fight shadows of yourselves. Win and an altar gives
    /// everyone a prize pick (rare or better, sometimes a legendary); lose (or all go down) and you're simply sent
    /// back, the downed back up at low health. Either way the break then carries on. Ignore it and it closes.
    /// The room is the same arena with its own look (drawn in Game/ChallengeLook.cs); the room's state rides in
    /// every snapshot (SyncRoom).
    public enum ChallengeKind : byte { None, Defend, MiniBoss, KillRush, HoldZones, Mirror }

    /// None; Portal (open, in the break); Intro (in the room, the challenge named); Active; Won (the altar is up);
    /// Claimed (the prize given, leaving); Lost (failed, leaving).
    public enum RoomPhase : byte { None, Portal, Intro, Active, Won, Claimed, Lost }

    public static class Challenges
    {
        public static readonly ChallengeKind[] All = { ChallengeKind.Defend, ChallengeKind.MiniBoss, ChallengeKind.KillRush, ChallengeKind.HoldZones, ChallengeKind.Mirror };

        public const float PortalLife = 15f;    // seconds the portal stays open (counting only while nobody's choosing cards)
        public const float PortalR = 70f;       // stand this close to its middle to be in it
        public const float PortalFillTime = 1.6f;  // with everyone in it; with fewer it fills slower
        public const float IntroTime = 3f, ClaimTime = 1.4f, LostTime = 2.6f, AltarWait = 12f, AltarR = 46f;
        public const float SafetyTime = 150f;   // the duels give up after this long (sent back, no prize)

        // Defend the crystal.
        public const float DefendTime = 40f, CrystalR = 34f, CrystalHpBase = 600f, CrystalHpPerPlayer = 0.3f, CrystalContact = 18f;
        public const float DefendRanged = 0.15f;  // the share of the Defend room's enemies that shoot (the rest are melee)
        // Kill rush.
        public const float RushTime = 40f, RushHp = 0.5f;
        public static int RushGoal(int players) => 40 + 14 * (players - 1);
        // Hold the zones.
        public const float ZonesTime = 60f, ZoneR = 110f, ZoneFillTime = 8f, ZoneDrain = 0.05f;
        public const int ZoneCount = 3;
        public static readonly V2[] ZoneSpots = { new V2(420f, 300f), new V2(1180f, 300f), new V2(800f, 690f) };
        // The room's own enemies' health: a share of what the Warden would have at that wave, for this party (BossEquivalentHp).
        public const float MiniBossShare = 0.5f, ShadowShare = 0.3f;
        // The prize.
        public const float PrizeLegendaryChance = 0.25f;

        public static string Name(ChallengeKind k)
        {
            switch (k)
            {
                case ChallengeKind.Defend: return "Defend the Crystal";
                case ChallengeKind.MiniBoss: return "The Gatekeeper";
                case ChallengeKind.KillRush: return "Kill Rush";
                case ChallengeKind.HoldZones: return "Hold the Zones";
                case ChallengeKind.Mirror: return "Mirror Match";
            }
            return "";
        }

        public static string Goal(ChallengeKind k, int players)
        {
            switch (k)
            {
                case ChallengeKind.Defend: return "Keep the crystal alive for " + (int)DefendTime + " seconds";
                case ChallengeKind.MiniBoss: return "Defeat the mini-boss";
                case ChallengeKind.KillRush: return "Kill " + RushGoal(players) + " enemies in " + (int)RushTime + " seconds";
                case ChallengeKind.HoldZones: return "Stand in each zone until it fills";
                case ChallengeKind.Mirror: return players > 1 ? "Defeat your shadows" : "Defeat your shadow";
            }
            return "";
        }

        /// The room's timer (0: none, only the safety limit).
        public static float Clock(ChallengeKind k) => k == ChallengeKind.Defend ? DefendTime : k == ChallengeKind.KillRush ? RushTime : k == ChallengeKind.HoldZones ? ZonesTime : 0f;

        public static Rgba Accent(ChallengeKind k)
        {
            switch (k)
            {
                case ChallengeKind.Defend: return new Rgba(0.45f, 0.9f, 1f);
                case ChallengeKind.MiniBoss: return new Rgba(1f, 0.62f, 0.3f);
                case ChallengeKind.KillRush: return new Rgba(1f, 0.38f, 0.42f);
                case ChallengeKind.HoldZones: return new Rgba(0.55f, 1f, 0.55f);
                case ChallengeKind.Mirror: return new Rgba(0.72f, 0.5f, 1f);
            }
            return new Rgba(0.7f, 0.6f, 1f);
        }

        /// Enemies that only live in the rooms: their AI is in this file.
        public static bool Own(EnemyType t) => t == EnemyType.Gatekeeper || t == EnemyType.Shadow;
    }

    public partial class Game
    {
        public RoomPhase Room;
        public ChallengeKind RoomKind;
        public float RoomT;          // the phase's own clock: the portal's time left, the intro's, the altar's, leaving
        public float RoomClock;      // Active: the challenge's time left (or, for the duels, time spent)
        public V2 PortalPos;
        public float PortalFill;     // 0-1: everyone's in at 1
        public V2 CrystalPos, AltarPos;
        public float CrystalHp, CrystalMax;
        public int RoomKills, RoomGoal;
        public int ZoneIndex;        // the zone being held (ZoneCount when all are done)
        public float ZoneFill;
        public readonly V2[] RoomZones = new V2[Challenges.ZoneCount];
        public readonly Dictionary<int, int> ShadowOf = new Dictionary<int, int>();  // a Mirror Match shadow's enemy id -> the player it copies
        public static bool ChallengesByDefault = true;  // the test runner turns them off (like props) unless a test asks
        public bool ChallengesOn = ChallengesByDefault;
        public int RoomsWon, RoomsPlayed;  // this run (the log and the results)

        int portalBlock = -1;          // the last five-wave stretch a portal opened in
        ChallengeKind lastRoom;
        float roomSpawnT;

        /// A legendary on offer after a regular wave can only be a challenge room's prize (a boss's come after its wave).
        public bool PrizeLegendary(Player p) => p.OfferLegendary && Wave % 5 != 0;

        /// In the room (its intro to leaving).
        public bool InRoom => Room >= RoomPhase.Intro;

        /// The room's fight is on.
        public bool RoomActive => Room == RoomPhase.Active;

        void ResetRoom()
        {
            Room = RoomPhase.None;
            RoomKind = ChallengeKind.None;
            portalBlock = -1;
            lastRoom = ChallengeKind.None;
            ShadowOf.Clear();
            RoomsWon = RoomsPlayed = 0;
        }

        // --- The portal ---------------------------------------------------------------------------------------------

        /// Host, as a wave is cleared: a portal once in every five waves, after one of the first three regular waves
        /// (each equally likely; the first stretch skips wave 1).
        void MaybeOpenPortal()
        {
            if (!IsHost || !ChallengesOn || Practice != null || Hunt != null || ShowcaseMode || Room != RoomPhase.None || Wave >= MaxWave && !Endless)
                return;
            int k = Wave % 5, block = (Wave - 1) / 5;
            if (k < 1 || k > 3 || block == portalBlock)
                return;
            double chance = block == 0 ? (k == 1 ? 0.0 : k == 2 ? 0.5 : 1.0) : 1.0 / (4 - k);
            if (rng.NextDouble() >= chance)
                return;
            portalBlock = block;
            var pool = new List<ChallengeKind>();
            foreach (var c in Challenges.All)
                if (c != lastRoom)
                    pool.Add(c);
            OpenPortal(pool[rng.Next(pool.Count)]);
        }

        void OpenPortal(ChallengeKind kind)
        {
            RoomKind = kind;
            lastRoom = kind;
            Room = RoomPhase.Portal;
            RoomT = Challenges.PortalLife;
            PortalFill = 0f;
            // Somewhere open, away from where everyone is standing.
            PortalPos = new V2(Width / 2f, Height / 2f);
            for (int tries = 0; tries < 20; tries++)
            {
                var at = RandomInArena(320f, 230f);
                bool clear = true;
                foreach (var p in Players.Values)
                    clear &= p.Pos.DistanceTo(at) > 220f;
                PortalPos = at;
                if (clear)
                    break;
            }
            BroadcastFx(FxKind.Ring, PortalPos, Challenges.Accent(kind), 160f, 0.8f, Sfx.BossWarp);
            DoLog("a portal opens: " + Challenges.Name(kind));
        }

        /// Dev menu: open a portal to this challenge now (in a break), or straight into the room with `enter`.
        public void DevOpenPortal(ChallengeKind kind, bool enter = false)
        {
            if (!IsHost || kind == ChallengeKind.None || State != MatchState.Intermission || Room != RoomPhase.None)
                return;
            OpenPortal(kind);
            if (enter)
                EnterRoom();
        }

        /// Host, during the break: the portal fills while everyone alive stands in it, and closes when its time is up.
        void TickPortal(float dt)
        {
            int alive = 0, inside = 0;
            foreach (var p in Players.Values)
                if (!p.Downed)
                {
                    alive++;
                    if (p.Pos.DistanceTo(PortalPos) < Challenges.PortalR)
                        inside++;
                }
            if (inside > 0 && alive > 0)
                PortalFill += dt / Challenges.PortalFillTime * (0.35f + 0.65f * inside / alive);
            else
                PortalFill = Math.Max(0f, PortalFill - dt * 0.6f);
            if (inside < alive)
                PortalFill = Math.Min(PortalFill, 0.95f);  // everyone goes, or nobody
            if (PortalFill >= 1f)
            {
                EnterRoom();
                return;
            }
            if (!AnyonePicking() && PathOffer == null && !ShopOpen)
                RoomT -= dt;
            if (RoomT <= 0f)
            {
                Room = RoomPhase.None;
                RoomKind = ChallengeKind.None;
                BroadcastFx(FxKind.Implode, PortalPos, new Rgba(0.7f, 0.6f, 1f), 120f, 0.5f, Sfx.None);
                DoLog("the portal closes");
            }
        }

        // --- In the room --------------------------------------------------------------------------------------------

        void EnterRoom()
        {
            Room = RoomPhase.Intro;
            RoomT = Challenges.IntroTime;
            State = MatchState.Fighting;
            RoomsPlayed++;
            RoomKills = 0;
            RoomGoal = RoomKind == ChallengeKind.KillRush ? Challenges.RushGoal(Players.Count) : 0;
            RoomClock = Challenges.Clock(RoomKind);
            ZoneIndex = 0;
            ZoneFill = 0f;
            ShadowOf.Clear();
            roomSpawnT = 0f;
            spawnQueue.Clear();
            squadQueue.Clear();
            Props.Clear();
            Patches.Clear();
            Zones.Clear();
            Hazards.Clear();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            var mid = new V2(Width / 2f, Height / 2f);
            CrystalPos = mid;
            CrystalMax = CrystalHp = RoomKind == ChallengeKind.Defend ? Challenges.CrystalHpBase * (1f + Challenges.CrystalHpPerPlayer * (Players.Count - 1)) : 0f;
            AltarPos = mid;
            // The zones, in a random order.
            var spots = new List<V2>(Challenges.ZoneSpots);
            for (int i = 0; i < RoomZones.Length; i++)
            {
                int k = rng.Next(spots.Count);
                RoomZones[i] = spots[k];
                spots.RemoveAt(k);
            }
            // Everyone lands together: round the crystal, or in a line below the middle.
            int n = 0, count = Players.Count;
            foreach (var p in Players.Values)
            {
                V2 at = RoomKind == ChallengeKind.Defend ? mid + V2.FromAngle(MathX.Pi / 2f + (n - (count - 1) / 2f) * 0.7f) * 110f
                    : new V2(Width / 2f + (n - (count - 1) / 2f) * 90f, Height * 0.72f);
                MovePlayer(p, at);  // Ninja.cs: each player's own game moves them
                n++;
            }
            BroadcastFx(FxKind.Ring, mid, Challenges.Accent(RoomKind), 600f, 0.9f, Sfx.BossWarp);
            DoLog("into the room: " + Challenges.Name(RoomKind));
        }

        /// Host, each tick while in the room (instead of the wave's own flow, TickWaves).
        void TickRoom(float dt)
        {
            switch (Room)
            {
                case RoomPhase.Intro:
                    RoomT -= dt;
                    if (RoomT <= 0f)
                        StartChallenge();
                    break;
                case RoomPhase.Active:
                    TickChallenge(dt);
                    break;
                case RoomPhase.Won:
                    RoomT -= dt;
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.Pos.DistanceTo(AltarPos) < Challenges.AltarR + p.Def.HitR)
                            RoomT = 0f;
                    if (RoomT <= 0f)
                        ClaimPrize();
                    break;
                case RoomPhase.Claimed:
                case RoomPhase.Lost:
                    RoomT -= dt;
                    if (RoomT <= 0f)
                        ExitRoom();
                    break;
            }
        }

        void StartChallenge()
        {
            Room = RoomPhase.Active;
            var top = new V2(Width / 2f, Height * 0.3f);
            switch (RoomKind)
            {
                case ChallengeKind.MiniBoss:
                    SpawnEnemyAt(EnemyType.Gatekeeper, top, 1.2f, 0).T2 = 2.5f;
                    break;
                case ChallengeKind.Mirror:
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed)
                            continue;
                        var at = ClampToArena(new V2(Width - p.Pos.X, Height - p.Pos.Y), 30f);  // across the middle from you
                        var e = SpawnEnemyAt(EnemyType.Shadow, at, 1.2f, 0);
                        e.MaxHp = e.Hp = ShadowHp(p);
                        e.TargetId = p.Id;
                        e.PhaseT = Rand(2f, 3.5f);
                        ShadowOf[e.Id] = p.Id;
                    }
                    break;
            }
            Sounds.Add(Sfx.WaveStart);
        }

        void TickChallenge(float dt)
        {
            bool timed = Challenges.Clock(RoomKind) > 0f;
            if (timed)
                RoomClock -= dt;
            else
                RoomClock += dt;
            switch (RoomKind)
            {
                case ChallengeKind.Defend:
                    RoomSpawner(dt, 1.3f, 9 + 2 * Players.Count);
                    TickCrystal(dt);
                    if (CrystalHp <= 0f)
                    {
                        BroadcastFx(FxKind.Shatter, CrystalPos, CrystalPos + new V2(0f, -1f), Challenges.Accent(RoomKind), 60f, 0.6f, Sfx.BossCrack);
                        LoseRoom("the crystal broke");
                    }
                    else if (RoomClock <= 0f)
                        WinRoom();
                    break;
                case ChallengeKind.KillRush:
                    RoomSpawner(dt, 0.55f, 14 + 4 * Players.Count);
                    if (RoomKills >= RoomGoal)
                        WinRoom();
                    else if (RoomClock <= 0f)
                        LoseRoom("out of time (" + RoomKills + " of " + RoomGoal + ")");
                    break;
                case ChallengeKind.HoldZones:
                    RoomSpawner(dt, 1.5f, 7 + 2 * Players.Count);
                    TickZone(dt);
                    if (ZoneIndex >= Challenges.ZoneCount)
                        WinRoom();
                    else if (RoomClock <= 0f)
                        LoseRoom("out of time");
                    break;
                case ChallengeKind.MiniBoss:
                case ChallengeKind.Mirror:
                    if (Foes() == 0)
                        WinRoom();
                    else if (RoomClock > Challenges.SafetyTime)
                        LoseRoom("took too long");
                    break;
            }
        }

        /// The room's stream of enemies: one every `every` seconds (faster with more players), up to `cap` at once.
        void RoomSpawner(float dt, float every, int cap)
        {
            roomSpawnT -= dt * (1f + 0.3f * (Players.Count - 1));
            if (roomSpawnT > 0f || Enemies.Count >= cap)
                return;
            roomSpawnT = every;
            EnemyType type;
            if (RoomKind == ChallengeKind.KillRush)
            {
                var weak = new[] { EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Grunt, EnemyType.Lunger, EnemyType.Sniper };
                type = weak[rng.Next(weak.Length)];
            }
            else if (RoomKind == ChallengeKind.Defend)
            {
                // Bodies rushing the crystal, for the party to cut off: melee enemies, with a gun only now and then
                // (DefendRanged), so the crystal isn't simply shot down from across the room.
                var melee = new[] { EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Lunger, EnemyType.Brute };
                type = rng.NextDouble() < Challenges.DefendRanged ? (Wave >= 3 && rng.Next(2) == 0 ? EnemyType.Sniper : EnemyType.Grunt)
                    : melee[rng.Next(melee.Length)];
            }
            else
            {
                var pool = new List<EnemyType> { EnemyType.Grunt, EnemyType.Swarmer };
                if (Wave >= 3) pool.Add(EnemyType.Sniper);
                if (Wave >= 4) pool.Add(EnemyType.Brute);
                if (Wave >= 6) pool.Add(EnemyType.Charger);
                if (Wave >= 8) pool.Add(EnemyType.Mortar);
                if (Wave >= 16) pool.Add(EnemyType.Phantom);
                if (Wave >= 19) pool.Add(EnemyType.Leader);
                type = pool[rng.Next(pool.Count)];
            }
            var d = Defs.Get(type);
            float m = d.Radius + 30f;
            V2 lo = ArenaMin + new V2(m, m), hi = ArenaMax - new V2(m, m), pos;
            switch (rng.Next(4))
            {
                case 0: pos = new V2(Rand(lo.X, hi.X), lo.Y); break;
                case 1: pos = new V2(Rand(lo.X, hi.X), hi.Y); break;
                case 2: pos = new V2(lo.X, Rand(lo.Y, hi.Y)); break;
                default: pos = new V2(hi.X, Rand(lo.Y, hi.Y)); break;
            }
            var e = SpawnEnemyAt(type, pos, 0.7f, 0);
            if (RoomKind == ChallengeKind.KillRush)
                e.MaxHp = e.Hp = e.MaxHp * Challenges.RushHp;
        }

        /// Defend: enemy shots that reach the crystal hit it; enemies that reach it batter it and are pushed off.
        void TickCrystal(float dt)
        {
            scratchIds.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(CrystalPos) < Challenges.CrystalR + b.Radius)
                {
                    CrystalHp -= b.Dmg;
                    scratchIds.Add(b.Id);
                }
            foreach (int id in scratchIds)
            {
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }
            if (scratchIds.Count > 0)
                AddFx(FxKind.Spark, CrystalPos, Challenges.Accent(ChallengeKind.Defend), Challenges.CrystalR + 10f, 0.25f);
            float scale = EnemyDamageScale(Wave);
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f)
                    continue;
                V2 d = e.Pos - CrystalPos;
                float reach = Challenges.CrystalR + e.Radius;
                if (d.Length < reach)
                {
                    CrystalHp -= (e.Def.Contact > 0f ? e.Def.Contact : Challenges.CrystalContact) * scale * dt;
                    e.Pos = CrystalPos + (d.Length > 0.1f ? d.Normalized() : new V2(1f, 0f)) * reach;
                }
            }
            CrystalHp = Math.Max(0f, CrystalHp);
        }

        /// Hold the zones: the zone fills while someone alive stands in it (half as fast with an enemy in it too),
        /// and drains slowly when nobody is.
        void TickZone(float dt)
        {
            if (ZoneIndex >= Challenges.ZoneCount)
                return;
            V2 z = RoomZones[ZoneIndex];
            int alive = 0, inside = 0;
            foreach (var p in Players.Values)
                if (!p.Downed)
                {
                    alive++;
                    if (p.Pos.DistanceTo(z) < Challenges.ZoneR)
                        inside++;
                }
            bool contested = false;
            foreach (var e in Enemies.Values)
                contested |= e.SpawnT <= 0f && e.Pos.DistanceTo(z) < Challenges.ZoneR;
            if (inside > 0)
                ZoneFill += dt / Challenges.ZoneFillTime * (0.7f + 0.3f * inside / Math.Max(1, alive)) * (contested ? 0.5f : 1f);
            else
                ZoneFill = Math.Max(0f, ZoneFill - Challenges.ZoneDrain * dt);
            if (ZoneFill >= 1f)
            {
                BroadcastFx(FxKind.Ring, z, Challenges.Accent(ChallengeKind.HoldZones), Challenges.ZoneR + 60f, 0.6f, Sfx.BossChime);
                ZoneIndex++;
                ZoneFill = 0f;
            }
        }

        /// Where a room's enemy goes for instead of its player (TickEnemies): the crystal, or the zone to stand in.
        V2 RoomTarget(Enemy e, V2 tpos)
        {
            if (!RoomActive)
                return tpos;
            if (e.Type == EnemyType.Shadow)
            {
                var me = CopyOf(e);
                if (me != null && !me.Downed && me.VanishT <= 0f)
                    return me.Pos;  // a shadow hunts its own player while they're up
                return tpos;
            }
            if (RoomKind == ChallengeKind.Defend && e.Id % 3 != 0)
                return CrystalPos;  // two in three go for the crystal
            if (RoomKind == ChallengeKind.HoldZones && e.Id % 2 == 0 && ZoneIndex < Challenges.ZoneCount)
                return RoomZones[ZoneIndex];  // half crowd the zone
            return tpos;
        }

        /// Host, when an enemy dies in the room (OnEnemyKilled).
        void RoomKilled(Enemy e)
        {
            if (RoomActive)
                RoomKills++;
            ShadowOf.Remove(e.Id);
        }

        void WinRoom()
        {
            Room = RoomPhase.Won;
            RoomT = Challenges.AltarWait;
            RoomsWon++;
            RoomRecord(RoomKind, true);  // everyone's Records (RunSaves.cs)
            ClearRoomEnemies();
            BroadcastFx(FxKind.Ring, AltarPos, new Rgba(1f, 0.85f, 0.4f), 500f, 0.8f, Sfx.WaveClear);
            DoLog("challenge complete: " + Challenges.Name(RoomKind));
        }

        void LoseRoom(string why)
        {
            Room = RoomPhase.Lost;
            RoomT = Challenges.LostTime;
            RoomRecord(RoomKind, false);
            ClearRoomEnemies();
            Sounds.Add(Sfx.BossGong);
            DoLog("challenge failed: " + Challenges.Name(RoomKind) + " (" + why + ")");
        }

        void ClearRoomEnemies()
        {
            foreach (var e in Enemies.Values)
                AddFx(FxKind.Implode, e.Pos, Defs.Get(e.Type).Color, e.Radius + 20f, 0.4f);
            Enemies.Clear();
            ShadowOf.Clear();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
        }

        /// Someone touched the altar (or it waited long enough): everyone gets a prize pick, and some a legendary.
        void ClaimPrize()
        {
            int next = (Wave / 5 + 1) * 5;
            var boss = BossForWave(Math.Min(MaxWave, Math.Max(5, next)));
            var key = IsReaper(boss) ? EnemyType.TwinA : boss;
            foreach (var p in Players.Values)
            {
                p.PrizePicks++;
                if (rng.NextDouble() < Challenges.PrizeLegendaryChance)
                    p.LegendaryQueue.Add(key);
            }
            Room = RoomPhase.Claimed;
            RoomT = Challenges.ClaimTime;
            BroadcastFx(FxKind.Ring, AltarPos, new Rgba(1f, 0.85f, 0.4f), 220f, 0.6f, Sfx.LevelUp);
        }

        /// Back to the break: the downed get up at low health, the prize picks are offered, and the countdown starts over.
        void ExitRoom()
        {
            ClearRoomEnemies();
            Room = RoomPhase.None;
            RoomKind = ChallengeKind.None;
            State = MatchState.Intermission;
            StateTimer = IntermissionTime;
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    RevivePlayer(p, 0.3f);
                if (p.Offer == null)
                    NextOffer(p);
            }
            BroadcastFx(FxKind.Ring, new V2(Width / 2f, Height / 2f), new Rgba(0.7f, 0.6f, 1f), 600f, 0.7f, Sfx.BossWarp);
        }

        /// Everyone's down in the room: no game over, the challenge is just lost (CheckGameOver).
        bool RoomCatchesWipe()
        {
            if (!InRoom)
                return false;
            if (Room == RoomPhase.Active || Room == RoomPhase.Intro)
                LoseRoom("everyone went down");
            return true;
        }

        /// Bots: into an open portal, onto the zone being held, back near the crystal, and to the altar.
        void BotRoom(Player me, ref V2 move)
        {
            if (me.Downed || Room == RoomPhase.None)
                return;
            V2? goal = null;
            float near = 20f;
            if (Room == RoomPhase.Portal)
            {
                goal = PortalPos;
                near = Challenges.PortalR * 0.4f;
            }
            else if (Room == RoomPhase.Won)
                goal = AltarPos;
            else if (RoomActive && RoomKind == ChallengeKind.HoldZones && ZoneIndex < Challenges.ZoneCount)
            {
                goal = RoomZones[ZoneIndex];
                near = Challenges.ZoneR * 0.5f;
            }
            else if (RoomActive && RoomKind == ChallengeKind.Defend)
            {
                goal = CrystalPos;
                near = 240f;
            }
            if (!goal.HasValue)
                return;
            if (me.Pos.DistanceTo(goal.Value) > near)
                move = (goal.Value - me.Pos).LimitLength(1f);
            else if (Room == RoomPhase.Portal)
                move = V2.Zero;  // stay in
            else if (RoomKind == ChallengeKind.HoldZones)
                move *= 0.3f;    // dodge a little, but stay on it
        }

        // --- The room's own enemies: the Gatekeeper and the shadows --------------------------------------------------

        public const float GateRange = 260f, GateDash = 720f, GateWindup = 0.9f, GateDashTime = 0.45f;
        public const float ShadowRange = 300f, ShadowMeleeRange = 70f, ShadowBlink = 130f;

        /// What the Warden would have at this wave against this party: a boss's health, growth and build boost.
        public float BossEquivalentHp(int wave)
        {
            float late = LateBossBoost(wave);
            return SpawnHp(EnemyType.Warden, wave) * Math.Max(late, BossPowerScale()) / late;
        }

        float ShadowHp(Player p) => SpawnHp(EnemyType.Shadow, Wave);

        Player CopyOf(Enemy e)
        {
            int id;
            Player p;
            return ShadowOf.TryGetValue(e.Id, out id) && Players.TryGetValue(id, out p) ? p : null;
        }

        V2 RoomMove(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            if (e.Type == EnemyType.Gatekeeper)
            {
                if (e.Phase == 2)
                {
                    speed = GateDash;
                    return V2.FromAngle(e.Facing);
                }
                return e.Phase == 0 ? KeepDistance(dir, dist, GateRange) + dir.Orthogonal() * e.Strafe * 0.5f : V2.Zero;
            }
            // A shadow: in close with a melee weapon, else at range, always on the move.
            var p = CopyOf(e);
            bool melee = p != null && p.Weapon.Melee;
            return melee ? (dist > ShadowMeleeRange ? dir : V2.Zero) + dir.Orthogonal() * e.Strafe * 0.4f
                : KeepDistance(dir, dist, ShadowRange) + dir.Orthogonal() * e.Strafe * 0.8f;
        }

        /// The Gatekeeper's attacks, in turn: a ring of shots with a gap, three aimed fans, and a charge that ends in a
        /// burst. Below half health it attacks a third faster and rings twice.
        void RoomSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.Type == EnemyType.Shadow)
            {
                e.PhaseT -= dt;
                if (e.PhaseT <= 0f)  // a sidestep, like a dash
                {
                    e.PhaseT = Rand(2.2f, 3.8f);
                    V2 side = (tpos - e.Pos).Normalized().Orthogonal() * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    var from = e.Pos;
                    e.Pos = ClampToArena(e.Pos + side * ShadowBlink, e.Radius);
                    BroadcastFx(FxKind.Ring, from, Defs.Get(EnemyType.Shadow).Color, 30f, 0.3f, Sfx.Blink);
                }
                return;
            }
            bool enraged = e.Hp < e.MaxHp * 0.5f;
            float aim = (tpos - e.Pos).Angle;
            switch (e.Phase)
            {
                case 0:
                    e.T2 -= dt * (enraged ? 1.35f : 1f);
                    if (e.T2 > 0f)
                        break;
                    int attack = (int)e.Ang % 3;
                    e.Ang += 1f;
                    if (attack == 0)
                    {
                        GateRing(e);
                        if (enraged)
                        {
                            e.Phase = 4;  // the second ring, a moment later
                            e.PhaseT = 0.55f;
                        }
                        e.T2 = 2.4f;
                    }
                    else if (attack == 1)
                    {
                        e.Phase = 3;
                        e.SquadSlot = enraged ? 4 : 3;  // fans to go
                        e.PhaseT = 0f;
                    }
                    else
                    {
                        e.Phase = 1;
                        e.PhaseT = GateWindup;
                        e.Facing = aim;
                        Sounds.Add(Sfx.BossCharge);
                    }
                    break;
                case 1:  // winding up the charge, its line shown
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 2;
                        e.PhaseT = GateDashTime;
                        Sounds.Add(Sfx.BossWhoosh);
                    }
                    break;
                case 2:  // charging
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                    {
                        int n = enraged ? 18 : 14;
                        for (int i = 0; i < n; i++)
                            EnemyShot(e, MathX.Tau * i / n + e.Facing, 190f, Defs.SmallShot);
                        BroadcastFx(FxKind.Ring, e.Pos, Defs.Get(EnemyType.Gatekeeper).Color, 120f, 0.4f, Sfx.BossSlam);
                        e.Phase = 0;
                        e.T2 = 2.1f;
                    }
                    break;
                case 3:  // aimed fans
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                    {
                        for (int i = 0; i < 5; i++)
                            EnemyShot(e, aim + (i - 2) * 0.16f, 260f, Defs.FastShot);
                        e.SquadSlot--;
                        e.PhaseT = 0.32f;
                        if (e.SquadSlot <= 0)
                        {
                            e.Phase = 0;
                            e.T2 = 2.2f;
                        }
                    }
                    break;
                case 4:  // the enraged second ring
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                    {
                        GateRing(e);
                        e.Phase = 0;
                    }
                    break;
            }
        }

        /// A ring of big shots with a gap of four, the gap somewhere new each time.
        void GateRing(Enemy e)
        {
            const int n = 24;
            int gap = rng.Next(n);
            for (int i = 0; i < n; i++)
            {
                int off = (i - gap + n) % n;
                if (off < 4)
                    continue;
                EnemyShot(e, MathX.Tau * i / n, 150f, Defs.BigShot);
            }
            Sounds.Add(Sfx.BossPulse);
        }

        /// The room's enemies' shots (EnemyFire). A shadow fires its player's way: a fan as wide as their extra shots,
        /// or, with a melee weapon, a short-lived slash up close.
        bool RoomFire(Enemy e, V2 tpos, float aim)
        {
            if (e.Type == EnemyType.Gatekeeper)
            {
                e.FireT = 99f;  // its attacks are its Special
                return true;
            }
            var p = CopyOf(e);
            if (p == null)
            {
                e.FireT = 1f;
                return true;
            }
            if (p.Weapon.Melee)
            {
                if (e.Pos.DistanceTo(tpos) > ShadowMeleeRange + 60f)
                {
                    e.FireT = 0.2f;
                    return true;
                }
                for (int i = 0; i < 7; i++)
                {
                    V2 dir = V2.FromAngle(aim + (i - 3) * 0.2f);
                    var b = SpawnBullet(Defs.SmallShot, e.Pos + dir * e.Radius, dir * 380f, 0f, 0.35f, Defs.BulletDamage(Defs.SmallShot), 0);
                    b.Source = e.Type;
                }
                e.FireT = 0.95f;
                Sounds.Add(Sfx.SwingLight);
                return true;
            }
            int shots = Math.Min(5, 1 + p.Stats.ExtraShots + p.Stats.Fan);
            for (int i = 0; i < shots; i++)
                EnemyShot(e, aim + (i - (shots - 1) / 2f) * 0.14f, 300f, Defs.FastShot);
            e.FireT = Math.Max(0.6f, Math.Min(1.6f, p.Weapon.FireInterval * 2.5f));
            return true;
        }

        /// The snapshot's room block: everyone draws the portal and the room from this.
        void SyncRoom(NetSync s)
        {
            int room = (int)Room, kind = (int)RoomKind, zone = ZoneIndex;
            s.Byte(ref room);
            s.Byte(ref kind);
            Room = (RoomPhase)room;
            RoomKind = (ChallengeKind)kind;
            if (Room == RoomPhase.None)
                return;
            s.Float(ref RoomT);
            s.Float(ref RoomClock);
            s.Vec(ref PortalPos);
            s.Float(ref PortalFill);
            s.Vec(ref CrystalPos);
            s.Float(ref CrystalHp);
            s.Float(ref CrystalMax);
            s.Vec(ref AltarPos);
            s.Int(ref RoomKills);
            s.Int(ref RoomGoal);
            s.Byte(ref zone);
            ZoneIndex = zone;
            s.Float(ref ZoneFill);
            for (int i = 0; i < RoomZones.Length; i++)
                s.Vec(ref RoomZones[i]);
            int shadows = ShadowOf.Count;
            s.Byte(ref shadows);
            if (s.Reading)
            {
                ShadowOf.Clear();
                for (int i = 0; i < shadows; i++)
                {
                    int id = 0, of = 0;
                    s.Int(ref id);
                    s.Byte(ref of);
                    ShadowOf[id] = of;
                }
            }
            else
                foreach (var kv in ShadowOf)
                {
                    int id = kv.Key, of = kv.Value;
                    s.Int(ref id);
                    s.Byte(ref of);
                }
        }
    }
}
