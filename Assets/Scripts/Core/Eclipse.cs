using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Eclipse's stage of the fight, by its health: five stages, then a last stand.
    public enum EclipseStage : byte { Corona, Penumbra, Umbra, Echoes, LastLight, Totality }

    /// What it's doing now. Each stage loops its turns, one attack at a time, with breathers (Rest) between.
    public enum EclipseTurn : byte
    {
        Rest, Spiral, CoronaRings, Flares, LunarWake, Flash, Gravity, Crescents, ClosingUmbra, DarkSpiral,
        EchoColossus, EchoLich, EchoHerald, EchoSingularity, FranticSpiral, ShadowNova, SolarStorm, Totality,
    }

    /// A solar flare: a warning line from where it was fired for FlareWarn seconds (T counts up), then a lane of fire.
    public class EclipseFlare
    {
        public V2 From;
        public float Ang, T;
        public readonly HashSet<int> Hit = new HashSet<int>();  // host: players it has burned (once each)
    }

    /// A ring of shadow closing in on the sun from the edges, with two gaps.
    public class EclipseRing
    {
        public V2 Center;
        public float R, Gap1, Gap2;
        public readonly HashSet<int> Hit = new HashSet<int>();  // host: players it has caught (once each)
    }

    /// A pool of sunlight drifting across the Umbra's dark.
    public class Sunspot
    {
        public V2 Pos;
        public float Dir;
    }

    /// Everything about the Eclipse's fight its body doesn't carry. The host runs it; clients get it in the snapshot
    /// (SyncEclipse) to draw the moon and its shadow, flares, rings, sunspots, the echoes, the burning edge and the
    /// nova timer, and to keep their own player inside the burning edge.
    public class EclipseState
    {
        public EclipseStage Stage, TransTo;
        public EclipseTurn Turn;
        public float TurnT, TransT;     // seconds into the turn; > 0: changing stage (can't be hurt), counting down
        public float MoonA, MoonD;      // the moon's angle round the sun and its distance (0: no moon)
        public float FlashT;            // > 0: the sun is swelling, this long until it flashes
        public int FlashLeft;           // flashes still to come this turn
        public float FlashLen;          // how long this swell lasts (for drawing it)
        public int SweepOn;             // the Lich echo's beams: 0 off, 1 warning, 2 live
        public float SweepA;
        public float DiskT = -1f;       // the Singularity echo's disk: seconds since it started (< 0: none)
        public float Inset;             // Last Light's burning edge: how far in from each side
        public float NovaT;             // Totality: seconds until the nova
        public readonly List<EclipseFlare> Flares = new List<EclipseFlare>();
        public readonly List<EclipseRing> Rings = new List<EclipseRing>();
        public readonly List<Sunspot> Spots = new List<Sunspot>();

        // Host only.
        public int Step = -1, Fired;
        public float TurnLen, ActT, Orbit, SweepT, DrainT, MoonFireT, BurnT;
    }

    /// The Eclipse (wave 50, the final boss): five stages by its health, then a last stand. A sun and its moon line up
    /// as the fight goes on, and each stage keeps a mechanic from the one before.
    ///   Corona (100-80%): it orbits the middle as before; Spiral and Corona rings (as before), Solar flares.
    ///   Penumbra (80-60%): the moon orbits it, soaking your shots; its shadow is the only shelter from the Eclipse
    ///     flash. Lunar wake (the moon trails shots), Eclipse flash, Gravity (as before). Two Phantoms.
    ///   Umbra (60-40%): the arena goes dark; three sunspots drift about and the dark drains you. Crescents (they
    ///     loop out through you and back), Closing umbra (rings of shadow with two gaps), Dark spiral. Two Hexers.
    ///   Echoes (40-20%): turns borrowed from the bosses before it: the Colossus's stomps, the Lich's sweep, the Storm
    ///     Herald's strike rain, the Singularity's pull and disk. The moon shoots back.
    ///   Last Light (20-5%): the edges of the arena burn and close in; Frantic spiral, Shadow nova (two flashes, the
    ///     moon jumps between them), Solar storm (flares and crescents).
    ///   Totality (below 5%): the moon is gone; a 12 s nova timer while it takes +50% damage. The nova hits everyone
    ///     for 70% of their health and heals it a little, then the timer starts again.
    /// Built from the mockup in Docs/Mockups/eclipse-5-stage.html. Drawn in EclipseLook.cs.
    public partial class Game
    {
        const float EclipsePenumbraAt = 0.8f, EclipseUmbraAt = 0.6f, EclipseEchoesAt = 0.4f, EclipseLastLightAt = 0.2f, EclipseTotalityAt = 0.05f;
        public const float EclipseTransTime = 2.5f;
        public const float EclipseOrbit = 140f;
        public const float MoonDist = 300f, MoonDistLast = 220f, MoonRadius = 42f, MoonSpin = 0.35f, MoonWakeSpin = 1.2f;
        public const float ShadowSpread = 0.1f;  // how fast the moon's shadow widens with distance
        public const float FlashCharge = 3f, NovaFirst = 2.5f, NovaSecond = 3f;
        public const float FlareWarn = 1f, FlareLive = 0.45f, FlareLen = 1300f, FlareWidth = 40f;
        public const float SunspotRadius = 120f, SunspotSpeed = 35f, DarkDrain = 6f;
        public const float UmbraRingStart = 950f, UmbraRingSpeed = 130f, UmbraRingWidth = 30f, UmbraGapHalf = 0.35f;
        public const float EchoSweepLen = 750f, EchoSweepWarn = 1f, EchoSweepSpin = 35f * MathX.Pi / 180f, EchoSweepWidth = 16f;
        public const int DiskOrbs = 12;
        public const float DiskSpin = 1.1f, DiskGrow = 95f, DiskStart = 90f, EchoDiskOrbR = 15f, DiskTime = 6f;
        public const float EdgeMax = 160f, EdgeSpeed = 8f, EdgeBurn = 20f;
        public const float NovaTime = 12f, NovaFrac = 0.7f, EclipseNovaHeal = 0.02f, TotalityMult = 1.5f;
        const float CrescentSpeed = 420f, CrescentLoop = 250f;

        static readonly EclipseTurn[][] EclipseTurns =
        {
            new[] { EclipseTurn.Spiral, EclipseTurn.Rest, EclipseTurn.CoronaRings, EclipseTurn.Rest, EclipseTurn.Flares, EclipseTurn.Rest },
            new[] { EclipseTurn.LunarWake, EclipseTurn.Rest, EclipseTurn.Flash, EclipseTurn.Rest, EclipseTurn.Gravity, EclipseTurn.Rest },
            new[] { EclipseTurn.Crescents, EclipseTurn.Rest, EclipseTurn.ClosingUmbra, EclipseTurn.Rest, EclipseTurn.DarkSpiral, EclipseTurn.Rest },
            new[] { EclipseTurn.EchoColossus, EclipseTurn.Rest, EclipseTurn.EchoLich, EclipseTurn.Rest, EclipseTurn.EchoHerald, EclipseTurn.Rest, EclipseTurn.EchoSingularity, EclipseTurn.Rest },
            new[] { EclipseTurn.FranticSpiral, EclipseTurn.Rest, EclipseTurn.ShadowNova, EclipseTurn.Rest, EclipseTurn.SolarStorm, EclipseTurn.Rest },
            new[] { EclipseTurn.Totality },
        };
        static readonly float[][] EclipseTimes =
        {
            new[] { 6f, 1f, 5f, 1f, 6f, 1f },
            new[] { 7f, 1f, 5f, 1f, 6f, 1f },
            new[] { 6f, 1f, 7f, 1f, 5f, 1f },
            new[] { 6f, 1f, 6f, 1f, 6f, 1f, 6f, 1f },
            new[] { 6f, 1f, 6f, 1f, 6f, 1f },
            new[] { 999f },
        };

        EclipseState EclipseOf(Enemy e) => e.Eclipse ?? (e.Eclipse = new EclipseState());

        public static bool EclipseImmune(Enemy e) => e.Type == EnemyType.Eclipse && e.Eclipse != null && e.Eclipse.TransT > 0f;

        /// Totality: it takes +50% damage while the nova builds.
        public static bool EclipseExposed(Enemy e) => e.Type == EnemyType.Eclipse && e.Eclipse != null && e.Eclipse.Stage == EclipseStage.Totality && e.Eclipse.TransT <= 0f;

        float FlareDamage => 25f + Wave * 0.3f;        // 40 at wave 50
        float EclipseFlashDamage => 40f + Wave * 0.3f; // 55
        float UmbraRingDamage => 20f + Wave * 0.3f;    // 35
        float EchoDamage => 15f + Wave * 0.3f;         // 30

        /// The Eclipse on the field (its fight state set up), or null.
        public Enemy TheEclipse()
        {
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Eclipse && e.Eclipse != null && e.Hp > 0f && !IsSpawning(e))
                    return e;
            return null;
        }

        public static V2 MoonPos(Enemy e, V2 sun) => sun + V2.FromAngle(e.Eclipse.MoonA) * e.Eclipse.MoonD;

        /// Inside the moon's shadow: beyond the moon, in a cone pointing away from the sun that widens a little with distance.
        public static bool InMoonShadow(EclipseState s, V2 sun, V2 p, float pad = 0f)
        {
            if (s == null || s.MoonD <= 0f)
                return false;
            V2 u = V2.FromAngle(s.MoonA), rel = p - sun;
            float along = rel.X * u.X + rel.Y * u.Y, perp = Math.Abs(rel.X * u.Y - rel.Y * u.X);
            return along > s.MoonD && perp < MoonRadius * 0.9f + (along - s.MoonD) * ShadowSpread - pad;
        }

        /// How fast the moon turns round the sun now (it holds still while the sun swells for a flash).
        public static float MoonSpinNow(EclipseState s) => s.FlashT > 0f ? 0f : s.Turn == EclipseTurn.LunarWake ? MoonWakeSpin : MoonSpin;

        /// In one of the Umbra's sunspots.
        public static bool InSunlight(EclipseState s, V2 p, float pad = 0f)
        {
            foreach (var sp in s.Spots)
                if (sp.Pos.DistanceTo(p) < SunspotRadius - pad)
                    return true;
            return false;
        }

        /// Where the Singularity echo's disk orbs are now.
        public static V2 DiskOrbAt(V2 sun, float diskT, int k) =>
            sun + V2.FromAngle(k * MathX.Tau / DiskOrbs + diskT * DiskSpin) * (DiskStart + diskT * DiskGrow);

        public static bool FlareLiveNow(EclipseFlare f) => f.T >= FlareWarn && f.T < FlareWarn + FlareLive;

        /// Where the Eclipse wants to be: orbiting the middle as it always has (holding its place while the sun swells
        /// for a flash), in the middle for Totality.
        V2 EclipseGoal(Enemy e)
        {
            var s = e.Eclipse;
            var mid = new V2(Width / 2f, Height / 2f);
            if (s == null)
                return mid + V2.FromAngle(e.T * 0.3f) * EclipseOrbit;
            if (s.Stage == EclipseStage.Totality)
                return mid;
            return mid + V2.FromAngle(s.Orbit) * EclipseOrbit;
        }

        void EclipseSpecial(Enemy e, V2 tpos, float dt)
        {
            var s = EclipseOf(e);
            if (s.Step < 0)
            {
                s.Orbit = e.T * 0.3f;
                EnterEclipseStage(e, EclipseStage.Corona);
            }
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;

            if (s.TransT > 0f)
            {
                s.TransT -= dt;
                if (s.TransT <= 0f)
                {
                    s.TransT = 0f;
                    EnterEclipseStage(e, s.TransTo);
                    StageStartBurst(e, EclipseColor(s.Stage));
                }
                return;
            }
            if (s.Stage == EclipseStage.Corona && frac <= EclipsePenumbraAt) { StartEclipseTransition(e, EclipseStage.Penumbra); return; }
            if (s.Stage == EclipseStage.Penumbra && frac <= EclipseUmbraAt) { StartEclipseTransition(e, EclipseStage.Umbra); return; }
            if (s.Stage == EclipseStage.Umbra && frac <= EclipseEchoesAt) { StartEclipseTransition(e, EclipseStage.Echoes); return; }
            if (s.Stage == EclipseStage.Echoes && frac <= EclipseLastLightAt) { StartEclipseTransition(e, EclipseStage.LastLight); return; }
            if (s.Stage == EclipseStage.LastLight && frac <= EclipseTotalityAt)
                EnterEclipseStage(e, EclipseStage.Totality);  // straight away, as the others' last stands do

            s.TurnT += dt;
            if (s.FlashT <= 0f && s.Stage != EclipseStage.Totality)
                s.Orbit += dt * 0.3f;
            if (s.MoonD > 0f)
                s.MoonA += MoonSpinNow(s) * dt;
            if (s.Stage == EclipseStage.LastLight)
                s.Inset = Math.Min(EdgeMax, s.Inset + EdgeSpeed * dt);
            TickEclipseHazards(e, s, dt);

            var alive = AlivePlayersList();
            Player target;
            Players.TryGetValue(e.TargetId, out target);
            if (target == null || target.Downed)
                target = FirstAlive();
            V2 at = target != null ? target.Pos : tpos;

            s.ActT -= dt;
            if (s.ActT <= 0f)
                switch (s.Turn)
                {
                    case EclipseTurn.Spiral:  // as before: four arms
                        s.ActT = 0.07f;
                        e.Ang += 0.21f;
                        for (int k = 0; k < 4; k++)
                            EnemyShot(e, e.Ang + MathX.Tau * k / 4f, 150f, Defs.SmallShot);
                        break;
                    case EclipseTurn.CoronaRings:  // as before: 24 big shots
                        s.ActT = 1f;
                        e.Ang += 0.13f;
                        for (int i = 0; i < 24; i++)
                            EnemyShot(e, e.Ang + MathX.Tau * i / 24f, 110f, Defs.BigShot);
                        break;
                    case EclipseTurn.Flares:  // one at each player and one at random
                        s.ActT = 2f;
                        foreach (var p in alive)
                            AddFlare(e, s, (p.Pos - e.Pos).Angle);
                        AddFlare(e, s, Rand(0f, MathX.Tau));
                        break;
                    case EclipseTurn.LunarWake:  // the moon drops a slow shot behind it
                    {
                        s.ActT = 0.12f;
                        V2 m = MoonPos(e, e.Pos), dir = V2.FromAngle(s.MoonA);
                        SpawnBullet(Defs.SmallShot, m + dir * MoonRadius, dir * 60f, 0f, 10f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = e.Type;
                        break;
                    }
                    case EclipseTurn.Gravity:  // as before: the pull, with 4-way triple shots
                        s.ActT = 0.5f;
                        e.Ang += 0.3f;
                        for (int d = 0; d < 4; d++)
                            for (int k = 0; k < 3; k++)
                                EnemyShot(e, e.Ang + MathX.Tau * d / 4f, 120f + k * 50f, Defs.SmallShot);
                        break;
                    case EclipseTurn.Crescents:
                        s.ActT = 1.2f;
                        for (int i = 0; i < alive.Count; i++)
                            ThrowCrescent(e, alive[i].Pos, i % 2 == 0 ? 1 : -1);
                        Sounds.Add(Sfx.SpellZap);
                        break;
                    case EclipseTurn.ClosingUmbra:
                        if (s.Fired < 2)
                        {
                            s.Fired++;
                            s.ActT = 3.4f;
                            float g = Rand(0f, MathX.Tau);
                            s.Rings.Add(new EclipseRing { Center = e.Pos, R = UmbraRingStart, Gap1 = g, Gap2 = MathX.Fmod(g + Rand(2f, 4f), MathX.Tau) });
                            Sounds.Add(Sfx.BossPulse);
                        }
                        else
                            s.ActT = 99f;
                        break;
                    case EclipseTurn.DarkSpiral:
                        s.ActT = 0.09f;
                        e.Ang += 0.17f;
                        for (int k = 0; k < 3; k++)
                            EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 130f, Defs.SmallShot);
                        break;
                    case EclipseTurn.EchoColossus:  // a stomp: a ring of 30 big shots with one gap
                    {
                        s.ActT = 1.6f;
                        float gap = (at - e.Pos).Angle + Rand(-1.2f, 1.2f), off = Rand(0f, 1f);
                        for (int i = 0; i < 30; i++)
                        {
                            float a = off + MathX.Tau * i / 30f;
                            if (Math.Abs(MathX.Fmod(a - gap + MathX.Pi, MathX.Tau) - MathX.Pi) < 1.5f * MathX.Tau / 30f)
                                continue;
                            EnemyShot(e, a, 180f, Defs.BigShot);
                        }
                        BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.85f, 0.63f, 0.4f), 120f, 0.4f, Sfx.BossPulse);
                        break;
                    }
                    case EclipseTurn.EchoLich:
                        if (s.SweepOn == 0 && s.Fired == 0)
                        {
                            s.Fired = 1;
                            s.SweepOn = 1;
                            s.SweepT = 0f;
                            s.SweepA = (at - e.Pos).Angle + MathX.Pi / 2f;
                        }
                        s.ActT = 99f;
                        break;
                    case EclipseTurn.EchoHerald:  // strike rain
                        s.ActT = 0.6f;
                        for (int i = 0; i < 3; i++)
                            AddHazard(RandomInArena(80f), 55f, 0.9f);
                        if (alive.Count > 0)
                            AddHazard(alive[rng.Next(alive.Count)].Pos, 55f, 0.9f);
                        break;
                    case EclipseTurn.EchoSingularity:
                        if (s.DiskT < 0f)
                            s.DiskT = 0f;
                        s.ActT = 99f;
                        break;
                    case EclipseTurn.FranticSpiral:  // its old final spiral: five arms, switching every 3 s
                        s.ActT = 0.06f;
                        e.Ang += (int)(s.TurnT / 3f) % 2 == 0 ? 0.19f : -0.19f;
                        for (int k = 0; k < 5; k++)
                            EnemyShot(e, e.Ang + MathX.Tau * k / 5f, 170f, Defs.SmallShot);
                        break;
                    case EclipseTurn.SolarStorm:  // flares at everyone, then crescents, by turns
                        s.ActT = 0.8f;
                        if (s.Fired++ % 2 == 0)
                            foreach (var p in alive)
                                AddFlare(e, s, (p.Pos - e.Pos).Angle);
                        else
                            for (int i = 0; i < alive.Count; i++)
                                ThrowCrescent(e, alive[i].Pos, i % 2 == 0 ? 1 : -1);
                        break;
                    case EclipseTurn.Totality:  // slow rings of big shots
                        s.ActT = 1.4f;
                        e.Ang += 0.16f;
                        for (int i = 0; i < 20; i++)
                            EnemyShot(e, e.Ang + MathX.Tau * i / 20f, 100f, Defs.BigShot);
                        break;
                    default:  // breathers (EclipseFire), Flash and Shadow nova (TickEclipseHazards)
                        s.ActT = 0.1f;
                        break;
                }
            // The Echoes: the moon shoots back, three shots at the nearest player every 2 s.
            if (s.Stage == EclipseStage.Echoes && s.MoonD > 0f && (s.MoonFireT -= dt) <= 0f)
            {
                s.MoonFireT = 2f;
                V2 m = MoonPos(e, e.Pos);
                Player near = null;
                foreach (var p in alive)
                    if (near == null || p.Pos.DistanceSq(m) < near.Pos.DistanceSq(m))
                        near = p;
                if (near != null)
                    for (int i = -1; i <= 1; i++)
                    {
                        V2 dir = V2.FromAngle((near.Pos - m).Angle + i * 0.1f);
                        SpawnBullet(Defs.SmallShot, m + dir * MoonRadius, dir * 240f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = e.Type;
                    }
            }
            if (s.Stage != EclipseStage.Totality && s.TurnT >= s.TurnLen)
                NextEclipseTurn(e, s);
        }

        /// A crescent blade: it flies a loop that passes through `at` and comes back round to where it was thrown.
        void ThrowCrescent(Enemy e, V2 at, int side)
        {
            float d = Math.Max(40f, at.DistanceTo(e.Pos)), r = Math.Max(CrescentLoop, d / 2f + 30f);
            float th = (float)Math.Asin(Math.Min(1f, d / (2f * r)));
            float ang = (at - e.Pos).Angle - side * th, spin = side * CrescentSpeed / r;
            V2 dir = V2.FromAngle(ang);
            SpawnBullet(Defs.CrescentShot, e.Pos + dir * e.Radius, dir * CrescentSpeed, spin, MathX.Tau * r / CrescentSpeed, Defs.BulletDamage(Defs.CrescentShot), 0, true).Source = e.Type;
        }

        void AddFlare(Enemy e, EclipseState s, float ang)
        {
            s.Flares.Add(new EclipseFlare { From = e.Pos, Ang = ang });
            Sounds.Add(Sfx.BossCharge);
        }

        static Rgba EclipseColor(EclipseStage stage)
        {
            switch (stage)
            {
                case EclipseStage.Penumbra: return new Rgba(1f, 0.7f, 0.36f);
                case EclipseStage.Umbra: return new Rgba(0.61f, 0.48f, 1f);
                case EclipseStage.Echoes: return new Rgba(1f, 0.44f, 0.64f);
                case EclipseStage.LastLight: return new Rgba(1f, 0.54f, 0.24f);
                case EclipseStage.Totality: return new Rgba(1f, 0.3f, 0.3f);
                default: return new Rgba(1f, 0.82f, 0.4f);
            }
        }

        /// Host: flares, the flash, closing rings, sunspots and the dark, the sweep, the disk and its pull, the burning
        /// edge and the nova.
        void TickEclipseHazards(Enemy e, EclipseState s, float dt)
        {
            // Flares burn whoever stands in their lane while they're live.
            foreach (var f in s.Flares)
            {
                bool was = FlareLiveNow(f);
                f.T += dt;
                if (!was && FlareLiveNow(f))
                    Sounds.Add(Sfx.SpellFire);
                if (!FlareLiveNow(f))
                    continue;
                V2 tip = f.From + V2.FromAngle(f.Ang) * FlareLen;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && !f.Hit.Contains(p.Id) && SegmentDistance(p.Pos, f.From, tip) < FlareWidth / 2f + p.Def.HitR)
                    {
                        f.Hit.Add(p.Id);
                        Damage(p, FlareDamage, "The Eclipse (solar flare)");
                    }
            }
            s.Flares.RemoveAll(f => f.T >= FlareWarn + FlareLive);

            // The flash: everyone outside the moon's shadow is hit.
            if (s.FlashT > 0f)
            {
                s.FlashT -= dt;
                if (s.FlashT <= 0f)
                {
                    s.FlashT = 0f;
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.InvulnT <= 0f && !InMoonShadow(s, e.Pos, p.Pos, -p.Def.HitR * 0.5f))
                            Damage(p, EclipseFlashDamage, "The Eclipse (eclipse flash)");
                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.95f, 0.8f), 1600f, 0.6f, Sfx.Roar);
                    if (--s.FlashLeft > 0)
                    {
                        s.MoonA += MathX.Tau / 5f;  // the moon jumps round before the second
                        s.FlashT = s.FlashLen = NovaSecond;
                        BroadcastFx(FxKind.Ring, MoonPos(e, e.Pos), new Rgba(0.79f, 0.82f, 0.89f), 90f, 0.4f, Sfx.Blink);
                    }
                }
            }

            // Closing umbra: rings of shadow close in on where the sun was; touching one away from its gaps hurts.
            foreach (var r in s.Rings)
            {
                r.R -= UmbraRingSpeed * dt;
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f || r.Hit.Contains(p.Id))
                        continue;
                    if (InUmbraRing(r, p.Pos, p.Def.HitR))
                    {
                        r.Hit.Add(p.Id);
                        Damage(p, UmbraRingDamage, "The Eclipse (closing umbra)");
                    }
                }
            }
            s.Rings.RemoveAll(r => r.R < 70f);

            // Sunspots drift; outside them the dark drains you (without the brief immunity a hit gives).
            foreach (var sp in s.Spots)
                MoveSunspot(sp, dt, (float)(rng.NextDouble() - 0.5) * 1.2f * dt);
            if (s.Stage == EclipseStage.Umbra && (s.DrainT -= dt) <= 0f)
            {
                s.DrainT = 0.5f;
                foreach (var p in Players.Values)
                    if (!p.Downed && !InSunlight(s, p.Pos))
                        HurtNoIframes(p, DarkDrain * 0.5f, "The Eclipse (darkness)");
            }

            // The Lich echo: two beams opposite each other, a warning, then they turn.
            if (s.SweepOn > 0)
            {
                s.SweepT += dt;
                if (s.SweepOn == 1 && s.SweepT >= EchoSweepWarn)
                {
                    s.SweepOn = 2;
                    Sounds.Add(Sfx.BossCharge);
                }
                if (s.SweepOn == 2)
                {
                    s.SweepA += EchoSweepSpin * dt;
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed || p.InvulnT > 0f)
                            continue;
                        for (int k = 0; k < 2; k++)
                            if (SegmentDistance(p.Pos, e.Pos, e.Pos + V2.FromAngle(s.SweepA + k * MathX.Pi) * EchoSweepLen) < EchoSweepWidth / 2f + p.Def.HitR)
                            {
                                Damage(p, EchoDamage, "The Eclipse (echo of the Lich)");
                                break;
                            }
                    }
                }
            }

            // The Singularity echo: a pull for 3.5 s, and a disk of orbs spinning out.
            if (s.DiskT >= 0f)
            {
                s.DiskT += dt;
                if (s.DiskT < 3.5f)
                {
                    PullCenter = e.Pos;
                    PullStrength = 110f;
                }
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    for (int k = 0; k < DiskOrbs; k++)
                        if (DiskOrbAt(e.Pos, s.DiskT, k).DistanceTo(p.Pos) < EchoDiskOrbR + p.Def.HitR)
                        {
                            Damage(p, EchoDamage * 0.7f, "The Eclipse (echo of the Singularity)");
                            break;
                        }
                }
                if (s.DiskT > DiskTime)
                    s.DiskT = -1f;
            }
            if (s.Turn == EclipseTurn.Gravity && s.TurnT < 3.5f)
            {
                PullCenter = e.Pos;
                PullStrength = 120f;  // as before
            }

            // Last Light: the burning edge (each game keeps its own player inside it: KeepInEclipse).
            if (s.Inset > 0f && (s.BurnT -= dt) <= 0f)
            {
                s.BurnT = 0.25f;
                foreach (var p in Players.Values)
                    if (!p.Downed && OnBurningEdge(s, p.Pos, p.Def.HitR + 3f))
                        HurtNoIframes(p, EdgeBurn * 0.25f, "The Eclipse (burning edge)");
            }

            // Totality: the nova.
            if (s.Stage == EclipseStage.Totality)
            {
                s.NovaT -= dt;
                if (s.NovaT <= 0f)
                {
                    s.NovaT = NovaTime;
                    foreach (var p in Players.Values)
                        if (!p.Downed)
                        {
                            p.InvulnT = 0f;
                            Damage(p, p.MaxHp * NovaFrac, "The Eclipse (nova)");
                        }
                    e.Hp = Math.Min(e.MaxHp * EclipseTotalityAt, e.Hp + e.MaxHp * EclipseNovaHeal);
                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.9f, 0.8f), 1700f, 1f, Sfx.Roar);
                    DoLog("the Eclipse: nova");
                }
            }
        }

        /// A dark tick or a burn: it hurts, but doesn't give the brief immunity a hit does.
        void HurtNoIframes(Player p, float amount, string source)
        {
            float inv = p.InvulnT;
            Damage(p, amount, source);
            if (!p.Downed)
                p.InvulnT = inv;
        }

        static void MoveSunspot(Sunspot sp, float dt, float turn)
        {
            sp.Dir += turn;
            V2 next = sp.Pos + V2.FromAngle(sp.Dir) * SunspotSpeed * dt;
            const float m = 160f;
            if (next.X < m || next.X > Width - m)
                sp.Dir = MathX.Pi - sp.Dir;
            if (next.Y < m || next.Y > Height - m)
                sp.Dir = -sp.Dir;
            sp.Pos = new V2(Math.Max(m, Math.Min(Width - m, next.X)), Math.Max(m, Math.Min(Height - m, next.Y)));
        }

        public static bool InUmbraGap(EclipseRing r, V2 p)
        {
            float a = (p - r.Center).Angle;
            return Math.Abs(MathX.Fmod(a - r.Gap1 + MathX.Pi, MathX.Tau) - MathX.Pi) < UmbraGapHalf
                || Math.Abs(MathX.Fmod(a - r.Gap2 + MathX.Pi, MathX.Tau) - MathX.Pi) < UmbraGapHalf;
        }

        public static bool InUmbraRing(EclipseRing r, V2 p, float pad) =>
            Math.Abs(p.DistanceTo(r.Center) - r.R) < UmbraRingWidth / 2f + pad && !InUmbraGap(r, p);

        public static bool OnBurningEdge(EclipseState s, V2 p, float pad) =>
            s.Inset > 0f && (p.X < s.Inset + pad || p.Y < s.Inset + pad || p.X > Width - s.Inset - pad || p.Y > Height - s.Inset - pad);

        /// Your own game keeps you inside Last Light's burning edge (KeepInArena).
        V2 KeepInEclipse(V2 pos, float r)
        {
            float inset = 0f;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Eclipse && e.Eclipse != null && e.Hp > 0f && e.Eclipse.TransT <= 0f)
                    inset = e.Eclipse.Inset;
            if (inset <= 0f)
                return pos;
            return new V2(Math.Max(inset + r, Math.Min(Width - inset - r, pos.X)), Math.Max(inset + r, Math.Min(Height - inset - r, pos.Y)));
        }

        /// Collide: the moon soaks players' shots.
        Enemy moonEclipse;

        void FindEclipseMoon()
        {
            moonEclipse = null;
            var e = TheEclipse();
            if (e != null && e.Eclipse.MoonD > 0f && e.Eclipse.TransT <= 0f)
                moonEclipse = e;
        }

        bool MoonStops(Bullet b) =>
            moonEclipse != null && !Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(MoonPos(moonEclipse, moonEclipse.Pos)) < MoonRadius + b.Radius;

        void EnterEclipseStage(Enemy e, EclipseStage stage)
        {
            var s = EclipseOf(e);
            s.Stage = stage;
            e.Phase = (byte)stage;
            s.Step = -1;
            s.Flares.Clear();
            s.Rings.Clear();
            s.Spots.Clear();
            s.FlashT = 0f;
            s.SweepOn = 0;
            s.DiskT = -1f;
            s.MoonFireT = 2f;
            if (stage == EclipseStage.Penumbra || stage == EclipseStage.Umbra || stage == EclipseStage.Echoes || stage == EclipseStage.LastLight)
            {
                s.MoonD = stage == EclipseStage.LastLight ? MoonDistLast : MoonDist;
                if (stage == EclipseStage.Penumbra)
                    s.MoonA = Rand(0f, MathX.Tau);
            }
            else
                s.MoonD = 0f;
            if (stage == EclipseStage.Penumbra)
                for (int i = 0; i < 2; i++)
                    SpawnEnemyAt(EnemyType.Phantom, e.Pos + V2.FromAngle(MathX.Pi * i) * 200f, 0.8f, e.Id);
            if (stage == EclipseStage.Umbra)
            {
                for (int i = 0; i < 2; i++)
                    SpawnEnemyAt(EnemyType.Hexer, e.Pos + V2.FromAngle(MathX.Pi * i + MathX.Pi / 2f) * 200f, 0.8f, e.Id);
                for (int i = 0; i < 3; i++)
                    s.Spots.Add(new Sunspot { Pos = new V2(300f + i * 500f, i == 1 ? 250f : Height - 220f), Dir = Rand(0f, MathX.Tau) });
            }
            if (stage == EclipseStage.Totality)
            {
                s.Inset = 0f;
                s.NovaT = NovaTime;
                BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.79f, 0.82f, 0.89f), 400f, 0.8f, Sfx.Roar);  // the moon breaks apart
            }
            NextEclipseTurn(e, s);
            DoLog("the Eclipse: " + stage);
        }

        void StartEclipseTransition(Enemy e, EclipseStage to)
        {
            var s = EclipseOf(e);
            s.TransT = EclipseTransTime;
            s.TransTo = to;
            s.Flares.Clear();
            s.Rings.Clear();
            s.FlashT = 0f;
            s.SweepOn = 0;
            s.DiskT = -1f;
            Hazards.Clear();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, EclipseColor(to), 900f, 0.8f, Sfx.BossChime);
            DoLog("the Eclipse: changing to " + to);
        }

        void NextEclipseTurn(Enemy e, EclipseState s)
        {
            var turns = EclipseTurns[(int)s.Stage];
            s.Step = (s.Step + 1) % turns.Length;
            s.Turn = turns[s.Step];
            s.TurnLen = EclipseTimes[(int)s.Stage][s.Step];
            s.TurnT = 0f;
            s.ActT = 0.3f;
            s.Fired = 0;
            s.SweepOn = 0;
            s.DiskT = -1f;
            if (s.Turn == EclipseTurn.Flash)
            {
                s.FlashT = s.FlashLen = FlashCharge;
                s.FlashLeft = 1;
            }
            else if (s.Turn == EclipseTurn.ShadowNova)
            {
                s.FlashT = s.FlashLen = NovaFirst;
                s.FlashLeft = 2;
            }
            e.FireT = 0.3f;
        }

        /// Its breathers: an aimed volley (or a ring) between turns.
        void EclipseFire(Enemy e, float aim)
        {
            var s = EclipseOf(e);
            if (s.TransT > 0f || s.Turn != EclipseTurn.Rest)
            {
                e.FireT = 0.2f;
                return;
            }
            e.FireT = 1f;
            switch (s.Stage)
            {
                case EclipseStage.Corona:  // three sun shots
                    for (int i = -1; i <= 1; i++)
                        EnemyShot(e, aim + i * 0.12f, 280f, Defs.BigShot);
                    break;
                case EclipseStage.Penumbra:
                case EclipseStage.LastLight:  // as before: five piercing shots
                    for (int i = -2; i <= 2; i++)
                        EnemyShot(e, aim + i * 0.14f, 210f, Defs.PierceShot);
                    break;
                case EclipseStage.Echoes:  // a corona ring
                    e.Ang += 0.13f;
                    for (int i = 0; i < 24; i++)
                        EnemyShot(e, e.Ang + MathX.Tau * i / 24f, 110f, Defs.BigShot);
                    break;
            }
        }

        void SyncEclipse(NetSync s, Enemy e)
        {
            bool has = e.Eclipse != null;
            s.Bool(ref has);
            if (!has)
                return;
            var q = e.Eclipse ?? (e.Eclipse = new EclipseState { Step = 0 });
            int stage = (int)q.Stage, turn = (int)q.Turn, transTo = (int)q.TransTo;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Byte(ref transTo);
            s.Float(ref q.TurnT);
            s.Float(ref q.TransT);
            s.Float(ref q.MoonA);
            s.Float(ref q.MoonD);
            s.Float(ref q.FlashT);
            s.Float(ref q.FlashLen);
            s.Byte(ref q.FlashLeft);
            s.Byte(ref q.SweepOn);
            s.Float(ref q.SweepA);
            s.Float(ref q.DiskT);
            s.Float(ref q.Inset);
            s.Float(ref q.NovaT);
            SyncList(s, q.Flares, (n, f) => { n.Vec(ref f.From); n.Float(ref f.Ang); n.Float(ref f.T); });
            SyncList(s, q.Rings, (n, r) => { n.Vec(ref r.Center); n.Float(ref r.R); n.Float(ref r.Gap1); n.Float(ref r.Gap2); });
            SyncList(s, q.Spots, (n, sp) => { n.Vec(ref sp.Pos); n.Float(ref sp.Dir); });
            if (s.Reading)
            {
                q.Stage = (EclipseStage)stage;
                q.Turn = (EclipseTurn)turn;
                q.TransTo = (EclipseStage)transTo;
            }
        }

        /// Clients, each frame: the clocks run between snapshots (the turn, the moon, the flash, flares, rings,
        /// sunspots, the sweep, the disk and the nova).
        void UpdateEclipseView(float dt)
        {
            if (IsHost)
                return;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Eclipse && e.Eclipse != null)
                {
                    var s = e.Eclipse;
                    if (s.TransT > 0f)
                    {
                        s.TransT = Math.Max(0f, s.TransT - dt);
                        continue;
                    }
                    s.TurnT += dt;
                    if (s.MoonD > 0f)
                        s.MoonA += MoonSpinNow(s) * dt;
                    s.FlashT = Math.Max(0f, s.FlashT - dt);
                    foreach (var f in s.Flares)
                        f.T += dt;
                    foreach (var r in s.Rings)
                        r.R -= UmbraRingSpeed * dt;
                    foreach (var sp in s.Spots)
                        MoveSunspot(sp, dt, 0f);
                    if (s.SweepOn == 2)
                        s.SweepA += EchoSweepSpin * dt;
                    if (s.DiskT >= 0f)
                        s.DiskT += dt;
                    if (s.Stage == EclipseStage.LastLight)
                        s.Inset = Math.Min(EdgeMax, s.Inset + EdgeSpeed * dt);
                    if (s.Stage == EclipseStage.Totality)
                        s.NovaT = Math.Max(0f, s.NovaT - dt);
                }
        }
    }
}
