using System;
using System.Collections.Generic;

namespace Arena
{
    /// Spirit Avatar: the Tank Ninja's third ultimate (lobby key 2 of the Ultimate row; Thousand Clones stays the default, Dragon's
    /// Dogma is key 1). A giant ink spirit, the Sumi Giant (Docs/SpiritAvatar.md, Docs/Mockups/spirit-avatar.html look C), wraps you
    /// for SpTime s.
    ///   - Rise (SpRise s): the ink pulls in round you (SpRiseDamage and a shove within SpRiseRadius); you can't be hurt while it forms.
    ///   - Avatar: SpCut less damage, SpMove of your speed, every enemy (bosses too) goes for you, and enemy shots that reach the
    ///     spirit's body (SpBodyR) are swallowed.
    ///   - Moon Cleave: Fire is the cleave (your weapon is put away). A SpWindup s wind-up (the brush drawn back, a red arc filling in
    ///     on the floor, the heading turning after your aim at SpTurn rad/s), then one sweep across SpHalfArc either side of the
    ///     heading out to SpReach px over SpSweep s: SpDamage to each enemy the brush passes, a SpStun s stun and a SpKnock px push
    ///     (heavies: half the push, no stun; bosses: the damage only). One every SpEvery s while Fire is held.
    ///   - Release: when it ends (time, going down, the wave over) the spirit bursts: SpReleaseDamage and a shove within SpReleaseRadius.
    /// All damage scales by your damage. Mastered: SpTimeMastered s and the cleave reaches SpMasterReach further; the Rare card
    /// Kami Vessel adds time and shortens the wind-up.
    /// Replicated: Player.SpT (time left, 0 = off), SpAge (seconds since the cast), SpWind (the cleave: 0 none, 0..1 the wind-up,
    /// 1..2 the sweep) and SpAng (its heading). The floor marks are FxKind.SpiritInk (Game/SpiritAvatarLook.cs).
    public partial class Game
    {
        public const float SpTime = 12f, SpTimeMastered = 15f, SpRise = 0.8f;
        public const float SpCut = 0.6f;                               // damage you take while it stands is cut by this much
        public const float SpMove = 0.85f, SpMoveRising = 0.4f, SpMoveCasting = 0.55f;  // your speed share
        public const float SpRiseRadius = 95f, SpRiseDamage = 30f, SpRiseKnock = 90f;
        public const float SpBodyR = 64f;                              // enemy shots inside this swallowed
        public const float SpWindup = 0.9f, SpSweep = 0.24f, SpEvery = 1.6f, SpTurn = 0.6f;
        public const float SpReach = 170f, SpHalfArc = 100f * MathX.Pi / 180f, SpMasterReach = 0.2f;
        public const float SpBladeFrom = -1.95f, SpBladeTo = 1.95f;    // the brush's sweep, either side of the heading (the look uses it too)
        public const float SpDamage = 80f, SpStun = 0.5f, SpKnock = 150f;
        public const float SpReleaseRadius = 130f, SpReleaseDamage = 40f, SpReleaseKnock = 110f;
        public const float SpVesselTime = 2f, SpVesselWind = 0.2f, SpWindupMin = 0.4f;  // the Rare card Kami Vessel, a stack
        public const float SpStrokeTime = 2f;                          // the ink stroke stays on the floor this long
        public const int SpBotCrowd = 4;

        /// FxKind.SpiritInk's kinds (Pos2.X): the stroke (Pos2.Y its heading, Radius its reach), the rise, the release, a swallowed shot.
        public const int SpFxStroke = 0, SpFxRise = 1, SpFxRelease = 2, SpFxSwallow = 3;

        public static readonly Rgba SpInk = new Rgba(0.04f, 0.045f, 0.065f), SpSeal = new Rgba(0.89f, 0.27f, 0.23f), SpPaper = new Rgba(0.94f, 0.9f, 0.82f);

        /// Spirit Avatar's entry: key 2 of the tank Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] SpiritUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.SpiritAvatar, Kit = Kit.Ninja, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Spirit Avatar", Cooldown = 70f,
                Desc = "A giant ink spirit wraps you: Fire paints a slow, wide Moon Cleave that throws enemies back.",
                Capstone = "Lasts 15 s and the cleave reaches 20% further.",
                Bot = b => b.Crowd >= SpBotCrowd,
            },
        };

        /// The spirit is out (rising included).
        public static bool SpOn(Player p) => p.SpT > 0f && !p.Downed;
        /// Past the rise: the cleave and the shot swallowing work.
        public static bool SpStanding(Player p) => SpOn(p) && p.SpAge >= SpRise;

        static float SpMult(Player p) => SpOn(p) ? 1f - SpCut : 1f;

        /// Your speed share while the spirit is out (Player.Speed).
        public static float SpSpeed(Player p) => p.SpT <= 0f ? 1f : p.SpAge < SpRise ? SpMoveRising : p.SpWind > 0f ? SpMoveCasting : SpMove;

        public static float SpReachOf(Player p) => SpReach * (1f + (p.SpMastered ? SpMasterReach : 0f));
        public static float SpWindupOf(Player p) => Math.Max(SpWindupMin, SpWindup - p.Stats.SpWindCut);

        /// Where the brush points during the sweep, from the heading (0..1 through the sweep; the look eases it the same way).
        public static float SpBladeAt(float k)
        {
            k = Math.Max(0f, Math.Min(1f, k));
            return SpBladeFrom + (SpBladeTo - SpBladeFrom) * (1f - (1f - k) * (1f - k) * (1f - k));
        }

        void CastSpirit(Player p, bool mastered)
        {
            float time = (mastered ? SpTimeMastered : SpTime) + p.Stats.SpBonusTime;
            p.SpT = SpRise + time;
            p.SpAge = 0f;
            p.SpWind = 0f;
            p.SpAng = p.Aim;
            p.SpCastT = -1f;
            p.SpCd = 0f;
            p.SpMastered = mastered;
            p.SpUp = true;
            p.UltT = p.SpT;
            p.InvulnT = Math.Max(p.InvulnT, SpRise);
            foreach (var e in Enemies.Values)  // the taunt, as Stone Colossus
            {
                e.TargetId = p.Id;
                e.RetargetT = Math.Max(e.RetargetT, 1f);
            }
            SpBurst(p, SpRiseRadius, SpRiseDamage, SpRiseKnock);
            BroadcastFx(FxKind.SpiritInk, p.Pos, new V2(SpFxRise, p.Id), SpSeal, SpRiseRadius, SpRise + 0.4f, Sfx.Shockwave);
        }

        /// Damage and a shove to every enemy within `radius` of you (the rise and the release).
        void SpBurst(Player p, float radius, float damage, float knock)
        {
            float dmg = damage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Pos.FartherOrAt(p.Pos, radius + e.Radius))
                    continue;
                if (!Heavy(e))
                    KnockEnemy(e, AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim)) * knock);
                HurtEnemy(e, dmg, p.Id);
            }
        }

        /// The spirit bursts (time, down, the wave over, the slot changed).
        void EndSpirit(Player p)
        {
            if (!p.SpUp)
                return;
            p.SpUp = false;
            p.SpT = 0f;
            p.SpWind = 0f;
            p.SpCastT = -1f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.SpiritAvatar)
                p.UltT = 0f;
            SpBurst(p, SpReleaseRadius, SpReleaseDamage, SpReleaseKnock);
            BroadcastFx(FxKind.SpiritInk, p.Pos, new V2(SpFxRelease, p.Id), SpSeal, SpReleaseRadius, 1.1f, Sfx.BigExplode);
        }

        readonly List<int> spSwallow = new List<int>();

        /// Host, every tick: the clock, the cleave, the swallowed shots.
        void TickSpirit(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.SpUp)
                    continue;
                p.SpT = Math.Max(0f, p.SpT - dt);
                p.SpAge += dt;
                if (p.SpT <= 0f || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.SpiritAvatar)
                {
                    EndSpirit(p);
                    continue;
                }
                p.SpCd -= dt;
                if (p.SpAge < SpRise)
                {
                    p.SpAng = p.Aim;
                    continue;
                }
                // the swallowed shots
                spSwallow.Clear();
                foreach (var b in Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && b.Pos.CloserThan(p.Pos, SpBodyR + b.Radius))
                        spSwallow.Add(b.Id);
                foreach (int id in spSwallow)
                {
                    if (!Bullets.TryGetValue(id, out var b))
                        continue;
                    p.SpSwallowed++;
                    BroadcastFx(FxKind.SpiritInk, b.Pos, new V2(SpFxSwallow, p.Id), SpSeal, b.Radius, 0.35f, Sfx.None);
                    KillBullet(id);
                }
                // the cleave
                if (p.SpCastT < 0f)
                {
                    p.SpAng = p.Aim;
                    continue;
                }
                float wind = SpWindupOf(p);
                p.SpCastT += dt;
                if (p.SpCastT < wind)
                {
                    float turn = MathX.WrapPi(p.Aim - p.SpAng), most = SpTurn * dt;
                    p.SpAng = MathX.WrapPi(p.SpAng + Math.Max(-most, Math.Min(most, turn)));
                    p.SpWind = Math.Max(0.001f, p.SpCastT / wind);
                    continue;
                }
                if (p.SpWind <= 1f)  // the sweep starts: the stroke goes down on every game
                {
                    p.SpCleaves++;
                    p.SpHit.Clear();
                    BroadcastFx(FxKind.SpiritInk, p.Pos, new V2(SpFxStroke, p.SpAng), SpSeal, SpReachOf(p), SpSweep + SpStrokeTime, Sfx.Shockwave);
                }
                float k = Math.Min(1f, (p.SpCastT - wind) / SpSweep);
                p.SpWind = 1f + Math.Max(0.001f, k);
                SpSweepHits(p, k);
                if (k >= 1f)
                {
                    p.SpCastT = -1f;
                    p.SpWind = 0f;
                }
            }
        }

        /// Everything the brush has passed by `k` of the sweep, once a cleave.
        void SpSweepHits(Player p, float k)
        {
            float blade = SpBladeAt(k), reach = SpReachOf(p), dmg = SpDamage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (p.SpHit.Contains(e.Id) || e.Pos.FartherThan(p.Pos, reach + e.Radius))
                    continue;
                V2 off = e.Pos - p.Pos;
                float rel = off.Length < 0.01f ? 0f : MathX.WrapPi(off.Angle - p.SpAng);
                if (Math.Abs(rel) > SpHalfArc || rel > blade)
                    continue;
                p.SpHit.Add(e.Id);
                if (!e.Def.IsBoss)
                {
                    V2 away = AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.SpAng));
                    KnockEnemy(e, away * (e.Def.Heavy ? SpKnock * 0.5f : SpKnock));
                    if (!e.Def.Heavy)
                        Stun(e, SpStun);
                }
                HurtEnemy(e, dmg, p.Id);
            }
        }

        /// TickPlayers' fire branch while the spirit is out: Fire starts a cleave (the weapon is put away).
        void SpiritTrigger(Player p)
        {
            if (!SpStanding(p) || !p.Firing || p.ParryActive || p.SpCastT >= 0f || p.SpCd > 0f || State == MatchState.GameOver)
                return;
            p.SpCastT = 0f;
            p.SpWind = 0.001f;
            p.SpAng = p.Aim;
            p.SpCd = SpEvery;
        }
    }
}
