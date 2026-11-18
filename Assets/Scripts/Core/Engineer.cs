using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Engineer (Docs/Engineer.md): a builder who works in vectors. Step 1 is a DPS-only kit (its other lobby places are
    /// stand-ins, locked: WeaponDef.FillsGaps):
    ///   Vector Forge (weapon): Fire is a light rivet gun; the weapon keeps one Strider at your side, a walker that hunts the enemy
    ///     nearest you, punches, slams every few seconds and draws enemies near it. Alt (the weapon-action key) on an enemy locks it on
    ///     (+50% damage and faster until that enemy dies); on the floor it posts it there to guard; near you it calls it back.
    ///     A broken Strider is rebuilt after StriderRebuild seconds.
    ///   Drag Pylon (Space): a pylon at the cursor whose field slows enemies inside; it stays until the next one, which folds it up.
    ///   Draftsman (passive, step 2): +25% vectors from every gain; standing by a hurt Strider repairs it with vectors.
    ///   Breach (ultimate): a hammer smashes the wall you aim at: a blast by the wall, and in the Expanding arena a free random room
    ///     behind it with one thing in it (vectors, gold, a repair, a workbench or an ambush). No wall in reach: the blast and the loot
    ///     land where you aim.
    /// Step 4, the Tank and Healer kits:
    ///   Tank: Bulwark Forge (a nail shotgun; its walker is the Sentinel, a heavier Strider that taunts from farther), Vector Wall
    ///     (Space: a 5-tile wall across your aim at the cursor that stops enemy shots for 8 s), Bastion (ultimate: a ring of walls
    ///     round you for 10 s with four gaps).
    ///   Healer: Medi-Drone Forge (a light rivet gun; a med-drone companion heals the most hurt ally), Med Pylon (Space: the Drag
    ///     Pylon's twin, its field heals allies), Safe Room (ultimate: the Breach hammer opens a room that heals everyone inside and
    ///     cuts their damage taken for 12 s; with no room, a healing dome at the cursor).
    public partial class Game
    {
        internal static readonly KitDef EngineerKit = new KitDef
        {
            Id = Kit.Engineer, Name = "Engineer", Icon = "class_engineer", HpMult = 1f, SpeedMult = 0.95f,
            Blurb = "Builds in vectors: walkers, drones, walls, fields and breaches. Draftsman: +25% vectors, and vectors repair your walker. Full health, a little slower.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireEngineerWeapon(p, aim, dir, muzzle, dmg, extra),
            Ability = (g, p, id, target) => g.EngineerAbility(p, id, target),
            Tick = (g, dt) => g.TickEngineer(dt),
        };

        // --- Numbers ---
        const byte DashStrider = 43;
        public const float StriderHp = 450f, StriderHpPerWave = 0.08f, StriderRebuild = 8f, StriderBuild = 1.2f;
        public const float StriderLockMult = 1.5f, StriderLockSpeed = 1.4f, StriderHuntLeash = 340f, StriderGuardRadius = 200f;
        public const float StriderSlamEvery = 4f, StriderSlamDmg = 60f, StriderSlamStun = 0.4f, StriderSlamKnock = 40f;
        public const float StriderTaunt = 150f;     // enemies this close to a Strider go for it instead of you
        public const float StriderPick = 60f;       // Alt picks the enemy this close to the cursor
        public const float StriderBenchMult = 1.25f;
        public const float PylonRadius = 130f, PylonSlow = 0.55f, PylonBossSlow = 0.8f, PylonReach = 320f, PylonLinger = 0.6f;
        public const float BreachRange = 220f, BreachRadius = 140f, BreachHitDmg = 80f, BreachShove = 90f, BreachFxTime = 1.3f;
        public const int BreachGold = 40, BreachVectors = 10;
        public const float DraftsmanMult = 1.25f, RepairReach = 90f, RepairHp = 30f, RepairEvery = 0.25f, RebuildMin = 2f;
        public const float SentinelHp = 900f, SentinelRebuild = 10f, SentinelTaunt = 220f, SentinelSlamDmg = 45f;
        public const int VWallTiles = 5;
        public const float VWallReach = 220f, VWallSpacing = 36f, VWallLife = 8f, BastionRadius = 130f, BastionLife = 10f;
        public const int BastionTiles = 16, BastionGaps = 4;
        public const float SafeRoomLife = 12f, SafeRoomCut = 0.3f, SafeDomeRadius = 160f;

        /// Class cards (Upgrades.All gathers them): the Strider's and the Drag Pylon's.
        internal static readonly UpgradeDef[] EngineerCards =
        {
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.VectorForge }, MaxStacks = 3, Name = "Reinforced Frame", Desc = "Your Strider has 30% more health",
                Apply = s => s.StriderHpMult += 0.3f },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.VectorForge }, MaxStacks = 2, Name = "Hydraulic Fists", Desc = "Your Strider punches and slams 20% more often",
                Apply = s => s.StriderRateMult += 0.2f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Weapons = new[] { WeaponId.VectorForge }, MaxStacks = 2, Name = "Quick Assembly", Desc = "A broken Strider is rebuilt 2.5 s sooner",
                Apply = s => s.StriderRebuildCut += 2.5f },
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

        public static float StriderRebuildTime(Player p) => Math.Max(RebuildMin, StriderRebuild - p.Stats.StriderRebuildCut);

        internal static readonly WeaponDef[] EngineerWeapons =
        {
            new WeaponDef { Id = WeaponId.VectorForge, Kit = Kit.Engineer, Role = Role.Dps, Choice = 0, FillsGaps = true, Speed = 900f, Life = 0.5f, Damage = 10f,
                FireInterval = 0.3f, BotRange = 300f, Name = "Vector Forge", Sound = Sfx.ShotPistol,
                Desc = "A light rivet gun. Its real job is the Strider: one walker, built at your side, that hunts the enemy nearest you, punches, "
                    + "slams the ground every 4 s and draws enemies near it. Alt on an enemy locks the Strider on (+50% damage until it dies); "
                    + "Alt on the floor posts it there to guard; Alt by you calls it back. A broken Strider is rebuilt in 8 s.",
                ExtraDesc = "+1 rivet, fanned out" },
            new WeaponDef { Id = WeaponId.BulwarkForge, Kit = Kit.Engineer, Role = Role.Tank, Choice = 0, FillsGaps = true, Speed = 650f, Life = 0.35f, Damage = 7f,
                FireInterval = 0.55f, BotRange = 170f, Name = "Bulwark Forge", Sound = Sfx.ShotShotgun,
                Desc = "A nail shotgun: six nails in a spread. The weapon keeps a Sentinel at your side, a heavy walker (900 health) that soaks shots, "
                    + "draws enemies from 220 px, punches and slams. Alt orders it like the Strider: lock on, post, call back. A broken Sentinel is rebuilt in 10 s.",
                ExtraDesc = "+2 nails" },
            new WeaponDef { Id = WeaponId.MediDroneForge, Kit = Kit.Engineer, Role = Role.Healer, Choice = 0, FillsGaps = true, Speed = 850f, Life = 0.5f, Damage = 8f,
                FireInterval = 0.3f, BotRange = 300f, Name = "Medi-Drone Forge", Sound = Sfx.ShotPistol, Companion = MinionKind.MediDrone, CompanionCount = 1,
                Desc = "A light rivet gun. A med-drone flies with you, goes to the most hurt ally and heals everyone near it for 12 a second.",
                ExtraDesc = "+1 rivet and +1 med-drone" },
            // the second weapons: a robot Engineer of the role, and a basic attack (EngineerBots.cs)
            new WeaponDef { Id = WeaponId.IronFist, Kit = Kit.Engineer, Role = Role.Tank, Choice = 1, Melee = true, Reach = 95f, Grow = 15f, Damage = 10f,
                FireInterval = 0.7f, BotRange = 80f, Name = "Iron Fist", Sound = Sfx.SwingHeavy,
                Desc = "Click to summon a Tank Bot at your cursor: a heavy robot Engineer (850 health) with a nail shotgun that draws enemies from 240 px. "
                    + "Click an enemy to send it there. With the robot out, a click is a robotic fist: a shove in an arc in front of you, "
                    + "10 damage and a long push. A broken robot can be summoned again after 8 s.",
                ExtraDesc = "the fist's arc reaches 15 px further" },
            new WeaponDef { Id = WeaponId.StasisGun, Kit = Kit.Engineer, Role = Role.Healer, Choice = 1, Reach = StasisReach, Damage = 14f,
                FireInterval = 0.1f, BotRange = 250f, Name = "Stasis Gun", Sound = Sfx.Ability,
                Desc = "Click to summon a Medic Bot at your cursor: a robot Engineer (420 health) that shoots rivets and heals allies near it for 9 a second. "
                    + "Click an enemy to send it there. With the robot out, hold Fire for a stasis field at your cursor (60 px, up to 300 px away): "
                    + "enemies in it are held in place (bosses slowed) and take 14 damage a second. A broken robot can be summoned again after 8 s.",
                ExtraDesc = "the stasis field is 8 px wider" },
            new WeaponDef { Id = WeaponId.VectorBlade, Kit = Kit.Engineer, Role = Role.Dps, Choice = 1, Melee = true, Reach = 88f, Grow = 12f, Damage = 24f,
                FireInterval = 0.4f, BotRange = 75f, Name = "Vector Blade", Sound = Sfx.SwingLight,
                Desc = "Click to summon an Assault Bot at your cursor: a fast robot Engineer (380 health) with a rapid rivet gun. "
                    + "Click an enemy to send it there. With the robot out, a click swipes a cyan blade in front of you for 24. "
                    + "A broken robot can be summoned again after 8 s.",
                ExtraDesc = "the swipe reaches 12 px further" },
        };

        internal static readonly AbilityDef[] EngineerAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.DragPylon, Kit = Kit.Engineer, Role = Role.Dps, Choice = 0, FillsGaps = true,
                Name = "Drag Pylon", Cooldown = 6f,
                Desc = "Plant a pylon at your cursor (up to 320 px away). Its field slows enemies inside by 45% (bosses 20%). It stays until you plant the next one.",
                Capstone = "Its field is 40% wider.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.VectorWall, Kit = Kit.Engineer, Role = Role.Tank, Choice = 0, FillsGaps = true,
                Name = "Vector Wall", Cooldown = 10f,
                Desc = "Draw a 5-tile wall across your aim at the cursor (up to 220 px away). It stops enemy shots for 8 s, or until it has soaked its fill.",
                Capstone = "Seven tiles, and it lasts 12 s.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Bastion, Kit = Kit.Engineer, Role = Role.Tank, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Bastion", Cooldown = 75f,
                Desc = "Raise a ring of vector walls round you (130 px) for 10 s, with four gaps to shoot out of. The walls stop enemy shots.",
                Capstone = "No gaps for the first 3 s, and it lasts 14 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.MedPylon, Kit = Kit.Engineer, Role = Role.Healer, Choice = 0, FillsGaps = true,
                Name = "Med Pylon", Cooldown = 6f,
                Desc = "Plant a pylon at your cursor (up to 320 px away). Its field heals allies inside for 10 a second. It stays until you plant the next one.",
                Capstone = "Its field is 40% wider.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.SafeRoom, Kit = Kit.Engineer, Role = Role.Healer, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Safe Room", Cooldown = 80f,
                Desc = "The Breach hammer opens the wall you aim at: in the Expanding arena a room behind it heals everyone inside for 20 a second and cuts the damage they take by 30% for 12 s. "
                    + "No room: a healing dome (160 px) at your cursor.",
                Capstone = "It lasts 18 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Breach, Kit = Kit.Engineer, Role = Role.Dps, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Breach", Cooldown = 70f,
                Desc = "Smash the wall you aim at (up to 220 px): 80 damage and a shove by the wall. In the Expanding arena a free random room opens behind it "
                    + "with one thing inside: vectors, gold, a Strider repair, a workbench (+25% Strider damage this wave) or an ambush. No wall in reach: the blast and the loot land at your cursor.",
                Capstone = "The blast is twice as wide.",
            },
        };

        /// The Vector Forge's rivets (the Strider is kept up by TickEngineer).
        void FireEngineerWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            switch (w.Id)
            {
                case WeaponId.VectorForge:
                    for (int i = 0; i <= extra; i++)
                        SpawnBullet(Defs.DpsShot, muzzle, V2.FromAngle(aim + (i - extra / 2f) * 0.1f) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                case WeaponId.BulwarkForge:
                {
                    int n = 6 + 2 * extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + (i - (n - 1) / 2f) * 0.5f / (n - 1) + Rand(-0.03f, 0.03f)) * w.Speed * Rand(0.9f, 1.05f), 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                }
                case WeaponId.MediDroneForge:
                    for (int i = 0; i <= extra; i++)
                        SpawnBullet(Defs.HealerShot, muzzle, V2.FromAngle(aim + (i - extra / 2f) * 0.1f) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                case WeaponId.IronFist:
                case WeaponId.StasisGun:
                case WeaponId.VectorBlade:
                    break;  // the robot weapons: the summon and the basic attacks are TickBotWeapon's (EngineerBots.cs)
            }
        }

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

        // --- the Strider ---------------------------------------------------------------

        /// The Engineer's walkers: the Strider (Vector Forge) and the heavier Sentinel (Bulwark Forge). Both run TickStrider.
        public static bool IsWalker(MinionKind k) => k == MinionKind.Strider || k == MinionKind.Sentinel;

        static MinionKind WalkerFor(WeaponId w) => w == WeaponId.VectorForge ? MinionKind.Strider : w == WeaponId.BulwarkForge ? MinionKind.Sentinel : MinionKind.None;

        /// The player's walker, or null.
        Minion StriderOf(Player p)
        {
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && IsWalker(m.Kind))
                    return m;
            return null;
        }

        public static float StriderMaxHp(int wave, MinionKind k = MinionKind.Strider) => (k == MinionKind.Sentinel ? SentinelHp : StriderHp) * (1f + StriderHpPerWave * Math.Max(0, wave - 1));

        /// Host, every tick: each Engineer's Strider is kept up (rebuilt after it falls), healed between waves; the pylons slow.
        void TickEngineer(float dt)
        {
            foreach (var p in Players.Values)
            {
                KeepBots(p, dt);  // the robots (EngineerBots.cs)
                if (p.Weapon.Id != WeaponId.StasisGun || p.Downed || !p.Firing)
                    EndStasis(p);
                var s = StriderOf(p);
                var kind = WalkerFor(p.Weapon.Id);
                if (s != null && s.Kind != kind)
                {
                    Minions.Remove(s.Id);  // re-armed: the old walker goes
                    s = null;
                }
                if (kind == MinionKind.None)
                {
                    p.StriderCd = 0.6f;
                    continue;
                }
                if (s != null)
                {
                    float max = StriderMaxHp(Wave, kind) * p.Stats.StriderHpMult;
                    if (max > s.MaxHp)
                        s.Hp += max - s.MaxHp;  // a bigger frame (a new wave, Reinforced Frame) comes with the health
                    s.MaxHp = Math.Max(s.MaxHp, max);
                    if (!Battle)
                        s.Hp = s.MaxHp;  // patched up between waves
                    RepairStrider(p, s, dt);
                    continue;
                }
                p.StriderCd -= dt;
                if (p.StriderCd > 0f)
                    continue;
                s = SpawnMinion(kind, p, p.Pos + V2.FromAngle(p.Aim + MathX.Pi * 0.75f) * 50f, -1f, true);
                s.MaxHp = s.Hp = StriderMaxHp(Wave, kind) * p.Stats.StriderHpMult;
                s.SlamT = 1f;
                BroadcastFx(FxKind.Ring, s.Pos, s.Def.Color, 40f, StriderBuild, Sfx.Summon);
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

        /// Draftsman's repair (the Expanding arena): standing within RepairReach of your hurt Strider spends a vector every
        /// RepairEvery seconds for RepairHp health.
        void RepairStrider(Player p, Minion s, float dt)
        {
            p.RepairT -= dt;
            if (!ExpandingOn || p.Downed || p.Vectors <= 0 || s.Hp >= s.MaxHp || p.Pos.FartherThan(s.Pos, RepairReach + s.Def.Radius) || p.RepairT > 0f)
                return;
            p.RepairT = RepairEvery;
            p.Vectors--;
            s.Hp = Math.Min(s.MaxHp, s.Hp + RepairHp);
            BroadcastFx(FxKind.Spark, s.Pos, s.Def.Color, 18f, 0.3f, Sfx.None);
        }

        /// Host, from TickMinions: the Strider walks to its target (its lock, else the enemy nearest you or its post), punches it,
        /// and slams when enemies crowd it. Replicated for the look: Aim, Wind (the punch), Stacks (0 hunting, 1 locked on, 2 posted),
        /// LinkTo (the locked enemy's id).
        void TickStrider(Minion m, Player owner, float dt, float speed, float dmg)
        {
            var d = m.Def;
            m.SentT += dt;
            if (m.SentT < StriderBuild)
            {
                m.Wind = 2f + m.SentT / StriderBuild;  // building: its lines draw in (Wind 2-3 tells the look; it does nothing yet)
                return;
            }
            m.Wind = m.Wind >= 2f ? 0f : Math.Max(0f, m.Wind - dt * 5f);
            Enemy target = null;
            if (m.GoalEnemy != 0)
            {
                Enemy locked;
                if (Enemies.TryGetValue(m.GoalEnemy, out locked) && Targetable(locked))
                    target = locked;
                else
                    m.GoalEnemy = 0;  // it died (or left): back to hunting
            }
            bool isLocked = target != null;
            if (target == null && Battle)
            {
                V2 anchor = m.Sent ? m.Goal : owner.Pos;
                float leash = m.Sent ? StriderGuardRadius : StriderHuntLeash;
                foreach (var e in Enemies.Values)
                    if (Targetable(e) && e.Pos.CloserThan(anchor, leash + e.Radius) && (target == null || e.Pos.DistanceSq(m.Pos) < target.Pos.DistanceSq(m.Pos)))
                        target = e;
            }
            float mult = (isLocked ? StriderLockMult : 1f) * (m.Hold > 0f ? StriderBenchMult : 1f);
            V2 goal = target != null ? target.Pos : m.Sent ? m.Goal : owner.Pos + V2.FromAngle(owner.Aim + MathX.Pi * 0.75f) * 50f;
            V2 to = goal - m.Pos;
            float stop = target != null ? target.Radius + d.Radius : 6f;
            float sp = speed * (isLocked ? StriderLockSpeed : 1f);
            if (to.Length > stop)
                m.Pos += to.Normalized() * Math.Min(sp * dt, to.Length - stop);
            m.Pos = ClampToArena(m.Pos, d.Radius);
            if (to.Length > 1f)
                m.Aim = to.Angle;
            if (target != null && m.Cd <= 0f && target.Pos.CloserThan(m.Pos, d.Radius + target.Radius + 12f))
            {
                m.Cd = d.Interval;
                m.Wind = 1f;
                HurtEnemy(target, dmg * mult, m.Owner);
                Sounds.Add(Sfx.Hit);
            }
            m.Cd -= dt * (owner.Stats.StriderRateMult - 1f);  // Hydraulic Fists (the loop already counted the fire rate)
            m.SlamT -= dt * owner.Stats.StriderRateMult;
            if (m.SlamT <= 0f && Battle)
            {
                bool crowd = false;
                foreach (var e in Enemies.Values)
                    if (Targetable(e) && e.Pos.CloserThan(m.Pos, d.Range + e.Radius))
                    {
                        crowd = true;
                        break;
                    }
                if (crowd)
                {
                    m.SlamT = StriderSlamEvery;
                    foreach (var e in Enemies.Values)
                    {
                        if (!Targetable(e) || e.Pos.FartherThan(m.Pos, d.Range + e.Radius))
                            continue;
                        HurtEnemy(e, (m.Kind == MinionKind.Sentinel ? SentinelSlamDmg : StriderSlamDmg) * dmg / Math.Max(1f, d.Damage) * mult, m.Owner);
                        if (!Heavy(e))
                        {
                            e.FrozenT = Math.Max(e.FrozenT, StriderSlamStun);
                            V2 away = e.Pos - m.Pos;
                            KnockEnemy(e, (away.Length < 1f ? V2.FromAngle(m.Aim) : away.Normalized()) * StriderSlamKnock);
                        }
                    }
                    BroadcastFx(FxKind.Ring, m.Pos, d.Color, d.Range, 0.45f, Sfx.Shockwave);
                }
            }
            if (!Battle)
                m.Hold = 0f;  // the workbench lasts the wave
            m.Stacks = isLocked ? 1 : m.Sent ? 2 : 0;
            m.LinkTo = isLocked ? target.Id : 0;
        }

        /// Where an enemy goes (EnemyAI): an enemy close to a Strider (or a robot, EngineerBots.cs) goes for it instead.
        V2 StriderTarget(Enemy e, V2 tpos)
        {
            Minion best = null;
            float bestD = float.MaxValue;
            foreach (var m in Minions.Values)
                if (IsWalker(m.Kind) || IsBot(m.Kind) && m.SentT >= BotBuild)
                {
                    float dd = m.Pos.DistanceSq(e.Pos), taunt = IsBot(m.Kind) ? BotTaunt(m.Kind) : m.Kind == MinionKind.Sentinel ? SentinelTaunt : StriderTaunt;
                    if (dd < taunt * taunt && dd < bestD && dd < tpos.DistanceSq(e.Pos))
                    {
                        bestD = dd;
                        best = m;
                    }
                }
            return best != null ? best.Pos : tpos;
        }

        float striderLocalCd;

        /// Your game: the weapon-action key with the Vector Forge orders the Strider. True when the key belongs to it.
        bool LocalStriderOrder(Player me, bool pressed, V2 aimAt, float dt)
        {
            striderLocalCd -= dt;
            if (WalkerFor(me.Weapon.Id) == MinionKind.None)
                return false;
            if (pressed && striderLocalCd <= 0f)
            {
                striderLocalCd = 0.15f;
                if (IsHost)
                    HostWeaponDash(MyId, DashStrider, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(DashStrider);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        /// Host: Alt with the Vector Forge at `at`: lock onto the enemy there, else call it back (near you) or post it there.
        void HostStriderOrder(Player p, V2 at)
        {
            var m = StriderOf(p);
            if (m == null || WalkerFor(p.Weapon.Id) == MinionKind.None)
                return;
            Enemy pick = null;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.CloserThan(at, StriderPick + e.Radius) && (pick == null || e.Pos.DistanceSq(at) < pick.Pos.DistanceSq(at)))
                    pick = e;
            if (pick != null)
            {
                m.GoalEnemy = pick.Id;
                m.Sent = false;
                BroadcastFx(FxKind.Target, pick.Pos, m.Def.Color, pick.Radius + 12f, 0.5f, Sfx.Pick);
                return;
            }
            m.GoalEnemy = 0;
            if (at.CloserThan(p.Pos, StriderPick))
            {
                m.Sent = false;  // back at your side
                BroadcastFx(FxKind.Ring, p.Pos, m.Def.Color, 30f, 0.3f, Sfx.Pick);
                return;
            }
            m.Sent = true;
            m.Goal = ClampToArena(at, m.Def.Radius);
            BroadcastFx(FxKind.Ring, m.Goal, m.Def.Color, StriderGuardRadius, 0.5f, Sfx.Pick);
        }

        /// A Strider fell (HurtGuard): its owner's next one is rebuilt after StriderRebuild.
        void StriderFallen(Minion m)
        {
            Player owner;
            if (Players.TryGetValue(m.Owner, out owner))
                owner.StriderCd = m.Kind == MinionKind.Sentinel ? Math.Max(RebuildMin, SentinelRebuild - owner.Stats.StriderRebuildCut) : StriderRebuildTime(owner);
            BroadcastFx(FxKind.Boom, m.Pos, m.Def.Color, 50f, 0.5f, Sfx.Explode);
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
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(hit, blast + e.Radius))
                    continue;
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

        /// The one thing behind the wall: 0 vectors, 1 gold, 2 a Strider repair, 3 a workbench, 4 an ambush (only in a real room;
        /// vectors only in the Expanding arena).
        int RollBreachLoot(bool room)
        {
            int roll = rng.Next(100);
            int kind = roll < 30 ? 0 : roll < 55 ? 1 : roll < 70 ? 2 : roll < 85 ? 3 : 4;
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

        /// The loot itself, for `p` (who took it), at `at`. Repairs and the workbench go to every Strider.
        void ApplyBreachLoot(Player p, V2 at, int kind)
        {
            switch (kind)
            {
                case 0:
                    DropVectorAt(at, BreachVectors);
                    break;
                case 1:
                    if (ExpandingOn)
                        for (int i = 0; i < BreachGold / 5; i++)
                        {
                            var c = new Coin { Id = nextCoinId++, Value = 5 };
                            c.Pos = c.Disp = ClampToArena(at + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * (8f + 18f * (float)rng.NextDouble()), 12f);
                            Coins[c.Id] = c;
                        }
                    else
                        foreach (var q in Players.Values)
                            q.Gold += BreachGold;
                    break;
                case 2:
                    foreach (var m in Minions.Values)
                        if (IsWalker(m.Kind))
                            m.Hp = m.MaxHp;
                    SpawnOrb(at, true);
                    break;
                case 3:
                    foreach (var m in Minions.Values)
                        if (IsWalker(m.Kind))
                            m.Hold = 1f;  // +25% Strider damage until the wave ends (TickStrider)
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
