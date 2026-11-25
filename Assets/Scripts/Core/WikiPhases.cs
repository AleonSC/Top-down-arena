namespace Arena
{
    /// The wiki's boss phases (WikiKind.Phase), in the README's words: their names are the ones under the boss bars (HudBossBars.cs),
    /// so the eyedropper on a phase name opens its page. Every boss's last stand shares one entry (LastStand).
    public static class WikiPhases
    {
        public struct Phase
        {
            public EnemyType Boss;
            public string Name, When, Text;
            public Phase(EnemyType boss, string name, string when, string text) { Boss = boss; Name = name; When = when; Text = text; }
        }

        public static readonly Phase[] All =
        {
            new Phase(EnemyType.Warden, "Count", "100–65%", "It sways through the middle firing orb rings, a spiral and aimed fans, then an 18 s searchlight from the corner tower follows you while Taser Guards do the stunning; get stunned or linger in the light and a shackle ring closes in. Lead the light across the Warden to blind it for 3 s (exposed: no firing, +50% damage)."),
            new Phase(EnemyType.Warden, "Lockdown", "65–30%", "A cross of static shots (dash, plant, fire, four times) alongside three gates: walls of push bullets with two doors that sweep across. Six hits break a gate's gold lock and open a third door. Then a tear gas barrage with clear lanes between."),
            new Phase(EnemyType.Warden, "Riot", "30–0%", "Cell doors burst open and rioters pour out, attacking whoever is nearest, the Warden too; guards with batons follow. Its riot shield soaks hits from the front: break it and it takes 50% more for the rest of that turn; then a telegraphed baton charge."),
            new Phase(EnemyType.HiveQueen, "Swarm", "100–65%", "She sways along the top: drones (she's shielded while they live, and they heal her if they fly back), bombers that swoop out and dive in from the side, aimed fans and orb rings."),
            new Phase(EnemyType.HiveQueen, "Honeycomb", "65–30%", "She lays 4 eggs that each hatch 2 drones unless you break them in 6 s; the floor's hex cells glow and seal in waves, always leaving a safe cell next to you; a sweeping nectar spray leaves honey puddles that slow you."),
            new Phase(EnemyType.HiveQueen, "Frenzy", "30–0%", "She drops into the arena: a swarm cloud of bees hunts you at an outrunnable pace, three royal dives land on your shadow (she can't be hurt in the air), and a bomber barrage aims at where you are and where you're heading."),
            new Phase(EnemyType.ReaperOfDusk, "Twilight", "100–65%", "A tether sweep spins round a point following you, then lines and a cross fired holding still. Soul Bond: the more-hurt Reaper is immune until you even them out."),
            new Phase(EnemyType.ReaperOfDusk, "The Dual Reaper", "65–30%", "They fuse, armored to a third of the damage: twin rotating lasers while 4 corner pillars stand (kill them, each leaves a hole), then it chases you; lure it over a hole and it falls in, open for 4 s at half again the damage."),
            new Phase(EnemyType.ReaperOfDusk, "Sundered", "30–0%", "It splits back into Dusk and Dawn: marked reaps, a twin spiral, crossfire. Kill one and the other enrages, and the fallen one revives at 60% after 6 s unless you finish the pair."),
            new Phase(EnemyType.Singularity, "Event Horizon", "100–65%", "In the middle: Pull (drags everyone in under a spiral), Collapse (bullet rings) and Lasers that sweep slowly round. Four Gravity Wells in the corners bend your shots into themselves."),
            new Phase(EnemyType.Singularity, "Accretion", "65–30%", "It orbits the middle inside a disk of two rings of big orbs: Cage (small wells ring each player and shrink; shoot them to break it), Slingshot (orbs leave the ring at you) and faster twin lasers."),
            new Phase(EnemyType.Singularity, "Spaghettify", "30–10%", "Back in the middle behind an event horizon that grows and pulls all the time: Hawking bursts, a White hole that turns every enemy bullet round, and Split: three shards orbit and fire, and damage to them goes to the core."),
            new Phase(EnemyType.Colossus, "Awakening", "100–65%", "Three times its size, it walks at you with rock volleys between turns: Stomps (rings of stone orbs with a few small openings), Boulders held over its head then lobbed where you're heading, and Fissures that erupt and stay as scars. At 65% it kneels, staggered: shoot it to break its shell off."),
            new Phase(EnemyType.Colossus, "Crumbling", "65–30%", "Twice its size, shedding chips as it goes, its armour falls as four plates of cover: a Charge (a plate in the way breaks and dazes it for +50% damage), Rockslide rows with a moving gap, and Aftershocks whose gaps spiral."),
            new Phase(EnemyType.Colossus, "Molten Core", "30–10%", "Its own size now, everything leaves lava: Lava stomps (orb rings with three openings), Meteors that land in orb rings and pools, and a Cave-in of pillars that stand as cover for 3 s, then shatter."),
            new Phase(EnemyType.Duelist, "En Garde", "100–65%", "It strafes 280 px out: Volleys, a Lunge that leaves blades firing sideways, a Guard (shots on its front bounce back; get behind it) and a Blink that leaves a decoy."),
            new Phase(EnemyType.Duelist, "Masquerade", "65–30%", "Three identical decoys circle you with it, and only its shots are solid (theirs are hollow rings); a Crossing of dashes and a lunge chain."),
            new Phase(EnemyType.Duelist, "To the Death", "30–10%", "A dueling circle you can't leave. It salutes before it lunges: a perfect parry as it arrives knocks it off balance for 2.5 s at +50% damage; then a Flurry of slashes."),
            new Phase(EnemyType.Lich, "The Crypt", "100–65%", "One phylactery guards it (break it for a 12 s window); soul fans, grave rings, the dead rising."),
            new Phase(EnemyType.Lich, "Soul Harvest", "65–30%", "Three phylacteries joined by soul beams that hurt to cross; a soul sweep, bone prisons, wailing spirits."),
            new Phase(EnemyType.Lich, "Undeath", "30–10%", "A spectre blinking from grave to grave; dead hands and a soul storm."),
            new Phase(EnemyType.Lich, "The Final Rite", "below 10%", "The Lich's last stand: soul fans every second, grave rings every two, and the dead rise from all six graves every 6 s."),
            new Phase(EnemyType.StormHerald, "Gathering Storm", "100–65%", "It drifts along the top: Strikes on each player, Lightning rods that arc between pairs, and a Thunderclap of rings with two gaps."),
            new Phase(EnemyType.StormHerald, "Gale", "65–30%", "A wind blows across the arena all phase, pushing players and drifting every bullet, yours too: Crosswind volleys, Ball lightning that drifts at you and pops, and a Downburst of strikes in the wind's direction."),
            new Phase(EnemyType.StormHerald, "Supercell", "30–10%", "The sky goes dark. The Conductor marks one player at a time: when it runs out the charge jumps to every ally in the ring. A Storm front of strikes with a drifting gap, Strike rain and a Rod cage."),
            new Phase(EnemyType.Devourer, "Appetite", "100–65%", "It keeps its distance: a double Spiral, a Snap of three lunges, and Hungry: your shots heal it while its mouth is open, and it spits morsels to eat (shoot them or walk over them first)."),
            new Phase(EnemyType.Devourer, "Gorging", "65–30%", "Inhale pulls you toward its mouth: whoever reaches it is swallowed for 3 s while allies' hits do double. Bile pools of acid, and Burrow: a ripple chases a player, then erupts with a bite."),
            new Phase(EnemyType.Devourer, "Ravenous", "30–10%", "The Maw: shots into the cone in front of it heal it, shots on its back do ×1.25. Frenzy lunges, Jaws (rows of teeth close on a player) and a sweeping Vomit of bile."),
            new Phase(EnemyType.Devourer, "Last Meal", "below 10%", "In the middle it pulls everyone in while ten teeth turn round it; its spiral comes back, and being swallowed here does 20 a second."),
            new Phase(EnemyType.Eclipse, "Corona", "100–80%", "It orbits the middle: a 4-arm Spiral, Corona rings, and Solar flares (a warning line at each player, then a lane of fire)."),
            new Phase(EnemyType.Eclipse, "Penumbra", "80–60%", "A moon orbits it, can't be hurt and soaks your shots: Lunar wake, an Eclipse flash (everyone outside the moon's shadow takes 40) and Gravity."),
            new Phase(EnemyType.Eclipse, "Umbra", "60–40%", "The arena goes dark: you see a little round you and in three drifting sunspots, and outside them the dark drains you. Crescents, a Closing umbra and a Dark spiral."),
            new Phase(EnemyType.Eclipse, "Echoes", "40–20%", "Turns borrowed from earlier bosses: the Colossus's stomp rings, the Lich's soul beams, the Storm Herald's strike rain and the Singularity's pull and disk; the moon shoots back."),
            new Phase(EnemyType.Eclipse, "Last Light", "20–5%", "The arena's edges burn and close in: a frantic 5-arm spiral, a Shadow nova and a Solar storm of flares and crescents."),
            new Phase(EnemyType.Eclipse, "Totality", "below 5%", "The moon is gone: it takes +50% damage while a 12 s nova timer runs; the nova hits everyone for 70% of their health and heals it a little, so every cycle leaves it lower."),
        };

        /// The shared last stand (below 10%), and the bosses that make one.
        public static readonly EnemyType[] LastStandBosses =
            { EnemyType.Warden, EnemyType.HiveQueen, EnemyType.Singularity, EnemyType.Colossus, EnemyType.Duelist, EnemyType.StormHerald };
        public const string LastStandName = "Last stand";
        public const string LastStandText = "A boss's final phase below 10% of its health, in the middle of the arena: its fiercest attacks at once. The Warden puts on armor first and the lights go out; the Duelist's circle closes; the Storm Herald turns four lightning spokes.";
    }
}
