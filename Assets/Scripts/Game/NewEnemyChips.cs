using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The new-enemy chips (option B of Docs/Mockups/new-enemy-banner.html): under the between-waves box (and under the
    /// GET READY band), one CRT key per enemy type the next wave brings that this profile has not met yet: the job's
    /// marker, its name, the job, a gold NEW tag and one line on what to do about it (Game.EnemyNote). A type counts as met
    /// once it has carried its name tag in a fight (EnemyTagLook.cs, Profile.TagSeen), so a chip shows until you have
    /// seen the enemy. At most three chips; boss waves and the Practice Range show none. Front end only.
    public partial class ArenaApp
    {
        const float ChipW = 320f, ChipH = 80f, ChipGap = 12f;  // (room for a two-line note: the longest are about 70 characters)
        const int ChipMax = 3;
        readonly List<EnemyType> chipTypes = new List<EnemyType>();
        int chipWave = -1;  // (the wave after this one, doubled; plus one when the boss comes first)

        /// The types to show for the wave after this one: new on it, not yet met, with a note.
        List<EnemyType> ChipsFor(int next)
        {
            int key = next * 2 + (game.BossNext ? 1 : 0);
            if (chipWave != key || game.Profile == null)
            {
                chipWave = key;
                chipTypes.Clear();
                if (game.Profile != null && game.Practice == null && !game.BossNext)
                    foreach (var t in Game.NewTypesAt(next))
                    {
                        game.Profile.TagSeen.TryGetValue(t.ToString(), out int met);
                        if (met == 0 && Game.EnemyNote(t).Length > 0 && chipTypes.Count < ChipMax)
                            chipTypes.Add(t);
                    }
            }
            return chipTypes;
        }

        /// Draws the chips in a centred row, their tops at `top`, fading in over `ease` (0-1).
        void DrawNewEnemyChips(float top, float ease)
        {
            var list = ChipsFor(game.Wave + 1);
            if (list.Count == 0 || ease <= 0f)
                return;
            float oldAlpha = Ui.Alpha;
            Ui.Alpha *= ease;
            float total = list.Count * ChipW + (list.Count - 1) * ChipGap, x = W / 2f - total / 2f;
            foreach (var t in list)
            {
                DrawEnemyChip(new Rect(x, top, ChipW, ChipH), t);
                x += ChipW + ChipGap;
            }
            Ui.Alpha = oldAlpha;
        }

        void DrawEnemyChip(Rect r, EnemyType t)
        {
            var d = Defs.Get(t);
            int job = (int)Game.JobOf(t);
            Color jc = ToColor(JobColors[job]);
            Ui.Fill(r, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.95f));  // opaque enough to read over the arena
            CrtKit.SoftBox(r, CrtUi.Ink, false, false, false);
            Ui.Fill(new Rect(r.x, r.y, 3f, r.height), jc);  // the job's colour down the left edge
            DrawJobGlyph(new Rect(r.x + 14f, r.y + 8f, 20f, 20f), (EnemyJob)job, jc);
            HudCrt(new Rect(r.x + 44f, r.y + 5f, 170f, 22f), d.Name.ToUpperInvariant(), 22, Color.white);
            HudCrt(new Rect(r.x + 44f, r.y + 26f, 130f, 14f), JobNames[job], 14, jc);
            var tag = new Rect(r.xMax - 12f - 38f, r.y + 8f, 38f, 17f);
            Ui.Fill(tag, CrtStyle.Gold);
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            Ui.Label(tag, "NEW", 15, new Color(0.11f, 0.07f, 0f), TextAnchor.MiddleCenter, false);
            Ui.CrtFont = was;
            Ui.Label(new Rect(r.x + 14f, r.y + 42f, r.width - 26f, 34f), Game.EnemyNote(t), 12, CrtUi.Dim, TextAnchor.UpperLeft, true, true);
        }

        /// The job marker's shape in HUD space (the same five shapes as the one under the enemy, JobMarkerLook.cs).
        void DrawJobGlyph(Rect b, EnemyJob job, Color c)
        {
            Vector2 m = b.center;
            switch (job)
            {
                case EnemyJob.Pressure:
                    Ui.Dot(new Rect(m.x - 6f, m.y - 6f, 12f, 12f), c);
                    break;
                case EnemyJob.Zoner:
                    Ui.Dot(new Rect(m.x - 8f, m.y - 8f, 16f, 16f), c, true);
                    break;
                case EnemyJob.Anchor:
                    Ui.Fill(new Rect(m.x - 7f, m.y - 7f, 14f, 14f), c);
                    break;
                case EnemyJob.Disruptor:
                    RotatedBar(m, 14f, 14f, 45f, c);
                    break;
                default:  // Hunter: a chevron, two bars
                    RotatedBar(m + new Vector2(-4f, 1f), 11f, 3f, 50f, c);
                    RotatedBar(m + new Vector2(4f, 1f), 11f, 3f, -50f, c);
                    break;
            }
        }

        static void RotatedBar(Vector2 center, float w, float h, float degrees, Color c)
        {
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, center);
            Ui.Fill(new Rect(center.x - w / 2f, center.y - h / 2f, w, h), c);
            GUI.matrix = old;
        }
    }
}
