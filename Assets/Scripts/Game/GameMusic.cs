using System;
using System.Collections.Generic;

namespace Arena
{
    /// In-match music, synthesized in code like the menu theme: a soft track for regular waves and
    /// a unique track for each boss. Each track is a recipe (tempo, key, chords and which layers
    /// play); Render() turns one into mono samples that loop seamlessly. Plain C# (no Unity), so it
    /// runs on a background thread.
    public static class GameMusic
    {
        const int Rate = MenuMusic.Rate;

        public enum Bass { None, Whole, Eighths, Sixteenths, Offbeat, Pulse }
        public enum Tone { Pluck, Bell, Harpsichord, Square }

        /// One track. Chords are written like "Em", "C", "B5" (power chord), "Bdim", "F#m".
        public class Recipe
        {
            public float Bpm = 100f;
            public string[] Chords = { "Am", "F", "C", "G" };
            public int Passes = 2;               // times through the chords (the second pass adds layers)
            public int ChordBars = 1;            // bars per chord
            public float Gain = 1f;

            public float Pad = 0.045f, PadCutoff = 1100f, PadWobble = 0f;  // wobble: filter LFO rate (Hz)
            public int PadOctave = -12;
            public float Choir;                  // extra high, airy pad voices

            public Bass BassStyle = Bass.Eighths;
            public float BassLevel = 0.28f, BassDrive;  // drive: grit (distortion)
            public bool PowerChords;             // low distorted fifths on each chord

            public int ArpRate;                  // 0 = none, 8 = eighths, 16 = sixteenths
            public Tone ArpTone = Tone.Pluck;
            public float ArpLevel = 0.09f;
            public bool ArpCallResponse;         // alternate octaves bar by bar (two voices trading)
            public bool ArpFromSecondPass;

            // Drums, one character per 16th: x = hit, o = soft hit, . = rest.
            public string Kick = "", Snare = "", Hat = "";
            public float DrumLevel = 1f;
            public bool Toms, Thunder, Heartbeat;

            public bool Lead;                    // a generated melody
            public Tone LeadTone = Tone.Square;
            public float LeadLevel = 0.07f;
            public int LeadSeed = 1;

            public float Reverb = 0.5f;
        }

        // --- The tracks ----------------------------------------------------------

        /// Regular waves: soft and steady so it sits under the gunfire.
        public static readonly Recipe Battle = new Recipe
        {
            Bpm = 86f, Chords = new[] { "Dm", "Bb", "F", "C", "Dm", "Bb", "Gm", "A" }, Passes = 2, Gain = 0.9f,
            Pad = 0.05f, PadCutoff = 900f, BassStyle = Bass.Pulse, BassLevel = 0.2f,
            ArpRate = 8, ArpTone = Tone.Bell, ArpLevel = 0.06f,
            Kick = "x.......x.......", Hat = "..o...o...o...o.", DrumLevel = 0.6f, Reverb = 0.65f,
        };

        public static Recipe ForBoss(EnemyType boss)
        {
            switch (boss)
            {
                case EnemyType.Boss:  // The Warden: driving and martial
                    return new Recipe
                    {
                        Bpm = 124f, Chords = new[] { "Em", "C", "D", "B" }, Passes = 4,
                        BassStyle = Bass.Sixteenths, BassLevel = 0.24f, PadCutoff = 1400f,
                        ArpRate = 16, ArpLevel = 0.07f, ArpFromSecondPass = true,
                        Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "o.o.o.o.o.o.o.o.",
                        Lead = true, LeadTone = Tone.Square, LeadSeed = 5,
                    };
                case EnemyType.HiveQueen:  // buzzing, insectile
                    return new Recipe
                    {
                        Bpm = 136f, Chords = new[] { "F#m", "G", "F#m", "E" }, Passes = 4,
                        BassStyle = Bass.Eighths, BassDrive = 0.4f, PadCutoff = 1600f, PadWobble = 6f,
                        ArpRate = 16, ArpTone = Tone.Square, ArpLevel = 0.06f,
                        Kick = "x.....x...x.....", Snare = "....x.......x...", Hat = "oooooooooooooooo",
                        Lead = true, LeadTone = Tone.Pluck, LeadSeed = 9,
                    };
                case EnemyType.TwinA:  // the Twin Reapers: two voices trading phrases
                case EnemyType.TwinB:
                    return new Recipe
                    {
                        Bpm = 112f, Chords = new[] { "Cm", "Ab", "Eb", "Bb" }, Passes = 4,
                        BassStyle = Bass.Offbeat, PadCutoff = 1200f,
                        ArpRate = 8, ArpTone = Tone.Harpsichord, ArpLevel = 0.08f, ArpCallResponse = true,
                        Kick = "x.......x.x.....", Snare = "....x.......x...", Hat = "..o...o...o...o.",
                        Lead = true, LeadTone = Tone.Bell, LeadSeed = 13, LeadLevel = 0.06f,
                    };
                case EnemyType.Singularity:  // half-time, deep and warping
                    return new Recipe
                    {
                        Bpm = 88f, Chords = new[] { "Bbm", "Gb", "Db", "Ab" }, Passes = 2, ChordBars = 2,
                        Pad = 0.06f, PadCutoff = 700f, PadWobble = 0.5f, PadOctave = -24, Choir = 0.02f,
                        BassStyle = Bass.Whole, BassLevel = 0.32f,
                        ArpRate = 8, ArpTone = Tone.Bell, ArpLevel = 0.05f, ArpFromSecondPass = true,
                        Kick = "x...............", Snare = "........x.......", Hat = "o...o...o...o...", Reverb = 0.8f,
                    };
                case EnemyType.Colossus:  // slow and enormous
                    return new Recipe
                    {
                        Bpm = 78f, Chords = new[] { "D5", "Bb5", "C5", "A5" }, Passes = 3,
                        PowerChords = true, BassStyle = Bass.Whole, BassLevel = 0.3f, BassDrive = 0.5f, PadCutoff = 800f,
                        Kick = "x.......x.......", Snare = "........x.......", Toms = true, DrumLevel = 1.2f,
                        Lead = true, LeadTone = Tone.Square, LeadSeed = 21, LeadLevel = 0.05f,
                    };
                case EnemyType.Duelist:  // quick, precise, baroque
                    return new Recipe
                    {
                        Bpm = 144f, Chords = new[] { "Am", "E", "Am", "Dm", "G", "C", "Dm", "E" }, Passes = 2,
                        BassStyle = Bass.Eighths, BassLevel = 0.22f, Pad = 0.03f,
                        ArpRate = 16, ArpTone = Tone.Harpsichord, ArpLevel = 0.09f,
                        Kick = "x.......x.......", Snare = "....x.......x...", Hat = "o.o.o.o.o.o.o.o.", DrumLevel = 0.8f,
                    };
                case EnemyType.Lich:  // eerie organ, a slow heartbeat, bells
                    return new Recipe
                    {
                        Bpm = 92f, Chords = new[] { "Cm", "Ab", "Fm", "G" }, Passes = 3,
                        Pad = 0.06f, PadCutoff = 1000f, Choir = 0.03f, BassStyle = Bass.Whole, BassLevel = 0.25f,
                        ArpRate = 8, ArpTone = Tone.Square, ArpLevel = 0.045f,
                        Heartbeat = true, Lead = true, LeadTone = Tone.Bell, LeadSeed = 31, LeadLevel = 0.07f, Reverb = 0.85f,
                    };
                case EnemyType.StormHerald:  // electric, with thunder
                    return new Recipe
                    {
                        Bpm = 130f, Chords = new[] { "Gm", "Eb", "Bb", "F" }, Passes = 4,
                        BassStyle = Bass.Sixteenths, BassLevel = 0.22f, PadCutoff = 2000f,
                        ArpRate = 16, ArpTone = Tone.Square, ArpLevel = 0.07f,
                        Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "oxoxoxoxoxoxoxox", Thunder = true,
                        Lead = true, LeadTone = Tone.Pluck, LeadSeed = 41,
                    };
                case EnemyType.Devourer:  // grinding and hungry
                    return new Recipe
                    {
                        Bpm = 100f, Chords = new[] { "E5", "F5", "E5", "D5" }, Passes = 4,
                        PowerChords = true, BassStyle = Bass.Eighths, BassLevel = 0.3f, BassDrive = 0.8f, Pad = 0.03f, PadCutoff = 900f,
                        Kick = "x..x..x...x..x..", Snare = "....x.......x...", Hat = "o.o.o.o.o.o.o.o.", DrumLevel = 1.1f,
                    };
                case EnemyType.Eclipse:  // the finale: big, choral, driving
                    return new Recipe
                    {
                        Bpm = 116f, Chords = new[] { "Dm", "Bb", "F", "C", "Gm", "Bb", "A", "A" }, Passes = 2,
                        Pad = 0.06f, PadCutoff = 1500f, Choir = 0.035f,
                        BassStyle = Bass.Eighths, BassLevel = 0.26f,
                        ArpRate = 16, ArpTone = Tone.Pluck, ArpLevel = 0.06f, ArpFromSecondPass = true,
                        Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "o.o.o.o.o.o.o.o.", Toms = true,
                        Lead = true, LeadTone = Tone.Square, LeadSeed = 50, LeadLevel = 0.08f,
                    };
                default:
                    return Battle;
            }
        }

        // --- Rendering -------------------------------------------------------------

        static readonly string[] NoteNames = { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };

        /// MIDI notes for a chord name, around the octave below middle C.
        static int[] ChordNotes(string name)
        {
            int len = name.Length > 1 && (name[1] == '#' || name[1] == 'b') ? 2 : 1;
            string root = name.Substring(0, len), quality = name.Substring(len);
            int pc = Array.IndexOf(NoteNames, root);
            if (pc < 0)
                pc = Array.IndexOf(NoteNames, root == "Db" ? "C#" : root == "Gb" ? "F#" : root == "A#" ? "Bb" : root == "D#" ? "Eb" : "C");
            int r = 48 + pc;
            switch (quality)
            {
                case "m": return new[] { r, r + 3, r + 7 };
                case "5": return new[] { r, r + 7, r + 12 };
                case "dim": return new[] { r, r + 3, r + 6 };
                default: return new[] { r, r + 4, r + 7 };
            }
        }

        public static float[] Render(Recipe k)
        {
            float beat = 60f / k.Bpm, barLen = beat * 4f, step = beat / 4f;
            int bars = k.Chords.Length * k.ChordBars * k.Passes;
            int n = (int)(barLen * bars * Rate), tail = Rate * 3;
            var dry = new float[n + tail];
            var wet = new float[n + tail];
            var echo = new float[n + tail];
            var rng = new Random(k.LeadSeed * 7 + 3);
            var melody = MakeMelody(k, rng);

            for (int bar = 0; bar < bars; bar++)
            {
                float t0 = bar * barLen;
                int[] chord = ChordNotes(k.Chords[(bar / k.ChordBars) % k.Chords.Length]);
                bool firstBarOfChord = bar % k.ChordBars == 0;
                bool laterPass = bar >= bars / 2;

                // Pads (one voice per chord note, detuned pairs), plus an airy "choir" an octave up.
                if (firstBarOfChord && k.Pad > 0f)
                    foreach (int note in chord)
                        for (int d = -1; d <= 1; d += 2)
                            Pad(dry, wet, t0, barLen * k.ChordBars + 0.5f, MenuMusic.Hz(note + k.PadOctave + 12) * (1f + d * 0.003f), k.Pad, k.PadCutoff, k.PadWobble);
                if (firstBarOfChord && k.Choir > 0f)
                    foreach (int note in chord)
                        for (int d = -1; d <= 1; d += 2)
                            Pad(dry, wet, t0, barLen * k.ChordBars + 0.5f, MenuMusic.Hz(note + 12) * (1f + d * 0.006f), k.Choir, 2400f, 0.3f);
                if (k.PowerChords)
                    for (int h = 0; h < 2; h++)
                        Power(dry, t0 + h * barLen / 2f, barLen / 2f * 0.95f, MenuMusic.Hz(chord[0] - 12), 0.12f);

                // Bass.
                float root = MenuMusic.Hz(chord[0] - 12);
                switch (k.BassStyle)
                {
                    case Bass.Whole:
                        BassNote(dry, t0, barLen * 0.98f, root, k.BassLevel, k.BassDrive);
                        break;
                    case Bass.Eighths:
                        for (int e = 0; e < 8; e++)
                            BassNote(dry, t0 + e * beat / 2f, beat / 2f * 0.9f, root, e % 2 == 0 ? k.BassLevel : k.BassLevel * 0.7f, k.BassDrive);
                        break;
                    case Bass.Sixteenths:
                        for (int e = 0; e < 16; e++)
                            BassNote(dry, t0 + e * step, step * 0.85f, e % 8 == 6 ? root * 1.5f : root, e % 4 == 0 ? k.BassLevel : k.BassLevel * 0.65f, k.BassDrive);
                        break;
                    case Bass.Offbeat:
                        for (int e = 0; e < 4; e++)
                            BassNote(dry, t0 + e * beat + beat / 2f, beat / 2f * 0.9f, root, k.BassLevel, k.BassDrive);
                        break;
                    case Bass.Pulse:
                        for (int e = 0; e < 4; e++)
                            BassNote(dry, t0 + e * beat, beat * 0.9f, root, k.BassLevel, k.BassDrive);
                        break;
                }

                // Arpeggio.
                if (k.ArpRate > 0 && (!k.ArpFromSecondPass || laterPass))
                {
                    int notes = k.ArpRate, octave = k.ArpCallResponse && bar % 2 == 1 ? 24 : 12;
                    int[] pattern = { 0, 1, 2, 3, 4, 3, 2, 1 };
                    for (int s = 0; s < notes; s++)
                    {
                        int st = pattern[(s + bar) % pattern.Length];
                        int note = chord[st % 3] + 12 * (st / 3) + octave;
                        float vol = s % 4 == 0 ? k.ArpLevel : k.ArpLevel * 0.7f;
                        Voice(k.ArpTone, dry, echo, t0 + s * barLen / notes, barLen / notes * 1.6f, MenuMusic.Hz(note), vol);
                    }
                }

                // Lead melody: the same phrase each pass, with the later pass an octave up for lift.
                if (k.Lead && (laterPass || bars <= 8))
                    foreach (var m in melody)
                        if (m.Bar == bar % (k.Chords.Length * k.ChordBars))
                            Voice(k.LeadTone, dry, echo, t0 + m.Step * step, m.Steps * step * 0.9f, MenuMusic.Hz(m.Note + (laterPass && bars > 8 ? 12 : 0)), k.LeadLevel);

                // Drums.
                float dl = k.DrumLevel;
                for (int s = 0; s < 16; s++)
                {
                    float ts = t0 + s * step;
                    if (At(k.Kick, s) > 0f) MenuMusic.AddKick(dry, ts, 0.5f * dl * At(k.Kick, s));
                    if (At(k.Snare, s) > 0f) MenuMusic.AddClap(dry, wet, ts, 0.18f * dl * At(k.Snare, s), rng);
                    if (At(k.Hat, s) > 0f) MenuMusic.AddHat(dry, ts, 0.045f * dl * At(k.Hat, s), rng);
                }
                if (k.Toms && bar % 4 == 3)  // a fill into the next phrase
                    for (int s = 0; s < 4; s++)
                        Tom(dry, t0 + (12 + s) * step, 110f - s * 14f, 0.35f * dl);
                if (k.Thunder && bar % 4 == 0)
                    Thunder(dry, wet, t0, 0.12f, rng);
                if (k.Heartbeat)
                {
                    MenuMusic.AddKick(dry, t0, 0.45f);
                    MenuMusic.AddKick(dry, t0 + beat * 0.4f, 0.3f);
                }
            }

            // Echo on the arp and lead, reverb on pads and echoes.
            int delay = (int)(beat * 0.75f * Rate);
            for (int i = delay; i < echo.Length; i++)
                echo[i] += echo[i - delay] * 0.38f;
            var lp = new MenuMusic.OnePole(2600f);
            for (int i = 0; i < echo.Length; i++)
            {
                float e = lp.Process(echo[i]);
                dry[i] += e;
                wet[i] += e * 0.5f;
            }
            var verb = MenuMusic.Reverb(wet);
            for (int i = 0; i < dry.Length; i++)
                dry[i] += verb[i] * k.Reverb + wet[i] * 0.3f;

            // Fold the tail onto the start so it loops cleanly, soft-clip and normalize.
            var outp = new float[n];
            Array.Copy(dry, outp, n);
            for (int i = 0; i < tail; i++)
                outp[i] += dry[n + i];
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                outp[i] = (float)Math.Tanh(outp[i] * 1.2f);
                peak = Math.Max(peak, Math.Abs(outp[i]));
            }
            if (peak > 0f)
                for (int i = 0; i < n; i++)
                    outp[i] *= 0.8f * k.Gain / peak;
            return outp;
        }

        static float At(string pattern, int s) =>
            pattern == null || s >= pattern.Length ? 0f : pattern[s] == 'x' ? 1f : pattern[s] == 'o' ? 0.55f : 0f;

        struct MelodyNote { public int Bar, Step, Steps, Note; }

        /// A melody over one pass of the chords: chord tones on strong beats, neighbors between,
        /// with a seeded rhythm so each boss gets its own tune.
        static List<MelodyNote> MakeMelody(Recipe k, Random rng)
        {
            var notes = new List<MelodyNote>();
            int barsPerPass = k.Chords.Length * k.ChordBars;
            int[] rhythm = { 4, 2, 2, 4, 2, 2 };  // in 16ths
            var seeded = new Random(k.LeadSeed);
            int prev = -1;
            for (int bar = 0; bar < barsPerPass; bar++)
            {
                int[] chord = ChordNotes(k.Chords[(bar / k.ChordBars) % k.Chords.Length]);
                int s = 0, r = seeded.Next(rhythm.Length);
                while (s < 16)
                {
                    int len = Math.Min(16 - s, rhythm[r++ % rhythm.Length] * (seeded.NextDouble() < 0.25 ? 2 : 1));
                    int note;
                    if (s % 4 == 0 || prev < 0)
                        note = chord[seeded.Next(3)] + 24;
                    else
                        note = prev + (seeded.NextDouble() < 0.5 ? 2 : -2) * (seeded.NextDouble() < 0.3 ? 2 : 1);
                    if (seeded.NextDouble() > 0.2)  // leave some space
                        notes.Add(new MelodyNote { Bar = bar, Step = s, Steps = len, Note = note });
                    prev = note;
                    s += len;
                }
            }
            return notes;
        }

        // --- Instruments -----------------------------------------------------------

        static void Pad(float[] dry, float[] wet, float start, float dur, float hz, float vol, float cutoff, float wobble)
        {
            if (wobble <= 0f)
            {
                MenuMusic.AddSaw(dry, wet, start, dur, hz, vol, 0.5f, 0.8f, cutoff);
                return;
            }
            // A saw through a filter whose cutoff swings with an LFO.
            int i0 = (int)(start * Rate), len = (int)((dur + 0.8f) * Rate);
            double phase = (hz * 0.21) % 1.0;
            float y1 = 0f, y2 = 0f;
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                phase += hz / Rate;
                float x = (float)(2.0 * (phase - Math.Floor(phase)) - 1.0);
                float cut = cutoff * (0.55f + 0.45f * (float)Math.Sin(2.0 * Math.PI * wobble * (start + t)));
                float a = 1f - (float)Math.Exp(-2.0 * Math.PI * cut / Rate);
                y1 += a * (x - y1);
                y2 += a * (y1 - y2);
                float env = Math.Min(1f, t / 0.5f) * (t > dur ? Math.Max(0f, 1f - (t - dur) / 0.8f) : 1f);
                float y = y2 * env * vol;
                dry[i0 + k] += y * 0.6f;
                wet[i0 + k] += y * 0.8f;
            }
        }

        static void BassNote(float[] dry, float start, float dur, float hz, float vol, float drive)
        {
            if (drive <= 0f)
            {
                MenuMusic.AddBass(dry, start, dur, hz, vol);
                return;
            }
            int i0 = (int)(start * Rate), len = (int)(dur * Rate);
            double phase = 0;
            var lp = new MenuMusic.OnePole(700f);
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                phase += hz / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float x = 2f * p - 1f;
                x = (float)Math.Tanh(x * (1f + drive * 6f));  // grit
                float env = Math.Min(1f, t / 0.005f) * (float)Math.Exp(-t / (dur * 0.9f)) * Math.Min(1f, (dur - t) / 0.01f);
                dry[i0 + k] += lp.Process(x) * env * vol * 0.8f;
            }
        }

        static void Power(float[] dry, float start, float dur, float hz, float vol)
        {
            foreach (float f in new[] { hz, hz * 1.5f, hz * 2f })
                BassNote(dry, start, dur, f, vol, 0.7f);
        }

        static void Voice(Tone tone, float[] dry, float[] echo, float start, float dur, float hz, float vol)
        {
            switch (tone)
            {
                case Tone.Pluck:
                    MenuMusic.AddPluck(dry, echo, start, Math.Max(0.2f, dur), hz, vol);
                    return;
                case Tone.Bell:
                    Bell(dry, echo, start, hz, vol);
                    return;
                case Tone.Harpsichord:
                    Harpsichord(dry, echo, start, hz, vol);
                    return;
                default:
                    SquareLead(dry, echo, start, dur, hz, vol);
                    return;
            }
        }

        static void Bell(float[] dry, float[] echo, float start, float hz, float vol)
        {
            int i0 = (int)(start * Rate), len = (int)(1.2f * Rate);
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                double w = 2.0 * Math.PI * hz * t;
                // Slightly inharmonic partials give it the metallic ring.
                float x = (float)(Math.Sin(w) + 0.5 * Math.Sin(w * 2.76) * Math.Exp(-t / 0.3) + 0.25 * Math.Sin(w * 5.4) * Math.Exp(-t / 0.12));
                float y = x * Math.Min(1f, t / 0.002f) * (float)Math.Exp(-t / 0.45f) * vol * 0.6f;
                dry[i0 + k] += y;
                echo[i0 + k] += y * 0.6f;
            }
        }

        static void Harpsichord(float[] dry, float[] echo, float start, float hz, float vol)
        {
            int i0 = (int)(start * Rate), len = (int)(0.4f * Rate);
            double phase = 0;
            var lp = new MenuMusic.OnePole(5000f);
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                phase += hz / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float x = (2f * p - 1f) * 0.6f + (p < 0.3f ? 0.5f : -0.2f);  // bright and nasal
                float y = lp.Process(x) * Math.Min(1f, t / 0.001f) * (float)Math.Exp(-t / 0.12f) * vol;
                dry[i0 + k] += y;
                echo[i0 + k] += y * 0.5f;
            }
        }

        static void SquareLead(float[] dry, float[] echo, float start, float dur, float hz, float vol)
        {
            int i0 = (int)(start * Rate), len = (int)((dur + 0.08f) * Rate);
            double phase = 0;
            var lp = new MenuMusic.OnePole(2200f);
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                float vib = 1f + 0.004f * (float)Math.Sin(2.0 * Math.PI * 5.5 * t) * Math.Min(1f, t / 0.3f);
                phase += hz * vib / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float x = p < 0.5f ? 1f : -1f;
                float env = Math.Min(1f, t / 0.01f) * (t > dur ? Math.Max(0f, 1f - (t - dur) / 0.08f) : 1f) * (0.75f + 0.25f * (float)Math.Exp(-t / 0.1f));
                float y = lp.Process(x) * env * vol;
                dry[i0 + k] += y;
                echo[i0 + k] += y * 0.5f;
            }
        }

        static void Tom(float[] dry, float start, float hz, float vol)
        {
            int i0 = (int)(start * Rate), len = (int)(0.3f * Rate);
            double phase = 0;
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                phase += hz * (1f + 0.6f * (float)Math.Exp(-t / 0.04f)) / Rate;
                dry[i0 + k] += (float)Math.Sin(2.0 * Math.PI * phase) * (float)Math.Exp(-t / 0.15f) * vol;
            }
        }

        static void Thunder(float[] dry, float[] wet, float start, float vol, Random rng)
        {
            int i0 = (int)(start * Rate), len = (int)(2.5f * Rate);
            var lp = new MenuMusic.OnePole(500f);
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                float x = lp.Process((float)rng.NextDouble() * 2f - 1f);
                float env = Math.Min(1f, t / 0.02f) * (float)Math.Exp(-t / 0.8f) * (0.7f + 0.3f * (float)Math.Sin(t * 23f));
                dry[i0 + k] += x * env * vol * 2f;
                wet[i0 + k] += x * env * vol * 2f;
            }
        }
    }
}
