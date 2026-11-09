using System;
using System.Collections.Generic;

namespace Arena
{
    /// Elementalist and Summoner kits: their abilities (KitAbility), minions and ground zones (host only). Their
    /// weapons are in KitWeapons.cs.
    public partial class Game
    {

        // --- Abilities ---------------------------------------------------------

        /// The Elementalist (Kit.Elementalist): weapons in FireKitWeapon, abilities in KitAbility (both here). See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef ElementalistKit = new KitDef
        {
            Id = Kit.Elementalist, Name = "Elementalist", Icon = "class_elementalist", HpMult = 0.85f,
            Blurb = "Spells: fire, frost, lightning and arcane magic. 85% health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireKitWeapon(p, aim, dir, muzzle, dmg, extra),
        };

        /// The Elementalist's weapons (Kit.Elementalist; fired in FireKitWeapon).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] ElementalistWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Flamethrower, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 0, Speed = 480f, Life = 0.4f, Damage = 3f, Grow = 0.25f, BotRange = 160f, ExtraDesc = "+1 flame a burst and 25% more reach", Name = "Flamethrower", FireInterval = 0.06f,
                Desc = "A short-range stream of fire. Each flame hits the first enemy hard and carries on through the rest, setting them all burning.",
                Sound = Sfx.SpellFire },
            new WeaponDef { Id = WeaponId.FrostLance, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 1, Damage = 18f, Reach = 230f, Grow = 30f, BotRange = 190f, ExtraDesc = "+30 px longer icicle and 25% more damage, and its splinters fly 15 px further", Name = "Frost Lance", FireInterval = 0.4f,
                Desc = "A great icicle juts out from you, then splinters off its sides into enemies beside it. Both slow.",
                Sound = Sfx.SpellFrost, AreaDesc = "the icicle's splinters fly 25% further out" },
            new WeaponDef { Id = WeaponId.StormHammer, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 2, Speed = 460f, Life = 1.1f, Damage = 40f, BotRange = 260f, ExtraDesc = "+15% hammer damage, and the electrified enemy arcs to 1 more enemy", Name = "Storm Hammer", FireInterval = 0.7f,
                Desc = "Throws a spinning hammer that smashes one enemy and electrifies it, so it arcs lightning to the enemies around it.",
                Sound = Sfx.SpellZap, AreaDesc = "the electrified enemy's lightning reaches 25% farther" },
            // Healer
            new WeaponDef { Id = WeaponId.RadiantOrb, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 0, Damage = 8f, Reach = 520f, BotRange = 300f, ExtraDesc = "+1 light orb that circles your Radiant Orb (you, when it's not out), hurting enemies and healing allies it touches", Name = "Radiant Orb", FireInterval = 0.2f,
                Desc = "Hold to cast an orb of light out to your cursor; it stays there while you hold. Drag it through enemies to burn them and over allies to heal them.",
                Sound = Sfx.SpellHoly, BarrageDesc = "+1 light orb per stack that circles your orb, hurting enemies and healing allies it touches" },
            new WeaponDef { Id = WeaponId.ChainLight, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 1, Damage = 40f, BotRange = 240f, ExtraDesc = "+25% damage and a 15% longer cone", Name = "Chain Light", FireInterval = 0.5f,
                Desc = "A great cone of light in front of you. Its damage is shared between every enemy in it, so one alone takes it all.",
                Sound = Sfx.SpellZap, AreaDesc = "the cone of light 25% wider", HomingDesc = "the cone of light 15% narrower and 10% stronger per stack, instead of bending shots: fewer enemies share its damage", BarrageDesc = "+1 cone of light beside the first, sharing its own damage. Reaches more enemies; a lone one is still in one cone" },
            new WeaponDef { Id = WeaponId.Nimbus, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 2, Damage = 16f, Reach = 380f, BotRange = 300f, ExtraDesc = "+1 lightning strike on every click, landing in a ring round the cloud", Name = "Nimbus", FireInterval = 0.6f,
                Desc = "A small storm cloud floats over your cursor. Click to call lightning down beneath it. Rain falls under the cloud and heals every ally standing in it, with no clicking.",
                Sound = Sfx.SpellZap, AreaDesc = "the strike and the rain are 25% wider",
                Companion = MinionKind.Nimbus, CompanionCount = 1 },
            // DPS
            new WeaponDef { Id = WeaponId.Fireball, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 0, Speed = 430f, Life = 1.4f, Damage = 22f, BotRange = 390f, ExtraDesc = "+1 fireball, fanned out", Name = "Fireball", FireInterval = 0.5f,
                Desc = "A big, slow fireball that rolls on through every enemy in its path, hurting each and setting it burning.",
                Sound = Sfx.SpellFire },
            new WeaponDef { Id = WeaponId.ArcLightning, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 1, Damage = 40f, BotRange = 320f, ExtraDesc = "+25% damage", Name = "Arc Lightning", FireInterval = 0.25f,
                Desc = "Hold to charge a strip of ground in front of you, longer the longer you hold. Let go: lightning shocks everything in it and stuns it.",
                Sound = Sfx.SpellZap, AreaDesc = "the lightning strip 25% wider", BarrageDesc = "+1 strip of lightning side by side with the first. Covers more ground; nothing is shocked twice" },
            new WeaponDef { Id = WeaponId.ArcaneMissiles, Kit = Kit.Elementalist, Role = Role.Dps, Choice = 2, Speed = 520f, Life = 1f, Damage = 7f, ExtraDesc = "+1 shard a cast, +2 in orbit, and faster launches", Name = "Ice Shards", FireInterval = 0.3f,
                Desc = "Each cast conjures shards of ice that orbit you. They launch at nearby enemies, homing, one after another. With the orbit full, a cast flies at your aim.",
                Sound = Sfx.SpellArcane },
        };

        /// The Mage's Space abilities (its ultimates are in Ultimates.cs). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] ElementalistAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.ArcaneAegis, Kit = Kit.Elementalist, Role = Role.Tank, Choice = 0,
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
                Id = AbilityId.HealingRain, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 0,
                Name = "Healing Rain", Cooldown = 16f, Desc = "A rain at your cursor for 6 s: heals allies inside 8 HP/s and slows enemies by 30%.",
                Capstone = "Lasts 10 s, and downed allies inside revive twice as fast.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.EnergyWard, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 1, Instant = true,
                Name = "Energy Ward", Cooldown = 14f, Desc = "Wrap you and allies within 300 px in a bubble of energy that soaks 60 damage for 8 s. It shrinks and turns green as it wears; when it breaks or runs out, half of what it soaked comes back as healing.",
                Capstone = "The shield soaks 120 damage.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Sanctify, Kit = Kit.Elementalist, Role = Role.Healer, Choice = 2,  // the AbilityId stays Sanctify; the player-facing name is Lumen Ward (LumenWard.cs)
                Name = "Lumen Ward", Cooldown = 20f, Desc = "A huge cone of light along your aim for 6 s: heals everyone inside 9 HP/s and burns away enemy bullets in it. Enemy bullets everywhere else slow to a stop and fly again when it ends.",
                Capstone = "Also freezes enemies outside the light for as long as it lasts (not bosses).",
                Bot = b => b.AllyHurt || b.Danger,
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
        };

        /// The Summoner: weapons in FireKitWeapon, abilities in KitAbility (both here). See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef SummonerKit = new KitDef
        {
            Id = Kit.Summoner, Name = "Summoner", Icon = "class_summoner", HpMult = 0.9f,
            Blurb = "Fights alongside minions that come with your weapon. 90% health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireKitWeapon(p, aim, dir, muzzle, dmg, extra),  // the Bear Totem has its own case
        };

        /// The Summoner's weapons (fired in FireKitWeapon; the Bear Totem in FireBearClaws).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] SummonerWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.GolemStaff, Kit = Kit.Summoner, Role = Role.Tank, Choice = 0, Damage = 40f, BotRange = 380f, ExtraDesc = "+1 Golem (up to 3)", Name = "Golem Staff", FireInterval = 0.5f, Desc = "Hold to heave a boulder, let go to throw it in an arc: the longer you hold, the farther it flies. It crushes everything where it lands. A rune-carved stone Golem (a small Titan) rises beside you, slams enemies and blocks bullets.",
                Companion = MinionKind.Golem, CompanionCount = 1,
                AreaDesc = "the boulder crushes 25% wider" },
            new WeaponDef { Id = WeaponId.BoneGuard, Kit = Kit.Summoner, Role = Role.Tank, Choice = 1, Speed = 720f, Life = 0.8f, Damage = 7f, ExtraDesc = "+1 Skeleton Guard", Name = "Bone Guard", FireInterval = 0.3f,
                Desc = "Bone darts that stick in enemies: enough in one cages it in place (not bosses). Skeleton guards circle you, block bullets, and shoot at caged enemies.",
                Companion = MinionKind.Guard, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.BearTotem, Kit = Kit.Summoner, Role = Role.Tank, Choice = 2, Damage = 11f, Reach = 85f, Grow = 15f, Melee = true, ExtraDesc = "+15 px reach, 25% more bleed, and your Bear's bites hit enemies around its target", Name = "Bear Totem", FireInterval = 0.45f,
                Desc = "Two quick claw swipes in front of you that make enemies bleed. A Bear companion mauls nearby enemies.",
                Companion = MinionKind.Bear, CompanionCount = 1 },
            // Healer
            new WeaponDef { Id = WeaponId.SpriteStaff, Kit = Kit.Summoner, Role = Role.Healer, Choice = 0, Speed = 540f, Life = 1f, Damage = 7f, ExtraDesc = "+1 Sprite", Name = "Sprite Staff", FireInterval = 0.5f,
                Desc = "A firefly that seeds the enemy it hits, then hops to nearby enemies that aren't seeded yet. Sprites fly to whoever is hurt and heal them, and every seeded enemy you kill frees another sprite for a while.",
                Companion = MinionKind.Sprite, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.TreantRod, Kit = Kit.Summoner, Role = Role.Healer, Choice = 1, Damage = 15f, Reach = 250f, BotRange = 210f, ExtraDesc = "Your Treant grows 30% bigger: blocks more, hits harder and reaches farther", Name = "Treant Rod", FireInterval = 0.55f,
                Desc = "A thorny root whips out along your aim and snaps back, hitting everything on the line and healing allies it touches. Your Treant roots where it stands and blocks bullets; a root runs from you to it, healing allies who cross it and slowing enemies.",
                Sound = Sfx.SwingRoot, AreaDesc = "the lash is 25% wider",
                Companion = MinionKind.Treant, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.SpiritChalk, Kit = Kit.Summoner, Role = Role.Healer, Choice = 2, Damage = 26f, Reach = 450f, BotRange = 300f, ExtraDesc = "+20% ink, so a longer line before the start rubs out", Name = "Spirit Chalk", FireInterval = 0.1f,
                Desc = "Hold and drag to draw a line of spirit ink. It burns and slows enemies touching it, heals allies standing on it and destroys enemy bullets that cross it. Draw a circle round enemies to trap them inside (not bosses).",
                Sound = Sfx.None, AreaDesc = "the line is 25% wider" },
            // DPS
            new WeaponDef { Id = WeaponId.WolfWhistle, Kit = Kit.Summoner, Role = Role.Dps, Choice = 0, Speed = 720f, Life = 0.8f, Damage = 6.5f, ExtraDesc = "+1 Wolf", Name = "Wolf Whistle", FireInterval = 0.3f,
                Desc = "Darts mark the enemy they hit as prey, and your wolves hunt it. When every wolf bites it close together, they pull off a takedown that hurts and stuns.",
                Companion = MinionKind.Wolf, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.WispLantern, Kit = Kit.Summoner, Role = Role.Dps, Choice = 1, Damage = 22f, BotRange = 340f, ExtraDesc = "+1 Wisp, and each shot sends one more", Name = "Wisp Lantern", FireInterval = 0.25f,
                Desc = "Wisps orbit you, and they are your shots: each time you fire, one flies at the enemy nearest your cursor, strikes it and comes back.",
                Companion = MinionKind.Wisp, CompanionCount = 3,
                Sound = Sfx.SpellArcane, HomingDesc = "wisps pick an enemy 50% farther from your cursor per stack, and one sent at empty ground turns onto an enemy near it. 5% less damage per stack" },
            new WeaponDef { Id = WeaponId.ImpCaller, Kit = Kit.Summoner, Role = Role.Dps, Choice = 2, BotRange = 380f, ExtraDesc = "+1 Imp, and they arrive 50% faster", Name = "Imp Caller", FireInterval = 0.35f,
                Desc = "Imps arrive over time and wait by you. Tap fire to send one charging at your cursor to explode; hold to merge them all into one big imp whose blast is bigger than theirs put together.",
                Companion = MinionKind.Imp, CompanionCount = 5, CompanionRespawn = 1f,
                HomingDesc = "imps pick an enemy 50% farther from your cursor per stack, and one sent at empty ground turns onto an enemy near it. 5% less damage per stack" },
        };

        /// The Summoner's Space abilities (its ultimates are in Ultimates.cs). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] SummonerAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.GolemWall, Kit = Kit.Summoner, Role = Role.Tank, Choice = 0,
                Name = "Golem Wall", Cooldown = 13f, Desc = "Raise three basalt golems in front of you. They block every enemy bullet and stay until they are broken (200 health each); casting again replaces them.",
                Capstone = "Raises five golems.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Earthquake, Kit = Kit.Summoner, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Earthquake", Cooldown = 10f, Desc = "You slam the ground (25 damage, slows), then a Burrower breaks out at your cursor and tunnels through the enemies for 3 s, hurting each one it passes under (30 damage).",
                Capstone = "Also freezes everything it hurts for 1 s (not bosses).",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.GuardianSpirit, Kit = Kit.Summoner, Role = Role.Tank, Choice = 2,
                Name = "Force Pact", Cooldown = 12f, Desc = "Take the regular enemy nearest your cursor (not bosses) and make it fight for you. It stays near you with its own attack and comes back 15 s after it falls. Cast again on another enemy to switch the pact to that type.",
                Capstone = "A second thrall: the pact keeps the last two types you chose.",
                Bot = b => b.HaveTarget,  // it takes whatever is nearest the cursor, so any target will do
            },
            new AbilityDef
            {
                Id = AbilityId.SpiritSwarm, Kit = Kit.Summoner, Role = Role.Healer, Choice = 0,
                Name = "Spirit Swarm", Cooldown = 14f, Desc = "Release 5 healing sprites for 6 s.",
                Capstone = "Releases 8 sprites.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.Phoenix, Kit = Kit.Summoner, Role = Role.Healer, Choice = 1,
                Name = "Phoenix", Cooldown = 22f, Desc = "A phoenix fights for 10 s. If anyone goes down (you too), it revives them instantly.",
                Capstone = "Revives at full health, and the phoenix keeps fighting.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.SoulLantern, Kit = Kit.Summoner, Role = Role.Healer, Choice = 2,
                Name = "Soul Lantern", Cooldown = 18f, Desc = "A lantern appears beside the ally nearest your cursor (you, alone) and links to them for 6 s: it follows, heals 8 health a second and flames nearby enemies.",
                Capstone = "Lasts 9 s, and the linked player can't go down.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.BatSwarm, Kit = Kit.Summoner, Role = Role.Dps, Choice = 0,
                Name = "Bat Swarm", Cooldown = 12f, Desc = "Release 8 bats that hunt enemies for 6 s.",
                Capstone = "Releases 14 bats.",
            },
            new AbilityDef
            {
                Id = AbilityId.Hydra, Kit = Kit.Summoner, Role = Role.Dps, Choice = 1,
                Name = "Hydra", Cooldown = 18f, Desc = "A three-headed hydra rises at your cursor for 8 s. Each head picks its own enemy and spits venom at it (10 damage, every 0.6 s per head).",
                Capstone = "A fourth head.",
                Bot = b => b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.AlphaCall, Kit = Kit.Summoner, Role = Role.Dps, Choice = 2,
                Name = "Alpha Call", Cooldown = 14f, Desc = "Blood Scent for 6 s: your minions start at 2 stacks, and each bite adds one (up to 5): faster, and quicker bites. At 5 stacks the next bite lunges for triple damage and drops back to 2.",
                Capstone = "Also summons 3 wolves for 6 s.",
            },
        };

        public const float FrostNovaFxTime = 3.3f;
        // Alpha Call's Blood Scent: stacks per minion, and what each one adds to its speed and attack rate (2 stacks is about the old flat frenzy)
        public const int ScentStart = 2, ScentMax = 5;
        public const float ScentSpeedPer = 0.16f, ScentRatePer = 0.5f, ScentLunge = 3f;

        void KitAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            float dmg = s.DmgMult;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                // Mage
                case AbilityId.ArcaneAegis:  // Aeroaegis (Aegis.cs): the vortex goes to whoever your cursor is nearest
                {
                    var ward = AegisTarget(p, target);
                    ward.AegisT = cap ? 4.5f : 3f;
                    ward.AegisHp = AegisCapacity * pw;
                    ward.AegisReflectMult = pw * (cap ? 2f : 1f);
                    KeepOutOfAegis(0f);  // enemies already inside are put out at the rim
                    BroadcastFx(FxKind.Ring, ward.Pos, new Rgba(0.84f, 1f, 0.97f), AegisRadius + 70f, 0.5f, Sfx.Ability);
                    break;
                }
                case AbilityId.FrostNova:
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) > 220f + e.Radius)
                            continue;
                        HurtEnemy(e, 40f * dmg, p.Id);
                        e.SlowT = 3f;
                        e.SlowMult = 0.1f;  // nearly frozen
                    }
                    if (cap)
                        EraseEnemyBullets(p.Pos, 220f);
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.FrostShot), 220f, FrostNovaFxTime, Sfx.Freeze);  // lasts as long as the freeze: the frost stays on the ground (Game/FrostNovaLook.cs)
                    break;
                case AbilityId.FlameWall:
                    AddZone(ZoneKind.FlameWall, p, target, 90f, 5f).ExtraSlow = cap ? 0.4f : 0f;
                    BroadcastFx(FxKind.Ring, target, Kits.Get(ZoneKind.FlameWall).Color, 90f, 0.4f, Sfx.SpellFire);
                    break;
                case AbilityId.HealingRain:
                    AddZone(ZoneKind.HealingRain, p, target, 130f, cap ? 10f : 6f).ExtraRevive = cap ? 1f : 0f;
                    BroadcastFx(FxKind.Ring, target, Kits.Get(ZoneKind.HealingRain).Color, 130f, 0.4f, Sfx.SpellHoly);
                    break;
                case AbilityId.EnergyWard:
                    foreach (var q in Players.Values)
                        if (!q.Downed && q.Pos.DistanceTo(p.Pos) < 300f)
                        {
                            if (q.EnergyOn)
                                EnergyPayback(q);  // a fresh ward pays out the old one first
                            q.ShieldHp = q.EnergyMax = (cap ? 120f : 60f) * pw;
                            q.ShieldT = 8f;
                            q.EnergyOn = true;
                            q.EnergyStore = 0f;
                            q.EnergyBy = p.Id;
                            castHits++;
                            if (q != p)
                                BroadcastFx(FxKind.Link, p.Pos, q.Pos, new Rgba(0.5f, 0.7f, 1f), 0f, 0.45f, Sfx.None);
                        }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.5f, 0.7f, 1f), 300f, 0.5f, Sfx.Ability);
                    break;
                case AbilityId.Sanctify:  // Lumen Ward (LumenWard.cs)
                    CastLumenWard(p, target, cap);
                    break;
                case AbilityId.Meteor:
                    AddZone(ZoneKind.Meteor, p, target, 120f, 0.9f).Embers = cap;  // the zone is the warning; it hits when it expires
                    Sounds.Add(Sfx.SpellFire);
                    break;
                case AbilityId.Blizzard:
                    AddZone(ZoneKind.Blizzard, p, target, 130f, cap ? 10f : 5f);
                    BroadcastFx(FxKind.Ring, target, Kits.Get(ZoneKind.Blizzard).Color, 130f, 0.4f, Sfx.Freeze);
                    break;
                case AbilityId.Tsunami:
                    CastTsunami(p, cap, pw);
                    break;
                // Summoner
                case AbilityId.GolemWall:
                {
                    V2 fwd = V2.FromAngle(p.Aim), side = fwd.Orthogonal();
                    int half = cap ? 2 : 1;
                    var old = new List<int>();  // a new wall replaces the caster's old one
                    foreach (var m in Minions.Values)
                        if (m.Kind == MinionKind.Wall && m.Owner == p.Id)
                            old.Add(m.Id);
                    foreach (int oldId in old)
                        Minions.Remove(oldId);
                    for (int i = -half; i <= half; i++)
                        SpawnMinion(MinionKind.Wall, p, p.Pos + fwd * GolemWallAhead + side * (i * GolemWallGap), -1f);  // -1: no timer, it stands until broken
                    Sounds.Add(Sfx.Summon);
                    break;
                }
                case AbilityId.Earthquake:
                    CastEarthquake(p, target, cap, dmg);  // Burrower.cs
                    break;
                case AbilityId.GuardianSpirit:
                    ForcePactCast(p, target, cap);  // ForcePact.cs
                    break;
                case AbilityId.SpiritSwarm:
                {
                    int sprites = cap ? 8 : 5;
                    for (int i = 0; i < sprites; i++)
                        SpawnMinion(MinionKind.Sprite, p, p.Pos + V2.FromAngle(MathX.Tau * i / sprites) * 20f, 6f).HealRate = 10f * pw;
                    Sounds.Add(Sfx.Summon);
                    break;
                }
                case AbilityId.Phoenix:
                    SpawnMinion(MinionKind.Phoenix, p, p.Pos, 10f);
                    Sounds.Add(Sfx.Summon);
                    break;
                case AbilityId.SoulLantern:
                    SpawnSoulLantern(p, target, cap, pw);  // SoulLantern.cs
                    break;
                case AbilityId.BatSwarm:
                {
                    int bats = cap ? 14 : 8;
                    for (int i = 0; i < bats; i++)
                    {
                        var bat = SpawnMinion(MinionKind.Bat, p, p.Pos + V2.FromAngle(MathX.Tau * i / bats) * 15f, 6f);
                        bat.Ang = MathX.Tau * i / bats;
                        bat.Launch = BatGuardTime + i * (BatPeelSpan / bats);  // they circle you first, then peel off one by one
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.7f, 0.5f, 0.95f), 120f, 0.5f, Sfx.Summon);  // the bell toll
                    break;
                }
                case AbilityId.Hydra:
                    CastHydra(p, target, pw);  // Hydra.cs (mastered: a fourth head)
                    break;
                case AbilityId.AlphaCall:
                    p.FrenzyT = 6f * pw;
                    p.ScentT = 6f * pw;  // Blood Scent: the minions' stacks build while it runs (ScentStart..ScentMax)
                    if (cap)
                        for (int i = 0; i < 3; i++)
                            SpawnMinion(MinionKind.Wolf, p, p.Pos + V2.FromAngle(MathX.Tau * i / 3f) * 30f, 6f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.5f, 0.3f), 120f, 0.4f, Sfx.Roar);
                    break;
                default:
                    foreach (var cast in Classes.AbilityHooks)  // each class's KitDef.Ability (Knight.cs, Ninja.cs, BloodMage.cs)
                        if (cast(this, p, id, target))
                            return;
                    Ultimate(p, id, target);  // Ultimates.cs
                    break;
            }
        }

        // --- Minions -----------------------------------------------------------

        const float BatGuardTime = 0.6f, BatPeelSpan = 0.6f, BatRingNear = 34f, BatRingGrow = 18f;  // Bat Swarm: circle the owner 0.6 s, then each bat peels off in turn over 0.6 s

        public const float GolemWallAhead = 112f, GolemWallGap = 76f;  // Golem Wall: golems touch (radius 38), their backs clear the caster

        Minion SpawnMinion(MinionKind kind, Player owner, V2 pos, float life, bool companion = false)
        {
            var m = new Minion
            {
                Id = nextMinionId++, Kind = kind, Owner = owner.Id, Pos = ClampToArena(pos, 10f), T = life, Duration = life,
                Companion = companion, Ang = Rand(0f, MathX.Tau), Aim = owner.Aim,
            };
            m.Disp = m.Pos;
            m.HealRate = m.Def.HealRate;
            m.BlockHp = m.Def.BlockCapacity;
            if (casting)
            {
                m.Power = castPower;
                m.Mastered = castMastered;
                m.HealRate *= castPower;
                m.BlockHp *= castPower;
            }
            Minions[m.Id] = m;
            return m;
        }

        void RemoveTemporaryMinions()
        {
            var gone = new List<int>();
            foreach (var m in Minions.Values)
                if (!m.Companion)
                    gone.Add(m.Id);
            foreach (int id in gone)
                Minions.Remove(id);
        }

        void ExplodeMinion(Minion m, float dmg, float radius)
        {
            bool shot = m.Sent && m.Kind == MinionKind.Imp;  // an Imp Caller imp is the weapon's shot: weapon mods ride its blast
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(m.Pos) < radius + e.Radius)
                {
                    HurtEnemy(e, dmg, m.Owner);
                    if (shot)
                        OnBulletHit(new Bullet { Pos = e.Pos, Owner = m.Owner, Dmg = dmg, Kind = Defs.FireShot }, e);
                }
            BroadcastFx(FxKind.Boom, m.Pos, m.Def.Color, radius, 0.35f, Sfx.Explode);
            Minions.Remove(m.Id);
        }

        Enemy NearestEnemy(V2 pos, float range)
        {
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(pos) < range && (best == null || e.Pos.DistanceSq(pos) < best.Pos.DistanceSq(pos)))
                    best = e;
            return best;
        }

        void TickMinions(float dt)
        {
            // Summoner weapons keep their companions topped up; the totem staff plants totems.
            foreach (var p in Players.Values)
            {
                TickGuards(p, dt);  // Warden's Guard (Stun.cs)
                TickPact(p, dt);  // Force Pact's thralls (ForcePact.cs)
                TickHiveDrones(p, dt);  // the Hive Queen's Hive Drones and Bomber Escort (QueenLoot.cs)
                TickBombers(p, dt);
                TickTwinScythes(p, dt);  // the Twin Reapers' Twin Scythes (ReaperLoot.cs)
                var w = p.Weapon;
                var kind = CompanionKind(p);
                int extra = ExtraShots(p);
                if (kind != MinionKind.None)
                {
                    int want = CompanionCount(p), have = 0;
                    Minion surplus = null;
                    foreach (var m in Minions.Values)
                        if (m.Owner == p.Id && m.Companion && m.Kind != MinionKind.WardenGuard && m.Kind != MinionKind.Thrall && m.Kind != MinionKind.HiveDrone && m.Kind != MinionKind.Scythe)
                        {
                            have++;
                            surplus = m;
                            float scale = m.Kind == MinionKind.Treant ? 1f + 0.3f * extra : m.Kind == MinionKind.Golem ? GolemScale : 1f;
                            if (scale != m.Scale)
                            {
                                if (m.StaggerT <= 0f)
                                    m.BlockHp *= scale / m.Scale;  // keeps the same fraction of a bigger guard
                                m.Scale = scale;
                            }
                        }
                    if (have > want && surplus != null && kind != MinionKind.Imp)
                        Minions.Remove(surplus.Id);  // Artillery Assault's extras leave when it ends
                    p.CompanionCd -= dt;
                    if (have < want && p.CompanionCd <= 0f)
                    {
                        var m = SpawnMinion(kind, p, p.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * 30f, kind == MinionKind.Golem ? GolemLife : -1f, true);
                        if (kind == MinionKind.LightOrb)
                            m.Ang = MathX.Tau * have / Math.Max(1, want);
                        p.CompanionCd = kind == MinionKind.Imp ? w.CompanionRespawn / (1f + 0.5f * extra) : 0.3f;
                    }
                }
            }

            var expired = scratchMinionsExpired;
            var exploding = scratchMinionsExploding;
            expired.Clear();
            exploding.Clear();
            foreach (var m in Minions.Values)
            {
                Player owner;
                if (!Players.TryGetValue(m.Owner, out owner))
                {
                    expired.Add(m);
                    continue;
                }
                if (m.T >= 0f)
                {
                    m.T -= dt;
                    if (m.T <= 0f)
                    {
                        if (m.Kind == MinionKind.Titan)
                            TitanFallen(m);  // Titan.cs
                        expired.Add(m);
                        continue;
                    }
                }
                var d = m.Def;
                m.HurtT -= dt;
                m.InvulnT -= dt;
                if (m.StaggerT > 0f)
                {
                    m.StaggerT -= dt;
                    if (m.StaggerT <= 0f)
                        m.BlockHp = d.BlockCapacity * m.Scale;  // back on its feet
                }
                bool frenzy = owner.FrenzyT > 0f && !m.NoFrenzy;
                bool scent = owner.ScentT > 0f && !m.NoFrenzy;  // Alpha Call: the frenzy builds with each bite
                if (scent && m.Stacks < ScentStart)
                    m.Stacks = ScentStart;
                else if (!scent && m.Stacks != 0)
                    m.Stacks = 0;
                float speed = d.Speed * (scent ? 1f + ScentSpeedPer * m.Stacks : frenzy ? 1.6f : 1f) * (m.NoFrenzy && owner.LegionT > 0f ? LegionSpeed : 1f);
                float rate = owner.Stats.FireRateMult * (scent ? 1f + ScentRatePer * m.Stacks : frenzy ? 2f : 1f);
                float dmg = d.Damage * owner.Stats.DmgMult * (frenzy ? 1.3f : 1f) * m.Scale * m.Power;
                float reach = d.Range * m.Scale;
                m.Cd -= dt * rate;

                if (m.Kind == MinionKind.Thrall)
                {
                    TickThrall(m, owner, dt);  // ForcePact.cs
                    continue;
                }
                if (m.Kind == MinionKind.SoulLantern)
                {
                    if (!TickSoulLantern(m, owner, dt))  // its player is gone (SoulLantern.cs)
                        expired.Add(m);
                    continue;
                }
                if (m.Kind == MinionKind.Butterfly)
                {
                    if (MoveButterfly(m, owner, dt))  // WorldTree.cs
                        expired.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if (m.Kind == MinionKind.Hydra)
                {
                    TickHydra(m, dmg);  // Hydra.cs
                    continue;
                }
                if (m.Kind == MinionKind.Burrower)
                {
                    TickBurrower(m, owner, dt, dmg);  // Burrower.cs
                    continue;
                }
                if (IsTitanLike(m.Kind))
                {
                    TickTitan(m, owner, dt, speed, dmg, reach);  // Titan.cs
                    continue;
                }
                if (m.Kind == MinionKind.HiveDrone)
                {
                    MoveDrone(m, owner, dt);  // QueenLoot.cs
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if (m.Kind == MinionKind.Scythe)
                {
                    MoveScythe(m, owner, dt);  // ReaperLoot.cs
                    continue;
                }
                if (m.Kind == MinionKind.ThrownScythe)
                {
                    if (MoveThrownScythe(m, owner, dt))  // caught (ReaperLoot.cs)
                        expired.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if (m.Kind == MinionKind.HiveBomber)
                {
                    if (MoveBomber(m, dt))  // QueenLoot.cs
                        exploding.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if ((m.Kind == MinionKind.Wisp && owner.Weapon.Id == WeaponId.WispLantern || m.Kind == MinionKind.Imp && owner.Weapon.Id == WeaponId.ImpCaller) && m.Companion)
                {
                    if (MoveSent(m, owner, dt))  // sent at something by the Wisp Lantern or Imp Caller (they don't act on their own)
                        exploding.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }

                if (m.Hold > 0f)  // Legion's pack circles its owner before it is let loose
                {
                    m.Hold -= dt;
                    m.Ang += 7f * dt;
                    float ring = m.Kind == MinionKind.Wolf ? LegionRing : LegionRing * 0.65f;
                    m.Pos += (owner.Pos + V2.FromAngle(m.Ang) * ring - m.Pos).LimitLength(900f * dt);
                    continue;
                }

                // Movement
                Enemy target = null;
                switch (d.Move)
                {
                    case MinionMove.Orbit:
                        m.Ang += speed * dt;
                        if (m.Kind == MinionKind.LightOrb && owner.RadiantOn)  // Radiant Orb's light orbs circle the orb while it's out
                            m.Pos += (owner.RadiantPos + V2.FromAngle(m.Ang) * LightOrbRing - m.Pos).LimitLength(LightOrbSpeed * dt);
                        else if (m.Kind == MinionKind.LightOrb)
                            m.Pos += (owner.Pos + V2.FromAngle(m.Ang) * d.OrbitRadius - m.Pos).LimitLength(LightOrbSpeed * dt);
                        else
                            m.Pos = owner.Pos + V2.FromAngle(m.Ang) * d.OrbitRadius;
                        break;
                    case MinionMove.Chase:
                    {
                        if (m.Launch > 0f)  // a Bat Swarm bat still circling its owner
                        {
                            m.Launch -= dt;
                            V2 before = m.Pos;
                            m.Ang += 6f * dt;
                            float ring = BatRingNear + Math.Min((m.Duration - m.T) * 40f, BatRingGrow);
                            m.Pos = owner.Pos + V2.FromAngle(m.Ang) * ring;
                            m.Aim = (m.Pos - before).Angle;
                            break;
                        }
                        target = (m.Kind == MinionKind.Wolf ? Prey(owner) : null) ?? NearestEnemy(m.Pos, MinionChaseRange);  // wolves hunt the Wolf Whistle's prey
                        V2 goal = target != null ? target.Pos : owner.Pos + V2.FromAngle(m.Id) * 50f;
                        V2 to = goal - m.Pos;
                        float stop = target != null ? target.Radius + d.Radius : 10f;
                        if (to.Length > stop)
                            m.Pos += to.Normalized() * Math.Min(speed * dt, to.Length - stop);
                        if (m.Kind == MinionKind.Bat && to.Length > 1f)
                            m.Aim = to.Angle;  // faces where it's flying (the bat's look streaks behind it)
                        break;
                    }
                    case MinionMove.FollowHurt:
                    {
                        // Go to whoever has the lowest health fraction; otherwise stay by the owner.
                        Player who = null;
                        foreach (var q in Players.Values)
                            if (!q.Downed && q.Hp < q.MaxHp && (who == null || q.Hp / q.MaxHp < who.Hp / who.MaxHp))
                                who = q;
                        V2 goal = (who ?? owner).Pos + V2.FromAngle(m.Ang + AnimT) * 25f;
                        m.Pos += (goal - m.Pos).LimitLength(speed * dt);
                        break;
                    }
                    case MinionMove.Escort:
                        target = MoveGuard(m, owner, speed, dt);  // Stun.cs
                        break;
                    case MinionMove.FollowOwner:
                    {
                        // The treant stands in front of you; the guardian spirit hovers on you.
                        V2 goal = m.Kind == MinionKind.Treant ? TreantGoal(m, owner)
                            : m.Kind == MinionKind.Nimbus ? NimbusGoal(owner)  // the cloud drifts after your cursor (Nimbus.cs)
                            : m.Kind == MinionKind.SoulGhost ? owner.Pos + V2.FromAngle(owner.Aim + 2f) * 60f  // a Soul Ghost keeps beside you, a little back
                            : owner.Pos;
                        m.Pos += (goal - m.Pos).LimitLength(speed * dt);
                        break;
                    }
                }
                m.Pos = ClampToArena(m.Pos, d.Radius);

                // Attacks
                switch (d.Attack)
                {
                    case MinionAttack.Shoot:
                    {
                        var e = NearestEnemy(m.Pos, d.Range);
                        if (e == null)
                            break;
                        m.Aim = (e.Pos - m.Pos).Angle;
                        if (m.Cd <= 0f && State == MatchState.Fighting)
                        {
                            m.Cd = d.Interval;
                            V2 dir = V2.FromAngle(m.Aim);
                            byte kind = m.Kind == MinionKind.Turret ? Defs.TurretShot : m.Kind == MinionKind.Phoenix ? Defs.FireShot
                                : m.Kind == MinionKind.SoulGhost ? Defs.HealerShot : Defs.MinionShot;  // the ghost fires its healer's light bolts
                            SpawnBullet(kind, m.Pos + dir * d.Radius, dir * 650f, 0f, d.Range / 650f, dmg, m.Owner);
                        }
                        break;
                    }
                    case MinionAttack.Bite:
                    {
                        Enemy caged = m.Kind == MinionKind.Guard ? NearestCaged(m.Pos) : null;
                        if (caged != null)  // Skeleton Guards shoot at enemies the Bone Guard has caged
                        {
                            m.Aim = (caged.Pos - m.Pos).Angle;
                            if (m.Cd <= 0f && State == MatchState.Fighting)
                            {
                                m.Cd = d.Interval;
                                V2 dir = V2.FromAngle(m.Aim);
                                SpawnBullet(Defs.MinionShot, m.Pos + dir * d.Radius, dir * 650f, 0f, GuardShotRange / 650f, dmg, m.Owner);
                            }
                            break;
                        }
                        var e = target ?? NearestEnemy(m.Pos, d.Radius + 40f);
                        if (e != null && m.Cd <= 0f && e.Pos.DistanceTo(m.Pos) < e.Radius + d.Radius + 6f)
                        {
                            m.Cd = d.Interval;
                            m.Aim = (e.Pos - m.Pos).Angle;
                            HurtEnemy(e, dmg, m.Owner);
                            if (m.Kind == MinionKind.Bat && m.BiteN++ % 2 == 0)  // a crescent slash across the prey (Game/BatLook.cs draws the bat)
                                BroadcastFx(FxKind.Slash, m.Pos, e.Pos, new Rgba(0.85f, 0.7f, 1f), 0.9f, 0.2f, Sfx.None);
                            if (scent)
                            {
                                if (m.Stacks >= ScentMax)  // a full stack lunges: triple damage in all, then back to 2
                                {
                                    HurtEnemy(e, dmg * (ScentLunge - 1f), m.Owner);
                                    m.Stacks = ScentStart;
                                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.95f, 0.15f, 0.2f), 48f, 0.35f, Sfx.None);
                                }
                                else
                                    m.Stacks++;
                            }
                            if (m.Kind == MinionKind.Wolf)
                                PackBite(owner, m, e);
                            // Bear Totem's extra projectiles: the bear's bites also hit enemies around its target.
                            int extraBear = m.Kind == MinionKind.Bear && owner.Weapon.Id == WeaponId.BearTotem ? ExtraShots(owner) : 0;
                            if (extraBear > 0)
                            {
                                foreach (var o in Enemies.Values)
                                    if (o != e && o.SpawnT <= 0f && o.Pos.DistanceTo(e.Pos) < 55f + o.Radius)
                                        HurtEnemy(o, dmg * Math.Min(1f, 0.4f * extraBear), m.Owner);
                                BroadcastFx(FxKind.Ring, e.Pos, d.Color, 55f, 0.25f, Sfx.None);
                            }
                        }
                        if (m.Kind == MinionKind.LightOrb)  // also heals allies it passes over
                            foreach (var q in Players.Values)
                                if (!q.Downed && q.Pos.DistanceTo(m.Pos) < reach + d.Radius)
                                    Heal(q, m.HealRate * dt, owner);
                        break;
                    }
                    case MinionAttack.Slam:
                        if (m.Kind == MinionKind.Treant)
                            TickTreantSeeds(m, owner, rate, dt);  // KitWeapons.cs
                        if (m.Cd <= 0f && NearestEnemy(m.Pos, reach) != null)
                        {
                            m.Cd = d.Interval;
                            foreach (var e in Enemies.Values)
                                if (e.SpawnT <= 0f && e.Pos.DistanceTo(m.Pos) < reach + e.Radius)
                                    HurtEnemy(e, dmg, m.Owner);
                            BroadcastFx(FxKind.Ring, m.Pos, d.Color, reach, 0.3f, Sfx.None);
                        }
                        break;
                    case MinionAttack.Explode:
                        if (target != null && target.Pos.DistanceTo(m.Pos) < target.Radius + d.Radius + 6f)
                            exploding.Add(m);
                        break;
                    case MinionAttack.Heal:
                        foreach (var q in Players.Values)
                            if (q.Pos.DistanceTo(m.Pos) < d.Range)
                                Heal(q, m.HealRate * dt, owner);
                        break;
                }

                // The phoenix trades itself to bring back a downed ally.
                // Mastered, it revives them at full health and keeps fighting.
                if (m.Kind == MinionKind.Phoenix)
                    foreach (var q in Players.Values)
                        if (q.Downed)
                        {
                            RevivePlayer(q, m.Mastered ? 1f : 0.5f, owner);
                            BroadcastFx(FxKind.Ring, q.Pos, d.Color, 90f, 0.6f, Sfx.Revive);
                            if (!m.Mastered)
                                expired.Add(m);
                            break;
                        }
            }
            foreach (var m in exploding)
                if (Minions.ContainsKey(m.Id))
                    ExplodeMinion(m, m.Def.Damage * OwnerDmg(m) * m.Power * SentCost(m), m.Def.Range * m.Scale);  // a merged imp blasts wider
            foreach (var m in expired)
                Minions.Remove(m.Id);
        }

        /// Homing Rounds' damage cost on an Imp Caller imp sent at something (the homing it pays for is in MoveSent).
        float SentCost(Minion m)
        {
            Player p;
            return m.Sent && m.Kind == MinionKind.Imp && Players.TryGetValue(m.Owner, out p) ? HomingCost(p) : 1f;
        }

        float OwnerDmg(Minion m)
        {
            Player p;
            return Players.TryGetValue(m.Owner, out p) ? p.Stats.DmgMult : 1f;
        }

        /// Golems, guards, treants, walls, the guardian spirit and flame walls eat enemy bullets.
        /// They only stop bullets coming in from outside, and each soaks a limited amount
        /// of damage: walls and the guardian break, companions are staggered for a few seconds.
        bool BlockedBySummons(Bullet b)
        {
            foreach (var m in Minions.Values)
            {
                if (m.Def.BlockRadius <= 0f || m.StaggerT > 0f || m.BlockHp <= 0f || (IsTitanLike(m.Kind) && !TitanBlocking(m)) || !Enters(b, m.Pos, m.Def.BlockRadius * m.Scale))
                    continue;
                m.BlockHp -= m.Kind == MinionKind.CloneWall || m.Kind == MinionKind.Clone || m.Kind == MinionKind.Wall ? b.Dmg * EnemyDamageScale(Wave) : BlockerWear(b);  // a wall clone or a golem wears from every shot, not just a boss's
                if (m.BlockHp <= 0f)
                {
                    if (m.Companion)
                        m.StaggerT = 4f;
                    else
                        m.T = 0.01f;  // breaks at the end of this tick
                    BroadcastFx(FxKind.Boom, m.Pos, m.Def.Color, m.Def.BlockRadius * m.Scale, 0.3f, Sfx.Hit);
                }
                return true;
            }
            foreach (var z in Zones.Values)
            {
                if (z.BlockHp <= 0f || !Enters(b, z.Pos, z.Radius))
                    continue;
                z.BlockHp -= BlockerWear(b);
                if (z.BlockHp <= 0f)
                    BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius, 0.4f, Sfx.Hit);  // still burns, no longer blocks
                return true;
            }
            return false;
        }

        // --- Zones -------------------------------------------------------------

        internal Zone AddZone(ZoneKind kind, Player owner, V2 pos, float radius, float duration)
        {
            var z = new Zone { Id = nextZoneId++, Kind = kind, Owner = owner.Id, Pos = pos, Radius = radius, Duration = duration, T = duration, DmgMult = owner.Stats.DmgMult };
            z.BlockHp = z.Def.BlockCapacity;
            if (casting)
            {
                z.DmgMult *= castPower;
                z.HealMult = castPower;
                z.BlockHp *= castPower;
            }
            Zones[z.Id] = z;
            return z;
        }

        public const float EnergyRepay = 0.5f;  // an Energy Ward pays back this share of what it soaked, as healing, when it ends or breaks

        void EnergyPayback(Player p)
        {
            p.EnergyOn = false;
            p.EnergyMax = 0f;
            float heal = p.EnergyStore * EnergyRepay;
            p.EnergyStore = 0f;
            if (heal <= 0f || p.Downed)
                return;
            Heal(p, heal, PlayerOrNull(p.EnergyBy));
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.5f, 0.7f, 1f), 40f, 0.4f, Sfx.SpellHoly);
        }

        // Reused every tick by TickMinions and TickZones.
        readonly List<Minion> scratchMinionsExpired = new List<Minion>();
        readonly List<Minion> scratchMinionsExploding = new List<Minion>();
        readonly List<Zone> scratchZonesExpired = new List<Zone>();

        void TickZones(float dt)
        {
            var expired = scratchZonesExpired;
            expired.Clear();
            foreach (var z in Zones.Values)
            {
                var d = z.Def;
                z.T -= dt;
                Player follow;
                if (z.Follow && Players.TryGetValue(z.Owner, out follow))
                    z.Pos = follow.Pos;
                if (z.T <= 0f)
                {
                    expired.Add(z);
                    continue;
                }
                if (z.Kind == ZoneKind.LumenWard)
                {
                    TickLumen(z, dt);  // a cone, not a circle (LumenWard.cs)
                    continue;
                }
                foreach (var q in Players.Values)
                {
                    if (q.Pos.DistanceTo(z.Pos) > z.Radius)
                        continue;
                    if (q.Downed && d.ReviveBoost + z.ExtraRevive > 0f)
                    {
                        q.Revive += dt / D(DiffKnob.ReviveTime) * (d.ReviveBoost + z.ExtraRevive);  // on top of the normal revive
                        q.ReviveBy = z.Owner;
                    }
                    else if (d.HealRate > 0f && !HallowedSpent(z))
                        Heal(q, d.HealRate * z.HealMult * (d.ReviveBoost > 0f && Players.Count == 1 ? SoloZoneHeal : 1f) * dt, PlayerOrNull(z.Owner));  // alone, nobody to revive: heals faster
                }
                if (z.Kind == ZoneKind.Sanctum)
                    SanctumPulse(z, dt);  // Sanctum.cs
                if (z.Kind == ZoneKind.HallowedBlade)
                    HallowedPulse(z, dt);  // HallowedBlade.cs
                if (z.Kind == ZoneKind.Grove)
                    WorldTreeWave(z, dt);  // WorldTree.cs
                if (z.Kind == ZoneKind.BloodPool && !z.Congealed && z.Duration - z.T >= PoolCongeal)
                    CongealPool(z);  // BloodMage.cs
                if (z.Kind == ZoneKind.VeinTrail)
                    TickVeinTrail(z);  // VeinRush.cs
                if (z.Kind == ZoneKind.Drawbridge)
                    TickDrawbridge(z);  // Drawbridge.cs
                if (z.Kind == ZoneKind.Bloodbath)
                    TickBloodbath(z, dt);  // Bloodbath.cs
                if (z.Kind == ZoneKind.Phalanx)
                {
                    TickPhalanx(z, dt);  // BonePhalanx.cs
                    continue;  // (not a circle: nothing below applies)
                }
                if (d.Dps > 0f || d.Slow > 0f || z.ExtraSlow > 0f)
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(z.Pos) > z.Radius + e.Radius)
                            continue;
                        if (d.Dps > 0f && !HallowedSpent(z))
                            HurtEnemy(e, d.Dps * z.DmgMult * dt, z.Owner);
                        if (z.Kind == ZoneKind.BulletRain)
                            e.WetT = Math.Max(e.WetT, BulletRainWetTime);  // soaked: slowed, and lightning arcs to it (Tsunami.cs)
                        float slow = Math.Max(d.Slow, z.ExtraSlow);
                        if (slow > 0f)
                        {
                            e.SlowT = Math.Max(e.SlowT, 0.3f);
                            e.SlowMult = Math.Min(e.SlowMult, 1f - slow);
                        }
                    }
            }
            foreach (var z in expired)
            {
                if (z.Kind == ZoneKind.Ward)
                    WardEnds(z);  // Ninja.cs
                if (z.Def.BlastDamage > 0f)  // the meteor lands
                {
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && e.Pos.DistanceTo(z.Pos) < z.Radius + e.Radius)
                            HurtEnemy(e, z.Def.BlastDamage * z.DmgMult, z.Owner);
                    if (z.Kind == ZoneKind.Meteor)
                        BroadcastFx(FxKind.Meteor, z.Pos, z.Def.Color, z.Radius, 2.8f, Sfx.BigExplode);  // flash, ring, sparks and a scorch mark (ElementalistLooks.cs)
                    else
                        BroadcastFx(FxKind.Boom, z.Pos, z.Def.Color, z.Radius, 0.5f, Sfx.BigExplode);
                    Player caster;
                    if (z.Consecrates && Players.TryGetValue(z.Owner, out caster))
                    {
                        var holy = AddZone(ZoneKind.HallowedBlade, caster, z.Pos, z.Radius, HallowedLife);  // the sword stays planted (HallowedBlade.cs)
                        holy.DmgMult = z.DmgMult;
                        holy.HealMult = z.HealMult;
                    }
                    if (z.Embers && Players.TryGetValue(z.Owner, out caster))
                    {
                        // A mastered Meteor leaves burning ground (it doesn't block bullets).
                        var burn = AddZone(ZoneKind.FlameWall, caster, z.Pos, 60f, 3f);
                        burn.DmgMult = z.DmgMult;
                        burn.BlockHp = 0f;
                    }
                }
                Zones.Remove(z.Id);
            }
        }

        bool InLantern(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Def.PreventDown && p.Pos.DistanceTo(z.Pos) <= z.Radius)
                    return true;
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.SoulLantern && m.Mastered && m.LinkTo == p.Id)
                    return true;  // a mastered Soul Lantern's linked player can't go down (SoulLantern.cs)
            return false;
        }

        // --- Time Rewind -------------------------------------------------------

        float historyT;

        /// Every 0.25 s, remember each player's health, overwriting the entry from 4 s ago.
        void RecordHistory(float dt)
        {
            historyT += dt;
            if (historyT < 0.25f)
                return;
            historyT = 0f;
            foreach (var p in Players.Values)
            {
                p.HpHistory[p.HistoryIdx] = p.Hp;
                p.DownHistory[p.HistoryIdx] = p.Downed;
                p.HistoryIdx = (p.HistoryIdx + 1) % p.HpHistory.Length;
            }
        }
    }
}
