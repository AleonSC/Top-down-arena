using System;

namespace Arena
{
    /// The Rapier's Tempo and Flourish (Docs/Mockups/rapier-tempo.html). The thrust itself is in FireKnightWeapon and LandSwing
    /// (Knight.cs).
    ///   - Tempo: every enemy a thrust hits adds a stack (up to RapierTempoMax; skewered bullets add none). The more Tempo, the
    ///     sooner the next thrust: RapierSlow at 0, RapierFast at full, straight between. After the last hit Tempo holds for
    ///     RapierGrace seconds, then drains RapierDrain stacks a second.
    ///   - Flourish (the weapon-action key, Msg.WeaponDash DashFlourish): with at least RapierFlourishMin Tempo, spend all of
    ///     it. The blade whips back and forth, one swing per two stacks, RapierFlourishSwing apart; each swing throws a crescent
    ///     wave (a piercing KnightWave) down your aim. No thrusts until it's done.
    public partial class Game
    {
        public const int RapierTempoMax = 20, RapierFlourishMin = 3;
        public const float RapierSlow = 0.48f, RapierFast = 0.16f, RapierMinInterval = 0.08f;
        public const float RapierGrace = 1.2f, RapierDrain = 2f;
        public const float RapierFlourishSwing = 0.1f, RapierWaveDmg = 22f, RapierWaveSpeed = 560f, RapierWaveRange = 380f;
        const float RapierWaveSkew = 0.09f, RapierFlourishArc = 1.9f;  // radians: each wave's lean off the aim; each swing's arc

        /// Seconds between thrusts at this much Tempo, before fire-rate upgrades.
        public static float RapierInterval(int tempo) => RapierSlow + (RapierFast - RapierSlow) * Math.Min(1f, Math.Max(0, tempo) / (float)RapierTempoMax);

        /// How many swings (and waves) a Flourish with this much Tempo makes.
        public static int RapierFlourishSwings(int tempo) => (tempo + 1) / 2;

        /// The next thrust's wait: Tempo's interval, sped up by fire-rate upgrades, never under RapierMinInterval.
        float RapierCooldown(Player p) => Math.Max(RapierMinInterval, RapierInterval(p.RapierTempo) / FireRate(p));

        /// Host: a thrust hit `hits` enemies.
        void RapierGainTempo(Player p, int hits)
        {
            p.RapierTempoF = Math.Min(RapierTempoMax, (float)Math.Floor(p.RapierTempoF) + hits);
            p.RapierTempo = (int)p.RapierTempoF;
            p.RapierHitAgo = 0f;
            p.RapierGraceLeft = 255;
        }

        /// Host, every tick for every player: the grace period, the drain, and the Flourish's swings.
        void TickRapier(Player p, float dt)
        {
            if (p.Weapon.Id != WeaponId.Rapier)
            {
                p.RapierTempo = 0;
                p.RapierTempoF = 0f;
                p.RapierGraceLeft = 0;
                p.RapierFlourishLeft = 0;
                return;
            }
            p.RapierHitAgo += dt;
            if (p.RapierTempoF > 0f && p.RapierHitAgo > RapierGrace)
                p.RapierTempoF = Math.Max(0f, p.RapierTempoF - RapierDrain * dt);
            p.RapierTempo = (int)p.RapierTempoF;
            p.RapierGraceLeft = p.RapierTempo > 0 && p.RapierHitAgo < RapierGrace ? (byte)(255f * (1f - p.RapierHitAgo / RapierGrace)) : (byte)0;

            if (p.RapierFlourishLeft <= 0)
                return;
            if (p.Downed)
            {
                p.RapierFlourishLeft = 0;
                return;
            }
            for (p.RapierFlourishT -= dt; p.RapierFlourishLeft > 0 && p.RapierFlourishT <= 0f; p.RapierFlourishT += RapierFlourishSwing)
                RapierFlourishWave(p, RapierFlourishSwings(p.RapierFlourishCharge) - p.RapierFlourishLeft--);
        }

        /// Host: a player pressed the weapon-action key with the Rapier, aiming at `aimAt`.
        void HostRapierFlourish(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Rapier || p.RapierTempo < RapierFlourishMin || p.RapierFlourishLeft > 0 || p.Downed || p.StunT > 0f
                || p.SwallowT > 0f || p.ParryActive || State == MatchState.GameOver)
                return;
            V2 toward = aimAt - p.Pos;
            int n = RapierFlourishSwings(p.RapierTempo);
            p.RapierFlourishAim = toward.Length > 1f ? toward.Angle : p.Aim;
            p.RapierFlourishCharge = p.RapierTempo;
            p.RapierFlourishLeft = n;
            p.RapierFlourishT = 0f;  // the first swing goes this tick
            p.RapierTempo = 0;
            p.RapierTempoF = 0f;
            p.RapierGraceLeft = 0;
            p.RapierHitAgo = 99f;
            p.FireCd = Math.Max(p.FireCd, n * RapierFlourishSwing + 0.12f);  // no thrusts while it plays
        }

        /// One Flourish swing: a sweep across RapierFlourishArc, each one the other way, and its wave.
        void RapierFlourishWave(Player p, int i)
        {
            float aim = p.RapierFlourishAim, side = i % 2 == 0 ? -1f : 1f, reach = MeleeReach(p, p.Weapon.Reach) * 0.75f;
            bool full = p.RapierFlourishCharge >= RapierTempoMax;
            BroadcastFx(FxKind.Slash, p.Pos, p.Pos + V2.FromAngle(aim) * reach, full ? new Rgba(1f, 0.84f, 0.51f) : new Rgba(0.8f, 0.72f, 1f),
                RapierFlourishArc * 0.5f, 0.2f, i == 0 ? Sfx.SwingHeavy : Sfx.SwingLight);
            float a = aim + side * RapierWaveSkew;
            var b = SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(aim) * p.Def.BodyR, V2.FromAngle(a) * RapierWaveSpeed, 0f,
                RapierWaveRange / RapierWaveSpeed, RapierWaveDmg * p.Stats.DmgMult, p.Id);
            b.Pierce += 99;
            p.Run.ShotsFired++;
        }
    }
}
