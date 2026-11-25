using System;
using System.Collections.Generic;

namespace Arena
{
    /// Player weapons: firing volleys, the Revolver and Mender's Bow, and how far each weapon reaches.
    public partial class Game
    {
        /// Everything that speeds up firing: upgrades, Overdrive (Adrenaline Shot doesn't stack with it: the bigger wins), Artillery Assault, Counterstance.
        float FireRate(Player p) => p.Stats.FireRateMult * MomentumMult(p) * Math.Max(p.OverdriveT > 0f ? 2f : 1f, p.AdrenalineT > 0f ? AdrenalineFire : 1f) * (Storming(p) ? 3f : 1f) * (p.OverchargeT > 0f ? OverchargeFire : 1f)  // Overcharge (StaticShock.cs)
            * (p.CounterT > 0f ? 1f + p.Stats.CounterFireRate : 1f) * (p.SurgeT > 0f ? 1f + p.Stats.SurgeRate : 1f)  // Arcane Surge
            * (OverclockOn(p) ? OverclockMult : 1f)  // an Overclock Chip (Gadgets.cs)
            * (Supplied(p) ? SupplyFire : 1f);  // a Supply Drop (SupplyDrop.cs)

        /// spread: each shot lands somewhere within this many radians of your aim.
        void FirePlayer(Player p, float spread = 0f)
        {
            // Each extra projectile (Multishot, Artillery Assault) upgrades the weapon in its own way.
            int extra = ExtraShots(p);
            FireFanned(p, p.Aim + (spread > 0f ? Rand(-spread, spread) : 0f), extra);
            if (p.Weapon.Id == WeaponId.TwinPistols)
                TwinPistolsFired(p);  // the other gun's turn, or Laser Sights' clock (TwinPistols.cs)
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
                e.Suppress = Math.Min(e.Def.IsBoss ? SuppressMax * 0.5f : SuppressMax, e.Suppress + SuppressPerHit * (Players.ContainsKey(b.Owner) ? Players[b.Owner].Stats.SuppressMult : 1f));  // (Crowd Control: faster)
        }

        /// Twin Rifle: the streams cross at your cursor's distance (from TwinCrossMin to their full range); a hit within
        /// TwinCrossZone of the crossing is crossfire, for TwinCrossMult.
        /// TwinBarrelGap is the total width of the barrels (however many); TwinCopySpan is the same for a Barrage copy.
        public const float TwinCopySpan = 20f;
        /// How far past your edge the Twin Rifle's muzzles are: the guns are held out wide (Spread arms,
        /// Docs/Mockups/twin-rifle-firing.html) and the rounds start at the drawn muzzles. Against a wall they start at your edge.
        public const float TwinMuzzleOut = 22f;
        public const float TwinBarrelGap = 60f, TwinCrossMin = 120f, TwinCrossZone = 45f, TwinCrossMult = 1.5f;
        public float TwinCrossDistance(Player p) => Math.Max(TwinCrossMin, Math.Min(p.Weapon.Speed * p.Weapon.Life * p.Stats.ShotSpeedMult, p.AimAt.DistanceTo(p.Pos)));

        float CrossfireMult(Bullet b, Enemy e)
        {
            Player p;
            if (!b.Crossfire)
                return 1f;
            Players.TryGetValue(b.Owner, out p);
            return e.Pos.WithinReach(b.Cross, CrossZone(p) + e.Radius) ? TwinCrossMult + (p != null ? p.Stats.CrossBonus : 0f) : 1f;  // (Kill Zone: wider, harder)
        }

        /// The Twin Rifle's crossfire zone for this player (Kill Zone widens it).
        public static float CrossZone(Player p) => TwinCrossZone * (p != null ? p.Stats.CrossZoneMult : 1f);

        /// Burst SMG: holding fire spins it up to full over SmgSpinTime (the spray tightening from SmgSpray to SmgSprayTight
        /// either side, damage rising to +SmgSpinDamage); letting go spins it down over SmgSpinDown.
        public const float SmgSpinTime = 2f, SmgSpinDown = 0.6f, SmgSpray = 0.16f, SmgSprayTight = 0.04f, SmgSpinDamage = 0.6f;

        void TickSpinUp(Player p, float dt)
        {
            bool spinning = p.Weapon.Id == WeaponId.BurstSmg && p.Firing && !p.ParryActive && !p.Downed;
            p.SpinUp = spinning ? Math.Min(1f, p.SpinUp + dt / (SmgSpinTime * p.Stats.SpinTimeMult)) : Math.Max(0f, p.SpinUp - dt / (SmgSpinDown * p.Stats.SpinDownMult));  // (Spin-Up)
        }

        /// Scattergun: pellets that hit within ScatterClose of you (to the enemy's edge) do double and shove it back.
        public const float ScatterClose = 120f, ScatterPointBlank = 2f, ScatterShove = 16f;

        /// Flak Cannon: the shell bursts at your cursor (never nearer than FlakMin), hurting enemies and destroying
        /// enemy bullets within FlakBurst (red aimed shots get through).
        public const float FlakMin = 60f, FlakBurst = 80f;

        float PointBlankMult(Bullet b, Enemy e)
        {
            Player p;
            if (!b.PointBlank || !Players.TryGetValue(b.Owner, out p) || p.Pos.DistanceTo(e.Pos) - e.Radius > ScatterClose * p.Stats.PointBlankReachMult)  // (Point Blank: farther, harder)
                return 1f;
            if (!Heavy(e))
            {
                ShoveFrom(e, p.Pos, ScatterShove * p.Stats.ScatterShoveMult, b.Vel.Normalized());  // slides (Knock.cs)
            }
            return ScatterPointBlank;
        }

        readonly List<int> flakScratch = new List<int>();  // (FlakAirburst: the enemy shots it pops)

        /// A Flak Cannon shell reached the end of its flight: the airburst.
        void FlakAirburst(Bullet b)
        {
            foreach (var e in FoesInCircle(b.Pos, b.Splash, b.Pos, by: b.Owner))
            {
                HurtEnemy(e, b.Dmg, b.Owner);
                OnBulletHit(b, e);
            }
            var shot = flakScratch;
            shot.Clear();
            foreach (var o in Bullets.Values)
                if (Defs.IsEnemyShot(o.Kind) && !Defs.Unblockable(o.Kind) && o.Pos.CloserThan(b.Pos, b.Splash + o.Radius))
                    shot.Add(o.Id);
            foreach (int id in shot)
            {
                var o = Bullets[id];
                AddFx(FxKind.Spark, o.Pos, Defs.BulletColor(o.Kind), 8f, 0.25f);
                KillBullet(id);
            }
            float offset = RandAngle();
            for (int i = 0; i < b.Frags; i++)  // extra projectiles: bomblets scattering round the burst, popping where they land
            {
                V2 d = V2.FromAngle(offset + MathX.Tau * i / b.Frags);
                var nb = SpawnBullet(b.FragKind, b.Pos + d * 6f, d * b.FragSpeed, 0f, b.FragLife, b.FragDmg, b.Owner);
                nb.Splash = b.FragSplash;
                nb.Bomblet = true;
                nb.Burst = 1;
            }
            GateLockBlast(b.Pos, b.Splash, b.Owner);  // Warden.cs
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
            if (p.Weapon.Id == WeaponId.LeechTendrils || p.Weapon.Id == WeaponId.ChainLight || p.Weapon.Id == WeaponId.ArcLightning)
            {
                FireVolley(p, aim, extra);  // Barrage adds a tendril, cones side by side or parallel strips instead (Hemomancer.cs, Minions.cs)
                return;
            }
            if (p.Weapon.Id == WeaponId.Implosion || p.Weapon.Id == WeaponId.HoneybeeStaff)
            {
                FireVolley(p, aim, extra + p.Stats.Fan);  // no direction to fan in: each copy is another implosion (or another bee)
                return;
            }
            for (int v = 0; v <= p.Stats.Fan; v++)
            {
                volleyShare = v == 0 ? 1f : BarrageCopyDamage;  // Barrage's fanned copies hit softer than the shot itself
                FireVolley(p, aim + (v == 0 ? 0f : (v % 2 == 1 ? 1f : -1f) * 0.14f * ((v + 1) / 2)), extra);
            }
            volleyShare = 1f;
        }

        /// Barrage: each fanned copy deals this share of the shot's damage (was full damage before the Oct 2026 pass;
        /// copies multiplied every other upgrade).
        public const float BarrageCopyDamage = 0.6f;
        float volleyShare = 1f;  // set while a Barrage copy fires

        /// Extra projectiles from upgrades, plus two during Artillery Assault.
        public int ExtraShots(Player p) => p.Stats.ExtraShots + (Storming(p) ? (Mastered(p, AbilityId.ArtilleryAssault) ? 4 : 2) : 0)
            + (p.OverdriveT > 0f && Mastered(p, AbilityId.Overdrive) ? 1 : 0);

        // --- Revolver (the Healer's Pistol): a single-action gun ------------------------------------------------------

        public const float RevCock = 0.15f;     // a click after a shot pulls the hammer back over this long...
        public const float RevShotRecover = 0.08f;  // ...and a shot holds the gun this long before the next click counts
        const float RevBotClick = 0.25f;        // a bot holding Fire clicks this often (four clicks a second: two shots)

        /// How long the hammer takes to come back (fire-rate upgrades and Hot Lead shorten it).
        public float RevCockTime(Player p) => RevCock / FireRate(p) * p.Stats.RevolverCockMult;

        // --- Mender's Bow: hold to draw, release to fire ----------------------------------

        public const float BowMinDraw = 0.15f;   // a quick tap still looses a weak arrow (drawn this far)
        public const float BowFullDraw = 0.9f;   // a full draw (fire-rate upgrades shorten it)
        public const float BowPerfectWindow = 0.25f;  // the Railgun: let go this soon after full draw for a perfect shot (the bow has the Focus bar: BowFocus.cs)
        public const float BowRecover = 0.25f;   // after a shot, the next draw starts this much later
        public const float BowDrawSlow = 0.85f;  // move speed while drawing

        public float BowFull(Player p) => p.Weapon != null && p.Weapon.Id == WeaponId.ArcLightning ? ArcTagGap(p) * ArcTagMax(p) + 0.05f  // Arc Lightning: every tag in (ArcLock.cs)
            : (p.Weapon != null && p.Weapon.Id == WeaponId.Railgun ? RailFullDraw : BowFullDraw) / FireRate(p) * (p.Weapon != null && p.Weapon.Id == WeaponId.MendersBow ? p.Stats.BowDrawMult : p.Weapon != null && p.Weapon.Id == WeaponId.ArcLightning ? p.Stats.ArcChargeMult : 1f);  // (Steady Draw, Static Field)  // (Steady Draw: sooner)

        /// Weapons you hold to charge and let go to fire: the Mender's Bow, the Railgun, the Warhammer, the Golem Staff's boulder, Arc Lightning, the Imp Caller and the Gore Mortar.
        public static bool ChargeWeapon(WeaponId w) => w == WeaponId.MendersBow || w == WeaponId.Railgun || w == WeaponId.Warhammer || w == WeaponId.GolemStaff || w == WeaponId.ArcLightning || w == WeaponId.ImpCaller || w == WeaponId.GoreMortar;

        /// 0 for a tap, 1 at full draw.
        public float BowCharge(Player p, float draw) => MathX.Clamp01((draw - BowMinDraw) / Math.Max(0.01f, BowFull(p) - BowMinDraw));

        public bool BowPerfect(Player p, float draw) => draw >= BowFull(p) && draw <= BowFull(p) + BowPerfectWindow;


        /// The host fires a bow shot when the shooter's release arrives (with how long they drew).
        void TickBow(Player p)
        {
            if (p.BowRelease <= 0f || State == MatchState.GameOver)
                return;
            p.BowShotDraw = p.BowRelease;
            p.BowShotFocus = p.BowFocus && p.Weapon.Id == WeaponId.MendersBow;
            p.BowRelease = 0f;
            p.BowFocus = false;
            FirePlayer(p);
        }

        // Local draw state for this machine's player (the shooter measures the draw).
        float bowDraw = -1f, bowCd;

        /// Your own draw: starts when you hold fire (once the last shot has recovered); letting go fires.
        /// Returns the draw time on the frame you let go, else 0.
        /// Your own charge weapon's releases, counted, with the last one's weapon and charge (0 a tap, 1 full), for the
        /// charge camera's kick (Game/ChargeCameraFx.cs). Local only.
        public int ChargeReleases { get; private set; }
        public WeaponId ChargeLastWeapon { get; private set; }
        public float ChargeLastCharge { get; private set; }

        float LocalBow(Player me, bool firing, float dt)
        {
            bowCd -= dt;
            float release = 0f;
            bowFocusOut = false;
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
                bowFocusOut = BowInZone(me, bowDraw);  // let go in the Focus bar's zone: a Focus shot (BowFocus.cs)
                NoteBowRelease(me, bowDraw);
                ChargeReleases++;  // (the charge camera, Game/ChargeCameraFx.cs)
                ChargeLastWeapon = me.Weapon.Id;
                ChargeLastCharge = BowCharge(me, bowDraw);
                release = Math.Min(10f, Math.Max(BowMinDraw, bowDraw));
                bowDraw = -1f;
                bowCd = BowRecover / FireRate(me);
            }
            me.DrawT = Math.Max(0f, bowDraw);
            return release;
        }

        /// The Revolver's trigger, both stances. Gunslinger: every click alternates between firing (a hard, perfectly aimed
        /// round) and pulling the hammer back (RevCock), so it shoots as fast as you click; a click that comes while the gun
        /// is busy is kept and used the moment it can be. The Fan stance's clicks are the Pistol Whip and Fan the Hammer
        /// (PistolWhip.cs). A bot holding Fire clicks every RevBotClick.
        void RevolverTrigger(Player p, float dt)
        {
            bool pressed = p.Firing && !p.WasFiring;
            p.WasFiring = p.Firing;
            if (p.IsBot)
            {
                p.RevBotClick -= dt;
                pressed = p.Firing && p.RevBotClick <= 0f;
                if (pressed)
                    p.RevBotClick = RevBotClick;
            }
            if (p.RevCockT > 0f)
            {
                p.RevCockT -= dt;
                if (p.RevCockT <= 0f)
                {
                    p.RevCockT = 0f;
                    p.RevCocked = true;
                }
            }
            p.TapQueued |= pressed;
            if (p.RevStance == 1)
            {
                RevFanStance(p, dt);  // PistolWhip.cs
                return;
            }
            if (!p.TapQueued || p.FireCd > 0f || p.RevCockT > 0f || p.Downed || p.StunT > 0f || State == MatchState.GameOver)
                return;
            if (p.ParryActive)
            {
                p.TapQueued = false;  // (can't shoot behind the shield)
                return;
            }
            p.TapQueued = false;
            if (p.RevCocked)
            {
                p.RevCocked = false;
                p.FireCd = RevShotRecover / FireRate(p);
                FirePlayer(p);
            }
            else
                p.RevCockT = RevCockTime(p);
        }

        /// One shot of the player's weapon, upgraded by `extra` extra projectiles.
        void FireVolley(Player p, float aim, int extra)
        {
            firingWeapon = true;
            float dmg = p.Stats.DmgMult * volleyShare;
            V2 dir = V2.FromAngle(aim);
            V2 muzzle = p.Pos + dir * p.Def.BodyR;
            var w = p.Weapon;  // its Speed, Life and Damage (Defs)
            switch (w.Id)
            {
                case WeaponId.TrainingPistol:  // Training's recruit (Training.cs): one plain round
                    SpawnBullet(Defs.DpsShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                case WeaponId.Scattergun:
                {
                    // +2 pellets and a 15% tighter cone per extra projectile.
                    int n = 5 + 2 * extra;
                    float cone = 0.52f * (float)Math.Pow(0.85, extra);
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + (i / (n - 1f) - 0.5f) * cone) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).PointBlank = true;
                    break;
                }
                case WeaponId.GrenadeLauncher:
                    LaunchGrenade(p, aim, dmg, extra);  // GrenadeLauncher.cs
                    break;
                case WeaponId.FlakCannon:
                {
                    // An airburst: the shell flies over enemies to your cursor (as far as its range) and bursts there,
                    // shredding enemies and enemy bullets in the burst.
                    // Velocity Rounds speed the shell up (SpawnBullet) and reach farther, but it still bursts at the cursor.
                    float fast = w.Speed * p.Stats.ShotSpeedMult;
                    float far = Math.Min(fast * w.Life, Math.Max(FlakMin, p.AimAt.DistanceTo(p.Pos) - p.Def.BodyR));
                    var b = SpawnBullet(Defs.FlakShell, muzzle, dir * w.Speed, 0f, far / fast, w.Damage * dmg, p.Id);
                    b.Splash = FlakBurst * AreaMult(p) * p.Stats.FlakBurstMult;  // (Wide Burst: wider)
                    b.Burst = 1;  // marks it as bursting where its flight ends (MoveBullets)
                    if (extra > 0)  // bomblets scatter round the burst and pop where they land
                        SetFrags(b, 2 * extra, Defs.GrenadeShot, 10f * dmg, 260f, 0.3f, 45f * AreaMult(p), false);
                    break;
                }
                case WeaponId.RiotRepeater:
                    SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + Rand(-0.06f, 0.06f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Suppressive = true;
                    break;
                case WeaponId.BearTotem:
                    FireBearClaws(p, aim, dmg, extra);  // a summoner's melee weapon (KitWeapons.cs)
                    break;
                case WeaponId.RepulsionCannon:
                    FireRepulsion(p, aim, dmg, extra);  // the Repulsion Cannon (Repulsion.cs)
                    break;
                case WeaponId.Revolver:
                {
                    // Revolver: one hard, perfectly accurate round; each extra projectile lets it go through one more enemy.
                    var rb = SpawnBullet(Defs.HealerShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * (1f + p.Stats.RevolverShotBonus) * dmg, p.Id);  // (Hot Lead: harder)
                    rb.Radius = 6f * p.Stats.ShotSizeMult;  // Magnum Rounds
                    rb.Pierce += extra;
                    break;
                }
                case WeaponId.MendersBow:
                {
                    // Draw and release: the longer the draw, the harder, faster and farther the arrow. Let go in the
                    // Focus bar's zone and the first arrow is a Focus arrow instead (BowFocus.cs).
                    float c = BowCharge(p, p.BowShotDraw);
                    bool focus = p.BowShotFocus && c >= 1f;
                    float baseDmg = MathX.Lerp(8f, 40f, c), speed = MathX.Lerp(450f, 950f, c), reach = MathX.Lerp(500f, 900f, c);
                    float heal = MathX.Lerp(5f, 20f, c);
                    // A full draw also looses an extra arrow (half damage) per extra projectile, in a tight fan.
                    int arrows = c >= 1f ? 1 + extra : 1;
                    for (int k = 0; k < arrows; k++)
                    {
                        float a = aim + (k == 0 ? 0f : (k % 2 == 1 ? 1f : -1f) * 0.07f * ((k + 1) / 2));
                        if (k == 0 && focus)
                        {
                            FocusArrow(p, muzzle, a, dmg).Radius = 6f * p.Stats.ShotSizeMult;  // (Magnum Rounds)
                            continue;
                        }
                        float d = baseDmg * (k == 0 ? 1f : 0.5f);
                        var b = SpawnBullet(Defs.ArrowShot, muzzle, V2.FromAngle(a) * speed, 0f, reach / speed, d * dmg, p.Id);
                        b.HealAlly = heal * (k == 0 ? 1f : 0.5f) * 9f / d;  // HealAllyHit scales it by Dmg / 9 (so damage upgrades heal more too)
                        b.Pierce += (c >= 1f ? 2 : c >= 0.5f ? 1 : 0) + p.Stats.BowPierceBonus;  // (Steady Draw: one more)
                        b.Bolts = c >= 0.5f;  // a drawn arrow pierces: Ricochet flings bounce bolts off it
                    }
                    if (focus)
                        BroadcastFx(FxKind.Spark, muzzle, new Rgba(1f, 0.85f, 0.35f), 26f, 0.3f, Sfx.Pick);
                    break;
                }
                case WeaponId.TwinRifle:
                {
                    // Barrels spread apart (two, plus one per extra projectile), angled in so every stream crosses at
                    // your cursor's distance; hits near the crossing are crossfire, for 1.5x. Each volley strays up to 2°.
                    // The barrels share a fixed width (extra projectiles fill it in, not widen it), and Barrage's fanned
                    // copies fire from a narrow cluster so the fan reads as a fan, not overlapping wide pairs.
                    int n = 2 + extra;
                    float span = volleyShare < 1f ? TwinCopySpan : TwinBarrelGap;
                    float gap = span / (n - 1);
                    float a = aim + Rand(-TwinRifleSpread, TwinRifleSpread);
                    V2.Axes(a, out V2 v, out V2 side);
                    V2 cross = p.Pos + v * TwinCrossDistance(p);
                    float outFrom = p.Def.BodyR + TwinMuzzleOut;
                    for (int i = 0; i < n; i++)
                    {
                        V2 from = p.Pos + v * outFrom + side * ((i - (n - 1) / 2f) * gap);
                        if (ActiveLayout != null && SegBlocked(ActiveLayout, p.Pos, from, 0f))
                            from = muzzle + side * ((i - (n - 1) / 2f) * gap);  // a gun pushed into a wall fires from your edge
                        var b = SpawnBullet(Defs.DpsShot, from, (cross - from).Normalized() * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.Cross = cross;
                        b.Crossfire = true;
                    }
                    break;
                }
                case WeaponId.OutOfAmmo:  // (it works on its own, TickOutOfAmmo; this is for anything that fires the weapon by hand: the next move of the rifle arms)
                    if (p.AmmoMove == 0)
                        AmmoStart(p);
                    break;
                case WeaponId.TwinRifleAuto:  // (it fires itself, TickTwinAuto; this is for anything that fires the weapon by hand, like the range test)
                    for (int gun = 0; gun < 2; gun++)
                        AutoShoot(p, gun, null, false, extra);
                    break;
                case WeaponId.DoubleBarrel:
                    FireDoubleBarrel(p, aim, dmg, extra, false);  // DoubleBarrel.cs
                    break;
                case WeaponId.TwinPistols:
                    FireTwinPistols(p, aim, dmg, extra);  // TwinPistols.cs
                    break;
                case WeaponId.Railgun:
                    FireRailgun(p, muzzle, dir, dmg, extra);  // the charged ricochet beam (RailBeam.cs)
                    break;
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
                    Classes.Get(w.Kit).Fire?.Invoke(this, p, aim, dir, muzzle, dmg, extra);  // the weapon's class fires it (its KitDef.Fire)
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
                    return p.Def.BodyR + MathX.Lerp(500f, 900f, p.Id == MyId && bowDraw >= 0f ? BowCharge(p, bowDraw) : 0f) * p.Stats.ShotSpeedMult;
                case WeaponId.GolemStaff:  // where the boulder lands with the current charge (Minions.cs)
                    return BoulderDistance(p.Id == MyId && bowDraw >= 0f ? BowCharge(p, bowDraw) : 0f) * p.Stats.ShotSpeedMult;
                case WeaponId.GoreMortar:  // where the Gore Mortar's clot lands with the current charge (Hemomancer.cs)
                    return ClotDistance(p.Id == MyId && bowDraw >= 0f ? BowCharge(p, bowDraw) : 0f) * p.Stats.ShotSpeedMult;
                case WeaponId.Warhammer:  // the middle of its slam's circle, the furthest aftershock's if it has them (Knight.cs)
                    return HammerCentre(MeleeReach(p, w.Reach + w.Grow * x));
                case WeaponId.Ofuda:  // where a charm lands as a ward, at most (Ninja.cs)
                    return p.Def.BodyR + WardRange * p.Stats.ShotSpeedMult * PierceReach(p);
                case WeaponId.WispLantern:  // how far a wisp is sent (Minions.cs)
                    return WispReach * PierceReach(p);
                case WeaponId.ImpCaller:  // how far imps are sent (Minions.cs)
                    return ImpReach * PierceReach(p);
                case WeaponId.OrbitShards:  // how far a shard is flung (Arcanist.cs)
                    return ShardReach * PierceReach(p);
                case WeaponId.DancingBlades:  // how far a blade is launched (Arcanist.cs)
                    return BladeReach * PierceReach(p);
                case WeaponId.HoneybeeStaff:  // how far from you a bee can hatch (HoneybeeStaff.cs)
                    return HatchReach;
                case WeaponId.WyvernStaff:  // how far from you the breath can be aimed (WyvernStaff.cs)
                    return BreathReach;
                case WeaponId.BloodBlast:  // the next step's reach (BloodBlast.cs)
                    return BlastNextReach(p);
                case WeaponId.Railgun:  // the beam runs to the first wall (RailBeam.cs)
                    return RailFirstReach(p);
                case WeaponId.EmbolusSpears:  // a spear flies until a wall stops it (EmbolusSpears.cs)
                    return EsRange;
                case WeaponId.ArcLightning:  // the tagging cone's reach (ArcLock.cs)
                    return ArcReach(p);
                case WeaponId.ChainLight:  // the cone's reach (Minions.cs)
                    return LightConeLength(p, x);
                case WeaponId.FumaShuriken:  // the far end of its widest loop (Ninja.cs)
                    return FumaReach * FumaSize(x) * p.Stats.ShotSpeedMult * PierceReach(p);
                case WeaponId.Implosion:  // how far away you can click (Hemomancer.cs)
                case WeaponId.BloodBrawler:  // (BloodBrawler.cs)
                    return p.Def.BodyR + w.Reach * p.Stats.ShotSpeedMult;
                case WeaponId.Flamethrower:  // the lead flame's reach (Minions.cs)
                case WeaponId.WaterGun:      // (ElementGuns.cs)
                case WeaponId.GaleGun:
                case WeaponId.SpiritFan:     // the gust's reach (Ninja.cs)
                    return p.Def.BodyR + w.Speed * w.Life * (1f + w.Grow * x) * p.Stats.ShotSpeedMult * PierceReach(p);
                case WeaponId.RepulsionCannon:  // the Repulsion Cannon's cone
                    return p.Def.BodyR + w.Reach * AreaMult(p) * PierceReach(p);
                case WeaponId.StasisGun:  // how far from you the field can sit (EngineerBots.cs)
                    return StasisReach * p.Stats.ShotSpeedMult;
                case WeaponId.LeechTendrils:  // Leech Tendrils reach from your edge (Hemomancer.cs)
                    return p.Def.BodyR + GraspReach(p);
                case WeaponId.StaticShock:  // the cone (StaticShock.cs)
                    return StaticHand(p, p.Pos).DistanceTo(p.Pos) + StaticRangeOf(p);
                case WeaponId.RadiantOrb:  // as far out as the orb can go (RadiantOrb.cs)
                    return RadiantReach(p);
                case WeaponId.VeinTether:  // a beam from your edge (Hemomancer.cs)
                    return p.Def.BodyR + w.Reach * (1f + w.Grow * x) * p.Stats.ShotSpeedMult;
                case WeaponId.Lightbringer:  // its sword waves reach past the swing
                    return Math.Max(MeleeReach(p, w.Reach), p.Def.BodyR + 600f * 0.45f * p.Stats.ShotSpeedMult);
                case WeaponId.Nimbus:  // how far from you the cloud can drift (Nimbus.cs)
                    return NimbusReach(p);
                case WeaponId.SpiritChalk:  // how far from you you can draw (Chalk.cs)
                    return ChalkReach(p);
                case WeaponId.TwinRifle:  // the rounds start out at the muzzles
                    return p.Def.BodyR + TwinMuzzleOut + w.Speed * w.Life * (1f + w.Grow * x) * p.Stats.ShotSpeedMult;
                case WeaponId.OutOfAmmo:  // how far the knife chain's strikes reach (OutOfAmmo.cs)
                    return p.Def.BodyR + AmmoReach;
                case WeaponId.TwinRifleAuto:  // the rounds start out at the guns' muzzles, out to your sides (TwinAuto.cs)
                    return p.Def.BodyR + AutoShoulder + w.Speed * w.Life * (1f + w.Grow * x) * p.Stats.ShotSpeedMult;
                case WeaponId.Fireball:  // Piercing Rounds roll it farther (Minions.cs)
                case WeaponId.Waterball:
                    return p.Def.BodyR + w.Speed * w.Life * p.Stats.ShotSpeedMult * PierceReach(p);
                case WeaponId.FrostLance:  // the sweeps' reach (FrostLance.cs)
                    return p.Def.BodyR + FlArcR * AreaMult(p) * (1f + 0.1f * x) * p.Stats.IceReachMult;
                case WeaponId.TreantRod:   // the root lash, out from you
                    return MeleeReach(p, w.Reach + w.Grow * x);
                case WeaponId.Katana:  // Hayate's dashes reach further than Senbon's swipes (Katana.cs)
                    return p.KatanaStance == 0 ? MeleeReach(p, KatanaDashLen) : MeleeReach(p, w.Reach);
                case WeaponId.BoStaff:  // each step of the chain has its own reach (Ninja.cs)
                    return MeleeReach(p, (p.BoStep == 1 ? BoJabReach : p.BoStep == 2 ? BoSpinReach : w.Reach) + w.Grow * x);
                case WeaponId.Greatsword:  // the next chain step's reach (GreatswordChain.cs; extra projectiles only add Eclipse's sword waves)
                    return GreatNextReach(p);
                case WeaponId.Lance:  // the jabs' reach, or the Breaching Thrust's when it is next (extra projectiles only lengthen the thrust; Lance.cs)
                    return MeleeReach(p, p.LancePrimed || TwinBladesMotion.StepOf(p.Combo) == 3 ? w.Reach + BreachExtraReach + w.Grow * x : w.Reach);
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
