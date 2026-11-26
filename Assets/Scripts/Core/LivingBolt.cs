using System;
using System.Collections.Generic;

namespace Arena
{
    /// Living Bolt: the DPS Elementalist's second ultimate (lobby key 1 of the Ultimate row; Bullet Rain stays the default).
    /// From the mockup (claude.ai artifact "Living Bolt"):
    ///   - Channel (BoltChannel s): arcs gather onto you; you can't move or attack.
    ///   - Electrified (BoltTime s): your weapon is put away and Fire is the dash. Each click blinks you to the cursor (up to
    ///     BoltRange px; walls and props stop it short, as the Blink), as often as you like with only BoltLock s between dashes.
    ///     You can't be hurt for the blink (BoltInvuln s).
    ///   - Shock: every enemy within BoltWidth px of the path (plus its size) takes BoltDamage and a BoltStun s stun (heavies and
    ///     bosses: the damage only). It's lightning: wet enemies arc on (WetArcs).
    ///   - Residue: each path stays live (ZoneKind.StaticResidue, one lane a dash) for ResidueTime s: ResidueDps a second to
    ///     enemies touching it, ticked every ResidueTick s (an enemy on several lanes of one player's is hurt once a tick).
    ///   - Discharge: when the time runs out, DischargeDamage to every enemy within DischargeRadius px of you. Going down, the
    ///     wave ending or the slot changing ends it at once, with no discharge.
    /// All damage scales by your damage. Mastered: BoltTimeMastered s, and the residue stays ResidueTimeMastered s.
    /// Like the Blink, your own game moves you (LocalLivingBolt) and sends Msg.WeaponDash (WeaponAlt.Bolt) to the host, which shocks
    /// along the path from where it last had you (HostBoltDash).
    /// Replicated: Player.BoltT (time left, the channel included; 0 = off) and BoltAge (seconds since the cast); the residue is
    /// a zone (Pos its middle, Aim its heading, Radius half its length). FxKind.LivingBolt is one dash; the shocks are
    /// FxKind.RodArc. Drawn by Game/LivingBoltLook.cs.
    public partial class Game
    {
        public const float BoltChannel = 0.5f, BoltTime = 10f, BoltTimeMastered = 13f;
        public const float BoltRange = 260f, BoltLock = 0.12f, BoltBotLock = 0.4f, BoltInvuln = 0.12f, BoltMinDash = 12f;
        public const float BoltWidth = 30f, BoltDamage = 40f, BoltStun = 0.3f;
        public const float ResidueTime = 3f, ResidueTimeMastered = 5f, ResidueDps = 12f, ResidueTick = 0.25f, ResidueWidth = 14f;
        public const float DischargeRadius = 130f, DischargeDamage = 60f;
        public const float BoltDashFx = 0.35f;     // a dash's look: the bolt and its afterimages fade out over this
        const float BoltSlack = 80f;               // the host lets a dash run this much past its reach (where it had you lags a little)
        public static readonly Rgba BoltCyan = new Rgba(0.5f, 0.91f, 1f), BoltViolet = new Rgba(0.79f, 0.65f, 1f);
        public static readonly Rgba BoltWhite = new Rgba(0.96f, 0.99f, 1f), BoltGold = new Rgba(1f, 0.79f, 0.29f);

        /// Living Bolt's entry: key 1 of the DPS Elementalist's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] BoltUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.LivingBolt, Kit = Kit.Elementalist, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Living Bolt", Cooldown = 80f,
                Desc = "Become lightning for 10 s: every click blinks you to the cursor, shocking all you pass and leaving a live trail.",
                Capstone = "Lasts 13 s and the trail stays 5 s.",
                Bot = b => b.HaveTarget && b.TargetD < BoltRange + 120f,
            },
        };

        /// Electrified (the channel included).
        public static bool BoltOn(Player p) => p.BoltT > 0f && !p.Downed;
        /// Past the channel: Fire dashes.
        public static bool BoltCharged(Player p) => BoltOn(p) && p.BoltAge >= BoltChannel;
        /// Your speed share (Player.Speed): rooted while it channels.
        public static float BoltSpeed(Player p) => p.BoltT > 0f && p.BoltAge < BoltChannel ? 0f : 1f;

        /// Where a dash toward `aimAt` lands: up to BoltRange along the line to it (along your aim if the cursor is on you),
        /// stopped short by walls and props.
        public V2 BoltLanding(Player p, V2 aimAt)
        {
            V2 off = aimAt - p.Pos;
            if (off.Length < 1f)
                off = V2.FromAngle(p.Aim);
            return TeleportLanding(p.Pos, p.Pos + off.LimitLength(BoltRange), p.Def.BodyR);
        }

        /// A residue lane: does a body of radius r at pos touch it?
        public static bool InResidue(Zone z, V2 pos, float r)
        {
            V2 ex = V2.FromAngle(z.Aim), d = pos - z.Pos;
            float along = Math.Max(-z.Radius, Math.Min(z.Radius, d.Dot(ex)));
            return pos.CloserThan(z.Pos + ex * along, ResidueWidth + r);
        }

        void CastLivingBolt(Player p, bool mastered)
        {
            p.BoltT = BoltChannel + (mastered ? BoltTimeMastered : BoltTime);
            p.BoltAge = 0f;
            p.BoltLockT = 0f;
            p.BoltMastered = mastered;
            p.BoltUp = true;
            p.UltT = p.BoltT;
            BroadcastFx(FxKind.Ring, p.Pos, BoltViolet, 70f, BoltChannel, Sfx.SpellChainArc);
        }

        /// It ends: on time with the discharge; going down, the wave over or the slot changed without.
        void EndLivingBolt(Player p, bool discharge)
        {
            if (!p.BoltUp)
                return;
            p.BoltUp = false;
            p.BoltT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.LivingBolt)
                p.UltT = 0f;
            if (!discharge)
                return;
            float dmg = DischargeDamage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Pos.FartherOrAt(p.Pos, DischargeRadius + e.Radius))
                    continue;
                HurtEnemy(e, dmg, p.Id);
                WetArcs(e.Pos, e, dmg, p.Id);
                p.BoltDischarged++;
                BroadcastFx(FxKind.RodArc, p.Pos, e.Pos, BoltGold, dmg, 0.3f, Sfx.None);
            }
            BroadcastFx(FxKind.Ring, p.Pos, BoltGold, DischargeRadius, 0.4f, Sfx.Shockwave);
        }

        float residueTickT;
        readonly HashSet<long> residueHit = new HashSet<long>();

        /// Host, every tick: the clocks, the ends, the residue's damage.
        void TickLivingBolt(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.BoltUp)
                    continue;
                p.BoltT = Math.Max(0f, p.BoltT - dt);
                p.BoltAge += dt;
                p.BoltLockT -= dt;
                if (p.Slots[Upgrades.UltSlot].Id != AbilityId.LivingBolt || p.Downed || !Battle)
                    EndLivingBolt(p, false);
                else if (p.BoltT <= 0f)
                    EndLivingBolt(p, true);
            }
            residueTickT += dt;
            if (residueTickT < ResidueTick)
                return;
            residueTickT -= ResidueTick;
            residueHit.Clear();
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.StaticResidue || z.T <= 0f)
                    continue;
                Player owner;
                Players.TryGetValue(z.Owner, out owner);
                foreach (var e in AllFoes(by: z.Owner))
                {
                    if (e.SpawnT > 0f || !InResidue(z, e.Pos, e.Radius) || !residueHit.Add(z.Owner * 1000000L + e.Id))
                        continue;
                    HurtEnemy(e, ResidueDps * ResidueTick * z.DmgMult, z.Owner);
                    if (owner != null)
                        owner.BoltResidueHits++;
                }
            }
        }

        /// Host: a dash to `to` (Msg.WeaponDash WeaponAlt.Bolt, or the host's own game). The path runs from where the host has them.
        void HostBoltDash(Player p, V2 to)
        {
            if (!BoltCharged(p) || p.BoltLockT > 0f || State == MatchState.GameOver || !to.IsFinite)
                return;
            V2 from = p.Pos;
            V2 path = to - from;
            float len = path.Length;
            if (len > BoltRange + BoltSlack)
            {
                path *= (BoltRange + BoltSlack) / len;
                len = BoltRange + BoltSlack;
            }
            if (len < 1f)
                return;
            to = from + path;
            V2 dir = path / len;
            p.BoltLockT = BoltLock * 0.5f;  // (half: the player's own game keeps the real gap; this only stops a flood)
            p.InvulnT = Math.Max(p.InvulnT, BoltInvuln);
            p.BoltDashes++;
            float dmg = BoltDamage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                float along = Math.Max(0f, Math.Min(len, (e.Pos.X - from.X) * dir.X + (e.Pos.Y - from.Y) * dir.Y));
                V2 near = from + dir * along;
                if (e.Pos.FartherThan(near, e.Radius + BoltWidth))
                    continue;
                if (!Heavy(e))
                    Stun(e, BoltStun);
                HurtEnemy(e, dmg, p.Id);
                WetArcs(e.Pos, e, dmg, p.Id);
                p.BoltShocks++;
                BroadcastFx(FxKind.RodArc, near, e.Pos, BoltCyan, dmg, 0.22f, Sfx.None);
            }
            var z = AddZone(ZoneKind.StaticResidue, p, from + path * 0.5f, len * 0.5f, p.BoltMastered ? ResidueTimeMastered : ResidueTime);
            z.Aim = dir.Angle;
            BroadcastFx(FxKind.LivingBolt, from, to, BoltCyan, p.Id, BoltDashFx, Sfx.SpellZap);
        }

        float boltLocalCd, boltBuffered;
        bool boltWasFiring;

        /// Your own game, each tick: while electrified Fire is the dash (true: Fire is taken, nothing else shoots). A click
        /// dashes (one made in the lockout goes as soon as it ends); a bot holding Fire dashes every BoltBotLock s.
        bool LocalLivingBolt(Player me, bool firing, V2 aimAt, float dt)
        {
            boltLocalCd -= dt;
            boltBuffered -= dt;
            if (!BoltOn(me))
            {
                boltWasFiring = false;
                boltBuffered = 0f;
                return false;
            }
            if (firing && (!boltWasFiring || Bot))
                boltBuffered = BoltLock + 0.05f;
            boltWasFiring = firing;
            if (boltBuffered <= 0f || !BoltCharged(me) || boltLocalCd > 0f || me.StunT > 0f || me.ParryActive || State == MatchState.GameOver)
                return true;
            boltBuffered = 0f;
            V2 to = BoltLanding(me, aimAt);
            if (to.DistanceTo(me.Pos) < BoltMinDash)
                return true;
            if (IsHost)
                HostBoltDash(me, to);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Bolt);
                w.Write(to);
                session.SendToHost(Proto.End(w));
            }
            me.Pos = to;
            DashT = 0f;
            boltLocalCd = Bot ? BoltBotLock : BoltLock;
            return true;
        }

        // --- Network: the player's Living Bolt fields (SyncPlayer) ---

        void SyncLivingBolt(NetSync s, Player p)
        {
            s.Float(ref p.BoltT);  // Living Bolt
            s.Float(ref p.BoltAge);
        }
    }
}
