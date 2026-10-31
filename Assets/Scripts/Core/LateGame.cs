using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A warning circle on the ground; when its timer runs out, lightning strikes it.
    public class Hazard
    {
        public int Id;
        public V2 Pos;
        public float Radius, T, Duration;
        public byte Kind;  // HazardLightning (the Storm Herald) or HazardHand (the Lich's dead hands)
    }

    /// Waves 16-50: the late-game enemies, the bosses from wave 25 on, and winning at wave 50 (host only).
    public partial class Game
    {
        public bool Endless;  // kept playing after beating wave 50
        public readonly Dictionary<int, Hazard> Hazards = new Dictionary<int, Hazard>();
        int nextHazardId = 1;

        static bool IsLateGame(EnemyType t) => Defs.Get(t).Late;

        /// Too heavy to slow down, shove around or knock back.
        static bool Heavy(Enemy e) => e.Def.IsBoss || e.Def.Heavy;

        /// The Devourer heals from your shots while it's hungry (its Hungry turn, Devourer.cs).
        public static bool Hungry(Enemy e) => e.Type == EnemyType.Devourer && e.Phase == 1;

        // --- Winning ------------------------------------------------------------

        void Win()
        {
            State = MatchState.Victory;
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastRunStats();
            Recorder?.End(this, "victory");
            DoLog("victory at wave " + Wave + ", score " + Score);
        }

        /// Host, from the victory screen: keep going past wave 50.
        public void ContinueEndless()
        {
            if (!IsHost || State != MatchState.Victory)
                return;
            Endless = true;
            Recorder?.ContinueEndless();
            WaveCleared();
        }

        // --- Movement -----------------------------------------------------------

        V2 LateMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.Phantom:
                    return KeepDistance(dir, dist, 250f) + dir.Orthogonal() * e.Strafe * 0.7f;
                case EnemyType.Hexer:
                    return KeepDistance(dir, dist, 300f) + dir.Orthogonal() * e.Strafe * 0.5f;
                case EnemyType.Juggernaut:
                    return KeepDistance(dir, dist, 140f);
                case EnemyType.Colossus:
                    return ColossusMove(e, dir, dist, ref speed);  // Colossus.cs
                case EnemyType.Duelist:
                    return DuelistMove(e, tpos, dir, dist, ref speed);  // Duelist.cs
                case EnemyType.Devourer:
                    return DevourMove(e, dir, dist, ref speed);  // Devourer.cs
                case EnemyType.Lich:
                    return V2.Zero;  // it moves itself: drifting, blinking between graves (Lich.cs)
                case EnemyType.StormHerald:
                    if (HeraldImmune(e))
                        return V2.Zero;  // holds still while it changes stage
                    return (HeraldGoal(e) - e.Pos) / 60f;  // along the top, or the middle for the last stand (Herald.cs)
                case EnemyType.Eclipse:
                {
                    var goal = new V2(Width / 2f, Height / 2f) + V2.FromAngle(e.T * 0.3f) * 140f;
                    return (goal - e.Pos) / 60f;
                }
                case EnemyType.GravityWell:
                {
                    // Holds its corner, bobbing a little.
                    var goal = e.Waypoint + V2.FromAngle(e.T * 0.8f + e.Id) * 12f;
                    return (goal - e.Pos) / 30f;
                }
                default:  // Nest, Decoy, Phylactery stay put
                    return V2.Zero;
            }
        }

        // --- Mechanics ----------------------------------------------------------

        void LateSpecial(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            switch (e.Type)
            {
                case EnemyType.Phantom:
                    if (e.PhaseT <= 0f)
                    {
                        // Blink somewhere near the target, then burst.
                        e.PhaseT = Rand(3f, 4.5f);
                        V2 from = e.Pos;
                        e.Pos = ClampToArena(tpos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(220f, 300f), e.Radius);
                        e.Disp = e.Pos;
                        BroadcastFx(FxKind.Ring, from, e.Def.Color, 30f, 0.3f, Sfx.None);
                        BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 40f, 0.3f, Sfx.Blink);
                        for (int i = 0; i < 12; i++)
                            EnemyShot(e, MathX.Tau * i / 12f, 130f, Defs.SmallShot);
                    }
                    break;
                case EnemyType.Nest:
                    if (e.PhaseT <= 0f)
                    {
                        e.PhaseT = 5f;
                        if (CountChildren(e, EnemyType.Shard) < 6)
                            for (int i = 0; i < 2; i++)
                                SpawnEnemyAt(EnemyType.Shard, e.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * 30f, 0.4f, e.Id);
                    }
                    break;
                case EnemyType.Colossus:
                    ColossusSpecial(e, tpos, dt);  // Colossus.cs: its stages, turns, stomp rings, plates and lava
                    break;
                case EnemyType.Duelist:
                    DuelistSpecial(e, tpos, dt);  // Duelist.cs: its stages, turns, dashes and salutes
                    break;
                case EnemyType.Decoy:
                    if (e.PhaseT <= 0f)
                    {
                        for (int i = 0; i < 16; i++)
                            EnemyShot(e, MathX.Tau * i / 16f, 140f, Defs.SmallShot);
                        e.Hp = 0f;  // pops; decoys give no score, XP or drops
                    }
                    break;
                case EnemyType.Lich:
                    LichSpecial(e, dt);  // Lich.cs
                    break;
                case EnemyType.StormHerald:
                    HeraldSpecial(e, tpos, dt);  // its stages and turns (Herald.cs)
                    break;
                case EnemyType.Devourer:
                    DevourSpecial(e, tpos, dt);  // its stages and turns (Devourer.cs)
                    break;
                case EnemyType.Eclipse:
                {
                    float frac = e.Hp / e.MaxHp;
                    byte phase = (byte)(frac > 0.66f ? 0 : frac > 0.33f ? 1 : 2);
                    if (phase > e.Phase)
                    {
                        e.Phase = phase;
                        BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 300f, 0.8f, Sfx.Roar);
                        var add = phase == 1 ? EnemyType.Phantom : EnemyType.Hexer;
                        for (int i = 0; i < 2; i++)
                            SpawnEnemyAt(add, e.Pos + V2.FromAngle(MathX.Pi * i) * 200f, 0.8f, e.Id);
                        DoLog("The Eclipse enters phase " + (phase + 1));
                    }
                    if (e.Phase >= 1)
                    {
                        // A gravity well for 3.5 s of every 10.
                        e.T2 += dt;
                        if (MathX.Fmod(e.T2, 10f) < 3.5f)
                        {
                            PullCenter = e.Pos;
                            PullStrength = 120f;
                        }
                    }
                    if (e.Phase == 2 && e.PhaseT <= 0f)
                    {
                        e.PhaseT = 2.5f;
                        foreach (var p in Players.Values)
                            if (!p.Downed)
                                AddHazard(p.Pos, 75f, 1.2f);
                    }
                    break;
                }
            }
        }

        void LateFire(Enemy e, V2 tpos, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.Phantom:
                    e.FireT = 1.5f;
                    EnemyShot(e, aim - 0.05f, 200f, Defs.SmallShot);
                    EnemyShot(e, aim + 0.05f, 200f, Defs.SmallShot);
                    break;
                case EnemyType.Hexer:
                    // Two pairs of shots that curve in like pincers.
                    e.FireT = 2f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        EnemyShot(e, aim + s * 0.7f, 170f, Defs.SmallShot, -s * 1.1f);
                        EnemyShot(e, aim + s * 1.2f, 150f, Defs.SmallShot, -s * 1.6f);
                    }
                    break;
                case EnemyType.Juggernaut:
                    e.FireT = 2.5f;
                    e.Ang += 0.2f;
                    for (int k = 0; k < 8; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 8f, 110f, Defs.BigShot);
                    break;
                case EnemyType.Colossus:
                    ColossusFire(e, aim);  // Colossus.cs: its rock volley, between turns
                    break;
                case EnemyType.Duelist:
                    DuelistFire(e, aim);  // Duelist.cs
                    break;
                case EnemyType.Lich:
                    LichFire(e, aim);  // Lich.cs
                    break;
                case EnemyType.StormHerald:
                    HeraldFire(e, tpos, aim);  // Herald.cs
                    break;
                case EnemyType.Devourer:
                    DevourFire(e, tpos, aim);  // Devourer.cs
                    break;
                case EnemyType.Eclipse:
                    switch (e.Phase)
                    {
                        case 0:
                            if ((int)(e.T / 4f) % 2 == 0)
                            {
                                e.FireT = 0.07f;
                                e.Ang += 0.21f;
                                for (int k = 0; k < 4; k++)
                                    EnemyShot(e, e.Ang + MathX.Tau * k / 4f, 150f, Defs.SmallShot);
                            }
                            else
                            {
                                e.FireT = 1f;
                                e.Ang += 0.13f;
                                for (int i = 0; i < 24; i++)
                                    EnemyShot(e, e.Ang + MathX.Tau * i / 24f, 110f, Defs.BigShot);
                            }
                            break;
                        case 1:
                            e.FireT = 0.5f;
                            e.Ang += 0.3f;
                            for (int d = 0; d < 4; d++)
                                for (int k = 0; k < 3; k++)
                                    EnemyShot(e, e.Ang + MathX.Tau * d / 4f, 120f + k * 50f, Defs.SmallShot);
                            for (int i = 0; i < 5; i++)
                                EnemyShot(e, aim + (i - 2) * 0.14f, 210f, Defs.PierceShot);
                            break;
                        default:
                        {
                            // Final phase: a dense five-armed spiral that switches direction.
                            e.FireT = 0.06f;
                            e.Ang += (int)(e.T / 3f) % 2 == 0 ? 0.19f : -0.19f;
                            for (int k = 0; k < 5; k++)
                                EnemyShot(e, e.Ang + MathX.Tau * k / 5f, 170f, Defs.SmallShot);
                            break;
                        }
                    }
                    break;
                default:  // Nest, Decoy, Phylactery and gravity wells don't shoot
                    e.FireT = 99f;
                    break;
            }
        }

        // --- Helpers ------------------------------------------------------------

        int CountChildren(Enemy parent, EnemyType type)
        {
            int n = 0;
            foreach (var o in Enemies.Values)
                if (o.ParentId == parent.Id && o.Type == type)
                    n++;
            return n;
        }

        void SpawnDecoy(V2 pos)
        {
            var d = SpawnEnemyAt(EnemyType.Decoy, pos, 0f, 0);
            d.PhaseT = 5f;  // pops after 5 s unless killed first
            d.FireT = 99f;
        }

        /// A spot at least 250 px from every living player (best of a few tries).
        V2 FarFromPlayers(float radius)
        {
            V2 best = new V2(Width / 2f, Height / 2f);
            float bestDist = -1f;
            for (int t = 0; t < 12; t++)
            {
                var pos = new V2(Rand(radius + 60f, Width - radius - 60f), Rand(radius + 60f, Height - radius - 60f));
                float d = float.MaxValue;
                foreach (var p in Players.Values)
                    if (!p.Downed)
                        d = Math.Min(d, p.Pos.DistanceTo(pos));
                if (d > bestDist)
                {
                    best = pos;
                    bestDist = d;
                }
                if (d >= 250f)
                    break;
            }
            return best;
        }

        // --- Lightning strikes --------------------------------------------------

        Hazard AddHazard(V2 pos, float radius, float delay)
        {
            var h = new Hazard { Id = nextHazardId++, Pos = ClampToArena(pos, 10f), Radius = radius, T = delay, Duration = delay };
            Hazards[h.Id] = h;
            return h;
        }

        void TickHazards(float dt)
        {
            var done = new List<Hazard>();
            foreach (var h in Hazards.Values)
            {
                h.T -= dt;
                if (h.T <= 0f)
                    done.Add(h);
            }
            foreach (var h in done)
            {
                if (h.Kind == HazardHand)
                {
                    HandBursts(h);  // Lich.cs
                    Hazards.Remove(h.Id);
                    continue;
                }
                float dmg = 25f + Wave * 0.3f;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(h.Pos) < h.Radius)
                        Damage(p, dmg, "Storm Herald (lightning)");
                var color = new Rgba(0.7f, 0.85f, 1f);
                BroadcastFx(FxKind.Bolt, new V2(h.Pos.X + Rand(-60f, 60f), -20f), h.Pos, color, 0f, 0.25f, Sfx.SpellZap);
                BroadcastFx(FxKind.Boom, h.Pos, color, h.Radius, 0.3f, Sfx.None);
                Hazards.Remove(h.Id);
            }
        }

        void SyncHazards(NetSync s)
        {
            int n = Hazards.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                Hazards.Clear();
                for (int i = 0; i < n; i++)
                {
                    var h = new Hazard();
                    SyncHazard(s, h);
                    Hazards[h.Id] = h;
                }
            }
            else
                foreach (var h in Hazards.Values)
                    SyncHazard(s, h);
        }

        static void SyncHazard(NetSync s, Hazard h)
        {
            s.Int(ref h.Id);
            s.Vec(ref h.Pos);
            s.Float(ref h.Radius);
            s.Float(ref h.T);
            s.Float(ref h.Duration);
            s.Byte(ref h.Kind);
        }
    }
}
