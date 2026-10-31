using UnityEngine;

namespace Arena
{
    /// The pause menu's "Field guide": cards for the in-game Bestiary and Armory databases (FieldGuide.cs), with live
    /// previews of what's inside. Each also links to its published web page (sources in the repo's Docs folder).
    public partial class ArenaApp
    {
        const string BestiaryUrl = "https://claude.ai/artifact/SxsKDQBSXHmNN2QTM3f664";
        const string ArmoryUrl = "https://claude.ai/artifact/Vx6Rkk2y7TyRd47ADoSnKz";

        static readonly EnemyType[] BestiaryShowcase = { EnemyType.Swarmer, EnemyType.Shielder, EnemyType.Boss, EnemyType.Spinner, EnemyType.Singularity };

        void DrawFieldGuide(Rect col, float t)
        {
            Ui.Fancy(new Rect(col.x, col.y, col.width, 30f), "FIELD GUIDE", 20, Color.white, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleLeft, Ui.Accent);
            Ui.Label(new Rect(col.x, col.y + 28f, col.width, 16f), "Look anything up without leaving the match", 11, Ui.Muted);
            float y = col.y + 52f, h = (col.yMax - y - 12f) / 2f;
            DrawRefCard(new Rect(col.x, y, col.width, h), "Bestiary", "All " + BestiaryData.All.Length + " enemies and bosses: how they fight, their attacks, and their health at your wave.",
                BestiaryUrl, "Docs/Bestiary/arena-bestiary.html", new Color(1f, 0.45f, 0.5f), t, true);
            DrawRefCard(new Rect(col.x, y + h + 12f, col.width, h), "Armory", "Every class, weapon, ability, ultimate and upgrade card, with what's yours this match.",
                ArmoryUrl, "Docs/Armory/out/arena-armory.html", new Color(0.45f, 0.75f, 1f), t, false);
        }

        void DrawRefCard(Rect r, string title, string blurb, string url, string localPath, Color accent, float t, bool enemies)
        {
            Ui.Card(r, accent, 0.6f);
            // A little parade of what's inside.
            var stage = new Rect(r.x + 12f, r.y + 12f, r.width - 24f, r.height * 0.5f);
            Ui.Pill(stage, new Color(accent.r * 0.15f, accent.g * 0.15f, accent.b * 0.15f, 0.9f));
            if (BeginUiGl())
            {
                if (enemies)
                    for (int i = 0; i < BestiaryShowcase.Length; i++)
                    {
                        bool boss = Defs.Get(BestiaryShowcase[i]).IsBoss;
                        float x = stage.x + stage.width * (i + 0.5f) / BestiaryShowcase.Length;
                        DrawEnemyPreview(BestiaryShowcase[i], new V2(x, stage.center.y + (i % 2 == 0 ? -6f : 8f)), boss ? stage.height * 0.62f : stage.height * 0.38f, t + i, true);
                    }
                else
                {
                    Role[] roles = { Role.Tank, Role.Healer, Role.Dps, Role.Tank };
                    Kit[] kits = { Kit.Knight, Kit.Mage, Kit.Soldier, Kit.Summoner };
                    for (int i = 0; i < 4; i++)
                    {
                        UiModelAt(new V2(stage.x + stage.width * (i + 0.5f) / 4f, stage.center.y + (i % 2 == 0 ? -6f : 8f)), 1.1f);
                        DrawPreviewPlayer(roles[i], kits[i], i % 3, t + i * 1.3f);
                    }
                }
                EndUiGl();
            }
            if (GUI.Button(stage, GUIContent.none, GUIStyle.none))
                OpenFieldGuide(!enemies);  // the parade opens it too
            float y = stage.yMax + 8f;
            Ui.Fancy(new Rect(r.x + 16f, y, r.width - 32f, 28f), title.ToUpperInvariant(), 20, Color.white, Color.Lerp(accent, Color.white, 0.35f), TextAnchor.MiddleLeft, accent);
            y += 28f;
            Ui.Label(new Rect(r.x + 16f, y, r.width - 32f, 32f), blurb, 11, Ui.Text, TextAnchor.UpperLeft, false, true);
            // Open the database here; the published page (with live weapon demos) is a click away too. In the Unity
            // editor the web button opens the local copy, which works offline.
            float by = r.yMax - 40f, bw = (r.width - 40f) * 0.66f;
            if (Ui.Button(new Rect(r.x + 16f, by, bw, 30f), "Open " + title, new Color(accent.r * 0.8f, accent.g * 0.8f, accent.b * 0.8f)))
                OpenFieldGuide(!enemies);
            string local = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", localPath));
            bool hasLocal = Application.isEditor && System.IO.File.Exists(local);
            if (Ui.SmallButton(new Rect(r.x + 24f + bw, by, r.width - 40f - bw, 30f), hasLocal ? "Local page" : "Web page"))
                Application.OpenURL(hasLocal ? "file:///" + local.Replace('\\', '/') : url);
        }
    }
}
