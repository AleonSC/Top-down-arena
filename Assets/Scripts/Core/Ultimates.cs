using System;
using System.Collections.Generic;

namespace Arena
{
    /// Ultimates (F): one per role and class, on a long cooldown (host only).
    public partial class Game
    {
        public const float LegionRing = 46f, LegionHold = 1.1f, LegionSpeed = 1.4f;  // Legion: the pack circles you, then runs 40% faster
        public const float BulletRainRadius = 260f, BulletRainWetTime = 3f;  // Bullet Rain's zone: a square, Radius = its half-side (520 px across; the Elementalist DPS ultimate)
        /// Lifeline: seconds of invulnerability, how long its look runs (the 3 s plus a 0.7 s fade, Game/LifelineLook.cs), and the
        /// plain ring and beam it also sends, which that look replaces when Visual effects are on (their durations mark them).
        public const float LifelineInvuln = 3f, LifelineFxTime = 3.7f, LifelinePlainRing = 0.62f, LifelinePlainLink = 0.56f;

        bool Storming(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.ArtilleryAssault;

        /// How long the Glacial Tomb's Ring effect lives: its freeze (4 s, 6 s mastered) and a second for the thaw. The look tells it by this.
        public const float TombFxTail = 1f;
        public static float TombFxTime(bool mastered) => (mastered ? 6f : 4f) + TombFxTail;

        /// Returns how many it erased; during a cast, every 5 count as a hit for mastery.
        readonly List<int> eraseScratch = new List<int>();  // (EraseEnemyBullets runs every tick during Blade Fan and Blossom Storm)

        int EraseEnemyBullets(V2 center, float range)
        {
            var erase = eraseScratch;
            erase.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && b.Pos.CloserThan(center, range))
                    erase.Add(b.Id);
            foreach (int bid in erase)
            {
                KillBullet(bid);
            }
            if (casting)
                castHits += (erase.Count + 4) / 5;
            return erase.Count;
        }

        /// The Soldier, Elementalist and Summoner ultimates. Their cooldowns aren't shortened by cooldown upgrades. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] UltimateAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.Fortress, Kit = Kit.Soldier, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Fortress", Cooldown = 70f, Desc = "A dome only players and their bullets can cross.",
                Capstone = "Lasts 14 s.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.JuggernautSuit, Kit = Kit.Soldier, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Juggernaut Suit", Cooldown = 75f, Desc = "Heavy armour draws every enemy and throws flak back at shooters.",
                Capstone = "450 armour, and each shard does 18.",
                Bot = b => b.Crowd >= Game.JuggBotCrowd,
            },
            new AbilityDef
            {
                Id = AbilityId.GlacialTomb, Kit = Kit.Elementalist, Role = Role.Tank, Choice = AbilityDef.Ult, Instant = true,
                Name = "Glacial Tomb", Cooldown = 75f, Desc = "Erase every enemy bullet and freeze every enemy.",
                Capstone = "Enemies stay frozen for 6 s and bosses take 500.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Titan, Kit = Kit.Summoner, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Titan", Cooldown = 80f, Desc = "Summon a huge stone titan that stomps enemies and blocks bullets.",
                Capstone = "The titan is 30% bigger and stays for 27 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.BonePhalanx, Kit = Kit.Summoner, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Bone Phalanx", Cooldown = 75f, Desc = "Skeletons with tower shields march ahead, blocking shots and spearing enemies.",
                Capstone = "10 skeletons with 180 health each.",
                Bot = b => b.HaveTarget && b.TargetD < 460f || b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Lifeline, Kit = Kit.Soldier, Role = Role.Healer, Choice = AbilityDef.Ult, Instant = true,
                Name = "Lifeline", Cooldown = 90f, Desc = "Revive and fully heal the whole party, with brief invulnerability.",
                Capstone = "Everyone also gets 4 s of Overdrive.",
                Bot = b => b.AllyDown || b.Me.Hp < b.Me.MaxHp * 0.3f,
            },
            new AbilityDef
            {
                Id = AbilityId.FieldHospital, Kit = Kit.Soldier, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Field Hospital", Cooldown = 150f, Desc = "Open a hospital cell behind a door: a shrine, healing orbs and a laser gate for a minute.",
                Capstone = "8 orbs, one back every 3 s.",
                Bot = b => b.Danger || b.AllyHurt && b.Me.Hp < b.Me.MaxHp * 0.6f,
            },
            new AbilityDef
            {
                Id = AbilityId.Sanctum, Kit = Kit.Elementalist, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Sanctum", Cooldown = 80f, Desc = "A dome of light that blocks bullets, keeps allies up and heals them.",
                Capstone = "The dome is half again as wide (300 px).",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.WorldTree, Kit = Kit.Summoner, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "World Tree", Cooldown = 80f, Desc = "Grow a tree that heals and slows around it and sends healing butterflies.",
                Capstone = "Lasts 16 s with 8 sprites.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.ArtilleryAssault, Kit = Kit.Soldier, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Artillery Assault", Cooldown = 60f, Desc = "Fire three times as fast with extra projectiles.",
                Capstone = "+4 extra projectiles instead of +2.",
            },
            new AbilityDef
            {
                Id = AbilityId.AttackHelicopter, Kit = Kit.Soldier, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Attack Helicopter", Cooldown = 70f, Desc = "A gunship locks enemies one at a time and can't be hit.",
                Capstone = "A second gun on the wing: two targets at once.",
            },
            new AbilityDef
            {
                Id = AbilityId.BulletRain, Kit = Kit.Elementalist, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Bullet Rain", Cooldown = 70f, Desc = "A downpour of needles that hurts and soaks everything in a big square.",
                Capstone = "Lasts 9 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Legion, Kit = Kit.Summoner, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Legion", Cooldown = 70f, Desc = "A blood moon: wolves and bats circle you, you run faster and your minions frenzy.",
                Capstone = "Summons 7 wolves and 7 bats.",
            },
        };

        void Ultimate(Player p, AbilityId id, V2 target)
        {
            float dmg = p.Stats.DmgMult;
            bool cap = castMastered;
            switch (id)
            {
                case AbilityId.Fortress:  // Tank: Soldier
                {
                    float time = cap ? DomeTimeMastered : DomeTime;
                    p.UltT = time;
                    p.DomeT = time;
                    p.DomePos = p.Pos;
                    EraseEnemyBullets(p.Pos, DomeRadius + 30f);  // anything already in flight inside is gone
                    KeepOutOfDomes();  // enemies inside are put out at the edge (Dome.cs)
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.8f, 0.35f), DomeRadius, 0.6f, Sfx.Roar);
                    break;
                }

                case AbilityId.JuggernautSuit:  // Tank: Soldier, the second ultimate (Juggernaut.cs)
                    CastJugg(p, cap);
                    break;

                case AbilityId.LiteralTank:  // Tank: Soldier, the third ultimate (LiteralTank.cs)
                    CastTank(p, cap);
                    break;

                case AbilityId.IronArmadillo:  // Tank: Summoner, the third ultimate (IronArmadillo.cs)
                    CastArmadillo(p, cap);
                    break;

                case AbilityId.GlacialTomb:  // Tank: Elementalist
                    EraseEnemyBullets(p.Pos, 99999f);
                    foreach (var e in AllFoes(by: p.Id))
                    {
                        if (e.Def.IsBoss)
                            HurtEnemy(e, (cap ? 500f : 250f) * dmg, p.Id);
                        else
                        {
                            HurtEnemy(e, 40f * dmg, p.Id);
                            e.FrozenT = Math.Max(e.FrozenT, cap ? 6f : 4f);
                            e.SlowT = Math.Max(e.SlowT, e.FrozenT);
                        }
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.FrostShot), 900f, TombFxTime(cap), Sfx.Freeze);  // lasts the freeze plus the thaw; Game/GlacialTombLook.cs draws it
                    break;

                case AbilityId.LightningRod:  // Tank: Elementalist, the second ultimate (LightningRod.cs)
                    CastLightningRod(p, cap);
                    break;

                case AbilityId.StoneColossus:  // Tank: Elementalist, the third ultimate (StoneColossus.cs)
                    CastColossus(p, cap);
                    break;

                case AbilityId.SpiritAvatar:  // Tank: Ninja, the third ultimate (SpiritAvatar.cs)
                    CastSpirit(p, cap);
                    break;

                case AbilityId.Vortex:  // Tank: Elementalist, the fourth ultimate (Vortex.cs)
                    CastVortex(p, cap);
                    break;

                case AbilityId.RodentMinigun:  // DPS: Summoner, the second ultimate
                    CastRodentMinigun(p, cap, castPower);  // RodentMinigun.cs
                    break;
                case AbilityId.MammothFall:  // DPS: Summoner, the third ultimate
                    CastMammothFall(p, target, cap);  // MammothFall.cs
                    break;
                case AbilityId.BloodRavager:  // DPS: Hemomancer, the second ultimate (BloodRavager.cs)
                    CastRavager(p, cap, castPower);
                    break;
                case AbilityId.BloodBoil:  // DPS: Hemomancer, the third ultimate (BloodBoil.cs)
                    CastBoil(p);
                    break;
                case AbilityId.ForbiddenPath:  // DPS: Ninja, the second ultimate (ForbiddenPath.cs)
                    CastForbiddenPath(p, cap);
                    break;
                case AbilityId.StaticBubble:  // Healer: Elementalist, the second ultimate (StaticBubble.cs)
                    CastStaticBubble(p, cap);
                    break;
                case AbilityId.AstralProjection:  // Healer: Ninja, the second ultimate (AstralProjection.cs)
                    CastAstral(p, cap);
                    break;
                case AbilityId.LivingBolt:  // DPS: Elementalist, the second ultimate (LivingBolt.cs)
                    CastLivingBolt(p, cap);
                    break;
                case AbilityId.FlashFire:  // DPS: Elementalist, the third ultimate (FlashFire.cs)
                    CastFlashFire(p, cap);
                    break;

                case AbilityId.DragonsDogma:  // Tank: Ninja, the second ultimate (DragonsDogma.cs)
                    CastDogma(p, cap);
                    break;

                case AbilityId.Titan:  // Tank: Summoner
                    CastTitan(p, cap);  // Titan.cs
                    break;

                case AbilityId.BonePhalanx:  // Tank: Summoner, the second ultimate
                    CastPhalanx(p, target, cap);  // BonePhalanx.cs
                    break;

                case AbilityId.Lifeline:  // Healer: Soldier
                    foreach (var q in Players.Values)
                    {
                        if (q.Downed)
                            RevivePlayer(q, 1f, p);
                        Heal(q, q.MaxHp, p);
                        q.InvulnT = Math.Max(q.InvulnT, LifelineInvuln);
                        if (cap)
                            q.OverdriveT = Math.Max(q.OverdriveT, 4f);
                        if (q != p)
                            BroadcastFx(FxKind.Link, p.Pos, q.Pos, new Rgba(0.4f, 1f, 0.55f), 0f, LifelinePlainLink, Sfx.None);
                        BroadcastFx(FxKind.Ring, q.Pos, new Rgba(0.4f, 1f, 0.55f), 90f, LifelinePlainRing, Sfx.None);
                        BroadcastFx(FxKind.Lifeline, p.Pos, new V2(q.Id, p.Id), new Rgba(0.35f, 1f, 0.55f), q.Pos.DistanceTo(p.Pos), LifelineFxTime, Sfx.None);
                    }
                    Sounds.Add(Sfx.Ability);
                    break;

                case AbilityId.FieldHospital:  // Healer: Soldier, the second ultimate (FieldHospital.cs)
                    CastHospital(p, target, cap);
                    break;

                case AbilityId.SupplyDrop:  // Healer: Soldier, the third ultimate (SupplyDrop.cs)
                    CastSupplyDrop(p, target, cap);
                    break;

                case AbilityId.ArmorDrop:  // Tank: Soldier, the fourth ultimate (ArmorDrop.cs)
                    CastArmorDrop(p, target, cap);
                    break;

                case AbilityId.Sanctum:  // Healer: Elementalist
                {
                    float r = cap ? 300f : 200f;
                    EraseEnemyBullets(p.Pos, r);
                    AddZone(ZoneKind.Sanctum, p, p.Pos, r, 7f);
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Sanctum).Color, r, 0.6f, Sfx.SpellHoly);
                    break;
                }

                case AbilityId.WorldTree:  // Healer: Summoner
                {
                    AddZone(ZoneKind.Grove, p, p.Pos, WorldTreeRadius, cap ? WorldTreeTimeMastered : WorldTreeTime);  // butterflies leave it in waves (WorldTree.cs)
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Grove).Color, WorldTreeRadius, 0.6f, Sfx.Summon);
                    break;
                }

                case AbilityId.ArtilleryAssault:  // DPS: Soldier
                    p.UltT = 6f;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.6f, 0.25f), 140f, 0.6f, Sfx.Ability);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.95f, 0.8f), 85f, 0.45f, Sfx.None);
                    break;

                case AbilityId.AttackHelicopter:  // DPS: Soldier, the second ultimate (AttackHelicopter.cs)
                    CastHeli(p, cap);
                    break;

                case AbilityId.OrbitalLaser:  // DPS: Soldier, the third ultimate (OrbitalLaser.cs)
                    CastOrbital(p, target, cap);
                    break;

                case AbilityId.BulletRain:  // DPS: Elementalist
                {
                    V2 at = ClampToArena(target, BulletRainRadius);
                    AddZone(ZoneKind.BulletRain, p, at, BulletRainRadius, cap ? 9f : 6f);
                    BroadcastFx(FxKind.Ring, at, Kits.Get(ZoneKind.BulletRain).Color, BulletRainRadius, 0.6f, Sfx.SpellFire);
                    break;
                }

                case AbilityId.Legion:  // DPS: Summoner. Blood Moon: the pack circles you, then is let loose, faster than normal
                    int pack = cap ? 7 : 4;
                    for (int i = 0; i < pack; i++)
                    {
                        float a = MathX.Tau * i / pack;
                        var w = SpawnMinion(MinionKind.Wolf, p, p.Pos + V2.FromAngle(a) * LegionRing, 10f);
                        var b = SpawnMinion(MinionKind.Bat, p, p.Pos + V2.FromAngle(a + 0.5f) * (LegionRing * 0.65f), 10f);
                        w.NoFrenzy = b.NoFrenzy = true;  // the pack has its own speed-up
                        w.Hold = b.Hold = LegionHold;
                        w.Ang = a;
                        b.Ang = a + 0.5f;
                    }
                    p.LegionT = 10f;
                    p.FrenzyT = Math.Max(p.FrenzyT, 10f);  // your other minions frenzy
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.3f, 0.3f), 140f, 0.5f, Sfx.Roar);
                    break;
            }
        }
    }
}
