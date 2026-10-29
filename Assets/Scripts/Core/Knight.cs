using System;
using System.Collections.Generic;

namespace Arena
{
    /// One melee strike, waiting to land (host only). Swings are resolved inside Collide so a hit goes
    /// through the same on-hit pipeline as a bullet: weapon mods, legendaries, crits, lifesteal, kills.
    public class Swing
    {
        public int Owner;
        public float Aim, Reach, HalfArc, Dmg, Knock, Delay;
        public int MaxTargets;        // 0 = everything in the arc; otherwise the nearest few (Rapier)
        public bool CutAll;           // also cuts big orbs and mines (mastered Whirlwind)
        public float HealPerHit, HealRange;
        public int HealHits;          // most enemies a swing's healing counts (Holy Mace)
        public float HealSpin;        // heals allies near you once per swing (Censer)
        public bool Rapier, Heavy, Silent;
        public bool Thrust;           // a forward jab (Lance, Halberd, Rapier): drawn as a thrust, not a sweep
        public float Stun;            // freezes what it hits for this long (not bosses)
        public float HalfWidth;       // > 0: hits a rectangle this wide either side of the aim line, out to Reach (Lance), not an arc
        public bool NoCut;            // doesn't cut bullets (the Ninja's Kusarigama: a chain, not a blade)
        public float Bleed;           // each hit bleeds for this much of its damage per second (Bear Totem)
        public float ClawSide;        // != 0: a bear-claw swipe, drawn as claw marks raking this way (+1 / -1)
        public float Tip;             // > 0: a whip (the Blood Mage's Blood Lash): hits beyond this distance crack, closer ones are flung out to it
        public bool Clots;            // hits build the swinger's Clot (Blood Lash; BloodMage.cs)
        public float Taunt;           // taunts what it hits for this long (a Clot Burst)
    }

    /// The Knight: a melee class for every role. Its weapons swing instead of shooting, cutting small
    /// bullets out of the air; its abilities close the distance and keep it standing.
    public partial class Game
    {
        readonly List<Swing> pendingSwings = new List<Swing>();

        const float LeapRange = 320f, LeapTime = 0.45f, SlamRadius = 130f, SlamDamage = 50f;
        const float LungeRange = 260f, LungeSpeed = 1000f, LungeDamage = 35f;
        const float WallCapacity = 400f, WallTime = 4f, WallHalfArc = 1.05f;  // 120 degrees across
        const float WhirlRadius = 110f, WhirlDamage = 12f, WhirlEvery = 0.2f;
        public const float LanceHalfWidth = 44f, LanceStun = 1f;  // the Lance hits an 88 px wide box straight ahead

        // --- Weapons ------------------------------------------------------------------

        /// How far a melee weapon reaches (Piercing Rounds lengthen it 10% a stack).
        public static float MeleeReach(Player p, float baseReach) => baseReach * (1f + 0.1f * p.Stats.Pierce);

        /// A melee weapon's base swing reach (Defs; bots and tests use it).
        public static float MeleeRange(WeaponId w) => Defs.WeaponOf(w).Reach;

        Swing NewSwing(Player p, float aim, float reach, float arcDeg, float dmg)
        {
            return new Swing { Owner = p.Id, Aim = aim, Reach = MeleeReach(p, reach), HalfArc = arcDeg * MathX.Pi / 360f, Dmg = dmg };
        }

        /// A Knight weapon's attack. `extra` is the number of extra projectiles; each weapon uses them its own way.
        void FireKnightWeapon(Player p, float aim, float dmg, int extra)
        {
            p.SwingCount++;
            var w = p.Weapon;  // its Reach, Damage and Grow (Defs)
            switch (w.Id)
            {
                case WeaponId.Warhammer:
                {
                    var s = NewSwing(p, aim, w.Reach, 150f, w.Damage * dmg);
                    s.Knock = 35f;
                    s.Heavy = true;
                    pendingSwings.Add(s);
                    for (int k = 1; k <= extra; k++)  // aftershocks
                    {
                        var a = NewSwing(p, aim, w.Reach + w.Grow * k, 150f, 30f * dmg);
                        a.Delay = 0.2f * k;
                        a.Knock = 20f;
                        a.Heavy = true;
                        pendingSwings.Add(a);
                    }
                    break;
                }
                case WeaponId.Lance:
                {
                    // A big jab straight ahead that drives everything it hits far back (not bosses or heavies).
                    var s = NewSwing(p, aim, w.Reach + w.Grow * extra, 0f, w.Damage * dmg);
                    s.HalfWidth = LanceHalfWidth + 12f * extra;
                    s.Stun = LanceStun;
                    s.Knock = 140f + 60f * extra;
                    s.Heavy = true;
                    s.Thrust = true;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.Halberd:
                {
                    float reach = w.Reach + w.Grow * extra;
                    var h = NewSwing(p, aim, reach, 40f, w.Damage * dmg);
                    h.Thrust = true;
                    pendingSwings.Add(h);
                    if (extra > 0)
                    {
                        var f = NewSwing(p, aim, reach, 40f, 19f * dmg);
                        f.Delay = 0.12f;
                        f.Thrust = true;
                        pendingSwings.Add(f);
                    }
                    break;
                }
                case WeaponId.HolyMace:
                {
                    var s = NewSwing(p, aim, w.Reach, 120f, w.Damage * dmg);
                    s.HealPerHit = 2f + extra;
                    s.HealRange = 220f + 60f * extra;
                    s.HealHits = 4;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.Lightbringer:
                {
                    pendingSwings.Add(NewSwing(p, aim, w.Reach, 100f, w.Damage * dmg));
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float f = n == 1 ? 0f : i * 2f / (n - 1) - 1f;
                        var b = SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(aim) * p.Def.BodyR, V2.FromAngle(aim + f * 0.2f * (n - 1)) * 600f, 0f, 0.45f, 12f * dmg, p.Id);
                        b.Pierce += 2;
                        b.HealAlly = 6f;
                    }
                    break;
                }
                case WeaponId.Censer:
                {
                    var s = NewSwing(p, aim, w.Reach + w.Grow * extra, 360f, w.Damage * dmg);
                    s.HealSpin = 3f + extra;
                    s.HealRange = 140f;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.TwinBlades:
                {
                    float side = p.SwingCount % 2 == 0 ? 0.2f : -0.2f;
                    pendingSwings.Add(NewSwing(p, aim + side, w.Reach, 90f, w.Damage * dmg));
                    for (int k = 1; k <= extra; k++)
                    {
                        var c = NewSwing(p, aim - side * (k % 2 == 1 ? 1f : -1f), w.Reach, 90f, 9f * dmg);
                        c.Delay = 0.05f * k;
                        c.Silent = true;
                        pendingSwings.Add(c);
                    }
                    break;
                }
                case WeaponId.Greatsword:
                {
                    var s = NewSwing(p, aim, w.Reach, 170f, w.Damage * dmg);
                    s.Heavy = true;
                    pendingSwings.Add(s);
                    for (int i = 0; i < extra; i++)
                    {
                        float f = extra == 1 ? 0f : i * 2f / (extra - 1) - 1f;
                        var b = SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(aim) * p.Def.BodyR, V2.FromAngle(aim + f * 0.25f) * 700f, 0f, 0.6f, 24f * dmg, p.Id);
                        b.Pierce += 99;
                    }
                    break;
                }
                case WeaponId.Rapier:
                {
                    var s = NewSwing(p, aim, w.Reach, 36f, w.Damage * dmg);
                    s.Rapier = true;
                    s.Thrust = true;
                    s.MaxTargets = 1 + extra;
                    pendingSwings.Add(s);
                    break;
                }
            }

            // Blade Tempest: every swing also hurls piercing sword beams.
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BladeTempest)
            {
                int n = Mastered(p, AbilityId.BladeTempest) ? 5 : 3;
                for (int i = 0; i < n; i++)
                {
                    float f = i * 2f / (n - 1) - 1f;
                    var b = SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(aim) * p.Def.BodyR, V2.FromAngle(aim + f * 0.22f) * 750f, 0f, 0.7f, 20f * dmg, p.Id);
                    b.Pierce += 99;
                }
            }
        }

        /// Barrage on a Knight weapon: a sword wave per stack, fanned out to the sides.
        void FireKnightWaves(Player p, float aim)
        {
            for (int v = 1; v <= p.Stats.Fan; v++)
            {
                float a = aim + (v % 2 == 1 ? 1f : -1f) * 0.14f * ((v + 1) / 2);
                SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(a) * p.Def.BodyR, V2.FromAngle(a) * 650f, 0f, 0.5f, 9f * p.Stats.DmgMult, p.Id);
            }
        }

        /// Called from Collide (after it clears its queues): land every swing that's due.
        void ResolveSwings()
        {
            for (int i = pendingSwings.Count - 1; i >= 0; i--)
            {
                var s = pendingSwings[i];
                s.Delay -= tickDt;
                if (s.Delay > 0f)
                    continue;
                pendingSwings.RemoveAt(i);
                Player p;
                if (Players.TryGetValue(s.Owner, out p) && !p.Downed)
                    LandSwing(p, s);
            }
        }

        static float Dot(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;

        static float AngleTo(V2 from, V2 to, float aim) => Math.Abs(MathX.Fmod((to - from).Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi);

        void LandSwing(Player p, Swing s)
        {
            V2 origin = p.Pos, dir = V2.FromAngle(s.Aim);
            bool full = s.HalfArc >= MathX.Pi - 0.01f, box = s.HalfWidth > 0f;
            if (s.ClawSide != 0f)
                BroadcastFx(FxKind.Claw, origin, origin + dir * s.Reach, new Rgba(1f, 0.86f, 0.78f), s.ClawSide * s.HalfArc, 0.2f,
                    s.Silent ? Sfx.None : Sfx.SwingLight);
            else if (box)
                BroadcastFx(FxKind.Jab, origin, origin + dir * s.Reach, Defs.BulletColor(Defs.KnightSlash), s.HalfWidth, 0.26f,
                    s.Silent ? Sfx.None : Sfx.SwingHeavy);
            else
                BroadcastFx(FxKind.Slash, origin, origin + dir * s.Reach, s.Tip > 0f || s.Clots ? LashRed : Defs.BulletColor(Defs.KnightSlash), s.Thrust ? -s.HalfArc : s.HalfArc, s.Heavy ? 0.24f : 0.16f,
                    s.Silent ? Sfx.None : s.Heavy ? Sfx.SwingHeavy : Sfx.SwingLight);

            // Who's in the arc (the Rapier only takes the nearest few).
            var hits = new List<Enemy>();
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                float d = e.Pos.DistanceTo(origin);
                if (box)
                {
                    // The Lance's box: from your body out to its reach, HalfWidth either side of the aim line.
                    V2 rel = e.Pos - origin;
                    float along = Dot(rel, dir), across = Math.Abs(Dot(rel, dir.Orthogonal()));
                    if (along < -e.Radius - p.Def.BodyR || along > s.Reach + e.Radius || across > s.HalfWidth + e.Radius)
                        continue;
                    if (!BlocksShot(e, origin))
                        hits.Add(e);
                    else
                        WearShield(e, s.Dmg);
                    continue;
                }
                if (d - e.Radius > s.Reach)
                    continue;
                float slack = d > 1f ? (float)Math.Asin(Math.Min(1f, e.Radius / d)) : MathX.Pi;
                if (!full && AngleTo(origin, e.Pos, s.Aim) > s.HalfArc + slack && d > e.Radius + p.Def.BodyR)
                    continue;
                if (BlocksShot(e, origin))
                {
                    WearShield(e, s.Dmg);
                    continue;  // shields stop swords too (and wear)
                }
                hits.Add(e);
            }
            if (s.MaxTargets > 0 && hits.Count > s.MaxTargets)
            {
                hits.Sort((a, b) => a.Pos.DistanceSq(origin).CompareTo(b.Pos.DistanceSq(origin)));
                hits.RemoveRange(s.MaxTargets, hits.Count - s.MaxTargets);
            }

            p.Run.ShotsFired++;
            if (hits.Count > 0)
                p.Run.ShotsHit++;
            int healed = 0;
            foreach (var e in hits)
            {
                float dmg = s.Dmg;
                bool crack = s.Tip > 0f && e.Pos.DistanceTo(origin) + e.Radius >= s.Tip;
                if (crack)
                    dmg *= LashCrack;
                if (s.Rapier)
                {
                    // Each hit in a row on the same enemy deals 10% more (up to +60%).
                    p.RapierStreak = e.Id == p.RapierTarget ? Math.Min(6, p.RapierStreak + 1) : 0;
                    p.RapierTarget = e.Id;
                    dmg *= 1f + 0.1f * p.RapierStreak;
                }
                dmg *= NinjaHitMult(p.Id, Defs.KnightSlash, e);  // Vanish's ambush (Ninja.cs)
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
                if (s.Bleed > 0f)
                    ApplyBleed(e, dmg * s.Bleed, p.Id);  // Minions.cs
                if (s.Stun > 0f && !e.Def.IsBoss)
                {
                    e.FrozenT = Math.Max(e.FrozenT, s.Stun);
                    e.SlowT = Math.Max(e.SlowT, s.Stun);
                }
                if (s.Knock > 0f && !Heavy(e))
                {
                    V2 away = (e.Pos - origin).Normalized();
                    e.Pos = ClampToArena(e.Pos + (away.IsZero ? dir : away) * s.Knock, e.Radius);
                }
                else if (s.Knock < 0f && !Heavy(e))  // pulled in (Kusarigama), but never into you
                {
                    V2 away = (e.Pos - origin).Normalized();
                    float d = Math.Max(e.Pos.DistanceTo(origin) + s.Knock, e.Radius + p.Def.BodyR + 4f);
                    e.Pos = ClampToArena(origin + (away.IsZero ? dir : away) * d, e.Radius);
                }
                if (s.HealPerHit > 0f && healed < s.HealHits)
                {
                    healed++;
                    foreach (var q in Players.Values)
                        if (q.Pos.DistanceTo(origin) < s.HealRange)
                            Heal(q, s.HealPerHit, p);
                }
                if (s.Tip > 0f)
                    LashHit(p, e, s, crack);  // BloodMage.cs
                else if (s.Taunt > 0f)
                    TauntEnemy(p, e, s.Taunt);
                if (s.Clots)
                    AddClot(p, crack ? 2 : 1);
            }
            if (s.HealSpin > 0f)
                foreach (var q in Players.Values)
                    if (q.Pos.DistanceTo(origin) < s.HealRange)
                        Heal(q, s.HealSpin, p);

            if (!s.NoCut)
                CutBullets(origin, s.Aim, s.Reach + p.Def.BodyR * 0.5f, full ? MathX.Pi : s.HalfArc, s.CutAll, s.HalfWidth);
        }

        /// Swings slice small enemy bullets out of the air; a mastered Whirlwind (CutAll) stops big orbs too.
        /// Bosses' red aimed shots always get through.
        int CutBullets(V2 origin, float aim, float reach, float halfArc, bool all, float halfWidth = 0f)
        {
            var cut = new List<int>();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                if (!all && b.Kind != Defs.SmallShot && b.Kind != Defs.FastShot)
                    continue;
                if (halfWidth > 0f)
                {
                    V2 dir = V2.FromAngle(aim), rel = b.Pos - origin;
                    float along = Dot(rel, dir);
                    if (along < -b.Radius || along > reach + b.Radius || Math.Abs(Dot(rel, dir.Orthogonal())) > halfWidth + b.Radius)
                        continue;
                    cut.Add(b.Id);
                    continue;
                }
                if (b.Pos.DistanceTo(origin) > reach + b.Radius)
                    continue;
                if (halfArc < MathX.Pi && AngleTo(origin, b.Pos, aim) > halfArc)
                    continue;
                cut.Add(b.Id);
            }
            foreach (int id in cut)
            {
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }
            return cut.Count;
        }

        // --- Abilities ------------------------------------------------------------------

        /// The Knight's abilities and ultimates. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] KnightAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.ShieldWall, Kit = Kit.Knight, Role = Role.Tank, Choice = 0,
                Name = "Shield Wall", Cooldown = 12f, Desc = "Raise your shield for 4 s: it stops every enemy bullet coming at your front (120°), even red aimed shots. Bosses can break it (it soaks 400 of their damage).",
                Capstone = "Bullets that hit the shield are reflected back as your own.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.LeapSlam, Kit = Kit.Knight, Role = Role.Tank, Choice = 1,
                Name = "Leap Slam", Cooldown = 9f, Desc = "Leap to your cursor (up to 320 px), untouchable in the air. Landing deals 50 damage around you and stuns enemies for 1 s.",
                Capstone = "The landing also erases enemy bullets within 160 px.",
                Bot = b => b.HaveTarget && b.TargetD > 140f && b.TargetD < 340f || b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Unbreakable, Kit = Kit.Knight, Role = Role.Tank, Choice = 2,
                Name = "Unbreakable", Cooldown = 16f, Desc = "For 5 s you take 50% less damage and your hits heal you for 15% of the damage they deal.",
                Capstone = "Lasts 8 s.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Consecrate, Kit = Kit.Knight, Role = Role.Healer, Choice = 0,
                Name = "Consecrate", Cooldown = 14f, Desc = "Bless the ground at your feet for 6 s: allies inside heal 12 HP/s and enemies burn for 20 damage a second.",
                Capstone = "The blessed ground follows you.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.GuardiansOath, Kit = Kit.Knight, Role = Role.Healer, Choice = 1,
                Name = "Guardian's Oath", Cooldown = 15f, Desc = "Shield the ally nearest your cursor for 100 damage (and yourself for 50) for 6 s.",
                Capstone = "Everyone within 300 px gets the full shield.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.LayOnHands, Kit = Kit.Knight, Role = Role.Healer, Choice = 2,
                Name = "Lay on Hands", Cooldown = 18f, Desc = "Heal the most hurt ally within 300 px (or yourself) for 70, or instantly revive them at half health if they're down.",
                Capstone = "Also heals everyone else near them for half as much.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.Whirlwind, Kit = Kit.Knight, Role = Role.Dps, Choice = 0,
                Name = "Whirlwind", Cooldown = 10f, Desc = "Spin for 2 s, hitting everything within 110 px for 12 every 0.2 s and slicing small bullets out of the air.",
                Capstone = "Lasts 3 s and cuts every bullet except red aimed shots.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 150f,
            },
            new AbilityDef
            {
                Id = AbilityId.Lunge, Kit = Kit.Knight, Role = Role.Dps, Choice = 1, Instant = true, ExtraCharges = 1, CapstoneCharge = true,
                Name = "Lunge", Cooldown = 6f, Desc = "Dash 260 px toward your cursor, hitting everything you pass for 35. Two charges.",
                Capstone = "+1 charge (three in all).",
                Bot = b => b.HaveTarget && b.TargetD > 110f && b.TargetD < 300f || b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.BattleFury, Kit = Kit.Knight, Role = Role.Dps, Choice = 2,
                Name = "Battle Fury", Cooldown = 16f, Desc = "For 6 s you swing 40% faster and deal 25% more damage.",
                Capstone = "+1 extra projectile while it lasts.",
                Bot = b => b.HaveTarget && b.TargetD < 220f,
            },
            new AbilityDef
            {
                Id = AbilityId.LastStand, Kit = Kit.Knight, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Last Stand", Cooldown = 70f, Desc = "For 8 s you can't drop below 1 HP, every enemy targets you, and you deal 50% more damage.",
                Capstone = "Lasts 12 s.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 220f,
            },
            new AbilityDef
            {
                Id = AbilityId.HallowedBlade, Kit = Kit.Knight, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Hallowed Blade", Cooldown = 80f, Desc = "A giant holy sword falls at your cursor: 200 damage around it, then the ground is blessed for 8 s (allies heal 15 HP/s, enemies burn).",
                Capstone = "Three swords fall: one at your cursor and one either side.",
            },
            new AbilityDef
            {
                Id = AbilityId.BladeTempest, Kit = Kit.Knight, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Blade Tempest", Cooldown = 65f, Desc = "For 6 s every swing also hurls three piercing sword beams.",
                Capstone = "Five sword beams a swing instead of three.",
                Bot = b => b.HaveTarget && b.TargetD < 220f,
            },
        };

        /// Knight abilities and ultimates. Returns false if `id` isn't one.
        bool KnightAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                case AbilityId.ShieldWall:
                    p.WallT = WallTime + s.WallBonusTime;
                    p.WallHp = WallCapacity * s.WallCapMult * pw;
                    p.WallReflect = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.75f, 0.85f, 1f), 50f, 0.35f, Sfx.Clang);
                    return true;
                case AbilityId.LeapSlam:
                    // The player's own game moves them (LocalControl); the slam lands where they come down.
                    p.InvulnT = Math.Max(p.InvulnT, LeapTime + 0.1f);
                    slams.Add(new Slam { Owner = p.Id, Pos = target, T = LeapTime, Power = pw, Mastered = cap,
                        Radius = SlamRadius * s.SlamRadiusMult, Stun = 1f + s.SlamStunBonus });
                    return true;
                case AbilityId.Unbreakable:
                    p.UnbreakT = cap ? 8f : 5f;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.75f, 0.5f), 60f, 0.4f, Sfx.Clang);
                    return true;
                case AbilityId.Consecrate:
                {
                    var z = AddZone(ZoneKind.Consecrate, p, p.Pos, 130f * s.ConsecrateRadiusMult, 6f + s.ConsecrateBonusTime);
                    z.Follow = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.5f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.GuardiansOath:
                {
                    float shield = 100f * s.OathMult * pw;
                    Player best = null;
                    foreach (var q in Players.Values)
                        if (q.Id != p.Id && !q.Downed && (best == null || q.Pos.DistanceSq(target) < best.Pos.DistanceSq(target)))
                            best = q;
                    foreach (var q in Players.Values)
                    {
                        if (q.Downed)
                            continue;
                        float amount = q == best || cap && q.Pos.DistanceTo(p.Pos) < 300f ? shield : q == p ? shield * 0.5f : 0f;
                        if (best == null && q == p)
                            amount = shield;  // alone: the full shield is yours
                        if (amount <= 0f)
                            continue;
                        q.ShieldHp = Math.Max(q.ShieldHp, amount);
                        q.ShieldT = 6f;
                        castHits++;
                        BroadcastFx(FxKind.Ring, q.Pos, new Rgba(1f, 0.9f, 0.5f), 45f, 0.4f, Sfx.None);
                    }
                    Sounds.Add(Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.LayOnHands:
                {
                    float range = 300f * (1f + 0.5f * (s.LayMult - 1f));
                    Player best = null;
                    foreach (var q in Players.Values)
                    {
                        if (q.Pos.DistanceTo(p.Pos) > range)
                            continue;
                        if (best == null || q.Downed && !best.Downed || q.Downed == best.Downed && q.Hp / q.MaxHp < best.Hp / best.MaxHp)
                            best = q;
                    }
                    if (best != null)
                    {
                        if (best.Downed)
                            RevivePlayer(best, 0.5f, p);
                        else
                            Heal(best, 70f * s.LayMult, p);
                        castHits++;
                        BroadcastFx(FxKind.Ring, best.Pos, new Rgba(1f, 0.95f, 0.6f), 70f, 0.5f, Sfx.SpellHoly);
                        if (cap)
                            foreach (var q in Players.Values)
                                if (q != best && !q.Downed && q.Pos.DistanceTo(best.Pos) < 200f)
                                    Heal(q, 35f * s.LayMult, p);
                    }
                    return true;
                }
                case AbilityId.Whirlwind:
                    p.WhirlT = (cap ? 3f : 2f) + s.WhirlBonusTime;
                    p.WhirlTick = 0f;
                    p.WhirlPower = pw;
                    p.WhirlAll = cap;
                    return true;
                case AbilityId.Lunge:
                {
                    // Like Bulldoze: the player's own game moves them; everything near the path takes a hit.
                    p.InvulnT = Math.Max(p.InvulnT, LungeRange * s.LungeRangeMult / LungeSpeed + 0.1f);
                    V2 from = p.Pos, path = target - from;
                    float len = Math.Max(1f, path.Length);
                    V2 dir = path / len;
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f)
                            continue;
                        float along = Math.Max(0f, Math.Min(len, (e.Pos.X - from.X) * dir.X + (e.Pos.Y - from.Y) * dir.Y));
                        if (e.Pos.DistanceTo(from + dir * along) > e.Radius + 34f)
                            continue;
                        float dmg = LungeDamage * s.DmgMult * s.LungeDmgMult;
                        HurtEnemy(e, dmg, p.Id);
                        OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
                    }
                    BroadcastFx(FxKind.Slash, from, target, Defs.BulletColor(Defs.KnightSlash), 0.12f, 0.2f, Sfx.SwingLight);
                    return true;
                }
                case AbilityId.BattleFury:
                    p.FuryT = 6f * pw;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.35f, 0.25f), 60f, 0.4f, Sfx.Roar);
                    return true;

                // Ultimates
                case AbilityId.LastStand:
                {
                    float time = cap ? 12f : 8f;
                    p.UltT = time;
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = time;
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.5f, 0.3f), 160f, 0.6f, Sfx.Roar);
                    return true;
                }
                case AbilityId.HallowedBlade:
                {
                    var spots = new List<V2> { target };
                    if (cap)
                    {
                        V2 side = (target - p.Pos).Normalized().Orthogonal();
                        if (side.IsZero)
                            side = new V2(1f, 0f);
                        spots.Add(ClampToArena(target + side * 220f, 10f));
                        spots.Add(ClampToArena(target - side * 220f, 10f));
                    }
                    foreach (var at in spots)
                        AddZone(ZoneKind.HolySword, p, at, 200f, 0.6f).Consecrates = true;
                    Sounds.Add(Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.BladeTempest:
                    p.UltT = 6f;
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.KnightWave), 80f, 0.4f, Sfx.SwingHeavy);
                    return true;
            }
            return false;
        }

        bool LastStanding(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.LastStand;

        /// Damage multiplier from Knight buffs on whoever dealt the hit (applied in HurtEnemy).
        float KnightDamageMult(Player p) => (p.FuryT > 0f ? 1.25f : 1f) * (LastStanding(p) ? 1.5f : 1f);

        /// Unbreakable and Battle Fury (with Bloodlust) heal you for part of what you deal.
        void KnightLifesteal(Player p, float dealt)
        {
            float frac = (p.UnbreakT > 0f ? 0.15f + p.Stats.UnbreakHealBonus : 0f) + (p.FuryT > 0f ? p.Stats.FuryLifesteal : 0f);
            if (frac > 0f && !p.Downed && dealt > 0f)
                Heal(p, dealt * frac, p);
        }

        // --- Timed effects ---------------------------------------------------------------

        class Slam
        {
            public int Owner;
            public V2 Pos;
            public float T, Power, Radius, Stun;
            public bool Mastered;
        }

        readonly List<Slam> slams = new List<Slam>();

        /// Host, every tick: Knight buffs run down, Whirlwind spins, Leap Slams land.
        void TickKnights(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.WallT = Math.Max(0f, p.WallT - dt);
                p.UnbreakT = Math.Max(0f, p.UnbreakT - dt);
                p.FuryT = Math.Max(0f, p.FuryT - dt);
                p.BossContactT = Math.Max(0f, p.BossContactT - dt);
                if (p.Downed)
                    p.WallT = p.WhirlT = p.UnbreakT = p.FuryT = 0f;
                if (p.WhirlT > 0f)
                {
                    p.WhirlT -= dt;
                    p.WhirlTick -= dt;
                    if (p.WhirlTick <= 0f)
                    {
                        p.WhirlTick += WhirlEvery;
                        var s = NewSwing(p, p.Aim, WhirlRadius * p.Stats.WhirlRadiusMult, 360f, WhirlDamage * p.Stats.DmgMult * p.WhirlPower);
                        s.CutAll = p.WhirlAll;
                        s.Silent = MathX.Fmod(p.WhirlT, 0.6f) > 0.2f;
                        pendingSwings.Add(s);
                    }
                }
            }
            for (int i = slams.Count - 1; i >= 0; i--)
            {
                var sl = slams[i];
                sl.T -= dt;
                if (sl.T > 0f)
                    continue;
                slams.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(sl.Owner, out p))
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Pos.DistanceTo(sl.Pos) > sl.Radius + e.Radius)
                        continue;
                    HurtEnemy(e, SlamDamage * p.Stats.DmgMult * sl.Power, p.Id);
                    if (!e.Def.IsBoss)
                    {
                        e.FrozenT = Math.Max(e.FrozenT, sl.Stun);
                        e.SlowT = Math.Max(e.SlowT, sl.Stun);
                    }
                }
                if (sl.Mastered)
                    EraseEnemyBullets(sl.Pos, 160f);
                BroadcastFx(FxKind.Boom, sl.Pos, new Rgba(0.85f, 0.8f, 0.65f), sl.Radius, 0.4f, Sfx.Explode);
            }
        }

        /// Does a raised Shield Wall cover this point (in front of the knight)?
        bool WallCovers(Player p, V2 pos, float radius)
        {
            V2 d = pos - p.Pos;
            float reach = p.Def.BodyR + 34f + radius;
            if (d.LengthSq > reach * reach)
                return false;
            return AngleTo(p.Pos, pos, p.Aim) < WallHalfArc * p.Stats.WallArcMult;
        }
    }
}
