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
    }

    /// Room traps: every Combat cell's trap slots each roll a trap or stay empty, seeded from the cell (no wire data: every machine
    /// derives the same list from `Expanded`). A turret shoots the nearest player in a wave, a spike plate hurts whoever stands on it,
    /// a slow field slows whoever stands in it. Shops, the Black Market, chest and other rooms never get any. Look: Game/RoomTrapLook.cs.
    public static class RoomTraps
    {
        public const float EmptyChance = 0.35f;
        public const float SpikeHalf = 32f, SpikeTick = 0.6f, SlowHalf = 48f, SlowMult = 0.55f;  // (the plates are squares: half a side)
        public const float TurretRange = 520f, TurretInterval = 1.5f, TurretShotSpeed = 330f, TurretLife = 1.8f;

        public static float SpikeDamage(int wave) => 6f + 0.4f * Math.Max(0, wave);
        public static float TurretDamage(int wave) => 8f + 0.5f * Math.Max(0, wave);
        public const float TurretHitR = 26f;

        /// A turret's health: your shots wear it down, and at zero it is wrecked for the rest of the map.
        public static float TurretHp(int wave) => 40f + 4f * Math.Max(0, wave);

        public static bool InSquare(V2 centre, V2 p, float half) => Math.Abs(p.X - centre.X) <= half && Math.Abs(p.Y - centre.Y) <= half;

        /// What a slot holds: nothing, or a kind (a slot on a wall favours a turret, one on the floor a spike or a slow field).
        public static TrapKind? Roll(DetRandom rng, bool onWall)
        {
            if (rng.Float() < EmptyChance)
                return null;
            float r = rng.Float();
            if (onWall)
                return r < 0.6f ? TrapKind.Turret : r < 0.8f ? TrapKind.Spike : TrapKind.Slow;
            return r < 0.2f ? TrapKind.Turret : r < 0.6f ? TrapKind.Spike : TrapKind.Slow;
        }

        /// A room that may hold traps: a whole-cell Combat room (not the core).
        public static bool Eligible(ExpansionRoom r)
            => r.Kind == RoomKind.Combat && !r.Floating && r.Surprise == 0 && CellLayouts.OnLattice(r) && !Expansion.IsGridCore(r);

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
                if (!Eligible(r))
                    continue;
                foreach (var o in CellLayouts.CellOrigins(r))
                {
                    CellLayouts.CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                    if (!CellLayouts.TryPlan(plans, cx, cy, out var plan))
                        continue;
                    var slots = CellLayouts.TurnedOf(plan).Traps;
                    var rng = new DetRandom(cx * 83492791 ^ cy * 2654435 ^ 0x2545f491);
                    foreach (var s in slots)
                    {
                        var kind = Roll(rng, s[2] == 1);
                        if (kind.HasValue)
                            list.Add(new RoomTrap { Pos = new V2(o.X + s[0], o.Y + s[1]), Kind = kind.Value });
                    }
                }
            }
            return list;
        }
    }

    public partial class Game
    {
        List<RoomTrap> roomTrapList = new List<RoomTrap>();
        int roomTrapKey = int.MinValue;
        readonly Dictionary<int, float> trapCd = new Dictionary<int, float>();
        readonly HashSet<long> trapWrecks = new HashSet<long>();      // turrets shot to bits this map (synced in Msg.ExpandState)
        readonly Dictionary<long, float> trapHp = new Dictionary<long, float>();  // host: a turret's health left

        static long TrapKey(V2 p) => ((long)(int)Math.Round(p.X) << 32) ^ (uint)(int)Math.Round(p.Y);

        /// Every room trap on the map (rebuilt when the rooms change; the same on every machine).
        public List<RoomTrap> RoomTrapList
        {
            get
            {
                int key = Expanded.Count;
                foreach (var r in Expanded)
                    key = key * 31 + r.X * 7 + r.Y * 3 + (int)r.Kind + (r.Floating ? 1 : 0) + r.Surprise;
                if (key != roomTrapKey)
                {
                    roomTrapKey = key;
                    roomTrapList = RoomTraps.For(Expanded);
                    roomTrapList.RemoveAll(t => t.Kind == TrapKind.Turret && trapWrecks.Contains(TrapKey(t.Pos)));  // (a wrecked turret stays gone)
                    trapCd.Clear();
                }
                return roomTrapList;
            }
        }

        /// A slow field's pull on a mover: 1, or RoomTraps.SlowMult while standing in one.
        float TrapSlowAt(V2 pos)
        {
            if (!ExpandingOn)
                return 1f;
            foreach (var t in RoomTrapList)
                if (t.Kind == TrapKind.Slow && RoomTraps.InSquare(t.Pos, pos, RoomTraps.SlowHalf))
                    return RoomTraps.SlowMult;
            return 1f;
        }

        /// In WorldStops, for each bullet: your shot hits a turret (it wears down and finally wrecks); true if it stopped there.
        bool RoomTrapStops(Bullet b)
        {
            if (!IsHost || !ExpandingOn || Defs.IsEnemyShot(b.Kind) || b.Owner == 0)
                return false;
            var traps = RoomTrapList;
            for (int i = 0; i < traps.Count; i++)
            {
                var t = traps[i];
                if (t.Kind != TrapKind.Turret || t.Pos.FartherOrAt(b.Pos, RoomTraps.TurretHitR + b.Radius))
                    continue;
                long key = TrapKey(t.Pos);
                if (!trapHp.TryGetValue(key, out float hp))
                    hp = RoomTraps.TurretHp(Wave);
                hp -= Math.Max(1f, b.Dmg);
                if (hp > 0f)
                {
                    trapHp[key] = hp;
                    BroadcastFx(FxKind.Ring, t.Pos, new Rgba(1f, 0.45f, 0.4f), 34f, 0.2f, Sfx.None);
                    return true;
                }
                trapHp.Remove(key);
                trapWrecks.Add(key);
                roomTrapKey = int.MinValue;  // (rebuilt without it)
                BroadcastFx(FxKind.Boom, t.Pos, new Rgba(1f, 0.5f, 0.3f), 70f, 0.4f, Sfx.Explode);
                foreach (int owner in LootOwners())  // (a few coins, a full copy each)
                    AddCoin(owner, 5, false, t.Pos, 8f, 14f);
                DoLog("a room turret is destroyed");
                BroadcastExpansion();
                return true;
            }
            return false;
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
            roomTrapKey = int.MinValue;
        }

        /// Host, every tick: spike plates hurt whoever stands on them, turrets shoot the nearest player in a wave.
        void TickRoomTraps(float dt)
        {
            if (!IsHost || !ExpandingOn)
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
                            if (!p.Downed && RoomTraps.InSquare(t.Pos, p.Pos, RoomTraps.SpikeHalf + p.Def.HitR * 0.5f))
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
                        if (!p.Downed && (p.Pos - t.Pos).LengthSq < bd)
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
