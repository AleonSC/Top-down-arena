using System;
using UnityEngine;

namespace Arena
{
    /// The lobby's Boss Hunt tab (Hunts.cs): a card for each of the ten bosses, its live model, and whether its hunt
    /// is open (beaten in a run), locked, or still to come; the picked one's details and this player's hunt records.
    public partial class ArenaApp
    {
        /// A boss the Boss Hunt list names: beaten at least once (or its hunt opened), else a "???" row like the Quick Play tab's pool.
        static bool HuntKnown(EnemyType boss, bool open, Profile profile) => open || (profile != null && profile.Beaten(boss) > 0);

        static readonly Color HuntColor = new Color(0.95f, 0.42f, 0.32f);

        // The boss drum: an endless vertical loop with the picked boss on top. huntTarget counts rows without end (its boss is
        // huntTarget mod 10); huntPos eases toward it. The host can scroll onto a boss whose hunt isn't open to look at it
        // (huntBrowsingLocked then stops START); the party's pick (session.HuntBoss) only moves onto open hunts.
        float huntPos;
        int huntTarget;
        bool huntInit;
        EnemyType huntLastBoss;
        bool huntBrowsingLocked;
        static int HuntMod(int a) => ((a % 10) + 10) % 10;

        static readonly Color PhosGreen = new Color(0.30f, 1f, 0.60f);
        static Color FadedBlue(float a) => new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, a);

        static void HuntBox(Rect r, Color c)
        {
            Ui.Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Ui.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Ui.Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Ui.Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        /// A small outlined status tag ("WON x2", "OPEN", ...), right-aligned to xRight. Returns its width.
        static void HuntChip(float xRight, float cy, string text, Color c)
        {
            float tw = LobbyKit.SpacedWidth(text, 10, 1f) + 10f;
            var r = new Rect(xRight - tw, cy - 8f, tw, 16f);
            Ui.Fill(r, new Color(c.r, c.g, c.b, 0.12f));
            HuntBox(r, new Color(c.r, c.g, c.b, 0.55f));
            LobbyKit.Spaced(r, text, 10, c, TextAnchor.MiddleCenter, 1f);
        }

        void DrawHuntPicker(Rect r, float t)
        {
            LobbyKit.Panel(r);  // the lobby screen (LobbyKit.cs)
            bool host = session.IsHost;
            var profile = game.Profile;
            var ink = CrtUi.Ink;
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            y = ModeHeader(r, "BOSS HUNT", "10 WAVES  ·  ONE BOSS", host ? "Pick a boss. Beat one in a run to open its hunt."
                : "The host picks the boss (from the hunts they've opened).", Ui.Muted);
            float bodyY = y, bodyH = Mathf.Max(200f, r.yMax - 14f - bodyY);

            // Left: the ten bosses as a looping drum, the picked one on top.
            const int count = 10;
            const float listW = 268f, rowH = 44f, step = 50f;
            var ev = Event.current;
            var bosses = new EnemyType[count];
            for (int i = 0; i < count; i++)
                bosses[i] = BossSchedule.Campaign.BossAt((i + 1) * 5);
            int hi = Mathf.Max(0, Array.IndexOf(bosses, session.HuntBoss));
            if (!huntInit)
            {
                huntTarget = hi;
                huntPos = hi;
                huntInit = true;
            }
            else if (HuntMod(huntTarget) != hi && (!host || (huntLastBoss != session.HuntBoss && Hunts.Available(Hunts.Get(session.HuntBoss), profile))))
            {
                int d = HuntMod(hi - huntTarget);  // the party's pick changed (a guest follows the host): roll the short way round
                huntTarget += d > count / 2 ? d - count : d;
            }
            huntLastBoss = session.HuntBoss;
            float viewH = bodyH - 44f;
            var listRect = new Rect(x, bodyY, listW, viewH);
            if (host && ev.type == EventType.ScrollWheel && GUI.enabled && listRect.Contains(ev.mousePosition))
            {
                huntTarget += ev.delta.y > 0f ? 1 : -1;
                ev.Use();
            }
            if (ev.type == EventType.Repaint)
            {
                huntPos += (huntTarget - huntPos) * (1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
                if (Mathf.Abs(huntTarget - huntPos) < 0.004f)
                    huntPos = huntTarget;
                if (Mathf.Abs(huntTarget) > 1000)  // keep the numbers small
                {
                    int sub = huntTarget / count * count;
                    huntTarget -= sub;
                    huntPos -= sub;
                }
            }
            int ti = HuntMod(huntTarget);
            var topBoss = bosses[ti];
            var topHunt = Hunts.Get(topBoss);
            bool can = Hunts.Available(topHunt, profile);
            bool topKnown = HuntKnown(topBoss, can, profile);
            if (host && can && session.HuntBoss != topBoss)
                session.SetHuntBoss(topBoss);
            huntLastBoss = session.HuntBoss;
            huntBrowsingLocked = host && !can;

            var rowRects = new Rect[count];
            var rowAlpha = new float[count];
            float oldAlpha = Ui.Alpha;
            for (int i = 0; i < count; i++)
            {
                float wv = ((i - huntPos) % count + count) % count;
                if (wv >= count - 1.5f)
                    wv -= count;  // the tail wraps round above the top row
                float ry = bodyY + 8f + wv * step;
                float a = Mathf.Clamp01(1f + wv * 1.4f) * Mathf.Clamp01((bodyY + viewH - (ry + rowH)) / 60f);
                rowAlpha[i] = a;
                var c = new Rect(x, ry, listW, rowH);
                rowRects[i] = c;
                if (a <= 0.01f)
                    continue;
                var boss = bosses[i];
                var hunt = Hunts.Get(boss);
                bool open = Hunts.Available(hunt, profile);
                bool known = HuntKnown(boss, open, profile);
                bool top = Mathf.Abs(wv) < 0.5f;
                bool hover = host && c.Contains(ev.mousePosition) && ev.mousePosition.y > bodyY && ev.mousePosition.y < bodyY + viewH;
                Ui.Alpha = oldAlpha * a;
                string chip = hunt == null ? "SOON" : !open ? "LOCKED" : profile.HuntWon(boss, true) > 0 ? "WON x" + profile.HuntWon(boss, true) : "OPEN";
                Color cc = hunt == null ? FadedBlue(0.55f) : !open ? HuntColor : profile.HuntWon(boss, true) > 0 ? Ui.Gold : new Color(0.5f, 0.88f, 0.69f);
                ModeRow(c, (i + 1).ToString("00"), BossName(boss), known, open, top, hover, HuntColor, chip, cc);
                if (hover && !top && Ui.HitArea(c))
                    huntTarget += Mathf.RoundToInt(wv);  // roll this one to the top
            }
            Ui.Alpha = oldAlpha;

            // Under the drum: step buttons and where we are in the loop.
            float by = bodyY + bodyH - 36f;
            if (LobbyKit.Key(new Rect(x, by, 70f, 34f), "PREV", false, false, size: 13, minSize: 10, enabled: host))
                huntTarget--;
            if (LobbyKit.Key(new Rect(x + 78f, by, 70f, 34f), "NEXT", false, false, size: 13, minSize: 10, enabled: host))
                huntTarget++;
            Ui.Label(new Rect(x + 156f, by, listW - 156f, 34f), (ti + 1).ToString("00") + " / 10  LOOPS", 11, Ui.Muted, TextAnchor.MiddleRight);

            // Right: the top boss's dossier.
            float x2 = x + listW + 14f, w2 = w - listW - 14f, y2 = bodyY;
            string name = topKnown ? BossName(topBoss) : "this boss";
            var scope = new Rect(x2, y2, w2, Mathf.Min(220f, bodyH * 0.38f));
            ModeScope(scope, "TARGET LOCK", (ti + 1).ToString("00") + " / 10");
            y2 += scope.height + 12f;
            if (!topKnown)
                CrtUi.Text(scope, "?", 72, new Color(PhosGreen.r, PhosGreen.g, PhosGreen.b, 0.7f), TextAnchor.MiddleCenter, true, CabCalm);
            Ui.Glitch(new Rect(x2, y2, w2, 28f), !topKnown ? "???" : (topHunt != null ? topHunt.Name : name).ToUpperInvariant(), 20, Color.white, TextAnchor.MiddleLeft);
            y2 += 32f;
            string copy = topHunt == null ? (topKnown ? name + " has no hunt yet." : "Unknown boss: meet it in a run to learn more.")
                : !can ? (host ? "Locked: beat " + name + " in a run to open this hunt." : "Locked for the host.")
                : "Waves 1-" + ((session.HuntLong ? Hunts.LongLength : Hunts.Length) - 1) + ": only " + name + "'s own enemies, tougher ones joining as you go. Wave " + (session.HuntLong ? Hunts.LongLength : Hunts.Length) + ": " + name + ". Beat it and the hunt is won.";
            Ui.Label(new Rect(x2, y2, w2, 42f), copy, 13, can ? Ui.Text : topHunt == null ? Ui.Muted : Ui.Warn, TextAnchor.UpperLeft, false, true);
            y2 += 48f;
            if (topHunt != null)
            {
                // Wave ladder: nine small pips, then the boss.
                float px = x2;
                int pipLen = session.HuntLong ? Hunts.LongLength : Hunts.Length;
                for (int i = 1; i <= pipLen; i++)
                {
                    bool bossPip = i == pipLen;
                    bool longPip = pipLen > Hunts.Length;
                    var pr = new Rect(px, y2, bossPip ? 34f : longPip ? 13f : 22f, 20f);
                    ModePip(pr, bossPip ? "BOSS" : longPip ? (i % 5 == 0 ? i.ToString() : "") : i.ToString(), bossPip || (longPip && i == pipLen / 2) ? HuntColor : new Color(ink.r, ink.g, ink.b, 1f), bossPip);
                    px += pr.width + (longPip ? 3f : 5f);
                }
                y2 += 30f;

                // Record tiles.
                int played = profile.HuntPlayed(topBoss, true);
                float best = profile.HuntBestTime(topBoss, true);
                float tw = (w2 - 16f) / 3f, th = 46f;
                LobbyKit.Tile(new Rect(x2, y2, tw, th), can ? profile.HuntWon(topBoss, true) + " / " + played : "-", "HUNTS WON", false);
                LobbyKit.Tile(new Rect(x2 + tw + 8f, y2, tw, th), best > 0f ? (int)(best / 60f) + ":" + ((int)best % 60).ToString("00") : "-", "BEST TIME", false);
                LobbyKit.Tile(new Rect(x2 + 2f * (tw + 8f), y2, tw, th), can ? "OPEN" : "LOCKED", "STATUS", false);
                y2 += th + 10f;
                if (can)
                {
                    y2 = ModeRules(x2, y2, w2, new[] { "NO PATHS", "NO DATA GATES", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" });
                    // The hunt is always a map of 20 steps (Docs/HuntMap.md, Docs/LongHunt.md).
                    y2 += 30f;
                    int steps = session.HuntLong ? Hunts.LongLength : Hunts.Length;
                    Ui.Label(new Rect(x2, y2, w2, 52f),
                        steps + " steps on a map. Each step the party votes on the next node: an Elite is a tougher wave and gives a boss clue (up to " + HuntMap.MaxClues + "). Step " + (steps / 2) + " is a miniboss and saves the hunt. Step " + steps + " is " + name + ".", 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
                }
            }

            if (BeginUiGl())
            {
                float oldGl = glAlpha;
                for (int i = 0; i < count; i++)
                {
                    if (rowAlpha[i] <= 0.01f || !HuntKnown(bosses[i], Hunts.Available(Hunts.Get(bosses[i]), profile), profile))
                        continue;
                    glAlpha = rowAlpha[i];
                    ModeRowModel(rowRects[i], bosses[i], t, i == ti);
                }
                glAlpha = oldGl;
                if (topKnown)
                    ModeScopeModel(scope, topBoss, t);
                EndUiGl();
            }
            ModeScopeFinish(scope);
        }
    }
}
