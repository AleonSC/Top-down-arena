using System;
using UnityEngine;

namespace Arena
{
    /// The lobby's Boss Hunt tab (Hunts.cs): a card for each of the ten bosses, its live model, and whether its hunt
    /// is open (beaten in a run), locked, or still to come; the picked one's details and this player's hunt records.
    public partial class ArenaApp
    {
        static readonly Color HuntColor = new Color(0.95f, 0.42f, 0.32f);

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

            // Left: the ten bosses in the order they come, one row each.
            const int count = 10;
            const float gap = 5f, listW = 268f;
            float rh = Mathf.Clamp((bodyH - gap * (count - 1)) / count, 30f, 46f);
            var rows = new Rect[count];
            var bosses = new EnemyType[count];
            for (int i = 0; i < count; i++)
            {
                var boss = Game.BossForWave((i + 1) * 5);
                bosses[i] = boss;
                var hunt = Hunts.Get(boss);
                bool open = Hunts.Available(hunt, profile);
                bool picked = session.HuntBoss == boss && hunt != null;
                var c = new Rect(x, bodyY + i * (rh + gap), listW, rh);
                rows[i] = c;
                bool hover = host && open && c.Contains(Event.current.mousePosition);
                Ui.Card(c, picked ? HuntColor : open ? Ui.Gold : Ui.Muted, picked ? 0.9f : hover ? 0.5f : open ? 0.25f : 0.08f);
                if (picked)
                    Ui.Pill(new Rect(c.x + 2f, c.y + 2f, c.width - 4f, c.height - 4f), new Color(HuntColor.r, HuntColor.g, HuntColor.b, 0.12f + 0.04f * Mathf.Sin(t * 3f)));
                Ui.Label(new Rect(c.x + 8f, c.y, 20f, c.height), (i + 1).ToString("00"), 10, Ui.Muted, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(c.x + 70f, c.y, c.width - 70f - 78f, c.height), BossName(boss), 14, open || picked ? Color.white : Ui.Muted, TextAnchor.MiddleLeft, true);
                string chip = hunt == null ? "SOON" : !open ? "LOCKED" : profile.HuntWon(boss) > 0 ? "WON x" + profile.HuntWon(boss) : "OPEN";
                Color cc = hunt == null ? Ui.Muted : !open ? HuntColor : profile.HuntWon(boss) > 0 ? Ui.Gold : new Color(0.5f, 0.88f, 0.69f);
                HuntChip(c.xMax - 8f, c.center.y, chip, cc);
                if (hover && !picked && Ui.HitArea(c))
                    session.SetHuntBoss(boss);
            }

            // Right: the picked hunt's dossier.
            float x2 = x + listW + 14f, w2 = w - listW - 14f, y2 = bodyY;
            var picked2 = Hunts.Get(session.HuntBoss);
            var scope = new Rect(x2, y2, w2, Mathf.Min(220f, bodyH * 0.38f));
            Ui.Fill(scope, new Color(0.02f, 0.04f, 0.09f, 0.92f));
            HuntBox(scope, new Color(ink.r, ink.g, ink.b, 0.35f));
            Ui.Fill(new Rect(scope.x, scope.center.y, scope.width, 1f), new Color(ink.r, ink.g, ink.b, 0.12f));
            Ui.Fill(new Rect(scope.center.x, scope.y, 1f, scope.height), new Color(ink.r, ink.g, ink.b, 0.12f));
            LobbyKit.Corners(scope, 14f, 2f);
            y2 += scope.height + 12f;
            if (picked2 == null)
            {
                Ui.Label(new Rect(scope.x, scope.y, scope.width, scope.height), "NO TARGET", 18, Ui.Muted, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(x2, y2, w2, 24f), "Pick a boss on the left.", 13, Ui.Muted, TextAnchor.MiddleLeft);
            }
            else
            {
                bool can = Hunts.Available(picked2, profile);
                string name = BossName(picked2.Boss);
                var dim = new Color(ink.r, ink.g, ink.b, 0.7f);
                LobbyKit.Spaced(new Rect(scope.x + 10f, scope.y + 6f, 160f, 16f), "TARGET LOCK", 11, dim, TextAnchor.MiddleLeft, 2f);
                LobbyKit.Spaced(new Rect(scope.xMax - 170f, scope.y + 6f, 160f, 16f), "WAVE " + Hunts.Length + " / " + Hunts.Length, 11, Ui.Gold, TextAnchor.MiddleRight, 2f);
                Ui.Glitch(new Rect(x2, y2, w2, 28f), picked2.Name.ToUpperInvariant(), 20, Color.white, TextAnchor.MiddleLeft);
                y2 += 32f;
                Ui.Label(new Rect(x2, y2, w2, 42f),
                    "Waves 1-" + (Hunts.Length - 1) + ": only " + name + "'s own enemies, tougher ones joining as you go. Wave " + Hunts.Length + ": " + name
                    + ". Beat it and the hunt is won.", 13, Ui.Text, TextAnchor.UpperLeft, false, true);
                y2 += 48f;

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
                int played = profile.HuntPlayed(picked2.Boss);
                float best = profile.HuntBestTime(picked2.Boss);
                float tw = (w2 - 16f) / 3f, th = 46f;
                LobbyKit.Tile(new Rect(x2, y2, tw, th), can ? profile.HuntWon(picked2.Boss) + " / " + played : "-", "HUNTS WON", false);
                LobbyKit.Tile(new Rect(x2 + tw + 8f, y2, tw, th), best > 0f ? (int)(best / 60f) + ":" + ((int)best % 60).ToString("00") : "-", "BEST TIME", false);
                LobbyKit.Tile(new Rect(x2 + 2f * (tw + 8f), y2, tw, th), can ? "OPEN" : "LOCKED", "STATUS", false);
                y2 += th + 10f;
                if (!can)
                    Ui.Label(new Rect(x2, y2, w2, 18f), host ? "Locked: beat " + name + " in a run to open this hunt." : "Locked for the host.", 12, Ui.Warn, TextAnchor.MiddleLeft, true);
                else
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
                for (int i = 0; i < count; i++)
                {
                    var c = rows[i];
                    UiClip(new Rect(c.x + 30f, c.y + 2f, 36f, c.height - 4f));
                    DrawEnemyPreview(bosses[i] == EnemyType.TwinB ? EnemyType.TwinA : bosses[i], new V2(c.x + 48f, c.center.y), Mathf.Min(34f, c.height - 6f), t, session.HuntBoss == bosses[i]);
                }
                if (picked2 != null)
                {
                    UiClip(new Rect(scope.x + 2f, scope.y + 22f, scope.width - 4f, scope.height - 44f));
                    DrawEnemyPreview(picked2.Boss == EnemyType.TwinB ? EnemyType.TwinA : picked2.Boss, new V2(scope.center.x, scope.center.y), scope.height - 56f, t, true);
                }
                EndUiGl();
            }
        }
    }
}
