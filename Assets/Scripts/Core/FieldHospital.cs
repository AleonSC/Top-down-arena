using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Field Hospital: the Healer Soldier's second ultimate (`AbilityId.FieldHospital`, key 1 of the lobby's Ultimate row;
    /// Docs/Mockups/field-hospital.html). Pressing it starts a pick (`HospPickSlot`): every free door spot in reach lights up
    /// as a green hologram door (`HospSpots`), and a click on one opens a single lattice cell behind it, a real room of the
    /// Expanding map (`RoomKind.Hospital`). A `ZoneKind.FieldHospital` zone over it runs the rest (host, `TickHospital`):
    /// a health shrine in the middle (each player once: full health and 25% past max), healing orbs that come back over
    /// time, and a laser gate that keeps enemies and their shots out. A minute after it opens (mid-wave or between waves alike)
    /// a countdown runs, everyone inside is put out at its door and the room is taken off the map (`EndHospital`).
    /// Outside a Grid run there are no door spots: the hospital is a tent at the cursor, a ring of the same gate.
    public partial class Game
    {
        public const float HospLife = 60f, HospEject = 3f, HospReach = 900f, HospPickR = 200f;
        public const int HospOrbs = 6, HospOrbsMastered = 8;
        public const float HospOrbHeal = 20f, HospOrbEvery = 4f, HospOrbEveryMastered = 3f;
        public const float HospOverFrac = 0.25f;       // the shrine's health past max, as a share of max health
        public const float HospTentRadius = 150f, HospTentReach = 360f;  // outside a Grid run: the tent's ring and how far off it can go
        public const float HospOrbRing = 0.36f;        // the orbs sit on a ring this share of the cell round the shrine
        public const float HospOut = 70f;              // how far past the door the eject puts players

        /// A cell a Field Hospital could open into: the door spot on the wall (Door), the cell's corner (X, Y) and middle, the side
        /// of the wall it hangs off (0 top, 1 bottom, 2 left, 3 right: the cell lies that way), and whether it is free.
        public struct HospSpot
        {
            public V2 Door, Mid;
            public int X, Y;
            public byte Side;
            public bool Free;
        }

        /// Your own game: the slot of a Field Hospital waiting for you to click a door (-1: not picking).
        public int HospPickSlot = -1;
        bool hospFireWas, hospFireHold;

        /// Is this a Grid run (the map built on the lattice), where the hospital opens a cell?
        public bool HospOnGrid
        {
            get
            {
                if (!ExpandingOn)
                    return false;
                foreach (var r in Expanded)
                    if (Expansion.IsGridCore(r))
                        return true;
                return false;
            }
        }

        readonly List<HospSpot> hospSpots = new List<HospSpot>();
        World hospSpotsWorld;
        long hospSpotsKey = -1;

        /// Every door spot within HospReach of `me`, free or not (red on the pick).
        public List<HospSpot> HospSpots(Player me)
        {
            if (me == null || !HospOnGrid)
            {
                hospSpots.Clear();
                hospSpotsKey = -1;
                return hospSpots;
            }
            long key = (((long)Expanded.Count * 64 + MysteryDoors.Count) * 64 + Nodes.Count) * 1000003L + (long)(me.Pos.X / 50f) * 4099L + (long)(me.Pos.Y / 50f);
            if (key == hospSpotsKey && hospSpotsWorld == ExpansionWorld)
                return hospSpots;  // (worked out again only when the map or where you stand changes)
            hospSpotsKey = key;
            hospSpotsWorld = ExpansionWorld;
            hospSpots.Clear();
            foreach (var s in Expansion.GridSpots(Expanded))
            {
                var door = new V2(s.SX, s.SY);
                if (door.FartherThan(me.Pos, HospReach))
                    continue;
                hospSpots.Add(new HospSpot
                {
                    Door = door, X = s.X, Y = s.Y, Side = s.Side,
                    Mid = new V2(s.X + Expansion.GridCell / 2f, s.Y + Expansion.GridCell / 2f),
                    Free = HospCellFree(s.X, s.Y, door),
                });
            }
            return hospSpots;
        }

        /// Can a hospital go in the cell at (x, y)? Not over or into anything: a legal one-cell piece there, no mystery door on its
        /// wall, no map node in it (a shrine, chest or relic waiting for a room), and not against a sealed floating room (it would open it).
        bool HospCellFree(int x, int y, V2 door)
        {
            if (Expansion.HospRoomFor(Expanded, x, y) == null)
                return false;
            foreach (var d in MysteryDoors)
                if (d.Pos.CloserThan(door, 4f))
                    return false;
            int c = Expansion.GridCell;
            foreach (var n in Nodes)
                if (n.X > x && n.X < x + c && n.Y > y && n.Y < y + c)
                    return false;
            foreach (var r in Expanded)
                if (r.Floating && Expansion.Touches(r, x, y, x + c, y + c))
                    return false;
            return true;
        }

        /// The free spot a click at `aimAt` picks: the free door spot nearest the cursor within HospPickR, or the free one whose cell
        /// the cursor is in. Null when there's none.
        public HospSpot? HospPickTarget(Player me, V2 aimAt)
        {
            HospSpot? best = null;
            float bestD = float.MaxValue;
            int c = Expansion.GridCell;
            foreach (var s in HospSpots(me))
            {
                if (!s.Free)
                    continue;
                bool inCell = aimAt.X > s.X && aimAt.X < s.X + c && aimAt.Y > s.Y && aimAt.Y < s.Y + c;
                float d = inCell ? 0f : s.Door.DistanceTo(aimAt);
                if ((inCell || d <= HospPickR) && d < bestD)
                {
                    best = s;
                    bestD = d;
                }
            }
            return best;
        }

        /// Your own game, each tick before the ability keys: while picking, Fire picks a door instead of shooting. Returns true on the
        /// tick a click lands on a free door (the cast); cancels the pick when you go down or the slot changes.
        bool LocalHospPick(Player me, V2 aimAt, bool fireDown, ref bool firing)
        {
            bool click = fireDown && !hospFireWas;
            hospFireWas = fireDown;
            if (hospFireHold && !fireDown)
                hospFireHold = false;
            if (HospPickSlot >= 0 && (me.Downed || HoldControls || State == MatchState.GameOver || me.Slots[HospPickSlot].Id != AbilityId.FieldHospital || !HospOnGrid))
                HospPickSlot = -1;
            if (HospPickSlot < 0)
            {
                if (hospFireHold)
                    firing = false;
                return false;
            }
            firing = false;
            if (!click || !HospPickTarget(me, aimAt).HasValue)
                return false;
            hospFireHold = true;
            return true;
        }

        /// Any free door spot in reach (the pick only starts when there is one).
        public bool AnyHospSpot(Player me)
        {
            foreach (var s in HospSpots(me))
                if (s.Free)
                    return true;
            return false;
        }

        /// A bot's pick: the free door nearest itself (a Grid run), or its own spot for the tent.
        V2 HospBotTarget(Player me)
        {
            if (!HospOnGrid)
                return me.Pos;
            HospSpot? best = null;
            foreach (var s in HospSpots(me))
                if (s.Free && (best == null || s.Door.DistanceSq(me.Pos) < best.Value.Door.DistanceSq(me.Pos)))
                    best = s;
            return best.HasValue ? best.Value.Door : me.Pos;
        }

        // --- Host ---------------------------------------------------------------------------------------------------

        /// Host: the cast. On the lattice `target` is the door clicked: the cell behind it opens; anywhere else a tent goes up at it.
        void CastHospital(Player p, V2 target, bool cap)
        {
            Zone z;
            if (HospOnGrid)
            {
                HospSpot? spot = null;
                foreach (var s in HospSpots(p))
                    if (s.Free && s.Door.CloserThan(target, 60f) && (spot == null || s.Door.DistanceSq(target) < spot.Value.Door.DistanceSq(target)))
                        spot = s;
                if (spot == null)
                    spot = HospPickTarget(p, target);  // (a bot, or the click slid a little)
                if (spot == null)
                {
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.3f, 0.35f), 40f, 0.3f, Sfx.None);
                    return;
                }
                var s0 = spot.Value;
                var rooms = Expansion.HospRoomFor(Expanded, s0.X, s0.Y);
                for (int i = 0; i < rooms.Count; i++)
                {
                    var q = rooms[i];
                    q.Kind = Arena.RoomKind.Hospital;
                    q.Owner = p.Id;
                    q.Unseen = false;
                    rooms[i] = q;
                }
                Expanded.AddRange(rooms);
                expWorld = null;
                LayoutAge = 0f;
                BroadcastExpansion();
                z = AddZone(ZoneKind.FieldHospital, p, s0.Mid, Expansion.GridCell / 2f, HospLife);
                z.Origin = s0.Door;
                z.Aim = (s0.Door - s0.Mid).Angle;  // replicated: the way out (the door)
                BroadcastFx(FxKind.Ring, s0.Door, new Rgba(1f, 0.8f, 0.3f), 120f, 0.6f, Sfx.Shockwave);
            }
            else
            {
                V2 to = target - p.Pos;
                V2 at = ClampToArena(to.Length > HospTentReach ? p.Pos + to.Normalized() * HospTentReach : target, HospTentRadius);
                z = AddZone(ZoneKind.FieldHospital, p, at, HospTentRadius, HospLife);
                z.Origin = at;
                z.Aim = float.NaN;  // a tent: no door
                BroadcastFx(FxKind.Ring, at, new Rgba(0.4f, 1f, 0.55f), HospTentRadius, 0.6f, Sfx.Shockwave);
            }
            z.Cap = cap;
            z.TickT = 0.5f;  // the first orbs pop out as it opens
            z.HealBack = HospOrbHeal * z.HealMult;
            castHits++;
            Sounds.Add(Sfx.Ability);
        }

        /// Host: may this orb drift to this player? A hospital's orbs only go to players inside it (never through its walls).
        bool HospLets(Orb o, Player p)
        {
            if (o.Hosp == 0)
                return true;
            Zone z;
            return Zones.TryGetValue(o.Hosp, out z) && InHospital(z, p.Pos);
        }

        /// Is the zone's hospital a room (a door on the lattice) rather than a tent?
        public static bool HospIsRoom(Zone z) => !float.IsNaN(z.Aim);

        /// Is `pos` (a body of radius r) inside the hospital: the cell, or the tent's ring?
        public static bool InHospital(Zone z, V2 pos, float r = 0f)
        {
            if (!HospIsRoom(z))
                return pos.CloserThan(z.Pos, z.Radius + r);
            return Math.Abs(pos.X - z.Pos.X) < z.Radius + r && Math.Abs(pos.Y - z.Pos.Y) < z.Radius + r;
        }

        /// Host, each tick from TickZones: orbs, shrine, the gate, and the countdown.
        void TickHospital(Zone z, float dt)
        {
            if (HospIsRoom(z) && Expansion.HospitalAt(Expanded, z.Pos) < 0)
            {
                z.T = 0f;  // its room is gone (a map reset took the map back to the hub): it ends with it
                return;
            }
            // Orbs: up to the cap, one back every few seconds.
            int have = 0;
            foreach (var o in Orbs.Values)
                if (o.Hosp == z.Id)
                    have++;
            int max = z.Cap ? HospOrbsMastered : HospOrbs;
            z.TickT -= dt;
            if (z.T > HospEject && have < max && z.TickT <= 0f)
            {
                z.TickT = z.Cap ? HospOrbEveryMastered : HospOrbEvery;
                int add = z.Duration - z.T < 1.5f ? max - have : 1;  // it opens with the full set
                for (int k = 0; k < add; k++)
                    SpawnHospOrb(z, have + k, max);
            }
            // The shrine: each player once.
            foreach (var q in Players.Values)
            {
                int bit = 1 << (q.Id & 31);
                if (q.Downed || (z.Side & bit) != 0 || !q.Pos.CloserThan(z.Pos, Shrines.Reach + q.Def.BodyR))
                    continue;
                z.Side |= bit;
                q.Wound = 0f;
                q.Hp = Math.Max(q.Hp, q.HealCap);
                q.OverHp = Math.Max(q.OverHp, q.MaxHp * HospOverFrac);
                BroadcastFx(FxKind.Ring, z.Pos, new Rgba(0.4f, 1f, 0.55f), 110f, 0.6f, Sfx.Revive);
                BroadcastFx(FxKind.Ring, q.Pos, new Rgba(1f, 0.85f, 0.35f), 60f, 0.5f, Sfx.None);
            }
            // The gate: enemies are put back out, their shots burn away.
            foreach (var e in Enemies.Values)
            {
                if (IsSpawning(e) || !InHospital(z, e.Pos, e.Radius))
                    continue;
                e.Pos = HospPushOut(z, e.Pos, e.Radius + 2f);
            }
            var burn = eraseScratch;
            burn.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && InHospital(z, b.Pos))
                    burn.Add(b.Id);
            foreach (int id in burn)
                KillBullet(id);
        }

        /// Where an enemy inside the hospital is put: just outside its nearest edge (a room's), or its ring (a tent's).
        static V2 HospPushOut(Zone z, V2 pos, float r)
        {
            if (!HospIsRoom(z))
            {
                V2 away = pos - z.Pos;
                return z.Pos + (away.IsZero ? V2.FromAngle(z.Id) : away.Normalized()) * (z.Radius + r);
            }
            float dx = pos.X - z.Pos.X, dy = pos.Y - z.Pos.Y, h = z.Radius + r;
            if (h - Math.Abs(dx) < h - Math.Abs(dy))
                return new V2(z.Pos.X + (dx < 0f ? -h : h), pos.Y);
            return new V2(pos.X, z.Pos.Y + (dy < 0f ? -h : h));
        }

        void SpawnHospOrb(Zone z, int i, int max)
        {
            float ring = (HospIsRoom(z) ? Expansion.GridCell : HospTentRadius * 2f) * HospOrbRing * (HospIsRoom(z) ? 1f : 0.8f);
            V2 at = z.Pos + V2.FromAngle(MathX.Tau * i / max + 0.3f) * ring;
            var o = new Orb { Id = nextOrbId++, Med = true, MedHeal = z.HealBack, Pos = at, Life = z.T, Hosp = z.Id };
            o.Disp = z.Pos;  // they pop out of the shrine
            Orbs[o.Id] = o;
        }

        /// Host: the time is up. Everyone inside is put out at its door, the orbs go, and the room comes off the map.
        void EndHospital(Zone z)
        {
            var gone = new List<int>();
            foreach (var o in Orbs.Values)
                if (o.Hosp == z.Id)
                    gone.Add(o.Id);
            foreach (int id in gone)
                Orbs.Remove(id);
            if (!HospIsRoom(z) || Expansion.HospitalAt(Expanded, z.Pos) < 0)
            {
                BroadcastFx(FxKind.Ring, z.Pos, new Rgba(0.4f, 1f, 0.55f), z.Radius, 0.4f, Sfx.None);
                return;  // (a tent, or the map was reset under it)
            }
            V2 outward = V2.FromAngle(z.Aim), side = outward.Orthogonal();
            int n = 0;
            foreach (var q in Players.Values)
            {
                if (!InHospital(z, q.Pos, -2f))
                    continue;
                V2 to = z.Origin + outward * HospOut + side * ((n % 3) - 1) * 40f + outward * (n / 3) * 40f;
                n++;
                MovePlayer(q, to);
            }
            BroadcastFx(FxKind.Ring, z.Origin, new Rgba(1f, 0.8f, 0.3f), 90f, 0.5f, n > 0 ? Sfx.Blink : Sfx.None);
            if (Expansion.RemoveHospital(Expanded, z.Pos))
            {
                expWorld = null;
                LayoutAge = 0f;
                BroadcastExpansion();
            }
        }

        /// Is this player's own Field Hospital up (the cooldown waits for it to come down: TickSlots)?
        public bool HospitalOut(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.FieldHospital && z.Owner == p.Id && z.T > 0f)
                    return true;
            return false;
        }

        /// Is a Field Hospital up anywhere (no run save while one is)?
        public bool HospitalUp
        {
            get
            {
                foreach (var z in Zones.Values)
                    if (z.Kind == ZoneKind.FieldHospital)
                        return true;
                return false;
            }
        }
    }

    public static partial class Expansion
    {
        /// The one-cell room a Field Hospital would build with its corner at (x, y), or null where a piece can't go.
        public static List<ExpansionRoom> HospRoomFor(IList<ExpansionRoom> built, int x, int y)
        {
            string why;
            return PieceShapeRooms(built, PieceShape.Mono, 0, x, y, out why);
        }

        /// Does room r share any wall with the box (x0, y0)-(x1, y1)?
        public static bool Touches(ExpansionRoom r, int x0, int y0, int x1, int y1) => SharedRun(r.X, r.Y, r.X1, r.Y1, x0, y0, x1, y1) > 0;

        /// A Field Hospital's room: no door spots on it, nothing hangs off it, and it isn't counted as a built room.
        public static bool IsHospital(ExpansionRoom r) => r.Kind == RoomKind.Hospital;

        /// The index of the Field Hospital room round `mid` (-1: none).
        public static int HospitalAt(IList<ExpansionRoom> rooms, V2 mid)
        {
            for (int i = rooms.Count - 1; i >= 0; i--)
            {
                var r = rooms[i];
                if (IsHospital(r) && mid.X >= r.X && mid.X <= r.X1 && mid.Y >= r.Y && mid.Y <= r.Y1)
                    return i;
            }
            return -1;
        }

        /// Take the Field Hospital room round `mid` off the map, fixing the parents of the rooms after it. False if there was none.
        public static bool RemoveHospital(List<ExpansionRoom> rooms, V2 mid)
        {
            int i = HospitalAt(rooms, mid);
            if (i >= 0)
            {
                var r = rooms[i];
                rooms.RemoveAt(i);
                int area = i + 1;  // (a room's Parent is an area index: 0 the hub, room j is area j + 1)
                for (int j = 0; j < rooms.Count; j++)
                {
                    var q = rooms[j];
                    if (q.Parent > area)
                    {
                        q.Parent--;
                        rooms[j] = q;
                    }
                    else if (q.Parent == area)
                    {
                        q.Parent = r.Parent;  // (never happens: nothing hangs off a hospital; kept safe)
                        rooms[j] = q;
                    }
                }
                return true;
            }
            return false;
        }
    }
}
