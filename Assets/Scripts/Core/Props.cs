using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Things standing in the arena during regular waves (Props.cs):
    /// Crate / HeavyCrate: cover. Enemy shots stop at it and wear it down; your shots fly over it. Solid to walk into.
    /// Barrel: rare. Any shot sets it fizzing; 0.4 s later it blows up, hurting enemies (a lot), players (a little),
    ///   smashing crates and setting off other barrels.
    /// Chest: rare treasure. Touch it to open it: every player gets their own item (an upgrade card or a legendary),
    ///   which only they can see. Walk onto it, then take it (G) or discard it (X). The wave waits for both.
    /// HealthBarrel: boss fights only. A green barrel with a white cross drops in now and then; shoot it or walk
    ///   into it and it breaks into health orbs.
    /// Pot is retired (no longer placed or drawn); its slot stays so the wire numbers don't move.
    public enum PropKind : byte { Crate, HeavyCrate, Pot, Barrel, Chest, HealthBarrel }

    /// What a chest holds, and so how it looks (Game/ChestLooks.cs). A wave's chest is Upgrade (everyone's own card);
    /// an Expanding chest node rolls Gold (a burst of coins), Piece chest (a piece each), Buildables (one Black Market build each) or Upgrade.
    /// Vector and Relic are the vector chest and relic nodes' looks (they are nodes, not props). New kinds go at the end.
    public enum ChestKind : byte { Upgrade, Gold, Vector, Relic, Piece, Buildable, Gadget }

    public class Prop
    {
        public int Id;
        public PropKind Kind;
        /// Where it stands. Crossing a cell of the prop grid tells every game's grid to rebuild (PropGrid.cs).
        public V2 Pos
        {
            get { return pos; }
            set
            {
                pos = value;
                int c = Game.PropCell(value);
                if (c != GridCell)
                {
                    GridCell = c;
                    System.Threading.Interlocked.Increment(ref Game.PropStamp);
                }
            }
        }
        V2 pos;
        internal int GridCell = int.MinValue, GridOrder;  // its cell and its place in Props' order (PropGrid.cs)
        public float Hp, MaxHp;  // crates: enemy hits left; the rest: 1 until broken
        public float Fuse;       // a barrel: time left before it blows (0 = not lit)
        public bool Open;        // a chest that's been opened
        public ChestKind Chest;  // a chest: what it holds (in the snapshot)
        public int Seed;         // look variety
        public int LitBy;        // host: the player who set a barrel off (kill credit)
        // Every game, for the look.
        public float Age, HitT, OpenT;
        public float PushT;      // seconds left of "it's being pushed" (dust, a shudder)
        public V2 PushDir, SeenAt;  // the way it last slid, and where we last saw it
        public readonly List<Scuff> Scuffs = new List<Scuff>();  // drag lines left on the floor behind it (Game/PropsLook.cs)
    }

    /// One short drag line scratched into the floor by a pushed prop; it fades over ScuffLife seconds.
    public struct Scuff
    {
        public V2 A, B;
        public float Age;
    }

    /// What a chest item is: an upgrade card (Upgrade), a Black Market build for your stash (Build: Item is the Market kind), or a
    /// piece or service card for your hand (Card: Item is its Piece). New values go at the end.
    public enum LootKind : byte { Upgrade, Build, Card }

    /// A chest's item for one player. Everyone's game has every item, but only its owner's draws it. Its owner takes it or throws it away.
    public class Loot
    {
        public int Id, Owner, Upgrade;
        public LootKind Kind;
        public int Item;  // a Build's Market kind, a Card's Piece (Upgrade uses Upgrade)
        public V2 Pos;
        public float Age;  // every game, for the look
    }

    public partial class Game
    {
        public const float CrateSize = 44f, HeavyCrateSize = 48f, BarrelR = 17f, ChestW = 48f, ChestH = 34f;
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
        /// measure exact hits aren't thrown by a crate in the way; the run and props tests turn them on.
        public bool PropsOn = PropsByDefault;
        public static bool PropsByDefault = true;

        // --- Numbers by wave ------------------------------------------------------------------------

        /// Crates (cover) on the field in a regular wave: more as the bullets thicken.
        public static int CrateTarget(int wave) => Math.Min(10, 2 + (int)(wave * 0.3f));
        /// The Cluttered modifier: a maze of crates, growing every wave (they stay until broken; bosses clear them).
        public static int ClutterTarget(int wave) => Math.Min(48, 14 + wave * 3);
        public static float BarrelChance(int wave) => Math.Min(0.45f, 0.25f + wave * 0.005f);
        /// Enemy hits a crate takes before it breaks.
        public static float CrateHits(PropKind k, int wave) => (8f + wave / 3) * (k == PropKind.HeavyCrate ? 2f : 1f);
        public static float BarrelDamage(int wave) => 40f + 10f * wave;

        // --- Shapes ---------------------------------------------------------------------------------

        public static bool Round(Prop p) => p.Kind == PropKind.Barrel || p.Kind == PropKind.HealthBarrel;
        public static float PropRadius(Prop p) => BarrelR;
        public static float HalfW(PropKind k) => k == PropKind.Crate ? CrateSize / 2f : k == PropKind.HeavyCrate ? HeavyCrateSize / 2f : k == PropKind.Chest ? ChestW / 2f : BarrelR;
        public static float HalfH(PropKind k) => k == PropKind.Chest ? ChestH / 2f : HalfW(k);
        public static float HalfW(Prop p) => HalfW(p.Kind);
        public static float HalfH(Prop p) => HalfH(p.Kind);
        public static bool IsCrate(Prop p) => p.Kind == PropKind.Crate || p.Kind == PropKind.HeavyCrate;
        /// You can't walk through it (health barrels smash instead; an open chest is just treasure).
        public static bool Solid(Prop p) => p.Kind != PropKind.HealthBarrel && !(p.Kind == PropKind.Chest && p.Open);
        /// Breaks when you walk into it or shoot it (your shots carry on).
        static bool Smashable(Prop p) => p.Kind == PropKind.HealthBarrel;

        public static bool PropCovers(Prop p, V2 pos, float pad)
        {
            if (Round(p))
                return pos.CloserThan(p.Pos, PropRadius(p) + pad);
            return Math.Abs(pos.X - p.Pos.X) < HalfW(p) + pad && Math.Abs(pos.Y - p.Pos.Y) < HalfH(p) + pad;
        }

        /// Moves something of radius r out of every solid prop (out the nearest side of a box).
        public V2 PushOutOfProps(V2 pos, float r)
        {
            // Only the props near pos, in Props' order; if a push carries pos past the margin, the rest of the props
            // are checked in full from there (the same props in the same order as a full pass: PropGrid.cs).
            var near = PropsNear(pos, r + PropOutMargin, pushNear);
            V2 from = pos;
            for (int i = 0; i < near.Count; i++)
            {
                pos = PushOutOfProp(near[i], pos, r);
                if (pos.FartherThan(from, PropOutMargin))
                {
                    var all = PropOrder();
                    for (int j = near[i].GridOrder + 1; j < all.Count; j++)
                        pos = PushOutOfProp(all[j], pos, r);
                    break;
                }
            }
            return PushOutOfSolids(pos, r);  // and the layout's walls and pillars (LayoutRules.cs)
        }

        V2 PushOutOfProp(Prop p, V2 pos, float r)
        {
            {
                if (!Solid(p) || p.Hp <= 0f || !PropCovers(p, pos, r))
                    return pos;
                if (Round(p))
                {
                    V2 away = pos - p.Pos;
                    return p.Pos + (away.Length > 0.5f ? away.Normalized() : new V2(1f, 0f)) * (PropRadius(p) + r);
                }
                float hw = HalfW(p) + r, hh = HalfH(p) + r;
                float l = pos.X - (p.Pos.X - hw), rr = p.Pos.X + hw - pos.X, t = pos.Y - (p.Pos.Y - hh), b = p.Pos.Y + hh - pos.Y;
                float m = Math.Min(Math.Min(l, rr), Math.Min(t, b));
                return m == l ? new V2(pos.X - l, pos.Y) : m == rr ? new V2(pos.X + rr, pos.Y) : m == t ? new V2(pos.X, pos.Y - t) : new V2(pos.X, pos.Y + b);
            }
        }

        // --- Pushing (host) -------------------------------------------------------------------------

        /// How fast (px/s) each kind of player shoves a crate or barrel along: Tanks fastest, Dps next, Healers slowest.
        /// Enemies barely move them. Even the Healer is ten times the enemies. A heavy crate gives at 60%.
        public const float PushTank = 70f, PushDps = 50f, PushHealer = 34f, PushEnemy = 3f, HeavyPushMult = 0.6f;
        public const float ScuffLife = 3f;  // seconds a drag line stays on the floor
        const float PushContact = 2f;      // a remote player counts as leaning on a prop this close (their own game already holds them out)
        const float PushLeanSpeed = 15f;   // ...if they're moving toward it at least this fast (px/s)

        public static float PushSpeed(Player p) => p.Role == Role.Tank ? PushTank : p.Role == Role.Healer ? PushHealer : PushDps;
        /// Crates and barrels slide; chests and health barrels stay put.
        public static bool Pushable(Prop p) => p.Hp > 0f && (p.Kind == PropKind.Crate || p.Kind == PropKind.HeavyCrate || p.Kind == PropKind.Barrel);

        /// Host: something of radius r at pos is leaning into crates and barrels: each one it overlaps (or touches, within
        /// reach) slides away along the way it is being pushed, by at most speed × dt and never by more than the overlap.
        /// The rest of the overlap is then pushed back out of the body by PushOutOfProps, so the pusher is held to the
        /// prop's pace. heading (a remote player's smoothed motion) must point at the prop, or zero to skip that check.
        public void ShoveProps(V2 pos, float r, float speed, float dt, float reach, V2 heading)
        {
            if (Props.Count == 0 || speed <= 0f)
                return;
            foreach (var p in PropsNear(pos, r + reach, shoveNear))  // (MoveProp leaves the grid as it was: these are a copy)
            {
                if (!Pushable(p))
                    continue;
                V2 dir;
                float pen;
                if (Round(p))
                {
                    V2 d = p.Pos - pos;
                    float dist = d.Length;
                    pen = PropRadius(p) + r + reach - dist;
                    dir = dist > 0.5f ? d / dist : (heading.IsZero ? new V2(1f, 0f) : heading.Normalized());
                }
                else
                {
                    float dx = p.Pos.X - pos.X, dy = p.Pos.Y - pos.Y;
                    float px = HalfW(p) + r + reach - Math.Abs(dx), py = HalfH(p) + r + reach - Math.Abs(dy);
                    if (px <= 0f || py <= 0f)
                        continue;
                    if (px < py)
                    {
                        pen = px;
                        dir = new V2(dx < 0f ? -1f : 1f, 0f);
                    }
                    else
                    {
                        pen = py;
                        dir = new V2(0f, dy < 0f ? -1f : 1f);
                    }
                }
                if (pen <= 0f || !heading.IsZero && heading.Dot(dir) < PushLeanSpeed)
                    continue;
                float amt = Math.Min(pen, speed * (p.Kind == PropKind.HeavyCrate ? HeavyPushMult : 1f) * dt);
                MoveProp(p, dir * amt);
            }
        }

        const float PropLook = 70f;  // how far ahead (px) an enemy looks for a crate or barrel in its way

        /// Would something of radius r walking from `from` along dir, for PropLook px, run into a crate or barrel?
        /// near: the props within PropLook + r of from (PropsNear).
        static bool PropAhead(List<Prop> near, V2 from, V2 dir, float r)
        {
            for (int i = 1; i <= 5; i++)
            {
                V2 pt = from + dir * (PropLook * i / 5f);
                foreach (var p in near)
                    if (Pushable(p) && PropCovers(p, pt, r))
                        return true;
            }
            return false;
        }

        /// Host: which way a walking enemy heads with crates and barrels about. Straight on if nothing is in the way;
        /// otherwise the nearest turn (up to a little past sideways) that is clear of props and walls. If no way round
        /// exists, it keeps going straight and shoves the prop.
        V2 PropDir(Enemy e, V2 dir)
        {
            float r = e.Radius * 0.8f + 2f;
            if (dir.IsZero)
                return dir;
            var near = PropsNear(e.Pos, PropLook + r, aheadNear);
            if (near.Count == 0 || !PropAhead(near, e.Pos, dir, r))
                return dir;
            var layout = LayoutOn ? ActiveLayout : null;
            float first = (e.Id & 1) == 0 ? 1f : -1f;  // each enemy keeps to its own side, so they don't dither
            for (int i = 1; i <= 3; i++)
                for (int side = 0; side < 2; side++)
                {
                    V2 d = dir.Rotated(first * (side == 0 ? 1f : -1f) * 0.55f * i);
                    if (!PropAhead(near, e.Pos, d, r) && (layout == null || !SegBlocked(layout, e.Pos, e.Pos + d * PropLook, r)))
                        return d;
                }
            return dir;
        }

        /// Slides a prop by delta unless that puts it in a wall, the arena's edge or another solid prop (it stops there).
        bool MoveProp(Prop p, V2 delta)
        {
            float hw = HalfW(p), hh = HalfH(p);
            V2 to = (p.Pos + delta).Clamp(ArenaMin + new V2(hw, hh), ArenaMax - new V2(hw, hh));
            V2 fit = PushOutOfSolids(to, Math.Min(hw, hh));
            if (fit.FartherThan(to, 0.01f))
                return false;
            foreach (var q in Props.Values)
                if (q != p && Solid(q) && q.Hp > 0f && Math.Abs(to.X - q.Pos.X) < hw + HalfW(q) - 0.5f && Math.Abs(to.Y - q.Pos.Y) < hh + HalfH(q) - 0.5f)
                    return false;
            p.Pos = to;
            return true;
        }

        /// Host, each tick: players in other games lean on props where we can only see them touch (their own game holds
        /// them out, so there's no overlap). Our own player and the enemies shove from their own movement step.
        void ShoveFromRemotePlayers(float dt)
        {
            foreach (var pl in Players.Values)
                if (pl.Id != MyId && !pl.Downed)
                    ShoveProps(pl.Pos, pl.Def.HitR + PropPad, PushSpeed(pl), dt, PushContact, pl.Motion);
        }

        // --- Spawning (host) ------------------------------------------------------------------------

        /// At the start of each wave: bosses get a clear arena; regular waves top the crates and barrels up to the
        /// wave's numbers (what's left from the last wave stays), and now and then bring a chest.
        void WaveProps()
        {
            if (!PropsOn || !ModeRules.Extras || ShowcaseMode)
                return;
            if (BossNow)
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
            int crates = 0;
            foreach (var p in Props.Values)
                if (IsCrate(p))
                    crates++;
            bool clutter = Has(RunMod.Cluttered);
            int crateGoal = clutter ? ClutterTarget(Wave) : CrateTarget(Wave);
            for (int tries = 0; crates < crateGoal && tries < (clutter ? 300 : 20); tries++)
            {
                var kind = Wave >= (clutter ? 8 : 12) && rng.NextDouble() < 0.35 ? PropKind.HeavyCrate : PropKind.Crate;
                V2 at;
                if (!FreeSpot(HalfW(kind), out at, true))  // (on a square of the floor grid, RoomTraps.Tile)
                    break;
                AddProp(kind, at);
                crates++;
                // Often a second one alongside: a little wall to hide behind.
                if (crates < crateGoal && rng.NextDouble() < 0.45)
                {
                    const float step = RoomTraps.Tile;  // (the next square over)
                    V2 next = at + (rng.Next(2) == 0 ? new V2(rng.Next(2) == 0 ? step : -step, 0f) : new V2(0f, rng.Next(2) == 0 ? step : -step));
                    if (SpotClear(next, HalfW(kind), 0f))
                    {
                        AddProp(kind, next);
                        crates++;
                    }
                }
            }
            if (Wave >= 3 && rng.NextDouble() < BarrelChance(Wave))  // (the first waves are melee rushers: nothing to blow up yet)
                TryAdd(PropKind.Barrel);
            if (Wave >= 20 && rng.NextDouble() < 0.15)
                TryAdd(PropKind.Barrel);
            bool chest = Wave >= 2 && rng.NextDouble() < ChestChance * Pace;  // (a Campaign: half as often, twice the waves)
            if (!chest && ContrabandChest())
            {
                chest = true;  // Contraband: the first wave after each boss always has one (Contraband.cs)
                DoLog("Contraband: a cycle chest");
            }
            if (chest)
                TrySpawnChest(ChestKind.Upgrade);
        }

        void TryAdd(PropKind kind)
        {
            V2 at;
            if (FreeSpot(Math.Max(HalfW(kind), HalfH(kind)), out at))
                AddProp(kind, at);
        }

        /// Host: what spawned a chest and what to do about it (not synced, not saved: a source that matters spawns its chest again, as a node does).
        class ChestSource
        {
            public Action<Prop> OnOpened;  // called once, as the chest opens
            public bool HoldsWave = true;  // an unopened chest holds the wave (PropsHoldWave); a node's never does
            public int Seed = -1;          // a piece chest's cards are seeded from this (else the chest's id)
        }

        readonly Dictionary<int, ChestSource> chestSources = new Dictionary<int, ChestSource>();

        /// Host: **the one way to make a chest** (Docs/Chests.md): a treasure chest holding `kind`, for everyone's own item. `onOpened` runs once as it
        /// opens (a node spends itself with it); `holdsWave` false lets the wave end with it still shut.
        public Prop SpawnChest(V2 pos, ChestKind kind, Action<Prop> onOpened = null, bool holdsWave = true, int seed = -1)
        {
            var c = AddProp(PropKind.Chest, pos);
            c.Chest = kind;
            if (onOpened != null || !holdsWave || seed >= 0)
                chestSources[c.Id] = new ChestSource { OnOpened = onOpened, HoldsWave = holdsWave, Seed = seed };
            return c;
        }

        /// Host: a chest at a free spot in the arena (a wave's drop, an Ambush's reward). Null if there is no room.
        Prop TrySpawnChest(ChestKind kind, Action<Prop> onOpened = null)
        {
            V2 at;
            return FreeSpot(Math.Max(HalfW(PropKind.Chest), HalfH(PropKind.Chest)), out at) ? SpawnChest(at, kind, onOpened) : null;
        }

        /// Host: the unopened chest standing at this spot, if any.
        Prop ChestAt(V2 pos)
        {
            foreach (var p in Props.Values)
                if (p.Kind == PropKind.Chest && !p.Open && p.Pos.DistanceTo(pos) < 1f)
                    return p;
            return null;
        }

        /// Dev and tests: put a prop somewhere.
        public Prop AddProp(PropKind kind, V2 at)
        {
            var p = new Prop { Id = nextPropId++, Kind = kind, Pos = at, Seed = rng.Next(256) };
            p.Hp = p.MaxHp = IsCrate(p) ? CrateHits(kind, Wave) : 1f;
            Props[p.Id] = p;
            return p;
        }

        /// A random free spot; onGrid: the middle of a square of the 50 px floor grid (wave crates, so they line up with the core plans').
        bool FreeSpot(float size, out V2 at, bool onGrid = false)
        {
            for (int i = 0; i < 40; i++)
            {
                at = RandomInArena(110f);
                if (onGrid)
                    at = new V2(RoomTraps.SnapCentre(at.X, RoomTraps.Tile / 2f), RoomTraps.SnapCentre(at.Y, RoomTraps.Tile / 2f));
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
            if (RoomsOn)
                foreach (var pt in CurrentWorld.Portals)  // and off the portals and the floor in front of them
                {
                    float keep = (pt.Wall ? pt.Half + 60f : CurrentWorld.PortalR + CurrentWorld.PortalExit) + size;
                    if (at.CloserThan(pt.A, keep) || at.CloserThan(pt.B, keep))
                        return false;
                }
            foreach (var pl in Players.Values)
                if (pl.Pos.CloserThan(at, playerGap + size))
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
            chestSources.Clear();
            Loots.Clear();
        }

        void ResetProps()
        {
            Props.Clear();
            chestSources.Clear();
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
            if (!PropsOn || !ModeRules.Extras || ShowcaseMode || State != MatchState.Fighting || !BossNow)
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
                if (at.CloserThan(boss.Pos, boss.Radius + 120f) || !SpotClear(at, BarrelR, 120f))
                    continue;
                AddProp(PropKind.HealthBarrel, at);
                return;
            }
        }

        // --- Hits (host) ----------------------------------------------------------------------------

        /// In Collide, for each bullet: does a prop stop it? Crates stop enemy shots (each one wears it down), barrels
        /// stop every shot and go off, health barrels smash under your shots (which carry on).
        bool PropStops(Bullet b)
        {
            bool enemy = Defs.IsEnemyShot(b.Kind);
            foreach (var p in PropsNear(b.Pos, b.Radius, stopNear))
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

        /// Your blasts (grenades, Explosive Rounds, flak) set barrels off and smash health barrels in reach.
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

        /// Host, each tick: fuses, touches (health barrels smash, chests open), breaks and blasts; items whose owner left go.
        void TickProps(float dt)
        {
            if (Props.Count == 0 && Loots.Count == 0)
                return;
            propsGone.Clear();
            ShoveFromRemotePlayers(dt);
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
                        SpawnOrbEach(p.Pos, false);
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
            foreach (var e in FoesInCircle(b.Pos, BarrelBlast, by: b.LitBy))
                HurtEnemy(e, BarrelDamage(Wave), b.LitBy);
            foreach (var pl in Players.Values)
                if (!pl.Downed && pl.Pos.CloserThan(b.Pos, BarrelBlast + pl.Def.HitR))
                    Damage(pl, BarrelHurt, "Barrel");
            foreach (var p in Props.Values)
            {
                if (p.Hp <= 0f || !PropCovers(p, b.Pos, BarrelBlast))
                    continue;
                if (p.Kind == PropKind.Barrel)
                    LightBarrel(p, b.LitBy, BarrelChainFuse);
                else if (p.Kind != PropKind.Chest)
                    p.Hp = 0f;  // crates are blown apart (next tick)
            }
        }

        // --- The chest and its items ----------------------------------------------------------------

        /// remote: collected from the wave box's Chests tab (RemoteChests.cs): nothing lands at the chest; gold goes straight into each
        /// wallet and each player's card lands at their own feet.
        void OpenChest(Prop c, bool remote = false)
        {
            c.Open = true;
            c.OpenT = 0f;
            ChestSource src;
            chestSources.TryGetValue(c.Id, out src);
            int seedIdx = src != null ? src.Seed : -1;  // (a piece chest's cards are seeded from its node)
            if (src != null)
                chestSources.Remove(c.Id);
            src?.OnOpened?.Invoke(c);
            BroadcastFx(FxKind.Ring, c.Pos, ChestFxInk(c.Chest), 70f, 0.6f, Sfx.LevelUp);
            switch (c.Chest)
            {
                case ChestKind.Gold:
                    if (remote)
                        foreach (var pl in Players.Values)
                            pl.Gold += ChestGold + ChestGoldPerWave * Math.Max(1, Wave);
                    else
                        DropGoldBurst(c.Pos, ChestGold + ChestGoldPerWave * Math.Max(1, Wave));  // (each player's own burst, the same gold each as before)
                    break;
                case ChestKind.Vector:  // bursts into vector pickups (they fly to the nearest player), or straight into each stock
                    if (remote)
                        foreach (var pl in Players.Values)
                            AddVectors(pl, VectorChestPickups * VectorDropSmall);
                    else
                        for (int k = 0; k < VectorChestPickups; k++)
                            DropVectorAt(c.Pos, VectorDropSmall);
                    break;
                case ChestKind.Buildable:  // a build card each (a kind they still have room for), to take or throw away
                {
                    int n = Players.Count, i = 0;
                    foreach (var pl in Players.Values)
                    {
                        int kind = RollChestBuild(pl);
                        if (kind >= 0)
                            AddChestLoot(c, pl, i, n, remote, LootKind.Build, kind);
                        i++;
                    }
                    break;
                }
                case ChestKind.Gadget:  // a gadget each, into the hand or the bag (Gadgets.cs)
                    foreach (var pl in Players.Values)
                        GiveChestGadget(pl, remote ? pl.Pos : c.Pos);
                    break;
                case ChestKind.Piece:  // a piece each (Grid runs), to take or throw away
                {
                    if (!ExpandingOn)
                        break;
                    int n = Players.Count, i = 0;
                    foreach (var pl in Players.Values)
                        AddChestLoot(c, pl, i++, n, remote, LootKind.Card, (int)ChestCardFor(pl, seedIdx >= 0 ? seedIdx : c.Id));
                    break;
                }
                default:
                {
                    int n = Players.Count, i = 0;
                    foreach (var pl in Players.Values)
                    {
                        int u = RollChestLoot(pl);
                        if (u >= 0)
                            AddChestLoot(c, pl, i, n, remote, LootKind.Upgrade, u);
                        i++;
                    }
                    break;
                }
            }
            DoLog(c.Chest + " chest opened");
        }

        /// A Gold chest's payout per player (spilled as coins anyone can pick up), and how many coins it spills.
        public const int ChestGold = 40, ChestGoldPerWave = 4, ChestGoldCoins = 14;

        /// A chest item's colour: an upgrade's rarity, the Buildables pink, the piece orange.
        public static Rgba LootInk(Loot l) => l.Kind == LootKind.Build ? new Rgba(1f, 0.4f, 0.72f) : l.Kind == LootKind.Card ? new Rgba(1f, 0.55f, 0.3f)
            : l.Upgrade >= 0 && l.Upgrade < Upgrades.All.Length ? Upgrades.RarityColor(Upgrades.All[l.Upgrade].Rarity) : new Rgba(0.4f, 1f, 0.5f);

        static Rgba ChestFxInk(ChestKind k) => k == ChestKind.Gold ? new Rgba(1f, 0.85f, 0.35f) : k == ChestKind.Vector ? new Rgba(0.3f, 0.88f, 1f) : k == ChestKind.Piece ? new Rgba(1f, 0.55f, 0.3f) : k == ChestKind.Gadget ? new Rgba(0.92f, 0.2f, 0.22f)
            : k == ChestKind.Buildable ? new Rgba(1f, 0.4f, 0.72f) : new Rgba(0.4f, 1f, 0.5f);

        /// Host: a Gold chest's coins, `perPlayer` gold for each player (their own copy), spread in a ring round pos (ChestGoldCoins
        /// coins each, the last carrying what's left).
        void DropGoldBurst(V2 pos, int perPlayer)
        {
            foreach (int owner in LootOwners())
            {
                int gold = perPlayer, each = Math.Max(1, gold / ChestGoldCoins);
                for (int k = 0; k < ChestGoldCoins && gold > 0; k++)
                {
                    int v = k == ChestGoldCoins - 1 ? gold : Math.Min(gold, each);
                    gold -= v;
                    var c = new Coin { Id = nextCoinId++, Value = v, Owner = owner };
                    float ang = k * MathX.Tau / ChestGoldCoins + (float)rng.NextDouble() * 0.3f;
                    c.Pos = ClampToArena(pos + V2.FromAngle(ang) * (34f + 30f * (float)rng.NextDouble()), 12f);
                    c.Disp = c.Pos;
                    Coins[c.Id] = c;
                }
            }
        }

        /// Host: player pl's item from chest c (the i-th of n), lined up under the chest, or at their own feet when it was collected remotely.
        void AddChestLoot(Prop c, Player pl, int i, int n, bool remote, LootKind kind, int item)
        {
            V2 at = remote ? ClampToArena(pl.Pos + new V2(0f, 50f), 20f)  // (at their own feet)
                : ClampToArena(c.Pos + new V2((i - (n - 1) / 2f) * 46f, ChestH / 2f + 34f), 20f);
            var l = new Loot { Id = nextLootId++, Owner = pl.Id, Kind = kind, Upgrade = kind == LootKind.Upgrade ? item : 0, Item = item, Pos = at };
            Loots[l.Id] = l;
        }

        /// Host: the Black Market build a Buildables chest offers this player: a random kind they still have room for (-1: their stash is full).
        int RollChestBuild(Player p)
        {
            int start = rng.Next(Market.Count);
            for (int k = 0; k < Market.Count; k++)
            {
                int kind = (start + k) % Market.Count;
                if (p.Stash[kind] < Market.StashMax)
                    return kind;
            }
            return -1;
        }

        /// A chest item's name ("Turret", "L piece", an upgrade's name).
        public static string LootName(Loot l) => l.Kind == LootKind.Build ? (l.Item >= 0 && l.Item < Market.Count ? Market.Names[l.Item] : "build")
            : l.Kind == LootKind.Card ? Expansion.CardName((PieceCard)l.Item) + " card"
            : l.Upgrade >= 0 && l.Upgrade < Upgrades.All.Length ? Upgrades.All[l.Upgrade].Name : "item";

        /// Host: player p takes chest item l; false when it no longer fits (an upgrade they can't have, a full stash or hand).
        bool TakeLoot(Player p, Loot l)
        {
            switch (l.Kind)
            {
                case LootKind.Build:
                    if (l.Item < 0 || l.Item >= Market.Count || p.Stash[l.Item] >= Market.StashMax)
                        return false;
                    p.Stash[l.Item]++;
                    DoLog(p.Role + " took a " + Market.Names[l.Item] + " from a chest");
                    return true;
                case LootKind.Card:
                    return GiveCard(p, (PieceCard)l.Item, "chest");
                default:
                    if (!Upgrades.Available(l.Upgrade, OfferFor(p)))
                        return false;
                    GrantUpgrade(p.Id, l.Upgrade);
                    DoLog(p.Role + " took " + Upgrades.All[l.Upgrade].Name + " from a chest");
                    return true;
            }
        }

        /// What a chest holds for this player: sometimes a legendary (from a boss met so far, or the next one), else an
        /// uncommon to very rare card from their own pool.
        int RollChestLoot(Player p)
        {
            if (rng.NextDouble() < ChestLegendaryChance)
            {
                int cycles = Math.Max(1, (Wave - 1) / 5 + 1);  // the bosses beaten, and the next
                var boss = BossSchedule.Campaign.BossAt(5 * (1 + rng.Next(cycles)));  // (the Campaign's order; Quick Play gives it the run's bosses)
                if (IsReaper(boss))
                    boss = EnemyType.ReaperOfDusk;
                var offer = Upgrades.LegendaryOffer(rng, boss, OfferFor(p));
                int u = offer[rng.Next(offer.Length)];
                if (Upgrades.Available(u, OfferFor(p)) && Upgrades.All[u].Rarity == Rarity.Legendary)
                    return u;
            }
            double roll = rng.NextDouble();
            var want = roll < 0.2 ? Rarity.Uncommon : roll < 0.65 ? Rarity.Rare : Rarity.VeryRare;
            if (want == Rarity.Uncommon && HasPerk(p, PerkId.Contraband))
                want = Rarity.Rare;  // Contraband: rare or better
            return Upgrades.RandomOfRarity(rng, want, OfferFor(p));
        }

        /// Host: does the wave have to wait (an unopened chest, or an item nobody has dealt with)?
        public bool PropsHoldWave()
        {
            foreach (var p in Props.Values)
                if (p.Kind == PropKind.Chest && !p.Open && p.Hp > 0f && (!chestSources.ContainsKey(p.Id) || chestSources[p.Id].HoldsWave))
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
                    if (l.Owner == MyId && l.Pos.CloserThan(me.Pos, LootReach + me.Def.BodyR))
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
            if (l.Pos.FartherThan(p.Pos, LootReach + p.Def.BodyR + 80f))
                return;  // (a little slack for the client's position arriving late)
            Loots.Remove(lootId);
            if (take && TakeLoot(p, l))
                BroadcastFx(FxKind.Ring, p.Pos, LootInk(l), 60f, 0.5f, Sfx.None);
            else
            {
                BroadcastFx(FxKind.Ring, l.Pos, new Rgba(0.6f, 0.6f, 0.65f, 0.6f), 26f, 0.3f, Sfx.None);
                DoLog(p.Role + " discarded " + LootName(l) + " from a chest");
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
            {
                V2 straight = (goal.Value - me.Pos).LimitLength(1f);
                move = RoomsOn && straight.Length > 0.01f ? SteerDir(-1000 - me.Id, me.Pos, botT, me.Def.BodyR + 4f, goal.Value, straight.Normalized()) : straight;  // through the Rooms arena's doors (LayoutRules.cs)
            }
        }

        /// Every game, each frame: ages the props and items for their looks.
        void UpdatePropView(float dt)
        {
            foreach (var p in Props.Values)
            {
                p.Age += dt;
                p.HitT = Math.Max(0f, p.HitT - dt);
                V2 slid = p.Pos - p.SeenAt;
                for (int i = p.Scuffs.Count - 1; i >= 0; i--)
                {
                    var sc = p.Scuffs[i];
                    sc.Age += dt;
                    if (sc.Age >= ScuffLife)
                        p.Scuffs.RemoveAt(i);
                    else
                        p.Scuffs[i] = sc;
                }
                if (p.Age > dt && slid.Length > 0.02f && slid.Length < 30f)
                {
                    p.PushT = 0.25f;
                    p.PushDir = slid.Normalized();
                    V2 side = new V2(-p.PushDir.Y, p.PushDir.X) * ((Round(p) ? PropRadius(p) : Math.Min(HalfW(p), HalfH(p))) * 0.5f);
                    if (p.Scuffs.Count < 80)
                    {
                        p.Scuffs.Add(new Scuff { A = p.SeenAt + side, B = p.Pos + side });
                        p.Scuffs.Add(new Scuff { A = p.SeenAt - side, B = p.Pos - side });
                    }
                }
                else
                    p.PushT = Math.Max(0f, p.PushT - dt);
                p.SeenAt = p.Pos;
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
            int chest = p != null ? (int)p.Chest : 0;
            s.Int(ref id);
            s.Byte(ref kind);
            s.Byte(ref seed);
            s.Vec(ref pos);
            s.Float(ref hp);
            s.Float(ref max);
            s.Float(ref fuse);
            s.Bool(ref open);
            s.Byte(ref chest);
            if (s.Reading)
            {
                if (!Props.TryGetValue(id, out p))
                    Props[id] = p = new Prop { Id = id, Kind = (PropKind)kind, Seed = seed, Hp = hp, Age = SurvivalOn ? 1f : 0f };  // (Survival's room is full of props the moment a run loads: they are just there, no drop-in each)
                if (hp < p.Hp)
                    p.HitT = 0.15f;
                if (open && !p.Open)
                    p.OpenT = 0f;
                p.Pos = pos;
                p.Hp = hp;
                p.MaxHp = max;
                p.Fuse = fuse;
                p.Open = open;
                p.Chest = (ChestKind)chest;
            }
            return id;
        }

        int SyncLoot(NetSync s, Loot l)
        {
            int id = l != null ? l.Id : 0, owner = l != null ? l.Owner : 0, u = l != null ? l.Upgrade : 0;
            int kind = l != null ? (int)l.Kind : 0, item = l != null ? l.Item : 0;
            V2 pos = l != null ? l.Pos : default(V2);
            s.Int(ref id);
            s.Int(ref owner);
            s.Short(ref u);
            s.Byte(ref kind);
            s.Short(ref item);
            s.Vec(ref pos);
            if (s.Reading && lootSent.Contains(id))
                return id;
            if (s.Reading)
            {
                if (!Loots.TryGetValue(id, out l))
                    Loots[id] = l = new Loot { Id = id };
                l.Owner = owner;
                l.Upgrade = u;
                l.Kind = (LootKind)kind;
                l.Item = item;
                l.Pos = pos;
            }
            return id;
        }
    }
}
