using System;
using System.Collections.Generic;

namespace Arena
{
    /// Blood Freeze (Healer Hemomancer ability, replaces Siphon Field; `AbilityId.SiphonField` kept), from Docs/Mockups/blood-freeze.html.
    /// You pay 10% of your max health, a rune circle spins up at your feet for FreezeCharge s, then a shockwave tears across the whole
    /// arena (FreezeSweep s, slowing as it goes, `FreezeWaveR`). Every enemy it reaches is frozen (`Enemy.FrozenT`: no moving, no shooting)
    /// for FreezeTime s, a boss for FreezeBossTime s. Enemies still spawning, training dummies and anything that turns up after the wave
    /// has passed are skipped, and allies are untouched. It does no damage; mastered, frozen enemies take FreezeVuln times the damage
    /// (`Enemy.IceT` and `IceMult`, applied in `HurtEnemy`). Hungry Earth adds `Stats.FreezeBonusTime` seconds.
    /// The cast sends one `FxKind.BloodFreeze`; every screen draws the whole show from its age (Game/BloodFreezeLook.cs).
    public partial class Game
    {
        public const float FreezeCharge = 0.55f, FreezeSweep = 1.1f, FreezeMaxR = 1950f;
        public const float FreezeTime = 5f, FreezeBossTime = 3f, FreezeThaw = 0.6f, FreezeVuln = 1.25f;

        class FreezeWave
        {
            public V2 Pos;
            public float Age, Bonus;
            public bool Cap;
            public HashSet<int> Hit = new HashSet<int>();
        }
        readonly List<FreezeWave> freezeWaves = new List<FreezeWave>();

        /// How far the wave has gone `age` seconds after the cast: nothing while charging, then fast and slowing (ease out).
        public static float FreezeWaveR(float age)
        {
            if (age < FreezeCharge)
                return 0f;
            float u = Math.Min(1f, (age - FreezeCharge) / FreezeSweep);
            return FreezeMaxR * (1f - (float)Math.Pow(1f - u, 3f));
        }

        /// The age at which the wave reaches distance `d` (the inverse of `FreezeWaveR`).
        public static float FreezeWaveAt(float d)
        {
            float f = Math.Max(0f, Math.Min(1f, d / FreezeMaxR));
            return FreezeCharge + FreezeSweep * (1f - (float)Math.Pow(1f - f, 1f / 3f));
        }

        /// Seconds a regular enemy stays frozen for a cast with `bonus` extra seconds (a boss: FreezeTime - FreezeBossTime less).
        public static float FreezeHold(bool boss, float bonus) => (boss ? FreezeBossTime : FreezeTime) + bonus;

        /// How long the whole effect lasts: charge, wave, the longest freeze, the thaw.
        public static float FreezeFxTime(float bonus) => FreezeCharge + FreezeSweep + FreezeTime + bonus + FreezeThaw;

        void CastBloodFreeze(Player p, bool cap, float bonus)
        {
            freezeWaves.Add(new FreezeWave { Pos = p.Pos, Bonus = bonus, Cap = cap });
            BroadcastFx(FxKind.BloodFreeze, p.Pos, new V2(p.Id, FreezeTime + bonus), new Rgba(0.88f, 0.16f, 0.29f, cap ? 1f : 0.5f), FreezeMaxR, FreezeFxTime(bonus), Sfx.Freeze);
        }

        /// Host, every tick: the waves move out and freeze what they reach; the ice timers run down.
        void TickBloodFreeze(float dt)
        {
            for (int i = freezeWaves.Count - 1; i >= 0; i--)
            {
                var w = freezeWaves[i];
                w.Age += dt;
                if (w.Age >= FreezeCharge)
                {
                    float r = FreezeWaveR(w.Age);
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Hp <= 0f || e.Dummy || w.Hit.Contains(e.Id) || e.Pos.DistanceTo(w.Pos) > r + e.Radius)
                            continue;
                        w.Hit.Add(e.Id);
                        float hold = FreezeHold(e.Def.IsBoss, w.Bonus);
                        e.FrozenT = Math.Max(e.FrozenT, hold);
                        e.SlowT = Math.Max(e.SlowT, hold);
                        e.IceT = hold;
                        e.IceMult = w.Cap ? FreezeVuln : 1f;
                    }
                }
                if (w.Age >= FreezeCharge + FreezeSweep + 0.1f)
                    freezeWaves.RemoveAt(i);
            }
            foreach (var e in Enemies.Values)
                if (e.IceT > 0f)
                {
                    e.IceT -= dt;
                    if (e.IceT <= 0f)
                    {
                        e.IceT = 0f;
                        e.IceMult = 1f;
                    }
                }
        }
    }
}
