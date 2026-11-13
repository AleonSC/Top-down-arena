using System;
using System.Collections.Generic;

namespace Arena
{
    /// Zombie Infection (a run modifier): every regular wave is a horde of zombies, more of them every wave, and the dead
    /// don't always stay dead. Seven kinds (Docs/Mockups/zombie-enemies.html; drawn in ZombieLook.cs):
    ///   Shambler: wide and slow, hits hard.          Runner: frail and fast, in packs of three.
    ///   Spitter: hangs back, lobs acid (a patch).     Crawler: grabs you and roots you (your StunT) until you shoot it free.
    ///   Bloater: bursts into a slowing spore cloud.  Shrieker: screams, frenzying the zombies round it.
    ///   Mender: heals the zombies round it, and the dead near it rise twice as often and twice as fast.
    /// And one in the Lich's soul cyan, only in his faction (Docs/Mockups/lich-faction.html):
    ///   Grave Caller: hangs back and cracks three graves open round a player; a Shambler claws out of each (half
    ///   health, no XP, at most four up per Caller). Kill it and its risen crumble, its open graves close.
    /// Bosses, their adds and the challenge rooms are left as they are. See Docs/ZombieInfection.md.
    public static class Zombies
    {
        public const float CountStart = 1.5f, CountMax = 3f;  // the wave's usual count times this: x1.5 at wave 1, x3 by wave 20
        public const int CountFullWave = 20;
        public const int Cap = 60;                 // at most this many alive at once; the rest wait to come in
        public const float RiseChance = 0.25f;     // a killed zombie gets back up...
        public const float RiseTime = 3f;          // ...this long after, twitching on the ground first
        public const float RiseHp = 0.5f;          // at this share of its health (and it gives no XP, and won't rise again)

        public const float SpitRange = 260f, SpitEvery = 3f, SpitFlight = 0.9f, AcidRadius = 70f, AcidLife = 4f, AcidDps = 10f, AcidSlow = 0.7f;
        public const float GrabReach = 12f, GrabTime = 1.5f, GrabDps = 6f, GrabBreak = 25f, GrabCooldown = 4f;
        public const float SporeRadius = 110f, SporeLife = 4f, SporeSlow = 0.6f;
        public const float ShriekRange = 320f, ShriekEvery = 6f, FrenzyTime = 4f, FrenzySpeed = 1.3f, ShriekerKeep = 350f;
        public const float MendRange = 220f, MendEvery = 3f, MendHeal = 0.15f, MenderKeep = 300f, MendedRise = 0.5f;
        public const float CallerKeep = 320f, CallEvery = 7f, GraveRing = 120f, GraveRise = 1.6f, GraveRadius = 26f, GraveLife = 2.4f;
        public const int GravesPerCall = 3, MaxCalled = 4;  // graves a call opens; Shamblers up at once per Caller

        /// Per-kind caps in one wave: the support zombies are few.
        public const int MaxSpitters = 4, MaxCrawlers = 3, MaxShriekers = 2, MaxMenders = 2, MaxCallers = 2;

        // Phase bits, sent with every enemy so each game draws them (ZombieLook.cs).
        public const byte PhFrenzy = 1, PhGrab = 2, PhAct = 4;  // PhAct: spitting, screaming, pulsing or calling just now
        public const byte PhCalled = 8;  // a Shambler a Grave Caller raised (its eyes turn cyan; it crumbles with its Caller)

        public static bool Is(EnemyType t) => t >= EnemyType.Shambler && t <= EnemyType.Mender || t == EnemyType.GraveCaller;

        /// How many more enemies a wave has.
        public static float CountMult(int wave) => CountStart + (CountMax - CountStart) * Math.Max(0f, Math.Min(1f, (wave - 1f) / (CountFullWave - 1f)));

        /// The horde's mix at a wave: each kind's weight once it has joined (Shamblers from 1, Runners 3, Spitters 6,
        /// Bloaters 8, Crawlers 9, Shriekers 12, Menders 15).
        public static EnemyType Pick(Random rng, int wave)
        {
            var w = new List<KeyValuePair<EnemyType, float>> { new KeyValuePair<EnemyType, float>(EnemyType.Shambler, 45f) };
            if (wave >= 3) w.Add(new KeyValuePair<EnemyType, float>(EnemyType.Runner, 10f));  // (each pick is a pack of three: about 30% of the bodies)
            if (wave >= 6) w.Add(new KeyValuePair<EnemyType, float>(EnemyType.Spitter, 15f));
            if (wave >= 8) w.Add(new KeyValuePair<EnemyType, float>(EnemyType.Bloater, 8f));
            if (wave >= 9) w.Add(new KeyValuePair<EnemyType, float>(EnemyType.Crawler, 10f));
            if (wave >= 12) w.Add(new KeyValuePair<EnemyType, float>(EnemyType.Shrieker, 4f));
            if (wave >= 15) w.Add(new KeyValuePair<EnemyType, float>(EnemyType.Mender, 4f));
            float total = 0f;
            foreach (var kv in w) total += kv.Value;
            float r = (float)rng.NextDouble() * total;
            foreach (var kv in w)
            {
                r -= kv.Value;
                if (r <= 0f)
                    return kv.Key;
            }
            return EnemyType.Shambler;
        }

        public static int CapOf(EnemyType t) =>
            t == EnemyType.Spitter ? MaxSpitters : t == EnemyType.Crawler ? MaxCrawlers : t == EnemyType.Shrieker ? MaxShriekers : t == EnemyType.Mender ? MaxMenders : t == EnemyType.GraveCaller ? MaxCallers : int.MaxValue;
    }

    public partial class Game
    {
        class Riser { public EnemyType Type; public V2 Pos; public float T; public bool Elite; public int Caller; }  // Caller: the Grave Caller whose grave it is (0: none)
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
            var counts = new Dictionary<EnemyType, int>();
            for (int tries = 0; spawnQueue.Count < total && tries < total * 20; tries++)
            {
                var type = Zombies.Pick(rng, n);
                int had;
                counts.TryGetValue(type, out had);
                if (had >= Zombies.CapOf(type))
                    continue;
                counts[type] = had + 1;
                spawnQueue.Add(type);
                if (type == EnemyType.Runner)
                    for (int k = 0; k < 2 && spawnQueue.Count < total; k++)
                        spawnQueue.Add(type);  // runners come in packs of three
            }
            while (spawnQueue.Count < total)
                spawnQueue.Add(EnemyType.Shambler);
            return true;
        }

        /// A regular wave's horde comes in faster, so a bigger wave doesn't take longer to arrive; and waits while the
        /// arena already holds the cap. Returns false to hold this spawn back.
        bool ZombieSpawnReady(ref float timer)
        {
            if (!ZombiesOn || BossNow)
                return true;
            if (Enemies.Count >= Zombies.Cap)
                return false;
            timer /= Zombies.CountMult(Wave);
            return true;
        }

        /// XP for a kill: the horde's share is split so levels keep pace with a normal run; a risen zombie gives none.
        float ZombieXpScale(Enemy e)
        {
            if (e.Risen)
                return 0f;  // (a Grave Caller's Shamblers too, with or without the modifier)
            if (!ZombiesOn || e.Def.IsBoss)
                return 1f;
            return BossNow ? 1f : 1f / Zombies.CountMult(Wave);
        }

        Enemy MenderNear(V2 at, Enemy not = null)
        {
            foreach (var m in Enemies.Values)
                if (m.Type == EnemyType.Mender && m != not && m.Hp > 0f && m.Pos.DistanceTo(at) < Zombies.MendRange)
                    return m;
            return null;
        }

        /// Host, as an enemy dies: a Bloater bursts into spores; a horde zombie may get back up, unless fire, a melee
        /// weapon or a boss wave ended it. Near a Mender the dead rise twice as often, and twice as fast.
        void MaybeRise(Enemy e, Player killer)
        {
            if (e.Type == EnemyType.Bloater)
                SpawnPatchAt(PatchKind.Spores, ClampToArena(e.Pos, 40f), 0f, Zombies.SporeLife, Zombies.SporeRadius, Zombies.SporeRadius);
            if (e.Type == EnemyType.Crawler)
                ReleaseGrab(e);
            if (e.Type == EnemyType.GraveCaller)
                CallerDied(e);
            if (!ZombiesOn || State != MatchState.Fighting || InRoom || BossNow || e.Risen || e.Def.IsBoss || e.Def.Xp <= 0)
                return;
            if (!Zombies.Is(e.Type) || e.Type == EnemyType.Bloater || e.Type == EnemyType.GraveCaller)
                return;  // (a Bloater burst: nothing left to rise; a Grave Caller stays down, so killing it ends its graves)
            if (e.Burning || e.BurnT > 0f || e.BurnPool > 0f)
                return;  // burned: it stays down
            if (killer != null && killer.Weapon.Melee)
                return;  // cut down up close: it stays down
            bool mended = MenderNear(e.Pos, e) != null;
            if (rng.NextDouble() >= (mended ? Zombies.MendedRise : Zombies.RiseChance))
                return;
            float time = Zombies.RiseTime * (mended ? 0.5f : 1f);
            risers.Add(new Riser { Type = e.Type, Pos = e.Pos, T = time, Elite = e.Elite });
            BroadcastFx(FxKind.Target, e.Pos, new Rgba(0.45f, 0.85f, 0.35f), e.Radius + 14f, time, Sfx.None);
        }

        /// Host, each tick of a fight: the twitching dead get back up, acid burns.
        void TickRisers(float dt)
        {
            for (int i = risers.Count - 1; i >= 0; i--)
            {
                var r = risers[i];
                r.T -= dt;
                if (r.T > 0f)
                    continue;
                risers.RemoveAt(i);
                var z = SpawnEnemyAt(r.Type, ClampToArena(r.Pos, Defs.Get(r.Type).Radius), r.Caller != 0 ? 0.2f : 0.5f, 0, r.Elite);
                z.Hp = z.MaxHp * Zombies.RiseHp;
                z.Risen = true;
                if (r.Caller != 0)
                {
                    z.LinkId = r.Caller;
                    z.Phase |= Zombies.PhCalled;
                }
                BroadcastFx(FxKind.Ring, r.Pos, r.Caller != 0 ? CallerSoul : new Rgba(0.45f, 0.85f, 0.35f), 40f, 0.4f, Sfx.ZombieRise);
            }
            TickAcid(dt);
            TickGroans(dt);
        }

        float groanT;

        /// Host: every few seconds one of the horde, picked at random, groans (a sound for everyone, nothing on screen).
        void TickGroans(float dt)
        {
            bool any = false;  // (no zombies, no rolls: waves without them play out exactly as before)
            foreach (var e in Enemies.Values)
                if (Zombies.Is(e.Type) && e.Hp > 0f)
                {
                    any = true;
                    break;
                }
            if (!any)
                return;
            groanT -= dt;
            if (groanT > 0f)
                return;
            groanT = Rand(2.5f, 5f);
            foreach (var e in Enemies.Values)
                if (Zombies.Is(e.Type) && e.Hp > 0f && rng.NextDouble() < 0.1)
                {
                    BroadcastSound(Sfx.ZombieGroan);
                    return;
                }
        }

        void ClearRisers() => risers.Clear();

        float acidTickT;

        /// Host: a Spitter's acid burns whoever stands in it (its slow is in PatchSlowAt, on each player's own game).
        void TickAcid(float dt)
        {
            acidTickT -= dt;
            if (acidTickT > 0f)
                return;
            acidTickT = 0.25f;
            foreach (var pt in Patches.Values)
            {
                if (pt.Kind != PatchKind.Acid || !pt.Active)
                    continue;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                        DamageOverTime(p, Zombies.AcidDps * 0.25f, "Spitter acid");
            }
        }

        // --- Moving, acting, firing -------------------------------------------------------------------------------

        /// MoveEnemy: where each zombie goes. Melee kinds come at you (Shamblers sway, Crawlers skitter); the Spitter,
        /// Shrieker and Mender keep their distance behind the horde. A frenzied zombie runs 30% faster.
        V2 ZombieMove(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            e.Facing = dir.IsZero ? e.Facing : dir.Angle;
            if (e.FrenzyT > 0f)
                speed *= Zombies.FrenzySpeed;
            switch (e.Type)
            {
                case EnemyType.Shambler:
                    return V2.FromAngle(dir.Angle + 0.45f * (float)Math.Sin(AnimT * 2.4f + e.Id));
                case EnemyType.Crawler:
                    if ((e.Phase & Zombies.PhGrab) != 0)
                        return V2.Zero;  // holding on
                    return V2.FromAngle(dir.Angle + 0.25f * (float)Math.Sin(AnimT * 5f + e.Id));
                case EnemyType.Spitter:
                    return KeepDistance(dir, dist, Zombies.SpitRange) + dir.Orthogonal() * e.Strafe * 0.6f;
                case EnemyType.Shrieker:
                    return KeepDistance(dir, dist, Zombies.ShriekerKeep) + dir.Orthogonal() * e.Strafe * 0.5f;
                case EnemyType.Mender:
                    return KeepDistance(dir, dist, Zombies.MenderKeep) + dir.Orthogonal() * e.Strafe * 0.6f;
                case EnemyType.GraveCaller:
                    if ((e.Phase & Zombies.PhAct) != 0)
                        return V2.Zero;  // stands still while it calls
                    return KeepDistance(dir, dist, Zombies.CallerKeep) + dir.Orthogonal() * e.Strafe * 0.5f;
                default:
                    return dir;  // Runners and Bloaters: straight at you
            }
        }

        /// Special: the Crawler's grab, the Bloater bursting on you, the Shrieker's scream and the Mender's pulse.
        void ZombieSpecial(Enemy e, V2 tpos, float dt)
        {
            e.FrenzyT = Math.Max(0f, e.FrenzyT - dt);
            e.PhaseT = Math.Max(0f, e.PhaseT - dt);
            if (e.T < 0.05f && (e.Type == EnemyType.Shrieker || e.Type == EnemyType.Mender || e.Type == EnemyType.GraveCaller))
                e.T2 = e.Type == EnemyType.GraveCaller ? Rand(2.5f, 4f) : Rand(1.5f, 3f);  // not the moment it arrives
            byte act = e.PhaseT > 0f ? Zombies.PhAct : (byte)0;
            switch (e.Type)
            {
                case EnemyType.Crawler:
                    TickGrab(e, dt);
                    break;
                case EnemyType.Bloater:
                {
                    var p = NearestPlayer(e.Pos);
                    if (p != null && p.InvulnT <= 0f && p.Pos.DistanceTo(e.Pos) < e.Radius + p.Def.HitR + 4f)
                    {
                        Damage(p, e.Def.Contact * D(DiffKnob.EnemyDamage), "Bloater");
                        BroadcastFx(FxKind.Boom, e.Pos, e.Def.Color, Zombies.SporeRadius * 0.6f, 0.4f, Sfx.ZombieBurst);
                        e.Hp = 0f;  // it bursts on you (the spores come with its death, MaybeRise)
                    }
                    break;
                }
                case EnemyType.Shrieker:
                    e.T2 -= dt;
                    if (e.T2 <= 0f)
                    {
                        e.T2 = Zombies.ShriekEvery;
                        e.PhaseT = 0.6f;
                        act = Zombies.PhAct;
                        BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.89f, 1f, 0.61f), Zombies.ShriekRange, 0.8f, Sfx.ZombieShriek);
                        foreach (var o in Enemies.Values)
                            if (o != e && !o.Def.IsBoss && o.Pos.DistanceTo(e.Pos) < Zombies.ShriekRange)
                            {
                                o.FrenzyT = Zombies.FrenzyTime;
                                o.RetargetT = 0f;  // and it turns on you
                            }
                    }
                    break;
                case EnemyType.Mender:
                    e.T2 -= dt;
                    if (e.T2 <= 0f)
                    {
                        e.T2 = Zombies.MendEvery;
                        e.PhaseT = 0.6f;
                        act = Zombies.PhAct;
                        BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, Zombies.MendRange, 0.9f, Sfx.ZombieMend);
                        foreach (var o in Enemies.Values)
                            if (o != e && !o.Def.IsBoss && o.Hp < o.MaxHp && o.Pos.DistanceTo(e.Pos) < Zombies.MendRange)
                            {
                                o.Hp = Math.Min(o.MaxHp, o.Hp + o.MaxHp * Zombies.MendHeal);
                                BroadcastFx(FxKind.Link, e.Pos, o.Pos, e.Def.Color, 2f, 0.5f, Sfx.None);
                            }
                    }
                    break;
                case EnemyType.GraveCaller:
                    e.T2 -= dt;
                    if (e.T2 <= 0f && CallGraves(e, tpos))
                    {
                        e.T2 = Zombies.CallEvery;
                        e.PhaseT = 0.8f;
                        act = Zombies.PhAct;
                    }
                    else if (e.T2 <= 0f)
                        e.T2 = 1f;  // its four are up: it tries again in a moment
                    break;
            }
            byte keep = (byte)(e.Phase & (Zombies.PhGrab | Zombies.PhCalled));
            e.Phase = (byte)(keep | (e.FrenzyT > 0f ? Zombies.PhFrenzy : 0) | act);
        }

        /// The Crawler: in reach of a player it grabs on and roots them (their StunT: they can still shoot), hurting
        /// 6 a second, for 1.5 s or until it has taken 25 damage. Each Crawler grabs once every 4 s.
        void TickGrab(Enemy e, float dt)
        {
            Player held;
            if ((e.Phase & Zombies.PhGrab) != 0)
            {
                if (!Players.TryGetValue(e.GrabId, out held) || held.Downed || held.StunT <= 0f || e.GrabHp - e.Hp >= Zombies.GrabBreak)
                {
                    ReleaseGrab(e);
                    return;
                }
                e.Pos = held.Pos - V2.FromAngle(e.Facing) * (e.Radius + held.Def.HitR + 2f);
                if (held.InvulnT <= 0f)
                    DamageOverTime(held, Zombies.GrabDps * dt * D(DiffKnob.EnemyDamage), "Crawler");
                return;
            }
            e.T2 -= dt;
            if (e.T2 > 0f)
                return;
            var p = NearestPlayer(e.Pos);
            if (p == null || p.StunT > 0f || p.InvulnT > 0f || p.Pos.DistanceTo(e.Pos) > e.Radius + p.Def.HitR + Zombies.GrabReach)
                return;
            e.Phase |= Zombies.PhGrab;
            e.GrabId = p.Id;
            e.GrabHp = e.Hp;
            e.Facing = (p.Pos - e.Pos).Angle;
            p.StunT = Zombies.GrabTime;
            BroadcastFx(FxKind.Ring, p.Pos, e.Def.Color, 34f, 0.3f, Sfx.ZombieGrab);
        }

        void ReleaseGrab(Enemy e)
        {
            if ((e.Phase & Zombies.PhGrab) == 0)
                return;
            Player held;
            if (Players.TryGetValue(e.GrabId, out held))
                held.StunT = 0f;
            e.Phase = (byte)(e.Phase & ~Zombies.PhGrab);
            e.GrabId = 0;
            e.T2 = Zombies.GrabCooldown;
        }

        public static readonly Rgba CallerSoul = new Rgba(0.37f, 0.85f, 1f);  // the Lich's soul cyan

        /// A Grave Caller's Shamblers: up, and still in their graves.
        int CalledBy(Enemy caller)
        {
            int n = 0;
            foreach (var o in Enemies.Values)
                if (o.LinkId == caller.Id && (o.Phase & Zombies.PhCalled) != 0 && o.Hp > 0f)
                    n++;
            foreach (var r in risers)
                if (r.Caller == caller.Id)
                    n++;
            return n;
        }

        /// Host: the Grave Caller cracks graves open in a ring round the player it's after (up to its four); each opens
        /// GraveRise later and a Shambler climbs out. False when it has its four already.
        bool CallGraves(Enemy e, V2 tpos)
        {
            int room = Math.Min(Zombies.GravesPerCall, Zombies.MaxCalled - CalledBy(e));
            if (room <= 0 || e.Pos.DistanceTo(tpos) > Zombies.CallerKeep * 2f)
                return false;
            float a0 = Rand(0f, MathX.Tau);
            for (int i = 0; i < room; i++)
            {
                V2 at = ClampToArena(tpos + V2.FromAngle(a0 + MathX.Tau * i / Zombies.GravesPerCall) * Zombies.GraveRing, 30f);
                risers.Add(new Riser { Type = EnemyType.Shambler, Pos = at, T = Zombies.GraveRise, Caller = e.Id });
                SpawnPatchAt(PatchKind.Grave, at, Zombies.GraveRise, Zombies.GraveLife, Zombies.GraveRadius, Zombies.GraveRadius);
            }
            BroadcastFx(FxKind.Ring, e.Pos, CallerSoul, 60f, 0.5f, Sfx.ZombieMend);
            return true;
        }

        /// Host, a Grave Caller down: its risen crumble (no XP: they're risen) and its graves still opening close.
        void CallerDied(Enemy e)
        {
            risers.RemoveAll(r => r.Caller == e.Id);
            foreach (var o in Enemies.Values)
                if (o.LinkId == e.Id && (o.Phase & Zombies.PhCalled) != 0 && o.Hp > 0f)
                {
                    o.Hp = 0f;
                    BroadcastFx(FxKind.Ring, o.Pos, new Rgba(0.91f, 0.89f, 0.78f), o.Radius + 12f, 0.35f, Sfx.None);
                }
        }

        /// EnemyFire: the Spitter lobs a glob at where you stand (a ring marks it); it lands as an acid patch.
        bool ZombieFire(Enemy e, V2 tpos)
        {
            if (!Zombies.Is(e.Type))
                return false;
            if (e.Type != EnemyType.Spitter || e.Pos.DistanceTo(tpos) > Zombies.SpitRange * 2.2f)
            {
                e.FireT = e.Type == EnemyType.Spitter ? 0.5f : 99f;
                return true;
            }
            e.FireT = Zombies.SpitEvery;
            e.PhaseT = 0.5f;
            V2 at = ClampToArena(tpos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(0f, 20f), 50f);
            SpawnPatchAt(PatchKind.Acid, at, Zombies.SpitFlight, Zombies.SpitFlight + Zombies.AcidLife, Zombies.AcidRadius, Zombies.AcidRadius);
            BroadcastFx(FxKind.Target, at, new Rgba(0.91f, 0.89f, 0.35f), Zombies.AcidRadius, Zombies.SpitFlight, Sfx.ZombieSpit);
            return true;
        }
    }
}
