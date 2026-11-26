using System;

namespace Arena
{
    /// The Knight Banners: one planted standard per role, each an ultimate (Docs/Mockups/knight-banners.html):
    ///   **Aegis Standard** (Tank, key 3 of the Ultimate row): allies inside wear a shield of StdShield (mastered
    ///     StdShieldMastered) that fills when they step in and regenerates StdShieldRegen a second once StdShieldDelay s (mastered
    ///     StdShieldDelayMastered) pass without a hit; out of the ring it fades over StdShieldFade s. It soaks damage before any other
    ///     shield (`StdShieldSoak`, from Combat.cs `Damage`). `Player.StdShieldHp`/`StdShieldMax` are replicated for the look.
    ///   **Pilgrim's Standard** (Healer, key 1): allies inside heal PilgrimHeal (mastered PilgrimHealMastered) of their max health
    ///     a second, in ticks of PilgrimTick s; downed allies inside are stood back up PilgrimRevive faster.
    ///   **War Standard** (DPS, key 2): allies inside attack WarFire faster and deal WarDmg more damage (mastered WarFireMastered,
    ///     WarDmgMastered). Worked out from the replicated zone on every screen (`WarFireMult` in Weapons.cs `FireRate`,
    ///     `WarDmgMult` in Combat.cs `HurtEnemy`), so your own swings speed up on your own game too.
    /// The knight throws the standard up to BannerThrowReach px toward the cursor; it lands after BannerThrow s and its ring grows
    /// over BannerGrow s. It stands BannerTime s, or until the ultimate is pressed again (`BannerPress`, no cooldown spent for the
    /// press), its owner goes down or the wave ends; then it folds over BannerFold s (`Zone.Aim` 1, replicated) and the cooldown
    /// starts (`LiftBanner`, like Last Stand). A zone of its own kind per banner (ZoneKind.AegisStandard, PilgrimsStandard,
    /// WarStandard): Pos the pole's foot, Radius the ring, `Zone.Cap` mastered; its age sets the phase. Look: Game/KnightBannersLook.cs.
    public partial class Game
    {
        public const float BannerTime = 60f, BannerFold = 0.6f, BannerThrow = 0.35f, BannerGrow = 0.3f, BannerThrowReach = 160f;
        public const float StdShieldRadius = 220f, StdShield = 80f, StdShieldMastered = 120f, StdShieldRegen = 25f, StdShieldDelay = 1.5f, StdShieldDelayMastered = 0.75f, StdShieldFade = 1.5f, StdShieldCooldown = 75f;
        public const float PilgrimRadius = 230f, PilgrimHeal = 0.04f, PilgrimHealMastered = 0.05f, PilgrimTick = 0.5f, PilgrimRevive = 0.5f, PilgrimCooldown = 80f;
        public const float WarRadius = 210f, WarFire = 0.4f, WarFireMastered = 0.55f, WarDmg = 0.25f, WarDmgMastered = 0.35f, WarCooldown = 70f;

        /// The three banner ultimates' entries. Upgrades.Abilities gathers them.
        internal static readonly AbilityDef[] BannerUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.AegisStandard, Kit = Kit.Knight, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Aegis Standard", Cooldown = StdShieldCooldown,
                Desc = "Plant a standard for 60 s: allies inside wear an 80-point shield that regenerates. Press again to lift it.",
                Capstone = "A 120-point shield that starts regenerating after 0.75 s.",
                Bot = b => b.HaveTarget && b.TargetD < 600f,
            },
            new AbilityDef
            {
                Id = AbilityId.PilgrimsStandard, Kit = Kit.Knight, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Pilgrim's Standard", Cooldown = PilgrimCooldown,
                Desc = "Plant a standard for 60 s: allies inside heal 4% of their max health a second. Press again to lift it.",
                Capstone = "Allies inside heal 5% a second.",
                Bot = b => b.HaveTarget && b.TargetD < 600f,
            },
            new AbilityDef
            {
                Id = AbilityId.WarStandard, Kit = Kit.Knight, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "War Standard", Cooldown = WarCooldown,
                Desc = "Plant a standard for 60 s: allies inside attack 40% faster and hit 25% harder. Press again to lift it.",
                Capstone = "+55% attack speed and +35% damage.",
                Bot = b => b.HaveTarget && b.TargetD < 300f,
            },
        };

        public static bool IsBanner(ZoneKind k) => k == ZoneKind.AegisStandard || k == ZoneKind.PilgrimsStandard || k == ZoneKind.WarStandard;
        public static bool IsBanner(AbilityId id) => id == AbilityId.AegisStandard || id == AbilityId.PilgrimsStandard || id == AbilityId.WarStandard;

        public static ZoneKind BannerKind(AbilityId id) =>
            id == AbilityId.AegisStandard ? ZoneKind.AegisStandard : id == AbilityId.PilgrimsStandard ? ZoneKind.PilgrimsStandard : ZoneKind.WarStandard;

        public static AbilityId BannerId(ZoneKind k) =>
            k == ZoneKind.AegisStandard ? AbilityId.AegisStandard : k == ZoneKind.PilgrimsStandard ? AbilityId.PilgrimsStandard : AbilityId.WarStandard;

        /// Seconds since the standard was thrown.
        public static float BannerAge(Zone z) => z.Duration - z.T;

        /// Planted (or still in the air) and not folding.
        public static bool BannerOn(Zone z) => IsBanner(z.Kind) && z.Aim <= 0f && z.T > 0f;

        /// How far its ring reaches: nothing in the air, then growing out over BannerGrow s (easing out).
        public static float BannerReach(Zone z)
        {
            float x = Math.Max(0f, Math.Min(1f, (BannerAge(z) - BannerThrow) / BannerGrow));
            return z.Radius * (1f - (1f - x) * (1f - x) * (1f - x));
        }

        /// Is a standing ally at pos inside this banner's ring (and is it working)?
        public static bool InBanner(Zone z, V2 pos) => BannerOn(z) && BannerAge(z) >= BannerThrow && pos.WithinReach(z.Pos, BannerReach(z));

        /// The banner this player has up, if any.
        public Zone ActiveBanner(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Owner == p.Id && BannerOn(z))
                    return z;
            return null;
        }

        /// True if this slot is a banner already up: pressing it lifts the banner instead of casting again.
        public bool BannerPress(Player p, int slot) => IsBanner(p.Slots[slot].Id) && ActiveBanner(p) != null;

        /// War Standard: how much faster this player attacks (1 outside any). Every screen works it out from the zones.
        public float WarFireMult(Player p)
        {
            float best = 0f;
            if (p.Downed)
                return 1f;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.WarStandard && InBanner(z, p.Pos))
                    best = Math.Max(best, z.Cap ? WarFireMastered : WarFire);
            return 1f + best;
        }

        /// War Standard: how much harder this player's hits land (1 outside any).
        float WarDmgMult(int attacker)
        {
            Player p;
            if (attacker <= 0 || !Players.TryGetValue(attacker, out p) || p.Downed)
                return 1f;
            float best = 0f;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.WarStandard && InBanner(z, p.Pos))
                    best = Math.Max(best, z.Cap ? WarDmgMastered : WarDmg);
            return 1f + best;
        }

        void CastBanner(Player p, AbilityId id, V2 target, bool cap)
        {
            var old = ActiveBanner(p);
            if (old != null)
                LiftBanner(old);
            V2 at = ClampToArena(p.Pos + (target - p.Pos).LimitLength(BannerThrowReach), 20f);
            float r = id == AbilityId.AegisStandard ? StdShieldRadius : id == AbilityId.PilgrimsStandard ? PilgrimRadius : WarRadius;
            var z = AddZone(BannerKind(id), p, at, r, BannerTime + BannerFold);
            z.Cap = cap;
            Sounds.Add(Sfx.Clang);
        }

        /// The banner comes down: it stops working at once, folds over BannerFold s, and its owner's cooldown starts now.
        void LiftBanner(Zone z)
        {
            if (!BannerOn(z))
                return;
            z.Aim = 1f;
            z.Duration = BannerAge(z) + BannerFold;  // (keeps the age, so the ring stays where it got to)
            z.T = BannerFold;
            Player p;
            int slot = Upgrades.UltSlot;
            if (Players.TryGetValue(z.Owner, out p) && p.Slots[slot].Id == BannerId(z.Kind) && p.Slots[slot].Charges <= 0)
                p.Slots[slot].Timer = Cooldown(p, slot);
        }

        /// Every banner comes down (the wave is over: its zones are about to be cleared).
        void LiftAllBanners()
        {
            foreach (var z in Zones.Values)
                LiftBanner(z);
        }

        /// Host, every tick, for each banner (TickZones): it comes down at its time, or when its owner goes down or changes
        /// ultimate; Pilgrim's Standard heals and helps revives. (The Aegis shield is TickStdShield, the War Standard's buff is read live.)
        void TickBanner(Zone z, float dt)
        {
            if (!BannerOn(z))
                return;
            Player o;
            if (!Players.TryGetValue(z.Owner, out o) || o.Downed || o.Slots[Upgrades.UltSlot].Id != BannerId(z.Kind) || BannerAge(z) >= BannerTime)
            {
                LiftBanner(z);
                return;
            }
            if (z.Kind != ZoneKind.PilgrimsStandard)
                return;
            foreach (var q in Players.Values)
                if (q.Downed && InBanner(z, q.Pos))
                {
                    q.Revive += dt / D(DiffKnob.ReviveTime) * PilgrimRevive;  // on top of the normal revive
                    q.ReviveBy = z.Owner;
                }
            z.TickT += dt;
            while (z.TickT >= PilgrimTick)
            {
                z.TickT -= PilgrimTick;
                float rate = z.Cap ? PilgrimHealMastered : PilgrimHeal;
                foreach (var q in Players.Values)
                    if (!q.Downed && InBanner(z, q.Pos))
                        Heal(q, q.MaxHp * rate * PilgrimTick * z.HealMult, o, "Pilgrim's Standard");
            }
        }

        /// Host, every tick, for each player: the Aegis Standard's shield fills, regenerates or fades.
        void TickStdShield(Player q, float dt)
        {
            Zone best = null;
            if (!q.Downed)
                foreach (var z in Zones.Values)
                    if (z.Kind == ZoneKind.AegisStandard && InBanner(z, q.Pos) && (best == null || z.Cap && !best.Cap))
                        best = z;
            q.StdShieldHitT += dt;
            if (best == null)
            {
                q.StdShieldIn = false;
                if (q.StdShieldHp > 0f)
                    q.StdShieldHp = Math.Max(0f, q.StdShieldHp - Math.Max(1f, q.StdShieldMax) / StdShieldFade * dt);
                if (q.StdShieldHp <= 0f)
                    q.StdShieldMax = 0f;
                return;
            }
            float max = (best.Cap ? StdShieldMastered : StdShield) * best.HealMult;
            float delay = best.Cap ? StdShieldDelayMastered : StdShieldDelay;
            q.StdShieldMax = max;
            if (!q.StdShieldIn && q.StdShieldHitT >= delay)
                q.StdShieldHp = max;  // stepping in fills it (not straight after a hit: no stepping out and back to dodge the wait)
            q.StdShieldIn = true;
            if (q.StdShieldHitT >= delay && q.StdShieldHp < max)
                q.StdShieldHp = Math.Min(max, q.StdShieldHp + StdShieldRegen * best.HealMult * dt);
            q.StdShieldHp = Math.Min(q.StdShieldHp, max);
        }

        /// Combat.cs Damage: the Aegis shield takes what it can before any other shield. Returns what gets through.
        float StdShieldSoak(Player p, float amount)
        {
            if (amount <= 0f)
                return amount;
            if (p.StdShieldMax > 0f)
                p.StdShieldHitT = 0f;  // any hit inside holds back the regeneration
            if (p.StdShieldHp <= 0f)
                return amount;
            float soak = Math.Min(p.StdShieldHp, amount);
            p.StdShieldHp -= soak;
            return amount - soak;
        }

        void SyncKnightBanners(NetSync s, Player p)
        {
            s.Float(ref p.StdShieldHp);
            s.Float(ref p.StdShieldMax);
        }
    }
}
