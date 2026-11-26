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
            new WeaponDef { Id = WeaponId.Scattergun, BotAlt = ScattergunBotAlt, Kit = Kit.Soldier, Role = Role.Tank, Choice = 0, Speed = 520f, Life = 0.5f, Damage = 7f, BotRange = 210f, ExtraDesc = "+2 pellets and a 15% tighter cone", Name = "Scattergun", FireInterval = 0.45f,
                Desc = "Primary Fire: A short-range shotgun spread, brutal up close.\n\nAlternative Fire: Lob a shock grenade that stuns enemies where it lands.",
                Sound = Sfx.ShotShotgun },
            new WeaponDef { Id = WeaponId.FlakCannon, BotAlt = FlakCannonBotAlt, Kit = Kit.Soldier, Role = Role.Tank, Choice = 1, Speed = 380f, Life = 1.1f, Damage = 30f, ExtraDesc = "Shells burst into 2 more bomblets", Name = "Flak Cannon", FireInterval = 0.8f,
                Desc = "Primary Fire: Shells that burst in the air at your cursor and destroy enemy bullets.\n\nAlternative Fire: Lay a proximity shell that bursts when an enemy steps on it.",
                Sound = Sfx.ShotCannon, AreaDesc = "blasts (and bomblets) 25% wider" },
            new WeaponDef { Id = WeaponId.RiotRepeater, BotAlt = RiotRepeaterBotAlt, Kit = Kit.Soldier, Role = Role.Tank, Choice = 2, Speed = 600f, Life = 0.8f, Damage = 11f, ExtraDesc = "+1 round per burst (each trigger pull fires a burst, then a short pause)", Name = "Riot Repeater", FireInterval = 0.18f,
                Desc = "Primary Fire: Steady shots that suppress enemies so they shoot slower.\n\nAlternative Fire: Lob tear gas that slows and suppresses.",
                Sound = Sfx.ShotRifle },
            new WeaponDef { Id = WeaponId.GrenadeLauncher, BotAlt = GrenadeLauncherBotAlt, Kit = Kit.Soldier, Role = Role.Tank, Choice = 3, Speed = 900f, Life = 0.42f, Damage = 34f, BotRange = 340f, ExtraDesc = "+1 grenade a shot, fanned out", Name = "Grenade Launcher", FireInterval = 0.42f,
                Desc = "Primary Fire: Lob a drum of grenades that burst where they land.\n\nAlternative Fire: Swap to smoke that slows enemies and stops them attacking.",
                Sound = Sfx.TankMineLaunch, AreaDesc = "blasts and clouds 25% wider" },
            // Healer
            new WeaponDef { Id = WeaponId.RepulsionCannon, BotAlt = RepulsionCannonBotAlt, BotAltHold = true, Kit = Kit.Soldier, Role = Role.Healer, Choice = 0, Damage = 6f, Reach = 240f, ExtraDesc = "+20° wider blast, +2 damage and a harder push", Name = "Repulsion Cannon", FireInterval = 0.7f,
                Desc = "Primary Fire: Blasts enemies away in a cone and knocks health orbs loose.\n\nAlternative Fire: Hold to pull enemies in.",
                HomingDesc = "the blast 15% narrower, and 10% stronger (damage and push) per stack, instead of bending shots", Sound = Sfx.ShotRepulse },
            new WeaponDef { Id = WeaponId.Revolver, BotAlt = RevolverBotAlt, Kit = Kit.Soldier, Role = Role.Healer, Choice = 1, Speed = 700f, Life = 1.1f, Damage = 50f, ExtraDesc = "The shot goes through one more enemy, and Fan the Hammer fires 2 more rounds", Name = "Revolver", FireInterval = 0.5f, Desc = "Primary Fire: Click to fire a hard, aimed shot, click again to pull the hammer back: it shoots as fast as you click.\n\nAlternative Fire: Fan stance: click to Pistol Whip (stuns what it hits), click again to Fan the Hammer. Hold parry and click to fan any time; parry after a whip to whip again.",
                Sound = Sfx.ShotPistol },
            new WeaponDef { Id = WeaponId.MendersBow, BotAlt = MendersBowBotAlt, Kit = Kit.Soldier, Role = Role.Healer, Choice = 2, BotRange = 390f, ExtraDesc = "+1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan", Name = "Mender's Bow", FireInterval = 0.9f, Desc = "Primary Fire: Hold to draw, release to fire a piercing arrow that heals allies it passes.",
                Sound = Sfx.ShotBow },
            new WeaponDef { Id = WeaponId.TwinPistols, BotAlt = TwinPistolsBotAlt, Kit = Kit.Soldier, Role = Role.Healer, Choice = 3, Speed = 780f, Life = 0.8f, Damage = 10f, BotRange = 380f, ExtraDesc = "+1 round a shot, fanned a little, and Laser Sights lasts 1.5 s longer", Name = "Twin Pistols", FireInterval = 0.16f,
                Desc = "Primary Fire: A pistol in each hand, firing in turn; hits knock health orbs loose.\n\nAlternative Fire: Laser Sights: both guns fire together for harder rounds.",
                Sound = Sfx.ShotPistol },
            // DPS
            new WeaponDef { Id = WeaponId.TwinRifle, BotAlt = TwinRifleBotAlt, Kit = Kit.Soldier, Role = Role.Dps, Choice = 0, Speed = 820f, Life = 0.7f, Damage = 4f, ExtraDesc = "+1 barrel", Name = "Twin Rifle", FireInterval = 0.125f,
                Desc = "Primary Fire: Two rifles whose streams cross at your cursor for crossfire damage.",
                Sound = Sfx.ShotAssault },
            new WeaponDef { Id = WeaponId.Railgun, BotAlt = RailgunBotAlt, Kit = Kit.Soldier, Role = Role.Dps, Choice = 1, Speed = 1600f, Life = 0.7f, Damage = 120f, BotRange = 460f, ExtraDesc = "+1 bounce and 12% more damage", Name = "Railgun", FireInterval = 0.7f,
                Desc = "Primary Fire: Hold to charge a beam that ricochets off walls.\n\nAlternative Fire: Dump the gun's heat as a cone of fire.",
                Sound = Sfx.ShotSniper },
            new WeaponDef { Id = WeaponId.BurstSmg, Kit = Kit.Soldier, Role = Role.Dps, Choice = 2, Speed = 760f, Life = 0.8f, Damage = 5f, BotRange = 280f, ExtraDesc = "+1 bullet per shot, with a wider spray", Name = "Burst SMG", FireInterval = 0.055f,
                Desc = "Primary Fire: A very fast spray that tightens and hits harder the longer you fire.",
                Sound = Sfx.ShotSmg },
            new WeaponDef { Id = WeaponId.DoubleBarrel, BotAlt = DoubleBarrelBotAlt, Kit = Kit.Soldier, Role = Role.Dps, Choice = 3, Speed = 900f, Life = 0.5f, Damage = 7f, BotRange = 200f, ExtraDesc = "+4 pellets a barrel", Name = "Double Barrel", FireInterval = 0.32f,
                Desc = "Primary Fire: A two-shell shotgun, strongest up close, with pellets that pierce.\n\nAlternative Fire: Fire both barrels at once.",
                Sound = Sfx.ShotShotgun },
            new WeaponDef { Id = WeaponId.TwinRifleAuto, BotAlt = TwinRifleAutoBotAlt, Kit = Kit.Soldier, Role = Role.Dps, Choice = 4, Speed = 820f, Life = 0.5f, Damage = 4f, ExtraDesc = "+1 round per gun, fanned a little", Name = "Twin Rifle Auto", Variant = true, FireInterval = 0.125f,
                Desc = "Primary Fire: Two rifles that aim and fire on their own at the nearest enemies.\n\nAlternative Fire: Lock both onto the enemy nearest your cursor.",
                Sound = Sfx.ShotAssault },
            new WeaponDef { Id = WeaponId.OutOfAmmo, BotAlt = OutOfAmmoBotAlt, Kit = Kit.Soldier, Role = Role.Dps, Choice = 5, Damage = 12f, Reach = 80f, Name = "Out of Ammo", Variant = true, FireInterval = 0.5f, ExtraDesc = "+50% damage on every strike and spin tick",
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
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Claymore, MaxStacks = 2, Name = "Shaped Charge", Desc = "Claymore's blast deals 40% more damage",
                Apply = s => s.ClaymoreDmgMult += 0.4f },
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
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Revolver }, MaxStacks = 2, Name = "Hot Lead", Desc = "The Revolver's hammer comes back 25% faster (Fan the Hammer reloads faster too), and its shot hits 25% harder",
                Apply = s => { s.RevolverCockMult *= 0.75f; s.RevolverShotBonus += 0.25f; } },
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
                Name = "Barrier", Icon = "bubble", Cooldown = 12f, Desc = "A bubble that stops enemy bullets.",
                Capstone = "Bullets that hit the barrier are reflected back as your own.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.MendPulse, Kit = Kit.Soldier, Role = Role.Healer, Choice = 0, Instant = true,
                Name = "Mend Pulse", Icon = "pulse", Cooldown = 10f, Desc = "Heal everyone nearby and revive downed allies.",
                Capstone = "Also erases enemy bullets within its range.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.PoisonDart, Kit = Kit.Soldier, Role = Role.Dps, Choice = 0,
                Name = "Poison Dart", Icon = "poisondart", Cooldown = 8f, Desc = "Poison the nearest enemy; the poison spreads when it dies.",
                Capstone = "Spreads to 5 enemies at a time, at full strength.",
                Bot = b => b.HaveTarget && b.TargetD < 700f,
            },
            new AbilityDef
            {
                Id = AbilityId.Shockwave, Instant = true,
                Name = "Shockwave", Icon = "shockwave", Cooldown = 14f, Desc = "A shockwave erases bullets and shoves enemies back.",
                Capstone = "The wave runs out 300 px.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Overdrive,
                Name = "Overdrive", Icon = "bolt", Cooldown = 18f, Desc = "Double your fire rate for a few seconds.",
                Capstone = "+1 extra projectile while it lasts.",
            },
            new AbilityDef
            {
                Id = AbilityId.Blink,
                Name = "Blink", Icon = "blink", Cooldown = 7f, Desc = "Teleport toward your cursor.",
                Capstone = "Erases enemy bullets where you leave and where you land.",
                Bot = b => b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Starburst, Instant = true,
                Name = "Starburst", Cooldown = 9f, Desc = "Fire a ring of heavy shots.",
                Capstone = "Fires 48 shots instead of 24.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.Turret,
                Name = "Turret", Icon = "turret", Cooldown = 20f, Desc = "Deploy a turret that shoots for a while.",
                Capstone = "Deploys two turrets.",
            },
            new AbilityDef
            {
                Id = AbilityId.Roar, Kit = Kit.Soldier, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Roar", Icon = "roar", Cooldown = 14f, Desc = "Every enemy targets you, and you take less damage.",
                Capstone = "Also knocks enemies within 250 px back and slows them for 2 s.",
                Bot = b => b.HaveTarget && b.Players > 1 && (b.AllyThreatened || b.Me.Hp < b.Me.MaxHp * 0.5f),  // draw the enemies off a teammate in trouble
            },
            new AbilityDef
            {
                Id = AbilityId.Bulldoze, Kit = Kit.Soldier, Role = Role.Tank, Choice = 2, Instant = true,
                Name = "Bulldoze", Icon = "fist", Cooldown = 9f, Desc = "Charge forward, damaging and knocking back everything in your path.",
                Capstone = "Erases enemy bullets along the charge.",
            },
            new AbilityDef
            {
                Id = AbilityId.DeployableCover, Kit = Kit.Soldier, Role = Role.Tank, Choice = AbilityDef.Fourth, ExtraCharges = 1,  // the lobby's 4th choice (DeployableCover.cs)
                Name = "Deployable Cover", Icon = "guard_wide", Cooldown = CoverCooldown, Desc = "Plant steel cover that stops enemy bullets while your team shoots through.",
                Capstone = "Covers take 600 damage, and each charge comes back 25% sooner.",
                Bot = b => b.Danger || b.NearEnemies >= 3,
            },
            new AbilityDef
            {
                Id = AbilityId.Adrenaline, Kit = Kit.Soldier, Role = Role.Healer, Choice = 1,
                Name = "Adrenaline Shot", Icon = "pulse", Cooldown = AdrenalineCooldown, Desc = "Boost a player's speed and fire rate and cut their damage taken.",
                Capstone = "Also gives the nearest other ally within 250 px a half-length (4 s) dose.",
                Bot = b => b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.Medkit, Kit = Kit.Soldier, Role = Role.Healer, Choice = 2,
                Name = "Deployable Medkit", Icon = "pulse", Cooldown = MedkitCooldown, Desc = "Throw a medkit that bursts into health orbs anyone can grab.",
                Capstone = "Bursts into 9 orbs worth 20 health each.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.DecoyDummy, Kit = Kit.Soldier, Role = Role.Healer, Choice = AbilityDef.Fourth, ExtraCharges = DecoyCharges - 1,  // the lobby's 4th choice (DecoyDummy.cs)
                Name = "Decoy Dummy", Icon = "clone", Cooldown = DecoyCooldown, Desc = "Throw an inflatable soldier that enemies shoot at instead of you.",
                Capstone = "Dummies have 50% more health, and the confetti blast reaches 130 px.",
                Bot = b => b.Danger || b.NearEnemies >= 3,
            },
            new AbilityDef
            {
                Id = AbilityId.Airstrike, Kit = Kit.Soldier, Role = Role.Dps, Choice = 1,
                Name = "Airstrike", Icon = "explode", Cooldown = 16f, Desc = "Call a jet to carpet a line with bombs.",
                Capstone = "A second wall lands 160 px further out, half a second later.",
            },
            new AbilityDef
            {
                Id = AbilityId.Claymore, Kit = Kit.Soldier, Role = Role.Dps, Choice = 2, ExtraCharges = ClaymoreCharges - 1,  // (Claymore.cs)
                Name = "Claymore", Icon = "claymore", Cooldown = ClaymoreCooldown, Desc = "Plant a mine at your cursor; the first enemy to step into the cone in front of it sets off a wide blast.",
                Capstone = "Each claymore re-arms once after its first blast: it goes off twice.",
                Bot = b => b.HaveTarget && b.TargetD < 420f,
            },
            new AbilityDef
            {
                Id = AbilityId.DeployableTurret, Kit = Kit.Soldier, Role = Role.Dps, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (DeployableTurret.cs)
                Name = "Deployable Turret", Cooldown = DtCooldown, Desc = "Climb into a heavy turret that spools up to a fast piercing fire.",
                Capstone = "Fires 14 s, rounds pierce 4, and the blast does double.",
                Bot = b => b.HaveTarget && b.NearEnemies >= 3 && b.TargetD < 480f,
            },
        };
    }
}
