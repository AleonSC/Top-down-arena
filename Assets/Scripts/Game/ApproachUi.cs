using UnityEngine;

namespace Arena
{
    /// Boss approaches on screen (Approaches.cs): a chip above the wave panel while a wave leads up to a boss, and a
    /// line in the between-waves banner as you set off and when the boss is near.
    public partial class ArenaApp
    {
        /// The objective as one quiet CRT line at the top centre, in the wave panel's thin CRT font: the coming boss's name, then
        /// how far off it is ("THE WARDEN  IN 3 WAVES", "... NEXT WAVE"), and the arena layout's name as a small tag after it.
        /// Every piece is measured in the font it's drawn in (CrtUi.Title), so the pieces never overlap.
        void DrawObjectiveLine()
        {
            string head = null, count = null;
            if (game.State == MatchState.Fighting && game.ApproachShare(game.Wave) > 0f)
            {
                var boss = game.Hunt != null ? game.Hunt.Boss : game.ApproachBossAt(game.Wave);
                head = Defs.Get(boss).Name.ToUpperInvariant();
                int left = game.Bosses.NextBossWaveFrom(game.Wave) - game.Wave;
                count = left <= 1 ? "NEXT WAVE" : "IN " + left + " WAVES";
            }
            string tag = game.LayoutOn && game.ActiveLayout != null ? game.ActiveLayout.Name.ToUpperInvariant() : null;
            if (head == null && tag == null)
                return;
            bool calm = CabCalm;
            const int size = 17, tagSize = 11;
            float hw = head != null ? CrtUi.TitleWidth(head, size) : 0f, cg = head != null ? 10f : 0f;
            float cw = count != null ? CrtUi.TitleWidth(count, size) : 0f;
            float tw = tag != null ? CrtUi.TitleWidth(tag, tagSize) + 14f : 0f, gap = head != null && tag != null ? 14f : 0f;
            float x = W / 2f - (hw + cg + cw + gap + tw) / 2f, y = BossUp() ? HudObjectiveUnderBossY : HudObjectiveY;  // (HudLayout.cs)
            if (head != null)
            {
                CrtUi.Title(new Rect(x, y, hw + 4f, 22f), head, size, CrtUi.Ink, TextAnchor.MiddleLeft, calm);
                CrtUi.Title(new Rect(x + hw + cg, y, cw + 4f, 22f), count, size, CrtUi.Dim, TextAnchor.MiddleLeft, true);
                x += hw + cg + cw + gap;
            }
            if (tag != null)
            {
                var r = new Rect(x, y + 3f, tw, 16f);
                Ui.Fill(r, new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.12f));
                Ui.Rim(r, CrtUi.Dim, 0.8f);
                CrtUi.Title(r, tag, tagSize, CrtUi.Dim, TextAnchor.MiddleCenter, true);
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
