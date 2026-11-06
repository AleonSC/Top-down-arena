using UnityEngine;

namespace Arena
{
    /// The Look and Rank tabs of your profile (ProfileUi.cs; Docs/Mockups/customization.html, look C, before the merge): a category
    /// row per cosmetic, and the open one on the right: a live preview of your look, a card showing how others see you, and the list
    /// to equip from. What's here is what Rank.cs unlocks. Equipping saves the profile and tells the lobby (Session.ShareCosmetics).
    public partial class ArenaApp
    {
        float ladderScroll = -1f;  // the reward ladder's scroll, in px (-1: it opens at your rank)
        bool editingName;  // the name in your profile is a text field while the pencil is on
        int cosmeticsSection;  // 0 Frame, 1 Trail, 2 Background, 3 Card background, 4 Name effect, 5 Aim line, 6 Cursor (7: the Reward ladder, which has its own panel)

        static readonly string[] CosmeticSections = { "Frame", "Trail", "Background", "Card background", "Name effect", "Aim line", "Cursor", "Reward ladder" };
        static readonly string[] CosmeticIcons = { "ui_frame", "ui_trail", "ui_scene", "ui_card", "ui_name", "ui_aim", "ui_cursor", "ui_trophy" };

        static CosmeticKind KindOfSection(int s) => s == 0 ? CosmeticKind.Frame : s == 1 ? CosmeticKind.Trail : s == 3 ? CosmeticKind.CardBackground : s == 4 ? CosmeticKind.NameEffect : s == 5 ? CosmeticKind.AimLine : s == 6 ? CosmeticKind.Cursor : CosmeticKind.Background;

        static string EquippedName(Profile p, CosmeticKind kind)
        {
            var c = Rank.Cosmetic(p.Equipped(kind));
            return c != null ? c.Name : kind == CosmeticKind.Background ? "Your pick" : kind == CosmeticKind.AimLine || kind == CosmeticKind.Cursor ? "Classic" : "None";
        }


        // --- A Look option: its window (Docs/Mockups/crt-profile.html) ----------------------------------------------
        // On the left "TRY IT ON" (a live preview) and "HOW OTHERS SEE YOU" (your party card); on the right every cosmetic of the kind,
        // each with its picture, name, status and an EQUIP key. Hovering a row tries it on in the preview and the card.

        static readonly string[] LookSubs =
        {
            "A rim on your lobby party card and your menu chip.",
            "Drawn behind you in the match, for everyone to see.",
            "Your menu background. Dusk Grid is a rank reward; the others are in Settings > Visual effects.",
            "The fill of your lobby party card, behind your name. Everyone in the lobby sees it.",
            "How your name looks to everyone: the lobby, the Hangout, and over your head in a match.",
            "The line from your weapon to the crosshair in a match. Only you see it. Classic is the default.",
            "Your crosshair in a match. Only you see it. Classic is the default.",
        };

        int lookScroll;          // the list's first visible row
        int lookScrollFor = -1;  // the section lookScroll belongs to
        bool lookBarDrag;        // the scroll bar is being dragged

        static string NoneName(CosmeticKind kind) => kind == CosmeticKind.Background ? "Your pick" : kind == CosmeticKind.AimLine || kind == CosmeticKind.Cursor ? "Classic" : "None";

        void DrawLookWindow(Profile p, float t, int section)
        {
            var kind = KindOfSection(section);
            OpenSectionPanel(CosmeticSections[section].ToUpperInvariant(), CrtUi.Ink, LookSubs[section], 1f);
            float px = DrawerPanel.x + 28f, top = DrawerPanel.y + 102f;
            int rank = p.RankNow;
            var items = Rank.OfKind(kind);
            items.Insert(0, null);  // null: none
            const int fit = 9;
            const float rowH = 52f, step = 56f;
            int maxScroll = Mathf.Max(0, items.Count - fit);
            float lx = px + 364f, lw = DrawerPanel.xMax - 28f - lx, rw = lw - (maxScroll > 0 ? 14f : 0f);
            var view = new Rect(lx, top + 22f, lw, fit * step - 4f);
            var ev = Event.current;
            var m = ev.mousePosition;
            string equipped = p.Equipped(kind);

            // The list's scroll: the equipped row is in view when the section opens; wheel and bar.
            if (lookScrollFor != section)
            {
                lookScrollFor = section;
                int eq = 0;
                for (int i = 0; i < items.Count; i++)
                    if (equipped == (items[i] == null ? "" : items[i].Id))
                        eq = i;
                lookScroll = Mathf.Clamp(eq - fit + 1, 0, maxScroll);
            }
            var track = new Rect(view.xMax - 8f, view.y, 8f, view.height);
            float thumbH = Mathf.Max(30f, track.height * fit / Mathf.Max(1, items.Count));
            if (GUI.enabled && maxScroll > 0)
            {
                if (ev.type == EventType.ScrollWheel && new Rect(px, top, lx + lw - px, view.yMax - top).Contains(m))
                {
                    lookScroll += ev.delta.y > 0f ? 1 : -1;
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0 && new Rect(track.x - 8f, track.y, track.width + 8f, track.height).Contains(m))
                {
                    lookBarDrag = true;
                    ev.Use();
                }
                if (lookBarDrag && (ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag))
                {
                    lookScroll = Mathf.RoundToInt(Mathf.Clamp01((m.y - track.y - thumbH / 2f) / (track.height - thumbH)) * maxScroll);
                    if (ev.type == EventType.MouseDrag)
                        ev.Use();
                }
            }
            if (ev.type == EventType.MouseUp)
                lookBarDrag = false;
            lookScroll = Mathf.Clamp(lookScroll, 0, maxScroll);

            // The row under the mouse is tried on.
            int tryIdx = -1;
            string tryId = null;
            if (GUI.enabled && !lookBarDrag && m.x >= view.x && m.x < view.x + rw && m.y >= view.y && m.y < view.yMax)
            {
                int i = lookScroll + (int)((m.y - view.y) / step);
                if (i < items.Count && (m.y - view.y) - (i - lookScroll) * step < rowH)
                {
                    tryIdx = i;
                    tryId = items[i] == null ? "" : items[i].Id;
                }
            }

            // Left: the preview.
            var tryKind = tryId != null ? kind : (CosmeticKind?)null;
            Ui.Label(new Rect(px, top, 340f, 18f), "TRY IT ON", 12, CabSoft, TextAnchor.MiddleLeft, true);
            if (tryIdx >= 0)
            {
                var tc = items[tryIdx];
                Ui.Label(new Rect(px + 80f, top, 260f, 18f), (tc == null ? NoneName(kind) : tc.Name) + (tc != null && rank < tc.Rank ? "  (rank " + tc.Rank + ")" : ""), 12, Ui.Gold, TextAnchor.MiddleRight, true);
            }
            var pv = new Rect(px, top + 22f, 340f, 250f);
            Ui.Fill(pv, new Color(0.04f, 0.02f, 0.07f, 1f));
            int keepBg = menuBg;
            if (kind == CosmeticKind.Background && tryId != null)
                menuBg = tryId == "" ? PreviousBackground() : DuskGridIndex;  // (only for the draw: nothing is saved)
            int trail = Rank.TrailIndex(tryKind == CosmeticKind.Trail ? tryId : p.Trail);
            int aim = tryKind == CosmeticKind.AimLine ? Rank.AimLineIndex(tryId) : AimLook();
            int cursor = tryKind == CosmeticKind.Cursor ? Rank.CursorIndex(tryId) : CursorLook();
            int fx = tryKind == CosmeticKind.NameEffect ? Rank.NameFxIndex(tryId) : Rank.Owns(rank, p.NameFx) ? Rank.NameFxIndex(p.NameFx) : 0;
            int cardBg = tryKind == CosmeticKind.CardBackground ? Rank.CardBgIndex(tryId) : Rank.Owns(rank, p.CardBg) ? Rank.CardBgIndex(p.CardBg) : 0;
            if (kind == CosmeticKind.CardBackground)
                DrawCardBackground(new Rect(pv.x + 10f, pv.center.y - 62f, pv.width - 20f, 124f), cardBg, t);  // CardBackgrounds.cs
            if (BeginUiGl())
            {
                if (kind != CosmeticKind.CardBackground)
                    BackgroundThumb(pv, CrtUi.Ink, 0.4f);
                UiClip(pv);  // (what is drawn stays on the glass; EndUiGl lifts the clip)
                if (kind == CosmeticKind.CardBackground)
                    UiModelAt(new V2(pv.x + 70f, pv.center.y), 1.4f);
                else if (kind != CosmeticKind.NameEffect)
                    UiModelAt(new V2(pv.center.x + 10f, pv.center.y), 1.6f);
                if (kind != CosmeticKind.NameEffect)
                {
                    if (kind != CosmeticKind.CardBackground)
                        DrawTrailSample(trail, 90f, t, 14f);
                    if (kind == CosmeticKind.AimLine)
                        DrawAimLook(aim, new V2(18f, 0f), new V2(1f, 0f), 100f, t, AimSampleColor, true);  // AimLineLooks.cs
                    DrawGenericPlayer(t);
                }
                if (kind == CosmeticKind.Cursor)
                    DrawCursorSample(cursor, new V2(pv.center.x + 120f, pv.center.y), t);  // CursorLooks.cs
                EndUiGl();
            }
            menuBg = keepBg;
            if (kind == CosmeticKind.NameEffect || kind == CosmeticKind.CardBackground)
            {
                int size = kind == CosmeticKind.NameEffect ? 44 : 26;
                float nx = kind == CosmeticKind.NameEffect ? pv.x + 10f : pv.x + 120f, nw = kind == CosmeticKind.NameEffect ? pv.width - 20f : pv.width - 140f;
                while (size > 18 && Ui.TextWidth(nameText, size, true) + 8f > nw)
                    size--;
                var tcol = RankLook.TierColor(rank);
                DrawName(new Rect(nx, pv.center.y - 30f, nw, 56f), nameText, size, fx, t, Color.white, kind == CosmeticKind.NameEffect ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);  // NameEffects.cs
                Ui.Label(new Rect(nx, pv.center.y + 24f, nw, 22f), Rank.Title(rank) + "  ·  Rank " + rank, 14, tcol, kind == CosmeticKind.NameEffect ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, true);
            }
            if (kind == CosmeticKind.Frame)
            {
                int fi = tryKind == CosmeticKind.Frame ? Rank.FrameIndex(tryId) : Rank.Owns(rank, p.Frame) ? Rank.FrameIndex(p.Frame) : 0;
                if (fi > 0)
                    FrameOutline(pv, FrameTint(fi, t));
                else
                    CrtKit.Frame(pv, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.5f));
            }
            else
                CrtKit.Frame(pv, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.5f));
            Ui.Label(new Rect(px, pv.yMax + 10f, 340f, 18f), "HOW OTHERS SEE YOU", 12, CabSoft, TextAnchor.MiddleLeft, true);
            DrawLookCard(new Rect(px, pv.yMax + 32f, 340f, 88f), p, t, tryKind, tryId);
            Ui.Label(new Rect(px, pv.yMax + 132f, 340f, 36f), "Hover a row to try it on. Press Equip to keep it.", 13, CabSoft, TextAnchor.UpperLeft, false, true);

            // Right: what to choose.
            Ui.Label(new Rect(lx, top, 200f, 18f), "CHOOSE", 12, CabSoft, TextAnchor.MiddleLeft, true);
            var ink = CrtUi.Ink;
            for (int i = lookScroll; i < Mathf.Min(items.Count, lookScroll + fit); i++)
            {
                var c = items[i];
                string id = c == null ? "" : c.Id;
                bool ok = c == null || rank >= c.Rank, on = equipped == id;
                var rr = new Rect(lx, view.y + (i - lookScroll) * step, rw, rowH);
                Ui.Fill(rr, on ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.08f) : new Color(ink.r, ink.g, ink.b, i == tryIdx ? 0.12f : 0.04f));
                CrtKit.Frame(rr, 1f, on ? Ui.Gold : new Color(ink.r, ink.g, ink.b, 0.25f));
                var th = new Rect(rr.x + 8f, rr.y + (rowH - 40f) / 2f, 64f, 40f);
                RowThumb(th, kind, c, p, t, ok);
                CrtKit.Frame(th, 1f, new Color(ink.r, ink.g, ink.b, ok ? 0.5f : 0.25f));
                string name = c != null ? c.Name : NoneName(kind);
                float nx = rr.x + 82f, nw = rr.width - 82f - 106f;
                int size;
                bool wrap;
                name = FitCosmeticName(name, nw, 15, out size, out wrap);
                string shown = WinText("win.n" + section + "." + id, name);
                if (wrap)
                    CrtUi.Text(new Rect(nx, rr.y + 3f, nw, 32f), shown, 14, ok ? CrtUi.Ink : CrtUi.Dim, TextAnchor.UpperLeft, true, CabCalm, true);
                else
                    CrtUi.Text(new Rect(nx, rr.y + 5f, nw, 24f), shown, size, ok ? CrtUi.Ink : CrtUi.Dim, TextAnchor.MiddleLeft, true, CabCalm);
                string sub = c == null ? (kind == CosmeticKind.Background ? "default: what you chose in Settings" : "default") : ok ? "unlocked" : "locked: rank " + c.Rank;
                Ui.Label(new Rect(nx, rr.y + 31f, nw, 18f), sub, 13, c != null && ok ? Ui.Good : CrtUi.Dim, TextAnchor.MiddleLeft);
                var kr = new Rect(rr.xMax - 8f - 92f, rr.y + (rowH - 32f) / 2f, 92f, 32f);
                if (!ok)
                {
                    CrtKit.Dashed(kr, 2f, new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.8f));
                    Ui.Icon(new Rect(kr.x + 7f, kr.center.y - 8f, 16f, 16f), PixelArt.Get("ui_lock"), CrtUi.Dim);
                    Ui.Label(new Rect(kr.x + 26f, kr.y, kr.width - 28f, kr.height), "RANK " + c.Rank, 13, CrtUi.Dim, TextAnchor.MiddleLeft, true);
                }
                else if (on)
                {
                    Ui.Fill(kr, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.95f));
                    Ui.Label(kr, "EQUIPPED", 14, new Color(0.1f, 0.07f, 0.01f), TextAnchor.MiddleCenter, true);
                }
                else if (WinKey(kr, "EQUIP", false, 16))
                    EquipCosmetic(p, kind, id);
            }
            if (maxScroll > 0)
            {
                Ui.Fill(track, new Color(ink.r, ink.g, ink.b, 0.1f));
                float ty = track.y + (track.height - thumbH) * lookScroll / maxScroll;
                Ui.Fill(new Rect(track.x, ty, track.width, thumbH), new Color(ink.r, ink.g, ink.b, lookBarDrag ? 0.95f : 0.6f));
                Ui.Label(new Rect(view.x, view.yMax + 3f, view.width, 14f), "WHEEL OR DRAG TO SCROLL  ·  " + (lookScroll + 1) + " TO " + Mathf.Min(items.Count, lookScroll + fit) + " OF " + items.Count, 10, CrtUi.Dim, TextAnchor.MiddleCenter, true);
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
        void DrawLookCard(Rect r, Profile p, float t, CosmeticKind? tryKind = null, string tryId = null)
        {
            // (tryKind, tryId: a cosmetic being tried on, locked or not; it stands in for what is equipped)
            int rank = p.RankNow;
            int bg = tryKind == CosmeticKind.CardBackground ? Rank.CardBgIndex(tryId) : Rank.Owns(rank, p.CardBg) ? Rank.CardBgIndex(p.CardBg) : 0;
            int frame = tryKind == CosmeticKind.Frame ? Rank.FrameIndex(tryId) : Rank.Owns(rank, p.Frame) ? Rank.FrameIndex(p.Frame) : 0;
            int fx = tryKind == CosmeticKind.NameEffect ? Rank.NameFxIndex(tryId) : Rank.Owns(rank, p.NameFx) ? Rank.NameFxIndex(p.NameFx) : 0;
            if (frame > 0)
                DrawFrameCard(r, frame, t, true, bg);  // FrameLooks.cs
            else
            {
                Ui.Card(r, Ui.Accent, 0.85f);
                DrawCardBackground(r, bg, t);  // CardBackgrounds.cs
            }
            var face = new Rect(r.x + 8f, r.y + 8f, 72f, 72f);
            Ui.Pill(face, new Color(0.06f, 0.09f, 0.16f, 0.9f));
            if (BeginUiGl())
            {
                UiModelAt(new V2(face.center.x, face.center.y), 0.95f);
                DrawGenericPlayer(t);
                EndUiGl();
            }
            RankLook.Draw(new Rect(face.xMax - 22f, face.yMax - 22f, 30f, 30f), rank);
            DrawName(new Rect(r.x + 96f, r.y + 8f, r.width - 106f, 26f), nameText, 19, fx, t, Color.white);  // NameEffects.cs
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
            else if (kind == CosmeticKind.NameEffect)
            {
                Ui.Pill(r, new Color(0.05f, 0.07f, 0.12f, 0.95f));
                DrawName(new Rect(r.x, r.y, r.width, r.height), "Name", 14, c != null ? Rank.NameFxIndex(c.Id) : 0, t, Color.white, TextAnchor.MiddleCenter);
            }
            else if (kind == CosmeticKind.CardBackground)
            {
                Ui.Pill(r, new Color(0.05f, 0.07f, 0.12f, 0.95f));
                if (c != null)
                    DrawCardBackground(new Rect(r.x - CardBgInset, r.y - CardBgInset, r.width + 2f * CardBgInset, r.height + 2f * CardBgInset), Rank.CardBgIndex(c.Id), t);
            }
            else if (kind == CosmeticKind.AimLine)
            {
                Ui.Pill(r, new Color(0.05f, 0.07f, 0.12f, 0.95f));
                if (BeginUiGl())
                {
                    UiModelAt(new V2(r.x + 12f, r.center.y), 0.5f);
                    DrawAimLook(c != null ? Rank.AimLineIndex(c.Id) : 0, new V2(12f, 0f), new V2(1f, 0f), 92f, t, AimSampleColor, true);
                    DrawGenericPlayer(t);
                    EndUiGl();
                }
            }
            else if (kind == CosmeticKind.Cursor)
            {
                Ui.Pill(r, new Color(0.05f, 0.07f, 0.12f, 0.95f));
                if (BeginUiGl())
                {
                    DrawCursorSample(c != null ? Rank.CursorIndex(c.Id) : 0, new V2(r.center.x, r.center.y), t);
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
        // Docs/Mockups/reward-ladder-next.html, look 1: the next reward pinned in a box on the left, the ladder as a vertical
        // rail on the right (rank shields on a line, rewards beside them, a gold "you" marker at your rank).

        /// One thing on the rail: a tier banner, a rank's node (its rewards), or the "you are here" marker.
        class LadderItem
        {
            public int Kind;  // 0 tier banner, 1 node, 2 you
            public int Rank, Tier;
            public int First, Count;  // for a node: its rows in Ladder()
            public float Y, H;
        }

        const float LadderRowH = 52f, LadderRowGap = 6f;

        static Color LadderKindColor(string kind)
        {
            switch (kind)
            {
                case "Frame": return new Color(1f, 0.82f, 0.31f);
                case "Trail": return new Color(0.44f, 0.85f, 1f);
                case "Background": return new Color(0.5f, 0.7f, 1f);
                case "Card background": return new Color(0.31f, 0.88f, 0.75f);
                case "Emote": return new Color(1f, 0.56f, 0.75f);
                case "Aim line": return new Color(1f, 0.48f, 0.36f);
                case "Name effect": return new Color(0.73f, 0.54f, 1f);
                default: return new Color(0.96f, 0.97f, 1f);  // Title
            }
        }

        static string LadderKindIcon(string kind)
        {
            switch (kind)
            {
                case "Frame": return "ui_frame";
                case "Trail": return "ui_trail";
                case "Background": return "ui_scene";
                case "Card background": return "ui_card";
                case "Emote": return "ui_emote";
                case "Aim line": return "ui_aim";
                case "Cursor": return "ui_cursor";
                case "Name effect": return "ui_name";
                default: return "ui_trophy";
            }
        }

        static int LadderTierOf(int rank)
        {
            int[] at = { 0, 5, 10, 20, 30 };
            int t = 0;
            for (int i = 0; i < at.Length; i++)
                if (at[i] <= rank) t = i;
            return t;
        }

        void DrawLadderPanel(Profile p, float k)
        {
            OpenSectionPanel("REWARD LADDER", Ui.Gold, "What each rank unlocks. Titles come with your tier.", k);
            int rank = p.RankNow;
            var rows = Ladder();  // RankUi.cs
            LadderRow next = null;
            foreach (var r in rows)
                if (r.Rank > rank) { next = r; break; }
            float t = Time.unscaledTime;
            float top = DrawerPanel.y + 112f, viewH = DrawerPanel.height - 128f;

            // Left: the next reward.
            var box = new Rect(DrawerPanel.x + 26f, top, 280f, Mathf.Min(viewH, 360f));
            Ui.Fill(box, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.05f));
            CrtKit.Frame(box, 1f, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.45f));
            if (next == null)
            {
                Ui.Label(new Rect(box.x + 16f, box.y + 16f, box.width - 32f, 18f), "ALL DONE", 11, Ui.Gold, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(box.x + 16f, box.y + 50f, box.width - 32f, 60f), "You've earned every reward.", 17, Color.white, TextAnchor.MiddleCenter, true, true);
            }
            else
            {
                var kc = LadderKindColor(next.Kind);
                Ui.Label(new Rect(box.x + 16f, box.y + 14f, box.width - 32f, 18f), "NEXT REWARD  ·  RANK " + next.Rank, 11, Ui.Gold, TextAnchor.MiddleCenter, true);
                DrawRewardPreview(new Rect(box.x + 16f, box.y + 40f, box.width - 32f, 120f), next, t, false);
                Ui.Fancy(new Rect(box.x + 12f, box.y + 170f, box.width - 24f, 34f), next.Name, 22, Color.white, Ui.Gold, TextAnchor.MiddleCenter);
                Ui.Icon(new Rect(box.center.x - Ui.TextWidth(next.Kind.ToUpper(), 11, true) / 2f - 20f, box.y + 210f, 14f, 14f), PixelArt.Get(LadderKindIcon(next.Kind)), kc);
                Ui.Label(new Rect(box.x + 12f, box.y + 208f, box.width - 24f, 18f), next.Kind.ToUpper(), 11, kc, TextAnchor.MiddleCenter, true);
                long have = p.Xp, from = Rank.XpAt(rank), to = Rank.XpAt(next.Rank);
                float frac = to > from ? Mathf.Clamp01((float)(have - from) / (to - from)) : 1f;
                BlockBar(new Rect(box.x + 16f, box.y + 242f, box.width - 32f, 12f), frac, Ui.Gold, 24);
                Ui.Label(new Rect(box.x + 12f, box.y + 258f, box.width - 24f, 18f), Mathf.Max(0L, to - have).ToString("N0") + " XP to go", 12, Ui.Muted, TextAnchor.MiddleCenter);
            }

            // Right: the rail, built as a list so it can be measured, scrolled and drawn from one place.
            float rx = box.xMax + 18f, rw = DrawerPanel.xMax - 22f - rx;
            var items = new System.Collections.Generic.List<LadderItem>();
            float y = 0f, youY = 0f;
            int lastTier = -1;
            bool youDone = false;
            for (int i = 0; i < rows.Count;)
            {
                int j = i;
                while (j < rows.Count && rows[j].Rank == rows[i].Rank) j++;
                int rk = rows[i].Rank, tier = LadderTierOf(rk);
                if (tier != lastTier)
                {
                    items.Add(new LadderItem { Kind = 0, Rank = rk, Tier = tier, Y = y, H = 34f });
                    y += 34f;
                    lastTier = tier;
                }
                if (!youDone && rk > rank)
                {
                    items.Add(new LadderItem { Kind = 2, Rank = rank, Y = y, H = 46f });
                    youY = y;
                    y += 46f;
                    youDone = true;
                }
                float h = 8f + (j - i) * (LadderRowH + LadderRowGap);
                items.Add(new LadderItem { Kind = 1, Rank = rk, First = i, Count = j - i, Y = y, H = h });
                y += h;
                i = j;
            }
            if (!youDone)
            {
                items.Add(new LadderItem { Kind = 2, Rank = rank, Y = y, H = 46f });
                youY = y;
                y += 46f;
            }
            float contentH = y + 8f;

            var view = new Rect(rx, top, rw, viewH);
            var ev = Event.current;
            if (ladderScroll < 0f)
                ladderScroll = youY - viewH * 0.4f;  // first look: your place on the ladder
            if (ev.type == EventType.ScrollWheel && view.Contains(ev.mousePosition))
            {
                ladderScroll += ev.delta.y * 30f;
                ev.Use();
            }
            ladderScroll = Mathf.Clamp(ladderScroll, 0f, Mathf.Max(0f, contentH - viewH));
            float cx = 26f;  // the shields' centre line, in the group
            GUI.BeginGroup(view);
            foreach (var it in items)
            {
                float iy = it.Y - ladderScroll;
                if (iy + it.H < 0f || iy > viewH)
                    continue;
                if (it.Kind == 0)
                {
                    var tc = RankLook.TierColor(it.Rank);
                    int[] at = { 0, 5, 10, 20, 30 };
                    string range = it.Tier + 1 < at.Length ? "ranks " + Mathf.Max(1, at[it.Tier]) + " to " + (at[it.Tier + 1] - 1) : "rank " + at[it.Tier] + " and up";
                    string head = (Rank.TierNames[it.Tier] + " tier").ToUpper() + "  ·  " + range;
                    Ui.Label(new Rect(cx + 40f, iy + 8f, rw - cx - 40f, 22f), head, 12, tc, TextAnchor.MiddleLeft, true);
                    float lx = cx + 40f + Ui.TextWidth(head, 12, true) + 12f;
                    for (int s = 0; s < 20 && lx + s * 14f < rw - 8f; s++)
                        Ui.Fill(new Rect(lx + s * 14f, iy + 19f, 14f, 1f), new Color(tc.r, tc.g, tc.b, 0.5f * (1f - s / 20f)));
                }
                else if (it.Kind == 2)
                {
                    Ui.Fill(new Rect(cx - 1.5f, iy, 3f, it.H), new Color(0.14f, 0.17f, 0.28f));
                    Ui.Capsule(new Rect(cx - 12f, iy + it.H / 2f - 12f, 24f, 24f), new Color(1f, 0.82f, 0.31f, 0.22f));
                    Ui.Capsule(new Rect(cx - 7f, iy + it.H / 2f - 7f, 14f, 14f), Ui.Gold);
                    long have = p.Xp;
                    LadderRow nx = null;
                    foreach (var r in rows) if (r.Rank > rank) { nx = r; break; }
                    long from = Rank.XpAt(rank), to = nx != null ? Rank.XpAt(nx.Rank) : from + 1;
                    Ui.Label(new Rect(cx + 40f, iy + 4f, 260f, 16f), "YOU  ·  RANK " + rank, 12, Ui.Gold, TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(rw - 270f, iy + 4f, 262f, 16f), nx != null ? Mathf.Max(0L, to - have).ToString("N0") + " XP to rank " + nx.Rank : "Every reward unlocked", 12, Ui.Text, TextAnchor.MiddleRight);
                    BlockBar(new Rect(cx + 40f, iy + 24f, rw - cx - 48f, 10f), nx != null ? Mathf.Clamp01((float)(have - from) / (to - from)) : 1f, Ui.Gold, 32);
                }
                else
                {
                    bool got = it.Rank <= rank;
                    var tc = RankLook.TierColor(it.Rank);
                    Ui.Fill(new Rect(cx - 1.5f, iy, 3f, it.H), got ? new Color(tc.r, tc.g, tc.b, 0.8f) : new Color(0.14f, 0.17f, 0.28f));
                    RankLook.Draw(new Rect(cx - 20f, iy + 8f, 40f, 40f), it.Rank, got ? 1f : 0.45f);
                    for (int n = 0; n < it.Count; n++)
                    {
                        var row = rows[it.First + n];
                        var rr = new Rect(cx + 40f, iy + 8f + n * (LadderRowH + LadderRowGap), rw - cx - 48f, LadderRowH);
                        bool isNext = row == next;
                        var kc = LadderKindColor(row.Kind);
                        Ui.Fill(rr, isNext ? new Color(1f, 0.82f, 0.31f, 0.09f) : new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, got ? 0.05f : 0.025f));
                        if (isNext)
                            CrtKit.Frame(rr, 1f, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.7f));
                        Ui.Icon(new Rect(rr.x + 12f, rr.center.y - 11f, 22f, 22f), PixelArt.Get(LadderKindIcon(row.Kind)), got ? kc : new Color(kc.r, kc.g, kc.b, 0.4f));
                        // Two lines (Docs/Mockups/reward-ladder-text.html, A): the name gets the width, the kind sits under it.
                        float nameW = rr.width - 46f - 100f;
                        Ui.Label(new Rect(rr.x + 46f, rr.y + 5f, nameW, 24f), row.Name, 16, got ? Color.white : Ui.Muted, TextAnchor.MiddleLeft, true);
                        Ui.Label(new Rect(rr.x + 46f, rr.y + 28f, nameW, 16f), row.Kind.ToUpper(), 10, got ? kc : new Color(kc.r, kc.g, kc.b, 0.6f), TextAnchor.MiddleLeft, true);
                        Ui.Label(new Rect(rr.xMax - 100f, rr.y, 88f, rr.height), got ? "Unlocked" : isNext ? "Next" : "Locked", 12, got ? Ui.Good : isNext ? Ui.Gold : Ui.Muted, TextAnchor.MiddleRight, true);
                    }
                }
            }
            GUI.EndGroup();
            if (contentH > viewH)
                Ui.Label(new Rect(view.x, view.yMax + 2f, view.width, 14f), "SCROLL FOR MORE", 9, Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        // --- Drawing helpers ---------------------------------------------------------------------------------------

        /// GL (after UiModelAt, the figure at the origin): a trail flowing off to the left, as RankTrails.cs draws it in the arena.
        void DrawTrailSample(int kind, float len, float t, float amp)
        {
            if (kind == 0)
                return;
            string id = Rank.TrailId(kind);
            if (id == CycleId)
            {
                DrawCycleSample(len, amp);  // LightCycleTrail.cs
                return;
            }
            const int n = 18;
            V2 prev = V2.Zero;
            for (int i = 1; i <= n; i++)
            {
                float f = i / (float)n;
                var pt = new V2(-f * len, Mathf.Sin(f * 5f + t * 3f) * amp * f);
                TrailPiece(id, pt, prev, i, f, t, V2.Zero);
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
