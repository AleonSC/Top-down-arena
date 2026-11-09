using System;

namespace Arena
{
    /// Bloodbath (the Tank Hemomancer's second ultimate, key 1 of the lobby's Ultimate row; Vein Tithe stays the default), from
    /// Docs/Mockups/ultimate-ideas.html (Tank > Hemomancer, the first card) and Docs/Mockups/bloodbath.html.
    /// A channel, like Sanguine Rain: nothing up front, but while it runs it drains BathDrain of your max health a second (no hit grace,
    /// never below 1 HP). Blood floods the floor round where you cast: a `ZoneKind.Bloodbath` zone of BathRadius (mastered
    /// BathRadiusMastered) that grows out over BathGrow s and doesn't follow you (you can move and shoot). The channel ends on a second
    /// press (`BathPress`, no cooldown spent), after BathTime s, when the drain takes you to BathFloor of your max health, when you go
    /// down, or between waves (WaveCleared clears the zones). Ending it (`EndBath`) turns the pool inert at once (`Zone.Aim` 1,
    /// replicated) and leaves it BathDry s to dry up on screen.
    ///   Enemies wading in it are slowed (BathSlow; Heavy enemies BathSlowHeavy and bosses BathSlowBoss, through `Enemy.WadeMult`, as the usual
    ///   slow skips them) and bleed BathBleed a second,
    ///   bosses too, as a hit from you every BathTick s (so your damage multiplier, Blood Price and the rest scale it).
    ///   Every tick heals you BathHeal (mastered BathHealMastered) of the damage it dealt, wherever you are; those ticks don't also
    ///   give the class's usual 3% (bathTick, BloodLifesteal). Allies standing in it, you included, take BathGuard less damage.
    ///   The drain and the heal pull against each other: with about four enemies wading, the pool pays for itself.
    /// Its own tick rather than the shared bleed (ApplyBleed): that bleed stacks and lingers BleedTime s after the last hit, and
    /// heals through the normal 3%, while this one must stop the moment an enemy steps out and pay back its own share.
    /// Replicated as an ordinary zone (Pos, Radius, T, Duration), so every screen grows, ripples and dries it the same
    /// (Game/BloodbathLook.cs); each tick's heal is sent as `FxKind.BathHeal` for the small numbers over you.
    public partial class Game
    {
        public const float BathDrain = 0.04f, BathFloor = 0.15f, BathCooldown = 75f;  // the channel's cost: 4% of max health a second, ending at 15%
        public const float BathRadius = 170f, BathRadiusMastered = 220f, BathGrow = 0.9f, BathTime = 10f;
        public const float BathSlow = 0.5f, BathSlowHeavy = 0.35f, BathSlowBoss = 0.2f;
        public const float BathBleed = 15f, BathTick = 0.5f, BathHeal = 0.25f, BathHealMastered = 0.35f, BathGuard = 0.2f;
        public const float BathStart = 0.12f;  // the pool's size the moment it's cast, as a share of its full size
        public const float BathDry = 1.2f;     // how long an ended pool takes to dry up on screen (it does nothing meanwhile)
        public const float BathHealFxTime = 0.9f;
        public static readonly Rgba BathRed = new Rgba(0.55f, 0.03f, 0.08f);

        bool bathTick;  // a Bloodbath tick is dealing its damage: BloodLifesteal gives no 3% on it
        readonly System.Collections.Generic.List<Enemy> bathScratch = new System.Collections.Generic.List<Enemy>();

        /// How far the pool has spread: from BathStart of its size to all of it over BathGrow s (easing out).
        public static float BathReach(Zone z)
        {
            float x = Math.Max(0f, Math.Min(1f, (z.Duration - z.T) / BathGrow));
            float ease = 1f - (1f - x) * (1f - x) * (1f - x);
            return z.Radius * (BathStart + (1f - BathStart) * ease);
        }

        /// Is something of this radius at pos wading in the pool? (Only while it's channelled: a drying pool does nothing.)
        public static bool InBath(Zone z, V2 pos, float radius) => BathOn(z) && pos.DistanceTo(z.Pos) <= BathReach(z) + radius;

        /// A Bloodbath still being channelled (not drying up).
        public static bool BathOn(Zone z) => z.Kind == ZoneKind.Bloodbath && z.T > 0f && z.Aim <= 0f;

        /// Seconds since this pool was cast.
        public static float BathAge(Zone z) => z.Duration - z.T;

        /// The Bloodbath this player is channelling, if any.
        public Zone ActiveBath(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Owner == p.Id && BathOn(z))
                    return z;
            return null;
        }

        /// True if this slot is a Bloodbath being channelled: pressing it ends the channel instead of casting again.
        public bool BathPress(Player p, int slot) => p.Slots[slot].Id == AbilityId.Bloodbath && ActiveBath(p) != null;

        /// Enemies wading in this pool right now (the bot's reason to keep channelling).
        public int BathWading(Zone z)
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && InBath(z, e.Pos, e.Radius))
                    n++;
            return n;
        }

        /// The channel stops: the pool turns inert at once and dries up over BathDry s.
        void EndBath(Zone z)
        {
            if (!BathOn(z))
                return;
            z.Aim = 1f;
            float age = BathAge(z);
            z.Duration = age + BathDry;  // (keeps the age, so the reach stays where it got to)
            z.T = BathDry;
            Player p;
            if (Players.TryGetValue(z.Owner, out p))
                p.UltT = 0f;
        }

        /// How much an enemy in the pool is slowed: bosses least, Heavy enemies less than the rest.
        public static float BathSlowFor(Enemy e) => e.Def.IsBoss ? BathSlowBoss : e.Def.Heavy ? BathSlowHeavy : BathSlow;

        /// Damage cut for a player standing in any Bloodbath (Damage).
        float BathCut(Player p)
        {
            foreach (var z in Zones.Values)
                if (InBath(z, p.Pos, 0f))
                    return BathGuard;
            return 0f;
        }

        void CastBloodbath(Player p, bool cap)
        {
            var old = ActiveBath(p);
            if (old != null)
                EndBath(old);
            var z = AddZone(ZoneKind.Bloodbath, p, p.Pos, cap ? BathRadiusMastered : BathRadius, BathTime + BathDry);
            z.HealBack = cap ? BathHealMastered : BathHeal;
            p.UltT = BathTime;  // the channel's time left (the buff ring under you, and the HUD)
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), z.Radius, 0.6f, Sfx.BossSplat);
        }

        /// Host, every tick, for each Bloodbath (TickZones): run the channel and drain its owner, slow what's in it, and every BathTick s
        /// bleed it and heal the owner.
        void TickBloodbath(Zone z, float dt)
        {
            if (!BathOn(z))
                return;  // drying up: only the look is left
            Player p;
            if (!Players.TryGetValue(z.Owner, out p) || p.Downed || p.Slots[Upgrades.UltSlot].Id != AbilityId.Bloodbath || BathAge(z) >= BathTime)
            {
                EndBath(z);
                return;
            }
            if (!p.DevGod)
                PayHealth(p, p.MaxHp * BathDrain * dt);  // the channel's price (never below 1 HP)
            if (p.Hp <= p.MaxHp * BathFloor)
            {
                EndBath(z);
                return;
            }
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f || !InBath(z, e.Pos, e.Radius))
                    continue;
                if (Heavy(e))  // (LateGame.cs: Heavy enemies and bosses) the usual slow skips them, so they wade instead
                {
                    e.WadeT = Math.Max(e.WadeT, 0.2f);
                    e.WadeMult = Math.Min(e.WadeMult, 1f - BathSlowFor(e));
                }
                else  // the usual slow, so what cares about slowed enemies (Graviton Rounds) counts it
                {
                    e.SlowT = Math.Max(e.SlowT, 0.2f);
                    e.SlowMult = Math.Min(e.SlowMult, 1f - BathSlowFor(e));
                }
            }
            z.TickT += dt;
            while (z.TickT >= BathTick)
            {
                z.TickT -= BathTick;
                BathBleedTick(z);
            }
        }

        void BathBleedTick(Zone z)
        {
            float heal = 0f, dmg = BathBleed * BathTick * z.DmgMult;
            bathScratch.Clear();
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && InBath(z, e.Pos, e.Radius))
                    bathScratch.Add(e);
            foreach (var e in bathScratch)
            {
                float before = e.Hp;
                bathTick = true;
                try
                {
                    HurtEnemy(e, dmg, z.Owner);
                }
                finally
                {
                    bathTick = false;
                }
                heal += Math.Max(0f, before - Math.Max(0f, e.Hp)) * z.HealBack;
            }
            bathScratch.Clear();
            Player p;
            if (heal <= 0f || !Players.TryGetValue(z.Owner, out p))
                return;
            float healed = Heal(p, heal, p, "Bloodbath");  // nothing while you're down; the pool goes on
            if (healed >= 0.5f)
                BroadcastFx(FxKind.BathHeal, p.Pos, new V2(p.Id, 0f), new Rgba(1f, 0.35f, 0.4f), healed, BathHealFxTime, Sfx.None);
        }
    }
}
