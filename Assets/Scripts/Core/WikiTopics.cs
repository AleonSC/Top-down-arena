namespace Arena
{
    /// The wiki's mechanics (WikiKind.Mechanic): the things players meet that have no table of their own, in Docs/Glossary.md's words.
    /// Add a line here when a new mechanic gets a name players see; the wiki suite checks each has text.
    public static class WikiTopics
    {
        public struct Topic
        {
            public string Name, Text;
            public string[] From;  // entries it comes from, linked whatever the text says (a player status's causes)
            public Topic(string name, string text, params string[] from) { Name = name; Text = text; From = from; }
        }

        public static readonly Topic[] All =
        {
            new Topic("Alternative fire", "A weapon's secondary attack (\"Alt\" for short), on its own key: right after Fire on the Keybinds page."),
            new Topic("1st ability", "Picked in the lobby: 3 choices per role and class, sometimes a 4th choice."),
            new Topic("2nd, 3rd and 4th abilities", "Filled by very rare upgrade cards during a run."),
            new Topic("Dodge", "A tiny dash with a moment of invulnerability; double tap a movement key does it too."),
            new Topic("Parry", "Hold for the shield toward your cursor (right click by default)."),
            new Topic("Interact", "The one \"use what you stand by\" key (F by default): take a chest item, open a mystery door, shut a gate, open a room shop again."),
            new Topic("Level", "Your XP level in a run: each level-up brings upgrade cards, and every 10 levels a milestone card."),
            new Topic("Rank", "Your profile's progress across runs; rank rewards unlock cosmetics (frames, trails, backgrounds, cursors...)."),
            new Topic("Mastery", "An ability's 1-5 rank from using it; mastery 5 unlocks its capstone."),
            new Topic("Expertise", "A class's 0-10 level from the XP of runs played as it (account-wide, cosmetic); it unlocks the class's weapon skins."),
            new Topic("Companion", "The minion your weapon keeps (Golem, wolves, the Nimbus...)."),
            new Topic("Thrall", "An enemy chained to you by Force Pact: it fights near you, and comes back after it falls."),
            new Topic("Wound", "20% of every hit you take is a wound: it can't be healed back. Wounds carry across waves but never take more than 30% of your max health; they clear when a boss falls, at a health shrine, with a Medkit gadget, the Medic's heal or at a Boss Hunt Rest."),
            new Topic("Elite", "From wave 12 any enemy can spawn with a gold ring: 2.5x health, bigger, worth 3x XP, always drops an orb, and a trait shown over it (two traits from wave 35)."),
            new Topic("Health shrine", "A map node a room claims; one use per player: full health plus health past max."),
            new Topic("Vectors", "Every player's building budget in the Expanding arena: pieces are free; vectors pay for gates, mystery doors and the Builder Shop's cards."),
            new Topic("Pit", "A void in a cell's floor: enemies shoved in die, players lose 15% health and climb out."),
            new Topic("Boss phase", "One part of a boss fight; a phase change moves to the next."),
            new Topic("Steam", "Fire + water: an enemy that is wet and burning at once turns both to steam: a cloud for 3 s that scalds enemies in it and jams their weapons (bosses only take the scald). A freeze melts into 6 s of wet when it ends, so a frozen, burning enemy steams as it thaws."),
            new Topic("Wind Chill", "Wind on a wet enemy chills it (slowed; three chills freeze it). After a wind freeze, wind can't chill it again for 6 s."),
            new Topic("Fan the Flames", "Wind on a burning enemy spreads its fire, at 60%, to up to three enemies close by downwind."),
            new Topic("Downed", "At 0 health you go down; a teammate can revive you."),
        };

        /// Status effects on enemies (Enemy's timers in Entities.cs; the replicated flags are what the eyedropper's tag lists).
        public static readonly Topic[] Statuses =
        {
            new Topic("Slowed", "Moves slower for a while, from anything that slows (wind, honey, gas, ink, ice...). Only the strongest slow counts; the next-strongest takes over when it ends. It never builds to a freeze. Heavy enemies and bosses are too heavy to slow down. On you: honey puddles and some attacks slow you too."),
            new Topic("Burning", "On fire (the Flamethrower, the Fireball, Flash Fire, Flame Wall, Incendiary Rounds, burning ground): takes damage every second until it burns out."),
            new Topic("Bleeding", "The shared bleed (claws, Sanguine Orb, the Needle Swarm, Vein Rush, Arterial Spray): damage over time. Bleed stacks go up to five from any source; a Needle Swarm cut that fills them bursts it. On you: a Rioter's bottle cut bleeds you for a while."),
            new Topic("Chilled", "Ice only. Every ice attack (Frost Lance, Frost Nova, Blizzard, Cryo Rounds, Ice Shards, Wind Chill) slows it and adds a chill; steady ice adds one a second at most. The third chill freezes it, and the freeze lifts the slow. Bosses are only slowed."),
            new Topic("Frozen", "Encased in ice (Glacial Tomb, Blood Freeze, a third chill): can't move or shoot. It melts into 6 s of wet."),
            new Topic("Wet", "Soaked by water (Tsunami, the Water Gun, Waterball, Flood, Water Ward, the Nimbus, Healing Rain, Bullet Rain): slowed, and lightning arcs to it."),
            new Topic("Charged", "Hit by lightning (Arc Lightning, Storm Hammer, Static Shock, the Nimbus, Arc Knuckles, EMP, Shock Grenades...): charged for 4 s. Alone it does nothing; wet and charged, it shorts out: constant damage, and every 2.5 s an arc to the three enemies nearest it."),
            new Topic("Brittle", "Cracked by earth (Stonespear, the Earthball and its rubble, Meteor, Stone Colossus): for 5 s it takes 20% more damage (bosses 10%). Frozen while brittle, it shatters: the ice and the cracks break for a burst of damage, and half of it hits the enemies around it."),
            new Topic("Dazzled", "Dazzled by light (the Radiant Orb, Chain Light, standing in a Lumen Ward): for 3 s its shots stray up to 25° either way (bosses 12°)."),
            new Topic("Stunned", "Held in place, with stars over its head: can't move or shoot until it wears off. On you: a Taser Guard's Fork Bolt stops you moving for a moment."),
            new Topic("Dazed", "Dazed by a close-in hit: can't attack for a moment, then can't be dazed again for a while."),
            new Topic("Caged", "Caged by a third Bone Guard dart: can't move or shoot."),
            new Topic("Marked", "Mark for Death: takes extra damage while the mark lasts."),
            new Topic("Poisoned", "Senbon Seekers' poison: damage over time that stacks."),
            new Topic("Venom", "Poison Dart's venom: damage over time that spreads to enemies nearby when it dies."),
            new Topic("Suppressed", "The Riot Repeater's suppression: it shoots slower while it lasts."),
        };

        /// Status effects only players get (shown on a player under the eyedropper; Player's timers in Entities.cs).
        public static readonly Topic[] PlayerStatuses =
        {
            new Topic("Swallowed", "Inside the Devourer (its Inhale): hidden for 3 s, taking 10 damage a second; allies' hits on it do double, and 300 of them spit you out early.", "The Devourer"),
            new Topic("Grabbed", "A Crawler holds you: 25 damage to it frees you.", "Crawler"),
            new Topic("Taunting", "Roar: enemies target you while it lasts.", "Roar"),
        };

        /// Things standing in the arena (Props.cs): props by PropKind, chests by ChestKind (Kind is the PropKind or, for a chest, the
        /// ChestKind; Chest says which).
        public struct Thing
        {
            public string Name, Text;
            public bool Chest;
            public int Kind;
            public Thing(string name, bool chest, int kind, string text) { Name = name; Chest = chest; Kind = kind; Text = text; }
        }

        public static readonly Thing[] Things =
        {
            new Thing("Crate", false, (int)PropKind.Crate, "Cover: enemy shots stop at it and wear it down; your shots fly over it. Solid to walk into."),
            new Thing("Heavy crate", false, (int)PropKind.HeavyCrate, "A crate that takes twice the hits: cover from enemy shots, solid to walk into."),
            new Thing("Barrel", false, (int)PropKind.Barrel, "Rare. Any shot sets it fizzing; 0.4 s later it blows up, hurting enemies (a lot) and players (a little), smashing crates and setting off other barrels."),
            new Thing("Health barrel", false, (int)PropKind.HealthBarrel, "Boss fights only: a green barrel with a white cross drops in now and then; shoot it or walk into it and it breaks into health orbs."),
            new Thing("Upgrade chest", true, (int)ChestKind.Upgrade, "A wave's rare treasure: touch it to open it, and every player gets their own item (an upgrade card or a legendary) that only they can see. Walk onto it, then take it (Interact) or discard it. The wave waits for both."),
            new Thing("Gold chest", true, (int)ChestKind.Gold, "An Expanding arena chest: a burst of coins."),
            new Thing("Piece chest", true, (int)ChestKind.Piece, "The orange chest that gives every player a piece card."),
            new Thing("Buildables chest", true, (int)ChestKind.Buildable, "An Expanding arena chest: one Black Market buildable each."),
            new Thing("Gadget chest", true, (int)ChestKind.Gadget, "The red chest that gives every player a gadget."),
        };
    }
}
