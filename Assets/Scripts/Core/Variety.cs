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
    public enum SquadForm : byte { Line, Pincer, Around, RiotLine, Accretion }  // RiotLine: a wall of Riot Shields (or their kin) marching at you, the rest close behind (WardenFaction.cs); Accretion: a ring round you that turns and closes (Infall.cs)

    /// More variety between the bosses (built from Docs/Mockups/enemy-variety.html):
    ///   Four new enemies: the Beam Sentry (wave 18: plants itself, aims a line, sweeps a beam), the Scorcher (24:
    ///     circles you leaving embers), the Reaver (28: armoured melee that rakes a cone, then is open for a moment)
    ///     and the Warder (36: puts bubbles on up to three allies, which pop when it dies).
    ///   Three new shooting patterns: the Pauser (wave 14: a fan of shots that stop dead, hang, then each rushes at
    ///     the nearest player), the Leader (19: aims where you'll be, from how you're moving, after a short aim line)
    ///     and the Stitcher (22: slow orbs, each tied to the nearest other orb by a harmful thread; a pair stands side
    ///     by side and fires together, so the thread between their orbs sweeps at you like a moving fence).
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
        public const float WarderEvery = 6f, WarderRange = 320f, WarderShare = 0.5f, WarderCharge = 1.4f;  // WarderCharge: the last stretch before a ward, when Phase is 1 (the look winds up)
        public const int WarderMax = 3;
        // The Pauser: a fan of shots that fly out slowing to a stop (PauseFly), hang (PauseHold), then rush at a player.
        public const int PauseShots = 8;
        public const float PauseFan = 1.6f, PauseSpeed = 340f, PauseFly = 0.6f, PauseHold = 0.7f, PauseRushSpeed = 330f, PauseEvery = 3.4f, PauserRange = 300f;
        // The Leader: Phase 1 aims (a thin line along Facing, which tracks where you'll be), then fires a pair.
        public const float LeaderAim = 0.45f, LeaderSpeed = 260f, LeaderEvery = 1.9f, LeaderMaxLead = 1.6f, LeaderRange = 360f, LeaderSpread = 0.06f;
        // The Stitcher: slow orbs; each is tied to its nearest other orb within StitchLink by a thread this wide.
        // StitchEvery x StitchSpeed is more than StitchLink, so one Stitcher's volleys never tie to each other along their path.
        public const float StitchSpeed = 105f, StitchEvery = 3.2f, StitchLink = 280f, StitchWidth = 6f, StitchSpread = 0.32f, StitchLife = 7f, StitcherRange = 330f;
        public const float StitchPairGap = 240f;  // a pair stands this far apart, side by side, so its fence is this wide
        // Elite traits.
        public const float HasteMult = 1.4f, ShieldedShare = 0.4f, BubbleRegrow = 4f, VolatileFuse = 0.6f, CommandRange = 220f, CommandMult = 1.3f;
        public const int VolatileShots = 12, MirrorEvery = 4;
        public const float SquadShare = 0.7f, SquadFormTime = 2.5f;

        public static readonly SquadDef[] Squads =
        {
            new SquadDef { Name = "Rush", From = 1, Members = new[] { EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer } },
            new SquadDef { Name = "Firing line", From = 3, Members = new[] { EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Sniper } },
            new SquadDef { Name = "Brute pack", From = 5, Members = new[] { EnemyType.Brute, EnemyType.Brute, EnemyType.Lunger } },
            new SquadDef { Name = "Splinters", From = 7, Members = new[] { EnemyType.Splitter, EnemyType.Splitter, EnemyType.Grunt } },
            new SquadDef { Name = "Bombardment", From = 8, Members = new[] { EnemyType.Charger, EnemyType.Mortar, EnemyType.Mortar } },
            new SquadDef { Name = "Shield wall", From = 9, Members = new[] { EnemyType.Shielder, EnemyType.Sniper, EnemyType.Sniper } },
            new SquadDef { Name = "Field hospital", From = 11, Members = new[] { EnemyType.Brute, EnemyType.Brute, EnemyType.Brute, EnemyType.Medic } },
            new SquadDef { Name = "Ambush", From = 16, Members = new[] { EnemyType.Phantom, EnemyType.Phantom, EnemyType.Phantom }, Form = SquadForm.Around },
            new SquadDef { Name = "Beam battery", From = 18, Members = new[] { EnemyType.Shielder, EnemyType.BeamSentry, EnemyType.BeamSentry } },
            new SquadDef { Name = "Pincer", From = 21, Members = new[] { EnemyType.Hexer, EnemyType.Hexer, EnemyType.Grunt, EnemyType.Grunt }, Form = SquadForm.Pincer },
            new SquadDef { Name = "Scorched earth", From = 24, Members = new[] { EnemyType.Scorcher, EnemyType.Scorcher, EnemyType.Spiker, EnemyType.Spiker } },
            new SquadDef { Name = "Siege", From = 26, Members = new[] { EnemyType.Juggernaut, EnemyType.Medic, EnemyType.Medic } },
            new SquadDef { Name = "Reaver pack", From = 28, Members = new[] { EnemyType.Reaver, EnemyType.Reaver, EnemyType.Lunger, EnemyType.Lunger } },
            new SquadDef { Name = "Hive", From = 31, Members = new[] { EnemyType.Nest, EnemyType.Splitter, EnemyType.Splitter } },
            new SquadDef { Name = "Hold the line", From = 14, Members = new[] { EnemyType.Pauser, EnemyType.Grunt, EnemyType.Grunt } },
            new SquadDef { Name = "Hunting party", From = 19, Members = new[] { EnemyType.Leader, EnemyType.Leader, EnemyType.Sniper } },
            new SquadDef { Name = "Seamstresses", From = 22, Members = new[] { EnemyType.Stitcher, EnemyType.Stitcher, EnemyType.Grunt } },
            new SquadDef { Name = "Bastion", From = 36, Members = new[] { EnemyType.Warder, EnemyType.Juggernaut, EnemyType.Grunt, EnemyType.Grunt } },
            // Swordsmen (Swordsman.cs): melee only comes in squads from wave 13, so they join the late waves here
            new SquadDef { Name = "Sword line", From = SwordsmanFrom, Members = new[] { EnemyType.Swordsman, EnemyType.Swordsman, EnemyType.Swordsman } },
            new SquadDef { Name = "Shield line", From = ShieldSwordsmanFrom, Members = new[] { EnemyType.ShieldSwordsman, EnemyType.ShieldSwordsman, EnemyType.Swordsman } },
            // Enemies that don't shoot (NonShooters.cs)
            new SquadDef { Name = "Demolition", From = 15, Members = new[] { EnemyType.FuseRunner, EnemyType.FuseRunner, EnemyType.Grunt } },
            new SquadDef { Name = "Whalers", From = 17, Members = new[] { EnemyType.Hookman, EnemyType.Lunger, EnemyType.Lunger } },
            new SquadDef { Name = "Minefield", From = 18, Members = new[] { EnemyType.Sapper, EnemyType.SpikeBlock, EnemyType.SpikeBlock } },
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
        float emberTickT;

        public static bool HasTrait(Enemy e, EliteTrait t) => (e.Traits & (byte)t) != 0;

        float SentryDamage => 16f;  // the difficulty's per-wave damage growth scales it (27 at wave 23 on Normal; it was 45)
        public const int MaxSentries = 3;  // at most this many Beam Sentries in a wave (CapSentries)
        public const int MaxPulsars = 3;   // ...and Pulsars (Infall.cs)

        // --- Waves -----------------------------------------------------------------------------------------------

        /// A regular wave: about 70% squads (drawn from those unlocked by now, none more than twice), the rest
        /// single enemies as before (added to spawnQueue by BuildWave). Returns how many enemies the squads hold.
        int BuildSquads(int n, int count)
        {
            squadQueue.Clear();
            singlesSinceSquad = 0;
            var ok = new List<SquadDef>();
            foreach (var s in Squads)
                if (s.From <= n && SquadLive(s))  // (none with a retired enemy: BasicFaction.cs)
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

        /// at: where it comes in (a designed wave's spawn point, WaveDesign.cs); null: a random spot on an edge.
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
            {
                if (Rooms.RoomByRoom)
                    basePos = RoomSpawnSpot(60f, out spawnHome);  // by a room's nest (Rooms.cs); its members guard that room
                else
                    basePos = RoomsRandomSpot(80f);  // anywhere on the map, away from you (Rooms.cs)
            }
            else if (LayoutOn && !at.HasValue)
                basePos = LayoutSquadBase();  // in front of an enemy gate (LayoutRules.cs)
            V2 fwd = breach.HasValue ? breachIn : (new V2(Width / 2f, Height / 2f) - basePos).Normalized(), across = fwd.Orthogonal();  // (through a breach: facing into the map)
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
                else if (s.Form == SquadForm.Pincer && i < 2 && !breach.HasValue)  // (through a breach: they come in together)
                    pos = ClampToArena(new V2(i == 0 ? lo.X : hi.X, LayoutOn ? LayoutSideY(i == 0 ? 2 : 3, 150f) : Rand(lo.Y + 150f, hi.Y - 150f)), 40f);
                else
                {
                    bool behind = i > 0 && (type == EnemyType.Sniper || type == EnemyType.Medic || type == EnemyType.BeamSentry || type == EnemyType.Mortar || type == EnemyType.Warder || type == EnemyType.Hookman || type == EnemyType.Sapper);
                    pos = ClampToArena(basePos + across * ((i - (n - 1) / 2f) * 60f) - fwd * (behind ? 70f : 0f), 40f);
                }
                if (type == EnemyType.Nest)
                    pos = ClampToArena(basePos + fwd * 140f, 120f);
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
            DoLog("squad: " + s.Name);
        }

        /// From wave 12, regular enemies can show up as tougher elites: 4% more a wave, up to 35% (times the difficulty's Elites).
        bool RollElite(EnemyDef d) => !d.IsBoss && Wave >= 12 && rng.NextDouble() < EliteChance(Wave);

        public double EliteChance(int wave) => wave < 12 ? 0.0 : Math.Min(0.9, Math.Min(0.35, 0.04 * (wave - 11)) * D(DiffKnob.EliteChance));

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
            if (newRun)
            {
                nextSquadId = 1;
                singlesSinceSquad = 0;
            }
        }

        /// At most MaxSentries Beam Sentries a wave (recorded runs had 5-7, and their beams did more damage than any
        /// other regular enemy): squads that would bring more come apart into single enemies, and spare Sentries become
        /// Snipers or Grunts. The same for the Singularity's Pulsars (Infall.cs): spare ones become Void Acolytes; and the
        /// Lich's Grave Callers and Shriekers (Zombies.cs): spare ones become Shamblers.
        void CapSentries()
        {
            CapBeams(EnemyType.BeamSentry, MaxSentries, () => rng.NextDouble() < 0.5 ? EnemyType.Sniper : EnemyType.Grunt);
            CapBeams(EnemyType.Pulsar, MaxPulsars, () => EnemyType.VoidAcolyte);
            CapBeams(EnemyType.GraveCaller, Zombies.MaxCallers, () => EnemyType.Shambler);  // the Lich's: two Callers raise eight
            CapBeams(EnemyType.Shrieker, Zombies.MaxShriekers, () => EnemyType.Shambler);
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
                if (c != e && c.Pos.DistanceTo(e.Pos) < CommandRange)
                    return m * CommandMult;
            return m;
        }

        /// Host, after the enemies have moved: embers burn, Volatile bursts go off, Mirrored shots come back,
        /// Shielded bubbles grow back.
        void TickVariety(float dt)
        {
            // Stitcher threads: anyone touching one is hurt (hit grace keeps it to one hit at a time).
            StitchLinks(Bullets.Values, stitchLinks);
            foreach (var link in stitchLinks)
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && SegmentDistance(p.Pos, link.A, link.B) < StitchWidth / 2f + p.Def.HitR)
                        Damage(p, Defs.BulletDamage(Defs.SmallShot), "Stitcher (thread)");
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
                            DamageOverTime(p, EmberDps * 0.25f, "Scorcher (embers)");
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
                case EnemyType.Pauser:
                    return KeepDistance(dir, dist, PauserRange) + dir.Orthogonal() * e.Strafe * 0.4f;
                case EnemyType.Leader:  // quick on its feet, circling at range
                    return e.Phase == 1 ? V2.Zero : KeepDistance(dir, dist, LeaderRange) + dir.Orthogonal() * e.Strafe * 0.7f;
                case EnemyType.Stitcher:
                    return KeepDistance(dir, dist, StitcherRange) + dir.Orthogonal() * e.Strafe * 0.3f;
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
                    V2 mid = sum / n, away = (mid - tpos).IsZero ? -dir : (mid - tpos).Normalized();  // the straight line, not the way round a wall
                    V2 goal = mid + away * 160f;
                    return RouteVel(e, goal, (goal - e.Pos) / 80f);  // round walls and crates (LayoutRules.cs)
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
            SquadForm form;
            if (squadForms.TryGetValue(e.SquadId, out form) && form == SquadForm.RiotLine)
                return RiotLineMove(e, mates, tpos, v);  // WardenFaction.cs
            if (form == SquadForm.Accretion && accretionRings.ContainsKey(e.SquadId))
                return AccretionMove(e, mates, v);  // Infall.cs
            if (e.T < SquadFormTime + 0.9f && !e.Def.Melee && e.Type != EnemyType.Phantom && e.Type != EnemyType.Shade && e.Type != EnemyType.Nest)
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
                case EnemyType.Stitcher:
                {
                    // A pair stands side by side facing you, so the orbs they fire together make a fence as wide as the gap.
                    Enemy other = null;
                    foreach (var o in mates)  // (a loop, not Find: no closure every tick)
                        if (o != e && o.Type == EnemyType.Stitcher) { other = o; break; }
                    if (other == null)
                        break;
                    V2 mid = (e.Pos + other.Pos) / 2f;
                    V2 from = (mid - tpos).IsZero ? new V2(1f, 0f) : (mid - tpos).Normalized();
                    float side = e.SquadSlot < other.SquadSlot ? 0.5f : -0.5f;
                    return GoalVel(e, tpos + from * StitcherRange + from.Orthogonal() * (side * StitchPairGap));
                }
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
            List<Enemy> mates;
            if (e.SquadId == 0 || !squadMembers.TryGetValue(e.SquadId, out mates))
                return false;
            bool brutes = false;
            foreach (var o in mates)
                if (IsBrute(o.Type))
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
                case EnemyType.Leader:
                    if (e.Phase == 1)
                        e.Facing = LeadAngle(e, tpos);  // the aim line follows where you'll be
                    break;
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
                            RodTouched(p, e);  // LightningRod.cs
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
                    e.Phase = (byte)(e.WardT <= WarderCharge ? 1 : 0);  // charging: drawn only, the timing is unchanged
                    if (e.WardT <= 0f)
                    {
                        e.WardT = WarderEvery;
                        e.Phase = 0;
                        var near = new List<Enemy>();
                        foreach (var o in Enemies.Values)
                            if (o != e && !o.Def.IsBoss && o.Type != EnemyType.Warder && Targetable(o) && o.Pos.DistanceTo(e.Pos) < WarderRange)
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
                case EnemyType.Pauser:
                    e.FireT = PauseEvery;
                    for (int i = 0; i < PauseShots; i++)
                    {
                        V2 dir = V2.FromAngle(aim + PauseFan * (i / (PauseShots - 1f) - 0.5f));
                        var b = SpawnBullet(Defs.PauseShot, e.Pos + dir * e.Radius, dir * PauseSpeed, 0f, PauseFly + PauseHold, Defs.BulletDamage(Defs.PauseShot), 0);
                        b.Source = e.Type;
                        b.Rush = true;
                    }
                    return true;
                case EnemyType.Leader:
                    if (e.Phase == 0)
                    {
                        // Aim first: a thin line that follows where you'll be.
                        e.Phase = 1;
                        e.FireT = LeaderAim;
                        e.Facing = aim;
                        return true;
                    }
                    e.Phase = 0;
                    e.FireT = LeaderEvery;
                    EnemyShot(e, e.Facing - LeaderSpread, LeaderSpeed, Defs.FastShot);
                    EnemyShot(e, e.Facing + LeaderSpread, LeaderSpeed, Defs.FastShot);
                    return true;
                case EnemyType.Stitcher:
                {
                    e.FireT = StitchEvery;
                    // In a pair, the first fires for both (one orb each, at the same moment); its partner waits.
                    List<Enemy> mates;
                    Enemy lead = null;
                    if (e.SquadId != 0 && squadMembers.TryGetValue(e.SquadId, out mates))
                        foreach (var o in mates)
                            if (o.Type == EnemyType.Stitcher && o.Hp > 0f && (lead == null || o.SquadSlot < lead.SquadSlot))
                                lead = o;
                    Enemy partner = null;
                    if (lead != null && squadMembers.TryGetValue(e.SquadId, out mates))
                        foreach (var o in mates)
                            if (o != lead && o.Type == EnemyType.Stitcher && Targetable(o)) { partner = o; break; }
                    if (partner != null)
                    {
                        if (lead != e)
                            return true;  // the lead fires ours
                        // Both orbs fly the same way (at you from between them), so the thread between them stays as wide as the pair.
                        Player tp;
                        V2 mid = (e.Pos + partner.Pos) / 2f;
                        V2 target = Players.TryGetValue(e.TargetId, out tp) ? tp.Pos : e.Pos + V2.FromAngle(aim) * 300f;
                        float course = (target - mid).Angle;
                        StitchOrb(e, course);
                        StitchOrb(partner, course);
                        partner.FireT = StitchEvery;
                        return true;
                    }
                    // Alone: two orbs a little apart, tied to each other.
                    StitchOrb(e, aim - StitchSpread);
                    StitchOrb(e, aim + StitchSpread);
                    return true;
                }
            }
            return false;
        }

        void StitchOrb(Enemy e, float ang)
        {
            V2 dir = V2.FromAngle(ang);
            SpawnBullet(Defs.StitchShot, e.Pos + dir * e.Radius, dir * StitchSpeed, 0f, StitchLife, Defs.BulletDamage(Defs.StitchShot), 0).Source = e.Type;
        }

        // --- The new shooting patterns ---------------------------------------------------------------------------

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

        /// Where the Leader aims: the angle that meets a player moving as they are now (up to LeaderMaxLead seconds
        /// ahead). A decoy (a Shadow Clone, Vanish) gets aimed at as it stands.
        float LeadAngle(Enemy e, V2 tpos)
        {
            Player p;
            V2 rel = tpos - e.Pos;
            if (!Players.TryGetValue(e.TargetId, out p) || p.Pos.DistanceTo(tpos) > 1f)
                return rel.Angle;
            V2 v = p.Motion;
            if (v.Length > 450f)
                v = v.Normalized() * 450f;
            return (rel + v * LeadTime(rel, v, LeaderSpeed)).Angle;
        }

        /// How long a shot at this speed takes to meet something at rel moving at v (0 if it can't).
        public static float LeadTime(V2 rel, V2 v, float speed)
        {
            float a = v.Dot(v) - speed * speed, b = 2f * rel.Dot(v), c = rel.Dot(rel);
            float t;
            if (Math.Abs(a) < 1e-3f)
                t = b < 0f ? -c / b : 0f;
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f)
                    return 0f;
                float sq = (float)Math.Sqrt(disc);
                float t1 = (-b - sq) / (2f * a), t2 = (-b + sq) / (2f * a);
                t = t1 > 0f && (t1 < t2 || t2 <= 0f) ? t1 : t2;
            }
            return t > 0f ? Math.Min(LeaderMaxLead, t) : 0f;
        }

        /// A Pauser's shot: full speed out of the barrel, slowing to a stop by PauseFly, then hanging still.
        /// Worked out from its age, so every machine moves it the same.
        public static float PauseSpeedNow(Bullet b)
        {
            float age = b.Life0 - b.Life;
            return age >= PauseFly ? 0f : 1f - age / PauseFly;
        }

        /// How far (in seconds of its full speed) a Pauser's shot will have gone in the next t seconds.
        public static float PauseTravel(Bullet b, float t)
        {
            float age = b.Life0 - b.Life, m = Math.Min(t, Math.Max(0f, PauseFly - age));
            return m - ((age + m) * (age + m) - age * age) / (2f * PauseFly);
        }

        /// The bots' list of Stitcher threads, rebuilt each time they decide (BotBrain.cs).
        readonly List<StitchThread> botThreads = new List<StitchThread>();

        /// Host: a Pauser's shot has hung its time: a shot rushes from there at the nearest player.
        void PauseRush(Bullet b)
        {
            Player near = null;
            foreach (var p in Players.Values)
                if (!p.Downed && p.VanishT <= 0f && (near == null || p.Pos.DistanceSq(b.Pos) < near.Pos.DistanceSq(b.Pos)))
                    near = p;
            if (near == null)
                return;
            V2 dir = (near.Pos - b.Pos).Normalized();
            SpawnBullet(Defs.SmallShot, b.Pos, dir * PauseRushSpeed, 0f, 4f, Defs.BulletDamage(Defs.PauseShot), 0).Source = b.Source;
        }

        public struct StitchThread { public V2 A, B; }

        readonly List<StitchThread> stitchLinks = new List<StitchThread>();
        static readonly List<Bullet> stitchScratch = new List<Bullet>();

        /// The Stitcher's threads: each orb is tied to its nearest other orb within StitchLink (each pair once).
        /// Worked out from the orbs where they are, so the host (which hurts with them) and every screen agree.
        public static void StitchLinks(IEnumerable<Bullet> bullets, List<StitchThread> links)
        {
            links.Clear();
            stitchScratch.Clear();
            foreach (var b in bullets)
                if (b.Kind == Defs.StitchShot)
                    stitchScratch.Add(b);
            if (stitchScratch.Count < 2)
                return;
            stitchScratch.Sort((x, y) => x.Id.CompareTo(y.Id));
            for (int i = 0; i < stitchScratch.Count; i++)
            {
                int best = -1;
                float bestD = StitchLink * StitchLink;
                for (int j = 0; j < stitchScratch.Count; j++)
                {
                    float d = j == i ? float.MaxValue : stitchScratch[i].Pos.DistanceSq(stitchScratch[j].Pos);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = j;
                    }
                }
                if (best < 0)
                    continue;
                // Each pair once: skip it if the other one already chose this one.
                if (best < i && NearestOther(best) == i)
                    continue;
                links.Add(new StitchThread { A = stitchScratch[i].Pos, B = stitchScratch[best].Pos });
            }
        }

        static int NearestOther(int i)
        {
            int best = -1;
            float bestD = StitchLink * StitchLink;
            for (int j = 0; j < stitchScratch.Count; j++)
            {
                if (j == i)
                    continue;
                float d = stitchScratch[i].Pos.DistanceSq(stitchScratch[j].Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = j;
                }
            }
            return best;
        }

        /// The Reaver takes half again as much while it's open after a swipe.
        public static bool ReaverIsOpen(Enemy e) => e.Type == EnemyType.Reaver && e.Phase == 3;

        // --- Hits and deaths -------------------------------------------------------------------------------------

        public const int BubbleCells = 12;

        /// The cell a direction points at. Cell i sits at angle -90 degrees + i * 30 degrees.
        public static int CellAt(float ang)
        {
            int c = (int)Math.Round((ang + MathX.Tau * 0.25f) / (MathX.Tau / BubbleCells));
            return ((c % BubbleCells) + BubbleCells) % BubbleCells;
        }

        /// Keeps the twelve cells adding up to BubbleHp. Anything that sets BubbleHp directly (a new ward, a regrow, a
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

        /// A bubble (Shielded, or a Warder's) soaks damage first. Returns what gets through. The damage lands on the
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
                e.WardId = 0;
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
                int n = e.Type == EnemyType.Juggernaut || e.Type == EnemyType.Nest || IsBrute(e.Type) ? 4 : 2;
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
