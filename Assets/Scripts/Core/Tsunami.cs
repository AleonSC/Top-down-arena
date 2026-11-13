using System;
using System.Collections.Generic;

namespace Arena
{
    /// Tsunami (Elementalist DPS ability, took Arcane Surge's place): a wave rolls out from you along your aim. It barely hurts
    /// (12), but everything it crosses is WET for 8 s: slowed 30% (bosses and heavy enemies 15%), and any lightning hit
    /// anywhere on the map arcs to every wet enemy for 40% of that hit's damage (Arc Lightning, Storm Hammer pulses, Chain Light,
    /// the Nimbus). A wet enemy can be arced at most once every WetArcEvery seconds, so chained pulses don't become a laser.
    /// Mastered: wet lasts 14 s, and the wave rolls back to you, wetting whatever it missed (no damage on the way back).
    /// The wave is host logic; clients get one FxKind.Tsunami (Pos = where it starts, Pos2 = where it ends, Radius = half its
    /// width) and animate it from that. Enemy.Wet is replicated for the drips. Look: Game/TsunamiLook.cs.
    /// Mockup: Docs/Mockups/elementalist-dps-abilities.html (look A, Crest).
    public partial class Game
    {
        public const float TsunamiHalfWidth = 170f, TsunamiThick = 70f, TsunamiSpeed = 520f, TsunamiRange = 1000f, TsunamiDamage = 12f;
        public const float WetTime = 8f, WetTimeMastered = 14f, WetSlow = 0.3f, WetSlowHeavy = 0.15f;
        public const float WetArcShare = 0.4f, WetArcEvery = 0.4f;

        class SeaWave
        {
            public int Owner;
            public V2 Origin, Dir;
            public float Dist, Dmg, WetFor, HalfWidth = TsunamiHalfWidth;  // Riptide: wider
            public bool ComeBack;  // mastered: roll back from where it ends
            public HashSet<int> Hit = new HashSet<int>();
        }

        readonly List<SeaWave> waves = new List<SeaWave>();

        void CastTsunami(Player p, bool mastered, float power)
        {
            LaunchWave(p.Id, p.Pos, V2.FromAngle(p.Aim), TsunamiDamage * p.Stats.DmgMult * power, (mastered ? WetTimeMastered : WetTime) * power + p.Stats.WetBonusTime,
                mastered, Sfx.Freeze, TsunamiHalfWidth * p.Stats.TsunamiWidthMult);
        }

        void LaunchWave(int owner, V2 origin, V2 dir, float dmg, float wetFor, bool comeBack, Sfx sound, float halfWidth)
        {
            waves.Add(new SeaWave { Owner = owner, Origin = origin, Dir = dir, Dmg = dmg, WetFor = wetFor, ComeBack = comeBack, HalfWidth = halfWidth });
            BroadcastFx(FxKind.Tsunami, origin, origin + dir * TsunamiRange, new Rgba(0.31f, 0.82f, 0.91f), halfWidth, TsunamiRange / TsunamiSpeed + 0.35f, sound);
        }

        /// Host, every tick: move the waves, wet and nick what they cross, and send a mastered one back.
        void TickTsunami(float dt)
        {
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                float from = w.Dist;
                w.Dist = Math.Min(TsunamiRange + TsunamiThick, w.Dist + TsunamiSpeed * dt);
                V2 side = w.Dir.Orthogonal();
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || w.Hit.Contains(e.Id))
                        continue;
                    V2 rel = e.Pos - w.Origin;
                    float along = Dot(rel, w.Dir), across = Dot(rel, side);
                    if (Math.Abs(across) > w.HalfWidth + e.Radius || along > w.Dist + e.Radius || along < from - TsunamiThick - e.Radius)
                        continue;
                    w.Hit.Add(e.Id);
                    e.WetT = Math.Max(e.WetT, w.WetFor);
                    if (w.Dmg > 0f)
                        HurtEnemy(e, w.Dmg, w.Owner);
                }
                if (w.Dist < TsunamiRange + TsunamiThick)
                    continue;
                waves.RemoveAt(i);
                if (w.ComeBack)  // the second pass wets only (no damage) and doesn't come back again
                    LaunchWave(w.Owner, w.Origin + w.Dir * TsunamiRange, -w.Dir, 0f, w.WetFor, false, Sfx.None, w.HalfWidth);
            }
        }

        /// Host, every tick per enemy: wet wears off, and while it lasts it slows.
        void TickWet(Enemy e, float dt)
        {
            e.WetArcCd -= dt;
            if (e.WetT <= 0f)
                return;
            e.WetT = Math.Max(0f, e.WetT - dt);
            Slow(e, 0.2f, 1f - (Heavy(e) ? WetSlowHeavy : WetSlow));
        }

        /// A lightning hit on struck (at where it landed): a bolt jumps from there to every other wet enemy on the map.
        /// dmg is the hit's own damage; each arc does WetArcShare of it. Called by every lightning source (see the class comment).
        void WetArcs(V2 at, Enemy struck, float dmg, int owner)
        {
            if (dmg <= 0f)
                return;
            List<Enemy> targets = null;
            foreach (var e in Enemies.Values)
                if (e != struck && e.WetT > 0f && e.WetArcCd <= 0f && Targetable(e))
                    (targets ?? (targets = new List<Enemy>())).Add(e);
            if (targets == null)
                return;
            var bolt = Defs.BulletColor(Defs.BoltShot);
            foreach (var e in targets)
            {
                e.WetArcCd = WetArcEvery;
                BroadcastFx(FxKind.Bolt, at, e.Pos, bolt, 0f, 0.2f, Sfx.None);
                HurtEnemy(e, dmg * WetArcShare, owner);
            }
        }
    }
}
