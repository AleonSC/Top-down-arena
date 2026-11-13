using System.Collections.Generic;

namespace Arena
{
    /// One of an enemy's attacks, as the Field guide's Bestiary lists it.
    public class BestiaryAttack
    {
        public string Name, Pattern;
        public string Shot;          // the kind of bullet ("Small shot", "Aimed shot"...), or null
        public Rgba ShotColor;
        public string Count, Speed, Damage, Notes;  // shown as written (null: not shown)
        public float Every = -1f;    // seconds between volleys at wave 1 (shorter on later waves unless Fixed), or -1
        public string EveryText;     // ...or a timing that isn't a plain number
        public bool Fixed;           // doesn't speed up with the wave
    }

    /// An enemy or boss in the in-game Bestiary (the pause menu's Field guide). Its numbers (health, speed, size, XP)
    /// come from its EnemyDef; this holds the words.
    public class BestiaryEntry
    {
        public EnemyType Type;
        public string Name, Group, Appears, Lead;
        public int Wave;             // the first wave it can show up in
        public string[] Stages;      // a staged boss's stages, in order
        public BestiaryAttack[] Attacks = new BestiaryAttack[0];
        public string[] Notes = new string[0];
    }

    /// The in-game Bestiary. Most entries are in BestiaryData.Entries.cs (hand-edited); the boss adds it doesn't list are below.
    public static partial class BestiaryData
    {
        public static readonly string[] Groups = { "Melee", "Shooters", "Late game", "Bosses", "Summons", "Challenge rooms", "Zombies" };

        static readonly BestiaryEntry[] Extra =
        {
            new BestiaryEntry
            {
                Type = EnemyType.Rioter, Name = "Rioter", Group = "Summons", Wave = 1,
                Appears = "The Warden's approach (waves 1-4, in pairs and Breakout squads), and his Riot stage: cell doors burst open, 9 at a time",
                Lead = "A prisoner on the loose. Runs at whoever is nearest, the Warden included.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "10" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Thug, Name = "Thug", Group = "Melee", Wave = 2,
                Appears = "The Warden's approach (Yard squads and on its own)",
                Lead = "A prisoner brute dragging a ball and chain. Slow, tough, and it hits hard. Moves like a Brute.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "20", Notes = "Walks straight at its target" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Shiv, Name = "Shiv", Group = "Melee", Wave = 3,
                Appears = "The Warden's approach (Yard squads and on its own)",
                Lead = "A prisoner with a taped shank. Circles in close, flashes a lane, then lunges along it. Moves like a Lunger.",
                Attacks = new[] { new BestiaryAttack { Name = "Lunge", Pattern = "Wind-up 0.6 s (shows a lane) then a leap, same as the Lunger", Damage = "26", Notes = "18 contact" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Wisp, Name = "Wisp", Group = "Melee", Wave = 1,
                Appears = "The Twin Reapers' approach (in threes, Vigil and Harvest squads)",
                Lead = "A soul flame sworn to Dusk or Dawn. Drifts straight at you and burns on touch. Moves like a Swarmer.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "6" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Acolyte, Name = "Acolyte", Group = "Melee", Wave = 1,
                Appears = "The Twin Reapers' approach (Vigil and Procession squads)",
                Lead = "A hooded cultist with a sickle. Stops short, raises it over a pale cone, then one wide swing.",
                Attacks = new[] { new BestiaryAttack { Name = "Sickle", Pattern = "Wind-up 0.5 s over a 120 degree cone, 96 px", Damage = "12", Notes = "Like a Swordsman, shorter and wider" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.BoundWraith, Name = "Bound Wraith", Group = "Melee", Wave = 2,
                Appears = "The Twin Reapers' approach (always a pair; Twin graves squads)",
                Lead = "Two wraiths circling near you, joined by a tether that hurts. Kill one and the other channels it back in 3 s at 60% health, unless you kill it too.",
                Attacks = new[] { new BestiaryAttack { Name = "Tether", Pattern = "Sweeps as the pair circles 75 px out", Damage = "10" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.ScytheWraith, Name = "Scythe Wraith", Group = "Melee", Wave = 3,
                Appears = "The Twin Reapers' approach (Harvest squads)",
                Lead = "Marks a long line through you, locks it, then dashes along it and reaps both sides of its path.",
                Attacks = new[] { new BestiaryAttack { Name = "Reap", Pattern = "Mark 0.8 s (locks after 0.25 s), then a 270 px dash", Damage = "22", Notes = "Hits 26 px either side" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Lamplighter, Name = "Lamplighter", Group = "Shooters", Wave = 2,
                Appears = "The Twin Reapers' approach (Procession squads)",
                Lead = "Keeps its distance and plants its feet as its lantern flares; gold lines show the volley.",
                Attacks = new[] { new BestiaryAttack { Name = "Lines / Cross", Pattern = "Three lines at you, then a four-way cross, streaks of 4", Damage = "Small shots", Notes = "Every 3.2 s" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Mourner, Name = "Mourner", Group = "Shooters", Wave = 3,
                Appears = "The Twin Reapers' approach (the Dusk and Dawn finale)",
                Lead = "Stands still and weeps a twin spiral, one arm each way, for 2.5 s, then rests for 3 s.",
                Attacks = new[] { new BestiaryAttack { Name = "Twin spiral", Pattern = "Two arms turning opposite ways", Damage = "Small shots" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Gravedigger, Name = "Gravedigger", Group = "Melee", Wave = 2,
                Appears = "The Twin Reapers' approach (Twin graves squads)",
                Lead = "A cultist brute with a shovel. Slow, tough, and it hits hard. Moves like a Brute.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "20", Notes = "Walks straight at its target" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Shade, Name = "Shade", Group = "Shooters", Wave = 3,
                Appears = "The Twin Reapers' approach (Harvest squads)",
                Lead = "A shrouded Phantom: shows a ring where it will land, blinks there and fires. Moves like a Phantom.",
                Attacks = new[] { new BestiaryAttack { Name = "Blink and fire", Pattern = "Same as the Phantom", Damage = "Small shots" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Guard, Name = "Guard", Group = "Summons", Wave = 1,
                Appears = "The Warden's approach (waves 1-4), and his Riot stage when rioters are still loose after 10 s",
                Lead = "Blue riot guards with batons. In the Riot they beat down the rioters left over, then come for the players; on the approach they come straight for you.",
                Attacks = new[] { new BestiaryAttack { Name = "Baton", Pattern = "Touch", Damage = "14 contact (25 a swing to rioters)" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.RiotShield, Name = "Riot Shield", Group = "Melee", Wave = 2,
                Appears = "The Warden's approach (waves 2-4, alone or two in a Cell block squad), and two in each of his searchlight turns",
                Lead = "A guard behind a tall shield that stops shots and swings from the front. Up close its shield flashes, then it lunges and bashes you 160 px back (once every 0.8 s at most). After a bash it holds the shield low for a second. The shield holds 4x its health and cracks as it wears; broken, the guard staggers, then flails and takes 50% more damage. In the Warden's searchlight turn his pair work round to your side away from the light, to bash you into it.",
                Attacks = new[]
                {
                    new BestiaryAttack { Name = "Shield bash", Pattern = "0.4 s flash, then a short lunge", Damage = "10, and knocked back 160 px" },
                    new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "8" },
                },
            },
            new BestiaryEntry
            {
                Type = EnemyType.GasGrenadier, Name = "Gas Grenadier", Group = "Shooters", Wave = 2,
                Appears = "The Warden's approach (waves 2-4)",
                Lead = "Keeps its distance and lobs a tear-gas canister near you every 3.6 s. The cloud (70 px) settles a second later and lasts 4 s: step out of it.",
                Attacks = new[] { new BestiaryAttack { Name = "Tear gas", Pattern = "A canister lobbed near you", Damage = "8 a second in the cloud", Every = 3.6f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.SecurityGuard, Name = "Security Guard", Group = "Shooters", Wave = 2,
                Appears = "The Warden's approach (waves 2-4), alone or in a Patrol",
                Lead = "Holds its distance and strafes, then locks on for 0.25 s while still walking (a red lane on the floor) and fires one tracer round straight at you. It fires seven, one every 1.5 s, then reloads for 2.2 s: rush it then.",
                Attacks = new[] { new BestiaryAttack { Name = "Tracer round", Pattern = "One round aimed straight at you, seven to a magazine", Shot = "Tracer", ShotColor = new Rgba(0.94f, 0.97f, 1f), Speed = "340", Damage = "7", Every = 0.8f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.TaserGuard, Name = "Taser Guard", Group = "Shooters", Wave = 3,
                Appears = "The Warden's approach (waves 3-4)",
                Lead = "Keeps its distance, plants its feet and charges its taser for 0.7 s (a dashed aim line, the prongs crackling), then fires a white Fork Bolt that stuns you for 1 s. It doesn't lead you: strafe across the line, or parry the bolt.",
                Attacks = new[] { new BestiaryAttack { Name = "Fork Bolt", Pattern = "One aimed bolt after a 0.7 s charge", Shot = "Fork Bolt", ShotColor = new Rgba(0.75f, 0.91f, 1f), Speed = "260", Damage = "8, and stuns for 1 s", Every = 3.2f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Worker, Name = "Worker", Group = "Melee", Wave = 6,
                Appears = "The Hive Queen's approach (waves 6-9, in pairs and Swarm squads)",
                Lead = "A small, quick worker bee. Weaves at you, in loose pairs. Moves like a Rioter.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "10" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Soldier, Name = "Soldier", Group = "Melee", Wave = 6,
                Appears = "The Hive Queen's approach (waves 6-9)",
                Lead = "An armoured bee that rushes straight at you with its jaws snapping. Moves like a Guard.",
                Attacks = new[] { new BestiaryAttack { Name = "Mandibles", Pattern = "Touch", Damage = "14" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.CombShield, Name = "Comb Shield", Group = "Melee", Wave = 7,
                Appears = "The Hive Queen's approach (waves 7-9, alone or two in a Honeycomb squad)",
                Lead = "A bee behind a plate of seven wax cells that stops shots and swings from the front. Up close the plate flashes, then it lunges and bashes you 160 px back. The cells break as it wears; broken, the bee staggers. Plays like a Riot Shield.",
                Attacks = new[]
                {
                    new BestiaryAttack { Name = "Shield bash", Pattern = "0.4 s flash, then a short lunge", Damage = "10, and knocked back 160 px" },
                    new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "8" },
                },
            },
            new BestiaryEntry
            {
                Type = EnemyType.HoneyLobber, Name = "Honey Lobber", Group = "Shooters", Wave = 7,
                Appears = "The Hive Queen's approach (waves 7-9)",
                Lead = "Keeps its distance, plants its feet and swells, then lobs a glob of honey at a ring marked near you, every 3.6 s. The puddle (55 px) lasts 4.5 s and slows whoever stands in it: step out of it.",
                Attacks = new[] { new BestiaryAttack { Name = "Honey glob", Pattern = "A glob lobbed at a marked ring", Damage = "none: a slowing puddle", Every = 3.6f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Stinger, Name = "Stinger", Group = "Shooters", Wave = 8,
                Appears = "The Hive Queen's approach (waves 8-9)",
                Lead = "Keeps its distance, plants its feet and charges a venom lance for 0.7 s (a lime aim line), then fires one barbed dart that stuns you for 1 s. It doesn't lead you: strafe across the line, or parry the dart. Plays like a Taser Guard.",
                Attacks = new[] { new BestiaryAttack { Name = "Venom dart", Pattern = "One aimed dart after a 0.7 s charge", Shot = "Venom dart", ShotColor = new Rgba(0.784f, 1f, 0.239f), Speed = "260", Damage = "8, and stuns for 1 s", Every = 3.2f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Needler, Name = "Needler", Group = "Shooters", Wave = 7,
                Appears = "The Hive Queen's approach (waves 7-9), alone or in a Picket",
                Lead = "Hovers and strafes, then locks on for 0.25 s (a red lane) and fires one white needle straight at you. It fires seven, one every 1.5 s, then reloads for 2.2 s: rush it then. Plays like a Security Guard.",
                Attacks = new[] { new BestiaryAttack { Name = "Needle", Pattern = "One needle aimed straight at you, seven to a magazine", Shot = "Needle", ShotColor = new Rgba(0.94f, 0.97f, 1f), Speed = "340", Damage = "7", Every = 0.8f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Bumble, Name = "Bumble", Group = "Melee", Wave = 7,
                Appears = "The Hive Queen's approach (Nursery squads and on its own)",
                Lead = "A huge, furry bumblebee. Slow, tough, and it hits hard. Moves like a Brute.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "20", Notes = "Walks straight at its target" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Hornet, Name = "Hornet", Group = "Melee", Wave = 8,
                Appears = "The Hive Queen's approach (Nursery squads and on its own)",
                Lead = "Circles in close, flashes a red lane, then darts along it. Moves like a Lunger.",
                Attacks = new[] { new BestiaryAttack { Name = "Lunge", Pattern = "Wind-up 0.6 s (shows a lane) then a leap, same as the Lunger", Damage = "26", Notes = "18 contact" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Debris, Name = "Debris", Group = "Melee", Wave = 16,
                Appears = "The Singularity's approach (waves 16-19, in clumps and Infall squads)",
                Lead = "Tumbling rubble that falls at you on an inward spiral: it comes in from the side when far and straight at you within 90 px, so it's hard to line up.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "9" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.VoidAcolyte, Name = "Void Acolyte", Group = "Shooters", Wave = 16,
                Appears = "The Singularity's approach (waves 16-19), alone or leading an Infall squad",
                Lead = "A void cultist holding a tiny black hole. Keeps 280 px away and fires three slow orbs: one straight, and two that leave wide and bend back in to meet it where you stood. Step forward or back, not sideways.",
                Attacks = new[] { new BestiaryAttack { Name = "Bent orbs", Pattern = "Three orbs that meet on you", Shot = "Orb", ShotColor = new Rgba(1f, 0.47f, 0.86f), Speed = "150", Damage = "14 each", Every = 2.4f } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Pulsar, Name = "Pulsar", Group = "Shooters", Wave = 17,
                Appears = "The Singularity's approach (waves 17-19), alone or two in a Binary squad",
                Lead = "A spinning neutron star. Within 300 px it plants, and its two beams (one each way) flicker for 1 s a little behind you, then switch on and sweep a quarter turn through you over 4 s. It rests 2.5 s and goes again. Walk the way the beams turn.",
                Attacks = new[] { new BestiaryAttack { Name = "Beams", Pattern = "Two opposite beams, 1 s flicker, then a 90° sweep over 4 s", Damage = "16 a touch", Every = 7.5f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Graviton, Name = "Graviton", Group = "Shooters", Wave = 17,
                Appears = "The Singularity's approach (waves 17-19), alone or in a Gravity trap squad",
                Lead = "Keeps 320 px away. Every 6 s it plants its feet for 0.6 s, a ring marks a spot near you, and 0.8 s later a Small Well lands there: for 4 s it drags you toward it (80 px/s at its middle, 240 px out) and bends your shots into it. Get clear, or shoot the well (30 health). A new lob takes its old well away.",
                Attacks = new[] { new BestiaryAttack { Name = "Small Well", Pattern = "A well lobbed near you", Damage = "none: a pull, and it swallows shots", Every = 6f } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Orbiter, Name = "Orbiter", Group = "Shooters", Wave = 18,
                Appears = "The Singularity's approach (waves 18-19)",
                Lead = "Circles you 260 px out with three orbs turning round it. The orb facing you flares for 0.55 s, then it slings it straight at you, fast. Sidestep as it flares.",
                Attacks = new[] { new BestiaryAttack { Name = "Slung orb", Pattern = "One fast orb after a 0.55 s flare", Shot = "Orb", ShotColor = new Rgba(0.85f, 0.35f, 1f), Speed = "330", Damage = "17", Every = 1.8f } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Collapsar, Name = "Collapsar", Group = "Melee", Wave = 17,
                Appears = "The Singularity's approach (waves 17-19), alone or in a Gravity trap squad",
                Lead = "A slow, dense star that walks at you and shrinks as it's shot. Killed, it collapses: for 0.8 s it pulls everyone near in (100 px/s at the middle, 260 px out), then bursts into a ring of 12 shots. Kill it at range.",
                Attacks = new[]
                {
                    new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "20" },
                    new BestiaryAttack { Name = "Collapse", Pattern = "When killed: a 0.8 s pull, then a ring of 12", Speed = "150", Damage = "14 each" },
                },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Lens, Name = "Lens", Group = "Melee", Wave = 17,
                Appears = "The Singularity's approach (waves 17-19), alone or two in front of a Lens array squad",
                Lead = "Walks at you holding a gravity lens out front. Your shots that come within 90 px of its face curve round it and away, and any that reach the lens do nothing. It turns slowly (1.3 rad a second): get to its side and shoot it there.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "12" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.WhiteDwarf, Name = "White Dwarf", Group = "Melee", Wave = 18,
                Appears = "The Singularity's approach (waves 18-19)",
                Lead = "A small, hot star that darts in zigzagging. Within 120 px it stops and flashes white for 0.5 s, then pulses: everyone within 170 px is shoved 150 px back, and up to 10 of your shots within 150 px turn round and fly back at you as its own. Then it backs off and comes again. Stop shooting when it flashes.",
                Attacks = new[]
                {
                    new BestiaryAttack { Name = "Pulse", Pattern = "0.5 s flash, then a shove and your shots turned round", Speed = "260", Damage = "14 each turned shot" },
                    new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "10" },
                },
            },
            new BestiaryEntry
            {
                Type = EnemyType.MiniWell, Name = "Small Well", Group = "Summons", Wave = 17,
                Appears = "Lobbed by a Graviton",
                Lead = "A small gravity well that lasts 4 s: it drags you toward it and bends your shots into it. Shoot it (30 health) or get clear.",
                Attacks = new BestiaryAttack[0],
            },
            new BestiaryEntry
            {
                Type = EnemyType.Implosion, Name = "Implosion", Group = "Summons", Wave = 17,
                Appears = "Left by a Collapsar when it dies",
                Lead = "Where a Collapsar fell: it pulls everyone near in for 0.8 s, then bursts into a ring of 12 shots. It can't be hurt; get away from it.",
                Attacks = new[] { new BestiaryAttack { Name = "Burst", Pattern = "A ring of 12 after 0.8 s", Speed = "150", Damage = "14 each" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Rubble, Name = "Rubble", Group = "Melee", Wave = 21,
                Appears = "The Colossus's approach (in pairs, Landslip and Rockfall squads)",
                Lead = "A fist-sized chunk of the Colossus that rolls at you, weaving, in loose pairs. Moves like a Rioter.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "10" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Quarryman, Name = "Quarryman", Group = "Melee", Wave = 21,
                Appears = "The Colossus's approach (leading Landslip squads, and on its own)",
                Lead = "A quarry worker with a hard hat, a headlamp and a pickaxe. Walks straight at you; its lamp shows where it's heading. Moves like a Guard.",
                Attacks = new[] { new BestiaryAttack { Name = "Pickaxe", Pattern = "Touch", Damage = "14" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Stomper, Name = "Stomper", Group = "Melee", Wave = 22,
                Appears = "The Colossus's approach (Rockfall squads and on its own)",
                Lead = "A slab-shouldered stone brute that walks at you like a Thug. Within 260 px it stops and lifts a foot for 0.8 s, showing a green gap beside you, then stomps out a small ring of push blocks (gone at 220 px). Step into the gap; caught, it passes over you once and leaves you behind it.",
                Attacks = new[]
                {
                    new BestiaryAttack { Name = "Stomp ring", Pattern = "0.8 s wind-up, then a ring of push blocks with one gap", Damage = "12", Every = 4f, Fixed = true },
                    new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "20" },
                },
            },
            new BestiaryEntry
            {
                Type = EnemyType.SlabBearer, Name = "Slab Bearer", Group = "Melee", Wave = 22,
                Appears = "The Colossus's approach (alone, or two in a Wall-in squad)",
                Lead = "Carries a quarried slab that stops shots and swings from the front. Up close the slab flashes (0.4 s), then it lunges and bashes you 160 px back. The slab cracks as it wears; broken, it drops in front of the bearer as cover for 3 s that stops shots from both sides, and the bearer staggers and takes 50% more. Plays like a Riot Shield.",
                Attacks = new[]
                {
                    new BestiaryAttack { Name = "Slab bash", Pattern = "0.4 s flash, then a short lunge", Damage = "10, and knocked back 160 px" },
                    new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "8" },
                },
            },
            new BestiaryEntry
            {
                Type = EnemyType.RockHurler, Name = "Rock Hurler", Group = "Shooters", Wave = 22,
                Appears = "The Colossus's approach (alone, or two behind Quarrymen in a Dig crew)",
                Lead = "Keeps its distance and hauls a boulder back while an orange ring fills where you're heading (picked as the wind-up starts), then lobs it every 3.6 s. It lands 1 s later for 18, bursts into six shards and lies as a rubble pile for 2 s that stops shots from both sides. Plays like a Gas Grenadier.",
                Attacks = new[] { new BestiaryAttack { Name = "Boulder", Pattern = "Lobbed at a ring on your lead, then six shards", Damage = "18, then six small shots", Every = 3.6f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Driller, Name = "Driller", Group = "Melee", Wave = 23,
                Appears = "The Colossus's approach (in Quarry squads and on its own)",
                Lead = "Circles in close with a jackhammer, shows the lunge lane, then drills along it. The crack it leaves glows and erupts 0.4 s after it stops: dodge across the line, not along it. Moves like a Shiv.",
                Attacks = new[]
                {
                    new BestiaryAttack { Name = "Drill", Pattern = "Wind-up 0.6 s (shows a lane) then a leap, same as the Shiv", Damage = "26", Notes = "18 contact" },
                    new BestiaryAttack { Name = "Crack", Pattern = "Along the drill's path, 0.4 s after it stops", Damage = "12" },
                },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Geomancer, Name = "Geomancer", Group = "Shooters", Wave = 23,
                Appears = "The Colossus's approach (alone, or three spread wide in a Fault line)",
                Lead = "A Colossus cultist with a crystal staff. Keeps its distance, plants the staff and charges for 0.7 s (the aim line), then sends a crack racing at where you stood. It glows for 0.5 s, then erupts along its length: step off the line.",
                Attacks = new[] { new BestiaryAttack { Name = "Crack", Pattern = "Races 600 px/s toward you, up to 460 px; erupts 0.5 s after it stops", Damage = "12", Every = 3.2f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.CinderPriest, Name = "Cinder Priest", Group = "Shooters", Wave = 23,
                Appears = "The Colossus's approach (waves 23-24)",
                Lead = "Cups a molten core and charges for 0.7 s along a shooting line, then fires one magma bolt straight at you (240 px/s, no lead). Wherever it stops, a hit, cover or the end of its 300 px, it leaves a lava pool for 2.5 s (20 a second). A perfect parry sends it back, and leaves no lava. Plays like a Taser Guard.",
                Attacks = new[] { new BestiaryAttack { Name = "Magma bolt", Pattern = "One aimed bolt after a 0.7 s charge, then a lava pool", Shot = "Magma bolt", ShotColor = new Rgba(1f, 0.48f, 0.23f), Speed = "240", Damage = "9, then lava 20/s", Every = 3.2f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Fencer, Name = "Fencer", Group = "Melee", Wave = 26,
                Appears = "The Duelist's approach (in crowds, Salon and Guard line squads)",
                Lead = "A masked foil-fencer that runs in crowds. Within 160 px it shows a short red line for 0.4 s, then hops about 100 px down it. Moves like a Shiv.",
                Attacks = new[] { new BestiaryAttack { Name = "Hop-lunge", Pattern = "0.4 s line, then a short leap", Damage = "16", Notes = "10 contact" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.BladeDancer, Name = "Blade Dancer", Group = "Melee", Wave = 26,
                Appears = "The Duelist's approach (leading Salon squads, and on its own)",
                Lead = "Within 300 px it shows the Duelist's warning line for 0.6 s, then lunges up to 230 px and leaves three blades hanging on the path. After 0.6 s each fires one shot out of each side. Moves like a Guard.",
                Attacks = new[]
                {
                    new BestiaryAttack { Name = "Lunge blades", Pattern = "0.6 s line, a lunge, then 2 shots from each blade", Damage = "small shots", Every = 3.5f, Fixed = true },
                    new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "14" },
                },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Parrier, Name = "Parrier", Group = "Melee", Wave = 27,
                Appears = "The Duelist's approach (Guard line squads and on its own)",
                Lead = "A Riot Shield with a main-gauche: shots on its front are parried straight back at whoever fired them, and it bashes you away. It turns slowly, so get behind it; break the guard and it falls fast.",
                Attacks = new[] { new BestiaryAttack { Name = "Parry", Pattern = "A blocked shot comes back at you", Damage = "small shot" }, new BestiaryAttack { Name = "Bash", Pattern = "Shield flash, then a short lunge", Damage = "10" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Second, Name = "Second", Group = "Shooters", Wave = 27,
                Appears = "The Duelist's approach (Seconds squads and on its own)",
                Lead = "The duelist's second: keeps its distance and locks on like a Security Guard, glints gold, then throws an aimed fan of five daggers (230 px/s). It never reloads.",
                Attacks = new[] { new BestiaryAttack { Name = "Dagger fan", Pattern = "5 daggers, 0.16 rad apart, aimed", Damage = "10 each", Every = 2.2f } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Marshal, Name = "Marshal", Group = "Shooters", Wave = 27,
                Appears = "The Duelist's approach",
                Lead = "The duel's referee: keeps its distance like a Gas Grenadier and raises its baton, then draws a gold circle 140 px round where you stand. It forms over 0.6 s (step out now), then for 4 s you can't leave it and leaning on the edge hurts.",
                Attacks = new[] { new BestiaryAttack { Name = "Dueling circle", Pattern = "0.6 s to form, holds you 4 s", Damage = "8 on the edge", Every = 7f, Fixed = true } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Gallant, Name = "Gallant", Group = "Shooters", Wave = 28,
                Appears = "The Duelist's approach",
                Lead = "A champion who keeps its distance, then salutes (0.8 s, sword up, a gold glint) and lunges through you. A perfect parry as it arrives knocks it off balance for 2.5 s, taking 50% more damage; dodge it and it rakes the spot with five shots.",
                Attacks = new[] { new BestiaryAttack { Name = "Salute lunge", Pattern = "0.8 s salute, then a lunge up to 400 px", Damage = "20", Every = 4.5f, Fixed = true }, new BestiaryAttack { Name = "Rake", Pattern = "5 shots where it lands, if dodged", Damage = "small shots" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Harlequin, Name = "Harlequin", Group = "Melee", Wave = 27,
                Appears = "The Duelist's approach (Masked ball squads and on its own)",
                Lead = "A heavy two-sworded jester (240 health) that walks at you like a Thug. Within 260 px it shows a line through you for 0.7 s and dashes it, 170 px past you, then vaults to the other arm of the X, shows the crossing line for 0.45 s and dashes that. Both leave blades that fire out sideways: stand in a gap of the X.",
                Attacks = new[] { new BestiaryAttack { Name = "Crossing", Pattern = "Two crossed dashes, blades every 70 px", Damage = "small shots", Every = 6f, Fixed = true }, new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "20" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Mummer, Name = "Mummer", Group = "Melee", Wave = 28,
                Appears = "The Duelist's approach (Masked ball squads and on its own)",
                Lead = "A fast masked player that circles in like a Shiv. Within 260 px it blinks and becomes three, circling you 150 px out for 5 s, each firing every 0.9 s; only the real one's shots are solid, the copies' are hollow rings that pass through you. The copies have 1 health and give nothing; kill the real one and they vanish.",
                Attacks = new[] { new BestiaryAttack { Name = "Masquerade", Pattern = "Three of it round you, one shot each every 0.9 s", Damage = "small shot (the real one's)", Every = 9f, Fixed = true }, new BestiaryAttack { Name = "Lunge", Pattern = "As a Shiv", Damage = "26", Notes = "14 contact" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.MummerCopy, Name = "Mummer copy", Group = "Summons", Wave = 28,
                Appears = "Two at a time, when a Mummer blinks into three",
                Lead = "One of a Mummer's two copies: it looks just like the Mummer and circles you with it, but its shots are hollow rings that pass through you. 1 health, no XP; it vanishes after 5 s or when the real one dies.",
                Attacks = new[] { new BestiaryAttack { Name = "Hollow shot", Pattern = "Aimed, every 0.9 s", Damage = "0 (passes through you)" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.HiveEgg, Name = "Hive Egg", Group = "Summons", Wave = 10,
                Appears = "Laid by the Hive Queen in her Honeycomb stage",
                Lead = "Doesn't move or attack. Break it before it hatches into drones.",
            },
            new BestiaryEntry
            {
                Type = EnemyType.DualReaper, Name = "The Dual Reaper", Group = "Summons", Wave = 15,
                Appears = "The Twin Reapers fused into one, in their second stage",
                Lead = "Holds both twins' health and is armored (a third of the damage). Twin rotating lasers while its 4 corner pillars stand (kill them or it pulls you in and blows), then it chases you swinging both scythes. Lure it into a pillar's hole and break its shield: it's open for 4 s (half again the damage). At 30% it splits again.",
            },
            new BestiaryEntry
            {
                Type = EnemyType.ReaperPillar, Name = "Reaper Pillar", Group = "Summons", Wave = 15,
                Appears = "The Dual Reaper's corner pillars",
                Lead = "Each one you kill leaves a hole in the floor that can trap the Dual Reaper.",
            },
            new BestiaryEntry
            {
                Type = EnemyType.SingShard, Name = "Singularity Shard", Group = "Summons", Wave = 20,
                Appears = "The Singularity's Split",
                Lead = "Shards that orbit and fire. Damage you deal them goes to the core.",
            },
            new BestiaryEntry
            {
                Type = EnemyType.BallLightning, Name = "Ball Lightning", Group = "Summons", Wave = 40,
                Appears = "The Storm Herald's Gale",
                Lead = "Slow orbs (3 at a time) that drift at you at 80 px/s plus the wind. Two within 200 px arc lightning to each other, and each pops into 8 shots after 6 s.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "20" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Morsel, Name = "Morsel", Group = "Summons", Wave = 45,
                Appears = "Spat out by the Devourer when it turns Hungry",
                Lead = "Spat 220–440 px away, 4 at a time; the Devourer crawls to the nearest at 90 px/s, and each one it eats heals it 1% of its health. Shoot them or step on them first.",
            },
        };

        static BestiaryEntry[] all;

        /// Every entry, by group (Groups' order), then by first wave.
        public static BestiaryEntry[] All
        {
            get
            {
                if (all != null)
                    return all;
                var list = new List<BestiaryEntry>(FromPage);
                list.AddRange(Extra);
                var sorted = new List<BestiaryEntry>();
                foreach (var g in Groups)
                {
                    var inGroup = list.FindAll(e => e.Group == g);
                    // Stable by first wave, keeping the page's order for ties.
                    for (int i = 1; i < inGroup.Count; i++)
                        for (int j = i; j > 0 && inGroup[j - 1].Wave > inGroup[j].Wave; j--)
                        {
                            var t = inGroup[j];
                            inGroup[j] = inGroup[j - 1];
                            inGroup[j - 1] = t;
                        }
                    sorted.AddRange(inGroup);
                }
                foreach (var e in list)
                    if (!sorted.Contains(e))
                        sorted.Add(e);  // a group the list above doesn't name: still shown, last
                return all = sorted.ToArray();
            }
        }

        /// The entry for an enemy type (the second Twin Reaper shares the first's), or null.
        public static BestiaryEntry For(EnemyType t)
        {
            if (t == EnemyType.TwinB)
                t = EnemyType.TwinA;
            foreach (var e in All)
                if (e.Type == t)
                    return e;
            return null;
        }
    }
}
