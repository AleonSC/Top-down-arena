using UnityEngine;

namespace Arena
{
    /// Boss approaches on screen (Approaches.cs): a chip above the wave panel while a wave leads up to a boss, and a
    /// line in the between-waves banner as you set off and when the boss is near.
    public partial class ArenaApp
    {
        static readonly Color ApproachColor = new Color(1f, 0.72f, 0.4f);

        /// The objective as one quiet CRT line at the top centre: "THE WARDEN'S OWN  1/9  ·  [FUNNELS]" (a hunt), or
        /// "APPROACH  ·  THE WARDEN  3/4" on the road to a boss, with the arena layout's name as a small tag after it.
        /// Replaces the chips that used to stack above the wave panel (Docs/Mockups/crt-hud-a-variants.html, A1).
        void DrawObjectiveLine()
        {
            string head = null, count = null;
            if (game.State == MatchState.Fighting && game.ApproachShare(game.Wave) > 0f)
            {
                if (game.Hunt != null)
                {
                    head = Defs.Get(game.Hunt.Boss).Name.ToUpperInvariant() + "'S OWN";
                    count = game.HuntWave + "/" + (Hunts.Length - 1);
                }
                else
                {
                    head = "APPROACH  ·  " + Defs.Get(Game.ApproachBoss(game.Wave)).Name.ToUpperInvariant();
                    count = Game.ApproachStep(game.Wave) + "/4";
                }
            }
            string tag = game.LayoutOn && game.ActiveLayout != null ? game.ActiveLayout.Name.ToUpperInvariant() : null;
            if (head == null && tag == null)
                return;
            bool calm = CabCalm;
            const int size = 15;
            float hw = head != null ? CrtUi.TitleWidth(head, size) + 8f : 0f, cw = count != null ? CrtUi.TitleWidth(count, size) + 14f : 0f;
            float tw = tag != null ? CrtUi.TitleWidth(tag, 12) + 16f : 0f, gap = head != null && tag != null ? 10f : 0f;
            float x = W / 2f - (hw + cw + gap + tw) / 2f, y = BossUp() ? 92f : 14f;
            if (head != null)
            {
                CrtUi.Text(new Rect(x, y, hw, 20f), head, size, Ui.Gold, TextAnchor.MiddleLeft, true, calm);
                CrtUi.Text(new Rect(x + hw, y, cw, 20f), count, size, CrtUi.Dim, TextAnchor.MiddleLeft, true, true);
                x += hw + cw + gap;
            }
            if (tag != null)
            {
                var r = new Rect(x, y + 2f, tw, 17f);
                Ui.Fill(r, new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.12f));
                Ui.Rim(r, CrtUi.Dim, 0.8f);
                CrtUi.Text(r, tag, 12, CrtUi.Dim, TextAnchor.MiddleCenter, true, true);
            }
        }

        /// The banner's line before the next wave: setting off toward a boss, or that it's near.
        string ApproachLine(int next)
        {
            if (game.ApproachShare(next) <= 0f)
                return "";
            string boss = Defs.Get(game.ApproachBossAt(next)).Name;
            if (game.Hunt != null)
                return next == Hunts.Length - 1 ? boss + " is near  ·  " : next == 1 ? "On the hunt for " + boss + "  ·  " : "";
            int step = game.ApproachStepAt(next);
            return step == 4 ? boss + " is near  ·  " : step == 1 ? "On the road to " + boss + "  ·  " : "";
        }
    }
}
