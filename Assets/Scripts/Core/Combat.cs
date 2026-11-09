using System;
using System.Collections.Generic;

namespace Arena
{
    /// Combat (host): bullets, collisions, damage to players and enemies, on-hit and on-kill effects, orbs.
    public partial class Game
    {
        static string ShotSource(Bullet b) =>
            (b.Source.HasValue ? Defs.Get(b.Source.Value).Name : "Enemy") + (b.Kind == Defs.PierceShot ? " (aimed)" : "");

        void Damage(Player p, float amount, string source = "Other")
        {
            lastTaken = 0f;  // (Lightning Rod's grey number reads what got through; LightningRod.cs)
            if (p.DevGod)
                return;
            if (p.SwallowT > 0f && !stomach)
                return;  // inside the Devourer: nothing out here can reach you (Devourer.cs)
            if (InDome(p.Pos, -2f))
                return;  // inside Fortress's dome nothing hostile reaches you (Dome.cs)
            if (!tickDamage && RiotBlocks(p, source))
                return;  // Riot Gear took the hit (RiotGear.cs)
            amount *= EnemyDamageScale(Math.Max(1, Wave));  // the difficulty's enemy damage, growing each wave (Difficulty.cs)
            if (p.TauntT > 0f)
                amount *= Math.Max(0.2f, 1f - RoarReduction - p.Stats.RoarExtraReduction);
            amount *= RodMult(p);  // Lightning Rod's charge (LightningRod.cs)
            foreach (var q in Players.Values)
                if (q.LinkT > 0f && q.LinkTo == p.Id && q != p)
                    amount *= 0.5f;  // a healer's Soul Link halves the damage
            amount *= p.Stats.DamageTaken;
            if (Has(RunMod.GlassCannon))
                amount *= Modifiers.GlassCannonTaken;
            float cut = 0f;
            foreach (var z in Zones.Values)
            {
                if (z.DamageCut > 0f && p.Pos.DistanceTo(z.Pos) <= z.Radius)
                    cut = Math.Max(cut, z.DamageCut);
                if (InBath(z, p.Pos, 0f))
                    cut = Math.Max(cut, BathGuard);  // standing in anyone's Bloodbath (Bloodbath.cs)
            }
            amount *= 1f - cut;  // the Ninja's smoke
            if (p.AdrenalineT > 0f)
                amount *= 1f - AdrenalineCut;  // Adrenaline Shot
            if (p.UnbreakT > 0f)
                amount *= 1f - Math.Min(0.8f, CavGuard + p.Stats.UnbreakBonus);
            amount *= 1f - IronBodyCut(p);  // the Ninja's Iron Body
            amount *= 1f - ClotCut(p);      // Blood Lash's Clot
            amount = WardSoak(p, amount);  // a Spirit Ward near you takes it first (Ninja.cs)
            amount = JuggSoak(p, amount);  // then the Juggernaut Suit's armour (Juggernaut.cs)
            if (p.ShieldHp > 0f)  // shields soak damage next (Energy Ward, Guardian's Oath, Substitution, Blood Shell)
            {
                float soak = Math.Min(p.ShieldHp, amount);
                p.ShieldHp -= soak;
                amount -= soak;
                if (p.MercyT > 0f && soak > 0f)  // a Lightbringer's ward took it: the plate facing the shot flashes (Lightbringer.cs)
                    LightMark(p, 3, (RiotImpactPoint(p) - p.Pos).Angle, false);
                if (p.EnergyOn)
                {
                    p.EnergyStore += soak;  // an Energy Ward keeps it, to pay back
                    if (p.ShieldHp <= 0f)
                        EnergyPayback(p);
                }
            }
            p.Run.DamageTaken += Math.Min(amount, Math.Max(0f, p.Hp));
            DamageEvent?.Invoke(p, Math.Min(amount, Math.Max(0f, p.Hp)), source);
            Recorder?.DamageTaken(p, Math.Min(amount, Math.Max(0f, p.Hp)), source);
            if (amount > 0f && !tickDamage)
                SteelHurt(p);  // a hit knocks a third of Caught Steel's shards loose (CaughtSteel.cs)
            lastTaken = Math.Max(0f, amount);
            p.Hp -= amount;
            p.InvulnT = D(DiffKnob.HitGrace);
            p.Flash = 0.1f;
            if (p.Hp <= 0f && InLantern(p))
                p.Hp = 1f;  // a Soul Lantern keeps you standing
            if (p.Hp <= 0f && p.WardT > 0f)
            {
                // Martyr's ward: straight back up instead of going down.
                p.WardT = 0f;
                p.Hp = Math.Max(1f, p.MaxHp * p.WardFrac);
                p.InvulnT = Math.Max(p.InvulnT, p.WardInvuln);
                BroadcastFx(FxKind.MartyrCast, p.Pos, p.Pos, new Rgba(0.9f, 0.15f, 0.25f), 2f + 10f * p.Id, Math.Max(MartyrFxTime, p.WardInvuln), Sfx.Revive);  // the halo spends itself (Game/MartyrLook.cs)
            }
            DownIfDead(p, source);
        }

        /// A player at 0 health goes down (Damage, and Vein Tithe's drain, which isn't a hit and gives no grace).
        void DownIfDead(Player p, string source)
        {
            if (p.Hp > 0f || p.Downed)
                return;
            p.Hp = 0f;
            p.Downed = true;
            p.Run.Downs++;
            Recorder?.Downed(this, p, source);
            p.Revive = 0f;
            p.BarrierT = 0f;
            p.OverdriveT = 0f;
            p.AdrenalineT = 0f;
            p.Firing = false;
        }

        void RevivePlayer(Player p, float fraction, Player by = null)
        {
            Recorder?.Revived(p, by);
            if (by != null && by != p)
                by.Run.Revives++;
            p.Downed = false;
            p.Revive = 0f;
            p.Hp = Math.Max(p.Hp, p.MaxHp * fraction);
            p.InvulnT = 1.5f;
        }

        Bullet SpawnBullet(byte kind, V2 pos, V2 vel, float angVel, float life, float dmg, int owner, bool fromBoss = false, float homingTurn = 0f)
        {
            Player p;
            float homing = homingTurn;
            bool stamp = false;
            bool modded = owner != 0 && Players.TryGetValue(owner, out p) && Moddable(kind);
            if (modded)
            {
                p = Players[owner];
                vel *= p.Stats.ShotSpeedMult;  // Velocity Rounds
                if (!AreaWeapon(p.Weapon.Id))  // area weapons get bigger areas instead (AreaMult)
                    homing = Math.Max(homing, p.Stats.Homing);
                dmg *= HomingCost(p);  // Homing Rounds trade damage for forgiveness
            }
            if (owner == 0 && Defs.IsEnemyShot(kind))
            {
                enemyShots++;
                stamp = true;  // (below: who fired it, for Lightning Rod)
                if (!Defs.Unblockable(kind) && InIncense(pos))
                    vel = vel.Rotated(Rand(-IncenseStray, IncenseStray));  // fired from inside the Censer's incense: it strays
            }
            var b = MakeBullet(nextBulletId++, kind, owner, pos, vel, angVel, life, fromBoss, homing);
            if (stamp)
            {
                b.ShooterId = actingEnemy != null ? actingEnemy.Id : 0;  // the enemy whose turn it is (TickEnemies); 0 for hazards and the like
                b.ShooterPos = actingEnemy != null ? actingEnemy.Pos : pos;
            }
            b.Dmg = casting && !firingWeapon ? dmg * castPower : dmg;
            if (owner != 0 && Players.TryGetValue(owner, out p))
            {
                if (modded)
                    b.Bounces = p.Stats.Ricochet;
                if (firingWeapon)
                {
                    b.FromWeapon = true;
                    p.Run.ShotsFired++;
                }
                b.Pierce = p.Stats.Pierce;
            }
            var sent = new Bullet { Id = b.Id, Kind = kind, Owner = owner, Pos = pos, Vel = vel, AngVel = angVel, Life = life, FromBoss = fromBoss, Homing = homing };
            pendingSpawns.Add(sent);
            b.Sent = sent;  // a shape set after this (EnemyShot) goes out with it
            return b;
        }

        /// Weapon mods apply to players' shots, but not to grenades (they land where you aim)
        /// or to Swarm Rounds' splinters.
        static bool Moddable(byte kind) => !Defs.IsEnemyShot(kind) && kind != Defs.GrenadeShot && kind != Defs.SwarmShot && kind != Defs.MedkitShot && kind != Defs.TreantSeed && kind != Defs.SteelShard;

        Bullet MakeBullet(int id, byte kind, int owner, V2 pos, V2 vel, float angVel, float life, bool fromBoss = false, float homing = 0f)
        {
            var b = new Bullet { Id = id, Kind = kind, Owner = owner, Pos = pos, Vel = vel, AngVel = angVel, Life = life, Life0 = life, FromBoss = fromBoss, Homing = homing, Radius = Defs.BulletRadius(kind) };
            Player shooter;
            if (owner != 0 && Moddable(kind) && Players.TryGetValue(owner, out shooter))
                b.Radius *= shooter.Stats.ShotSizeMult;  // Magnum Rounds (every game knows everyone's upgrades, so it's the same size everywhere)
            Bullets[id] = b;
            if (owner == MyId && owner != 0 && kind != Defs.GrenadeShot && kind != Defs.MedkitShot)
                NoteShot(b);
            if (owner == 0)
                Sounds.Add(new SoundCue { Sfx = Sfx.EnemyShot, Quiet = QuietAt(pos) });  // quieter from across the arena (MeleeSounds.cs)
            else
                Sounds.Add(new SoundCue { Sfx = ShotSound(kind, owner), Ally = owner != MyId });
            return b;
        }

        Sfx ShotSound(byte kind, int owner)
        {
            if (kind == Defs.TurretShot || kind == Defs.MinionShot)
                return Sfx.ShotTurret;
            if (kind == Defs.NovaShot)
                return Sfx.ShotNova;
            if (kind == Defs.TreantSeed)
                return Sfx.ShotSeed;
            Player p;
            if (!Players.TryGetValue(owner, out p))
                return Sfx.ShotRifle;
            return p.Weapon.Sound;  // WeaponDef.Sound
        }

        readonly List<int> scratchIds = new List<int>();
        readonly List<Enemy> scratchDead = new List<Enemy>();  // Collide's dead enemies
        readonly List<int> scratchOrbsGone = new List<int>();  // TickOrbs

        readonly List<Bullet> landed = new List<Bullet>();

        void ExplodeGrenade(Bullet b)
        {
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(b.Pos) < b.Splash + e.Radius)
                    HurtEnemy(e, b.Dmg, b.Owner);
            float offset = Rand(0f, MathX.Tau);
            for (int i = 0; i < b.Burst; i++)
            {
                V2 dir = V2.FromAngle(offset + MathX.Tau * i / b.Burst);
                SpawnBullet(Defs.DpsShot, b.Pos, dir * 520f, 0f, 0.45f, b.Dmg * 0.12f, b.Owner);
            }
            BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.GrenadeShot), b.Splash, 0.35f, Sfx.Explode);
            PropBlast(b.Pos, b.Splash, b.Owner);  // sets barrels off (Props.cs)
            GateLockBlast(b.Pos, b.Splash, b.Owner);  // and cracks a Warden gate's lock (Warden.cs)
        }

        void MoveBullets(float dt)
        {
            scratchIds.Clear();
            landed.Clear();
            wells.Clear();
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.GravityWell && !IsSpawning(e))
                    wells.Add(e);
            bool wellsUp = wells.Count > 0;
            V2 wind = HeraldWind(WindDrift);  // the Storm Herald's Gale bends every bullet (Herald.cs)
            PrepareNinjaBullets(dt);  // Smoke Bombs and Fuma Shuriken loops (Ninja.cs)
            PrepareLumen();           // Lumen Wards (LumenWard.cs)
            // Bullets leave a little past the arena's edges, or stop dead at Narrow Pass's walls.
            V2 lo = ArenaMin, hi = ArenaMax, margin = new V2(Walled ? 0f : 40f, Walled ? 0f : 40f);
            lo -= margin;
            hi += margin;
            bool domesUp = AnyDome();  // Fortress's dome (Dome.cs)
            foreach (var b in Bullets.Values)
            {
                if (b.AngVel != 0f && b.Kind != Defs.FumaStar)  // a Fuma Shuriken's AngVel only says which way it loops (Ninja.cs)
                    b.Vel = b.Vel.Rotated(b.AngVel * dt);
                if (b.Homing > 0f)
                    Steer(b, dt);
                if (wellsUp && b.Owner != 0 && b.Kind != Defs.GrenadeShot && b.Kind != Defs.BoulderShot && b.Kind != Defs.ClotShot && b.Kind != Defs.MedkitShot)
                    PullIntoWells(b, dt);
                float lumen = LumenSpeed(b, dt);  // 1 normally; falls to 0 as a Lumen Ward holds it still (LumenWard.cs)
                if (b.Kind == Defs.PauseShot)
                    b.Pos += b.Vel * (dt * PauseSpeedNow(b) * SmokeSpeed(b) * lumen);  // slows to a stop, then hangs (Variety.cs)
                else
                    b.Pos += b.Vel * (dt * SmokeSpeed(b) * lumen);
                if (!wind.IsZero && b.Kind != Defs.GrenadeShot && b.Kind != Defs.BoulderShot && b.Kind != Defs.ClotShot && b.Kind != Defs.MedkitShot)
                    b.Pos += wind * dt;
                b.Life -= dt * lumen;  // a held bullet doesn't run out of range
                if (domesUp && Defs.IsEnemyShot(b.Kind) && InDome(b.Pos))
                {
                    scratchIds.Add(b.Id);  // an enemy shot touching a dome is gone
                    continue;
                }
                if (b.Life <= 0f || b.Pos.X < lo.X || b.Pos.Y < lo.Y || b.Pos.X > hi.X || b.Pos.Y > hi.Y)
                {
                    scratchIds.Add(b.Id);
                    if (Walled && b.Life > 0f)
                        AddFx(FxKind.Ring, b.Pos.Clamp(lo, hi), Defs.BulletColor(b.Kind), 7f, 0.18f);  // a spark on the wall
                    if ((b.Burst > 0 || b.Rush) && b.Life <= 0f)
                        landed.Add(b);
                    if (b.ShadowKunai)
                        ShadowKunaiLanded(b, b.Pos);  // a shadow kunai that missed marks where it stopped (host only: clients don't know which it was)
                    if (b.DropsSpear)
                        DropSpear(b);  // a Bone Spear stays stuck where it stopped (BloodMage.cs)
                }
            }
            foreach (int id in scratchIds)
                Bullets.Remove(id);
            // Host only (Burst is never set on clients): landed mortar bombs and grenades explode.
            foreach (var b in landed)
            {
                if (b.Rush)
                {
                    PauseRush(b);  // Variety.cs
                    continue;
                }
                if (b.Kind == Defs.MedkitShot)
                {
                    LandMedkit(b);  // Medkit.cs
                    continue;
                }
                if (b.Kind == Defs.BoulderShot)
                {
                    LandBoulder(b);  // KitWeapons.cs
                    continue;
                }
                if (b.Kind == Defs.ClotShot)
                {
                    LandClot(b);  // BloodMage.cs
                    continue;
                }
                if (b.Kind == Defs.FlakShell)
                {
                    FlakAirburst(b);  // Weapons.cs
                    continue;
                }
                if (b.Ward)
                {
                    PlaceWard(b);  // Ninja.cs
                    continue;
                }
                if (b.Bomblet)
                {
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && e.Pos.DistanceTo(b.Pos) < b.Splash + e.Radius && !BlocksShot(e, b.Pos))
                            HurtEnemy(e, b.Dmg, b.Owner);
                    BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.FlakShell), b.Splash, 0.25f, Sfx.None);
                    PropBlast(b.Pos, b.Splash, b.Owner);
                    GateLockBlast(b.Pos, b.Splash, b.Owner);
                    continue;
                }
                if (b.Owner != 0)
                {
                    ExplodeGrenade(b);
                    continue;
                }
                float offset = Rand(0f, MathX.Tau);
                for (int i = 0; i < b.Burst; i++)
                {
                    V2 dir = V2.FromAngle(offset + MathX.Tau * i / b.Burst);
                    SpawnBullet(Defs.SmallShot, b.Pos, dir * 125f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0).Source = b.Source;  // a mortar bomb's ring (was 150)
                }
                BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.MineShot), 30f, 0.3f, Sfx.Hit);
            }
        }

        readonly List<Bullet> splashes = new List<Bullet>();

        /// Homing Rounds (2 turn per stack) cost 2.5% damage per point of turn: 5% a stack (was 10%).
        public const float HomingDamageCost = 0.025f;

        /// Weapons whose damage lands in an area (blasts, pools, the Implosion, the Vein Tether beam, the Leech
        /// Tendrils' arc, Storm Hammer's lightning). On them Homing Rounds don't bend shots: each stack makes the area 25% bigger instead, for
        /// the same damage cost.
        public static bool AreaWeapon(WeaponId w) => Defs.WeaponOf(w).AreaDesc != null;  // WeaponDef.AreaDesc
        public const float HomingAreaPerStack = 0.25f;
        public static float AreaMult(Player p) => AreaWeapon(p.Weapon.Id)
            ? (ConeWeapon(p.Weapon.Id) ? ConeNarrow(p) : 1f + HomingAreaPerStack * p.Stats.Homing / 2f) * p.Stats.ShotSizeMult : 1f;  // Magnum Rounds widen areas too  // Homing Rounds give 2 turn a stack

        /// Cone weapons (Chain Light, Leech Tendrils, the Repulsion Cannon): Homing Rounds condense the cone instead,
        /// 15% narrower and 10% stronger a stack, so a wide cone for crowds or a focused one is a choice.
        public const float HomingConeNarrow = 0.15f, HomingConeFocus = 0.1f;
        public static bool ConeWeapon(WeaponId w) => w == WeaponId.ChainLight || w == WeaponId.LeechBeam || w == WeaponId.LightBolt;
        public static float ConeNarrow(Player p) => 1f - HomingConeNarrow * p.Stats.Homing / 2f;  // how wide the cone is, against its usual width
        public static float ConeFocus(Player p) => 1f + HomingConeFocus * p.Stats.Homing / 2f;    // how hard it hits, against usual
        static float HomingCost(Player p) => 1f - HomingDamageCost * p.Stats.Homing;
        const float HomingRange = 260f, HomingCone = 0.7f;  // only enemies within 260 px and ~45 degrees of the shot's path

        /// Homing Rounds: bend toward the nearest enemy close ahead, so near misses connect but a shot
        /// fired the wrong way doesn't turn around to find someone. Clients steer the same way with the
        /// enemies they see, and the host sends the exact course 20 times a second.
        void Steer(Bullet b, float dt)
        {
            Enemy best = null;
            bool late = b.Kind == Defs.SanguineShot;  // a Sanguine Orb only homes at the last moment, but hard
            float bestD = late ? SanguineHomeRange * SanguineHomeRange : HomingRange * HomingRange, cone = late ? 0.3f : HomingCone;
            V2 dir = b.Vel.Normalized();
            foreach (var e in Enemies.Values)
            {
                if (e.Spawning || e.SpawnT > 0f || e.Id == b.LastHit || e.Def.Xp == 0)
                    continue;
                V2 to = e.Pos - b.Pos;
                float d = to.LengthSq;
                if (d < bestD && to.X * dir.X + to.Y * dir.Y > cone * (float)Math.Sqrt(d))
                {
                    best = e;
                    bestD = d;
                }
            }
            if (best == null)
                return;
            float want = (best.Pos - b.Pos).Angle, have = b.Vel.Angle;
            float diff = MathX.Fmod(want - have + MathX.Pi, MathX.Tau) - MathX.Pi;
            float turn = Math.Max(-b.Homing * dt, Math.Min(b.Homing * dt, diff));
            b.Vel = b.Vel.Rotated(turn);
        }

        readonly List<Enemy> wells = new List<Enemy>();

        /// The Singularity's gravity wells bend players' shots into themselves, harder the closer
        /// they pass. Clients bend them the same way; the host sends the exact course.
        void PullIntoWells(Bullet b, float dt)
        {
            foreach (var w in wells)
            {
                float d = b.Pos.DistanceTo(w.Pos);
                if (d > WellPullRange)
                    continue;
                float want = (w.Pos - b.Pos).Angle, have = b.Vel.Angle;
                float diff = MathX.Fmod(want - have + MathX.Pi, MathX.Tau) - MathX.Pi;
                float rate = (2.5f + 12f * (1f - d / WellPullRange)) * dt;  // hard: a shot near a corner is lost
                b.Vel = b.Vel.Rotated(Math.Max(-rate, Math.Min(rate, diff)));
                b.Pulled = true;
                return;  // one well at a time
            }
        }

        bool HealAllyHit(Bullet b)
        {
            foreach (var p in Players.Values)
            {
                float r = p.Def.BodyR + b.Radius;
                if (p.Id == b.Owner || p.Downed || (p.Hp >= p.MaxHp && b.WardAlly <= 0f) || b.Pos.DistanceSq(p.Pos) >= r * r)
                    continue;
                if (b.WardAlly > 0f)
                {
                    Ward(p, b.WardAlly);  // Mercy Cut's crescent (Lightbringer.cs)
                    LightMark(p, 2, 0f, false);
                    BroadcastSound(Sfx.LightWard);
                    return true;
                }
                if (Heal(p, b.HealAlly * (b.Dmg / 9f), PlayerOrNull(b.Owner)) > 0f && b.Kind == Defs.KnightWave)
                {
                    LightMark(p, 1, 0f, false);  // a Lightbringer crescent (the only healing sword wave)
                    BroadcastSound(Sfx.LightHeal);
                }
                TransfusionCost(b);  // Transfusion Darts cost their healer (BloodMage.cs)
                return true;
            }
            return false;
        }

        void Collide()
        {
            scratchIds.Clear();
            // splashes and splits are NOT cleared here: hits outside the bullet loop (beams, orbs, sent wisps, chains,
            // melee) queue Explosive / Collapse / Swarm Rounds into them too, and they're spent below with the rest.
            frags.Clear();
            reflects.Clear();
            ricochets.Clear();
            hops.Clear();
            ResolveSwings();  // Knight melee hits land first, through the same on-hit effects as bullets
            ResolveBeams();   // then Vein Tether's beam ticks (BloodMage.cs)
            ResolveGrasps();  // Leech Tendrils' holds
            ResolveImplosions();  // and Implosions
            FindColCover();       // the Colossus's plates and pillars (Colossus.cs)
            FindEclipseMoon();    // the Eclipse's moon (Eclipse.cs)
            foreach (var b in Bullets.Values)
            {
                if (b.Kind == Defs.GrenadeShot || b.Kind == Defs.BoulderShot || b.Kind == Defs.ClotShot || b.Kind == Defs.MedkitShot || b.Kind == Defs.FlakShell || b.Ward)
                    continue;  // grenades, boulders, Clot Cannon clots, medkits and flak shells fly over enemies and land or burst where you aimed
                if (LayoutOn && SolidStops(b))
                {
                    scratchIds.Add(b.Id);  // a wall or pillar stops every shot (LayoutRules.cs)
                    continue;
                }
                if (Props.Count > 0 && PropStops(b))
                {
                    scratchIds.Add(b.Id);  // a crate takes an enemy shot; a barrel any shot (Props.cs)
                    continue;
                }
                if (colCover != null && ColossusCoverStops(b))
                {
                    scratchIds.Add(b.Id);  // cover stops shots both ways
                    continue;
                }
                if (moonEclipse != null && MoonStops(b))
                {
                    scratchIds.Add(b.Id);  // the Eclipse's moon soaks players' shots
                    continue;
                }
                if (!Defs.IsEnemyShot(b.Kind))
                {
                    if (GateLockHit(b))
                    {
                        scratchIds.Add(b.Id);  // a Warden gate's gold lock takes it (Warden.cs)
                        continue;
                    }
                    if (b.Medicine ? DoseAlly(b) : (b.HealAlly > 0f || b.WardAlly > 0f) && HealAllyHit(b) || b.Kind == Defs.SanguineShot && CoatAlly(b))  // RefractThroughAlly: KitWeapons.cs; DoseAlly: Ninja.cs; CoatAlly: BloodMage.cs
                    {
                        scratchIds.Add(b.Id);
                        continue;
                    }
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Hp <= 0f || e.Id == b.LastHit || DevourUnder(e))
                            continue;  // (the Devourer underground: shots pass over it)
                        float r = e.Radius + b.Radius;
                        if (b.Pos.DistanceSq(e.Pos) < r * r)
                        {
                            if (BlocksShot(e, b.Pos))
                            {
                                DuelGuardBounce(e, b);  // the Duelist's guard sends it back (Duelist.cs)
                                WearShield(e, b.Dmg);  // the shield takes it, and wears
                                scratchIds.Add(b.Id);
                                break;
                            }
                            if (MirrorBounces(e, b))
                            {
                                scratchIds.Add(b.Id);  // a Mirrored elite sends it back (Variety.cs)
                                break;
                            }
                            if (b.FromWeapon && b.LastHit == 0)
                            {
                                Player shooter;
                                if (Players.TryGetValue(b.Owner, out shooter))
                                    shooter.Run.ShotsHit++;
                            }
                            if (b.Kind == Defs.SanguineShot)
                            {
                                StartBloodCoat(b, e);  // it circles in and its hit lands when it has covered the enemy (BloodMage.cs)
                                scratchIds.Add(b.Id);
                                break;
                            }
                            if (b.StunBack && e.Type == EnemyType.Boss)
                                StunWarden(e);  // its own stun shot, parried back (Warden.cs)
                            float hit = b.Dmg * NinjaHitMult(b.Owner, b.Kind, e) * PointBlankMult(b, e) * CrossfireMult(b, e);  // Kunai and ambush bonuses (Ninja.cs); the Scattergun up close and the Twin Rifle's crossfire (Weapons.cs)
                            HurtEnemy(e, hit, b.Owner);
                            if (b.Crossfire)
                                TwinRifleHit(b, e);  // Twin Rifle hits load the grenade tubes (TwinGrenades.cs)
                            foreach (var onHit in Classes.HitHooks)  // each class's KitDef.OnShotHit (BloodShotHit: Blood Mage shots drain, pool and drop orbs)
                                onHit(this, b, e, hit);
                            OnBulletHit(b, e);
                            b.LastHit = e.Id;
                            if (b.Frags > 0)
                                frags.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = b.Owner, LastHit = e.Id, Frags = b.Frags, FragKind = b.FragKind,
                                    FragDmg = b.FragDmg, FragSpeed = b.FragSpeed, FragLife = b.FragLife, FragSplash = b.FragSplash, FragSideways = b.FragSideways });
                            if (b.Splash > 0f)
                                splashes.Add(b);
                            if (b.Pierce > 0)
                                b.Pierce--;
                            else
                            {
                                scratchIds.Add(b.Id);
                                if (b.DropsSpear)
                                    DropSpear(b);  // a Bone Spear drops where it's spent (BloodMage.cs)
                                if (b.Bounces > 0 && !b.Bolts)  // a piercing shot flung bounce bolts instead
                                    ricochets.Add(b);
                                if (b.Hops > 0)
                                    hops.Add(b);
                            }
                            break;
                        }
                    }
                }
                else
                {
                    if (b.Kind == Defs.HollowShot)
                        continue;  // a Duelist decoy's shot: harmless, it passes through everything (Duelist.cs)
                    if (!Defs.Unblockable(b.Kind) && BlockedBySummons(b) || GuardStopsBullet(b))  // GuardStopsBullet: Stun.cs
                    {
                        scratchIds.Add(b.Id);
                        continue;
                    }
                    if (PhalanxStops(b))
                    {
                        scratchIds.Add(b.Id);  // a Bone Phalanx's shields: everything from the front stops, red aimed shots too (BonePhalanx.cs)
                        continue;
                    }
                    if (BridgeStops(b))
                    {
                        scratchIds.Add(b.Id);  // a Drawbridge's rail: everything stops there, red aimed shots too (Drawbridge.cs)
                        continue;
                    }
                    if (WallStops(b))
                    {
                        scratchIds.Add(b.Id);  // a Blood Wall: everything stops there, red aimed shots too (BloodWall.cs)
                        continue;
                    }
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed || p.SwallowT > 0f)
                            continue;  // (inside the Devourer, out of reach)
                        bool blockable = !Defs.Unblockable(b.Kind);
                        if (p.ParryActive && InParryArc(p, b.Pos, b.Radius))
                        {
                            // The parry stops anything in front of it, even shots barriers can't.
                            scratchIds.Add(b.Id);
                            if (ParryHit(p, b.Dmg, b.Pos))  // perfect parry or Mirror Guard: sent back
                                reflects.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = p.Id, Dmg = p.Stats.ReflectMult, StunBack = b.Kind == Defs.StunShot });
                            break;
                        }
                        if (p.WallUp && WallCovers(p, b.Pos, b.Radius, out int plate))
                        {
                            scratchIds.Add(b.Id);  // Shield Wall: stops everything from the front, red aimed shots included
                            if (p.WallReflect)
                                reflects.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = p.Id });
                            WearWall(p, plate, b.Dmg, b.Pos);  // every shot wears a plate, not only a boss's
                            break;
                        }
                        if (p.StandOn && StandBlocks(p, b))
                        {
                            scratchIds.Add(b.Id);  // Last Stand: stops everything from the front, red aimed shots too, and you take a tenth of it (LastStand.cs)
                            StandHit(p, b);
                            break;
                        }
                        if (p.ShellOn && Enters(b, p.Pos, ShellRadius))
                        {
                            scratchIds.Add(b.Id);  // Blood Shell: stops everything, red aimed shots too, and bills its owner (BloodShell.cs)
                            ShellHit(p, b);
                            break;
                        }
                        if (blockable && p.AegisT > 0f && Enters(b, p.Pos, AegisRadius))
                        {
                            reflects.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = p.Id, Dmg = p.AegisReflectMult });  // Aeroaegis
                            scratchIds.Add(b.Id);
                            p.AegisHp -= BlockerWear(b);
                            if (p.AegisHp <= 0f)
                                p.AegisT = 0f;
                            break;
                        }
                        if (blockable && p.BarrierT > 0f && Enters(b, p.Pos, p.BarrierRadius))
                        {
                            scratchIds.Add(b.Id);
                            if (p.BarrierReflect)
                                reflects.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = p.Id });
                            p.BarrierHp -= BlockerWear(b);
                            if (p.BarrierHp <= 0f)
                            {
                                p.BarrierT = 0f;  // shattered
                                BroadcastFx(FxKind.Boom, p.Pos, new Rgba(0.5f, 0.8f, 1f), p.BarrierRadius, 0.4f, Sfx.Hit);
                            }
                            break;
                        }
                        if (SteelGraze(p, b))
                        {
                            scratchIds.Add(b.Id);  // Caught Steel: it skimmed past and hangs round them now (CaughtSteel.cs)
                            break;
                        }
                        float r = p.Def.HitR + b.Radius;
                        if (b.Pos.DistanceSq(p.Pos) < r * r)
                        {
                            // Invulnerable players (dash, recent hit) let bullets pass through, unless a Lightning Rod grounds them:
                            // then it's used up and calls its bolt whether it hurt or not (LightningRod.cs).
                            bool grounded = p.RodT > 0f;
                            if (grounded && p.InvulnT > 0f)
                            {
                                scratchIds.Add(b.Id);
                                RodShot(p, b, 0f);
                            }
                            else if (p.InvulnT <= 0f)
                            {
                                if (JuggOn(p))
                                    JuggShot(p, b);  // the suit's armour takes it and throws flak back (Juggernaut.cs)
                                else
                                    Damage(p, b.Dmg, ShotSource(b));
                                scratchIds.Add(b.Id);
                                if (grounded)
                                    RodShot(p, b, p.DevGod ? b.Dmg * EnemyDamageScale(Math.Max(1, Wave)) * (1f - RodCut) : lastTaken);  // (the preview's god mode takes nothing: show what it would have)
                                if (b.Kind == Defs.StunShot && !p.Downed)
                                {
                                    p.StunT = StunTime;  // the Warden's stun shot: stuck for 1 s (Warden.cs)
                                    BroadcastFx(FxKind.Spark, p.Pos, Defs.BulletColor(Defs.StunShot), 30f, 0.35f, Sfx.SpellZap);
                                }
                            }
                            break;
                        }
                    }
                }
            }
            foreach (int id in scratchIds)
            {
                Bullet b;
                if (!Bullets.TryGetValue(id, out b))
                    continue;
                RemovedFx(b);
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }

            foreach (var rb in reflects)
            {
                Player owner;
                if (!Players.TryGetValue(rb.Owner, out owner))
                    continue;
                var warden = rb.StunBack ? TheWarden() : null;
                V2 back = warden != null ? (warden.Pos - rb.Pos).Normalized() : -rb.Vel.Normalized();  // a parried stun shot goes straight back at the Warden
                var shot = SpawnBullet(Defs.ArcaneShot, rb.Pos, back * 520f, 0f, warden != null ? 2.5f : 1f, 12f * owner.Stats.DmgMult * (rb.Dmg > 0f ? rb.Dmg : 1f), rb.Owner);
                shot.StunBack = rb.StunBack;
            }

            // Ricochet: the bullet carries on toward the nearest other enemy.
            foreach (var rb in ricochets)
            {
                Enemy next = BounceTarget(rb.Pos, rb.LastHit);
                if (next == null)
                    continue;
                float speed = Math.Max(400f, rb.Vel.Length);
                V2 dir = (next.Pos - rb.Pos).Normalized();
                // SpawnBullet applies Velocity Rounds again, so undo it on the carried-over speed.
                Player owner;
                if (Players.TryGetValue(rb.Owner, out owner))
                    speed /= owner.Stats.ShotSpeedMult;
                var nb = SpawnBullet(rb.Kind, rb.Pos, dir * speed, 0f, 0.8f, rb.Dmg * RicochetMult, rb.Owner);
                nb.LastHit = rb.LastHit;
                nb.Pierce = 0;
                nb.Bounces = rb.Bounces - 1;
                nb.Splash = rb.Splash;
                nb.Chain = rb.Chain;
                nb.Shock = rb.Shock;
                nb.SlowAmt = rb.SlowAmt;
            }

            foreach (var hb in hops)
                FireflyHop(hb);  // KitWeapons.cs

            // Fragments from Frost Lance, Railgun, Flak Cannon and Fireball hits.
            foreach (var f in frags)
            {
                float baseAng = f.Vel.Angle, offset = Rand(0f, MathX.Tau);
                for (int i = 0; i < f.Frags; i++)
                {
                    float a = f.FragSideways
                        ? baseAng + (i % 2 == 0 ? 1f : -1f) * (MathX.Pi / 2f - 0.25f * (i / 2))  // pairs out to either side, fanning forward
                        : offset + MathX.Tau * i / f.Frags;
                    V2 d = V2.FromAngle(a);
                    var nb = SpawnBullet(f.FragKind, f.Pos + d * 6f, d * f.FragSpeed, 0f, f.FragLife, f.FragDmg, f.Owner);
                    nb.LastHit = f.LastHit;
                    nb.Splash = f.FragSplash;
                    if (f.FragKind == Defs.GrenadeShot)  // lobbed bomblets: they fly over enemies and pop where they land
                    {
                        nb.Bomblet = true;
                        nb.Burst = 1;
                    }
                }
            }

            // Bounce bolts (Ricochet on hits that can't bounce): from the enemy hit to the nearest other one.
            foreach (var bb in bolts)
            {
                Enemy next = BounceTarget(bb.Pos, bb.LastHit);
                if (next == null)
                    continue;
                V2 dir = (next.Pos - bb.Pos).Normalized();
                var nb = SpawnBullet(Defs.BoltShot, bb.Pos, dir * 650f, 0f, 0.8f, bb.Dmg, bb.Owner);
                nb.LastHit = bb.LastHit;
                nb.Pierce = 0;
                nb.Bounces = bb.Bounces;  // more stacks: it bounces on as an ordinary ricochet
            }
            bolts.Clear();

            foreach (var sp in splits)
            {
                Enemy hit;
                if (Enemies.TryGetValue(sp.LastHit, out hit) && hit.Hp > 0f && BounceTarget(sp.Pos, sp.LastHit, SwarmAloneReach) == null)
                {
                    HurtEnemy(hit, sp.Dmg * sp.Pierce * SwarmAloneShare, sp.Owner);  // alone (a boss): the splinters bite the enemy hit
                    continue;
                }
                float offset = Rand(0f, MathX.Tau);
                for (int i = 0; i < sp.Pierce; i++)
                {
                    V2 d = V2.FromAngle(offset + MathX.Tau * i / sp.Pierce);
                    SpawnBullet(Defs.SwarmShot, sp.Pos + d * 6f, d * 520f, 0f, 0.35f, sp.Dmg, sp.Owner).LastHit = sp.LastHit;
                }
            }

            // Flak shells (and Collapse Rounds) hurt everything near the impact, at 60% damage.
            foreach (var b in splashes)
            {
                foreach (var e in Enemies.Values)
                {
                    if (e.Id == b.LastHit || e.SpawnT > 0f || e.Pos.DistanceTo(b.Pos) > b.Splash + e.Radius || BlocksShot(e, b.Pos))
                        continue;
                    HurtEnemy(e, b.Dmg * 0.6f, b.Owner);
                }
                if (b.Kind == Defs.FlakShell || !SplashFxNearby(b))
                    BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(b.Kind), b.Splash, 0.3f, b.Kind == Defs.FlakShell ? Sfx.Explode : Sfx.None);
                PropBlast(b.Pos, b.Splash, b.Owner);
                if (b.HealBurst > 0f)
                    CharmHeal(b);  // Ofuda (Ninja.cs)
            }
            splits.Clear();
            splashes.Clear();

            var dead = scratchDead;
            dead.Clear();
            foreach (var e in Enemies.Values)
                if (e.Hp <= 0f)
                    dead.Add(e);
            foreach (var e in dead)
            {
                DecoyShatter(e);  // a Duelist's masquerade decoy shot down bursts into a ring (Duelist.cs)
                NonShooterDeath(e);  // a Fuse Runner killed during its fuse, a Tripwire Pod (NonShooters.cs)
                Recorder?.EnemyKilled(e);
                SeededKill(e);  // a Sprite Staff seed in it frees a sprite (KitWeapons.cs)
                foreach (var onKill in Classes.KillHooks)  // each class's KitDef.OnKill
                    onKill(this, e);
                bool bossReward = OnEnemyKilled(e);
                if (bossReward)
                    BossRecord(e);  // everyone's profile (RunSaves.cs)
                KillEffects(e);
                var killer = PlayerOrNull(e.LastHitBy);
                if (killer != null && e.Def.Xp > 0)
                {
                    killer.Run.Kills++;
                    if (e.Elite)
                        killer.Run.EliteKills++;
                    if (e.Def.IsBoss)
                        killer.Run.BossKills++;
                }
                if (killer != null && e.Def.Xp > 0 && OnPathOf(PathId.BloodMoon))
                    Heal(killer, Paths.BloodMoonHeal(Wave), null, "Blood Moon");  // Blood Moon: the hunt keeps you alive
                else if (killer != null && e.Def.Xp > 0 && Has(RunMod.Bloodlust))
                    Heal(killer, Paths.BloodMoonHeal(Wave), null, "Bloodlust");
                Score += (int)Math.Round(e.Score * (e.Elite ? 3 : 1) * PathBonus);
                GrantXp(KillXp(e) * (e.Elite ? 3 : 1) * PathBonus * ZombieXpScale(e));  // (Zombie Infection splits it over the horde)
                MaybeRise(e, killer);  // Zombies.cs
                if (OnPathOf(PathId.Bazaar) || Has(RunMod.GoldRush))
                    GrantGold(e);  // Shop.cs
                DropOrbs(e, bossReward);
                DeathFx(e, e.Pos);
                Enemies.Remove(e.Id);
            }
        }

        /// All damage to enemies goes through here so marks and other modifiers apply.
        float tickDt = 1f / 60f;

        /// Blockers hold for their full duration against regular waves; only bosses' bullets wear them down.
        static float BlockerWear(Bullet b) => b.FromBoss ? b.Dmg : 0f;

        /// Did this bullet just come in from outside the circle? Bullets that started inside
        /// (fired from within a barrier or wall) pass through, so a blocker can't smother a shooter.
        bool Enters(Bullet b, V2 center, float radius)
        {
            float r = radius + b.Radius;
            if (b.Pos.DistanceSq(center) >= r * r)
                return false;
            V2 before = b.Pos - b.Vel * tickDt;
            return before.DistanceSq(center) >= r * r;
        }

        /// A boss that takes no damage right now (changing stage, the Queen up in a dive, the Lich guarded...);
        /// the Singularity's split is checked on its own (SingImmune), since its shards pass hits to the core.
        bool BossImmune(Enemy e) =>
            WardenImmune(e) || QueenImmune(e) || ReaperImmune(e) || ColossusImmune(e) || LichImmune(e) || DuelImmune(e) || HeraldImmune(e) || DevourImmune(e) || EclipseImmune(e);

        void HurtEnemy(Enemy e, float dmg, int attacker)
        {
            if (SoulBound(e))
                return;  // immune until its twin is brought down to match
            if (ShardPassesHit(e, dmg, attacker))
                return;  // a Singularity shard: the core takes it (Singularity.cs)
            if (BossImmune(e) || (SingImmune(e) && !shardPass))
                return;  // a boss changing stage, the Queen up in a dive, the Singularity split, the Lich guarded
            if (ColossusDazed(e))
                dmg *= ColDazeMult;  // it charged into a plate (Colossus.cs)
            if (!DualReaperHit(e, ref dmg))
                return;  // the Dual Reaper's shield took it (Reapers.cs); otherwise its armor, or its open window
            if (DuelOpen(e))
                dmg *= DuelOpenMult;  // a parried salute left the Duelist open (Duelist.cs)
            if (EclipseExposed(e))
                dmg *= TotalityMult;  // the Eclipse's Totality (Eclipse.cs)
            if (ReaverIsOpen(e))
                dmg *= ReaverOpenMult;  // just swiped (Variety.cs)
            if (HookIsOpen(e))
                dmg *= HookOpenMult;  // a Hookman that just missed (NonShooters.cs)
            if (RiotShieldBroken(e))
                dmg *= RiotBrokenMult;  // its shield's gone (WardenFaction.cs)
            if (WardenExposed(e))
                dmg *= ExposedMult;  // blinded by its own searchlight (Warden.cs)
            if (WardenShieldBroken(e))
                dmg *= RiotBrokenMult;  // its riot shield's broken this riot turn (Warden.cs)
            if (attacker > 0 && Has(RunMod.GlassCannon))
                dmg *= Modifiers.GlassCannonDealt;
            if (casting)
            {
                dmg *= castPower;
                castHits++;
            }
            if (e.MarkT > 0f)
                dmg *= e.MarkMult;
            if (e.IceT > 0f && e.FrozenT > 0f)
                dmg *= e.IceMult;  // the mastered Blood Freeze (BloodFreeze.cs)
            if (attacker > 0)
                dmg *= LitMult(e);  // in a Searchlight (Stun.cs)
            if (Hungry(e))
            {
                e.Hp = Math.Min(e.MaxHp, e.Hp + dmg * 0.5f);  // the Devourer feeds on your shots
                return;
            }
            if (!DevourHit(e, ref dmg, attacker))
                return;  // into the Devourer's Maw: it fed on that (Devourer.cs)
            dmg = SoakBubble(e, dmg);  // a Shielded elite's or a Warder's bubble takes it first (Variety.cs)
            if (dmg <= 0f)
            {
                e.Flash = 0.06f;
                return;
            }
            Player p;
            if (Players.TryGetValue(attacker, out p))
            {
                var s = p.Stats;
                if (IsHunted(p))
                    dmg *= Modifiers.HuntedDamage;  // Hunted: the quarry hits twice as hard (Modifiers.cs)
                dmg *= BloodDamageMult(p);   // Blood Price
                dmg *= TitheOutput(p);       // Vein Tithe: 40% less while you hold the arena (VeinTithe.cs)
                if (s.ExecuteBonus > 0f && e.Hp < e.MaxHp * ExecuteBelow)
                    dmg *= 1f + s.ExecuteBonus;
                if (s.SlowedBonus > 0f && e.SlowT > 0f && e.SlowMult < 1f)  // Graviton Rounds: slowed enemies take more
                    dmg *= 1f + s.SlowedBonus;
                if (s.HiveDrones > 0 && e.Def.IsBoss)
                    DroneOnBossHit(p, e);  // Hive Drones: a sting every few seconds while you hit a boss (QueenLoot.cs)
                if (s.BroodChance > 0f && e.Def.IsBoss && p.BroodCd <= 0f)  // Brood Mother: hitting a boss brings turrets
                {
                    p.BroodCd = BroodBossEvery;
                    p.BroodDue = 1;  // spawned in TickPlayers (this can run inside the minion loop); one whatever the stacks: turrets don't scale with your weapon
                }
                if (s.ReapBossBonus > 0f && e.Def.IsBoss && e.Hp < e.MaxHp * FinalReapBossBelow)
                    dmg *= 1f + s.ReapBossBonus;  // Final Reap: a boss near its end (ReaperLoot.cs)
                if (s.CritChance > 0f && rng.NextDouble() < s.CritChance)
                    dmg *= 2f;
                if (s.Lifesteal > 0f && !p.Downed)
                {
                    float heal = Math.Min(s.Lifesteal, p.LifestealPool);
                    p.LifestealPool -= heal;
                    Recorder?.HealReceived(p, Math.Min(heal, p.MaxHp - p.Hp), "lifesteal");
                    p.Hp = Math.Min(p.MaxHp, p.Hp + heal);
                }
                if (s.SlowOnHit > 0f)
                {
                    e.SlowT = Math.Max(e.SlowT, 2f);
                    e.SlowMult = Math.Min(e.SlowMult, 1f - s.SlowOnHit);
                }
                e.LastHitBy = attacker;
                p.Run.DamageDealt += Math.Min(dmg, Math.Max(0f, e.Hp));
                if (e.Def.IsBoss)
                    p.Run.BossDamage += Math.Min(dmg, Math.Max(0f, e.Hp));
                p.Run.BiggestHit = Math.Max(p.Run.BiggestHit, dmg);
                BloodLifesteal(p, e, Math.Min(dmg, Math.Max(0f, e.Hp)));
            }
            Recorder?.DamageDealt(attacker, e, Math.Min(dmg, Math.Max(0f, e.Hp)));
            if (WardenArmorTakes(e, ref dmg))
            {
                e.Flash = 0.06f;
                return;  // the Warden's last-stand armor took it all (Warden.cs)
            }
            e.Hp -= dmg;
            e.Flash = 0.06f;
            if (p != null && p.Stats.ReapBelow > 0f)
                FinalReap(p, e);  // a phantom scythe finishes it off (ReaperLoot.cs)
        }

        // Explosive and Collapse Rounds blow up on every hit: a fast weapon on a boss would stack a dozen blasts a second
        // on one spot (a white blob, and an effect message each). The damage always lands; the blast is only shown
        // if no blast of the same kind has been shown that near in the last SplashFxGap ticks.
        public const int SplashFxGap = 8;              // ticks (about 0.13 s)
        public const float SplashFxNear = 0.6f;        // of the splash's radius
        struct SplashSeen { public V2 Pos; public int Tick; public byte Kind; }
        readonly List<SplashSeen> splashSeen = new List<SplashSeen>();

        bool SplashFxNearby(Bullet b)
        {
            for (int i = splashSeen.Count - 1; i >= 0; i--)
            {
                var r = splashSeen[i];
                if (tick - r.Tick > SplashFxGap || tick < r.Tick)
                    splashSeen.RemoveAt(i);
                else if (r.Kind == b.Kind && r.Pos.DistanceTo(b.Pos) < b.Splash * SplashFxNear)
                    return true;
            }
            splashSeen.Add(new SplashSeen { Pos = b.Pos, Tick = tick, Kind = b.Kind });
            return false;
        }

        // Swarm Rounds splits and Aegis reflections, queued while the bullet loop runs.
        readonly List<Bullet> splits = new List<Bullet>();
        readonly List<Bullet> frags = new List<Bullet>();
        readonly List<Bullet> reflects = new List<Bullet>();
        readonly List<Bullet> ricochets = new List<Bullet>();
        readonly List<Bullet> hops = new List<Bullet>();  // Sprite Staff fireflies that landed and may hop on (KitWeapons.cs)

        /// Legendary on-hit effects that spawn things (run after the collision loop).
        void OnBulletHit(Bullet b, Enemy e)
        {
            if (b.Chain > 0)
                ChainLightning(e, b.Dmg * 0.6f, b.Chain, b.Owner, ChainReach * (b.Owner != 0 && Players.ContainsKey(b.Owner) ? AreaMult(Players[b.Owner]) : 1f));
            StunBoltHit(b, e);  // Stun.cs
            BoneDartHit(b, e);  // the Bone Guard's darts cage what they stick in (KitWeapons.cs)
            WolfDartHit(b, e);  // the Wolf Whistle's darts mark the pack's prey (KitWeapons.cs)
            SuppressHit(b, e);  // Riot Repeater rounds slow its shooting (Weapons.cs)
            PoisonHit(b, e);    // Medicine Darts poison it (Ninja.cs)
            ShadowKunaiLanded(b, e.Pos);  // a shadow kunai marks where it hit (Ninja.cs)
            StarStuck(b, e);    // a Shuriken star sticks in it (Ninja.cs)
            SeedHit(b, e);      // Sprite Staff seeds and fireflies stick (KitWeapons.cs)
            if (b.Shock > 0)
                Electrify(e, b.Dmg, b.Shock, b.Owner);  // the Storm Hammer (KitWeapons.cs)
            if (b.Kind == Defs.HammerShot)
                HammerStrikeFx(e.Pos);  // a bolt from the sky where it landed (StormHammer.cs)
            if (b.Ignite > 0f)  // the Flamethrower and the Fireball: burning, the strongest fire on it counts
            {
                if (!b.Pierced && b.Kind == Defs.FlameShot)  // a flame does full damage to the first enemy, a third to the ones behind
                {
                    b.Pierced = true;
                    b.Dmg *= FlamePierced;
                }
                e.BurnT = Math.Max(e.BurnT, FlameBurnTime);
                e.BurnDps = Math.Max(e.BurnDps, b.Ignite);
                e.BurnBy = b.Owner;
            }
            if (b.SlowAmt > 0f && !e.Def.IsBoss)
            {
                e.SlowT = Math.Max(e.SlowT, 1.5f);
                e.SlowMult = Math.Min(e.SlowMult, 1f - b.SlowAmt);
            }
            Player p;
            if (!Players.TryGetValue(b.Owner, out p))
                return;
            if (p.Stats.SplitShots > 0 && b.Kind != Defs.SwarmShot)
                splits.Add(new Bullet { Pos = b.Pos, Owner = b.Owner, Dmg = b.Dmg * 0.4f, Pierce = p.Stats.SplitShots, LastHit = e.Id });
            if (p.Stats.ImpactSplash > 0f && b.Kind != Defs.SwarmShot)
            {
                splashes.Add(new Bullet { Pos = b.Pos, Owner = b.Owner, Kind = Defs.NovaShot, Splash = p.Stats.ImpactSplash, Dmg = b.Dmg * 0.67f, LastHit = e.Id });
                HurtEnemy(e, b.Dmg * 0.67f * 0.6f * CollapseCore, b.Owner);  // the enemy shot is in the implosion too, at half its damage
            }
            if (!Moddable(b.Kind))
                return;

            // Weapon mods.
            var s = p.Stats;
            if (s.ExplosiveRadius > 0f)
                splashes.Add(new Bullet { Pos = b.Pos, Owner = b.Owner, Kind = Defs.FireShot, Splash = s.ExplosiveRadius, Dmg = b.Dmg * 0.5f, LastHit = e.Id });
            if (s.BurnFrac > 0f)
            {
                e.BurnPool += b.Dmg * s.BurnFrac;  // every hit adds to the fire, so fast and slow weapons gain alike
                e.PoolT = IncendiaryTime;
                e.BurnBy = b.Owner;
            }
            if (s.CryoSlow > 0f && !Heavy(e))
            {
                e.SlowT = Math.Max(e.SlowT, 2f);
                e.SlowMult = Math.Min(e.SlowMult, 1f - s.CryoSlow);
                if (!e.Def.IsBoss && rng.NextDouble() < s.FreezeChance)
                {
                    e.SlowT = Math.Max(e.SlowT, 1f);
                    e.SlowMult = 0.1f;  // frozen solid
                    BroadcastFx(FxKind.Spark, e.Pos, Defs.BulletColor(Defs.FrostShot), e.Radius + 12f, 0.35f, Sfx.None);
                }
            }
            if (s.ShockChance > 0f && rng.NextDouble() < s.ShockChance)
                ChainLightning(e, b.Dmg * 0.6f, 2, b.Owner);
            if (s.Ricochet > 0 && !CanBounce(b))
            {
                // Ricochet on a hit that can't bounce (a swing, beam, cone, burst, sent wisp, or a shot that pierces
                // everything): the damage is stored, and a few times a second a bounce bolt carries 75% of it on.
                p.BoltStore += b.Dmg;
                if (p.BoltCd <= 0f)
                {
                    bolts.Add(new Bullet { Pos = e.Pos, Owner = b.Owner, Dmg = p.BoltStore * RicochetMult, LastHit = e.Id, Bounces = s.Ricochet - 1 });
                    p.BoltStore = 0f;
                    p.BoltCd = BoltEvery;
                }
            }
        }

        public const float SwarmAloneReach = 180f, SwarmAloneShare = 0.17f;  // Swarm Rounds: with no other enemy this near, the splinters bite the enemy hit for this share of their damage
        public const float CollapseCore = 0.5f;                        // Collapse Rounds: the enemy shot takes this share of the implosion (others take it all)
        public const float ExecuteBelow = 0.5f;                       // Executioner: double damage below this health (was 35%)
        public const float BroodTurretTime = 6f, BroodBossEvery = 8f;  // Brood Mother: a turret's life (was 4 s); one turret this often while you hit a boss

        public const float RicochetMult = 0.4f, RicochetReach = 300f, BoltEvery = 0.25f;  // a bounce's share of the hit (was 75% before the Oct 2026 balance pass); how far it looks; bounce bolts at most this often
        readonly List<Bullet> bolts = new List<Bullet>();

        /// A flying bullet that bounces on its own when it's spent (Ricochet's usual way). Stand-in bullets for
        /// swings, beams, cones and bursts can't, and nor can shots that pierce everything, grenades or boulders.
        bool CanBounce(Bullet b)
        {
            Bullet live;
            return b.Id != 0 && Bullets.TryGetValue(b.Id, out live) && live == b && b.Pierce < 50 && !b.Bolts && b.Burst == 0 && !b.Ward
                && b.Kind != Defs.BoulderShot && b.Kind != Defs.ClotShot && b.Kind != Defs.FumaStar;
        }

        /// The nearest enemy a bounce can go to from `pos`, other than the one it just hit.
        Enemy BounceTarget(V2 pos, int lastHit, float reach = RicochetReach)
        {
            Enemy next = null;
            float bestD = reach * reach;
            foreach (var e in Enemies.Values)
            {
                if (e.Id == lastHit || e.Hp <= 0f || e.SpawnT > 0f || e.Def.Xp == 0)
                    continue;
                float d = e.Pos.DistanceSq(pos);
                if (d < bestD)
                {
                    next = e;
                    bestD = d;
                }
            }
            return next;
        }

        public const float IncendiaryTime = 3f;  // Incendiary Rounds' pool burns off evenly over this long after the last hit

        /// Fire, credited to whoever lit it: the Flamethrower's burn, and Incendiary Rounds' pool.
        void TickBurn(Enemy e, float dt)
        {
            bool safe = Hungry(e) || e.SpawnT > 0f || SoulBound(e);
            if (e.PoolT > 0f)
            {
                float burn = e.BurnPool * Math.Min(1f, dt / e.PoolT);
                e.PoolT -= dt;
                e.BurnPool -= burn;
                if (e.PoolT <= 0f)
                    e.BurnPool = 0f;
                if (!safe)
                    DotDamage(e, burn, e.BurnBy);
            }
            if (e.BurnT <= 0f)
                return;
            e.BurnT -= dt;
            if (e.BurnT <= 0f)
                e.BurnDps = 0f;
            if (!safe)
                DotDamage(e, e.BurnDps * dt, e.BurnBy);
        }

        /// Damage over time (burning, bleeding), credited to whoever caused it.
        void DotDamage(Enemy e, float dmg, int by)
        {
            // The same immunities as a hit: a boss changing stage or guarded takes none, and bubbles soak it first.
            if (SoulBound(e) || Hungry(e) || BossImmune(e) || SingImmune(e))
                return;
            dmg = SoakBubble(e, dmg);
            if (dmg <= 0f)
                return;
            Player p;
            if (Players.TryGetValue(by, out p))
            {
                Recorder?.DamageDealt(p.Id, e, Math.Min(dmg, Math.Max(0f, e.Hp)));
                p.Run.DamageDealt += Math.Min(dmg, Math.Max(0f, e.Hp));
                DpsDot(p.Id, Math.Min(dmg, Math.Max(0f, e.Hp)));  // the DPS meter's over-time share (DpsTools.cs)
                if (e.Def.IsBoss)
                    p.Run.BossDamage += Math.Min(dmg, Math.Max(0f, e.Hp));
                e.LastHitBy = by;
            }
            e.Hp -= dmg;
        }

        /// Legendary on-kill effects for whoever landed the last hit.
        void KillEffects(Enemy e)
        {
            Player p;
            if (!Players.TryGetValue(e.LastHitBy, out p))
                return;
            var s = p.Stats;
            if (s.KillHeal > 0f && !p.Downed)
            {
                Recorder?.HealReceived(p, Math.Min(s.KillHeal, p.MaxHp - p.Hp), "heal on kill");
                p.Hp = Math.Min(p.MaxHp, p.Hp + s.KillHeal);
            }
            DroneOnKill(p, e);  // Hive Drones (QueenLoot.cs)
            for (float c = s.BroodChance; c > 0f; c -= 1f)  // over 100% (three stacks): a sure turret, and a chance at another
                if (rng.NextDouble() < c)
                    SpawnMinion(MinionKind.Turret, p, e.Pos, BroodTurretTime);
        }

        void DropOrbs(Enemy e, bool bossReward)
        {
            if (e.Def.Xp == 0)
                return;  // decoys and phylacteries
            if (e.Elite)
            {
                SpawnOrb(e.Pos, rng.NextDouble() < 0.25);  // elites always drop something
                return;
            }
            if (bossReward)
            {
                // Bosses always drop a large orb and a few small ones around it.
                SpawnOrb(e.Pos, true);
                for (int i = 0; i < 4; i++)
                    SpawnOrb(e.Pos + V2.FromAngle(MathX.Tau * i / 4f) * 40f, false);
                return;
            }
            if (Has(RunMod.Famine) || Has(RunMod.Bloodlust) || OnPathOf(PathId.BloodMoon))
                return;  // run modifier / Blood Moon: no health orbs (bosses still drop theirs)
            double roll = rng.NextDouble();
            if (roll < BigOrbChance)
                SpawnOrb(e.Pos, true);
            else if (roll < BigOrbChance + D(DiffKnob.OrbChance))
                SpawnOrb(e.Pos, false);
        }

        void SpawnOrb(V2 pos, bool big, bool tiny = false)
        {
            var o = new Orb { Id = nextOrbId++, Big = big, Tiny = tiny, Pos = ClampToArena(pos, 12f), Life = OrbLifetime };
            o.Disp = o.Pos;
            Orbs[o.Id] = o;
        }

        /// Orbs drift toward the nearest hurt player in range and heal whoever touches them.
        /// Players at full health leave them for their ally.
        void TickOrbs(float dt)
        {
            var gone = scratchOrbsGone;
            gone.Clear();
            foreach (var o in Orbs.Values)
            {
                o.Life -= dt;
                if (o.Life <= 0f)
                {
                    gone.Add(o.Id);
                    continue;
                }
                Player nearest = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.Hp < p.MaxHp && p.Pos.DistanceTo(o.Pos) < OrbMagnetRange &&
                        (nearest == null || p.Pos.DistanceSq(o.Pos) < nearest.Pos.DistanceSq(o.Pos)))
                        nearest = p;
                if (nearest == null && o.Med)
                    foreach (var p in Players.Values)  // a medkit's orb also picks a downed ally back up (Medkit.cs)
                        if (p.Downed && p.Pos.DistanceTo(o.Pos) < p.Def.BodyR + o.Radius + 6f)
                        {
                            RevivePlayer(p, 0.2f);
                            BroadcastFx(FxKind.Ring, o.Pos, new Rgba(0.4f, 1f, 0.5f), 50f, 0.35f, Sfx.PickupBig);
                            gone.Add(o.Id);
                            break;
                        }
                if (nearest == null)
                    continue;
                V2 to = nearest.Pos - o.Pos;
                o.Pos += to.LimitLength(300f * dt);
                if (to.Length < nearest.Def.BodyR + o.Radius)
                {
                    Recorder?.Orb(nearest, Math.Min(o.Heal, nearest.MaxHp - nearest.Hp));
                    nearest.Hp = Math.Min(nearest.MaxHp, nearest.Hp + o.Heal);
                    BroadcastFx(FxKind.Ring, o.Pos, new Rgba(0.4f, 1f, 0.5f), o.Big ? 50f : 25f, 0.35f, o.Big ? Sfx.PickupBig : Sfx.Pickup);
                    gone.Add(o.Id);
                }
            }
            foreach (int id in gone)
                Orbs.Remove(id);
        }

        void RemovedFx(Bullet b)
        {
            AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(b.Kind), 10f, 0.15f);
            if (b.Kind == Defs.TreantSeed)
                Sounds.Add(new SoundCue { Sfx = Sfx.ShotSeedHit, Ally = b.Owner != MyId });
            else if (!Defs.IsEnemyShot(b.Kind))
                Sounds.Add(Sfx.Hit);
        }

        void DeathFx(Enemy e, V2 pos)
        {
            AddFx(FxKind.Boom, pos, e.Def.Color, e.Radius * 2.2f, 0.4f);
            Sounds.Add(e.Def.IsBoss ? Sfx.BigExplode : Sfx.Explode);
        }

        void ClearEnemyBullets()
        {
            Patches.Clear();  // a wave was cleared: the path's fire and pools go too (Paths.cs)
            scratchIds.Clear();
            int pops = 0;
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind))
                {
                    scratchIds.Add(b.Id);
                    if (pops++ < 160)  // each one pops as it goes (on every game; nothing sent)
                        AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(b.Kind), b.Radius + 6f, 0.3f);
                }
            foreach (int id in scratchIds)
                Bullets.Remove(id);
            // Host: shots fired earlier this tick haven't gone out yet, and their Msg.Spawn is sent at the end of the tick,
            // after the clear message. Sent, they'd turn up on the clients after the clear and never go (ghost bullets),
            // so they're dropped from the queue too.
            pendingSpawns.RemoveAll(b => !Bullets.ContainsKey(b.Id));
        }
    }
}
