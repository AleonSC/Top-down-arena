using System;
using System.Collections.Generic;

namespace Arena
{
    /// Player weapons: firing volleys, the Revolver and Mender's Bow, and how far each weapon reaches.
    public partial class Game
    {
        /// Everything that speeds up firing: upgrades, Overdrive, Bullet Storm, Counterstance.
        float FireRate(Player p) => p.Stats.FireRateMult * (p.OverdriveT > 0f ? 2f : 1f) * (Storming(p) ? 3f : 1f) * (p.FuryT > 0f ? 1.4f : 1f)
            * (p.CounterT > 0f ? 1f + p.Stats.CounterFireRate : 1f) * BloodFireRate(p);

        /// spread: each shot lands somewhere within this many radians of your aim (fanning the hammer).
        void FirePlayer(Player p, float spread = 0f)
        {
            float dmg = p.Stats.DmgMult;
            // Each extra projectile (Multishot, Bullet Storm) upgrades the weapon in its own way.
            int extra = ExtraShots(p);
            FireFanned(p, p.Aim + (spread > 0f ? Rand(-spread, spread) : 0f), extra);
            if (p.Weapon.Id == WeaponId.Pistol)
            {
                p.AmmoUsed++;  // fanned copies come free with the round
                p.ReloadIdle = 0f;
            }
            if (p.Weapon.Id == WeaponId.RiotRepeater && extra > 0)
            {
                // A burst of 1 + extra rounds, then a longer pause.
                p.EchoLeft = extra;
                p.EchoGap = p.EchoT = 0.06f;
                p.FireCd *= 1f + 0.6f * extra;
            }
            firingWeapon = false;
        }

        /// Riot Repeater: each round that hits slows how fast the enemy shoots by SuppressPerHit, up to SuppressMax (half
        /// that on bosses); it fades by SuppressFade a second. Shown from SuppressShown.
        public const float SuppressPerHit = 0.08f, SuppressMax = 0.5f, SuppressFade = 0.25f, SuppressShown = 0.15f;

        void SuppressHit(Bullet b, Enemy e)
        {
            if (b.Suppressive)
                e.Suppress = Math.Min(e.Def.IsBoss ? SuppressMax * 0.5f : SuppressMax, e.Suppress + SuppressPerHit);
        }

        /// Twin Rifle: the streams cross at your cursor's distance (from TwinCrossMin to their full range); a hit within
        /// TwinCrossZone of the crossing is crossfire, for TwinCrossMult.
        public const float TwinBarrelGap = 28f, TwinCrossMin = 120f, TwinCrossZone = 45f, TwinCrossMult = 2f;
        public float TwinCrossDistance(Player p) => Math.Max(TwinCrossMin, Math.Min(p.Weapon.Speed * p.Weapon.Life * p.Stats.ShotSpeedMult, p.AimAt.DistanceTo(p.Pos)));

        float CrossfireMult(Bullet b, Enemy e) => b.Crossfire && e.Pos.DistanceTo(b.Cross) <= TwinCrossZone + e.Radius ? TwinCrossMult : 1f;

        /// Burst SMG: holding fire spins it up to full over SmgSpinTime (the spray tightening from SmgSpray to SmgSprayTight
        /// either side, damage rising to +SmgSpinDamage); letting go spins it down over SmgSpinDown.
        public const float SmgSpinTime = 2f, SmgSpinDown = 0.6f, SmgSpray = 0.16f, SmgSprayTight = 0.04f, SmgSpinDamage = 0.6f;

        void TickSpinUp(Player p, float dt)
        {
            bool spinning = p.Weapon.Id == WeaponId.BurstSmg && p.Firing && !p.ParryActive && !p.Downed;
            p.SpinUp = spinning ? Math.Min(1f, p.SpinUp + dt / SmgSpinTime) : Math.Max(0f, p.SpinUp - dt / SmgSpinDown);
        }

        /// Scattergun: pellets that hit within ScatterClose of you (to the enemy's edge) do double and shove it back.
        public const float ScatterClose = 120f, ScatterPointBlank = 2f, ScatterShove = 16f;

        /// Flak Cannon: the shell bursts at your cursor (never nearer than FlakMin), hurting enemies and destroying
        /// enemy bullets within FlakBurst (red aimed shots get through).
        public const float FlakMin = 60f, FlakBurst = 80f;

        float PointBlankMult(Bullet b, Enemy e)
        {
            Player p;
            if (!b.PointBlank || !Players.TryGetValue(b.Owner, out p) || p.Pos.DistanceTo(e.Pos) - e.Radius > ScatterClose)
                return 1f;
            if (!Heavy(e))
            {
                V2 away = (e.Pos - p.Pos).Normalized();
                e.Pos = ClampToArena(e.Pos + (away.IsZero ? b.Vel.Normalized() : away) * ScatterShove, e.Radius);
            }
            return ScatterPointBlank;
        }

        /// A Flak Cannon shell reached the end of its flight: the airburst.
        void FlakAirburst(Bullet b)
        {
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(b.Pos) < b.Splash + e.Radius && !BlocksShot(e, b.Pos))
                {
                    HurtEnemy(e, b.Dmg, b.Owner);
                    OnBulletHit(b, e);
                }
            var shot = new List<int>();
            foreach (var o in Bullets.Values)
                if (Defs.IsEnemyShot(o.Kind) && !Defs.Unblockable(o.Kind) && o.Pos.DistanceTo(b.Pos) < b.Splash + o.Radius)
                    shot.Add(o.Id);
            foreach (int id in shot)
            {
                AddFx(FxKind.Spark, Bullets[id].Pos, Defs.BulletColor(Bullets[id].Kind), 8f, 0.25f);
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }
            float offset = Rand(0f, MathX.Tau);
            for (int i = 0; i < b.Frags; i++)  // extra projectiles: bomblets scattering round the burst, popping where they land
            {
                V2 d = V2.FromAngle(offset + MathX.Tau * i / b.Frags);
                var nb = SpawnBullet(b.FragKind, b.Pos + d * 6f, d * b.FragSpeed, 0f, b.FragLife, b.FragDmg, b.Owner);
                nb.Splash = b.FragSplash;
                nb.Bomblet = true;
                nb.Burst = 1;
            }
            BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.FlakShell), b.Splash, 0.3f, Sfx.Explode);
            BroadcastFx(FxKind.Ring, b.Pos, Rgba.White, b.Splash * 1.15f, 0.2f, Sfx.None);
        }

        /// Barrage: the volley, plus a copy per stack fanned out to alternating sides (8 degrees apart).
        void FireFanned(Player p, float aim, int extra)
        {
            if (p.Weapon.Melee)
            {
                // One swing; each Barrage stack throws a sword wave instead of another swing.
                FireVolley(p, aim, extra);
                firingWeapon = true;
                FireKnightWaves(p, aim);
                firingWeapon = false;
                return;
            }
            if (p.Weapon.Id == WeaponId.LeechBeam || p.Weapon.Id == WeaponId.ChainLight || p.Weapon.Id == WeaponId.ArcLightning)
            {
                FireVolley(p, aim, extra);  // Barrage adds a tendril, cones side by side or parallel strips instead (BloodMage.cs, Minions.cs)
                return;
            }
            if (p.Weapon.Id == WeaponId.Implosion)
            {
                FireVolley(p, aim, extra + p.Stats.Fan);  // no direction to fan in: each copy is another implosion
                return;
            }
            for (int v = 0; v <= p.Stats.Fan; v++)
                FireVolley(p, aim + (v == 0 ? 0f : (v % 2 == 1 ? 1f : -1f) * 0.14f * ((v + 1) / 2)), extra);
        }

        /// Extra projectiles from upgrades, plus two during Bullet Storm.
        public int ExtraShots(Player p) => p.Stats.ExtraShots + (Storming(p) ? (Mastered(p, AbilityId.BulletStorm) ? 4 : 2) : 0)
            + (p.OverdriveT > 0f && Mastered(p, AbilityId.Overdrive) ? 1 : 0) + (p.SurgeT > 0f && Mastered(p, AbilityId.ArcaneSurge) ? 1 : 0)
            + (p.FuryT > 0f && Mastered(p, AbilityId.BattleFury) ? 1 : 0);

        // --- Revolver (the Healer's Pistol) -------------------------------------------

        public const int RevolverCylinder = 6;
        public const float RevolverReload = 1.2f;
        const float RevolverTopUpDelay = 0.8f;  // stop shooting this long and it starts sliding rounds back in...
        const float RevolverTopUpEach = 0.25f;  // ...one at a time (shooting stops it; it never locks you out)

        /// Six rounds, plus two per extra projectile.
        public int RevolverCapacity(Player p) => RevolverCylinder + 2 * ExtraShots(p);

        /// The last rounds in the cylinder are heavy (double damage): one, plus two per extra projectile.
        public int RevolverHeavy(Player p) => 1 + 2 * ExtraShots(p);

        // --- Mender's Bow: hold to draw, release to fire ----------------------------------

        public const float BowMinDraw = 0.15f;   // a quick tap still looses a weak arrow (drawn this far)
        public const float BowFullDraw = 0.9f;   // a full draw (fire-rate upgrades shorten it)
        public const float BowPerfectWindow = 0.25f;  // let go this soon after full draw for a perfect shot
        const float BowPerfectBonus = 1.25f;
        public const float BowRecover = 0.25f;   // after a shot, the next draw starts this much later
        public const float BowDrawSlow = 0.85f;  // move speed while drawing

        public float BowFull(Player p) => BowFullDraw / FireRate(p);

        /// Weapons you hold to charge and let go to fire: the Mender's Bow, the Warhammer, the Golem Staff's boulder, Arc Lightning and the Imp Caller.
        public static bool ChargeWeapon(WeaponId w) => w == WeaponId.MendersBow || w == WeaponId.Warhammer || w == WeaponId.GolemStaff || w == WeaponId.ArcLightning || w == WeaponId.ImpCaller;

        /// 0 for a tap, 1 at full draw.
        public float BowCharge(Player p, float draw) => MathX.Clamp01((draw - BowMinDraw) / Math.Max(0.01f, BowFull(p) - BowMinDraw));

        public bool BowPerfect(Player p, float draw) => draw >= BowFull(p) && draw <= BowFull(p) + BowPerfectWindow;

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// The host fires a bow shot when the shooter's release arrives (with how long they drew).
        void TickBow(Player p)
        {
            if (p.BowRelease <= 0f || State == MatchState.GameOver)
                return;
            p.BowShotDraw = p.BowRelease;
            p.BowRelease = 0f;
            FirePlayer(p);
        }

        // Local draw state for this machine's player (the shooter measures the draw).
        float bowDraw = -1f, bowCd;

        /// Your own draw: starts when you hold fire (once the last shot has recovered); letting go fires.
        /// Returns the draw time on the frame you let go, else 0.
        float LocalBow(Player me, bool firing, float dt)
        {
            bowCd -= dt;
            float release = 0f;
            if (!ChargeWeapon(me.Weapon.Id) || me.Downed || me.ParryActive || me.StunShots > 0)
                bowDraw = -1f;  // the shield (or going down) cancels a draw
            else if (firing)
            {
                if (bowDraw < 0f && bowCd <= 0f)
                    bowDraw = 0f;
                if (bowDraw >= 0f)
                    bowDraw += dt;
            }
            else if (bowDraw >= 0f)
            {
                release = Math.Min(10f, Math.Max(BowMinDraw, bowDraw));
                bowDraw = -1f;
                bowCd = BowRecover / FireRate(me);
            }
            me.DrawT = Math.Max(0f, bowDraw);
            return release;
        }

        const float FanDelay = 0.2f;      // hold the trigger this long and you start fanning the hammer
        const float FanInterval = 0.07f;  // fanned rounds come this fast...
        const float FanSpread = 0.087f;   // ...each within 5 degrees either side of your aim (a 10 degree arc)

        /// Tap to fire single aimed rounds (Weapon.FireInterval apart); hold to fan the hammer and empty
        /// the cylinder in a quick, imprecise burst. Keep holding and it fans again once it's reloaded.
        void RevolverTrigger(Player p, float dt)
        {
            bool pressed = p.Firing && !p.WasFiring;
            p.WasFiring = p.Firing;
            if (!p.Firing)
            {
                p.HeldT = 0f;
                p.Fanning = false;
            }
            else
                p.HeldT += dt;
            p.TapQueued |= pressed;
            if (p.ParryActive || p.ReloadT > 0f || State == MatchState.GameOver)
                return;
            if (p.Firing && p.HeldT >= FanDelay && !p.Fanning)
            {
                p.Fanning = true;
                p.FireCd = Math.Min(p.FireCd, 0f);  // the hammer goes right away, whatever the tap timer says
            }
            if (p.Fanning)
            {
                if (p.FireCd <= 0f)
                {
                    p.FireCd = FanInterval / FireRate(p);
                    p.TapQueued = false;
                    FirePlayer(p, FanSpread);
                }
            }
            else if (p.TapQueued && p.FireCd <= 0f)
            {
                p.FireCd = p.Weapon.FireInterval / FireRate(p);
                p.TapQueued = false;
                FirePlayer(p);
            }
        }

        void TickRevolver(Player p, float dt)
        {
            if (p.ReloadT > 0f)
            {
                p.ReloadT -= dt;
                if (p.ReloadT <= 0f)
                {
                    p.ReloadT = 0f;
                    p.AmmoUsed = 0;
                }
                return;
            }
            int cap = RevolverCapacity(p);
            if (p.AmmoUsed >= cap)
            {
                // Empty: a full reload, which you have to wait out.
                p.ReloadT = RevolverReload / p.Stats.FireRateMult;
                p.ReloadIdle = 0f;
                Sounds.Add(Sfx.Pick);
                return;
            }
            if (p.Firing || p.AmmoUsed == 0)
            {
                p.ReloadIdle = 0f;
                return;
            }
            // Not shooting: top the cylinder up a round at a time.
            p.ReloadIdle += dt;
            if (p.ReloadIdle >= RevolverTopUpDelay + RevolverTopUpEach / p.Stats.FireRateMult)
            {
                p.AmmoUsed--;
                p.ReloadIdle = RevolverTopUpDelay;
            }
        }

        /// One shot of the player's weapon, upgraded by `extra` extra projectiles.
        void FireVolley(Player p, float aim, int extra)
        {
            firingWeapon = true;
            float dmg = p.Stats.DmgMult;
            V2 dir = V2.FromAngle(aim);
            V2 muzzle = p.Pos + dir * p.Def.BodyR;
            var w = p.Weapon;  // its Speed, Life and Damage (Defs)
            switch (w.Id)
            {
                case WeaponId.Scattergun:
                {
                    // +2 pellets and a 15% tighter cone per extra projectile.
                    int n = 5 + 2 * extra;
                    float cone = 0.52f * (float)Math.Pow(0.85, extra);
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + (i / (n - 1f) - 0.5f) * cone) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).PointBlank = true;
                    break;
                }
                case WeaponId.FlakCannon:
                {
                    // An airburst: the shell flies over enemies to your cursor (as far as its range) and bursts there,
                    // shredding enemies and enemy bullets in the burst.
                    // Velocity Rounds speed the shell up (SpawnBullet) and reach farther, but it still bursts at the cursor.
                    float fast = w.Speed * p.Stats.ShotSpeedMult;
                    float far = Math.Min(fast * w.Life, Math.Max(FlakMin, p.AimAt.DistanceTo(p.Pos) - p.Def.BodyR));
                    var b = SpawnBullet(Defs.FlakShell, muzzle, dir * w.Speed, 0f, far / fast, w.Damage * dmg, p.Id);
                    b.Splash = FlakBurst * AreaMult(p);
                    b.Burst = 1;  // marks it as bursting where its flight ends (MoveBullets)
                    if (extra > 0)  // bomblets scatter round the burst and pop where they land
                        SetFrags(b, 2 * extra, Defs.GrenadeShot, 10f * dmg, 260f, 0.3f, 45f * AreaMult(p), false);
                    break;
                }
                case WeaponId.RiotRepeater:
                    SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + Rand(-0.06f, 0.06f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Suppressive = true;
                    break;
                case WeaponId.BearTotem:
                    FireBearClaws(p, aim, dmg, extra);  // a summoner's melee weapon (Minions.cs)
                    break;
                case WeaponId.LightBolt:
                    FireRepulsion(p, aim, dmg, extra);  // the Repulsion Cannon (Repulsion.cs)
                    break;
                case WeaponId.Pistol:
                {
                    // Revolver: a perfectly accurate round; the last ones in the cylinder are heavy.
                    int left = RevolverCapacity(p) - p.AmmoUsed;  // FirePlayer uses up the round
                    bool heavy = left <= RevolverHeavy(p);
                    SpawnBullet(Defs.HealerShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * (heavy ? 2f : 1f) * dmg, p.Id).Radius = heavy ? 6f : 4.5f;
                    break;
                }
                case WeaponId.MendersBow:
                {
                    // Draw and release: the longer the draw, the harder, faster and farther the arrow.
                    float c = BowCharge(p, p.BowShotDraw);
                    bool perfect = BowPerfect(p, p.BowShotDraw);
                    float bonus = perfect ? BowPerfectBonus : 1f;
                    float baseDmg = Lerp(8f, 40f, c) * bonus, speed = Lerp(450f, 950f, c), reach = Lerp(500f, 900f, c);
                    float heal = Lerp(5f, 20f, c) * bonus;
                    // A full draw also looses an extra arrow (half damage) per extra projectile, in a tight fan.
                    int arrows = c >= 1f ? 1 + extra : 1;
                    for (int k = 0; k < arrows; k++)
                    {
                        float a = aim + (k == 0 ? 0f : (k % 2 == 1 ? 1f : -1f) * 0.07f * ((k + 1) / 2));
                        float d = baseDmg * (k == 0 ? 1f : 0.5f);
                        var b = SpawnBullet(Defs.ArrowShot, muzzle, V2.FromAngle(a) * speed, 0f, reach / speed, d * dmg, p.Id);
                        b.HealAlly = heal * (k == 0 ? 1f : 0.5f) * 9f / d;  // HealAllyHit scales it by Dmg / 9 (so damage upgrades heal more too)
                        b.Pierce += c >= 1f ? 2 : c >= 0.5f ? 1 : 0;
                        b.Bolts = c >= 0.5f;  // a drawn arrow pierces: Ricochet flings bounce bolts off it
                        if (perfect)
                            b.Radius = 6f;
                    }
                    if (perfect)
                        BroadcastFx(FxKind.Spark, muzzle, new Rgba(1f, 0.85f, 0.35f), 26f, 0.3f, Sfx.Pick);
                    break;
                }
                case WeaponId.TwinRifle:
                {
                    // Barrels spread apart (two, plus one per extra projectile), angled in so every stream crosses at
                    // your cursor's distance; hits near the crossing are crossfire, for double. Each volley strays up to 2°.
                    int n = 2 + extra;
                    float a = aim + Rand(-TwinRifleSpread, TwinRifleSpread);
                    V2 v = V2.FromAngle(a), side = v.Orthogonal();
                    V2 cross = p.Pos + v * TwinCrossDistance(p);
                    for (int i = 0; i < n; i++)
                    {
                        V2 from = muzzle + side * ((i - (n - 1) / 2f) * TwinBarrelGap);
                        var b = SpawnBullet(Defs.DpsShot, from, (cross - from).Normalized() * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.Cross = cross;
                        b.Crossfire = true;
                    }
                    break;
                }
                case WeaponId.Railgun:
                {
                    var b = SpawnBullet(Defs.RailShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    b.Pierce += 3;
                    b.Bolts = true;  // it pierces: Ricochet flings bounce bolts off it
                    IonTrail(p, muzzle, dir, b.Vel.Length, w.Life, dmg);  // its path stays charged (Minions.cs)
                    if (extra > 0)
                        SetFrags(b, 2 * extra, Defs.DpsShot, 11f * dmg, 600f, 0.3f, 0f, true);
                    break;
                }
                case WeaponId.BurstSmg:
                {
                    // Spun up (SpinUp), the spray tightens and every bullet hits harder. +1 bullet per shot per extra
                    // projectile (at half damage), sprayed a little wider.
                    float spray = (SmgSpray - (SmgSpray - SmgSprayTight) * p.SpinUp) * (1f + 0.35f * extra);
                    float spun = 1f + SmgSpinDamage * p.SpinUp;
                    for (int i = 0; i <= extra; i++)
                        SpawnBullet(Defs.DpsShot, muzzle, V2.FromAngle(aim + Rand(-spray, spray)) * w.Speed, 0f, w.Life, (i == 0 ? w.Damage : w.Damage * 0.5f) * spun * dmg, p.Id);
                    break;
                }
                default:
                    if (w.Id >= WeaponId.BloodLash)
                        FireBloodWeapon(p, aim, dir, muzzle, dmg, extra);  // BloodMage.cs
                    else if (w.Id >= WeaponId.Kusarigama)
                        FireNinjaWeapon(p, aim, dir, muzzle, dmg, extra);  // Ninja.cs
                    else if (w.Melee)
                        FireKnightWeapon(p, aim, dmg, extra);  // Knight.cs
                    else
                        FireKitWeapon(p, aim, dir, muzzle, dmg, extra);  // mage and summoner weapons (Minions.cs)
                    break;
            }
            firingWeapon = false;
        }

        /// How far the player's weapon reaches from their centre right now: a shot's speed times its lifetime (with
        /// Velocity Rounds) plus the muzzle offset, or a swing's reach, from the weapon's numbers in Defs (the firing
        /// code reads the same ones; SimTest checks it against real shots). The aim line uses it.
        public float WeaponRange(Player p)
        {
            if (p.StunShots > 0)
                return StunGunRange(p);  // Stun.cs
            int x = ExtraShots(p);
            var w = p.Weapon;
            switch (w.Id)
            {
                case WeaponId.MendersBow:  // as far as the current draw sends it
                    return p.Def.BodyR + Lerp(500f, 900f, p.Id == MyId && bowDraw >= 0f ? BowCharge(p, bowDraw) : 0f) * p.Stats.ShotSpeedMult;
                case WeaponId.GolemStaff:  // where the boulder lands with the current charge (Minions.cs)
                    return BoulderDistance(p.Id == MyId && bowDraw >= 0f ? BowCharge(p, bowDraw) : 0f) * p.Stats.ShotSpeedMult;
                case WeaponId.Ofuda:  // where a charm lands as a ward, at most (Ninja.cs)
                    return p.Def.BodyR + WardRange * p.Stats.ShotSpeedMult * PierceReach(p);
                case WeaponId.WispLantern:  // how far a wisp is sent (Minions.cs)
                    return WispReach * PierceReach(p);
                case WeaponId.ImpCaller:  // how far imps are sent (Minions.cs)
                    return ImpReach * PierceReach(p);
                case WeaponId.ArcLightning:  // the strip's length with the current charge (Minions.cs)
                    return ArcLength(p, p.Id == MyId && bowDraw >= 0f ? BowCharge(p, bowDraw) : 0f);
                case WeaponId.ChainLight:  // the cone's reach (Minions.cs)
                    return LightConeLength(p, x);
                case WeaponId.FumaShuriken:  // the far end of its widest loop (Ninja.cs)
                    return FumaReach * FumaSize(x) * p.Stats.ShotSpeedMult * PierceReach(p);
                case WeaponId.Implosion:  // how far away you can click (BloodMage.cs)
                    return p.Def.BodyR + w.Reach * p.Stats.ShotSpeedMult;
                case WeaponId.Flamethrower:  // the lead flame's reach (Minions.cs)
                case WeaponId.SpiritFan:     // the gust's reach (Ninja.cs)
                    return p.Def.BodyR + w.Speed * w.Life * (1f + w.Grow * x) * p.Stats.ShotSpeedMult * PierceReach(p);
                case WeaponId.LightBolt:  // the Repulsion Cannon's cone
                    return p.Def.BodyR + w.Reach * AreaMult(p) * PierceReach(p);
                case WeaponId.LeechBeam:  // Leech Tendrils reach from your edge (BloodMage.cs)
                    return p.Def.BodyR + GraspReach(p);
                case WeaponId.RadiantOrb:  // as far out as the orb can go (RadiantOrb.cs)
                    return RadiantReach(p);
                case WeaponId.VeinTether:  // a beam from your edge (BloodMage.cs)
                    return p.Def.BodyR + w.Reach * (1f + w.Grow * x) * p.Stats.ShotSpeedMult;
                case WeaponId.Lightbringer:  // its sword waves reach past the swing
                    return Math.Max(MeleeReach(p, w.Reach), p.Def.BodyR + 600f * 0.45f * p.Stats.ShotSpeedMult);
                case WeaponId.FrostLance:  // the icicle, out from you
                    return MeleeReach(p, w.Reach + w.Grow * x);
                case WeaponId.Katana:  // sheathed, the quick-draw cut reaches much further (Ninja.cs)
                    return MeleeReach(p, p.SheathT >= KatanaSheath ? KatanaDrawReach : w.Reach);
                case WeaponId.Greatsword:  // extra projectiles add sword beams
                    return x > 0 ? p.Def.BodyR + 700f * 0.6f * p.Stats.ShotSpeedMult : MeleeReach(p, w.Reach);
            }
            if (w.Melee)
                return MeleeReach(p, w.Reach + w.Grow * x);
            return p.Def.BodyR + w.Speed * w.Life * (1f + w.Grow * x) * p.Stats.ShotSpeedMult;
        }

        /// On hit, the bullet throws `n` fragments: out to the sides, or all around.
        static void SetFrags(Bullet b, int n, byte kind, float dmg, float speed, float life, float splash, bool sideways)
        {
            b.Frags = n;
            b.FragKind = kind;
            b.FragDmg = dmg;
            b.FragSpeed = speed;
            b.FragLife = life;
            b.FragSplash = splash;
            b.FragSideways = sideways;
        }

        const float TwinRifleSpread = 2f * MathX.Pi / 180f;

        bool firingWeapon;  // set while a player's weapon fires, to tag gun shots for accuracy
    }
}
