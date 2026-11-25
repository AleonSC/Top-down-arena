using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Ninja: quick and lightly protected (85% health, 12% faster), with thrown blades and fast melee for
    /// every role. Tanks misdirect (clones, smoke, Ceremonial Armor), healers keep the party up from close by (mist,
    /// swapping places, wards), and DPS strike from the shadows (Shadow Step, Vanish's ambush, Blade Fan).
    public partial class Game
    {
        // The Katana (Katana.cs; Docs/KatanaCombo.md): two stance combos, switched with the weapon-action key; each keeps its place while
        // you're in the other. Damage is x the weapon's (20); times are seconds.
        public static readonly float[] KatanaHayNext = { 0.22f, 0.22f, 0.36f, 0.32f }, KatanaSenNext = { 0.25f, 0.25f, 0.34f, 0.36f, 0.48f };  // each step holds the next one off this long
        public const float KatanaDashGrace = 0.15f;  // i-frames kept after a dash ends (covers the lag before your own screen starts it)
        public const float KatanaDashLen = 120f, KatanaDashTime = 0.09f, KatanaDashWidth = 16f, KatanaDashMult = 1.1f, KatanaZig = 0.35f;  // Kiri and Kaeshi: the dash, its line's half width, its cut; Kaeshi angles this far to alternate sides
        public const float KatanaSpinDash = 70f, KatanaSpinDashTime = 0.08f, KatanaSpinReach = 85f, KatanaSpinMult = 1.3f;  // Tsumuji: a short dash into a full spin
        public const float KatanaBurstRadius = 150f, KatanaBurstPush = 120f, KatanaBurstMult = 0.4f, KatanaBurstMark = 4f, KatanaBurstAt = 0.12f, KatanaPlant = 0.2f;  // the Momentum Burst: its ring, shove, damage, how long it marks, when the blade goes in, how long you're planted
        public const float KatanaSwipeArc = 130f, KatanaSwipeMult = 0.8f, KatanaSweepTime = 0.12f;  // Agari and Kaeshi
        public const float KatanaMoonStep = 30f, KatanaMoonStepTime = 0.12f, KatanaMoonReach = 105f, KatanaMoonMult = 1f, KatanaMoonTime = 0.2f;  // Engetsu: a step into a full-moon spin
        public const int KatanaSlashes = 6;
        public const float KatanaSlashLen = 95f, KatanaSlashMult = 0.35f, KatanaSlashGap = 0.05f, KatanaSlashCone = 0.6f;  // Senbon: six crossing slashes in a cone
        public const int KatanaCaltropCount = 16;  // +2 a extra projectile
        public const float KatanaCaltropAt = 0.1f, KatanaToss = 0.25f, KatanaCaltropLife = 8f, KatanaCaltropMult = 0.2f, KatanaCaltropFlyMult = 0.4f, KatanaCaltropSlow = 0.5f, KatanaCaltropSpeed = 420f;  // thrown 60-150 px out, lying 8 s (hurts and slows), flung or seeking (8)
        public const float KatanaSwitchCd = 0.3f, KatanaIdleReset = 1.5f;  // the switch's cooldown; the stance you're in starts over after this long without a step
        public const float KatanaExtraMult = 0.6f;  // +1: each dash strikes once more at this; also +2 caltrops and a 25% wider Burst
        public const float KatanaDashHold = 0.025f, KatanaDashHoldPer = 0.01f;  // your own hits' hit-pause: the picture holds this long, plus this per extra enemy (up to 3 more)
        // The Bo Staff (Docs/Mockups/bo-staff-rework.html): slash, jab, spin, then round again. There is no timer: a click always carries
        // on from where you left off. The weapon-action key shoves and starts the chain over.
        public const float BoSlashArc = 130f, BoJabReach = 140f, BoJabWidth = 14f, BoSpinReach = 100f;  // the slash's cone (degrees), the jab's reach and half width, the spin's reach (the slash uses the weapon's own Reach)
        public const float BoJabMult = 22f / 16f, BoSpinMult = 30f / 16f;  // damage against the slash's
        public const float BoExtraDmg = 0.15f;  // each extra projectile (upgrade): +15% damage on every swing and the shove, on top of +20 px reach
        public static readonly float[] BoStepTime = { 0.32f, 0.30f, 0.80f };  // how long each step holds the next one off (the spin's includes its recovery)
        public const float BoShoveReach = 78f, BoShoveHalf = 50f, BoShoveMult = 6f / 16f, BoShoveKnock = 70f, BoShoveWind = 0.1f, BoShoveTime = 0.36f, BoShoveCd = 1f;  // the shove's bar: how far out, half its width, damage against the slash's, the push, the pull-back before it lands, how long it holds you, its cooldown
        public static readonly Rgba BoStaffSage = new Rgba(0.78f, 0.9f, 0.62f);
        public const float FumaReach = 380f, FumaWidth = 110f;  // a Fuma Shuriken's loop: this far out in front of you, this far either side (its Life is one lap)
        public static float FumaSize(int i) => 1f + 0.15f * (i / 2);  // the i-th one thrown loops this much wider (every second pair, a little more)
        const float StepRange = 450f, StepDamage = 80f;
        public const float ShadowStepFxTime = 1f;
        V2? StepFrom;  // where the caster stood when they pressed Shadow Step (set around HostAbility; null: use their position)
        const float CloneWallTime = 7f, CloneTime = 5f, CloneBurst = 60f, CloneBurstRadius = 110f;
        const int ThousandClonesCount = 12, ThousandClonesCapstone = 20;
        const float CloneSpacing = 140f, CloneNinjaGap = 90f, CloneEdge = 60f;
        const float SmokeRadius = 140f, SmokeTime = 5f, SmokeCut = 0.3f, SmokeBulletSpeed = 0.5f;
        const float SubShield = 60f, SubShieldTime = 6f, WardRadius = 140f, WardTime = 8f;
        const float VanishTime = 3f, AmbushGrace = 1f, FanDamage = 18f, KunaiBonus = 0.5f;
        public const float OrbitTime = 4f, BladeOrbitR = 60f, OrbitTurns = 2.2f, OrbitSpin = 0.25f, OrbitCutGap = 0.5f, OrbitBladeR = 8f;  // Blade Fan: how long the kunai circle you, how far out, turns a second, how long they take to spiral out, the gap between cuts on one enemy, a kunai's reach
        const float BlossomTime = 5f, BlossomHeal = 20f, BlossomClear = 100f, LotusRange = 350f, LotusDamage = 100f, LotusStagger = 0.11f, LotusStaggerCap = 0.07f, LotusStain = 2.2f;
        public static readonly Rgba LotusCrimson = new Rgba(1f, 0.18f, 0.42f);

        // --- Weapons ------------------------------------------------------------------

        /// A Ninja weapon's attack. `extra` is the number of extra projectiles; each weapon uses them its own way.
        void FireNinjaWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            p.SwingCount++;
            var w = p.Weapon;  // its Speed, Life, Damage, Reach and Grow (Defs)
            switch (w.Id)
            {
                case WeaponId.Kusarigama:
                    FireKusarigama(p, aim, dmg, extra);  // the sickle cut, then the weight throw (Kusarigama.cs)
                    break;
                case WeaponId.BoStaff:
                {
                    // Slash, jab, spin, then round again, with no timer (the weapon-action key shoves and starts it over).
                    int step = p.BoStep;
                    p.BoStep = (byte)((step + 1) % 3);
                    p.FireCd = BoStepTime[step] / FireRate(p);
                    float grow = w.Grow * extra;
                    dmg *= 1f + BoExtraDmg * extra;
                    Swing s;
                    if (step == 0)
                    {
                        s = NewSwing(p, aim, w.Reach + grow, BoSlashArc, w.Damage * dmg);  // a cone slash
                    }
                    else if (step == 1)
                    {
                        s = NewSwing(p, aim, BoJabReach + grow, 0f, w.Damage * BoJabMult * dmg);  // a jab: a line straight ahead
                        s.HalfWidth = BoJabWidth;
                        s.Thrust = true;
                    }
                    else
                    {
                        s = NewSwing(p, aim, BoSpinReach + grow, 360f, w.Damage * BoSpinMult * dmg * p.Stats.BoSpinDmgMult);  // the full spin (Iron Bo: harder)
                        s.Heavy = true;
                    }
                    QueueSwing(s);
                    break;
                }
                case WeaponId.FumaShuriken:
                {
                    if (p.FumaStance == 1)
                    {
                        FireFumaOrbit(p, aim, dmg);  // the Orbit stance: Lash, Lash, Cyclone (FumaOrbit.cs)
                        break;
                    }
                    // A giant star thrown in an oval loop out in front of you and back, cutting through everything.
                    // Each extra one loops the other way, and every second pair loops a little wider.
                    V2 front = V2.FromAngle(aim);
                    for (int i = 0; i <= extra; i++)
                    {
                        float s = i % 2 == 0 ? 1f : -1f, size = FumaSize(i);
                        V2 vel = front.Orthogonal() * (-s * FumaWidth * size * PierceReach(p) * p.Stats.FumaLoopMult * MathX.Tau / w.Life);  // (Long Loop: a bigger oval)  // it sets off sideways (FumaTurn); Piercing Rounds widen the loop
                        SpawnBullet(Defs.FumaStar, p.Pos, vel, s, w.Life, w.Damage * dmg, p.Id).Pierce += 99;
                    }
                    break;
                }
                case WeaponId.MedicineDarts:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float a = Fanned(aim, i, n, 0.08f);
                        var b = SpawnBullet(Defs.NeedleShot, muzzle, V2.FromAngle(a) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.Medicine = true;  // poisons enemies, doses allies (Ninja.cs)
                    }
                    break;
                }
                case WeaponId.SpiritFan:
                {
                    // A gust: five short-lived puffs that heal allies they pass, and small enemy bullets in front blown back.
                    // Shots alternate: a gust out that blows enemies away, then a draw in that pulls them to you.
                    float reach = (1f + w.Grow * extra) * PierceReach(p) * p.Stats.FanReachMult;  // Piercing Rounds and Gale Fan: the gust reaches farther
                    bool draw = p.SwingCount % 2 == 0;
                    float len = w.Speed * w.Life * reach * p.Stats.ShotSpeedMult;
                    for (int i = 0; i < 5; i++)
                    {
                        V2 d = V2.FromAngle(aim + (i / 4f - 0.5f) * 0.7f);
                        var b = SpawnBullet(Defs.GustShot, draw ? p.Pos + d * (p.Def.BodyR + len) : muzzle, (draw ? -d : d) * w.Speed, 0f, w.Life * reach, w.Damage * dmg, p.Id);  // a draw's puffs fly in
                        b.Pierce += 99;
                        b.HealAlly = (3f + extra) * 9f / 6f * p.Stats.FanHealMult;  // (Gale Fan: more)
                    }
                    FanEnemies(p, aim, p.Def.BodyR + len + FanReachSlack, draw ? -FanShove : FanShove);
                    float push = 130f * reach;
                    foreach (var b in Bullets.Values)
                    {
                        if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind) || b.Kind != Defs.SmallShot && b.Kind != Defs.FastShot)
                            continue;
                        if (b.Pos.FartherThan(p.Pos, push) || AngleTo(p.Pos, b.Pos, aim) > 0.45f)
                            continue;
                        b.Vel = (b.Pos - p.Pos).Normalized() * b.Vel.Length;
                        b.Pulled = true;  // the host sends its new course
                    }
                    break;
                }
                case WeaponId.Ofuda:
                    FireTalismans(p, aim, muzzle, dmg, extra);  // Healing Talismans (Ofuda.cs)
                    break;
                case WeaponId.Shuriken:
                {
                    int n = 3 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.StarShot, muzzle, V2.FromAngle(Fanned(aim, i, n, StarFan)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Sticks = true;  // (switching to the Great Star stance recalls them: GreatStar.cs)
                    break;
                }
                case WeaponId.Nodachi:
                    FireNodachi(p, aim, dmg, extra);  // the next step of the stance you're in (Nodachi.cs)
                    break;
                case WeaponId.Nunchaku:
                    FireNunchaku(p, aim, dmg, extra);  // the Twin Storm combo, or the Twin Counter from the guard (Nunchaku.cs)
                    break;
                case WeaponId.Katana:
                    FireKatana(p, aim, dmg, extra);  // Hayate's dash-cut or Senbon's swipe (Katana.cs)
                    break;
                case WeaponId.Kunai:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float a = aim + (i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * 0.07f * ((i + 1) / 2));
                        var b = SpawnBullet(Defs.KunaiShot, muzzle, V2.FromAngle(a) * w.Speed, 0f, w.Life, (i == 0 ? w.Damage : w.Damage * 0.5f) * dmg, p.Id);
                        b.ShadowKunai = i == 0 && ++p.KunaiCount % KunaiShadowEvery == 0;  // every throw: one leaves a mark to blink to
                    }
                    break;
                }
            }
        }

        /// How much harder a Ninja's direct hit lands: Kunai on enemies that aren't after you, and Vanish's
        /// ambush (the next hit after vanishing, used up by it). Only for shots and swings, not burns or zones.
        float NinjaHitMult(int owner, byte kind, Enemy e)
        {
            Player p;
            if (!Players.TryGetValue(owner, out p))
                return 1f;
            float m = 1f;
            if (kind == Defs.KunaiShot && e.TargetId != owner)
                m *= 1f + KunaiBonus;
            if (p.AmbushT > 0f)
            {
                m *= p.Stats.AmbushMult * p.BlinkBoost;
                p.AmbushT = 0f;
                p.BlinkBoost = 1f;
                BroadcastFx(FxKind.Spark, e.Pos, new Rgba(0.75f, 0.6f, 1f), 30f, 0.3f, Sfx.SwingHeavy);
            }
            return m;
        }

        // --- Blade Fan: the orbit ---------------------------------------------------------

        /// Where the i-th of a player's orbiting kunai is `el` seconds after the cast (the host, and every screen from the replicated timer).
        public static V2 OrbitBladeAt(Player p, int i, float el)
        {
            float rad = BladeOrbitR * MathX.Clamp01(el / OrbitSpin);  // they spiral out from the body
            return p.Pos + V2.FromAngle(MathX.Tau * (i / (float)Math.Max(1, p.OrbitN) + OrbitTurns * el)) * rad;
        }

        public static V2 OrbitBlade(Player p, int i) => OrbitBladeAt(p, i, OrbitTime - p.OrbitT);

        /// Host, every tick: the circling kunai cut what they touch and erase enemy bullets they meet.
        void TickOrbit(Player p, float dt)
        {
            if (p.OrbitT <= 0f)
                return;
            if (p.Downed)
            {
                p.OrbitT = 0f;
                p.OrbitN = 0;
                return;
            }
            p.OrbitT -= dt;
            if (p.OrbitT <= 0f)
            {
                ReleaseOrbit(p);
                return;
            }
            p.OrbitClock += dt;
            for (int i = 0; i < p.OrbitN; i++)
            {
                V2 b = OrbitBlade(p, i);
                EraseEnemyBullets(b, OrbitBladeR + 6f);
                foreach (var e in FoesInCircle(b, OrbitBladeR, by: p.Id))
                {
                    float last;
                    if ((p.OrbitCut.TryGetValue(e.Id, out last) && p.OrbitClock - last < OrbitCutGap))
                        continue;
                    p.OrbitCut[e.Id] = p.OrbitClock;
                    float dmg = FanDamage * p.Stats.DmgMult * NinjaHitMult(p.Id, Defs.KunaiShot, e);
                    HurtEnemy(e, dmg, p.Id);
                    ModHit(e, p.Id, dmg, Defs.KunaiShot, (e.Pos - p.Pos).Normalized());
                }
            }
        }

        /// Host: the orbit ends (time ran out, or Blade Fan was pressed again) and the kunai fly out from where they are, the old Blade Fan's ring.
        void ReleaseOrbit(Player p)
        {
            int n = p.OrbitN;
            float el = OrbitTime - Math.Max(0f, p.OrbitT);
            p.OrbitT = 0f;
            p.OrbitN = 0;
            if (n <= 0)
                return;
            for (int i = 0; i < n; i++)
            {
                V2 from = OrbitBladeAt(p, i, el), d = (from - p.Pos).Normalized();
                if (d.IsZero)
                    d = V2.FromAngle(MathX.Tau * i / n);
                SpawnBullet(Defs.KunaiShot, from, d * 600f, 0f, 0.7f, FanDamage * p.Stats.DmgMult, p.Id).Pierce += 1;
            }
            Sounds.Add(Sfx.NinjaFanBurst);
        }

        /// True if this slot is a Blade Fan that is circling you now: pressing it throws the kunai instead of casting again.
        public bool OrbitRelease(Player p, int slot) => p.Slots[slot].Id == AbilityId.BladeFan && p.OrbitT > 0f;

        // --- Abilities ------------------------------------------------------------------

        /// The Ninja: FireNinjaWeapon, NinjaAbility, TickNinjas. See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef NinjaKit = new KitDef
        {
            Id = Kit.Ninja, Name = "Ninja", Icon = "class_ninja", HpMult = 0.85f, SpeedMult = 1.12f,
            Blurb = "Thrown blades and quick melee with tricks and misdirection: stars that come back, stances, smoke and shadow clones. Fast (+12% move speed) but lightly protected (85% health): hit, slip away, hit again.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireNinjaWeapon(p, aim, dir, muzzle, dmg, extra),
            Ability = (g, p, id, target) => g.NinjaAbility(p, id, target),
            Tick = (g, dt) => g.TickNinjas(dt),
        };

        /// The Ninja's weapons (FireNinjaWeapon).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] NinjaWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Kusarigama, Kit = Kit.Ninja, Role = Role.Tank, Choice = 0, Damage = 26f, Reach = 280f, Grow = 40f, Melee = true, ExtraDesc = "+40 px throw and +20% damage", Name = "Kusarigama", FireInterval = 0.55f,
                Desc = "Primary Fire: Cut with the sickle, then throw the weight to drag an enemy to you.\n\nAlternative Fire: Whirl the weight around you, pulling enemies in." },
            new WeaponDef { Id = WeaponId.BoStaff, Kit = Kit.Ninja, Role = Role.Tank, Choice = 1, Damage = 16f, Reach = 105f, Grow = 20f, Melee = true, ExtraDesc = "+20 px reach and +15% damage on every swing and the shove, and +20 px on the shove's push", Name = "Bo Staff", FireInterval = 0.47f,
                Desc = "Primary Fire: A three-swing staff combo that cuts small bullets.\n\nAlternative Fire: Shove a wall of staff ahead of you." },
            new WeaponDef { Id = WeaponId.FumaShuriken, Kit = Kit.Ninja, Role = Role.Tank, Choice = 2, Life = 1.1f, Damage = 30f, BotRange = 300f, ExtraDesc = "+1 shuriken, looping the other way", Name = "Fuma Shuriken", FireInterval = 0.7f,
                Desc = "Primary Fire: A giant star thrown in a loop that cuts everything both ways." },
            new WeaponDef { Id = WeaponId.Nodachi, Kit = Kit.Ninja, Role = Role.Tank, Choice = 3, Damage = 20f, Reach = 150f, Melee = true, BotRange = 140f, ExtraDesc = "+1 ring after Tenchi's slam, 45 px farther out (60%)", Name = "Nodachi", FireInterval = 0.3f,
                Desc = "Primary Fire: A tall katana with two five-step combo stances: wide sweeps, or fast jabs that block.\n\nAlternative Fire: Switch stance." },
            // Healer
            new WeaponDef { Id = WeaponId.MedicineDarts, Kit = Kit.Ninja, Role = Role.Healer, Choice = 0, Speed = 900f, Life = 0.8f, Damage = 7f, ExtraDesc = "+1 dart, fanned out", Name = "Medicine Darts", FireInterval = 0.18f,
                Desc = "Primary Fire: Fast needles that poison enemies and heal allies." },
            new WeaponDef { Id = WeaponId.SpiritFan, Kit = Kit.Ninja, Role = Role.Healer, Choice = 1, Speed = 420f, Life = 0.3f, Damage = 6f, Grow = 0.25f, ExtraDesc = "+25% reach and +1 HP healed", Name = "Spirit Fan", FireInterval = 0.4f,
                Desc = "Primary Fire: A short gust that heals allies and pushes small bullets back." },
            new WeaponDef { Id = WeaponId.Ofuda, Kit = Kit.Ninja, Role = Role.Healer, Choice = 2, Speed = 380f, Life = 1.1f, Damage = 45f, ExtraDesc = "+1 talisman and +1 Blast Ofuda, fanned out", Name = "Ofuda", FireInterval = 0.45f,
                Desc = "Primary Fire: Healing Talisman: a paper charm that bends toward a hurt teammate at the end of its flight, heals them and bursts, healing teammates near, shoving enemies back and erasing small bullets.\n\nAlternative Fire: Blast Ofuda: a charm that flies straight, sticks to the enemy it hits and blows up; one that misses lands and blows up a moment later.",
                AreaDesc = "the talisman's burst and the Blast Ofuda's blast 25% wider" },
            // DPS
            new WeaponDef { Id = WeaponId.Shuriken, Kit = Kit.Ninja, Role = Role.Dps, Choice = 0, Speed = 700f, Life = 0.75f, Damage = 9f, ExtraDesc = "+1 star in the fan", Name = "Shuriken", FireInterval = 0.35f,
                Desc = "Primary Fire: Tight fans of stars that stick, then rip out and fly back.\n\nAlternative Fire: Switch to a huge star thrown along a route you draw." },
            new WeaponDef { Id = WeaponId.Katana, Kit = Kit.Ninja, Role = Role.Dps, Choice = 1, Damage = 20f, Reach = 120f, Melee = true, ExtraDesc = "+1: each dash strikes once more (60%), two more caltrops and a wider Momentum Burst", Name = "Katana", FireInterval = 0.3f,
                Desc = "Primary Fire: Two stance combos: dashing slices, or cuts that end in caltrops.\n\nAlternative Fire: Switch stance." },
            new WeaponDef { Id = WeaponId.Kunai, Kit = Kit.Ninja, Role = Role.Dps, Choice = 2, Speed = 1400f, Life = 0.5f, Damage = 42f, ExtraDesc = "+1 knife, fanned out (half damage)", Name = "Kunai", FireInterval = 0.7f, Sound = Sfx.ShotKunai,
                Desc = "Primary Fire: Slow, heavy throwing knives that leave a mark where they land.\n\nAlternative Fire: Blink to the mark, and your next hit lands double." },
            new WeaponDef { Id = WeaponId.Nunchaku, Kit = Kit.Ninja, Role = Role.Dps, Choice = 3, Damage = 20f, Reach = 95f, Melee = true, ExtraDesc = "+1 aftershock ring after the Twin Crush, 40 px farther out (60%)", Name = "Nunchaku", FireInterval = 0.3f,
                Desc = "Primary Fire: A four-step twin-nunchaku combo that ends in a stunning slam.\n\nAlternative Fire: Guard with spinning discs that swat shots back." },
        };

        /// The Ninja's own upgrade cards: Rare upgrades of its 1st abilities (RequiresAbility), and cards for its weapons
        /// (Weapons) or the whole class (Kit). Upgrades.All gathers every class's list (Docs/UpgradePools.md).
        internal static readonly UpgradeDef[] NinjaCards =
        {
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ShadowClone, MaxStacks = 2, Name = "Mirror Image", Desc = "Shadow Clone lasts 2 s longer and its clones have 50% more health",
                Apply = s => { s.CloneBonusTime += 2f; s.CloneHpMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SmokeBomb, MaxStacks = 2, Name = "Thick Smoke", Desc = "Smoke Bomb is 40% bigger and lasts 2 s longer",
                Apply = s => { s.SmokeRadiusMult += 0.4f; s.SmokeBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SenbonSeekers, MaxStacks = 2, Name = "Hidden Needles", Desc = "Senbon Seekers' needles hit 30% harder, and their poison stacks 2 higher",
                Apply = s => { s.SbDmgMult += 0.3f; s.SbStackBonus += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.CeremonialArmor, MaxStacks = 2, Name = "Tempered Steel", Desc = "Ceremonial Armor's plates have 50% more health",
                Apply = s => s.PlateHpMult += 0.5f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BambooGrove, MaxStacks = 2, Name = "Rhizome", Desc = "Bamboo Grove reaches 30% farther and its bleed lasts 50% longer",
                Apply = s => { s.BgRadiusMult += 0.3f; s.BgBleedMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.HealingDash, MaxStacks = 2, Name = "Morning Dew", Desc = "Healing Dash's footprints heal 50% more and are 25% bigger",
                Apply = s => { s.MistHealMult += 0.5f; s.MistRadiusMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Substitution, MaxStacks = 2, Name = "Body Double", Desc = "Substitution's shield is 60% stronger and lasts 2 s longer",
                Apply = s => { s.SubShieldMult += 0.6f; s.SubBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SpiritWard, MaxStacks = 2, Name = "Sacred Seal", Desc = "Spirit Ward is 40% bigger and lasts 3 s longer",
                Apply = s => { s.WardRadiusMult += 0.4f; s.WardBonusTime += 3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ShadowStep, MaxStacks = 2, Name = "Assassinate", Desc = "Shadow Step strikes 60% harder and reaches enemies 30% farther",
                Apply = s => { s.StepDmgMult += 0.6f; s.StepRangeMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Vanish, MaxStacks = 2, Name = "Unseen Blade", Desc = "Vanish lasts 1.5 s longer and your ambush hit deals one more times its damage",
                Apply = s => { s.VanishBonusTime += 1.5f; s.AmbushMult += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BladeFan, MaxStacks = 2, Name = "Storm of Steel", Desc = "Blade Fan throws 6 more kunai",
                Apply = s => s.FanExtraKunai += 6 },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SpiritAvatar, MaxStacks = 2, Name = "Kami Vessel", Desc = "Spirit Avatar lasts 2 s longer, and its Moon Cleave winds up 0.2 s faster",
                Apply = s => { s.SpBonusTime += Game.SpVesselTime; s.SpWindCut += Game.SpVesselWind; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ForbiddenPath, MaxStacks = 2, Name = "Forbidden Hunger", Desc = "Forbidden Path's combo hits 20% harder, and each kill heals twice as much",
                Apply = s => { s.FpDmgMult += Game.FpHungerDmg; s.FpKillHealMult += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Kit = Arena.Kit.Ninja, MaxStacks = 2, Name = "Ghost Step", Desc = "+6% move speed, and you take 30% less damage for 1 s after you dodge (1 s longer per extra copy)",
                Apply = s => { s.SpeedMult += 0.06f; s.GhostCut = 0.3f; s.GhostTime += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Kusarigama }, MaxStacks = 2, Name = "Heavy Weight", Desc = "The Kusarigama's thrown weight flies 25% farther and stuns 0.4 s longer",
                Apply = s => { s.KusariReachMult += 0.25f; s.KusariStunBonus += 0.4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.BoStaff }, MaxStacks = 2, Name = "Iron Bo", Desc = "The Bo Staff's shove knocks 40% farther, and its full spin hits 30% harder",
                Apply = s => { s.BoKnockMult += 0.4f; s.BoSpinDmgMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.FumaShuriken }, MaxStacks = 2, Name = "Long Loop", Desc = "The Fuma Shuriken's thrown loop reaches 20% farther, and its Orbit circle is 20% wider",
                Apply = s => { s.FumaLoopMult += 0.2f; s.FumaOrbitMult += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.MedicineDarts }, MaxStacks = 2, Name = "Potent Dose", Desc = "Each Medicine Dart's poison and healing stack is 30% stronger",
                Apply = s => { s.DoseMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.SpiritFan }, MaxStacks = 2, Name = "Gale Fan", Desc = "The Spirit Fan's gust reaches 25% farther and heals 30% more",
                Apply = s => { s.FanReachMult += 0.25f; s.FanHealMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Ofuda }, MaxStacks = 2, Name = "Blessed Paper", Desc = "Ofuda talisman bursts and Blast Ofudas 25% wider, talismans heal 30% more",
                Apply = s => { s.CharmBurstMult += 0.25f; s.CharmHealMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Shuriken }, MaxStacks = 2, Name = "Barbed Stars", Desc = "Stuck stars come back 1.5 s sooner, and their return hit is 40% harder",
                Apply = s => { s.StarStickCut += 1.5f; s.StarReturnMult += 0.4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Katana }, MaxStacks = 2, Name = "Honed Edge", Desc = "The Momentum Burst shoves 30% farther, and caltrops last 2 s longer",
                Apply = s => { s.BurstPushMult += 0.3f; s.CaltropBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Kunai }, MaxStacks = 2, Name = "Shadow Mark", Desc = "The hit after a Kunai blink deals 40% more, and marks last 2 s longer",
                Apply = s => { s.KunaiBlinkMult += 0.4f; s.KunaiMarkBonus += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Nunchaku }, MaxStacks = 2, Name = "Iron Links", Desc = "The Twin Windmill's discs are 25% wider, and each shot they swat adds 2 more to the Twin Counter",
                Apply = s => { s.NunDiscMult += 0.25f; s.NunSwatBonus += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Nodachi }, MaxStacks = 2, Name = "Tamahagane", Desc = "The Nodachi's sweeps and Tenchi's ring reach 15% farther, and Mamori's jabs block shots in a 30° wider cone",
                Apply = s => { s.NodReachMult += 0.15f; s.NodConeBonus += 30f; } },
        };

        /// The Ninja's abilities and ultimates. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] NinjaAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.ShadowClone, Kit = Kit.Ninja, Role = Role.Tank, Choice = 0,
                Name = "Shadow Clone", Cooldown = 14f, Desc = "A wall of clones that every enemy attacks.",
                Capstone = "Every clone has double the health.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.SmokeBomb, Kit = Kit.Ninja, Role = Role.Tank, Choice = 1,
                Name = "Smoke Bomb", Cooldown = 14f, Desc = "A smoke cloud that slows enemy bullets and shields allies.",
                Capstone = "Enemies inside the smoke are also slowed by 40%.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.CeremonialArmor, Kit = Kit.Ninja, Role = Role.Tank, Choice = 2,
                Name = "Ceremonial Armor", Cooldown = 2.5f, Desc = "Armour plates circle you, blocking bullets and shoving enemies.",
                Capstone = "A plate that breaks bursts for 30 damage to enemies within 60 px.",
                Bot = b => b.Me.PlateN < Game.CerePlateMax,
            },
            new AbilityDef
            {
                Id = AbilityId.BambooGrove, Kit = Kit.Ninja, Role = Role.Tank, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (BambooGrove.cs)
                Name = "Bamboo Grove", Cooldown = BgCooldown, Desc = "Bamboo bursts from the ground around you, stunning enemies, then bleeding them.",
                Capstone = "The bamboo fence lasts 6 s.",
                Bot = b => b.NearEnemies >= 3 && b.TargetD < BgRadius,
            },
            new AbilityDef
            {
                Id = AbilityId.HealingDash, Kit = Kit.Ninja, Role = Role.Healer, Choice = 0,
                Name = "Healing Dash", Cooldown = 15f, Desc = "Sprint, leaving footprints that heal you and allies.",
                Capstone = "The sprint lasts 4.5 s.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.Substitution, Kit = Kit.Ninja, Role = Role.Healer, Choice = 1,
                Name = "Substitution", Cooldown = 12f, Desc = "Swap places with an ally and shield them.",
                Capstone = "You and your ally are both invulnerable for 1 s after the swap.",
                Bot = b => b.AllyDown || b.AllyHurt && b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.SpiritWard, Kit = Kit.Ninja, Role = Role.Healer, Choice = 2,
                Name = "Spirit Ward", Cooldown = 18f, Desc = "Plant a talisman that absorbs damage for allies, then heals them.",
                Capstone = "The ward follows you.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.ShadowStep, Kit = Kit.Ninja, Role = Role.Dps, Choice = 0, CapstoneCharge = true,
                Name = "Shadow Step", Cooldown = 7f, Desc = "Teleport behind an enemy and strike it hard.",
                Capstone = "+1 charge.",
                Bot = b => b.HaveTarget && b.TargetD > 120f && b.TargetD < 440f || b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Vanish, Kit = Kit.Ninja, Role = Role.Dps, Choice = 1,
                Name = "Vanish", Cooldown = 16f, Desc = "Enemies lose track of you and your next hit deals double.",
                Capstone = "Erases enemy bullets around you when you vanish.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.BladeFan, Kit = Kit.Ninja, Role = Role.Dps, Choice = 2,
                Name = "Blade Fan", Cooldown = 12f, Desc = "Spinning kunai cut enemies around you, then fly out in a ring.",
                Capstone = "Throws twice as many kunai.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 260f,
            },
            new AbilityDef
            {
                Id = AbilityId.SenbonSeekers, Kit = Kit.Ninja, Role = Role.Dps, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (SenbonSeekers.cs)
                Name = "Senbon Seekers", Cooldown = SbCooldown, Desc = "Three poisoned needles circle you and fly at enemies that come close. Stacks to 9.",
                Capstone = "Holds up to 12 needles.",
                Bot = b => !SbFull(b.Me),
            },
            new AbilityDef
            {
                Id = AbilityId.ThousandClones, Kit = Kit.Ninja, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Thousand Clones", Cooldown = 70f, Desc = "Twelve clones appear all over the map and draw every enemy.",
                Capstone = "Twenty clones.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.BlossomStorm, Kit = Kit.Ninja, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Blossom Storm", Cooldown = 80f, Desc = "Petals heal the whole party and erase enemy bullets.",
                Capstone = "Lasts 8 s.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.DeathLotus, Kit = Kit.Ninja, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Death Lotus", Cooldown = 65f, Desc = "A flurry of cuts on up to 8 enemies near your cursor.",
                Capstone = "Strikes up to 14 enemies.",
            },
        };

        /// Ninja abilities and ultimates. Returns false if `id` isn't one.
        bool NinjaAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                case AbilityId.ShadowClone:
                {
                    SpawnCloneWall(p, CloneWallTime + s.CloneBonusTime, pw * s.CloneHpMult * (cap ? 2f : 1f));  // CloneWall.cs
                    Sounds.Add(Sfx.Summon);
                    return true;
                }
                case AbilityId.SmokeBomb:
                {
                    var z = AddZone(ZoneKind.Smoke, p, p.Pos, SmokeRadius * s.SmokeRadiusMult, SmokeTime + s.SmokeBonusTime);
                    z.DamageCut = SmokeCut;
                    if (cap)
                        z.ExtraSlow = 0.4f;
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.4f, Sfx.Throw);
                    return true;
                }
                case AbilityId.CeremonialArmor:  // Ceremonial Armor (CeremonialArmor.cs)
                    SummonCerePlate(p, pw, cap);
                    return true;
                case AbilityId.BambooGrove:  // Bamboo Grove (BambooGrove.cs)
                    CastBambooGrove(p, pw, cap);
                    return true;
                case AbilityId.SenbonSeekers:  // Senbon Seekers (SenbonSeekers.cs)
                    CastSenbon(p, pw, cap);
                    return true;
                case AbilityId.HealingDash:
                {
                    p.DashT = cap ? HealDashTimeMastered : HealDashTime;
                    p.DashHeal = s.MistHealMult * pw;
                    p.DashRadius = FootprintRadius * s.MistRadiusMult;
                    p.DashLast = p.Pos;
                    DropFootprint(p, p.Aim);
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Mist).Color, 80f, 0.45f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.Substitution:
                    Substitute(p, target, SubShield * s.SubShieldMult * pw, SubShieldTime + s.SubBonusTime, cap);
                    return true;
                case AbilityId.SpiritWard:
                {
                    var z = AddZone(ZoneKind.Ward, p, ClampToArena(target, 10f), WardRadius * s.WardRadiusMult, WardTime + s.WardBonusTime);
                    z.Follow = cap;
                    if (cap)
                        z.Pos = p.Pos;
                    z.Soak = z.SoakMax = WardSoakMax * pw;
                    BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius, 0.45f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.ShadowStep:
                {
                    // The player's own game moved them behind the enemy (LocalControl); strike whoever is right there.
                    Enemy best = null;
                    foreach (var e in AllFoes(by: p.Id))
                        if (e.SpawnT <= 0f && e.Pos.CloserThan(target, e.Radius + p.Def.BodyR + 60f) && (best == null || e.Pos.DistanceSq(target) < best.Pos.DistanceSq(target)))
                            best = e;
                    if (best != null)
                    {
                        float dmg = StepDamage * s.DmgMult * s.StepDmgMult * NinjaHitMult(p.Id, Defs.KnightSlash, best);
                        HurtEnemy(best, dmg, p.Id);
                        ModHit(best, p.Id, dmg, Defs.KnightSlash, (best.Pos - target).Normalized());
                        BroadcastFx(FxKind.Slash, target, best.Pos + (best.Pos - target).Normalized() * best.Radius, new Rgba(0.75f, 0.6f, 1f), 0.12f, 0.2f, Sfx.NinjaStepStrike);
                    }
                    // The smoke look (Game/ShadowStepLook.cs) starts where they stood; the plain ring and slash above stay for Visual effects off.
                    BroadcastFx(FxKind.ShadowStep, StepFrom ?? p.Pos, target, new Rgba(0.72f, 0.58f, 1f), p.Id * 1000f + (best != null ? best.Radius : 0f), ShadowStepFxTime, Sfx.NinjaStepCast);
                    return true;
                }
                case AbilityId.Vanish:
                    p.VanishT = VanishTime + s.VanishBonusTime;
                    p.AmbushT = p.VanishT + AmbushGrace;
                    p.VanishFrom = p.Pos;
                    foreach (var e in Enemies.Values)
                        if (e.TargetId == p.Id)
                            e.RetargetT = 0f;  // they look for someone else
                    if (cap)
                        EraseEnemyBullets(p.Pos, 160f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.45f, 0.4f, 0.65f), 45f, 0.35f, Sfx.NinjaVanish);
                    return true;
                case AbilityId.BladeFan:
                {
                    p.OrbitN = (12 + s.FanExtraKunai) * (cap ? 2 : 1);
                    p.OrbitT = OrbitTime;
                    p.OrbitClock = 0f;
                    p.OrbitCut.Clear();
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.75f, 0.65f, 1f), BladeOrbitR + 10f, 0.3f, Sfx.NinjaFanOpen);
                    return true;
                }

                // Ultimates
                case AbilityId.ThousandClones:
                {
                    int n = cap ? ThousandClonesCapstone : ThousandClonesCount;
                    foreach (var spot in CloneSpots(p, n))
                        SpawnClone(p, spot, -1f, s.DmgMult);  // life -1: no timer, they last until killed or the round ends
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(MinionKind.Clone).Color, 90f, 0.5f, Sfx.Summon);
                    return true;
                }
                case AbilityId.BlossomStorm:
                    p.UltT = cap ? 8f : BlossomTime;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.7f, 0.85f), 160f, 0.6f, Sfx.SpellHoly);
                    return true;
                case AbilityId.DeathLotus:
                {
                    var marks = new List<Enemy>();
                    foreach (var e in AllFoes(by: p.Id))
                        if (e.SpawnT <= 0f && e.Pos.CloserThan(target, LotusRange))
                            marks.Add(e);
                    marks.Sort((a, b) => a.Pos.DistanceSq(target).CompareTo(b.Pos.DistanceSq(target)));
                    int most = cap ? 14 : 8;
                    if (marks.Count > most)
                        marks.RemoveRange(most, marks.Count - most);
                    float gap = cap ? LotusStaggerCap : LotusStagger;  // the cuts show one after another; the damage lands now
                    BroadcastFx(FxKind.DeathLotus, p.Pos, new V2(-1f, p.Id), LotusCrimson, marks.Count, marks.Count * gap + 1.5f, Sfx.NinjaLotusCast);
                    for (int i = 0; i < marks.Count; i++)
                    {
                        var e = marks[i];
                        float dmg = LotusDamage * s.DmgMult;
                        HurtEnemy(e, dmg, p.Id);
                        ModHit(e, p.Id, dmg, Defs.KnightSlash, (e.Pos - p.Pos).Normalized());
                        BroadcastFx(FxKind.DeathLotus, e.Pos, new V2(i * gap, p.Id), LotusCrimson, e.Radius, i * gap + LotusStain, Sfx.None);
                    }
                    p.InvulnT = Math.Max(p.InvulnT, 1f);
                    return true;
                }
            }
            return false;
        }

        /// Thousand Clones: n spots spread over the whole arena, kept apart from each other (the gap relaxes if the map
        /// is crowded) and off the walls and pillars.
        List<V2> CloneSpots(Player p, int n)
        {
            var spots = new List<V2>();
            for (int tries = 0; spots.Count < n && tries < 4000; tries++)
            {
                var c = PushOutOfSolids(new V2(Rand(ArenaMin.X + CloneEdge, ArenaMax.X - CloneEdge), Rand(ArenaMin.Y + CloneEdge, ArenaMax.Y - CloneEdge)), 20f);
                if (RoomsOn && !CurrentWorld.Areas[CurrentWorld.AreaAt(c)].Contains(c))
                    continue;  // (the Expanding map has gaps between its rooms)
                float gap = CloneSpacing - Math.Min(60f, tries / 60f);
                if (c.CloserThan(p.Pos, CloneNinjaGap))
                    continue;
                bool ok = true;
                foreach (var o in spots)
                    if (o.CloserThan(c, gap)) { ok = false; break; }
                if (ok)
                    spots.Add(c);
            }
            while (spots.Count < n)  // (a map too crowded to space them out: ring the rest around the Ninja)
                spots.Add(ClampToArena(p.Pos + RandDir() * 100f, 10f));
            return spots;
        }

        /// A Shadow Clone: enemies go for it. It soaks bullets, bursts when killed, and (life < 0) never times out
        /// (burst = damage multiplier).
        void SpawnClone(Player p, V2 pos, float life, float burst)
        {
            var m = SpawnMinion(MinionKind.Clone, p, pos, life);
            m.Duration = Math.Max(0f, life);
            m.Power = burst;
            m.Aim = p.Aim;
        }

        /// Substitution: swap places with the ally nearest the cursor and shield them (alone: slip toward the
        /// cursor and shield yourself). Each player's own game moves them, so the host tells them where to go.
        void Substitute(Player p, V2 target, float shield, float time, bool mastered)
        {
            Player ally = null;
            foreach (var q in Players.Values)
                if (q.Id != p.Id && !q.Downed && (ally == null || q.Pos.DistanceSq(target) < ally.Pos.DistanceSq(target)))
                    ally = q;
            V2 mine = p.Pos;
            var guard = ally ?? p;
            if (ally != null)
            {
                MovePlayer(p, ally.Pos);
                MovePlayer(ally, mine);
            }
            else
                MovePlayer(p, ClampToArena(p.Pos + (target - p.Pos).LimitLength(BlinkRange), p.Def.BodyR));
            guard.ShieldHp = Math.Max(guard.ShieldHp, shield);
            guard.ShieldT = time;
            if (mastered)
            {
                p.InvulnT = Math.Max(p.InvulnT, 1f);
                guard.InvulnT = Math.Max(guard.InvulnT, 1f);
            }
            castHits++;
            BroadcastFx(FxKind.Ring, mine, new Rgba(0.55f, 1f, 0.8f), 35f, 0.35f, Sfx.Blink);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.55f, 1f, 0.8f), 35f, 0.35f, Sfx.None);
        }

        /// Host: move a player. Players move themselves, so their own game is told (the host's player directly).
        void MovePlayer(Player q, V2 pos)
        {
            q.Pos = pos;
            q.Disp = pos;
            var w = Proto.Begin(Msg.Teleport);
            w.Write(q.Id);
            w.Write(pos);
            session.Broadcast(Proto.End(w));
        }

        /// Shadow Step (your own game, as you press it): just behind the enemy nearest your cursor, if one is in
        /// reach; otherwise a short step toward the cursor.
        V2 ShadowStepLanding(Player me, V2 aimAt)
        {
            float range = StepRange * me.Stats.StepRangeMult;
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (!IsSpawning(e) && e.Pos.WithinReach(me.Pos, range + e.Radius) && (best == null || e.Pos.DistanceSq(aimAt) < best.Pos.DistanceSq(aimAt)))
                    best = e;
            if (best == null)
                return TeleportLanding(me.Pos, me.Pos + (aimAt - me.Pos).LimitLength(BlinkRange), me.Def.BodyR);
            V2 away = (best.Pos - me.Pos).IsZero ? V2.FromAngle(me.Aim) : (best.Pos - me.Pos).Normalized();
            return TeleportLanding(me.Pos, best.Pos + away * (best.Radius + me.Def.BodyR + 8f), me.Def.BodyR);
        }

        /// Where a teleport (Shadow Step, Blink) really lands: on the floor, never in a wall, pillar or crate (whose
        /// push-out could put you on its far side, off the map) or in the gaps between the Expanding map's rooms. Off
        /// the floor, the spot backs up toward where you stood until it's on it.
        V2 TeleportLanding(V2 from, V2 to, float r)
        {
            const int Steps = 16;
            for (int i = 0; i <= Steps; i++)
            {
                V2 at = ClampToArena(V2.Lerp(to, from, i / (float)Steps), r);
                if ((Props.Count > 0 || LayoutOn) && PushOutOfProps(at, r).FartherThan(at, 0.5f))
                    continue;
                if (RoomsOn && !CurrentWorld.Areas[CurrentWorld.AreaAt(at)].Contains(at))
                    continue;
                return at;
            }
            return from;
        }

        // --- Enemies and bullets --------------------------------------------------------

        readonly List<Minion> clones = new List<Minion>();
        readonly List<Zone> smokes = new List<Zone>();

        /// Where an enemy aims: at the nearest Shadow Clone if there is one, and at the spot it last saw you if
        /// you've vanished.
        V2 NinjaTarget(Enemy e, Player target, V2 tpos)
        {
            Minion best = null;
            foreach (var m in clones)
                if (best == null || m.Pos.DistanceSq(e.Pos) < best.Pos.DistanceSq(e.Pos))
                    best = m;
            if (best != null)
                return best.Pos;
            if (target != null && target.VanishT > 0f)
                return target.VanishFrom;
            return tpos;
        }

        /// Enemy bullets inside a Smoke Bomb move at half speed (on every peer, from the replicated zones).
        float SmokeSpeed(Bullet b)
        {
            if (smokes.Count == 0 || !Defs.IsEnemyShot(b.Kind))
                return 1f;
            foreach (var z in smokes)
                if (b.Pos.DistanceSq(z.Pos) < z.Radius * z.Radius)
                    return SmokeBulletSpeed;
            return 1f;
        }

        // --- Bo Staff, Twin Blades and the weapon-action key: weapons that move you ---------------------------------------------
        //
        // As with Lunge, the player's own game moves them (LocalWeaponDash) and tells the host (Msg.WeaponDash),
        // which does the damage (HostWeaponDash).

        const byte DashWhirlOn = 1, DashWhirlOff = 10, DashStrike = 2, DashBlink = 3, DashRecall = 4, DashKatanaStance = 5, DashShove = 6, DashJab = 7, DashHammerSlam = 11, DashLance = 12, DashCast = 13, DashHook = 14, DashNade = 15, DashFlourish = 16, DashRapierOn = 17, DashRapierOff = 18, DashPullOn = 19, DashPullOff = 20, DashDouble = 21, DashMine = 22, DashGas = 23, DashFumaStance = 24, DashKnuckleStance = 25, DashLock = 26, DashSpin = 27, DashBoth = 28, DashBoneWall = 29, DashDrum = 34;  // (30-33: DashVolley in Stonespear.cs, DashPack in WolfPack.cs, DashFork in ArcLock.cs, DashStorm in Nimbus.cs; 35-36: DashHoneyOn/Off in HoneybeeStaff.cs; 37: DashNunGuard in Nunchaku.cs; 38: DashTempest in WindClash.cs; 39: DashEmbolus in EmbolusSpears.cs; 40-41: DashBraceOn/Off in BulwarkBarrier.cs; 58-59: DashPikeOn/Off in Halberd.cs; 42: DashWhip in PistolWhip.cs; 43: free (was the Strider); 44: DashZero in FrostLance; 45: DashSympathy in BloodBrawler.cs; 46: DashTide in BloodBlast.cs; 47: DashIgnite in Chalk.cs; 48: DashNodStance in Nodachi.cs; 49: DashWyvernSlam in WyvernStaff.cs; 50: DashLaser in TwinPistols.cs; 51: DashVgStance in Vowguard.cs; 52-54: DashShardOn/Off, DashBladeRecall in Arcanist.cs; 55: DashHeatDump in RailBeam.cs; 56: DashArcCannon in StaticShock.cs; 57: DashScatter in BowFocus.cs; 60: DashBolt in LivingBolt.cs)

        // --- Spirit Fan: push and pull -----------------------------------------------------------

        public const float FanShove = 60f, FanReachSlack = 30f, FanCone = 0.45f;

        /// The Spirit Fan's gust shoves enemies in front of you away (shove > 0) or draws them in (shove < 0, never
        /// into you). Bosses and heavies don't budge.
        void FanEnemies(Player p, float aim, float reach, float shove)
        {
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || Heavy(e))
                    continue;
                float d = e.Pos.DistanceTo(p.Pos);
                if (d - e.Radius > reach || AngleTo(p.Pos, e.Pos, aim) > FanCone + (BodySlack(d, e.Radius)))
                    continue;
                V2 away = d > 1f ? (e.Pos - p.Pos) / d : V2.FromAngle(aim);
                WindHit(e, shove >= 0f ? away : -away);  // Wind.cs
                float to = Math.Max(e.Radius + p.Def.BodyR + 6f, d + shove);
                KnockEnemy(e, ClampToArena(p.Pos + away * to, e.Radius) - e.Pos);  // slides (Knock.cs)
            }
        }

        // --- Shuriken: stars that stick, recalled -------------------------------------------------

        public const float StarStick = 5f, RecallSpeed = 700f, RecallBack = 0.5f;  // stuck this long (then it comes back on its own); recalled stars fly back this fast, hitting what they pass for this share
        /// The angle between stars in a fan (was 0.22, when the side stars missed a boss past about 240 px; now all three hit one out to about 650).
        public const float StarFan = 0.08f;
        public const int StarCap = 15;

        class StuckStar
        {
            public int Enemy, Owner;
            public float Dmg, T;
        }

        readonly List<StuckStar> stuckStars = new List<StuckStar>();

        void StarStuck(Bullet b, Enemy e)
        {
            if (!b.Sticks)
                return;
            int mine = 0;
            foreach (var st in stuckStars)
                if (st.Owner == b.Owner)
                    mine++;
            if (mine >= StarCap)
                return;
            var thrower = PlayerOrNull(b.Owner);
            stuckStars.Add(new StuckStar { Enemy = e.Id, Owner = b.Owner, Dmg = b.Dmg, T = StarStick - (thrower != null ? thrower.Stats.StarStickCut : 0f) });  // (Barbed Stars: back sooner)
        }

        void TickStuckStars(float dt)
        {
            foreach (var e in Enemies.Values)
                e.StarsStuck = 0;
            for (int i = stuckStars.Count - 1; i >= 0; i--)
            {
                var st = stuckStars[i];
                Enemy e;
                if (!TryFoe(st.Enemy, out e) || e.Hp <= 0f)
                {
                    stuckStars.RemoveAt(i);
                    continue;
                }
                if ((st.T -= dt) <= 0f)
                {
                    // Its time's up: it comes back by itself, for the same second hit as a recall.
                    stuckStars.RemoveAt(i);
                    Player owner;
                    if (Players.TryGetValue(st.Owner, out owner) && !owner.Downed)
                        ReturnStar(st, e, owner);
                    continue;
                }
                e.StarsStuck++;
            }
        }

        /// Recalled: every star you have stuck in enemies rips out (hitting again) and flies back to you, cutting
        /// through whatever it passes.
        void RecallStars(Player p)
        {
            int n = 0;
            for (int i = stuckStars.Count - 1; i >= 0; i--)
            {
                var st = stuckStars[i];
                if (st.Owner != p.Id)
                    continue;
                stuckStars.RemoveAt(i);
                Enemy e;
                if (!TryFoe(st.Enemy, out e) || e.Hp <= 0f)
                    continue;
                ReturnStar(st, e, p);
                n++;
            }
            if (n > 0)
                BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.StarShot), 30f, 0.25f, Sfx.SwingLight);
        }

        /// A stuck star rips out of its enemy (another full hit) and flies back to its thrower, cutting what it passes.
        void ReturnStar(StuckStar st, Enemy e, Player p)
        {
            float rip = st.Dmg * p.Stats.StarReturnMult;  // Barbed Stars: the return hits harder
            WeaponHurt(e, rip, p.Id);
            ModHit(e, p.Id, rip, Defs.StarShot, default(V2));
            V2 to = p.Pos - e.Pos;
            float dist = Math.Max(1f, to.Length);
            var b = SpawnBullet(Defs.StarShot, e.Pos, to / dist * RecallSpeed / p.Stats.ShotSpeedMult, 0f, dist / RecallSpeed, rip * RecallBack, p.Id);
            b.Pierce += 99;
            b.LastHit = e.Id;
        }

        // --- Medicine Darts: stacking doses --------------------------------------------------------

        public const float DoseHeal = 2f, DoseTime = 3f, PoisonDps = 2f, PoisonTime = 3f, DoseStacks = 5f;  // a second, per dart; each dart renews it, stacking to DoseStacks

        /// A Medicine Dart through a hurt ally: another dose of healing over time (it doesn't go on).
        bool DoseAlly(Bullet b)
        {
            foreach (var q in Players.Values)
            {
                float r = q.Def.BodyR + b.Radius;
                if (q.Id == b.Owner || q.Downed || q.Hp >= q.MaxHp || b.Pos.DistanceSq(q.Pos) >= r * r)
                    continue;
                float each = DoseHeal * b.Dmg / Defs.WeaponOf(WeaponId.MedicineDarts).Damage * DoseMultOf(b.Owner);  // damage upgrades dose harder too (Potent Dose)
                q.DoseHps = Math.Min(q.DoseHps + each, each * DoseStacks);
                q.DoseT = DoseTime;
                q.DoseBy = b.Owner;
                return true;
            }
            return false;
        }

        float DoseMultOf(int owner) => Players.ContainsKey(owner) ? Players[owner].Stats.DoseMult : 1f;

        /// A Medicine Dart in an enemy: another dose of poison.
        void PoisonHit(Bullet b, Enemy e)
        {
            if (!b.Medicine)
                return;
            float each = PoisonDps * b.Dmg / Defs.WeaponOf(WeaponId.MedicineDarts).Damage * DoseMultOf(b.Owner);
            e.PoisonDps = Math.Min(e.PoisonDps + each, each * DoseStacks);
            e.PoisonT = PoisonTime;
            e.PoisonBy = b.Owner;
        }

        void TickPoison(Enemy e, float dt)
        {
            if (e.PoisonT <= 0f)
                return;
            if ((e.PoisonT -= dt) <= 0f)
                e.PoisonDps = 0f;
            else if (e.SpawnT <= 0f)
                DotDamage(e, e.PoisonDps * dt, e.PoisonBy);
        }

        // --- Kunai: the shadow kunai ---------------------------------------------------------------

        public const int KunaiShadowEvery = 1;
        public const float KunaiAmbush = 1.5f;  // after a blink, your next hit within this long lands double
        public const float KunaiMarkTime = 3f;  // the mark lasts this long; the weapon-action key blinks you to it

        /// A shadow kunai hit something or ran out: its mark is where you can blink to.
        void ShadowKunaiLanded(Bullet b, V2 at)
        {
            Player p;
            if (!b.ShadowKunai || !Players.TryGetValue(b.Owner, out p))
                return;
            p.KunaiMark = ClampToArena(at, p.Def.BodyR);
            p.KunaiMarkT = KunaiMarkTime + p.Stats.KunaiMarkBonus;  // (Shadow Mark: longer)
        }


        static readonly Rgba Steel = new Rgba(0.75f, 0.78f, 0.85f);

        // Local: this machine's player's weapon dashes.
        float reelCd, staffJabLocalCd, twinNadeLocalCd, fumaSwitchLocalCd;
        bool wasWeaponAction;

        /// Your own game, every tick: weapon secondaries go on the weapon-action key (Keybinds, Left Alt by default;
        /// PlayerInput.WeaponAction). The Bo Staff shoves; the Kusarigama whirls its weight while held (Kusarigama.cs); Twin Blades lunge when the combo is full; the Kunai blinks to its mark; the Shuriken
        /// stars come back.
        void LocalWeaponDash(Player me, V2 move, V2 aimAt, bool weaponAction, float dt)
        {
            reelCd -= dt;
            staffJabLocalCd -= dt;
            twinNadeLocalCd -= dt;
            bool pressed = weaponAction && !wasWeaponAction, released = !weaponAction && wasWeaponAction;
            wasWeaponAction = weaponAction;
            LocalTwinStep(me, dt);  // the Twin Blades' steps forward (TwinBlades.cs)
            LocalGreatStep(me);     // the Greatsword's hop back and leap (GreatswordChain.cs)
            LocalLightStep(me, dt);  // the Lightbringer's steps forward (Lightbringer.cs)
            if (LocalSunlance(me, aimAt, pressed))
                return;  // the Lightbringer's Sunlance (Lightbringer.cs)
            LocalLanceStep(me, dt);  // the Lance's steps forward (Lance.cs)
            if (LocalLanceHook(me, aimAt, pressed))
                return;  // the Lance's Hook & Haul (Lance.cs)
            if (LocalHalberd(me, aimAt, pressed, released))
                return;  // the Halberd's Set the Pike, held (Halberd.cs)
            if (LocalBulwark(me, aimAt, pressed, released, dt))
                return;  // the Bulwark Barrier's Brace, held, and its bash and steps moving you (BulwarkBarrier.cs)
            LocalCenserTick(dt);    // Censer Cast's cooldown (Censer.cs)
            if (LocalArcFork(me, pressed, aimAt, dt))
                return;  // Arc Lightning's Forked Volley (ArcLock.cs)
            if (LocalNimbusStorm(me, aimAt, pressed, dt))
                return;  // the Nimbus's Eye of the Storm (Nimbus.cs)
            if (LocalChalkIgnite(me, aimAt, pressed, dt))
                return;  // the Spirit Chalk's ignite (Chalk.cs)
            if (LocalTempest(me, aimAt, pressed && !me.Downed && me.StunT <= 0f, dt))
                return;  // Wind Clash's Tempest (WindClash.cs)
            if (LocalWyvernSlam(me, aimAt, pressed && !me.Downed && me.StunT <= 0f, dt))
                return;  // the Wyvern Staff's Wyrm Slam (WyvernStaff.cs)
            if (LocalTide(me, aimAt, pressed && !me.Downed && me.StunT <= 0f, dt))
                return;  // the Blood Blast's Crimson Tide (BloodBlast.cs)
            if (LocalZero(me, aimAt, pressed && !me.Downed && me.StunT <= 0f, dt))
                return;  // the Frost Lance's Absolute Zero (FrostLance.cs)
            if (LocalHeatDump(me, aimAt, pressed && !me.Downed && me.StunT <= 0f, dt))
                return;  // the Railgun's Heat Dump (RailBeam.cs)
            if (LocalKatana(me, aimAt, pressed, dt))
                return;  // the Katana's stance switch and steps (Katana.cs)
            if (LocalKnuckleDash(me, aimAt, pressed, dt))
                return;  // the Arc Knuckles' stance swap, and their dashes moving you (ArcKnuckles.cs)
            if (LocalArcanistAlt(me, pressed, released, aimAt))
                return;  // the Orbit Shards' Shard Guard, held, and the Dancing Blades' recall (Arcanist.cs)
            if (LocalHoneySpray(me, pressed, released, aimAt))
                return;  // the Honeybee Staff's honey spray, held (HoneybeeStaff.cs)
            if (LocalNunGuard(me, aimAt, pressed, dt))
                return;  // the Nunchaku's guard switch (Nunchaku.cs)
            if (LocalVowguard(me, aimAt, pressed, dt))
                return;  // the Vowguard's stance switch and its dashes (Vowguard.cs)
            if (LocalNodachi(me, aimAt, pressed, dt))
                return;  // the Nodachi's stance switch, Nagi's walk and Hiki-zuki's hop (Nodachi.cs)
            if (me.Weapon.Id == WeaponId.Kusarigama && (pressed || released))  // the Kusarigama's whirl: held, so both edges go to the host (Kusarigama.cs)
            {
                byte whirlKind = pressed ? DashWhirlOn : DashWhirlOff;
                if (IsHost)
                    HostWeaponDash(MyId, whirlKind, aimAt);
                else
                {
                    var ww = Proto.Begin(Msg.WeaponDash);
                    ww.Write(whirlKind);
                    ww.Write(aimAt);
                    session.SendToHost(Proto.End(ww));
                }
                return;
            }
            if ((me.Weapon.Id == WeaponId.RepulsionCannon || me.Weapon.Id == WeaponId.BloodLash) && (pressed || released))  // the Repulsion Cannon's Undertow and the Blood Lash's Martyr's Leash: held, so both edges go to the host (Repulsion.cs, BloodLash.cs)
            {
                byte pullKind = pressed ? DashPullOn : DashPullOff;
                if (IsHost)
                    HostWeaponDash(MyId, pullKind, aimAt);
                else
                {
                    var pw = Proto.Begin(Msg.WeaponDash);
                    pw.Write(pullKind);
                    pw.Write(aimAt);
                    session.SendToHost(Proto.End(pw));
                }
                return;
            }
            if ((me.Weapon.Id == WeaponId.TwinBlades || me.Weapon.Id == WeaponId.Greatsword) && pressed && fangLocalCd <= 0f && !me.Downed && me.StunT <= 0f)
            {
                fangLocalCd = me.Weapon.Id == WeaponId.Greatsword ? WheelCooldown : FangCooldown;  // Twin Fang (TwinBlades.cs) or the Moonwheel (GreatswordChain.cs): no move, the host throws at the cursor
                if (IsHost)
                    HostWeaponDash(MyId, DashStrike, aimAt);
                else
                {
                    var fw = Proto.Begin(Msg.WeaponDash);
                    fw.Write(DashStrike);
                    fw.Write(aimAt);
                    session.SendToHost(Proto.End(fw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.Rapier && pressed)
            {
                // Riposte (Rapier.cs): one press switches the stance on, the next drops it; a click while it is up lunges; the host decides which (the Flourish is still there: DashFlourish)
                byte rapierKind = DashRapierOn;
                if (IsHost)
                    HostWeaponDash(MyId, rapierKind, aimAt);
                else
                {
                    var rw = Proto.Begin(Msg.WeaponDash);
                    rw.Write(rapierKind);
                    rw.Write(aimAt);
                    session.SendToHost(Proto.End(rw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.Censer && pressed && censerLocalCd <= 0f && me.Embers > 0 && !me.Downed && me.StunT <= 0f)
            {
                censerLocalCd = CastCooldown;  // Censer Cast (Censer.cs): no move, the host throws the head at the cursor
                if (IsHost)
                    HostWeaponDash(MyId, DashCast, aimAt);
                else
                {
                    var cw = Proto.Begin(Msg.WeaponDash);
                    cw.Write(DashCast);
                    cw.Write(aimAt);
                    session.SendToHost(Proto.End(cw));
                }
                return;
            }
            fumaSwitchLocalCd -= dt;
            if (me.Weapon.Id == WeaponId.FumaShuriken && pressed && fumaSwitchLocalCd <= 0f && !me.Downed && me.StunT <= 0f)
            {
                fumaSwitchLocalCd = FumaSwitchCd;  // the Fuma Shuriken's stance switch (FumaOrbit.cs)
                if (IsHost)
                    HostWeaponDash(MyId, DashFumaStance, aimAt);
                else
                {
                    var fsw = Proto.Begin(Msg.WeaponDash);
                    fsw.Write(DashFumaStance);
                    fsw.Write(aimAt);
                    session.SendToHost(Proto.End(fsw));
                }
                return;
            }
            if (me.Downed || DashT > 0f || me.StunT > 0f)
            {
                LocalTankAlt(me, aimAt, false, dt);  // (just the cooldowns)
                LocalDrumSwap(me, aimAt, false, dt);
                LocalPackMerge(me, false, aimAt, dt);
                return;
            }
            if (LocalTankAlt(me, aimAt, pressed, dt))
                return;  // the Tank Soldier's Double Barrel, Proximity Shell and Tear Gas (TankAlts.cs)
            if (LocalDrumSwap(me, aimAt, pressed, dt))
                return;  // the Grenade Launcher's smoke canister (GrenadeLauncher.cs)
            if (LocalPackMerge(me, pressed, aimAt, dt))
                return;  // the Wolf Whistle's alpha (WolfPack.cs)
            starSwitchLocalCd -= dt;
            if (me.Weapon.Id == WeaponId.Shuriken && pressed && starSwitchLocalCd <= 0f)
            {
                starSwitchLocalCd = StarSwitchCd;  // the fan to the Great Star and back (GreatStar.cs)
                if (IsHost)
                    HostWeaponDash(MyId, DashRecall, me.Pos);  // no move: the stars come back to you
                else
                {
                    var rw = Proto.Begin(Msg.WeaponDash);
                    rw.Write(DashRecall);
                    rw.Write(me.Pos);
                    session.SendToHost(Proto.End(rw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.Kunai && pressed && me.KunaiMarkT > 0f)
            {
                // Blink to the shadow kunai's mark (like Blink, at once); the host readies a double-damage hit.
                V2 mark = me.KunaiMark;
                AddFx(FxKind.Ring, me.Pos, new Rgba(0.45f, 0.4f, 0.65f), 26f, 0.3f);
                me.Pos = mark;
                if (IsHost)
                    HostWeaponDash(MyId, DashBlink, mark);  // (it uses up the mark; on the host your player is the host's own)
                else
                {
                    var bw = Proto.Begin(Msg.WeaponDash);
                    bw.Write(DashBlink);
                    bw.Write(mark);
                    session.SendToHost(Proto.End(bw));
                }
                me.KunaiMarkT = 0f;  // used (the host's next snapshot agrees)
                return;
            }
            if (LocalStoneVolley(me, pressed, aimAt))
                return;  // the Stonespear's volley (Stonespear.cs)
            if (LocalEmbolusBlast(me, pressed, aimAt))
                return;  // the Embolus Spears burst (EmbolusSpears.cs)
            if (LocalNeedleBurst(me, pressed, aimAt))
                return;  // the Needle Swarm's burst (NeedleSwarm.cs)
            if (LocalSympathy(me, pressed, aimAt))
                return;  // the Blood Brawler's Sympathy (BloodBrawler.cs)
            if (LocalPistolWhip(me, pressed, aimAt, dt))
                return;  // the Revolver's stance switch (PistolWhip.cs)
            if (LocalScatter(me, pressed, aimAt, dt))
                return;  // the Mender's Bow's Scatter (BowFocus.cs)
            if (LocalTwinLaser(me, pressed, aimAt, dt))
                return;  // the Twin Pistols' Laser Sights (TwinPistols.cs)
            if (LocalOfudaBlast(me, pressed, aimAt, dt))
                return;  // the Ofuda's Blast Ofuda (Ofuda.cs)
            if (LocalArcCannon(me, pressed, aimAt, dt))
                return;  // Static Shock's Arc Cannon (StaticShock.cs)
            if (LocalHammerSlamReady(me, pressed, dt))
            {
                if (IsHost)
                    HostWeaponDash(MyId, DashHammerSlam, aimAt);  // no move: the hammer lands ahead of where you stand
                else
                {
                    var hw = Proto.Begin(Msg.WeaponDash);
                    hw.Write(DashHammerSlam);
                    hw.Write(aimAt);
                    session.SendToHost(Proto.End(hw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.BoStaff && pressed)
            {
                if (IsHost)
                    HostWeaponDash(MyId, DashShove, aimAt);  // no move: the shove lands where you stand, along your aim (the host checks its cooldown)
                else
                {
                    var sw = Proto.Begin(Msg.WeaponDash);
                    sw.Write(DashShove);
                    sw.Write(aimAt);
                    session.SendToHost(Proto.End(sw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.TwinRifle && pressed && twinNadeLocalCd <= 0f && me.TwinNades > 0)
            {
                twinNadeLocalCd = TwinNadeCd;  // one grenade a press, the tubes taking turns (TwinGrenades.cs)
                if (IsHost)
                    HostWeaponDash(MyId, DashNade, aimAt);  // no move: the grenade flies to where the streams cross
                else
                {
                    var nw = Proto.Begin(Msg.WeaponDash);
                    nw.Write(DashNade);
                    nw.Write(aimAt);
                    session.SendToHost(Proto.End(nw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.TwinRifleAuto && pressed && twinNadeLocalCd <= 0f)
            {
                twinNadeLocalCd = AutoLockCd;  // one press locks the enemy nearest your cursor, the next lets go (TwinAuto.cs)
                if (IsHost)
                    HostWeaponDash(MyId, DashLock, aimAt);
                else
                {
                    var lw = Proto.Begin(Msg.WeaponDash);
                    lw.Write(DashLock);
                    lw.Write(aimAt);
                    session.SendToHost(Proto.End(lw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.DoubleBarrel && pressed && me.DbUsed == 0 && me.DbReload <= 0f)
            {
                if (IsHost)
                    HostWeaponDash(MyId, DashBoth, aimAt);  // Alt: both barrels at once (the host checks the rest; DoubleBarrel.cs)
                else
                {
                    var bw = Proto.Begin(Msg.WeaponDash);
                    bw.Write(DashBoth);
                    bw.Write(aimAt);
                    session.SendToHost(Proto.End(bw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.OutOfAmmo && pressed && twinNadeLocalCd <= 0f)
            {
                twinNadeLocalCd = AutoLockCd;  // Alt: the spin (the host checks its cooldown; OutOfAmmo.cs)
                if (IsHost)
                    HostWeaponDash(MyId, DashSpin, aimAt);
                else
                {
                    var spw = Proto.Begin(Msg.WeaponDash);
                    spw.Write(DashSpin);
                    spw.Write(aimAt);
                    session.SendToHost(Proto.End(spw));
                }
                return;
            }
            if (me.Weapon.Id == WeaponId.Quarterstaff && weaponAction && staffJabLocalCd <= 0f && me.StaffStacks > 0)
            {
                staffJabLocalCd = StaffJabCd;  // (held, the key jabs again as soon as the host would let it)
                if (IsHost)
                    HostWeaponDash(MyId, DashJab, aimAt);  // no move: the jab lands where you stand, along your aim
                else
                {
                    var jw = Proto.Begin(Msg.WeaponDash);
                    jw.Write(DashJab);
                    jw.Write(aimAt);
                    session.SendToHost(Proto.End(jw));
                }
                return;
            }
        }

        /// Host: a player's weapon-action key did something (a blink, a recall, a shove, a jab, a guard or whirl going up or down, a Twin Fang).
        void HostWeaponDash(int id, int kind, V2 target)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Downed || TankOn(p))  // (in A Literal Tank: no Alt, LiteralTank.cs)
                return;
            if (kind == DashRavager)
            {
                HostRavagerLash(p);  // a Blood Ravager lash (BloodRavager.cs)
                return;
            }
            if (kind == DashBolt)
            {
                HostBoltDash(p, target);  // a Living Bolt dash (LivingBolt.cs)
                return;
            }
            var w = p.Weapon;
            if (HostArcanistDash(p, kind))
                return;  // Shard Guard and the blades' recall (Arcanist.cs)
            if (HostHoneyDash(p, kind))
                return;  // the Honeybee Staff's spray (HoneybeeStaff.cs)
            if (kind == DashWhirlOn || kind == DashWhirlOff)
            {
                HostKusariWhirl(p, kind == DashWhirlOn);
                return;
            }
            if (kind == DashPikeOn || kind == DashPikeOff)
            {
                HostPike(p, kind == DashPikeOn);  // the Halberd's Set the Pike (Halberd.cs)
                return;
            }
            if (kind == DashBraceOn || kind == DashBraceOff)
            {
                HostBrace(p, kind == DashBraceOn);  // the Bulwark Barrier's Brace (BulwarkBarrier.cs)
                return;
            }
            if (kind == DashKnuckleStance)
            {
                SwapKnuckleStance(p);  // the Arc Knuckles: Brawl to Rocket and back (ArcKnucklesBrawl.cs)
                return;
            }
            if (kind == DashFumaStance)
            {
                HostFumaSwitch(p);  // Throw stance to Orbit and back (FumaOrbit.cs)
                return;
            }
            if (kind == DashPullOn || kind == DashPullOff)
            {
                if (w.Id == WeaponId.BloodLash)
                    HostLeash(p, kind == DashPullOn);  // Martyr's Leash (BloodLash.cs)
                else
                    HostRepelPull(p, kind == DashPullOn);  // (Repulsion.cs)
                return;
            }
            if (kind == DashKatanaStance)
            {
                HostKatanaSwitch(p);  // the stance switch (Katana.cs)
                return;
            }
            if (kind == DashHammerSlam)
            {
                HostHammerSlam(p, target);  // (target: where they were aiming)
                return;
            }
            if (kind == DashHook)
            {
                HostLanceHook(p, target);  // (target: where they were aiming; Lance.cs)
                return;
            }
            if (kind == DashRapierOn || kind == DashRapierOff)
            {
                if (p.RapierGuard)
                    RapierDropStance(p);  // (a press toggles; the click lunges)
                else
                    HostRapierStance(p, true, target);  // (Rapier.cs)
                return;
            }
            if (kind == DashFlourish)
            {
                HostRapierFlourish(p, target);  // (target: where they were aiming; Rapier.cs)
                return;
            }
            if (kind == DashCast)
            {
                HostCenserCast(p, target);  // (target: where they were aiming)
                return;
            }
            if (kind == DashStrike)
            {
                if (p.Weapon.Id == WeaponId.Greatsword)
                    HostMoonwheel(p, target);  // GreatswordChain.cs
                else
                    HostTwinFang(p, target);  // (target: where they were aiming)
                return;
            }
            if (kind == DashLance)
            {
                HostSunlance(p, target);  // (target: where they were aiming)
                return;
            }
            if (kind == DashDouble || kind == DashMine || kind == DashGas)
            {
                if (kind == DashDouble) HostDoubleBarrel(p, target);  // the Tank Soldier's Alt fires (TankAlts.cs)
                else if (kind == DashMine) HostFlakMine(p, target);
                else HostTearGas(p, target);
                return;
            }
            if (kind == DashFork)
            {
                HostForkVolley(p, target);  // Arc Lightning's Forked Volley (ArcLock.cs)
                return;
            }
            if (kind == DashVgStance)
            {
                HostVgSwitch(p);  // the Vowguard's Valor to Sanctuary and back (Vowguard.cs)
                return;
            }
            if (kind == DashNodStance)
            {
                HostNodSwitch(p);  // the Nodachi's Storm to Guard and back (Nodachi.cs)
                return;
            }
            if (kind == DashNunGuard)
            {
                HostNunSwitch(p);  // the Nunchaku's combo to Twin Windmill and back (Nunchaku.cs)
                return;
            }
            if (kind == DashOfuda)
            {
                HostOfudaBlast(p, target);  // the Ofuda's Alt (Ofuda.cs)
                return;
            }
            if (kind == DashArcCannon)
            {
                HostArcCannon(p, target);  // Static Shock's Alt (StaticShock.cs)
                return;
            }
            if (kind == DashLaser)
            {
                HostTwinLaser(p);  // the Twin Pistols' Alt (TwinPistols.cs)
                return;
            }
            if (kind == DashRevStance)
            {
                HostRevStance(p);  // the Revolver's Alt: Gunslinger or Fan stance (PistolWhip.cs)
                return;
            }
            if (kind == DashScatter)
            {
                HostScatter(p, target);  // the Mender's Bow's Alt (BowFocus.cs)
                return;
            }
            if (kind == DashSympathy)
            {
                HostSympathy(p);  // the Blood Brawler's Alt (BloodBrawler.cs)
                return;
            }
            if (kind == DashEmbolus)
            {
                HostEmbolusBlast(p);  // the Embolus Spears' Alt (EmbolusSpears.cs)
                return;
            }
            if (kind == DashNeedles)
            {
                HostNeedleBurst(p, target);  // the Needle Swarm's Alt (NeedleSwarm.cs)
                return;
            }
            if (kind == DashVolley)
            {
                HostStoneVolley(p, target);  // the Stonespear's Alt (Stonespear.cs)
                return;
            }
            if (kind == DashPack)
            {
                HostPackMerge(p);  // the Wolf Whistle's Alt (WolfPack.cs)
                return;
            }
            if (kind == DashIgnite)
            {
                HostChalkIgnite(p);  // the Spirit Chalk's Alt (Chalk.cs)
                return;
            }
            if (kind == DashStorm)
            {
                HostNimbusStorm(p);  // the Nimbus's Alt (Nimbus.cs)
                return;
            }
            if (kind == DashHeatDump)
            {
                HostHeatDump(p, target);  // the Railgun's Alt (RailBeam.cs)
                return;
            }
            if (kind == DashZero)
            {
                HostAbsoluteZero(p);  // the Frost Lance's Alt (FrostLance.cs)
                return;
            }
            if (kind == DashTide)
            {
                HostTide(p, target);  // the Blood Blast's Alt (BloodBlast.cs)
                return;
            }
            if (kind == DashTempest)
            {
                HostTempest(p);  // Wind Clash's Alt (WindClash.cs)
                return;
            }
            if (kind == DashWyvernSlam)
            {
                HostWyvernSlam(p, target);  // the Wyvern Staff's Alt (WyvernStaff.cs)
                return;
            }
            if (kind == DashDrum)
            {
                HostSmokeLob(p, target);  // the Grenade Launcher's Alt (GrenadeLauncher.cs)
                return;
            }
            if (kind == DashBoneWall)
            {
                HostOssuaryWall(p, target);  // the Bone Guard's Alt (BoneGuard.cs)
                return;
            }
            if (kind == DashBoth)
            {
                HostBothBarrels(p, target);  // the Double Barrel's Alt (DoubleBarrel.cs)
                return;
            }
            if (kind == DashSpin)
            {
                HostAmmoSpin(p);  // (OutOfAmmo.cs)
                return;
            }
            if (kind == DashLock)
            {
                HostTwinAutoLock(p, target);  // (target: where they were aiming; TwinAuto.cs)
                return;
            }
            if (kind == DashNade)
            {
                HostTwinGrenade(p, target);  // (target: where they were aiming; TwinGrenades.cs)
                return;
            }
            if (kind == DashJab)
            {
                HostStaffJab(p, target);  // (target: where they were aiming)
                return;
            }
            if (kind == DashShove)
            {
                HostBoShove(p, target);  // (target: where they were aiming)
                return;
            }
            if (kind == DashRecall)
            {
                if (w.Id == WeaponId.Shuriken)
                    HostStarSwitch(p);  // the fan to the Great Star (recalling stuck stars) and back (GreatStar.cs)
                return;
            }
            if (kind == DashBlink)
            {
                if (w.Id != WeaponId.Kunai || p.KunaiMarkT <= 0f || p.KunaiMark.FartherThan(target, 40f))
                    return;
                p.KunaiMarkT = 0f;
                p.AmbushT = Math.Max(p.AmbushT, KunaiAmbush);  // the next hit lands double (NinjaHitMult)
                p.BlinkBoost = p.Stats.KunaiBlinkMult;      // Shadow Mark: and harder
                p.InvulnT = Math.Max(p.InvulnT, 0.2f);
                BroadcastFx(FxKind.Ring, target, new Rgba(0.45f, 0.4f, 0.65f), 30f, 0.3f, Sfx.NinjaBlink);
                return;
            }
        }

        /// Called by MoveBullets before it moves anything: gather the smoke, and turn chain shuriken around.
        void PrepareNinjaBullets(float dt)
        {
            smokes.Clear();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Smoke)
                    smokes.Add(z);
            foreach (var b in Bullets.Values)
                if (b.Kind == Defs.FumaStar)
                    FumaTurn(b, dt);
            TickGreatStars(dt);  // the Great Star along its drawn route and home (GreatStar.cs)
        }

        /// A Fuma Shuriken's loop is an oval through where it was thrown: at a time t into the lap (angle a = Tau t / lap)
        /// its velocity is M(a) V0, where V0 is the sideways velocity it set off with and M(a) = cos a + k sin a R turns it
        /// toward the front (R: a quarter turn, the way its AngVel's sign says; k: how much longer the oval is than wide).
        /// M is a scaled rotation, so V0 comes back from the velocity it has now: host and clients follow the same oval
        /// from what they already know. (Its AngVel is only that sign: MoveBullets doesn't turn it.)
        void FumaTurn(Bullet b, float dt)
        {
            float lap = Math.Max(0.01f, b.Life0), k = FumaReach * 0.5f / FumaWidth, s = b.AngVel >= 0f ? 1f : -1f;
            float a0 = MathX.Tau * (lap - b.Life) / lap, a1 = MathX.Tau * (lap - b.Life + dt) / lap;
            float c = (float)Math.Cos(a0), sn = k * (float)Math.Sin(a0);
            V2 v0 = (b.Vel * c - b.Vel.Orthogonal() * (s * sn)) / (c * c + sn * sn);
            float c1 = (float)Math.Cos(a1), sn1 = k * (float)Math.Sin(a1);
            b.Vel = v0 * c1 + v0.Orthogonal() * (s * sn1);
            if (a0 < MathX.Pi && a1 >= MathX.Pi)
                b.LastHit = b.PrevHit = 0;  // at the far end: it can cut the same enemy again on the way back
        }

        /// Ofuda charms heal allies near where they burst.
        void CharmHeal(Bullet b)
        {
            Player owner;
            Players.TryGetValue(b.Owner, out owner);
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.CloserThan(b.Pos, b.Splash + 40f))
                    Heal(q, b.HealBurst, owner);
        }

        // --- Timed effects ---------------------------------------------------------------

        /// Host, every tick: Vanish runs down, Ceremonial Armor's plates turn and block, Blossom Storm heals
        /// and clears bullets, and Shadow Clones about to end burst.
        public const float HealDashTime = 3f, HealDashTimeMastered = 4.5f, FootprintLife = 6f, FootprintStep = 64f, FootprintRadius = 40f, FootprintHeal = 12f;  // Healing Dash: the sprint, how long a footprint lasts, the gap between them, their radius, healing a second
        public const float WardSoakMax = 150f, WardRepay = 0.25f;           // Spirit Ward: the damage it takes for allies; the share it heals back when it ends

        /// A Healing Dash footprint goes down where the ninja stands, pointing the way it runs (Aim; the side is the zone's id).
        void DropFootprint(Player p, float heading)
        {
            var z = AddZone(ZoneKind.Mist, p, p.Pos, p.DashRadius, FootprintLife);
            z.HealMult = p.DashHeal;
            z.Aim = heading;
        }

        /// Healing Dash: the sprinting ninja leaves a footprint every FootprintStep px, and whoever stands in any footprint heals
        /// (once, however many overlap: the best one counts).
        void TickMists(float dt)
        {
            foreach (var p in Players.Values)
                if (p.DashT > 0f && !p.Downed && p.Pos.FartherOrAt(p.DashLast, FootprintStep))
                {
                    V2 d = p.Pos - p.DashLast;
                    DropFootprint(p, (float)Math.Atan2(d.Y, d.X));
                    p.DashLast = p.Pos;
                }
            foreach (var q in Players.Values)
            {
                if (q.Downed)
                    continue;
                Zone best = null;
                foreach (var z in Zones.Values)
                    if (z.Kind == ZoneKind.Mist && q.Pos.WithinReach(z.Pos, z.Radius) && (best == null || z.HealMult > best.HealMult))
                        best = z;
                if (best != null)
                    Heal(q, FootprintHeal * best.HealMult * dt, PlayerOrNull(best.Owner));
            }
        }

        /// A Spirit Ward near this player takes the damage first, while it lasts; returns what's left.
        float WardSoak(Player p, float amount)
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.Ward || z.Soak <= 0f || amount <= 0f || p.Pos.FartherThan(z.Pos, z.Radius))
                    continue;
                float take = Math.Min(z.Soak, amount);
                z.Soak -= take;
                z.Soaked += take;
                amount -= take;
                if (z.Soak <= 0f)
                    z.T = Math.Min(z.T, 0.001f);  // spent: it breaks
            }
            return amount;
        }

        /// A Spirit Ward ends (broken or run out): a share of what it took heals the allies near it.
        void WardEnds(Zone z)
        {
            float heal = z.Soaked * WardRepay * z.HealMult;
            if (heal > 0f)
                foreach (var q in Players.Values)
                    if (!q.Downed && q.Pos.WithinReach(z.Pos, z.Radius))
                        Heal(q, heal, PlayerOrNull(z.Owner));
            BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius, 0.45f, z.Soak <= 0f ? Sfx.Clang : Sfx.None);
        }

        void TickNinjas(float dt)
        {
            TickMists(dt);
            foreach (var p in Players.Values)  // Medicine Darts' doses heal; a Kunai mark fades
            {
                if (p.DoseT > 0f)
                {
                    p.DoseT = p.Downed ? 0f : Math.Max(0f, p.DoseT - dt);
                    if (p.DoseT > 0f)
                        Heal(p, p.DoseHps * dt, PlayerOrNull(p.DoseBy));
                    else
                        p.DoseHps = 0f;
                }
                p.KunaiMarkT = Math.Max(0f, p.KunaiMarkT - dt);
                TickKatana(p, dt);
                p.ShoveCd = Math.Max(0f, p.ShoveCd - dt);
                if (p.Downed || p.Weapon.Id != WeaponId.BoStaff)
                    p.BoStep = 0;  // (a downed or re-armed Ninja starts the chain over)
            }
            TickKusari(dt);  // Kusarigama.cs: the combo's place, the throw, the whirl
            TickFuma(dt);    // FumaOrbit.cs: the Fuma Shuriken's Orbit stance
            clones.Clear();
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.Clone || m.Kind == MinionKind.CloneWall)
                    clones.Add(m);
            TickCloneWalls(dt);  // CloneWall.cs
            TickBambooGroves(dt);  // BambooGrove.cs: the wave, the stuns' bleeds
            foreach (var p in Players.Values)
            {
                p.VanishT = Math.Max(0f, p.VanishT - dt);
                TickOrbit(p, dt);
                p.AmbushT = Math.Max(0f, p.AmbushT - dt);
                if (p.Downed)
                    p.VanishT = p.AmbushT = 0f;
                TickCereArmor(p, dt);  // Ceremonial Armor's plates (CeremonialArmor.cs)
                TickSenbon(p, dt);  // Senbon Seekers' needles (SenbonSeekers.cs)
                if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BlossomStorm)
                    foreach (var q in Players.Values)
                    {
                        if (q.Downed)
                            continue;
                        Heal(q, BlossomHeal * dt, p);
                        EraseEnemyBullets(q.Pos, BlossomClear);
                    }
            }
            foreach (var m in clones)
            {
                if (m.Kind != MinionKind.Clone || m.T < 0f || m.T > dt)
                    continue;
                Player owner;
                if (!Players.TryGetValue(m.Owner, out owner))
                    continue;
                foreach (var e in FoesInCircle(m.Pos, CloneBurstRadius, by: m.Owner))
                    HurtEnemy(e, CloneBurst * m.Power, m.Owner);
                BroadcastFx(FxKind.Boom, m.Pos, Kits.Get(MinionKind.Clone).Color, CloneBurstRadius, 0.35f, Sfx.Explode);
            }
        }
    }
}
