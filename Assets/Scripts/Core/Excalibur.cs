using System;
using System.Collections.Generic;

namespace Arena
{
    /// Excalibur: the DPS Knight's fourth ultimate (lobby key 3 of the Ultimate row; Warhorn Volley stays the default).
    /// From the mockup (claude.ai artifact "Excalibur Ability Mockup"):
    ///   - Draw (ExcDraw s): a shaft of light comes down on you and the giant sword grows in your hands. You walk at ExcDrawMove
    ///     and can't swing.
    ///   - Excalibur (ExcTime s): your weapon is put away and Fire swings the sword, one swing per ExcSwingEvery s while held,
    ///     left and right by turns. A swing cuts ExcBladeDamage into everything in a ExcArcDeg° arc out to ExcReach px, then
    ///     throws a shockwave: a crescent of light (ExcWaveHalf either side of the aim) that flies out from ExcWaveFrom at
    ///     ExcWaveSpeed px/s to ExcWaveRange, hitting each enemy it reaches once for ExcWaveDamage and cutting the small enemy
    ///     shots it passes.
    ///   - Judgement: every ExcJudgeEvery-th swing is an overhead chop: ExcJudgeDamage, then a wider wave (ExcJudgeHalf) that
    ///     flies ExcJudgeRange for ExcJudgeWaveDamage, shoves ExcJudgeKnock and cuts every enemy shot it passes, red aimed
    ///     ones too. The next swing waits ExcJudgeRecover s.
    ///   - End: when the time runs out the sword bursts into light: ExcBurstDamage to everything within ExcBurstRadius. Going
    ///     down, the wave ending or the slot changing ends it at once, with no burst.
    /// Shoves skip heavies and bosses. All damage scales by your damage. Mastered: ExcTimeMastered s, waves ExcMasterReach farther.
    /// Replicated: Player.ExcT (time left, the draw included; 0 = off), ExcAge (seconds since the cast) and ExcSwing (the swing
    /// number plus its progress 0..1; odd numbers swing left to right). The waves, the draw's shaft and the burst are
    /// FxKind.Excalibur. Drawn by Game/ExcaliburLook.cs.
    public partial class Game
    {
        public const float ExcDraw = 0.5f, ExcTime = 10f, ExcTimeMastered = 13f, ExcDrawMove = 0.5f, ExcMove = 0.9f;
        public const float ExcSwingEvery = 0.38f, ExcSwingTime = 0.24f, ExcJudgeSwing = 0.32f, ExcJudgeRecover = 0.5f;
        public const float ExcBladeDamage = 60f, ExcReach = 230f, ExcArcDeg = 150f, ExcBladeKnock = 30f;
        public const float ExcWaveDamage = 90f, ExcWaveSpeed = 900f, ExcWaveFrom = 60f, ExcWaveRange = 700f, ExcWaveHalf = 0.62f, ExcWaveDelay = 0.08f;
        public const int ExcJudgeEvery = 4;
        public const float ExcJudgeDamage = 140f, ExcJudgeWaveDamage = 180f, ExcJudgeRange = 1000f, ExcJudgeHalf = 1f, ExcJudgeKnock = 120f, ExcJudgeDelay = 0.16f;
        public const float ExcBurstDamage = 120f, ExcBurstRadius = 200f, ExcBurstFx = 0.6f;
        public const float ExcMasterReach = 0.3f;
        public const float ExcWaveTail = 0.25f;  // the look: a wave fades this long after it stops
        public const float ExcSwordLen = 190f;  // the look: the blade's length, hilt to tip
        /// FxKind.Excalibur's Radius: what it is.
        public const int ExcFxWave = 1, ExcFxJudge = 2, ExcFxBurst = 3, ExcFxDraw = 4;
        public static readonly Rgba ExcGold = new Rgba(1f, 0.8f, 0.35f), ExcHoly = new Rgba(1f, 0.95f, 0.77f), ExcGem = new Rgba(0.35f, 0.82f, 1f);

        /// Excalibur's entry: key 3 of the DPS Knight's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] ExcaliburUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.Excalibur, Kit = Kit.Knight, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Excalibur", Cooldown = 70f,
                Desc = "Draw a giant glowing sword for 10 s: every swing throws a shockwave that goes through enemies, and every 4th is a Judgement.",
                Capstone = "Lasts 13 s and the shockwaves fly 30% farther.",
                Bot = b => b.HaveTarget && b.TargetD < ExcWaveRange * 0.6f,
            },
        };

        class ExcWave
        {
            public int Owner;
            public V2 From;
            public float Aim, Delay, R, Range, Half, Dmg;
            public bool Judge, Out;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }

        readonly List<ExcWave> excWaves = new List<ExcWave>();
        readonly List<int> excCut = new List<int>();

        /// The sword is out (the draw included).
        public static bool ExcOn(Player p) => p.ExcT > 0f && !p.Downed;
        /// Past the draw: Fire swings.
        public static bool ExcReady(Player p) => ExcOn(p) && p.ExcAge >= ExcDraw;
        /// Your speed share (Player.Speed): slowed while drawing, a little slowed while you hold it.
        public static float ExcSpeed(Player p) => p.ExcT <= 0f ? 1f : p.ExcAge < ExcDraw ? ExcDrawMove : ExcMove;
        /// The swing number (from 1; 0 before the first) from the replicated ExcSwing.
        public static int ExcSwingNo(Player p) => (int)Math.Floor(p.ExcSwing);
        public static bool ExcIsJudge(int n) => n > 0 && n % ExcJudgeEvery == 0;
        /// A wave's reach.
        public static float ExcRange(bool judge, bool mastered) => (judge ? ExcJudgeRange : ExcWaveRange) * (mastered ? 1f + ExcMasterReach : 1f);
        /// A wave's radius `age` seconds after it's thrown.
        public static float ExcWaveR(float age, float range) => Math.Min(range, ExcWaveFrom + ExcWaveSpeed * Math.Max(0f, age));

        void CastExcalibur(Player p, bool mastered)
        {
            p.ExcT = ExcDraw + (mastered ? ExcTimeMastered : ExcTime);
            p.ExcAge = 0f;
            p.ExcSwing = 0f;
            p.ExcStep = 0;
            p.ExcCd = 0f;
            p.ExcSwingT = -1f;
            p.ExcMastered = mastered;
            p.ExcUp = true;
            p.UltT = p.ExcT;
            BroadcastFx(FxKind.Excalibur, p.Pos, new V2(p.Id, 0f), ExcHoly, ExcFxDraw, ExcDraw, Sfx.LightNoon);
        }

        /// It ends: on time with the burst; going down, the wave over or the slot changed without.
        void EndExcalibur(Player p, bool burst)
        {
            if (!p.ExcUp)
                return;
            p.ExcUp = false;
            p.ExcT = 0f;
            p.ExcSwingT = -1f;
            p.ExcSwing = 0f;
            excWaves.RemoveAll(w => w.Owner == p.Id && !w.Out);
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.Excalibur)
                p.UltT = 0f;
            if (!burst)
                return;
            float dmg = ExcBurstDamage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Pos.FartherOrAt(p.Pos, ExcBurstRadius + e.Radius))
                    continue;
                HurtEnemy(e, dmg, p.Id);
                p.ExcHits++;
            }
            BroadcastFx(FxKind.Excalibur, p.Pos, p.Pos, ExcHoly, ExcFxBurst, ExcBurstFx, Sfx.LightRadiant);
        }

        /// Host, every tick: the clock, the swing, the waves, the ends.
        void TickExcalibur(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.ExcUp)
                    continue;
                p.ExcT = Math.Max(0f, p.ExcT - dt);
                p.ExcAge += dt;
                p.ExcCd -= dt;
                if (p.Slots[Upgrades.UltSlot].Id != AbilityId.Excalibur || p.Downed || !Battle)
                {
                    EndExcalibur(p, false);
                    continue;
                }
                if (p.ExcT <= 0f)
                {
                    EndExcalibur(p, true);
                    continue;
                }
                float sw = ExcIsJudge(p.ExcStep) ? ExcJudgeSwing : ExcSwingTime;
                if (p.ExcSwingT >= 0f)
                {
                    p.ExcSwingT += dt;
                    if (p.ExcSwingT >= sw)
                        p.ExcSwingT = -1f;
                }
                p.ExcSwing = p.ExcStep + (p.ExcSwingT >= 0f ? Math.Min(0.999f, p.ExcSwingT / sw) : 0f);
            }
            TickExcWaves(dt);
        }

        /// TickPlayers' fire branch while the sword is out: Fire swings it (the weapon is put away).
        void ExcaliburTrigger(Player p)
        {
            if (!ExcReady(p) || !p.Firing || p.ParryActive || p.StunT > 0f || p.ExcCd > 0f || State == MatchState.GameOver)
                return;
            p.ExcStep++;
            p.ExcSwingT = 0f;
            bool judge = ExcIsJudge(p.ExcStep);
            p.ExcCd = judge ? ExcJudgeRecover : Math.Max(p.ExcCd, -ExcSwingEvery * 0.5f) + ExcSwingEvery;  // (keeps the leftover, so held Fire averages ExcSwingEvery whatever the tick)
            if (judge)
                p.ExcJudges++;
            else
                p.ExcSwings++;
            V2 dir = V2.FromAngle(p.Aim);
            float dmg = (judge ? ExcJudgeDamage : ExcBladeDamage) * p.Stats.DmgMult, half = ExcArcDeg * 0.5f * MathX.Pi / 180f;
            foreach (var e in AllFoes(by: p.Id))
            {
                float d = e.Pos.DistanceTo(p.Pos);
                if (d - e.Radius > ExcReach || AngleTo(p.Pos, e.Pos, p.Aim) > half + BodySlack(d, e.Radius))
                    continue;
                if (!Heavy(e))
                    ShoveFrom(e, p.Pos, judge ? ExcJudgeKnock : ExcBladeKnock, dir);
                HurtEnemy(e, dmg, p.Id);
                p.ExcHits++;
            }
            excWaves.Add(new ExcWave
            {
                Owner = p.Id, Aim = p.Aim, Judge = judge, Delay = judge ? ExcJudgeDelay : ExcWaveDelay,
                Range = ExcRange(judge, p.ExcMastered), Half = judge ? ExcJudgeHalf : ExcWaveHalf,
                Dmg = (judge ? ExcJudgeWaveDamage : ExcWaveDamage) * p.Stats.DmgMult,
            });
            Sounds.Add(judge ? Sfx.LightRadiant : p.ExcStep % 2 == 1 ? Sfx.LightCut1 : Sfx.LightCut2);
        }

        /// Host: the waves wait for the swing to come through, then fly out, hitting each enemy once and cutting shots.
        void TickExcWaves(float dt)
        {
            for (int i = excWaves.Count - 1; i >= 0; i--)
            {
                var w = excWaves[i];
                var p = PlayerOrNull(w.Owner);
                if (p == null)
                {
                    excWaves.RemoveAt(i);
                    continue;
                }
                if (!w.Out)
                {
                    w.Delay -= dt;
                    if (w.Delay > 0f)
                        continue;
                    w.Out = true;
                    w.From = p.Pos;
                    w.R = ExcWaveFrom;
                    BroadcastFx(FxKind.Excalibur, w.From, w.From + V2.FromAngle(w.Aim) * w.Range, ExcGold, w.Judge ? ExcFxJudge : ExcFxWave,
                        (w.Range - ExcWaveFrom) / ExcWaveSpeed + ExcWaveTail, w.Judge ? Sfx.SiegeBell : Sfx.None);
                }
                float before = w.R;
                w.R = Math.Min(w.Range, w.R + ExcWaveSpeed * dt);
                foreach (var e in AllFoes(by: p.Id))
                {
                    float d = e.Pos.DistanceTo(w.From);
                    if (w.Hit.Contains(e.Id) || d - e.Radius > w.R || AngleTo(w.From, e.Pos, w.Aim) > w.Half + BodySlack(d, e.Radius))
                        continue;
                    w.Hit.Add(e.Id);
                    if (w.Judge && !Heavy(e))
                        ShoveFrom(e, w.From, ExcJudgeKnock, V2.FromAngle(w.Aim));
                    HurtEnemy(e, w.Dmg, p.Id);
                    p.ExcHits++;
                }
                excCut.Clear();
                foreach (var b in Bullets.Values)
                {
                    if (!Defs.IsEnemyShot(b.Kind) || !w.Judge && (Defs.Unblockable(b.Kind) || b.Radius > 8f))
                        continue;
                    float d = b.Pos.DistanceTo(w.From);
                    if (d - b.Radius > w.R || d + b.Radius < before - 30f || AngleTo(w.From, b.Pos, w.Aim) > w.Half)
                        continue;
                    excCut.Add(b.Id);
                }
                foreach (int id in excCut)
                {
                    AddFx(FxKind.Spark, Bullets[id].Pos, ExcHoly, 8f, 0.2f);
                    KillBullet(id);
                    p.ExcCuts++;
                }
                if (w.R >= w.Range)
                    excWaves.RemoveAt(i);
            }
        }

        // --- Network: the player's Excalibur fields (SyncPlayer) ---

        void SyncExcalibur(NetSync s, Player p)
        {
            s.Float(ref p.ExcT);  // Excalibur
            if (p.ExcT > 0f)
            {
                s.Float(ref p.ExcAge);
                s.Float(ref p.ExcSwing);
            }
        }
    }
}
