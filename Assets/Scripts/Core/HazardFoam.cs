using System;

namespace Arena
{
    /// Hazard Foam: the Healer Soldier's fourth ultimate (`AbilityId.HazardFoam`, key 3 of the lobby's Ultimate row), from
    /// Docs/Mockups/hazard-foam.html (claude.ai artifact "Hazard Foam").
    ///   - The sprayer: for up to `FoamTime` s a foam sprayer replaces your weapon (`Player.FoamT`, replicated). Holding Fire
    ///     drains its tank (`Player.FoamTank`, replicated; `FoamTankMax`, mastered `FoamTankMastered`) at `FoamDrain` a second
    ///     and lobs `FoamRate` globs a second (Defs.FoamGlob) at the cursor, `FoamMinRange` to `FoamRange` px out. Your weapon's
    ///     Alt is locked; abilities and the Dodge still work; you move `FoamSlow` as fast. It ends when the time runs out, the
    ///     tank is empty, you go down, or the wave is over.
    ///   - A glob lands (`LandFoam`) and grows the lump it falls in (up to `FoamMaxR`) or starts a new one (`FoamR0`): one
    ///     `ZoneKind.HazardFoam` zone per lump, at most `FoamCap` in the arena. A lump topped up starts its life again.
    ///   - A lump is solid: players (allies too) and enemies can't walk through it (`PushOutOfFoam`, `KeepOffFoam`), and it stops
    ///     every shot, yours too (`FoamStops`, from WorldStops). Enemy shots and enemies pressed against it wear it down
    ///     (`FoamShotWear`, `FoamChew` a second; Soak/SoakMax its health, GuardFrac replicated); enemies touching it move at
    ///     `FoamEnemySlow`. Bosses aren't held: they plough through, eating `FoamBossChew` a second.
    ///   - Each lump lasts `FoamLife` s (mastered `FoamLifeMastered`), shrinking over its last `FoamFizz` s (`FoamR`), then dissolves.
    /// Look: Game/HazardFoamLook.cs. Test suite `hazardfoam`; preview `FOAM=<s since the cast>`.
    public partial class Game
    {
        public const float FoamTime = 12f, FoamTankMax = 100f, FoamTankMastered = 130f, FoamDrain = 14f, FoamSlow = 0.85f;
        public const float FoamRate = 16f, FoamSpeed = 560f, FoamRange = 300f, FoamMinRange = 50f, FoamSpread = 0.11f, FoamMuzzle = 26f;
        public const float FoamR0 = 10f, FoamMaxR = 24f, FoamGrow = 40f, FoamMerge = 0.85f, FoamHpPerR = 5f;
        public const float FoamLife = 60f, FoamLifeMastered = 90f, FoamFizz = 6f;
        public const float FoamShotWear = 10f, FoamBossShotWear = 30f, FoamChew = 8f, FoamBossChew = 80f, FoamEnemySlow = 0.45f;
        public const int FoamCap = 80;
        public const float FoamBotRange = 380f;
        public static readonly Rgba FoamCream = new Rgba(1f, 0.96f, 0.76f), FoamYellow = new Rgba(0.95f, 0.79f, 0.3f);

        /// Its entry: key 3 of the Healer Soldier's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] FoamUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.HazardFoam, Kit = Kit.Soldier, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Hazard Foam", Cooldown = 100f,
                Desc = "Strap on a foam sprayer for 12 s. Hold Fire to spray foam that sets into solid lumps: they block everyone's movement and shots for 60 s.",
                Capstone = "A 130 tank, and the foam lasts 90 s.",
                Bot = b => b.HaveTarget && b.TargetD < FoamBotRange,
            },
        };

        /// The sprayer is on.
        public static bool FoamOn(Player p) => p.FoamT > 0f && !p.Downed;

        /// A lump that still stands.
        public static bool FoamLive(Zone z) => z.Kind == ZoneKind.HazardFoam && z.T > 0f;

        /// A lump's solid radius now: full until its last FoamFizz seconds, then shrinking to a quarter as it dissolves.
        public static float FoamR(Zone z) => z.Radius * (0.25f + 0.75f * MathX.Clamp01(z.T / FoamFizz));

        /// A lump's health left (0-1): the host's own, or the replicated fraction.
        public float FoamFrac(Zone z) => IsHost && z.SoakMax > 0f ? Math.Max(0f, z.Soak / z.SoakMax) : z.GuardFrac;

        /// Any foam in the arena.
        public bool FoamUp()
        {
            foreach (var z in Zones.Values)
                if (FoamLive(z))
                    return true;
            return false;
        }

        /// Host: strap on the sprayer.
        void CastHazardFoam(Player p, bool mastered)
        {
            p.FoamT = p.UltT = FoamTime;
            p.FoamTank = mastered ? FoamTankMastered : FoamTankMax;
            p.FoamMastered = mastered;
            p.FoamGlobT = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, FoamYellow, 40f, 0.4f, Sfx.TankGasHiss);
        }

        /// Host: the sprayer comes off (time, empty, down, the wave over, or the ultimate swapped).
        void EndFoam(Player p)
        {
            if (p.FoamT <= 0f)
                return;
            p.FoamT = 0f;
            p.FoamTank = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.HazardFoam)
                p.UltT = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, FoamYellow, 26f, 0.3f, Sfx.Clang);
        }

        /// Host, every tick: the sprayer's time.
        void TickFoamSprayers(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.FoamT <= 0f)
                    continue;
                if (p.FoamT <= dt || p.FoamTank <= 0f || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.HazardFoam)
                {
                    EndFoam(p);
                    continue;
                }
                p.FoamT -= dt;
                p.UltT = p.FoamT;
            }
        }

        /// Host, from TickPlayers: the sprayer, in place of your weapon. Held Fire drains the tank and lobs globs.
        void TickFoamGun(Player p, float dt)
        {
            if (!p.Firing || p.FoamTank <= 0f || State == MatchState.GameOver)
            {
                p.FoamGlobT = 0f;
                return;
            }
            p.FoamTank = Math.Max(0f, p.FoamTank - FoamDrain * dt);
            p.FoamGlobT -= dt;
            while (p.FoamGlobT <= 0f)
            {
                p.FoamGlobT += 1f / FoamRate;
                SprayFoam(p);
            }
        }

        void SprayFoam(Player p)
        {
            float a = (p.AimAt - p.Pos).IsZero ? p.Aim : (p.AimAt - p.Pos).Angle;
            a += Rand(-FoamSpread, FoamSpread);
            V2 dir = V2.FromAngle(a), from = p.Pos + dir * FoamMuzzle;
            float want = p.AimAt.IsZero ? FoamRange : p.AimAt.DistanceTo(from);
            float range = Math.Max(FoamMinRange, Math.Min(FoamRange, want)) * Rand(0.86f, 1.05f);
            float speed = FoamSpeed * Rand(0.92f, 1.08f);
            var b = SpawnBullet(Defs.FoamGlob, from, dir * speed, 0f, range / speed, 0f, p.Id);
            b.Burst = 1;  // lands where it stops (MoveBullets: LandFoam)
            p.FoamGlobs++;
        }

        /// Host: a glob comes down: it grows the lump it falls in, or starts a new one.
        void LandFoam(Bullet b)
        {
            Player p;
            if (!Players.TryGetValue(b.Owner, out p))
                return;
            V2 at = ClampToArena(b.Pos, 8f);
            Zone best = null, nearest = null;
            float bd = float.MaxValue, nd = float.MaxValue;
            int count = 0;
            foreach (var z in Zones.Values)
            {
                if (!FoamLive(z))
                    continue;
                count++;
                float d = at.DistanceTo(z.Pos);
                if (d < nd)
                {
                    nd = d;
                    nearest = z;
                }
                if (z.Radius < FoamMaxR && d < z.Radius * FoamMerge + 6f && d < bd)
                {
                    bd = d;
                    best = z;
                }
            }
            if (best == null && count >= FoamCap)
            {
                if (nearest != null && nd < FoamMaxR * 2f)
                    nearest.T = nearest.Duration;  // full: the glob only freshens the lump it lands by
                return;
            }
            if (best != null)
            {
                float nr = Math.Min(FoamMaxR, (float)Math.Sqrt(best.Radius * best.Radius + FoamGrow));
                best.Soak += (nr - best.Radius) * FoamHpPerR;
                best.SoakMax = nr * FoamHpPerR;
                best.Radius = nr;
                best.Pos += (at - best.Pos) * 0.08f;  // the lump creeps toward where the foam piles on
                best.T = best.Duration;              // fresh foam on top: its clock starts again
                best.GuardFrac = MathX.Clamp01(best.Soak / best.SoakMax);
                return;
            }
            var nz = AddZone(ZoneKind.HazardFoam, p, at, FoamR0, p.FoamMastered ? FoamLifeMastered : FoamLife);
            nz.SoakMax = nz.Soak = FoamR0 * FoamHpPerR;
            nz.GuardFrac = 1f;
            nz.Aim = Rand(0f, MathX.Tau);  // its bubbles' turn, for the look
        }

        /// Host: a lump takes wear; it breaks apart at 0.
        void WearFoam(Zone z, float amount)
        {
            if (z.T <= 0f)
                return;
            z.Soak -= amount;
            z.GuardFrac = z.SoakMax > 0f ? MathX.Clamp01(z.Soak / z.SoakMax) : 0f;
            if (z.Soak > 0f)
                return;
            BroadcastFx(FxKind.Debris, z.Pos, FoamCream, z.Radius * 1.6f, 0.45f, Sfx.Hit);
            z.T = 0f;  // gone at the next zone tick
        }

        /// Host, from WorldStops: foam stops every shot, both ways; enemy shots wear it. A boss's aimed attacks go through.
        bool FoamStops(Bullet b)
        {
            if (Defs.Unblockable(b.Kind))
                return false;
            foreach (var z in Zones.Values)
            {
                if (!FoamLive(z) || b.Pos.FartherThan(z.Pos, FoamR(z) + b.Radius * 0.5f))
                    continue;
                if (Defs.IsEnemyShot(b.Kind))
                    WearFoam(z, b.FromBoss ? FoamBossShotWear : FoamShotWear);
                return true;
            }
            return false;
        }

        /// Moves something of radius r out of every lump (two passes, for where lumps overlap). Every game: players' own moves.
        public V2 PushOutOfFoam(V2 pos, float r)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool moved = false;
                foreach (var z in Zones.Values)
                {
                    if (!FoamLive(z))
                        continue;
                    float reach = FoamR(z) + r, d = pos.DistanceTo(z.Pos);
                    if (d >= reach)
                        continue;
                    pos = z.Pos + (d > 0.5f ? (pos - z.Pos) / d : new V2(1f, 0f)) * reach;
                    moved = true;
                }
                if (!moved)
                    break;
            }
            return pos;
        }

        /// Host, every tick: enemies can't walk through foam; pressed against it they slow and chew it. Bosses plough through.
        void KeepOffFoam(float dt)
        {
            bool up = FoamUp();
            foreach (var e in Enemies.Values)
            {
                if (e.FoamT > 0f)
                    e.FoamT -= dt;
                if (!up || !Targetable(e))
                    continue;
                foreach (var z in Zones.Values)
                {
                    if (!FoamLive(z))
                        continue;
                    float reach = FoamR(z) + e.Radius * 0.8f, d = e.Pos.DistanceTo(z.Pos);
                    if (d >= reach + 1f)
                        continue;
                    if (e.Def.IsBoss)
                    {
                        WearFoam(z, FoamBossChew * dt);
                        continue;
                    }
                    if (d < reach)
                        e.Pos = z.Pos + (d > 0.5f ? (e.Pos - z.Pos) / d : V2.FromAngle(Rand(0f, MathX.Tau))) * reach;
                    e.FoamT = 0.25f;
                    WearFoam(z, FoamChew * dt);
                }
            }
        }

        // --- Network: the player's Hazard Foam fields (SyncPlayer) ---

        void SyncHazardFoam(NetSync s, Player p)
        {
            s.Float(ref p.FoamT);
            s.Float(ref p.FoamTank);
        }
    }
}
