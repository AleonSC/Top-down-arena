namespace Arena
{
    /// The wiki's mechanics (WikiKind.Mechanic): the things players meet that have no table of their own, in Docs/Glossary.md's words.
    /// Add a line here when a new mechanic gets a name players see; the wiki suite checks each has text.
    public static class WikiTopics
    {
        public struct Topic
        {
            public string Name, Text;
            public Topic(string name, string text) { Name = name; Text = text; }
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
            new Topic("Wound", "20% of every hit you take is a wound: it can't be healed back. Wounds carry across waves but never take more than 30% of your max health; they clear when a boss falls, at a health shrine, with the Medic's heal or at a Boss Hunt Rest."),
            new Topic("Elite", "From wave 12 any enemy can spawn with a gold ring: 2.5x health, bigger, worth 3x XP, always drops an orb, and a trait shown over it (two traits from wave 35)."),
            new Topic("Health shrine", "A map node a room claims; one use per player: full health plus health past max."),
            new Topic("Vectors", "Every player's building budget in the Expanding arena: pieces are free; vectors pay for gates, mystery doors and the Builder Shop's cards."),
            new Topic("Gadget chest", "The red chest that gives every player a gadget."),
            new Topic("Piece chest", "The orange chest that gives every player a piece card."),
            new Topic("Pit", "A void in a cell's floor: enemies shoved in die, players lose 15% health and climb out."),
            new Topic("Boss phase", "One part of a boss fight; a phase change moves to the next."),
            new Topic("Downed", "At 0 health you go down; a teammate can revive you, and a Defib Kit stands you up."),
        };
    }
}
