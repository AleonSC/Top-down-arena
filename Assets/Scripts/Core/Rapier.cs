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
        // Riposte: press the weapon-action key to stand guard (again to drop it), click to lunge. Reflected bullets fill the charge; it also creeps up on its own.
        public const float RapierStanceSlow = 0.4f, RapierStanceRadius = 62f, RapierStanceHalfArc = 70f * MathX.Pi / 180f, RapierStanceBand = 14f;
        public const float RapierChargePassive = 0.04f, RapierChargePerBullet = 0.1f, RapierLungeMinCharge = 0.06f;  // charge is 0 to 1: per second held; per bullet reflected
        public const float RapierLungeMin = 130f, RapierLungeMax = 720f, RapierLungeSpeed = 1500f, RapierLungeBase = 20f, RapierLungeBonus = 50f, RapierLungeWidth = 18f;
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

        /// How far a lunge with this much charge (0 to 1) goes, and what it deals each enemy before damage upgrades.
        public static float RapierLungeLength(float charge) => RapierLungeMin + (RapierLungeMax - RapierLungeMin) * Math.Min(1f, Math.Max(0f, charge));
        public static float RapierLungeDamage(float charge) => RapierLungeBase + RapierLungeBonus * Math.Min(1f, Math.Max(0f, charge));

        /// Host: the stance goes up (on), or comes down in a lunge along `aimAt` (off: your primary click while on guard, RapierClickLunge).
        void HostRapierStance(Player p, bool on, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Rapier || State == MatchState.GameOver)
                return;
            if (on)
            {
                if (p.RapierGuard || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.ParryActive || p.RapierLungeDur > 0f || p.RapierFlourishLeft > 0)
                    return;
                p.RapierGuard = true;
                p.RapierChargeF = 0f;
                p.RapierCharge = 0;
                BroadcastSound(Sfx.GreatRaise);
                return;
            }
            if (!p.RapierGuard)
                return;
            float charge = p.RapierChargeF;
            p.RapierGuard = false;
            p.RapierChargeF = 0f;
            p.RapierCharge = 0;
            if (p.Downed || p.StunT > 0f || p.SwallowT > 0f || charge < RapierLungeMinCharge)
                return;  // (dropped by a stun or let go too soon: no lunge)
            V2 toward = aimAt - p.Pos;
            V2 dir = toward.Length > 1f ? toward.Normalized() : V2.FromAngle(p.Aim);
            V2 to = ClampToArena(p.Pos + dir * RapierLungeLength(charge), p.Def.BodyR);
            p.RapierLungeFrom = p.Pos;
            p.RapierLungeDir = dir;
            p.RapierLungeLen = p.Pos.DistanceTo(to);
            p.RapierLungeDur = Math.Max(0.05f, p.RapierLungeLen / RapierLungeSpeed);
            p.RapierLungeEl = 0f;
            p.RapierLungeDmg = RapierLungeDamage(charge) * p.Stats.DmgMult;
            p.RapierLungeHit.Clear();
            p.InvulnT = Math.Max(p.InvulnT, p.RapierLungeDur + 0.12f);
            p.FireCd = Math.Max(p.FireCd, p.RapierLungeDur + 0.1f);
            CarryPlayer(p, to, p.RapierLungeDur);
            BroadcastFx(FxKind.GauntletCharge, p.Pos, to, new Rgba(0.8f, 0.72f, 1f), p.Id, p.RapierLungeDur + 0.8f, Sfx.Blink);
        }

        /// Host: the weapon-action key was pressed with the stance up: it drops, charge and all, with no lunge.
        void RapierDropStance(Player p)
        {
            p.RapierGuard = false;
            p.RapierChargeF = 0f;
            p.RapierCharge = 0;
        }

        /// Host: your primary click while on guard lunges along your aim (the press itself, not a held button).
        void RapierClickLunge(Player p)
        {
            bool click = p.Firing && !p.RapierFireWas;
            p.RapierFireWas = p.Firing;
            if (click && p.RapierGuard)
                HostRapierStance(p, false, p.Pos + V2.FromAngle(p.Aim) * 100f);
        }

        /// Host: the stance stops bullets in front of you and sends them back as your own; each adds charge.
        void RapierStanceReflect(Player p)
        {
            guardStopped.Clear();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                if (Math.Abs(b.Pos.DistanceTo(p.Pos) - RapierStanceRadius) > RapierStanceBand + b.Radius || AngleTo(p.Pos, b.Pos, p.Aim) > RapierStanceHalfArc)
                    continue;
                guardStopped.Add(b.Id);
            }
            foreach (int id in guardStopped)
            {
                Bullet b;
                if (!Bullets.TryGetValue(id, out b))
                    continue;
                AddFx(FxKind.Spark, b.Pos, new Rgba(0.8f, 0.72f, 1f), 8f, 0.2f);
                V2 back = b.Vel.IsZero ? V2.FromAngle(p.Aim) : -b.Vel.Normalized();
                Bullets.Remove(id);
                pendingRemoved.Add(id);
                SpawnBullet(Defs.ArcaneShot, b.Pos, back * 520f, 0f, 1f, 12f * p.Stats.DmgMult, p.Id);
                p.RapierChargeF = Math.Min(1f, p.RapierChargeF + RapierChargePerBullet);
            }
            if (guardStopped.Count > 0)
                BroadcastSound(Sfx.GreatBlock);
        }

        /// Host: the lunge in flight cuts every enemy along its path once, each adding a stack of Tempo.
        void TickRapierLunge(Player p, float dt)
        {
            p.RapierLungeEl += dt;
            float u = Math.Min(1f, p.RapierLungeEl / p.RapierLungeDur), len = p.RapierLungeLen * u;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || p.RapierLungeHit.Contains(e.Id))
                    continue;
                float along = Math.Max(0f, Math.Min(len, (e.Pos - p.RapierLungeFrom).Dot(p.RapierLungeDir)));
                if (e.Pos.DistanceTo(p.RapierLungeFrom + p.RapierLungeDir * along) > e.Radius + RapierLungeWidth)
                    continue;
                p.RapierLungeHit.Add(e.Id);
                HurtEnemy(e, p.RapierLungeDmg, p.Id);
                RapierGainTempo(p, 1);
                BroadcastFx(FxKind.GauntletBowl, e.Pos, new V2(-p.RapierLungeDir.Y, p.RapierLungeDir.X), new Rgba(0.8f, 0.72f, 1f), e.Radius, 0.5f, Sfx.Hit);
            }
            if (u >= 1f)
                p.RapierLungeDur = 0f;
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
                p.RapierGuard = false;
                p.RapierCharge = 0;
                p.RapierLungeDur = 0f;
                return;
            }
            if (p.RapierLungeDur > 0f)
                TickRapierLunge(p, dt);
            if (p.RapierGuard)
            {
                if (p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.ParryActive || State == MatchState.GameOver)
                    p.RapierGuard = false;  // (the stance drops with what held it)
                else
                {
                    p.RapierChargeF = Math.Min(1f, p.RapierChargeF + RapierChargePassive * dt);
                    RapierClickLunge(p);
                    RapierStanceReflect(p);
                    p.RapierHitAgo = Math.Min(p.RapierHitAgo, 0.01f);  // Tempo holds while you stand guard
                }
            }
            p.RapierCharge = (byte)(p.RapierGuard ? Math.Min(255f, p.RapierChargeF * 255f) : 0f);
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
