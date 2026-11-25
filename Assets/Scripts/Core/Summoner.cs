using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Summoner's class definition: its weapons, ability cards and abilities (their casts are in KitAbility,
    /// Minions.cs; their weapons fire in KitWeapons.cs).
    public partial class Game
    {
        /// The Summoner: weapons in FireKitWeapon, abilities in KitAbility (both here). See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef SummonerKit = new KitDef
        {
            Id = Kit.Summoner, Name = "Summoner", Icon = "class_summoner", HpMult = 0.9f,
            Blurb = "Fights through minions. Your weapon brings its own creatures (wolves, wisps, imps, bees) that hunt, bite and soak hits, and they stay with you between waves. Point them at what matters. 90% health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireKitWeapon(p, aim, dir, muzzle, dmg, extra),  // the Bear Totem has its own case
        };

        /// The Summoner's weapons (fired in FireKitWeapon; the Bear Totem in FireBearClaws).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] SummonerWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.GolemStaff, Kit = Kit.Summoner, Role = Role.Tank, Choice = 0, Damage = 40f, BotRange = 380f, ExtraDesc = "+1 Golem (up to 3)", Name = "Golem Staff", FireInterval = 0.5f, Desc = "Primary Fire: Hold to heave a boulder that crushes where it lands; a stone Golem fights beside you.",
                Companion = MinionKind.Golem, CompanionCount = 1,
                AreaDesc = "the boulder crushes 25% wider" },
            new WeaponDef { Id = WeaponId.BoneGuard, Kit = Kit.Summoner, Role = Role.Tank, Choice = 1, Speed = 720f, Life = 0.8f, Damage = 7f, ExtraDesc = "+1 Skeleton Guard", Name = "Bone Guard", FireInterval = 0.3f,
                Desc = "Primary Fire: Bone darts that cage enemies; skeleton guards block bullets and shoot caged enemies.\n\nAlternative Fire: Raise an Ossuary Wall.",
                Companion = MinionKind.Guard, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.BearTotem, Kit = Kit.Summoner, Role = Role.Tank, Choice = 2, Damage = 11f, Reach = 85f, Grow = 15f, Melee = true, ExtraDesc = "+15 px reach, 25% more bleed, and your Bear's bites hit enemies around its target", Name = "Bear Totem", FireInterval = 0.45f,
                Desc = "Primary Fire: Quick claw swipes that make enemies bleed; a Bear mauls nearby enemies.",
                Companion = MinionKind.Bear, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.WyvernStaff, Kit = Kit.Summoner, Role = Role.Tank, Choice = 3, Damage = 22f, BotRange = 300f, ExtraDesc = "+25% breath damage, and a 10% bigger gale and slam", Name = "Wyvern Staff", FireInterval = 0.1f,
                Desc = "Primary Fire: A Wyvern slows and shoves enemies under it; hold to make it breathe fire.\n\nAlternative Fire: Wyrm Slam: a stunning dive.",
                Companion = MinionKind.Wyvern, CompanionCount = 1,
                Sound = Sfx.SpellFire, AreaDesc = "the gale and the slam are 15% wider" },
            // Healer
            new WeaponDef { Id = WeaponId.SpriteStaff, Kit = Kit.Summoner, Role = Role.Healer, Choice = 0, Speed = 540f, Life = 1f, Damage = 7f, ExtraDesc = "+1 Sprite", Name = "Sprite Staff", FireInterval = 0.5f,
                Desc = "Primary Fire: A firefly that seeds an enemy and hops on; sprites heal whoever is hurt.",
                Companion = MinionKind.Sprite, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.TreantRod, Kit = Kit.Summoner, Role = Role.Healer, Choice = 1, Damage = 15f, Reach = 250f, BotRange = 210f, ExtraDesc = "Your Treant grows 30% bigger: blocks more, hits harder and reaches farther", Name = "Treant Rod", FireInterval = 0.55f,
                Desc = "Primary Fire: A thorny root whips out along your aim; your Treant blocks bullets and heals along its root.",
                Sound = Sfx.SwingRoot, AreaDesc = "the lash is 25% wider",
                Companion = MinionKind.Treant, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.SpiritChalk, Kit = Kit.Summoner, Role = Role.Healer, Choice = 2, Damage = 26f, Reach = 450f, BotRange = 300f, ExtraDesc = "+20% ink, so a longer line before the start rubs out", Name = "Spirit Chalk", FireInterval = 0.1f,
                Desc = "Primary Fire: Hold and drag to draw ink that burns enemies and heals allies; circle enemies to trap them.\n\nAlternative Fire: Ignite your loops to burn what they hold.",
                Sound = Sfx.None, AreaDesc = "the line is 25% wider" },
            // DPS
            new WeaponDef { Id = WeaponId.WolfWhistle, Kit = Kit.Summoner, Role = Role.Dps, Choice = 0, Speed = 720f, Life = 0.8f, Damage = 6.5f, ExtraDesc = "+1 starting wolf", Name = "Wolf Whistle", FireInterval = 0.3f,
                Desc = "Primary Fire: Darts mark prey for your wolves; kills grow the pack.\n\nAlternative Fire: Merge the pack into an alpha.",
                Companion = MinionKind.Wolf, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.WispLantern, Kit = Kit.Summoner, Role = Role.Dps, Choice = 1, Damage = 22f, BotRange = 340f, ExtraDesc = "+1 Wisp, and each shot sends one more", Name = "Wisp Lantern", FireInterval = 0.25f,
                Desc = "Primary Fire: Wisps orbit you; each shot sends one to strike the nearest enemy and return.",
                Companion = MinionKind.Wisp, CompanionCount = 3,
                Sound = Sfx.SpellArcane, HomingDesc = "wisps pick an enemy 50% farther from your cursor per stack, and one sent at empty ground turns onto an enemy near it. 5% less damage per stack" },
            new WeaponDef { Id = WeaponId.ImpCaller, Kit = Kit.Summoner, Role = Role.Dps, Choice = 2, BotRange = 380f, ExtraDesc = "+1 Imp, and they arrive 50% faster", Name = "Imp Caller", FireInterval = 0.35f,
                Desc = "Primary Fire: Imps wait by you: tap to send one to explode at your cursor.\n\nAlternative Fire: Hold to merge them into one big imp.",
                Companion = MinionKind.Imp, CompanionCount = 5, CompanionRespawn = 1f,
                HomingDesc = "imps pick an enemy 50% farther from your cursor per stack, and one sent at empty ground turns onto an enemy near it. 5% less damage per stack" },
            new WeaponDef { Id = WeaponId.HoneybeeStaff, Kit = Kit.Summoner, Role = Role.Dps, Choice = 3, Damage = 22f, BotRange = 360f, ExtraDesc = "+1 bee a click, and 2 more can be out", Name = "Honeybee Staff", FireInterval = 0.4f,
                Desc = "Primary Fire: Click to hatch a bee that stings the nearest enemy; three stings in a row swarm it.\n\nAlternative Fire: Spray slowing honey; enemies that die in it burst into bees.",
                HomingDesc = "bees look for enemies 25% farther out per stack",
                BarrageDesc = "each click hatches one more bee (full damage)" },
        };

        /// The Summoner's own upgrade cards: Rare upgrades of its 1st abilities (RequiresAbility), and cards for its weapons
        /// (Weapons) or the whole class (Kit). Upgrades.All gathers every class's list (Docs/UpgradePools.md).
        internal static readonly UpgradeDef[] SummonerCards =
        {
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.SpiritChalk }, MaxStacks = 1, Name = "Warding Ink", Desc = "Spirit Chalk blocks enemy bullets, but every bullet it stops wears the ink away fast",
                Apply = s => s.ChalkWard = true },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SoulLantern, MaxStacks = 2, Name = "Kindred Spirits", Desc = "Soul Lantern heals twice as fast", Apply = s => s.LinkHealMult += 1f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.GolemWall, MaxStacks = 2, Name = "Basalt Hide", Desc = "Golem Wall's golems have 50% more health",
                Apply = s => { s.GolemHpMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Earthquake, MaxStacks = 2, Name = "Deep Tremor", Desc = "Earthquake's Burrower tunnels 1.5 s longer and hits 50% harder",
                Apply = s => { s.QuakeBonusTime += 1.5f; s.QuakeDmgMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ForcePact, MaxStacks = 2, Name = "Kindred Bond", Desc = "Force Pact's ally has 50% more health and comes back 5 s sooner",
                Apply = s => { s.PactHpMult += 0.5f; s.PactReturnCut += 5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Stampede, MaxStacks = 2, Name = "Thundering Herd", Desc = "Stampede tramples and slams 50% harder",
                Apply = s => s.StampedeDmgMult += 0.5f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.RodentMinigun, MaxStacks = 2, Name = "Plague Rats", Desc = "Rodent Minigun fires nearly twice as many rats, and every rat makes what it bites bleed",
                Apply = s => { s.RatShare += Game.RmPlagueShare; s.RatBleed += Game.RmPlagueBleed; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.MammothFall, MaxStacks = 2, Name = "Ice Age", Desc = "Mammoth Fall's shockwave reaches 25% farther and stuns 0.5 s longer",
                Apply = s => { s.MammothWaveMult += Game.MfIceAgeWave; s.MammothStun += Game.MfIceAgeStun; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.LandShark, MaxStacks = 2, Name = "Great White", Desc = "Land Shark bites 40% harder",
                Apply = s => s.LandSharkDmgMult += 0.4f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SpiritSwarm, MaxStacks = 2, Name = "Sprite Choir", Desc = "Spirit Swarm's sprites heal 40% more and last 2 s longer",
                Apply = s => { s.SpriteHealMult += 0.4f; s.SpriteBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Phoenix, MaxStacks = 2, Name = "Rebirth Flame", Desc = "The phoenix stays 4 s longer, and whoever it revives gets 25% more health",
                Apply = s => { s.PhoenixBonusTime += 4f; s.PhoenixReviveBonus += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BatSwarm, MaxStacks = 2, Name = "Blood Bats", Desc = "Bat Swarm's bats bite 40% harder and hunt 2 s longer",
                Apply = s => { s.BatDmgMult += 0.4f; s.BatBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Hydra, MaxStacks = 2, Name = "Venom Glands", Desc = "The hydra's venom slows its target 30% for 1.5 s, and the hydra stays 2 s longer",
                Apply = s => { s.HydraSlow = Math.Min(0.6f, s.HydraSlow + 0.3f); s.HydraBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.AlphaCall, MaxStacks = 2, Name = "Pack Leader", Desc = "Alpha Call's Blood Scent lasts 3 s longer and your minions start it with 1 more stack",
                Apply = s => { s.ScentBonusTime += 3f; s.ScentStartBonus += 1; } },
            new UpgradeDef { Rarity = Rarity.Rare, Kit = Arena.Kit.Summoner, MaxStacks = 2, Name = "Pack Bond", Desc = "Every minion of yours (weapon and ability) hits 15% harder and takes 25% more before it breaks or falls",
                Apply = s => { s.MinionDmgMult += 0.15f; s.MinionToughMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.GolemStaff }, MaxStacks = 2, Name = "Granite Golem", Desc = "The Golem Staff's Golem takes 40% more before it breaks, and the boulder crushes 20% wider",
                Apply = s => { s.StaffGolemTough += 0.4f; s.BoulderSplashMult += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.BoneGuard }, MaxStacks = 2, Name = "Ossuary", Desc = "Bone Guard's cages hold 0.5 s longer, and its skeletons shoot 30% faster",
                Apply = s => { s.CageBonusTime += 0.5f; s.SkeletonRateMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.BearTotem }, MaxStacks = 2, Name = "Deep Claws", Desc = "The Bear Totem's claws reach 15% farther, and their bleed is 30% stronger",
                Apply = s => { s.ClawReachMult += 0.15f; s.ClawBleedMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.SpriteStaff }, MaxStacks = 2, Name = "Wildfire Seeds", Desc = "The Sprite Staff's firefly hops to 1 more enemy, and its sprites heal 20% more",
                Apply = s => { s.FireflyHopBonus += 1; s.StaffSpriteHealMult += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.TreantRod }, MaxStacks = 2, Name = "Deep Roots", Desc = "The Treant Rod's root whip reaches 20% farther and heals allies it touches 30% more",
                Apply = s => { s.RootReachMult += 0.2f; s.RootHealMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.WolfWhistle }, MaxStacks = 2, Name = "Alpha Pack", Desc = "Wolf Whistle takedowns hit 30% harder and stun 0.3 s longer",
                Apply = s => { s.TakedownMult += 0.3f; s.TakedownStunBonus += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.WispLantern }, MaxStacks = 2, Name = "Will-o'-Wisp", Desc = "One more wisp orbits you",
                Apply = s => { s.WispExtra += 1; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.ImpCaller }, MaxStacks = 2, Name = "Imp Horde", Desc = "Hold 1 more imp, and they come 20% faster",
                Apply = s => { s.ImpExtra += 1; s.ImpRateMult += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.HoneybeeStaff }, MaxStacks = 2, Name = "Bigger Hive", Desc = "An enemy dying in the Honeybee Staff's honey bursts into one more bee, and its puddles spread 20% wider",
                Apply = s => { s.HarvestBonus += 1; s.PuddleSizeMult += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.WyvernStaff }, MaxStacks = 2, Name = "Stormwing", Desc = "The Wyvern's gale shoves and slows 25% harder, and the Wyrm Slam is 25 px wider",
                Apply = s => { s.WyvernPower += 0.25f; } },
        };

        /// The Summoner's 1st abilities (its ultimates are in Ultimates.cs). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] SummonerAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.GolemWall, Kit = Kit.Summoner, Role = Role.Tank, Choice = 0,
                Name = "Golem Wall", Icon = "guard_wide", Cooldown = 13f, Desc = "Raise three golems that block every enemy bullet.",
                Capstone = "Raises five golems.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Earthquake, Kit = Kit.Summoner, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Earthquake", Icon = "earthshaker", Cooldown = 10f, Desc = "Slam the ground, then a Burrower tunnels through enemies.",
                Capstone = "Also freezes everything it hurts for 1 s (not bosses).",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.ForcePact, Kit = Kit.Summoner, Role = Role.Tank, Choice = 2,
                Name = "Force Pact", Icon = "guard_reinforced", Cooldown = 12f, Desc = "Make a regular enemy fight for you.",
                Capstone = "A second thrall: the pact keeps the last two types you chose.",
                Bot = b => b.HaveTarget,  // it takes whatever is nearest the cursor, so any target will do
            },
            new AbilityDef
            {
                Id = AbilityId.Stampede, Kit = Kit.Summoner, Role = Role.Tank, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (Stampede.cs)
                Name = "Stampede", Cooldown = StampedeCooldown, Desc = "A line of spirit beasts tramples everything ahead.",
                Capstone = "9 beasts, and the herd turns and charges back.",
                Bot = b => b.HaveTarget && b.TargetD < 380f,
            },
            new AbilityDef
            {
                Id = AbilityId.SpiritSwarm, Kit = Kit.Summoner, Role = Role.Healer, Choice = 0,
                Name = "Spirit Swarm", Icon = "regen2", Cooldown = 14f, Desc = "Release healing sprites.",
                Capstone = "Releases 8 sprites.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.Phoenix, Kit = Kit.Summoner, Role = Role.Healer, Choice = 1,
                Name = "Phoenix", Icon = "flame", Cooldown = 22f, Desc = "A phoenix follows your cursor, shooting and healing, and revives anyone who goes down.",
                Capstone = "Revives at full health, and the phoenix keeps fighting.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.SoulLantern, Kit = Kit.Summoner, Role = Role.Healer, Choice = 2,
                Name = "Soul Lantern", Icon = "halo", Cooldown = 18f, Desc = "A lantern links to an ally, healing them and burning nearby enemies.",
                Capstone = "Lasts 9 s, and the linked player can't go down.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.BatSwarm, Kit = Kit.Summoner, Role = Role.Dps, Choice = 0,
                Name = "Bat Swarm", Icon = "brood", Cooldown = 12f, Desc = "Release bats that hunt enemies.",
                Capstone = "Releases 14 bats.",
            },
            new AbilityDef
            {
                Id = AbilityId.Hydra, Kit = Kit.Summoner, Role = Role.Dps, Choice = 1,
                Name = "Hydra", Icon = "split", Cooldown = 18f, Desc = "A three-headed hydra spits venom at enemies.",
                Capstone = "A fourth head.",
                Bot = b => b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.AlphaCall, Kit = Kit.Summoner, Role = Role.Dps, Choice = 2,
                Name = "Alpha Call", Icon = "fangs", Cooldown = 14f, Desc = "Your minions bite faster, then lunge for triple damage.",
                Capstone = "Also summons 3 wolves for 6 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.LandShark, Kit = Kit.Summoner, Role = Role.Dps, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (LandShark.cs)
                Name = "Land Shark", Cooldown = LandSharkCooldown, Desc = "A puddle opens at your cursor and a shark leaps out, biting everything in it.",
                Capstone = "A 30% wider puddle, and the bite hits for 160.",
                Bot = b => b.HaveTarget && b.TargetD < LandSharkRange,
            },
        };
    }
}
