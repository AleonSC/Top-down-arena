using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Tank Soldier's weapon-action (Alt) fires, one per gun. Your game sends Msg.WeaponDash (DashDouble, DashMine, DashGas) with your
    /// aim; the host checks the weapon, the stock or cooldown, and fires.
    ///   Scattergun, Double Barrel: ScatShellMax shells (Player.ScatShells) that load over time, one every ScatRegen s (Player.ScatCharge is
    ///     the percent toward the next). Each Alt press fires one: a wide wall of ScatDumpPellets pellets at ScatDumpDamage x, and the recoil
    ///     kicks you back ScatKick px (your own game moves you, like a dodge, with no invulnerability): a movement ability if you want one.
    ///   Flak Cannon, Proximity Shell: Alt lobs a shell to your cursor (as far as the cannon reaches) that lands, arms after MineArm and lies
    ///     there for MineLife (ZoneKind.FlakMine, MineCap a player, the oldest goes). An enemy stepping within MineTrigger sets it off: a flak
    ///     burst (FlakAirburst) of MineDamage in MineBurst, shredding enemy bullets too. Cooldown MineCd.
    ///   Riot Repeater, Tear Gas: Alt lobs a canister to your cursor; after TearGasFlight it bursts into a cloud (ZoneKind.TearGas, TearGasRadius,
    ///     TearGasLife). Enemies in it are suppressed to the cap at once (Weapons.cs SuppressMax, half on bosses) and slowed TearGasSlow. Cooldown TearGasCd.
    /// Zone.Aim carries the direction the shell or canister came in from, for drawing its flight (Game/TankAltLook.cs).
    public partial class Game
    {
        public const int ScatShellMax = 8, ScatDumpPellets = 9;
        public const float ScatDumpDamage = 1.5f, ScatDumpCone = 0.85f, ScatKick = 75f, ScatKickTime = 0.16f, ScatDumpCd = 0.3f, ScatRegen = 1.5f;
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
            if (!pressed)
                return false;
            byte kind;
            switch (me.Weapon.Id)
            {
                case WeaponId.Scattergun:
                    if (scatLocalCd > 0f || me.ScatShells == 0)
                        return false;
                    scatLocalCd = ScatDumpCd;
                    kind = DashDouble;
                    // The recoil: kicked straight back from your aim, farther for each shell (like a dodge, no invulnerability).
                    V2 back = -V2.FromAngle((aimAt - me.Pos).Length > 1f ? (aimAt - me.Pos).Angle : me.Aim);
                    float far = ScatKick;
                    V2 to = ClampToArena(me.Pos + back * far, me.Def.BodyR);
                    DashDir = back;
                    dashSpeed = far / ScatKickTime;
                    DashT = to.DistanceTo(me.Pos) / dashSpeed;
                    break;
                case WeaponId.FlakCannon:
                    if (mineLocalCd > 0f)
                        return false;
                    mineLocalCd = MineCd;
                    kind = DashMine;
                    break;
                case WeaponId.RiotRepeater:
                    if (gasLocalCd > 0f)
                        return false;
                    gasLocalCd = TearGasCd;
                    kind = DashGas;
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

        /// Host: Double Barrel. Every loaded shell goes at once.
        void HostDoubleBarrel(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Scattergun || p.StunT > 0f || p.ScatShells == 0 || p.ScatCd > 0f || State == MatchState.GameOver)
                return;
            var w = p.Weapon;
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            V2 muzzle = p.Pos + V2.FromAngle(aim) * p.Def.BodyR;
            int n = ScatDumpPellets;
            float cone = ScatDumpCone;
            for (int i = 0; i < n; i++)
            {
                float a = aim + (i / (n - 1f) - 0.5f) * cone + Rand(-0.03f, 0.03f);
                var b = SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(a) * w.Speed * Rand(0.9f, 1.1f), 0f, w.Life, w.Damage * ScatDumpDamage * p.Stats.DmgMult, p.Id);
                b.PointBlank = true;  // still double up close
            }
            p.ScatShells--;
            p.ScatCd = ScatDumpCd;
            BroadcastFx(FxKind.DoubleBarrel, muzzle, muzzle + V2.FromAngle(aim) * 100f, Defs.BulletColor(Defs.TankShot), p.Id * 10 + 1, 0.45f, Sfx.TankDouble);
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
            p.MineCd = MineCd;
            BroadcastFx(FxKind.Ring, p.Pos + V2.FromAngle(m.Aim) * p.Def.BodyR, Defs.BulletColor(Defs.FlakShell), 14f, 0.2f, Sfx.TankMineLaunch);
        }

        /// Host: Tear Gas. The canister flies for TearGasFlight, then the cloud is up.
        void HostTearGas(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.RiotRepeater || p.StunT > 0f || p.GasCd > 0f || State == MatchState.GameOver)
                return;
            var w = p.Weapon;
            V2 at = TankAltLanding(p, aimAt, Math.Max(380f, w.Speed * w.Life * p.Stats.ShotSpeedMult));
            var z = AddZone(ZoneKind.TearGas, p, at, TearGasRadius * AreaMult(p), TearGasFlight + TearGasLife);
            z.Aim = (at - p.Pos).Angle;
            p.GasCd = TearGasCd;
            BroadcastFx(FxKind.Ring, p.Pos + V2.FromAngle(z.Aim) * p.Def.BodyR, TearGasColor, 14f, 0.2f, Sfx.TankGasLaunch);
        }

        public static readonly Rgba TearGasColor = new Rgba(0.92f, 0.9f, 0.7f);

        /// Is a mine or gas zone still in the air (its flight not done)?
        public static bool TankAltInFlight(Zone z) => z.Duration - z.T < (z.Kind == ZoneKind.FlakMine ? MineArm : TearGasFlight);

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
                }
                else if (p.ScatShells >= ScatShellMax)
                    p.ScatCharge = 0;
                else
                {
                    float c = p.ScatCharge + dt / ScatRegen * 100f;  // percent toward the next shell
                    if (c >= 100f)
                    {
                        c -= 100f;
                        p.ScatShells++;
                        BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.TankShot), 24f, 0.25f, Sfx.TankShellLoad);
                    }
                    p.ScatCharge = (byte)Math.Min(99f, c);
                }
            }
            List<Zone> burst = null;
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.FlakMine && z.Kind != ZoneKind.TearGas || TankAltInFlight(z))
                    continue;
                if (!z.Landed)
                {
                    z.Landed = true;  // down: the mine clunks and arms, the canister clatters and starts to hiss (TankAltSoundBank.cs)
                    BroadcastFx(FxKind.Spark, z.Pos, z.Def.Color, 6f, 0.1f, z.Kind == ZoneKind.FlakMine ? Sfx.TankMineArm : Sfx.TankGasHiss);
                }
                if (z.Kind == ZoneKind.FlakMine)
                {
                    foreach (var e in Enemies.Values)
                        if (Targetable(e) && e.Pos.DistanceTo(z.Pos) < MineTrigger + e.Radius)
                        {
                            (burst = burst ?? new List<Zone>()).Add(z);
                            break;
                        }
                    continue;
                }
                foreach (var e in Enemies.Values)  // tear gas
                {
                    if (!Targetable(e) || e.Pos.DistanceTo(z.Pos) > z.Radius + e.Radius)
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
    }
}
