using System;

namespace Arena
{
    /// The Twin Reapers' faction, the Dusk and Dawn cult (Docs/Mockups/reaper-faction.html, Approaches.cs). Each member previews
    /// one Reaper mechanic; each is sworn to Dusk (pink) or Dawn (blue) by its id, which only changes its colour (Game/ReaperCultLook.cs).
    ///   Wisp: a Swarmer (straight at you, in threes). Gravedigger: a Brute (PrisonKin.cs IsBrute). Shade: a Phantom (LateGame.cs).
    ///   Acolyte: a Swordsman with a sickle, shorter and wider (Swordsman.cs SickleReach...).
    ///   Scythe Wraith: a Lunger (PrisonKin.cs IsLunger) that marks from further out, locks its line after ScytheTrack s, dashes
    ///     ScytheSpeed * ScytheDash px and reaps ScytheReap px either side of its path (the Sundered reaps).
    ///   Bound Wraiths: always a pair (SpawnEnemyAt brings the partner, Enemy.LinkId). They circle a point that follows you,
    ///     BoundOrbit px out, joined by a tether that hurts (the tether sweep). Kill one and the other stops and channels for
    ///     BoundRevive s (Phase 1; the ghost's spot is its Waypoint), then brings it back at BoundReviveHp, unless you kill
    ///     it first (the Soul Bond).
    ///   Lamplighter: keeps its distance, plants its feet while its lantern flares (LampWind s, gold guide lines), then fires
    ///     LampLines lines of LampStreak shots at you, or a four-way cross; it alternates (Phase 0 lines, 1 cross).
    ///   Mourner: keeps its distance and weeps a twin spiral (Phase 1, MournBurst s: one arm each way), then rests MournRest s.
    public partial class Game
    {
        public static bool IsReaperCult(EnemyType t) => t >= EnemyType.Wisp && t <= EnemyType.Shade;

        // --- Scythe Wraith ---------------------------------------------------------------------------------------------
        public const float ScytheStart = 280f, ScytheMark = 0.8f, ScytheTrack = 0.25f, ScytheDash = 0.38f, ScytheSpeed = 700f,
            ScytheRecover = 0.9f, ScytheReap = 26f, ScytheDamage = 22f;

        // --- Bound Wraiths ---------------------------------------------------------------------------------------------
        public const float BoundOrbit = 75f, BoundSpin = 1.25f, BoundRevive = 3f, BoundReviveHp = 0.6f, BoundTetherDamage = 10f,
            BoundTetherWidth = 8f, BoundPairGap = 2f * BoundOrbit, BoundHover = 40f;
        public const byte BoundChanneling = 1;
        bool boundPairing;  // host: SpawnEnemyAt is making a partner (it doesn't make one of its own)

        // --- Lamplighter and Mourner -----------------------------------------------------------------------------------
        public const float LampRange = 280f, LampWind = 0.6f, LampEvery = 3.2f, LampSpread = 0.2f, LampSpeed = 170f, LampStreakGap = 15f;
        public const int LampLines = 3, LampStreak = 4;
        public const float MournRange = 260f, MournBurst = 2.5f, MournRest = 3f, MournEvery = 0.12f, MournTurn = 0.22f, MournSpeed = 95f, MournWind = 0.5f;

        /// SpawnEnemyAt: a Bound Wraith brings its partner, across the orbit from it.
        void PairBound(Enemy e, float spawnT, int parent, bool elite)
        {
            if (boundPairing || e.LinkId != 0)
                return;
            boundPairing = true;
            var b = SpawnEnemyAt(EnemyType.BoundWraith, e.Pos + V2.FromAngle(e.Ang) * BoundPairGap, spawnT, parent, elite);
            boundPairing = false;
            e.LinkId = b.Id;
            b.LinkId = e.Id;
            b.SquadId = e.SquadId;
        }

        /// Its partner, alive (null: none).
        Enemy BoundPartner(Enemy e)
        {
            Enemy o;
            return e.LinkId != 0 && Enemies.TryGetValue(e.LinkId, out o) && o.Hp > 0f && o.Type == EnemyType.BoundWraith ? o : null;
        }

        V2 ReaperCultMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.BoundWraith:
                {
                    if (e.Phase == BoundChanneling)
                        return V2.Zero;  // stands over its fallen partner: your window
                    var o = BoundPartner(e);
                    if (o == null)
                        return dir;  // alone for good: a plain chaser
                    // The pair spins round its middle, which drifts to just beside you, so the tether sweeps over you.
                    V2 mid = (e.Pos + o.Pos) * 0.5f;
                    V2 toT = tpos - mid;
                    if (toT.Length > BoundHover)
                        mid += toT.Normalized() * Math.Min(toT.Length - BoundHover, e.Speed * 0.8f * dt);
                    V2 off = e.Pos - (e.Pos + o.Pos) * 0.5f;
                    float a = (off.Length < 1f ? e.Ang : off.Angle) + BoundSpin * dt;
                    float rad = off.Length + (BoundOrbit - off.Length) * Math.Min(1f, dt * 2f);
                    V2 goal = ClampToArena(mid + V2.FromAngle(a) * rad, e.Radius);
                    V2 step = goal - e.Pos;
                    speed = Math.Min(e.Speed * 3f, step.Length / Math.Max(dt, 0.001f));
                    return step;
                }
                case EnemyType.Lamplighter:
                    return e.Winding ? V2.Zero : KeepDistance(dir, dist, LampRange) + dir.Orthogonal() * e.Strafe * 0.5f;  // plants its feet as the lantern flares
                case EnemyType.Mourner:
                    return e.Phase == 1 ? V2.Zero : KeepDistance(dir, dist, MournRange) + dir.Orthogonal() * e.Strafe * 0.3f;  // still while it weeps
            }
            return dir;
        }

        /// Host, from Special: the Bound Wraiths' revive and the Mourner's burst clock.
        void ReaperCultSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.Type == EnemyType.BoundWraith && e.Phase == BoundChanneling)
            {
                e.PhaseT -= dt;
                if (e.PhaseT > 0f)
                    return;
                e.Phase = 0;
                boundPairing = true;
                var b = SpawnEnemyAt(EnemyType.BoundWraith, e.Waypoint, 0.4f, 0, e.Elite);
                boundPairing = false;
                b.MaxHp = Math.Max(1f, e.T2);
                b.Hp = b.MaxHp * BoundReviveHp;
                b.Score = 0;  // no second bounty
                b.LinkId = e.Id;
                b.SquadId = e.SquadId;
                e.LinkId = b.Id;
                BroadcastFx(FxKind.Ring, e.Waypoint, e.Def.Color, 90f, 0.5f, Sfx.Ability);
            }
            else if (e.Type == EnemyType.Mourner && e.Phase == 1)
            {
                e.PhaseT -= dt;
                if (e.PhaseT <= 0f)
                {
                    e.Phase = 0;
                    e.FireT = MournRest * WaveFireRate(Wave) * D(DiffKnob.FireRate);  // rests MournRest real seconds (the fire clock runs at the wave's rate): push in now
                }
            }
        }

        /// Host, from EnemyFire. True if it handled the shot.
        bool ReaperCultFire(Enemy e, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.Lamplighter:
                {
                    e.FireT = LampEvery;
                    bool cross = e.Phase == 1;
                    int lanes = cross ? 4 : LampLines;
                    for (int i = 0; i < lanes; i++)
                    {
                        float a = cross ? aim + i * MathX.Pi / 2f : aim + (i - (LampLines - 1) / 2f) * LampSpread;
                        for (int k = 0; k < LampStreak; k++)
                            EnemyShot(e, a, LampSpeed + k * LampStreakGap, Defs.SmallShot);  // a streak: the faster ones lead
                    }
                    e.Phase = (byte)(cross ? 0 : 1);  // lines, then the cross
                    BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(aim) * (e.Radius + 6f), new Rgba(1f, 0.8f, 0.45f), 26f, 0.25f, Sfx.SpellZap);
                    return true;
                }
                case EnemyType.Mourner:
                    if (e.Phase == 0)
                    {
                        e.Phase = 1;
                        e.PhaseT = MournBurst;
                        e.T2 = aim;  // the spiral's start
                        e.Ang = 0f;
                    }
                    e.FireT = MournEvery;
                    e.Ang += MournTurn;
                    EnemyShot(e, e.T2 + e.Ang, MournSpeed, Defs.SmallShot);                // Dusk's arm, one way
                    EnemyShot(e, e.T2 + MathX.Pi - e.Ang, MournSpeed, Defs.SmallShot);     // Dawn's arm, the other
                    return true;
            }
            return false;
        }

        /// Host, OnEnemyKilled: a fallen Bound Wraith's partner stops and channels it back.
        void ReaperCultKilled(Enemy e)
        {
            if (e.Type != EnemyType.BoundWraith)
                return;
            var o = BoundPartner(e);
            if (o == null || o.Id == e.Id || o.Phase == BoundChanneling)
                return;
            o.Phase = BoundChanneling;
            o.PhaseT = BoundRevive;
            o.Waypoint = e.Pos;
            o.T2 = e.MaxHp;
            o.LinkId = 0;
            BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 40f, BoundRevive, Sfx.None);
        }

        /// Host, every tick: the Bound Wraiths' tethers and the Scythe Wraiths' reaps.
        void TickReaperFaction(float dt)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.Hp <= 0f || e.SpawnT > 0f)
                    continue;
                if (e.Type == EnemyType.BoundWraith && e.Phase != BoundChanneling)
                {
                    var o = BoundPartner(e);
                    if (o != null && e.Id < o.Id && o.Phase != BoundChanneling && o.SpawnT <= 0f)
                        BoundTether(e, o);
                }
                else if (e.Type == EnemyType.ScytheWraith && e.Phase == 2)
                {
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed || p.InvulnT > 0f || p.SwallowT > 0f || p.Pos.DistanceTo(e.Pos) > e.Radius + ScytheReap + p.Def.HitR)
                            continue;
                        if (p.ParryActive && ParryContact(p, e, ScytheDamage))
                            continue;
                        Damage(p, ScytheDamage, e.Def.Name + " (reap)");
                        RodTouched(p, e);  // LightningRod.cs
                    }
                }
            }
        }

        void BoundTether(Enemy a, Enemy b)
        {
            V2 dvec = b.Pos - a.Pos;
            float len = Math.Max(1f, dvec.Length);
            V2 dir = dvec / len;
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f || p.SwallowT > 0f)
                    continue;
                float along = (p.Pos - a.Pos).Dot(dir);
                if (along < a.Radius || along > len - b.Radius)
                    continue;
                if (p.Pos.DistanceTo(a.Pos + dir * along) < BoundTetherWidth / 2f + p.Def.HitR)
                    Damage(p, BoundTetherDamage, "Bound Wraiths (tether)");
            }
        }

        /// A Bound Wraith's partner and, while it channels, the ghost's spot and the time left (GameNet.cs SyncEnemy).
        void SyncBound(NetSync s, Enemy e)
        {
            int link = e.LinkId;
            s.Int(ref link);
            e.LinkId = link;
            if (e.Phase == BoundChanneling)
            {
                V2 at = e.Waypoint;
                float left = e.PhaseT;
                s.Vec(ref at);
                s.Float(ref left);
                if (s.Reading)
                {
                    e.Waypoint = at;
                    e.PhaseT = left;
                }
            }
        }
    }
}
