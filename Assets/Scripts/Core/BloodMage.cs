using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Blood Mage: health is its resource. Every Space ability costs health (never below 1), it hits harder
    /// the more health it's missing (Blood Price, up to +40%), and all its hits heal it a little. Tanks turn
    /// health into protection, healers give their own health away, and DPS are strongest near death.
    public partial class Game
    {
        const float LashReach = 110f, ClotPoolRadius = 70f, ClotPoolTime = 3f;
        const float BloodPriceMax = 0.4f, BloodSteal = 0.03f, LeechSteal = 0.3f, VeinSteal = 0.4f, VeinRange = 300f, SanguineDrop = 0.4f;
        public const float BoneSpearCost = 2f, TransfuseCost = 3f;
        const float ShellTime = 5f, ShellMult = 3f;
        const float PactTime = 4f, PactRadius = 220f, PactBurst = 1.5f, PactMasteredCut = 0.3f;
        const float DrainRadius = 220f, DrainAmount = 20f, DrainPull = 60f;
        const float GiftMult = 2f, GiftSplash = 200f, SiphonRadius = 140f, SiphonTime = 6f, MartyrRevive = 0.5f;
        const float BoilRadius = 260f, BoilDelay = 1f, BoilMult = 3f, BoilSplash = 80f;
        const float RushTime = 6f, RushRate = 0.5f, RushSteal = 0.1f, RushDrain = 3f;
        const float NovaDamage = 20f, NovaHeal = 3f;
        const float MoonCut = 0.6f, RainSteal = 0.2f;

        // --- Weapons ------------------------------------------------------------------

        /// A Blood Mage weapon's attack. `extra` is the number of extra projectiles; each weapon uses them its own way.
        void FireBloodWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            switch (p.Weapon.Id)
            {
                case WeaponId.BloodLash:
                {
                    var s = NewSwing(p, aim, LashReach + 20f * extra, 130f, 24f * dmg);
                    s.NoCut = true;  // a whip of blood, not a blade
                    s.SelfHeal = 3f + extra;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.ClotBolt:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        var b = SpawnBullet(Defs.ClotShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.2f)) * 520f, 0f, 1f, 26f * dmg, p.Id);
                        b.Pool = ClotPoolRadius;
                        b.PoolTime = ClotPoolTime;
                    }
                    break;
                }
                case WeaponId.LeechBeam:
                case WeaponId.VeinTether:
                {
                    float k = 1f + 0.25f * extra;
                    byte kind = p.Weapon.Id == WeaponId.LeechBeam ? Defs.LeechShot : Defs.VeinShot;
                    SpawnBullet(kind, muzzle, V2.FromAngle(aim + Rand(-0.02f, 0.02f)) * 1100f, 0f, (kind == Defs.LeechShot ? 0.3f : 0.32f) * k, 4f * k * dmg, p.Id);
                    break;
                }
                case WeaponId.TransfusionDarts:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        var b = SpawnBullet(Defs.TransfuseShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.08f)) * 850f, 0f, 0.85f, 7f * dmg, p.Id);
                        b.HealAlly = 8f * 9f / 7f;  // HealAllyHit scales it by Dmg / 9; the dart costs you TransfuseCost when it heals
                    }
                    break;
                }
                case WeaponId.SanguineOrb:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.SanguineShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.35f)) * 420f, 0f, 1.4f, 16f * dmg, p.Id, false, 3f);
                    break;
                }
                case WeaponId.BloodBolts:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.BloodShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.07f)) * 800f, 0f, 0.8f, 9f * dmg, p.Id);
                    break;
                }
                case WeaponId.HemoSpray:
                {
                    int n = 5 + 2 * extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.DropletShot, muzzle, V2.FromAngle(aim + (i / (n - 1f) - 0.5f) * 0.52f) * 620f, 0f, 0.5f, 7f * dmg, p.Id).Pierce += 1;
                    break;
                }
                case WeaponId.BoneSpear:
                {
                    PayHealth(p, BoneSpearCost);
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.BoneSpearShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.12f)) * 1000f, 0f, 0.9f, 60f * dmg, p.Id).Pierce += 3;
                    break;
                }
            }
        }

        /// Shot i of n, fanned out `step` radians apart around the aim.
        static float Fanned(float aim, int i, int n, float step) => aim + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * step * (n - 1));

        /// What a Blood Mage's shot does when it hits, after the damage (dmg) is dealt.
        void BloodShotHit(Bullet b, Enemy e, float dmg)
        {
            if (b.Kind < Defs.BloodShot || b.Kind > Defs.BoneSpearShot)
                return;
            Player p;
            if (!Players.TryGetValue(b.Owner, out p) || p.Downed)
                return;
            if (b.HealOwner > 0f)
                Heal(p, b.HealOwner, p);  // Hemo Nova
            switch (b.Kind)
            {
                case Defs.LeechShot:
                    Heal(p, dmg * LeechSteal, p);
                    break;
                case Defs.VeinShot:
                {
                    // Split between you and the nearest ally (all yours if no one is near).
                    Player ally = null;
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed && q.Pos.DistanceTo(p.Pos) < VeinRange && (ally == null || q.Pos.DistanceSq(p.Pos) < ally.Pos.DistanceSq(p.Pos)))
                            ally = q;
                    float heal = dmg * VeinSteal;
                    if (ally == null)
                        Heal(p, heal, p);
                    else
                    {
                        Heal(p, heal * 0.5f, p);
                        Heal(ally, heal * 0.5f, p);
                    }
                    break;
                }
                case Defs.SanguineShot:
                    if (e.Hp <= 0f && !e.BloodDropped)
                    {
                        e.BloodDropped = true;
                        if (rng.NextDouble() < SanguineDrop)
                            SpawnOrb(e.Pos, false);
                    }
                    break;
                case Defs.ClotShot:
                    AddZone(ZoneKind.BloodPool, p, b.Pos, b.Pool, b.PoolTime);
                    break;
            }
        }

        /// Transfusion Darts cost the healer some of their own health for each ally they heal.
        void TransfusionCost(Bullet b)
        {
            Player p;
            if (b.Kind == Defs.TransfuseShot && Players.TryGetValue(b.Owner, out p))
                PayHealth(p, TransfuseCost);
        }

        // --- Health as a resource ---------------------------------------------------------

        /// Spend health, never below 1 (or nothing while down). Returns how much was actually paid.
        float PayHealth(Player p, float cost)
        {
            if (p.Downed || cost <= 0f)
                return 0f;
            float paid = Math.Max(0f, Math.Min(cost, p.Hp - 1f));
            p.Hp -= paid;
            return paid;
        }

        float castPaid;  // health paid for the ability being cast (Blood Mage abilities scale with it)

        /// Host, as an ability is cast: pay its health cost, if it has one.
        void PayAbilityCost(Player p, AbilityId id)
        {
            castPaid = 0f;
            if (id == AbilityId.None)
                return;
            var d = Upgrades.Ability(id);
            if (d.HealthCost > 0f)
                castPaid = PayHealth(p, (d.CostsCurrent ? p.Hp : p.MaxHp) * d.HealthCost);
        }

        /// Blood Price: a Blood Mage deals up to 40% more damage the more health it's missing (full bonus at 10%
        /// health); during Crimson Apotheosis up to 100% more. Applied in HurtEnemy.
        float BloodDamageMult(Player p)
        {
            if (p.Kit != Kit.BloodMage || p.MaxHp <= 0f)
                return 1f;
            float missing = Math.Max(0f, Math.Min(1f, (1f - p.Hp / p.MaxHp) / 0.9f));
            return 1f + (Exalted(p) ? 1f : BloodPriceMax) * missing;
        }

        float rainSteal;  // the strongest Sanguine Rain up right now (0 if none)

        /// Healing from damage dealt (HurtEnemy): a Blood Mage's 3% and Frenzy Pact's 10%, and Sanguine Rain for everyone.
        void BloodLifesteal(Player p, float dealt)
        {
            if (p.Downed || dealt <= 0f)
                return;
            float frac = rainSteal;
            if (p.Kit == Kit.BloodMage)
                frac += BloodSteal;
            if (p.RushT > 0f)
                frac += RushSteal;
            if (frac > 0f)
                Heal(p, dealt * frac, p);
            if (Exalted(p))
                p.ApoDealt += dealt;
        }

        /// Faster firing during Frenzy Pact (FireRate).
        static float BloodFireRate(Player p) => p.RushT > 0f ? 1f + RushRate + p.Stats.RushRate : 1f;

        bool Exalted(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.CrimsonApotheosis;
        bool BloodRiteUp(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BloodRite;

        /// Enemies all go for a Blood Mage under Crimson Pact or Blood Rite (PickTarget).
        bool BloodTaunting(Player p) => p.PactT > 0f || BloodRiteUp(p);

        /// Damage cut from Blood Rite and a mastered Crimson Pact (Damage).
        float BloodCut(Player p) => Math.Max(BloodRiteUp(p) ? MoonCut : 0f, p.PactT > 0f && p.PactMastered ? PactMasteredCut : 0f);

        /// Damage about to come off a player's health (after shields): Crimson Pact stores it, Blood Rite turns it
        /// into healing for everyone else.
        void BloodTookHit(Player p, float amount)
        {
            if (amount <= 0f)
                return;
            if (p.PactT > 0f)
                p.PactStore += amount;
            if (BloodRiteUp(p))
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed)
                        Heal(q, amount, p);
        }

        // --- Abilities ------------------------------------------------------------------

        readonly List<Boil> boils = new List<Boil>();

        class Boil
        {
            public int Enemy, Owner;
            public float T, Dmg;
            public bool Splash;
        }

        /// Blood Mage abilities and ultimates. Returns false if `id` isn't one. castPaid is what this cast cost.
        bool BloodAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            bool cap = castMastered;
            float pw = castPower;
            var blood = new Rgba(0.85f, 0.12f, 0.2f);
            switch (id)
            {
                case AbilityId.BloodShell:
                    p.ShieldHp = Math.Max(p.ShieldHp, castPaid * (ShellMult + s.ShellBonus) * pw);
                    p.ShieldT = Math.Max(p.ShieldT, ShellTime + s.ShellBonusTime);
                    if (cap)
                        EraseEnemyBullets(p.Pos, 160f);
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 60f, 0.4f, Sfx.Ability);
                    return true;
                case AbilityId.CrimsonPact:
                    p.PactT = PactTime;
                    p.PactStore = 0f;
                    p.PactPower = pw * s.PactMult;
                    p.PactRadius = PactRadius * s.PactRadiusMult;
                    p.PactMastered = cap;
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = PactTime;
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 260f, 0.5f, Sfx.Roar);
                    return true;
                case AbilityId.Exsanguinate:
                {
                    float r = DrainRadius * s.DrainRadiusMult, drain = DrainAmount * s.DrainMult;
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) > r + e.Radius)
                            continue;
                        if (!Heavy(e))
                        {
                            V2 away = (e.Pos - p.Pos).Normalized();
                            float d = Math.Max(e.Pos.DistanceTo(p.Pos) - DrainPull, e.Radius + p.Def.BodyR + 4f);
                            e.Pos = ClampToArena(p.Pos + (away.IsZero ? V2.FromAngle(p.Aim) : away) * d, e.Radius);
                        }
                        HurtEnemy(e, drain * s.DmgMult, p.Id);
                        Heal(p, drain, p);
                        if (cap && !e.Def.IsBoss)
                        {
                            e.SlowT = Math.Max(e.SlowT, 2f);
                            e.SlowMult = Math.Min(e.SlowMult, 0.5f);
                        }
                        BroadcastFx(FxKind.Slash, e.Pos, p.Pos, blood, 0.1f, 0.3f, Sfx.None);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, blood, r, 0.4f, Sfx.Shockwave);
                    return true;
                }
                case AbilityId.BloodGift:
                {
                    Player ally = null;
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed && (ally == null || q.Hp / q.MaxHp < ally.Hp / ally.MaxHp))
                            ally = q;
                    float gift = castPaid * GiftMult * s.GiftMult;
                    if (ally == null)
                    {
                        // Alone: it becomes a shield for you.
                        p.ShieldHp = Math.Max(p.ShieldHp, gift * pw);
                        p.ShieldT = Math.Max(p.ShieldT, ShellTime);
                        BroadcastFx(FxKind.Ring, p.Pos, blood, 50f, 0.4f, Sfx.SpellHoly);
                        return true;
                    }
                    Heal(ally, gift, p);
                    if (cap)
                        foreach (var q in Players.Values)
                            if (q != ally && !q.Downed && q.Pos.DistanceTo(ally.Pos) < GiftSplash)
                                Heal(q, gift * 0.5f, p);
                    BroadcastFx(FxKind.Slash, p.Pos, ally.Pos, blood, 0.12f, 0.35f, Sfx.None);
                    BroadcastFx(FxKind.Ring, ally.Pos, blood, 50f, 0.4f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.SiphonField:
                {
                    var z = AddZone(ZoneKind.Siphon, p, p.Pos, SiphonRadius * s.SiphonRadiusMult, SiphonTime + s.SiphonBonusTime);
                    z.Follow = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.45f, Sfx.Ability);
                    return true;
                }
                case AbilityId.Martyr:
                {
                    Player down = null, hurt = null;
                    foreach (var q in Players.Values)
                    {
                        if (q == p)
                            continue;
                        if (q.Downed && (down == null || q.Pos.DistanceSq(target) < down.Pos.DistanceSq(target)))
                            down = q;
                        if (!q.Downed && (hurt == null || q.Hp / q.MaxHp < hurt.Hp / hurt.MaxHp))
                            hurt = q;
                    }
                    if (down != null)
                    {
                        RevivePlayer(down, Math.Min(1f, (MartyrRevive + s.MartyrBonus) * pw), p);
                        down.InvulnT = Math.Max(down.InvulnT, 1.5f + s.MartyrInvuln);
                        castHits += 3;
                        BroadcastFx(FxKind.Slash, p.Pos, down.Pos, blood, 0.14f, 0.4f, Sfx.None);
                        BroadcastFx(FxKind.Ring, down.Pos, blood, 70f, 0.5f, Sfx.Revive);
                    }
                    else if (hurt != null)
                    {
                        Heal(hurt, castPaid * GiftMult, p);
                        BroadcastFx(FxKind.Ring, hurt.Pos, blood, 50f, 0.4f, Sfx.SpellHoly);
                    }
                    if (cap)
                        p.InvulnT = Math.Max(p.InvulnT, 2f);
                    return true;
                }
                case AbilityId.BloodBoil:
                {
                    float r = BoilRadius * s.BoilRadiusMult, dmg = castPaid * BoilMult * s.BoilMult * s.DmgMult * pw;
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) > r + e.Radius)
                            continue;
                        boils.Add(new Boil { Enemy = e.Id, Owner = p.Id, T = BoilDelay, Dmg = dmg, Splash = cap });
                        castHits++;
                        BroadcastFx(FxKind.Ring, e.Pos, blood, e.Radius + 14f, BoilDelay, Sfx.None);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, blood, r, 0.45f, Sfx.Ability);
                    return true;
                }
                case AbilityId.FrenzyPact:
                    p.RushT = RushTime + s.RushBonusTime;
                    p.RushMastered = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 60f, 0.4f, Sfx.Roar);
                    return true;
                case AbilityId.HemoNova:
                {
                    int n = 16 + s.NovaExtraBolts;
                    for (int i = 0; i < n; i++)
                    {
                        V2 d = V2.FromAngle(p.Aim + MathX.Tau * i / n);
                        var b = SpawnBullet(Defs.BloodShot, p.Pos + d * p.Def.BodyR, d * 600f, 0f, 0.7f, NovaDamage * s.DmgMult, p.Id);
                        b.HealOwner = NovaHeal;
                        if (cap)
                            b.Pierce += 2;
                    }
                    Sounds.Add(Sfx.SpellArcane);
                    return true;
                }

                // Ultimates
                case AbilityId.BloodRite:
                {
                    float time = cap ? 12f : 8f;
                    p.UltT = time;
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = time;
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 240f, 0.6f, Sfx.Roar);
                    return true;
                }
                case AbilityId.SanguineRain:
                    p.UltT = cap ? 9f : 6f;
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 200f, 0.6f, Sfx.SpellHoly);
                    return true;
                case AbilityId.CrimsonApotheosis:
                    p.UltT = cap ? 9f : 6f;
                    p.ApoDealt = 0f;
                    p.ApoUp = true;
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 120f, 0.5f, Sfx.Roar);
                    return true;
            }
            return false;
        }

        // --- Timed effects ---------------------------------------------------------------

        /// Host, every tick: Crimson Pact runs down and bursts, Frenzy Pact drains, Crimson Apotheosis pays out
        /// when it ends, Blood Boils go off, and Sanguine Rain's strength is noted for HurtEnemy.
        void TickBloodMages(float dt)
        {
            rainSteal = 0f;
            foreach (var p in Players.Values)
            {
                if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.SanguineRain)
                    rainSteal = Math.Max(rainSteal, Mastered(p, AbilityId.SanguineRain) ? 0.3f : RainSteal);
                if (p.Downed)
                {
                    p.PactT = p.RushT = 0f;
                    p.ApoUp = false;
                    continue;
                }
                if (p.PactT > 0f)
                {
                    p.PactT -= dt;
                    if (p.PactT <= 0f)
                    {
                        p.PactT = 0f;
                        float dmg = p.PactStore * PactBurst * p.PactPower * p.Stats.DmgMult;
                        if (dmg > 0f)
                            foreach (var e in Enemies.Values)
                                if (e.SpawnT <= 0f && e.Pos.DistanceTo(p.Pos) < p.PactRadius + e.Radius)
                                    HurtEnemy(e, dmg, p.Id);
                        BroadcastFx(FxKind.Boom, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), p.PactRadius, 0.4f, Sfx.Explode);
                    }
                }
                if (p.RushT > 0f)
                {
                    p.RushT = Math.Max(0f, p.RushT - dt);
                    if (!p.RushMastered)
                        PayHealth(p, RushDrain * dt);
                }
                if (p.ApoUp && !Exalted(p))
                {
                    p.ApoUp = false;
                    Heal(p, p.ApoDealt * 0.5f, p);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), 70f, 0.4f, Sfx.SpellHoly);
                }
            }
            for (int i = boils.Count - 1; i >= 0; i--)
            {
                var b = boils[i];
                b.T -= dt;
                if (b.T > 0f)
                    continue;
                boils.RemoveAt(i);
                Enemy e;
                if (!Enemies.TryGetValue(b.Enemy, out e) || e.Hp <= 0f)
                    continue;
                HurtEnemy(e, b.Dmg, b.Owner);
                if (b.Splash)
                    foreach (var o in Enemies.Values)
                        if (o != e && o.SpawnT <= 0f && o.Pos.DistanceTo(e.Pos) < BoilSplash + o.Radius)
                            HurtEnemy(o, b.Dmg, b.Owner);
                BroadcastFx(FxKind.Boom, e.Pos, new Rgba(0.85f, 0.12f, 0.2f), b.Splash ? BoilSplash : e.Radius + 20f, 0.3f, Sfx.Explode);
            }
        }
    }
}
