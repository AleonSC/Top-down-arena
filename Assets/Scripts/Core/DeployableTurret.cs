using System;

namespace Arena
{
    /// Deployable Turret, the DPS Soldier's default ultimate (`AbilityId.DeployableTurret`, key 0 of the lobby's Ultimate row; it
    /// took Artillery Assault's place, and the DPS Soldier's 4th choice is empty for now), from Docs/Mockups/deployable-turret.html. A crate drops on your spot (DtLand s), unfolds (until DtDeploy s) and you
    /// sit in it for DtLife s; a `ZoneKind.DeployTurret` zone: Pos the turret, Aim where its twin barrels point, Radius the seat
    /// (DtSeat), BlockHp its health (DtHp; GuardFrac its fraction, replicated):
    ///   it takes the damage meant for you while you sit in it, after your armour and shields (`TurretSoak` from `Damage`); at 0
    ///   health it is wrecked: you are thrown clear (as stepping out) and it overheats and blows;
    ///   while it stands you are rooted (no walking, dodging or moving abilities) and it fires instead of your weapon: hold Fire
    ///   and it turns onto your aim (DtTurn rad/s) and fires hitscan rounds of DtDamage down the lane (they pierce DtPierce
    ///   enemies, DtPierceMastered mastered); the rate spools up from DtRateLow to DtRateHigh rounds a second over DtSpool s,
    ///   and winds down when you let go;
    ///   it overheats for the last DtDying s (it can't fire) and then blows: DtBlastDamage to every enemy within DtBlast px
    ///   (double mastered). It folds away quietly when you go down or the fight ends.
    /// Mastered: DtLifeMastered s of fire, pierce DtPierceMastered, and the blast doubled. Its Rare card Overclocked Barrels
    /// adds Stats.DtDmgMult and DtBonusTime. The look (Game/DeployTurretLook.cs) draws the drop, the guns, the heat and the
    /// blast on each screen from the zone and its owner's aim and trigger; only the damage is the host's.
    public partial class Game
    {
        public const float DtCooldown = 60f, DtLand = 0.55f, DtDeploy = 1.1f, DtLife = 20f, DtLifeMastered = 26f, DtDying = 0.8f;
        public const float DtRange = 560f, DtDamage = 22f, DtRateLow = 7f, DtRateHigh = 30f, DtSpool = 1.2f, DtTurn = 8f;
        public const float DtBlast = 160f, DtBlastDamage = 80f, DtSeat = 44f, DtHp = 350f;
        public const int DtPierce = 2, DtPierceMastered = 4;

        /// Seconds since the turret was cast.
        public static float DtAge(Zone z) => z.Duration - z.T;

        /// When its guns stop (the overheat starts).
        public static float DtFireEnd(Zone z) => z.Duration - DtDying;

        /// The turret this player is sitting in (or waiting to be dropped to), or null.
        public Zone TurretOf(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.DeployTurret && z.Owner == p.Id && z.T > 0f)
                    return z;
            return null;
        }

        /// In a Deployable Turret: rooted, and it fires instead of your weapon.
        public bool Seated(Player p)
        {
            var z = p != null && !p.Downed ? TurretOf(p) : null;
            return z != null && z.Radius > 0f  // (a Radius of 0 is an owner who has stepped out: it overheats on its own)
                || OnBallista(p);  // riding a Ballista: rooted on its seat, and the bow fires instead (Ballista.cs)
        }

        /// Is this slot the Deployable Turret with you in its seat and its guns still up? Pressing it again steps you out early.
        public bool TurretPress(Player p, int slot) => p.Slots[slot].Id == AbilityId.DeployableTurret && !p.Downed && TurretOf(p) != null && TurretOf(p).Radius > 0f && DtAge(TurretOf(p)) < DtFireEnd(TurretOf(p));

        /// Host: step out. The guns stop, the overheat starts now, you are free and the seat's cut goes; it still blows when it ends,
        /// so the way out is also a fuse.
        void DismountTurret(Player p)
        {
            var z = TurretOf(p);
            if (z == null)
                return;
            z.T = Math.Min(z.T, DtDying);
            z.Radius = 0f;
        }

        /// Host, from Damage: a hit meant for the player in the turret's seat wears the turret first; what's left over goes on to them.
        /// At 0 health it is wrecked: the owner steps out as if they had pressed the key.
        float TurretSoak(Player p, float amount)
        {
            var z = TurretOf(p);
            if (z == null || z.Radius <= 0f || amount <= 0f)
                return amount;
            float soak = Math.Min(z.BlockHp, amount);
            z.BlockHp -= soak;
            z.GuardFrac = z.SoakMax > 0f ? Math.Max(0f, z.BlockHp / z.SoakMax) : 0f;
            if (z.BlockHp <= 0f)
                DismountTurret(p);
            return amount - soak;
        }

        /// Is this player holding Fire, as this machine knows it (their own screen, or the host's, has it in Firing; everyone else's in FiringSeen)?
        public bool HoldingFire(Player p) => p.Id == MyId || !IsHost ? p.FiringSeen : p.Firing;

        void CastDeployTurret(Player p, V2 target, bool cap)
        {
            if (TurretOf(p) != null || OnBallista(p))
                return;  // one at a time (and not from a Ballista's seat)
            float life = (cap ? DtLifeMastered : DtLife) + p.Stats.DtBonusTime;
            var z = AddZone(ZoneKind.DeployTurret, p, p.Pos, DtSeat, DtDeploy + life + DtDying);
            z.Aim = p.Aim;
            z.SoakMax = z.BlockHp = DtHp;
            z.GuardFrac = 1f;
            z.Cap = cap;
        }

        /// Host, every tick, for each turret (TickZones): the turn, the spool, the rounds.
        void TickDeployTurret(Zone z, float dt)
        {
            Player p;
            if (!Players.TryGetValue(z.Owner, out p) || p.Downed || !Battle)
            {
                z.Quiet = true;  // you went down or the fight ended: it folds away with no blast
                z.T = 0f;
                return;
            }
            float age = DtAge(z);
            z.Aim += Math.Max(-dt * DtTurn, Math.Min(dt * DtTurn, MathX.WrapPi(p.Aim - z.Aim)));
            bool fire = p.Firing && !p.ParryActive && age >= DtDeploy && age < DtFireEnd(z);
            z.Spool = fire ? Math.Min(1f, z.Spool + dt / DtSpool) : Math.Max(0f, z.Spool - dt / 0.6f);
            if (!fire)
            {
                z.GunCd = 0f;
                return;
            }
            float ease = z.Spool * z.Spool * (3f - 2f * z.Spool);
            z.GunCd -= dt * (DtRateLow + (DtRateHigh - DtRateLow) * ease);
            for (int guard = 0; z.GunCd <= 0f && guard < 4; guard++)
            {
                z.GunCd += 1f;
                DtShot(z, p);
            }
        }

        /// One round: down the lane from the muzzle, hurting the first enemies it crosses.
        void DtShot(Zone z, Player p)
        {
            z.Side ^= 1;
            V2 dir = V2.FromAngle(z.Aim + Rand(-1f, 1f) * (0.025f + (1f - z.Spool) * 0.025f)), side = dir.Orthogonal();
            V2 from = z.Pos + dir * 56f + side * (z.Side == 0 ? -8f : 8f);
            int pierce = z.Cap ? DtPierceMastered : DtPierce;
            scratchHits.Clear();
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                V2 rel = e.Pos - from;
                float u = rel.Dot(dir), v = Math.Abs(rel.Dot(side));
                if (u < 0f || u > DtRange || v > e.Radius + 3f)
                    continue;
                scratchHits.Add(new System.Collections.Generic.KeyValuePair<float, Enemy>(u, e));
            }
            scratchHits.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < scratchHits.Count && i < pierce; i++)
                HurtEnemy(scratchHits[i].Value, DtDamage * z.DmgMult * p.Stats.DtDmgMult, p.Id);
        }

        readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, Enemy>> scratchHits = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, Enemy>>();

        /// Host, as a turret expires: the overheat blast (not when it folded away quietly).
        void TurretBlow(Zone z)
        {
            if (z.Quiet)
                return;
            Player p;
            if (!Players.TryGetValue(z.Owner, out p))
                return;
            float dmg = DtBlastDamage * z.DmgMult * (z.Cap ? 2f : 1f);
            foreach (var e in FoesInCircle(z.Pos, DtBlast, by: p.Id))
                HurtEnemy(e, dmg, p.Id);
        }
    }
}
