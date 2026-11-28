using System;
using System.IO;
using System.Collections.Generic;

namespace Arena
{
    public enum TrapKind : byte { Turret, Spike, Slow }

    /// A room trap (owner Nov 20): stands at a trap slot of a cell layout (`CellLayout.Traps`) in a Combat room.
    public struct RoomTrap
    {
        public V2 Pos;
        public TrapKind Kind;
        public float HalfX, HalfY;  // a spike plate's or slow field's half width and half height (a square, or a strip in a corridor; 0 for a turret)
        public V2 Face;             // a turret set into a wall (the embrasure): the way out of the wall; zero for a free-standing one (Dev Kit)
    }

    /// Room traps: every Combat cell's trap slots each roll a trap or stay empty, seeded from the cell (no wire data: every machine
    /// derives the same list from `Expanded`). A turret shoots the nearest player in a wave, a spike plate hurts whoever stands on it,
    /// a slow field slows whoever stands in it. Shops, the Black Market, chest and other rooms never get any. Look: Game/RoomTrapLook.cs.
    public static class RoomTraps
    {
        public const float EmptyChance = 0.35f;
        public const float SpikeTick = 0.6f, SlowMult = 0.55f;
        public const float Tile = 50f;  // the floor grid (FloorGridLook.cs): world-anchored lines this far apart (the lattice's origin and cell are on it); plates sit on whole squares of it
        public static readonly float[] SpikeSizes = { 25f, 50f }, SlowSizes = { 25f, 50f, 75f }, SpikeLongs = { 75f, 100f, 125f }, SlowLongs = { 100f, 125f, 150f };  // (half a side, whole grid squares; a slot takes the biggest that fits, or one smaller)
        public const float SizeGap = 10f;  // (a plate keeps this far from walls and the cell's edge)
        public const float TurretRange = 520f, TurretInterval = 1.5f, TurretShotSpeed = 330f, TurretLife = 1.8f;

        public static float SpikeDamage(int wave) => 6f + 0.4f * Math.Max(0, wave);
        public static float TurretDamage(int wave) => 8f + 0.5f * Math.Max(0, wave);
        public const float TurretHitR = 26f, TurretSolidR = 24f;  // (a turret is a solid post: nothing walks through it)
        /// A map turret sits in a wall (owner Nov 24): `SnapReach` how far its slot looks for a wall straight up, down, left or right,
        /// `WallOut` how far its middle stands out from the wall's face, `ArcCos` the cosine of its 75 degree half arc of fire.
        public const float SnapReach = 110f, WallOut = 14f, ArcCos = 0.2588f;

        /// Where a turret slot's turret goes: flush on the nearest wall straight up, down, left or right within SnapReach (its face
        /// pointing out), or false when no wall is there (an open side or a door: no turret).
        public static bool SnapToWall(V2 p, World w, out V2 pos, out V2 face)
        {
            pos = p; face = new V2(0f, 0f);
            float best = SnapReach;
            bool found = false;
            SnapTry(w.Walls(), p, ref best, ref pos, ref face, ref found);
            SnapTry(w.Cover, p, ref best, ref pos, ref face, ref found);
            return found;
        }

    static void SnapTry(Solid[] boxes, V2 p, ref float best, ref V2 pos, ref V2 face, ref bool found)
        {
            if (boxes == null)
                return;
            foreach (var b in boxes)
            {
                if (b.Round)
                    continue;
                float x0 = b.Pos.X - b.HW, x1 = b.Pos.X + b.HW, y0 = b.Pos.Y - b.HH, y1 = b.Pos.Y + b.HH;
                if (p.Y > y0 && p.Y < y1)
                {
                    if (x0 >= p.X && x0 - p.X < best) { best = x0 - p.X; pos = new V2(x0 - WallOut, p.Y); face = new V2(-1f, 0f); found = true; }
                    if (x1 <= p.X && p.X - x1 < best) { best = p.X - x1; pos = new V2(x1 + WallOut, p.Y); face = new V2(1f, 0f); found = true; }
                }
                if (p.X > x0 && p.X < x1)
                {
                    if (y0 >= p.Y && y0 - p.Y < best) { best = y0 - p.Y; pos = new V2(p.X, y0 - WallOut); face = new V2(0f, -1f); found = true; }
                    if (y1 <= p.Y && p.Y - y1 < best) { best = p.Y - y1; pos = new V2(p.X, y1 + WallOut); face = new V2(0f, 1f); found = true; }
                }
            }
        }

        /// Can a turret facing `face` (zero: free-standing, any way) aim at `to` from `from`? A wall turret turns 75 degrees either side of straight out.
        public static bool InArc(V2 from, V2 face, V2 to)
        {
            if (face.X == 0f && face.Y == 0f)
                return true;
            var d = to - from;
            float l = d.Length;
            return l > 0.01f && d.Dot(face) >= ArcCos * l;
        }

        /// A turret's health: your shots wear it down, and at zero it is wrecked for the rest of the map.
        public static float TurretHp(int wave) => 40f + 4f * Math.Max(0, wave);

        /// Room around a slot: how big a square (half side) fits at cell-local (x, y) without touching a wall or leaving the cell.
        public static float Fit(int[][] walls, float x, float y)
        {
            float fit = Math.Min(Math.Min(x, y), Math.Min(CellLayouts.Cell - x, CellLayouts.Cell - y));
            foreach (var w in walls)
            {
                float dx = Math.Max(Math.Max(w[0] - x, x - (w[0] + w[2])), 0f), dy = Math.Max(Math.Max(w[1] - y, y - (w[1] + w[3])), 0f);
                fit = Math.Min(fit, Math.Max(dx, dy));
            }
            return fit - SizeGap;
        }

        /// A plate's half side: the biggest size that fits, now and then (a third) the one below, so neighbours differ.
        public static float PickHalf(float[] sizes, float fit, DetRandom rng)
        {
            int k = 0;
            while (k + 1 < sizes.Length && sizes[k + 1] <= fit)
                k++;
            if (k > 0 && rng.Float() < 0.34f)
                k--;
            else
                rng.Float();
            return sizes[k];
        }

        /// A plate's middle on one axis so its edges lie on grid lines: a square's middle for an odd number of squares, a line for an even one.
        public static float SnapCentre(float v, float half)
        {
            bool odd = ((int)Math.Round(2f * half / Tile) & 1) == 1;
            return odd ? (float)Math.Floor(v / Tile) * Tile + Tile / 2f : (float)Math.Round(v / Tile) * Tile;
        }

        /// A plate moved onto the grid: the spot nearest the slot (at most a square away) that stays clear of walls; false if none.
        public static bool SnapPlate(int[][] walls, ref float x, ref float y, float hx, float hy, List<float[]> others = null)
        {
            float cx = SnapCentre(x, hx), cy = SnapCentre(y, hy), best = float.MaxValue, bx = 0f, by = 0f;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    float px = cx + i * Tile, py = cy + j * Tile, d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < best && RectFree(walls, px, py, hx, hy, SnapGap) && !Overlaps(others, px, py, hx, hy))
                    { best = d; bx = px; by = py; }
                }
            if (best == float.MaxValue)
                return false;
            x = bx; y = by;
            return true;
        }

        /// Does a plate here overlap one of `others` (each: x, y, half x, half y)? Plates may touch, never overlap.
        public static bool Overlaps(List<float[]> others, float x, float y, float hx, float hy)
        {
            if (others != null)
                foreach (var o in others)
                    if (Math.Abs(x - o[0]) < hx + o[2] - 0.5f && Math.Abs(y - o[1]) < hy + o[3] - 0.5f)
                        return true;
            return false;
        }

        public const float SnapGap = 2f;  // (a snapped plate may come this close to a wall: grid lines don't follow the walls)

        /// True when a rectangle (half sides hx, hy) at cell-local (x, y) stays inside the cell and clear of every wall, with the gap.
        public static bool RectFree(int[][] walls, float x, float y, float hx, float hy, float g = SizeGap)
        {
            if (x - hx < g || y - hy < g || x + hx > CellLayouts.Cell - g || y + hy > CellLayouts.Cell - g)
                return false;
            foreach (var w in walls)
                if (x + hx + g > w[0] && x - hx - g < w[0] + w[2] && y + hy + g > w[1] && y - hy - g < w[1] + w[3])
                    return false;
            return true;
        }

        /// A plate's half width and height: a square, or, where walls pinch the square (a lane or an alcove), now and then (half the time)
        /// a strip lying along the open way: the long side the longest that fits.
        public static void PickSize(int[][] walls, float x, float y, float[] sizes, float[] longs, DetRandom rng, out float hx, out float hy)
        {
            float fit = Fit(walls, x, y), h = PickHalf(sizes, fit, rng);
            hx = hy = h;
            bool strip = rng.Float() < 0.5f;
            if (!strip || fit >= sizes[sizes.Length - 1] || h > sizes[1])
                return;
            for (int k = longs.Length - 1; k >= 0; k--)
            {
                bool okX = RectFree(walls, x, y, longs[k], h), okY = RectFree(walls, x, y, h, longs[k]);
                if (okX || okY)
                {
                    if (okX && (!okY || rng.Float() < 0.5f)) hx = longs[k]; else hy = longs[k];
                    return;
                }
            }
        }

        public static bool InRect(V2 centre, V2 p, float hx, float hy) => Math.Abs(p.X - centre.X) <= hx && Math.Abs(p.Y - centre.Y) <= hy;

        /// What a slot holds: nothing, or a kind (a slot on a wall favours a turret, one on the floor gets a spike or a slow field, never a turret).
        public static TrapKind? Roll(DetRandom rng, bool onWall)
        {
            if (rng.Float() < EmptyChance)
                return null;
            float r = rng.Float();
            if (onWall)
                return r < 0.6f ? TrapKind.Turret : r < 0.8f ? TrapKind.Spike : TrapKind.Slow;
            return r < 0.5f ? TrapKind.Spike : TrapKind.Slow;  // (turrets only on wall slots: they sit in the wall)
        }

        /// A room that may hold traps: a whole-cell Combat room (not the core).
        public static bool Eligible(ExpansionRoom r)
            => r.Kind == RoomKind.Combat && !r.Floating && r.Surprise == 0 && CellLayouts.OnLattice(r) && !Expansion.IsGridCore(r);

        /// One cell's traps, in the cell's own frame (0..600): each trap slot rolls a kind (seeded from the cell's place cx, cy), a plate
        /// its size, snapped onto the floor grid clear of the walls and the cell's other plates.
        public static List<RoomTrap> CellTraps(CellLayouts.CellPlan plan, int cx, int cy)
        {
            var list = new List<RoomTrap>();
            var turned = CellLayouts.TurnedOf(plan);
            var slots = turned.Traps;
            var sizeRng = new DetRandom(cx * 40503 ^ cy * 69069 ^ 0x1b873593);  // (its own stream: the kinds roll as before)
            var rng = new DetRandom(cx * 83492791 ^ cy * 2654435 ^ 0x2545f491);
            var plates = new List<float[]>();  // (this cell's plates so far: a new one never overlaps them)
            foreach (var s in slots)
            {
                var kind = Roll(rng, s[2] == 1);
                if (!kind.HasValue)
                    continue;
                float hx = 0f, hy = 0f;
                if (kind.Value != TrapKind.Turret)
                {
                    bool spike = kind.Value == TrapKind.Spike;
                    var sizes = spike ? SpikeSizes : SlowSizes;
                    PickSize(turned.Walls, s[0], s[1], sizes, spike ? SpikeLongs : SlowLongs, sizeRng, out hx, out hy);
                    float x = s[0], y = s[1];
                    if (!SnapPlate(turned.Walls, ref x, ref y, hx, hy, plates))  // (onto the floor grid, clear of the walls and this cell's other plates)
                    {
                        bool placed = false;  // too tight there: the next square size down, down to the smallest, or none
                        for (int k = sizes.Length - 1; k >= 0 && !placed; k--)
                        {
                            if (sizes[k] >= Math.Max(hx, hy))
                                continue;
                            x = s[0]; y = s[1];
                            if (SnapPlate(turned.Walls, ref x, ref y, sizes[k], sizes[k], plates))
                            {
                                hx = hy = sizes[k];
                                placed = true;
                            }
                        }
                        if (!placed)
                            continue;
                    }
                    plates.Add(new[] { x, y, hx, hy });
                    list.Add(new RoomTrap { Pos = new V2(x, y), Kind = kind.Value, HalfX = hx, HalfY = hy });
                    continue;
                }
                list.Add(new RoomTrap { Pos = new V2(s[0], s[1]), Kind = kind.Value, HalfX = hx, HalfY = hy });
            }
            return list;
        }

        public static List<RoomTrap> For(IList<ExpansionRoom> rooms)
        {
            var list = new List<RoomTrap>();
            bool grid = false;
            foreach (var r in rooms)
                grid |= Expansion.IsGridCore(r);
            if (!grid)
                return list;
            var plans = CellLayouts.Plans(rooms);
            foreach (var r in rooms)
            {
                if (RelicGauntlet.Is(r))
                    RelicGauntlet.AddTurrets(list, r);  // (the Relic Gauntlet's four wall turrets)
                if (!Eligible(r))
                    continue;
                foreach (var o in CellLayouts.CellOrigins(r))
                {
                    CellLayouts.CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                    if (!CellLayouts.TryPlan(plans, cx, cy, out var plan))
                        continue;
                    foreach (var t in CellTraps(plan, cx, cy))
                        list.Add(new RoomTrap { Pos = new V2(o.X + t.Pos.X, o.Y + t.Pos.Y), Kind = t.Kind, HalfX = t.HalfX, HalfY = t.HalfY });
                }
            }
            return list;
        }
    }

    public partial class Game
    {
        List<RoomTrap> roomTrapList = new List<RoomTrap>();
        V2[] roomTurretPosts = new V2[0];  // the standing turrets, as the last RoomTrapList build left them (every frame builds it: the host's tick, everyone's draw)
        int roomTrapKey = int.MinValue;
        World roomTrapWorld;  // the world the list's turrets were set into (a new world: build it again)
        readonly Dictionary<int, float> trapCd = new Dictionary<int, float>();
        readonly HashSet<long> trapWrecks = new HashSet<long>();      // turrets shot to bits this map (synced in Msg.ExpandState)
        readonly Dictionary<long, float> trapHp = new Dictionary<long, float>();  // host: a turret's health left

        /// Every game, for the look: a damaged turret's health left (0-1) and when it was last hit, by its place.
        readonly Dictionary<long, float> trapHpFrac = new Dictionary<long, float>();
        readonly Dictionary<long, float> trapHitAt = new Dictionary<long, float>();

        public float TurretHpFrac(V2 pos) => trapHpFrac.TryGetValue(TrapKey(pos), out float f) ? f : 1f;
        public float TurretHitAgo(V2 pos) => trapHitAt.TryGetValue(TrapKey(pos), out float t) ? AnimT - t : 99f;

        static long TrapKey(V2 p) => ((long)(int)Math.Round(p.X) << 32) ^ (uint)(int)Math.Round(p.Y);

        /// Every room trap on the map (rebuilt when the rooms change; the same on every machine).
        public List<RoomTrap> RoomTrapList
        {
            get
            {
                int key = Expanded.Count + devTrapVer * 977;
                foreach (var r in Expanded)
                    key = key * 31 + r.X * 7 + r.Y * 3 + (int)r.Kind + (r.Floating ? 1 : 0) + r.Surprise;
                var world = CurrentWorld;
                if (key != roomTrapKey || world != roomTrapWorld)
                {
                    roomTrapKey = key;
                    roomTrapWorld = world;
                    roomTrapList = RoomTraps.For(Expanded);
                    for (int i = roomTrapList.Count - 1; i >= 0; i--)  // a map turret sits in its wall, or isn't there (no wall: an open side or a door)
                    {
                        var tr = roomTrapList[i];
                        if (tr.Kind != TrapKind.Turret)
                            continue;
                        if (world != null && RoomTraps.SnapToWall(tr.Pos, world, out V2 at, out V2 face))
                        {
                            tr.Pos = at;
                            tr.Face = face;
                            roomTrapList[i] = tr;
                        }
                        else
                            roomTrapList.RemoveAt(i);
                    }
                    roomTrapList.AddRange(devTraps);  // (DevTraps.cs)
                    roomTrapList.RemoveAll(t => t.Kind == TrapKind.Turret && trapWrecks.Contains(TrapKey(t.Pos)));  // (a wrecked turret stays gone)
                    var posts = new List<V2>();
                    foreach (var t in roomTrapList)
                        if (t.Kind == TrapKind.Turret)
                            posts.Add(t.Pos);
                    roomTurretPosts = posts.ToArray();
                    trapCd.Clear();
                }
                return roomTrapList;
            }
        }

        /// Moves something of radius r out of every standing room turret (a solid post), from PushOutOfSolids.
        V2 PushOutOfTurrets(V2 pos, float r)
        {
            var posts = roomTurretPosts;
            for (int i = 0; i < posts.Length; i++)
            {
                float dist = pos.DistanceTo(posts[i]), want = RoomTraps.TurretSolidR + r;
                if (dist < want)
                    pos = posts[i] + (dist > 0.5f ? (pos - posts[i]) / dist : new V2(1f, 0f)) * want;
            }
            return pos;
        }

        /// A slow field's pull on a mover: 1, or RoomTraps.SlowMult while standing in one.
        float TrapSlowAt(V2 pos)
        {
            if (!ExpandingOn && devTraps.Count == 0)
                return 1f;
            foreach (var t in RoomTrapList)
                if (t.Kind == TrapKind.Slow && RoomTraps.InRect(t.Pos, pos, t.HalfX, t.HalfY))
                    return RoomTraps.SlowMult;
            return 1f;
        }

        /// In WorldStops, for each bullet: your shot hits a turret (it wears down and finally wrecks); true if it stopped there.
        bool RoomTrapStops(Bullet b)
        {
            if (!IsHost || !ExpandingOn && devTraps.Count == 0 || Defs.IsEnemyShot(b.Kind) || b.Owner == 0)
                return false;
            var traps = RoomTrapList;
            for (int i = 0; i < traps.Count; i++)
            {
                var t = traps[i];
                if (t.Kind != TrapKind.Turret || t.Pos.FartherOrAt(b.Pos, RoomTraps.TurretHitR + b.Radius))
                    continue;
                HurtRoomTurret(t, b.Dmg);
                return true;
            }
            return false;
        }

        /// Host: a melee cut (swing or Burst) wears down every room turret `reaches` says it touches (Katana.cs, Knight.cs).
        void MeleeHitRoomTurrets(float dmg, Func<V2, float, bool> reaches)
        {
            if (!IsHost || !ExpandingOn && devTraps.Count == 0)
                return;
            var traps = RoomTrapList;
            for (int i = traps.Count - 1; i >= 0; i--)
            {
                var t = traps[i];
                if (t.Kind == TrapKind.Turret && reaches(t.Pos, RoomTraps.TurretHitR))
                    HurtRoomTurret(t, dmg);
            }
        }

        void HurtRoomTurret(RoomTrap t, float dmg)
        {
            long key = TrapKey(t.Pos);
            if (!trapHp.TryGetValue(key, out float hp))
                hp = RoomTraps.TurretHp(Wave);
            hp -= Math.Max(1f, dmg);
            if (hp > 0f)
            {
                trapHp[key] = hp;
                BroadcastFx(FxKind.Ring, t.Pos, new Rgba(1f, 0.45f, 0.4f), 38f, 0.25f, Sfx.Hit);
                var tw = Proto.Begin(Msg.TrapHit);  // everyone's game draws the bar (RoomTrapLook.cs)
                tw.Write((short)Math.Round(t.Pos.X)); tw.Write((short)Math.Round(t.Pos.Y)); tw.Write((byte)Math.Max(1, Math.Min(255, (int)(255f * hp / RoomTraps.TurretHp(Wave)))));
                session.Broadcast(Proto.End(tw));
                NoteTrapHit(t.Pos, hp / RoomTraps.TurretHp(Wave));
                return;
            }
            trapHp.Remove(key);
            trapWrecks.Add(key);
            roomTrapKey = int.MinValue;  // (rebuilt without it)
            BroadcastFx(FxKind.Boom, t.Pos, new Rgba(1f, 0.5f, 0.3f), 70f, 0.4f, Sfx.Explode);
            foreach (int owner in LootOwners())  // (a few coins, a full copy each)
                AddCoin(owner, 5, false, t.Pos, 8f, 14f);
            DoLog("a room turret is destroyed");
            BroadcastExpansion();
        }

        void NoteTrapHit(V2 pos, float frac)
        {
            long key = TrapKey(pos);
            trapHpFrac[key] = Math.Max(0.01f, Math.Min(1f, frac));
            trapHitAt[key] = AnimT;
        }

        void ReadTrapHit(BinaryReader r)
        {
            int x = r.ReadInt16(), y = r.ReadInt16();
            float frac = r.ReadByte() / 255f;
            if (!IsHost)
                NoteTrapHit(new V2(x, y), frac);
        }

        void WriteTrapWrecks(BinaryWriter w)
        {
            w.Write((ushort)trapWrecks.Count);
            foreach (long k in trapWrecks)
            {
                w.Write((short)(k >> 32)); w.Write((short)(int)(k & 0xFFFFFFFF));
            }
        }

        void ReadTrapWrecks(BinaryReader r)
        {
            int n = r.ReadUInt16();
            var set = new HashSet<long>();
            for (int i = 0; i < n; i++)
            {
                int x = r.ReadInt16(), y = r.ReadInt16();
                set.Add(((long)x << 32) ^ (uint)y);
            }
            if (!set.SetEquals(trapWrecks))
            {
                trapWrecks.Clear();
                trapWrecks.UnionWith(set);
                roomTrapKey = int.MinValue;
            }
        }

        /// Host, on a map reset: the turrets come back with the new map.
        internal void ClearTrapWrecks()
        {
            trapWrecks.Clear();
            trapHp.Clear();
            trapHpFrac.Clear();
            trapHitAt.Clear();
            roomTrapKey = int.MinValue;
        }

        /// Host, every tick: spike plates hurt whoever stands on them, turrets shoot the nearest player in a wave.
        void TickRoomTraps(float dt)
        {
            if (!IsHost || !ExpandingOn && devTraps.Count == 0)
                return;
            var traps = RoomTrapList;
            for (int i = 0; i < traps.Count; i++)
            {
                var t = traps[i];
                if (t.Kind == TrapKind.Slow)  // (turrets and spikes work in fog too)
                    continue;
                trapCd.TryGetValue(i, out float cd);
                cd -= dt;
                if (t.Kind == TrapKind.Spike)
                {
                    if (cd <= 0f)
                        foreach (var p in Players.Values)
                            if (!p.Downed && RoomTraps.InRect(t.Pos, p.Pos, t.HalfX + p.Def.HitR * 0.5f, t.HalfY + p.Def.HitR * 0.5f))
                            {
                                Damage(p, RoomTraps.SpikeDamage(Wave), "Spike trap");
                                cd = RoomTraps.SpikeTick;
                                break;
                            }
                }
                else if (cd <= 0f)
                {
                    Player best = null;
                    float bd = RoomTraps.TurretRange * RoomTraps.TurretRange;
                    foreach (var p in Players.Values)
                        if (!p.Downed && (p.Pos - t.Pos).LengthSq < bd && RoomTraps.InArc(t.Pos, t.Face, p.Pos))
                        {
                            bd = (p.Pos - t.Pos).LengthSq;
                            best = p;
                        }
                    if (best != null)
                    {
                        V2 dir = (best.Pos - t.Pos).Normalized();
                        SpawnBullet(Defs.SmallShot, t.Pos + dir * 26f, dir * RoomTraps.TurretShotSpeed, 0f, RoomTraps.TurretLife, RoomTraps.TurretDamage(Wave), 0);
                        cd = RoomTraps.TurretInterval;
                    }
                }
                trapCd[i] = Math.Max(cd, 0f);
            }
        }
    }
}
