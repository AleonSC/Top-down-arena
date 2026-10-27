using System;
using System.Collections.Generic;

namespace Arena
{
    /// Ultimates (F): one per role and class, on a long cooldown (host only).
    public partial class Game
    {
        bool Storming(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BulletStorm;

        void EraseEnemyBullets(V2 center, float range)
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
        }

        void Ultimate(Player p, AbilityId id, V2 target)
        {
            float dmg = p.Stats.DmgMult;
            switch (id)
            {
                case AbilityId.Fortress:  // Tank: Soldier
                    p.UltT = 8f;
                    p.TauntT = Math.Max(p.TauntT, 8f);
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = 8f;
                        if (e.SpawnT <= 0f && !Heavy(e) && e.Pos.DistanceTo(p.Pos) < 200f + e.Radius)
                            e.Pos = ClampToArena(e.Pos + (e.Pos - p.Pos).Normalized() * 120f, e.Radius);
                    }
                    EraseEnemyBullets(p.Pos, 220f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.8f, 0.35f), 220f, 0.6f, Sfx.Roar);
                    break;

                case AbilityId.GlacialTomb:  // Tank: Mage
                    EraseEnemyBullets(p.Pos, 99999f);
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f)
                            continue;
                        if (e.Def.IsBoss)
                            HurtEnemy(e, 250f * dmg, p.Id);
                        else
                        {
                            HurtEnemy(e, 40f * dmg, p.Id);
                            e.FrozenT = 4f;
                            e.SlowT = Math.Max(e.SlowT, 4f);
                        }
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.FrostShot), 900f, 0.8f, Sfx.Freeze);
                    break;

                case AbilityId.Titan:  // Tank: Summoner
                    SpawnMinion(MinionKind.Titan, p, p.Pos + V2.FromAngle(p.Aim) * 60f, 15f);
                    BroadcastFx(FxKind.Boom, p.Pos + V2.FromAngle(p.Aim) * 60f, Kits.Get(MinionKind.Titan).Color, 90f, 0.5f, Sfx.Explode);
                    Sounds.Add(Sfx.Summon);
                    break;

                case AbilityId.Lifeline:  // Healer: Soldier
                    foreach (var q in Players.Values)
                    {
                        if (q.Downed)
                            RevivePlayer(q, 1f, p);
                        Heal(q, q.MaxHp, p);
                        q.InvulnT = Math.Max(q.InvulnT, 3f);
                        BroadcastFx(FxKind.Ring, q.Pos, new Rgba(0.4f, 1f, 0.55f), 90f, 0.6f, Sfx.None);
                    }
                    Sounds.Add(Sfx.Ability);
                    break;

                case AbilityId.Sanctum:  // Healer: Mage
                    EraseEnemyBullets(p.Pos, 200f);
                    AddZone(ZoneKind.Sanctum, p, p.Pos, 200f, 7f);
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Sanctum).Color, 200f, 0.6f, Sfx.SpellHoly);
                    break;

                case AbilityId.WorldTree:  // Healer: Summoner
                    AddZone(ZoneKind.Grove, p, p.Pos, 170f, 10f);
                    for (int i = 0; i < 4; i++)
                        SpawnMinion(MinionKind.Sprite, p, p.Pos + V2.FromAngle(MathX.Tau * i / 4f) * 30f, 10f).HealRate = 10f;
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Grove).Color, 170f, 0.6f, Sfx.Summon);
                    break;

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
                    for (int i = 0; i < 12; i++)
                    {
                        V2 at = targets.Count > 0
                            ? targets[(i + rng.Next(targets.Count)) % targets.Count].Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(0f, 30f)
                            : target + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(0f, 200f);
                        AddZone(ZoneKind.Meteor, p, ClampToArena(at, 10f), 90f, 0.7f + 0.2f * i);
                    }
                    Sounds.Add(Sfx.SpellFire);
                    break;
                }

                case AbilityId.Legion:  // DPS: Summoner
                    for (int i = 0; i < 6; i++)
                    {
                        V2 d = V2.FromAngle(MathX.Tau * i / 6f);
                        SpawnMinion(MinionKind.Wolf, p, p.Pos + d * 30f, 10f);
                        SpawnMinion(MinionKind.Bat, p, p.Pos + d * 18f, 10f);
                    }
                    p.FrenzyT = Math.Max(p.FrenzyT, 10f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.5f, 0.3f), 140f, 0.5f, Sfx.Roar);
                    break;
            }
        }
    }
}
