using System;
using System.Collections.Generic;

namespace Arena
{
    /// One part of a walled world (World.cs): the hub (today's arena), a room or a hallway. A box in world coordinates.
    public struct RoomArea
    {
        public string Name;
        public V2 Min, Max;
        public bool Hub, Hall, Vault;
        public bool Floating, Sealed;  // a floating room off the map, and one nobody has connected yet (Expansion.cs)
        public byte Surprise;   // a surprise room: 1 + its SurpriseShape (Docs/SurpriseRooms.md); 0: none
        public int Owner;       // the player who drew it (the Expanding arena; 0: none)
        public RoomKind Kind;   // what the room does (Expansion.cs)
        public PathId Floor;    // a rule room's path (its ground and hazards); None otherwise
        public byte PedestalWalls;  // a vendor room (shop, Black Market, Arms Dealer, Medic): the walls its pedestals line, ShopPedestals.West/East/North/South bits; 0 none
        public int Group;       // a multi-box room (an Expanding arena outline or bent hallway): its head's area index + 1, shared by its boxes; 0: a plain box
        public V2 Mid => (Min + Max) * 0.5f;
        public V2 Size => Max - Min;
        public bool Contains(V2 p, float pad = 0f) => p.X >= Min.X - pad && p.X <= Max.X + pad && p.Y >= Min.Y - pad && p.Y <= Max.Y + pad;
    }

    /// A doorway between two areas: a gap in the wall, Rooms.DoorW long. Vertical: the wall runs up and down (x = Pos.X).
    public struct RoomDoor
    {
        public V2 Pos;
        public bool Vertical;
        public float Width;  // the gap's length (0: Rooms.DoorW); the Expanding arena's opened walls are wider
        public float W => Width > 0f ? Width : Rooms.DoorW;
        public int A, B;  // the areas it joins (indexes into its World's Areas)
        public sbyte Pass;  // a One-way piece's door (OneWayDoors.cs): 0 both ways, +1 only from A into B, -1 only from B into A
        public V2 Across => Vertical ? new V2(1f, 0f) : new V2(0f, 1f);  // through the door
        public V2 Along => Vertical ? new V2(0f, 1f) : new V2(1f, 0f);   // along the wall
        public bool Joins(int area) => A == area || B == area;
        public int Other(int area) => A == area ? B : A;
    }

    /// The walled worlds' shared numbers (World.cs): the Expanding arena (Expansion.cs) and Survival's big room
    /// (SurvivalWorld.cs) are built from areas joined by doors. (The fixed Rooms map, Wings, was removed Nov 18.)
    public static class Rooms
    {
        public const float DoorW = 200f, WallT = 24f, DoorPad = 60f;  // (DoorW: four squares of the 50 px floor grid, so a grid door's edges sit on grid lines, 200 and 400 across a cell)
        public const float PortalR = 42f, PortalCooldown = 1.2f;

        /// No enemy comes in closer than SpawnClear to a player and, where there is a choice, no farther than SpawnReach.
        public const float SpawnClear = 350f, SpawnReach = 1500f;

        /// Room rounds start at this zoom, with a prompt to zoom out (CameraZoom.cs).
        public const float StartZoom = 1.5f;

        /// Everything that hurts players hits this much harder while a grown Expanding arena is up (Game.RoomsDamageMult).
        public static float EnemyDamageMult = 1.3f;  // (static, not const: the balance report tries others)
    }

    public partial class Game
    {
        /// Host, at each wave's start: the walled world is up for this wave (synced in the snapshot with the layout).
        public bool RoomsUp;

        /// The walled world is what you're playing in right now (not in a Data Gate's challenge or
        /// the Showcase).
        public bool RoomsOn => RoomsUp && !InRoom && ModeRules.Extras && !ShowcaseMode;

        /// Enemy damage to players: Rooms.EnemyDamageMult in a grown Expanding arena, else 1 (Combat.cs).
        public float RoomsDamageMult => RoomsOn && !SurvivalOn && ArenaKind == ArenaLayout.Expanding && Expansion.Grown(Expanded) ? Rooms.EnemyDamageMult : 1f;  // (Survival's big room is tuned at 1x; so is the Expanding arena's bare hub)

        /// The run's arena layout (Docs/ExpandingArena.md). The Campaign (a 100-wave run, Hunts.cs LongLegs) and Quick Play
        /// (QuickPlay.cs) are the Expanding arena; the Simple Arena modifier keeps it the open arena.
        public ArenaLayout ArenaKind => DrillOn ? ArenaLayout.Single : SurvivalOn ? ArenaLayout.Survival : Has(RunMod.SimpleArena) ? ArenaLayout.Single
            : D(DiffKnob.ArenaExpanding) >= 0.5f || LongLegs || Quick ? ArenaLayout.Expanding : ArenaLayout.Single;

        /// The geometry being played on right now: the walled world while it is up, else the plain arena box.
        public World CurrentWorld => RoomsOn ? (SurvivalOn ? SurvivalRoom.World : ExpansionWorld) : World.Single;

        /// Host, at the start of each wave (with PickLayout): regular waves get the rooms, boss waves the hub alone
        /// (bosses are tuned for 1600x900). Not with Tiny Arena or on Narrow Pass, which wall the hub in themselves,
        /// and not in a Boss Hunt or the Showcase.
        void PickRooms()
        {
            bool was = RoomsUp;
            bool grown = ArenaKind == ArenaLayout.Expanding;  // (the Expanding arena is its walled world from wave 1, the hub alone until a room is bought or a wall pushed: no wrap round the edges, the camera follows you)
            RoomsUp = grown && ModeRules.RunEvents && !ShowcaseMode && !BossNow
                && !Has(RunMod.TinyArena) && !OnPathOf(PathId.NarrowPass);
            if (RoomsUp != was)
            {
                LayoutAge = 0f;  // the walls rise (or the old ones go) with the change
                DoLog(RoomsUp ? "rooms: up (Expanding, " + Expanded.Count + " rooms)" : "rooms: down (the hub alone)");
            }
            if (was && !RoomsUp)
                PullIntoHub();
        }

        /// Host: a room was just built or gifted between waves. The Expanding arena is the plain box until the first room exists, and
        /// PickRooms only runs as a wave starts, so the world goes up now, not a wave late. Only ever raises it (PickRooms lowers it).
        void RaiseRoomsNow()
        {
            if (RoomsUp || ArenaKind != ArenaLayout.Expanding || !Expansion.Grown(Expanded) || !ModeRules.RunEvents || ShowcaseMode
                || Has(RunMod.TinyArena) || OnPathOf(PathId.NarrowPass))
                return;
            RoomsUp = true;
            LayoutAge = 0f;
            DoLog("rooms: up (Expanding, " + Expanded.Count + " rooms, built between waves)");
        }

        /// Host, every tick of a wave: the Expanding arena's rooms.
        void TickRoomsWave(float dt)
        {
            if (!RoomsOn || State != MatchState.Fighting)
                return;
            TickDuelRooms(dt);  // the Expanding arena's Gatekeeper rooms (GatekeeperRoom.cs)
            TickNodes();      // chest nodes (ExpansionGame.cs)
            TickStashClaims();  // a breach Stash's vector cache (BreachEvent.cs)
            TickBreachEvent();  // the gold breach and its surprise room (BreachEvent.cs)
            TickMysteryDoors();  // MysteryDoors.cs
        }

        /// A mini-boss duel is won (an Expanding Gatekeeper room): a prize pick for everyone, sometimes a legendary.
        void GrantVaultPrize(V2 at)
        {
            var bossType = BossAt(Math.Min(MaxWave, Bosses.NextBossWaveFrom(Wave)));  // the boss this cycle leads up to
            var key = IsReaper(bossType) ? EnemyType.ReaperOfDusk : bossType;
            foreach (var p in Players.Values)
            {
                p.PrizePicks++;
                if (rng.NextDouble() < Challenges.PrizeLegendaryChance)
                    p.LegendaryQueue.Add(key);
                if (State == MatchState.Intermission && p.Offer == null)
                    NextOffer(p);  // a duel won between waves: offer it now (the wave's clear won't), or the pick blocks Ready
            }
            BroadcastFx(FxKind.Ring, at, new Rgba(1f, 0.85f, 0.4f), 400f, 0.8f, Sfx.WaveClear);
        }

        /// Where an enemy (or a squad) comes in: a random spot anywhere on the map (an area picked by its
        /// size), clear of walls and at least SpawnClear from every player.
        V2 RoomsRandomSpot(float radius)
        {
            V2 at = ArenaMid, best = at, nearBest = at;
            float bestScore = float.MaxValue, nearD = float.MaxValue;
            int found = 0, foundFar = 0;
            int[] crowd = null;
            for (int tries = 0; tries < 60 && found < 6; tries++)
            {
                at = RandomInRooms(radius + 30f, radius + 30f);
                if (NearSolid(at, radius + 8f))
                    continue;
                float nearest = float.MaxValue;
                foreach (var p in Players.Values)
                    if (!p.Downed)
                        nearest = Math.Min(nearest, p.Pos.DistanceTo(at));
                if (nearest < Rooms.SpawnClear)
                    continue;
                found++;
                if (nearest < nearD) { nearD = nearest; nearBest = at; }  // (the fallback: the nearest valid spot)
                if (nearest > Rooms.SpawnReach)
                    continue;  // too far to run: no stragglers on the other side of a big map
                if (crowd == null)
                    crowd = AreaCrowd();
                // Within reach, the emptiest room wins (a built room nobody stands in still gets its share), nearer breaks ties.
                int ai = ExpandingOn ? CurrentWorld.AreaAt(at) : -1;
                float density = ai >= 0 && ai < crowd.Length ? crowd[ai] * 100000f / Math.Max(1f, CurrentWorld.Areas[ai].Size.X * CurrentWorld.Areas[ai].Size.Y) : 0f;
                float score = density * 100f + Rand(0f, 300f);  // (a little luck so ties don't always pick the nearest)
                foundFar++;
                if (score < bestScore) { bestScore = score; best = at; }
            }
            return foundFar > 0 ? best : found > 0 ? nearBest : at;
        }

        /// How many enemies are alive in each area (a new spawn goes where it is emptiest).
        int[] AreaCrowd()
        {
            var counts = new int[CurrentWorld.Areas.Length];
            foreach (var e in Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                int ai = CurrentWorld.AreaAt(e.Pos);
                if (ai >= 0 && ai < counts.Length)
                    counts[ai]++;
            }
            return counts;
        }

        /// Host, from TickEnemies: a duel room's mini-boss stays in its room, after whoever is in it. Returns true while
        /// holding (no shooting at nothing).
        bool RoomsLeash(Enemy e, ref V2 tpos)
        {
            if (!RoomsOn || e.Home < 0 || !ExpandingOn || !FactionMinis.Duels(e.Type) || e.Home >= CurrentWorld.Areas.Length)
                return false;
            var area = CurrentWorld.Areas[e.Home];
            Player near = null;
            foreach (var p in Players.Values)
                if (!p.Downed && area.Contains(p.Pos) && (near == null || p.Pos.DistanceSq(e.Pos) < near.Pos.DistanceSq(e.Pos)))
                    near = p;
            if (near != null)
            {
                tpos = near.Pos;
                return false;
            }
            float rx = Math.Min(150f, area.Size.X / 3f), ry = Math.Min(130f, area.Size.Y / 3f);
            tpos = area.Mid + new V2((float)Math.Cos(e.T * 0.5f + e.Id) * rx, (float)Math.Sin(e.T * 0.4f + e.Id * 1.7f) * ry);
            return true;
        }

        /// Is this area's door locked for whoever is in it: a Gatekeeper room while its duel is on. One way: you can
        /// come in, not go out.
        public bool AreaLocked(int area)
        {
            if (!RoomsOn || !ExpandingOn)
                return false;
            return DuelBoss(area) != null;
        }

        /// Your own game, after you move: in a locked room you stay in it (a dodge or dash can't take you out either).
        V2 RoomsHold(V2 before, V2 now, float r)
        {
            if (!RoomsOn)
                return now;
            int a = CurrentWorld.AreaAt(before);
            if (!AreaLocked(a) || !CurrentWorld.Areas[a].Contains(before) || CurrentWorld.AreaAt(now) == a)
                return now;
            var area = CurrentWorld.Areas[a];
            return now.Clamp(area.Min + new V2(r, r), area.Max - new V2(r, r));
        }

        /// Host, after an enemy moves: a duel room's mini-boss stays in its room while the door is locked,
        /// so a charge down the doorway stops at it like a wall (the Riot Captain is dazed) instead of leaving.
        void RoomsKeepHome(Enemy e)
        {
            if (!RoomsOn || e.Home < 0 || e.Home >= CurrentWorld.Areas.Length)
                return;
            if (!ExpandingOn || !FactionMinis.Duels(e.Type) || !AreaLocked(e.Home))
                return;
            var area = CurrentWorld.Areas[e.Home];
            e.Pos = e.Pos.Clamp(area.Min + new V2(e.Radius, e.Radius), area.Max - new V2(e.Radius, e.Radius));
        }

        /// Host, when a wave is cleared: an unbeaten duel ends with it (GatekeeperRoom.cs).
        void EndRoomsWave()
        {
            EndDuels();
        }

        /// Host, when the rooms go (a boss wave): everyone outside the hub is brought in, and the props left in the
        /// rooms and hallways are cleared away.
        void PullIntoHub()
        {
            var hub = new RoomArea { Min = V2.Zero, Max = new V2(Width, Height) };
            foreach (var p in Players.Values)
                if (!hub.Contains(p.Pos, -1f))
                {
                    V2 at = p.Pos.Clamp(hub.Min + new V2(90f, 90f), hub.Max - new V2(90f, 90f));
                    MovePlayer(p, PushOutOfProps(at, p.Def.BodyR));
                }
            scratchIds.Clear();
            foreach (var pr in Props.Values)
                if (!hub.Contains(pr.Pos))
                    scratchIds.Add(pr.Id);
            foreach (int id in scratchIds)
                Props.Remove(id);
        }

        /// Your own game for you, the host for enemies: through a portal if you're in one (and not just out of one).
        V2 RoomsPortal(V2 pos, ref float cooldown, float dt, float r = 0f)
        {
            cooldown = Math.Max(0f, cooldown - dt);
            if (!RoomsOn || cooldown > 0f)
                return pos;
            var exit = CurrentWorld.PortalExitFor(pos, r);
            if (!exit.HasValue)
                return pos;
            cooldown = Rooms.PortalCooldown;
            return exit.Value;
        }

        /// The hub's bounds while the rooms are up (else the playable arena): things that belong in the middle of
        /// the fight, like a Data Gate, go here.
        public V2 HubMin => RoomsOn ? V2.Zero : ArenaMin;
        public V2 HubMax => RoomsOn ? new V2(Width, Height) : ArenaMax;

        /// Which part of the walled world a point is in (the hub when the rooms aren't up).
        public int AreaAt(V2 p) => CurrentWorld.AreaAt(p);

        /// A random spot in the walled world (m from an area's edges): in one of its areas, chosen by size.
        /// How likely an area is to be where enemies come in: its size, double for a rule room of an enemy-mix path (Swarm Fields,
        /// Sniper Ridge, Elite Gauntlet: they're where the fight is), none for a shop room (a safe place to shop).
        float SpawnWeight(RoomArea a)
        {
            float w = a.Size.X * a.Size.Y;
            if (!ExpandingOn)
                return w;
            if (a.Sealed || a.Kind == Arena.RoomKind.Shop || a.Kind == Arena.RoomKind.BlackMarket || a.Kind == Arena.RoomKind.Arms || a.Kind == Arena.RoomKind.Medic || a.Kind == Arena.RoomKind.Builder || a.Kind == Arena.RoomKind.Gatekeeper || a.Kind == Arena.RoomKind.Hospital)
                return 0f;  // (the shop, Black Market, Arms Dealer, Medic and builder shop are safe rooms, the Gatekeeper has its own fight, and a sealed room can't be reached)
            if (a.Kind == Arena.RoomKind.Floor && (a.Floor == PathId.SwarmFields || a.Floor == PathId.SniperRidge || a.Floor == PathId.EliteGauntlet))
                return w * 2f;
            return w;
        }

        V2 RandomInRooms(float mx, float my)
        {
            float total = 0f;
            foreach (var a in CurrentWorld.Areas)
                total += SpawnWeight(a);
            float pick = Rand(0f, total);
            foreach (var a in CurrentWorld.Areas)
            {
                pick -= SpawnWeight(a);
                if (pick <= 0f)
                {
                    float x0 = a.Min.X + Math.Min(mx, a.Size.X / 2f - 1f), x1 = a.Max.X - Math.Min(mx, a.Size.X / 2f - 1f);
                    float y0 = a.Min.Y + Math.Min(my, a.Size.Y / 2f - 1f), y1 = a.Max.Y - Math.Min(my, a.Size.Y / 2f - 1f);
                    return new V2(Rand(x0, x1), Rand(y0, y1));
                }
            }
            return ArenaMid;
        }
    }
}
