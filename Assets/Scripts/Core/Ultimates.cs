using System;
using System.Collections.Generic;

namespace Arena
{
    /// Ultimates (F): one per role and class, on a long cooldown (host only).
    public partial class Game
    {
        bool Storming(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BulletStorm;

        /// Returns how many it erased; during a cast, every 5 count as a hit for mastery.
        int EraseEnemyBullets(V2 center, float range)
        {
            var erase = new List<int>();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(center) < range)
                    erase.Add(b.Id);
            foreach (int bid in erase)
            {
                Bullets.Remove(bid);
                pendingRemoved.Add(bid);
            }
            if (casting)
                castHits += (erase.Count + 4) / 5;
            return erase.Count;
        }

        /// The Soldier, Mage and Summoner ultimates. Their cooldowns aren't shortened by cooldown upgrades. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] UltimateAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.Fortress, Kit = Kit.Soldier, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Fortress", Cooldown = 70f, Desc = "For 8 s every enemy targets you and you take 75% less damage. Erases nearby bullets and shoves enemies away.",
                Capstone = "Lasts 12 s.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.GlacialTomb, Kit = Kit.Mage, Role = Role.Tank, Choice = AbilityDef.Ult, Instant = true,
                Name = "Glacial Tomb", Cooldown = 75f, Desc = "Erase every enemy bullet and freeze every enemy solid for 4 s (bosses take 250 damage instead).",
                Capstone = "Enemies stay frozen for 6 s and bosses take 500.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Titan, Kit = Kit.Summoner, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Titan", Cooldown = 80f, Desc = "Summon a stone titan for 15 s that smashes enemies and blocks bullets (it soaks 1000 boss damage).",
                Capstone = "The titan is bigger and stays for 25 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Lifeline, Kit = Kit.Soldier, Role = Role.Healer, Choice = AbilityDef.Ult, Instant = true,
                Name = "Lifeline", Cooldown = 90f, Desc = "Instantly revive and fully heal the whole party, with 3 s of invulnerability.",
                Capstone = "Everyone also gets 4 s of Overdrive.",
                Bot = b => b.AllyDown || b.Me.Hp < b.Me.MaxHp * 0.3f,
            },
            new AbilityDef
            {
                Id = AbilityId.Sanctum, Kit = Kit.Mage, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Sanctum", Cooldown = 80f, Desc = "A holy dome for 7 s: allies inside heal fast and can't go down, and bullets can't get in.",
                Capstone = "The dome is half again as wide (300 px).",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.WorldTree, Kit = Kit.Summoner, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "World Tree", Cooldown = 80f, Desc = "Grow a great tree for 10 s that heals allies, entangles enemies around it and sends out healing sprites.",
                Capstone = "Lasts 16 s with 8 sprites.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.BulletStorm, Kit = Kit.Soldier, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Bullet Storm", Cooldown = 60f, Desc = "For 6 s you fire three times as fast, with +2 extra projectiles.",
                Capstone = "+4 extra projectiles instead of +2.",
            },
            new AbilityDef
            {
                Id = AbilityId.MeteorStorm, Kit = Kit.Mage, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Meteor Storm", Cooldown = 70f, Desc = "Twelve meteors rain down on enemies over 3 s, 150 damage each.",
                Capstone = "Twenty meteors.",
            },
            new AbilityDef
            {
                Id = AbilityId.Legion, Kit = Kit.Summoner, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Legion", Cooldown = 70f, Desc = "Summon 6 wolves and 6 bats for 10 s, and your other minions frenzy.",
                Capstone = "Summons 10 wolves and 10 bats.",
            },
        };

        void Ultimate(Player p, AbilityId id, V2 target)
        {
            float dmg = p.Stats.DmgMult;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                case AbilityId.Fortress:  // Tank: Soldier
                {
                    float time = cap ? 12f : 8f;
                    p.UltT = time;
                    p.TauntT = Math.Max(p.TauntT, time);
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = time;
                        if (e.SpawnT <= 0f && !Heavy(e) && e.Pos.DistanceTo(p.Pos) < 200f + e.Radius)
                            e.Pos = ClampToArena(e.Pos + (e.Pos - p.Pos).Normalized() * 120f, e.Radius);
                    }
                    EraseEnemyBullets(p.Pos, 220f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.8f, 0.35f), 220f, 0.6f, Sfx.Roar);
                    break;
                }

                case AbilityId.GlacialTomb:  // Tank: Mage
                    EraseEnemyBullets(p.Pos, 99999f);
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f)
                            continue;
                        if (e.Def.IsBoss)
                            HurtEnemy(e, (cap ? 500f : 250f) * dmg, p.Id);
                        else
                        {
                            HurtEnemy(e, 40f * dmg, p.Id);
                            e.FrozenT = cap ? 6f : 4f;
                            e.SlowT = Math.Max(e.SlowT, e.FrozenT);
                        }
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.FrostShot), 900f, 0.8f, Sfx.Freeze);
                    break;

                case AbilityId.Titan:  // Tank: Summoner
                {
                    var titan = SpawnMinion(MinionKind.Titan, p, p.Pos + V2.FromAngle(p.Aim) * 60f, cap ? 25f : 15f);
                    if (cap)
                    {
                        titan.Scale = 1.3f;
                        titan.BlockHp *= 1.3f;
                    }
                    BroadcastFx(FxKind.Boom, p.Pos + V2.FromAngle(p.Aim) * 60f, Kits.Get(MinionKind.Titan).Color, 90f, 0.5f, Sfx.Explode);
                    Sounds.Add(Sfx.Summon);
                    break;
                }

                case AbilityId.Lifeline:  // Healer: Soldier
                    foreach (var q in Players.Values)
                    {
                        if (q.Downed)
                            RevivePlayer(q, 1f, p);
                        Heal(q, q.MaxHp, p);
                        q.InvulnT = Math.Max(q.InvulnT, 3f);
                        if (cap)
                            q.OverdriveT = Math.Max(q.OverdriveT, 4f);
                        BroadcastFx(FxKind.Ring, q.Pos, new Rgba(0.4f, 1f, 0.55f), 90f, 0.6f, Sfx.None);
                    }
                    Sounds.Add(Sfx.Ability);
                    break;

                case AbilityId.Sanctum:  // Healer: Mage
                {
                    float r = cap ? 300f : 200f;
                    EraseEnemyBullets(p.Pos, r);
                    AddZone(ZoneKind.Sanctum, p, p.Pos, r, 7f);
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Sanctum).Color, r, 0.6f, Sfx.SpellHoly);
                    break;
                }

                case AbilityId.WorldTree:  // Healer: Summoner
                {
                    float time = cap ? 16f : 10f;
                    int sprites = cap ? 8 : 4;
                    AddZone(ZoneKind.Grove, p, p.Pos, 170f, time);
                    for (int i = 0; i < sprites; i++)
                        SpawnMinion(MinionKind.Sprite, p, p.Pos + V2.FromAngle(MathX.Tau * i / sprites) * 30f, time).HealRate = 10f * pw;
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Grove).Color, 170f, 0.6f, Sfx.Summon);
                    break;
                }

                case AbilityId.BulletStorm:  // DPS: Soldier
                    p.UltT = 6f;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.6f, 0.25f), 80f, 0.4f, Sfx.Ability);
                    break;

                case AbilityId.MeteorStorm:  // DPS: Mage
                {
                    var targets = new List<Enemy>();
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && e.Def.Xp > 0)
                            targets.Add(e);
                    int meteors = cap ? 20 : 12;
                    for (int i = 0; i < meteors; i++)
                    {
                        V2 at = targets.Count > 0
                            ? targets[(i + rng.Next(targets.Count)) % targets.Count].Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(0f, 30f)
                            : target + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(0f, 200f);
                        AddZone(ZoneKind.Meteor, p, ClampToArena(at, 10f), 90f, 0.7f + 0.2f * i * 12f / meteors);
                    }
                    Sounds.Add(Sfx.SpellFire);
                    break;
                }

                case AbilityId.Legion:  // DPS: Summoner
                    int pack = cap ? 10 : 6;
                    for (int i = 0; i < pack; i++)
                    {
                        V2 d = V2.FromAngle(MathX.Tau * i / pack);
                        SpawnMinion(MinionKind.Wolf, p, p.Pos + d * 30f, 10f).NoFrenzy = true;
                        SpawnMinion(MinionKind.Bat, p, p.Pos + d * 18f, 10f).NoFrenzy = true;
                    }
                    p.FrenzyT = Math.Max(p.FrenzyT, 10f);  // your other minions frenzy; the pack itself doesn't
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.5f, 0.3f), 140f, 0.5f, Sfx.Roar);
                    break;
            }
        }
    }
}
