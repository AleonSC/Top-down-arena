using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Elementalist's class definition: its weapons, ability cards and abilities (their casts are in KitAbility,
    /// Minions.cs; their weapons fire in KitWeapons.cs).
    public partial class Game
    {
        /// The Elementalist (Kit.Elementalist): weapons in FireKitWeapon (KitWeapons.cs), abilities in KitAbility (Minions.cs). See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef ElementalistKit = new KitDef
        {
            Id = Kit.Elementalist, Name = "Elementalist", Icon = "class_elementalist", HpMult = 0.85f,
            Blurb = "Spells of fire, frost, lightning, water, wind, earth and light. Elements combine: soaked enemies conduct lightning, earth cracks enemies so they take more damage (and frozen ones shatter), light dazzles them so their shots go wide. Big area damage, at 85% health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireKitWeapon(p, aim, dir, muzzle, dmg, extra),
        };

        /// The Elementalist's weapons (Kit.Elementalist; fired in FireKitWeapon).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] ElementalistWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Flamethrower, Range = StreamRange, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 0, Speed = 480f, Life = 0.4f, Damage = 3f, Grow = 0.25f, BotRange = 160f, ExtraDesc = "+1 flame a burst and 25% more reach", Name = "Flamethrower", KeyLabel = "Flame-\nthrower", FireInterval = 0.06f,
                Desc = "Primary Fire: A short stream of fire that burns everything it hits.",
                Sound = Sfx.SpellFire },
            new WeaponDef { Id = WeaponId.FrostLance, Range = (g, p, w, x) => p.Def.BodyR + FlArcR * AreaMult(p) * (1f + 0.1f * x) * p.Stats.IceReachMult, BotAlt = FrostLanceBotAlt, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 1, Damage = 18f, Reach = 178f, BotRange = 160f, ExtraDesc = "20% more damage and 10% more reach", Name = "Frost Lance", FireInterval = 0.4f,
                Desc = "Primary Fire: A five-step lance combo that chills, freezes, then shatters frozen enemies.\n\nAlternative Fire: Absolute Zero: a freezing burst around you.",
                Sound = Sfx.SpellFrost, AreaDesc = "every sweep, crystal and blast is 25% bigger" },
            new WeaponDef { Id = WeaponId.WindClash, BotAlt = WindClashBotAlt, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 3, Damage = 18f, Reach = 170f, BotRange = 150f, ExtraDesc = "+20% damage and 10% bigger waves", Name = "Wind Clash", FireInterval = 0.34f,
                Desc = "Primary Fire: Hold for a wind combo that hurts and shoves every enemy it passes.\n\nAlternative Fire: A storm around you hurts enemies and catches their shots, but slows you.",
                Sound = Sfx.None, AreaDesc = "the waves and the Tempest 25% bigger" },
            new WeaponDef { Id = WeaponId.StormHammer, BotAlt = StormHammerBotAlt, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 2, Speed = 460f, Life = 1.1f, Damage = 40f, BotRange = 260f, ExtraDesc = "+15% hammer damage, and the electrified enemy arcs to 1 more enemy", Name = "Storm Hammer", FireInterval = 0.7f,
                Desc = "Primary Fire: Throw a hammer that smashes one enemy and arcs lightning to its neighbours.",
                Sound = Sfx.SpellZap, AreaDesc = "the electrified enemy's lightning reaches 25% farther" },
            // Healer
            new WeaponDef { Id = WeaponId.RadiantOrb, Range = (g, p, w, x) => g.RadiantReach(p), Kit = Kit.Elementalist, Role = Role.Healer, Choice = 0, Damage = 8f, Reach = 520f, BotRange = 300f, ExtraDesc = "+1 light orb that circles your Radiant Orb (you, when it's not out), hurting enemies and healing allies it touches", Name = "Radiant Orb", FireInterval = 0.2f,
                Desc = "Primary Fire: Hold to guide an orb of light: it burns enemies and heals allies it passes over.",
                Sound = Sfx.SpellHoly, BarrageDesc = "+1 light orb per stack that circles your orb, hurting enemies and healing allies it touches" },
            new WeaponDef { Id = WeaponId.ChainLight, Range = (g, p, w, x) => LightConeLength(p, x), Kit = Kit.Elementalist, Role = Role.Healer, Choice = 1, Damage = 40f, BotRange = 240f, ExtraDesc = "+25% damage and a 15% longer cone", Name = "Chain Light", FireInterval = 0.5f,
                Desc = "Primary Fire: A cone of light whose damage is shared between every enemy in it.",
                Sound = Sfx.SpellZap, AreaDesc = "the cone of light 25% wider", HomingDesc = "the cone of light 15% narrower and 10% stronger per stack, instead of bending shots: fewer enemies share its damage", BarrageDesc = "+1 cone of light beside the first, sharing its own damage. Reaches more enemies; a lone one is still in one cone" },
            new WeaponDef { Id = WeaponId.Nimbus, Range = (g, p, w, x) => NimbusReach(p), BotAlt = NimbusBotAlt, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 2, Damage = 16f, Reach = 380f, BotRange = 300f, ExtraDesc = "+1 lightning strike on every click, landing in a ring round the cloud", Name = "Nimbus", FireInterval = 0.6f,
                Desc = "Primary Fire: A storm cloud over your cursor: click to call lightning, and its rain heals allies and soaks enemies.\n\nAlternative Fire: The cloud becomes a hurricane that drags enemies in and heals allies.",
                Sound = Sfx.SpellZap, AreaDesc = "the strike, the rain and the hurricane are 25% wider",
                Companion = MinionKind.Nimbus, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.StaticShock, Range = (g, p, w, x) => StaticHand(p, p.Pos).DistanceTo(p.Pos) + StaticRangeOf(p), BotAlt = StaticShockBotAlt, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 3, Damage = 24f, Reach = Game.StaticRange, BotRange = 190f, ExtraDesc = "+1 enemy wired into the circuit and 15% more shock", Name = "Static Shock", FireInterval = Game.StaticTick,
                Desc = "Primary Fire: Hold for a short cone of static that wires enemies into one circuit and shocks them, harder the longer they stay. Teammates it touches get Overcharge: faster movement and shots for 10 s.\n\nAlternative Fire: Arc Cannon: a giant ball of lightning rolls through everyone to your cursor and bursts into a stunning shock.",
                Sound = Sfx.SpellZap, AreaDesc = "the cone and the Arc Cannon's burst 25% bigger" },
            // DPS
            new WeaponDef { Id = WeaponId.Fireball, Range = (g, p, w, x) => p.Def.BodyR + w.Speed * w.Life * p.Stats.ShotSpeedMult * PierceReach(p), Kit = Kit.Elementalist, Role = Role.Dps, Choice = 0, Speed = 430f, Life = 1.4f, Damage = 32f, BotRange = 390f, ExtraDesc = "+1 fireball, fanned out", Name = "Fireball", FireInterval = 0.5f,
                Desc = "Primary Fire: A big, slow fireball that rolls through enemies and sets them burning.",
                Sound = Sfx.ShotFireball },
            new WeaponDef { Id = WeaponId.ArcLightning, Charge = true, Range = (g, p, w, x) => ArcReach(p), BotAlt = ArcLightningBotAlt, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 1, Damage = 16f, BotRange = 320f, ExtraDesc = "+25% damage", Name = "Arc Lightning", FireInterval = 0.25f,
                Desc = "Primary Fire: Hold to tag up to 10 enemies, let go to chain lightning through them and stun.\n\nAlternative Fire: Three forked bolts at the enemies nearest your cursor.",
                Sound = Sfx.SpellZap, AreaDesc = "the tagging cone 25% wider", BarrageDesc = "+2 tags a stack (up to 16), and the chain grows with them" },
            new WeaponDef { Id = WeaponId.IceShards, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 2, Speed = 520f, Life = 1f, Damage = 7f, ExtraDesc = "+1 shard a cast, +2 in orbit, and faster launches", Name = "Ice Shards", FireInterval = 0.3f,
                Desc = "Primary Fire: Ice shards orbit you, then launch homing at nearby enemies.",
                Sound = Sfx.SpellArcane },
            new WeaponDef { Id = WeaponId.Stonespear, BotAlt = StonespearBotAlt, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 3, Speed = 900f, Life = 0.6f, Damage = 24f, BotRange = 420f, ExtraDesc = "+1 spear a side, and each click throws one more", Name = "Stonespear", FireInterval = 0.18f,
                Desc = "Primary Fire: Slam the ground, then throw the earth spears that rise from it, one per click.\n\nAlternative Fire: Hurl every spear at once.",
                Sound = Sfx.None, BarrageDesc = "each throw also flings a fanned copy of the spear (60% damage) that uses none up" },
            // The Flamethrower's variants (Variants.cs; ElementGuns.cs)
            new WeaponDef { Id = WeaponId.WaterGun, Range = StreamRange, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 4, Speed = 640f, Life = 0.55f, Damage = 5f, Grow = 0.25f, BotRange = 300f, ExtraDesc = "+1 jet a burst and 25% more reach", Name = "Water Gun", Variant = true, FireInterval = 0.07f,
                Desc = "Primary Fire: A long spray of water that splashes the enemy it hits and those near it: wet enemies are slowed.",
                Sound = Sfx.SpellFrost },
            new WeaponDef { Id = WeaponId.GaleGun, Range = StreamRange, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 5, Speed = 560f, Life = 0.4f, Damage = 2.5f, Grow = 0.25f, BotRange = 180f, ExtraDesc = "+1 gust a burst and 25% more reach", Name = "Gale Gun", Variant = true, FireInterval = 0.06f,
                Desc = "Primary Fire: A blast of cutting wind that passes through enemies: it slices them (they bleed) and slows them.",
                Sound = Sfx.GaleHum },
            // The Fireball's variants (Variants.cs; ElementBalls.cs)
            new WeaponDef { Id = WeaponId.Waterball, Range = (g, p, w, x) => p.Def.BodyR + w.Speed * w.Life * p.Stats.ShotSpeedMult * PierceReach(p), Kit = Kit.Elementalist, Role = Role.Dps, Choice = 4, Speed = 430f, Life = 1.4f, Damage = 28f, BotRange = 390f, ExtraDesc = "+1 waterball, fanned out", Name = "Waterball", Variant = true, FireInterval = 0.5f,
                Desc = "Primary Fire: A big, slow water ball that soaks everything it rolls through: wet enemies are slowed.",
                Sound = Sfx.WwSplash },
            new WeaponDef { Id = WeaponId.Earthball, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 5, Speed = 360f, Life = 1.4f, Damage = 48f, BotRange = 380f, ExtraDesc = "+1 earthball, fanned out", Name = "Earthball", Variant = true, FireInterval = 0.6f,
                Desc = "Primary Fire: A heavy rock ball that stuns the first enemy it hits, then shatters into rubble.",
                Sound = Sfx.StoneThrow, AreaDesc = "the rubble 25% wider" },
        };

        /// The Elementalist's own upgrade cards: Rare upgrades of its 1st abilities (RequiresAbility), and cards for its weapons
        /// (Weapons) or the whole class (Kit). Upgrades.All gathers every class's list (Docs/UpgradePools.md).
        internal static readonly UpgradeDef[] ElementalistCards =
        {
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Aeroaegis, MaxStacks = 2, Name = "Eye of the Storm", Desc = "Aeroaegis's vortex lasts 1.5 s longer and the bullets it reflects hit 50% harder",
                Apply = s => { s.AegisBonusTime += 1.5f; s.AegisReflectBonus += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Flood, MaxStacks = 2, Name = "High Tide", Desc = "Flood spreads 25% wider and lasts 2 s longer",
                Apply = s => { s.FloodRadiusMult += 0.25f; s.FloodBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.FrostNova, MaxStacks = 2, Name = "Permafrost", Desc = "Frost Nova freezes 1 s longer and reaches 30% farther",
                Apply = s => { s.NovaFreezeBonus += 1f; s.NovaRangeMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.FlameWall, MaxStacks = 2, Name = "Inferno", Desc = "Flame Wall stands 2 s longer and burns 50% hotter",
                Apply = s => { s.FlameWallBonusTime += 2f; s.FlameWallBurnMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.HealingRain, MaxStacks = 2, Name = "Monsoon", Desc = "Healing Rain is 40% wider and heals 40% more",
                Apply = s => { s.RainRadiusMult += 0.4f; s.RainHealMult += 0.4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.GustGate, MaxStacks = 2, Name = "Jet Stream", Desc = "Gust Gate's ring lasts 3 s longer, and its speed boost 1 s longer",
                Apply = s => { s.GustLifeBonus += 3f; s.GustBoostBonus += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.LumenWard, MaxStacks = 2, Name = "Dawnlight", Desc = "Lumen Ward shines 2 s longer and heals 30% more",
                Apply = s => { s.LumenBonusTime += 2f; s.LumenHealMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Meteor, MaxStacks = 2, Name = "Cataclysm", Desc = "Meteor hits 50% harder and 30% wider",
                Apply = s => { s.MeteorDmgMult += 0.5f; s.MeteorRadiusMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Blizzard, MaxStacks = 2, Name = "Whiteout", Desc = "Blizzard is 40% bigger and lasts 2 s longer",
                Apply = s => { s.BlizzardRadiusMult += 0.4f; s.BlizzardBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.GaleWall, MaxStacks = 2, Name = "Momentum Bank", Desc = "Each shot Gale Wall catches adds 40% more damage to its blast",
                Apply = s => s.GaleChargeMult += 0.4f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.WaterWard, MaxStacks = 2, Name = "Deep Current", Desc = "Water Ward's splashes hit 40% harder, and its drops keep enemies wet 2 s longer",
                Apply = s => { s.WwDmgMult += 0.4f; s.WwWetBonus += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Tsunami, MaxStacks = 2, Name = "Riptide", Desc = "Tsunami's wave is 40% wider and enemies stay wet 4 s longer",
                Apply = s => { s.TsunamiWidthMult += 0.4f; s.WetBonusTime += 4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Kit = Arena.Kit.Elementalist, MaxStacks = 2, Name = "Arcane Surge", Desc = "Casting your 1st ability makes your weapon fire 25% faster for 3 s",
                Apply = s => { s.SurgeRate += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Flamethrower }, MaxStacks = 2, Name = "Napalm", Desc = "The Flamethrower's stream reaches 15% farther, and its burn is 40% hotter",
                Apply = s => { s.FlameReachMult += 0.15f; s.FlameBurnMult += 0.4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.FrostLance }, MaxStacks = 2, Name = "Glacier", Desc = "The Frost Lance reaches 20% farther, and its freezes last 0.6 s longer",
                Apply = s => { s.IceReachMult += 0.2f; s.IceFreezeBonus += 0.6f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.StormHammer }, MaxStacks = 2, Name = "Forked Thunder", Desc = "The Storm Hammer's lightning arcs to 1 more enemy, and its arcs hit 40% harder",
                Apply = s => { s.ShockJumpBonus += 1; s.ShockArcMult += 0.4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.RadiantOrb }, MaxStacks = 2, Name = "Sunstone", Desc = "The Radiant Orb is 30% bigger and heals 30% more",
                Apply = s => { s.OrbSizeMult += 0.3f; s.OrbHealMult2 += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.ChainLight }, MaxStacks = 2, Name = "Prism", Desc = "Chain Light's cone reaches 20% farther and is 20° wider",
                Apply = s => { s.ConeReachMult += 0.2f; s.ConeWidenBonus += 10f * MathX.Pi / 180f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Nimbus }, MaxStacks = 2, Name = "Cloudburst", Desc = "The Nimbus's rain circle is 30% wider and heals 30% more",
                Apply = s => { s.RainSizeMult += 0.3f; s.RainHealMult2 += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Fireball }, MaxStacks = 2, Name = "Wildfire", Desc = "The Fireball's burn is 50% hotter and lasts 1 s longer",
                Apply = s => { s.FireballBurnMult += 0.5f; s.FireballBurnBonus += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.ArcLightning }, MaxStacks = 2, Name = "Static Field", Desc = "Arc Lightning tags enemies 25% faster, and its stun lasts 0.2 s longer",
                Apply = s => { s.ArcChargeMult *= 0.75f; s.ArcStunBonus += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.IceShards }, MaxStacks = 2, Name = "Frozen Halo", Desc = "Ice Shards' orbit holds 2 more shards, and they launch at enemies 25% farther out",
                Apply = s => { s.OrbitExtra += 2; s.LaunchRangeMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Stonespear }, MaxStacks = 2, Name = "Tectonic", Desc = "The Stonespear raises one more spear a side, and its slam reaches 20% farther",
                Apply = s => { s.SsSideBonus += 1; s.SsSlamMult += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.WindClash }, MaxStacks = 2, Name = "Gale Force", Desc = "Wind Clash's waves reach 20% farther, and shove 30% harder",
                Apply = s => { s.WcReachMult += 0.2f; s.WcShoveMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.FlashFire, MaxStacks = 2, Name = "Backdraft", Desc = "Flash Fire reaches 20% farther, and what it leaves burning burns 50% hotter",
                Apply = s => { s.FfRadiusMult += 0.2f; s.FfBurnMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.StoneColossus, MaxStacks = 2, Name = "Bedrock", Desc = "Stone Colossus lasts 3 s longer, and its slams reach 25% wider",
                Apply = s => { s.ColBonusTime += Game.ColBedrockTime; s.ColSlamWide += Game.ColBedrockWide; } },
        };

        /// The Elementalist's 1st abilities (its ultimates are in Ultimates.cs). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] ElementalistAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.Aeroaegis, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 0,
                Name = "Aeroaegis", Icon = "aeroaegis", Cooldown = 11f, Desc = "A wind vortex wraps a player: enemies can't enter and bullets reflect back.",
                Capstone = "Lasts 4.5 s and reflects bullets for double damage.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.FrostNova, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Frost Nova", Icon = "snow", Cooldown = 12f, Desc = "Freeze every enemy near you and damage them.",
                Capstone = "Also erases enemy bullets around you.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.FlameWall, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 2,
                Name = "Flame Wall", Icon = "flame", Cooldown = 14f, Desc = "A wall of fire at your cursor that burns enemies and blocks bullets.",
                Capstone = "The fire also slows enemies by 40%.",
            },
            new AbilityDef
            {
                Id = AbilityId.Flood, Kit = Kit.Elementalist, Role = Role.Tank, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (Flood.cs)
                Name = "Flood", Icon = "flood", Cooldown = FloodCooldown, Desc = "Flood the ground around you: it throws enemies back, slows them and soaks them.",
                Capstone = "Deluge: the flood is 30% wider and lasts 9 s.",
                Bot = b => b.NearEnemies >= 3 || b.Danger && b.HaveTarget && b.TargetD < 260f,
            },
            new AbilityDef
            {
                Id = AbilityId.HealingRain, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 0,
                Name = "Healing Rain", Icon = "rain", Cooldown = 16f, Desc = "A rain at your cursor that heals allies, and slows and soaks enemies.",
                Capstone = "Lasts 10 s, and downed allies inside revive twice as fast.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.GustGate, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 1,  // Gust Gate (GustGate.cs)
                Name = "Gust Gate", Icon = "galewall", Cooldown = GgCooldown, Desc = "A wind ring that gives anyone passing through a speed boost.",
                Capstone = "The ring is 40% wider and pushes enemies twice as hard.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.LumenWard, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 2,  // Lumen Ward (LumenWard.cs)
                Name = "Lumen Ward", Icon = "aura", Cooldown = 20f, Desc = "A cone of light that heals allies and burns away enemy bullets.",
                Capstone = "Also freezes enemies outside the light for as long as it lasts (not bosses).",
                Bot = b => b.AllyHurt || b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.WaterWard, Kit = Kit.Elementalist, Role = Role.Healer, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (WaterWard.cs)
                Name = "Water Ward", Icon = "waterward", Cooldown = WwCooldown,
                Desc = "Water drops orbit you and splash enemies and eat shots.",
                Capstone = "Splashes and bursts reach 40% farther.",
                Bot = b => b.HaveTarget && b.TargetD < 300f || b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Meteor, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 0,
                Name = "Meteor", Icon = "explode", Cooldown = 10f, Desc = "Call a meteor down at your cursor.",
                Capstone = "Leaves burning ground for 3 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Blizzard, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 1,
                Name = "Blizzard", Icon = "snow", Cooldown = 14f, Desc = "A storm at your cursor that slows and damages enemies.",
                Capstone = "Lasts 10 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Tsunami, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 2,
                Name = "Tsunami", Icon = "cyclone", Cooldown = 16f, Desc = "A wave that soaks enemies, so lightning arcs to every one.",
                Capstone = "Wet lasts 14 s, and the wave rolls back to you, wetting what it missed.",
            },
            new AbilityDef
            {
                Id = AbilityId.GaleWall, Kit = Kit.Elementalist, Role = Role.Dps, Choice = AbilityDef.Fourth,
                Name = "Gale Wall", Icon = "galewall", Cooldown = 18f,
                Desc = "A wind wall that traps enemy shots, then unleashes them as a blast.",
                Capstone = "The wall is 40% wider and holds up to 60 charges.",
            },
        };
    }
}
