using System;
using System.Collections.Generic;

namespace Arena
{
    /// Decoy Dummy, the Healer Soldier's 4th choice (`AbilityId.DecoyDummy`, `AbilityDef.Fourth`: the lobby's 4th ability key),
    /// from the mockup in session chat (an inflatable soldier). Three charges, DecoyCooldown each; each throws one at your cursor
    /// (up to DecoyRange away):
    ///   a `ZoneKind.DecoyDummy` zone: Pos where it stands, Aim the way it was thrown, Marched how far (both replicated, for the
    ///     canister's flight), BlockHp its health and SoakMax its full health (replicated as GuardFrac), TickT its contact timer;
    ///   it lands after DecoyFly s and puffs up over DecoyInflate s; from then on (DecoyUp) regular enemies within DecoyPull go for
    ///     it and shoot at it instead of you (DecoyTarget, from TickEnemies); bosses and mini-bosses only glance at it for
    ///     DecoyBossGlance s after it goes up;
    ///   enemy shots that touch it stop there and wear it (DecoyStops, from the enemy-bullet loop in Combat.cs), and melee bodies
    ///     wear it every DecoyContactEvery s and can't walk through it (DecoyContact); your team's shots pass;
    ///   at 0 it pops (PopDecoy): a confetti blast shoves regular enemies within DecoyBlast DecoyShove px and stuns every non-boss
    ///     there for DecoyStun s; after DecoyLife s, a 4th throw (DecoyMax per player) or the wave's end it deflates (FoldDecoy).
    /// Health: DecoyHp + DecoyHpPerWave a wave. Mastered: DecoyHpMastered times the health, the blast DecoyBlastMastered.
    /// Its Rare card Double-Ply adds Stats.DecoyHpMult. FxKind.DecoyHit is a shot or a bite landing on it, DecoyPop the pop and
    /// DecoyFold the deflate (Game/DecoyDummyLook.cs draws them and the dummy).
    public partial class Game
    {
        public const float DecoyCooldown = 4f, DecoyRange = 280f, DecoyR = 16f;
        public const float DecoyHp = 40f, DecoyHpPerWave = 4f, DecoyHpMastered = 1.5f;
        public const float DecoyFly = 0.3f, DecoyInflate = 0.4f, DecoyLife = 12f;
        public const float DecoyPull = 240f, DecoyBossGlance = 1f, DecoyContactEvery = 0.5f;
        public const float DecoyBlast = 90f, DecoyBlastMastered = 130f, DecoyShove = 60f, DecoyStun = 0.4f;
        public const int DecoyMax = 3, DecoyCharges = 3;

        /// Seconds since this dummy was thrown.
        public static float DecoyAge(Zone z) => z.Duration - z.T;

        /// Is it up (landed and puffed up), drawing enemies and taking shots?
        public static bool DecoyUp(Zone z) => z.Kind == ZoneKind.DecoyDummy && z.T > 0f && DecoyAge(z) >= DecoyFly + DecoyInflate;

        /// Its full health for a throw on this wave.
        public static float DecoyHealth(int wave, bool mastered) => (DecoyHp + DecoyHpPerWave * Math.Max(0, wave - 1)) * (mastered ? DecoyHpMastered : 1f);

        /// Its health left, 0-1 (the host's own numbers, or the snapshot's GuardFrac on a client).
        public float DecoyFrac(Zone z) => IsHost && z.SoakMax > 0f ? Math.Max(0f, z.BlockHp / z.SoakMax) : z.GuardFrac;

        /// This player's dummies standing (or in the air), oldest first.
        public List<Zone> DecoysOf(int owner)
        {
            var list = new List<Zone>();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.DecoyDummy && z.Owner == owner && z.T > 0f)
                    list.Add(z);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        void CastDecoy(Player p, V2 target, bool cap)
        {
            var mine = DecoysOf(p.Id);
            for (int i = 0; i <= mine.Count - DecoyMax; i++)
                FoldDecoy(mine[i]);  // a 4th deflates the oldest
            V2 to = (target - p.Pos).LimitLength(DecoyRange);
            V2 at = ClampToArena(p.Pos + to, DecoyR);
            var z = AddZone(ZoneKind.DecoyDummy, p, at, DecoyR, DecoyFly + DecoyInflate + DecoyLife);
            z.Aim = to.LengthSq > 1f ? to.Angle : p.Aim;
            z.Marched = (at - p.Pos).Length;
            z.SoakMax = z.BlockHp = DecoyHealth(Wave, cap) * p.Stats.DecoyHpMult;
            z.GuardFrac = 1f;
            z.Blast = cap ? DecoyBlastMastered : DecoyBlast;
        }

        /// Deflates a dummy without a blast (FxKind.DecoyFold); it goes at the end of this tick.
        void FoldDecoy(Zone z)
        {
            if (z.T <= 0f)
                return;
            BroadcastFx(FxKind.DecoyFold, z.Pos, z.Pos + V2.FromAngle(z.Aim), Kits.Get(ZoneKind.DecoyDummy).Color, DecoyFrac(z), 0.6f, Sfx.None);
            z.T = 0f;
        }

        /// Host, as the wave is cleared: every dummy deflates.
        void FoldAllDecoys()
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.DecoyDummy)
                    FoldDecoy(z);
        }

        /// Shot down: the confetti blast shoves regular enemies near it back and stuns every non-boss there.
        void PopDecoy(Zone z)
        {
            if (z.T <= 0f)
                return;
            float reach = z.Blast > 0f ? z.Blast : DecoyBlast;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Def.IsBoss || e.Pos.FartherThan(z.Pos, reach + e.Radius))
                    continue;
                Stun(e, DecoyStun);
                if (!Heavy(e))
                {
                    ShoveFrom(e, z.Pos, DecoyShove, V2.FromAngle(z.Aim));  // slides (Knock.cs)
                }
            }
            BroadcastFx(FxKind.DecoyPop, z.Pos, z.Pos + V2.FromAngle(z.Aim), Kits.Get(ZoneKind.DecoyDummy).Color, reach, 1.1f, Sfx.None);  // (its pop plays on each screen)
            z.T = 0f;  // gone at the end of this tick
        }

        /// Wears a standing dummy (a shot or a bite); at 0 it pops.
        void WearDecoy(Zone z, float dmg, V2 at)
        {
            z.BlockHp -= dmg;
            z.GuardFrac = Math.Max(0f, z.BlockHp / Math.Max(1f, z.SoakMax));
            BroadcastFx(FxKind.DecoyHit, at, z.Pos + V2.FromAngle(z.Aim), Kits.Get(ZoneKind.DecoyDummy).Color, 0f, 0.25f, Sfx.None);
            if (z.BlockHp <= 0f)
                PopDecoy(z);
        }

        /// Host, every tick, for each dummy (TickZones): its contact timer, and it deflates as its time runs out or the fight ends.
        void TickDecoy(Zone z, float dt)
        {
            z.TickT = Math.Max(0f, z.TickT - dt);
            if (!Battle || z.T <= dt * 1.5f)
                FoldDecoy(z);
        }

        /// Host, from the enemy-bullet loop in Combat.cs: does a dummy stop this shot? It wears it by what it would have hit for.
        bool DecoyStops(Bullet b)
        {
            foreach (var z in Zones.Values)
            {
                if (!DecoyUp(z) || b.Pos.FartherThan(z.Pos, DecoyR + b.Radius))
                    continue;
                WearDecoy(z, b.Dmg * EnemyDamageScale(Math.Max(1, Wave)), b.Pos);
                return true;
            }
            return false;
        }

        /// Host, from TickEnemies: where an enemy goes and aims when a dummy draws it (the nearest one up within DecoyPull);
        /// a boss or mini-boss only for its first DecoyBossGlance s up.
        V2 DecoyTarget(Enemy e, V2 tpos)
        {
            Zone best = null;
            float bestD = DecoyPull * DecoyPull;
            bool boss = e.Def.IsBoss || e.Def.Mini;
            foreach (var z in Zones.Values)
            {
                if (!DecoyUp(z) || boss && DecoyAge(z) > DecoyFly + DecoyInflate + DecoyBossGlance)
                    continue;
                float d = z.Pos.DistanceSq(e.Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = z;
                }
            }
            return best != null ? best.Pos : tpos;
        }

        /// Host, from TickEnemies: a regular enemy's body against a dummy wears it (every DecoyContactEvery s) and stays outside it.
        void DecoyContact(Enemy e, float contact)
        {
            foreach (var z in Zones.Values)
            {
                if (!DecoyUp(z))
                    continue;
                V2 d = e.Pos - z.Pos;
                float reach = DecoyR + e.Radius;
                if (d.LengthSq >= reach * reach)
                    continue;
                e.Pos = ClampToArena(z.Pos + (d.LengthSq > 0.01f ? d.Normalized() : V2.FromAngle(z.Aim)) * reach, e.Radius);
                if (z.TickT <= 0f && contact > 0f)
                {
                    z.TickT = DecoyContactEvery;
                    WearDecoy(z, contact * EnemyDamageScale(Math.Max(1, Wave)), z.Pos + d.Normalized() * DecoyR);
                }
            }
        }
    }
}
