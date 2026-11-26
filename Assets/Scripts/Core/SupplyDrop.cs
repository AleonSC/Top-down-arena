using System;

namespace Arena
{
    /// Supply Drop: the Healer Soldier's third ultimate (key 2 of the lobby's Ultimate row). A smoke flare at your cursor (up to
    /// `SupplyReach` away) calls in a crate: for `SupplyCallIn` s a red ring marks where it lands, then it slams down for
    /// `SupplyImpactDmg` to every enemy within `SupplyBlast` and staggers them. It opens over `SupplyOpen` s into a
    /// `SupplyRadius` zone that lasts `SupplyTime` s: every player inside has their weapon and ability cooldowns recharge
    /// `SupplyCdMult` times as fast (not the ultimate), fires `SupplyFire` times as fast and moves `SupplyMove` times as fast.
    /// Every `SupplyPackEvery` s the crate throws out a health pack (a medkit orb, `SupplyPackMax` at most on the floor) that
    /// heals `SupplyPackHeal`. Then it folds away over `SupplyFold` s.
    /// One `ZoneKind.SupplyDrop` zone; its replicated timer gives every screen the phase. The buff is `Player.SupplyT`
    /// (replicated), kept topped up by the host while you stand inside. Look: Game/SupplyDropLook.cs. Mockup:
    /// https://claude.ai/artifact/1ck4pSWwzcDfzphP6APDyi. Test suite `supplydrop`; preview `SUPPLY=<s since the cast>`.
    public partial class Game
    {
        public const float SupplyReach = 520f, SupplyCallIn = 1.5f, SupplyOpen = 0.4f, SupplyFold = 0.6f;
        public const float SupplyTime = 12f, SupplyTimeMastered = 18f;
        public const float SupplyRadius = 170f, SupplyBlast = 90f, SupplyImpactDmg = 60f, SupplyStagger = 0.4f;
        public const float SupplyCdMult = 1.4f, SupplyFire = 1.3f, SupplyMove = 1.2f;
        public const float SupplyLinger = 0.25f;  // the buff outlasts leaving the zone by this much (so it doesn't flicker at the edge)
        public const float SupplyPackEvery = 2f, SupplyPackHeal = 25f, SupplyPackHealMastered = 40f, SupplyPackLife = 8f;
        public const float SupplyPackMin = 60f, SupplyPackMax = 150f;  // how far out a pack lands
        public const int SupplyPackCap = 4;

        /// Its entry: key 2 of the Healer Soldier's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] SupplyUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.SupplyDrop, Kit = Kit.Soldier, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Supply Drop", Cooldown = 110f,
                Desc = "Call in a crate that hits enemies where it lands, then opens: faster cooldowns, shooting and movement inside, and health packs.",
                Capstone = "The zone lasts 18 s and packs heal 40.",
                Bot = b => b.AllyHurt || b.HaveTarget && b.TargetD < 460f,
            },
        };

        /// Seconds since the flare was thrown, from the zone's replicated timer.
        public static float SupplyAge(Zone z) => z.Duration - z.T;

        /// The crate has landed.
        public static bool SupplyLanded(Zone z) => SupplyAge(z) >= SupplyCallIn;

        /// The zone is up: opened (halfway through the lid popping) and not yet folding away.
        public static bool SupplyZoneOn(Zone z) => SupplyAge(z) >= SupplyCallIn + SupplyOpen * 0.5f && z.T > SupplyFold;

        /// How long the zone stays open, from the zone's own length (mastered lasts longer).
        public static float SupplyZoneTime(Zone z) => z.Duration - SupplyCallIn - SupplyOpen - SupplyFold;

        /// This player stands in an open Supply Drop.
        public static bool Supplied(Player p) => p.SupplyT > 0f && !p.Downed;

        /// Host: throw the flare.
        void CastSupplyDrop(Player p, V2 target, bool cap)
        {
            V2 to = target - p.Pos;
            if (to.Length > SupplyReach)
                to = to.Normalized() * SupplyReach;
            V2 at = ClampToArena(p.Pos + to, 40f);
            AddZone(ZoneKind.SupplyDrop, p, at, SupplyRadius, SupplyCallIn + SupplyOpen + (cap ? SupplyTimeMastered : SupplyTime) + SupplyFold);
            BroadcastFx(FxKind.Ring, at, new Rgba(1f, 0.35f, 0.4f), SupplyBlast, 0.5f, Sfx.Throw);
        }

        /// Host, each tick for a Supply Drop zone: the landing, the buff and the health packs.
        void TickSupplyDrop(Zone z, float dt)
        {
            float age = SupplyAge(z), before = age - dt;
            if (before < SupplyCallIn && age >= SupplyCallIn)
                SupplyImpact(z);
            if (!SupplyZoneOn(z))
                return;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.CloserThan(z.Pos, z.Radius))
                    q.SupplyT = SupplyLinger;

            float first = SupplyCallIn + SupplyOpen;  // the first pack comes as the lid comes off
            for (int k = 0; ; k++)
            {
                float at = first + SupplyPackEvery * k;
                if (at > age || z.T - (age - at) <= SupplyFold)  // (none while it folds)
                    break;
                if (at <= before)
                    continue;
                ThrowSupplyPack(z);
            }
        }

        void SupplyImpact(Zone z)
        {
            foreach (var e in FoesInCircle(z.Pos, SupplyBlast, by: z.Owner))
            {
                HurtEnemy(e, SupplyImpactDmg * z.DmgMult, z.Owner);
                if (!e.Def.IsBoss)
                    Stun(e, SupplyStagger);
            }
            BroadcastFx(FxKind.Ring, z.Pos, new Rgba(1f, 0.8f, 0.35f), SupplyBlast, 0.45f, Sfx.Explode);
        }

        /// One health pack out of the crate, unless there are already enough of its own on the floor.
        void ThrowSupplyPack(Zone z)
        {
            int out_ = 0;
            foreach (var o in Orbs.Values)
                if (o.Supply == z.Id)
                    out_++;
            if (out_ >= SupplyPackCap)
                return;
            bool cap = SupplyZoneTime(z) > SupplyTime + 1f;
            V2 at = ClampToArena(z.Pos + RandDir() * Rand(SupplyPackMin, SupplyPackMax), 12f);
            var orb = new Orb { Id = nextOrbId++, Med = true, Supply = z.Id, MedHeal = (cap ? SupplyPackHealMastered : SupplyPackHeal) * z.HealMult, Pos = at, Life = SupplyPackLife };
            orb.Disp = z.Pos;  // it pops out of the crate
            Orbs[orb.Id] = orb;
            BroadcastFx(FxKind.Ring, z.Pos, new Rgba(0.4f, 1f, 0.55f), 40f, 0.3f, Sfx.None);
        }
    }
}
