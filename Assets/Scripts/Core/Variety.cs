using System;
using System.Collections.Generic;

namespace Arena
{
    /// An elite's traits (flags; one from wave 12, two from wave 35). Replicated so every screen draws them.
    [Flags]
    public enum EliteTrait : byte { None = 0, Hasted = 1, Shielded = 2, Volatile = 4, Splitting = 8, Mirrored = 16, Commander = 32, CoreCarrier = 64 }  // CoreCarrier: not an elite trait (never rolled) but a glowing enemy that drops Cores (CoreDrops.cs)

    /// A squad: a small team that arrives together from one edge and looks after itself.
    public class SquadDef
    {
        public string Name;
        public int From;              // the first wave it can show up in
        public EnemyType[] Members;   // front first
        public SquadForm Form;
    }

    /// How a squad arrives: in a line across its edge, split to opposite edges, or blinking in around a player.
    public enum SquadForm : byte { Line, Pincer, Around, RiotLine, Accretion, Honour, StormFront, LastLight }  // RiotLine: a wall of Riot Shields (or their kin) marching at you, the rest close behind (WardenFaction.cs); Accretion: a ring round you that turns and closes (Infall.cs); Honour: the Ring of Honour, fencers round you that lunge in by pairs (CourtFaction.cs); StormFront: Stormcallers in a line behind Grounders, sweeping a wall of strikes across you (Squall.cs); LastLight: a square round you that turns and closes (Penumbral.cs)

    /// More variety between the bosses (built from Docs/Mockups/enemy-variety.html):
    ///   Elite traits: Hasted, Shielded, Volatile, Splitting, Mirrored, Commander (EliteTrait).
    ///   Squads: about 70% of a regular wave is small teams (Squads) that arrive together and keep each other going:
    ///     Snipers keep behind their Shielder, a Lunger waits for a Brute to reach you.
    /// Drawn in VarietyLook.cs.
    public partial class Game
    {
        // Elite traits.
        public const float HasteMult = 1.4f, ShieldedShare = 0.4f, BubbleRegrow = 4f, VolatileFuse = 0.6f, CommandRange = 220f, CommandMult = 1.3f;
        public const int VolatileShots = 12, MirrorEvery = 4;
        public const float SquadShare = 0.7f, SquadFormTime = 2.5f;

        public static readonly SquadDef[] Squads =
        {
            new SquadDef { Name = "Rush", From = 1, Members = new[] { EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer } },
            new SquadDef { Name = "Firing line", From = 3, Members = new[] { EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Sniper } },
            new SquadDef { Name = "Brute pack", From = 5, Members = new[] { EnemyType.Brute, EnemyType.Brute, EnemyType.Lunger } },
            new SquadDef { Name = "Shield wall", From = 9, Members = new[] { EnemyType.Shielder, EnemyType.Sniper, EnemyType.Sniper } },
        };

        /// Host: squads still to arrive this wave (they come in turn with the single enemies of spawnQueue).
        readonly List<SquadDef> squadQueue = new List<SquadDef>();
        int nextSquadId = 1, singlesSinceSquad;
        int stragglers;  // the last singles in spawnQueue are a lesson's new enemy: the squads come out first (Approaches.cs MixApproach)
        int spawnHome = -1;  // host: the Rooms arena's room the enemies being spawned now belong to (Rooms.cs; -1: none)
        /// Host, rebuilt each tick: each squad's living members, and the living Commanders.
        readonly Dictionary<int, List<Enemy>> squadMembers = new Dictionary<int, List<Enemy>>();
        readonly Dictionary<int, SquadForm> squadForms = new Dictionary<int, SquadForm>();  // host: each squad's form (by SquadId)
        readonly List<Enemy> commanders = new List<Enemy>();
        /// Host: Volatile elites' bursts waiting on their fuse, and Mirrored elites' bounced shots (fired next tick).
        readonly List<V2> volatilePos = new List<V2>();
        readonly List<float> volatileT = new List<float>();
        readonly List<EnemyType> volatileType = new List<EnemyType>();
        readonly List<int> volatileN = new List<int>();  // shots in each burst (Volatile 12; a Death Throes burst stored as minus its 6)
        readonly List<V2> mirrorFrom = new List<V2>();
        readonly List<int> mirrorAt = new List<int>();
        readonly List<EnemyType> mirrorType = new List<EnemyType>();

        public static bool HasTrait(Enemy e, EliteTrait t) => (e.Traits & (byte)t) != 0;

        public const int MaxPulsars = 3;   // at most this many Pulsars in a wave (CapSentries, Infall.cs)

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
            bool recipe = n >= RecipeFrom;  // the late recipe: half the wave, mostly-not-shooting squads as often as those jobs' share (WaveRecipe.cs)
            float melee = recipe ? 1f - JobShares[(int)EnemyJob.Pressure] : MeleeShare(n), share = recipe ? RecipeSquadShare : SquadShare;
            int kindCapHere = recipe ? KindCap(n) : 99;  // the late recipe bounds the wave's distinct types (WaveRecipe.cs)
            if (recipe)  // (plan.Kinds already holds the boss factions' types: BuildWave started it)
                foreach (var h in Headliners)
                    plan.Kinds.Add(h);
            Func<SquadDef, bool> fits = q =>
            {
                int extra = 0;
                var seen = new HashSet<EnemyType>();
                foreach (var m in q.Members)
                    if (!plan.Kinds.Contains(m) && seen.Add(m))
                        extra++;
                return plan.Kinds.Count + extra <= kindCapHere;
            };
            for (int guard = 0; guard < 40 && spent < count * share && ok.Count > 0; guard++)
            {
                if (recipe && !ok.Exists(q => fits(q)))
                    break;
                // Early waves keep their share of rushers: a melee squad that often, a shooting one otherwise.
                bool wantMelee = rng.NextDouble() < melee;
                var from = ok.FindAll(q => (recipe ? !PressureSquad(q) : MeleeSquad(q)) == wantMelee);
                if (recipe)
                    from = from.FindAll(q => fits(q));
                if (from.Count == 0)
                    from = recipe ? ok.FindAll(q => fits(q)) : ok;
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
                if (spent + pick.Members.Length > count * (recipe ? RecipeSquadShare + 0.1f : 0.8f))
                    break;
                squadQueue.Add(pick);
                if (recipe)
                    foreach (var m in pick.Members)
                        plan.Kinds.Add(m);
                spent += pick.Members.Length;
                if (squadQueue.FindAll(q => q == pick).Count >= 2)
                    ok.Remove(pick);
            }
            return spent;
        }

        static bool MeleeSquad(SquadDef s) => Array.TrueForAll(s.Members, t => Defs.Get(t).Melee);

        /// Mostly shooters (the late recipe's Pressure job, WaveRecipe.cs)?
        static bool PressureSquad(SquadDef s) => Array.FindAll(s.Members, t => JobOf(t) == EnemyJob.Pressure).Length * 2 > s.Members.Length;

        /// Newer squads a little more often; on a recipe wave old squads retire as old types do (WaveRecipe.cs).
        static float SquadWeight(SquadDef s, int n) => (1f + Math.Max(0, 6 - (n - s.From)) * 0.15f) * (n >= RecipeFrom ? RecipeWeight(s.From, n) : 1f);

        /// Host, while a wave spawns: a squad comes after every three single enemies (or once the singles run out).
        bool SpawnNextSquad()
        {
            if (squadQueue.Count == 0 || (singlesSinceSquad < 3 && spawnQueue.Count > stragglers))
                return false;
            var s = squadQueue[0];
            squadQueue.RemoveAt(0);
            singlesSinceSquad = 0;
            SpawnSquad(s);
            return true;
        }

        void SpawnSquad(SquadDef s) => SpawnSquadAt(s, null);

        /// at: where it comes in (a set spawn point); null: a random spot on an edge.
        void SpawnSquadAt(SquadDef s, V2? at)
        {
            int id = nextSquadId++, n = s.Members.Length;
            V2 lo = ArenaMin + new V2(50f, 50f), hi = ArenaMax - new V2(50f, 50f);
            int side = at.HasValue ? 0 : rng.Next(4);
            V2 basePos = at.HasValue ? ClampToArena(at.Value, 50f)
                : side == 0 ? new V2(Rand(lo.X + 150f, hi.X - 150f), lo.Y) : side == 1 ? new V2(Rand(lo.X + 150f, hi.X - 150f), hi.Y)
                : side == 2 ? new V2(lo.X, Rand(lo.Y + 120f, hi.Y - 120f)) : new V2(hi.X, Rand(lo.Y + 120f, hi.Y - 120f));
            V2 breachIn = V2.Zero;
            V2? breach = at.HasValue ? null : BreachSpot(60f, out breachIn);  // the Expanding arena: through one of the wave's breaches (Breaches.cs)
            if (breach.HasValue)
                basePos = breach.Value + breachIn * 80f;  // (a squad forms up a little further in: its back row stands behind it)
            else if (RoomsOn && !at.HasValue)
                basePos = RoomsRandomSpot(80f);  // anywhere on the map, away from you (Rooms.cs)
            else if (LayoutOn && !at.HasValue)
                basePos = LayoutSquadBase();  // in front of an enemy gate (LayoutRules.cs)
            V2 fwd = breach.HasValue ? breachIn : (ArenaMid - basePos).Normalized(), across = fwd.Orthogonal();  // (through a breach: facing into the map)
            Player target = null;
            var alive = AlivePlayersList();
            if (alive.Count > 0)
                target = alive[rng.Next(alive.Count)];
            for (int i = 0; i < n; i++)
            {
                var type = s.Members[i];
                V2 pos;
                if (s.Form == SquadForm.Around && target != null && !RoomsOn)  // (the Rooms arena: they form up in their room)
                    pos = ClampToArena(target.Pos + V2.FromAngle(MathX.Tau * i / n + Rand(0f, 1f)) * 280f, 40f);
                else if (s.Form == SquadForm.RiotLine)
                    pos = ClampToArena(basePos + across * ((RiotLineSlot(s, i) - (RiotLineCount(s, type) - 1) / 2f) * (IsWallKin(type) ? RiotLineGap : RiotGuardGap))
                        - fwd * (IsWallKin(type) ? 0f : RiotGuardBehind), 40f);  // the wall in front, the guards behind
                else if (s.Form == SquadForm.Accretion && target != null && !RoomsOn)
                    pos = AccretionSpawn(id, i, target);  // in a ring round a player (Infall.cs)
                else if (s.Form == SquadForm.Honour && target != null && !RoomsOn)
                    pos = HonourSpawn(id, i, target);  // in a ring round a player (CourtFaction.cs)
                else if (s.Form == SquadForm.LastLight && target != null && !RoomsOn)
                    pos = LastLightSpawn(id, i, target);  // on a square round a player (Penumbral.cs)
                else if (s.Form == SquadForm.Pincer && i < 2 && !breach.HasValue)  // (through a breach: they come in together)
                    pos = ClampToArena(new V2(i == 0 ? lo.X : hi.X, LayoutOn ? LayoutSideY(i == 0 ? 2 : 3, 150f) : Rand(lo.Y + 150f, hi.Y - 150f)), 40f);
                else
                {
                    bool behind = i > 0 && (type == EnemyType.Sniper || type == EnemyType.Mortar);
                    pos = ClampToArena(basePos + across * ((i - (n - 1) / 2f) * 60f) - fwd * (behind ? 70f : 0f), 40f);
                    if (s.Form == SquadForm.StormFront && type == EnemyType.Grounder)
                        pos = ClampToArena(pos + fwd * StormFrontGuard, 40f);  // the Grounders stand in front of the callers (Squall.cs)
                }
                if (breach.HasValue)  // through a breach: everyone stays inside the room it opens into (never out past a corner)
                {
                    var room = CurrentWorld.Areas[CurrentWorld.AreaAt(basePos)];
                    float pad = Defs.Get(type).Radius + 30f;
                    pos = new V2(Math.Max(room.Min.X + pad, Math.Min(room.Max.X - pad, pos.X)), Math.Max(room.Min.Y + pad, Math.Min(room.Max.Y - pad, pos.Y)));
                }
                var e = SpawnEnemyAt(type, pos, 0.9f, 0, RollElite(Defs.Get(type)));
                e.SquadId = id;
                e.SquadSlot = i;
            }
            spawnHome = -1;
            squadForms[id] = s.Form;
            if (s.Form == SquadForm.RiotLine)
                riotLines[id] = RiotLineCount(s, EnemyType.RiotShield);
            if (s.Form == SquadForm.StormFront)
                StartStormFront(id, fwd, across);  // Squall.cs
            DoLog("squad: " + s.Name);
        }

        /// From wave 12, regular enemies can show up as tougher elites: 4% more a wave, up to 35% (times the difficulty's Elites).
        bool RollElite(EnemyDef d) => !d.IsBoss && ((Wave >= 12 && rng.NextDouble() < EliteChance(Wave)) || HuntEliteSquad && rng.NextDouble() < HuntEliteChance);  // (an Elite node of a hunt map: HuntMapGame.cs)

        public double EliteChance(int wave) => wave < 12 ? 0.0 : Math.Min(0.9, Math.Min(0.35, 0.04 * (wave - 11)) * D(DiffKnob.EliteChance));

        /// An elite rolls one trait (two from wave 35).
        /// A trait a regular (non-elite) enemy can carry before elites begin: from MinWave to MaxWave, PerWave more likely each wave, up to Cap.
        public class MutationDef { public EliteTrait Trait; public int MinWave, MaxWave; public float PerWave, Cap; }

        public static readonly MutationDef[] Mutations =
        {
            new MutationDef { Trait = EliteTrait.Hasted, MinWave = 3, MaxWave = 11, PerWave = 0.03f, Cap = 0.2f },
        };

        /// Host, at spawn: a regular enemy may carry a trait early (Hasted from wave 3). Rolled from the id, not the rng; bosses and children never.
        void RollMutations(Enemy e, int parent)
        {
            if (e.Def.IsBoss || parent != 0)
                return;
            foreach (var m in Mutations)
            {
                if (Wave < m.MinWave || Wave > m.MaxWave)
                    continue;
                float chance = Math.Min(m.Cap, m.PerWave * (Wave - m.MinWave + 1)) * VariantChanceScale;
                if (IdRoll(e.Id, 11 + (uint)m.Trait) < chance)
                    e.Traits |= (byte)m.Trait;
            }
        }

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

        /// Host: drop what's still pending from the enemies of a finished wave or run (Volatile bursts on their fuse,
        /// bounced Mirrored shots, squad lists), so none of it goes off in the intermission or the next run.
        void ResetVariety(bool newRun)
        {
            volatilePos.Clear();
            volatileT.Clear();
            volatileType.Clear();
            volatileN.Clear();
            mirrorFrom.Clear();
            mirrorAt.Clear();
            mirrorType.Clear();
            squadMembers.Clear();
            squadForms.Clear();
            riotLines.Clear();
            riotLineBroke.Clear();
            accretionRings.Clear();  // Infall.cs
            honourRings.Clear();     // CourtFaction.cs
            stormFronts.Clear();     // Squall.cs
            lastLights.Clear();      // Penumbral.cs
            honourTurn.Clear();
            if (newRun)
            {
                nextSquadId = 1;
                singlesSinceSquad = 0;
            }
        }

        /// At most MaxPulsars of the Singularity's Pulsars a wave (Infall.cs): squads that would bring more come apart into
        /// single enemies, and spare ones become Void Acolytes. The same for the Storm Herald's Pylons and Ball Lightning
        /// (Squall.cs), the Eclipse's Heliographs (Penumbral.cs), and the Lich's Grave Callers and Shriekers (Zombies.cs):
        /// spare ones become Shamblers.
        void CapSentries()
        {
            CapBeams(EnemyType.Pulsar, MaxPulsars, () => EnemyType.VoidAcolyte);
            CapBeams(EnemyType.Pylon, MaxPylonPairs, () => EnemyType.Stormcaller);
            CapBeams(EnemyType.BallLightning, MaxBalls, () => EnemyType.DustDevil);  // (their arcs stack up)  // the Storm Herald's: each brings its partner (Squall.cs)
            CapBeams(EnemyType.GraveCaller, Zombies.MaxCallers, () => EnemyType.Shambler);  // the Lich's: two Callers raise eight
            CapBeams(EnemyType.Shrieker, Zombies.MaxShriekers, () => EnemyType.Shambler);
            CapBeams(EnemyType.Heliograph, MaxHeliographs, () => EnemyType.CoronaPriest);  // (Penumbral.cs)
        }

        void CapBeams(EnemyType beam, int max, Func<EnemyType> instead)
        {
            int count = 0;
            for (int i = 0; i < squadQueue.Count; i++)
            {
                int here = 0;
                foreach (var t in squadQueue[i].Members)
                    if (t == beam)
                        here++;
                if (count + here <= max)
                {
                    count += here;
                    continue;
                }
                foreach (var t in squadQueue[i].Members)
                    if (t != beam)
                        spawnQueue.Add(t);
                squadQueue.RemoveAt(i--);
            }
            for (int i = 0; i < spawnQueue.Count; i++)
                if (spawnQueue[i] == beam && ++count > max)
                    spawnQueue[i] = instead();
        }

        /// How much faster it fires: Hasted, and near a living Commander.
        float TraitFireMult(Enemy e)
        {
            float m = HasTrait(e, EliteTrait.Hasted) ? HasteMult : 1f;
            foreach (var c in commanders)
                if (c != e && c.Pos.CloserThan(e.Pos, CommandRange))
                    return m * CommandMult;
            return m;
        }

        /// Host, after the enemies have moved: Volatile bursts go off, Mirrored shots come back,
        /// Shielded bubbles grow back.
        void TickVariety(float dt)
        {
            for (int i = volatileT.Count - 1; i >= 0; i--)
            {
                volatileT[i] -= dt;
                if (volatileT[i] > 0f)
                    continue;
                V2 at = volatilePos[i];
                int n = Math.Abs(volatileN[i]);
                bool throes = volatileN[i] < 0;  // (a Death Throes burst is stored negative)
                SpawnRing(Defs.SmallShot, at, n, throes ? Modifiers.ThroesSpeed : 125f, 8f, Defs.BulletDamage(Defs.SmallShot), 0, 12f, volatileType[i]);
                BroadcastFx(FxKind.Boom, at, throes ? new Rgba(0.75f, 0.3f, 0.35f) : new Rgba(1f, 0.55f, 0.23f), throes ? 34f : 60f, 0.3f, throes ? Sfx.None : Sfx.Explode);
                volatileN.RemoveAt(i);
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
                if (HasTrait(e, EliteTrait.Shielded) && e.BubbleHp < e.BubbleMax)
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


        /// A squad's members keep together: in formation as they arrive, then the escort rules.
        V2 SquadMove(Enemy e, V2 tpos, V2 dir, V2 v)
        {
            List<Enemy> mates;
            if (e.SquadId == 0 || !squadMembers.TryGetValue(e.SquadId, out mates) || mates.Count < 2)
                return v;
            SquadForm form;
            if (squadForms.TryGetValue(e.SquadId, out form) && form == SquadForm.RiotLine)
                return RiotLineMove(e, mates, tpos, v);  // WardenFaction.cs
            if (form == SquadForm.Accretion && accretionRings.ContainsKey(e.SquadId))
                return AccretionMove(e, mates, v);  // Infall.cs
            if (form == SquadForm.Honour && honourRings.ContainsKey(e.SquadId))
                return HonourMove(e, mates, v);  // CourtFaction.cs
            if (form == SquadForm.LastLight && lastLights.ContainsKey(e.SquadId))
                return LastLightMove(e, mates, v);  // Penumbral.cs
            if (e.T < SquadFormTime + 0.9f && !e.Def.Melee && e.Type != EnemyType.Shade)
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
            }
            return v;
        }

        V2 GoalVel(Enemy e, V2 goal) => RouteVel(e, goal, (goal - e.Pos) / 60f);  // round walls and crates (LayoutRules.cs)

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
            if (HonourHolds(e))
                return true;  // in the Ring of Honour it lunges only on its turn (CourtFaction.cs)
            List<Enemy> mates;
            if (e.SquadId == 0 || !squadMembers.TryGetValue(e.SquadId, out mates))
                return false;
            bool brutes = false;
            foreach (var o in mates)
                if (IsBrute(o.Type))
                {
                    brutes = true;
                    if (o.Pos.CloserThan(tpos, 90f))
                        return false;
                }
            return brutes;
        }

        // --- Behaviour -------------------------------------------------------------------------------------------



        // --- Player motion ------------------------------------------------------------------------------------------

        /// Host: how each player is moving, smoothed from their positions (clients move themselves). A jump (wrapping
        /// round the edge, a blink) starts it over.
        void TrackMotion(Player p, float dt)
        {
            if (dt <= 0f)
                return;
            V2 step = p.Pos - p.MotionFrom;
            p.MotionFrom = p.Pos;
            if (step.Length > 60f)
            {
                p.Motion = V2.Zero;
                return;
            }
            float k = Math.Min(1f, dt * 8f);
            p.Motion = p.Motion * (1f - k) + step / dt * k;
        }

        // --- Hits and deaths -------------------------------------------------------------------------------------

        public const int BubbleCells = 12;

        /// The cell a direction points at. Cell i sits at angle -90 degrees + i * 30 degrees.
        public static int CellAt(float ang)
        {
            int c = (int)Math.Round((ang + MathX.Tau * 0.25f) / (MathX.Tau / BubbleCells));
            return ((c % BubbleCells) + BubbleCells) % BubbleCells;
        }

        /// Keeps the twelve cells adding up to BubbleHp. Anything that sets BubbleHp directly (a regrow, a
        /// dev tool) just refills them evenly; only SoakBubble wears single cells down.
        void SyncCells(Enemy e)
        {
            if (e.CellHp == null)
                e.CellHp = new float[BubbleCells];
            float sum = 0f;
            for (int i = 0; i < BubbleCells; i++)
                sum += e.CellHp[i];
            if (Math.Abs(sum - e.BubbleHp) > 0.01f)
                for (int i = 0; i < BubbleCells; i++)
                    e.CellHp[i] = e.BubbleHp / BubbleCells;
        }

        /// The mask that goes in the snapshot: bits 0-11 cells still up, bits 12-23 cells worn below full.
        public static int CellMask(Enemy e)
        {
            if (e.BubbleHp <= 0f || e.CellHp == null)
                return 0;
            float full = e.BubbleMax / BubbleCells - 0.01f;
            int m = 0;
            for (int i = 0; i < BubbleCells; i++)
                if (e.CellHp[i] > 0.01f)
                {
                    m |= 1 << i;
                    if (e.CellHp[i] < full)
                        m |= 1 << (i + BubbleCells);
                }
            return m;
        }

        /// A Shielded elite's bubble soaks damage first. Returns what gets through. The damage lands on the
        /// cell facing whoever hit it (attacker > 0), spilling to its neighbours once that cell is gone; burning and
        /// bleeding (attacker -1) keep working on the weakest cell instead.
        float SoakBubble(Enemy e, float dmg, int attacker = 0)
        {
            if (e.BubbleHp <= 0f)
                return dmg;
            SyncCells(e);
            float soak = Math.Min(e.BubbleHp, dmg), left = soak;
            int start = -1;
            Player p;
            if (attacker > 0 && Players.TryGetValue(attacker, out p) && !(p.Pos - e.Pos).IsZero)
                start = CellAt((p.Pos - e.Pos).Angle);
            else if (attacker == 0)
                start = rng.Next(BubbleCells);
            else
            {
                float low = float.MaxValue;
                for (int i = 0; i < BubbleCells; i++)
                    if (e.CellHp[i] > 0.01f && e.CellHp[i] < low)
                    {
                        low = e.CellHp[i];
                        start = i;
                    }
            }
            for (int step = 0; step < BubbleCells && left > 0f; step++)  // 0, +1, -1, +2, -2 ... round the ring
            {
                int off = (step + 1) / 2 * ((step & 1) == 1 ? 1 : -1);
                int c = ((start + off) % BubbleCells + BubbleCells) % BubbleCells;
                float take = Math.Min(e.CellHp[c], left);
                e.CellHp[c] -= take;
                left -= take;
            }
            e.BubbleHp -= soak;
            e.BubbleT = 0f;
            if (e.BubbleHp <= 0.001f)
            {
                e.BubbleHp = 0f;
                for (int i = 0; i < BubbleCells; i++)
                    e.CellHp[i] = 0f;
                BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.71f, 0.61f, 1f), e.Radius + 16f, 0.3f, Sfx.Clang);
            }
            else
            {
                float sum = 0f;
                for (int i = 0; i < BubbleCells; i++)
                    sum += e.CellHp[i];
                e.BubbleHp = sum;  // no drift from rounding
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
                volatileN.Add(VolatileShots);
                BroadcastFx(FxKind.Target, e.Pos, new Rgba(1f, 0.55f, 0.23f), 60f, VolatileFuse, Sfx.BossCharge);
            }
            else if (Has(RunMod.DeathThroes) && !e.Def.IsBoss && e.Def.Xp > 0)
            {
                // Death Throes: every regular enemy bursts into a small ring a moment after it dies (Modifiers.cs).
                volatilePos.Add(e.Pos);
                volatileT.Add(Modifiers.ThroesFuse);
                volatileType.Add(e.Type);
                volatileN.Add(-Modifiers.ThroesShots);  // negative: a Death Throes burst
                BroadcastFx(FxKind.Target, e.Pos, new Rgba(0.75f, 0.3f, 0.35f), 34f, Modifiers.ThroesFuse, Sfx.None);
            }
            if (HasTrait(e, EliteTrait.Splitting))
            {
                int n = IsBrute(e.Type) ? 4 : 2;
                for (int i = 0; i < n; i++)
                    SpawnEnemyAt(EnemyType.Shard, e.Pos + V2.FromAngle(MathX.Tau * i / n) * 20f, 0.15f, 0);
            }
        }
    }
}
