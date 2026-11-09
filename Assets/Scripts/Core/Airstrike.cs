using System;
using System.Collections.Generic;

namespace Arena
{
    /// Airstrike (Soldier DPS, replaces the Cluster Grenade; the Carpet Wall look of Docs/Mockups/airstrike-rework.html). You throw a
    /// smoke flare at your cursor (anywhere in the arena) and a lane lights up through it, across your aim, from wall to wall. AirJetT
    /// after the cast a jet comes in from one end of the lane and drops a bomb every AirSpacing px: AirBombDamage each in AirBombRadius
    /// (Bigger Boom widens it). After the last bomb the lane burns for AirFireTime: AirFireDps to every enemy within AirFireHalf of
    /// the line. Mastered, a second wall lands AirSecondGap px further out, AirSecondDelay later. Allies take no damage.
    /// The run is host logic. Clients get one FxKind.Airstrike per wall (Pos = where the jet comes in, Pos2 = the lane's far end,
    /// Radius = the thrower's id * 10000 + how far along the lane the flare is, colour alpha 1 = the cast with the flare, 0.5 = the
    /// mastered second wall) and draw it from its age with AirBombCount, AirBombAlong and AirBombTime (Game/AirstrikeLook.cs).
    /// Each bomb also sends a Boom in the grenade colour, so it gets the Frag Burst blast and its sound (Game/SoldierLooks.cs).
    public partial class Game
    {
        public const float AirFlareT = 0.4f, AirJetT = 1.2f, AirJetSpeed = 1700f, AirLead = 400f, AirSpacing = 140f, AirFirstBomb = 40f, AirFall = 0.28f;
        public const float AirBombDamage = 55f, AirBombRadius = 85f, AirFireTime = 3f, AirFireDps = 15f, AirFireTick = 0.25f, AirFireHalf = 80f;
        public const float AirSecondGap = 160f, AirSecondDelay = 0.5f, AirFxTail = 0.6f, AirEdgeInset = 12f;

        /// How many bombs fall on a lane `len` px long: one every AirSpacing from AirFirstBomb in, none in the last 20 px.
        public static int AirBombCount(float len) => len <= AirFirstBomb + 20f ? 0 : 1 + (int)((len - AirFirstBomb - 20f) / AirSpacing);
        public static float AirBombAlong(int i) => AirFirstBomb + i * AirSpacing;
        /// When bomb i bursts, from the lane's start (its effect's age): the jet starts AirLead before the near wall at AirJetT.
        public static float AirBombTime(int i) => AirJetT + (AirLead + AirBombAlong(i)) / AirJetSpeed + AirFall;
        /// When the lane stops burning.
        public static float AirFireEnd(float len) => AirBombTime(Math.Max(0, AirBombCount(len) - 1)) + AirFireTime;

        class AirRun
        {
            public int Owner;
            public V2 From, To, Flare;
            public float T, Dmg, Radius, FireDmg, FireTick;
            public int Next;     // the next bomb to burst
            public bool Sent, WithFlare, Whoosh;
        }

        readonly List<AirRun> airRuns = new List<AirRun>();

        /// The line through `at` along `dir`, cut to the arena (AirEdgeInset inside its edges); false if it misses.
        bool ClipToArena(V2 at, V2 dir, out V2 a, out V2 b)
        {
            V2 lo = ArenaMin + new V2(AirEdgeInset, AirEdgeInset), hi = ArenaMax - new V2(AirEdgeInset, AirEdgeInset);
            float t0 = float.MinValue, t1 = float.MaxValue;
            for (int axis = 0; axis < 2; axis++)
            {
                float o = axis == 0 ? at.X : at.Y, d = axis == 0 ? dir.X : dir.Y, l = axis == 0 ? lo.X : lo.Y, h = axis == 0 ? hi.X : hi.Y;
                if (Math.Abs(d) < 1e-6f)
                {
                    if (o < l || o > h) { a = b = at; return false; }
                    continue;
                }
                float ta = (l - o) / d, tb = (h - o) / d;
                t0 = Math.Max(t0, Math.Min(ta, tb));
                t1 = Math.Min(t1, Math.Max(ta, tb));
            }
            a = at + dir * t0;
            b = at + dir * t1;
            return t1 > t0;
        }

        void CastAirstrike(Player p, V2 target, bool cap, float pw)
        {
            V2 aim = target - p.Pos;
            aim = aim.Length < 1f ? V2.FromAngle(p.Aim) : aim.Normalized();
            float dmg = AirBombDamage * p.Stats.DmgMult * pw, radius = AirBombRadius * p.Stats.GrenadeRadiusMult;
            AddAirRun(p, target, aim, 0f, true, dmg, radius, pw);
            if (cap)
                AddAirRun(p, ClampToArena(target + aim * AirSecondGap, AirEdgeInset), aim, -AirSecondDelay, false, dmg, radius, pw);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.36f, 0.26f), 22f, 0.25f, Sfx.Throw);
        }

        void AddAirRun(Player p, V2 at, V2 aim, float t, bool flare, float dmg, float radius, float pw)
        {
            V2 across = aim.Orthogonal(), a, b;
            if (!ClipToArena(at, across, out a, out b))
                return;
            airRuns.Add(new AirRun
            {
                Owner = p.Id, From = a, To = b, Flare = at, T = t, WithFlare = flare, Dmg = dmg, Radius = radius,
                FireDmg = AirFireDps * AirFireTick * p.Stats.DmgMult * pw
            });
        }

        /// Host, every tick: send each wall's effect when it starts, burst its bombs on time, then burn the lane.
        void TickAirstrikes(float dt)
        {
            for (int i = airRuns.Count - 1; i >= 0; i--)
            {
                var r = airRuns[i];
                r.T += dt;
                if (r.T < 0f)
                    continue;
                V2 lane = r.To - r.From;
                float len = lane.Length;
                V2 dir = len > 1f ? lane / len : new V2(1f, 0f);
                if (!r.Sent)
                {
                    r.Sent = true;
                    float along = Math.Max(0f, Math.Min(9999f, (r.Flare - r.From).Dot(dir)));
                    BroadcastFx(FxKind.Airstrike, r.From, r.To, new Rgba(1f, 0.36f, 0.26f, r.WithFlare ? 1f : 0.5f), r.Owner * 10000f + (float)Math.Round(along),
                        AirFireEnd(len) + AirFxTail, Sfx.None);
                }
                if (!r.Whoosh && r.T >= AirJetT)
                {
                    r.Whoosh = true;  // the jet's roar: a silent spark where it comes in
                    BroadcastFx(FxKind.Spark, r.From, new Rgba(1f, 1f, 1f, 0f), 0f, 0.05f, Sfx.BossWhoosh);
                }
                int bombs = AirBombCount(len);
                while (r.Next < bombs && r.T >= AirBombTime(r.Next))
                {
                    V2 at = r.From + dir * AirBombAlong(r.Next);
                    r.Next++;
                    foreach (var e in Enemies.Values)
                        if (Targetable(e) && e.Pos.DistanceTo(at) < r.Radius + e.Radius)
                            HurtEnemy(e, r.Dmg, r.Owner);
                    BroadcastFx(FxKind.Boom, at, Defs.BulletColor(Defs.GrenadeShot), r.Radius, 0.35f, r.Next % 2 == 1 ? Sfx.Explode : Sfx.None);
                    PropBlast(at, r.Radius, r.Owner);      // sets barrels off (Props.cs)
                    GateLockBlast(at, r.Radius, r.Owner);  // and cracks a Warden gate's lock (Warden.cs)
                }
                float fireStart = bombs > 0 ? AirBombTime(bombs - 1) : AirJetT, fireEnd = fireStart + AirFireTime;
                if (r.T >= fireStart && r.T < fireEnd)
                {
                    r.FireTick -= dt;
                    if (r.FireTick <= 0f)
                    {
                        r.FireTick += AirFireTick;
                        foreach (var e in Enemies.Values)
                        {
                            if (!Targetable(e))
                                continue;
                            V2 rel = e.Pos - r.From;
                            float u = rel.Dot(dir), v = Math.Abs(rel.Dot(dir.Orthogonal()));
                            if (u > -e.Radius && u < len + e.Radius && v < AirFireHalf + e.Radius)
                                HurtEnemy(e, r.FireDmg, r.Owner);
                        }
                    }
                }
                if (r.T >= fireEnd)
                    airRuns.RemoveAt(i);
            }
        }
    }
}
