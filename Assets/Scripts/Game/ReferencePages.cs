using UnityEngine;

namespace Arena
{
    /// The published Bestiary and Armory pages (claude.ai artifacts; their sources are in the repo's Docs folder),
    /// opened from the Field guide's "Web page" button (FieldGuide.cs). In the Unity editor it opens the local copy,
    /// which works offline.
    public partial class ArenaApp
    {
        const string BestiaryUrl = "https://claude.ai/artifact/SxsKDQBSXHmNN2QTM3f664";
        const string ArmoryUrl = "https://claude.ai/artifact/Vx6Rkk2y7TyRd47ADoSnKz";

        /// The Field guide button's colour (Settings and the pause menu).
        static readonly Color FieldGuideColor = new Color(0.32f, 0.45f, 0.78f);

        void OpenReferencePage(bool armory)
        {
            string local = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..",
                armory ? "Docs/Armory/out/arena-armory.html" : "Docs/Bestiary/arena-bestiary.html"));
            if (Application.isEditor && System.IO.File.Exists(local))
                Application.OpenURL("file:///" + local.Replace('\\', '/'));
            else
                Application.OpenURL(armory ? ArmoryUrl : BestiaryUrl);
        }
    }
}
