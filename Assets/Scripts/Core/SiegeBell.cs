using System;
using System.Collections.Generic;

namespace Arena
{
    /// Siege Bell, the Tank Knight's 4th choice (`AbilityId.SiegeBell`, `AbilityDef.Fourth`: the lobby's 4th ability key), look C
    /// "Cathedral" of the Siege Bell mock-up (claude.ai artifact "Siege Bell"). A ghostly bell comes down 50 px ahead of you and
    /// rings three times while you stand braced (rooted, 25% less damage). The first two tolls send a 120 px ring: 8 damage
    /// and a 0.2 s flinch that breaks a wind-up. The third throws a shockwave out to 260 px over 0.45 s: 20 damage, a 24 px
    /// shove and a 2.5 s stun (heavy enemies 1.5 s, bosses 0.8 s). Mastered: a 3.5 s stun, and the wave erases the enemy
    /// shots it passes.
    ///
    /// The host keeps each bell (`bells`, ticked by `TickBells` from TickKnights): the tolls, the wave's front and who it hit.
    /// Every screen draws it from one effect (Game/SiegeBellLook.cs): `FxKind.SiegeBell` (Pos = the bell, Pos2 = (the caster's
    /// id, the stun), Radius = the wave's reach); its sound is Sfx.SiegeBell, the three tolls in one take (SiegeBellSoundBank.cs).
    /// The caster's own game roots them from that effect (`BellRooted`), so no snapshot field is needed.
    public partial class Game
    {
        public static readonly float[] BellTolls = { 0.30f, 0.80f, 1.30f };  // the tolls, from the cast
        public const float BellAhead = 50f;            // the bell comes down this far ahead of you
        public const float BellRing = 120f, BellRingT = 0.55f;  // tolls 1 and 2: the ring's reach and how long it runs out
        public const float BellRadius = 260f, BellWave = 0.45f;  // toll 3: the shockwave's reach and how long it takes to get there
        public const float BellRoot = 1.40f;           // you stand braced until just after the third toll
        public const float BellDmgCut = 0.25f;            //   and take this much less damage
        public const float BellStun = 2.5f, BellStunMastered = 3.5f, BellStunHeavy = 1.5f, BellStunBoss = 0.8f;
        public const float BellCooldown = 22f;
        public const float BellLinger = 0.2f;          // the effect outlives the longest stun by this much
        public const float BellShove = 24f;            // the shockwave's shove
        const float BellTollDamage = 8f, BellFlinch = 0.2f, BellWaveDamage = 20f;

        class Bell
        {
            public int Owner;
            public V2 Pos;
            public float R, T, Stun, Power;
            public bool Mastered;
            public int Tolled;  // how many of the tolls have rung
            public readonly HashSet<int> Hit = new HashSet<int>();
        }
        readonly List<Bell> bells = new List<Bell>();
        readonly List<int> bellShots = new List<int>();

        /// The shockwave's reach for this player (Great Bell: +25% a stack).
        public static float BellRadiusOf(Player p) => BellRadius * p.Stats.BellRadiusMult;

        /// How far the shockwave has run `since` the cast, in a bell of reach `r` (-1 before the third toll).
        public static float BellFrontAt(float since, float r)
        {
            float a = since - BellTolls[2];
            if (a < 0f)
                return -1f;
            float k = Math.Min(1f, a / BellWave);
            return r * (1f - (1f - k) * (1f - k) * (1f - k));
        }

        /// When the shockwave reaches `d` px out, counted from the cast (the inverse of BellFrontAt).
        public static float BellReachT(float d, float r) =>
            BellTolls[2] + BellWave * (1f - (float)Math.Pow(Math.Max(0f, 1f - Math.Min(1f, d / r)), 1.0 / 3.0));

        /// The effect's life for a stun of `stun` s.
        public static float BellFxDur(float stun) => BellTolls[2] + BellWave + stun + BellLinger;

        /// Host: Siege Bell cast.
        void CastSiegeBell(Player p, float power, bool mastered)
        {
            V2 at = ClampToArena(p.Pos + V2.FromAngle(p.Aim) * BellAhead, 20f);
            float stun = (mastered ? BellStunMastered : BellStun) + p.Stats.BellStunBonus;
            var b = new Bell { Owner = p.Id, Pos = at, R = BellRadiusOf(p), Stun = stun, Power = power, Mastered = mastered };
            bells.Add(b);
            BroadcastFx(FxKind.SiegeBell, at, new V2(p.Id, stun), new Rgba(0.5f, 0.94f, 0.88f), b.R, BellFxDur(stun), Sfx.SiegeBell);
        }

        /// Is `p` braced under a bell of their own (rooted, the damage cut)? Read from the effect, so the caster's own game,
        /// the host and every other screen agree without a snapshot field.
        public bool BellRooted(Player p)
        {
            foreach (var fx in Effects)
                if (fx.Kind == FxKind.SiegeBell && fx.T < BellRoot && (int)fx.Pos2.X == p.Id)
                    return true;
            return false;
        }

        /// Host: the share of a hit a braced caster is spared (Combat.cs Damage).
        float BellCut(Player p)
        {
            foreach (var b in bells)
                if (b.Owner == p.Id && b.T < BellRoot)
                    return BellDmgCut;
            return 0f;
        }

        /// Host, every tick: the tolls ring, the shockwave strikes what it reaches, bells end.
        void TickBells(float dt)
        {
            for (int i = bells.Count - 1; i >= 0; i--)
            {
                var b = bells[i];
                float before = b.T;
                b.T += dt;
                if (b.T > BellTolls[2] + BellWave + 0.1f)
                {
                    bells.RemoveAt(i);
                    continue;
                }
                Player p;
                if (!Players.TryGetValue(b.Owner, out p))
                    continue;
                // Tolls 1 and 2: a ring out to 120 px (the hits land as the toll rings: the ring is quick and short).
                while (b.Tolled < 2 && b.T >= BellTolls[b.Tolled])
                {
                    foreach (var e in Enemies.Values)
                    {
                        if (!Targetable(e) || e.Pos.DistanceTo(b.Pos) > BellRing + e.Radius * 0.5f)
                            continue;
                        HurtEnemy(e, BellTollDamage * p.Stats.DmgMult * b.Power, p.Id);
                        Stun(e, BellFlinch);  // a flinch: breaks a wind-up (Stun clears nothing else)
                        e.Winding = false;
                        castHits++;
                    }
                    b.Tolled++;
                }
                // Toll 3: the shockwave.
                float front = BellFrontAt(b.T, b.R);
                if (front < 0f)
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || b.Hit.Contains(e.Id))
                        continue;
                    float d = e.Pos.DistanceTo(b.Pos);
                    if (d > b.R + e.Radius * 0.5f || d - e.Radius * 0.5f > front)
                        continue;
                    b.Hit.Add(e.Id);
                    HurtEnemy(e, BellWaveDamage * p.Stats.DmgMult * b.Power, p.Id);
                    float stun = e.Def.IsBoss ? BellStunBoss : e.Def.Heavy ? BellStunHeavy : b.Stun;
                    Stun(e, stun);  // Daze.cs: held, with the daze stars over its head
                    e.Winding = false;
                    if (!e.Def.IsBoss)
                        ShoveFrom(e, b.Pos, BellShove);
                    castHits++;
                }
                if (b.Mastered && before < BellTolls[2] + BellWave)  // mastered: the wave erases the enemy shots it passes
                {
                    bellShots.Clear();
                    foreach (var s in Bullets.Values)
                        if (Defs.IsEnemyShot(s.Kind) && !Defs.Unblockable(s.Kind) && s.Pos.DistanceTo(b.Pos) <= front + s.Radius)
                            bellShots.Add(s.Id);
                    foreach (int id in bellShots)
                    {
                        Bullet s;
                        if (Bullets.TryGetValue(id, out s))
                            BroadcastFx(FxKind.Spark, s.Pos, new Rgba(0.75f, 1f, 0.95f), 6f, 0.2f, Sfx.None);
                        KillBullet(id);
                    }
                }
            }
        }
    }
}
