using System;
using System.Collections.Generic;

namespace Arena
{
    /// Player weapons: firing volleys, the Revolver and Mender's Bow, and how far each weapon reaches.
    public partial class Game
    {
        /// Everything that speeds up firing: upgrades, Overdrive, Bullet Storm, Counterstance.
        float FireRate(Player p) => p.Stats.FireRateMult * (p.OverdriveT > 0f ? 2f : 1f) * (Storming(p) ? 3f : 1f) * (p.FuryT > 0f ? 1.4f : 1f)
            * (p.CounterT > 0f ? 1f + p.Stats.CounterFireRate : 1f);

        /// spread: each shot lands somewhere within this many radians of your aim (fanning the hammer).
        void FirePlayer(Player p, float spread = 0f)
        {
            float dmg = p.Stats.DmgMult;
            p.Volleys++;
            firingWeapon = true;
            if (p.Stats.HaloEvery > 0 && p.Volleys % p.Stats.HaloEvery == 0)
                for (int i = 0; i < 8; i++)
                {
                    V2 d = V2.FromAngle(p.Aim + MathX.Tau * i / 8f);
                    SpawnBullet(Defs.NovaShot, p.Pos + d * p.Def.BodyR, d * 450f, 0f, 0.8f, 9f * dmg, p.Id);
                }
            // Each extra projectile (Multishot, Iron Volley, Bullet Storm) upgrades the weapon in its own way.
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
        const float RevolverDamage = 24f;

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
            if (me.Weapon.Id != WeaponId.MendersBow || me.Downed || me.ParryActive)
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
            switch (p.Weapon.Id)
            {
                case WeaponId.Scattergun:
                {
                    // +2 pellets and a 15% tighter cone per extra projectile.
                    int n = 5 + 2 * extra;
                    float cone = 0.52f * (float)Math.Pow(0.85, extra);
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + (i / (n - 1f) - 0.5f) * cone) * 520f, 0f, 0.5f, 7f * dmg, p.Id);
                    break;
                }
                case WeaponId.FlakCannon:
                {
                    var b = SpawnBullet(Defs.FlakShell, muzzle, dir * 380f, 0f, 1.1f, 30f * dmg, p.Id);
                    b.Splash = 70f;
                    if (extra > 0)  // bomblets scatter around the impact and pop where they land
                        SetFrags(b, 2 * extra, Defs.GrenadeShot, 10f * dmg, 260f, 0.3f, 45f, false);
                    break;
                }
                case WeaponId.RiotRepeater:
                    SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + Rand(-0.06f, 0.06f)) * 600f, 0f, 0.8f, 11f * dmg, p.Id);
                    break;
                case WeaponId.BearTotem:
                    FireBearClaws(p, aim, dmg, extra);  // a summoner's melee weapon (Minions.cs)
                    break;
                case WeaponId.LightBolt:
                    SpawnBullet(Defs.HealerShot, muzzle, dir * 650f, 0f, 1.3f, 11f * dmg, p.Id);
                    for (int k = 0; k < extra; k++)  // smaller bolts that seek out the nearest enemy
                    {
                        float a = aim + (k % 2 == 0 ? 1f : -1f) * (0.35f + 0.15f * (k / 2));
                        SpawnBullet(Defs.HealerShot, muzzle, V2.FromAngle(a) * 600f, 0f, 1.3f, 6f * dmg, p.Id, false, 6f).Radius = 3f;
                    }
                    break;
                case WeaponId.Pistol:
                {
                    // Revolver: a perfectly accurate round; the last ones in the cylinder are heavy.
                    int left = RevolverCapacity(p) - p.AmmoUsed;  // FirePlayer uses up the round
                    bool heavy = left <= RevolverHeavy(p);
                    SpawnBullet(Defs.HealerShot, muzzle, dir * 700f, 0f, 1.1f, RevolverDamage * (heavy ? 2f : 1f) * dmg, p.Id).Radius = heavy ? 6f : 4.5f;
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
                        if (perfect)
                            b.Radius = 6f;
                    }
                    if (perfect)
                        BroadcastFx(FxKind.Spark, muzzle, new Rgba(1f, 0.85f, 0.35f), 26f, 0.3f, Sfx.Pick);
                    break;
                }
                case WeaponId.TwinRifle:
                {
                    // Parallel barrels, 10 px apart: two, plus one per extra projectile.
                    int n = 2 + extra;
                    V2 side = dir.Orthogonal();
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.DpsShot, muzzle + side * ((i - (n - 1) / 2f) * 10f), dir * 820f, 0f, 1f, 6.5f * dmg, p.Id);
                    break;
                }
                case WeaponId.Railgun:
                {
                    var b = SpawnBullet(Defs.RailShot, muzzle, dir * 1600f, 0f, 0.7f, 45f * dmg, p.Id);
                    b.Pierce += 3;
                    if (extra > 0)
                        SetFrags(b, 2 * extra, Defs.DpsShot, 11f * dmg, 600f, 0.3f, 0f, true);
                    break;
                }
                case WeaponId.BurstSmg:
                {
                    // +1 bullet per shot per extra projectile (at half damage), sprayed a little wider.
                    float spray = 0.16f * (1f + 0.35f * extra);
                    for (int i = 0; i <= extra; i++)
                        SpawnBullet(Defs.DpsShot, muzzle, V2.FromAngle(aim + Rand(-spray, spray)) * 760f, 0f, 0.8f, (i == 0 ? 5f : 2.5f) * dmg, p.Id);
                    break;
                }
                default:
                    if (p.Weapon.Id >= WeaponId.Kusarigama)
                        FireNinjaWeapon(p, aim, dir, muzzle, dmg, extra);  // Ninja.cs
                    else if (p.Weapon.Melee)
                        FireKnightWeapon(p, aim, dmg, extra);  // Knight.cs
                    else
                        FireKitWeapon(p, aim, dir, muzzle, dmg, extra);  // mage and summoner weapons (Minions.cs)
                    break;
            }
            firingWeapon = false;
        }

        /// How far the player's weapon reaches from their centre right now: a shot's speed times its lifetime
        /// (with Velocity Rounds) plus the muzzle offset, or a melee weapon's reach. Keep in step with FireVolley,
        /// FireKitWeapon and FireKnightWeapon (SimTest checks it against real shots). The aim line uses it.
        public float WeaponRange(Player p)
        {
            int x = ExtraShots(p);
            float shot;
            switch (p.Weapon.Id)
            {
                case WeaponId.Scattergun: shot = 520f * 0.5f; break;
                case WeaponId.FlakCannon: shot = 380f * 1.1f; break;
                case WeaponId.RiotRepeater: shot = 600f * 0.8f; break;
                case WeaponId.LightBolt: shot = 650f * 1.3f; break;
                case WeaponId.Pistol: shot = 700f * 1.1f; break;
                case WeaponId.MendersBow: shot = Lerp(500f, 900f, p.Id == MyId && bowDraw >= 0f ? BowCharge(p, bowDraw) : 0f); break;
                case WeaponId.TwinRifle: shot = 820f * 1f; break;
                case WeaponId.Railgun: shot = 1600f * 0.7f; break;
                case WeaponId.BurstSmg: shot = 760f * 0.8f; break;
                case WeaponId.FlameBurst: shot = 480f * 0.35f * (1f + 0.25f * x); break;
                case WeaponId.FrostLance: shot = 700f * 0.9f; break;
                case WeaponId.StormHammer: shot = 420f * 1.2f; break;
                case WeaponId.RadiantOrb: shot = 300f * 1.8f; break;
                case WeaponId.ChainLight: shot = 800f * 0.8f; break;
                case WeaponId.PrismBeam: shot = 1200f * 0.35f * (1f + 0.3f * x); break;
                case WeaponId.Fireball: shot = 520f * 1.2f; break;
                case WeaponId.ArcLightning: shot = 900f * 0.6f; break;
                case WeaponId.ArcaneMissiles: shot = 520f * 1f; break;
                case WeaponId.GolemStaff: shot = 480f * 0.6f; break;
                case WeaponId.BoneGuard: case WeaponId.WolfWhistle: case WeaponId.ImpCaller: shot = 720f * 0.8f; break;
                case WeaponId.BearTotem: return MeleeReach(p, BearReach + 15f * x);
                case WeaponId.SpriteStaff: case WeaponId.TotemStaff: case WeaponId.TreantRod: case WeaponId.WispLantern: shot = 600f * 1f; break;
                // Ninjas
                case WeaponId.ChainShuriken: shot = ChainStarSpeed * ChainStarOut; break;  // as far as it goes before it turns back
                case WeaponId.MedicineDarts: shot = 900f * 0.8f; break;
                case WeaponId.SpiritFan: shot = 420f * 0.3f * (1f + 0.25f * x); break;
                case WeaponId.Ofuda: shot = 360f * 1.6f; break;
                case WeaponId.Shuriken: shot = 700f * 0.75f; break;
                case WeaponId.Kunai: shot = 850f * 0.75f; break;
                case WeaponId.Kusarigama: return MeleeReach(p, KusariReach + 20f * x);
                case WeaponId.BoStaff: return MeleeReach(p, BoReach + 20f * x);
                // Knights: the reach of the swing.
                case WeaponId.Warhammer: return MeleeReach(p, 100f + 30f * x);
                case WeaponId.Lance: return MeleeReach(p, LanceReach + 30f * x);
                case WeaponId.Halberd: return MeleeReach(p, 160f + 30f * x);
                case WeaponId.Censer: return MeleeReach(p, 80f + 20f * x);
                case WeaponId.Lightbringer: return Math.Max(MeleeReach(p, 80f), p.Def.BodyR + 600f * 0.45f * p.Stats.ShotSpeedMult);  // its sword waves
                case WeaponId.Greatsword: return x > 0 ? p.Def.BodyR + 700f * 0.6f * p.Stats.ShotSpeedMult : MeleeReach(p, 125f);
                default: return p.Weapon.Melee ? MeleeReach(p, MeleeRange(p.Weapon.Id)) : 600f;
            }
            return p.Def.BodyR + shot * p.Stats.ShotSpeedMult;
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

        bool firingWeapon;  // set while a player's weapon fires, to tag gun shots for accuracy
    }
}
