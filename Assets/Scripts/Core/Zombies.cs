using System;
using System.Collections.Generic;

namespace Arena
{
    /// Zombie Infection (a run modifier): every regular wave is a horde of melee enemies (Swarmers, Brutes and
    /// Lungers: no guns), more of them every wave, and the dead don't always stay dead. Bosses, their adds and the
    /// challenge rooms are left as they are. See Docs/ZombieInfection.md (this is its basic version: steps 1 and 2).
    public static class Zombies
    {
        public const float CountStart = 1.5f, CountMax = 3f;  // the wave's usual count times this: x1.5 at wave 1, x3 by wave 20
        public const int CountFullWave = 20;
        public const int Cap = 60;                 // at most this many alive at once; the rest wait to come in
        public const float RiseChance = 0.25f;     // a killed zombie gets back up...
        public const float RiseTime = 3f;          // ...this long after, twitching on the ground first
        public const float RiseHp = 0.5f;          // at this share of its health (and it gives no XP, and won't rise again)

        /// How many more enemies a wave has.
        public static float CountMult(int wave) => CountStart + (CountMax - CountStart) * Math.Max(0f, Math.Min(1f, (wave - 1f) / (CountFullWave - 1f)));

        /// The horde's mix: Swarmers (in pairs) to start, Brutes from wave 3, Lungers from wave 5, the bigger ones
        /// a larger share as the waves go up.
        public static EnemyType Pick(Random rng, int wave)
        {
            float brute = wave >= 3 ? Math.Min(0.35f, 0.15f + 0.01f * wave) : 0f;
            float lunger = wave >= 5 ? Math.Min(0.3f, 0.1f + 0.01f * wave) : 0f;
            double r = rng.NextDouble();
            return r < brute ? EnemyType.Brute : r < brute + lunger ? EnemyType.Lunger : EnemyType.Swarmer;
        }
    }

    public partial class Game
    {
        class Riser { public EnemyType Type; public V2 Pos; public float T; public bool Elite; }
        readonly List<Riser> risers = new List<Riser>();  // host: dead zombies about to get back up

        bool ZombiesOn => Has(RunMod.ZombieInfection) && Practice == null;

        /// Zombies still to get up this wave: it isn't over until they're dealt with.
        public bool ZombiesRising => risers.Count > 0;

        /// Host, building a regular wave: the horde instead of the usual mix (count: the wave's usual count).
        bool BuildZombieWave(int n, int count)
        {
            if (!ZombiesOn)
                return false;
            int total = Math.Max(1, (int)Math.Round(count * Zombies.CountMult(n)));
            while (spawnQueue.Count < total)
            {
                var type = Zombies.Pick(rng, n);
                spawnQueue.Add(type);
                if (type == EnemyType.Swarmer && spawnQueue.Count < total)
                    spawnQueue.Add(type);  // swarmers come in pairs
            }
            return true;
        }

        /// A regular wave's horde comes in faster, so a bigger wave doesn't take longer to arrive; and waits while the
        /// arena already holds the cap. Returns false to hold this spawn back.
        bool ZombieSpawnReady(ref float timer)
        {
            if (!ZombiesOn || Wave % 5 == 0)
                return true;
            if (Enemies.Count >= Zombies.Cap)
                return false;
            timer /= Zombies.CountMult(Wave);
            return true;
        }

        /// XP for a kill: the horde's share is split so levels keep pace with a normal run; a risen zombie gives none.
        float ZombieXpScale(Enemy e)
        {
            if (!ZombiesOn || e.Def.IsBoss)
                return 1f;
            if (e.Risen)
                return 0f;
            return Wave % 5 == 0 ? 1f : 1f / Zombies.CountMult(Wave);
        }

        /// Host, as an enemy dies: a horde zombie may get back up, unless fire, a melee weapon or a boss wave ended it.
        void MaybeRise(Enemy e, Player killer)
        {
            if (!ZombiesOn || State != MatchState.Fighting || InRoom || Wave % 5 == 0 || e.Risen || e.Def.IsBoss || !e.Def.Melee || e.Def.Xp <= 0)
                return;
            if (e.Burning || e.BurnT > 0f || e.BurnPool > 0f)
                return;  // burned: it stays down
            if (killer != null && killer.Weapon.Melee)
                return;  // cut down up close: it stays down
            if (rng.NextDouble() >= Zombies.RiseChance)
                return;
            risers.Add(new Riser { Type = e.Type, Pos = e.Pos, T = Zombies.RiseTime, Elite = e.Elite });
            BroadcastFx(FxKind.Target, e.Pos, new Rgba(0.45f, 0.85f, 0.35f), e.Radius + 14f, Zombies.RiseTime, Sfx.None);
        }

        /// Host, each tick of a fight: the twitching dead get back up.
        void TickRisers(float dt)
        {
            for (int i = risers.Count - 1; i >= 0; i--)
            {
                var r = risers[i];
                r.T -= dt;
                if (r.T > 0f)
                    continue;
                risers.RemoveAt(i);
                var z = SpawnEnemyAt(r.Type, ClampToArena(r.Pos, Defs.Get(r.Type).Radius), 0.5f, 0, r.Elite);
                z.Hp = z.MaxHp * Zombies.RiseHp;
                z.Risen = true;
                BroadcastFx(FxKind.Ring, r.Pos, new Rgba(0.45f, 0.85f, 0.35f), 40f, 0.4f, Sfx.None);
            }
        }

        void ClearRisers() => risers.Clear();
    }
}
