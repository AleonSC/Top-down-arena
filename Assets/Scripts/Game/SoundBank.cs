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
        public void RequestTrack(string id, Func<float[]> render, float gain)
        {
            if (tracks.ContainsKey(id))
                return;
            var track = new Track { Id = id, Gain = gain };
            tracks[id] = track;
            System.Threading.ThreadPool.QueueUserWorkItem(_ => track.Samples = render());
        }

        public static string BossTrackId(EnemyType boss) => "boss:" + boss;

        public void RequestBossTrack(EnemyType boss) =>
            RequestTrack(BossTrackId(boss), () => GameMusic.Render(GameMusic.ForBoss(boss)), 0.55f);

        /// Call every frame. wanted: the track that should be playing (null for silence);
        /// keep: another track to hold on to (the next boss's), so it's ready in time.
        public void UpdateMusic(string wanted, string keep, float dt)
        {
            if (musicVoices[0] == null)
                return;
            // Finished renders become clips.
            foreach (var t in tracks.Values)
                if (t.Clip == null && t.Samples != null)
                {
                    t.Clip = AudioClip.Create(t.Id, t.Samples.Length, 1, MenuMusic.Rate, false);
                    t.Clip.SetData(t.Samples, 0);
                    t.Samples = null;
                }

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
                if (t.Id.StartsWith("boss:") && t.Clip != null && t.Id != wanted && t.Id != keep && t != voiceTrack[0] && t != voiceTrack[1])
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
            if (t != null && !t.Id.StartsWith("boss:"))
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
            if (name.StartsWith("Shot") || name.StartsWith("Spell") || s == Sfx.Throw || s == Sfx.Hit)
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
                Crack = 0.7f, CrackDecay = 0.004f, CrackHighpass = 2000f,
                Body = 1f, BodyDecay = 0.09f, BodyLowpass = 2500f,
                Thump = 0.9f, ThumpFrom = 140f, ThumpTo = 45f, ThumpDecay = 0.12f,
                Tail = 0.35f, TailDecay = 0.4f, TailLowpass = 900f,
                Reverb = 0.25f, Duration = 0.7f, Peak = 0.85f,
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
            SetGun(Sfx.ShotWisp, 0.1f, new Gun
            {
                Crack = 0.2f, CrackDecay = 0.0012f, CrackHighpass = 2500f,
                Body = 0.6f, BodyDecay = 0.02f, BodyLowpass = 2500f,
                Thump = 0.35f, ThumpFrom = 260f, ThumpTo = 120f, ThumpDecay = 0.025f,
                Tail = 0.04f, TailDecay = 0.06f, TailLowpass = 1000f,
                Reverb = 0.1f, Duration = 0.16f, Peak = 0.35f,
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
            Set(Sfx.SpellHoly, Notes(new[] { 1047f, 1319f }, 0.06f, Osc.Sine, 0.18f, 1f), 0.1f);
            Set(Sfx.Throw, Tone(0.1f, Osc.Sine, 400f, 200f, 0.16f, noise: 0.6f, lowpass: 0.3f), 0.07f);
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
