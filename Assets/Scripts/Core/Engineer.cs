using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Engineer (Docs/Engineer.md): a builder who works in vectors. Each role's weapon summons a robot Engineer
    /// (EngineerBots.cs: Iron Fist, Vector Blade, Stasis Gun) and stands in for the class's other lobby places (WeaponDef.FillsGaps).
    ///   Drag Pylon (DPS 1st ability): a pylon at the cursor whose field slows enemies inside; it stays until the next one, which folds it up.
    ///   Draftsman (passive): +25% vectors from every gain.
    ///   Breach (DPS ultimate): a hammer smashes the wall you aim at: a blast by the wall, and in the Expanding arena a free random room
    ///     behind it with one thing in it (vectors, gold or an ambush). No wall in reach: the blast and the loot land where you aim.
    ///   Tank: Vector Wall (1st ability: a 5-tile wall across your aim at the cursor that stops enemy shots for 8 s), Bastion
    ///     (ultimate: a ring of walls round you for 10 s with four gaps).
    ///   Healer: Med Pylon (1st ability: the Drag Pylon's twin, its field heals allies), Safe Room (ultimate: the Breach hammer opens a
    ///     room that heals everyone inside and cuts their damage taken for 12 s; with no room, a healing dome at the cursor).
    public partial class Game
    {
        internal static readonly KitDef EngineerKit = new KitDef
        {
            Id = Kit.Engineer, Name = "Engineer", Icon = "class_engineer", HpMult = 1f, SpeedMult = 0.95f,
            Blurb = "A builder who works in vectors: robots, walls, pylons and breaches drawn in cyan ink. Draftsman: +25% vectors. One robot weapon, ability and ultimate per role for now. Full health, a little slower (95%).",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => { },  // the robot weapons: the summon and the basic attacks are TickBotWeapon's (EngineerBots.cs)
            Ability = (g, p, id, target) => g.EngineerAbility(p, id, target),
            Tick = (g, dt) => g.TickEngineer(dt),
        };

        // --- Numbers ---
        public const float PylonRadius = 130f, PylonSlow = 0.55f, PylonBossSlow = 0.8f, PylonReach = 320f, PylonLinger = 0.6f;
        public const float BreachRange = 220f, BreachRadius = 140f, BreachHitDmg = 80f, BreachShove = 90f, BreachFxTime = 1.3f;
        public const int BreachGold = 40, BreachVectors = 10;
        public const float DraftsmanMult = 1.25f;
        public const int VWallTiles = 5;
        public const float VWallReach = 220f, VWallSpacing = 36f, VWallLife = 8f, BastionRadius = 130f, BastionLife = 10f;
        public const int BastionTiles = 16, BastionGaps = 4;
        public const float SafeRoomLife = 12f, SafeRoomCut = 0.3f, SafeDomeRadius = 160f;

        /// Class cards (Upgrades.All gathers them): the Drag Pylon's.
        internal static readonly UpgradeDef[] EngineerCards =
        {
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.DragPylon, MaxStacks = 2, Name = "Wide Field", Desc = "Drag Pylon's field is 25% wider and slows 10% more",
                Apply = s => { s.PylonSizeMult += 0.25f; s.PylonSlowBonus += 0.1f; } },
        };

        /// Draftsman, the Engineer's passive: +25% vectors from every gain (part-vectors carried to the next one).
        int Draftsman(Player p, int n)
        {
            if (n <= 0 || p.Kit != Kit.Engineer)
                return n;
            float more = n * (DraftsmanMult - 1f) + p.DraftCarry;
            int whole = (int)Math.Floor(more);
            p.DraftCarry = more - whole;
            return n + whole;
        }

        internal static readonly WeaponDef[] EngineerWeapons =
        {
            // each role's one weapon: a robot Engineer of the role, and a basic attack (EngineerBots.cs); it fills the class's other lobby places
            new WeaponDef { Id = WeaponId.IronFist, Kit = Kit.Engineer, FillsGaps = true, Role = Role.Tank, Choice = 0, Melee = true, Reach = 95f, Grow = 15f, Damage = 10f,
                FireInterval = 0.7f, BotRange = 80f, Name = "Iron Fist", Sound = Sfx.SwingHeavy,
                Desc = "Primary Fire: Summon a heavy Tank Bot that draws enemies and shoots nails.\n\nAlternative Fire: With the bot out, shove enemies away with a robotic fist.",
                ExtraDesc = "the fist's arc reaches 15 px further" },
            new WeaponDef { Id = WeaponId.StasisGun, Kit = Kit.Engineer, FillsGaps = true, Role = Role.Healer, Choice = 0, Reach = StasisReach, Damage = 14f,
                FireInterval = 0.1f, BotRange = 250f, Name = "Stasis Gun", Sound = Sfx.Ability,
                Desc = "Primary Fire: Summon a Medic Bot that shoots rivets and heals nearby allies.\n\nAlternative Fire: With the bot out, hold to freeze enemies in place at your cursor.",
                ExtraDesc = "the stasis field is 8 px wider" },
            new WeaponDef { Id = WeaponId.VectorBlade, Kit = Kit.Engineer, FillsGaps = true, Role = Role.Dps, Choice = 0, Melee = true, Reach = 88f, Grow = 12f, Damage = 24f,
                FireInterval = 0.4f, BotRange = 75f, Name = "Vector Blade", Sound = Sfx.SwingLight,
                Desc = "Primary Fire: Summon a fast Assault Bot with a rapid rivet gun.\n\nAlternative Fire: With the bot out, swipe a cyan blade in front of you.",
                ExtraDesc = "the swipe reaches 12 px further" },
        };

        internal static readonly AbilityDef[] EngineerAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.DragPylon, Kit = Kit.Engineer, Role = Role.Dps, Choice = 0, FillsGaps = true,
                Name = "Drag Pylon", Icon = "dragpylon", Cooldown = 6f,
                Desc = "Plant a pylon whose field slows enemies.",
                Capstone = "Its field is 40% wider.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.VectorWall, Kit = Kit.Engineer, Role = Role.Tank, Choice = 0, FillsGaps = true,
                Name = "Vector Wall", Icon = "vectorwall", Cooldown = 10f,
                Desc = "Draw a wall that stops enemy shots.",
                Capstone = "Seven tiles, and it lasts 12 s.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Bastion, Kit = Kit.Engineer, Role = Role.Tank, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Bastion", Icon = "bubble", Cooldown = 75f,
                Desc = "Raise a ring of walls around you with gaps to shoot through.",
                Capstone = "No gaps for the first 3 s, and it lasts 14 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.MedPylon, Kit = Kit.Engineer, Role = Role.Healer, Choice = 0, FillsGaps = true,
                Name = "Med Pylon", Icon = "medpylon", Cooldown = 6f,
                Desc = "Plant a pylon whose field heals allies.",
                Capstone = "Its field is 40% wider.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.SafeRoom, Kit = Kit.Engineer, Role = Role.Healer, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Safe Room", Cooldown = 80f,
                Desc = "Open a wall into a room that heals and protects everyone inside.",
                Capstone = "It lasts 18 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Breach, Kit = Kit.Engineer, Role = Role.Dps, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Breach", Cooldown = 70f,
                Desc = "Smash a wall to open a random room with loot or an ambush.",
                Capstone = "The blast is twice as wide.",
            },
        };

        bool EngineerAbility(Player p, AbilityId id, V2 target)
        {
            switch (id)
            {
                case AbilityId.DragPylon:
                    CastDragPylon(p, target, castMastered);
                    return true;
                case AbilityId.Breach:
                    CastBreach(p, target, castMastered);
                    return true;
                case AbilityId.MedPylon:
                    CastPylon(p, target, castMastered, ZoneKind.MedPylon);
                    return true;
                case AbilityId.VectorWall:
                    CastVectorWall(p, target, castMastered);
                    return true;
                case AbilityId.Bastion:
                    CastBastion(p, castMastered);
                    return true;
                case AbilityId.SafeRoom:
                    CastSafeRoom(p, target, castMastered);
                    return true;
            }
            return false;
        }

        /// Host, every tick: each Engineer's robot is kept up (EngineerBots.cs); the Drag Pylons slow.
        void TickEngineer(float dt)
        {
            foreach (var p in Players.Values)
            {
                KeepBots(p, dt);  // the robots (EngineerBots.cs)
                if (p.Weapon.Id != WeaponId.StasisGun || p.Downed || !p.Firing)
                    EndStasis(p);
            }
            TickBreachLoot();
            foreach (var z in Zones.Values)
            {
                if (z.Kind == ZoneKind.SafeRoom)
                    z.DamageCut = SafeRoomCut;  // (the generic zone tick heals; Combat.cs reads the cut)
                if (z.Kind != ZoneKind.DragPylon)
                    continue;
                Player owner;
                float bonus = Players.TryGetValue(z.Owner, out owner) ? owner.Stats.PylonSlowBonus : 0f;
                foreach (var e in Enemies.Values)
                    if (e.SpawnT <= 0f && e.Pos.CloserThan(z.Pos, z.Radius + e.Radius * 0.5f))
                        Slow(e, PylonLinger, e.Def.IsBoss ? PylonBossSlow - bonus * 0.5f : PylonSlow - bonus);
            }
        }

        /// Where an enemy goes (EnemyAI): an enemy close to an Engineer's robot (EngineerBots.cs) goes for it instead.
        V2 RobotTarget(Enemy e, V2 tpos)
        {
            Minion best = null;
            float bestD = float.MaxValue;
            foreach (var m in Minions.Values)
                if (IsBot(m.Kind) && m.SentT >= BotBuild)
                {
                    float dd = m.Pos.DistanceSq(e.Pos), taunt = BotTaunt(m.Kind);
                    if (dd < taunt * taunt && dd < bestD && dd < tpos.DistanceSq(e.Pos))
                    {
                        bestD = dd;
                        best = m;
                    }
                }
            return best != null ? best.Pos : tpos;
        }

        // --- Drag Pylon ------------------------------------------------------------------

        void CastDragPylon(Player p, V2 target, bool cap) => CastPylon(p, target, cap, ZoneKind.DragPylon);

        /// A Drag Pylon or a Med Pylon: one of each kind per player (the next folds the old one up).
        void CastPylon(Player p, V2 target, bool cap, ZoneKind kind)
        {
            foreach (var z in Zones.Values)
                if (z.Owner == p.Id && z.Kind == kind)
                    z.T = 0.01f;  // the old one folds up
            V2 to = target - p.Pos;
            if (to.Length > PylonReach)
                target = p.Pos + to.Normalized() * PylonReach;
            AddZone(kind, p, ClampToArena(target, 10f), PylonRadius * (cap ? 1.4f : 1f) * (kind == ZoneKind.DragPylon ? p.Stats.PylonSizeMult : 1f), 100000f);
            BroadcastFx(FxKind.Ring, target, Kits.Get(kind).Color, 40f, 0.35f, Sfx.Ability);
        }

        // --- Breach ----------------------------------------------------------------------

        static readonly SurpriseShape[] BreachShapes = { SurpriseShape.Stash, SurpriseShape.Cross, SurpriseShape.LHall, SurpriseShape.THall, SurpriseShape.Ring, SurpriseShape.Chamfer };

        /// True when `at` is on the floor of the world (inside one of its areas).
        bool OnFloor(V2 at)
        {
            var w = CurrentWorld;
            foreach (var a in w.Areas)
                if (a.Contains(at))
                    return true;
            return false;
        }

        void CastBreach(Player p, V2 target, bool cap)
        {
            V2 hit, dir;
            var made = SmashWall(p, target, cap, 1f, out hit, out dir);
            if (made == null)
            {
                ApplyBreachLoot(p, hit - dir * 30f, RollBreachLoot(false));  // no room: the loot lands at once
                return;
            }
            if (made[0].Surprise == (byte)(1 + (int)SurpriseShape.Stash))
                return;  // a Stash holds its own vector cache (BreachEvent.cs): that is its loot
            V2 lootAt = BiggestMiddle(made);  // the loot waits in the middle of the room's biggest piece, to walk up to (step 3)
            Nodes.Add(new MapNode { X = (short)Math.Round(lootAt.X), Y = (short)Math.Round(lootAt.Y), Kind = NodeKind.BreachLoot, Rule = (byte)RollBreachLoot(true) });
            BroadcastExpansion();
        }

        /// The middle of a new room's biggest piece, and how far it reaches from there.
        static V2 BiggestMiddle(List<ExpansionRoom> made) { float r; return BiggestMiddle(made, out r); }

        static V2 BiggestMiddle(List<ExpansionRoom> made, out float reach)
        {
            var big = made[0];
            foreach (var r in made)
                if (r.W * r.H > big.W * big.H)
                    big = r;
            reach = Math.Max(big.W, big.H) / 2f;
            return new V2((big.X + big.X1) / 2f, (big.Y + big.Y1) / 2f);
        }

        /// The Breach hammer (Breach and Safe Room): the blast where it lands (the wall you aim at, or your cursor with no wall in
        /// reach), and in the Expanding arena a new room behind a wall it hit; null when no room opened.
        List<ExpansionRoom> SmashWall(Player p, V2 target, bool cap, float dmgMult, out V2 hit, out V2 dir)
        {
            dir = (target - p.Pos).Length > 1f ? (target - p.Pos).Normalized() : V2.FromAngle(p.Aim);
            V2? wall = null;
            for (float d = p.Def.BodyR; d <= BreachRange + p.Def.BodyR; d += 8f)
            {
                V2 at = p.Pos + dir * d;
                if (!OnFloor(at))
                {
                    wall = at;
                    break;
                }
            }
            hit = wall ?? p.Pos + dir * Math.Min(BreachRange, (target - p.Pos).Length);
            float blast = BreachRadius * (cap ? 2f : 1f);
            foreach (var e in FoesInCircle(hit, blast, by: p.Id))
            {
                HurtEnemy(e, BreachHitDmg * dmgMult * p.Stats.DmgMult * castPower, p.Id);
                if (!Heavy(e))
                {
                    V2 away = e.Pos - hit;
                    KnockEnemy(e, (away.Length < 1f ? -dir : away.Normalized()) * BreachShove);
                }
            }
            BroadcastFx(FxKind.BreachSmash, hit, hit + dir, new Rgba(1f, 0.71f, 0.28f), blast, BreachFxTime, Sfx.BigExplode);
            List<ExpansionRoom> made = null;
            if (wall.HasValue && IsHost && ExpandingOn && Expansion.BoughtCount(Expanded) < Expansion.MaxRooms)
            {
                V2 through = wall.Value + dir * (BreachEvent.Behind + 10f);
                int start = rng.Next(BreachShapes.Length);
                for (int k = 0; k < BreachShapes.Length && made == null; k++)
                {
                    var opts = Expansion.GridSurpriseOptions(Expanded, BreachShapes[(start + k) % BreachShapes.Length], through);
                    if (opts.Count > 0)
                        made = opts[0].Rooms;
                }
                if (made != null)
                {
                    Expanded.AddRange(made);
                    expWorld = null;
                    LayoutAge = 0f;
                    NoteSurprise(made[0].Surprise);
                    BroadcastExpansion();
                }
            }
            return made;
        }

        // --- Vector Wall, Bastion and Safe Room (step 4) -------------------------------------

        Minion PlaceBlock(Player p, V2 at, float life)
        {
            var b = SpawnMinion(MinionKind.VectorBlock, p, at, life);
            b.MaxHp = b.BlockHp;
            return b;
        }

        /// A wall of tiles across your aim, centred on the cursor (at most VWallReach away).
        void CastVectorWall(Player p, V2 target, bool cap)
        {
            V2 to = target - p.Pos;
            V2 dir = to.Length > 1f ? to.Normalized() : V2.FromAngle(p.Aim), side = new V2(-dir.Y, dir.X);
            V2 mid = p.Pos + dir * Math.Max(60f, Math.Min(VWallReach, to.Length));
            int n = cap ? VWallTiles + 2 : VWallTiles;
            for (int i = 0; i < n; i++)
            {
                var b = PlaceBlock(p, mid + side * ((i - (n - 1) / 2f) * VWallSpacing), cap ? 12f : VWallLife);
                b.Aim = dir.Angle;
            }
            BroadcastFx(FxKind.Ring, mid, Kits.Get(MinionKind.VectorBlock).Color, VWallSpacing * n / 2f, 0.35f, Sfx.Ability);
        }

        /// A ring of tiles round you with four gaps (mastered: the gaps fill in for the first 3 s).
        void CastBastion(Player p, bool cap)
        {
            float life = cap ? 14f : BastionLife;
            for (int i = 0; i < BastionTiles; i++)
            {
                bool gap = i % (BastionTiles / BastionGaps) == 0;
                if (gap && !cap)
                    continue;
                float a = MathX.Tau * i / BastionTiles;
                var b = PlaceBlock(p, p.Pos + V2.FromAngle(a) * BastionRadius, gap ? 3f : life);
                b.Aim = a;
            }
            BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(MinionKind.VectorBlock).Color, BastionRadius, 0.5f, Sfx.Shockwave);
        }

        /// Safe Room: the Breach hammer (half its damage), then a healing zone over the new room, or a dome at the cursor.
        void CastSafeRoom(Player p, V2 target, bool cap)
        {
            V2 hit, dir;
            var made = SmashWall(p, target, cap, 0.5f, out hit, out dir);
            float life = cap ? 18f : SafeRoomLife;
            Zone z;
            if (made != null)
            {
                float reach;
                V2 mid = BiggestMiddle(made, out reach);
                z = AddZone(ZoneKind.SafeRoom, p, mid, reach + 20f, life);
            }
            else
            {
                V2 to = target - p.Pos;
                V2 at = to.Length > BreachRange ? p.Pos + to.Normalized() * BreachRange : target;
                z = AddZone(ZoneKind.SafeRoom, p, ClampToArena(at, 10f), SafeDomeRadius, life);
            }
            z.DamageCut = SafeRoomCut;
        }

        /// The one thing behind the wall: 0 vectors, 1 gold, 4 an ambush (only in a real room; vectors only in the Expanding arena).
        /// (2 and 3 were the Strider's repair and workbench, retired with it.)
        int RollBreachLoot(bool room)
        {
            int roll = rng.Next(100);
            int kind = roll < 40 ? 0 : roll < 80 ? 1 : 4;
            if (kind == 4 && !room)
                kind = 1;
            if (kind == 0 && !ExpandingOn)
                kind = 1;
            return kind;
        }

        public const float BreachLootReach = 46f;  // walk this close (plus your body) to a Breach room's loot to take it

        /// Host, every tick (the Expanding arena): a Breach room's loot (NodeKind.BreachLoot, its kind in Rule) is taken by the first
        /// player to walk up to it; an ambush springs then. The node is spent.
        void TickBreachLoot()
        {
            if (!IsHost || !ExpandingOn)
                return;
            for (int i = 0; i < Nodes.Count; i++)
            {
                var nd = Nodes[i];
                if (nd.Kind != NodeKind.BreachLoot || nd.Spent)
                    continue;
                Player taker = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.Pos.CloserThan(nd.Pos, BreachLootReach + p.Def.BodyR))
                        taker = p;
                if (taker == null)
                    continue;
                nd.Spent = true;
                Nodes[i] = nd;
                ApplyBreachLoot(taker, nd.Pos, nd.Rule);
                BroadcastExpansion();
            }
        }

        /// The loot itself, for `p` (who took it), at `at`.
        void ApplyBreachLoot(Player p, V2 at, int kind)
        {
            switch (kind)
            {
                case 0:
                    DropVectorAt(at, BreachVectors);
                    break;
                case 1:
                    if (ExpandingOn)
                        foreach (int owner in LootOwners())  // (a full copy each: Coins.cs)
                            for (int i = 0; i < BreachGold / 5; i++)
                                AddCoin(owner, 5, false, at, 8f, 18f);
                    else
                        foreach (var q in Players.Values)
                            q.Gold += BreachGold;
                    break;
                default:
                    var ambush = AmbushPool();  // (the wave's own types: MysteryDoors.cs)
                    for (int i = 0; i < 2; i++)
                    {
                        var e = SpawnEnemyAt(ambush[rng.Next(ambush.Count)], at + new V2(i == 0 ? -20f : 20f, 0f), 0.6f, 0, true);
                        if (e != null)
                            e.Pos = e.Disp = ClampToArena(e.Pos, e.Radius);
                    }
                    break;
            }
            BroadcastFx(FxKind.Spark, at, new Rgba(1f, 0.71f, 0.28f), 30f, 0.5f, Sfx.Pick);
        }
    }
}
