using System;
using System.Collections.Generic;

namespace Arena
{
    /// Every track the music player can play: the menu theme, the wave battle track, the ten boss themes and a set of
    /// extra tracks for different moods. Plain C# (no Unity), so SimTest covers it. Each track is synthesised from a
    /// GameMusic recipe the first time it is asked for (SoundBank.cs); Library only says what exists.
    public static class MusicLibrary
    {
        public class Entry
        {
            public string Id, Title, Mood, Blurb;
            public float Gain;
            public Func<float[]> Render;
            public string Path;       // custom tracks: the file
            public object Clip;       // an AudioClip assigned in Unity (MusicPack.cs, Assets/Resources/Music)
            public bool NeedsUnity;    // custom mp3 / ogg: Unity decodes it (MusicPlayerUi.cs)
            public EnemyType? Boss;   // set for boss themes (their id is SoundBank.BossTrackId)
        }

        public const string Menu = "menu", Battle = "battle";

        /// Ids that are rendered on demand and let go again when nothing needs them (a few MB each).
        public static bool IsTransient(string id) => id != null && (id.StartsWith("boss:") || id.StartsWith("juke:") || id.StartsWith(CustomMusic.IdPrefix));

        public static readonly string[] Moods = { "Chill", "Action", "Dark", "Epic", "Boss", "Custom" };

        static Entry Extra(string id, string title, string mood, string blurb, GameMusic.Recipe r) =>
            new Entry { Id = "juke:" + id, Title = title, Mood = mood, Blurb = blurb, Gain = 0.5f, Render = () => GameMusic.Render(r) };

        static Entry BossEntry(EnemyType boss, string title, string blurb) =>
            new Entry { Id = "boss:" + boss, Title = title, Mood = "Boss", Blurb = blurb, Gain = 0.55f, Boss = boss, Render = () => GameMusic.Render(GameMusic.ForBoss(boss)) };

        // --- The extra tracks (one recipe each; see GameMusic.Recipe for what the knobs do) -----------------------------

        public static readonly GameMusic.Recipe Lofi = new GameMusic.Recipe
        {
            Bpm = 74f, Chords = new[] { "Em", "C", "G", "D" }, Passes = 4, Gain = 0.9f,
            Pad = 0.05f, PadCutoff = 800f, BassStyle = GameMusic.Bass.Offbeat, BassLevel = 0.2f,
            ArpRate = 8, ArpTone = GameMusic.Tone.Pluck, ArpLevel = 0.06f,
            Kick = "x.....x...x.....", Snare = "....x.......x...", Hat = "..o...o...o...o.", DrumLevel = 0.55f,
            Lead = true, LeadTone = GameMusic.Tone.Bell, LeadSeed = 61, LeadLevel = 0.05f, Reverb = 0.6f,
        };

        public static readonly GameMusic.Recipe Neon = new GameMusic.Recipe
        {
            Bpm = 108f, Chords = new[] { "Fm", "Db", "Ab", "Eb" }, Passes = 4,
            Pad = 0.05f, PadCutoff = 1500f, PadWobble = 0.25f, BassStyle = GameMusic.Bass.Eighths, BassLevel = 0.26f,
            ArpRate = 16, ArpTone = GameMusic.Tone.Square, ArpLevel = 0.06f, ArpFromSecondPass = true,
            Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "..o...o...o...o.",
            Lead = true, LeadTone = GameMusic.Tone.Square, LeadSeed = 67, LeadLevel = 0.06f,
        };

        public static readonly GameMusic.Recipe Haunted = new GameMusic.Recipe
        {
            Bpm = 66f, Chords = new[] { "Cm", "Ab", "Bdim", "G" }, Passes = 3,
            Pad = 0.06f, PadCutoff = 900f, PadWobble = 0.3f, Choir = 0.03f, BassStyle = GameMusic.Bass.Whole, BassLevel = 0.24f,
            ArpRate = 8, ArpTone = GameMusic.Tone.Bell, ArpLevel = 0.05f, ArpFromSecondPass = true,
            Lead = true, LeadTone = GameMusic.Tone.Bell, LeadSeed = 71, LeadLevel = 0.06f, Reverb = 0.95f,
        };

        public static readonly GameMusic.Recipe Crystal = new GameMusic.Recipe
        {
            Bpm = 60f, Chords = new[] { "Dsus2", "Bm", "Gsus2", "A" }, Passes = 3,
            Pad = 0.055f, PadCutoff = 1100f, PadWobble = 0.2f, Choir = 0.03f, BassStyle = GameMusic.Bass.Whole, BassLevel = 0.15f,
            ArpRate = 8, ArpTone = GameMusic.Tone.Bell, ArpLevel = 0.07f, ArpCallResponse = true,
            Lead = true, LeadTone = GameMusic.Tone.Bell, LeadSeed = 73, LeadLevel = 0.05f, Reverb = 0.9f,
        };

        public static readonly GameMusic.Recipe Iron = new GameMusic.Recipe
        {
            Bpm = 118f, Chords = new[] { "E5", "G5", "D5", "C5" }, Passes = 4,
            PowerChords = true, BassStyle = GameMusic.Bass.Sixteenths, BassLevel = 0.24f, BassDrive = 0.9f, Pad = 0.025f, PadCutoff = 900f,
            Kick = "x..x..x...x.x...", Snare = "....x.......x...", Hat = "oooooooooooooooo", DrumLevel = 1.1f, Toms = true,
            Reverb = 0.35f,
        };

        public static readonly GameMusic.Recipe Sunrise = new GameMusic.Recipe
        {
            Bpm = 126f, Chords = new[] { "D", "A", "Bm", "G" }, Passes = 4, Gain = 0.95f,
            Pad = 0.045f, PadCutoff = 1800f, BassStyle = GameMusic.Bass.Eighths, BassLevel = 0.22f,
            ArpRate = 8, ArpTone = GameMusic.Tone.Pluck, ArpLevel = 0.08f,
            Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "o.o.o.o.o.o.o.o.", DrumLevel = 0.8f,
            Lead = true, LeadTone = GameMusic.Tone.Pluck, LeadSeed = 79, LeadLevel = 0.08f, Reverb = 0.45f,
        };

        public static readonly GameMusic.Recipe Dust = new GameMusic.Recipe
        {
            Bpm = 96f, Chords = new[] { "Am", "Bb", "Am", "G" }, Passes = 4,
            Pad = 0.04f, PadCutoff = 1000f, BassStyle = GameMusic.Bass.Offbeat, BassLevel = 0.26f,
            ArpRate = 8, ArpTone = GameMusic.Tone.Harpsichord, ArpLevel = 0.08f, ArpCallResponse = true,
            Kick = "x.......x.x.....", Snare = "........x.......", Hat = "o.o.o.o.o.o.o.o.", DrumLevel = 0.75f, Toms = true,
            Lead = true, LeadTone = GameMusic.Tone.Square, LeadSeed = 83, LeadLevel = 0.06f, Reverb = 0.4f,
        };

        public static readonly GameMusic.Recipe Frost = new GameMusic.Recipe
        {
            Bpm = 82f, Chords = new[] { "Gm", "Eb", "Cm", "D" }, Passes = 3,
            Pad = 0.06f, PadCutoff = 1300f, PadWobble = 0.4f, PadOctave = -12, Choir = 0.025f, BassStyle = GameMusic.Bass.Whole, BassLevel = 0.22f,
            ArpRate = 16, ArpTone = GameMusic.Tone.Bell, ArpLevel = 0.045f, ArpFromSecondPass = true,
            Kick = "x...............", Hat = "....o.......o...", DrumLevel = 0.6f, Reverb = 0.85f,
            Lead = true, LeadTone = GameMusic.Tone.Bell, LeadSeed = 89, LeadLevel = 0.05f,
        };

        public static readonly GameMusic.Recipe LastStand = new GameMusic.Recipe
        {
            Bpm = 104f, Chords = new[] { "Dm", "Bb", "Gm", "A", "Dm", "F", "C", "A" }, Passes = 2,
            Pad = 0.06f, PadCutoff = 1500f, Choir = 0.035f, PowerChords = true,
            BassStyle = GameMusic.Bass.Eighths, BassLevel = 0.26f, BassDrive = 0.3f,
            ArpRate = 16, ArpTone = GameMusic.Tone.Pluck, ArpLevel = 0.05f, ArpFromSecondPass = true,
            Kick = "x..x..x...x..x..", Snare = "....x.......x...", Hat = "o.o.o.o.o.o.o.o.", Toms = true, DrumLevel = 1.1f,
            Lead = true, LeadTone = GameMusic.Tone.Square, LeadSeed = 97, LeadLevel = 0.08f, Reverb = 0.6f,
        };

        public static readonly GameMusic.Recipe Arcade = new GameMusic.Recipe
        {
            Bpm = 140f, Chords = new[] { "Cm", "Gm", "Ab", "Bb" }, Passes = 4,
            Pad = 0.02f, PadCutoff = 2400f, BassStyle = GameMusic.Bass.Sixteenths, BassLevel = 0.2f,
            ArpRate = 16, ArpTone = GameMusic.Tone.Square, ArpLevel = 0.075f,
            Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "o.o.o.o.o.o.o.o.", DrumLevel = 0.7f,
            Lead = true, LeadTone = GameMusic.Tone.Square, LeadSeed = 101, LeadLevel = 0.07f, Reverb = 0.25f,
        };

        /// The list the music player shows, in order: calm tracks first, boss themes last.
        public static readonly List<Entry> All = Build();

        static List<Entry> Build()
        {
            return new List<Entry>
            {
                new Entry { Id = Menu, Title = "Main Menu", Mood = "Chill", Blurb = "Slow synthwave in A minor", Gain = 0.6f, Render = MenuMusic.Render },
                Extra("lofi", "Lo-fi Lounge", "Chill", "Dusty beat, soft bells, nowhere to be", Lofi),
                Extra("crystal", "Crystal Caves", "Chill", "Slow ambient bells, no drums", Crystal),
                Extra("frost", "Deep Freeze", "Chill", "Cold, wide and quiet", Frost),
                new Entry { Id = Battle, Title = "Wave Battle", Mood = "Action", Blurb = "The soft track under normal waves", Gain = 0.42f, Render = () => GameMusic.Render(GameMusic.Battle) },
                Extra("neon", "Neon Highway", "Action", "Four on the floor, night drive synths", Neon),
                Extra("sunrise", "Sunrise Run", "Action", "Bright, major and bouncy", Sunrise),
                Extra("arcade", "Arcade Heist", "Action", "Fast chiptune squares", Arcade),
                Extra("dust", "Dust Road", "Action", "Desert groove with a dark turn", Dust),
                Extra("haunted", "Haunted Hollow", "Dark", "Slow, eerie bells and choir", Haunted),
                Extra("iron", "Iron Rain", "Dark", "Industrial grind, distorted fifths", Iron),
                Extra("laststand", "Last Stand", "Epic", "Big drums, choir, one more wave", LastStand),
                BossEntry(EnemyType.Boss, "The Warden", "Driving and martial"),
                BossEntry(EnemyType.HiveQueen, "Hive Queen", "Buzzing and insectile"),
                BossEntry(EnemyType.TwinA, "The Twin Reapers", "Two voices trading phrases"),
                BossEntry(EnemyType.Singularity, "The Singularity", "Half-time, deep and warping"),
                BossEntry(EnemyType.Colossus, "The Colossus", "Slow and enormous"),
                BossEntry(EnemyType.Duelist, "The Duelist", "Quick, precise, baroque"),
                BossEntry(EnemyType.Lich, "The Lich", "Eerie organ, a slow heartbeat"),
                BossEntry(EnemyType.StormHerald, "Storm Herald", "Electric, with thunder"),
                BossEntry(EnemyType.Devourer, "The Devourer", "Grinding and hungry"),
                BossEntry(EnemyType.Eclipse, "The Eclipse", "The finale: big, choral, driving"),
            };
        }

        /// Swap in the tracks found in the music folder (after the built-in ones).
        public static void SetCustom(List<Entry> entries)
        {
            All.RemoveAll(e => e.Id.StartsWith(CustomMusic.IdPrefix));
            All.AddRange(entries);
        }

        public const string ClipPrefix = "clip:";

        /// Swap in the clips found in the Unity project (Assets/Resources/Music and the Music Pack's Tracks).
        public static void SetProject(List<Entry> entries)
        {
            All.RemoveAll(e => e.Id.StartsWith(ClipPrefix));
            int at = All.FindIndex(e => e.Id.StartsWith(CustomMusic.IdPrefix));
            All.InsertRange(at < 0 ? All.Count : at, entries);   // (before any tracks from the music folder)
        }

        public static Entry Find(string id)
        {
            foreach (var e in All)
                if (e.Id == id)
                    return e;
            return null;
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id)
                    return i;
            return -1;
        }
    }

    /// What the music player has chosen: a track that plays everywhere in place of the automatic music (null =
    /// automatic), plus shuffle and repeat. Pure logic; the UI (MusicPlayerUi.cs) and SoundBank do the rest.
    public class Jukebox
    {
        public string Selected;            // a MusicLibrary id, or null for "Automatic: the game picks"
        public bool Shuffle;
        public bool RepeatOne;             // false: go on to the next track when one ends
        public bool BossThemes = true;     // a boss fight still plays its own theme

        readonly List<string> history = new List<string>();
        readonly Random rng;

        public Jukebox(int seed = 5) { rng = new Random(seed); }

        public void Select(string id, bool remember = true)
        {
            if (remember && Selected != null && Selected != id)
                history.Add(Selected);
            if (history.Count > 32)
                history.RemoveAt(0);
            Selected = MusicLibrary.IndexOf(id) >= 0 ? id : null;
        }

        /// The track after the current one: random with shuffle (never the same one twice in a row), else the next in the list.
        public string PickNext()
        {
            int n = MusicLibrary.All.Count, cur = MusicLibrary.IndexOf(Selected);
            if (Shuffle && n > 1)
            {
                int i;
                do i = rng.Next(n); while (i == cur);
                return MusicLibrary.All[i].Id;
            }
            return MusicLibrary.All[(cur + 1) % n].Id;
        }

        public void Next() => Select(PickNext());

        public void Previous()
        {
            if (history.Count > 0)
            {
                string id = history[history.Count - 1];
                history.RemoveAt(history.Count - 1);
                Select(id, false);
                return;
            }
            int n = MusicLibrary.All.Count, cur = MusicLibrary.IndexOf(Selected);
            Select(MusicLibrary.All[(cur <= 0 ? n : cur) - 1].Id, false);
        }

        /// The track that will follow this one, rendered ahead of time (null when it repeats or nothing is chosen).
        public string Upcoming => Selected == null || RepeatOne || Shuffle ? null : MusicLibrary.All[(MusicLibrary.IndexOf(Selected) + 1) % MusicLibrary.All.Count].Id;
    }
}
