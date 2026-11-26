using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Tank Soldier's weapon-action (Alt) fires, one per gun. Your game sends Msg.WeaponDash (WeaponAlt.Double, WeaponAlt.Mine, WeaponAlt.Gas) with your
    /// aim; the host checks the weapon, the stock or cooldown, and fires.
    ///   Scattergun, Shock Grenade: ScatShellMax grenades (Player.ScatShells) that load over time, one every ScatRegen s (Player.ScatCharge is
    ///     the percent toward the next). Each Alt press lobs one to your cursor (ZoneKind.ShockGrenade, up to ScatLobRange px out); after
    ///     ScatFlight it goes off: every enemy within ScatShockRadius takes ScatShockDamage and is stunned ScatStun s (ScatBossStun on
    ///     bosses), with bolts jumping to each. The zone lingers ScatShockLinger s for the flash.
    ///   Flak Cannon, Proximity Shell: Alt lobs a shell to your cursor (as far as the cannon reaches) that lands, arms after MineArm and lies
    ///     there for MineLife (ZoneKind.FlakMine, MineCap a player, the oldest goes). An enemy stepping within MineTrigger sets it off: a flak
    ///     burst (FlakAirburst) of MineDamage in MineBurst, shredding enemy bullets too. Cooldown MineCd.
    ///   Riot Repeater, Tear Gas: Alt lobs a canister to your cursor; after TearGasFlight it bursts into a cloud (ZoneKind.TearGas, TearGasRadius,
    ///     TearGasLife). Enemies in it are suppressed to the cap at once (Weapons.cs SuppressMax, half on bosses) and slowed TearGasSlow. Cooldown TearGasCd.
    /// Zone.Aim carries the direction the shell or canister came in from, for drawing its flight (Game/TankAltLook.cs).
    public partial class Game
    {
        public const int ScatShellMax = 3;
        public const float ScatDumpCd = 0.4f, ScatRegen = 5f, ScatFlight = 0.5f, ScatShockLinger = 0.45f, ScatLobRange = 380f;
        public const float ScatShockRadius = 110f, ScatShockDamage = 30f, ScatStun = 1.8f, ScatBossStun = 0.7f;
        public const int MineCap = 3;
        public const float MineCd = 5f, MineArm = 0.55f, MineLife = 15f, MineTrigger = 40f, MineBurst = 95f, MineDamage = 45f;
        public const float TearGasCd = 9f, TearGasFlight = 0.5f, TearGasLife = 5.5f, TearGasRadius = 100f, TearGasSlow = 0.3f;

        // Your own game: until each key fires again (the host checks its own; drawn as a ring under your feet).
        float scatLocalCd, mineLocalCd, gasLocalCd;
        public float MineLocalCd => mineLocalCd;
        public float GasLocalCd => gasLocalCd;

        /// Your own game: the Tank Soldier's Alt. True when it sent something (LocalWeaponDash stops there).
        bool LocalTankAlt(Player me, V2 aimAt, bool pressed, float dt)
        {
            scatLocalCd = Math.Max(0f, scatLocalCd - dt);
            mineLocalCd = Math.Max(0f, mineLocalCd - dt);
            gasLocalCd = Math.Max(0f, gasLocalCd - dt);
            boneWallLocalCd = Math.Max(0f, boneWallLocalCd - dt);
            if (!pressed)
                return false;
            byte kind;
            switch (me.Weapon.Id)
            {
                case WeaponId.Scattergun:
                    if (scatLocalCd > 0f || me.ScatShells == 0)
                        return false;
                    scatLocalCd = ScatDumpCd;
                    kind = WeaponAlt.Double;
                    break;
                case WeaponId.FlakCannon:
                    if (mineLocalCd > 0f)
                        return false;
                    mineLocalCd = MineCd - me.Stats.MineCdCut;  // (Wide Burst: sooner)
                    kind = WeaponAlt.Mine;
                    break;
                case WeaponId.RiotRepeater:
                    if (gasLocalCd > 0f)
                        return false;
                    gasLocalCd = TearGasCd;
                    kind = WeaponAlt.Gas;
                    break;
                case WeaponId.BoneGuard:  // (the Tank Summoner's Ossuary Wall: BoneGuard.cs)
                    if (boneWallLocalCd > 0f)
                        return false;
                    boneWallLocalCd = BoneWallCd;
                    kind = WeaponAlt.BoneWall;
                    break;
                default:
                    return false;
            }
            if (IsHost)
                HostWeaponDash(MyId, kind, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(kind);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Host: where a lobbed Alt lands: your cursor, no farther than the gun reaches, inside the arena.
        V2 TankAltLanding(Player p, V2 aimAt, float reach)
        {
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            return ClampToArena(p.Pos + V2.FromAngle(aim) * Math.Max(FlakMin, Math.Min(reach, toward.Length)), 12f);
        }

        /// Host: Shock Grenade. Lobbed to the cursor; it goes off ScatFlight later (TickTankAlts).
        void HostDoubleBarrel(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Scattergun || p.StunT > 0f || p.ScatShells == 0 || p.ScatCd > 0f || State == MatchState.GameOver)
                return;
            V2 at = TankAltLanding(p, aimAt, ScatLobRange);
            var z = AddZone(ZoneKind.ShockGrenade, p, at, ScatShockRadius * AreaMult(p), ScatFlight + ScatShockLinger);
            z.Aim = (at - p.Pos).Angle;
            p.ScatShells--;
            p.ScatCd = ScatDumpCd;
            BroadcastFx(FxKind.DoubleBarrel, p.Pos + V2.FromAngle(z.Aim) * p.Def.BodyR, p.Pos + V2.FromAngle(z.Aim) * (p.Def.BodyR + 100f), ShockColor, p.Id * 10 + 1, 0.45f, Sfx.TankDouble);
        }

        public static readonly Rgba ShockColor = new Rgba(0.45f, 0.85f, 1f);

        /// Host: a shock grenade lands: damage, stun and a bolt to every enemy in its reach.
        void ShockGrenadeBurst(Zone z)
        {
            Player owner;
            Players.TryGetValue(z.Owner, out owner);
            foreach (var e in FoesInCircle(z.Pos, z.Radius, by: z.Owner))
            {
                BroadcastFx(FxKind.Bolt, z.Pos, e.Pos, ShockColor, 0f, 0.25f, Sfx.None);
                HurtEnemy(e, ScatShockDamage * z.DmgMult, z.Owner);
                Charge(e, z.Owner);  // Charged.cs
                if (e.Hp > 0f)
                    Stun(e, e.Def.IsBoss ? ScatBossStun : ScatStun);
            }
            BroadcastFx(FxKind.Ring, z.Pos, ShockColor, z.Radius, 0.35f, Sfx.TankMineBurst);
        }

        /// Host: Proximity Shell. Lobbed to the cursor; your oldest goes if you have MineCap.
        void HostFlakMine(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.FlakCannon || p.StunT > 0f || p.MineCd > 0f || State == MatchState.GameOver)
                return;
            var w = p.Weapon;
            V2 at = TankAltLanding(p, aimAt, w.Speed * w.Life * p.Stats.ShotSpeedMult);
            Zone oldest = null;
            int mine = 0;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.FlakMine && z.Owner == p.Id)
                {
                    mine++;
                    if (oldest == null || z.T < oldest.T)
                        oldest = z;
                }
            if (mine >= MineCap && oldest != null)
                Zones.Remove(oldest.Id);
            var m = AddZone(ZoneKind.FlakMine, p, at, MineTrigger, MineLife);
            m.Aim = (at - p.Pos).Angle;
            m.Radius = MineBurst * AreaMult(p);  // drawn as the burst's reach; it goes off at MineTrigger
            p.MineCd = MineCd - p.Stats.MineCdCut;
            BroadcastFx(FxKind.Ring, p.Pos + V2.FromAngle(m.Aim) * p.Def.BodyR, Defs.BulletColor(Defs.FlakShell), 14f, 0.2f, Sfx.TankMineLaunch);
        }

        /// Host: Tear Gas. The canister flies for TearGasFlight, then the cloud is up.
        void HostTearGas(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.RiotRepeater || p.StunT > 0f || p.GasCd > 0f || State == MatchState.GameOver)
                return;
            var w = p.Weapon;
            V2 at = TankAltLanding(p, aimAt, Math.Max(380f, w.Speed * w.Life * p.Stats.ShotSpeedMult));
            var z = AddZone(ZoneKind.TearGas, p, at, TearGasRadius * AreaMult(p) * p.Stats.TearGasMult, TearGasFlight + TearGasLife);
            z.Aim = (at - p.Pos).Angle;
            p.GasCd = TearGasCd;
            BroadcastFx(FxKind.Ring, p.Pos + V2.FromAngle(z.Aim) * p.Def.BodyR, TearGasColor, 14f, 0.2f, Sfx.TankGasLaunch);
        }

        public static readonly Rgba TearGasColor = new Rgba(0.92f, 0.9f, 0.7f);

        /// Is a mine or gas zone still in the air (its flight not done)?
        public static bool TankAltInFlight(Zone z) => z.Duration - z.T < (z.Kind == ZoneKind.FlakMine ? MineArm : z.Kind == ZoneKind.ShockGrenade ? ScatFlight : TearGasFlight);

        /// Host, every tick: the cooldowns, the shells emptied when the Scattergun is put away, mines going off, gas suppressing.
        void TickTankAlts(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.ScatCd = Math.Max(0f, p.ScatCd - dt);
                p.MineCd = Math.Max(0f, p.MineCd - dt);
                p.GasCd = Math.Max(0f, p.GasCd - dt);
                if (p.Weapon.Id != WeaponId.Scattergun)
                {
                    p.ScatShells = ScatShellMax;  // ready again when you pick it back up
                    p.ScatCharge = 0;
                    p.ScatAcc = 0f;
                }
                else if (p.ScatShells >= ScatShellMax)
                {
                    p.ScatCharge = 0;
                    p.ScatAcc = 0f;
                }
                else
                {
                    p.ScatAcc += dt / ScatRegen * 100f;  // percent toward the next grenade
                    if (p.ScatAcc >= 100f)
                    {
                        p.ScatAcc -= 100f;
                        p.ScatShells++;
                        BroadcastFx(FxKind.Ring, p.Pos, ShockColor, 24f, 0.25f, Sfx.TankShellLoad);
                    }
                    p.ScatCharge = (byte)Math.Min(99f, p.ScatAcc);
                }
            }
            List<Zone> burst = null;
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.FlakMine && z.Kind != ZoneKind.TearGas && z.Kind != ZoneKind.ShockGrenade || TankAltInFlight(z))
                    continue;
                if (z.Kind == ZoneKind.ShockGrenade)
                {
                    if (!z.Landed)
                    {
                        z.Landed = true;
                        ShockGrenadeBurst(z);
                    }
                    continue;
                }
                if (!z.Landed)
                {
                    z.Landed = true;  // down: the mine clunks and arms, the canister clatters and starts to hiss (TankAltSoundBank.cs)
                    BroadcastFx(FxKind.Spark, z.Pos, z.Def.Color, 6f, 0.1f, z.Kind == ZoneKind.FlakMine ? Sfx.TankMineArm : Sfx.TankGasHiss);
                }
                if (z.Kind == ZoneKind.FlakMine)
                {
                    foreach (var e in Enemies.Values)
                        if (Targetable(e) && e.Pos.CloserThan(z.Pos, MineTrigger + e.Radius))
                        {
                            (burst = burst ?? new List<Zone>()).Add(z);
                            break;
                        }
                    continue;
                }
                foreach (var e in Enemies.Values)  // tear gas
                {
                    if (!Targetable(e) || e.Pos.FartherThan(z.Pos, z.Radius + e.Radius))
                        continue;
                    e.Suppress = Math.Max(e.Suppress, e.Def.IsBoss ? SuppressMax * 0.5f : SuppressMax);
                    Slow(e, 0.3f, 1f - TearGasSlow);
                }
            }
            if (burst != null)
                foreach (var z in burst)
                {
                    Zones.Remove(z.Id);
                    BroadcastFx(FxKind.Spark, z.Pos, z.Def.Color, 6f, 0.1f, Sfx.TankMineBurst);  // the crack and shrapnel, over the burst's boom
                    FlakAirburst(new Bullet { Pos = z.Pos, Owner = z.Owner, Kind = Defs.FlakShell, Dmg = MineDamage * z.DmgMult, Splash = z.Radius });
                }
        }

        // --- Network: the player's Scattergun fields (SyncPlayer) ---

        void SyncScattergun(NetSync s, Player p)
        {
            s.Byte(ref p.ScatShells);   // the Scattergun's Double Barrel shells
            s.Byte(ref p.ScatCharge);
        }
    }
}
