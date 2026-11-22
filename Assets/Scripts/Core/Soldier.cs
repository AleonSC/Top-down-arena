using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Soldier's class definition: its weapons, ability cards and abilities, with the ones only very rare cards
    /// give (its weapons fire in FireVolley, Weapons.cs; its abilities in CastAbility, Abilities.cs).
    public partial class Game
    {
        /// The Soldier: its weapons fire in FireVolley's own cases, its abilities in CastAbility (Abilities.cs). See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef SoldierKit = new KitDef
        {
            Id = Kit.Soldier, Name = "Soldier", Icon = "class_soldier",
            Blurb = "Guns and gear. Straightforward firearms with an Alt trick each (a point-blank shell, a proximity mine, a grenade volley) and field kit for abilities. Full health and normal speed: the steady pick.",
        };

        /// The Soldier's weapons (fired in FireVolley below).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] SoldierWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Scattergun, Kit = Kit.Soldier, Role = Role.Tank, Choice = 0, Speed = 520f, Life = 0.5f, Damage = 7f, BotRange = 210f, ExtraDesc = "+2 pellets and a 15% tighter cone", Name = "Scattergun", FireInterval = 0.45f,
                Desc = "Primary Fire: A short-range shotgun spread, brutal up close.\n\nAlternative Fire: Fire all your loaded shells at once.",
                Sound = Sfx.ShotShotgun },
            new WeaponDef { Id = WeaponId.FlakCannon, Kit = Kit.Soldier, Role = Role.Tank, Choice = 1, Speed = 380f, Life = 1.1f, Damage = 30f, ExtraDesc = "Shells burst into 2 more bomblets", Name = "Flak Cannon", FireInterval = 0.8f,
                Desc = "Primary Fire: Shells that burst in the air at your cursor and destroy enemy bullets.\n\nAlternative Fire: Lay a proximity shell that bursts when an enemy steps on it.",
                Sound = Sfx.ShotCannon, AreaDesc = "blasts (and bomblets) 25% wider" },
            new WeaponDef { Id = WeaponId.RiotRepeater, Kit = Kit.Soldier, Role = Role.Tank, Choice = 2, Speed = 600f, Life = 0.8f, Damage = 11f, ExtraDesc = "+1 round per burst (each trigger pull fires a burst, then a short pause)", Name = "Riot Repeater", FireInterval = 0.18f,
                Desc = "Primary Fire: Steady shots that suppress enemies so they shoot slower.\n\nAlternative Fire: Lob tear gas that slows and suppresses.",
                Sound = Sfx.ShotRifle },
            new WeaponDef { Id = WeaponId.GrenadeLauncher, Kit = Kit.Soldier, Role = Role.Tank, Choice = 3, Speed = 900f, Life = 0.42f, Damage = 34f, BotRange = 340f, ExtraDesc = "+1 grenade a shot, fanned out", Name = "Grenade Launcher", FireInterval = 0.42f,
                Desc = "Primary Fire: Lob a drum of grenades that burst where they land.\n\nAlternative Fire: Swap to smoke that slows enemies and stops them attacking.",
                Sound = Sfx.TankMineLaunch, AreaDesc = "blasts and clouds 25% wider" },
            // Healer
            new WeaponDef { Id = WeaponId.RepulsionCannon, Kit = Kit.Soldier, Role = Role.Healer, Choice = 0, Damage = 6f, Reach = 240f, ExtraDesc = "+20° wider blast, +2 damage and a harder push", Name = "Repulsion Cannon", FireInterval = 0.7f,
                Desc = "Primary Fire: Blasts enemies away in a cone and knocks health orbs loose.\n\nAlternative Fire: Hold to pull enemies in.",
                HomingDesc = "the blast 15% narrower, and 10% stronger (damage and push) per stack, instead of bending shots", Sound = Sfx.ShotRepulse },
            new WeaponDef { Id = WeaponId.Revolver, Kit = Kit.Soldier, Role = Role.Healer, Choice = 1, Speed = 700f, Life = 1.1f, Damage = 24f, ExtraDesc = "+2 rounds in the cylinder, loaded as heavy rounds (double damage)", Name = "Revolver", FireInterval = 0.45f, Desc = "Primary Fire: Tap for aimed shots, or hold to fan the hammer for a quick spray.\n\nAlternative Fire: Pistol whip: a wide swing that destroys bullets and shoves enemies.",
                Sound = Sfx.ShotPistol },
            new WeaponDef { Id = WeaponId.MendersBow, Kit = Kit.Soldier, Role = Role.Healer, Choice = 2, BotRange = 390f, ExtraDesc = "+1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan", Name = "Mender's Bow", FireInterval = 0.9f, Desc = "Primary Fire: Hold to draw, release to fire a piercing arrow that heals allies it passes.",
                Sound = Sfx.ShotBow },
            new WeaponDef { Id = WeaponId.TwinPistols, Kit = Kit.Soldier, Role = Role.Healer, Choice = 3, Speed = 780f, Life = 0.8f, Damage = 10f, BotRange = 380f, ExtraDesc = "+1 round a shot, fanned a little, and Laser Sights lasts 1.5 s longer", Name = "Twin Pistols", FireInterval = 0.16f,
                Desc = "Primary Fire: A pistol in each hand, firing in turn; hits knock health orbs loose.\n\nAlternative Fire: Laser Sights: both guns fire together for harder rounds.",
                Sound = Sfx.ShotPistol },
            // DPS
            new WeaponDef { Id = WeaponId.TwinRifle, Kit = Kit.Soldier, Role = Role.Dps, Choice = 0, Speed = 820f, Life = 0.7f, Damage = 4f, ExtraDesc = "+1 barrel", Name = "Twin Rifle", FireInterval = 0.125f,
                Desc = "Primary Fire: Two rifles whose streams cross at your cursor for crossfire damage.",
                Sound = Sfx.ShotAssault },
            new WeaponDef { Id = WeaponId.Railgun, Kit = Kit.Soldier, Role = Role.Dps, Choice = 1, Speed = 1600f, Life = 0.7f, Damage = 120f, BotRange = 460f, ExtraDesc = "+1 bounce and 12% more damage", Name = "Railgun", FireInterval = 0.7f,
                Desc = "Primary Fire: Hold to charge a beam that ricochets off walls.\n\nAlternative Fire: Dump the gun's heat as a cone of fire.",
                Sound = Sfx.ShotSniper },
            new WeaponDef { Id = WeaponId.BurstSmg, Kit = Kit.Soldier, Role = Role.Dps, Choice = 2, Speed = 760f, Life = 0.8f, Damage = 5f, BotRange = 280f, ExtraDesc = "+1 bullet per shot, with a wider spray", Name = "Burst SMG", FireInterval = 0.055f,
                Desc = "Primary Fire: A very fast spray that tightens and hits harder the longer you fire.",
                Sound = Sfx.ShotSmg },
            new WeaponDef { Id = WeaponId.DoubleBarrel, Kit = Kit.Soldier, Role = Role.Dps, Choice = 3, Speed = 900f, Life = 0.5f, Damage = 7f, BotRange = 200f, ExtraDesc = "+4 pellets a barrel", Name = "Double Barrel", FireInterval = 0.32f,
                Desc = "Primary Fire: A two-shell shotgun, strongest up close, with pellets that pierce.\n\nAlternative Fire: Fire both barrels at once.",
                Sound = Sfx.ShotShotgun },
            new WeaponDef { Id = WeaponId.TwinRifleAuto, Kit = Kit.Soldier, Role = Role.Dps, Choice = 4, Speed = 820f, Life = 0.5f, Damage = 4f, ExtraDesc = "+1 round per gun, fanned a little", Name = "Twin Rifle Auto", Variant = true, FireInterval = 0.125f,
                Desc = "Primary Fire: Two rifles that aim and fire on their own at the nearest enemies.\n\nAlternative Fire: Lock both onto the enemy nearest your cursor.",
                Sound = Sfx.ShotAssault },
            new WeaponDef { Id = WeaponId.OutOfAmmo, Kit = Kit.Soldier, Role = Role.Dps, Choice = 5, Damage = 12f, Reach = 80f, Name = "Out of Ammo", Variant = true, FireInterval = 0.5f, ExtraDesc = "+50% damage on every strike and spin tick",
                Desc = "Primary Fire: Empty rifles swat bullets away and bash enemies in a five-move chain.\n\nAlternative Fire: Spin for a moment, cutting everything and swatting every bullet.",
                Sound = Sfx.SwingLight },
        };

        /// The Soldier's own upgrade cards: Rare upgrades of its 1st abilities (RequiresAbility), and cards for its weapons
        /// (Weapons) or the whole class (Kit). Upgrades.All gathers every class's list (Docs/UpgradePools.md).
        internal static readonly UpgradeDef[] SoldierCards =
        {
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Barrier, MaxStacks = 2, Name = "Bastion", Desc = "Barrier is 40% bigger, lasts 1.5 s longer and soaks 50% more",
                Apply = s => { s.BarrierRadiusMult += 0.4f; s.BarrierBonusTime += 1.5f; s.BarrierCapacityMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Roar, MaxStacks = 2, Name = "Bellow", Desc = "Roar lasts 2 s longer and blocks 15% more damage",
                Apply = s => { s.RoarBonusTime += 2f; s.RoarExtraReduction += 0.15f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Bulldoze, MaxStacks = 2, Name = "Juggernaut", Desc = "Bulldoze deals double damage", Apply = s => s.BulldozeMult += 1f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.DeployableCover, MaxStacks = 2, Name = "Reinforced Plating", Desc = "Deployable Cover takes 40% more damage, and one more can stand at once",
                Apply = s => { s.CoverHpMult += 0.4f; s.CoverExtra += 1; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.DeployableTurret, MaxStacks = 2, Name = "Overclocked Barrels", Desc = "Deployable Turret rounds hit 25% harder, and it fires 2 s longer",
                Apply = s => { s.DtDmgMult += 0.25f; s.DtBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.DecoyDummy, MaxStacks = 2, Name = "Double-Ply", Desc = "Decoy Dummy has 50% more health",
                Apply = s => s.DecoyHpMult += 0.5f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.MendPulse, MaxStacks = 2, Name = "Greater Mending", Desc = "Mend Pulse heals 60% more and reaches 25% farther",
                Apply = s => { s.PulseHealMult += 0.6f; s.PulseRangeMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Adrenaline, MaxStacks = 2, Name = "Long Dose", Desc = "Adrenaline Shot lasts 3 s longer",
                Apply = s => s.AdrenalineBonusTime += 3f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.PoisonDart, MaxStacks = 2, Name = "Virulence", Desc = "Poison Dart's venom deals 30% more damage and spreads 60 px further",
                Apply = s => { s.VenomMult *= 1.3f; s.VenomReachBonus += 60f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Airstrike, MaxStacks = 2, Name = "Bigger Boom", Desc = "Airstrike's bombs blast 50% wider",
                Apply = s => s.GrenadeRadiusMult += 0.5f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Mark, MaxStacks = 2, Name = "Exploit Weakness", Desc = "Marked enemies take an extra 50% damage", Apply = s => s.MarkBonus += 0.5f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Medkit, MaxStacks = 2, Name = "Field Surgeon", Desc = "Medkit bursts into 3 more orbs, and each heals 5 more",
                Apply = s => { s.MedkitExtraOrbs += 3; s.MedkitHealBonus += 5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Kit = Arena.Kit.Soldier, MaxStacks = 2, Name = "Quartermaster", Desc = "Health and medkit orbs you pick up heal 30% more, and your 2nd, 3rd and 4th abilities recharge 10% faster",
                Apply = s => { s.OrbHealMult += 0.3f; s.BonusCdMult *= 0.9f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Scattergun }, MaxStacks = 2, Name = "Point Blank", Desc = "The Scattergun's close-range double damage reaches 30% farther, and its shove is 30% harder",
                Apply = s => { s.PointBlankReachMult += 0.3f; s.ScatterShoveMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.FlakCannon }, MaxStacks = 2, Name = "Wide Burst", Desc = "Flak airbursts are 25% wider, and the Proximity Shell recharges 1 s sooner",
                Apply = s => { s.FlakBurstMult += 0.25f; s.MineCdCut += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.RiotRepeater }, MaxStacks = 2, Name = "Crowd Control", Desc = "Riot Repeater suppression builds 50% faster, and the Tear Gas cloud is 25% bigger",
                Apply = s => { s.SuppressMult += 0.5f; s.TearGasMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.RepulsionCannon }, MaxStacks = 2, Name = "Undertow Pump", Desc = "The Undertow meter fills 30% faster, and the health orbs the Repulsion Cannon knocks loose heal 30% more",
                Apply = s => { s.UndertowGainMult += 0.3f; s.ShedHealMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Revolver }, MaxStacks = 2, Name = "Hot Lead", Desc = "The Revolver reloads 25% faster, and its heavy rounds deal one more times their damage",
                Apply = s => { s.RevolverReloadMult *= 0.75f; s.HeavyRoundBonus += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.MendersBow }, MaxStacks = 2, Name = "Steady Draw", Desc = "The Mender's Bow reaches a full draw 20% sooner, and its arrows pierce 1 more",
                Apply = s => { s.BowDrawMult *= 0.8f; s.BowPierceBonus += 1; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.TwinRifle, WeaponId.TwinRifleAuto }, MaxStacks = 2, Name = "Kill Zone", Desc = "The Twin Rifle's crossfire zone is 40% wider, and crossfire hits for 0.25x more",
                Apply = s => { s.CrossZoneMult += 0.4f; s.CrossBonus += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Railgun }, MaxStacks = 2, Name = "Supercharge", Desc = "The Railgun's charged path lasts 0.5 s longer and burns 40% harder",
                Apply = s => { s.RailTrailBonus += 0.5f; s.RailBurnMult += 0.4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.BurstSmg }, MaxStacks = 2, Name = "Spin-Up", Desc = "The Burst SMG reaches full spin 30% sooner and winds down 50% slower",
                Apply = s => { s.SpinTimeMult *= 0.7f; s.SpinDownMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.DoubleBarrel }, MaxStacks = 2, Name = "Quick Loader", Desc = "The Double Barrel reloads 25% faster, and its pellets keep full damage 30% farther",
                Apply = s => { s.DbReloadMult *= 0.75f; s.DbFullMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.GrenadeLauncher }, MaxStacks = 2, Name = "Heavy Drum", Desc = "The Grenade Launcher holds 2 more HE grenades a drum, and its smoke clouds are 20% bigger",
                Apply = s => { s.GlDrumBonus += 2; s.GlCloudMult += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.OutOfAmmo }, MaxStacks = 2, Name = "Gun Kata", Desc = "Out of Ammo's rifle arms swat bullets 25% farther out and recover 30% faster",
                Apply = s => { s.SwatReachMult += 0.25f; s.SwatCdMult *= 0.7f; } },
        };

        /// The Soldier's abilities, and the ones only very rare cards give (Shockwave, Overdrive, Blink, Starburst, Turret). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] SoldierAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.Barrier, Kit = Kit.Soldier, Role = Role.Tank, Choice = 0,
                Name = "Barrier", Cooldown = 12f, Desc = "A bubble that stops incoming enemy bullets. Bosses can break it (it soaks 360 of their damage).",
                Capstone = "Bullets that hit the barrier are reflected back as your own.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.MendPulse, Kit = Kit.Soldier, Role = Role.Healer, Choice = 0, Instant = true,
                Name = "Mend Pulse", Cooldown = 10f, Desc = "Heal everyone nearby and revive downed allies.",
                Capstone = "Also erases enemy bullets within its range.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.PoisonDart, Kit = Kit.Soldier, Role = Role.Dps, Choice = 0,
                Name = "Poison Dart", Cooldown = 8f, Desc = "Dart the enemy nearest your cursor: 20 damage, then poison for 18 a second over 5 s. If it dies poisoned, the poison spreads to the 3 nearest enemies (80% as strong), and on from them.",
                Capstone = "Spreads to 5 enemies at a time, at full strength.",
                Bot = b => b.HaveTarget && b.TargetD < 700f,
            },
            new AbilityDef
            {
                Id = AbilityId.Shockwave, Instant = true,
                Name = "Shockwave", Cooldown = 14f, Desc = "A shockwave runs out 230 px from you: it erases enemy bullets, hits enemies for 60 and shoves them back 120 px.",
                Capstone = "The wave runs out 300 px.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Overdrive,
                Name = "Overdrive", Cooldown = 18f, Desc = "Double fire rate for 5 seconds.",
                Capstone = "+1 extra projectile while it lasts.",
            },
            new AbilityDef
            {
                Id = AbilityId.Blink,
                Name = "Blink", Cooldown = 7f, Desc = "Teleport toward your cursor.",
                Capstone = "Erases enemy bullets where you leave and where you land.",
                Bot = b => b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Starburst, Instant = true,
                Name = "Starburst", Cooldown = 9f, Desc = "Fire a ring of 24 heavy shots.",
                Capstone = "Fires 48 shots instead of 24.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.Turret,
                Name = "Turret", Cooldown = 20f, Desc = "Deploy a turret that shoots for 10 seconds.",
                Capstone = "Deploys two turrets.",
            },
            new AbilityDef
            {
                Id = AbilityId.Roar, Kit = Kit.Soldier, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Roar", Cooldown = 14f, Desc = "Every enemy targets you for 5 s, and you take 40% less damage meanwhile.",
                Capstone = "Also knocks enemies within 250 px back and slows them for 2 s.",
                Bot = b => b.HaveTarget && b.Players > 1 && (b.AllyThreatened || b.Me.Hp < b.Me.MaxHp * 0.5f),  // draw the enemies off a teammate in trouble
            },
            new AbilityDef
            {
                Id = AbilityId.Bulldoze, Kit = Kit.Soldier, Role = Role.Tank, Choice = 2, Instant = true,
                Name = "Bulldoze", Cooldown = 9f, Desc = "Charge forward, damaging and knocking back everything in your path.",
                Capstone = "Erases enemy bullets along the charge.",
            },
            new AbilityDef
            {
                Id = AbilityId.DeployableCover, Kit = Kit.Soldier, Role = Role.Tank, Choice = AbilityDef.Fourth, ExtraCharges = 1,  // the lobby's 4th choice (DeployableCover.cs)
                Name = "Deployable Cover", Cooldown = CoverCooldown, Desc = "Two charges. Plant a bracket of steel plate in front of you that stops enemy bullets and enemies until it has taken 400 damage; your team's shots pass through. Up to 3 stand at once (another folds away the oldest), and they fold away when the wave is cleared.",
                Capstone = "Covers take 600 damage, and each charge comes back 25% sooner.",
                Bot = b => b.Danger || b.NearEnemies >= 3,
            },
            new AbilityDef
            {
                Id = AbilityId.Adrenaline, Kit = Kit.Soldier, Role = Role.Healer, Choice = 1,
                Name = "Adrenaline Shot", Cooldown = AdrenalineCooldown, Desc = "Inject the player closest to your cursor (you, if alone): 8 s of +30% move speed, +40% fire rate and 30% less damage taken.",
                Capstone = "Also gives the nearest other ally within 250 px a half-length (4 s) dose.",
                Bot = b => b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.Medkit, Kit = Kit.Soldier, Role = Role.Healer, Choice = 2,
                Name = "Deployable Medkit", Cooldown = MedkitCooldown, Desc = "Throw a medkit at your cursor: it bursts into 6 healing orbs (15 health each) that anyone can pick up for 6 s.",
                Capstone = "Bursts into 9 orbs worth 20 health each.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.DecoyDummy, Kit = Kit.Soldier, Role = Role.Healer, Choice = AbilityDef.Fourth, ExtraCharges = DecoyCharges - 1,  // the lobby's 4th choice (DecoyDummy.cs)
                Name = "Decoy Dummy", Cooldown = DecoyCooldown, Desc = "Three charges. Throw an inflatable soldier at your cursor: regular enemies within 240 px go for it and shoot at it instead of you (bosses only glance at it for a second). It has 40 health (+4 a wave) and lasts 12 s; up to 3 stand at once. When it's popped, a confetti blast shoves enemies within 90 px back and stuns them for 0.4 s.",
                Capstone = "Dummies have 50% more health, and the confetti blast reaches 130 px.",
                Bot = b => b.Danger || b.NearEnemies >= 3,
            },
            new AbilityDef
            {
                Id = AbilityId.Airstrike, Kit = Kit.Soldier, Role = Role.Dps, Choice = 1,
                Name = "Airstrike", Cooldown = 16f, Desc = "Throw a flare at your cursor: a jet carpets a line across the arena through it, across your aim (55 a bomb), and the line burns for 3 s.",
                Capstone = "A second wall lands 160 px further out, half a second later.",
            },
            new AbilityDef
            {
                Id = AbilityId.Mark, Kit = Kit.Soldier, Role = Role.Dps, Choice = 2, Instant = true,
                Name = "Mark for Death", Cooldown = 12f, Desc = "Mark the enemy nearest your cursor: it takes 50% more damage for 6 s.",
                Capstone = "Marks the three enemies nearest your cursor.",
                Bot = b => b.HaveTarget && !b.TargetMarked && b.TargetD < 700f,  // on the boss (or the team's focus), and not again while it's marked
            },
            new AbilityDef
            {
                Id = AbilityId.DeployableTurret, Kit = Kit.Soldier, Role = Role.Dps, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (DeployableTurret.cs)
                Name = "Deployable Turret", Cooldown = DtCooldown, Desc = "Drop a heavy twin-barrel turret on your spot and climb in for 10 s: you can't move, and it fires instead of your weapon. Hold Fire and it turns onto your cursor, spooling up from 7 to 30 rounds a second of 22 damage that pierce 2 enemies. The turret takes the damage meant for you (350 health); if it is wrecked you are thrown clear and it blows early. It overheats and blows after, for 80 damage within 160 px.",
                Capstone = "Fires 14 s, rounds pierce 4, and the blast does double.",
                Bot = b => b.HaveTarget && b.NearEnemies >= 3 && b.TargetD < 480f,
            },
        };
    }
}
