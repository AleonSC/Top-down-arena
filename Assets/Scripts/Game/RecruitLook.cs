namespace Arena
{
    /// A Training recruit's model (Core/DrillGame.cs MakeRecruit): no class or role look, just plain grey fatigues with a dark
    /// outline, a helmet with a Training-cyan visor, and the Training Pistol held out in the right hand, the left hand at the hip.
    /// Drawn in place of the class model (CharacterModels.cs DrawPlayerModel). Preview: `DRILL=1,6.2 shot.sh x hud 1 1 2`.
    public partial class ArenaApp
    {
        static readonly Rgba RecruitDark = new Rgba(0.13f, 0.15f, 0.2f), RecruitBase = new Rgba(0.5f, 0.55f, 0.62f),
            RecruitLight = new Rgba(0.74f, 0.78f, 0.84f), RecruitVisor = new Rgba(0.45f, 0.9f, 1f), RecruitGun = new Rgba(0.24f, 0.27f, 0.33f),
            RecruitHelmet = new Rgba(0.33f, 0.37f, 0.44f);

        /// A recruit's footprint ring: neutral, not a role's colour.
        public static readonly Rgba RecruitRing = new Rgba(0.6f, 0.66f, 0.75f);

        void DrawRecruitModel(Player p, float r, float t, Rgba col)
        {
            mO = p.Disp; mAim = p.Aim; mDir = V2.FromAngle(p.Aim); mSide = new V2(-mDir.Y, mDir.X); mU = r / 18f; mT = t;
            mAlpha = col.A; mWhite = p.Flash > 0f ? 0.85f : 0f;
            MFrame(0f, 0f, 0f, 1f, false);
            float kick = p.FireCd > 0f ? UnityEngine.Mathf.Clamp01(p.FireCd / 0.12f) * 2f : 0f;  // a small recoil as it fires
            MEll(1f, 2f, 11f, 15f, new Rgba(0f, 0f, 0f, 0.28f));  // the shadow
            // Shoulders: a plain outline and fill, a lighter strip across the chest.
            MEll(-1f, 0f, 9.5f, 13.5f, RecruitDark);
            MEll(-1f, 0f, 8f, 12f, RecruitBase);
            MRect(-2.5f, -9f, 2.2f, 18f, RecruitLight.WithAlpha(0.35f));
            // Left hand at the hip, right arm out to the pistol.
            MCirc(3f, -11f, 2.6f, RecruitDark);
            MCirc(3f, -11f, 1.8f, RecruitLight);
            float hx = 14f - kick;
            MLine(2f, 8f, hx, 6f, 4.2f, RecruitDark);
            MLine(2f, 8f, hx, 6f, 2.6f, RecruitBase);
            // The Training Pistol: grip under the hand, a short slide, a cyan sight dot.
            MRect(hx - 1.5f, 4.5f, 4f, 4.5f, RecruitGun);
            MRect(hx - 2f, 3.4f, 11f, 3.6f, RecruitDark);
            MRect(hx - 1.4f, 4f, 9.8f, 2.4f, RecruitGun);
            MCirc(hx + 8.2f, 5.2f, 0.9f, RecruitVisor);
            MCirc(hx, 6f, 2.6f, RecruitDark);
            MCirc(hx, 6f, 1.8f, RecruitLight);
            // The helmet and its visor.
            MCirc(0.5f, 0f, 6.6f, RecruitDark, 16);
            MCirc(0.5f, 0f, 5.4f, RecruitHelmet, 16);
            MCirc(-0.8f, -1.2f, 2.2f, RecruitBase.WithAlpha(0.6f), 10);  // a dull highlight on the crown
            MArc(0.5f, 0f, 4.2f, -0.85f, 0.85f, 2.2f, RecruitVisor);
        }
    }
}
