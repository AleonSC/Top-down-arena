using UnityEngine;

namespace Arena
{
    /// The animated background behind the menus, lobby and runs. The main menu always shows the Holo Floor (MenuHolo.cs) unless
    /// the player turns on "Override menu background" (Look window > Background); everywhere else shows the player's pick: Deep
    /// Field (the free default) or a rank-reward one (BackgroundArena.cs).
    public partial class ArenaApp
    {
        public static readonly string[] BackgroundNames = { "Holo Floor", "Deep Field", "Nebula Drift", "Aurora Veil" };  // 1: the default pick, 2-3: rank rewards (BackgroundArena.cs)

        const int HoloFloorBg = 0;   // the main menu's own
        const int DeepFieldBg = 1;   // everyone's pick until they equip a reward one

        /// Where the rank-reward backgrounds start in BackgroundNames. Each is a Background cosmetic in Rank.cs.
        const int FirstRewardBg = 2;

        /// The cosmetic id of each reward background, in BackgroundNames order from FirstRewardBg. To add one: a name here and in
        /// BackgroundNames, an accent, a `case` in DrawMenuBackground, a CosmeticDef in Rank.cs.
        static readonly string[] RewardBackgrounds = { "bg-react-nebula", "bg-react-aurora" };

        static string BackgroundCosmeticId(int i) => i >= FirstRewardBg && i - FirstRewardBg < RewardBackgrounds.Length ? RewardBackgrounds[i - FirstRewardBg] : null;
        static int BackgroundIndexOfCosmetic(string id) { int k = System.Array.IndexOf(RewardBackgrounds, id); return k < 0 ? DeepFieldBg : FirstRewardBg + k; }
        static bool BackgroundLocked(int i) { var id = BackgroundCosmeticId(i); return id != null && !Rank.Owns(Profile.Current.RankNow, id); }

        int menuBg = DeepFieldBg;    // the player's pick
        bool menuBgOverride;         // pref "menu_bg_override": the pick shows on the main menu too, instead of the Holo Floor
        bool bgThumb;                // drawing a thumbnail: always the background asked for
        Profile bgProfile;           // the profile menuBg was read from (a profile switch reads it again)

        void LoadBackgroundSettings()
        {
            bgProfile = Profile.Current;
            menuBg = BackgroundIndexOfCosmetic(bgProfile.Background);  // the equipped reward one, else Deep Field
            if (BackgroundLocked(menuBg))
                menuBg = DeepFieldBg;
            menuBgOverride = PlayerPrefs.GetInt("menu_bg_override", 0) == 1;
        }

        void SetMenuBgOverride(bool on)
        {
            menuBgOverride = on;
            PlayerPrefs.SetInt("menu_bg_override", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        void SetBackground(int i)
        {
            menuBg = BackgroundLocked(i) || i < FirstRewardBg || i >= BackgroundNames.Length ? DeepFieldBg : i;
            var prof = Profile.Current;
            prof.Background = BackgroundCosmeticId(menuBg) ?? "";  // what Records shows as equipped
            prof.Save();
        }

        /// Behind the menus, in UI canvas coordinates.
        void DrawMenuBackground(float time)
        {
            ReactTick(time);  // BackgroundReact.cs: pointer and clicks for the reactive backgrounds
            if (!bgThumb && bgProfile != Profile.Current)
                LoadBackgroundSettings();
            int bg = !bgThumb && session != null && (session.Screen == AppScreen.Menu || crtBgMenu || (crtEntry && LobbyEntering)) && !menuBgOverride ? HoloFloorBg : menuBg;
            switch (bg)
            {
                case 1: BgDeepField(time); break;
                case 2: BgNebulaDrift(time); break;
                case 3: BgAuroraVeil(time); break;
                default: BgHoloFloor(time); break;  // MenuHolo.cs
            }
        }

        static readonly Color[] BackgroundAccents = { new Color(1f, 0.45f, 0.5f), new Color(0.72f, 0.82f, 1f), new Color(0.45f, 0.4f, 0.95f), new Color(0.2f, 0.75f, 0.85f) };  // Holo Floor, Deep Field, Nebula Drift, Aurora Veil



        /// A chevron (two strokes meeting at a point) centred on c, pointing along dir.
        void Chevron(V2 c, V2 dir, float size, float width, Rgba col)
        {
            var side = new V2(-dir.Y, dir.X);
            var tip = c + dir * (size * 0.3f);
            Line(c - dir * (size * 0.3f) + side * (size * 0.5f), tip, width, col);
            Line(c - dir * (size * 0.3f) - side * (size * 0.5f), tip, width, col);
        }

        /// A live thumbnail of the background, rimmed in `accent`.
        void BackgroundThumb(Rect thumb, Color accent, float rimAlpha)
        {
            // Scaled to cover the whole thumbnail (a taller one crops the sides, so no empty strip is left under the picture).
            float sc = Mathf.Max(thumb.width / W, thumb.height / H);
            var org = new V2(thumb.x - (W * sc - thumb.width) / 2f, thumb.y - (H * sc - thumb.height) / 2f);
            UiModelAt(org, sc);
            UiClip(thumb);
            bgViewOrigin = org;  // (BackgroundReact.cs: the pointer is only over this view if it is in the thumbnail)
            bgViewScale = sc;
            bgViewRect = thumb;
            bgThumb = true;
            DrawMenuBackground(Time.time);
            bgThumb = false;
            bgViewOrigin = V2.Zero;
            bgViewScale = 1f;
            bgViewRect = new Rect(0f, 0f, W, H);
            UiNoClip();
            UiModelAt(V2.Zero, 1f);
            var edge = accent.ToRgba(rimAlpha);
            Line(new V2(thumb.x, thumb.y), new V2(thumb.xMax, thumb.y), 1.5f, edge);
            Line(new V2(thumb.x, thumb.yMax), new V2(thumb.xMax, thumb.yMax), 1.5f, edge);
            Line(new V2(thumb.x, thumb.y), new V2(thumb.x, thumb.yMax), 1.5f, edge);
            Line(new V2(thumb.xMax, thumb.y), new V2(thumb.xMax, thumb.yMax), 1.5f, edge);
        }

        // --- Helpers ------------------------------------------------------------------

        /// A quick repeatable "random" number in 0..1 for index i.
        static float Hash(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        /// A quad shaded from `top` (a, b) to `bottom` (c, d).
        void GradQuad(V2 a, V2 b, V2 c, V2 d, Rgba top, Rgba bottom)
        {
            if (glClipOn)  // a clipped thumbnail (WorldRenderer.cs UiClip): cut like Tri, so the picture stays inside its frame
            {
                VTri(a, top, b, top, c, bottom);
                VTri(a, top, c, bottom, d, bottom);
                return;
            }
            SetColor(top); Vtx(Px(a)); Vtx(Px(b));
            SetColor(bottom); Vtx(Px(c));
            SetColor(top); Vtx(Px(a));
            SetColor(bottom); Vtx(Px(c)); Vtx(Px(d));
        }

        /// A soft round glow: c.A at the centre fading smoothly to nothing at radius r. Drawn as bands of shaded
        /// triangles (the colour blends across each), so there are no hard rings. `rings` is unused (it was the old
        /// stacked discs' count); kept so the callers read the same.
        void SoftGlow(V2 at, float r, Rgba c, int rings = 4)
        {
            const int segs = 32, bands = 8;
            for (int b = 0; b < bands; b++)
            {
                float f0 = b / (float)bands, f1 = (b + 1) / (float)bands;
                var c0 = c.WithAlpha(c.A * GlowFalloff(f0));
                var c1 = c.WithAlpha(c.A * GlowFalloff(f1));
                for (int i = 0; i < segs; i++)
                {
                    V2 d0 = V2.FromAngle(MathX.Tau * i / segs), d1 = V2.FromAngle(MathX.Tau * (i + 1) / segs);
                    if (b == 0)
                    {
                        VTri(at + d0 * (r * f1), c1, at + d1 * (r * f1), c1, at, c0);
                        continue;
                    }
                    V2 a0 = at + d0 * (r * f0), a1 = at + d1 * (r * f0), b0 = at + d0 * (r * f1), b1 = at + d1 * (r * f1);
                    VTri(b0, c1, b1, c1, a0, c0);
                    VTri(a0, c0, b1, c1, a1, c0);
                }
            }
        }

        /// 1 at the centre to 0 at the edge, flat at both ends (smoothstep), so the glow has no visible rim or peak.
        static float GlowFalloff(float f)
        {
            float x = 1f - f;
            return x * x * (3f - 2f * x);
        }

        void Stars(int n, float t, float alpha, int seed = 0)
        {
            for (int i = 0; i < n; i++)
            {
                var p = new V2(Hash(seed + i * 3) * W, Hash(seed + i * 3 + 1) * H);
                float tw = 0.5f + 0.5f * Mathf.Sin(t * (1f + Hash(seed + i * 3 + 2) * 2f) + i);
                Circle(p, 0.8f + Hash(i + 7) * 1.2f, new Rgba(0.85f, 0.9f, 1f, alpha * (0.3f + 0.7f * tw)), 6);
            }
        }

    }
}
