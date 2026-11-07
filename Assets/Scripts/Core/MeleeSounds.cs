using System;
using System.Collections.Generic;

namespace Arena
{
    /// Sounds for the melee rushers (Swarmer, Spiker, Brute, Lunger, Buzzsaw, Spike Block), from Docs/Mockups/melee-enemy-sounds.html.
    /// They're made on every machine from the enemies' replicated state (type, position, Phase), so nothing is sent.
    ///   - Distance: a cue gets `SoundCue.Quiet` from `QuietAt`: full volume inside `EarNear`, easing down to `EarFloor` of it at `EarFar`.
    ///     Enemy shots use it too.
    ///   - Buzzsaw: no tick per touch. One whirr loops while a saw touches someone (`SawWhirrLevel`, 0..1; the front end plays it).
    ///   - Deaths: each type has its own. Several of the same type dying together (within `DeathWindow`) make one sound, the nearest one's.
    ///   - Min gaps in SoundBank still cap how often one cue can play.
    public partial class Game
    {
        public const float EarNear = 260f, EarFar = 1100f;  // inside the first: full volume; beyond the second: the floor
        public const float EarFloor = 0.12f;                // the share of its volume a far cue keeps
        public const float DeathWindow = 0.15f;             // same-type deaths this close together share one sound
        const float BiteEvery = 0.45f, StepEvery = 0.3f, RockEvery = 0.5f;

        /// How loud the Buzzsaw's contact whirr should be right now, 0 (silent) to 1. Read by the front end each frame.
        public float SawWhirrLevel;

        class MeleeVoice
        {
            public EnemyType Type;
            public V2 Pos, Last;
            public byte Phase;
            public float BiteT, StepT;
            public bool Seen, Alerted;
        }

        readonly Dictionary<int, MeleeVoice> meleeVoices = new Dictionary<int, MeleeVoice>();
        readonly Dictionary<EnemyType, float> lastMeleeDeath = new Dictionary<EnemyType, float>();
        readonly Dictionary<EnemyType, float> deathQuiet = new Dictionary<EnemyType, float>();
        readonly List<int> goneVoices = new List<int>();
        float meleeClock;

        void ResetMeleeSounds()
        {
            meleeVoices.Clear();
            lastMeleeDeath.Clear();
            SawWhirrLevel = 0f;
            meleeClock = 0f;
        }

        /// Where you are listening from: your own player, or the nearest one if you're not in the game. False if nobody is.
        bool ListenerPos(out V2 ear)
        {
            Player me;
            if (Players.TryGetValue(MyId, out me))
            {
                ear = me.Pos;
                return true;
            }
            ear = default(V2);
            bool any = false;
            foreach (var p in Players.Values)
            {
                ear = p.Pos;
                any = true;
                break;
            }
            return any;
        }

        /// How much quieter a sound at `pos` is for being far from you: 0 near, up to 1 - EarFloor far away.
        public float QuietAt(V2 pos)
        {
            V2 ear;
            if (!ListenerPos(out ear))
                return 0f;
            float d = ear.DistanceTo(pos);
            if (d <= EarNear)
                return 0f;
            float t = Math.Min(1f, (d - EarNear) / (EarFar - EarNear));
            t = t * t * (3f - 2f * t);  // smoothstep: no sudden steps as an enemy walks by
            return (1f - EarFloor) * t;
        }

        static bool IsMeleeVoice(EnemyType t)
        {
            return t == EnemyType.Swarmer || t == EnemyType.Spiker || t == EnemyType.Brute || t == EnemyType.Lunger
                || t == EnemyType.Buzzsaw || t == EnemyType.SpikeBlock;
        }

        void MeleeCue(Sfx s, float quiet)
        {
            if (quiet < 0.97f)
                Sounds.Add(new SoundCue { Sfx = s, Quiet = quiet });
        }

        bool TouchingPlayer(Enemy e)
        {
            foreach (var p in Players.Values)
                if (!p.Downed && p.Pos.DistanceTo(e.Pos) < e.Radius + p.Def.HitR + 2f)
                    return true;
            return false;
        }

        static Sfx DeathSound(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Swarmer: return Sfx.SwarmerDie;
                case EnemyType.Spiker: return Sfx.SpikerDie;
                case EnemyType.Brute: return Sfx.BruteDie;
                case EnemyType.Lunger: return Sfx.LungerDie;
                case EnemyType.Buzzsaw: return Sfx.SawDie;
                default: return Sfx.SpikeBlockDie;
            }
        }

        void UpdateMeleeSounds(float dt)
        {
            if (Enemies.Count == 0 && meleeVoices.Count == 0 && SawWhirrLevel <= 0f)
                return;
            meleeClock += dt;
            float sawTarget = 0f;
            foreach (var m in meleeVoices.Values)
                m.Seen = false;

            foreach (var e in Enemies.Values)
            {
                if (!IsMeleeVoice(e.Type))
                    continue;
                MeleeVoice m;
                bool fresh = !meleeVoices.TryGetValue(e.Id, out m);
                if (fresh)
                {
                    m = new MeleeVoice { Type = e.Type, Phase = e.Phase, Last = e.Pos, StepT = StepEvery * 0.5f };
                    meleeVoices[e.Id] = m;
                }
                m.Seen = true;
                m.Pos = e.Pos;
                m.BiteT -= dt;
                m.StepT -= dt;
                float q = QuietAt(e.Pos);
                bool moved = e.Pos.DistanceTo(m.Last) > 0.3f;
                m.Last = e.Pos;
                bool touching = !fresh && TouchingPlayer(e);
                bool bite = touching && m.BiteT <= 0f;
                if (bite)
                    m.BiteT = BiteEvery;
                bool changed = !fresh && e.Phase != m.Phase;

                switch (e.Type)
                {
                    case EnemyType.Swarmer:
                        if (bite)
                            MeleeCue(Sfx.SwarmerChomp, q);
                        break;
                    case EnemyType.Spiker:
                        if (changed)
                            MeleeCue(Sfx.SpikerFlip, q);
                        if (bite)
                            MeleeCue(Sfx.SpikerStab, q);
                        break;
                    case EnemyType.Brute:
                        if (!fresh && !m.Alerted && q <= 0.2f)
                        {
                            m.Alerted = true;  // one bark when it first comes within earshot
                            MeleeCue(Sfx.BruteGrunt, q);
                        }
                        if (moved && m.StepT <= 0f)
                        {
                            m.StepT = StepEvery;
                            MeleeCue(Sfx.BruteStep, q);
                        }
                        if (bite)
                            MeleeCue(Sfx.BruteBash, q);
                        break;
                    case EnemyType.Lunger:  // Phase 1 is the pink line, 2 the leap, 3 the recovery
                        if (changed && e.Phase == 1)
                            MeleeCue(Sfx.LungerTell, q);
                        else if (changed && e.Phase == 2)
                            MeleeCue(Sfx.LungerLeap, q);
                        else if (changed && e.Phase == 3)
                            MeleeCue(Sfx.LungerLand, q);
                        break;
                    case EnemyType.Buzzsaw:  // no ticks: one whirr while it touches someone
                        if (touching)
                            sawTarget = Math.Max(sawTarget, 1f - q);
                        break;
                    case EnemyType.SpikeBlock:  // 0 rocks in, 1 shakes (the tell), 2 lunges, 3 rests
                        if (changed && e.Phase == 1)
                            MeleeCue(Sfx.SpikeBlockShake, q);
                        else if (changed && e.Phase == 2)
                            MeleeCue(Sfx.SpikeBlockLunge, q);
                        else if (changed && e.Phase == 3)
                            MeleeCue(Sfx.SpikeBlockRest, q);
                        else if (e.Phase == 0 && moved && m.StepT <= 0f)
                        {
                            m.StepT = RockEvery;
                            MeleeCue(Sfx.SpikeBlockRock, q);
                        }
                        break;
                }
                m.Phase = e.Phase;
            }

            // Whirr: rises quickly when a saw touches you, fades out over about a third of a second.
            float rate = sawTarget > SawWhirrLevel ? 8f : 3f;
            SawWhirrLevel = SawWhirrLevel < sawTarget
                ? Math.Min(sawTarget, SawWhirrLevel + rate * dt)
                : Math.Max(sawTarget, SawWhirrLevel - rate * dt);

            // Anything that was here and now isn't has died. One sound per type per DeathWindow, at the nearest one's volume.
            goneVoices.Clear();
            deathQuiet.Clear();
            foreach (var kv in meleeVoices)
            {
                if (kv.Value.Seen)
                    continue;
                goneVoices.Add(kv.Key);
                float q = QuietAt(kv.Value.Pos), best;
                if (!deathQuiet.TryGetValue(kv.Value.Type, out best) || q < best)
                    deathQuiet[kv.Value.Type] = q;
            }
            foreach (int id in goneVoices)
                meleeVoices.Remove(id);
            foreach (var kv in deathQuiet)
            {
                float last;
                if (lastMeleeDeath.TryGetValue(kv.Key, out last) && meleeClock - last < DeathWindow)
                    continue;
                lastMeleeDeath[kv.Key] = meleeClock;
                MeleeCue(DeathSound(kv.Key), kv.Value);
            }
        }
    }
}
