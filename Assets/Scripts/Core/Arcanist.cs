using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Arcanist (Docs/Arcanist.md): telekinesis, runes and enchantments. One weapon, 1st ability and ultimate per role so far
    /// (WeaponDef.FillsGaps / AbilityDef.FillsGaps stand in for its other lobby places, like the Engineer's).
    ///   Kinetic Field (passive): enemy shots within FieldRadius of an Arcanist fly at FieldSlow speed (every game, ArcShotSpeed).
    ///   Tank: Orbit Shards (MinionKind.ArcShard companions circle you cutting small shots; Fire flings one at the cursor and back;
    ///     Alt held: Shard Guard, they pull in tight and stop every blockable shot, you move slower), Kinetic Grasp (1st: lift the
    ///     biggest enemy near the cursor and hold it in front of you as a shield; press again, or wait, to throw it), Grand Sigil
    ///     (ultimate: a rune circle that binds and taunts enemies inside and freezes enemy shots at its rim; as it ends they fly
    ///     back out as spectral blades).
    ///   Healer: Glyph Stylus (rune bolts through enemies that heal the allies they pass), Arcane Tether (1st: pull the ally nearest
    ///     your cursor to your side, untouchable on the way, healed), Sanctum Sigil (ultimate: a healing circle that follows you, six
    ///     runes on its rim each bursting on the first enemy that touches it).
    ///   DPS: Dancing Blades (MinionKind.ArcBlade companions hover behind you; Fire launches one to slash at the cursor, Alt recalls
    ///     them all), Telekinetic Crush (1st: lift the enemies at the cursor, pull them together, slam), Runic Array (ultimate:
    ///     four sigils behind you; every Fire also sends a spectral blade from each).
    public partial class Game
    {
        internal static readonly KitDef ArcanistKit = new KitDef
        {
            Id = Kit.Arcanist, Name = "Arcanist", Icon = "class_arcanist", HpMult = 0.9f, SpeedMult = 1f,
            Blurb = "A mage of telekinesis, runes and enchantments: shards and blades that fly at your will, enemies lifted and thrown, sigils on the floor. Kinetic Field: enemy shots slow down near you. One weapon, ability and ultimate per role for now. 90% health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireArcanist(p, aim, muzzle, dmg, extra),
            Ability = (g, p, id, target) => g.ArcanistAbility(p, id, target),
            Tick = (g, dt) => g.TickArcanist(dt),
        };

        // --- Numbers ---
        public const float FieldRadius = 90f, FieldSlow = 0.75f;                     // Kinetic Field
        public const float OsOrbit = 44f, OsGuardOrbit = 30f, OsSpin = 2.6f, OsGuardSpin = 6f, ShardCut = 11f;
        public const float ShardOut = 800f, ShardBack = 900f, ShardReach = 360f, ShardHitR = 12f;
        public const float ShardGuardSlow = 0.55f, ShardGuardR = 46f;                // Shard Guard: your speed; the ring that stops shots
        public const float BladeOut = 950f, BladeBack = 1000f, BladeReach = 380f, BladeHover = 30f, BladeSlashR = 42f;
        public const float BladeSlashTime = 1f, BladeSlashEvery = 0.25f, BladePassMult = 0.5f, BladePick = 70f;
        public const float GlyphHeal = 7f;                                           // Glyph Stylus: heal per bolt through an ally
        public const float GraspPick = 150f, GraspRange = 450f, KgHoldTime = 4f, GraspCarry = 700f, GraspSoak = 10f;
        public const float GraspThrow = 700f, GraspThrowRange = 380f, GraspHit = 50f, GraspKnock = 50f, GraspStun = 0.6f;
        public const float SigilRadius = 200f, SigilTime = 8f, SigilBand = 24f, SigilBind = 1f, SigilSlow = 0.5f, SigilBossSlow = 0.8f;
        public const float SigilShotSpeed = 650f, SigilShotLife = 0.7f, SigilShotDmg = 14f;
        public const int SigilShotMax = 80;
        public const float TetherRange = 480f, TetherHeal = 30f;
        public const float SanctumRadius = 170f, SanctumTime = 8f, SanctumRuneR = 22f, SanctumBurst = 70f, SanctumBurstDmg = 50f, SanctumSpin = 0.9f;
        public const int SanctumRunes = 6;
        public const float CrushRange = 400f, CrushRadius = 120f, CrushTime = 0.5f, CrushPull = 420f, CrushDmg = 40f, CrushPer = 12f, CrushStun = 0.4f;
        public const int CrushPerMax = 5;
        public const float ArrayTime = 6f, ArrayShotSpeed = 900f, ArrayShotLife = 0.5f, ArrayShotDmg = 16f, ArraySigilR = 38f;

        internal static readonly WeaponDef[] ArcanistWeapons =
        {
            new WeaponDef { Id = WeaponId.OrbitShards, Range = (g, p, w, x) => ShardReach * PierceReach(p), Kit = Kit.Arcanist, FillsGaps = true, Role = Role.Tank, Choice = 0, Speed = ShardOut, Life = ShardReach / ShardOut,
                Damage = 18f, FireInterval = 0.3f, BotRange = 260f, Companion = MinionKind.ArcShard, CompanionCount = 4, Name = "Orbit Shards", Sound = Sfx.SpellArcane,
                Desc = "Primary Fire: Fling one of four orbiting shards that cuts everything it passes, out and back.\n\nAlternative Fire: Pull the shards in tight to stop every bullet, but you move slower.",
                ExtraDesc = "+1 shard, and one more flung with each click" },
            new WeaponDef { Id = WeaponId.GlyphStylus, Kit = Kit.Arcanist, FillsGaps = true, Role = Role.Healer, Choice = 0, Speed = 650f, Life = 0.7f, Damage = 10f,
                FireInterval = 0.26f, BotRange = 330f, Name = "Glyph Stylus", Sound = Sfx.SpellArcane,
                Desc = "Primary Fire: Writes rune bolts that pierce two enemies and heal an ally they pass through.",
                ExtraDesc = "+1 bolt, fanned out" },
            new WeaponDef { Id = WeaponId.DancingBlades, Range = (g, p, w, x) => BladeReach * PierceReach(p), Kit = Kit.Arcanist, FillsGaps = true, Role = Role.Dps, Choice = 0, Speed = BladeOut, Life = BladeReach / BladeOut,
                Damage = 10f, FireInterval = 0.3f, BotRange = 300f, Companion = MinionKind.ArcBlade, CompanionCount = 3, Name = "Dancing Blades", Sound = Sfx.SwingLight,
                Desc = "Primary Fire: Launch a floating sword that cuts, dances on the enemy, then returns.\n\nAlternative Fire: Call every sword home at once, cutting on the way.",
                ExtraDesc = "+1 blade" },
        };

        internal static readonly AbilityDef[] ArcanistAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.KineticGrasp, Kit = Kit.Arcanist, Role = Role.Tank, Choice = 0, FillsGaps = true,
                Name = "Kinetic Grasp", Icon = "kineticgrasp", Cooldown = 10f,
                Desc = "Lift the biggest nearby enemy as a shield, then throw it.",
                Capstone = "The throw hits 50% harder and stuns for twice as long.",
                Bot = b => b.HaveTarget && b.TargetD < GraspRange,
            },
            new AbilityDef
            {
                Id = AbilityId.GrandSigil, Kit = Kit.Arcanist, Role = Role.Tank, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Grand Sigil", Cooldown = 75f,
                Desc = "A rune circle around you binds and slows enemies and freezes their bullets, then fires them back.",
                Capstone = "It lasts 12 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.ArcaneTether, Kit = Kit.Arcanist, Role = Role.Healer, Choice = 0, FillsGaps = true,
                Name = "Arcane Tether", Icon = "arcanetether", Cooldown = 12f,
                Desc = "Pull the ally nearest your cursor to your side, safe and healed.",
                Capstone = "It heals twice as much.",
                Bot = b => b.AllyDown || b.AllyThreatened,
            },
            new AbilityDef
            {
                Id = AbilityId.SanctumSigil, Kit = Kit.Arcanist, Role = Role.Healer, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Sanctum Sigil", Cooldown = 80f,
                Desc = "A healing circle follows you and its runes burst on enemies.",
                Capstone = "It lasts 12 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.TelekineticCrush, Kit = Kit.Arcanist, Role = Role.Dps, Choice = 0, FillsGaps = true,
                Name = "Telekinetic Crush", Icon = "telekineticcrush", Cooldown = 9f,
                Desc = "Lift enemies at your cursor, pull them together and slam them.",
                Capstone = "It grabs 40% wider.",
                Bot = b => b.HaveTarget && b.TargetD < CrushRange,
            },
            new AbilityDef
            {
                Id = AbilityId.RunicArray, Kit = Kit.Arcanist, Role = Role.Dps, Choice = AbilityDef.Ult, FillsGaps = true,
                Name = "Runic Array", Cooldown = 65f,
                Desc = "Four sigils behind you fire a spectral blade with every shot.",
                Capstone = "It lasts 9 s.",
            },
        };

        // --- Firing ---------------------------------------------------------------------

        void FireArcanist(Player p, float aim, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            switch (w.Id)
            {
                case WeaponId.OrbitShards:
                    if (!p.ShardGuard)
                        for (int i = 0; i <= extra; i++)
                            SendArcane(p, MinionKind.ArcShard, ShardReach);  // one a click, plus one per extra projectile
                    break;
                case WeaponId.GlyphStylus:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        var b = SpawnBullet(Defs.GlyphShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.1f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.Pierce += 2;
                        b.HealAlly = GlyphHeal * 9f / w.Damage;  // HealAllyHit scales it by Dmg / 9 (so damage upgrades heal more too)
                    }
                    break;
                }
                case WeaponId.DancingBlades:
                    SendArcane(p, MinionKind.ArcBlade, BladeReach);
                    if (p.ArrayT > 0f)
                        ArrayVolley(p, dmg);
                    break;
            }
        }

        /// A waiting shard or blade (the one most in front of you) goes out at your cursor.
        void SendArcane(Player p, MinionKind kind, float reach)
        {
            Minion pick = null;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == kind && m.Companion && !m.Sent && (pick == null || AngleTo(p.Pos, m.Pos, p.Aim) < AngleTo(p.Pos, pick.Pos, p.Aim)))
                    pick = m;
            if (pick == null)
                return;
            V2 at = ClampToArena(p.Pos + (p.AimAt - p.Pos).LimitLength(reach * PierceReach(p)), 10f);  // Piercing Rounds send it farther
            var e = kind == MinionKind.ArcBlade ? NearestEnemy(at, BladePick, by: p.Id) : null;
            pick.Goal = at;
            pick.GoalEnemy = e != null ? e.Id : 0;
            pick.Sent = true;
            pick.Returning = false;
            pick.SentT = 0f;
            pick.Cd = 0f;
            ArcHits(pick).Clear();
            p.Run.ShotsFired++;
        }

        readonly Dictionary<int, HashSet<int>> arcHits = new Dictionary<int, HashSet<int>>();  // shard or blade -> enemies it cut on this leg

        HashSet<int> ArcHits(Minion m)
        {
            HashSet<int> set;
            if (!arcHits.TryGetValue(m.Id, out set))
                arcHits[m.Id] = set = new HashSet<int>();
            return set;
        }

        /// Where the k-th of Runic Array's four sigils hangs, behind `pos` facing `aim` (the look draws them here too).
        public static V2 ArraySigil(V2 pos, float aim, int k) => pos + V2.FromAngle(aim + MathX.Pi + (k < 2 ? -1f : 1f) * (k % 2 == 0 ? 0.55f : 1.15f)) * ArraySigilR;

        void ArrayVolley(Player p, float dmg)
        {
            for (int k = 0; k < 4; k++)
            {
                V2 from = ArraySigil(p.Pos, p.Aim, k), to = p.AimAt - from;
                var b = SpawnBullet(Defs.SpectralBlade, from, (to.Length > 1f ? to.Normalized() : V2.FromAngle(p.Aim)) * ArrayShotSpeed, 0f, ArrayShotLife, ArrayShotDmg * dmg, p.Id);
                b.Pierce += 1;
            }
        }

        // --- The shards and blades (from TickMinions) --------------------------------------

        /// The i-th of a player's companions of `kind` (by id) and how many there are.
        void ArcSlot(Minion m, out int i, out int n)
        {
            i = 0;
            n = 0;
            foreach (var o in Minions.Values)
                if (o.Owner == m.Owner && o.Kind == m.Kind && o.Companion)
                {
                    n++;
                    if (o.Id < m.Id)
                        i++;
                }
        }

        /// Host, each tick for an Orbit Shard or Dancing Blade (instead of the usual moves).
        void MoveArcane(Minion m, Player owner, float dt)
        {
            bool blade = m.Kind == MinionKind.ArcBlade;
            int i, n;
            ArcSlot(m, out i, out n);
            V2 slot;
            if (blade)
                slot = owner.Pos + V2.FromAngle(owner.Aim + MathX.Pi + (i - (n - 1) / 2f) * 0.55f) * BladeHover;
            else
            {
                bool guard = owner.ShardGuard;
                m.Ang = arcClock * (guard ? OsGuardSpin : OsSpin) + MathX.Tau * i / Math.Max(1, n);
                slot = owner.Pos + V2.FromAngle(m.Ang) * (guard ? OsGuardOrbit : OsOrbit);
            }
            if (owner.Downed && m.Sent)
                m.Returning = true;
            if (!m.Sent)
            {
                m.Pos += (slot - m.Pos).LimitLength(1400f * dt);
                m.Aim = blade ? owner.Aim : m.Ang + MathX.Pi / 2f;
                m.Wind = 0f;
                if (!blade && !owner.Downed)
                    CutSmallBullets(m.Pos, ShardCut);  // an orbiting shard cuts small shots
                return;
            }
            m.SentT += dt;
            var hits = ArcHits(m);
            var w = owner.Weapon;
            float dmg = w.Damage * owner.Stats.DmgMult * HomingCost(owner);
            if (m.Returning)
            {
                V2 back = slot - m.Pos;
                float step = (blade ? BladeBack : ShardBack) * dt;
                m.Aim = back.Angle;
                m.Wind = 0.5f;
                if (back.Length <= step + 4f)
                {
                    m.Pos = slot;
                    m.Sent = m.Returning = false;
                    hits.Clear();
                    return;
                }
                m.Pos += back.Normalized() * step;
                ArcCut(m, owner, hits, blade ? dmg * BladePassMult : dmg, blade ? BladeSlashR * 0.6f : ShardHitR);
                return;
            }
            Enemy prey = null;
            if (blade && m.GoalEnemy != 0 && (!TryFoe(m.GoalEnemy, out prey) || !Targetable(prey)))
            {
                prey = null;
                m.GoalEnemy = 0;
            }
            if (blade && m.Cd > 0f)  // dancing on the spot (on its enemy, if it still has one)
            {
                if (prey != null)
                    m.Goal = prey.Pos;
                m.Pos += (m.Goal - m.Pos).LimitLength(BladeOut * dt);
                m.Wind = 1f;
                m.Aim += 14f * dt;
                m.Cd -= dt;
                if (Math.Floor((m.Cd + dt) / BladeSlashEvery) != Math.Floor(m.Cd / BladeSlashEvery) || m.Cd <= 0f)
                    BladeSlash(m, owner, dmg);
                if (m.Cd <= 0f)
                {
                    m.Returning = true;
                    hits.Clear();
                }
                return;
            }
            V2 goal = prey != null ? prey.Pos : m.Goal;
            V2 to = goal - m.Pos;
            float speed = blade ? BladeOut : ShardOut;
            m.Aim = to.Angle;
            m.Wind = 0.5f;
            bool arrived = to.Length <= speed * dt + (prey != null ? prey.Radius : 2f) || m.SentT > 1.5f;
            m.Pos += to.LimitLength(speed * dt);
            ArcCut(m, owner, hits, blade ? dmg * BladePassMult : dmg, blade ? BladeSlashR * 0.6f : ShardHitR);
            if (!arrived)
                return;
            if (blade)
            {
                m.Cd = BladeSlashTime;  // start dancing: a slash every BladeSlashEvery, the last as it ends
                m.Goal = goal;
            }
            else
            {
                m.Returning = true;
                hits.Clear();  // the way back cuts everything again
            }
        }

        /// A shard or blade in flight cuts each enemy it passes once a leg (the weapon's hit: its mods ride it).
        void ArcCut(Minion m, Player owner, HashSet<int> hits, float dmg, float reach)
        {
            if (owner.Downed)
                return;
            V2 dir = V2.FromAngle(m.Aim);
            foreach (var e in AllFoes(by: owner.Id))
                if (!hits.Contains(e.Id) && e.Pos.CloserThan(m.Pos, e.Radius + reach))
                {
                    hits.Add(e.Id);
                    WeaponHurt(e, dmg, owner.Id);
                    ModHit(e, owner.Id, dmg, Defs.ArcaneShot, dir);
                    owner.Run.ShotsHit++;
                    BroadcastFx(FxKind.Spark, e.Pos, m.Def.Color, e.Radius + 8f, 0.2f, Sfx.None);
                }
        }

        /// A dancing blade's slash: everything within BladeSlashR.
        void BladeSlash(Minion m, Player owner, float dmg)
        {
            if (owner.Downed)
                return;
            bool any = false;
            foreach (var e in FoesInCircle(m.Pos, BladeSlashR, by: owner.Id))
            {
                WeaponHurt(e, dmg, owner.Id);
                ModHit(e, owner.Id, dmg, Defs.ArcaneShot, (e.Pos - m.Pos).Normalized());
                any = true;
            }
            if (any)
                owner.Run.ShotsHit++;
            float a = m.Aim;
            BroadcastFx(FxKind.Slash, m.Pos, m.Pos + V2.FromAngle(a) * BladeSlashR, m.Def.Color, 1.1f, 0.18f, any ? Sfx.SwingLight : Sfx.None);
        }

        // --- Alt: Shard Guard and the blades' recall (Msg.WeaponDash) ------------------------

        /// Your own game: the Orbit Shards' Alt is held (both edges go to the host); the Dancing Blades' is a tap.
        bool LocalArcanistAlt(Player me, bool pressed, bool released, V2 aimAt)
        {
            byte kind;
            if (me.Weapon.Id == WeaponId.OrbitShards)
            {
                if (!pressed && !released)
                    return true;
                kind = pressed ? WeaponAlt.ShardOn : WeaponAlt.ShardOff;
            }
            else if (me.Weapon.Id == WeaponId.DancingBlades)
            {
                if (!pressed)
                    return true;
                kind = WeaponAlt.BladeRecall;
            }
            else
                return false;
            if (IsHost)
                HostWeaponDash(MyId, kind, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(kind);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// From HostWeaponDash: the Arcanist's kinds.
        bool HostArcanistDash(Player p, int kind)
        {
            if (kind == WeaponAlt.ShardOn || kind == WeaponAlt.ShardOff)
            {
                p.ShardGuard = kind == WeaponAlt.ShardOn && p.Weapon.Id == WeaponId.OrbitShards && !p.Downed;
                if (p.ShardGuard)
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(MinionKind.ArcShard).Color, ShardGuardR, 0.25f, Sfx.SpellArcane);
                return true;
            }
            if (kind == WeaponAlt.BladeRecall)
            {
                bool any = false;
                foreach (var m in Minions.Values)
                    if (m.Owner == p.Id && m.Kind == MinionKind.ArcBlade && m.Sent && !m.Returning)
                    {
                        m.Returning = true;
                        m.Cd = 0f;
                        ArcHits(m).Clear();
                        any = true;
                    }
                if (any)
                    BroadcastSound(Sfx.SwingLight);
                return true;
            }
            return false;
        }

        // --- Abilities ----------------------------------------------------------------------

        bool ArcanistAbility(Player p, AbilityId id, V2 target)
        {
            switch (id)
            {
                case AbilityId.KineticGrasp:
                    CastGrasp(p, target);
                    return true;
                case AbilityId.GrandSigil:
                    CastGrandSigil(p);
                    return true;
                case AbilityId.ArcaneTether:
                    CastTether(p, target);
                    return true;
                case AbilityId.SanctumSigil:
                {
                    var z = AddZone(ZoneKind.SanctumSigil, p, p.Pos, SanctumRadius, castMastered ? 12f : SanctumTime);
                    z.Follow = true;
                    z.Aim = (1 << SanctumRunes) - 1;  // every rune up
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, SanctumRadius, 0.5f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.TelekineticCrush:
                {
                    V2 at = ClampToArena(p.Pos + (target - p.Pos).LimitLength(CrushRange), 10f);
                    AddZone(ZoneKind.ArcCrush, p, at, CrushRadius * (castMastered ? 1.4f : 1f), CrushTime);
                    BroadcastFx(FxKind.Implode, at, Kits.Get(ZoneKind.ArcCrush).Color, CrushRadius, CrushTime, Sfx.SpellArcane);
                    return true;
                }
                case AbilityId.RunicArray:
                    p.ArrayT = castMastered ? 9f : ArrayTime;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.78f, 0.62f, 1f), ArraySigilR + 20f, 0.4f, Sfx.SpellArcane);
                    return true;
            }
            return false;
        }

        // Kinetic Grasp

        class GraspRun { public int Owner, Target; public float Power = 1f; public bool Mastered, Flying; public V2 Dir; public float Flown; }
        readonly List<GraspRun> graspRuns = new List<GraspRun>();

        /// Kinetic Grasp pressed again while it holds an enemy: throw it (no cooldown spent).
        public bool GraspRelease(Player p, int slot) => p.KgHeld != 0 && p.Slots[slot].Id == AbilityId.KineticGrasp;

        void CastGrasp(Player p, V2 target)
        {
            Enemy pick = null;
            foreach (var e in AllFoes(by: p.Id))
                if (Targetable(e) && !e.Def.IsBoss && e.Pos.CloserThan(target, GraspPick + e.Radius) && e.Pos.CloserThan(p.Pos, GraspRange + e.Radius)
                    && (pick == null || e.MaxHp > pick.MaxHp))
                    pick = e;
            if (pick == null)
            {
                CutCooldown(p, AbilityId.KineticGrasp, 0.8f);  // nothing to lift: most of it back
                BroadcastFx(FxKind.Ring, target, Kits.Get(MinionKind.ArcShard).Color, 30f, 0.3f, Sfx.None);
                return;
            }
            graspRuns.RemoveAll(r => r.Owner == p.Id && !r.Flying);
            graspRuns.Add(new GraspRun { Owner = p.Id, Target = pick.Id, Power = castPower, Mastered = castMastered });
            p.KgHeld = pick.Id;
            p.KgT = 0f;
            BroadcastFx(FxKind.Link, p.Pos, pick.Pos, Kits.Get(MinionKind.ArcShard).Color, 0f, 0.35f, Sfx.SpellArcane);
        }

        /// Throw what Kinetic Grasp holds, along your aim.
        void ThrowGrasp(Player p)
        {
            foreach (var r in graspRuns)
                if (r.Owner == p.Id && !r.Flying)
                {
                    r.Flying = true;
                    r.Dir = V2.FromAngle(p.Aim);
                    BroadcastSound(Sfx.Throw);
                }
            p.KgHeld = 0;
        }

        public static V2 GraspHoldPoint(Player p, float enemyR) => p.Pos + V2.FromAngle(p.Aim) * (p.Def.BodyR + enemyR + 14f);

        void TickGrasps(float dt)
        {
            for (int i = graspRuns.Count - 1; i >= 0; i--)
            {
                var r = graspRuns[i];
                Player p;
                Enemy e;
                bool ownerOk = Players.TryGetValue(r.Owner, out p);
                if (!ownerOk || !TryFoe(r.Target, out e) || e.Hp <= 0f || (!r.Flying && p.Downed))
                {
                    if (ownerOk && !r.Flying)
                        p.KgHeld = 0;
                    graspRuns.RemoveAt(i);
                    continue;
                }
                Stun(e, 0.2f);  // held or flying: it can't move or shoot
                e.KnockLeft = V2.Zero;
                if (!r.Flying)
                {
                    p.KgT += dt;
                    V2 hold = GraspHoldPoint(p, e.Radius);
                    KnuckleCarry(e, e.Pos + (hold - e.Pos).LimitLength(GraspCarry * dt));
                    graspCut.Clear();
                    foreach (var b in Bullets.Values)  // the held enemy soaks the bullets that reach it
                        if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.CloserThan(e.Pos, e.Radius + b.Radius + 4f))
                            graspCut.Add(b.Id);
                    foreach (int id in graspCut)
                    {
                        KillBullet(id);
                        HurtEnemy(e, GraspSoak * p.Stats.DmgMult, p.Id);
                    }
                    if (p.KgT >= KgHoldTime)
                        ThrowGrasp(p);
                    continue;
                }
                float step = GraspThrow * dt;
                r.Flown += step;
                bool wall = KnuckleCarry(e, e.Pos + r.Dir * step);
                Enemy into = null;
                foreach (var o in AllFoes(by: p.Id))
                    if (o != e && Targetable(o) && o.Pos.WithinReach(e.Pos, o.Radius + e.Radius))
                    {
                        into = o;
                        break;
                    }
                if (into == null && !wall && r.Flown < GraspThrowRange)
                    continue;
                float dmg = GraspHit * p.Stats.DmgMult * r.Power * (r.Mastered ? 1.5f : 1f);
                HurtEnemy(e, dmg, p.Id);
                if (into != null)
                {
                    HurtEnemy(into, dmg, p.Id);
                    if (!Heavy(into))
                        KnockEnemy(into, r.Dir * GraspKnock);
                    if (!into.Def.IsBoss)
                        Stun(into, GraspStun * (r.Mastered ? 2f : 1f));
                }
                e.FrozenT = 0f;
                e.StunT = 0f;
                Stun(e, GraspStun * (r.Mastered ? 2f : 1f));
                BroadcastFx(FxKind.Boom, e.Pos, Kits.Get(MinionKind.ArcShard).Color, e.Radius + 26f, 0.3f, Sfx.Hit);
                graspRuns.RemoveAt(i);
            }
        }

        readonly List<int> graspCut = new List<int>();

        // Grand Sigil

        void CastGrandSigil(Player p)
        {
            var z = AddZone(ZoneKind.GrandSigil, p, p.Pos, SigilRadius, castMastered ? 12f : SigilTime);
            foreach (var e in AllFoes(by: p.Id))
                if (Targetable(e) && !e.Def.IsBoss && e.Pos.CloserThan(z.Pos, z.Radius + e.Radius))
                    Hold(e, SigilBind);  // bound
            BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, SigilRadius, 0.6f, Sfx.Shockwave);
        }

        /// Is `at` on a Grand Sigil's rim (where enemy shots freeze)?
        static bool OnSigilRim(Zone z, V2 at)
        {
            float d = at.DistanceTo(z.Pos);
            return d >= z.Radius - SigilBand && d <= z.Radius + 8f;
        }

        /// TickZones, as a Grand Sigil ends: its frozen shots fly back out as spectral blades.
        void ReleaseSigil(Zone z)
        {
            Player owner;
            if (!Players.TryGetValue(z.Owner, out owner))
                return;
            graspCut.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && OnSigilRim(z, b.Pos) && graspCut.Count < SigilShotMax)
                    graspCut.Add(b.Id);
            var spots = new List<V2>();
            foreach (int id in graspCut)
            {
                Bullet b;
                if (Bullets.TryGetValue(id, out b))
                    spots.Add(b.Pos);
                KillBullet(id);
            }
            foreach (var at in spots)
            {
                V2 out1 = at - z.Pos;
                var s = SpawnBullet(Defs.SpectralBlade, at, (out1.Length > 1f ? out1.Normalized() : V2.FromAngle(RandAngle())) * SigilShotSpeed, 0f, SigilShotLife, SigilShotDmg * z.DmgMult, owner.Id);
                s.Pierce += 1;
            }
            BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius + 30f, 0.45f, spots.Count > 0 ? Sfx.Shockwave : Sfx.None);
        }

        // Kinetic Field and the sigil's rim, on every game (MoveBullets)

        readonly List<Zone> arcSigils = new List<Zone>();
        readonly List<Player> arcFields = new List<Player>();

        void PrepareArcBullets()
        {
            arcSigils.Clear();
            arcFields.Clear();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.GrandSigil)
                    arcSigils.Add(z);
            foreach (var p in Players.Values)
                if (p.Kit == Kit.Arcanist && !p.Downed)
                    arcFields.Add(p);
        }

        /// An enemy shot's speed share here: 0 frozen on a Grand Sigil's rim, FieldSlow near an Arcanist (Kinetic Field), else 1.
        float ArcShotSpeed(Bullet b)
        {
            if ((arcSigils.Count == 0 && arcFields.Count == 0) || !Defs.IsEnemyShot(b.Kind))
                return 1f;
            foreach (var z in arcSigils)
                if (OnSigilRim(z, b.Pos))
                    return 0f;
            foreach (var p in arcFields)
                if (b.Pos.CloserThan(p.Pos, FieldRadius))
                    return FieldSlow;
            return 1f;
        }

        // Arcane Tether

        class TetherRun { public int Ally; public V2 Dest; public float T; }
        readonly List<TetherRun> tethers = new List<TetherRun>();

        void CastTether(Player p, V2 target)
        {
            Player ally = null;
            foreach (var q in Players.Values)
                if (q.Id != p.Id && q.Pos.WithinReach(p.Pos, TetherRange + q.Def.BodyR) && (ally == null || q.Pos.DistanceSq(target) < ally.Pos.DistanceSq(target)))
                    ally = q;
            if (ally == null)
            {
                CutCooldown(p, AbilityId.ArcaneTether, 0.8f);  // nobody to pull: most of it back
                return;
            }
            V2 from = ally.Pos - p.Pos;
            V2 dir = from.Length > 1f ? from.Normalized() : V2.FromAngle(p.Aim);
            V2 dest = ClampToArena(p.Pos + dir * (p.Def.BodyR + ally.Def.BodyR + 6f), ally.Def.BodyR);
            V2 path = dest - ally.Pos;
            float dist = path.Length, time = dist / KnockSpeed;
            if (dist > 1f)
            {
                if (ally.Downed)
                    tethers.Add(new TetherRun { Ally = ally.Id, Dest = dest, T = time + 0.1f });  // a downed ally can't be knocked: carried
                else
                    KnockPlayer(ally, path / dist, dist);
            }
            ally.InvulnT = Math.Max(ally.InvulnT, time + 0.4f);
            Heal(ally, TetherHeal * castPower * (castMastered ? 2f : 1f), p);
            BroadcastFx(FxKind.Link, p.Pos, ally.Pos, Kits.Get(ZoneKind.SanctumSigil).Color, 0f, time + 0.25f, Sfx.SpellHoly);
            BroadcastFx(FxKind.Ring, dest, Kits.Get(ZoneKind.SanctumSigil).Color, 36f, 0.35f, Sfx.None);
        }

        // --- Host, every tick ------------------------------------------------------------------

        float arcClock;

        void TickArcanist(float dt)
        {
            arcClock += dt;
            foreach (var p in Players.Values)
            {
                if (p.ArrayT > 0f)
                    p.ArrayT = Math.Max(0f, p.ArrayT - dt);
                if (p.ShardGuard && (p.Weapon.Id != WeaponId.OrbitShards || p.Downed || p.StunT > 0f))
                    p.ShardGuard = false;
                if (p.ShardGuard)
                {
                    graspCut.Clear();
                    foreach (var b in Bullets.Values)  // Shard Guard: every blockable shot that reaches the ring stops
                        if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.CloserThan(p.Pos, ShardGuardR + b.Radius))
                            graspCut.Add(b.Id);
                    foreach (int id in graspCut)
                        KillBullet(id);
                }
            }
            TickGrasps(dt);
            for (int i = tethers.Count - 1; i >= 0; i--)
            {
                var r = tethers[i];
                Player ally;
                r.T -= dt;
                if (!Players.TryGetValue(r.Ally, out ally) || r.T <= 0f)
                {
                    tethers.RemoveAt(i);
                    continue;
                }
                float step = Math.Min(KnockSpeed * dt, ally.Pos.DistanceTo(r.Dest));
                if (step > 0.01f)
                    MovePlayer(ally, ally.Pos + (r.Dest - ally.Pos).Normalized() * step);
            }
            foreach (var z in Zones.Values)
                switch (z.Kind)
                {
                    case ZoneKind.GrandSigil:
                    {
                        Player owner = PlayerOrNull(z.Owner);
                        foreach (var e in AllFoes(by: z.Owner))
                            if (Targetable(e) && e.Pos.CloserThan(z.Pos, z.Radius + e.Radius))
                            {
                                Slow(e, 0.25f, e.Def.IsBoss ? SigilBossSlow : SigilSlow);
                                if (owner != null && !owner.Downed)
                                    TauntEnemy(owner, e, 0.4f);
                            }
                        break;
                    }
                    case ZoneKind.SanctumSigil:
                        TickSanctumRunes(z);
                        break;
                    case ZoneKind.ArcCrush:
                        foreach (var e in AllFoes(by: z.Owner))
                            if (Targetable(e) && !e.Def.IsBoss && e.Pos.CloserThan(z.Pos, z.Radius + e.Radius))
                            {
                                Stun(e, 0.15f);
                                e.KnockLeft = V2.Zero;
                                KnuckleCarry(e, e.Pos + (z.Pos - e.Pos).LimitLength(CrushPull * dt));
                            }
                        break;
                }
        }

        /// Where the k-th of a Sanctum Sigil's runes is (the look draws them here too).
        public static V2 SanctumRune(Zone z, int k) => z.Pos + V2.FromAngle((z.Duration - z.T) * SanctumSpin + MathX.Tau * k / SanctumRunes) * z.Radius;

        void TickSanctumRunes(Zone z)
        {
            int left = (int)z.Aim;
            for (int k = 0; k < SanctumRunes; k++)
            {
                if ((left & (1 << k)) == 0)
                    continue;
                V2 at = SanctumRune(z, k);
                Enemy touch = null;
                foreach (var e in AllFoes(by: z.Owner))
                    if (Targetable(e) && e.Pos.CloserThan(at, e.Radius + SanctumRuneR))
                    {
                        touch = e;
                        break;
                    }
                if (touch == null)
                    continue;
                left &= ~(1 << k);
                foreach (var e in FoesInCircle(at, SanctumBurst, by: z.Owner))
                {
                    HurtEnemy(e, SanctumBurstDmg * z.DmgMult, z.Owner);
                    if (!Heavy(e))
                    {
                        V2 away = e.Pos - at;
                        KnockEnemy(e, (away.Length > 1f ? away.Normalized() : (at - z.Pos).Normalized()) * 60f);
                    }
                }
                BroadcastFx(FxKind.Boom, at, z.Def.Color, SanctumBurst, 0.35f, Sfx.SpellHoly);
            }
            z.Aim = left;
        }

        /// TickZones, as a Telekinetic Crush ends: the slam.
        void CrushSlam(Zone z)
        {
            int caught = 0;
            foreach (var e in AllFoes(by: z.Owner))
                if (Targetable(e) && e.Pos.CloserThan(z.Pos, z.Radius + e.Radius))
                    caught++;
            if (caught == 0)
            {
                BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius * 0.5f, 0.3f, Sfx.None);
                return;
            }
            float dmg = (CrushDmg + CrushPer * Math.Min(CrushPerMax, caught - 1)) * z.DmgMult;
            foreach (var e in FoesInCircle(z.Pos, z.Radius, by: z.Owner))
            {
                HurtEnemy(e, dmg, z.Owner);
                if (!e.Def.IsBoss)
                    Stun(e, CrushStun);
            }
            BroadcastFx(FxKind.Boom, z.Pos, z.Def.Color, z.Radius * 0.6f, 0.4f, Sfx.BigExplode);
        }

        /// TickZones: an Arcanist zone has just ended.
        void ArcZoneEnds(Zone z)
        {
            if (z.Kind == ZoneKind.GrandSigil)
                ReleaseSigil(z);
            else if (z.Kind == ZoneKind.ArcCrush)
                CrushSlam(z);
        }

        // --- Network: the player's Arcanist fields (SyncPlayer) ---------------------------------

        void SyncArcanist(NetSync s, Player p)
        {
            s.Int(ref p.KgHeld);
            s.Bool(ref p.ShardGuard);
            s.Float(ref p.ArrayT);
        }
    }
}
