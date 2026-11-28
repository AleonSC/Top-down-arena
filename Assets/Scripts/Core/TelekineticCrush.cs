using System;
using System.Collections.Generic;

namespace Arena
{
    /// The DPS Arcanist's Telekinetic Crush (1st ability; Docs/Arcanist.md). A toggle that runs on a force meter instead of a
    /// cooldown: press to open a grip (ZoneKind.ArcCrush, CrushGripR) at your cursor. It follows the cursor on a springy lag, up to
    /// CrushLeash from you, lifting the regular enemies it touches (up to CrushHoldMax; heavies are only slowed, bosses untouched):
    /// they're carried round inside it, can't move or shoot, and are squeezed for CrushSqueeze a second. Enemy shots that enter it
    /// are caught and orbit inside (Zone.Aim counts them). The grip drains the meter (CrushDrain + CrushDrainPer per enemy held);
    /// press again to let go:
    ///   the grip nearly still (under CrushFlingMin): Slam, everything held is crushed together (CrushSlamDmg + CrushSlamPer per
    ///     other enemy) and stunned;
    ///   the grip moving: Fling, everything held flies on at the grip's speed, and hitting a wall, a prop or another enemy at
    ///     CrushFlingMin or more hurts it (CrushImpact) and the enemy it hits (CrushStruck), stunning both;
    ///   either way the caught shots fly back out as spectral blades (CrushBladeDmg): along the throw, or all round on a slam.
    /// The meter refills while the grip is closed, from CrushDelay after it closed, at the ability's cooldown (so cooldown cards
    /// speed it up); it takes CrushMinOn to open. Run it dry and the grip lets go on its own and burns out (CrushLockTime).
    /// The ability slot mirrors the meter (TickCrush), so every HUD shows it: a charge while it can open, the timer counting to that.
    /// Player.CrushOn and CrushMeter are replicated (SyncArcanist); the grip is its zone. Look: DrawArcCrush (Game/ArcanistLook.cs),
    /// mockup https://claude.ai/artifact/JkkFovW3SgSusrqHoiuYNe. Mastered: the grip is 40% wider.
    public partial class Game
    {
        public const float CrushGripR = 70f, CrushLeash = 420f, CrushMinOn = 0.25f, CrushZoneLife = 3600f;
        public const float CrushDrain = 0.22f, CrushDrainPer = 0.03f, CrushDelay = 0.8f, CrushLockTime = 1.5f;
        public const int CrushHoldMax = 6, CrushCatchMax = 10, CrushSlamPerMax = 4;
        public const float CrushSqueeze = 6f, CrushHeavySlow = 0.4f, CrushSpin = 1.6f;
        public const float CrushSpring = 95f, CrushDamp = 0.72f, CrushTopSpeed = 1100f;
        public const float CrushFlingMin = 220f, CrushFlingMult = 1.15f, CrushFlingDrag = 1.6f, CrushFlingTime = 1.2f;
        public const float CrushImpact = 25f, CrushImpactStun = 0.5f, CrushStruck = 15f, CrushStruckStun = 0.4f;
        public const float CrushSlamDmg = 30f, CrushSlamPer = 10f, CrushSlamStun = 0.6f;
        public const float CrushBladeDmg = 8f, CrushBladeSpeed = 600f, CrushBladeLife = 0.6f;

        class CrushHold { public int Owner, Target; public V2 Off; public float Sq; }
        class CrushFling { public int Owner, Target; public V2 Vel; public float T, Dmg; }
        readonly List<CrushHold> crushHolds = new List<CrushHold>();
        readonly List<CrushFling> crushFlings = new List<CrushFling>();
        readonly List<int> crushCut = new List<int>();
        readonly List<Enemy> crushHeld = new List<Enemy>();

        /// Pressing Telekinetic Crush again while the grip is open lets go (no meter spent for the press).
        public bool CrushRelease(Player p, int slot) => p.CrushOn && p.Slots[slot].Id == AbilityId.TelekineticCrush;

        /// The grip's radius (mastered: 40% wider).
        public static float CrushRadiusOf(bool mastered) => CrushGripR * (mastered ? 1.4f : 1f);

        /// Where the grip is pulled to: the owner's cursor (or `at`), kept within CrushLeash of them.
        public V2 CrushTarget(Player p) => CrushTarget(p, p.AimAt);
        public V2 CrushTarget(Player p, V2 at) => ClampToArena(p.Pos + (at - p.Pos).LimitLength(CrushLeash), 10f);

        /// Host: the grip of this player, if it's open.
        Zone CrushGrip(Player p)
        {
            Zone z;
            return p.CrushZone != 0 && Zones.TryGetValue(p.CrushZone, out z) && z.Kind == ZoneKind.ArcCrush ? z : null;
        }

        /// How many enemies this player's grip holds (host).
        public int CrushHolding(Player p)
        {
            int n = 0;
            foreach (var h in crushHolds)
                if (h.Owner == p.Id)
                    n++;
            return n;
        }

        /// Is this enemy in a grip or flying from one? (It isn't picked up again, and fliers don't count it as something to hit.)
        bool CrushTaken(Enemy e)
        {
            foreach (var h in crushHolds)
                if (h.Target == e.Id)
                    return true;
            foreach (var f in crushFlings)
                if (f.Target == e.Id)
                    return true;
            return false;
        }

        /// Host: Telekinetic Crush was cast (the press that opens the grip, at the cursor as it was pressed).
        void OpenCrush(Player p, V2 target)
        {
            if (p.CrushOn)
                return;
            V2 at = CrushTarget(p, target);
            var z = AddZone(ZoneKind.ArcCrush, p, at, CrushRadiusOf(castMastered), CrushZoneLife);
            p.CrushOn = true;
            p.CrushZone = z.Id;
            p.CrushVel = V2.Zero;
            p.CrushT = 0f;
            p.CrushMastered = castMastered;
            BroadcastFx(FxKind.Ring, at, z.Def.Color, z.Radius * 1.9f, 0.45f, Sfx.SpellArcane);
        }

        /// Host: the grip closes. `burnout`: the meter ran dry (it locks); `drop`: you went down or the run ended (what it holds
        /// is just let go). Otherwise a slam if the grip was nearly still, a fling if it was moving.
        void CloseCrush(Player p, bool burnout, bool drop)
        {
            if (!p.CrushOn)
                return;
            var z = CrushGrip(p);
            p.CrushOn = false;
            p.CrushOff = 0f;
            p.CrushZone = 0;
            if (burnout)
                p.CrushLockT = CrushLockTime;
            V2 at = z != null ? z.Pos : p.Pos;
            float dm = z != null ? z.DmgMult : p.Stats.DmgMult, speed = p.CrushVel.Length, radius = z != null ? z.Radius : CrushGripR;
            int shots = z != null ? (int)z.Aim : 0;
            if (z != null)
                z.T = 0f;  // TickZones takes it away (ArcZoneEnds leaves it be)
            crushHeld.Clear();
            for (int i = crushHolds.Count - 1; i >= 0; i--)
            {
                var h = crushHolds[i];
                if (h.Owner != p.Id)
                    continue;
                Enemy e;
                if (TryFoe(h.Target, out e) && e.Hp > 0f)
                    crushHeld.Add(e);
                crushHolds.RemoveAt(i);
            }
            var col = Kits.Get(ZoneKind.ArcCrush).Color;
            if (drop)
            {
                foreach (var e in crushHeld)
                    Stun(e, 0.3f);
                return;
            }
            bool fling = speed >= CrushFlingMin;
            if (fling)
            {
                V2 dir = p.CrushVel / speed;
                foreach (var e in crushHeld)
                {
                    V2 off = e.Pos - at;
                    crushFlings.Add(new CrushFling { Owner = p.Id, Target = e.Id, Vel = p.CrushVel * CrushFlingMult + off * 1.6f, Dmg = dm });
                }
                for (int k = 0; k < shots; k++)
                {
                    V2 d = V2.FromAngle(dir.Angle + (shots > 1 ? (k / (float)(shots - 1) - 0.5f) * 0.7f : 0f));
                    SpawnBullet(Defs.SpectralBlade, at + d * 12f, d * CrushBladeSpeed, 0f, CrushBladeLife, CrushBladeDmg * dm, p.Id);
                }
                BroadcastFx(FxKind.Ring, at, burnout ? new Rgba(1f, 0.33f, 0.41f) : col, radius * 0.4f, 0.3f, crushHeld.Count > 0 ? Sfx.Throw : Sfx.None);
            }
            else
            {
                int n = crushHeld.Count;
                float dmg = (CrushSlamDmg + CrushSlamPer * Math.Min(CrushSlamPerMax, Math.Max(0, n - 1))) * dm;
                foreach (var e in crushHeld)
                {
                    KnuckleCarry(e, at + (e.Pos - at) * 0.35f);  // crushed in together
                    HurtEnemy(e, dmg, p.Id);
                    e.FrozenT = 0f;
                    e.StunT = 0f;
                    Stun(e, CrushSlamStun);
                }
                for (int k = 0; k < shots; k++)
                {
                    V2 d = V2.FromAngle(MathX.Tau * k / shots + p.CrushT);
                    SpawnBullet(Defs.SpectralBlade, at + d * 12f, d * CrushBladeSpeed, 0f, CrushBladeLife, CrushBladeDmg * dm, p.Id);
                }
                if (n > 0)
                    BroadcastFx(FxKind.Boom, at, col, radius * 0.9f, 0.4f, Sfx.BigExplode);
                else
                    BroadcastFx(FxKind.Ring, at, burnout ? new Rgba(1f, 0.33f, 0.41f) : col, radius * 0.5f, 0.3f, shots > 0 ? Sfx.SpellArcane : Sfx.None);
            }
            if (burnout)
                BroadcastFx(FxKind.Ring, at, new Rgba(1f, 0.33f, 0.41f), radius * 1.6f, 0.4f, Sfx.None);
        }

        /// Host, every tick (TickArcanist): the meter, the grip following the cursor, what it holds and catches, and the fliers.
        void TickCrush(float dt)
        {
            foreach (var p in Players.Values)
            {
                int slot = -1;
                for (int i = 0; i < p.Slots.Length; i++)
                    if (p.Slots[i].Id == AbilityId.TelekineticCrush)
                        slot = i;
                if (slot < 0 || p.Downed || State == MatchState.GameOver || p.CrushOn && CrushGrip(p) == null)
                    CloseCrush(p, false, true);
                if (slot < 0)
                {
                    p.CrushMeter = 1f;
                    p.CrushLockT = 0f;
                    continue;
                }
                float cd = Math.Max(0.5f, Cooldown(p, slot));
                if (p.CrushOn)
                {
                    var z = CrushGrip(p);
                    p.CrushT += dt;
                    V2 want = CrushTarget(p);
                    float damp = 2f * (float)Math.Sqrt(CrushSpring) * CrushDamp;
                    p.CrushVel = (p.CrushVel + ((want - z.Pos) * CrushSpring - p.CrushVel * damp) * dt).LimitLength(CrushTopSpeed);
                    V2 moved = ClampToArena(z.Pos + p.CrushVel * dt, 10f);
                    if (dt > 0f)
                        p.CrushVel = (moved - z.Pos) / dt;  // (stopped by the arena's edge: so is its speed)
                    z.Pos = moved;
                    z.T = z.Duration;  // it lasts as long as it's held open
                    int held = HoldInGrip(p, z, dt);
                    CatchInGrip(z);
                    if (!HangoutOn)
                        p.CrushMeter -= (CrushDrain + CrushDrainPer * held) * dt;
                    if (p.CrushMeter <= 0f)
                    {
                        p.CrushMeter = 0f;
                        CloseCrush(p, true, false);
                    }
                }
                else if (HangoutOn)
                {
                    p.CrushMeter = 1f;  // the Hangout (HangoutGame.cs): no cooldowns
                    p.CrushLockT = 0f;
                }
                else if (p.CrushLockT > 0f)
                    p.CrushLockT = Math.Max(0f, p.CrushLockT - dt);
                else if ((p.CrushOff += dt) >= CrushDelay)
                    p.CrushMeter = Math.Min(1f, p.CrushMeter + dt / cd);
                // The slot mirrors the meter: a charge while the grip can open; the timer counts to that (or, open, to running dry).
                var s = p.Slots[slot];
                s.MaxCharges = 1;
                if (p.CrushOn)
                {
                    s.Charges = 0;
                    s.Timer = p.CrushMeter / (CrushDrain + CrushDrainPer * CrushHolding(p));
                }
                else if (p.CrushLockT <= 0f && p.CrushMeter >= CrushMinOn)
                {
                    s.Charges = 1;
                    s.Timer = 0f;
                }
                else
                {
                    s.Charges = 0;
                    s.Timer = p.CrushLockT + Math.Max(0f, CrushDelay - p.CrushOff) + Math.Max(0f, CrushMinOn - p.CrushMeter) * cd;
                }
            }
            TickCrushFlings(dt);
        }

        /// The grip carries what it holds round inside it, squeezing it, and picks up the regular enemies it touches. Returns how
        /// many it holds.
        int HoldInGrip(Player p, Zone z, float dt)
        {
            int n = 0;
            float a = CrushSpin * dt, cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
            for (int i = crushHolds.Count - 1; i >= 0; i--)
            {
                var h = crushHolds[i];
                if (h.Owner != p.Id)
                    continue;
                Enemy e;
                if (!TryFoe(h.Target, out e) || !Targetable(e))
                {
                    crushHolds.RemoveAt(i);
                    continue;
                }
                V2 off = new V2(h.Off.X * cs - h.Off.Y * sn, h.Off.X * sn + h.Off.Y * cs);
                float len = off.Length, keep = Math.Min(len, z.Radius * 0.5f);
                if (len > 0.01f)
                    off *= (len + (keep - len) * Math.Min(1f, dt * 5f)) / len;  // drawn in toward the middle
                h.Off = off;
                Stun(e, 0.2f);  // held: it can't move or shoot
                e.KnockLeft = V2.Zero;
                KnuckleCarry(e, z.Pos + off);
                if (e.Pos.FartherThan(z.Pos, z.Radius + e.Radius + 30f))
                {
                    crushHolds.RemoveAt(i);  // snagged on a wall as the grip swept on: it drops
                    continue;
                }
                if ((h.Sq += dt) >= 0.5f)
                {
                    h.Sq -= 0.5f;
                    HurtEnemy(e, CrushSqueeze * 0.5f * z.DmgMult, p.Id);
                }
                n++;
            }
            foreach (var e in AllFoes(by: p.Id))
            {
                if (!Targetable(e) || e.Def.IsBoss || !e.Pos.CloserThan(z.Pos, z.Radius + e.Radius * 0.4f))
                    continue;
                if (Heavy(e))
                {
                    Slow(e, 0.3f, CrushHeavySlow);  // too big to lift: dragged at
                    continue;
                }
                if (n >= CrushHoldMax || CrushTaken(e))
                    continue;
                crushHolds.Add(new CrushHold { Owner = p.Id, Target = e.Id, Off = e.Pos - z.Pos });
                Stun(e, 0.2f);
                n++;
            }
            return n;
        }

        /// Enemy shots (not unblockable ones) that enter the grip are caught; Zone.Aim counts them (the look orbits them inside).
        void CatchInGrip(Zone z)
        {
            crushCut.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.CloserThan(z.Pos, z.Radius))
                    crushCut.Add(b.Id);
            foreach (int id in crushCut)
            {
                KillBullet(id);
                if (z.Aim < CrushCatchMax)
                    z.Aim += 1f;
            }
        }

        /// Flung enemies fly on, slowing; one that hits a wall, a prop or another enemy fast enough is hurt, and so is what it hit.
        void TickCrushFlings(float dt)
        {
            var col = Kits.Get(ZoneKind.ArcCrush).Color;
            for (int i = crushFlings.Count - 1; i >= 0; i--)
            {
                var f = crushFlings[i];
                Enemy e;
                if (!TryFoe(f.Target, out e) || e.Hp <= 0f)
                {
                    crushFlings.RemoveAt(i);
                    continue;
                }
                Stun(e, 0.2f);
                e.KnockLeft = V2.Zero;
                f.T += dt;
                float speed = f.Vel.Length;
                bool wall = KnuckleCarry(e, e.Pos + f.Vel * dt);
                Enemy into = null;
                if (speed >= CrushFlingMin)
                    foreach (var o in AllFoes(by: f.Owner))
                        if (o != e && Targetable(o) && o.Pos.WithinReach(e.Pos, o.Radius + e.Radius) && !CrushTaken(o))
                        {
                            into = o;
                            break;
                        }
                if (speed >= CrushFlingMin && (wall || into != null))
                {
                    HurtEnemy(e, CrushImpact * f.Dmg, f.Owner);
                    e.FrozenT = 0f;
                    e.StunT = 0f;
                    Stun(e, CrushImpactStun);
                    if (into != null)
                    {
                        HurtEnemy(into, CrushStruck * f.Dmg, f.Owner);
                        if (!Heavy(into))
                            KnockEnemy(into, f.Vel / speed * 50f);
                        if (!into.Def.IsBoss)
                            Stun(into, CrushStruckStun);
                    }
                    BroadcastFx(FxKind.Boom, e.Pos, col, e.Radius + 26f, 0.3f, Sfx.Hit);
                    crushFlings.RemoveAt(i);
                    continue;
                }
                f.Vel *= (float)Math.Exp(-CrushFlingDrag * dt);
                if (wall || f.Vel.Length < 60f || f.T >= CrushFlingTime)
                {
                    Stun(e, 0.4f);  // it lands, dazed
                    crushFlings.RemoveAt(i);
                }
            }
        }
    }
}
