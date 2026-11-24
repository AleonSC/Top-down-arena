using System;
using System.Collections.Generic;

namespace Arena
{
    /// Shockwave (the universal very rare ability), look B "Kinetic Burst" of the Shockwave Rework mockup. The cast sends one
    /// `FxKind.Shockwave` and starts a wave at the caster's feet: its front runs out to ShockwaveRange (ShockwaveCapRange mastered)
    /// in ShockwaveSweep s, fast then slowing (`ShockwaveR`). Enemy bullets vanish as the front reaches them; each enemy it reaches
    /// takes ShockwaveDamage once and is shoved ShockwaveKnock px straight away from where it was cast (Heavies ShockwaveHeavyKnock,
    /// bosses not at all). Every screen draws the wave from the effect's age (Game/ShockwaveLook.cs).
    public partial class Game
    {
        public const float ShockwaveSweep = 0.42f, ShockwaveKnock = 120f, ShockwaveHeavyKnock = 50f, ShockwaveCapRange = 300f;
        public const float ShockwaveFxTime = 1.6f;  // the wave, then the scorch and dust settling
        public static readonly Rgba ShockwaveColor = new Rgba(1f, 0.64f, 0.36f);

        class ShockRun
        {
            public V2 Pos;
            public int Owner;
            public float Age, Range, Dmg;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }
        readonly List<ShockRun> shockRuns = new List<ShockRun>();

        /// How far a wave reaching `range` has gone `age` seconds after the cast (ease out).
        public static float ShockwaveR(float age, float range)
        {
            if (age <= 0f)
                return 0f;
            float u = Math.Min(1f, age / ShockwaveSweep);
            return range * (1f - (float)Math.Pow(1f - u, 3f));
        }

        /// The cast (inside BeginCast/EndCast): the wave's hits land after the cast is over, so its mastery power goes into
        /// its damage now, and its mastery XP counts what is in reach now (each enemy, and a hit per 5 enemy shots).
        void CastShockwave(Player p, bool cap, float dmg)
        {
            float range = cap ? ShockwaveCapRange : ShockwaveRange;
            shockRuns.Add(new ShockRun { Pos = p.Pos, Owner = p.Id, Range = range, Dmg = dmg * castPower });
            BroadcastFx(FxKind.Shockwave, p.Pos, new V2(p.Id, 0f), ShockwaveColor, range, ShockwaveFxTime, Sfx.Shockwave);
            int shots = 0;
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && b.Pos.CloserThan(p.Pos, range))
                    shots++;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.DistanceTo(p.Pos) - e.Radius <= range)
                    castHits++;
            castHits += (shots + 4) / 5;
        }

        /// Host, every tick: the fronts move out, erasing bullets and hitting what they reach.
        void TickShockwaves(float dt)
        {
            for (int i = shockRuns.Count - 1; i >= 0; i--)
            {
                var w = shockRuns[i];
                w.Age += dt;
                TickShockRun(w);
                if (w.Age >= ShockwaveSweep + 0.05f)
                    shockRuns.RemoveAt(i);
            }
        }

        void TickShockRun(ShockRun w)
        {
            float r = Math.Max(24f, ShockwaveR(w.Age, w.Range));
            EraseEnemyBullets(w.Pos, r);
            var snap7 = SnapFoes(w.Owner);
            foreach (var e in snap7)
            {
                if (!Targetable(e) || w.Hit.Contains(e.Id))
                    continue;
                float d = e.Pos.DistanceTo(w.Pos);
                if (d - e.Radius > r)
                    continue;
                w.Hit.Add(e.Id);
                string was = CreditTo("ability:" + Upgrades.Ability(AbilityId.Shockwave).Name);  // (the session recording)
                HurtEnemy(e, w.Dmg, w.Owner);
                dmgSrc = was;
                if (e.Hp <= 0f || e.Def.IsBoss)
                    continue;
                V2 away = (e.Pos - w.Pos).Normalized();
                if (away.IsZero)
                    away = RandDir();
                KnockEnemy(e, away * (Heavy(e) ? ShockwaveHeavyKnock : ShockwaveKnock));  // slides (Knock.cs)
            }
            Free(snap7);
        }
    }
}
