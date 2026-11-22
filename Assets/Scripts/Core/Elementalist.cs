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
            Blurb = "Spells of fire, frost, lightning, water, wind, earth and light. Elements combine: soaked enemies conduct lightning, burning ones keep burning, frozen ones slow down. Big area damage, at 85% health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireKitWeapon(p, aim, dir, muzzle, dmg, extra),
        };

        /// The Elementalist's weapons (Kit.Elementalist; fired in FireKitWeapon).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] ElementalistWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Flamethrower, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 0, Speed = 480f, Life = 0.4f, Damage = 3f, Grow = 0.25f, BotRange = 160f, ExtraDesc = "+1 flame a burst and 25% more reach", Name = "Flamethrower", FireInterval = 0.06f,
                Desc = "Primary Fire: A short stream of fire that burns everything it hits.",
                Sound = Sfx.SpellFire },
            new WeaponDef { Id = WeaponId.FrostLance, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 1, Damage = 18f, Reach = 178f, BotRange = 160f, ExtraDesc = "20% more damage and 10% more reach", Name = "Frost Lance", FireInterval = 0.4f,
                Desc = "Primary Fire: A five-step lance combo that chills, freezes, then shatters frozen enemies.\n\nAlternative Fire: Absolute Zero: a freezing burst around you.",
                Sound = Sfx.SpellFrost, AreaDesc = "every sweep, crystal and blast is 25% bigger" },
            new WeaponDef { Id = WeaponId.WindClash, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 3, Damage = 18f, Reach = 170f, BotRange = 150f, ExtraDesc = "+20% damage and 10% bigger waves", Name = "Wind Clash", FireInterval = 0.34f,
                Desc = "Primary Fire: Hold for a wind combo that hurts and shoves every enemy it passes.\n\nAlternative Fire: A storm around you hurts enemies and catches their shots, but slows you.",
                Sound = Sfx.None, AreaDesc = "the waves and the Tempest 25% bigger" },
            new WeaponDef { Id = WeaponId.StormHammer, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 2, Speed = 460f, Life = 1.1f, Damage = 40f, BotRange = 260f, ExtraDesc = "+15% hammer damage, and the electrified enemy arcs to 1 more enemy", Name = "Storm Hammer", FireInterval = 0.7f,
                Desc = "Primary Fire: Throw a hammer that smashes one enemy and arcs lightning to its neighbours.",
                Sound = Sfx.SpellZap, AreaDesc = "the electrified enemy's lightning reaches 25% farther" },
            // Healer
            new WeaponDef { Id = WeaponId.RadiantOrb, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 0, Damage = 8f, Reach = 520f, BotRange = 300f, ExtraDesc = "+1 light orb that circles your Radiant Orb (you, when it's not out), hurting enemies and healing allies it touches", Name = "Radiant Orb", FireInterval = 0.2f,
                Desc = "Primary Fire: Hold to guide an orb of light: it burns enemies and heals allies it passes over.",
                Sound = Sfx.SpellHoly, BarrageDesc = "+1 light orb per stack that circles your orb, hurting enemies and healing allies it touches" },
            new WeaponDef { Id = WeaponId.ChainLight, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 1, Damage = 40f, BotRange = 240f, ExtraDesc = "+25% damage and a 15% longer cone", Name = "Chain Light", FireInterval = 0.5f,
                Desc = "Primary Fire: A cone of light whose damage is shared between every enemy in it.",
                Sound = Sfx.SpellZap, AreaDesc = "the cone of light 25% wider", HomingDesc = "the cone of light 15% narrower and 10% stronger per stack, instead of bending shots: fewer enemies share its damage", BarrageDesc = "+1 cone of light beside the first, sharing its own damage. Reaches more enemies; a lone one is still in one cone" },
            new WeaponDef { Id = WeaponId.Nimbus, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 2, Damage = 16f, Reach = 380f, BotRange = 300f, ExtraDesc = "+1 lightning strike on every click, landing in a ring round the cloud", Name = "Nimbus", FireInterval = 0.6f,
                Desc = "Primary Fire: A storm cloud over your cursor: click to call lightning, and its rain heals allies and soaks enemies.\n\nAlternative Fire: The cloud becomes a hurricane that drags enemies in and heals allies.",
                Sound = Sfx.SpellZap, AreaDesc = "the strike, the rain and the hurricane are 25% wider",
                Companion = MinionKind.Nimbus, CompanionCount = 1 },
            // DPS
            new WeaponDef { Id = WeaponId.Fireball, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 0, Speed = 430f, Life = 1.4f, Damage = 32f, BotRange = 390f, ExtraDesc = "+1 fireball, fanned out", Name = "Fireball", FireInterval = 0.5f,
                Desc = "Primary Fire: A big, slow fireball that rolls through enemies and sets them burning.",
                Sound = Sfx.ShotFireball },
            new WeaponDef { Id = WeaponId.ArcLightning, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 1, Damage = 16f, BotRange = 320f, ExtraDesc = "+25% damage", Name = "Arc Lightning", FireInterval = 0.25f,
                Desc = "Primary Fire: Hold to tag up to 10 enemies, let go to chain lightning through them and stun.\n\nAlternative Fire: Three forked bolts at the enemies nearest your cursor.",
                Sound = Sfx.SpellZap, AreaDesc = "the tagging cone 25% wider", BarrageDesc = "+2 tags a stack (up to 16), and the chain grows with them" },
            new WeaponDef { Id = WeaponId.IceShards, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 2, Speed = 520f, Life = 1f, Damage = 7f, ExtraDesc = "+1 shard a cast, +2 in orbit, and faster launches", Name = "Ice Shards", FireInterval = 0.3f,
                Desc = "Primary Fire: Ice shards orbit you, then launch homing at nearby enemies.",
                Sound = Sfx.SpellArcane },
            new WeaponDef { Id = WeaponId.Stonespear, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 3, Speed = 900f, Life = 0.6f, Damage = 24f, BotRange = 420f, ExtraDesc = "+1 spear a side, and each click throws one more", Name = "Stonespear", FireInterval = 0.18f,
                Desc = "Primary Fire: Slam the ground, then throw the earth spears that rise from it, one per click.\n\nAlternative Fire: Hurl every spear at once.",
                Sound = Sfx.None, BarrageDesc = "each throw also flings a fanned copy of the spear (60% damage) that uses none up" },
            // The Fireball's variants (Variants.cs; ElementBalls.cs)
            new WeaponDef { Id = WeaponId.Waterball, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 4, Speed = 430f, Life = 1.4f, Damage = 28f, BotRange = 390f, ExtraDesc = "+1 waterball, fanned out", Name = "Waterball", Variant = true, FireInterval = 0.5f,
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
        };

        /// The Elementalist's 1st abilities (its ultimates are in Ultimates.cs). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] ElementalistAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.Aeroaegis, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 0,
                Name = "Aeroaegis", Cooldown = 11f, Desc = "For 3 s, a wind vortex wraps the player your cursor is nearest: enemies can't get in, and bullets that touch it are reflected back as your own.",
                Capstone = "Lasts 4.5 s and reflects bullets for double damage.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.FrostNova, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Frost Nova", Cooldown = 12f, Desc = "Freeze every enemy near you for 3 s and deal 40 damage.",
                Capstone = "Also erases enemy bullets around you.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.FlameWall, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 2,
                Name = "Flame Wall", Cooldown = 14f, Desc = "A wall of fire at your cursor for 5 s: burns enemies and blocks bullets.",
                Capstone = "The fire also slows enemies by 40%.",
            },
            new AbilityDef
            {
                Id = AbilityId.Flood, Kit = Kit.Elementalist, Role = Role.Tank, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (Flood.cs)
                Name = "Flood", Cooldown = FloodCooldown, Desc = "Stamp and flood the ground 190 px round you for 6 s: enemies touching you are thrown back 60 px (10 damage), and every enemy wading in the water is slowed 40% (heavies and bosses 20%) and soaked wet, so lightning arcs to it, while it's in and 3 s after.",
                Capstone = "Deluge: the flood is 30% wider and lasts 9 s.",
                Bot = b => b.NearEnemies >= 3 || b.Danger && b.HaveTarget && b.TargetD < 260f,
            },
            new AbilityDef
            {
                Id = AbilityId.HealingRain, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 0,
                Name = "Healing Rain", Cooldown = 16f, Desc = "A rain at your cursor for 6 s: heals allies inside 8 HP/s and slows enemies by 30%.",
                Capstone = "Lasts 10 s, and downed allies inside revive twice as fast.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.GustGate, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 1,  // Gust Gate (GustGate.cs)
                Name = "Gust Gate", Cooldown = GgCooldown, Desc = "A ring of wind at your cursor for 8 s. Anyone who walks through it, you included, gets +80% move speed for 3 s. Enemies inside are pushed gently out.",
                Capstone = "The ring is 40% wider and pushes enemies twice as hard.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.LumenWard, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 2,  // Lumen Ward (LumenWard.cs)
                Name = "Lumen Ward", Cooldown = 20f, Desc = "A huge cone of light toward your cursor for 6 s that turns as you aim: heals everyone inside 9 HP/s and burns away enemy bullets in it or touching it.",
                Capstone = "Also freezes enemies outside the light for as long as it lasts (not bosses).",
                Bot = b => b.AllyHurt || b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.WaterWard, Kit = Kit.Elementalist, Role = Role.Healer, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (WaterWard.cs)
                Name = "Water Ward", Cooldown = WwCooldown,
                Desc = "Call 2 drops of water down round you; they circle you, up to 10. A drop that touches an enemy splashes: 12 damage to every enemy within 52 px, and they're soaked wet (slowed 30%, lightning arcs to them) for 4 s. A drop that meets an enemy shot eats it and bursts into a mist that soaks everything within 72 px.",
                Capstone = "Splashes and bursts reach 40% farther.",
                Bot = b => b.HaveTarget && b.TargetD < 300f || b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Meteor, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 0,
                Name = "Meteor", Cooldown = 10f, Desc = "Call a meteor at your cursor: after a short warning it hits for 150 damage.",
                Capstone = "Leaves burning ground for 3 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Blizzard, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 1,
                Name = "Blizzard", Cooldown = 14f, Desc = "A storm at your cursor for 5 s that slows and damages enemies.",
                Capstone = "Lasts 10 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Tsunami, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 2,
                Name = "Tsunami", Cooldown = 16f, Desc = "A wave rolls out where you aim: 12 damage, and it leaves enemies wet for 8 s, slowed 30%. Lightning (Arc Lightning, Storm Hammer, Chain Light, Nimbus) arcs to every wet enemy on the map.",
                Capstone = "Wet lasts 14 s, and the wave rolls back to you, wetting what it missed.",
            },
            new AbilityDef
            {
                Id = AbilityId.GaleWall, Kit = Kit.Elementalist, Role = Role.Dps, Choice = AbilityDef.Fourth,
                Name = "Gale Wall", Cooldown = 18f,
                Desc = "A curved wall of wind in front of you for 5 s: enemy shots that reach it stop and hang in it, each one a charge (up to 40). Press again (or when it ends) to unleash a big cone toward your cursor: 40 damage + 6 per charge, and enemies are shoved back.",
                Capstone = "The wall is 40% wider and holds up to 60 charges.",
            },
        };
    }
}
