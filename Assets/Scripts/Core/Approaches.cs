using System;
using System.Collections.Generic;

namespace Arena
{
    /// One enemy of a boss's faction: how often it's picked, and the first wave it can come in.
    public class FactionMember
    {
        public EnemyType Type;
        public float Weight = 1f;
        public int From = 1;
        public bool NotFirst;  // not in the first round of a run (Rioters open the Warden's approach alone)
        public int Fade = 3;  // after its boss: the last of the 3 fade waves it is still in (0: gone at the boss, 1-3), Long Campaign only
    }

    /// A boss's world: the enemies of its approach, and squads of them (which count toward the themed share).
    public class FactionDef
    {
        public EnemyType Boss;
        public FactionMember[] Members;
        public SquadDef[] Squads = new SquadDef[0];
        public SquadDef Finale;  // always in the last wave before the boss (when the approach is on), counting toward its share
        public WaveLesson[] Lessons;  // a Campaign's ten waves before this boss, one lesson each (null: built by reach, as before)
        public SquadDef[] LessonSquads = new SquadDef[0];  // small squads only the lessons use (by name); reach, comebacks and callbacks never pick them
    }

    /// One wave of a boss's teaching cycle in the 100-wave Campaign (Docs/RunLength.md step 3): the faction's enemies and
    /// squads that wave uses, so each wave previews one thing the boss will test. Squads by name (FactionDef.Squads).
    public class WaveLesson
    {
        public string Teaches;                 // what the wave is for (the docs and the test)
        public FactionMember[] Members;        // null: all the faction's members
        public string[] Squads = new string[0];  // null: all the faction's squads
        public bool Finale;                    // the faction's set piece comes in
        public string Also;                    // a squad (by name) that comes in big on top of the share, like a callback (the exam's Breakout)
        public float SquadChance = 0.5f;       // how often a fitting squad is picked over a single
        public EnemyType? Star;  // the enemy the wave is about: always at least StarPicks of it (null: none)
        public bool Trickle;  // the star comes in as stragglers through the wave's second half, not mixed in anywhere (Docs/Approaches.md)
        public const int StarPicks = 2;
    }

    /// Boss approaches (Docs/Approaches.md): the four regular waves before each boss take part of their enemies from
    /// that boss's faction. How much is the difficulty's Boss approach (ApproachShare: the share of the last wave before
    /// the boss) and Approach ramp (on: the four waves rise to it, a quarter at a time; off: all four use it). At 0% the
    /// waves are built exactly as before. Waves are built on the host; every game works out the share from the wave and
    /// the difficulty, so the HUD needs nothing extra on the wire.
    public static class Approaches
    {
        public static readonly FactionDef[] All =
        {
            new FactionDef
            {
                Boss = EnemyType.Boss,  // the Warden: prisoners, guards and riot gear (Warden.cs, WardenFaction.cs)
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Rioter, Weight = 3f },
                    new FactionMember { Type = EnemyType.Guard, NotFirst = true, Weight = 2f },
                    new FactionMember { Type = EnemyType.RiotShield, Fade = 0, Weight = 2f, From = 2 },
                    new FactionMember { Type = EnemyType.GasGrenadier, Fade = 0, Weight = 1.5f, From = 2 },
                    new FactionMember { Type = EnemyType.TaserGuard, Fade = 0, Weight = 1.5f, From = 3 },  // the faction's shooter that stuns (WardenFaction.cs)
                    new FactionMember { Type = EnemyType.SecurityGuard, Fade = 1, Weight = 2f, From = 2 },  // its pistol guard: leading tracer rounds, a magazine of seven (SecurityGuard.cs)
                    new FactionMember { Type = EnemyType.Thug, Fade = 2, Weight = 1.5f, From = 2 },  // the prisoners' brute: a Brute with a ball and chain (PrisonKin.cs)
                    new FactionMember { Type = EnemyType.Shiv, Fade = 2, Weight = 1f, From = 3 },  // the prisoners' lunger: a taped shank (PrisonKin.cs)
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Breakout", From = 1, Members = new[] { EnemyType.Guard, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Yard", From = 3, Members = new[] { EnemyType.Thug, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Shiv } },
                    new SquadDef { Name = "Patrol", From = 2, Members = new[] { EnemyType.SecurityGuard, EnemyType.SecurityGuard, EnemyType.SecurityGuard, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Cell block", From = 3, Members = new[] { EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                },
                // Wave 4: a wall of four Riot Shields marching at you, five Guards close behind it (WardenFaction.cs).
                LessonSquads = new[]
                {
                    new SquadDef { Name = "Rioter pack", From = 1, Members = new[] { EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Guard pair", From = 1, Members = new[] { EnemyType.Guard, EnemyType.Guard } },
                },
                Finale = new SquadDef { Name = "Riot line", From = 4, Form = SquadForm.RiotLine, Members = new[] {
                    EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.RiotShield,
                    EnemyType.Guard, EnemyType.Guard, EnemyType.Guard, EnemyType.Guard, EnemyType.Guard } },
                // The Campaign's Warden cycle: each wave previews one part of its fight (Docs/RunLength.md).
                Lessons = new[]
                {
                    // Docs/Approaches.md's chart: each wave one new enemy, alone and late; squads only use enemies you have already met.
                    new WaveLesson { Teaches = "Rioters swarm (the riot stage's crowds)", Members = new[] { M(EnemyType.Rioter, 1f) } },
                    new WaveLesson { Teaches = "Guards join (the first one trickles in late)", Trickle = true, Star = EnemyType.Guard, Members = new[] { M(EnemyType.Rioter, 1f) }, Squads = new[] { "Rioter pack" } },
                    new WaveLesson { Teaches = "Thugs: the prisoners' brute", Trickle = true, Star = EnemyType.Thug, Members = new[] { M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 1.5f) }, Squads = new[] { "Rioter pack", "Guard pair" } },
                    new WaveLesson { Teaches = "Security Guards' aimed tracers (its aimed fans)", Trickle = true, Star = EnemyType.SecurityGuard, Members = new[] { M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 2f), M(EnemyType.Thug, 1.5f) }, Squads = new[] { "Yard", "Guard pair" } },
                    new WaveLesson { Teaches = "A recap of everything so far", Members = new[] { M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 2f), M(EnemyType.Thug, 1.5f), M(EnemyType.SecurityGuard, 2f) }, Squads = new[] { "Yard", "Patrol" } },
                    new WaveLesson { Teaches = "Riot Shields: flank them (its riot shield)", Trickle = true, Star = EnemyType.RiotShield, Members = new[] { M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 2f), M(EnemyType.Thug, 1.5f), M(EnemyType.SecurityGuard, 2f) }, Squads = new[] { "Yard", "Patrol" } },
                    new WaveLesson { Teaches = "Tear gas, and shields shoving you into it", Trickle = true, Star = EnemyType.GasGrenadier, Members = new[] { M(EnemyType.RiotShield, 2f), M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 1f), M(EnemyType.SecurityGuard, 1f) }, Squads = new[] { "Yard", "Cell block" } },
                    new WaveLesson { Teaches = "Taser Guards: stuns, and parrying the Fork Bolt back", Trickle = true, Star = EnemyType.TaserGuard, Members = new[] { M(EnemyType.GasGrenadier, 1.5f), M(EnemyType.RiotShield, 2f), M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 1f) }, Squads = new[] { "Yard", "Cell block" } },
                    new WaveLesson { Teaches = "Every squad, tighter", Members = null, Squads = null, SquadChance = 0.75f },
                    new WaveLesson { Teaches = "The exam: the Riot line and a big Breakout, with everything", Members = null, Squads = new string[0], Finale = true, Also = "Breakout" },
                },
            },
            new FactionDef
            {
                Boss = EnemyType.HiveQueen,  // the Hive Queen: bees, one for each of the Warden's roles (HiveKin.cs, Docs/HiveFaction.md)
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Worker, Weight = 3f },
                    new FactionMember { Type = EnemyType.Soldier, NotFirst = true, Weight = 2f },
                    new FactionMember { Type = EnemyType.CombShield, Fade = 0, Weight = 2f, From = 2 },
                    new FactionMember { Type = EnemyType.HoneyLobber, Fade = 0, Weight = 1.5f, From = 2 },
                    new FactionMember { Type = EnemyType.Stinger, Fade = 0, Weight = 1.5f, From = 3 },
                    new FactionMember { Type = EnemyType.Needler, Fade = 1, Weight = 2f, From = 2 },
                    new FactionMember { Type = EnemyType.Bumble, Fade = 2, Weight = 1.5f, From = 2 },
                    new FactionMember { Type = EnemyType.Hornet, Fade = 2, Weight = 1f, From = 3 },
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Swarm", From = 1, Members = new[] { EnemyType.Soldier, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker } },
                    new SquadDef { Name = "Nursery", From = 3, Members = new[] { EnemyType.Bumble, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker, EnemyType.Hornet } },
                    new SquadDef { Name = "Picket", From = 2, Members = new[] { EnemyType.Needler, EnemyType.Needler, EnemyType.Needler, EnemyType.Worker, EnemyType.Worker } },
                    new SquadDef { Name = "Honeycomb", From = 3, Members = new[] { EnemyType.CombShield, EnemyType.CombShield, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker } },
                },
                LessonSquads = new[]
                {
                    new SquadDef { Name = "Worker pack", From = 1, Members = new[] { EnemyType.Worker, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker, EnemyType.Worker } },
                    new SquadDef { Name = "Soldier pair", From = 1, Members = new[] { EnemyType.Soldier, EnemyType.Soldier } },
                },
                // Wave 4: a wall of four Comb Shields with five Soldiers close behind.
                Finale = new SquadDef { Name = "Royal guard", From = 4, Members = new[] {
                    EnemyType.CombShield, EnemyType.CombShield, EnemyType.CombShield, EnemyType.CombShield,
                    EnemyType.Soldier, EnemyType.Soldier, EnemyType.Soldier, EnemyType.Soldier, EnemyType.Soldier } },
                // The Campaign's Hive cycle: the same ten lessons as the Warden's, in the same order (Docs/Approaches.md's chart).
                Lessons = new[]
                {
                    new WaveLesson { Teaches = "Workers swarm (the Swarm stage's drone crowds)", Members = new[] { M(EnemyType.Worker, 1f) } },
                    new WaveLesson { Teaches = "Soldiers join (the first one trickles in late)", Trickle = true, Star = EnemyType.Soldier, Members = new[] { M(EnemyType.Worker, 1f) }, Squads = new[] { "Worker pack" } },
                    new WaveLesson { Teaches = "Bumbles: the heavy ones", Trickle = true, Star = EnemyType.Bumble, Members = new[] { M(EnemyType.Worker, 2f), M(EnemyType.Soldier, 1.5f) }, Squads = new[] { "Worker pack", "Soldier pair" } },
                    new WaveLesson { Teaches = "Needlers' aimed shots (the bomber barrage)", Trickle = true, Star = EnemyType.Needler, Members = new[] { M(EnemyType.Worker, 2f), M(EnemyType.Soldier, 2f), M(EnemyType.Bumble, 1.5f) }, Squads = new[] { "Nursery", "Soldier pair" } },
                    new WaveLesson { Teaches = "A recap of everything so far", Members = new[] { M(EnemyType.Worker, 2f), M(EnemyType.Soldier, 2f), M(EnemyType.Bumble, 1.5f), M(EnemyType.Needler, 2f) }, Squads = new[] { "Nursery", "Picket" } },
                    new WaveLesson { Teaches = "Comb Shields: break the cell facing you (the honeycomb floor)", Trickle = true, Star = EnemyType.CombShield, Members = new[] { M(EnemyType.Worker, 2f), M(EnemyType.Soldier, 2f), M(EnemyType.Bumble, 1.5f), M(EnemyType.Needler, 2f) }, Squads = new[] { "Nursery", "Picket" } },
                    new WaveLesson { Teaches = "Honey: leave the ring, the puddle slows you (the nectar spray)", Trickle = true, Star = EnemyType.HoneyLobber, Members = new[] { M(EnemyType.CombShield, 2f), M(EnemyType.Worker, 2f), M(EnemyType.Soldier, 1f), M(EnemyType.Needler, 1f) }, Squads = new[] { "Nursery", "Honeycomb" } },
                    new WaveLesson { Teaches = "Stingers: a stunning dart, strafe across the line (the aimed fans)", Trickle = true, Star = EnemyType.Stinger, Members = new[] { M(EnemyType.HoneyLobber, 1.5f), M(EnemyType.CombShield, 2f), M(EnemyType.Worker, 2f), M(EnemyType.Soldier, 1f) }, Squads = new[] { "Nursery", "Honeycomb" } },
                    new WaveLesson { Teaches = "Every squad, tighter", Members = null, Squads = null, SquadChance = 0.75f },
                    new WaveLesson { Teaches = "The exam: the Royal guard and a big Swarm, with everything", Members = null, Squads = new string[0], Finale = true, Also = "Swarm" },
                },
            },
            new FactionDef
            {
                Boss = EnemyType.TwinA,  // the Twin Reapers: the Dusk and Dawn cult (ReaperFaction.cs, Docs/Mockups/reaper-faction.html)
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Wisp, Weight = 3f },  // (a pick is a pack of three)
                    new FactionMember { Type = EnemyType.Acolyte, NotFirst = true, Weight = 2f },
                    new FactionMember { Type = EnemyType.Lamplighter, Fade = 1, Weight = 2f, From = 2 },
                    new FactionMember { Type = EnemyType.BoundWraith, Fade = 0, Weight = 1.5f, From = 2 },  // (a pick is a pair)
                    new FactionMember { Type = EnemyType.Gravedigger, Fade = 2, Weight = 1.5f, From = 2 },  // the cult's brute (a Brute with a shovel)
                    new FactionMember { Type = EnemyType.Mourner, Fade = 0, Weight = 1.5f, From = 3 },
                    new FactionMember { Type = EnemyType.ScytheWraith, Fade = 0, Weight = 1.5f, From = 3 },
                    new FactionMember { Type = EnemyType.Shade, Fade = 2, Weight = 1f, From = 3 },  // the cult's Phantom
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Vigil", From = 1, Members = new[] { EnemyType.Acolyte, EnemyType.Wisp, EnemyType.Wisp, EnemyType.Wisp, EnemyType.Wisp, EnemyType.Wisp, EnemyType.Wisp } },
                    new SquadDef { Name = "Procession", From = 2, Members = new[] { EnemyType.Acolyte, EnemyType.Acolyte, EnemyType.Acolyte, EnemyType.Acolyte, EnemyType.Lamplighter, EnemyType.Lamplighter } },
                    new SquadDef { Name = "Twin graves", From = 2, Members = new[] { EnemyType.Gravedigger, EnemyType.BoundWraith, EnemyType.BoundWraith } },  // (two pairs)
                    new SquadDef { Name = "Harvest", From = 3, Members = new[] { EnemyType.ScytheWraith, EnemyType.ScytheWraith, EnemyType.Shade, EnemyType.Shade, EnemyType.Wisp, EnemyType.Wisp, EnemyType.Wisp } },
                },
                // Wave 4: split to opposite edges, a Mourner on each side with a Bound pair and three Acolytes (three types, so the
                // wave stays under its type cap: Docs/EnemyReadability.md).
                Finale = new SquadDef { Name = "Dusk and Dawn", From = 4, Form = SquadForm.Pincer, Members = new[] {
                    EnemyType.Mourner, EnemyType.Mourner, EnemyType.BoundWraith, EnemyType.BoundWraith,
                    EnemyType.Acolyte, EnemyType.Acolyte, EnemyType.Acolyte, EnemyType.Acolyte, EnemyType.Acolyte, EnemyType.Acolyte } },
                // The Campaign's Twin Reapers cycle: each wave previews one part of their fight (Docs/RunLength.md).
                Lessons = new[]
                {
                    new WaveLesson { Teaches = "Wisps drift in (the Reapers' soul shots)", Members = new[] { M(EnemyType.Wisp, 1f) } },
                    new WaveLesson { Teaches = "Acolytes: a raised sickle means step back (the Dual Reaper's chase)", Trickle = true, Star = EnemyType.Acolyte, Members = new[] { M(EnemyType.Wisp, 2f), M(EnemyType.Acolyte, 2f) }, Squads = new[] { "Vigil" } },
                    new WaveLesson { Teaches = "Lamplighters: lines and the cross, fired standing still (Twilight)", Trickle = true, Star = EnemyType.Lamplighter, Members = new[] { M(EnemyType.Lamplighter, 2f), M(EnemyType.Acolyte, 2f), M(EnemyType.Wisp, 1f) }, Squads = new[] { "Procession" } },
                    new WaveLesson { Teaches = "Bound Wraiths: the tether sweep, and killing both (Soul Bond)", Trickle = true, Star = EnemyType.BoundWraith, Members = new[] { M(EnemyType.BoundWraith, 2f), M(EnemyType.Wisp, 2f) }, Squads = new[] { "Twin graves" } },
                    new WaveLesson { Teaches = "A recap of everything so far, and the Gravediggers join", Trickle = true, Star = EnemyType.Gravedigger, Members = new[] { M(EnemyType.Wisp, 2f), M(EnemyType.Acolyte, 2f), M(EnemyType.Gravedigger, 1.5f), M(EnemyType.Lamplighter, 1f), M(EnemyType.BoundWraith, 1f) }, Squads = new[] { "Vigil", "Twin graves" } },
                    new WaveLesson { Teaches = "Mourners: the twin spiral, and pushing in while they rest", Trickle = true, Star = EnemyType.Mourner, Members = new[] { M(EnemyType.Mourner, 2.5f), M(EnemyType.Acolyte, 2f), M(EnemyType.Wisp, 1f) } },
                    new WaveLesson { Teaches = "Scythe Wraiths: marked reaps (Sundered), and the Shades blink in", Trickle = true, Star = EnemyType.ScytheWraith, Members = new[] { M(EnemyType.ScytheWraith, 2f), M(EnemyType.Shade, 1.5f), M(EnemyType.Lamplighter, 1.5f), M(EnemyType.Wisp, 1f) }, Squads = new[] { "Harvest" } },
                    new WaveLesson { Teaches = "Squads: Vigil, Procession, Twin graves, Harvest", Members = new[] { M(EnemyType.Wisp, 1f), M(EnemyType.Acolyte, 1f) }, Squads = null, SquadChance = 0.9f },
                    new WaveLesson { Teaches = "Everything, tighter", Members = null, Squads = null, SquadChance = 0.75f },
                    new WaveLesson { Teaches = "The exam: Dusk and Dawn with everything", Members = null, Squads = null, Finale = true },
                },
            },
            new FactionDef
            {
                Boss = EnemyType.Singularity,  // the Singularity: the Infall, matter falling into the black hole (Infall.cs, Docs/SingularityFaction.md)
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Debris, Weight = 3f },
                    new FactionMember { Type = EnemyType.VoidAcolyte, NotFirst = true, Weight = 2f },
                    new FactionMember { Type = EnemyType.Lens, Fade = 0, Weight = 2f, From = 2 },  // bends your shots round its face (Gravity Wells)
                    new FactionMember { Type = EnemyType.Graviton, Fade = 0, Weight = 1.5f, From = 2 },  // lobs a Small Well (the Pull, Gravity Wells)
                    new FactionMember { Type = EnemyType.Orbiter, Fade = 0, Weight = 1.5f, From = 3 },  // slings its orbs (the disk, Slingshot)
                    new FactionMember { Type = EnemyType.Pulsar, Fade = 1, Weight = 1.5f, From = 2 },  // its sweeping beams (the boss's Lasers)
                    new FactionMember { Type = EnemyType.Collapsar, Fade = 2, Weight = 1.5f, From = 2 },  // implodes when killed (Collapse, Hawking)
                    new FactionMember { Type = EnemyType.WhiteDwarf, Fade = 2, Weight = 1f, From = 3 },  // turns your shots round (White hole)
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Infall", From = 1, Members = new[] { EnemyType.VoidAcolyte, EnemyType.Debris, EnemyType.Debris, EnemyType.Debris, EnemyType.Debris, EnemyType.Debris, EnemyType.Debris } },
                    new SquadDef { Name = "Binary", From = 2, Members = new[] { EnemyType.Pulsar, EnemyType.Pulsar } },
                    new SquadDef { Name = "Gravity trap", From = 3, Members = new[] { EnemyType.Graviton, EnemyType.Collapsar, EnemyType.Debris, EnemyType.Debris, EnemyType.Debris } },
                    new SquadDef { Name = "Lens array", From = 3, Members = new[] { EnemyType.Lens, EnemyType.Lens, EnemyType.VoidAcolyte, EnemyType.VoidAcolyte, EnemyType.VoidAcolyte } },
                },
                // Wave 4: the Accretion ring, eight Debris and four Orbiters in a ring round you that turns and closes, two gaps open (Infall.cs).
                Finale = new SquadDef { Name = "Accretion ring", From = 4, Form = SquadForm.Accretion, Members = new[] {
                    EnemyType.Orbiter, EnemyType.Debris, EnemyType.Debris, EnemyType.Orbiter, EnemyType.Debris, EnemyType.Debris,
                    EnemyType.Orbiter, EnemyType.Debris, EnemyType.Debris, EnemyType.Orbiter, EnemyType.Debris, EnemyType.Debris } },
                // The Campaign's Singularity cycle: each wave previews one thing its fight tests (Docs/SingularityFaction.md).
                Lessons = new[]
                {
                    new WaveLesson { Teaches = "Debris falls in on a spiral (the Pull)", Members = new[] { M(EnemyType.Debris, 1f) } },
                    new WaveLesson { Teaches = "Void Acolytes' orbs bend in to meet on you (bent shots)", Trickle = true, Star = EnemyType.VoidAcolyte, Members = new[] { M(EnemyType.Debris, 2f) }, Squads = new[] { "Infall" } },
                    new WaveLesson { Teaches = "Lenses: flank them (its Gravity Wells bend your shots)", Trickle = true, Star = EnemyType.Lens, Members = new[] { M(EnemyType.Debris, 2f), M(EnemyType.VoidAcolyte, 1.5f) }, Squads = new[] { "Lens array" } },
                    new WaveLesson { Teaches = "Gravitons' Small Wells: get clear or pop them (the Pull, Gravity Wells)", Trickle = true, Star = EnemyType.Graviton, Members = new[] { M(EnemyType.Lens, 1.5f), M(EnemyType.Debris, 2f), M(EnemyType.VoidAcolyte, 1.5f) } },
                    new WaveLesson { Teaches = "Pulsars' flickering beams (its Lasers)", Trickle = true, Star = EnemyType.Pulsar, Members = new[] { M(EnemyType.Debris, 2f), M(EnemyType.VoidAcolyte, 1.5f) }, Squads = new[] { "Binary" } },
                    new WaveLesson { Teaches = "A recap of everything so far, and Collapsars join (Collapse, Hawking)", Trickle = true, Star = EnemyType.Collapsar, Members = new[] { M(EnemyType.Debris, 2f), M(EnemyType.VoidAcolyte, 1.5f), M(EnemyType.Lens, 1f), M(EnemyType.Graviton, 1f), M(EnemyType.Pulsar, 1f) }, Squads = new[] { "Gravity trap", "Infall" } },
                    new WaveLesson { Teaches = "Orbiters sling their orbs (the disk, Slingshot)", Trickle = true, Star = EnemyType.Orbiter, Members = new[] { M(EnemyType.Orbiter, 2f), M(EnemyType.Debris, 2f), M(EnemyType.VoidAcolyte, 1f) } },
                    new WaveLesson { Teaches = "White Dwarfs turn your shots round (White hole)", Trickle = true, Star = EnemyType.WhiteDwarf, Members = new[] { M(EnemyType.Orbiter, 1.5f), M(EnemyType.Pulsar, 1f), M(EnemyType.Collapsar, 1.5f), M(EnemyType.Debris, 1.5f) } },
                    new WaveLesson { Teaches = "Squads: Infall, Binary, Gravity trap, Lens array", Members = new[] { M(EnemyType.Debris, 1f), M(EnemyType.VoidAcolyte, 1f) }, Squads = null, SquadChance = 0.9f },
                    new WaveLesson { Teaches = "The exam: the Accretion ring with everything", Members = null, Squads = null, Finale = true },
                },
            },
            new FactionDef
            {
                Boss = EnemyType.Colossus,  // the Colossus: The Quarry, its crew and the stone that broke off it (QuarryFaction.cs, Docs/Approaches.md "The Quarry") (Docs/Approaches.md)
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Rubble, Weight = 3f },
                    new FactionMember { Type = EnemyType.Quarryman, NotFirst = true, Weight = 2f },
                    new FactionMember { Type = EnemyType.Stomper, Fade = 2, Weight = 1.5f, From = 2 },  // a Thug that stomps out a small push ring
                    new FactionMember { Type = EnemyType.SlabBearer, Fade = 0, Weight = 2f, From = 2 },  // a Riot Shield whose broken slab is cover
                    new FactionMember { Type = EnemyType.RockHurler, Fade = 0, Weight = 1.5f, From = 2 },  // a Gas Grenadier that lobs a boulder
                    new FactionMember { Type = EnemyType.Driller, Fade = 2, Weight = 1f, From = 3 },  // a Shiv whose lunge leaves a crack that erupts
                    new FactionMember { Type = EnemyType.Geomancer, Fade = 1, Weight = 2f, From = 3 },  // sends a crack racing at you
                    new FactionMember { Type = EnemyType.CinderPriest, Fade = 0, Weight = 1.5f, From = 3 },  // a magma bolt that leaves lava
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Landslip", From = 1, Members = new[] { EnemyType.Quarryman, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble } },
                    new SquadDef { Name = "Dig crew", From = 2, Members = new[] { EnemyType.Quarryman, EnemyType.Quarryman, EnemyType.RockHurler, EnemyType.RockHurler } },
                    new SquadDef { Name = "Wall-in", From = 3, Members = new[] { EnemyType.SlabBearer, EnemyType.SlabBearer, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble } },
                    new SquadDef { Name = "Quarry", From = 3, Members = new[] { EnemyType.Stomper, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Driller } },
                    new SquadDef { Name = "Fault line", From = 3, Members = new[] { EnemyType.Geomancer, EnemyType.Geomancer, EnemyType.Geomancer } },
                },
                LessonSquads = new[]
                {
                    new SquadDef { Name = "Rubble pack", From = 1, Members = new[] { EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble, EnemyType.Rubble } },
                    new SquadDef { Name = "Quarryman pair", From = 1, Members = new[] { EnemyType.Quarryman, EnemyType.Quarryman } },
                },
                // Wave 4: the Avalanche, its Riot line: four Slab Bearers marching at you as a wall, two Stompers and two Rock
                // Hurlers close behind (the hurlers lob over it); within RiotLineRelease, or with half the slabs broken, they break out.
                Finale = new SquadDef { Name = "Avalanche", From = 4, Form = SquadForm.RiotLine, Members = new[] {
                    EnemyType.SlabBearer, EnemyType.SlabBearer, EnemyType.SlabBearer, EnemyType.SlabBearer,
                    EnemyType.Stomper, EnemyType.RockHurler, EnemyType.RockHurler, EnemyType.Stomper } },
                // The Campaign's Colossus cycle: the Warden's ten lessons in the same order, each pointing at one of its turns.
                Lessons = new[]
                {
                    new WaveLesson { Teaches = "Rubble rolls in (the Colossus's rock volleys)", Members = new[] { M(EnemyType.Rubble, 1f) } },
                    new WaveLesson { Teaches = "Quarrymen join (the first one trickles in late)", Trickle = true, Star = EnemyType.Quarryman, Members = new[] { M(EnemyType.Rubble, 1f) }, Squads = new[] { "Rubble pack" } },
                    new WaveLesson { Teaches = "Stompers: step into the green gap (its Stomps)", Trickle = true, Star = EnemyType.Stomper, Members = new[] { M(EnemyType.Rubble, 2f), M(EnemyType.Quarryman, 1.5f) }, Squads = new[] { "Rubble pack", "Quarryman pair" } },
                    new WaveLesson { Teaches = "Geomancers: step off the crack (its Fissures)", Trickle = true, Star = EnemyType.Geomancer, Members = new[] { M(EnemyType.Rubble, 2f), M(EnemyType.Quarryman, 2f), M(EnemyType.Stomper, 1.5f) }, Squads = new[] { "Quarry", "Quarryman pair" } },
                    new WaveLesson { Teaches = "A recap, and Drillers join (a crack behind the lunge)", Trickle = true, Star = EnemyType.Driller, Members = new[] { M(EnemyType.Rubble, 2f), M(EnemyType.Quarryman, 2f), M(EnemyType.Stomper, 1.5f), M(EnemyType.Geomancer, 2f) }, Squads = new[] { "Quarry", "Fault line" } },
                    new WaveLesson { Teaches = "Slab Bearers: flank them, a broken slab is cover (its fallen armour)", Trickle = true, Star = EnemyType.SlabBearer, Members = new[] { M(EnemyType.Rubble, 2f), M(EnemyType.Quarryman, 2f), M(EnemyType.Stomper, 1.5f), M(EnemyType.Geomancer, 2f) }, Squads = new[] { "Quarry", "Fault line" } },
                    new WaveLesson { Teaches = "Rock Hurlers: change direction out of the ring (its Boulders)", Trickle = true, Star = EnemyType.RockHurler, Members = new[] { M(EnemyType.SlabBearer, 2f), M(EnemyType.Rubble, 2f), M(EnemyType.Quarryman, 1f), M(EnemyType.Geomancer, 1f) }, Squads = new[] { "Quarry", "Wall-in", "Dig crew" } },
                    new WaveLesson { Teaches = "Cinder Priests: lava where the bolt stops, parry it back (its Molten Core)", Trickle = true, Star = EnemyType.CinderPriest, Members = new[] { M(EnemyType.RockHurler, 1.5f), M(EnemyType.SlabBearer, 2f), M(EnemyType.Rubble, 2f), M(EnemyType.Quarryman, 1f) }, Squads = new[] { "Quarry", "Wall-in" } },
                    new WaveLesson { Teaches = "Every squad, tighter", Members = null, Squads = null, SquadChance = 0.75f },
                    new WaveLesson { Teaches = "The exam: the Avalanche and a big Landslip, with everything", Members = null, Squads = new string[0], Finale = true, Also = "Landslip" },
                },
            },
            new FactionDef
            {
                Boss = EnemyType.Duelist,  // the Duelist: the Masked Court, masked fencers (CourtFaction.cs, Docs/Mockups/duelist-faction.html)
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Fencer, Weight = 3f },
                    new FactionMember { Type = EnemyType.BladeDancer, NotFirst = true, Weight = 2f },  // a Guard that lunges and leaves blades
                    new FactionMember { Type = EnemyType.Parrier, Fade = 0, Weight = 2f, From = 2 },   // a Riot Shield that parries shots back
                    new FactionMember { Type = EnemyType.Second, Fade = 1, Weight = 2f, From = 2 },    // a Security Guard that throws dagger fans
                    new FactionMember { Type = EnemyType.Marshal, Fade = 0, Weight = 1.5f, From = 2 }, // a Gas Grenadier that draws a dueling circle round you
                    new FactionMember { Type = EnemyType.Gallant, Fade = 0, Weight = 1.5f, From = 3 }, // a Taser Guard that salutes and lunges: parry it
                    new FactionMember { Type = EnemyType.Harlequin, Fade = 2, Weight = 1.5f, From = 2 }, // a Thug that dashes an X of blades
                    new FactionMember { Type = EnemyType.Mummer, Fade = 2, Weight = 1f, From = 3 },      // a Shiv that splits into three
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Salon", From = 1, Members = new[] { EnemyType.BladeDancer, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer } },
                    new SquadDef { Name = "Seconds", From = 2, Members = new[] { EnemyType.Second, EnemyType.Second, EnemyType.Second, EnemyType.Fencer, EnemyType.Fencer } },
                    new SquadDef { Name = "Masked ball", From = 3, Members = new[] { EnemyType.Harlequin, EnemyType.Mummer, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer } },
                    new SquadDef { Name = "Guard line", From = 3, Members = new[] { EnemyType.Parrier, EnemyType.Parrier, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer } },
                },
                LessonSquads = new[]
                {
                    new SquadDef { Name = "Fencer pack", From = 1, Members = new[] { EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer } },
                    new SquadDef { Name = "Dancer pair", From = 1, Members = new[] { EnemyType.BladeDancer, EnemyType.BladeDancer } },
                },
                // Wave 4: the Ring of Honour, ten Fencers and two Gallants in a ring round you that turns slowly and closes; every
                // 1.4 s an opposite pair of Fencers lunges in, and the Gallants salute (parry them); it breaks up after 14 s or
                // once half is down (CourtFaction.cs).
                Finale = new SquadDef { Name = "Ring of Honour", From = 4, Form = SquadForm.Honour, Members = new[] {
                    EnemyType.Fencer, EnemyType.Fencer, EnemyType.Gallant, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer,
                    EnemyType.Fencer, EnemyType.Fencer, EnemyType.Gallant, EnemyType.Fencer, EnemyType.Fencer, EnemyType.Fencer } },
                // The Campaign's Duelist cycle: the Warden's ten lessons in the same order, each pointing at one of its moves.
                Lessons = new[]
                {
                    new WaveLesson { Teaches = "Fencers hop in (the Duelist's Lunge line)", Members = new[] { M(EnemyType.Fencer, 1f) } },
                    new WaveLesson { Teaches = "Blade Dancers: never stand beside the line (its Lunge blades)", Trickle = true, Star = EnemyType.BladeDancer, Members = new[] { M(EnemyType.Fencer, 1f) }, Squads = new[] { "Fencer pack" } },
                    new WaveLesson { Teaches = "Harlequins: stand in a gap of the X (its Crossing)", Trickle = true, Star = EnemyType.Harlequin, Members = new[] { M(EnemyType.Fencer, 2f), M(EnemyType.BladeDancer, 1.5f) }, Squads = new[] { "Fencer pack", "Dancer pair" } },
                    new WaveLesson { Teaches = "Seconds: slip between the daggers (its Volleys)", Trickle = true, Star = EnemyType.Second, Members = new[] { M(EnemyType.Fencer, 2f), M(EnemyType.BladeDancer, 2f), M(EnemyType.Harlequin, 1.5f) }, Squads = new[] { "Salon", "Dancer pair" } },
                    new WaveLesson { Teaches = "A recap, and Mummers join: find the real one (its Masquerade)", Trickle = true, Star = EnemyType.Mummer, Members = new[] { M(EnemyType.Fencer, 2f), M(EnemyType.BladeDancer, 2f), M(EnemyType.Harlequin, 1.5f), M(EnemyType.Second, 2f) }, Squads = new[] { "Salon", "Seconds" } },
                    new WaveLesson { Teaches = "Parriers: get behind them (its Guard)", Trickle = true, Star = EnemyType.Parrier, Members = new[] { M(EnemyType.Fencer, 2f), M(EnemyType.BladeDancer, 2f), M(EnemyType.Harlequin, 1.5f), M(EnemyType.Second, 2f) }, Squads = new[] { "Salon", "Seconds" } },
                    new WaveLesson { Teaches = "Marshals: fight in the middle of the circle (its dueling circle)", Trickle = true, Star = EnemyType.Marshal, Members = new[] { M(EnemyType.Parrier, 2f), M(EnemyType.Fencer, 2f), M(EnemyType.BladeDancer, 1f), M(EnemyType.Second, 1f) }, Squads = new[] { "Salon", "Guard line" } },
                    new WaveLesson { Teaches = "Gallants salute: parry them as they arrive (its Salute)", Trickle = true, Star = EnemyType.Gallant, Members = new[] { M(EnemyType.Marshal, 1.5f), M(EnemyType.Parrier, 2f), M(EnemyType.Fencer, 2f), M(EnemyType.BladeDancer, 1f) }, Squads = new[] { "Masked ball", "Guard line" } },
                    new WaveLesson { Teaches = "Every squad, tighter", Members = null, Squads = null, SquadChance = 0.75f },
                    new WaveLesson { Teaches = "The exam: the Ring of Honour and a big Salon, with everything", Members = null, Squads = new string[0], Finale = true, Also = "Salon" },
                },
            },
            new FactionDef
            {
                Boss = EnemyType.Lich,  // the Lich: the restless dead, Zombie Infection's seven and his own Grave Caller (Zombies.cs, Docs/Mockups/lich-faction.html); they don't get back up outside that modifier, only out of a Caller's graves
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Shambler, Weight = 4f },
                    new FactionMember { Type = EnemyType.Runner, Weight = 1.5f, From = 2 },  // (a pick is a pack of three)
                    new FactionMember { Type = EnemyType.Spitter, Weight = 1.5f, From = 2 },
                    new FactionMember { Type = EnemyType.Crawler, Weight = 1f, From = 3 },
                    new FactionMember { Type = EnemyType.Bloater, Weight = 1f, From = 3 },  // bursts into spores (the soul storm: don't fight inside it)
                    new FactionMember { Type = EnemyType.Shrieker, Fade = 1, Weight = 0.5f, From = 3 },  // its scream frenzies the horde (the wailing spirits)
                    new FactionMember { Type = EnemyType.Mender, Weight = 0.7f, From = 4 },
                    new FactionMember { Type = EnemyType.GraveCaller, Fade = 0, Weight = 0.6f, From = 4 },  // graves round you, the dead climbing out (grave rings, the dead rising)
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Graveyard shift", From = 1, Members = new[] { EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler } },
                    new SquadDef { Name = "Grave rush", From = 2, Members = new[] { EnemyType.Runner, EnemyType.Runner, EnemyType.Runner, EnemyType.Runner, EnemyType.Runner, EnemyType.Shambler, EnemyType.Shambler } },
                    new SquadDef { Name = "Gravediggers", From = 3, Members = new[] { EnemyType.Crawler, EnemyType.Crawler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler } },
                    new SquadDef { Name = "Plague cart", From = 3, Members = new[] { EnemyType.Bloater, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Spitter, EnemyType.Spitter } },
                    new SquadDef { Name = "Wake", From = 3, Members = new[] { EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Runner, EnemyType.Runner, EnemyType.Runner, EnemyType.Shrieker } },
                    new SquadDef { Name = "Wake the dead", From = 4, Members = new[] { EnemyType.Shambler, EnemyType.Shambler, EnemyType.Spitter, EnemyType.Spitter, EnemyType.GraveCaller } },
                },
                LessonSquads = new[]
                {
                    new SquadDef { Name = "Shambling pair", From = 1, Members = new[] { EnemyType.Shambler, EnemyType.Shambler } },
                },
                // Wave 4: a shambling wall with Menders to keep it standing, Runners on its flanks, a Shrieker and two Grave Callers at the back.
                Finale = new SquadDef { Name = "Dead march", From = 4, Members = new[] {
                    EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler,
                    EnemyType.Mender, EnemyType.Mender, EnemyType.Runner, EnemyType.Runner, EnemyType.Runner,
                    EnemyType.Shrieker, EnemyType.GraveCaller, EnemyType.GraveCaller } },
                // The Campaign's Lich cycle: one zombie a wave, the same shape as the others' (Docs/Mockups/lich-faction.html).
                Lessons = new[]
                {
                    new WaveLesson { Teaches = "Shamblers: a slow wall, don't get cornered (the Crypt's dead)", Members = new[] { M(EnemyType.Shambler, 1f) }, Squads = new[] { "Graveyard shift" } },
                    new WaveLesson { Teaches = "Runners: turn and shoot, kiting doesn't work", Trickle = true, Star = EnemyType.Runner, Members = new[] { M(EnemyType.Shambler, 2f) }, Squads = new[] { "Graveyard shift", "Grave rush" } },
                    new WaveLesson { Teaches = "Spitters: keep moving out of the acid (its soul fans' floor)", Trickle = true, Star = EnemyType.Spitter, Members = new[] { M(EnemyType.Shambler, 2f), M(EnemyType.Runner, 1f) }, Squads = new[] { "Grave rush" } },
                    new WaveLesson { Teaches = "Crawlers: shoot it off you (dead hands)", Trickle = true, Star = EnemyType.Crawler, Members = new[] { M(EnemyType.Shambler, 2f), M(EnemyType.Runner, 1f), M(EnemyType.Spitter, 1f) }, Squads = new[] { "Grave rush", "Gravediggers" } },
                    new WaveLesson { Teaches = "Bloaters: pop it at range, never fight in the spores (the soul storm)", Trickle = true, Star = EnemyType.Bloater, Members = new[] { M(EnemyType.Shambler, 2f), M(EnemyType.Runner, 1f), M(EnemyType.Spitter, 1f) }, Squads = new[] { "Plague cart", "Shambling pair" } },
                    new WaveLesson { Teaches = "A recap, and Shriekers join (the wailing spirits)", Trickle = true, Star = EnemyType.Shrieker, Members = new[] { M(EnemyType.Shambler, 2f), M(EnemyType.Runner, 1f), M(EnemyType.Crawler, 1f), M(EnemyType.Bloater, 1f) }, Squads = new[] { "Wake", "Plague cart" } },
                    new WaveLesson { Teaches = "Menders: kill it first (the Lich's phylactery keeps him standing)", Trickle = true, Star = EnemyType.Mender, Members = new[] { M(EnemyType.Shambler, 2f), M(EnemyType.Spitter, 1f), M(EnemyType.Bloater, 1f), M(EnemyType.Shrieker, 0.5f) }, Squads = new[] { "Wake", "Gravediggers" } },
                    new WaveLesson { Teaches = "Grave Callers: step out of the ring, then kill it (grave rings, the dead rising)", Trickle = true, Star = EnemyType.GraveCaller, Members = new[] { M(EnemyType.Shambler, 2f), M(EnemyType.Runner, 1f), M(EnemyType.Spitter, 1f), M(EnemyType.Mender, 0.7f) }, Squads = new[] { "Wake the dead", "Wake" } },
                    new WaveLesson { Teaches = "Every squad, tighter", Members = null, Squads = null, SquadChance = 0.75f },
                    new WaveLesson { Teaches = "The exam: the Dead march with everything", Members = null, Squads = new string[0], Finale = true, Also = "Graveyard shift" },
                },
            },
            new FactionDef
            {
                Boss = EnemyType.Devourer,  // the Devourer: The Larder, its brood and the butchers who feed it (LarderFaction.cs, Docs/Approaches.md "The Larder"); step 3 of 4
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Gnawling, Weight = 3f },
                    new FactionMember { Type = EnemyType.Butcher, NotFirst = true, Weight = 2f },  // a Guard that chops a half-circle
                    new FactionMember { Type = EnemyType.Gulper, Fade = 2, Weight = 1.5f, From = 2 },  // a Thug that breathes you in
                    new FactionMember { Type = EnemyType.CarcassBearer, Fade = 0, Weight = 2f, From = 2 },  // a Riot Shield whose broken slab is a morsel
                    new FactionMember { Type = EnemyType.BileSpitter, Fade = 0, Weight = 1.5f, From = 2 },  // a Gas Grenadier that lobs bile
                    new FactionMember { Type = EnemyType.Gutworm, Fade = 2, Weight = 1f, From = 3 },  // dives, chases, erupts under you
                    new FactionMember { Type = EnemyType.HookThrower, Fade = 1, Weight = 2f, From = 3 },  // a meat hook that reels you in
                    new FactionMember { Type = EnemyType.MawPriest, Fade = 0, Weight = 1.5f, From = 3 },  // a maw that feeds on your shots
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Feeding Frenzy", From = 1, Members = new[] { EnemyType.Butcher, EnemyType.Gnawling, EnemyType.Gnawling, EnemyType.Gnawling, EnemyType.Gnawling, EnemyType.Gnawling, EnemyType.Gnawling } },
                    new SquadDef { Name = "Larder Wall", From = 3, Members = new[] { EnemyType.CarcassBearer, EnemyType.CarcassBearer, EnemyType.Gnawling, EnemyType.Gnawling, EnemyType.Gnawling, EnemyType.Gnawling } },
                    new SquadDef { Name = "Meat Hooks", From = 3, Members = new[] { EnemyType.Butcher, EnemyType.Butcher, EnemyType.HookThrower, EnemyType.HookThrower } },
                    new SquadDef { Name = "Gullet", From = 3, Members = new[] { EnemyType.Gulper, EnemyType.Gnawling, EnemyType.Gnawling, EnemyType.Gnawling, EnemyType.Gutworm } },
                    new SquadDef { Name = "Bile Rain", From = 3, Members = new[] { EnemyType.BileSpitter, EnemyType.BileSpitter, EnemyType.BileSpitter } },
                },
            },
            new FactionDef
            {
                Boss = EnemyType.Eclipse,  // the Eclipse: the Penumbral Court, each previewing one of its stages (Penumbral.cs, Docs/EclipseFaction.md); step 3 of 4
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Ember, Weight = 3f },  // sparks hang behind it (Lunar wake)
                    new FactionMember { Type = EnemyType.CoronaPriest, NotFirst = true, Weight = 2f },  // a ring with one gap (Corona rings)
                    new FactionMember { Type = EnemyType.Moonward, Fade = 0, Weight = 2f, From = 2 },  // its moon soaks your shots (the Penumbra's moon)
                    new FactionMember { Type = EnemyType.DuskLobber, Fade = 0, Weight = 1.5f, From = 2 },  // a pool of dark (the Umbra)
                    new FactionMember { Type = EnemyType.Heliograph, Fade = 1, Weight = 1.5f, From = 2 },  // a line, then a lane of fire (Solar flares)
                    new FactionMember { Type = EnemyType.Sickle, Fade = 0, Weight = 1.5f, From = 3 },  // a crescent that comes back (Crescents)
                    new FactionMember { Type = EnemyType.Gnomon, Fade = 2, Weight = 1.5f, From = 2 },  // hide in its shadow (Eclipse flash)
                    new FactionMember { Type = EnemyType.Nova, Fade = 2, Weight = 1f, From = 3 },  // a fuse, then a nova (Totality)
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Dawn", From = 1, Members = new[] { EnemyType.CoronaPriest, EnemyType.Ember, EnemyType.Ember, EnemyType.Ember, EnemyType.Ember, EnemyType.Ember, EnemyType.Ember } },
                    new SquadDef { Name = "Mirror pair", From = 2, Members = new[] { EnemyType.Heliograph, EnemyType.Heliograph } },
                    new SquadDef { Name = "Moon court", From = 3, Members = new[] { EnemyType.Moonward, EnemyType.Moonward, EnemyType.Sickle, EnemyType.Sickle } },
                    new SquadDef { Name = "Night watch", From = 3, Members = new[] { EnemyType.DuskLobber, EnemyType.Gnomon, EnemyType.Ember, EnemyType.Ember, EnemyType.Ember } },
                },
                // No set piece or lesson cycle yet (steps 2-4): the Campaign builds its waves by reach, as for the Lich.
            },
        };

        static FactionMember M(EnemyType t, float weight) => new FactionMember { Type = t, Weight = weight };

        /// The faction of a boss, or null if it has none yet.
        public static FactionDef For(EnemyType boss) => Array.Find(All, f => f.Boss == boss);

        /// The fading boss's share of the five waves of a cycle (Docs/Campaign.md), and the older pool's from wave 11 on.
        public static readonly float[] FadeShare = { 0.35f, 0.25f, 0.15f, 0.05f, 0f };
        public static readonly float[] LongFade = { 0.40f, 0.20f, 0.08f };  // the Long Campaign's fade, by shown wave after a boss: gone after three (FactionMember.Fade says who stays)

        /// The fading faction's share of track wave `n`; `shown` is the Long Campaign's wave number (0: the plain track).
        public static float FadeAt(int n, int shown)
        {
            int cycle = (n - 1) / 5, p = (n - 1) % 5;
            if (cycle < 1)
                return 0f;
            if (shown <= 0)
                return FadeShare[p];
            int i = (shown - 1) % 10;
            return i < LongFade.Length ? LongFade[i] : 0f;
        }
        public const float PoolShare = 0.10f;

        /// One faction's slice of a campaign wave.
        public struct Slice
        {
            public FactionDef Faction;
            public float Share;
            public bool Current;  // the faction of the boss this wave leads to
            public float Pool;    // the part of Share that is the older pool (a comeback squad takes it over, Comeback)
        }

        /// The campaign's split of regular wave `n` between factions (Docs/Campaign.md): the last boss's faction fading
        /// out, the older bosses' as a pool, the next boss's taking the rest. Only factions that are built appear, so
        /// the shares may add up to less than 1 (the rest is the usual random mix). Depends on the wave alone.
        public static List<Slice> Campaign(int n, int shown = 0)
        {
            var list = new List<Slice>();
            if (n < 1)
                return list;
            int cycle = (n - 1) / 5;
            float fade = FadeAt(n, shown);
            float pool = cycle >= 2 ? PoolShare : 0f;
            Action<EnemyType, float, bool, float> add = (boss, share, cur, poolPart) =>
            {
                var f = For(boss);
                if (f == null || share <= 0f)
                    return;
                int i = list.FindIndex(x => x.Faction == f);
                if (i >= 0)
                    list[i] = new Slice { Faction = f, Share = list[i].Share + share, Current = list[i].Current || cur, Pool = list[i].Pool + poolPart };
                else
                    list.Add(new Slice { Faction = f, Share = share, Current = cur, Pool = poolPart });
            };
            var order = BossSchedule.Campaign;  // (the Campaign's own faction mix)
            add(order.BossAt(5 * (cycle + 1)), 1f - fade - pool, true, 0f);
            if (cycle >= 1)
                add(order.BossAt(5 * cycle), fade, false, 0f);
            var older = new List<EnemyType>();
            for (int j = 1; j < cycle; j++)
                if (For(order.BossAt(5 * j)) != null)
                    older.Add(order.BossAt(5 * j));
            foreach (var b in older)
                add(b, pool / older.Count, false, pool / older.Count);
            return list;
        }

        /// The comeback squad of wave `n` (Docs/Campaign.md), or null: one wave in every five from wave 11 (where the older
        /// pool starts), never the last wave of a cycle (the approach stays clean). One squad from an earlier built
        /// faction, picked from the wave number alone so every game agrees. It takes over the wave's older-pool slice.
        public static SquadDef Comeback(int n, out FactionDef from)
        {
            from = null;
            int cycle = (n - 1) / 5, p = (n - 1) % 5;
            if (n < 11 || p != (cycle * 3 + 1) % 4)  // one wave in every five, never the last of the cycle, its step moving about
                return null;
            uint h = (uint)n * 2654435761u;
            var pool = Campaign(n).FindAll(x => x.Pool > 0f);
            pool.RemoveAll(x => x.Faction.Squads.Length == 0);
            if (pool.Count == 0)
                return null;
            from = pool[(int)((h >> 12) % (uint)pool.Count)].Faction;
            return from.Squads[(int)((h >> 16) % (uint)from.Squads.Length)];
        }

        /// A comeback squad as it turns up in a late wave: its members twice over and the faction's toughest enemy as an anchor,
        /// so an old squad is a real event (a Breakout of 12 Rioters and a Guard, with a Riot Shield at the back). Docs/Approaches.md.
        public static SquadDef Callback(SquadDef sq, FactionDef from)
        {
            var members = new List<EnemyType>(sq.Members);
            members.AddRange(sq.Members);
            EnemyType anchor = sq.Members[0];
            float hp = 0f;
            foreach (var m in from.Members)
                if (Defs.Get(m.Type).Hp > hp) { hp = Defs.Get(m.Type).Hp; anchor = m.Type; }
            members.Add(anchor);
            return new SquadDef { Name = sq.Name, From = sq.From, Form = sq.Form, Members = members.ToArray() };
        }

        /// Is this type one of a faction's (for the Bestiary and the Practice Range)?
        public static FactionDef Of(EnemyType t) => Array.Find(All, f => Array.Exists(f.Members, m => m.Type == t));
    }

    public partial class Game
    {
        /// XP for killing a faction enemy pays like a regular enemy of its health (Defs: Rioter 7, Guard 15, Riot Shield 10,
        /// Gas Grenadier 9), so a themed approach wave, much tougher than a regular one, doesn't leave you levels behind
        /// at the boss. The Warden's own riot on its boss wave sends dozens of Rioters and Guards, so those keep the old
        /// fodder XP there (RioterFodderXp, GuardFodderXp).
        public const float RioterFodderXp = 2f, GuardFodderXp = 4f;

        /// The XP a kill gives everyone, before elites, paths and modifiers (Combat.cs).
        float KillXp(Enemy e)
        {
            if (BossNow && e.Type == EnemyType.Rioter)
                return RioterFodderXp;
            if (BossNow && e.Type == EnemyType.Guard)
                return GuardFodderXp;
            return e.Def.Xp;
        }

        /// The themed share of this wave (0 to 1): 0 on boss waves, for bosses without a faction, or when it's turned off.
        public float ApproachShare(int wave)
        {
            if (Hunt == null && BossSlot && wave == Wave)
                return 0f;  // the boss fight itself has no approach
            int step = ApproachStepAt(wave);
            float share = D(DiffKnob.ApproachShare);
            if (step == 0 || (!(Hunt != null || Quick) && Approaches.For(ApproachBossAt(wave)) == null) || Practice != null)
                return 0f;
            if (Hunt != null || Quick)
                return 1f;  // a Boss Hunt's (and Quick Play's) build-up waves are only the boss's own enemies (Hunts.cs, QuickPlay.cs)
            if (CampaignOn)
                return Approaches.Campaign(wave, wave == Wave && LongLegs ? ShownWave : 0).Find(x => x.Current).Share;  // the next boss's slice (0 if it has no faction)
            if (share <= 0f)
                return 0f;
            return D(DiffKnob.ApproachRamp) >= 0.5f ? share * RampSteps[step - 1] : share;
        }

        /// Is the campaign buildup on (every wave themed, Approaches.Campaign)? Not in a hunt or the Practice Range.
        public bool CampaignOn => Hunt == null && !Quick && Practice == null && D(DiffKnob.Campaign) >= 0.5f;

        /// With the ramp on, each approach wave's share of the full share: from 40% up to all of it.
        static readonly float[] RampSteps = { 0.4f, 0.6f, 0.8f, 1f };

        /// Host, BuildWave: takes the themed share off `count` and picks the faction's enemies for it (squads where they
        /// fit, singles for the rest). They're mixed into the wave by MixApproach once the rest is built. Uses no random
        /// rolls when the share is 0, so those waves come out exactly as before.
        void BuildApproach(int n, ref int count)
        {
            plan.ApproachSingles.Clear();
            plan.ApproachSquads.Clear();
            plan.ApproachTrickle.Clear();
            int of = OnPath && CurrentPath == PathId.EliteGauntlet ? count / 2 : count;  // Elite Gauntlet halves the whole wave
            if (CampaignOn)
            {
                if (ApproachStepAt(n) == 0)
                    return;
                int step = ApproachStepAt(n);
                FactionDef cbFrom;
                var cb = Approaches.Comeback(n, out cbFrom);  // (a comeback squad takes the older pool's slice this wave)
                int shown = LongLegs ? ShownWave : 0;
                if (cb != null)  // (first, so its types count against the wave's budget of distinct types before the factions pick theirs)
                {
                    var big = Approaches.Callback(cb, cbFrom);  // (a callback: the old squad, big)
                    plan.ApproachSquads.Add(big);
                    foreach (var t in big.Members)
                        plan.Kinds.Add(t);
                    count -= Math.Min(count, big.Members.Length);
                }
                foreach (var sl in Approaches.Campaign(n, shown))  // (every faction's enemies, each by its slice; the rest stays the usual mix)
                {
                    int themed = Math.Min(count, (int)Math.Round(of * (cb != null ? sl.Share - sl.Pool : sl.Share)));
                    if (themed > 0 && sl.Current && LessonNow(sl.Faction) != null)
                        PickLesson(sl.Faction, LessonNow(sl.Faction), ref count, themed);  // a Campaign's teaching cycle: this wave's lesson
                    else if (themed > 0 && shown > 0 && !sl.Current && sl.Pool <= 0f)
                    {
                        // The last boss's faction fading out (Long Campaign): singles only, the tricky ones first to go (FactionMember.Fade).
                        int stay = (shown - 1) % 10 + 1;
                        var left = Array.FindAll(sl.Faction.Members, m => m.Fade >= stay);
                        if (left.Length > 0)
                            PickFrom(left, new SquadDef[0], null, 0f, ref count, themed);
                    }
                    else if (themed > 0)
                        PickFaction(sl.Faction, sl.Current ? step : 99, sl.Current && Bosses.FinaleWave(n), ref count, themed);  // (older factions are in full; only the next boss's builds up)
                }
                return;
            }
            float share = ApproachShare(n);
            if (share <= 0f)
                return;
            var f = Hunt != null || Quick ? FactionFor(ApproachBossAt(n)) : Approaches.For(ApproachBossAt(n));  // (Quick Play, a hunt: a boss with no faction yet brings the basic one, BasicFaction.cs)
            int themed1 = Math.Min(count, (int)Math.Round(of * share));
            if (themed1 <= 0)
                return;
            int reach = ApproachStepAt(n);  // (a hunt's nine waves reach through the faction as a run's four do)
            if (LessonNow(f) != null)
            {
                PickLesson(f, LessonNow(f), ref count, themed1);
                return;
            }
            PickFaction(f, reach, Bosses.FinaleWave(n), ref count, themed1);
        }

        /// Takes `themed` enemies off `count` and picks them from a faction: squads where they fit, singles for the rest.
        /// `reach` is how far into the faction's members and squads the wave goes (their From); the finale comes in when
        /// asked for, whatever the share.
        void PickFaction(FactionDef f, int reach, bool withFinale, ref int count, int themed)
        {
            bool first = Wave <= 1;  // the first round: no NotFirst members, nor squads holding one
            var members = Array.FindAll(f.Members, m => m.From <= reach && !(first && m.NotFirst));
            var squads = Array.FindAll(f.Squads, s => s.From <= reach && !(first && Array.Exists(s.Members, t => Array.Exists(f.Members, m => m.NotFirst && m.Type == t))));
            PickFrom(members, squads,
                withFinale && f.Finale != null && f.Finale.From <= reach ? f.Finale : null, 0.5f, ref count, themed);
        }

        static SquadDef[] Concat(SquadDef[] a, SquadDef[] b)
        {
            var r = new SquadDef[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        /// The lesson of the wave being built, in a Campaign wave that leads to this faction's boss (null: none).
        public WaveLesson LessonNow(FactionDef f)
        {
            if (!LongLegs || f == null || f.Lessons == null || f.Lessons.Length == 0 || ShownWave < 1)
                return null;
            return f.Lessons[Math.Min(f.Lessons.Length - 1, (ShownWave - 1) % 10)];  // (a cycle is ten Campaign waves)
        }

        /// Takes `themed` enemies off `count` from a lesson's members and squads (Approaches.cs WaveLesson).
        void PickLesson(FactionDef f, WaveLesson l, ref int count, int themed)
        {
            var members = l.Members ?? f.Members;
            var squads = l.Squads == null ? f.Squads : Array.FindAll(f.Squads, s => Array.IndexOf(l.Squads, s.Name) >= 0);
            if (l.Squads != null)
                squads = Concat(squads, Array.FindAll(f.LessonSquads, s => Array.IndexOf(l.Squads, s.Name) >= 0));
            if (l.Also != null)
            {
                var also = Array.Find(f.Squads, s => s.Name == l.Also);
                if (also != null)
                {
                    var big = Approaches.Callback(also, f);
                    plan.ApproachSquads.Add(big);
                    foreach (var t in big.Members)
                        plan.Kinds.Add(t);
                    count -= Math.Min(count, big.Members.Length);
                }
            }
            int stars = 0;
            if (l.Star.HasValue)
                for (; stars < WaveLesson.StarPicks && stars < themed; stars++)
                    (l.Trickle ? plan.ApproachTrickle : plan.ApproachSingles).Add(l.Star.Value);  // the wave's lesson always shows up (a Trickle one only late in the wave)
            if (stars > 0)
                plan.Kinds.Add(l.Star.Value);
            count -= stars;
            PickFrom(members, squads, l.Finale ? f.Finale : null, l.SquadChance, ref count, themed - stars);
        }

        /// A boss faction may bring in this type (already in the wave, or a new type within its share of the type budget).
        bool ApproachKindOk(EnemyType t) => plan.Kinds.Count < plan.ApproachKindCap || plan.Kinds.Contains(t);

        /// ...and these squad members, all of them.
        bool ApproachKindsFit(EnemyType[] members)
        {
            int extra = 0;
            for (int i = 0; i < members.Length; i++)
                if (!plan.Kinds.Contains(members[i]) && Array.IndexOf(members, members[i]) == i)
                    extra++;
            return extra == 0 || plan.Kinds.Count + extra <= plan.ApproachKindCap;
        }

        /// Takes `themed` enemies off `count`: the finale if given (whatever the share), then squads (about `squadChance`
        /// of picks, where one fits) and weighted singles for the rest.
        void PickFrom(FactionMember[] members, SquadDef[] squads, SquadDef finale, float squadChance, ref int count, int themed)
        {
            int spent = 0;
            if (finale != null)
            {
                themed = Math.Max(themed, Math.Min(count, finale.Members.Length));  // the set piece comes whatever the share
                plan.ApproachSquads.Add(finale);
                spent = finale.Members.Length;
                foreach (var t in finale.Members)
                    plan.Kinds.Add(t);
            }
            count -= themed;
            while (spent < themed)
            {
                // About half of it as squads, if one fits in what's left (and in the wave's type budget).
                var fits = Array.FindAll(squads, s => s.Members.Length <= themed - spent && plan.ApproachSquads.FindAll(a => a == s).Count < 2 && ApproachKindsFit(s.Members));  // (none more than twice)
                if (fits.Length > 0 && rng.NextDouble() < squadChance)
                {
                    var s = fits[rng.Next(fits.Length)];
                    plan.ApproachSquads.Add(s);
                    spent += s.Members.Length;
                    foreach (var t in s.Members)
                        plan.Kinds.Add(t);
                    continue;
                }
                float total = 0f;
                foreach (var m in members)
                    if (ApproachKindOk(m.Type))
                        total += m.Weight;
                if (total <= 0f)
                {
                    count += themed - spent;  // the type budget is spent: the rest of the share goes back to the usual mix
                    break;
                }
                float r = (float)rng.NextDouble() * total;
                FactionMember pick = null;
                foreach (var m in members)
                {
                    if (!ApproachKindOk(m.Type))
                        continue;
                    pick = m;
                    r -= m.Weight;
                    if (r <= 0f)
                        break;
                }
                plan.Kinds.Add(pick.Type);
                plan.ApproachSingles.Add(pick.Type);
                int pack = pick.Type == EnemyType.Rioter ? 2 : pick.Type == EnemyType.Runner || pick.Type == EnemyType.Wisp ? 3 : 1;  // rioters come in pairs, runners and wisps in threes (a Bound Wraith brings its own partner)
                for (int k = 1; k < pack; k++)
                    plan.ApproachSingles.Add(pick.Type);
                spent += pack;
            }
        }

        /// Mixes the approach's enemies in with the rest of the wave: singles at random places in the spawn queue,
        /// squads among the others.
        void MixApproach()
        {
            foreach (var t in plan.ApproachSingles)
                spawnQueue.Insert(rng.Next(spawnQueue.Count + 1), t);
            foreach (var s in plan.ApproachSquads)
                squadQueue.Insert(rng.Next(squadQueue.Count + 1), s);
            // The trickle: the new enemy comes last, after the wave's squads (SpawnNextSquad), alone, one at a time.
            spawnQueue.AddRange(plan.ApproachTrickle);
            stragglers = plan.ApproachTrickle.Count;
            plan.ApproachSingles.Clear();
            plan.ApproachSquads.Clear();
            plan.ApproachTrickle.Clear();
        }
    }
}
