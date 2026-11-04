using UnityEngine;

namespace Arena
{
    /// Your cosmetics window (Docs/Mockups/customization.html, look C): the side drawer opened by clicking your name chip on the
    /// main menu. A drawer with your rank and a row per category (Frame, Trail, Background, Reward ladder), and the open one
    /// on the right: a live preview of your look, a card showing how others see you, and the list to equip from. What's
    /// here is what Rank.cs unlocks. Equipping saves the profile and tells the lobby (Session.ShareCosmetics).
    public partial class ArenaApp
    {
        bool cosmeticsOpen;
        int cosmeticsSection;  // 0 Frame, 1 Trail, 2 Background, 3 Reward ladder

        static readonly string[] CosmeticSections = { "Frame", "Trail", "Background", "Reward ladder" };
        static readonly string[] CosmeticIcons = { "ui_star", "blink", "snow", "ui_trophy" };

        void OpenCosmetics()
        {
            cosmeticsOpen = true;
            cosmeticsSection = 0;
        }

        void DrawCosmetics() { if (!SkipWhileClosing) DrawCosmeticsWindow(); }

        static CosmeticKind KindOfSection(int s) => s == 0 ? CosmeticKind.Frame : s == 1 ? CosmeticKind.Trail : CosmeticKind.Background;

        void DrawCosmeticsWindow()
        {
            var p = Profile.Current;
            float t = Time.unscaledTime;
            float k = DrawerIn("cosmetics", true);
            var info = Rank.Of(p.Xp);
            var tc = RankLook.TierColor(info.Rank);
            BeginDrawer("YOUR LOOK", Ui.Gold, k, "Cosmetics from your rank");

            // You: the badge, your name and title, the XP towards the next rank.
            RankLook.Draw(new Rect(40f, 130f, 72f, 72f), info.Rank);
            Ui.Label(new Rect(128f, 134f, DrawerW - 160f, 30f), nameText, 22, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(128f, 164f, DrawerW - 160f, 20f), Rank.Title(info.Rank) + "  ·  Rank " + info.Rank, 14, tc, TextAnchor.MiddleLeft, true);
            Ui.Bar(new Rect(42f, 218f, DrawerW - 84f, 9f), info.Fraction, tc);
            Ui.Label(new Rect(42f, 230f, DrawerW - 84f, 18f), info.Into.ToString("N0") + " / " + info.Need.ToString("N0") + " XP", 11, Ui.Muted, TextAnchor.MiddleRight);

            float y = 262f;
            for (int i = 0; i < CosmeticSections.Length; i++)
            {
                bool on = i == cosmeticsSection;
                var r = new Rect(28f, y, DrawerW - 56f, on ? 62f : 52f);
                string line = i == 3 ? "Rank by rank" : EquippedName(p, KindOfSection(i));
                if (DrawerRow(r, PixelArt.Get(CosmeticIcons[i]), CosmeticSections[i], line, null, null, Ui.Gold, on))
                    cosmeticsSection = i;
                y += r.height + 4f;
            }
            if (Ui.FancyButton(new Rect(40f, H - 100f, 220f, 48f), "Back  [Esc]", Ui.Primary, PixelArt.Get("ui_back"), t))
                CloseWindow(() => cosmeticsOpen = false);
            EndDrawer();

            BeginPanelAnim("cosmetics" + cosmeticsSection);
            if (cosmeticsSection == 3)
                DrawLadderPanel(p, k);
            else
                DrawCosmeticPanel(p, t, k);
            EndPanelAnim();
        }

        static string EquippedName(Profile p, CosmeticKind kind)
        {
            var c = Rank.Cosmetic(p.Equipped(kind));
            return c != null ? c.Name : kind == CosmeticKind.Background ? "Your pick" : "None";
        }

        // --- A category: the preview, and what to choose ---------------------------------------------------------

        void DrawCosmeticPanel(Profile p, float t, float k)
        {
            var kind = KindOfSection(cosmeticsSection);
            string[] subs =
            {
                "A rim on your lobby party card and your menu chip.",
                "Drawn behind you in the match, for everyone to see.",
                "Your menu background. Dusk Grid is a rank reward; the others are in Settings > Visual effects.",
            };
            OpenSectionPanel(CosmeticSections[cosmeticsSection].ToUpperInvariant(), Ui.Gold, subs[cosmeticsSection], k);
            float x = DrawerPanel.x + 26f, top = DrawerPanel.y + 112f;
            int rank = p.RankNow;

            // The preview: you, moving, with your trail, background and frame.
            var look = new Rect(x, top, 340f, 200f);
            if (BeginUiGl())
            {
                BackgroundThumb(look, Ui.Gold, 0.4f);
                UiModelAt(new V2(look.center.x + 10f, look.center.y), 1.6f);
                DrawTrailSample(Rank.TrailIndex(p.Trail), 90f, t, 14f);
                DrawGenericPlayer(t);
                EndUiGl();
            }
            Color? frameCol = FrameOf(p);
            if (frameCol.HasValue)
                FrameOutline(new Rect(look.x + 70f, look.y + 36f, look.width - 140f, look.height - 72f), frameCol.Value);
            Ui.Label(new Rect(x, look.yMax + 8f, 340f, 16f), "HOW OTHERS SEE YOU", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            DrawLookCard(new Rect(x, look.yMax + 28f, 340f, 88f), p, t);

            // The list: none, then each cosmetic of this kind, with what it needs.
            float lx = x + 364f;
            Ui.Label(new Rect(lx, top - 4f, 352f, 16f), "CHOOSE", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            var items = Rank.OfKind(kind);
            items.Insert(0, null);  // null: none
            float y = top + 16f;
            foreach (var c in items)
            {
                string id = c == null ? "" : c.Id;
                bool ok = c == null || rank >= c.Rank, on = p.Equipped(kind) == id;
                var rr = new Rect(lx, y, 352f, 58f);
                Ui.Pill(rr, on ? new Color(1f, 0.8f, 0.35f, 0.12f) : new Color(1f, 1f, 1f, ok ? 0.05f : 0.025f));
                RowThumb(new Rect(rr.x + 8f, rr.y + 8f, 64f, 42f), kind, c, p, t, ok);
                string name = c != null ? c.Name : kind == CosmeticKind.Background ? "Your pick" : "None";
                Ui.Label(new Rect(rr.x + 84f, rr.y + 8f, rr.width - 180f, 20f), name, 15, ok ? Color.white : Ui.Muted, TextAnchor.MiddleLeft, true);
                string sub = c == null ? (kind == CosmeticKind.Background ? "what you chose in Settings" : "nothing equipped") : ok ? "unlocked" : "needs rank " + c.Rank;
                Ui.Label(new Rect(rr.x + 84f, rr.y + 30f, rr.width - 180f, 18f), sub, 11, ok ? Ui.Good : Ui.Muted, TextAnchor.MiddleLeft, true);
                if (ok)
                {
                    if (Ui.SmallButton(new Rect(rr.xMax - 88f, rr.y + 17f, 78f, 24f), on ? (c == null ? "In use" : "Equipped") : "Equip", on ? Ui.GoldButton : Ui.Neutral, 12) && !on)
                        EquipCosmetic(p, kind, id);
                }
                else
                    Ui.Icon(new Rect(rr.xMax - 34f, rr.y + 18f, 22f, 22f), PixelArt.Get("ui_lock"), Ui.Muted);
                y += 66f;
            }
        }

        /// Equip (or, with id "", take off) a cosmetic: the profile, the lobby, and for the background the menu itself.
        void EquipCosmetic(Profile p, CosmeticKind kind, string id)
        {
            if (kind == CosmeticKind.Background)
            {
                SetDuskGridEquipped(id == "bg-dusk");  // MenuBackgrounds.cs: it keeps the profile too
                return;
            }
            p.Equip(kind, id);
            p.Save();
            session.ShareCosmetics();
        }

        static Color? FrameOf(Profile p)
        {
            if (!Rank.Owns(p.RankNow, p.Frame))
                return null;
            return RankLook.Tint(Rank.FrameColor(Rank.FrameIndex(p.Frame)));
        }

        /// A rim in four thin lines (soft glow under it), for a frame preview.
        static void FrameOutline(Rect r, Color c)
        {
            Ui.Fill(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, 7f), new Color(c.r, c.g, c.b, 0.1f));
            Ui.Fill(new Rect(r.x - 2f, r.yMax - 5f, r.width + 4f, 7f), new Color(c.r, c.g, c.b, 0.1f));
            Ui.Fill(new Rect(r.x, r.y, r.width, 3f), c);
            Ui.Fill(new Rect(r.x, r.yMax - 3f, r.width, 3f), c);
            Ui.Fill(new Rect(r.x, r.y, 3f, r.height), c);
            Ui.Fill(new Rect(r.xMax - 3f, r.y, 3f, r.height), c);
        }

        /// How your lobby party card looks to others: your figure, name, title and badge in your frame. The role and class
        /// aren't part of your look (they're picked in the lobby).
        void DrawLookCard(Rect r, Profile p, float t)
        {
            int rank = p.RankNow;
            Color? fc = FrameOf(p);
            if (fc.HasValue)
                for (int g = 3; g >= 1; g--)
                    Ui.Pill(new Rect(r.x - 3f * g, r.y - 3f * g, r.width + 6f * g, r.height + 6f * g), new Color(fc.Value.r, fc.Value.g, fc.Value.b, 0.05f * (4 - g) / 3f));
            Ui.Card(r, fc ?? Ui.Accent, fc.HasValue ? 1f : 0.85f);
            var face = new Rect(r.x + 8f, r.y + 8f, 72f, 72f);
            Ui.Pill(face, new Color(0.06f, 0.09f, 0.16f, 0.9f));
            if (BeginUiGl())
            {
                UiModelAt(new V2(face.center.x, face.center.y), 0.95f);
                DrawGenericPlayer(t);
                EndUiGl();
            }
            RankLook.Draw(new Rect(face.xMax - 22f, face.yMax - 22f, 30f, 30f), rank);
            Ui.Label(new Rect(r.x + 96f, r.y + 8f, r.width - 106f, 26f), nameText, 19, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 96f, r.y + 34f, r.width - 106f, 20f), Rank.Title(rank) + "  ·  Rank " + rank, 14, RankLook.TierColor(rank), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 96f, r.y + 56f, r.width - 106f, 20f), "Role and class: picked in the lobby", 11, Ui.Muted, TextAnchor.MiddleLeft);
        }

        /// A small picture of one choice, in a 64 x 42 box: a frame's rim, a trail behind a figure, or a background.
        void RowThumb(Rect r, CosmeticKind kind, CosmeticDef c, Profile p, float t, bool ok)
        {
            if (kind == CosmeticKind.Background)
            {
                if (BeginUiGl())
                {
                    int idx = c != null ? DuskGridIndex : PreviousBackground();
                    int keep = menuBg;
                    menuBg = idx;  // (only for the draw: nothing is saved)
                    BackgroundThumb(r, BackgroundAccents[idx], 0.3f);
                    menuBg = keep;
                    EndUiGl();
                }
            }
            else
            {
                Ui.Pill(r, new Color(0.05f, 0.07f, 0.12f, 0.95f));
                if (kind == CosmeticKind.Frame)
                    FrameOutline(new Rect(r.x + 12f, r.y + 7f, r.width - 24f, r.height - 14f), c != null ? RankLook.Tint(Rank.FrameColor(Rank.FrameIndex(c.Id))) : new Color(0.42f, 0.6f, 1f, 0.8f));
                if (BeginUiGl())
                {
                    UiModelAt(new V2(r.center.x + (kind == CosmeticKind.Trail ? 10f : 0f), r.center.y), kind == CosmeticKind.Trail ? 0.45f : 0.5f);
                    if (kind == CosmeticKind.Trail && c != null)
                        DrawTrailSample(Rank.TrailIndex(c.Id), 56f, t, 6f);
                    DrawGenericPlayer(t);
                    EndUiGl();
                }
            }
            if (!ok)
                Ui.Fill(r, new Color(0.03f, 0.04f, 0.08f, 0.55f));
        }

        // --- The reward ladder --------------------------------------------------------------------------------------

        void DrawLadderPanel(Profile p, float k)
        {
            OpenSectionPanel("REWARD LADDER", Ui.Gold, "What each rank unlocks. Titles come with your tier.", k);
            int rank = p.RankNow;
            var rows = Ladder();  // RankUi.cs
            float x = DrawerPanel.x + 26f, top = DrawerPanel.y + 112f, cw = (DrawerPanel.width - 52f - 14f) / 2f;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                bool got = row.Rank <= rank;
                var rr = new Rect(x + (i % 2) * (cw + 14f), top + (i / 2) * 92f, cw, 80f);
                Ui.Pill(rr, new Color(1f, 1f, 1f, got ? 0.05f : 0.025f));
                RankLook.Draw(new Rect(rr.x + 12f, rr.y + 16f, 48f, 48f), row.Rank, got ? 1f : 0.5f);
                Ui.Label(new Rect(rr.x + 76f, rr.y + 14f, rr.width - 90f, 24f), row.Name, 18, got ? Color.white : Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(rr.x + 76f, rr.y + 40f, rr.width - 90f, 20f), row.Kind + (got ? "  ·  unlocked" : ""), 12, got ? Ui.Good : Ui.Muted, TextAnchor.MiddleLeft, true);
            }
        }

        // --- Drawing helpers ---------------------------------------------------------------------------------------

        /// GL (after UiModelAt, the figure at the origin): a trail flowing off to the left, as RankTrails.cs draws it in the arena.
        void DrawTrailSample(int kind, float len, float t, float amp)
        {
            if (kind == 0)
                return;
            bool gold = Rank.TrailId(kind) == "trail-gold";
            const int n = 18;
            V2 prev = V2.Zero;
            for (int i = 1; i <= n; i++)
            {
                float f = i / (float)n, a = 1f - f;
                var pt = new V2(-f * len, Mathf.Sin(f * 5f + t * 3f) * amp * f);
                if (gold)
                {
                    Line(prev, pt, 3f * a + 0.5f, new Rgba(1f, 0.85f, 0.4f, 0.22f * a));
                    if (i % 2 == 0)
                    {
                        float tw = 0.5f + 0.5f * Mathf.Sin(t * 14f + i * 1.7f);
                        Circle(pt + V2.FromAngle(i * 2.4f) * (4f * f), 1.2f + 1.4f * a * tw, new Rgba(1f, 0.95f, 0.75f, 0.85f * a * tw), 6);
                    }
                }
                else
                {
                    Line(prev, pt, 5f * a + 0.5f, new Rgba(1f, 0.4f, 0.1f, 0.16f * a));
                    float flick = 0.6f + 0.4f * Mathf.Sin(t * 20f + i * 2.3f);
                    Circle(pt + new V2(0f, -10f * f), 1.6f + 2.2f * a * flick, new Rgba(1f, 0.55f + 0.3f * a, 0.15f, 0.75f * a), 7);
                }
                prev = pt;
            }
        }

        /// The menu background you had before Dusk Grid (what "Your pick" goes back to).
        int PreviousBackground()
        {
            int i = menuBg != DuskGridIndex ? menuBg : PlayerPrefs.GetInt("menu_bg_prev", 0);
            return Mathf.Clamp(i, 0, DuskGridIndex - 1);
        }

        /// Equip Dusk Grid (remembering your pick), or go back to your pick.
        void SetDuskGridEquipped(bool on)
        {
            if (on)
            {
                if (menuBg != DuskGridIndex)
                {
                    PlayerPrefs.SetInt("menu_bg_prev", menuBg);
                    PlayerPrefs.Save();
                }
                SetBackground(DuskGridIndex);
            }
            else
                SetBackground(PreviousBackground());
        }
    }
}
