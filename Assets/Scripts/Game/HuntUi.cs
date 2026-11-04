using System;
using UnityEngine;

namespace Arena
{
    /// The lobby's Boss Hunt tab (Hunts.cs): a card for each of the ten bosses, its live model, and whether its hunt
    /// is open (beaten in a run), locked, or still to come; the picked one's details and this player's hunt records.
    public partial class ArenaApp
    {
        static readonly Color HuntColor = new Color(0.95f, 0.42f, 0.32f);

        void DrawHuntPicker(Rect r, float t)
        {
            Ui.BodyBox(r);
            bool host = session.IsHost;
            var profile = game.Profile;
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            Ui.Fancy(new Rect(x, y, w, 28f), "BOSS HUNT", 20, Color.white, Color.Lerp(HuntColor, Color.white, 0.4f), TextAnchor.MiddleLeft, HuntColor);
            Ui.Label(new Rect(x, y + 28f, w, 18f), host ? "Pick a boss to hunt: nine waves of its own enemies, then the boss. Beat a boss in a run to open its hunt."
                : "The host picks the boss (from the hunts they've opened).", 12, Ui.Muted);
            y += 56f;

            // Ten cards, five to a row, in the order the bosses come.
            const int cols = 5, count = 10;
            const float gap = 8f;
            float cw = (w - gap * (cols - 1)) / cols, ch = 150f;
            var cards = new Rect[count];
            var bosses = new EnemyType[count];
            for (int i = 0; i < count; i++)
            {
                int wave = (i + 1) * 5;
                var boss = Game.BossForWave(wave);
                bosses[i] = boss;
                var hunt = Hunts.Get(boss);
                bool open = Hunts.Available(hunt, profile);
                bool picked = session.HuntBoss == boss && hunt != null;
                var c = new Rect(x + (i % cols) * (cw + gap), y + (i / cols) * (ch + gap), cw, ch);
                cards[i] = c;
                bool hover = host && open && c.Contains(Event.current.mousePosition);
                Ui.Card(c, picked ? HuntColor : open ? Ui.Gold : Ui.Muted, picked ? 0.9f : hover ? 0.5f : open ? 0.25f : 0.08f);
                if (picked)
                    Ui.Pill(new Rect(c.x + 2f, c.y + 2f, c.width - 4f, c.height - 4f), new Color(HuntColor.r, HuntColor.g, HuntColor.b, 0.12f + 0.04f * Mathf.Sin(t * 3f)));
                Ui.Label(new Rect(c.x, c.y + 96f, c.width, 18f), BossName(boss), 13, open || picked ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
                string state = hunt == null ? "Coming soon" : open ? (profile.HuntWon(boss) > 0 ? "Won " + profile.HuntWon(boss) + " of " + profile.HuntPlayed(boss) : Hunts.Length + " waves") : "Locked";
                Ui.Label(new Rect(c.x, c.y + 116f, c.width, 16f), state, 11, hunt != null && open ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter);
                if (hunt != null && !open)
                    Ui.Label(new Rect(c.x + 4f, c.y + 132f, c.width - 8f, 14f), "Beat it in a run", 10, Ui.Muted, TextAnchor.MiddleCenter);
                if (hover && !picked && Ui.HitArea(c))
                    session.SetHuntBoss(boss);
            }
            if (BeginUiGl())
            {
                for (int i = 0; i < count; i++)
                {
                    var c = cards[i];
                    UiClip(new Rect(c.x + 2f, c.y + 2f, c.width - 4f, 92f));
                    DrawEnemyPreview(bosses[i] == EnemyType.TwinB ? EnemyType.TwinA : bosses[i], new V2(c.center.x, c.y + 50f), 80f, t, session.HuntBoss == bosses[i]);
                }
                EndUiGl();
            }
            y += 2f * (ch + gap) + 8f;

            // The picked hunt.
            var picked2 = Hunts.Get(session.HuntBoss);
            var info = new Rect(x, y, w, Mathf.Max(60f, r.yMax - 14f - y));
            Ui.Pill(info, new Color(1f, 1f, 1f, 0.04f));
            if (picked2 == null)
            {
                Ui.Label(new Rect(info.x + 14f, info.y, info.width - 28f, 40f), "Pick a boss above.", 13, Ui.Muted, TextAnchor.MiddleLeft);
                return;
            }
            bool can = Hunts.Available(picked2, profile);
            string name = BossName(picked2.Boss);
            Ui.Fancy(new Rect(info.x + 14f, info.y + 8f, info.width - 28f, 26f), picked2.Name.ToUpperInvariant(), 18, Color.white, Color.Lerp(HuntColor, Color.white, 0.4f), TextAnchor.MiddleLeft);
            Ui.Label(new Rect(info.x + 14f, info.y + 36f, info.width - 28f, 40f),
                "Waves 1-" + (Hunts.Length - 1) + ": only " + name + "'s own enemies, tougher ones joining as you go. Wave " + Hunts.Length + ": " + name
                + ". Beat it and the hunt is won. No paths, challenge rooms or saving; modifiers and difficulty still apply.", 12, Ui.Text, TextAnchor.UpperLeft, false, true);
            string rec;
            if (!can)
                rec = host ? "Locked: beat " + name + " in a run to open this hunt." : "Locked for the host.";
            else if (profile.HuntPlayed(picked2.Boss) == 0)
                rec = "Your hunts: not played yet.";
            else
            {
                float best = profile.HuntBestTime(picked2.Boss);
                rec = "Your hunts: won " + profile.HuntWon(picked2.Boss) + " of " + profile.HuntPlayed(picked2.Boss)
                    + (best > 0f ? "  ·  best " + (int)(best / 60f) + ":" + ((int)best % 60).ToString("00") : "");
            }
            Ui.Label(new Rect(info.x + 14f, info.y + 78f, info.width - 28f, 18f), rec, 12, can ? Ui.Gold : Ui.Warn, TextAnchor.MiddleLeft, true);
        }
    }
}
