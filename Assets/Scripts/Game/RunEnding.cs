using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The run ending ("last breath", Docs/Mockups/run-ending.html option A). Seconds from the killing blow:
    ///   0-0.35     time slows almost to a stop (EndSpeed scales the game's own clock, so the host's slow motion reaches
    ///              everyone) and the camera eases in on the blow: the last player to go down, or the last boss;
    ///   0.1-1.4    game over: the colour drains out (SetColor's glDesat) and a red vignette closes in.
    ///              Victory: a white flash, gold light rays and a ring out of the boss, its shots turning into gold sparks;
    ///   1.05       the title (GAME OVER / VICTORY!) slams down in the middle with a shake and a thud;
    ///   1.9-2.6    the results' dark layer comes in and the title glides up to its place;
    ///   2.45 on    the stat tiles drop in one by one, the player cards rise, then the buttons.
    /// On your own screen only, apart from the slow motion. Not in the Boss Lab (its bots restart at once).
    public partial class ArenaApp
    {
        const float EndSlowIn = 0.35f, EndSlow = 0.07f, EndSlowHold = 1.6f, EndSlowOut = 1.9f, EndFloor = 0.02f,
            EndStampAt = 1.05f, EndStampLen = 0.25f, EndBgAt = 1.9f, EndLiftAt = 2.0f, EndTilesAt = 2.45f, EndCardsAt = 2.8f,
            EndButtonsAt = 3.35f, EndLen = 4.0f, EndZoom = 0.25f;

        float endT = -1f;  // seconds since the run ended; below 0 when it hasn't
        bool endWin, endStamped;
        V2 endFocus;
        bool endWasOver;
        int endLastDown = -1;       // the last player to go down (the game over's focus)
        V2 endBossAt;               // where the last boss was seen (the victory's focus)
        bool endBossSeen;
        readonly Dictionary<int, bool> endDowned = new Dictionary<int, bool>();
        readonly List<V2> endShots = new List<V2>(), endShotsSeen = new List<V2>();  // enemy shots when the boss died (gold sparks)

        static float glDesat;  // SetColor drains this much of the colour out (the game over's grey)

        bool EndingOn => endT >= 0f;

        /// How fast the game runs: slowed down from the killing blow, and nearly still behind the results.
        float EndSpeed
        {
            get
            {
                if (!EndingOn || replay != null)
                    return 1f;
                if (endT < EndSlowIn)
                    return Mathf.Lerp(1f, EndSlow, Ease(endT / EndSlowIn));
                if (endT < EndSlowHold)
                    return EndSlow;
                return Mathf.Lerp(EndSlow, EndFloor, Seg(endT, EndSlowHold, EndSlowOut));
            }
        }

        /// Update, after the game's: notice the run ending, track what to focus on, and run the clock.
        void TickEnding(float dt)
        {
            bool inGame = session.Screen == AppScreen.Game && game.Running && !LabRunning;
            bool over = inGame && (game.State == MatchState.GameOver || game.State == MatchState.Victory);
            if (!over)
            {
                endT = -1f;
                endWasOver = false;
                endStamped = false;
                if (inGame)
                    TrackEndingFocus();
                return;
            }
            if (!endWasOver)
            {
                endWasOver = true;
                endT = 0f;
                endWin = game.State == MatchState.Victory;
                Player last;
                endFocus = endWin ? (endBossSeen ? endBossAt : new V2(AW / 2f, AH / 2f))
                    : game.Players.TryGetValue(endLastDown, out last) ? last.Disp : game.Me != null ? game.Me.Disp : new V2(AW / 2f, AH / 2f);
                endShots.Clear();
                if (endWin)
                    endShots.AddRange(endShotsSeen);
                return;
            }
            endT += dt;
            if (!endStamped && endT >= EndStampAt + EndStampLen)
            {
                endStamped = true;
                sounds?.Play(Sfx.BossSlam, 0.7f, Time.unscaledTime);
                Shake(0.55f);
            }
        }

        void TrackEndingFocus()
        {
            foreach (var p in game.Players.Values)
            {
                bool was;
                if (p.Downed && (!endDowned.TryGetValue(p.Id, out was) || !was))
                    endLastDown = p.Id;
                endDowned[p.Id] = p.Downed;
            }
            endBossSeen = false;
            foreach (var e in game.Enemies.Values)
                if (e.Def.IsBoss)
                {
                    endBossAt = e.Disp;
                    endBossSeen = true;
                    break;
                }
            endShotsSeen.Clear();
            if (endBossSeen && game.Wave >= Game.MaxWave)
                foreach (var b in game.Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && endShotsSeen.Count < 200)
                        endShotsSeen.Add(b.Pos);
        }

        // --- The world --------------------------------------------------------------------------------------------------

        /// DrawWorld, before the arena: the camera eased in on the blow, and the colour draining out.
        void BeginEndingWorld()
        {
            glDesat = 0f;
            if (!EndingOn)
                return;
            float z = fxReduced ? 1f : 1f + EndZoom * Ease(Seg(endT, 0f, 1.2f)) * (1f - Ease(Seg(endT, EndBgAt, EndBgAt + 0.5f)));
            glZoom = z;
            glShift = endFocus * (1f - z);
            if (!endWin)
                glDesat = Ease(Seg(endT, 0.1f, 1.2f));
        }

        /// DrawWorld, over the arena's contents (still zoomed): the victory's rays, ring and sparks, the game over's ring.
        void DrawEndingWorld()
        {
            if (!EndingOn)
                return;
            if (endWin)
            {
                float rk = Seg(endT, 0f, 2.2f);
                if (rk < 1f)
                {
                    float reach = 1400f * Ease(Mathf.Min(1f, rk * 1.5f)), a = 0.45f * Mathf.Min(1f, rk * 3f) * (1f - rk);
                    var gold = new Rgba(1f, 0.84f, 0.47f);
                    if (recording) recGlowFrom.Add(recVerts.Count);  // (a held frame replays it as glow too)
                    SwitchToGlow(true);
                    for (int i = 0; i < 16; i++)
                    {
                        float an = MathX.Tau * i / 16f + endT * 0.2f, w = 0.07f + (i % 2) * 0.05f;
                        VTri(endFocus + V2.FromAngle(an - w) * reach, gold.WithAlpha(0f), endFocus + V2.FromAngle(an + w) * reach, gold.WithAlpha(0f), endFocus, gold.WithAlpha(a));  // rim first, like GlowDisc
                    }
                    SwitchToGlow(false);
                    if (recording) recGlowTo.Add(recVerts.Count);
                }
                float ring = Seg(endT, 0f, 0.8f);
                if (ring < 1f)
                    Arc(endFocus, 60f + 900f * Ease(ring), 8f * (1f - ring) + 2f, new Rgba(1f, 0.95f, 0.78f, 1f - ring), 64);
                float sk = Seg(endT, 0.05f, 1.4f);
                if (sk < 1f)  // the boss's shots, each a gold spark drifting out and fading
                    foreach (var at in endShots)
                    {
                        V2 d = at - endFocus;
                        float len = Mathf.Max(1f, d.Length);
                        V2 p = at + d * (40f * Ease(sk) / len) + new V2(0f, -20f * sk);
                        Circle(p, 5f * (1f - sk) + 1.5f, new Rgba(1f, 0.85f, 0.42f, 1f - sk), 8);
                    }
                for (int i = 0; i < 48; i++)  // and sparks out of the boss
                {
                    float k = Seg(endT, 0f, 1.4f);
                    if (k >= 1f)
                        break;
                    float an = Hash(i) * MathX.Tau, sp = (0.3f + Hash(i + 50) * 0.7f) * 700f;
                    V2 p = endFocus + V2.FromAngle(an) * (sp * Ease(k)) + new V2(0f, 140f * k * k);
                    Circle(p, 4f * (1f - k) + 1f, new Rgba(1f, 0.93f, 0.66f, 1f - k), 6);
                }
            }
            else
            {
                float ring = Seg(endT, 0f, 0.6f);
                if (ring < 1f)
                    Arc(endFocus, 24f + 200f * Ease(ring), 5f * (1f - ring) + 1f, new Rgba(1f, 0.39f, 0.44f, 1f - ring), 48);
            }
        }

        /// DrawWorld, after the arena (the camera back to normal, full colour again): the vignette and the victory's flash.
        void EndEndingWorld()
        {
            glZoom = 1f;
            glShift = V2.Zero;
            glDesat = 0f;
            if (!EndingOn)
                return;
            float v = Ease(Seg(endT, 0.2f, 1.4f)) * (endWin ? 0.45f : 0.9f);
            if (v > 0f)  // a vignette closing in: dark red, or dark gold
            {
                var edge = endWin ? new Rgba(0.35f, 0.23f, 0f, v) : new Rgba(0.47f, 0f, 0.05f, v);
                var clear = edge.WithAlpha(0f);
                float band = Mathf.Lerp(120f, 420f, v);
                VQuad(new V2(0f, 0f), edge, new V2(AW, 0f), edge, new V2(AW, band), clear, new V2(0f, band), clear);
                VQuad(new V2(0f, AH), edge, new V2(AW, AH), edge, new V2(AW, AH - band), clear, new V2(0f, AH - band), clear);
                VQuad(new V2(0f, 0f), edge, new V2(0f, AH), edge, new V2(band, AH), clear, new V2(band, 0f), clear);
                VQuad(new V2(AW, 0f), edge, new V2(AW, AH), edge, new V2(AW - band, AH), clear, new V2(AW - band, 0f), clear);
            }
            if (endWin)
            {
                float f = 0.85f * (1f - Seg(endT, 0f, 0.45f));
                if (f > 0f && !fxReduced)
                    FillRect(0, 0, AW, AH, new Rgba(1f, 0.97f, 0.9f, f));
            }
        }

        /// How dark the results' layer is (0 while the ending plays out, then in; 1 with no ending).
        float EndBg => EndingOn ? Seg(endT, EndBgAt, EndBgAt + 0.5f) : 1f;

        // --- The results screen's entrance (DrawRunSummary) ---------------------------------------------------------

        /// The title: 0 before the stamp, then in.
        float EndTitleIn => EndingOn ? Seg(endT, EndStampAt, EndStampAt + EndStampLen) : 1f;

        /// The title's size (1 at its place) and how far it is from the middle of the screen (0) to its place (1).
        float EndTitleScale => EndingOn ? Mathf.Lerp(Mathf.Lerp(2.2f, 1.5f, Ease(EndTitleIn)), 1f, EndLift) : 1f;
        float EndLift => EndingOn ? Ease(Seg(endT, EndLiftAt, EndLiftAt + 0.6f)) : 1f;
        float EndSubIn => EndingOn ? Seg(endT, EndLiftAt + 0.5f, EndLiftAt + 0.9f) : 1f;
        float EndTileIn(int i) => EndingOn ? Seg(endT, EndTilesAt + i * 0.09f, EndTilesAt + i * 0.09f + 0.35f) : 1f;
        float EndCardIn(int i) => EndingOn ? Seg(endT, EndCardsAt + i * 0.1f, EndCardsAt + i * 0.1f + 0.5f) : 1f;
        float EndButtonsIn => EndingOn ? Seg(endT, EndButtonsAt, EndButtonsAt + 0.4f) : 1f;

        /// The title's shake right after it lands.
        Vector2 EndTitleShake()
        {
            if (!EndingOn || fxReduced)
                return Vector2.zero;
            float k = Mathf.Max(0f, 1f - (endT - EndStampAt - EndStampLen) * 4f);
            if (endT < EndStampAt + EndStampLen || k <= 0f)
                return Vector2.zero;
            return new Vector2(Mathf.Sin(endT * 90f), Mathf.Cos(endT * 77f)) * (8f * k);
        }

        /// Draw something faded in (alpha) and moved, then put things back.
        void EndPart(float a, Vector2 offset, System.Action draw)
        {
            if (a <= 0f)
                return;
            if (a >= 1f && offset == Vector2.zero)
            {
                draw();
                return;
            }
            var oldColor = GUI.color;
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * a;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * a);
            GUI.BeginGroup(new Rect(offset.x, offset.y, W, H));
            draw();
            GUI.EndGroup();
            Ui.Alpha = oldAlpha;
            GUI.color = oldColor;
        }
    }
}
