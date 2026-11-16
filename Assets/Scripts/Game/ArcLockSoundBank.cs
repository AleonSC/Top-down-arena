using System;
using UnityEngine;

namespace Arena
{
    /// Arc Lightning's lock-on sounds (Core/ArcLock.cs), ported from the mockup's synth: a charge hum that rises with every tag (a loop,
    /// your own draw only), an electric crackle and a low zap per tag climbing a step each time, a thunder crack when you let go, a
    /// snap per hop rising along the chain, a thin zap per Forked Volley bolt and a fizzle when nothing was tagged. They play from the
    /// effects the host sends (FxKind.ArcTag, ArcLink, ArcFork; Effect.Heard), at the Weapons volume; someone else's are quieter.
    public partial class SoundBank
    {
        AudioClip arcTag, arcHop, arcCrack, arcFork, arcFizzle;
        AudioSource arcHumVoice;
        float arcTagLast = -10f, arcHopLast = -10f, arcCrackLast = -10f, arcForkLast = -10f, arcFizzleLast = -10f;

        void BuildArcLock(GameObject owner)
        {
            const Osc S = Osc.Sine;
            // A tag: a burst of high crackle, a second ping of it, and a low sawtooth zap falling away.
            arcTag = Clip("arc_tag", Finish(Mix(
                Tone(0.09f, S, 3200f, 3000f, 0.35f, noise: 1f, lowpass: 0.95f, decay: 2.5f),
                Delay(Tone(0.05f, S, 1800f, 1700f, 0.2f, noise: 0.85f, lowpass: 0.6f, decay: 3f), 0.03f),
                Tone(0.16f, Osc.Saw, 160f, 90f, 0.12f, lowpass: 0.3f, decay: 2f)), 0.42f));
            // A hop: a snap of filtered noise with a falling buzz.
            arcHop = Clip("arc_hop", Finish(Mix(
                Tone(0.09f, S, 1300f, 1100f, 0.35f, noise: 0.95f, lowpass: 0.55f, decay: 3f),
                Tone(0.07f, Osc.Saw, 1900f, 240f, 0.08f, lowpass: 0.5f, decay: 2.2f)), 0.4f));
            // Letting go: a white crack, a rolling rumble under it and a low drop.
            arcCrack = Clip("arc_crack", Finish(Room(Mix(
                Tone(0.06f, S, 4200f, 3800f, 0.45f, noise: 1f, lowpass: 1f, decay: 3f),
                Tone(0.7f, S, 900f, 120f, 0.5f, noise: 1f, lowpass: 0.08f, attack: 0.01f, decay: 1.6f),
                Tone(0.35f, S, 120f, 40f, 0.3f, decay: 2f)), 0.2f), 0.6f));
            // A Forked Volley bolt: thinner and brighter than a hop.
            arcFork = Clip("arc_fork", Finish(Mix(
                Tone(0.07f, S, 2200f, 1800f, 0.3f, noise: 0.9f, lowpass: 0.7f, decay: 3f),
                Tone(0.08f, Osc.Saw, 2600f, 600f, 0.07f, lowpass: 0.6f, decay: 2.4f)), 0.34f));
            // Nothing tagged: a short dull fizz.
            arcFizzle = Clip("arc_fizzle", Finish(Tone(0.14f, S, 700f, 300f, 0.3f, noise: 0.95f, lowpass: 0.25f, decay: 2f), 0.2f));
            // The charge hum: two detuned saws near 55 Hz through a low-pass, throbbing 10 times a second. Whole cycles in the
            // 0.6 s loop (33 and 34 of the saws, 66 of the octave, 6 throbs) so it joins up without a click.
            const float len = 0.6f;
            var hum = Tremolo(Mix(
                Tone(len, Osc.Saw, 55f, 55f, 0.5f, lowpass: 0.06f, attack: 0.0005f, decay: 0f),
                Tone(len, Osc.Saw, 56.67f, 56.67f, 0.5f, lowpass: 0.06f, attack: 0.0005f, decay: 0f),
                Tone(len, S, 110f, 110f, 0.2f, attack: 0.0005f, decay: 0f)), 10f, 0.4f);
            float max = 0f;
            foreach (float v in hum)
                max = Math.Max(max, Math.Abs(v));
            for (int i = 0; i < hum.Length && max > 0f; i++)
                hum[i] *= 0.2f / max;
            arcHumVoice = owner.AddComponent<AudioSource>();
            arcHumVoice.playOnAwake = false;
            arcHumVoice.spatialBlend = 0f;
            arcHumVoice.loop = true;
            arcHumVoice.clip = Clip("arc_hum", hum);
        }

        void PlayArc(AudioClip clip, float now, ref float last, float gap, float volume, float pitch)
        {
            if (Muted || clip == null || now - last < gap)
                return;
            volume *= Master * Weapons;
            if (volume <= 0.001f)
                return;
            last = now;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = pitch * (1f + ((float)rng.NextDouble() - 0.5f) * 0.03f);
            voice.PlayOneShot(clip, volume);
        }

        /// Tag number `index` (from 0): each a step higher (a semitone), so ten tags climb.
        public void PlayArcTag(float now, int index, bool mine) => PlayArc(arcTag, now, ref arcTagLast, 0.03f, mine ? 1f : 0.45f, Mathf.Pow(2f, Mathf.Clamp(index, 0, 15) / 12f));

        /// A hop of the chain: the first carries the crack of letting go; each later one snaps a little higher.
        public void PlayArcHop(float now, int index, bool mine)
        {
            float v = mine ? 1f : 0.45f;
            if (index <= 0)
                PlayArc(arcCrack, now, ref arcCrackLast, 0.1f, v, 1f);
            PlayArc(arcHop, now, ref arcHopLast, 0.025f, v * 0.9f, 1f + 0.06f * Mathf.Clamp(index, 0, 15));
        }

        public void PlayArcFork(float now, int bolt, bool mine) => PlayArc(arcFork, now, ref arcForkLast, 0.03f, mine ? 0.9f : 0.4f, 1f + 0.08f * bolt);

        public void PlayArcFizzle(float now, bool mine) => PlayArc(arcFizzle, now, ref arcFizzleLast, 0.1f, mine ? 0.8f : 0.35f, 1f);

        /// The charge hum: `level` 0 (off) to 1, rising in pitch and getting brighter with each tag.
        public void SetArcHum(float level, int tags)
        {
            if (arcHumVoice == null)
                return;
            float v = Muted ? 0f : level * Master * Weapons;
            if (v <= 0.002f)
            {
                if (arcHumVoice.isPlaying)
                    arcHumVoice.Stop();
                return;
            }
            arcHumVoice.volume = Mathf.Min(1f, v * (1f + 0.05f * tags));
            arcHumVoice.pitch = 1f + 0.09f * tags;
            if (!arcHumVoice.isPlaying)
                arcHumVoice.Play();
        }
    }

    public partial class ArenaApp
    {
        /// Every frame (sound on whatever Visual effects say): the lock-on chain's one-shots from the effects as they arrive, and the hum
        /// while your own draw is held.
        void TickArcLockSounds()
        {
            float now = Time.unscaledTime;
            foreach (var fx in game.Effects)
            {
                if (fx.Heard)
                    continue;
                fx.Heard = true;
                if (fx.Kind == FxKind.ArcTag)
                    sounds.PlayArcTag(now, (int)fx.Radius, (int)fx.Pos2.X == game.MyId);
                else if (fx.Kind == FxKind.ArcLink || fx.Kind == FxKind.ArcFork)
                {
                    var me = game.Me;
                    bool mine = me != null && fx.Pos.DistanceTo(me.Pos) < me.Def.BodyR + 40f;  // it left from your staff
                    if (fx.Kind == FxKind.ArcFork)
                        sounds.PlayArcFork(now, fx.Radius < -1f ? 0 : fx.Radius > 1f ? 2 : 1, mine);
                    else if (fx.Radius < 0f)
                        sounds.PlayArcFizzle(now, mine);
                    else
                        sounds.PlayArcHop(now, (int)fx.Radius, mine || fx.Radius > 0.5f);  // (later hops are far from anyone's staff: full volume)
                }
            }
            var p = game.Me;
            bool humming = p != null && p.Weapon != null && p.Weapon.Id == WeaponId.ArcLightning && p.DrawT > 0f && !p.Downed;
            sounds.SetArcHum(humming ? 1f : 0f, humming ? p.ArcTags.Count : 0);
        }
    }
}
