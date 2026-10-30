using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Ninja: quick and lightly protected (85% health, 12% faster), with thrown blades and fast melee for
    /// every role. Tanks misdirect (clones, smoke, Iron Body), healers keep the party up from close by (mist,
    /// swapping places, wards), and DPS strike from the shadows (Shadow Step, Vanish's ambush, Blade Fan).
    public partial class Game
    {
        const float KatanaLungeReach = 170f;  // (each weapon's main reach, speed and damage are in Defs)
        public const float FumaReach = 380f, FumaWidth = 110f;  // a Fuma Shuriken's loop: this far out in front of you, this far either side (its Life is one lap)
        public static float FumaSize(int i) => 1f + 0.15f * (i / 2);  // the i-th one thrown loops this much wider (every second pair, a little more)
        const float StepRange = 450f, StepDamage = 80f;
        const float CloneTime = 5f, CloneBurst = 60f, CloneBurstRadius = 110f;
        const float SmokeRadius = 140f, SmokeTime = 5f, SmokeCut = 0.3f, SmokeBulletSpeed = 0.5f;
        const float IronTime = 2f, IronCut = 0.8f, IronWaveRadius = 140f, IronWaveDamage = 50f;
        const float MistRadius = 130f, MistTime = 6f, SubShield = 60f, SubShieldTime = 6f, WardRadius = 140f, WardTime = 8f;
        const float VanishTime = 3f, AmbushGrace = 1f, FanDamage = 18f, KunaiBonus = 0.5f;
        const float BlossomTime = 5f, BlossomHeal = 20f, BlossomClear = 100f, LotusRange = 350f, LotusDamage = 100f;

        // --- Weapons ------------------------------------------------------------------

        /// A Ninja weapon's attack. `extra` is the number of extra projectiles; each weapon uses them its own way.
        void FireNinjaWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            p.SwingCount++;
            var w = p.Weapon;  // its Speed, Life, Damage, Reach and Grow (Defs)
            switch (w.Id)
            {
                case WeaponId.Kusarigama:
                    if (!Hooked(p))
                        ThrowKusarigama(p, aim, w.Reach + w.Grow * extra, w.Damage * dmg);  // hooked: firing reels you in instead (LocalWeaponDash)
                    break;
                case WeaponId.BoStaff:
                    pendingSwings.Add(NewSwing(p, aim, w.Reach + w.Grow * extra, 360f, w.Damage * dmg));
                    break;
                case WeaponId.FumaShuriken:
                {
                    // A giant star thrown in an oval loop out in front of you and back, cutting through everything.
                    // Each extra one loops the other way, and every second pair loops a little wider.
                    V2 front = V2.FromAngle(aim);
                    for (int i = 0; i <= extra; i++)
                    {
                        float s = i % 2 == 0 ? 1f : -1f, size = FumaSize(i);
                        V2 vel = front.Orthogonal() * (-s * FumaWidth * size * MathX.Tau / w.Life);  // it sets off sideways (FumaTurn)
                        SpawnBullet(Defs.FumaStar, p.Pos, vel, s, w.Life, w.Damage * dmg, p.Id).Pierce += 99;
                    }
                    break;
                }
                case WeaponId.MedicineDarts:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float a = aim + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * 0.08f * (n - 1));
                        var b = SpawnBullet(Defs.NeedleShot, muzzle, V2.FromAngle(a) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.Medicine = true;  // poisons enemies, doses allies (Ninja.cs)
                    }
                    break;
                }
                case WeaponId.SpiritFan:
                {
                    // A gust: five short-lived puffs that heal allies they pass, and small enemy bullets in front blown back.
                    // Shots alternate: a gust out that blows enemies away, then a draw in that pulls them to you.
                    float reach = 1f + w.Grow * extra;
                    bool draw = p.SwingCount % 2 == 0;
                    float len = w.Speed * w.Life * reach * p.Stats.ShotSpeedMult;
                    for (int i = 0; i < 5; i++)
                    {
                        V2 d = V2.FromAngle(aim + (i / 4f - 0.5f) * 0.7f);
                        var b = SpawnBullet(Defs.GustShot, draw ? p.Pos + d * (p.Def.BodyR + len) : muzzle, (draw ? -d : d) * w.Speed, 0f, w.Life * reach, w.Damage * dmg, p.Id);  // a draw's puffs fly in
                        b.Pierce += 99;
                        b.HealAlly = (3f + extra) * 9f / 6f;
                    }
                    FanEnemies(p, aim, p.Def.BodyR + len + FanReachSlack, draw ? -FanShove : FanShove);
                    float push = 130f * reach;
                    foreach (var b in Bullets.Values)
                    {
                        if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind) || b.Kind != Defs.SmallShot && b.Kind != Defs.FastShot)
                            continue;
                        if (b.Pos.DistanceTo(p.Pos) > push || AngleTo(p.Pos, b.Pos, aim) > 0.45f)
                            continue;
                        b.Vel = (b.Pos - p.Pos).Normalized() * b.Vel.Length;
                        b.Pulled = true;  // the host sends its new course
                    }
                    break;
                }
                case WeaponId.Ofuda:
                {
                    int n = 1 + extra;
                    // Each charm flies to your cursor (over enemies) and lies there as a ward (PlaceWard), fanned apart.
                    float far = Math.Min(WardRange, Math.Max(40f, p.AimAt.DistanceTo(p.Pos) - p.Def.BodyR));
                    for (int i = 0; i < n; i++)
                    {
                        float a = aim + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * 0.4f * (n - 1));
                        var b = SpawnBullet(Defs.OfudaShot, muzzle, V2.FromAngle(a) * w.Speed, 0f, far / w.Speed, w.Damage * dmg, p.Id);
                        b.Ward = true;
                        b.Burst = 1;  // marks it as landing (MoveBullets)
                    }
                    break;
                }
                case WeaponId.Shuriken:
                {
                    int n = 3 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.StarShot, muzzle, V2.FromAngle(aim + (i / (n - 1f) - 0.5f) * 0.22f * (n - 1)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Sticks = true;  // (double-tap fire to recall them)
                    break;
                }
                case WeaponId.Katana:
                {
                    // Every 4th draw is a long lunging cut.
                    bool lunge = p.SwingCount % 4 == 0;
                    var s = lunge ? NewSwing(p, aim, KatanaLungeReach, 30f, w.Damage * 2f * dmg) : NewSwing(p, aim, w.Reach, 110f, w.Damage * dmg);
                    s.Thrust = lunge;
                    s.Heavy = lunge;
                    pendingSwings.Add(s);
                    for (int k = 1; k <= extra; k++)
                    {
                        var c = NewSwing(p, aim + (k % 2 == 1 ? 0.2f : -0.2f), w.Reach, 110f, 12f * dmg);
                        c.Delay = 0.05f * k;
                        c.Silent = true;
                        pendingSwings.Add(c);
                    }
                    break;
                }
                case WeaponId.Kunai:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float a = aim + (i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * 0.07f * ((i + 1) / 2));
                        var b = SpawnBullet(Defs.KunaiShot, muzzle, V2.FromAngle(a) * w.Speed, 0f, w.Life, (i == 0 ? w.Damage : w.Damage * 0.5f) * dmg, p.Id);
                        b.ShadowKunai = i == 0 && ++p.KunaiCount % KunaiShadowEvery == 0;  // every few throws, one leaves a mark to blink to
                    }
                    break;
                }
            }
        }

        /// How much harder a Ninja's direct hit lands: Kunai on enemies that aren't after you, and Vanish's
        /// ambush (the next hit after vanishing, used up by it). Only for shots and swings, not burns or zones.
        float NinjaHitMult(int owner, byte kind, Enemy e)
        {
            Player p;
            if (!Players.TryGetValue(owner, out p))
                return 1f;
            float m = 1f;
            if (kind == Defs.KunaiShot && e.TargetId != owner)
                m *= 1f + KunaiBonus;
            if (p.AmbushT > 0f)
            {
                m *= p.Stats.AmbushMult;
                p.AmbushT = 0f;
                BroadcastFx(FxKind.Spark, e.Pos, new Rgba(0.75f, 0.6f, 1f), 30f, 0.3f, Sfx.SwingHeavy);
            }
            return m;
        }

        // --- Abilities ------------------------------------------------------------------

        /// The Ninja's abilities and ultimates. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] NinjaAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.ShadowClone, Kit = Kit.Ninja, Role = Role.Tank, Choice = 0,
                Name = "Shadow Clone", Cooldown = 14f, Desc = "Leave a decoy for 5 s that every enemy targets. It bursts for 60 damage when it ends.",
                Capstone = "Leaves two clones.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.SmokeBomb, Kit = Kit.Ninja, Role = Role.Tank, Choice = 1,
                Name = "Smoke Bomb", Cooldown = 14f, Desc = "A 140 px cloud at your feet for 5 s: enemy bullets inside slow to half speed, and allies inside take 30% less damage.",
                Capstone = "Enemies inside the smoke are also slowed by 40%.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.IronBody, Kit = Kit.Ninja, Role = Role.Tank, Choice = 2,
                Name = "Iron Body", Cooldown = 12f, Desc = "For 2 s you take 80% less damage, then a shockwave hits everything within 140 px for 50.",
                Capstone = "The shockwave also erases enemy bullets within 160 px.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.HealingMist, Kit = Kit.Ninja, Role = Role.Healer, Choice = 0,
                Name = "Healing Mist", Cooldown = 15f, Desc = "A mist follows you for 6 s: allies inside heal 10 HP/s.",
                Capstone = "Lasts 9 s.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.Substitution, Kit = Kit.Ninja, Role = Role.Healer, Choice = 1,
                Name = "Substitution", Cooldown = 12f, Desc = "Swap places with the ally nearest your cursor and shield them for 60 for 6 s. Alone: slip to your cursor and shield yourself.",
                Capstone = "You and your ally are both invulnerable for 1 s after the swap.",
                Bot = b => b.AllyDown || b.AllyHurt && b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.SpiritWard, Kit = Kit.Ninja, Role = Role.Healer, Choice = 2,
                Name = "Spirit Ward", Cooldown = 18f, Desc = "Plant a talisman at your cursor for 8 s: allies near it regenerate 6 HP/s and revive 3x faster.",
                Capstone = "The ward follows you.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.ShadowStep, Kit = Kit.Ninja, Role = Role.Dps, Choice = 0, CapstoneCharge = true,
                Name = "Shadow Step", Cooldown = 7f, Desc = "Teleport behind the enemy nearest your cursor (up to 450 px away) and strike it for 80.",
                Capstone = "+1 charge.",
                Bot = b => b.HaveTarget && b.TargetD > 120f && b.TargetD < 440f || b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Vanish, Kit = Kit.Ninja, Role = Role.Dps, Choice = 1,
                Name = "Vanish", Cooldown = 16f, Desc = "For 3 s enemies lose track of you. Your next hit (within 4 s) deals double damage.",
                Capstone = "Erases enemy bullets around you when you vanish.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.BladeFan, Kit = Kit.Ninja, Role = Role.Dps, Choice = 2,
                Name = "Blade Fan", Cooldown = 9f, Desc = "Throw 12 kunai in a ring around you, 18 damage each.",
                Capstone = "Throws twice as many kunai.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 260f,
            },
            new AbilityDef
            {
                Id = AbilityId.ThousandClones, Kit = Kit.Ninja, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Thousand Clones", Cooldown = 70f, Desc = "Five clones surround you for 8 s: every enemy targets them, and each bursts for 60 when it ends.",
                Capstone = "Eight clones.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.BlossomStorm, Kit = Kit.Ninja, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Blossom Storm", Cooldown = 80f, Desc = "For 5 s petals heal the whole party 20 HP/s and erase enemy bullets near every ally.",
                Capstone = "Lasts 8 s.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.DeathLotus, Kit = Kit.Ninja, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Death Lotus", Cooldown = 65f, Desc = "Strike up to 8 enemies near your cursor in a flurry of cuts, 100 damage each.",
                Capstone = "Strikes up to 14 enemies.",
            },
        };

        /// Ninja abilities and ultimates. Returns false if `id` isn't one.
        bool NinjaAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                case AbilityId.ShadowClone:
                {
                    float life = CloneTime + s.CloneBonusTime, burst = pw * s.CloneBurstMult * s.DmgMult;
                    SpawnClone(p, p.Pos + V2.FromAngle(p.Aim + MathX.Pi) * 40f, life, burst);
                    if (cap)
                        SpawnClone(p, p.Pos + V2.FromAngle(p.Aim) * 60f, life, burst);
                    Sounds.Add(Sfx.Summon);
                    return true;
                }
                case AbilityId.SmokeBomb:
                {
                    var z = AddZone(ZoneKind.Smoke, p, p.Pos, SmokeRadius * s.SmokeRadiusMult, SmokeTime + s.SmokeBonusTime);
                    z.DamageCut = SmokeCut;
                    if (cap)
                        z.ExtraSlow = 0.4f;
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.4f, Sfx.Throw);
                    return true;
                }
                case AbilityId.IronBody:
                    p.IronT = IronTime + s.IronBonusTime;
                    p.IronPower = pw * s.IronWaveMult;
                    p.IronMastered = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.75f, 0.78f, 0.85f), 50f, 0.35f, Sfx.Clang);
                    return true;
                case AbilityId.HealingMist:
                {
                    var z = AddZone(ZoneKind.Mist, p, p.Pos, MistRadius * s.MistRadiusMult, cap ? 9f : MistTime);
                    z.Follow = true;
                    z.HealMult *= s.MistHealMult;
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.45f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.Substitution:
                    Substitute(p, target, SubShield * s.SubShieldMult * pw, SubShieldTime + s.SubBonusTime, cap);
                    return true;
                case AbilityId.SpiritWard:
                {
                    var z = AddZone(ZoneKind.Ward, p, ClampToArena(target, 10f), WardRadius * s.WardRadiusMult, WardTime + s.WardBonusTime);
                    z.Follow = cap;
                    if (cap)
                        z.Pos = p.Pos;
                    BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius, 0.45f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.ShadowStep:
                {
                    // The player's own game moved them behind the enemy (LocalControl); strike whoever is right there.
                    Enemy best = null;
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && e.Pos.DistanceTo(target) < e.Radius + p.Def.BodyR + 60f && (best == null || e.Pos.DistanceSq(target) < best.Pos.DistanceSq(target)))
                            best = e;
                    if (best != null)
                    {
                        float dmg = StepDamage * s.DmgMult * s.StepDmgMult * NinjaHitMult(p.Id, Defs.KnightSlash, best);
                        HurtEnemy(best, dmg, p.Id);
                        OnBulletHit(new Bullet { Pos = best.Pos, Vel = (best.Pos - target).Normalized(), Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, best);
                        BroadcastFx(FxKind.Slash, target, best.Pos + (best.Pos - target).Normalized() * best.Radius, new Rgba(0.75f, 0.6f, 1f), 0.12f, 0.2f, Sfx.SwingLight);
                    }
                    return true;
                }
                case AbilityId.Vanish:
                    p.VanishT = VanishTime + s.VanishBonusTime;
                    p.AmbushT = p.VanishT + AmbushGrace;
                    p.VanishFrom = p.Pos;
                    foreach (var e in Enemies.Values)
                        if (e.TargetId == p.Id)
                            e.RetargetT = 0f;  // they look for someone else
                    if (cap)
                        EraseEnemyBullets(p.Pos, 160f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.45f, 0.4f, 0.65f), 45f, 0.35f, Sfx.Blink);
                    return true;
                case AbilityId.BladeFan:
                {
                    int n = (12 + s.FanExtraKunai) * (cap ? 2 : 1);
                    for (int i = 0; i < n; i++)
                    {
                        V2 d = V2.FromAngle(p.Aim + MathX.Tau * i / n);
                        SpawnBullet(Defs.KunaiShot, p.Pos + d * p.Def.BodyR, d * 600f, 0f, 0.7f, FanDamage * s.DmgMult, p.Id).Pierce += 1;
                    }
                    Sounds.Add(Sfx.Throw);
                    return true;
                }

                // Ultimates
                case AbilityId.ThousandClones:
                {
                    int n = cap ? 8 : 5;
                    for (int i = 0; i < n; i++)
                        SpawnClone(p, p.Pos + V2.FromAngle(p.Aim + MathX.Tau * i / n) * 80f, 8f, s.DmgMult);
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(MinionKind.Clone).Color, 90f, 0.5f, Sfx.Summon);
                    return true;
                }
                case AbilityId.BlossomStorm:
                    p.UltT = cap ? 8f : BlossomTime;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.7f, 0.85f), 160f, 0.6f, Sfx.SpellHoly);
                    return true;
                case AbilityId.DeathLotus:
                {
                    var marks = new List<Enemy>();
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && e.Pos.DistanceTo(target) < LotusRange)
                            marks.Add(e);
                    marks.Sort((a, b) => a.Pos.DistanceSq(target).CompareTo(b.Pos.DistanceSq(target)));
                    int most = cap ? 14 : 8;
                    if (marks.Count > most)
                        marks.RemoveRange(most, marks.Count - most);
                    V2 from = p.Pos;
                    foreach (var e in marks)
                    {
                        float dmg = LotusDamage * s.DmgMult;
                        HurtEnemy(e, dmg, p.Id);
                        OnBulletHit(new Bullet { Pos = e.Pos, Vel = (e.Pos - from).Normalized(), Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
                        BroadcastFx(FxKind.Slash, from, e.Pos, new Rgba(1f, 0.55f, 0.75f), 0.12f, 0.35f, Sfx.None);
                        from = e.Pos;
                    }
                    p.InvulnT = Math.Max(p.InvulnT, 1f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.55f, 0.75f), 70f, 0.4f, Sfx.SwingHeavy);
                    return true;
                }
            }
            return false;
        }

        /// A Shadow Clone: enemies go for it, and it bursts when it ends (burst = damage multiplier).
        void SpawnClone(Player p, V2 pos, float life, float burst)
        {
            var m = SpawnMinion(MinionKind.Clone, p, pos, life);
            m.Power = burst;
            m.Aim = p.Aim;
        }

        /// Substitution: swap places with the ally nearest the cursor and shield them (alone: slip toward the
        /// cursor and shield yourself). Each player's own game moves them, so the host tells them where to go.
        void Substitute(Player p, V2 target, float shield, float time, bool mastered)
        {
            Player ally = null;
            foreach (var q in Players.Values)
                if (q.Id != p.Id && !q.Downed && (ally == null || q.Pos.DistanceSq(target) < ally.Pos.DistanceSq(target)))
                    ally = q;
            V2 mine = p.Pos;
            var guard = ally ?? p;
            if (ally != null)
            {
                MovePlayer(p, ally.Pos);
                MovePlayer(ally, mine);
            }
            else
                MovePlayer(p, ClampToArena(p.Pos + (target - p.Pos).LimitLength(BlinkRange), p.Def.BodyR));
            guard.ShieldHp = Math.Max(guard.ShieldHp, shield);
            guard.ShieldT = time;
            if (mastered)
            {
                p.InvulnT = Math.Max(p.InvulnT, 1f);
                guard.InvulnT = Math.Max(guard.InvulnT, 1f);
            }
            castHits++;
            BroadcastFx(FxKind.Ring, mine, new Rgba(0.55f, 1f, 0.8f), 35f, 0.35f, Sfx.Blink);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.55f, 1f, 0.8f), 35f, 0.35f, Sfx.None);
        }

        /// Host: move a player. Players move themselves, so their own game is told (the host's player directly).
        void MovePlayer(Player q, V2 pos)
        {
            q.Pos = pos;
            q.Disp = pos;
            var w = Proto.Begin(Msg.Teleport);
            w.Write(q.Id);
            w.Write(pos);
            session.Broadcast(Proto.End(w));
        }

        /// Shadow Step (your own game, as you press it): just behind the enemy nearest your cursor, if one is in
        /// reach; otherwise a short step toward the cursor.
        V2 ShadowStepLanding(Player me, V2 aimAt)
        {
            float range = StepRange * me.Stats.StepRangeMult;
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (!IsSpawning(e) && e.Pos.DistanceTo(me.Pos) <= range + e.Radius && (best == null || e.Pos.DistanceSq(aimAt) < best.Pos.DistanceSq(aimAt)))
                    best = e;
            if (best == null)
                return ClampToArena(me.Pos + (aimAt - me.Pos).LimitLength(BlinkRange), me.Def.BodyR);
            V2 away = (best.Pos - me.Pos).IsZero ? V2.FromAngle(me.Aim) : (best.Pos - me.Pos).Normalized();
            return ClampToArena(best.Pos + away * (best.Radius + me.Def.BodyR + 8f), me.Def.BodyR);
        }

        // --- Enemies and bullets --------------------------------------------------------

        readonly List<Minion> clones = new List<Minion>();
        readonly List<Zone> smokes = new List<Zone>();

        /// Where an enemy aims: at the nearest Shadow Clone if there is one, and at the spot it last saw you if
        /// you've vanished.
        V2 NinjaTarget(Enemy e, Player target, V2 tpos)
        {
            Minion best = null;
            foreach (var m in clones)
                if (best == null || m.Pos.DistanceSq(e.Pos) < best.Pos.DistanceSq(e.Pos))
                    best = m;
            if (best != null)
                return best.Pos;
            if (target != null && target.VanishT > 0f)
                return target.VanishFrom;
            return tpos;
        }

        /// Enemy bullets inside a Smoke Bomb move at half speed (on every peer, from the replicated zones).
        float SmokeSpeed(Bullet b)
        {
            if (smokes.Count == 0 || !Defs.IsEnemyShot(b.Kind))
                return 1f;
            foreach (var z in smokes)
                if (b.Pos.DistanceSq(z.Pos) < z.Radius * z.Radius)
                    return SmokeBulletSpeed;
            return 1f;
        }

        // --- Kusarigama and Bo Staff: weapons that move you ---------------------------------------------
        //
        // As with Lunge, the player's own game moves them (LocalWeaponDash) and tells the host (Msg.WeaponDash),
        // which does the damage (HostWeaponDash).

        public const float KusariHalfWidth = 16f, HookTime = 2.5f, ReelMinHook = 0.25f, ReelSpeed = 1100f;  // the thrown chain; how long a hook holds; how soon you can reel
        public const float ReelSlashMult = 2f, ReelSlashGrow = 0.3f, ReelSlashReach = 100f, ReelStun = 0.5f;  // the slash you land with
        public const float VaultDist = 150f, VaultGrow = 30f, VaultSpeed = 900f, VaultCd = 1.2f, VaultMult = 1.5f, VaultWidth = 34f;  // the Bo Staff's vault
        const byte DashVault = 0, DashReel = 1, DashStrike = 2, DashBlink = 3, DashRecall = 4;

        // --- Spirit Fan: push and pull -----------------------------------------------------------

        public const float FanShove = 60f, FanReachSlack = 30f, FanCone = 0.45f;

        /// The Spirit Fan's gust shoves enemies in front of you away (shove > 0) or draws them in (shove < 0, never
        /// into you). Bosses and heavies don't budge.
        void FanEnemies(Player p, float aim, float reach, float shove)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f || Heavy(e))
                    continue;
                float d = e.Pos.DistanceTo(p.Pos);
                if (d - e.Radius > reach || AngleTo(p.Pos, e.Pos, aim) > FanCone + (d > 1f ? (float)Math.Asin(Math.Min(1f, e.Radius / d)) : MathX.Pi))
                    continue;
                V2 away = d > 1f ? (e.Pos - p.Pos) / d : V2.FromAngle(aim);
                float to = Math.Max(e.Radius + p.Def.BodyR + 6f, d + shove);
                e.Pos = ClampToArena(p.Pos + away * to, e.Radius);
            }
        }

        // --- Ofuda: wards ---------------------------------------------------------------------

        public const float WardRange = 460f, WardTrigger = 26f, WardBurst = 70f, WardHeal = 12f, WardHealReach = 110f, WardLast = 10f;
        public const int WardCap = 5;

        /// An Ofuda charm landed: it lies there as a ward (your oldest goes if you have WardCap).
        void PlaceWard(Bullet b)
        {
            Player p;
            if (!Players.TryGetValue(b.Owner, out p))
                return;
            Zone oldest = null;
            int mine = 0;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.OfudaWard && z.Owner == p.Id)
                {
                    mine++;
                    if (oldest == null || z.T < oldest.T)
                        oldest = z;
                }
            if (mine >= WardCap && oldest != null)
                Zones.Remove(oldest.Id);
            var w = AddZone(ZoneKind.OfudaWard, p, ClampToArena(b.Pos, 10f), WardTrigger, WardLast);
            w.DmgMult = b.Dmg / Defs.WeaponOf(WeaponId.Ofuda).Damage;  // the charm's damage, upgrades and all
        }

        /// A ward an enemy stepped on bursts: damage round it, and healing for allies near it.
        void BurstWard(Zone z)
        {
            float r = WardBurst * (Players.ContainsKey(z.Owner) ? AreaMult(Players[z.Owner]) : 1f);
            float dmg = Defs.WeaponOf(WeaponId.Ofuda).Damage * z.DmgMult;
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(z.Pos) < r + e.Radius)
                    HurtEnemy(e, dmg, z.Owner);
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.DistanceTo(z.Pos) < WardHealReach)
                    Heal(q, WardHeal, PlayerOrNull(z.Owner));
            BroadcastFx(FxKind.Boom, z.Pos, z.Def.Color, r, 0.35f, Sfx.SpellHoly);
        }

        // --- Shuriken: stars that stick, recalled -------------------------------------------------

        public const float StarStick = 5f, RecallSpeed = 700f, RecallBack = 0.5f;  // stuck this long; recalled stars fly back this fast, hitting what they pass for this share
        public const int StarCap = 15;

        class StuckStar
        {
            public int Enemy, Owner;
            public float Dmg, T;
        }

        readonly List<StuckStar> stuckStars = new List<StuckStar>();

        void StarStuck(Bullet b, Enemy e)
        {
            if (!b.Sticks)
                return;
            int mine = 0;
            foreach (var st in stuckStars)
                if (st.Owner == b.Owner)
                    mine++;
            if (mine >= StarCap)
                return;
            stuckStars.Add(new StuckStar { Enemy = e.Id, Owner = b.Owner, Dmg = b.Dmg, T = StarStick });
        }

        void TickStuckStars(float dt)
        {
            foreach (var e in Enemies.Values)
                e.StarsStuck = 0;
            for (int i = stuckStars.Count - 1; i >= 0; i--)
            {
                var st = stuckStars[i];
                Enemy e;
                if ((st.T -= dt) <= 0f || !Enemies.TryGetValue(st.Enemy, out e) || e.Hp <= 0f)
                {
                    stuckStars.RemoveAt(i);
                    continue;
                }
                e.StarsStuck++;
            }
        }

        /// Recalled: every star you have stuck in enemies rips out (hitting again) and flies back to you, cutting
        /// through whatever it passes.
        void RecallStars(Player p)
        {
            int n = 0;
            for (int i = stuckStars.Count - 1; i >= 0; i--)
            {
                var st = stuckStars[i];
                if (st.Owner != p.Id)
                    continue;
                stuckStars.RemoveAt(i);
                Enemy e;
                if (!Enemies.TryGetValue(st.Enemy, out e) || e.Hp <= 0f)
                    continue;
                HurtEnemy(e, st.Dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Owner = p.Id, Dmg = st.Dmg, Kind = Defs.StarShot }, e);
                V2 to = p.Pos - e.Pos;
                float dist = Math.Max(1f, to.Length);
                var b = SpawnBullet(Defs.StarShot, e.Pos, to / dist * RecallSpeed / p.Stats.ShotSpeedMult, 0f, dist / RecallSpeed, st.Dmg * RecallBack, p.Id);
                b.Pierce += 99;
                b.LastHit = e.Id;
                n++;
            }
            if (n > 0)
                BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.StarShot), 30f, 0.25f, Sfx.SwingLight);
        }

        // --- Medicine Darts: stacking doses --------------------------------------------------------

        public const float DoseHeal = 2f, DoseTime = 3f, PoisonDps = 2f, PoisonTime = 3f, DoseStacks = 5f;  // a second, per dart; each dart renews it, stacking to DoseStacks

        /// A Medicine Dart through a hurt ally: another dose of healing over time (it doesn't go on).
        bool DoseAlly(Bullet b)
        {
            foreach (var q in Players.Values)
            {
                float r = q.Def.BodyR + b.Radius;
                if (q.Id == b.Owner || q.Downed || q.Hp >= q.MaxHp || b.Pos.DistanceSq(q.Pos) >= r * r)
                    continue;
                float each = DoseHeal * b.Dmg / Defs.WeaponOf(WeaponId.MedicineDarts).Damage;  // damage upgrades dose harder too
                q.DoseHps = Math.Min(q.DoseHps + each, each * DoseStacks);
                q.DoseT = DoseTime;
                q.DoseBy = b.Owner;
                return true;
            }
            return false;
        }

        /// A Medicine Dart in an enemy: another dose of poison.
        void PoisonHit(Bullet b, Enemy e)
        {
            if (!b.Medicine)
                return;
            float each = PoisonDps * b.Dmg / Defs.WeaponOf(WeaponId.MedicineDarts).Damage;
            e.PoisonDps = Math.Min(e.PoisonDps + each, each * DoseStacks);
            e.PoisonT = PoisonTime;
            e.PoisonBy = b.Owner;
        }

        void TickPoison(Enemy e, float dt)
        {
            if (e.PoisonT <= 0f)
                return;
            if ((e.PoisonT -= dt) <= 0f)
                e.PoisonDps = 0f;
            else if (e.SpawnT <= 0f)
                DotDamage(e, e.PoisonDps * dt, e.PoisonBy);
        }

        // --- Kunai: the shadow kunai ---------------------------------------------------------------

        public const int KunaiShadowEvery = 4;
        public const float KunaiAmbush = 1.5f;  // after a blink, your next hit within this long lands double
        public const float KunaiMarkTime = 3f, BlinkTapGap = 0.3f;  // the mark lasts this long; two fire presses this close blink you

        /// A shadow kunai hit something or ran out: its mark is where you can blink to.
        void ShadowKunaiLanded(Bullet b, V2 at)
        {
            Player p;
            if (!b.ShadowKunai || !Players.TryGetValue(b.Owner, out p))
                return;
            p.KunaiMark = ClampToArena(at, p.Def.BodyR);
            p.KunaiMarkT = KunaiMarkTime;
        }

        float sinceTap = 99f;

        bool Hooked(Player p)
        {
            Enemy e;
            return p.HookT > 0f && Enemies.TryGetValue(p.HookId, out e) && e.Hp > 0f;
        }

        /// The Kusarigama's throw: the chain-sickle flies straight out and hooks the first enemy in its path.
        void ThrowKusarigama(Player p, float aim, float reach, float dmg)
        {
            reach = MeleeReach(p, reach);
            V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
            Enemy first = null;
            float firstAlong = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                V2 rel = e.Pos - p.Pos;
                float along = Dot(rel, dir), across = Math.Abs(Dot(rel, side));
                if (along < 0f || along > reach + e.Radius || across > KusariHalfWidth + e.Radius || along >= firstAlong)
                    continue;
                first = e;
                firstAlong = along;
            }
            p.Run.ShotsFired++;
            V2 end = first != null ? first.Pos : p.Pos + dir * reach;
            BroadcastFx(FxKind.ChainThrow, p.Pos, end, Steel, first != null ? 1f : 0f, 0.25f, Sfx.SwingLight);
            if (first == null)
                return;
            if (BlocksShot(first, p.Pos))
            {
                WearShield(first, dmg);
                return;
            }
            p.Run.ShotsHit++;
            HurtEnemy(first, dmg, p.Id);
            OnBulletHit(new Bullet { Pos = first.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, first);
            p.HookId = first.Id;
            p.HookT = HookTime;
        }

        static readonly Rgba Steel = new Rgba(0.75f, 0.78f, 0.85f);

        // Local: this machine's player's weapon dashes.
        float vaultCd, reelCd;
        bool wasFiring;

        /// Your own game, every tick: the Bo Staff vaults when you click fire while moving; the Kusarigama reels you
        /// in to the enemy it's hooked in when you fire. Returns the dash it started, if any.
        void LocalWeaponDash(Player me, V2 move, bool firing, float dt)
        {
            vaultCd -= dt;
            reelCd -= dt;
            sinceTap += dt;
            bool pressed = firing && !wasFiring;
            wasFiring = firing;
            bool doubleTap = pressed && sinceTap < BlinkTapGap;
            if (pressed)
                sinceTap = 0f;
            if (me.Downed || DashT > 0f || me.StunT > 0f)
                return;
            if (me.Weapon.Id == WeaponId.Shuriken && doubleTap)
            {
                sinceTap = 99f;
                if (IsHost)
                    HostWeaponDash(MyId, DashRecall, me.Pos);  // no move: the stars come back to you
                else
                {
                    var rw = Proto.Begin(Msg.WeaponDash);
                    rw.Write(DashRecall);
                    rw.Write(me.Pos);
                    session.SendToHost(Proto.End(rw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.Kunai && doubleTap && me.KunaiMarkT > 0f)
            {
                // Blink to the shadow kunai's mark (like Blink, at once); the host readies a double-damage hit.
                V2 mark = me.KunaiMark;
                AddFx(FxKind.Ring, me.Pos, new Rgba(0.45f, 0.4f, 0.65f), 26f, 0.3f);
                me.Pos = mark;
                sinceTap = 99f;
                if (IsHost)
                    HostWeaponDash(MyId, DashBlink, mark);  // (it uses up the mark; on the host your player is the host's own)
                else
                {
                    var bw = Proto.Begin(Msg.WeaponDash);
                    bw.Write(DashBlink);
                    bw.Write(mark);
                    session.SendToHost(Proto.End(bw));
                }
                me.KunaiMarkT = 0f;  // used (the host's next snapshot agrees)
                return;
            }
            V2 target;
            byte kind;
            if (me.Weapon.Id == WeaponId.BoStaff && pressed && !move.IsZero && vaultCd <= 0f)
            {
                DashDir = move.Normalized();
                target = ClampToArena(me.Pos + DashDir * (VaultDist + VaultGrow * ExtraShots(me)), me.Def.BodyR);
                dashSpeed = VaultSpeed;
                vaultCd = VaultCd;
                kind = DashVault;
            }
            else if (me.Weapon.Id == WeaponId.TwinBlades && firing && me.Combo >= ComboMax && reelCd <= 0f)
            {
                DashDir = V2.FromAngle(me.Aim);
                target = ClampToArena(me.Pos + DashDir * DashStrikeDist, me.Def.BodyR);
                dashSpeed = DashStrikeSpeed;
                reelCd = 0.4f;  // (the host resets the combo; don't fire twice before that arrives)
                kind = DashStrike;
            }
            else if (me.Weapon.Id == WeaponId.Kusarigama && firing && me.HookT > 0f && me.HookT < HookTime - ReelMinHook && reelCd <= 0f)
            {
                Enemy e;
                if (!Enemies.TryGetValue(me.HookId, out e))
                    return;
                V2 to = e.Pos - me.Pos;
                float stop = e.Radius + me.Def.BodyR + 6f;
                if (to.Length <= stop)
                    target = me.Pos;
                else
                    target = ClampToArena(e.Pos - to.Normalized() * stop, me.Def.BodyR);
                DashDir = (target - me.Pos).IsZero ? V2.FromAngle(me.Aim) : (target - me.Pos).Normalized();
                dashSpeed = ReelSpeed;
                reelCd = 0.6f;
                kind = DashReel;
            }
            else
                return;
            DashT = target.DistanceTo(me.Pos) / dashSpeed;
            if (IsHost)
                HostWeaponDash(MyId, kind, target);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(kind);
                w.Write(target);
                session.SendToHost(Proto.End(w));
            }
        }

        /// Host: a player's weapon moved them to `target`. A vault strikes everything along the way; a reel lands a
        /// heavy slash on the hooked enemy when you arrive (and lets go of it).
        void HostWeaponDash(int id, int kind, V2 target)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Downed)
                return;
            var w = p.Weapon;
            if (kind == DashRecall)
            {
                if (w.Id == WeaponId.Shuriken)
                    RecallStars(p);
                return;
            }
            if (kind == DashBlink)
            {
                if (w.Id != WeaponId.Kunai || p.KunaiMarkT <= 0f || p.KunaiMark.DistanceTo(target) > 40f)
                    return;
                p.KunaiMarkT = 0f;
                p.AmbushT = Math.Max(p.AmbushT, KunaiAmbush);  // the next hit lands double (NinjaHitMult)
                p.InvulnT = Math.Max(p.InvulnT, 0.2f);
                BroadcastFx(FxKind.Ring, target, new Rgba(0.45f, 0.4f, 0.65f), 30f, 0.3f, Sfx.Blink);
                return;
            }
            float dmg = w.Damage * p.Stats.DmgMult;
            V2 from = p.Pos, path = target - from;
            float len = Math.Max(1f, path.Length);
            V2 dir = path / len;
            if (kind == DashVault && w.Id == WeaponId.BoStaff || kind == DashStrike && w.Id == WeaponId.TwinBlades && p.Combo >= ComboMax)
            {
                bool strike = kind == DashStrike;
                float time = len / (strike ? DashStrikeSpeed : VaultSpeed), mult = strike ? DashStrikeMult : VaultMult, width = strike ? DashStrikeWidth : VaultWidth;
                p.InvulnT = Math.Max(p.InvulnT, time + 0.1f);  // springing over them, or cutting through
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f)
                        continue;
                    float along = Math.Max(0f, Math.Min(len, Dot(e.Pos - from, dir)));
                    if (e.Pos.DistanceTo(from + dir * along) > e.Radius + width)
                        continue;
                    HurtEnemy(e, dmg * mult, p.Id);
                    OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg * mult, Kind = Defs.KnightSlash }, e);
                }
                if (strike)
                    p.Combo = 0;
                BroadcastFx(FxKind.Slash, from, target, Defs.BulletColor(Defs.KnightSlash), 0.12f, 0.2f, Sfx.SwingHeavy);
            }
            else if (kind == DashReel && w.Id == WeaponId.Kusarigama && Hooked(p))
            {
                Enemy e = Enemies[p.HookId];
                float time = len / ReelSpeed;
                p.InvulnT = Math.Max(p.InvulnT, time + 0.15f);
                var s = NewSwing(p, (e.Pos - from).Angle, ReelSlashReach, 160f, dmg * ReelSlashMult * (1f + ReelSlashGrow * ExtraShots(p)));
                s.Delay = time + 0.03f;  // lands once you've arrived (the swing hits from where you are then)
                s.Stun = ReelStun;
                s.Heavy = true;
                s.NoCut = true;
                pendingSwings.Add(s);
                p.HookT = 0f;
                p.HookId = 0;
                BroadcastFx(FxKind.ChainThrow, from, e.Pos, Steel, 1f, 0.15f, Sfx.None);
            }
        }

        /// Called by MoveBullets before it moves anything: gather the smoke, and turn chain shuriken around.
        void PrepareNinjaBullets(float dt)
        {
            smokes.Clear();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Smoke)
                    smokes.Add(z);
            foreach (var b in Bullets.Values)
                if (b.Kind == Defs.FumaStar)
                    FumaTurn(b, dt);
        }

        /// A Fuma Shuriken's loop is an oval through where it was thrown: at a time t into the lap (angle a = Tau t / lap)
        /// its velocity is M(a) V0, where V0 is the sideways velocity it set off with and M(a) = cos a + k sin a R turns it
        /// toward the front (R: a quarter turn, the way its AngVel's sign says; k: how much longer the oval is than wide).
        /// M is a scaled rotation, so V0 comes back from the velocity it has now: host and clients follow the same oval
        /// from what they already know. (Its AngVel is only that sign: MoveBullets doesn't turn it.)
        void FumaTurn(Bullet b, float dt)
        {
            float lap = Math.Max(0.01f, b.Life0), k = FumaReach * 0.5f / FumaWidth, s = b.AngVel >= 0f ? 1f : -1f;
            float a0 = MathX.Tau * (lap - b.Life) / lap, a1 = MathX.Tau * (lap - b.Life + dt) / lap;
            float c = (float)Math.Cos(a0), sn = k * (float)Math.Sin(a0);
            V2 v0 = (b.Vel * c - b.Vel.Orthogonal() * (s * sn)) / (c * c + sn * sn);
            float c1 = (float)Math.Cos(a1), sn1 = k * (float)Math.Sin(a1);
            b.Vel = v0 * c1 + v0.Orthogonal() * (s * sn1);
            if (a0 < MathX.Pi && a1 >= MathX.Pi)
                b.LastHit = 0;  // at the far end: it can cut the same enemy again on the way back
        }

        /// Ofuda charms heal allies near where they burst.
        void CharmHeal(Bullet b)
        {
            Player owner;
            Players.TryGetValue(b.Owner, out owner);
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.DistanceTo(b.Pos) < b.Splash + 40f)
                    Heal(q, b.HealBurst, owner);
        }

        // --- Timed effects ---------------------------------------------------------------

        /// Host, every tick: Vanish and Iron Body run down (Iron Body ends in a shockwave), Blossom Storm heals
        /// and clears bullets, and Shadow Clones about to end burst.
        void TickNinjas(float dt)
        {
            foreach (var p in Players.Values)  // Medicine Darts' doses heal; a Kunai mark fades
            {
                if (p.DoseT > 0f)
                {
                    p.DoseT = p.Downed ? 0f : Math.Max(0f, p.DoseT - dt);
                    if (p.DoseT > 0f)
                        Heal(p, p.DoseHps * dt, PlayerOrNull(p.DoseBy));
                    else
                        p.DoseHps = 0f;
                }
                p.KunaiMarkT = Math.Max(0f, p.KunaiMarkT - dt);
            }
            foreach (var p in Players.Values)  // a Kusarigama hook lets go in time, or when you go down
                if (p.HookT > 0f && ((p.HookT -= dt) <= 0f || p.Downed || !Hooked(p)))
                {
                    p.HookT = 0f;
                    p.HookId = 0;
                }
            clones.Clear();
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.Clone)
                    clones.Add(m);
            foreach (var p in Players.Values)
            {
                p.VanishT = Math.Max(0f, p.VanishT - dt);
                p.AmbushT = Math.Max(0f, p.AmbushT - dt);
                if (p.Downed)
                    p.VanishT = p.AmbushT = p.IronT = 0f;
                if (p.IronT > 0f)
                {
                    p.IronT -= dt;
                    if (p.IronT <= 0f)
                    {
                        p.IronT = 0f;
                        foreach (var e in Enemies.Values)
                            if (e.SpawnT <= 0f && e.Pos.DistanceTo(p.Pos) < IronWaveRadius + e.Radius)
                                HurtEnemy(e, IronWaveDamage * p.Stats.DmgMult * p.IronPower, p.Id);
                        if (p.IronMastered)
                            EraseEnemyBullets(p.Pos, 160f);
                        BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.8f, 0.82f, 0.9f), IronWaveRadius, 0.35f, Sfx.Shockwave);
                    }
                }
                if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BlossomStorm)
                    foreach (var q in Players.Values)
                    {
                        if (q.Downed)
                            continue;
                        Heal(q, BlossomHeal * dt, p);
                        EraseEnemyBullets(q.Pos, BlossomClear);
                    }
            }
            foreach (var m in clones)
            {
                if (m.T < 0f || m.T > dt)
                    continue;
                Player owner;
                if (!Players.TryGetValue(m.Owner, out owner))
                    continue;
                foreach (var e in Enemies.Values)
                    if (e.SpawnT <= 0f && e.Pos.DistanceTo(m.Pos) < CloneBurstRadius + e.Radius)
                        HurtEnemy(e, CloneBurst * m.Power, m.Owner);
                BroadcastFx(FxKind.Boom, m.Pos, Kits.Get(MinionKind.Clone).Color, CloneBurstRadius, 0.35f, Sfx.Explode);
            }
        }

        /// Iron Body's damage cut (applied in Damage).
        static float IronBodyCut(Player p) => p.IronT > 0f ? IronCut : 0f;
    }
}
