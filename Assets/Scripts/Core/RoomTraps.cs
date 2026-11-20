using System;
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
        public const float SpikeR = 34f, SpikeTick = 0.6f, SlowR = 52f, SlowMult = 0.55f;
        public const float TurretRange = 520f, TurretInterval = 1.5f, TurretShotSpeed = 330f, TurretLife = 1.8f;

        public static float SpikeDamage(int wave) => 6f + 0.4f * Math.Max(0, wave);
        public static float TurretDamage(int wave) => 8f + 0.5f * Math.Max(0, wave);

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
            foreach (var r in rooms)
            {
                if (!Eligible(r))
                    continue;
                foreach (var o in CellLayouts.CellOrigins(r))
                {
                    CellLayouts.CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                    var slots = CellLayouts.All[CellLayouts.Pick(cx, cy)].Traps;
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
                if (t.Kind == TrapKind.Slow && !t.Pos.FartherOrAt(pos, RoomTraps.SlowR))
                    return RoomTraps.SlowMult;
            return 1f;
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
                if (t.Kind == TrapKind.Slow || UnseenAt(t.Pos))
                    continue;
                trapCd.TryGetValue(i, out float cd);
                cd -= dt;
                if (t.Kind == TrapKind.Spike)
                {
                    if (cd <= 0f)
                        foreach (var p in Players.Values)
                            if (!p.Downed && !p.Pos.FartherOrAt(t.Pos, RoomTraps.SpikeR + p.Def.HitR))
                            {
                                Damage(p, RoomTraps.SpikeDamage(Wave), "Spike trap");
                                cd = RoomTraps.SpikeTick;
                                break;
                            }
                }
                else if (cd <= 0f && Battle)
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
