using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Turnkey, the Warden faction's trapper (drawn in Game/TurnkeyLook.cs). It never fights: it runs from every player
    /// near it (a little slower than any of them, and it slides along the arena's edge instead of backing into a corner)
    /// and stops to work, which is the window to catch it.
    ///   Plants a trap every TurnkeyTrapEvery s (TurnkeyPlant s standing still; its own spot, so a chase runs over them):
    ///     a Floor Mine (arms, then beeps and blows up when you step close; shoot it to blow it on enemies),
    ///     a Shackle Trap (arms, then snaps shut on the first player over it: rooted for SnareRoot s; shoot it to break it),
    ///     or a Mop Slick (PatchKind.Slick: a wet floor that slows you, SlickSlow).
    ///     At most TurnkeyTraps mines and snares each: a new one past that fizzles its oldest.
    ///   Builds a Cell Door every TurnkeyDoorEvery s (TurnkeyBuild s; at most TurnkeyDoors standing) into the nearest free
    ///     stretch of outer wall (DoorSpot: floor in front, nothing behind, DoorApart from another door; the arena's edge
    ///     where there are no walls), facing in: a barred cell that lets a Rioter out every DoorEvery s (at most DoorKids of
    ///     its own out at once). The door stands there while it's built (shootable); if its Turnkey dies first it goes. When
    ///     its Turnkey dies the built doors jam shut: no more Rioters, but they stand until broken or the wave ends.
    /// Traps and doors don't hold a wave open (Foes) and go when it's cleared (ClearPods). Phases: Turnkey 0 new, 1 running,
    /// 2 planting, 3 building (PhaseT: the work left, or until the next trap; T2: until the next door). Traps: 0 arming,
    /// 1 armed, 2 triggered (PhaseT: the beep, or the snapped snare lingering), 3 fizzled (T2: age). Door: 0 new, 1 working
    /// (T2: until the next Rioter, PhaseT: the door swinging open), 2 jammed, 3 being built (PhaseT: the work left). PhaseT and T2 are in the snapshot (SyncNonShooter).
    public partial class Game
    {
        public const float TurnkeyFlee = 340f, TurnkeyKeep = 430f, TurnkeyTrapEvery = 3.2f, TurnkeyPlant = 0.8f, TurnkeyBuild = 1.2f,
            TurnkeyDoorFirst = 4f, TurnkeyDoorEvery = 10f, TurnkeyEdge = 150f;
        public const int TurnkeyTraps = 3, TurnkeyDoors = 2;
        public const float FloorMineArm = 0.8f, FloorMineTrigger = 34f, FloorMineBeep = 0.35f, FloorMineBlast = 64f, FloorMineDamage = 22f, FloorMineEnemyDamage = 20f, TrapLife = 20f;
        public const float SnareArm = 0.8f, SnareTrigger = 26f, SnareRoot = 0.9f, SnareDamage = 8f, SnareLinger = 0.6f;
        public const float SlickRadius = 40f, SlickWarn = 0.3f, SlickLife = 14f, SlickSlow = 0.6f;
        public const float DoorFirst = 1.5f, DoorEvery = 4.5f, DoorSwing = 0.5f;
        /// A door's body stands DoorIn in from its wall's face; spots are DoorApart apart, DoorEdge from a wall's end, at most
        /// DoorReach from its Turnkey.
        public const float DoorIn = 22f, DoorApart = 150f, DoorEdge = 70f, DoorReach = 900f;
        public const int DoorKids = 3;

        /// The Turnkey and what it leaves behind (NonShooters.cs IsNonShooter: synced and drawn with them).
        public static bool IsTurnkeyKin(EnemyType t) => t == EnemyType.Turnkey || t == EnemyType.CellDoor || t == EnemyType.FloorMine || t == EnemyType.ShackleTrap;

        /// Planted things: they don't hold a wave open (Foes), don't bump into you, and go when the wave is cleared.
        public static bool IsPlanted(EnemyType t) => t == EnemyType.FloorMine || t == EnemyType.ShackleTrap || t == EnemyType.CellDoor;

        /// ...but a door, unlike the traps, is a target with a health bar.
        public static bool IsTrap(EnemyType t) => IsPlanted(t) && t != EnemyType.CellDoor;

        /// Runs from every player near it, sliding off the arena's edge; otherwise keeps its distance. Stands still to work.
        sealed class TurnkeyMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                if (e.Phase >= 2)
                {
                    speed = 0f;
                    return V2.Zero;
                }
                return g.TurnkeyRun(e, dir, dist);
            }
        }

        static void TurnkeyBrains(Brain[] t)
        {
            t[(int)EnemyType.Turnkey] = new Brain { Move = new TurnkeyMove(), Special = new CodeSpecial((g, e, p, dt) => g.TurnkeySpecial(e, dt)) };
            var still = new StillMove();
            t[(int)EnemyType.CellDoor] = new Brain { Move = still, Special = new CodeSpecial((g, e, p, dt) => g.DoorSpecial(e, dt)) };
            t[(int)EnemyType.FloorMine] = new Brain { Move = still, Special = new CodeSpecial((g, e, p, dt) => g.TrapSpecial(e, dt)) };
            t[(int)EnemyType.ShackleTrap] = new Brain { Move = still, Special = new CodeSpecial((g, e, p, dt) => g.TrapSpecial(e, dt)) };
        }

        V2 TurnkeyRun(Enemy e, V2 dir, float dist)
        {
            V2 away = V2.Zero;
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                V2 off = e.Pos - p.Pos;
                float d = off.Length;
                if (d < TurnkeyFlee && d > 0.01f)
                    away += off / d * (1f - d / TurnkeyFlee + 0.35f);  // the nearer, the harder it runs
            }
            if (away.LengthSq < 0.0001f)
                return dist > TurnkeyKeep + 60f ? dir * 0.6f : dir.Orthogonal() * e.Strafe * 0.5f;  // nobody close: keeps you in sight, edging round
            away = away.Normalized();
            // Near a wall it slides along it (what heads into the wall is stripped, all of it at the wall), and pinned in a
            // corner it runs along the wall that leads toward the middle: round you, not into the corner.
            V2 lo = ArenaMin, hi = ArenaMax, mid = (lo + hi) * 0.5f;
            V2 v = Slide(e.Pos, away, lo, hi);
            if (v.LengthSq < 0.25f)
            {
                V2 side = away.Orthogonal();
                if (side.Dot(mid - e.Pos) < 0f)
                    side = -side;
                v = Slide(e.Pos, away + side * 1.2f, lo, hi);
            }
            return v;
        }

        static V2 Slide(V2 pos, V2 v, V2 lo, V2 hi)
        {
            float l = MathX.Clamp01(1f - (pos.X - lo.X) / TurnkeyEdge), r = MathX.Clamp01(1f - (hi.X - pos.X) / TurnkeyEdge);
            float t = MathX.Clamp01(1f - (pos.Y - lo.Y) / TurnkeyEdge), b = MathX.Clamp01(1f - (hi.Y - pos.Y) / TurnkeyEdge);
            if (v.X < 0f) v.X *= 1f - l; else v.X *= 1f - r;
            if (v.Y < 0f) v.Y *= 1f - t; else v.Y *= 1f - b;
            return v;
        }

        // --- The Turnkey -----------------------------------------------------------------------------------------------

        void TurnkeySpecial(Enemy e, float dt)
        {
            if (e.Phase == 0)
            {
                e.Phase = 1;
                e.PhaseT = 1.4f;  // its first trap soon after it arrives
                e.T2 = TurnkeyDoorFirst;
            }
            if (e.SpawnT > 0f)
                return;
            e.T2 -= dt;
            e.PhaseT -= dt;
            if (e.PhaseT > 0f)
                return;
            switch (e.Phase)
            {
                case 1:
                    V2 spot = V2.Zero, inward = V2.Zero;
                    bool door = e.T2 <= 0f && Owned(e, EnemyType.CellDoor, false) < TurnkeyDoors && DoorSpot(e.Pos, out spot, out inward);
                    e.Phase = (byte)(door ? 3 : 2);
                    e.PhaseT = door ? TurnkeyBuild : TurnkeyPlant;
                    if (door)
                        StartDoor(e, spot, inward);
                    break;
                case 2:
                    PlantTrap(e);
                    e.Phase = 1;
                    e.PhaseT = TurnkeyTrapEvery * Rand(0.85f, 1.15f);
                    break;
                case 3:  // (the door finishes itself: DoorSpecial)
                    e.Phase = 1;
                    e.T2 = TurnkeyDoorEvery;
                    e.PhaseT = TurnkeyTrapEvery * 0.6f;
                    break;
            }
        }

        readonly List<Enemy> trapScratch = new List<Enemy>();

        /// Its live things of a type (traps not yet set off), oldest first in trapScratch.
        int Owned(Enemy owner, EnemyType type, bool armedOnly)
        {
            trapScratch.Clear();
            foreach (var o in Enemies.Values)
                if (o.Type == type && o.ParentId == owner.Id && o.Hp > 0f && (!armedOnly || o.Phase < 2))
                    trapScratch.Add(o);
            trapScratch.Sort((a, b) => b.T2.CompareTo(a.T2));
            return trapScratch.Count;
        }

        void PlantTrap(Enemy e)
        {
            int kind = rng.Next(3);
            if (kind == 2)
            {
                SpawnPatchAt(PatchKind.Slick, ClampToArena(e.Pos, SlickRadius * 0.5f), SlickWarn, SlickWarn + SlickLife, SlickRadius, SlickRadius);
                return;
            }
            var type = kind == 0 ? EnemyType.FloorMine : EnemyType.ShackleTrap;
            if (Owned(e, type, true) >= TurnkeyTraps)
                Fizzle(trapScratch[0]);  // (NonShooters.cs: goes quietly)
            var trap = SpawnEnemyAt(type, e.Pos, 0f, e.Id);
            Planted(trap);
            trap.Hp = trap.MaxHp = 1f;
        }

        /// Starts a door at its spot on the wall, facing in: it stands there being built (phase 3) while its Turnkey works.
        void StartDoor(Enemy e, V2 at, V2 inward)
        {
            var d = SpawnEnemyAt(EnemyType.CellDoor, at, 0f, e.Id);
            Planted(d);
            d.Facing = inward.Angle;  // the bars face in, the Rioters come out into the arena
            d.Phase = 3;
            d.PhaseT = TurnkeyBuild;
            e.Facing = (at - e.Pos).Angle;
        }

        /// The nearest free spot on an outer wall to `from` (within DoorReach): where a door's body goes, and the way in.
        /// In the Rooms world, the faces of its walls with floor in front and none behind; elsewhere the arena's edges.
        public bool DoorSpot(V2 from, out V2 at, out V2 inward)
        {
            at = V2.Zero;
            inward = V2.Zero;
            float best = DoorReach;
            var walls = RoomsOn ? CurrentWorld.Walls() : null;
            if (walls != null && walls.Length > 0)
            {
                foreach (var w in walls)
                {
                    if (w.Round)
                        continue;
                    bool flat = w.HW >= w.HH;  // along x
                    float a0 = flat ? w.Pos.X - w.HW : w.Pos.Y - w.HH, a1 = flat ? w.Pos.X + w.HW : w.Pos.Y + w.HH;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        V2 n = flat ? new V2(0f, side) : new V2(side, 0f);
                        float face = flat ? w.Pos.Y + side * w.HH : w.Pos.X + side * w.HW;
                        DoorFace(from, flat, face, a0, a1, n, true, ref best, ref at, ref inward);
                    }
                }
            }
            else
            {
                V2 lo = ArenaMin, hi = ArenaMax;
                DoorFace(from, true, lo.Y, lo.X, hi.X, new V2(0f, 1f), false, ref best, ref at, ref inward);
                DoorFace(from, true, hi.Y, lo.X, hi.X, new V2(0f, -1f), false, ref best, ref at, ref inward);
                DoorFace(from, false, lo.X, lo.Y, hi.Y, new V2(1f, 0f), false, ref best, ref at, ref inward);
                DoorFace(from, false, hi.X, lo.Y, hi.Y, new V2(-1f, 0f), false, ref best, ref at, ref inward);
            }
            return best < DoorReach;
        }

        /// A wall face (along x at `face` if flat, else along y) from a0 to a1, facing n: the spot on it nearest `from`, and
        /// a step or two along it either way (to get clear of a door already there).
        void DoorFace(V2 from, bool flat, float face, float a0, float a1, V2 n, bool rooms, ref float best, ref V2 at, ref V2 inward)
        {
            if (a1 - a0 < DoorEdge * 2f)
                return;
            float u0 = Math.Max(a0 + DoorEdge, Math.Min(a1 - DoorEdge, flat ? from.X : from.Y));
            for (int k = 0; k < 5; k++)
            {
                float u = Math.Max(a0 + DoorEdge, Math.Min(a1 - DoorEdge, u0 + ((k + 1) / 2) * (k % 2 == 1 ? DoorApart : -DoorApart)));
                V2 f = flat ? new V2(u, face) : new V2(face, u), spot = f + n * DoorIn;
                float d = spot.DistanceTo(from);
                if (d < best && DoorSpotFree(f, n, rooms))
                {
                    best = d;
                    at = spot;
                    inward = n;
                }
            }
        }

        bool DoorSpotFree(V2 face, V2 n, bool rooms)
        {
            if (rooms && (!OpenFloor(face + n * 40f) || OpenFloor(face - n * (Rooms.WallT + 16f))))
                return false;  // an outer wall: floor in front of it, none behind
            V2 body = face + n * DoorIn;
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.CellDoor && o.Hp > 0f && o.Pos.CloserThan(body, DoorApart))
                    return false;
            var plan = ActiveLayout;
            if (plan != null)
                foreach (var sd in plan.Solids)
                    if (!sd.Edge && Layouts.Inside(sd, face + n * 50f, 40f))
                        return false;  // a pillar or crate in its way
            if (rooms)
                foreach (var pt in CurrentWorld.Portals)
                    if (pt.Wall && (pt.A.CloserThan(face, pt.Half + 60f) || pt.B.CloserThan(face, pt.Half + 60f)))
                        return false;
            return !InCellZone(body);
        }

        /// In one of the world's open areas (not a sealed or floating room).
        bool OpenFloor(V2 p)
        {
            foreach (var a in CurrentWorld.Areas)
                if (!a.Sealed && !a.Floating && a.Contains(p))
                    return true;
            return false;
        }

        /// Nothing a path or a modifier can make elite, and nothing that moves.
        static void Planted(Enemy o)
        {
            o.Elite = false;
            o.Traits = 0;
            o.FireT = 99f;
            o.Speed = 0f;
        }

        // --- Traps ------------------------------------------------------------------------------------------------------

        void TrapSpecial(Enemy e, float dt)
        {
            e.T2 += dt;
            bool mine = e.Type == EnemyType.FloorMine;
            if (e.Phase == 0 && e.T2 >= (mine ? FloorMineArm : SnareArm))
                e.Phase = 1;
            if (e.Phase == 1)
            {
                if (e.T2 >= TrapLife)
                {
                    Fizzle(e);
                    return;
                }
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.Pos.FartherOrAt(e.Pos, (mine ? FloorMineTrigger : SnareTrigger) + p.Def.HitR))
                        continue;
                    if (mine)
                    {
                        e.Phase = 2;
                        e.PhaseT = FloorMineBeep;
                        BroadcastFx(FxKind.Target, e.Pos, e.Def.Color, FloorMineBlast, FloorMineBeep, Sfx.None);
                    }
                    else if (p.InvulnT <= 0f)
                        SnapSnare(e, p);
                    break;
                }
            }
            else if (e.Phase == 2)
            {
                e.PhaseT -= dt;
                if (e.PhaseT <= 0f)
                {
                    if (mine)
                    {
                        e.LastHitBy = 0;
                        e.Hp = 0f;  // TurnkeyDeath blows it up
                    }
                    else
                        Fizzle(e);  // the snapped snare goes once it lets go
                }
            }
        }

        void SnapSnare(Enemy e, Player p)
        {
            e.Phase = 2;
            e.PhaseT = SnareLinger;
            e.Pos = p.Pos;  // its jaws close round the ankle
            p.StunT = Math.Max(p.StunT, SnareRoot);
            Damage(p, SnareDamage, e.Def.Name);
            BroadcastFx(FxKind.Ring, p.Pos, e.Def.Color, 30f, 0.3f, Sfx.ZombieGrab);
        }

        /// Host, as a dead enemy is cleared up (Combat.cs, by NonShooterDeath): a Floor Mine that didn't fizzle blows up.
        void TurnkeyDeath(Enemy e)
        {
            if (e.Type == EnemyType.FloorMine && e.Phase != 3)
                NonShooterBlast(e, FloorMineBlast, FloorMineDamage, FloorMineEnemyDamage, Players.ContainsKey(e.LastHitBy) ? e.LastHitBy : 0);
        }

        // --- Cell Doors ---------------------------------------------------------------------------------------------------

        void DoorSpecial(Enemy e, float dt)
        {
            if (e.Phase == 0)
            {
                e.Phase = 1;
                e.T2 = DoorFirst;
            }
            e.PhaseT = Math.Max(0f, e.PhaseT - dt);
            Enemy owner;
            bool working = Enemies.TryGetValue(e.ParentId, out owner) && owner.Hp > 0f && owner.Type == EnemyType.Turnkey;
            if (e.Phase == 3)
            {
                if (!working)
                    Fizzle(e);  // its Turnkey died at the work: the half-built door goes
                else if (e.PhaseT <= 0f)
                {
                    e.Phase = 1;
                    e.T2 = DoorFirst;
                    BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 40f, 0.35f, Sfx.None);
                }
                return;
            }
            if (e.Phase != 1)
                return;
            if (!working)
            {
                e.Phase = 2;  // its Turnkey is gone: the door jams shut
                BroadcastFx(FxKind.Spark, e.Pos, e.Def.Color, 26f, 0.3f, Sfx.None);
                return;
            }
            e.T2 -= dt;
            if (e.T2 > 0f)
                return;
            e.T2 = DoorEvery;
            if (Owned(e, EnemyType.Rioter, false) >= DoorKids)
                return;
            V2 f = V2.FromAngle(e.Facing);
            SpawnEnemyAt(EnemyType.Rioter, e.Pos + f * (e.Radius + 14f), 0.35f, e.Id);
            e.PhaseT = DoorSwing;
        }
    }
}
