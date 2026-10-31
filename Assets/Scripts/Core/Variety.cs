using System;
using System.Collections.Generic;

namespace Arena
{
    /// An elite's traits (flags; one from wave 12, two from wave 35). Replicated so every screen draws them.
    [Flags]
    public enum EliteTrait : byte { None = 0, Hasted = 1, Shielded = 2, Volatile = 4, Splitting = 8, Mirrored = 16, Commander = 32 }

    /// A squad: a small team that arrives together from one edge and looks after itself.
    public class SquadDef
    {
        public string Name;
        public int From;              // the first wave it can show up in
        public EnemyType[] Members;   // front first
        public SquadForm Form;
    }

    /// How a squad arrives: in a line across its edge, split to opposite edges, or blinking in around a player.
    public enum SquadForm : byte { Line, Pincer, Around }

    /// More variety between the bosses (built from Docs/Mockups/enemy-variety.html):
    ///   Four new enemies: the Beam Sentry (wave 18: plants itself, aims a line, sweeps a beam), the Scorcher (24:
    ///     circles you leaving embers), the Reaver (28: armoured melee that rakes a cone, then is open for a moment)
    ///     and the Warder (36: puts bubbles on up to three allies, which pop when it dies).
    ///   Elite traits: Hasted, Shielded, Volatile, Splitting, Mirrored, Commander (EliteTrait).
    ///   Squads: about 70% of a regular wave is small teams (Squads) that arrive together and keep each other going:
    ///     a Shielder guards its Snipers, a Medic follows its Brutes, a Lunger waits for a Brute to reach you.
    /// Drawn in VarietyLook.cs.
    public partial class Game
    {
        // The Beam Sentry: Phase 0 walks in, 1 plants, 2 aims (Facing turns after you), 3 sweeps, 4 rests.
        public const float SentryRange = 380f, SentryBeamLen = 900f, SentryBeamWidth = 16f, SentryAim = 1f, SentrySweep = 1.5f, SentryRest = 1.5f, SentryTurn = 0.6f;
        public const float SentrySweepArc = MathX.Pi / 2f;
        // The Scorcher: circles at this distance, dropping embers.
        public const float ScorchRange = 260f, EmberEvery = 0.25f, EmberLife = 3f, EmberRadius = 24f, EmberDps = 15f;
        // The Reaver: Phase 0 chases, 1 winds up (a red cone), 2 has just swiped, 3 is open.
        public const float ReaverWindup = 0.4f, ReaverSwipeR = 110f, ReaverCone = MathX.Pi / 3f, ReaverOpen = 0.7f, ReaverOpenMult = 1.5f, ReaverDamage = 26f;
        // The Warder: wards up to three allies near it every 6 s.
        public const float WarderEvery = 6f, WarderRange = 320f, WarderShare = 0.5f;
        public const int WarderMax = 3;
        // Elite traits.
        public const float HasteMult = 1.4f, ShieldedShare = 0.4f, BubbleRegrow = 4f, VolatileFuse = 0.6f, CommandRange = 220f, CommandMult = 1.3f;
        public const int VolatileShots = 16, MirrorEvery = 4;
        public const float SquadShare = 0.7f, SquadFormTime = 2.5f;

        public static readonly SquadDef[] Squads =
        {
            new SquadDef { Name = "Rush", From = 1, Members = new[] { EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer } },
            new SquadDef { Name = "Firing line", From = 3, Members = new[] { EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Sniper } },
            new SquadDef { Name = "Brute pack", From = 5, Members = new[] { EnemyType.Brute, EnemyType.Brute, EnemyType.Lunger } },
            new SquadDef { Name = "Splinters", From = 7, Members = new[] { EnemyType.Splitter, EnemyType.Splitter, EnemyType.Grunt } },
            new SquadDef { Name = "Bombardment", From = 8, Members = new[] { EnemyType.Charger, EnemyType.Mortar, EnemyType.Mortar } },
            new SquadDef { Name = "Shield wall", From = 9, Members = new[] { EnemyType.Shielder, EnemyType.Sniper, EnemyType.Sniper } },
            new SquadDef { Name = "Field hospital", From = 11, Members = new[] { EnemyType.Brute, EnemyType.Brute, EnemyType.Brute, EnemyType.Medic } },
            new SquadDef { Name = "Ambush", From = 16, Members = new[] { EnemyType.Phantom, EnemyType.Phantom, EnemyType.Phantom }, Form = SquadForm.Around },
            new SquadDef { Name = "Beam battery", From = 18, Members = new[] { EnemyType.Shielder, EnemyType.BeamSentry, EnemyType.BeamSentry } },
            new SquadDef { Name = "Pincer", From = 21, Members = new[] { EnemyType.Hexer, EnemyType.Hexer, EnemyType.Grunt, EnemyType.Grunt }, Form = SquadForm.Pincer },
            new SquadDef { Name = "Scorched earth", From = 24, Members = new[] { EnemyType.Scorcher, EnemyType.Scorcher, EnemyType.Swarmer, EnemyType.Swarmer } },
            new SquadDef { Name = "Siege", From = 26, Members = new[] { EnemyType.Juggernaut, EnemyType.Medic, EnemyType.Medic } },
            new SquadDef { Name = "Reaver pack", From = 28, Members = new[] { EnemyType.Reaver, EnemyType.Reaver, EnemyType.Lunger, EnemyType.Lunger } },
            new SquadDef { Name = "Hive", From = 31, Members = new[] { EnemyType.Nest, EnemyType.Splitter, EnemyType.Splitter } },
            new SquadDef { Name = "Bastion", From = 36, Members = new[] { EnemyType.Warder, EnemyType.Juggernaut, EnemyType.Grunt, EnemyType.Grunt } },
        };

        /// Host: squads still to arrive this wave (they come in turn with the single enemies of spawnQueue).
        readonly List<SquadDef> squadQueue = new List<SquadDef>();
        int nextSquadId = 1, singlesSinceSquad;
        /// Host, rebuilt each tick: each squad's living members, and the living Commanders.
        readonly Dictionary<int, List<Enemy>> squadMembers = new Dictionary<int, List<Enemy>>();
        readonly List<Enemy> commanders = new List<Enemy>();
        /// Host: Volatile elites' bursts waiting on their fuse, and Mirrored elites' bounced shots (fired next tick).
        readonly List<V2> volatilePos = new List<V2>();
        readonly List<float> volatileT = new List<float>();
        readonly List<EnemyType> volatileType = new List<EnemyType>();
        readonly List<V2> mirrorFrom = new List<V2>();
        readonly List<int> mirrorAt = new List<int>();
        readonly List<EnemyType> mirrorType = new List<EnemyType>();
        float emberTickT;

        public static bool HasTrait(Enemy e, EliteTrait t) => (e.Traits & (byte)t) != 0;

        float SentryDamage => 20f + 0.3f * Wave;

        // --- Waves -----------------------------------------------------------------------------------------------

        /// A regular wave: about 70% squads (drawn from those unlocked by now, none more than twice), the rest
        /// single enemies as before (added to spawnQueue by BuildWave). Returns how many enemies the squads hold.
        int BuildSquads(int n, int count)
        {
            squadQueue.Clear();
            singlesSinceSquad = 0;
            var ok = new List<SquadDef>();
            foreach (var s in Squads)
                if (s.From <= n)
                    ok.Add(s);
            int spent = 0;
            float melee = MeleeShare(n);
            for (int guard = 0; guard < 40 && spent < count * SquadShare && ok.Count > 0; guard++)
            {
                // Early waves keep their share of rushers: a melee squad that often, a shooting one otherwise.
                bool wantMelee = rng.NextDouble() < melee;
                var from = ok.FindAll(q => MeleeSquad(q) == wantMelee);
                if (from.Count == 0)
                    from = ok;
                // Newer squads a little more often, so each one shows up soon after it unlocks.
                float total = 0f;
                foreach (var s in from)
                    total += SquadWeight(s, n);
                float r = (float)rng.NextDouble() * total;
                SquadDef pick = from[from.Count - 1];
                foreach (var s in from)
                {
                    r -= SquadWeight(s, n);
                    if (r <= 0f)
                    {
                        pick = s;
                        break;
                    }
                }
                if (spent + pick.Members.Length > count * 0.8f)
                    break;
                squadQueue.Add(pick);
                spent += pick.Members.Length;
                if (squadQueue.FindAll(q => q == pick).Count >= 2)
                    ok.Remove(pick);
            }
            return spent;
        }

        static bool MeleeSquad(SquadDef s) => Array.TrueForAll(s.Members, t => Defs.Get(t).Melee);

        static float SquadWeight(SquadDef s, int n) => 1f + Math.Max(0, 6 - (n - s.From)) * 0.15f;

        /// Host, while a wave spawns: a squad comes after every three single enemies (or once the singles run out).
        bool SpawnNextSquad()
        {
            if (squadQueue.Count == 0 || (singlesSinceSquad < 3 && spawnQueue.Count > 0))
                return false;
            var s = squadQueue[0];
            squadQueue.RemoveAt(0);
            singlesSinceSquad = 0;
            SpawnSquad(s);
            return true;
        }

        void SpawnSquad(SquadDef s)
        {
            int id = nextSquadId++, n = s.Members.Length;
            V2 lo = ArenaMin + new V2(50f, 50f), hi = ArenaMax - new V2(50f, 50f);
            int side = rng.Next(4);
            V2 basePos = side == 0 ? new V2(Rand(lo.X + 150f, hi.X - 150f), lo.Y) : side == 1 ? new V2(Rand(lo.X + 150f, hi.X - 150f), hi.Y)
                : side == 2 ? new V2(lo.X, Rand(lo.Y + 120f, hi.Y - 120f)) : new V2(hi.X, Rand(lo.Y + 120f, hi.Y - 120f));
            V2 fwd = (new V2(Width / 2f, Height / 2f) - basePos).Normalized(), across = fwd.Orthogonal();
            Player target = null;
            var alive = AlivePlayersList();
            if (alive.Count > 0)
                target = alive[rng.Next(alive.Count)];
            for (int i = 0; i < n; i++)
            {
                var type = s.Members[i];
                V2 pos;
                if (s.Form == SquadForm.Around && target != null)
                    pos = ClampToArena(target.Pos + V2.FromAngle(MathX.Tau * i / n + Rand(0f, 1f)) * 280f, 40f);
                else if (s.Form == SquadForm.Pincer && i < 2)
                    pos = ClampToArena(new V2(i == 0 ? lo.X : hi.X, Rand(lo.Y + 150f, hi.Y - 150f)), 40f);
                else
                {
                    bool behind = i > 0 && (type == EnemyType.Sniper || type == EnemyType.Medic || type == EnemyType.BeamSentry || type == EnemyType.Mortar || type == EnemyType.Warder);
                    pos = ClampToArena(basePos + across * ((i - (n - 1) / 2f) * 60f) - fwd * (behind ? 70f : 0f), 40f);
                }
                if (type == EnemyType.Nest)
                    pos = ClampToArena(basePos + fwd * 140f, 120f);
                var e = SpawnEnemyAt(type, pos, 0.9f, 0, RollElite(Defs.Get(type)));
                e.SquadId = id;
                e.SquadSlot = i;
            }
            DoLog("squad: " + s.Name);
        }

        bool RollElite(EnemyDef d) => !d.IsBoss && Wave >= 12 && rng.NextDouble() < Math.Min(0.35, 0.04 * (Wave - 11));

        /// An elite rolls one trait (two from wave 35).
        void RollTraits(Enemy e)
        {
            int want = Wave >= 35 ? 2 : 1;
            while (CountTraits(e.Traits) < want)
                e.Traits |= (byte)(1 << rng.Next(6));
            if (HasTrait(e, EliteTrait.Shielded))
                e.BubbleHp = e.BubbleMax = e.MaxHp * ShieldedShare;
        }

        static int CountTraits(byte t)
        {
            int n = 0;
            for (; t != 0; t &= (byte)(t - 1))
                n++;
            return n;
        }

        // --- Each tick -------------------------------------------------------------------------------------------

        /// Host, at the start of the enemy tick: who's in which squad, and where the Commanders are.
        void PrepareVariety()
        {
            foreach (var l in squadMembers.Values)
                l.Clear();
            commanders.Clear();
            foreach (var e in Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                if (e.SquadId != 0)
                {
                    List<Enemy> l;
                    if (!squadMembers.TryGetValue(e.SquadId, out l))
                        squadMembers[e.SquadId] = l = new List<Enemy>();
                    l.Add(e);
                }
                if (HasTrait(e, EliteTrait.Commander) && e.SpawnT <= 0f)
                    commanders.Add(e);
            }
        }

        /// How much faster it fires: Hasted, and near a living Commander.
        float TraitFireMult(Enemy e)
        {
            float m = HasTrait(e, EliteTrait.Hasted) ? HasteMult : 1f;
            foreach (var c in commanders)
                if (c != e && c.Pos.DistanceTo(e.Pos) < CommandRange)
                    return m * CommandMult;
            return m;
        }

        /// Host, after the enemies have moved: embers burn, Volatile bursts go off, Mirrored shots come back,
        /// Shielded bubbles grow back.
        void TickVariety(float dt)
        {
            emberTickT -= dt;
            if (emberTickT <= 0f)
            {
                emberTickT = 0.25f;
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    foreach (var pt in Patches.Values)
                        if (pt.Kind == PatchKind.Embers && pt.Active && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                        {
                            Damage(p, EmberDps * 0.25f, "Scorcher (embers)");
                            break;
                        }
                }
            }
            for (int i = volatileT.Count - 1; i >= 0; i--)
            {
                volatileT[i] -= dt;
                if (volatileT[i] > 0f)
                    continue;
                V2 at = volatilePos[i];
                float off = Rand(0f, MathX.Tau);
                for (int k = 0; k < VolatileShots; k++)
                {
                    V2 dir = V2.FromAngle(off + MathX.Tau * k / VolatileShots);
                    SpawnBullet(Defs.SmallShot, at + dir * 12f, dir * 150f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0).Source = volatileType[i];
                }
                BroadcastFx(FxKind.Boom, at, new Rgba(1f, 0.55f, 0.23f), 60f, 0.3f, Sfx.Explode);
                volatileT.RemoveAt(i);
                volatilePos.RemoveAt(i);
                volatileType.RemoveAt(i);
            }
            for (int i = 0; i < mirrorFrom.Count; i++)
            {
                Player p;
                if (!Players.TryGetValue(mirrorAt[i], out p) || p.Downed)
                    continue;
                V2 dir = (p.Pos - mirrorFrom[i]).Normalized();
                SpawnBullet(Defs.FastShot, mirrorFrom[i] + dir * 20f, dir * 520f, 0f, 3f, Defs.BulletDamage(Defs.FastShot), 0).Source = mirrorType[i];
            }
            mirrorFrom.Clear();
            mirrorAt.Clear();
            mirrorType.Clear();
            foreach (var e in Enemies.Values)
                if (HasTrait(e, EliteTrait.Shielded) && e.WardId == 0 && e.BubbleHp < e.BubbleMax)
                {
                    e.BubbleT += dt;
                    if (e.BubbleT >= BubbleRegrow)
                    {
                        e.BubbleHp = e.BubbleMax = e.MaxHp * ShieldedShare;
                        e.BubbleT = 0f;
                        BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.56f, 0.82f, 1f), e.Radius + 14f, 0.3f, Sfx.None);
                    }
                }
        }

        // --- Movement --------------------------------------------------------------------------------------------

        V2 VarietyMove(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.BeamSentry:
                    return e.Phase == 0 ? KeepDistance(dir, dist, SentryRange) + dir.Orthogonal() * e.Strafe * 0.3f : V2.Zero;
                case EnemyType.Scorcher:  // round you, drawing a ring of embers
                    return dir.Orthogonal() * e.Strafe + KeepDistance(dir, dist, ScorchRange) * 0.8f;
                case EnemyType.Reaver:
                    return e.Phase == 0 ? dir : V2.Zero;
                case EnemyType.Warder:
                {
                    // Behind its allies, between them and you.
                    V2 sum = V2.Zero;
                    int n = 0;
                    foreach (var o in Enemies.Values)
                        if (o != e && !o.Def.IsBoss && o.Type != EnemyType.Warder && o.Hp > 0f && o.Pos.DistanceTo(e.Pos) < 600f)
                        {
                            sum += o.Pos;
                            n++;
                        }
                    if (n == 0)
                        return KeepDistance(dir, dist, SentryRange);
                    V2 goal = sum / n - dir * 160f;
                    return (goal - e.Pos) / 80f;
                }
            }
            return V2.Zero;
        }

        /// A squad's members keep together: in formation as they arrive, then the escort rules.
        V2 SquadMove(Enemy e, V2 tpos, V2 dir, V2 v)
        {
            List<Enemy> mates;
            if (e.SquadId == 0 || !squadMembers.TryGetValue(e.SquadId, out mates) || mates.Count < 2)
                return v;
            if (e.T < SquadFormTime + 0.9f && !e.Def.Melee && e.Type != EnemyType.Phantom && e.Type != EnemyType.Nest)
                return dir * 0.6f;  // marching in together
            switch (e.Type)
            {
                case EnemyType.Sniper:
                {
                    var sh = mates.Find(o => o.Type == EnemyType.Shielder);
                    if (sh != null)  // behind its Shielder, a little to one side
                        return GoalVel(e, sh.Pos - (tpos - sh.Pos).Normalized() * 110f + (tpos - sh.Pos).Normalized().Orthogonal() * (e.SquadSlot % 2 == 0 ? 60f : -60f));
                    V2 c;
                    if (Centroid(mates, e, EnemyType.Grunt, out c))
                        return GoalVel(e, c - (tpos - c).Normalized() * 120f);  // behind the middle of the firing line
                    break;
                }
                case EnemyType.BeamSentry:
                    break;
                case EnemyType.Shielder:
                {
                    // It leads a Shield wall (its Snipers keep behind it); with planted Beam Sentries it stands
                    // between you and them.
                    V2 c;
                    if (Centroid(mates, e, EnemyType.BeamSentry, out c))
                        return GoalVel(e, c + (tpos - c).Normalized() * 120f);
                    break;
                }
                case EnemyType.Medic:
                {
                    V2 c;
                    if (Centroid(mates, e, null, out c))
                        return GoalVel(e, c - (tpos - c).Normalized() * 90f);  // just behind the squad
                    break;
                }
                case EnemyType.Splitter:
                {
                    var nest = mates.Find(o => o.Type == EnemyType.Nest);
                    if (nest != null)  // guarding the Nest
                        return GoalVel(e, nest.Pos + V2.FromAngle(e.T * 0.6f + e.SquadSlot * MathX.Pi) * 110f);
                    break;
                }
            }
            return v;
        }

        static V2 GoalVel(Enemy e, V2 goal) => (goal - e.Pos) / 60f;

        static bool Centroid(List<Enemy> mates, Enemy self, EnemyType? type, out V2 c)
        {
            c = V2.Zero;
            int n = 0;
            foreach (var o in mates)
                if (o != self && (type == null || o.Type == type.Value))
                {
                    c += o.Pos;
                    n++;
                }
            if (n == 0)
                return false;
            c /= n;
            return true;
        }

        /// A Lunger in a squad with Brutes waits for one of them to reach its target before it leaps.
        bool LungerWaits(Enemy e, V2 tpos)
        {
            List<Enemy> mates;
            if (e.SquadId == 0 || !squadMembers.TryGetValue(e.SquadId, out mates))
                return false;
            bool brutes = false;
            foreach (var o in mates)
                if (o.Type == EnemyType.Brute)
                {
                    brutes = true;
                    if (o.Pos.DistanceTo(tpos) < 90f)
                        return false;
                }
            return brutes;
        }

        // --- Behaviour -------------------------------------------------------------------------------------------

        void VarietySpecial(Enemy e, V2 tpos, float dt)
        {
            float aim = (tpos - e.Pos).Angle;
            switch (e.Type)
            {
                case EnemyType.BeamSentry:
                {
                    e.PhaseT -= dt;
                    float dist = tpos.DistanceTo(e.Pos);
                    if (e.Phase == 0 && e.T > 1.5f && dist > SentryRange - 70f && dist < SentryRange + 70f)
                    {
                        e.Phase = 1;
                        e.PhaseT = 0.6f;
                    }
                    else if (e.Phase == 1 && e.PhaseT <= 0f)
                    {
                        e.Phase = 2;
                        e.PhaseT = SentryAim;
                        e.Facing = aim;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    else if (e.Phase == 2)
                    {
                        float diff = MathX.Fmod(aim - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
                        e.Facing += Math.Max(-SentryTurn * dt, Math.Min(SentryTurn * dt, diff));
                        if (e.PhaseT <= 0f)
                        {
                            // It sweeps toward the side you were heading: it starts a little behind you on the other.
                            e.Strafe = diff >= 0f ? 1 : -1;
                            e.Facing -= e.Strafe * 0.5f;
                            e.Phase = 3;
                            e.PhaseT = SentrySweep;
                        }
                    }
                    else if (e.Phase == 3)
                    {
                        e.Facing += e.Strafe * SentrySweepArc / SentrySweep * dt;
                        V2 tip = e.Pos + V2.FromAngle(e.Facing) * SentryBeamLen;
                        foreach (var p in Players.Values)
                            if (!p.Downed && p.InvulnT <= 0f && SegmentDistance(p.Pos, e.Pos, tip) < SentryBeamWidth / 2f + p.Def.HitR)
                                Damage(p, SentryDamage, e.Def.Name + " (beam)");
                        if (e.PhaseT <= 0f)
                        {
                            e.Phase = 4;
                            e.PhaseT = SentryRest / TraitFireMult(e);
                        }
                    }
                    else if (e.Phase == 4 && e.PhaseT <= 0f)
                    {
                        // Still in range? Aim again; otherwise walk on.
                        e.Phase = dist < SentryRange + 200f ? (byte)2 : (byte)0;
                        e.PhaseT = SentryAim;
                    }
                    break;
                }
                case EnemyType.Scorcher:
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                    {
                        e.PhaseT = EmberEvery;
                        SpawnPatchAt(PatchKind.Embers, e.Pos, 0.15f, EmberLife, EmberRadius, EmberRadius);
                    }
                    break;
                case EnemyType.Reaver:
                {
                    e.PhaseT -= dt;
                    float dist = tpos.DistanceTo(e.Pos);
                    if (e.Phase == 0 && dist < ReaverSwipeR + e.Radius)
                    {
                        e.Phase = 1;
                        e.PhaseT = ReaverWindup;
                        e.Facing = aim;
                    }
                    else if (e.Phase == 1 && e.PhaseT <= 0f)
                    {
                        // The rake: everyone in the cone.
                        foreach (var p in Players.Values)
                        {
                            if (p.Downed || p.InvulnT > 0f)
                                continue;
                            V2 rel = p.Pos - e.Pos;
                            float off = Math.Abs(MathX.Fmod(rel.Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                            if (rel.Length > ReaverSwipeR + e.Radius + p.Def.HitR || off > ReaverCone)
                                continue;
                            if (p.ParryActive && ParryContact(p, e, ReaverDamage))
                                continue;  // a raised guard takes it
                            Damage(p, ReaverDamage, e.Def.Name + " (swipe)");
                        }
                        BroadcastFx(FxKind.Claw, e.Pos, e.Pos + V2.FromAngle(e.Facing) * (ReaverSwipeR + e.Radius), e.Def.Color, ReaverCone, 0.25f, Sfx.SwingHeavy);
                        e.Phase = 3;
                        e.PhaseT = ReaverOpen;
                    }
                    else if (e.Phase == 3 && e.PhaseT <= 0f)
                        e.Phase = 0;
                    break;
                }
                case EnemyType.Warder:
                    e.WardT -= dt;
                    if (e.WardT <= 0f)
                    {
                        e.WardT = WarderEvery;
                        var near = new List<Enemy>();
                        foreach (var o in Enemies.Values)
                            if (o != e && !o.Def.IsBoss && o.Type != EnemyType.Warder && o.Hp > 0f && o.SpawnT <= 0f && o.Pos.DistanceTo(e.Pos) < WarderRange)
                                near.Add(o);
                        near.Sort((a, b) => a.Pos.DistanceSq(e.Pos).CompareTo(b.Pos.DistanceSq(e.Pos)));
                        for (int i = 0; i < near.Count && i < WarderMax; i++)
                        {
                            var o = near[i];
                            o.WardId = e.Id;
                            o.BubbleMax = Math.Max(o.BubbleMax, o.MaxHp * WarderShare);
                            o.BubbleHp = Math.Max(o.BubbleHp, o.MaxHp * WarderShare);
                        }
                        if (near.Count > 0)
                            BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, WarderRange, 0.5f, Sfx.Ability);
                    }
                    break;
            }
        }

        bool VarietyFire(Enemy e, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.BeamSentry:
                    e.FireT = 99f;  // its beam is its attack (VarietySpecial)
                    return true;
                case EnemyType.Scorcher:
                    e.FireT = 2.5f;
                    EnemyShot(e, aim - 0.1f, 170f, Defs.SmallShot);
                    EnemyShot(e, aim + 0.1f, 170f, Defs.SmallShot);
                    return true;
                case EnemyType.Warder:
                    e.FireT = 3f;
                    EnemyShot(e, aim - 0.2f, 120f, Defs.BigShot);
                    EnemyShot(e, aim + 0.2f, 120f, Defs.BigShot);
                    return true;
            }
            return false;
        }

        /// The Reaver takes half again as much while it's open after a swipe.
        public static bool ReaverIsOpen(Enemy e) => e.Type == EnemyType.Reaver && e.Phase == 3;

        // --- Hits and deaths -------------------------------------------------------------------------------------

        /// A bubble (Shielded, or a Warder's) soaks damage first. Returns what gets through.
        float SoakBubble(Enemy e, float dmg)
        {
            if (e.BubbleHp <= 0f)
                return dmg;
            float soak = Math.Min(e.BubbleHp, dmg);
            e.BubbleHp -= soak;
            e.BubbleT = 0f;
            if (e.BubbleHp <= 0f)
            {
                e.BubbleHp = 0f;
                e.WardId = 0;
                BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.71f, 0.61f, 1f), e.Radius + 16f, 0.3f, Sfx.Clang);
            }
            return dmg - soak;
        }

        /// Collide: one in four player shots that hit a Mirrored elite bounce back at whoever fired them.
        bool MirrorBounces(Enemy e, Bullet b)
        {
            if (!HasTrait(e, EliteTrait.Mirrored) || b.Owner == 0)
                return false;
            if (++e.MirrorCount % MirrorEvery != 0)
                return false;
            mirrorFrom.Add(e.Pos);
            mirrorAt.Add(b.Owner);
            mirrorType.Add(e.Type);
            BroadcastFx(FxKind.Spark, b.Pos, Rgba.White, 14f, 0.2f, Sfx.Clang);
            return true;
        }

        void VarietyKilled(Enemy e)
        {
            if (HasTrait(e, EliteTrait.Volatile))
            {
                volatilePos.Add(e.Pos);
                volatileT.Add(VolatileFuse);
                volatileType.Add(e.Type);
                BroadcastFx(FxKind.Target, e.Pos, new Rgba(1f, 0.55f, 0.23f), 60f, VolatileFuse, Sfx.BossCharge);
            }
            if (HasTrait(e, EliteTrait.Splitting))
            {
                int n = e.Type == EnemyType.Juggernaut || e.Type == EnemyType.Nest || e.Type == EnemyType.Brute ? 4 : 2;
                for (int i = 0; i < n; i++)
                    SpawnEnemyAt(EnemyType.Shard, e.Pos + V2.FromAngle(MathX.Tau * i / n) * 20f, 0.15f, 0);
            }
            if (e.Type == EnemyType.Warder)
                foreach (var o in Enemies.Values)
                    if (o.WardId == e.Id)
                    {
                        o.WardId = 0;
                        o.BubbleHp = HasTrait(o, EliteTrait.Shielded) ? Math.Min(o.BubbleHp, o.MaxHp * ShieldedShare) : 0f;
                        o.BubbleMax = HasTrait(o, EliteTrait.Shielded) ? o.MaxHp * ShieldedShare : 0f;
                        BroadcastFx(FxKind.Ring, o.Pos, e.Def.Color, o.Radius + 16f, 0.3f, Sfx.None);
                    }
        }
    }
}
