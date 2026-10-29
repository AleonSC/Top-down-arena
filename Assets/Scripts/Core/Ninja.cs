using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Ninja: quick and lightly protected (85% health, 12% faster), with thrown blades and fast melee for
    /// every role. Tanks misdirect (clones, smoke, Iron Body), healers keep the party up from close by (mist,
    /// swapping places, wards), and DPS strike from the shadows (Shadow Step, Vanish's ambush, Blade Fan).
    public partial class Game
    {
        const float KusariPull = 40f, KatanaLungeReach = 170f;  // (each weapon's main reach, speed and damage are in Defs)
        public const float ChainStarOut = 0.42f;  // a Chain Shuriken flies out this long, then comes back as long
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
                {
                    var s = NewSwing(p, aim, w.Reach + w.Grow * extra, 120f, w.Damage * dmg);
                    s.Knock = -(KusariPull + 15f * extra);  // the chain yanks them in
                    s.NoCut = true;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.BoStaff:
                    pendingSwings.Add(NewSwing(p, aim, w.Reach + w.Grow * extra, 360f, w.Damage * dmg));
                    break;
                case WeaponId.ChainShuriken:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float a = aim + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * 0.3f * (n - 1));
                        SpawnBullet(Defs.ChainStar, muzzle, V2.FromAngle(a) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Pierce += 99;
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
                        b.HealAlly = 4f * 9f / 7f;  // HealAllyHit scales it by Dmg / 9 (so damage upgrades heal more too)
                    }
                    break;
                }
                case WeaponId.SpiritFan:
                {
                    // A gust: five short-lived puffs that heal allies they pass, and small enemy bullets in front blown back.
                    float reach = 1f + w.Grow * extra;
                    for (int i = 0; i < 5; i++)
                    {
                        var b = SpawnBullet(Defs.GustShot, muzzle, V2.FromAngle(aim + (i / 4f - 0.5f) * 0.7f) * w.Speed, 0f, w.Life * reach, w.Damage * dmg, p.Id);
                        b.Pierce += 99;
                        b.HealAlly = (3f + extra) * 9f / 6f;
                    }
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
                    for (int i = 0; i < n; i++)
                    {
                        float a = aim + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * 0.4f * (n - 1));
                        var b = SpawnBullet(Defs.OfudaShot, muzzle, V2.FromAngle(a) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id, false, 3f);
                        b.Splash = 60f * AreaMult(p);
                        b.HealBurst = 8f;
                    }
                    break;
                }
                case WeaponId.Shuriken:
                {
                    int n = 3 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.StarShot, muzzle, V2.FromAngle(aim + (i / (n - 1f) - 0.5f) * 0.22f * (n - 1)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Bounces += 1;
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
                        SpawnBullet(Defs.KunaiShot, muzzle, V2.FromAngle(a) * w.Speed, 0f, w.Life, (i == 0 ? w.Damage : w.Damage * 0.5f) * dmg, p.Id);
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

        /// Called by MoveBullets before it moves anything: gather the smoke, and turn chain shuriken around.
        void PrepareNinjaBullets(float dt)
        {
            smokes.Clear();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Smoke)
                    smokes.Add(z);
            foreach (var b in Bullets.Values)
                if (b.Kind == Defs.ChainStar && b.Life > ChainStarOut && b.Life - dt <= ChainStarOut)
                {
                    b.Vel = -b.Vel;  // halfway: back the way it came
                    b.LastHit = 0;   // and it can cut the same enemy again
                }
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
