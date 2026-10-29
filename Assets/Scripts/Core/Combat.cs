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
            if (p.DevGod)
                return;
            if (p.TauntT > 0f)
                amount *= Math.Max(0.2f, 1f - RoarReduction - p.Stats.RoarExtraReduction);
            foreach (var q in Players.Values)
                if (q.LinkT > 0f && q.LinkTo == p.Id && q != p)
                    amount *= 0.5f;  // a healer's Soul Link halves the damage
            amount *= p.Stats.DamageTaken;
            if (Has(RunMod.GlassCannon))
                amount *= Modifiers.GlassCannonTaken;
            float cut = 0f;
            foreach (var z in Zones.Values)
                if (z.DamageCut > 0f && p.Pos.DistanceTo(z.Pos) <= z.Radius)
                    cut = Math.Max(cut, z.DamageCut);
            amount *= 1f - cut;  // a mastered Haven
            if (p.UnbreakT > 0f)
                amount *= 1f - Math.Min(0.8f, 0.5f + p.Stats.UnbreakBonus);
            amount *= 1f - IronBodyCut(p);  // the Ninja's Iron Body
            amount *= 1f - BloodCut(p);     // Blood Rite, a mastered Crimson Pact
            amount *= 1f - ClotCut(p);      // Blood Lash's Clot
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.Fortress)
                amount *= 0.25f;
            if (p.ShieldHp > 0f)  // Arcane Ward soaks damage first
            {
                float soak = Math.Min(p.ShieldHp, amount);
                p.ShieldHp -= soak;
                amount -= soak;
            }
            p.Run.DamageTaken += Math.Min(amount, Math.Max(0f, p.Hp));
            DamageEvent?.Invoke(p, Math.Min(amount, Math.Max(0f, p.Hp)), source);
            Recorder?.DamageTaken(p, Math.Min(amount, Math.Max(0f, p.Hp)), source);
            BloodTookHit(p, amount);  // Crimson Pact stores it; Blood Rite heals allies with it
            p.Hp -= amount;
            p.InvulnT = HitIframes;
            p.Flash = 0.1f;
            if (p.Hp <= 0f && LastStanding(p))
                p.Hp = 1f;  // Last Stand
            if (p.Hp <= 0f && Exalted(p))
                p.Hp = 1f;  // Crimson Apotheosis
            if (p.Hp <= 0f && InLantern(p))
                p.Hp = 1f;  // a Soul Lantern keeps you standing
            if (p.Hp <= 0f)
            {
                p.Hp = 0f;
                p.Downed = true;
                p.Run.Downs++;
                Recorder?.Downed(this, p, source);
                p.Revive = 0f;
                p.BarrierT = 0f;
                p.OverdriveT = 0f;
                p.Firing = false;
            }
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
                enemyShots++;
            var b = MakeBullet(nextBulletId++, kind, owner, pos, vel, angVel, life, fromBoss, homing);
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
                if (p.SurgeT > 0f)  // Arcane Surge
                {
                    b.Dmg *= 1.6f;
                    b.Pierce += 2;
                }
            }
            pendingSpawns.Add(new Bullet { Id = b.Id, Kind = kind, Owner = owner, Pos = pos, Vel = vel, AngVel = angVel, Life = life, FromBoss = fromBoss, Homing = homing });
            return b;
        }

        /// Weapon mods apply to players' shots, but not to grenades (they land where you aim)
        /// or to Swarm Rounds' splinters.
        static bool Moddable(byte kind) => !Defs.IsEnemyShot(kind) && kind != Defs.GrenadeShot && kind != Defs.SwarmShot;

        Bullet MakeBullet(int id, byte kind, int owner, V2 pos, V2 vel, float angVel, float life, bool fromBoss = false, float homing = 0f)
        {
            var b = new Bullet { Id = id, Kind = kind, Owner = owner, Pos = pos, Vel = vel, AngVel = angVel, Life = life, Life0 = life, FromBoss = fromBoss, Homing = homing, Radius = Defs.BulletRadius(kind) };
            Bullets[id] = b;
            if (owner == MyId && owner != 0 && kind != Defs.GrenadeShot)
                NoteShot(b);
            if (owner == 0)
                Sounds.Add(Sfx.EnemyShot);
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
            Player p;
            if (!Players.TryGetValue(owner, out p))
                return Sfx.ShotRifle;
            switch (p.Weapon.Id)
            {
                case WeaponId.Scattergun: return Sfx.ShotShotgun;
                case WeaponId.FlakCannon: return Sfx.ShotCannon;
                case WeaponId.RiotRepeater: return Sfx.ShotRifle;
                case WeaponId.LightBolt: return Sfx.ShotSuppressed;
                case WeaponId.Pistol: return Sfx.ShotPistol;
                case WeaponId.MendersBow: return Sfx.ShotBow;
                case WeaponId.TwinRifle: return Sfx.ShotAssault;
                case WeaponId.Railgun: return Sfx.ShotSniper;
                case WeaponId.BurstSmg: return Sfx.ShotSmg;
                case WeaponId.Flamethrower:
                case WeaponId.Fireball: return Sfx.SpellFire;
                case WeaponId.FrostLance: return Sfx.SpellFrost;
                case WeaponId.StormHammer:
                case WeaponId.ChainLight:
                case WeaponId.ArcLightning: return Sfx.SpellZap;
                case WeaponId.PrismBeam:
                case WeaponId.ArcaneMissiles:
                case WeaponId.WispLantern: return Sfx.SpellArcane;
                case WeaponId.RadiantOrb: return Sfx.SpellHoly;
                case WeaponId.ClotBolt:
                case WeaponId.LeechBeam:
                case WeaponId.SanguineOrb:
                case WeaponId.VeinTether:
                case WeaponId.BloodBolts:
                case WeaponId.Implosion: return Sfx.SpellArcane;
                default: return Sfx.Throw;  // summoner staffs, darts and rocks
            }
        }

        readonly List<int> scratchIds = new List<int>();

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
            PrepareNinjaBullets(dt);  // Smoke Bombs and returning chain shuriken (Ninja.cs)
            // Bullets leave a little past the arena's edges, or stop dead at Narrow Pass's walls.
            V2 lo = ArenaMin, hi = ArenaMax, margin = new V2(Walled ? 0f : 40f, Walled ? 0f : 40f);
            lo -= margin;
            hi += margin;
            foreach (var b in Bullets.Values)
            {
                if (b.AngVel != 0f)
                    b.Vel = b.Vel.Rotated(b.AngVel * dt);
                if (b.Homing > 0f)
                    Steer(b, dt);
                if (wellsUp && b.Owner != 0 && b.Kind != Defs.GrenadeShot)
                    PullIntoWells(b, dt);
                b.Pos += b.Vel * (dt * SmokeSpeed(b));
                b.Life -= dt;
                if (b.Life <= 0f || b.Pos.X < lo.X || b.Pos.Y < lo.Y || b.Pos.X > hi.X || b.Pos.Y > hi.Y)
                {
                    scratchIds.Add(b.Id);
                    if (Walled && b.Life > 0f)
                        AddFx(FxKind.Ring, b.Pos.Clamp(lo, hi), Defs.BulletColor(b.Kind), 7f, 0.18f);  // a spark on the wall
                    if (b.Burst > 0 && b.Life <= 0f)
                        landed.Add(b);
                }
            }
            foreach (int id in scratchIds)
                Bullets.Remove(id);
            // Host only (Burst is never set on clients): landed mortar bombs and grenades explode.
            foreach (var b in landed)
            {
                if (b.Bomblet)
                {
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && e.Pos.DistanceTo(b.Pos) < b.Splash + e.Radius && !BlocksShot(e, b.Pos))
                            HurtEnemy(e, b.Dmg, b.Owner);
                    BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.FlakShell), b.Splash, 0.25f, Sfx.None);
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
                    SpawnBullet(Defs.SmallShot, b.Pos, dir * 150f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0).Source = b.Source;
                }
                BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.MineShot), 30f, 0.3f, Sfx.Hit);
            }
        }

        readonly List<Bullet> splashes = new List<Bullet>();

        /// Homing Rounds (2 turn per stack) cost 5% damage per point of turn: 10% a stack.
        public const float HomingDamageCost = 0.05f;

        /// Weapons whose damage lands in an area (blasts, pools, the Implosion, the Vein Tether beam, the Leech
        /// Tendrils' arc, Storm Hammer's lightning). On them Homing Rounds don't bend shots: each stack makes the area 25% bigger instead, for
        /// the same damage cost.
        public static bool AreaWeapon(WeaponId w) => w == WeaponId.Implosion || w == WeaponId.FlakCannon || w == WeaponId.Fireball || w == WeaponId.Ofuda
            || w == WeaponId.StormHammer || w == WeaponId.ClotBolt || w == WeaponId.VeinTether || w == WeaponId.LeechBeam;
        public const float HomingAreaPerStack = 0.25f;
        public static float AreaMult(Player p) => AreaWeapon(p.Weapon.Id) ? 1f + HomingAreaPerStack * p.Stats.Homing / 2f : 1f;  // Homing Rounds give 2 turn a stack
        static float HomingCost(Player p) => 1f - HomingDamageCost * p.Stats.Homing;
        const float HomingRange = 260f, HomingCone = 0.7f;  // only enemies within 260 px and ~45 degrees of the shot's path

        /// Homing Rounds: bend toward the nearest enemy close ahead, so near misses connect but a shot
        /// fired the wrong way doesn't turn around to find someone. Clients steer the same way with the
        /// enemies they see, and the host sends the exact course 20 times a second.
        void Steer(Bullet b, float dt)
        {
            Enemy best = null;
            float bestD = HomingRange * HomingRange;
            V2 dir = b.Vel.Normalized();
            foreach (var e in Enemies.Values)
            {
                if (e.Spawning || e.SpawnT > 0f || e.Id == b.LastHit || e.Def.Xp == 0)
                    continue;
                V2 to = e.Pos - b.Pos;
                float d = to.LengthSq;
                if (d < bestD && to.X * dir.X + to.Y * dir.Y > HomingCone * (float)Math.Sqrt(d))
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
                if (p.Id == b.Owner || p.Downed || p.Hp >= p.MaxHp || b.Pos.DistanceSq(p.Pos) >= r * r)
                    continue;
                Heal(p, b.HealAlly * (b.Dmg / 9f), PlayerOrNull(b.Owner));
                TransfusionCost(b);  // Transfusion Darts cost their healer (BloodMage.cs)
                return true;
            }
            return false;
        }

        void Collide()
        {
            scratchIds.Clear();
            splashes.Clear();
            splits.Clear();
            frags.Clear();
            reflects.Clear();
            ricochets.Clear();
            ResolveSwings();  // Knight melee hits land first, through the same on-hit effects as bullets
            ResolveBeams();   // then Vein Tether's beam ticks (BloodMage.cs)
            ResolveGrasps();  // Leech Tendrils' holds
            ResolveImplosions();  // and Implosions
            foreach (var b in Bullets.Values)
            {
                if (b.Kind == Defs.GrenadeShot)
                    continue;  // grenades fly over enemies and explode where they land
                if (!Defs.IsEnemyShot(b.Kind))
                {
                    if (b.HealAlly > 0f && HealAllyHit(b))
                    {
                        scratchIds.Add(b.Id);
                        continue;
                    }
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Hp <= 0f || e.Id == b.LastHit)
                            continue;
                        float r = e.Radius + b.Radius;
                        if (b.Pos.DistanceSq(e.Pos) < r * r)
                        {
                            if (BlocksShot(e, b.Pos))
                            {
                                WearShield(e, b.Dmg);  // the shield takes it, and wears
                                scratchIds.Add(b.Id);
                                break;
                            }
                            if (b.FromWeapon && b.LastHit == 0)
                            {
                                Player shooter;
                                if (Players.TryGetValue(b.Owner, out shooter))
                                    shooter.Run.ShotsHit++;
                            }
                            float hit = b.Dmg * NinjaHitMult(b.Owner, b.Kind, e);  // Kunai and ambush bonuses (Ninja.cs)
                            HurtEnemy(e, hit, b.Owner);
                            BloodShotHit(b, e, hit);  // Blood Mage shots drain, pool and drop orbs (BloodMage.cs)
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
                                if (b.Bounces > 0)
                                    ricochets.Add(b);
                            }
                            break;
                        }
                    }
                }
                else
                {
                    if (!Defs.Unblockable(b.Kind) && BlockedBySummons(b) || GuardStopsBullet(b))  // GuardStopsBullet: Stun.cs
                    {
                        scratchIds.Add(b.Id);
                        continue;
                    }
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed)
                            continue;
                        bool blockable = !Defs.Unblockable(b.Kind);
                        if (p.ParryActive && InParryArc(p, b.Pos, b.Radius))
                        {
                            // The parry stops anything in front of it, even shots barriers can't.
                            scratchIds.Add(b.Id);
                            if (ParryHit(p, b.Dmg, b.Pos))  // perfect parry or Mirror Guard: sent back
                                reflects.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = p.Id, Dmg = p.Stats.ReflectMult });
                            break;
                        }
                        if (p.WallT > 0f && WallCovers(p, b.Pos, b.Radius))
                        {
                            scratchIds.Add(b.Id);  // Shield Wall: stops everything from the front, red aimed shots included
                            if (p.WallReflect)
                                reflects.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = p.Id });
                            p.WallHp -= BlockerWear(b);
                            if (p.WallHp <= 0f)
                            {
                                p.WallT = 0f;
                                BroadcastFx(FxKind.Boom, p.Pos, new Rgba(0.75f, 0.85f, 1f), 50f, 0.4f, Sfx.Clang);
                            }
                            break;
                        }
                        if (blockable && p.AegisT > 0f && Enters(b, p.Pos, p.Def.BodyR + 24f))
                        {
                            reflects.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = p.Id, Dmg = p.AegisReflectMult });  // Arcane Aegis
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
                        float r = p.Def.HitR + b.Radius;
                        if (b.Pos.DistanceSq(p.Pos) < r * r)
                        {
                            // Invulnerable players (dash, recent hit) let bullets pass through.
                            if (p.InvulnT <= 0f)
                            {
                                Damage(p, b.Dmg, ShotSource(b));
                                scratchIds.Add(b.Id);
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
                if (Players.TryGetValue(rb.Owner, out owner))
                    SpawnBullet(Defs.ArcaneShot, rb.Pos, rb.Vel.Normalized() * -520f, 0f, 1f, 12f * owner.Stats.DmgMult * (rb.Dmg > 0f ? rb.Dmg : 1f), rb.Owner);
            }

            // Ricochet: the bullet carries on toward the nearest other enemy.
            foreach (var rb in ricochets)
            {
                Enemy next = null;
                float bestD = 300f * 300f;
                foreach (var e in Enemies.Values)
                {
                    if (e.Id == rb.LastHit || e.Hp <= 0f || e.SpawnT > 0f || e.Def.Xp == 0)
                        continue;
                    float d = e.Pos.DistanceSq(rb.Pos);
                    if (d < bestD)
                    {
                        next = e;
                        bestD = d;
                    }
                }
                if (next == null)
                    continue;
                float speed = Math.Max(400f, rb.Vel.Length);
                V2 dir = (next.Pos - rb.Pos).Normalized();
                // SpawnBullet applies Velocity Rounds again, so undo it on the carried-over speed.
                Player owner;
                if (Players.TryGetValue(rb.Owner, out owner))
                    speed /= owner.Stats.ShotSpeedMult;
                var nb = SpawnBullet(rb.Kind, rb.Pos, dir * speed, 0f, 0.8f, rb.Dmg * 0.75f, rb.Owner);
                nb.LastHit = rb.LastHit;
                nb.Pierce = 0;
                nb.Bounces = rb.Bounces - 1;
                nb.Splash = rb.Splash;
                nb.Chain = rb.Chain;
                nb.SlowAmt = rb.SlowAmt;
            }

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

            foreach (var sp in splits)
            {
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
                BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(b.Kind), b.Splash, 0.3f, b.Kind == Defs.FlakShell ? Sfx.Explode : Sfx.None);
                if (b.HealBurst > 0f)
                    CharmHeal(b);  // Ofuda (Ninja.cs)
            }

            var dead = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (e.Hp <= 0f)
                    dead.Add(e);
            foreach (var e in dead)
            {
                Recorder?.EnemyKilled(e);
                bool bossReward = OnEnemyKilled(e);
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
                Score += (int)Math.Round(e.Score * (e.Elite ? 3 : 1) * PathBonus);
                GrantXp(e.Def.Xp * (e.Elite ? 3 : 1) * PathBonus);
                if (OnPathOf(PathId.Bazaar))
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

        void HurtEnemy(Enemy e, float dmg, int attacker)
        {
            if (SoulBound(e))
                return;  // immune until its twin is brought down to match
            if (WardenImmune(e) || QueenImmune(e))
                return;  // the Warden or the Hive Queen changing stage, or the Queen up in a dive
            if (attacker > 0 && Has(RunMod.GlassCannon))
                dmg *= Modifiers.GlassCannonDealt;
            if (casting)
            {
                dmg *= castPower;
                castHits++;
            }
            if (e.MarkT > 0f)
                dmg *= e.MarkMult;
            if (attacker > 0)
                dmg *= LitMult(e);  // in a Searchlight (Stun.cs)
            if (Hungry(e))
            {
                e.Hp = Math.Min(e.MaxHp, e.Hp + dmg * 0.5f);  // the Devourer feeds on your shots
                return;
            }
            Player p;
            if (Players.TryGetValue(attacker, out p))
            {
                var s = p.Stats;
                dmg *= KnightDamageMult(p);  // Battle Fury, Last Stand
                dmg *= BloodDamageMult(p);   // Blood Price
                if (s.ExecuteBonus > 0f && e.Hp < e.MaxHp * 0.35f)
                    dmg *= 1f + s.ExecuteBonus;
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
                KnightLifesteal(p, Math.Min(dmg, Math.Max(0f, e.Hp)));
                BloodLifesteal(p, Math.Min(dmg, Math.Max(0f, e.Hp)));
            }
            Recorder?.DamageDealt(attacker, e, Math.Min(dmg, Math.Max(0f, e.Hp)));
            e.Hp -= dmg;
            e.Flash = 0.06f;
        }

        // Swarm Rounds splits and Aegis reflections, queued while the bullet loop runs.
        readonly List<Bullet> splits = new List<Bullet>();
        readonly List<Bullet> frags = new List<Bullet>();
        readonly List<Bullet> reflects = new List<Bullet>();
        readonly List<Bullet> ricochets = new List<Bullet>();

        /// Legendary on-hit effects that spawn things (run after the collision loop).
        void OnBulletHit(Bullet b, Enemy e)
        {
            if (b.Chain > 0)
                ChainLightning(e, b.Dmg * 0.6f, b.Chain, b.Owner, ChainReach * (b.Owner != 0 && Players.ContainsKey(b.Owner) ? AreaMult(Players[b.Owner]) : 1f));
            StunBoltHit(b, e);  // Stun.cs
            if (b.Ignite > 0f)  // the Flamethrower: burning, the strongest fire on it counts
            {
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
                splashes.Add(new Bullet { Pos = b.Pos, Owner = b.Owner, Kind = Defs.NovaShot, Splash = p.Stats.ImpactSplash, Dmg = b.Dmg * 0.67f, LastHit = e.Id });
            if (!Moddable(b.Kind))
                return;

            // Weapon mods.
            var s = p.Stats;
            if (s.ExplosiveRadius > 0f)
                splashes.Add(new Bullet { Pos = b.Pos, Owner = b.Owner, Kind = Defs.FireShot, Splash = s.ExplosiveRadius, Dmg = b.Dmg * 0.5f, LastHit = e.Id });
            if (s.BurnFrac > 0f)
            {
                e.BurnT = 3f;
                e.BurnDps = Math.Max(e.BurnDps, b.Dmg * s.BurnFrac);
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
        }

        /// Incendiary Rounds: damage over time, credited to whoever lit the fire.
        void TickBurn(Enemy e, float dt)
        {
            if (e.BurnT <= 0f)
                return;
            e.BurnT -= dt;
            if (e.BurnT <= 0f)
                e.BurnDps = 0f;
            if (Hungry(e) || e.SpawnT > 0f || SoulBound(e))
                return;
            DotDamage(e, e.BurnDps * dt, e.BurnBy);
        }

        /// Damage over time (burning, bleeding), credited to whoever caused it.
        void DotDamage(Enemy e, float dmg, int by)
        {
            Player p;
            if (Players.TryGetValue(by, out p))
            {
                Recorder?.DamageDealt(p.Id, e, Math.Min(dmg, Math.Max(0f, e.Hp)));
                p.Run.DamageDealt += Math.Min(dmg, Math.Max(0f, e.Hp));
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
            if (s.BroodChance > 0f && rng.NextDouble() < s.BroodChance)
                SpawnMinion(MinionKind.Turret, p, e.Pos, 4f);
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
            if (Has(RunMod.Famine) || OnPathOf(PathId.BloodMoon))
                return;  // run modifier / Blood Moon: no health orbs (bosses still drop theirs)
            double roll = rng.NextDouble();
            if (roll < BigOrbChance)
                SpawnOrb(e.Pos, true);
            else if (roll < BigOrbChance + OrbChance)
                SpawnOrb(e.Pos, false);
        }

        void SpawnOrb(V2 pos, bool big)
        {
            var o = new Orb { Id = nextOrbId++, Big = big, Pos = ClampToArena(pos, 12f), Life = OrbLifetime };
            o.Disp = o.Pos;
            Orbs[o.Id] = o;
        }

        /// Orbs drift toward the nearest hurt player in range and heal whoever touches them.
        /// Players at full health leave them for their ally.
        void TickOrbs(float dt)
        {
            var gone = new List<int>();
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
            if (!Defs.IsEnemyShot(b.Kind))
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
        }
    }
}
