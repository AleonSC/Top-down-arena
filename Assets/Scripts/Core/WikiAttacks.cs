namespace Arena
{
    /// The wiki's boss attacks (WikiKind.Attack), in the README's words, each under its boss and phase (a phase name from
    /// WikiPhases.cs, or null for one used all fight). The Lich's come from its turns (Lich.cs LichTurn), as the README only names them. An attack that is also an enemy (Ball Lightning, Hive Egg) stays an enemy.
    public static class WikiAttacks
    {
        public struct Attack
        {
            public EnemyType Boss;
            public string Phase, Name, Text;
            public Attack(EnemyType boss, string phase, string name, string text) { Boss = boss; Phase = phase; Name = name; Text = text; }
        }

        public static readonly Attack[] All =
        {
            // The Warden
            new Attack(EnemyType.Warden, "Count", "Searchlight", "18 s: a light from the corner tower follows you at an outrunnable pace while Taser Guards do the stunning. Lead it across the Warden to blind it for 3 s (exposed: no firing, +50% damage)."),
            new Attack(EnemyType.Warden, "Count", "Shackle ring", "A ring of orbs that closes in on you when you're stunned or linger in the searchlight; its gap faces away from the Warden."),
            new Attack(EnemyType.Warden, "Lockdown", "Static cross", "12 s: it dashes, plants and fires a static \"+\", four times."),
            new Attack(EnemyType.Warden, "Lockdown", "Gates", "Walls of push bullets two rows deep with two doors near the side walls: each slides in and stops a moment so you can line up a door, then sweeps across, shoving you back. Six hits break its gold lock, opening a third door."),
            new Attack(EnemyType.Warden, "Lockdown", "Tear gas", "Lobbed canisters that never land across a gate's doors; then a barrage of two volleys with clear lanes between, while it fires slow aimed bursts."),
            new Attack(EnemyType.Warden, "Riot", "Riot shield", "Soaks 7% of its max health from the front, cracking as it wears; break it and it's exposed for the rest of that riot turn, taking 50% more."),
            new Attack(EnemyType.Warden, "Riot", "Baton charge", "A telegraphed charge at you."),
            // The Hive Queen
            new Attack(EnemyType.HiveQueen, "Swarm", "Drones", "Tough shards: she's shielded and waits while they live, and any left after 10 s fly back into her and heal her 2% each."),
            new Attack(EnemyType.HiveQueen, "Swarm", "Bombers", "They swoop out like wasps to a point beside you, then lock onto your spot and dive in from the side (not along the line between you and her)."),
            new Attack(EnemyType.HiveQueen, "Honeycomb", "Hex cells", "The floor's hex cells glow and seal (18 damage) in waves, always leaving a safe cell next to you."),
            new Attack(EnemyType.HiveQueen, "Honeycomb", "Nectar spray", "A sweeping spray that leaves honey puddles that slow you."),
            new Attack(EnemyType.HiveQueen, "Frenzy", "Swarm cloud", "A cloud of bees that hunts you at an outrunnable 170 px/s."),
            new Attack(EnemyType.HiveQueen, "Frenzy", "Royal dive", "Three dives that land on your shadow with a ring of orbs; she can't be hurt in the air."),
            new Attack(EnemyType.HiveQueen, "Frenzy", "Bomber barrage", "Bombers aimed at where you are and where you're heading."),
            // The Twin Reapers
            new Attack(EnemyType.ReaperOfDusk, "Twilight", "Tether sweep", "A sweep that spins round a point following you, then lines and a cross fired holding still."),
            new Attack(EnemyType.ReaperOfDusk, "Twilight", "Soul Bond", "The more-hurt Reaper can't be hurt until you even them out."),
            new Attack(EnemyType.ReaperOfDusk, "The Dual Reaper", "Twin rotating lasers", "Fired while 4 pillars stand in the corners: kill them, or it pulls you in and blows up. Each pillar leaves a hole that can trap it."),
            new Attack(EnemyType.ReaperOfDusk, "Sundered", "Twin revive", "Kill one Reaper and the other enrages; the fallen one revives at 60% after 6 s unless you finish the pair."),
            // The Singularity
            new Attack(EnemyType.Singularity, "Event Horizon", "Pull", "4 s: drags everyone in under a three-armed spiral."),
            new Attack(EnemyType.Singularity, "Event Horizon", "Collapse", "3 s of bullet rings."),
            new Attack(EnemyType.Singularity, "Event Horizon", "Lasers", "8 s: a purple spiral, then three red lasers that flicker as a warning and sweep slowly round."),
            new Attack(EnemyType.Singularity, "Accretion", "Cage", "9 s: three small wells orbit every player in a shrinking ring; touching it hurts, and shooting the wells breaks it."),
            new Attack(EnemyType.Singularity, "Accretion", "Slingshot", "6 s: every 0.8 s three orbs leave the outer ring at you."),
            new Attack(EnemyType.Singularity, "Spaghettify", "Hawking", "Bursts of 18 shots off the event horizon."),
            new Attack(EnemyType.Singularity, "Spaghettify", "White hole", "A 1 s white flash, then for 1.5 s it pushes and every enemy bullet on screen turns round."),
            new Attack(EnemyType.Singularity, "Spaghettify", "Split", "Three shards orbit and fire; the core can't be hurt, and damage to the shards goes to it."),
            // The Colossus
            new Attack(EnemyType.Colossus, null, "Stomp", "Rings of indestructible stone orbs with a few small openings, one a short step to the side of each player: slip through an opening. Touch an orb and it hurts (once a ring)."),
            new Attack(EnemyType.Colossus, "Awakening", "Boulders", "It lifts a huge boulder over its head (a red ring follows where it'll land), then lobs it high where you're heading; it lands with a burst."),
            new Attack(EnemyType.Colossus, "Awakening", "Fissures", "A crack races toward you and erupts along its length, then stays as a scar until the phase ends. With five up, they all wake and erupt again in turn."),
            new Attack(EnemyType.Colossus, "Crumbling", "Colossus charge", "A red lane, then it runs; a plate of cover in the way breaks and dazes it (+50% damage)."),
            new Attack(EnemyType.Colossus, "Crumbling", "Rockslide", "Rows of rocks from an edge with a moving gap."),
            new Attack(EnemyType.Colossus, "Crumbling", "Aftershocks", "Rings of shots whose gaps spiral."),
            new Attack(EnemyType.Colossus, "Molten Core", "Meteors", "They land in orb rings and pools of lava."),
            new Attack(EnemyType.Colossus, "Molten Core", "Cave-in", "Pillars fall, stand as cover for 3 s, then shatter."),
            // The Duelist
            new Attack(EnemyType.Duelist, "En Garde", "Lunge", "A warning line, then a fast dash leaving blades that fire sideways."),
            new Attack(EnemyType.Duelist, "En Garde", "Guard", "Your shots on its front bounce back at you; it turns slowly, so get behind it."),
            new Attack(EnemyType.Duelist, "En Garde", "Blink", "It teleports, leaving a decoy."),
            new Attack(EnemyType.Duelist, "Masquerade", "Crossing", "An X of dashes leaving blades."),
            new Attack(EnemyType.Duelist, "To the Death", "Dueling circle", "A circle 360 px round the middle you can't leave; leaning on it hurts 15."),
            new Attack(EnemyType.Duelist, "To the Death", "Salute", "It salutes before it lunges: a perfect parry as it arrives knocks it off balance for 2.5 s at +50% damage; a dodge gets you raked."),
            // The Lich
            new Attack(EnemyType.Lich, "The Crypt", "Soul fans", "A fan of 5 aimed piercing shots."),
            new Attack(EnemyType.Lich, "The Crypt", "Grave rings", "A slow ring of 16 big orbs, each turned 12°."),
            new Attack(EnemyType.Lich, "The Crypt", "Raise the dead", "The dead climb out of its graves."),
            new Attack(EnemyType.Lich, "Soul Harvest", "Soul beams", "Beams joining the three phylacteries; they hurt to cross."),
            new Attack(EnemyType.Lich, "Soul Harvest", "Soul sweep", "Twice: a warning line, then a draining beam 900 px long swung 120° across toward the nearest player."),
            new Attack(EnemyType.Lich, "Soul Harvest", "Bone prison", "Twice: a ring of 14 spikes rises 110 px round each player, the gap facing away from the Lich; when up, they fire inward."),
            new Attack(EnemyType.Lich, "Soul Harvest", "Wailing spirits", "Spirits circle the Lich, then dive at where a player was."),
            new Attack(EnemyType.Lich, "Undeath", "Grave hop", "A spectre that fades out and blinks to another of the six graves every 2 s."),
            new Attack(EnemyType.Lich, "Undeath", "Dead hands", "Every 0.9 s hands stir under each player: get off the 50 px spot before they strike 0.8 s later."),
            new Attack(EnemyType.Lich, "Undeath", "Soul storm", "A two-armed spiral that reverses halfway."),
            // The Storm Herald
            new Attack(EnemyType.StormHerald, "Gathering Storm", "Strikes", "6 s: a 70 px strike on each player and two random spots every 1.8 s, after a 1.1 s warning."),
            new Attack(EnemyType.StormHerald, "Gathering Storm", "Lightning rods", "7 s: four rods fall around you; every 1.4 s an arc flickers between a pair, then fires along the line."),
            new Attack(EnemyType.StormHerald, "Gathering Storm", "Thunderclap", "5 s: rings of 36 shots with two gaps, each turned 25°."),
            new Attack(EnemyType.StormHerald, "Gale", "Crosswind", "6 s: 5-shot piercing volleys aimed upwind, so the wind carries them on."),
            new Attack(EnemyType.StormHerald, "Gale", "Downburst", "6 s: rows of six strikes at your height, landing one after another in the wind's direction."),
            new Attack(EnemyType.StormHerald, "Supercell", "Conductor", "Marks one player at a time (every 8 s, for 3 s, a 180 px ring): when it runs out the charge jumps to every ally in the ring, hurting both ends, and 10 shots burst out."),
            new Attack(EnemyType.StormHerald, "Supercell", "Storm front", "7 s: a wall of strikes sweeps across with a two-strike gap that drifts."),
            new Attack(EnemyType.StormHerald, "Supercell", "Strike rain", "6 s: smaller, quicker strikes."),
            new Attack(EnemyType.StormHerald, "Supercell", "Rod cage", "7 s: twice, six rods ring you 240 px out and arcs join them, all but one open side."),
            // The Devourer
            new Attack(EnemyType.Devourer, "Appetite", "Snap", "Three lunges at a player: a red line for 0.7 s, then a fast dash and a bite in a 110 px half-circle."),
            new Attack(EnemyType.Devourer, "Appetite", "Hungry", "Its mouth opens and your shots heal it for half; rings of big shots, and it spits morsels and crawls over to eat them."),
            new Attack(EnemyType.Devourer, "Gorging", "Inhale", "Twice, a 70° cone 600 px long pulls you toward its mouth; whoever reaches it is swallowed."),
            new Attack(EnemyType.Devourer, "Gorging", "Bile", "Every 1.5 s a glob at each player and one at random: a 90 px pool of acid for 6 s, 12 a second."),
            new Attack(EnemyType.Devourer, "Gorging", "Burrow", "Twice, it dives (can't be hurt), a ripple chases a player, stops and marks 100 px, then erupts with a bite and a ring of 16."),
            new Attack(EnemyType.Devourer, "Ravenous", "The Maw", "A 90° cone in front of it: shots into it heal it for half, shots on its back do ×1.25; it turns to the nearest player."),
            new Attack(EnemyType.Devourer, "Ravenous", "Devourer frenzy", "Four lunges with a 0.55 s warning, each ending in a ring of 12."),
            new Attack(EnemyType.Devourer, "Ravenous", "Jaws", "Twice, two rows of 7 teeth either side of a player show for 0.6 s, then close."),
            new Attack(EnemyType.Devourer, "Ravenous", "Vomit", "A spray of bile sweeping 70° either side of you and back."),
            // The Eclipse
            new Attack(EnemyType.Eclipse, "Corona", "Solar flares", "A line at each player and one more, a 1 s warning, then a 40 px lane of fire."),
            new Attack(EnemyType.Eclipse, "Corona", "Corona rings", "Rings of 24 shots."),
            new Attack(EnemyType.Eclipse, "Penumbra", "Lunar wake", "The moon speeds up, trailing slow shots."),
            new Attack(EnemyType.Eclipse, "Penumbra", "Eclipse flash", "The moon stops and the sun swells for 3 s, then everyone outside the moon's shadow takes 40."),
            new Attack(EnemyType.Eclipse, "Umbra", "Sunspots", "In the dark you see 140 px round you and in three drifting sunspots; outside them the dark drains 6 a second."),
            new Attack(EnemyType.Eclipse, "Umbra", "Crescents", "Blades that loop out through you and come back."),
            new Attack(EnemyType.Eclipse, "Umbra", "Closing umbra", "Rings of shadow close in with two gaps."),
            new Attack(EnemyType.Eclipse, "Last Light", "Shadow nova", "Two flashes, the moon closer and jumping round between them."),
            new Attack(EnemyType.Eclipse, "Last Light", "Solar storm", "Flares and crescents together."),
            new Attack(EnemyType.Eclipse, "Totality", "Nova timer", "A 12 s timer: the nova hits everyone for 70% of their health and heals the Eclipse a little, then starts again."),
        };
    }
}
