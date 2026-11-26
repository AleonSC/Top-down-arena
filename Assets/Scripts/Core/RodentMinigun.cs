using System;

namespace Arena
{
    /// Rodent Minigun: the DPS Summoner's second ultimate (key 1 of the lobby's Ultimate row; Legion stays the default), from the
    /// mockup (claude.ai artifact "Rodent Minigun").
    ///   - Open (RmOpen s): a small portal tears open RmAhead px in front of you, on your aim.
    ///   - Spin-up (RmSpin s): it starts spitting rodents at RmRateMin a second, rising to RmRateMax.
    ///   - Full fire (RmFull s, RmFullMastered mastered) at RmRateMax, then the wind-down (RmWind s) and the close (RmClose s).
    ///   - The barrel: the portal turns after your aim at RmTurn radians a second, so it drags like a heavy gun. The spread
    ///     widens from RmSpreadMin to RmSpreadMax as it heats.
    ///   - You: your weapon is put away while the portal is open, and you move at RmSlow speed.
    ///   - The ammo (Defs.RodentShot): which rodent each shot is comes from its bullet id (RodentOf), so every screen agrees
    ///     with no extra field. Mouse RmMouseDmg; rat RmRatDmg and a RmRatShove px shove (not heavies or bosses); hamster
    ///     RmHamsterDmg, through one enemy and into a second. All scale by your damage and the ultimate's mastery.
    ///   - It ends early, with no last shots, when you go down, the wave ends or the slot changes.
    /// The rare card Plague Rats (Stats.RatShare, RatBleed): rats come out more often and make what they bite bleed.
    /// Replicated: Player.RmT (seconds since the cast; 0 = off), RmAng (the portal's heading) and RmLen (its full-fire time).
    /// The rodents are ordinary bullets, so they fly and hit like any shot; the portal, the rodents and the ones scampering
    /// off after a hit are drawn by Game/RodentMinigunLook.cs.
    public partial class Game
    {
        public const float RmOpen = 0.4f, RmSpin = 1f, RmFull = 5f, RmFullMastered = 7f, RmWind = 0.6f, RmClose = 0.4f;
        public const float RmRateMin = 6f, RmRateMax = 36f, RmTurn = 240f * MathX.Pi / 180f, RmAhead = 46f;
        public const float RmSpreadMin = 4f * MathX.Pi / 180f, RmSpreadMax = 10f * MathX.Pi / 180f;
        public const float RmSpeed = 610f, RmSpeedJitter = 50f, RmLife = 0.9f, RmSlow = 0.75f;
        public const float RmMouseDmg = 5f, RmRatDmg = 9f, RmHamsterDmg = 6f, RmRatShove = 20f;
        public const float RmRatShare = 0.16f, RmHamsterShare = 0.10f;  // the rest are mice
        public const float RmPlagueShare = 0.14f, RmPlagueBleed = 3f;  // Plague Rats, a stack: rats this much more often, bleeding this much a second
        public enum Rodent : byte { Mouse, Rat, Hamster }

        /// Rodent Minigun's entry: key 1 of the DPS Summoner's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] RodentUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.RodentMinigun, Kit = Kit.Summoner, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Rodent Minigun", Cooldown = 70f,
                Desc = "A portal opens in front of you and spins up into a minigun of mice, rats and hamsters, following your aim.",
                Capstone = "Fires 2 s longer.",
                Bot = b => b.HaveTarget && b.TargetD < 520f,
            },
        };

        /// The whole show, from the cast to the portal gone, for a full-fire time of `full`.
        public static float RmTotal(float full) => RmOpen + RmSpin + full + RmWind + RmClose;

        /// The portal is open (from the cast until it has closed).
        public static bool RodentOn(Player p) => p.RmT > 0f && !p.Downed;

        /// Your speed share (Player.Speed) while the portal is open.
        public static float RodentSpeed(Player p) => p.RmT > 0f ? RmSlow : 1f;

        /// Rodents a second, t s after the cast.
        public static float RmRate(float t, float full)
        {
            float a = RmOpen, b = a + RmSpin, c = b + full, d = c + RmWind;
            if (t < a || t >= d)
                return 0f;
            if (t < b)
            {
                float k = (t - a) / RmSpin;
                return RmRateMin + (RmRateMax - RmRateMin) * k * k;
            }
            return t < c ? RmRateMax : RmRateMax * (1f - (t - c) / RmWind);
        }

        /// 0 to 1: how hot the barrel is (the spread, the look's glow).
        public static float RmHeat(float t, float full) => Math.Max(0f, Math.Min(1f, (t - RmOpen) / (RmSpin + full)));

        /// 0 to 1: how far open the portal is.
        public static float RmOpenness(float t, float full)
        {
            if (t <= 0f)
                return 0f;
            if (t < RmOpen)
                return t / RmOpen;
            float shut = RmTotal(full) - RmClose;
            return t < shut ? 1f : Math.Max(0f, 1f - (t - shut) / RmClose);
        }

        /// Where the portal is: RmAhead px out from `at` along `ang`.
        public static V2 RmMouth(V2 at, float ang) => at + V2.FromAngle(ang) * RmAhead;

        /// Which rodent a shot is, from its bullet id; `ratShare` is the owner's (Plague Rats raises it).
        public static Rodent RodentOf(int id, float ratShare)
        {
            uint h = (uint)id * 2654435761u;
            float r = ((h >> 8) & 0xFFFF) / 65536f;
            return r < ratShare ? Rodent.Rat : r < ratShare + RmHamsterShare ? Rodent.Hamster : Rodent.Mouse;
        }

        /// The owner's share of rats (Plague Rats adds to it).
        public static float RatShareOf(Player p) => RmRatShare + (p != null ? p.Stats.RatShare : 0f);

        void CastRodentMinigun(Player p, bool mastered, float pw)
        {
            p.RmLen = mastered ? RmFullMastered : RmFull;
            p.RmT = 0.0001f;
            p.RmAng = p.Aim;
            p.RmAcc = 0f;
            p.RmPw = pw;
            p.UltT = RmTotal(p.RmLen);
            BroadcastFx(FxKind.Ring, RmMouth(p.Pos, p.Aim), new Rgba(0.69f, 0.49f, 1f), 26f, RmOpen, Sfx.RodentPortal);
        }

        void EndRodentMinigun(Player p)
        {
            p.RmT = 0f;
            p.RmAcc = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.RodentMinigun)
                p.UltT = 0f;
        }

        /// Host, every tick: the clock, the barrel turning after the aim, and the rodents it spits.
        void TickRodentMinigun(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.RmT <= 0f)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.RodentMinigun)
                {
                    EndRodentMinigun(p);
                    continue;
                }
                p.RmT += dt;
                if (p.RmT >= RmTotal(p.RmLen))
                {
                    EndRodentMinigun(p);
                    continue;
                }
                float d = MathX.WrapPi(p.Aim - p.RmAng), step = RmTurn * dt;
                p.RmAng += Math.Max(-step, Math.Min(step, d));
                p.RmAcc += RmRate(p.RmT, p.RmLen) * dt;
                float spread = RmSpreadMin + (RmSpreadMax - RmSpreadMin) * RmHeat(p.RmT, p.RmLen);
                while (p.RmAcc >= 1f)
                {
                    p.RmAcc -= 1f;
                    FireRodent(p, spread);
                }
            }
        }

        void FireRodent(Player p, float spread)
        {
            float ang = p.RmAng + Rand(-spread, spread);
            V2 mouth = RmMouth(p.Pos, p.RmAng) + V2.FromAngle(p.RmAng + MathX.Pi / 2f) * Rand(-5f, 5f);
            var b = SpawnBullet(Defs.RodentShot, mouth, V2.FromAngle(ang) * (RmSpeed + Rand(-RmSpeedJitter, RmSpeedJitter)), 0f, RmLife, 0f, p.Id);
            var kind = RodentOf(b.Id, RatShareOf(p));
            float baseDmg = kind == Rodent.Rat ? RmRatDmg : kind == Rodent.Hamster ? RmHamsterDmg : RmMouseDmg;
            b.Dmg = baseDmg * p.Stats.DmgMult * p.RmPw;
            b.Pierce = kind == Rodent.Hamster ? 1 : 0;
            p.RodentsFired++;
        }

        /// OnBulletHit: a rat shoves what it bites (not heavies or bosses), and with Plague Rats makes it bleed.
        void RodentHit(Bullet b, Enemy e)
        {
            if (b.Kind != Defs.RodentShot)
                return;
            Player p;
            Players.TryGetValue(b.Owner, out p);
            if (RodentOf(b.Id, RatShareOf(p)) != Rodent.Rat)
                return;
            if (!e.Def.IsBoss && !e.Def.Heavy)
                KnockEnemy(e, b.Vel.Normalized() * RmRatShove);
            if (p != null && p.Stats.RatBleed > 0f)
                ApplyBleed(e, p.Stats.RatBleed, b.Owner);
        }

        // --- Network: the player's Rodent Minigun fields (SyncPlayer) ---

        void SyncRodentMinigun(NetSync s, Player p)
        {
            s.Float(ref p.RmT);  // Rodent Minigun
            if (p.RmT > 0f)
            {
                s.Float(ref p.RmAng);
                s.Float(ref p.RmLen);
            }
        }
    }
}
