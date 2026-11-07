using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Things standing in the arena during regular waves (Props.cs):
    /// Crate / HeavyCrate: cover. Enemy shots stop at it and wear it down; your shots fly over it. Solid to walk into.
    /// Pot: a breakable. Shoot it or walk through it: it smashes and drops a health orb or a little XP.
    /// Barrel: rare. Any shot sets it fizzing; 0.4 s later it blows up, hurting enemies (a lot), players (a little),
    ///   smashing crates and pots and setting off other barrels.
    /// Chest: rare treasure. Touch it to open it: every player gets their own item (an upgrade card or a legendary),
    ///   which only they can see. Walk onto it, then take it (G) or discard it (X). The wave waits for both.
    /// HealthBarrel: boss fights only. A green barrel with a white cross drops in now and then; shoot it or walk
    ///   into it and it breaks into health orbs.
    public enum PropKind : byte { Crate, HeavyCrate, Pot, Barrel, Chest, HealthBarrel }

    public class Prop
    {
        public int Id;
        public PropKind Kind;
        public V2 Pos;
        public float Hp, MaxHp;  // crates: enemy hits left; the rest: 1 until broken
        public float Fuse;       // a barrel: time left before it blows (0 = not lit)
        public bool Open;        // a chest that's been opened
        public int Seed;         // look variety
        public int LitBy;        // host: the player who set a barrel off (kill credit)
        // Every game, for the look.
        public float Age, HitT, OpenT;
    }

    /// A chest's item for one player. Everyone's game has every item, but only its owner's draws it.
    public class Loot
    {
        public int Id, Owner, Upgrade;
        public V2 Pos;
        public float Age;  // every game, for the look
    }

    public partial class Game
    {
        /// How long a smashed pot's effect lasts: the hologram's burst (Game/PropsLook.cs); the clay chips still play in 0.4 s.
        public const float PotFxTime = 1.3f;
        public const float CrateSize = 44f, HeavyCrateSize = 54f, PotR = 13f, BarrelR = 17f, ChestW = 48f, ChestH = 34f;
        public const float BarrelBlast = 120f, BarrelFuse = 0.4f, BarrelChainFuse = 0.15f;
        public const float LootReach = 30f;   // how close (plus your body) you must stand to an item to take it
        const float ChestStayOpen = 2.5f;     // an opened chest stays this long, then goes
        const float PropPad = 5f;             // players are kept this far past their hit dot from solid props
        const float ChestTouch = 12f;         // touching a chest (beyond that) opens it
        const float ChestChance = 0.08f, ChestLegendaryChance = 0.2f;
        const float BarrelHurt = 16f;         // to players caught in a blast (before the run's enemy damage scaling)

        public readonly Dictionary<int, Prop> Props = new Dictionary<int, Prop>();
        public readonly Dictionary<int, Loot> Loots = new Dictionary<int, Loot>();
        int nextPropId = 1, nextLootId = 1;
        /// Props come with regular waves. The test runner turns them off by default (PropsByDefault) so tests that
        /// measure exact hits aren't thrown by a crate in the way; the match and props tests turn them on.
        public bool PropsOn = PropsByDefault;
        public static bool PropsByDefault = true;

        // --- Numbers by wave ------------------------------------------------------------------------

        /// Crates (cover) on the field in a regular wave: more as the bullets thicken.
        public static int CrateTarget(int wave) => Math.Min(10, 2 + (int)(wave * 0.3f));
        /// The Cluttered modifier: a maze of crates, growing every wave (they stay until broken; bosses clear them).
        public static int ClutterTarget(int wave) => Math.Min(48, 14 + wave * 3);
        public static int PotTarget(int wave) => Math.Min(5, 1 + wave / 7);
        public static float BarrelChance(int wave) => Math.Min(0.45f, 0.25f + wave * 0.005f);
        /// Enemy hits a crate takes before it breaks.
        public static float CrateHits(PropKind k, int wave) => (8f + wave / 3) * (k == PropKind.HeavyCrate ? 2f : 1f);
        public static float BarrelDamage(int wave) => 40f + 10f * wave;

        // --- Shapes ---------------------------------------------------------------------------------

        public static bool Round(Prop p) => p.Kind == PropKind.Pot || p.Kind == PropKind.Barrel || p.Kind == PropKind.HealthBarrel;
        public static float PropRadius(Prop p) => p.Kind == PropKind.Pot ? PotR : BarrelR;
        public static float HalfW(PropKind k) => k == PropKind.Crate ? CrateSize / 2f : k == PropKind.HeavyCrate ? HeavyCrateSize / 2f : k == PropKind.Chest ? ChestW / 2f : k == PropKind.Pot ? PotR : BarrelR;
        public static float HalfH(PropKind k) => k == PropKind.Chest ? ChestH / 2f : HalfW(k);
        public static float HalfW(Prop p) => HalfW(p.Kind);
        public static float HalfH(Prop p) => HalfH(p.Kind);
        public static bool IsCrate(Prop p) => p.Kind == PropKind.Crate || p.Kind == PropKind.HeavyCrate;
        /// You can't walk through it (pots smash instead; an open chest is just treasure).
        public static bool Solid(Prop p) => p.Kind != PropKind.Pot && p.Kind != PropKind.HealthBarrel && !(p.Kind == PropKind.Chest && p.Open);
        /// Breaks when you walk into it or shoot it (your shots carry on).
        static bool Smashable(Prop p) => p.Kind == PropKind.Pot || p.Kind == PropKind.HealthBarrel;

        public static bool PropCovers(Prop p, V2 pos, float pad)
        {
            if (Round(p))
                return pos.DistanceTo(p.Pos) < PropRadius(p) + pad;
            return Math.Abs(pos.X - p.Pos.X) < HalfW(p) + pad && Math.Abs(pos.Y - p.Pos.Y) < HalfH(p) + pad;
        }

        /// Moves something of radius r out of every solid prop (out the nearest side of a box).
        public V2 PushOutOfProps(V2 pos, float r)
        {
            foreach (var p in Props.Values)
            {
                if (!Solid(p) || p.Hp <= 0f || !PropCovers(p, pos, r))
                    continue;
                if (Round(p))
                {
                    V2 away = pos - p.Pos;
                    pos = p.Pos + (away.Length > 0.5f ? away.Normalized() : new V2(1f, 0f)) * (PropRadius(p) + r);
                    continue;
                }
                float hw = HalfW(p) + r, hh = HalfH(p) + r;
                float l = pos.X - (p.Pos.X - hw), rr = p.Pos.X + hw - pos.X, t = pos.Y - (p.Pos.Y - hh), b = p.Pos.Y + hh - pos.Y;
                float m = Math.Min(Math.Min(l, rr), Math.Min(t, b));
                pos = m == l ? new V2(pos.X - l, pos.Y) : m == rr ? new V2(pos.X + rr, pos.Y) : m == t ? new V2(pos.X, pos.Y - t) : new V2(pos.X, pos.Y + b);
            }
            return PushOutOfSolids(pos, r);  // and the layout's walls and pillars (LayoutRules.cs)
        }

        // --- Spawning (host) ------------------------------------------------------------------------

        /// At the start of each wave: bosses get a clear arena; regular waves top the crates, pots and barrels up to the
        /// wave's numbers (what's left from the last wave stays), and now and then bring a chest.
        void WaveProps()
        {
            if (!PropsOn || Practice != null || ShowcaseMode)
                return;
            if (IsBossWave(Wave))
            {
                ClearProps(true);
                return;
            }
            PlaceLayoutCrates();  // a new cycle's floor plan brings its own cover (LayoutRules.cs)
            // Anything left outside the arena (Narrow Pass's walls closing in) goes, and the boss fight's health barrels.
            var outside = new List<int>();
            foreach (var p in Props.Values)
                if (p.Kind == PropKind.HealthBarrel || ClampToArena(p.Pos, Math.Max(HalfW(p), HalfH(p))).DistanceTo(p.Pos) > 0.5f)
                    outside.Add(p.Id);
            foreach (int id in outside)
                Props.Remove(id);
            int crates = 0, pots = 0;
            foreach (var p in Props.Values)
            {
                if (IsCrate(p))
                    crates++;
                else if (p.Kind == PropKind.Pot)
                    pots++;
            }
            bool clutter = Has(RunMod.Cluttered);
            int crateGoal = clutter ? ClutterTarget(Wave) : CrateTarget(Wave);
            for (int tries = 0; crates < crateGoal && tries < (clutter ? 300 : 20); tries++)
            {
                var kind = Wave >= (clutter ? 8 : 12) && rng.NextDouble() < 0.35 ? PropKind.HeavyCrate : PropKind.Crate;
                V2 at;
                if (!FreeSpot(HalfW(kind), out at))
                    break;
                AddProp(kind, at);
                crates++;
                // Often a second one alongside: a little wall to hide behind.
                if (crates < crateGoal && rng.NextDouble() < 0.45)
                {
                    float step = HalfW(kind) * 2f + 2f;
                    V2 next = at + (rng.Next(2) == 0 ? new V2(rng.Next(2) == 0 ? step : -step, 0f) : new V2(0f, rng.Next(2) == 0 ? step : -step));
                    if (SpotClear(next, HalfW(kind), 0f))
                    {
                        AddProp(kind, next);
                        crates++;
                    }
                }
            }
            for (int tries = 0; pots < PotTarget(Wave) && tries < 10; tries++)
            {
                V2 at;
                if (!FreeSpot(PotR, out at))
                    break;
                AddProp(PropKind.Pot, at);
                pots++;
            }
            if (Wave >= 3 && rng.NextDouble() < BarrelChance(Wave))  // (the first waves are melee rushers: nothing to blow up yet)
                TryAdd(PropKind.Barrel);
            if (Wave >= 20 && rng.NextDouble() < 0.15)
                TryAdd(PropKind.Barrel);
            bool chest = Wave >= 2 && rng.NextDouble() < ChestChance;
            if (!chest && ContrabandChest())
            {
                chest = true;  // Contraband: the first wave after each boss always has one (Contraband.cs)
                DoLog("Contraband: a cycle chest");
            }
            if (chest)
                TryAdd(PropKind.Chest);
        }

        void TryAdd(PropKind kind)
        {
            V2 at;
            if (FreeSpot(Math.Max(HalfW(kind), HalfH(kind)), out at))
                AddProp(kind, at);
        }

        /// Dev and tests: put a prop somewhere.
        public Prop AddProp(PropKind kind, V2 at)
        {
            var p = new Prop { Id = nextPropId++, Kind = kind, Pos = at, Seed = rng.Next(256) };
            p.Hp = p.MaxHp = IsCrate(p) ? CrateHits(kind, Wave) : 1f;
            Props[p.Id] = p;
            return p;
        }

        bool FreeSpot(float size, out V2 at)
        {
            for (int i = 0; i < 40; i++)
            {
                at = RandomInArena(110f);
                if (SpotClear(at, size, 170f))
                    return true;
            }
            at = default(V2);
            return false;
        }

        /// Room for a prop this big here: inside the arena, away from the other props, and (playerGap) from the players.
        bool SpotClear(V2 at, float size, float playerGap)
        {
            if (ClampToArena(at, size + 60f).DistanceTo(at) > 0.5f)
                return false;
            if (NearSolid(at, size + 16f))
                return false;  // and clear of the layout's walls and pillars (LayoutRules.cs)
            foreach (var pl in Players.Values)
                if (pl.Pos.DistanceTo(at) < playerGap + size)
                    return false;
            foreach (var p in Props.Values)
            {
                float other = Math.Max(HalfW(p), HalfH(p));
                // Crates side by side may touch; everything else keeps a walkway between.
                if (Math.Abs(at.X - p.Pos.X) < size + other + 1f && Math.Abs(at.Y - p.Pos.Y) < size + other + 1f && !(playerGap == 0f && IsCrate(p)))
                    return false;
                if (at.DistanceTo(p.Pos) < size + other + (playerGap == 0f ? 1f : 70f))
                    return false;
            }
            return true;
        }

        void ClearProps(bool poof)
        {
            if (poof)
                foreach (var p in Props.Values)
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.8f, 0.75f, 0.6f, 0.6f), 30f, 0.3f, Sfx.None);
            Props.Clear();
            Loots.Clear();
        }

        void ResetProps()
        {
            Props.Clear();
            Loots.Clear();
            lootSent.Clear();
            nextPropId = nextLootId = 1;
            bossBarrelT = BossBarrelFirst;
        }

        // --- Health barrels in boss fights (host) ---------------------------------------------------

        const float BossBarrelFirst = 15f, BossBarrelMin = 18f, BossBarrelMax = 26f;
        const int BossBarrelCap = 2, BossBarrelOrbs = 2;
        float bossBarrelT = BossBarrelFirst;

        /// While a boss is up, a health barrel drops in every 18-26 s (the first after 15 s), two at most at once,
        /// somewhere clear of the players and the boss.
        void TickBossBarrels(float dt)
        {
            if (!PropsOn || Practice != null || ShowcaseMode || State != MatchState.Fighting || !IsBossWave(Wave))
            {
                bossBarrelT = BossBarrelFirst;
                return;
            }
            Enemy boss = null;
            foreach (var e in Enemies.Values)
                if (e.Def.IsBoss && e.Hp > 0f)
                {
                    boss = e;
                    break;
                }
            if (boss == null || (bossBarrelT -= dt) > 0f)
                return;
            bossBarrelT = Rand(BossBarrelMin, BossBarrelMax);
            int up = 0;
            foreach (var p in Props.Values)
                if (p.Kind == PropKind.HealthBarrel)
                    up++;
            if (up >= BossBarrelCap)
                return;
            for (int i = 0; i < 30; i++)
            {
                V2 at = RandomInArena(120f);
                if (at.DistanceTo(boss.Pos) < boss.Radius + 120f || !SpotClear(at, BarrelR, 120f))
                    continue;
                AddProp(PropKind.HealthBarrel, at);
                return;
            }
        }

        // --- Hits (host) ----------------------------------------------------------------------------

        /// In Collide, for each bullet: does a prop stop it? Crates stop enemy shots (each one wears it down), barrels
        /// stop every shot and go off, pots smash under your shots (which carry on).
        bool PropStops(Bullet b)
        {
            bool enemy = Defs.IsEnemyShot(b.Kind);
            foreach (var p in Props.Values)
            {
                if (p.Hp <= 0f || !PropCovers(p, b.Pos, b.Radius))
                    continue;
                switch (p.Kind)
                {
                    case PropKind.Crate:
                    case PropKind.HeavyCrate:
                        if (!enemy)
                            continue;  // you fire over your cover
                        p.Hp -= 1f;
                        p.HitT = 0.15f;
                        return true;
                    case PropKind.Barrel:
                        LightBarrel(p, enemy ? 0 : b.Owner, BarrelFuse);
                        return true;
                    case PropKind.Pot:
                    case PropKind.HealthBarrel:
                        if (!enemy)
                            p.Hp = 0f;  // smashed; the shot carries on
                        continue;
                }
            }
            return false;
        }

        void LightBarrel(Prop p, int by, float fuse)
        {
            if (p.Fuse > 0f || p.Hp <= 0f)
                return;
            p.Fuse = fuse;
            p.LitBy = by;
            BroadcastFx(FxKind.Spark, p.Pos, new Rgba(1f, 0.6f, 0.2f), 20f, fuse, Sfx.Hit);
        }

        /// Your blasts (grenades, Explosive Rounds, flak) set barrels off and smash pots in reach.
        void PropBlast(V2 at, float radius, int owner)
        {
            if (Props.Count == 0 || owner == 0)
                return;
            foreach (var p in Props.Values)
            {
                if (p.Hp <= 0f || !PropCovers(p, at, radius))
                    continue;
                if (p.Kind == PropKind.Barrel)
                    LightBarrel(p, owner, BarrelChainFuse);
                else if (Smashable(p))
                    p.Hp = 0f;
            }
        }

        readonly List<Prop> propsGone = new List<Prop>();

        /// Host, each tick: fuses, touches (pots smash, chests open), breaks and blasts; items whose owner left go.
        void TickProps(float dt)
        {
            if (Props.Count == 0 && Loots.Count == 0)
                return;
            propsGone.Clear();
            foreach (var p in Props.Values)
            {
                if (p.Kind == PropKind.Barrel && p.Fuse > 0f && (p.Fuse -= dt) <= 0f)
                    p.Hp = 0f;
                if (p.Kind == PropKind.Chest && p.Open && p.OpenT >= ChestStayOpen)
                {
                    propsGone.Add(p);
                    continue;
                }
                if (p.Hp > 0f && (Smashable(p) || p.Kind == PropKind.Chest && !p.Open))
                    foreach (var pl in Players.Values)
                    {
                        if (pl.Downed || !PropCovers(p, pl.Pos, pl.Def.HitR + PropPad + (p.Kind == PropKind.Chest ? ChestTouch : 0f)))
                            continue;
                        if (Smashable(p))
                            p.Hp = 0f;
                        else
                            OpenChest(p);
                        break;
                    }
                if (p.Hp <= 0f)
                    propsGone.Add(p);
            }
            foreach (var p in propsGone)
            {
                Props.Remove(p.Id);
                BreakProp(p);
            }
            if (Loots.Count > 0)
            {
                lootGone.Clear();
                foreach (var l in Loots.Values)
                    if (!Players.ContainsKey(l.Owner))
                        lootGone.Add(l.Id);
                foreach (int id in lootGone)
                    Loots.Remove(id);
            }
        }
        readonly List<int> lootGone = new List<int>();

        void BreakProp(Prop p)
        {
            switch (p.Kind)
            {
                case PropKind.Crate:
                case PropKind.HeavyCrate:
                    BroadcastFx(FxKind.Debris, p.Pos, p.Kind == PropKind.HeavyCrate ? new Rgba(0.6f, 0.5f, 0.4f) : new Rgba(0.78f, 0.56f, 0.3f), HalfW(p) * 1.6f, 0.5f, Sfx.Clang);
                    if (rng.NextDouble() < 0.1)
                        SpawnOrb(p.Pos, false);
                    break;
                case PropKind.Pot:
                    BroadcastFx(FxKind.Debris, p.Pos, new Rgba(0.82f, 0.48f, 0.32f), 26f, PotFxTime, Sfx.Hit);
                    if (rng.NextDouble() < 0.35)
                        SpawnOrb(p.Pos, false);
                    else
                    {
                        GrantXp(3f + Wave * 0.5f);
                        BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.55f, 0.85f, 1f), 26f, 0.4f, Sfx.Pickup);
                    }
                    break;
                case PropKind.Barrel:
                    ExplodeBarrel(p);
                    break;
                case PropKind.HealthBarrel:
                    BroadcastFx(FxKind.Debris, p.Pos, new Rgba(0.35f, 0.75f, 0.4f), 30f, 0.45f, Sfx.PickupBig);
                    for (int i = 0; i < BossBarrelOrbs; i++)
                        SpawnOrb(p.Pos + V2.FromAngle(rng.Next(360) * MathX.Pi / 180f) * 14f, false);
                    break;
                case PropKind.Chest:
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.85f, 0.4f, 0.6f), 34f, 0.4f, Sfx.None);
                    break;
            }
        }

        void ExplodeBarrel(Prop b)
        {
            BroadcastFx(FxKind.Boom, b.Pos, new Rgba(1f, 0.55f, 0.15f), BarrelBlast, 0.5f, Sfx.BigExplode);
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(b.Pos) < BarrelBlast + e.Radius)
                    HurtEnemy(e, BarrelDamage(Wave), b.LitBy);
            foreach (var pl in Players.Values)
                if (!pl.Downed && pl.Pos.DistanceTo(b.Pos) < BarrelBlast + pl.Def.HitR)
                    Damage(pl, BarrelHurt, "Barrel");
            foreach (var p in Props.Values)
            {
                if (p.Hp <= 0f || !PropCovers(p, b.Pos, BarrelBlast))
                    continue;
                if (p.Kind == PropKind.Barrel)
                    LightBarrel(p, b.LitBy, BarrelChainFuse);
                else if (p.Kind != PropKind.Chest)
                    p.Hp = 0f;  // crates and pots are blown apart (next tick)
            }
        }

        // --- The chest and its items ----------------------------------------------------------------

        void OpenChest(Prop c)
        {
            c.Open = true;
            c.OpenT = 0f;
            BroadcastFx(FxKind.Ring, c.Pos, new Rgba(1f, 0.85f, 0.35f), 70f, 0.6f, Sfx.LevelUp);
            int n = Players.Count, i = 0;
            foreach (var pl in Players.Values)
            {
                int u = RollChestLoot(pl);
                if (u >= 0)
                {
                    V2 at = ClampToArena(c.Pos + new V2((i - (n - 1) / 2f) * 46f, ChestH / 2f + 34f), 20f);
                    var l = new Loot { Id = nextLootId++, Owner = pl.Id, Upgrade = u, Pos = at };
                    Loots[l.Id] = l;
                }
                i++;
            }
            DoLog("chest opened");
        }

        /// What a chest holds for this player: sometimes a legendary (from a boss met so far, or the next one), else an
        /// uncommon to very rare card from their own pool.
        int RollChestLoot(Player p)
        {
            if (rng.NextDouble() < ChestLegendaryChance)
            {
                int cycles = Math.Max(1, Wave / 5 + 1);
                var boss = BossForWave(5 * (1 + rng.Next(cycles)));
                if (IsReaper(boss))
                    boss = EnemyType.TwinA;
                var offer = Upgrades.LegendaryOffer(rng, boss, p.Role, p.RoleAbility, p.Taken);
                int u = offer[rng.Next(offer.Length)];
                if (Upgrades.Available(u, p.Role, p.RoleAbility, p.Taken) && Upgrades.All[u].Rarity == Rarity.Legendary)
                    return u;
            }
            double roll = rng.NextDouble();
            var want = roll < 0.2 ? Rarity.Uncommon : roll < 0.65 ? Rarity.Rare : Rarity.VeryRare;
            if (want == Rarity.Uncommon && HasPerk(p, PerkId.Contraband))
                want = Rarity.Rare;  // Contraband: rare or better
            return Upgrades.RandomOfRarity(rng, want, p.Role, p.RoleAbility, p.Taken, p.Weapon.Id);
        }

        /// Host: does the wave have to wait (an unopened chest, or an item nobody has dealt with)?
        public bool PropsHoldWave()
        {
            foreach (var p in Props.Values)
                if (p.Kind == PropKind.Chest && !p.Open && p.Hp > 0f)
                    return true;
            foreach (var l in Loots.Values)
                if (Players.ContainsKey(l.Owner))
                    return true;
            return false;
        }

        /// The enemies are gone but the treasure isn't: anyone down gets back up to go and collect.
        void HoldForTreasure()
        {
            foreach (var p in Players.Values)
                if (p.Downed)
                    RevivePlayer(p, 0.4f);
        }

        /// Your item, if you're standing on it.
        public Loot MyLootInReach
        {
            get
            {
                var me = Me;
                if (me == null || me.Downed)
                    return null;
                foreach (var l in Loots.Values)
                    if (l.Owner == MyId && l.Pos.DistanceTo(me.Pos) < LootReach + me.Def.BodyR)
                        return l;
                return null;
            }
        }

        /// Your item still waiting somewhere, if any.
        public Loot MyLoot
        {
            get
            {
                foreach (var l in Loots.Values)
                    if (l.Owner == MyId)
                        return l;
                return null;
            }
        }

        /// The local player takes (or throws away) the item they're standing on.
        public void ResolveLoot(bool take)
        {
            var l = MyLootInReach;
            if (l == null)
                return;
            Sounds.Add(take ? Sfx.Pick : Sfx.Hit);
            if (IsHost)
            {
                HostLoot(MyId, l.Id, take);
                return;
            }
            var w = Proto.Begin(Msg.Loot);
            w.Write(l.Id);
            w.Write(take);
            session.SendToHost(Proto.End(w));
            Loots.Remove(l.Id);  // gone from your screen now; the host confirms in the next snapshot
            lootSent.Add(l.Id);
        }

        readonly HashSet<int> lootSent = new HashSet<int>();  // client: items you've dealt with (not shown again while the host catches up)

        public void OnLoot(int peer, BinaryReader r)
        {
            int id = r.ReadInt32();
            HostLoot(peer, id, r.ReadBoolean());
        }

        void HostLoot(int playerId, int lootId, bool take)
        {
            Player p;
            Loot l;
            if (!IsHost || !Players.TryGetValue(playerId, out p) || !Loots.TryGetValue(lootId, out l) || l.Owner != playerId)
                return;
            if (l.Pos.DistanceTo(p.Pos) > LootReach + p.Def.BodyR + 80f)
                return;  // (a little slack for the client's position arriving late)
            Loots.Remove(lootId);
            var u = Upgrades.All[l.Upgrade];
            if (take && Upgrades.Available(l.Upgrade, p.Role, p.RoleAbility, p.Taken, p.Weapon.Id))
            {
                Recorder?.Picked(this, p, new[] { l.Upgrade }, l.Upgrade, "chest");
                GrantUpgrade(playerId, l.Upgrade);
                BroadcastFx(FxKind.Ring, p.Pos, Upgrades.RarityColor(u.Rarity), 60f, 0.5f, Sfx.None);
                DoLog(p.Role + " took " + u.Name + " from a chest");
            }
            else
            {
                BroadcastFx(FxKind.Ring, l.Pos, new Rgba(0.6f, 0.6f, 0.65f, 0.6f), 26f, 0.3f, Sfx.None);
                DoLog(p.Role + " discarded " + u.Name + " from a chest");
            }
            SendSnapshot();
        }

        /// Bots: once the enemies are down, open the chest and take their item.
        void BotTreasure(Player me, ref V2 move)
        {
            if (State != MatchState.Fighting || Foes() > 0 || me.Downed)
                return;
            V2? goal = null;
            var mine = MyLoot;
            if (mine != null)
            {
                if (MyLootInReach != null)
                {
                    if (MathX.Fmod(botT, 0.5f) < tickDt + 0.001f)
                        ResolveLoot(true);
                    move = V2.Zero;
                    return;
                }
                goal = mine.Pos;
            }
            else
                foreach (var p in Props.Values)
                    if (p.Kind == PropKind.Chest && !p.Open)
                        goal = p.Pos;
            if (goal.HasValue)
                move = (goal.Value - me.Pos).LimitLength(1f);
        }

        /// Every game, each frame: ages the props and items for their looks.
        void UpdatePropView(float dt)
        {
            foreach (var p in Props.Values)
            {
                p.Age += dt;
                p.HitT = Math.Max(0f, p.HitT - dt);
                if (p.Open)
                    p.OpenT += dt;
            }
            foreach (var l in Loots.Values)
                l.Age += dt;
        }

        // --- Sync -----------------------------------------------------------------------------------

        void SyncProps(NetSync s)
        {
            int n = Props.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                snapSeen.Clear();
                for (int i = 0; i < n; i++)
                    snapSeen.Add(SyncProp(s, null));
                RemoveUnseen(Props);
            }
            else
                foreach (var p in Props.Values)
                    SyncProp(s, p);
            n = Loots.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                snapSeen.Clear();
                for (int i = 0; i < n; i++)
                    snapSeen.Add(SyncLoot(s, null));
                RemoveUnseen(Loots);
            }
            else
                foreach (var l in Loots.Values)
                    SyncLoot(s, l);
        }

        int SyncProp(NetSync s, Prop p)
        {
            int id = p != null ? p.Id : 0, kind = p != null ? (int)p.Kind : 0, seed = p != null ? p.Seed : 0;
            V2 pos = p != null ? p.Pos : default(V2);
            float hp = p != null ? p.Hp : 0f, max = p != null ? p.MaxHp : 0f, fuse = p != null ? p.Fuse : 0f;
            bool open = p != null && p.Open;
            s.Int(ref id);
            s.Byte(ref kind);
            s.Byte(ref seed);
            s.Vec(ref pos);
            s.Float(ref hp);
            s.Float(ref max);
            s.Float(ref fuse);
            s.Bool(ref open);
            if (s.Reading)
            {
                if (!Props.TryGetValue(id, out p))
                    Props[id] = p = new Prop { Id = id, Kind = (PropKind)kind, Seed = seed, Hp = hp };
                if (hp < p.Hp)
                    p.HitT = 0.15f;
                if (open && !p.Open)
                    p.OpenT = 0f;
                p.Pos = pos;
                p.Hp = hp;
                p.MaxHp = max;
                p.Fuse = fuse;
                p.Open = open;
            }
            return id;
        }

        int SyncLoot(NetSync s, Loot l)
        {
            int id = l != null ? l.Id : 0, owner = l != null ? l.Owner : 0, u = l != null ? l.Upgrade : 0;
            V2 pos = l != null ? l.Pos : default(V2);
            s.Int(ref id);
            s.Int(ref owner);
            s.Short(ref u);
            s.Vec(ref pos);
            if (s.Reading && lootSent.Contains(id))
                return id;
            if (s.Reading)
            {
                if (!Loots.TryGetValue(id, out l))
                    Loots[id] = l = new Loot { Id = id };
                l.Owner = owner;
                l.Upgrade = u;
                l.Pos = pos;
            }
            return id;
        }
    }
}
