using System;

namespace Arena
{
    /// Forbidden Path: the DPS Ninja's second ultimate (lobby key 1 of the Ultimate row; Death Lotus stays the default).
    /// From its mockup (claude.ai artifact "Forbidden Path"):
    ///   - Shed the skin (FpShed s): smoke and a red seal; you can't be hit and can't move. Then you're a shadow beast on all
    ///     fours for FpTime s: FpSpeedMult speed, the weapon put away, and a drain of FpDrain of your max health a second (never
    ///     below 1 HP). Each kill the combo makes heals FpKillHeal of your max health.
    ///   - Lock-on: the healthiest enemy within FpLockRange px (kept while it lives and stays within FpKeepRange). With no lock,
    ///     Fire does nothing; when the target dies, the next healthiest in reach is locked at once.
    ///   - Fire (held or clicked) runs the next stage of a five-stage area combo on the locked target; a stage can't be cut
    ///     short, and you're rooted while it runs. FpReset s without a stage starts the combo over.
    ///       1 Rending Pounce (FpPounceTime): leap beside the target (untouchable for the leap), three claw slashes and a
    ///         FpPounceR px burst: FpPounceDmg.
    ///       2 Crescent Rake (FpRakeTime): two crossing 160 degree rakes toward it, FpRakeR px: FpRakeDmg each.
    ///       3 Gnashing Spiral (FpSpiralTime, untouchable): you circle it (FpSpiralPos), pulling enemies within FpPullR px in,
    ///         biting six times: FpBiteDmg to the target and anything near your jaws.
    ///       4 Shadow Rend (FpRendTime): three shades lock the three nearest other enemies in reach and tear through them:
    ///         FpRendDmg each, FpRendPathDmg to anything else on their path.
    ///       5 Forbidden Gate (FpGateTime): rise (untouchable) and slam a black sun on the target: FpGateDmg within FpGateR px,
    ///         then every Blood Brand on enemies within FpLockRange bursts for FpBrandDmg each.
    ///   - Blood Brand: every combo hit brands the enemy (up to FpBrandMax; they fade FpBrandTime s after the last).
    /// All damage scales by your damage and the Rare card Forbidden Hunger. Mastered: the beast lasts FpTimeMastered s.
    /// Host-run: the stage clock, hits and moves (MovePlayer) are the host's; your game only sends Fire (the input).
    /// Replicated: Player.FpT, FpAge, FpStage, FpStageT, FpTarget, FpAt, FpFrom; Enemy.FpBrands. FxKind.ForbiddenPath (Radius =
    /// FpFx*) marks the moments. Drawn by Game/ForbiddenPathLook.cs.
    public partial class Game
    {
        public const float FpShed = 0.6f, FpTime = 8f, FpTimeMastered = 11f, FpSpeedMult = 1.35f;
        public const float FpDrain = 0.03f, FpKillHeal = 0.05f, FpReset = 2.5f, FpHungerDmg = 0.2f;
        public const float FpLockRange = 420f, FpKeepRange = 520f;
        public const float FpPounceTime = 0.55f, FpLeap = 0.3f, FpPounceR = 120f, FpPounceDmg = 30f;
        public const float FpRakeTime = 0.7f, FpRakeR = 150f, FpRakeHalf = 1.4f, FpRakeDmg = 18f;
        public const float FpRakeAt1 = 0.12f, FpRakeAt2 = 0.42f;
        public const float FpSpiralTime = 1.4f, FpPullR = 200f, FpPullSpeed = 170f, FpBiteDmg = 8f, FpJawR = 60f;
        public const int FpBites = 6;
        public const float FpRendTime = 1f, FpRendPick = 0.15f, FpRendGap = 0.15f, FpShadeRun = 0.23f, FpRendDmg = 25f, FpRendPathDmg = 12f, FpRendWidth = 26f;
        public const float FpGateTime = 1.4f, FpGateRise = 0.55f, FpGateSlam = 0.75f, FpGateBurst = 1f, FpGateR = 260f, FpGateDmg = 90f;
        public const int FpBrandMax = 5;
        public const float FpBrandDmg = 10f, FpBrandTime = 6f;
        // FxKind.ForbiddenPath's Radius: which moment
        public const int FpFxShed = 0, FpFxPounce = 1, FpFxRake = 2, FpFxBite = 3, FpFxShade = 4, FpFxGate = 5, FpFxBrand = 6, FpFxRevert = 7, FpFxRake2 = 8;
        public static readonly Rgba FpRed = new Rgba(0.88f, 0.09f, 0.17f), FpEmber = new Rgba(1f, 0.35f, 0.28f), FpInk = new Rgba(0.024f, 0.02f, 0.03f);

        /// Forbidden Path's entry: key 1 of the DPS Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] FpUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.ForbiddenPath, Kit = Kit.Ninja, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Forbidden Path", Cooldown = 75f,
                Desc = "Become a shadow beast for 8 s: it locks onto the strongest enemy near and Fire tears through a five-part area combo. Drains your health.",
                Capstone = "The beast lasts 11 s.",
                Bot = b => b.HaveTarget && b.TargetD < FpLockRange,
            },
        };

        /// A beast (the shedding included).
        public static bool FpOn(Player p) => p.FpT > 0f && !p.Downed;
        /// A stage's length.
        public static float FpStageTime(int stage) =>
            stage == 1 ? FpPounceTime : stage == 2 ? FpRakeTime : stage == 3 ? FpSpiralTime : stage == 4 ? FpRendTime : stage == 5 ? FpGateTime : 0f;
        /// Your speed share (Player.Speed): rooted while shedding and while a stage runs, faster between them.
        public static float FpSpeed(Player p) => !FpOn(p) ? 1f : p.FpAge < FpShed || p.FpStage != 0 ? 0f : FpSpeedMult;

        /// Where the beast is during the Gnashing Spiral, u = 0..1 through it: round `at`, starting on the side of `from`
        /// (one and three quarter turns, swinging out to 95 px and back in to 45).
        public static V2 FpSpiralPos(V2 at, V2 from, float u)
        {
            float start = (from - at).IsZero ? MathX.Pi : (from - at).Angle;
            float th = start + u * MathX.Pi * 3.5f, r = 45f + 50f * (float)Math.Sin(Math.PI * u);
            return at + V2.FromAngle(th) * r;
        }

        void CastForbiddenPath(Player p, bool mastered)
        {
            p.FpT = FpShed + (mastered ? FpTimeMastered : FpTime);
            p.FpAge = 0f;
            p.FpStage = 0;
            p.FpStageT = 0f;
            p.FpNext = 1;
            p.FpIdle = 0f;
            p.FpTarget = -1;
            p.FpUp = true;
            p.UltT = p.FpT;
            p.InvulnT = Math.Max(p.InvulnT, FpShed);
            BroadcastFx(FxKind.ForbiddenPath, p.Pos, p.Pos, FpRed, FpFxShed, 1.1f, Sfx.NinjaVanish);
        }

        void EndForbiddenPath(Player p)
        {
            if (!p.FpUp)
                return;
            p.FpUp = false;
            p.FpT = 0f;
            p.FpStage = 0;
            p.FpTarget = -1;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.ForbiddenPath)
                p.UltT = 0f;
            BroadcastFx(FxKind.ForbiddenPath, p.Pos, p.Pos, FpRed, FpFxRevert, 0.7f, Sfx.NinjaVanish);
        }

        Enemy FpTargetOf(Player p)
        {
            Enemy e;
            return p.FpTarget >= 0 && Enemies.TryGetValue(p.FpTarget, out e) && e.Hp > 0f && Targetable(e) ? e : null;
        }

        /// The healthiest enemy within FpLockRange (none: null).
        Enemy FpPick(Player p)
        {
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (e.Hp > 0f && Targetable(e) && e.Pos.WithinReach(p.Pos, FpLockRange + e.Radius) && (best == null || e.Hp > best.Hp))
                    best = e;
            return best;
        }

        /// Host, in the fire branch (the weapon is put away): Fire starts the next stage on the locked target.
        void FpTrigger(Player p)
        {
            if (!p.Firing || p.FpStage != 0 || p.FpAge < FpShed || p.ParryActive || p.StunT > 0f || State == MatchState.GameOver)
                return;
            var target = FpTargetOf(p);
            if (target == null)
                return;
            FpStart(p, p.FpNext, target);
        }

        void FpStart(Player p, byte stage, Enemy target)
        {
            p.FpStage = stage;
            p.FpStageT = 0f;
            p.FpFrom = p.Pos;
            p.FpAt = target.Pos;
            p.FpStages++;
            float r = p.Def.BodyR;
            switch (stage)
            {
                case 1:
                    V2 dir = (target.Pos - p.Pos).IsZero ? V2.FromAngle(p.Aim) : (target.Pos - p.Pos).Normalized();
                    MovePlayer(p, TeleportLanding(p.Pos, target.Pos - dir * (target.Radius + r + 6f), r));
                    p.InvulnT = Math.Max(p.InvulnT, FpLeap);
                    break;
                case 3:
                    p.InvulnT = Math.Max(p.InvulnT, FpSpiralTime);
                    break;
                case 4:
                    for (int k = 0; k < p.FpShades.Length; k++)
                        p.FpShades[k] = -1;
                    break;
                case 5:
                    p.InvulnT = Math.Max(p.InvulnT, FpGateSlam);
                    break;
            }
        }

        /// A combo hit: damage, a brand, and a kill's heal.
        void FpHit(Player p, Enemy e, float dmg)
        {
            bool alive = e.Hp > 0f;
            HurtEnemy(e, dmg * p.Stats.DmgMult * p.Stats.FpDmgMult, p.Id);
            p.FpHits++;
            if (e.Stand == null)
            {
                e.FpBrands = (byte)Math.Min(FpBrandMax, e.FpBrands + 1);
                e.FpBrandT = FpBrandTime;
            }
            if (alive && e.Hp <= 0f && e.Stand == null)
            {
                p.FpKills++;
                Heal(p, p.MaxHp * FpKillHeal * p.Stats.FpKillHealMult, p);
            }
        }

        static bool Crossed(float before, float now, float at) => before < at && now >= at;

        /// Host, every tick: the clocks, the drain, the lock-on, the stages, the brands fading.
        void TickForbiddenPath(float dt)
        {
            foreach (var e in Enemies.Values)
                if (e.FpBrands > 0 && (e.FpBrandT -= dt) <= 0f)
                    e.FpBrands = 0;
            foreach (var p in Players.Values)
            {
                if (!p.FpUp)
                    continue;
                if (p.Slots[Upgrades.UltSlot].Id != AbilityId.ForbiddenPath || p.Downed || !Battle)
                {
                    EndForbiddenPath(p);
                    continue;
                }
                p.FpT = Math.Max(0f, p.FpT - dt);
                p.FpAge += dt;
                p.UltT = p.FpT;
                if (p.FpAge >= FpShed && p.Hp > 1f)
                    p.Hp = Math.Max(1f, p.Hp - p.MaxHp * FpDrain * dt);
                var target = FpTargetOf(p);
                if (target == null || target.Pos.FartherThan(p.Pos, FpKeepRange + target.Radius))
                {
                    target = FpPick(p);
                    p.FpTarget = target != null ? target.Id : -1;
                }
                if (p.FpStage != 0)
                    FpTickStage(p, target, dt);
                else if ((p.FpIdle += dt) > FpReset)
                    p.FpNext = 1;
                if (p.FpT <= 0f && p.FpStage == 0)
                    EndForbiddenPath(p);  // (a stage running when the time's up finishes first)
            }
        }

        void FpTickStage(Player p, Enemy target, float dt)
        {
            float before = p.FpStageT, now = before + dt;
            p.FpStageT = now;
            int stage = p.FpStage;
            V2 at = p.FpAt;
            switch (stage)
            {
                case 1:  // Rending Pounce
                    if (Crossed(before, now, FpLeap))
                    {
                        foreach (var e in FoesInCircle(p.Pos, FpPounceR, by: p.Id))
                            FpHit(p, e, FpPounceDmg);
                        BroadcastFx(FxKind.ForbiddenPath, p.Pos, at, FpRed, FpFxPounce, 0.8f, Sfx.KatanaHit);
                    }
                    break;
                case 2:  // Crescent Rake
                    for (int k = 0; k < 2; k++)
                        if (Crossed(before, now, k == 0 ? FpRakeAt1 : FpRakeAt2))
                        {
                            float aim = (at - p.Pos).IsZero ? p.Aim : (at - p.Pos).Angle;
                            foreach (var e in FoesInCone(p.Pos, aim, FpRakeHalf, FpRakeR, by: p.Id))
                                FpHit(p, e, FpRakeDmg);
                            BroadcastFx(FxKind.ForbiddenPath, p.Pos, p.Pos + V2.FromAngle(aim) * 10f, FpRed, k == 0 ? FpFxRake : FpFxRake2, 0.45f, Sfx.KatanaSwipe1);
                        }
                    break;
                case 3:  // Gnashing Spiral
                    foreach (var e in Enemies.Values)
                    {
                        if (e.Hp <= 0f || Heavy(e) || !Targetable(e) || e == target || e.Pos.FartherThan(at, FpPullR + e.Radius))
                            continue;
                        V2 off = at - e.Pos;
                        float d = off.Length, keep = e.Radius + 30f;
                        if (d > keep)
                            e.Pos = ClampToArena(e.Pos + off * (Math.Min(FpPullSpeed * dt, d - keep) / d), e.Radius);
                    }
                    for (int k = 0; k < FpBites; k++)
                    {
                        float bite = (k + 1) * FpSpiralTime / (FpBites + 1);
                        if (!Crossed(before, now, bite))
                            continue;
                        V2 jaws = FpSpiralPos(at, p.FpFrom, bite / FpSpiralTime);
                        if (target != null)
                            FpHit(p, target, FpBiteDmg);
                        foreach (var e in FoesInCircle(jaws, FpJawR, by: p.Id))
                            if (e != target)
                                FpHit(p, e, FpBiteDmg);
                        BroadcastFx(FxKind.ForbiddenPath, jaws, at, FpRed, FpFxBite, 0.3f, Sfx.SwingHeavy);
                    }
                    if (now >= FpSpiralTime)
                        MovePlayer(p, TeleportLanding(at, FpSpiralPos(at, p.FpFrom, 1f), p.Def.BodyR));
                    break;
                case 4:  // Shadow Rend
                    if (Crossed(before, now, FpRendPick))
                        FpPickShades(p, target);
                    for (int k = 0; k < p.FpShades.Length; k++)
                    {
                        Enemy e;
                        if (p.FpShades[k] < 0 || !Enemies.TryGetValue(p.FpShades[k], out e))
                            continue;
                        float go = FpRendPick + k * FpRendGap;
                        if (Crossed(before, now, go))
                            BroadcastFx(FxKind.ForbiddenPath, p.Pos, e.Pos, FpRed, FpFxShade, 0.75f, Sfx.NinjaStepStrike);
                        if (!Crossed(before, now, go + FpShadeRun))
                            continue;
                        if (e.Hp > 0f)
                            FpHit(p, e, FpRendDmg);
                        foreach (var o in FoesInBox(p.Pos, e.Pos, FpRendWidth, by: p.Id))
                            if (o != e)
                                FpHit(p, o, FpRendPathDmg);
                    }
                    break;
                case 5:  // Forbidden Gate
                    if (Crossed(before, now, FpGateSlam))
                    {
                        V2 slam = target != null ? target.Pos : at;
                        MovePlayer(p, TeleportLanding(p.Pos, slam, p.Def.BodyR));
                        p.FpAt = slam;
                        foreach (var e in FoesInCircle(slam, FpGateR, by: p.Id))
                            FpHit(p, e, FpGateDmg);
                        BroadcastFx(FxKind.ForbiddenPath, slam, slam, FpRed, FpFxGate, 1.1f, Sfx.BossSlam);
                    }
                    if (Crossed(before, now, FpGateBurst))
                        foreach (var e in Enemies.Values)
                        {
                            if (e.FpBrands == 0 || e.Hp <= 0f || !Targetable(e) || e.Pos.FartherThan(at, FpLockRange + e.Radius))
                                continue;
                            int n = e.FpBrands;
                            e.FpBrands = 0;
                            HurtEnemy(e, FpBrandDmg * n * p.Stats.DmgMult * p.Stats.FpDmgMult, p.Id);
                            if (e.Hp <= 0f)
                            {
                                p.FpKills++;
                                Heal(p, p.MaxHp * FpKillHeal * p.Stats.FpKillHealMult, p);
                            }
                            BroadcastFx(FxKind.ForbiddenPath, e.Pos, e.Pos, FpRed, FpFxBrand, 0.6f, Sfx.None);
                        }
                    break;
            }
            if (now >= FpStageTime(stage))
            {
                p.FpStage = 0;
                p.FpIdle = 0f;
                p.FpNext = (byte)(stage % 5 + 1);
            }
        }

        /// Shadow Rend's targets: the three nearest other enemies in reach of you.
        void FpPickShades(Player p, Enemy target)
        {
            for (int k = 0; k < p.FpShades.Length; k++)
            {
                Enemy best = null;
                foreach (var e in Enemies.Values)
                {
                    if (e == target || e.Hp <= 0f || !Targetable(e) || e.Pos.FartherThan(p.Pos, FpLockRange + e.Radius))
                        continue;
                    bool taken = false;
                    for (int j = 0; j < k; j++)
                        taken |= p.FpShades[j] == e.Id;
                    if (!taken && (best == null || e.Pos.DistanceSq(p.Pos) < best.Pos.DistanceSq(p.Pos)))
                        best = e;
                }
                p.FpShades[k] = best != null ? best.Id : -1;
            }
            if (p.FpShades[0] < 0 && target != null)
                p.FpShades[0] = target.Id;  // alone: one shade tears the target itself
        }

        // --- Network: the player's Forbidden Path fields (SyncPlayer) ---

        void SyncForbiddenPath(NetSync s, Player p)
        {
            s.Float(ref p.FpT);  // Forbidden Path
            if (p.FpT > 0f)
            {
                s.Float(ref p.FpAge);
                s.Byte(ref p.FpStage);
                s.Float(ref p.FpStageT);
                s.Int(ref p.FpTarget);
                s.Vec(ref p.FpAt);
                s.Vec(ref p.FpFrom);
            }
        }
    }
}
