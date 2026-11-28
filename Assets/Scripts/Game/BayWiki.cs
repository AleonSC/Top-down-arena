using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The main menu's Wiki, on the case's bay screen (Docs/Wiki.md): the WIKI key rolls the shutter up onto WIKI_ with a search box and
    /// every entry of Core/Wiki.cs (classes, roles, weapons, abilities, Ultimates, upgrade cards, perks, enemies, bosses). Typing narrows
    /// the list (names first, then text); a row (or Enter, for the first) opens its page: kind, text and every reference, each one a link.
    /// ‹ BACK steps back through the pages opened; Esc does too, then rolls the shutter down. WIKI again rolls it down.
    public partial class ArenaApp
    {
        string wikiQuery = "", wikiShownQuery;
        List<WikiEntry> wikiResults;
        WikiEntry wikiPage;                // the open page, null: the list
        readonly List<WikiEntry> wikiBack = new List<WikiEntry>();  // the pages before it (‹ BACK)
        float wikiScroll, wikiOpenedAt;
        const float WikiRowH = 26f;

        /// WIKI on the case: roll the shutter up onto the wiki, or back down.
        void ToggleMenuWiki()
        {
            SetBay(menuWikiOpen ? BayPage.None : BayPage.Wiki);
            if (menuWikiOpen)
            {
                wikiOpenedAt = Time.unscaledTime;
            }
            GUIUtility.keyboardControl = 0;
        }

        /// Esc on the wiki: back a page, back to the list, or the shutter down. True if it took the key.
        bool MenuWikiEsc()
        {
            if (!menuWikiOpen)
                return false;
            if (wikiPage != null)
                WikiGoBack();
            else
                ToggleMenuWiki();
            return true;
        }

        void WikiOpen(WikiEntry e)
        {
            if (e == null || e == wikiPage)
                return;
            if (wikiPage != null)
                wikiBack.Add(wikiPage);
            wikiPage = e;
            wikiScroll = 0f;
            MenuSound(Sfx.BayTick, 0.5f);
        }

        void WikiGoBack()
        {
            wikiPage = wikiBack.Count > 0 ? wikiBack[wikiBack.Count - 1] : null;
            if (wikiBack.Count > 0)
                wikiBack.RemoveAt(wikiBack.Count - 1);
            wikiScroll = 0f;
            MenuSound(Sfx.BayTick, 0.5f);
        }

        /// The bay's screen while the Wiki is up (scr: the screen, MenuCard). live: it takes clicks and keys.
        void DrawBayWiki(Rect scr, bool live)
        {
            bool calm = fxReduced;
            float now = Time.unscaledTime;
            CrtUi.Begin(scr);
            var e = Event.current;
            if (live && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && wikiPage == null
                && wikiResults != null && wikiResults.Count > 0)
            {
                e.Use();
                WikiOpen(wikiResults[0]);
            }

            // The header: WIKI typing itself in, and how many entries there are.
            const string title = "WIKI";
            int n = calm ? title.Length : Mathf.Min(title.Length, Mathf.FloorToInt((now - wikiOpenedAt) / 0.03f));
            bool cursor = n < title.Length || !calm && Mathf.FloorToInt(now * 2f) % 2 == 0;
            CrtUi.Text(new Rect(scr.x + 18f, scr.y + 12f, 200f, 32f), title.Substring(0, n) + (cursor ? "_" : " "), 22, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
            // PICK: the eyedropper from here (WikiPick.cs): the wiki steps aside, the next click on anything opens its page.
            if (DkKey(new Rect(scr.xMax - 104f, scr.y + 14f, 86f, 26f), "PICK", false, calm, PixelArt.Get("ui_cursor"), size: 11, rawSize: true) && live)
            {
                StartWikiPick();
                CrtUi.End(scr, now, calm);
                return;
            }
            CrtUi.Text(new Rect(scr.xMax - 290f, scr.y + 12f, 176f, 32f), Wiki.All.Count + " ENTRIES", 10, CrtStyle.Dim, TextAnchor.MiddleRight, false, true);
            Ui.Fill(new Rect(scr.x + 12f, scr.y + 50f, scr.width - 24f, 1f), CrtStyle.Ink.WithAlpha(0.25f));

            // The search box: typing goes back to the list.
            var field = new Rect(scr.x + 12f, scr.y + 60f, scr.width - 24f, 32f);
            Ui.Fill(field, CrtStyle.Ink.WithAlpha(0.06f));
            EdgeRing(field, 1f, CrtStyle.Ink.WithAlpha(0.45f));
            var inner = new Rect(field.x + 10f, field.y + 4f, field.width - 20f, 24f);
            if (live)
            {
                var oldTint = GUI.contentColor;
                GUI.contentColor = CrtStyle.Ink;
                GUI.SetNextControlName("wikiSearch");
                string q = Ui.BareField(inner, wikiQuery, 40, 15);
                GUI.contentColor = oldTint;
                if (wikiPage == null && GUIUtility.keyboardControl == 0 && e.type == EventType.Repaint)
                    GUI.FocusControl("wikiSearch");  // type straight away on the list
                if (q != wikiQuery)
                {
                    wikiQuery = q;
                    wikiPage = null;
                    wikiBack.Clear();
                    wikiScroll = 0f;
                }
            }
            else
                CrtUi.Text(inner, wikiQuery, 15, CrtStyle.Ink, TextAnchor.MiddleLeft, false, calm);
            if (wikiQuery.Length == 0)
                CrtUi.Text(inner, "TYPE ANYTHING: A WEAPON, AN ENEMY, A CARD...", 11, CrtStyle.Dim, TextAnchor.MiddleLeft, false, true);
            if (wikiResults == null || wikiShownQuery != wikiQuery)
            {
                wikiResults = Wiki.Search(wikiQuery, int.MaxValue);
                wikiShownQuery = wikiQuery;
            }

            var body = new Rect(scr.x + 12f, field.yMax + 8f, scr.width - 24f, scr.yMax - 30f - field.yMax - 8f);
            if (wikiPage == null)
                DrawWikiList(body, live, calm);
            else
                DrawWikiPage(body, live, calm);

            // The foot: what's on screen and the keys.
            string foot = wikiPage == null ? wikiResults.Count + (wikiResults.Count == 1 ? " RESULT" : " RESULTS") + (wikiResults.Count > 0 ? " · ENTER OPENS" : "")
                : Wiki.KindName(wikiPage.Kind).ToUpperInvariant();
            CrtUi.Text(new Rect(scr.x + 18f, scr.yMax - 26f, scr.width / 2f, 18f), foot, 10, CrtStyle.Dim, TextAnchor.MiddleLeft, false, true);
            CrtUi.Text(new Rect(scr.center.x, scr.yMax - 26f, scr.width / 2f - 18f, 18f), wikiPage == null ? "ESC CLOSES" : "ESC BACK", 10, CrtStyle.Dim, TextAnchor.MiddleRight, false, true);
            CrtUi.End(scr, now, calm);
        }

        /// The results: a row each (name, and its kind on the right), scrolled by the wheel; a click opens the page.
        void DrawWikiList(Rect body, bool live, bool calm)
        {
            float total = wikiResults.Count * WikiRowH;
            wikiScroll = Mathf.Clamp(wikiScroll + WheelOver(body, live) * 12f, 0f, Mathf.Max(0f, total - body.height));
            if (wikiResults.Count == 0)
            {
                CrtUi.Text(new Rect(body.x + 6f, body.y + 10f, body.width - 12f, 20f), "NOTHING BY THAT NAME", 12, CrtStyle.Dim, TextAnchor.MiddleLeft, false, true);
                return;
            }
            var mouse = Event.current.mousePosition;
            GUI.BeginGroup(body);
            int first = Mathf.FloorToInt(wikiScroll / WikiRowH), last = Mathf.Min(wikiResults.Count - 1, Mathf.CeilToInt((wikiScroll + body.height) / WikiRowH));
            for (int i = first; i <= last; i++)
            {
                var it = wikiResults[i];
                var r = new Rect(0f, i * WikiRowH - wikiScroll, body.width - 8f, WikiRowH - 2f);
                bool hover = live && body.Contains(mouse) && new Rect(body.x + r.x, body.y + r.y, r.width, r.height).Contains(mouse);
                Ui.Fill(r, CrtStyle.Ink.WithAlpha(hover ? 0.14f : i % 2 == 0 ? 0.04f : 0f));
                if (hover)
                    Ui.Fill(new Rect(r.x, r.y, 2f, r.height), CrtStyle.Ink);
                CrtUi.Text(new Rect(r.x + 8f, r.y, r.width - 120f, r.height), it.Name, 13, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
                CrtUi.Text(new Rect(r.xMax - 118f, r.y, 110f, r.height), Wiki.KindName(it.Kind).ToUpperInvariant(), 9, KindInk(it.Kind), TextAnchor.MiddleRight, false, true);
                if (live && Ui.HitArea(r))
                    WikiOpen(it);
            }
            GUI.EndGroup();
            DrawWikiScrollBar(body, total);
        }

        /// A page: the name, what it is, its text, and its references as rows of links.
        void DrawWikiPage(Rect body, bool live, bool calm)
        {
            var p = wikiPage;
            var refs = Wiki.References(p);
            float textW = body.width - 24f;
            float textH = Ui.TextHeight(p.Text, 12, textW);
            float refsY = 56f + textH + 14f;
            float total = refsY + 22f + refs.Count * WikiRowH;
            wikiScroll = Mathf.Clamp(wikiScroll + WheelOver(body, live) * 12f, 0f, Mathf.Max(0f, total - body.height));
            var mouse = Event.current.mousePosition;
            GUI.BeginGroup(body);
            float y = -wikiScroll;
            if (DkKey(new Rect(body.width - 84f, y + 2f, 76f, 24f), "‹ BACK", false, calm, size: 11, rawSize: true) && live)
            {
                WikiGoBack();
                GUI.EndGroup();
                return;
            }
            CrtUi.Text(new Rect(4f, y, body.width - 100f, 28f), p.Name, 19, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(4f, y + 28f, body.width - 12f, 16f), p.Sub.ToUpperInvariant(), 10, KindInk(p.Kind), TextAnchor.MiddleLeft, false, true);
            Ui.Fill(new Rect(4f, y + 50f, body.width - 12f, 1f), CrtStyle.Ink.WithAlpha(0.2f));
            CrtUi.Text(new Rect(4f, y + 56f, textW, textH + 4f), p.Text, 12, CrtStyle.Ink.WithAlpha(0.9f), TextAnchor.UpperLeft, false, calm, true);
            CrtUi.Text(new Rect(4f, y + refsY, body.width - 12f, 16f), refs.Count == 0 ? "NO REFERENCES YET" : "REFERENCES · " + refs.Count, 10, CrtStyle.Dim, TextAnchor.MiddleLeft, true, true);
            for (int i = 0; i < refs.Count; i++)
            {
                var it = refs[i];
                var r = new Rect(0f, y + refsY + 22f + i * WikiRowH, body.width - 8f, WikiRowH - 2f);
                if (r.yMax < 0f || r.y > body.height)
                    continue;
                bool hover = live && body.Contains(mouse) && new Rect(body.x + r.x, body.y + r.y, r.width, r.height).Contains(mouse);
                Ui.Fill(r, CrtStyle.Ink.WithAlpha(hover ? 0.14f : 0.04f));
                if (hover)
                    Ui.Fill(new Rect(r.x, r.y, 2f, r.height), CrtStyle.Ink);
                CrtUi.Text(new Rect(r.x + 8f, r.y, r.width - 150f, r.height), it.Name, 12, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
                CrtUi.Text(new Rect(r.xMax - 148f, r.y, 140f, r.height), WikiRefWhy(p, it), 9, KindInk(it.Kind), TextAnchor.MiddleRight, false, true);
                if (live && Ui.HitArea(r))
                    WikiOpen(it);
            }
            GUI.EndGroup();
            DrawWikiScrollBar(body, total);
        }

        /// How a reference touches the page: named in the page's text, naming the page in its own, or linked by the game's data
        /// (a weapon's class, a card's weapons, a boss's faction...), which shows just its kind.
        static string WikiRefWhy(WikiEntry page, WikiEntry it)
        {
            string kind = Wiki.KindName(it.Kind).ToUpperInvariant();
            if (page.Links.Contains(it) || it.Links.Contains(page))
                return kind;
            if (page.Mentions.Contains(it))
                return "NAMED HERE · " + kind;
            return "MENTIONS IT · " + kind;
        }

        /// The kind's colour in the lists: classes and roles gold, cards and perks green, enemies and bosses red, things you carry or
        /// find amber, the run's settings and places cyan, minions steel; gear and mechanics stay dim ink.
        static Color KindInk(WikiKind k)
        {
            switch (k)
            {
                case WikiKind.Class:
                case WikiKind.Role: return CrtStyle.Gold;
                case WikiKind.Card:
                case WikiKind.Perk: return LampGreen;
                case WikiKind.Enemy:
                case WikiKind.Boss:
                case WikiKind.Phase:
                case WikiKind.Attack: return CrtStyle.Danger;
                case WikiKind.Gadget:
                case WikiKind.Relic:
                case WikiKind.Buildable: return LampAmber;
                case WikiKind.Modifier:
                case WikiKind.Path:
                case WikiKind.Mode:
                case WikiKind.Difficulty:
                case WikiKind.Challenge:
                case WikiKind.Layout: return CrtStyle.Head;
                case WikiKind.Minion: return CrtStyle.Steel;
                case WikiKind.Status: return new Color(0.85f, 0.55f, 1f);  // violet: what's on an enemy
                case WikiKind.Prop: return LampAmber;
                default: return CrtStyle.Dim;
            }
        }

        void DrawWikiScrollBar(Rect body, float total)
        {
            if (total <= body.height)
                return;
            float h = Mathf.Max(18f, body.height * body.height / total);
            float y = body.y + (body.height - h) * (wikiScroll / (total - body.height));
            Ui.Fill(new Rect(body.xMax - 3f, body.y, 2f, body.height), CrtStyle.Ink.WithAlpha(0.12f));
            Ui.Fill(new Rect(body.xMax - 3f, y, 2f, h), CrtStyle.Ink.WithAlpha(0.6f));
        }
    }
}
