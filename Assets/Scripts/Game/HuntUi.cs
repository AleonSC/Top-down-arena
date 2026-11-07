using System;
using UnityEngine;

namespace Arena
{
    /// The lobby's Boss Hunt tab (Hunts.cs): a card for each of the ten bosses, its live model, and whether its hunt
    /// is open (beaten in a run), locked, or still to come; the picked one's details and this player's hunt records.
    public partial class ArenaApp
    {
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
            LobbyKit.Spaced(new Rect(x, y, 200f, 20f), "BOSS HUNT", 14, new Color(ink.r, ink.g, ink.b, 0.9f), TextAnchor.MiddleLeft, 3.6f);
            Ui.Label(new Rect(x + w - 240f, y, 240f, 20f), "10 WAVES TO ONE BOSS", 11, Ui.Muted, TextAnchor.MiddleRight);
            Ui.Fill(new Rect(x + 140f, y + 10f, w - 400f, 1f), new Color(ink.r, ink.g, ink.b, 0.25f));
            y += 24f;
            Ui.Label(new Rect(x, y, w, 18f), host ? "Pick a boss to hunt. Beat a boss in a run to open its hunt."
                : "The host picks the boss (from the hunts they've opened).", 12, Ui.Muted);
            y += 28f;
            float bodyY = y, bodyH = Mathf.Max(200f, r.yMax - 14f - bodyY);

            // Left: the ten bosses as a looping drum, the picked one on top.
            const int count = 10;
            const float listW = 268f, rowH = 44f, step = 50f;
            var ev = Event.current;
            var bosses = new EnemyType[count];
            for (int i = 0; i < count; i++)
                bosses[i] = Game.BossForWave((i + 1) * 5);
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
                bool top = Mathf.Abs(wv) < 0.5f;
                bool hover = host && c.Contains(ev.mousePosition) && ev.mousePosition.y > bodyY && ev.mousePosition.y < bodyY + viewH;
                Ui.Alpha = oldAlpha * a;
                // A digital key like the lobby's Ready button: soft tint and outline (coral and glowing on top); dashed and dim when not open yet.
                Color acc = top ? HuntColor : CrtUi.Ink;
                if (open || top)
                    CrtKit.SoftBox(c, acc, top, hover, false);
                else
                {
                    var d = CrtUi.Dim;
                    Ui.Fill(c, new Color(d.r, d.g, d.b, 0.015f));
                    CrtKit.Dashed(c, 1f, new Color(d.r, d.g, d.b, 0.3f));
                }
                Color tc = open || top ? CrtKit.SoftText(acc, hover || top) : new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.6f);
                Ui.Label(new Rect(c.x + 8f, c.y, 20f, c.height), (i + 1).ToString("00"), 10, new Color(tc.r, tc.g, tc.b, 0.6f), TextAnchor.MiddleLeft);
                CrtUi.Text(new Rect(c.x + 70f, c.y, c.width - 70f - 78f, c.height), BossName(boss), 14, tc, TextAnchor.MiddleLeft, true, false);
                string chip = hunt == null ? "SOON" : !open ? "LOCKED" : profile.HuntWon(boss) > 0 ? "WON x" + profile.HuntWon(boss) : "OPEN";
                Color cc = hunt == null ? FadedBlue(0.55f) : !open ? HuntColor : profile.HuntWon(boss) > 0 ? Ui.Gold : new Color(0.5f, 0.88f, 0.69f);
                HuntChip(c.xMax - 8f, c.center.y, chip, cc);
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
            string name = BossName(topBoss);
            var scope = new Rect(x2, y2, w2, Mathf.Min(220f, bodyH * 0.38f));
            Ui.Fill(scope, new Color(0.012f, 0.067f, 0.04f, 1f));  // phosphor green screen (the mockup's #03110a)
            Ui.Fill(new Rect(scope.x + scope.width * 0.15f, scope.y + scope.height * 0.15f, scope.width * 0.7f, scope.height * 0.7f), new Color(0.04f, 0.16f, 0.1f, 0.55f));
            HuntBox(scope, new Color(0.11f, 0.23f, 0.16f, 1f));
            Ui.Fill(new Rect(scope.x, scope.center.y, scope.width, 1f), new Color(PhosGreen.r, PhosGreen.g, PhosGreen.b, 0.22f));
            Ui.Fill(new Rect(scope.center.x, scope.y, 1f, scope.height), new Color(PhosGreen.r, PhosGreen.g, PhosGreen.b, 0.22f));
            foreach (float rad in new[] { 0.62f, 0.42f })
            {
                float rr = scope.height * rad * 0.5f;
                for (int k = 0; k < 48; k++)
                {
                    float a = k * Mathf.PI * 2f / 48f;
                    Ui.Fill(new Rect(scope.center.x + Mathf.Cos(a) * rr * 1.6f - 1f, scope.center.y + Mathf.Sin(a) * rr - 1f, 2f, 2f), new Color(PhosGreen.r, PhosGreen.g, PhosGreen.b, 0.28f));
                }
            }
            y2 += scope.height + 12f;
            var dim = new Color(ink.r, ink.g, ink.b, 0.7f);
            LobbyKit.Spaced(new Rect(scope.x + 10f, scope.y + 6f, 160f, 16f), "TARGET LOCK", 11, PhosGreen, TextAnchor.MiddleLeft, 2f);
            LobbyKit.Spaced(new Rect(scope.xMax - 170f, scope.y + 6f, 160f, 16f), (ti + 1).ToString("00") + " / 10", 11, Ui.Gold, TextAnchor.MiddleRight, 2f);
            Ui.Glitch(new Rect(x2, y2, w2, 28f), (topHunt != null ? topHunt.Name : name).ToUpperInvariant(), 20, Color.white, TextAnchor.MiddleLeft);
            y2 += 32f;
            string copy = topHunt == null ? name + " has no hunt yet."
                : !can ? (host ? "Locked: beat " + name + " in a run to open this hunt." : "Locked for the host.")
                : "Waves 1-" + (Hunts.Length - 1) + ": only " + name + "'s own enemies, tougher ones joining as you go. Wave " + Hunts.Length + ": " + name + ". Beat it and the hunt is won.";
            Ui.Label(new Rect(x2, y2, w2, 42f), copy, 13, can ? Ui.Text : topHunt == null ? Ui.Muted : Ui.Warn, TextAnchor.UpperLeft, false, true);
            y2 += 48f;
            if (topHunt != null)
            {
                // Wave ladder: nine small pips, then the boss.
                float px = x2;
                for (int i = 1; i <= Hunts.Length; i++)
                {
                    bool bossPip = i == Hunts.Length;
                    var pr = new Rect(px, y2, bossPip ? 34f : 22f, 20f);
                    var pc = bossPip ? HuntColor : new Color(ink.r, ink.g, ink.b, 1f);
                    Ui.Fill(pr, new Color(pc.r, pc.g, pc.b, bossPip ? 0.3f : 0.15f));
                    HuntBox(pr, new Color(pc.r, pc.g, pc.b, 0.7f));
                    Ui.Label(pr, bossPip ? "BOSS" : i.ToString(), 10, bossPip ? Color.white : Ui.Muted, TextAnchor.MiddleCenter);
                    px += pr.width + 5f;
                }
                y2 += 30f;

                // Record tiles.
                int played = profile.HuntPlayed(topBoss);
                float best = profile.HuntBestTime(topBoss);
                float tw = (w2 - 16f) / 3f, th = 46f;
                LobbyKit.Tile(new Rect(x2, y2, tw, th), can ? profile.HuntWon(topBoss) + " / " + played : "-", "HUNTS WON", false);
                LobbyKit.Tile(new Rect(x2 + tw + 8f, y2, tw, th), best > 0f ? (int)(best / 60f) + ":" + ((int)best % 60).ToString("00") : "-", "BEST TIME", false);
                LobbyKit.Tile(new Rect(x2 + 2f * (tw + 8f), y2, tw, th), can ? "OPEN" : "LOCKED", "STATUS", false);
                y2 += th + 10f;
                if (can)
                {
                    float rx = x2;
                    foreach (var rule in new[] { "NO PATHS", "NO CHALLENGE ROOMS", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" })
                    {
                        float rw = LobbyKit.SpacedWidth(rule, 10, 1f) + 12f;
                        if (rx + rw > x2 + w2) { rx = x2; y2 += 22f; }
                        var rr = new Rect(rx, y2, rw, 18f);
                        HuntBox(rr, new Color(ink.r, ink.g, ink.b, 0.3f));
                        LobbyKit.Spaced(rr, rule, 10, dim, TextAnchor.MiddleCenter, 1f);
                        rx += rw + 6f;
                    }
                }
            }

            if (BeginUiGl())
            {
                float oldGl = glAlpha;
                for (int i = 0; i < count; i++)
                {
                    if (rowAlpha[i] <= 0.01f)
                        continue;
                    var c = rowRects[i];
                    glAlpha = rowAlpha[i];
                    UiClip(new Rect(c.x + 30f, c.y + 2f, 36f, c.height - 4f));
                    DrawEnemyPreview(bosses[i] == EnemyType.TwinB ? EnemyType.TwinA : bosses[i], new V2(c.x + 48f, c.center.y), Mathf.Min(34f, c.height - 6f), t, i == ti);
                }
                glAlpha = oldGl;
                UiClip(new Rect(scope.x + 2f, scope.y + 22f, scope.width - 4f, scope.height - 44f));
                DrawEnemyPreview(topBoss == EnemyType.TwinB ? EnemyType.TwinA : topBoss, new V2(scope.center.x, scope.center.y), scope.height - 56f, t, true);
                EndUiGl();
            }
            // CRT finish over the scope (and the model): scan lines, then a vignette toward the edges.
            for (float sy = scope.y + 1f; sy < scope.yMax - 1f; sy += 3f)
                Ui.Fill(new Rect(scope.x + 1f, sy, scope.width - 2f, 1f), new Color(0f, 0f, 0f, 0.25f));
            for (int k = 0; k < 8; k++)
            {
                float a = 0.30f * (1f - k / 8f) * (1f - k / 8f);
                float d = 1f + k * 4f;
                var vc = new Color(0f, 0f, 0f, a);
                Ui.Fill(new Rect(scope.x + d, scope.y + d, scope.width - 2f * d, 4f), vc);
                Ui.Fill(new Rect(scope.x + d, scope.yMax - d - 4f, scope.width - 2f * d, 4f), vc);
                Ui.Fill(new Rect(scope.x + d, scope.y + d + 4f, 4f, scope.height - 2f * d - 8f), vc);
                Ui.Fill(new Rect(scope.xMax - d - 4f, scope.y + d + 4f, 4f, scope.height - 2f * d - 8f), vc);
            }
        }
    }
}
