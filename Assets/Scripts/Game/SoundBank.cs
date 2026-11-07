using System;
using UnityEngine;

namespace Arena
{
    /// Sound effects synthesized at startup, so the project needs no audio files.
    /// Gunshots are built from layers (muzzle crack, body blast, low thump, room
    /// tail and a small reverb); tweak the recipes in Build() to change them.
    public class SoundBank
    {
        const int Rate = 44100;
        const int Variants = 3;  // slightly different takes of each gunshot, played in turn

        enum Osc { Sine, Square, Triangle, Saw }

        /// Recipe for one gunshot. Decays are in seconds, filter cutoffs in Hz.
        class Gun
        {
            public float Crack, CrackDecay, CrackHighpass = 2500f;
            public float Body, BodyDecay, BodyLowpass;
            public float Thump, ThumpFrom, ThumpTo, ThumpDecay;
            public float Tail, TailDecay, TailLowpass;
            public float Reverb, Duration, Peak;
        }

        readonly int count = Enum.GetValues(typeof(Sfx)).Length;
        readonly AudioClip[][] clips;
        readonly float[] minGap, lastPlayed;
        readonly int[] nextVariant;
        readonly AudioSource[] voices;
        readonly System.Random rng = new System.Random(7);
        int nextVoice;

        public bool Muted;

        // Volume settings (0..1), set from the pause menu.
        public float Master = 1f, Weapons = 1f, Enemies = 1f, Effects = 1f, Music = 0.7f;

        // --- Music ---------------------------------------------------------------
        // Tracks are synthesized on background threads (a second or few each) the first time
        // they're asked for, then turned into clips here on the main thread. Two sources
        // crossfade between tracks. Boss tracks are released again once they're not needed.
        class Track
        {
            public string Id;
            public float Gain;
            public volatile float[] Samples;
            public AudioClip Clip;
            public float Length;    // seconds
            public bool External;   // a clip from the Unity project: never destroyed or dropped
            public float ResumeAt;  // where to pick up next time (menu and battle tracks)
        }

        readonly System.Collections.Generic.Dictionary<string, Track> tracks = new System.Collections.Generic.Dictionary<string, Track>();
        readonly AudioSource[] musicVoices = new AudioSource[2];
        readonly Track[] voiceTrack = new Track[2];
        readonly float[] voiceFade = new float[2];
        int activeVoice;

        public void StartMusic(GameObject owner)
        {
            for (int v = 0; v < musicVoices.Length; v++)
            {
                musicVoices[v] = owner.AddComponent<AudioSource>();
                musicVoices[v].playOnAwake = false;
                musicVoices[v].loop = true;
                musicVoices[v].spatialBlend = 0f;
                musicVoices[v].volume = 0f;
            }
            RequestTrack("menu", MenuMusic.Render, 0.6f);
            RequestTrack("battle", () => GameMusic.Render(GameMusic.Battle), 0.42f);  // soft, under the gunfire
        }

        /// Start rendering a track if it isn't already. Safe to call every frame.
        /// Clips assigned in Unity (MusicPack.cs) that play in place of a generated track: id -> (clip, volume).
        public readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.KeyValuePair<AudioClip, float>> Overrides =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.KeyValuePair<AudioClip, float>>();

        public void RequestTrack(string id, Func<float[]> render, float gain)
        {
            if (tracks.ContainsKey(id))
                return;
            if (Overrides.TryGetValue(id, out var own) && own.Key != null)
            {
                tracks[id] = new Track { Id = id, Gain = own.Value, Clip = own.Key, Length = own.Key.length, External = true };
                return;
            }
            var track = new Track { Id = id, Gain = gain };
            tracks[id] = track;
            System.Threading.ThreadPool.QueueUserWorkItem(_ => track.Samples = render());
        }

        public static string BossTrackId(EnemyType boss) => "boss:" + boss;

        /// Start rendering any track of the music player's library (MusicLibrary.cs) if it isn't already.
        public void RequestLibraryTrack(string id)
        {
            var e = id == null ? null : MusicLibrary.Find(id);
            if (e != null && e.Clip is AudioClip clip)
                Overrides[e.Id] = new System.Collections.Generic.KeyValuePair<AudioClip, float>(clip, e.Gain);
            if (e != null)
                RequestTrack(e.Id, e.Render, e.Gain);
        }

        /// Music player: pause or resume whatever is playing (the fades and track switches wait while paused).
        public bool MusicPaused;
        bool pauseApplied;

        /// The track on the active voice, how far into it we are, and how long it is (for the player's bar).
        public string PlayingId => voiceTrack[activeVoice] == null ? null : voiceTrack[activeVoice].Id;
        public float PlayingTime => voiceTrack[activeVoice] == null ? 0f : musicVoices[activeVoice].time;
        public float PlayingLength => voiceTrack[activeVoice] == null || voiceTrack[activeVoice].Clip == null ? 0f : voiceTrack[activeVoice].Length;

        /// True once the track has been synthesised (a few seconds for the first play of a track).
        public bool IsTrackReady(string id) => id != null && tracks.TryGetValue(id, out var t) && t.Clip != null;

        /// The next time this track starts, start it from the top (used when the player picks a track).
        public void RestartTrack(string id)
        {
            if (id != null && tracks.TryGetValue(id, out var t))
                t.ResumeAt = 0f;
        }

        public void RequestBossTrack(EnemyType boss) =>
            RequestTrack(BossTrackId(boss), () => GameMusic.Render(GameMusic.ForBoss(boss)), 0.55f);

        /// Call every frame. wanted: the track that should be playing (null for silence);
        /// keep: another track to hold on to (the next boss's), so it's ready in time.
        public void UpdateMusic(string wanted, string keep, float dt, string keep2 = null)
        {
            if (musicVoices[0] == null)
                return;
            // Finished renders become clips.
            foreach (var t in tracks.Values)
                if (t.Clip == null && t.Samples != null)
                {
                    t.Clip = AudioClip.Create(t.Id, t.Samples.Length, 1, MenuMusic.Rate, false);
                    t.Clip.SetData(t.Samples, 0);
                    t.Length = t.Samples.Length / (float)MenuMusic.Rate;
                    t.Samples = null;
                }

            if (MusicPaused != pauseApplied)
            {
                pauseApplied = MusicPaused;
                for (int v = 0; v < musicVoices.Length; v++)
                    if (MusicPaused)
                        musicVoices[v].Pause();
                    else
                        musicVoices[v].UnPause();
            }
            if (MusicPaused)
                return;

            // Switch when the wanted track is ready (until then, whatever's playing carries on).
            Track want = null;
            if (wanted != null)
                tracks.TryGetValue(wanted, out want);
            bool ready = want != null && want.Clip != null;
            if ((wanted == null || ready) && voiceTrack[activeVoice] != want)
            {
                activeVoice = 1 - activeVoice;
                var src = musicVoices[activeVoice];
                if (src.isPlaying)
                    Release(activeVoice);
                voiceTrack[activeVoice] = want;
                voiceFade[activeVoice] = 0f;
                if (want != null)
                {
                    src.clip = want.Clip;
                    src.time = want.ResumeAt;
                    src.Play();
                }
            }

            for (int v = 0; v < musicVoices.Length; v++)
            {
                var t = voiceTrack[v];
                bool active = v == activeVoice && t != null;
                voiceFade[v] = Mathf.Clamp01(voiceFade[v] + (active ? dt / 1.5f : -dt / 1.2f));
                musicVoices[v].volume = t == null || Muted ? 0f : voiceFade[v] * t.Gain * Master * Music;
                if (!active && voiceFade[v] <= 0f && t != null)
                    Release(v);
            }

            // Let go of boss tracks nobody needs any more (they're a few MB each).
            string drop = null;
            foreach (var t in tracks.Values)
                if (MusicLibrary.IsTransient(t.Id) && t.Clip != null && t.Id != wanted && t.Id != keep && t.Id != keep2 && !t.External && t != voiceTrack[0] && t != voiceTrack[1])
                    drop = t.Id;
            if (drop != null)
            {
                UnityEngine.Object.Destroy(tracks[drop].Clip);
                tracks.Remove(drop);
            }
        }

        void Release(int v)
        {
            var t = voiceTrack[v];
            if (t != null && !MusicLibrary.IsTransient(t.Id))
                t.ResumeAt = musicVoices[v].time;  // pick the menu / battle music up where it left off
            musicVoices[v].Stop();
            voiceTrack[v] = null;
            voiceFade[v] = 0f;
        }

        public enum Category { Weapons, Enemies, Effects }

        public static Category CategoryOf(Sfx s)
        {
            if (s == Sfx.EnemyShot)
                return Category.Enemies;
            string name = s.ToString();
            if (name.StartsWith("Boss") || name.StartsWith("Zombie"))
                return Category.Enemies;
            if (name.StartsWith("Shot") || name.StartsWith("Spell") || name.StartsWith("Swing") || s == Sfx.Throw || s == Sfx.Hit)
                return Category.Weapons;
            return Category.Effects;
        }

        float CategoryVolume(Sfx s)
        {
            switch (CategoryOf(s))
            {
                case Category.Weapons: return Weapons;
                case Category.Enemies: return Enemies;
                default: return Effects;
            }
        }

        public SoundBank(GameObject owner)
        {
            clips = new AudioClip[count][];
            minGap = new float[count];
            lastPlayed = new float[count];
            nextVariant = new int[count];
            for (int i = 0; i < count; i++)
                lastPlayed[i] = -10f;
            voices = new AudioSource[24];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = owner.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
            Build();
        }

        void Build()
        {
            // --- Gunshots (gap = shortest time between two plays of the same sound) ---
            SetGun(Sfx.ShotShotgun, 0.1f, new Gun
            {
                Crack = 0f, CrackDecay = 0.003f, CrackHighpass = 1800f,
                Body = 1f, BodyDecay = 0.05f, BodyLowpass = 900f,
                Thump = 0.7f, ThumpFrom = 100f, ThumpTo = 45f, ThumpDecay = 0.08f,
                Tail = 0.2f, TailDecay = 0.25f, TailLowpass = 600f,
                Reverb = 0.2f, Duration = 0.4f, Peak = 0.4f,
            });
            SetGun(Sfx.ShotCannon, 0.2f, new Gun
            {
                Crack = 0.4f, CrackDecay = 0.003f, CrackHighpass = 1500f,
                Body = 1f, BodyDecay = 0.15f, BodyLowpass = 1200f,
                Thump = 1f, ThumpFrom = 90f, ThumpTo = 35f, ThumpDecay = 0.25f,
                Tail = 0.4f, TailDecay = 0.7f, TailLowpass = 500f,
                Reverb = 0.3f, Duration = 1f, Peak = 0.9f,
            });
            SetGun(Sfx.ShotRifle, 0.1f, new Gun
            {
                Crack = 1f, CrackDecay = 0.0025f, CrackHighpass = 3000f,
                Body = 0.7f, BodyDecay = 0.05f, BodyLowpass = 4000f,
                Thump = 0.5f, ThumpFrom = 180f, ThumpTo = 70f, ThumpDecay = 0.06f,
                Tail = 0.25f, TailDecay = 0.25f, TailLowpass = 1500f,
                Reverb = 0.2f, Duration = 0.45f, Peak = 0.6f,
            });
            SetGun(Sfx.ShotSuppressed, 0.08f, new Gun
            {
                Crack = 0.25f, CrackDecay = 0.0015f, CrackHighpass = 1500f,
                Body = 0.6f, BodyDecay = 0.025f, BodyLowpass = 1800f,
                Thump = 0.4f, ThumpFrom = 220f, ThumpTo = 100f, ThumpDecay = 0.03f,
                Tail = 0.05f, TailDecay = 0.08f, TailLowpass = 800f,
                Reverb = 0.08f, Duration = 0.18f, Peak = 0.4f,
            });
            SetGun(Sfx.ShotPistol, 0.1f, new Gun  // a snappy handgun: sharp crack, short body, a little room
            {
                Crack = 0.85f, CrackDecay = 0.002f, CrackHighpass = 2600f,
                Body = 0.75f, BodyDecay = 0.035f, BodyLowpass = 3200f,
                Thump = 0.45f, ThumpFrom = 200f, ThumpTo = 90f, ThumpDecay = 0.045f,
                Tail = 0.18f, TailDecay = 0.18f, TailLowpass = 1400f,
                Reverb = 0.15f, Duration = 0.35f, Peak = 0.5f,
            });
            SetGun(Sfx.ShotAssault, 0.07f, new Gun
            {
                Crack = 0.9f, CrackDecay = 0.002f, CrackHighpass = 2800f,
                Body = 0.75f, BodyDecay = 0.045f, BodyLowpass = 3500f,
                Thump = 0.55f, ThumpFrom = 170f, ThumpTo = 60f, ThumpDecay = 0.055f,
                Tail = 0.2f, TailDecay = 0.22f, TailLowpass = 1300f,
                Reverb = 0.18f, Duration = 0.35f, Peak = 0.55f,
            });
            SetGun(Sfx.ShotSniper, 0.2f, new Gun
            {
                Crack = 1f, CrackDecay = 0.003f, CrackHighpass = 2500f,
                Body = 0.9f, BodyDecay = 0.07f, BodyLowpass = 4500f,
                Thump = 0.8f, ThumpFrom = 120f, ThumpTo = 40f, ThumpDecay = 0.12f,
                Tail = 0.45f, TailDecay = 0.9f, TailLowpass = 1200f,
                Reverb = 0.35f, Duration = 1.2f, Peak = 0.9f,
            });
            SetGun(Sfx.ShotSmg, 0.045f, new Gun
            {
                Crack = 0.8f, CrackDecay = 0.0015f, CrackHighpass = 3000f,
                Body = 0.6f, BodyDecay = 0.03f, BodyLowpass = 3000f,
                Thump = 0.4f, ThumpFrom = 200f, ThumpTo = 90f, ThumpDecay = 0.035f,
                Tail = 0.12f, TailDecay = 0.15f, TailLowpass = 1200f,
                Reverb = 0.12f, Duration = 0.22f, Peak = 0.45f,
            });
            SetGun(Sfx.ShotTurret, 0.12f, new Gun
            {
                Crack = 0.6f, CrackDecay = 0.0015f, CrackHighpass = 3500f,
                Body = 0.5f, BodyDecay = 0.025f, BodyLowpass = 3000f,
                Thump = 0.3f, ThumpFrom = 220f, ThumpTo = 110f, ThumpDecay = 0.03f,
                Tail = 0.08f, TailDecay = 0.12f, TailLowpass = 1200f,
                Reverb = 0.1f, Duration = 0.2f, Peak = 0.3f,
            });
            SetGun(Sfx.ShotNova, 0.1f, new Gun
            {
                Crack = 0.6f, CrackDecay = 0.005f, CrackHighpass = 1500f,
                Body = 1f, BodyDecay = 0.12f, BodyLowpass = 2000f,
                Thump = 1f, ThumpFrom = 110f, ThumpTo = 40f, ThumpDecay = 0.15f,
                Tail = 0.35f, TailDecay = 0.5f, TailLowpass = 700f,
                Reverb = 0.3f, Duration = 0.8f, Peak = 0.7f,
            });
            // Enemy fire: duller and quieter, like shots from across the room.
            SetGun(Sfx.EnemyShot, 0.07f, new Gun
            {
                Crack = 0.3f, CrackDecay = 0.002f, CrackHighpass = 1500f,
                Body = 0.7f, BodyDecay = 0.04f, BodyLowpass = 1200f,
                Thump = 0.4f, ThumpFrom = 150f, ThumpTo = 60f, ThumpDecay = 0.05f,
                Tail = 0.3f, TailDecay = 0.3f, TailLowpass = 600f,
                Reverb = 0.3f, Duration = 0.4f, Peak = 0.28f,
            });
            var bow = new float[Variants][];
            for (int v = 0; v < Variants; v++)
                bow[v] = Bow(v);
            Set(Sfx.ShotBow, bow, 0.1f);

            // --- Everything else ---
            Set(Sfx.Hit, Tone(0.05f, Osc.Sine, 300f, 200f, 0.14f, noise: 1f, lowpass: 0.6f, decay: 3f), 0.045f);
            Set(Sfx.Hurt, Tone(0.18f, Osc.Square, 220f, 90f, 0.3f, lowpass: 0.4f), 0.1f);
            Set(Sfx.Explode, Tone(0.4f, Osc.Sine, 120f, 40f, 0.45f, noise: 0.8f, lowpass: 0.2f), 0.05f);
            Set(Sfx.BigExplode, Tone(1.2f, Osc.Sine, 80f, 30f, 0.7f, noise: 0.7f, lowpass: 0.1f, decay: 1.5f));
            Set(Sfx.Ability, Tone(0.25f, Osc.Sine, 300f, 900f, 0.3f, attack: 0.02f));
            Set(Sfx.Shockwave, Tone(0.5f, Osc.Saw, 200f, 50f, 0.4f, noise: 0.4f, lowpass: 0.3f));
            Set(Sfx.Blink, Tone(0.15f, Osc.Sine, 1200f, 400f, 0.25f));
            Set(Sfx.Pick, Tone(0.08f, Osc.Sine, 660f, 990f, 0.25f));
            Set(Sfx.SpellFire, Tone(0.25f, Osc.Sine, 200f, 120f, 0.3f, noise: 0.8f, lowpass: 0.25f, attack: 0.02f), 0.07f);
            Set(Sfx.SpellFrost, Tone(0.18f, Osc.Triangle, 1800f, 2600f, 0.16f, noise: 0.15f), 0.08f);
            Set(Sfx.SpellZap, Tone(0.12f, Osc.Saw, 900f, 300f, 0.2f, noise: 0.3f, lowpass: 0.6f), 0.07f);
            Set(Sfx.SpellArcane, Tone(0.2f, Osc.Sine, 500f, 1100f, 0.2f, attack: 0.01f), 0.06f);
            // The Repulsion Cannon: a compressed-air thump with a crack on the front, a rush of air through the cone, and a short room tail.
            {
                var dry = Mix(
                    Tone(0.03f, Osc.Sine, 2600f, 800f, 0.55f, noise: 1f, lowpass: 0.85f, attack: 0.0005f, decay: 3f),                       // the crack
                    Tone(0.32f, Osc.Sine, 150f, 36f, 0.95f, lowpass: 0.5f, attack: 0.002f, decay: 2.2f),                                    // the thump
                    Tone(0.2f, Osc.Sine, 650f, 180f, 0.55f, noise: 0.95f, lowpass: 0.22f, attack: 0.004f, decay: 2.4f),                     // the burst of air
                    Tone(0.36f, Osc.Saw, 240f, 70f, 0.3f, lowpass: 0.16f, attack: 0.01f, decay: 1.8f),                                      // the body of the blast
                    Concat(new float[(int)(0.04f * Rate)], Tone(0.45f, Osc.Sine, 900f, 260f, 0.26f, noise: 0.9f, lowpass: 0.14f, attack: 0.12f, decay: 1.3f)));  // air racing out
                var wet = Reverb(dry);
                var all = new float[Math.Max(dry.Length, wet.Length)];
                for (int i = 0; i < all.Length; i++)
                    all[i] = (i < dry.Length ? dry[i] : 0f) + (i < wet.Length ? wet[i] * 0.7f : 0f);
                Set(Sfx.ShotRepulse, Finish(all, 0.6f), 0.1f);
            }
            // Chain Light: a bright flash and rising shimmer, a buzzing zap, and a crackle of light arcing to each enemy.
            Set(Sfx.SpellChainCone, Finish(Mix(
                Tone(0.1f, Osc.Sine, 3400f, 900f, 0.5f, noise: 0.8f, lowpass: 0.85f, attack: 0.001f, decay: 3f),                                          // the flash
                Tremolo(Tone(0.42f, Osc.Saw, 560f, 170f, 0.5f, noise: 0.15f, lowpass: 0.4f, attack: 0.004f, decay: 1.5f), 52f, 0.55f),               // the zap
                Notes(new[] { 1568f, 2093f, 2637f, 3136f, 4186f }, 0.035f, Osc.Sine, 0.22f, 1f),                                                       // light, rising
                Crackle(0.38f, 26)), 0.5f), 0.1f);
            Set(Sfx.SpellChainArc, Finish(Mix(
                Tone(0.07f, Osc.Square, 2200f, 700f, 0.35f, noise: 0.35f, lowpass: 0.7f, attack: 0.0008f, decay: 3f),
                Crackle(0.14f, 7)), 0.3f), 0.05f);
            Set(Sfx.SpellHoly, Notes(new[] { 1047f, 1319f }, 0.06f, Osc.Sine, 0.18f, 1f), 0.1f);
            Set(Sfx.Throw, Tone(0.1f, Osc.Sine, 400f, 200f, 0.16f, noise: 0.6f, lowpass: 0.3f), 0.07f);
            // Knight: a quick airy whoosh, a heavier one with a low thud, and steel on steel.
            Set(Sfx.SwingLight, Tone(0.12f, Osc.Sine, 900f, 300f, 0.14f, noise: 0.85f, lowpass: 0.45f, attack: 0.02f), 0.05f);
            Set(Sfx.SwingHeavy, Tone(0.26f, Osc.Sine, 260f, 70f, 0.3f, noise: 0.6f, lowpass: 0.22f, attack: 0.03f), 0.08f);
            Set(Sfx.Clang, Tone(0.3f, Osc.Square, 1400f, 1250f, 0.16f, noise: 0.1f, lowpass: 0.7f, decay: 4f), 0.08f);
            // Bosses: soft cues when they start an ability (kept quiet under the bullets).
            Set(Sfx.BossShift, Tone(0.45f, Osc.Sine, 140f, 200f, 0.1f, lowpass: 0.4f, attack: 0.12f, decay: 1.5f), 0.3f);
            Set(Sfx.BossCharge, Tone(0.6f, Osc.Triangle, 260f, 620f, 0.08f, lowpass: 0.5f, attack: 0.25f, decay: 1.2f), 0.3f);
            Set(Sfx.BossPulse, Tone(0.5f, Osc.Sine, 95f, 45f, 0.16f, noise: 0.15f, lowpass: 0.15f, attack: 0.02f, decay: 1.5f), 0.3f);
            Set(Sfx.BossChime, Notes(new[] { 392f, 523f, 659f }, 0.09f, Osc.Sine, 0.09f, 1f), 0.3f);
            // Bosses' own mechanics: each boss's attacks have a sound of their own (wired in each boss's file).
            Set(Sfx.BossLaserWarn, Tone(0.7f, Osc.Triangle, 380f, 1500f, 0.14f, lowpass: 0.5f, attack: 0.45f, decay: 0.6f), 0.25f);  // a thin rising whine
            Set(Sfx.BossLaser, Finish(Mix(Tremolo(Tone(0.65f, Osc.Saw, 160f, 120f, 0.5f, noise: 0.12f, lowpass: 0.35f, attack: 0.01f, decay: 1f), 38f, 0.5f),
                Tone(0.5f, Osc.Sine, 1300f, 900f, 0.18f, attack: 0.005f, decay: 2f)), 0.32f), 0.25f);  // a buzzing beam with a bright edge
            Set(Sfx.BossSlam, Finish(Mix(Tone(0.9f, Osc.Sine, 75f, 28f, 1f, noise: 0.5f, lowpass: 0.08f, attack: 0.002f, decay: 2.2f),
                Tone(0.08f, Osc.Sine, 900f, 300f, 0.5f, noise: 0.9f, lowpass: 0.5f, decay: 3f)), 0.6f), 0.15f);  // a crack, then a deep thud
            Set(Sfx.BossWhoosh, Tone(0.45f, Osc.Sine, 320f, 90f, 0.5f, noise: 0.9f, lowpass: 0.22f, attack: 0.12f, decay: 1.3f), 0.12f);  // something big rushing past
            Set(Sfx.BossSiren, Finish(Concat(Tone(0.22f, Osc.Square, 660f, 660f, 0.3f, lowpass: 0.25f, attack: 0.02f, decay: 0.3f), Tone(0.22f, Osc.Square, 880f, 880f, 0.3f, lowpass: 0.25f, attack: 0.02f, decay: 0.3f),
                Tone(0.22f, Osc.Square, 660f, 660f, 0.3f, lowpass: 0.25f, attack: 0.02f, decay: 0.3f), Tone(0.3f, Osc.Square, 880f, 880f, 0.3f, lowpass: 0.25f, attack: 0.02f, decay: 1.5f)), 0.14f), 0.8f);  // the prison alarm
            Set(Sfx.BossBuzz, Finish(Tremolo(Tone(0.75f, Osc.Saw, 150f, 175f, 0.6f, noise: 0.2f, lowpass: 0.3f, attack: 0.12f, decay: 0.8f), 27f, 0.7f), 0.22f), 0.3f);  // a swarm
            Set(Sfx.BossSplat, Tone(0.28f, Osc.Sine, 190f, 55f, 0.38f, noise: 0.65f, lowpass: 0.3f, attack: 0.002f, decay: 3f), 0.1f);  // something wet landing
            Set(Sfx.BossGong, Finish(Reverb(Bell(1.8f, 98f, 0.6f)), 0.42f), 0.6f);  // a deep bell: stage shifts, rites, tolls
            Set(Sfx.BossScythe, Finish(Mix(Tone(0.22f, Osc.Sine, 1200f, 350f, 0.4f, noise: 0.85f, lowpass: 0.5f, attack: 0.03f, decay: 1.6f),
                Tone(0.35f, Osc.Sine, 2300f, 2050f, 0.12f, attack: 0.04f, decay: 2.5f)), 0.36f), 0.1f);  // a blade's swish with a ring
            Set(Sfx.BossThunder, Finish(Mix(Tone(0.1f, Osc.Sine, 2000f, 600f, 0.9f, noise: 1f, lowpass: 0.8f, decay: 4f),
                Tremolo(Tone(1.6f, Osc.Sine, 60f, 35f, 0.8f, noise: 0.7f, lowpass: 0.04f, attack: 0.03f, decay: 1.6f), 6f, 0.4f)), 0.6f), 0.2f);  // a crack, then rolling thunder
            Set(Sfx.BossWarp, Finish(Reverse(Tone(0.85f, Osc.Sine, 700f, 70f, 0.5f, noise: 0.35f, lowpass: 0.28f, decay: 2f)), 0.34f), 0.4f);  // an in-rush: something pulling everything in
            Set(Sfx.BossRumble, Finish(Tremolo(Tone(1.3f, Osc.Sine, 50f, 38f, 0.8f, noise: 0.8f, lowpass: 0.035f, attack: 0.2f, decay: 1.2f), 9f, 0.5f), 0.5f), 0.4f);  // the ground shaking
            Set(Sfx.BossCrack, Finish(Mix(Crackle(0.35f, 7), Tone(0.3f, Osc.Sine, 120f, 50f, 0.6f, noise: 0.5f, lowpass: 0.12f, decay: 2.5f)), 0.42f), 0.15f);  // stone breaking
            Set(Sfx.BossCage, Finish(Concat(Tone(0.12f, Osc.Square, 720f, 690f, 0.3f, noise: 0.1f, lowpass: 0.6f, decay: 4f), Tone(0.4f, Osc.Square, 540f, 520f, 0.3f, noise: 0.1f, lowpass: 0.6f, decay: 3.5f)), 0.3f), 0.3f);  // bars slamming shut
            Set(Sfx.BossChomp, Finish(Concat(Tone(0.08f, Osc.Sine, 160f, 80f, 0.6f, noise: 0.6f, lowpass: 0.2f, decay: 3f), Tone(0.16f, Osc.Sine, 140f, 60f, 0.8f, noise: 0.5f, lowpass: 0.18f, decay: 3f)), 0.45f), 0.12f);  // jaws snapping shut

            // The upgrade picker (PickSounds.cs): the gold pillar rising out of you, each card flying out (a little higher
            // each time: the takes play in turn) and landing with a bell ping (rising across the three cards), a sparkle for
            // a very rare or better, the cards flying back in, and yours landing in you with a warm chord.
            Set(Sfx.PickRise, Finish(Reverb(Mix(Tone(0.75f, Osc.Sine, 220f, 880f, 0.3f, attack: 0.03f, decay: 1.3f),
                Tremolo(Tone(0.75f, Osc.Triangle, 880f, 1760f, 0.1f, attack: 0.15f, decay: 1.6f), 14f, 0.5f),
                Tone(0.55f, Osc.Sine, 300f, 90f, 0.25f, noise: 0.8f, lowpass: 0.25f, attack: 0.15f, decay: 1.4f))), 0.38f), 0.3f);
            var cardOut = new float[3][];
            var cardLand = new float[3][];
            float[] pings = { 1047f, 1175f, 1319f };  // C6, D6, E6
            for (int k = 0; k < 3; k++)
            {
                float f0 = 500f * (1f + 0.18f * k);
                cardOut[k] = Finish(Tone(0.17f, Osc.Sine, f0, f0 * 1.6f, 0.14f, noise: 0.7f, lowpass: 0.4f, attack: 0.03f, decay: 2f), 0.22f);
                cardLand[k] = Finish(Mix(Tone(0.05f, Osc.Sine, 260f, 140f, 0.35f, noise: 0.5f, lowpass: 0.3f, decay: 4f),
                    Tone(0.3f, Osc.Sine, pings[k], pings[k], 0.14f, attack: 0.002f, decay: 3f)), 0.26f);
            }
            Set(Sfx.PickCardOut, cardOut, 0.04f);
            Set(Sfx.PickCardLand, cardLand, 0.04f);
            Set(Sfx.PickRare, Finish(Notes(new[] { 1568f, 2093f, 2637f }, 0.045f, Osc.Sine, 0.12f, 1f), 0.2f), 0.1f);
            Set(Sfx.PickReturn, Finish(Reverse(Tone(0.3f, Osc.Sine, 900f, 300f, 0.14f, noise: 0.7f, lowpass: 0.35f, decay: 2f)), 0.22f), 0.05f);
            Set(Sfx.PickAbsorb, Finish(Reverb(Mix(Tone(0.5f, Osc.Sine, 110f, 70f, 0.4f, noise: 0.2f, lowpass: 0.15f, attack: 0.005f, decay: 2f),
                Tone(0.65f, Osc.Sine, 523f, 523f, 0.12f, attack: 0.01f, decay: 1.6f), Tone(0.65f, Osc.Sine, 659f, 659f, 0.1f, attack: 0.01f, decay: 1.6f),
                Tone(0.65f, Osc.Sine, 784f, 784f, 0.1f, attack: 0.01f, decay: 1.6f), Tone(0.4f, Osc.Sine, 1568f, 1568f, 0.05f, attack: 0.02f, decay: 2.5f))), 0.4f), 0.2f);

            // A run launching (LaunchIntro.cs): the lobby sweeping away, the ripple across the arena, each pillar beaming
            // down (a falling shimmer) and landing (a thump with a chime, rising player by player: the takes play in turn),
            // and the HUD sliding in. The fight's own WaveStart plays as WAVE 1 pops in.
            Set(Sfx.LaunchSweep, Finish(Mix(Tone(0.7f, Osc.Sine, 420f, 140f, 0.35f, noise: 0.9f, lowpass: 0.25f, attack: 0.18f, decay: 1.4f),
                Tone(0.6f, Osc.Triangle, 660f, 990f, 0.06f, attack: 0.2f, decay: 1.8f)), 0.3f), 0.5f);
            Set(Sfx.LaunchRipple, Finish(Reverb(Mix(Tone(0.9f, Osc.Sine, 85f, 38f, 0.7f, noise: 0.2f, lowpass: 0.1f, attack: 0.01f, decay: 2.2f),
                Tremolo(Tone(0.8f, Osc.Triangle, 1320f, 1760f, 0.06f, attack: 0.1f, decay: 1.8f), 11f, 0.5f))), 0.45f), 0.5f);
            Set(Sfx.LaunchBeam, Finish(Mix(Tone(0.35f, Osc.Triangle, 2400f, 700f, 0.12f, attack: 0.01f, decay: 1.6f),
                Tone(0.35f, Osc.Sine, 900f, 300f, 0.12f, noise: 0.5f, lowpass: 0.4f, attack: 0.02f, decay: 1.6f)), 0.26f), 0.08f);
            var lands = new float[3][];
            float[] chimes = { 784f, 988f, 1175f };  // G5, B5, D6
            for (int k = 0; k < 3; k++)
                lands[k] = Finish(Mix(Tone(0.3f, Osc.Sine, 130f, 55f, 0.6f, noise: 0.3f, lowpass: 0.15f, attack: 0.002f, decay: 2.5f),
                    Tone(0.45f, Osc.Sine, chimes[k], chimes[k], 0.15f, attack: 0.005f, decay: 2.5f), Tone(0.35f, Osc.Sine, chimes[k] * 2f, chimes[k] * 2f, 0.05f, attack: 0.005f, decay: 3f)), 0.4f);
            Set(Sfx.LaunchLand, lands, 0.08f);
            Set(Sfx.LaunchHud, Finish(Tone(0.25f, Osc.Sine, 380f, 760f, 0.12f, noise: 0.6f, lowpass: 0.35f, attack: 0.05f, decay: 2f), 0.2f), 0.3f);

            // The pause menu (WindowAnim.cs; it now uses the settle and the close; the tear and ticks were its old glitch opening's): a stuttering digital tear (chopped square chirps and static),
            // a key tick for each letter of PAUSED (three pitches, played in turn), a low CRT thunk as the panel settles,
            // and the tear backwards, short, as it closes.
            float[] silence = Tone(0.012f, Osc.Sine, 100f, 100f, 0f);
            var tear = Concat(Tone(0.03f, Osc.Square, 1800f, 1700f, 0.22f, lowpass: 0.6f, attack: 0.001f, decay: 1f), silence,
                Tone(0.028f, Osc.Saw, 420f, 380f, 0.28f, noise: 0.6f, lowpass: 0.5f, attack: 0.001f, decay: 1f),
                Tone(0.04f, Osc.Square, 3200f, 2600f, 0.16f, lowpass: 0.7f, attack: 0.001f, decay: 1f), silence, silence,
                Tone(0.05f, Osc.Sine, 600f, 600f, 0.3f, noise: 1f, lowpass: 0.8f, attack: 0.001f, decay: 1f),
                Tone(0.03f, Osc.Square, 900f, 950f, 0.22f, lowpass: 0.6f, attack: 0.001f, decay: 1f), silence,
                Tone(0.025f, Osc.Square, 2400f, 2400f, 0.14f, lowpass: 0.7f, attack: 0.001f, decay: 1f),
                Tone(0.08f, Osc.Saw, 240f, 110f, 0.28f, noise: 0.5f, lowpass: 0.35f, attack: 0.001f, decay: 2.5f));
            Set(Sfx.PauseTear, Finish(tear, 0.32f), 0.2f);
            var ticks = new float[3][];
            for (int k = 0; k < 3; k++)
                ticks[k] = Finish(Mix(Tone(0.03f, Osc.Square, 2000f + 300f * k, 1900f + 300f * k, 0.1f, lowpass: 0.5f, attack: 0.001f, decay: 6f),
                    Tone(0.012f, Osc.Sine, 500f, 500f, 0.12f, noise: 1f, lowpass: 0.6f, attack: 0.001f, decay: 5f)), 0.16f);
            Set(Sfx.PauseType, ticks, 0.03f);
            Set(Sfx.PauseSettle, Finish(Mix(Tone(0.35f, Osc.Sine, 140f, 62f, 0.4f, noise: 0.1f, lowpass: 0.15f, attack: 0.003f, decay: 2.5f),
                Tremolo(Tone(0.3f, Osc.Saw, 60f, 60f, 0.05f, lowpass: 0.08f, attack: 0.01f, decay: 2.5f), 50f, 0.6f),
                Tone(0.15f, Osc.Sine, 1600f, 1600f, 0.03f, attack: 0.002f, decay: 4f)), 0.3f), 0.2f);
            Set(Sfx.PauseClose, Finish(Reverse(Concat(Tone(0.03f, Osc.Square, 1500f, 1300f, 0.2f, lowpass: 0.6f, attack: 0.001f, decay: 1f), silence,
                Tone(0.04f, Osc.Sine, 600f, 600f, 0.25f, noise: 1f, lowpass: 0.7f, attack: 0.001f, decay: 1f),
                Tone(0.07f, Osc.Saw, 300f, 120f, 0.25f, noise: 0.4f, lowpass: 0.35f, attack: 0.001f, decay: 2f))), 0.26f), 0.15f);

            // The Radiant Orb (RadiantOrb.cs): a rising shimmer as it's cast, a small twinkle (three takes, in turn) each time its
            // tick touches an enemy or an ally, and a falling shimmer as it fades.
            Set(Sfx.RadiantCast, Finish(Reverb(Mix(Tone(0.35f, Osc.Sine, 700f, 1400f, 0.18f, attack: 0.03f, decay: 2f),
                Tone(0.4f, Osc.Triangle, 1047f, 2093f, 0.08f, attack: 0.05f, decay: 2f), Tone(0.3f, Osc.Sine, 1568f, 2349f, 0.05f, attack: 0.08f, decay: 2.5f))), 0.28f), 0.1f);
            var twinkles = new float[3][];
            float[][] pairs = { new[] { 2093f, 2637f }, new[] { 1760f, 2349f }, new[] { 1976f, 2794f } };
            for (int k = 0; k < 3; k++)
                twinkles[k] = Finish(Notes(pairs[k], 0.04f, Osc.Sine, 0.1f, 1f), 0.1f);
            Set(Sfx.RadiantTouch, twinkles, 0.1f);
            Set(Sfx.RadiantFade, Finish(Reverb(Mix(Tone(0.4f, Osc.Sine, 1400f, 500f, 0.16f, attack: 0.01f, decay: 2.2f),
                Tone(0.4f, Osc.Triangle, 2100f, 900f, 0.06f, attack: 0.01f, decay: 2.5f))), 0.22f), 0.1f);

            // Zombie Infection: rasping, wet and low. Groans come in three takes so a horde doesn't repeat itself.
            Set(Sfx.ZombieGroan, new[] {
                Tone(0.7f, Osc.Saw, 95f, 60f, 0.32f, noise: 0.3f, lowpass: 0.12f, attack: 0.12f, decay: 1.6f),
                Tone(0.8f, Osc.Saw, 80f, 50f, 0.3f, noise: 0.35f, lowpass: 0.1f, attack: 0.15f, decay: 1.4f),
                Tone(0.6f, Osc.Saw, 120f, 70f, 0.3f, noise: 0.25f, lowpass: 0.14f, attack: 0.1f, decay: 1.8f) }, 0.3f);
            Set(Sfx.ZombieRise, Mix(Tone(0.5f, Osc.Saw, 55f, 130f, 0.3f, noise: 0.4f, lowpass: 0.1f, attack: 0.2f, decay: 1.2f),
                Tone(0.25f, Osc.Sine, 160f, 50f, 0.25f, noise: 0.7f, lowpass: 0.3f, attack: 0.01f, decay: 3f)), 0.15f);
            Set(Sfx.ZombieSpit, Mix(Tone(0.12f, Osc.Sine, 300f, 700f, 0.2f, noise: 0.5f, lowpass: 0.4f, attack: 0.005f, decay: 3f),
                Tone(0.2f, Osc.Sine, 500f, 150f, 0.15f, noise: 0.7f, lowpass: 0.3f, attack: 0.03f, decay: 3f)), 0.2f);
            Set(Sfx.ZombieGrab, Mix(Tone(0.3f, Osc.Saw, 140f, 70f, 0.35f, noise: 0.3f, lowpass: 0.15f, attack: 0.005f, decay: 2.5f),
                Tone(0.12f, Osc.Sine, 200f, 60f, 0.3f, noise: 0.5f, lowpass: 0.25f, attack: 0.002f, decay: 4f)), 0.2f);
            Set(Sfx.ZombieShriek, Mix(Tone(0.8f, Osc.Saw, 700f, 1500f, 0.25f, noise: 0.25f, lowpass: 0.6f, attack: 0.02f, decay: 1.5f),
                Tone(0.8f, Osc.Square, 1050f, 2100f, 0.1f, attack: 0.05f, decay: 1.5f)), 0.5f);
            Set(Sfx.ZombieBurst, Mix(Tone(0.4f, Osc.Sine, 120f, 40f, 0.4f, noise: 0.6f, lowpass: 0.2f, attack: 0.002f, decay: 2.5f),
                Tone(0.3f, Osc.Sine, 400f, 120f, 0.2f, noise: 0.8f, lowpass: 0.4f, attack: 0.002f, decay: 3f)), 0.15f);
            Set(Sfx.ZombieMend, Notes(new[] { 330f, 440f, 392f }, 0.1f, Osc.Triangle, 0.18f, 0.5f), 0.3f);

            // The profile picker and the menu (ProfileTransition.cs): a two-note select click, four rising pings (takes played in turn)
            // as the cards become points, a zip as the others shoot off, a sweep as your point opens into the box (and backwards),
            // a pen scratch for the title, the bar sliding up, and a low chord as the menu settles.
            Set(Sfx.ProfSelect, Finish(Concat(Tone(0.09f, Osc.Triangle, 520f, 780f, 0.22f, attack: 0.003f, decay: 2.5f),
                Tone(0.14f, Osc.Triangle, 780f, 1170f, 0.2f, attack: 0.003f, decay: 2.5f)), 0.3f), 0.1f);
            var profPings = new float[4][];
            float[] pingHz = { 660f, 784f, 988f, 1175f };
            for (int k = 0; k < 4; k++)
                profPings[k] = Finish(Mix(Tone(0.22f, Osc.Sine, pingHz[k], pingHz[k], 0.18f, attack: 0.003f, decay: 3f),
                    Tone(0.15f, Osc.Sine, pingHz[k] * 2f, pingHz[k] * 2f, 0.04f, attack: 0.003f, decay: 4f)), 0.22f);
            Set(Sfx.ProfPing, profPings, 0.03f);
            Set(Sfx.ProfZip, Finish(Mix(Tone(0.22f, Osc.Sine, 3200f, 700f, 0.14f, noise: 0.8f, lowpass: 0.5f, attack: 0.01f, decay: 2f),
                Tone(0.16f, Osc.Sine, 1900f, 420f, 0.07f, attack: 0.005f, decay: 2.5f)), 0.22f), 0.1f);
            var sweep = Mix(Tone(0.5f, Osc.Sine, 300f, 1400f, 0.14f, attack: 0.12f, decay: 1.5f), Tone(0.5f, Osc.Sine, 450f, 2100f, 0.05f, attack: 0.12f, decay: 1.5f));
            Set(Sfx.ProfSweep, Finish(sweep, 0.2f), 0.3f);
            Set(Sfx.ProfSweepBack, Finish(Reverse(Mix(Tone(0.5f, Osc.Sine, 300f, 1400f, 0.14f, attack: 0.12f, decay: 1.5f), Tone(0.5f, Osc.Sine, 450f, 2100f, 0.05f, attack: 0.12f, decay: 1.5f))), 0.2f), 0.3f);
            Set(Sfx.ProfScratch, Finish(Tremolo(Tone(0.55f, Osc.Sine, 2800f, 3800f, 0.1f, noise: 1f, lowpass: 0.75f, attack: 0.05f, decay: 0.8f), 24f, 0.7f), 0.1f), 0.3f);
            Set(Sfx.ProfBar, Finish(Mix(Tone(0.3f, Osc.Sine, 900f, 300f, 0.12f, noise: 0.9f, lowpass: 0.2f, attack: 0.05f, decay: 2f),
                Tone(0.25f, Osc.Sine, 200f, 250f, 0.06f, attack: 0.08f, decay: 2f)), 0.16f), 0.2f);
            Set(Sfx.ProfChord, Finish(Reverb(Mix(Tone(0.85f, Osc.Sine, 110f, 110f, 0.12f, attack: 0.03f, decay: 2f), Tone(0.85f, Osc.Triangle, 165f, 165f, 0.12f, attack: 0.03f, decay: 2f),
                Tone(0.85f, Osc.Sine, 220f, 220f, 0.12f, attack: 0.03f, decay: 2f), Tone(0.85f, Osc.Triangle, 277f, 277f, 0.1f, attack: 0.03f, decay: 2f))), 0.22f), 0.4f);

            // The menu title typing (MenuIntro.cs): a mechanical keyboard. Each key is a click over a soft thock (four takes, slightly
            // different pitch, played in turn); the space bar is deeper; the last letter is Enter, a bigger thock.
            var titleKeys = new float[4][];
            float[] keyHz = { 0.92f, 1f, 1.08f, 0.97f };
            for (int k = 0; k < 4; k++)
                titleKeys[k] = Finish(Mix(Tone(0.008f, Osc.Sine, 2200f * keyHz[k], 2200f * keyHz[k], 0.18f, noise: 0.9f, lowpass: 0.6f, attack: 0.001f, decay: 3f),
                    Tone(0.045f, Osc.Sine, 150f * keyHz[k], 90f, 0.22f, attack: 0.002f, decay: 3f)), 0.2f);
            Set(Sfx.TitleKey, titleKeys, 0f);
            Set(Sfx.TitleSpace, Finish(Mix(Tone(0.012f, Osc.Sine, 1500f, 1500f, 0.16f, noise: 0.9f, lowpass: 0.5f, attack: 0.001f, decay: 3f),
                Tone(0.07f, Osc.Sine, 95f, 60f, 0.3f, attack: 0.002f, decay: 3f)), 0.26f), 0f);
            Set(Sfx.TitleEnter, Finish(Mix(Tone(0.014f, Osc.Sine, 1400f, 1400f, 0.22f, noise: 0.9f, lowpass: 0.5f, attack: 0.001f, decay: 3f),
                Tone(0.12f, Osc.Sine, 110f, 55f, 0.4f, attack: 0.002f, decay: 3f)), 0.34f), 0f);

            // A main menu button being hovered (MenuVhs.cs): a dry tick with a puff of tape hiss, like a head touching the tape. Three takes, rotated.
            var hover = new float[3][];
            float[] hoverHz = { 0.95f, 1f, 1.06f };
            for (int k = 0; k < 3; k++)
                hover[k] = Finish(Mix(Tone(0.012f, Osc.Sine, 5500f, 5500f, 0.12f, noise: 1f, lowpass: 0.9f, attack: 0.001f, decay: 3f),
                    Tone(0.018f, Osc.Sine, 1200f * hoverHz[k], 900f, 0.09f, attack: 0.001f, decay: 3f)), 0.14f);
            Set(Sfx.MenuHover, hover, 0.04f);

            // The menu's Play transition (MenuServo.cs): two beeps (takes rotated), the doors' servo whine and its reverse, a clamp click, the screen's
            // power-on zap, a falling whirr for each key leaving, a ratchet tick as it lands, the box lifting, its seating thud, and a flap click per header letter.
            Set(Sfx.BayBeep, new[] { Finish(Tone(0.07f, Osc.Sine, 1320f, 1320f, 0.3f, attack: 0.004f, decay: 2.5f), 0.26f),
                Finish(Tone(0.07f, Osc.Sine, 1760f, 1760f, 0.3f, attack: 0.004f, decay: 2.5f), 0.26f) }, 0.04f);
            Set(Sfx.BayServo, Finish(Tone(0.32f, Osc.Saw, 280f, 920f, 0.1f, lowpass: 0.12f, attack: 0.05f, decay: 1.2f), 0.22f), 0.1f);
            Set(Sfx.BayServoBack, Finish(Reverse(Tone(0.32f, Osc.Saw, 280f, 920f, 0.1f, lowpass: 0.12f, attack: 0.05f, decay: 1.2f)), 0.22f), 0.1f);
            Set(Sfx.BayClamp, Finish(Concat(Tone(0.012f, Osc.Sine, 2600f, 2600f, 0.3f, noise: 1f, lowpass: 0.6f, attack: 0.001f, decay: 3f),
                Tone(0.03f, Osc.Sine, 700f, 400f, 0.25f, noise: 0.6f, lowpass: 0.3f, attack: 0.001f, decay: 3f)), 0.3f), 0.05f);
            Set(Sfx.BayZap, Finish(Mix(Tone(0.18f, Osc.Saw, 90f, 2400f, 0.12f, lowpass: 0.4f, attack: 0.005f, decay: 1.5f),
                Tone(0.15f, Osc.Sine, 3500f, 3500f, 0.1f, noise: 1f, lowpass: 0.8f, attack: 0.002f, decay: 3f)), 0.2f), 0.05f);
            Set(Sfx.BayWhirr, Finish(Tone(0.24f, Osc.Saw, 700f, 260f, 0.07f, lowpass: 0.12f, attack: 0.03f, decay: 1.5f), 0.16f), 0.03f);
            Set(Sfx.BayTick, Finish(Mix(Tone(0.008f, Osc.Sine, 3600f, 3600f, 0.2f, noise: 1f, lowpass: 0.7f, attack: 0.001f, decay: 3f),
                Tone(0.02f, Osc.Square, 520f, 520f, 0.05f, attack: 0.001f, decay: 3f)), 0.22f), 0.03f);
            Set(Sfx.BayLift, Finish(Tone(0.24f, Osc.Saw, 420f, 760f, 0.08f, lowpass: 0.12f, attack: 0.04f, decay: 1.4f), 0.18f), 0.05f);
            Set(Sfx.BaySeat, Finish(Mix(Tone(0.18f, Osc.Sine, 95f, 40f, 0.45f, attack: 0.002f, decay: 3f),
                Tone(0.08f, Osc.Sine, 320f, 320f, 0.3f, noise: 1f, lowpass: 0.15f, attack: 0.001f, decay: 3f)), 0.4f), 0.05f);
            var clacks = new float[3][];
            float[] clackHz = { 0.92f, 1f, 1.1f };
            for (int k = 0; k < 3; k++)
                clacks[k] = Finish(Mix(Tone(0.014f, Osc.Sine, 2300f * clackHz[k], 2300f * clackHz[k], 0.25f, noise: 0.9f, lowpass: 0.6f, attack: 0.001f, decay: 3f),
                    Tone(0.01f, Osc.Square, 700f * clackHz[k], 700f * clackHz[k], 0.05f, attack: 0.001f, decay: 3f)), 0.16f);
            Set(Sfx.BayClack, clacks, 0.015f);

            // New run's CRT boot (LobbyCrt.cs): a degauss thump with a rising whine, a soft key per typed character (takes rotated), a two-tone beep per log line,
            // the scan line's rising sweep, and a short ready chime.
            Set(Sfx.CrtPower, Finish(Mix(Tone(0.35f, Osc.Sine, 70f, 38f, 0.5f, attack: 0.003f, decay: 2.5f),
                Tone(0.9f, Osc.Saw, 120f, 1800f, 0.07f, lowpass: 0.2f, attack: 0.15f, decay: 1.6f),
                Tone(0.2f, Osc.Sine, 3000f, 3000f, 0.08f, noise: 1f, lowpass: 0.8f, attack: 0.002f, decay: 3f)), 0.4f), 0.05f);
            var ctype = new float[3][];
            float[] ctypeHz = { 0.94f, 1f, 1.08f };
            for (int k = 0; k < 3; k++)
                ctype[k] = Finish(Mix(Tone(0.01f, Osc.Sine, 2800f * ctypeHz[k], 2800f * ctypeHz[k], 0.18f, noise: 0.9f, lowpass: 0.6f, attack: 0.001f, decay: 3f),
                    Tone(0.025f, Osc.Square, 900f * ctypeHz[k], 900f * ctypeHz[k], 0.05f, attack: 0.001f, decay: 3f)), 0.14f);
            Set(Sfx.CrtType, ctype, 0.01f);
            Set(Sfx.CrtLine, Finish(Concat(Tone(0.06f, Osc.Square, 880f, 880f, 0.1f, lowpass: 0.4f, attack: 0.003f, decay: 2.5f),
                Tone(0.09f, Osc.Square, 1320f, 1320f, 0.1f, lowpass: 0.4f, attack: 0.003f, decay: 2.5f)), 0.2f), 0.04f);
            Set(Sfx.CrtSweep, Finish(Mix(Tone(1.0f, Osc.Saw, 200f, 1400f, 0.08f, lowpass: 0.25f, attack: 0.1f, decay: 1.2f),
                Tone(1.0f, Osc.Sine, 3200f, 5200f, 0.04f, noise: 0.6f, lowpass: 0.8f, attack: 0.2f, decay: 1.5f)), 0.22f), 0.05f);
            Set(Sfx.CrtReady, Finish(Notes(new[] { 784f, 1047f, 1568f }, 0.09f, Osc.Sine, 0.22f, 1f), 0.3f), 0.05f);

            Set(Sfx.Summon, Tone(0.4f, Osc.Triangle, 200f, 600f, 0.3f, attack: 0.05f));
            Set(Sfx.Freeze, Tone(0.4f, Osc.Triangle, 2000f, 800f, 0.3f, noise: 0.2f));
            Set(Sfx.Victory, Notes(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f }, 0.13f, Osc.Square, 0.25f, 0.4f));
            Set(Sfx.Roar, Tone(0.7f, Osc.Saw, 110f, 55f, 0.45f, noise: 0.35f, lowpass: 0.15f, attack: 0.04f, decay: 1.2f));
            Set(Sfx.Pickup, Notes(new[] { 880f, 1320f }, 0.06f, Osc.Sine, 0.25f, 1f), 0.05f);
            Set(Sfx.PickupBig, Notes(new[] { 660f, 880f, 1100f, 1320f }, 0.07f, Osc.Triangle, 0.3f, 1f));
            Set(Sfx.LevelUp, Notes(new[] { 523f, 659f, 784f, 1047f }, 0.08f, Osc.Square, 0.2f, 0.5f));
            Set(Sfx.WaveStart, Notes(new[] { 392f, 523f }, 0.15f, Osc.Saw, 0.25f, 0.3f));
            Set(Sfx.WaveClear, Notes(new[] { 523f, 659f, 784f }, 0.1f, Osc.Triangle, 0.3f, 1f));
            Set(Sfx.GameOver, Notes(new[] { 392f, 330f, 262f, 196f }, 0.22f, Osc.Square, 0.3f, 0.3f));
            Set(Sfx.Downed, Tone(0.4f, Osc.Square, 400f, 100f, 0.3f, lowpass: 0.3f));
            Set(Sfx.Revive, Tone(0.3f, Osc.Sine, 300f, 800f, 0.3f, attack: 0.02f));

            // Menus: a soft, short tick when the mouse moves onto a button (not an Sfx: it never goes over the network).
            var tick = Tone(0.045f, Osc.Sine, 1500f, 2100f, 0.09f, lowpass: 0.5f, attack: 0.002f, decay: 3f);
            uiHover = AudioClip.Create("ui_hover", tick.Length, 1, Rate, false);
            uiHover.SetData(tick, 0);
            // ...and a soft, rounder click when one is pressed.
            var click = Tone(0.07f, Osc.Triangle, 950f, 560f, 0.16f, noise: 0.08f, lowpass: 0.35f, attack: 0.001f, decay: 4f);
            uiClick = AudioClip.Create("ui_click", click.Length, 1, Rate, false);
            uiClick.SetData(click, 0);
        }

        AudioClip uiClick;
        float uiClickLast = -10f;

        /// The menus' click, at the Effects volume.
        public void PlayUiClick(float now)
        {
            if (Muted || uiClick == null || now - uiClickLast < 0.05f)
                return;
            float volume = Master * Effects;
            if (volume <= 0.001f)
                return;
            uiClickLast = now;
            uiHoverLast = now;  // no hover tick right on top of the click
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.05f;
            voice.PlayOneShot(uiClick, volume);
        }

        AudioClip uiHover;
        float uiHoverLast = -10f;

        /// The menus' hover tick, at the Effects volume. Rapid sweeps across a row play at most every 40 ms.
        public void PlayUiHover(float now)
        {
            if (Muted || uiHover == null || now - uiHoverLast < 0.04f)
                return;
            float volume = Master * Effects;
            if (volume <= 0.001f)
                return;
            uiHoverLast = now;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.06f;
            voice.PlayOneShot(uiHover, volume);
        }

        void SetGun(Sfx s, float gap, Gun g)
        {
            var takes = new float[Variants][];
            for (int v = 0; v < Variants; v++)
                takes[v] = Gunshot(g, (int)s * 31 + v);
            Set(s, takes, gap);
        }

        void Set(Sfx s, float[] samples, float gap = 0f)
        {
            Set(s, new[] { samples }, gap);
        }

        void Set(Sfx s, float[][] takes, float gap)
        {
            var list = new AudioClip[takes.Length];
            for (int v = 0; v < takes.Length; v++)
            {
                list[v] = AudioClip.Create(s + "_" + v, takes[v].Length, 1, Rate, false);
                list[v].SetData(takes[v], 0);
            }
            clips[(int)s] = list;
            minGap[(int)s] = gap;
        }

        public void Play(Sfx s, float volume, float now)
        {
            int i = (int)s;
            if (Muted || i <= 0 || i >= count || clips[i] == null || now - lastPlayed[i] < minGap[i])
                return;
            volume *= Master * CategoryVolume(s);
            if (volume <= 0.001f)
                return;
            lastPlayed[i] = now;
            var takes = clips[i];
            var clip = takes[nextVariant[i]++ % takes.Length];
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.08f;
            voice.PlayOneShot(clip, volume);
        }

        // --- Muffled playback (the lobby's loadout preview) ------------------------
        // A copy of each clip run through a low-pass filter, built the first time it's needed.
        const float MuffleHz = 650f, MuffleVolume = 0.55f;
        AudioClip[][] muffled;
        float[] lastMuffled;

        /// Like Play, but dull and quieter: the sound as heard through a wall.
        public void PlayMuffled(Sfx s, float volume, float now)
        {
            int i = (int)s;
            if (Muted || i <= 0 || i >= count || clips[i] == null)
                return;
            if (muffled == null)
            {
                muffled = new AudioClip[count][];
                lastMuffled = new float[count];
                for (int k = 0; k < count; k++)
                    lastMuffled[k] = -10f;
            }
            if (now - lastMuffled[i] < minGap[i])
                return;
            volume *= Master * CategoryVolume(s) * MuffleVolume;
            if (volume <= 0.001f)
                return;
            if (muffled[i] == null)
            {
                var src = clips[i];
                var list = new AudioClip[src.Length];
                double a = 1.0 - Math.Exp(-2.0 * Math.PI * MuffleHz / Rate);  // one-pole coefficient, applied twice
                for (int v = 0; v < src.Length; v++)
                {
                    var data = new float[src[v].samples * src[v].channels];
                    src[v].GetData(data, 0);
                    double y1 = 0, y2 = 0;
                    for (int n = 0; n < data.Length; n++)
                    {
                        y1 += a * (data[n] - y1);
                        y2 += a * (y1 - y2);
                        data[n] = (float)y2 * 1.6f;  // (what the filter takes off the top, partly given back)
                    }
                    list[v] = AudioClip.Create(src[v].name + "_muffled", data.Length / src[v].channels, src[v].channels, Rate, false);
                    list[v].SetData(data, 0);
                }
                muffled[i] = list;
            }
            lastMuffled[i] = now;
            var takes = muffled[i];
            var clip = takes[nextVariant[i]++ % takes.Length];
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.08f;
            voice.PlayOneShot(clip, volume);
        }

        // --- Synthesis ---------------------------------------------------------

        /// Second-order filter (RBJ cookbook), used for the gunshot layers.
        class Biquad
        {
            double b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public static Biquad Lowpass(double hz) => Make(hz, false);
            public static Biquad Highpass(double hz) => Make(hz, true);

            static Biquad Make(double hz, bool high)
            {
                double w = 2.0 * Math.PI * Math.Min(hz, Rate * 0.45) / Rate;
                double cos = Math.Cos(w), alpha = Math.Sin(w) / (2.0 * 0.7071);
                double a0 = 1.0 + alpha;
                var f = new Biquad();
                f.b0 = (high ? (1.0 + cos) : (1.0 - cos)) / 2.0 / a0;
                f.b1 = (high ? -(1.0 + cos) : (1.0 - cos)) / a0;
                f.b2 = f.b0;
                f.a1 = -2.0 * cos / a0;
                f.a2 = (1.0 - alpha) / a0;
                return f;
            }

            public float Process(float x)
            {
                double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return (float)y;
            }
        }

        float[] Gunshot(Gun g, int seed)
        {
            var noise = new System.Random(seed);
            int n = (int)(g.Duration * Rate);
            var dry = new float[n];
            var crackHp = Biquad.Highpass(g.CrackHighpass);
            var bodyLp = Biquad.Lowpass(g.BodyLowpass * (0.9f + 0.2f * (float)noise.NextDouble()));
            var tailLp1 = Biquad.Lowpass(g.TailLowpass);
            var tailLp2 = Biquad.Lowpass(g.TailLowpass);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)noise.NextDouble() * 2f - 1f;
                float crack = crackHp.Process(white) * (float)Math.Exp(-t / g.CrackDecay);
                float body = bodyLp.Process(white) * Mathf.Min(1f, t / 0.0005f) * (float)Math.Exp(-t / g.BodyDecay);
                float sweep = Mathf.Min(1f, t / (g.ThumpDecay * 2f));
                phase += g.ThumpFrom * Math.Pow(g.ThumpTo / g.ThumpFrom, sweep) / Rate;
                float thump = (float)Math.Sin(2.0 * Math.PI * phase) * (float)Math.Exp(-t / g.ThumpDecay);
                float tail = tailLp2.Process(tailLp1.Process(white)) * (1f - (float)Math.Exp(-t / 0.01f)) * (float)Math.Exp(-t / g.TailDecay);
                float mix = g.Crack * crack + g.Body * body * 2f + g.Thump * thump + g.Tail * tail * 3f;
                dry[i] = (float)Math.Tanh(mix * 1.5f);  // soft clip for punch
            }
            var wet = Reverb(dry);
            for (int i = 0; i < n; i++)
                dry[i] += wet[i] * g.Reverb;
            return Finish(dry, g.Peak);
        }

        /// A bowstring: plucked-string synthesis (Karplus-Strong) plus the whoosh of the arrow.
        float[] Bow(int seed)
        {
            var noise = new System.Random(100 + seed);
            int n = (int)(0.35f * Rate);
            var data = new float[n];
            int period = (int)(Rate / (95f + seed * 4f));
            var str = new float[period];
            for (int i = 0; i < period; i++)
                str[i] = (float)noise.NextDouble() * 2f - 1f;
            var whooshHp = Biquad.Highpass(900);
            var whooshLp = Biquad.Lowpass(3500);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                int k = i % period;
                float s = str[k];
                str[k] = 0.996f * 0.5f * (s + str[(k + 1) % period]);
                float whoosh = whooshLp.Process(whooshHp.Process((float)noise.NextDouble() * 2f - 1f)) * (float)Math.Exp(-t / 0.05f);
                phase += (150.0 - 60.0 * Math.Min(1.0, t / 0.04)) / Rate;
                float thunk = (float)Math.Sin(2.0 * Math.PI * phase) * (float)Math.Exp(-t / 0.02f);
                data[i] = s * (float)Math.Exp(-t / 0.12f) * 0.8f + whoosh * 1.5f + thunk * 0.6f;
            }
            return Finish(data, 0.45f);
        }

        /// Small room reverb: four damped comb filters into two allpasses.
        static float[] Reverb(float[] input)
        {
            int n = input.Length;
            var outp = new float[n];
            float[] combMs = { 29.7f, 37.1f, 41.1f, 43.7f };
            foreach (float ms in combMs)
            {
                int d = (int)(ms * Rate / 1000f);
                var buf = new float[d];
                float store = 0f;
                for (int i = 0; i < n; i++)
                {
                    float y = buf[i % d];
                    store = y * 0.7f + store * 0.3f;  // damping: high frequencies die faster
                    buf[i % d] = input[i] + store * 0.72f;
                    outp[i] += y * 0.25f;
                }
            }
            foreach (float ms in new[] { 5f, 1.7f })
            {
                int d = (int)(ms * Rate / 1000f);
                var buf = new float[d];
                for (int i = 0; i < n; i++)
                {
                    float b = buf[i % d];
                    float x = outp[i];
                    buf[i % d] = x + b * 0.5f;
                    outp[i] = b - x * 0.5f;
                }
            }
            return outp;
        }

        /// Fade the end to avoid clicks and normalize to the requested peak.
        static float[] Finish(float[] data, float peak)
        {
            int n = data.Length, fade = Math.Min(n, Rate / 100);
            for (int i = 0; i < fade; i++)
                data[n - 1 - i] *= (float)i / fade;
            float max = 0f;
            foreach (float v in data)
                max = Math.Max(max, Math.Abs(v));
            if (max > 0f)
                for (int i = 0; i < n; i++)
                    data[i] *= peak / max;
            return data;
        }

        /// A single sweep from f0 to f1 Hz with an attack/decay envelope.
        /// noise mixes in white noise (0..1); lowpass smooths the result (1 = off).
        float[] Tone(float dur, Osc osc, float f0, float f1, float vol,
                     float noise = 0f, float lowpass = 1f, float attack = 0.003f, float decay = 2f)
        {
            int n = (int)(dur * Rate);
            var data = new float[n];
            double phase = 0;
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = (float)i / n;
                float f = f0 * Mathf.Pow(f1 / f0, u);
                phase += f / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float x;
                switch (osc)
                {
                    case Osc.Square: x = p < 0.5f ? 1f : -1f; break;
                    case Osc.Triangle: x = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                    case Osc.Saw: x = 2f * p - 1f; break;
                    default: x = Mathf.Sin(2f * Mathf.PI * p); break;
                }
                if (noise > 0f)
                    x = x * (1f - noise) + ((float)rng.NextDouble() * 2f - 1f) * noise;
                y += lowpass * (x - y);
                float env = Mathf.Min(1f, t / attack) * Mathf.Pow(1f - u, decay);
                data[i] = y * env * vol;
            }
            return data;
        }

        /// Two or more layers played together (the longest sets the length).
        static float[] Mix(params float[][] layers)
        {
            int n = 0;
            foreach (var l in layers)
                n = Math.Max(n, l.Length);
            var outp = new float[n];
            foreach (var l in layers)
                for (int i = 0; i < l.Length; i++)
                    outp[i] += l[i];
            return outp;
        }

        /// Sounds one after another.
        static float[] Concat(params float[][] parts)
        {
            int n = 0;
            foreach (var p in parts)
                n += p.Length;
            var outp = new float[n];
            int at = 0;
            foreach (var p in parts)
            {
                Array.Copy(p, 0, outp, at, p.Length);
                at += p.Length;
            }
            return outp;
        }

        /// Played backwards: a decaying sweep becomes a swell (an in-rush).
        static float[] Reverse(float[] data)
        {
            var outp = (float[])data.Clone();
            Array.Reverse(outp);
            return outp;
        }

        /// A wobble in loudness (hz times a second; depth 0..1): buzzing, humming, rumbling.
        static float[] Tremolo(float[] data, float hz, float depth)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= 1f - depth * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * hz * i / Rate));
            return data;
        }

        /// A struck bell: a few inharmonic partials, each dying away at its own pace.
        static float[] Bell(float dur, float f, float vol)
        {
            float[] ratio = { 1f, 2.76f, 5.4f, 8.93f }, amp = { 1f, 0.5f, 0.25f, 0.12f }, life = { 1f, 0.6f, 0.35f, 0.2f };
            int n = (int)(dur * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = (float)i / n, x = 0f;
                for (int k = 0; k < ratio.Length; k++)
                    x += amp[k] * Mathf.Sin(2f * Mathf.PI * f * ratio[k] * t) * Mathf.Pow(Mathf.Max(0f, 1f - u / life[k]), 2f);
                data[i] = x * vol * Mathf.Min(1f, t / 0.004f);
            }
            return data;
        }

        /// A burst of sharp clicks (stone cracking), fading over dur.
        float[] Crackle(float dur, int clicks)
        {
            int n = (int)(dur * Rate);
            var data = new float[n];
            for (int c = 0; c < clicks; c++)
            {
                int at = (int)(n * Mathf.Pow((float)rng.NextDouble(), 1.6f));
                float amp = 0.5f + 0.5f * (float)rng.NextDouble();
                for (int i = 0; i < Rate / 200 && at + i < n; i++)
                    data[at + i] += ((float)rng.NextDouble() * 2f - 1f) * amp * (1f - (float)i / (Rate / 200));
            }
            return data;
        }

        float[] Notes(float[] freqs, float noteDur, Osc osc, float vol, float lowpass)
        {
            int per = (int)(noteDur * Rate);
            var data = new float[per * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
            {
                var note = Tone(noteDur, osc, freqs[k], freqs[k], vol, lowpass: lowpass, decay: 1f);
                Array.Copy(note, 0, data, k * per, Math.Min(note.Length, per));
            }
            return data;
        }
    }
}
