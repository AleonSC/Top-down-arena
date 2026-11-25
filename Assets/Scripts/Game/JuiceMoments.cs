using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Juice moments (Docs/JuiceMoments.md), on this screen only like the rest of Juice.cs:
    ///   kill kick     your own kills give a tiny hold and kick, more when several drop at once (every weapon, KillMoments)
    ///   multikill     kills of yours chained close together call DOUBLE / TRIPLE / QUAD / RAMPAGE / MASSACRE (DrawMultiKill)
    ///   last kill     the wave's last enemy: a big flash ring, a gold ripple across the floor and a hold, before the
    ///                 WAVE N CLEARED band (Hud.DrawBanner)
    ///   boss slow     a boss's death slows time for a moment (BossSlowSpeed folds into EndSpeed, RunEnding.cs, so the
    ///                 host's slow reaches everyone, as the run ending's does)
    ///   level shock   a level up sends a white ring and a gold ripple out from you; enemies near it flinch (look only)
    ///   heartbeat     low on health, the red corners beat twice like a pulse, faster and stronger the lower you are
    ///   floor marks   scorch marks fade to a faint stain and stay for a while, so the floor shows how the fight went
    /// Which kills are yours: one of your shots ended on it that frame, or it died within reach of you (melee, blasts at
    /// your feet). Nothing is sent: a client sees the same as the host.
    public partial class ArenaApp
    {
        // --- Kills seen this frame (filled by UpdateJuice) ----------------------------------------------------------------
        struct KillSeen { public V2 Pos; public float R; public bool Boss; }
        readonly List<KillSeen> frameKills = new List<KillSeen>();
        readonly List<V2> myShotEnds = new List<V2>();

        // --- Multikill ---------------------------------------------------------------------------------------------------
        public const float ChainGap = 0.6f, CallLife = 1.35f;
        int chainN, callN;
        float chainLast = -99f, callT = -1f;
        V2 callAt;

        /// The call for a chain of n kills (2 and up).
        public static string MultiKillName(int n) =>
            n >= 8 ? "MASSACRE" : n >= 5 ? "RAMPAGE" : n == 4 ? "QUAD KILL" : n == 3 ? "TRIPLE KILL" : "DOUBLE KILL";

        /// Its colour, warming with the count: white, gold, orange, red, magenta.
        static Color MultiKillColor(int n) =>
            n >= 8 ? new Color(1f, 0.36f, 0.78f) : n >= 5 ? new Color(1f, 0.32f, 0.3f) : n == 4 ? new Color(1f, 0.6f, 0.25f)
            : n == 3 ? new Color(1f, 0.84f, 0.35f) : new Color(0.92f, 0.95f, 1f);

        /// 0: the call slams in at the top middle; 1: it rises off you like an ability's name (the preview picks).
#pragma warning disable 0649  // set only by Tools/Preview (reflection)
        int multiStyle;
#pragma warning restore 0649

        // --- The wave's last kill, the boss's slow motion, the level shock --------------------------------------------------
        MatchState juiceState = MatchState.Idle;
        V2 lastKillAt;
        float lastKillClock = -99f, bossSlowT = -1f;
        public const float BossSlowIn = 0.08f, BossSlowHold = 0.55f, BossSlowOut = 0.6f, BossSlow = 0.25f;

        /// How fast the game runs while a boss dies: down to a quarter quickly, held, then back. 1 otherwise.
        float BossSlowSpeed
        {
            get
            {
                if (bossSlowT < 0f || fxReduced || !fxOn)
                    return 1f;
                if (bossSlowT < BossSlowIn)
                    return Mathf.Lerp(1f, BossSlow, bossSlowT / BossSlowIn);
                if (bossSlowT < BossSlowIn + BossSlowHold)
                    return BossSlow;
                return Mathf.Lerp(BossSlow, 1f, Mathf.Clamp01((bossSlowT - BossSlowIn - BossSlowHold) / BossSlowOut));
            }
        }

        /// Update: the boss's slow motion runs on real time (not the slowed clock), and stops if the run ends meanwhile.
        void TickBossSlow(float realDt)
        {
            if (bossSlowT < 0f)
                return;
            bossSlowT += realDt;
            if (bossSlowT > BossSlowIn + BossSlowHold + BossSlowOut || EndingOn)
                bossSlowT = -1f;
        }

        void ClearJuiceMoments()
        {
            frameKills.Clear(); myShotEnds.Clear();
            chainN = callN = 0;
            chainLast = lastKillClock = -99f;
            callT = bossSlowT = -1f;
            juiceState = game != null ? game.State : MatchState.Idle;
        }

        /// UpdateJuice, after enemies and shots: which of this frame's kills were yours, and what they start.
        void KillMoments(float dt)
        {
            var me = game.Me;
            int mine = 0;
            V2 at = default(V2);
            foreach (var k in frameKills)
            {
                lastKillAt = k.Pos;
                lastKillClock = juiceClock;
                if (me == null || me.Downed)
                    continue;
                bool yours = me.Disp.DistanceTo(k.Pos) < me.Def.BodyR + k.R + 90f;
                for (int i = 0; !yours && i < myShotEnds.Count; i++)
                    yours = myShotEnds[i].DistanceTo(k.Pos) < k.R + 26f;
                if (!yours)
                    continue;
                mine++;
                at = k.Pos;
            }
            frameKills.Clear();
            myShotEnds.Clear();

            if (mine > 0)
            {
                // The kick: one frame's hold for a single kill (at most every 0.18 s), longer the more drop at once.
                if (mine >= 3)
                    Hold(0.03f + 0.01f * Mathf.Min(5, mine - 3), 0.3f);
                else
                    Hold(mine == 2 ? 0.024f : 0.012f, 0.18f);
                Shake(Mathf.Min(0.12f, 0.025f * mine));

                if (juiceClock - chainLast > ChainGap)
                    chainN = 0;
                chainN += mine;
                chainLast = juiceClock;
                if (chainN >= 2 && chainN > (callT >= 0f ? callN : 0))
                {
                    bool up = MultiKillTier(chainN) != MultiKillTier(callT >= 0f ? callN : 0);
                    callN = chainN;
                    callT = up || callT < 0f ? 0f : Mathf.Min(callT, 0.12f);  // a new tier slams in again; a higher count only bumps
                    callAt = at;
                    if (up && chainN >= 3)
                        Shake(0.05f + 0.02f * MultiKillTier(chainN));
                }
            }
            if (callT >= 0f)
            {
                callT += dt;
                if (callT > CallLife)
                    callT = -1f;
            }

            // The wave's last kill: the fight just ended with enemies gone (not a run ending, not a boss's own show).
            var state = game.State;
            if (juiceState == MatchState.Fighting && state == MatchState.Intermission && juiceClock - lastKillClock < 0.5f && bossSlowT < 0f)
                LastKill(lastKillAt);
            juiceState = state;
        }

        static int MultiKillTier(int n) => n >= 8 ? 5 : n >= 5 ? 4 : Mathf.Clamp(n - 1, 0, 3);

        /// The wave's last enemy: a white flash ring, a gold ring behind it, a ripple across the floor, a hold and a kick.
        void LastKill(V2 at)
        {
            var gold = new Rgba(1f, 0.84f, 0.35f);
            if (!fxReduced)
                Ring(at, 300f, 0.5f, Rgba.White, PFlash);
            Ring(at, 420f, 0.8f, gold);
            Sparks(at, gold, 36, 480f, 0.9f, 2.6f);
            ripples.Add(new Ripple { Pos = at, Power = 2f, Life = 1.6f, Col = gold });
            Hold(0.09f, 0.3f);
            Shake(0.25f);
        }

        /// A boss is dead (Shatter): time slows for a moment.
        void StartBossSlow()
        {
            if (!fxReduced)
                bossSlowT = 0f;
        }

        /// A level up (LevelBurst): a white ring and a gold ripple out from you, and the enemies it passes flinch.
        void LevelShock(Player me, bool legendary)
        {
            float reach = legendary ? 320f : 240f;
            if (!fxReduced)
                Ring(me.Pos, reach, 0.35f, Rgba.White, PFlash);
            ripples.Add(new Ripple { Pos = me.Pos, Power = legendary ? 1.8f : 1.2f, Life = 0.9f, Col = new Rgba(1f, 0.84f, 0.35f) });
            Shake(legendary ? 0.2f : 0.1f);
            foreach (var e in game.Enemies.Values)
            {
                EnemySeen s;
                if (e.Disp.DistanceTo(me.Pos) > reach + e.Radius || !seenEnemies.TryGetValue(e.Id, out s))
                    continue;
                s.Squash = 1f;
                s.Flash = 0.09f;
            }
        }

        // --- Low health: the heartbeat --------------------------------------------------------------------------------------

        /// 0..1, two beats a cycle (lub-dub), the cycle quicker the lower you are (`low` 0 at 30% health, 1 near none).
        static float Heartbeat(float time, float low)
        {
            float period = Mathf.Lerp(1.1f, 0.6f, low), p = Mathf.Repeat(time, period) / period;
            float lub = Mathf.Exp(-Mathf.Pow((p - 0.06f) / 0.05f, 2f)), dub = 0.7f * Mathf.Exp(-Mathf.Pow((p - 0.26f) / 0.06f, 2f));
            return Mathf.Max(lub, dub);
        }

        /// DrawMood: how red the corners are while you're low (0 when you're not).
        float LowHealthRed(Player me)
        {
            if (me == null || me.Downed || me.Hp >= me.MaxHp * 0.3f)
                return 0f;
            float low = 1f - Mathf.Clamp01(me.Hp / (me.MaxHp * 0.3f));
            return 0.1f + 0.06f * low + (0.16f + 0.14f * low) * Heartbeat(Time.unscaledTime, low);
        }

        // --- Floor marks ---------------------------------------------------------------------------------------------------

        public const float MarkFade = 8f, MarkStay = 0.22f;

        /// How dark a scorch mark is at its age: 0.5 fresh, fading to a faint stain by MarkFade, which then stays until the
        /// last few seconds of ScorchLife.
        static float ScorchAlpha(float age)
        {
            float a = age < MarkFade ? Mathf.Lerp(0.5f, MarkStay, age / MarkFade) : MarkStay;
            return a * Mathf.Clamp01((ScorchLife - age) / 6f);
        }

        // --- On the HUD -----------------------------------------------------------------------------------------------------

        /// The multikill call: it slams in big and settles, the count beside it, then fades.
        void DrawMultiKill()
        {
            if (callT < 0f || callN < 2)
                return;
            float t = callT, pop = Mathf.Clamp01(t / 0.12f), fade = t < CallLife - 0.35f ? 1f : (CallLife - t) / 0.35f;
            var c = MultiKillColor(callN);
            string name = MultiKillName(callN), count = "×" + callN;
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            if (multiStyle == 1)
            {
                // Off you, rising like an ability's name but bigger.
                var me = game.Me;
                V2 at = ToUi(me != null ? me.Disp : callAt);
                at.Y -= (me != null ? me.Def.BodyR * UiWorldScale : 20f) + 70f + 22f * Mathf.Clamp01(t / CallLife);
                int size = Mathf.RoundToInt(Mathf.Lerp(34f, 22f, pop));
                Ui.Shadowed(new Rect(at.X - 160f, at.Y - 16f, 320f, 32f), name, size, c.WithAlpha(fade));
                Ui.Shadowed(new Rect(at.X - 160f, at.Y + 12f, 320f, 18f), count, 14, c.WithAlpha(0.8f * fade));
            }
            else
            {
                // The top middle: a slam from big to its size, a white flash on it, thin rules either side, the count under it.
                float y = H * 0.2f, size = Mathf.Lerp(76f, 46f, 1f - (1f - pop) * (1f - pop));
                float wide = Mathf.Lerp(80f, 260f, pop);
                var rule = c.WithAlpha(0.6f * fade);
                Ui.Fill(new Rect(W / 2f - wide - 150f, y - 1f, 150f * pop, 2f), rule);
                Ui.Fill(new Rect(W / 2f + wide, y - 1f, 150f * pop, 2f), rule);
                if (!FxCalm)  // a soft halo, as the scan band's title has
                {
                    var g = c.WithAlpha(0.14f * fade);
                    Ui.Label(new Rect(-3f, y - 40f, W, 80f), name, Mathf.RoundToInt(size), g, TextAnchor.MiddleCenter, false);
                    Ui.Label(new Rect(3f, y - 40f, W, 80f), name, Mathf.RoundToInt(size), g, TextAnchor.MiddleCenter, false);
                }
                var col = Color.Lerp(Color.white, c, pop);
                col.a = fade;
                Ui.Shadowed(new Rect(0f, y - 40f, W, 80f), name, Mathf.RoundToInt(size), col);
                Ui.Shadowed(new Rect(0f, y + 26f, W, 24f), count, 18, c.WithAlpha(0.85f * fade));
            }
            Ui.CrtFont = was;
        }
    }
}
