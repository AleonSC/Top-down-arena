using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Arena
{
    /// The wiki everywhere (Docs/Wiki.md steps 3 and 4). The Wiki key (I by default, KeyAction.Wiki): a tap opens the wiki, on the main
    /// menu's bay (BayWiki.cs) or anywhere else as a modal screen over the game (solo, the run pauses while it's open; in co-op it
    /// doesn't, and it's only on your screen); the pause and lobby menus have a WIKI row too. Held, it's the eyedropper: on-screen text
    /// under the pointer is outlined in gold (Ui.PickText), and in a run the enemy or player
    /// under it counts too; a tag by the pointer names what a click opens, and a click opens its page. Nothing here is sent: no
    /// protocol change.
    public partial class ArenaApp
    {
        UiScreen screenWiki;
        bool wikiPaused;           // the wiki paused a solo run (it resumes when it closes)
        bool wikiKeyDown;          // the Wiki key went down while nothing was typing
        float wikiKeyAt;           // when
        bool wikiPicked;           // a click picked something while it was held (so letting go doesn't open the list too)
        bool wikiPicking;          // the eyedropper is on (the key held, or PICK pressed)
        bool wikiPickSticky;       // PICK in the wiki: the eyedropper stays on until a click picks something or Esc
        bool wikiPickPaused;       // the wiki had paused a solo run when PICK hid it: it stays paused while you pick
        WikiEntry wikiPickTarget;  // what a click would open (drawn in the tag)
        const float WikiTapLen = 0.3f;  // held shorter than this with no pick: a tap

        void InitWikiScreen()
        {
            if (screenWiki != null)
                return;
            screenWiki = new UiScreen("wiki", DrawWikiScreen)
            {
                OnEsc = () =>
                {
                    if (wikiPage == null)
                        return false;
                    WikiGoBack();
                    return true;
                },
                OnClose = () =>
                {
                    if (wikiPaused && game.Paused && !pauseMenuOpen)
                        game.RequestPause(false);
                    wikiPaused = false;
                    GUIUtility.keyboardControl = 0;
                },
            };
        }

        /// The main menu's machine is what's on screen (not the profile, settings drawer, Who's playing? or a transition).
        bool MenuMachineShown => session.Screen == AppScreen.Menu && !pacOpen && !profTr && !whoOpen && !settingsOpen && !profileOpen;

        /// Opens the wiki (on the bay on the main menu, else the screen), at a page if one is given.
        void OpenWiki(WikiEntry at = null)
        {
            InitWikiScreen();
            if (MenuMachineShown)
            {
                if (!menuWikiOpen)
                    ToggleMenuWiki();
            }
            else if (!screenWiki.IsOpen)
            {
                OpenScreen(screenWiki);
                wikiOpenedAt = Time.unscaledTime;
                GUIUtility.keyboardControl = 0;
                if (session.Screen == AppScreen.Game && game.Players.Count == 1 && !game.Paused
                    && (game.State == MatchState.Fighting || game.State == MatchState.Intermission))
                {
                    game.RequestPause(true);
                    wikiPaused = true;
                }
            }
            if (wikiPickPaused)  // back from PICK: the run it kept paused resumes when the wiki closes, as before
            {
                wikiPaused = game.Paused;
                wikiPickPaused = false;
            }
            if (at != null)
            {
                wikiPage = null;
                wikiBack.Clear();
                WikiOpen(at);
            }
            MenuSound(Sfx.MenuKey);
        }

        bool WikiOpenNow => menuWikiOpen && MenuMachineShown || screenWiki != null && screenWiki.IsOpen;

        /// The Wiki key, each frame before the pointer and blocking (Update): tap to open or close, hold for the eyedropper.
        void UpdateWikiKey()
        {
            InitWikiScreen();
            bool free = GUIUtility.keyboardControl == 0 && !chatOpen;
            if (Keybinds.Down(KeyAction.Wiki) && free && (!ModalScreenOpen || screenWiki.IsOpen))
            {
                wikiKeyDown = true;
                wikiKeyAt = Time.unscaledTime;
                wikiPicked = false;
            }
            bool held = wikiKeyDown && Keybinds.Held(KeyAction.Wiki);
            if (wikiPickSticky && (WikiOpenNow || session.Screen == AppScreen.Game && game.State != MatchState.Fighting && game.State != MatchState.Intermission && !game.Paused))
                EndWikiPick();  // the wiki came back, or the run ended under it
            wikiPicking = (held || wikiPickSticky) && !WikiOpenNow && !ModalScreenOpen;
            if (wikiKeyDown && !held)
            {
                wikiKeyDown = false;
                if (!wikiPicked && Time.unscaledTime - wikiKeyAt < WikiTapLen)
                {
                    if (screenWiki.IsOpen)
                        CloseScreen(screenWiki);
                    else if (menuWikiOpen && MenuMachineShown)
                        ToggleMenuWiki();
                    else
                        OpenWiki();
                }
            }
            if (!wikiPicking)
                wikiPickTarget = null;
        }

        /// PICK in the wiki: hide it (the bay's shutter or the screen) and keep the eyedropper on; a solo run it paused stays paused.
        void StartWikiPick()
        {
            InitWikiScreen();
            wikiPickPaused = wikiPaused;
            wikiPaused = false;  // (so closing the screen doesn't resume the run)
            if (screenWiki.IsOpen)
                CloseScreen(screenWiki);
            else if (menuWikiOpen)
                ToggleMenuWiki();
            wikiPickSticky = true;
            wikiPicked = false;
            MenuSound(Sfx.MenuKey);
        }

        /// PICK ends with nothing picked (Esc, or the run ended): a run it kept paused resumes.
        void EndWikiPick()
        {
            wikiPickSticky = false;
            if (wikiPickPaused && game.Paused && !pauseMenuOpen)
                game.RequestPause(false);
            wikiPickPaused = false;
        }

        /// Esc while PICK is on: cancel it. True if it took the key.
        bool WikiPickEsc()
        {
            if (!wikiPickSticky)
                return false;
            EndWikiPick();
            return true;
        }

        /// Start of OnGUI, while the eyedropper is on: a left click opens what's under the pointer (and goes no further).
        /// Start of OnGUI: note the text under the pointer while the Wiki key is held (Ui.Shown outlines it).
        void PickBeginGui()
        {
            Ui.PickText = wikiPicking;
            Ui.BeginPick();
        }

        /// Shot previews: the end of the pick pass.
        void PickEndGui() => Ui.EndPick();

        void WikiPickBeginGui()
        {
            var ev = Event.current;
            if (ev.type == EventType.Repaint)
                wikiPickTarget = wikiPicking ? WikiUnderPointer() : null;
            if (!wikiPicking || ev.type != EventType.MouseDown || ev.button > 1)
                return;
            var at = WikiUnderPointer();
            if (ev.button == 1)  // a right click: the first status effect on the enemy or player under the pointer
                at = wikiPickStatuses.Count > 0 ? wikiPickStatuses[0] : null;
            ev.Use();  // a click while picking never reaches the buttons under it
            if (at == null)
                return;
            wikiPicked = true;
            wikiPickSticky = false;
            OpenWiki(at);
        }

        readonly List<WikiEntry> wikiPickStatuses = new List<WikiEntry>();  // on the enemy under the pointer

        /// What the pointer is on: outlined text naming an entry first, then (in a run) the nearest enemy, minion, player or shot under
        /// it (a player's shot opens their weapon, an enemy's its shooter when this game knows it), then a prop or chest it's over. The
        /// status effects on an enemy under it go to wikiPickStatuses (the tag lists them; a right click opens the first).
        WikiEntry WikiUnderPointer()
        {
            wikiPickStatuses.Clear();
            if (session.Screen == AppScreen.Game && Ui.Hovered != null)  // a boss bar's phase or attack name: that boss's (WikiPhases.cs, WikiAttacks.cs)
                foreach (var b in game.Enemies.Values)
                    if (b.Hp > 0f && Defs.Get(b.Type).IsBoss)
                    {
                        var ph = Wiki.PhaseIn(Ui.Hovered, b.Type);
                        if (ph != null)
                            return ph;
                    }
            var e = Wiki.FindIn(Ui.Hovered);
            if (e != null || session.Screen != AppScreen.Game)
                return e;
            var m = MouseArena();
            float best = float.MaxValue;
            Enemy picked = null;
            Player pickedPlayer = null;
            foreach (var en in game.Enemies.Values)
            {
                if (en.Hp <= 0f)
                    continue;
                float d = en.Pos.DistanceTo(m), r = Defs.Get(en.Type).Radius + 8f;
                if (d < r && d < best && Wiki.Of(en.Type) != null)
                {
                    best = d;
                    e = Wiki.Of(en.Type);
                    picked = en;
                    pickedPlayer = null;
                }
            }
            foreach (var mn in game.Minions.Values)
            {
                var md = (int)mn.Kind < Kits.Minions.Length ? Kits.Minions[(int)mn.Kind] : null;
                float d = mn.Pos.DistanceTo(m), r = (md != null ? md.Radius : 12f) + 6f;
                if (d < r && d < best && Wiki.Of(mn.Kind) != null)
                {
                    best = d;
                    e = Wiki.Of(mn.Kind);
                    picked = null;
                    pickedPlayer = null;
                }
            }
            foreach (var p in game.Players.Values)
            {
                float d = p.Pos.DistanceTo(m);
                if (d < 22f && d < best && Wiki.Of(p.Kit) != null)
                {
                    best = d;
                    e = Wiki.Of(p.Kit);
                    picked = null;
                    pickedPlayer = p;
                }
            }
            foreach (var b in game.Bullets.Values)
            {
                float d = b.Pos.DistanceTo(m);
                if (d >= b.Radius + 6f || d >= best)
                    continue;
                var by = WikiShotSource(b);
                if (by != null)
                {
                    best = d;
                    e = by;
                    picked = null;
                    pickedPlayer = null;
                }
            }
            if (e == null)
                foreach (var pr in game.Props.Values)
                {
                    if (Mathf.Abs(pr.Pos.X - m.X) > Game.HalfW(pr) + 4f || Mathf.Abs(pr.Pos.Y - m.Y) > Game.HalfH(pr) + 4f)
                        continue;
                    e = Wiki.Of(pr.Kind, pr.Chest);
                    if (e != null)
                        break;
                }
            if (picked != null)
                WikiStatusesOn(picked, wikiPickStatuses);
            else if (pickedPlayer != null)
                WikiStatusesOn(pickedPlayer, wikiPickStatuses);
            return e;
        }

        /// Whose a shot is: a player's opens their weapon; an enemy's, the enemy that fired it when this game knows (the host).
        WikiEntry WikiShotSource(Bullet b)
        {
            if (b.Owner != 0)
                return game.Players.TryGetValue(b.Owner, out var p) && p.Weapon != null ? Wiki.Find(p.Weapon.Name, WikiKind.Weapon) : null;
            return b.Shooter != 0 && game.Enemies.TryGetValue(b.Shooter, out var en) ? Wiki.Of(en.Type) : null;
        }

        /// What's on a player (their replicated timers): stunned, swallowed, taunting, downed, wounded.
        static void WikiStatusesOn(Player p, List<WikiEntry> into)
        {
            WikiStatusIf(p.StunT > 0f, "Stunned", into);
            WikiStatusIf(p.SwallowT > 0f, "Swallowed", into);
            WikiStatusIf(p.TauntT > 0f, "Taunting", into);
            if (p.Downed && Wiki.Find("Downed", WikiKind.Mechanic) != null)
                into.Add(Wiki.Find("Downed", WikiKind.Mechanic));
            if (p.Wound > 0f && Wiki.Find("Wound", WikiKind.Mechanic) != null)
                into.Add(Wiki.Find("Wound", WikiKind.Mechanic));
        }

        static void WikiStatusIf(bool on, string name, List<WikiEntry> into)
        {
            var s = on ? Wiki.Status(name) : null;
            if (s != null)
                into.Add(s);
        }

        /// The status effects an enemy shows (its replicated flags, so every game sees the same).
        static void WikiStatusesOn(Enemy e, List<WikiEntry> into)
        {
            WikiStatusIf(e.Frozen, "Frozen", into);
            WikiStatusIf(e.Caged, "Caged", into);
            WikiStatusIf(e.Dazed, "Dazed", into);
            WikiStatusIf(e.Burning, "Burning", into);
            WikiStatusIf(e.Bleeding || e.BleedStacks > 0, "Bleeding", into);
            WikiStatusIf(e.Poisoned, "Poisoned", into);
            WikiStatusIf(e.Venomed, "Venom", into);
            WikiStatusIf(e.Wet, "Wet", into);
            WikiStatusIf(e.Charged, "Charged", into);
            WikiStatusIf(e.Brittle, "Brittle", into);
            WikiStatusIf(e.Dazzled, "Dazzled", into);
            WikiStatusIf(e.Chilled, "Chilled", into);
            WikiStatusIf(e.Slowed, "Slowed", into);
            WikiStatusIf(e.Marked, "Marked", into);
            WikiStatusIf(e.Suppressed, "Suppressed", into);
        }

        /// The eyedropper's tag by the pointer: WIKI and what a click opens, or a hint.
        void DrawWikiPickTag()
        {
            if (!wikiPicking)
                return;
            bool old = Ui.MenuFont;
            Ui.MenuFont = true;
            var mp = Event.current.mousePosition;
            var t = wikiPickTarget;
            string name = t != null ? t.Name : "POINT AT SOMETHING";
            string kind = t != null ? Wiki.KindName(t.Kind).ToUpperInvariant() + " · CLICK" : wikiPickSticky ? "TEXT, AN ENEMY, A SHOT... · ESC CANCELS" : "TEXT, AN ENEMY, A SHOT, A CRATE...";
            string fx = null;  // the status effects on the enemy or player, the first one a right click away
            if (t != null && wikiPickStatuses.Count > 0)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < wikiPickStatuses.Count; i++)
                    sb.Append(i == 0 ? "" : " · ").Append(wikiPickStatuses[i].Name.ToUpperInvariant());
                fx = sb.Append("  (RIGHT CLICK: ").Append(wikiPickStatuses[0].Name.ToUpperInvariant()).Append(')').ToString();
            }
            float w = Mathf.Max(Mathf.Max(Ui.TextWidth(name, 13, true), Ui.TextWidth(kind, 9)), fx != null ? Ui.TextWidth(fx, 9) : 0f) + 58f;
            var r = new Rect(Mathf.Min(mp.x + 18f, W - w - 6f), Mathf.Min(mp.y + 16f, H - 60f), w, fx != null ? 54f : 40f);
            Ui.Fill(r, new Color(0.02f, 0.035f, 0.08f, 0.94f));
            EdgeRing(r, 1f, (t != null ? CrtStyle.Gold : CrtStyle.Ink).WithAlpha(0.8f));
            Ui.Icon(new Rect(r.x + 9f, r.y + 10f, 20f, 20f), PixelArt.Get("ui_book"), t != null ? CrtStyle.Gold : CrtStyle.Dim);
            Ui.Label(new Rect(r.x + 38f, r.y + 4f, r.width - 42f, 18f), name, 13, t != null ? CrtStyle.Ink : CrtStyle.Dim, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 38f, r.y + 21f, r.width - 42f, 14f), kind, 9, t != null ? KindInk(t.Kind) : CrtStyle.Dim, TextAnchor.MiddleLeft, false);
            if (fx != null)
                Ui.Label(new Rect(r.x + 38f, r.y + 35f, r.width - 42f, 14f), fx, 9, KindInk(WikiKind.Status), TextAnchor.MiddleLeft, false);
            Ui.MenuFont = old;
        }

        /// The wiki as a screen over the game, the lobby or a menu: the room dimmed, a CRT panel with the wiki's screen; a click
        /// outside it closes it.
        void DrawWikiScreen()
        {
            bool old = Ui.MenuFont;
            Ui.MenuFont = true;
            Ui.Fill(new Rect(0f, 0f, W, H), new Color(0.01f, 0.01f, 0.03f, 0.78f));
            var box = new Rect(W / 2f - 300f, H / 2f - 262f, 600f, 524f);
            DkPanel(box);
            DrawBayWiki(new Rect(box.x + 16f, box.y + 16f, box.width - 32f, box.height - 32f), true);
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && !box.Contains(ev.mousePosition))
            {
                ev.Use();
                CloseScreen(screenWiki);
            }
            Ui.MenuFont = old;
        }
    }
}
