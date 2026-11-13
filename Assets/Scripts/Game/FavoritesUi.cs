using UnityEngine;

namespace Arena
{
    /// The lobby's Saved loadouts drawer (Core/Favorites.cs): the SAVED key at the right of the loadout's Role heading
    /// opens it over the right column (the preview and stats stay beside it, so a load shows at once). One card per
    /// saved loadout with LOAD, a pencil to rename it, and delete (click X, then SURE?), SAVE CURRENT LOADOUT (type a name, Enter saves), and
    /// dashed empty slots up to Favorites.Slots.
    public partial class ArenaApp
    {
        bool favOpen;          // the drawer is open (over the loadout's right column)
        bool favNaming;        // typing a name for the current loadout
        string favNameText = "";
        int favSure = -1;      // the card whose X was clicked once (a second click deletes)
        int favRenaming = -1;  // the card whose name is being typed over (its pencil key)
        string favRenameText = "";
        string favNote = "";
        float favNoteUntil;

        /// The SAVED key, at the right end of the loadout's Role heading.
        void DrawFavoritesKey(Rect key, bool calm)
        {
            var prof = session.Game.Profile;
            int n = prof != null ? prof.Favorites.Count : 0;
            if (LobbyKit.Key(key, "SAVED  " + n, false, calm, PixelArt.Get("ui_star"), colorIcon: true, size: 12, minSize: 9))
            {
                favOpen = true;
                favNaming = false;
                favRenaming = -1;
                favSure = -1;
            }
        }

        void DrawFavorites(LobbyEntry me, Rect r, bool calm)
        {
            var prof = session.Game.Profile;
            Ui.Fill(r, new Color(0.02f, 0.035f, 0.07f, 0.97f));  // (covers the picks under it)
            LobbyKit.Panel(r);
            int count = prof != null ? prof.Favorites.Count : 0;
            LobbyKit.Heading(r.x + 16f, r.y + 10f, r.width - 150f, "Saved loadouts", count + " of " + Favorites.Slots, calm);
            if (LobbyKit.Key(new Rect(r.xMax - 126f, r.y + 14f, 110f, 26f), "< LOADOUT", false, calm, size: 13, minSize: 10)
                || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && !favNaming && favRenaming < 0 && favOpen))
            {
                favOpen = false;
                favRenaming = -1;
                favSure = -1;
                if (Event.current.type == EventType.KeyDown) Event.current.Use();
                return;
            }
            float cardH = Mathf.Min(78f, (r.height - 60f) / Favorites.Slots - 8f);
            float y = r.y + 52f;
            for (int i = 0; i < Favorites.Slots; i++)
            {
                var c = new Rect(r.x + 16f, y, r.width - 32f, cardH);
                if (i < count)
                {
                    if (FavCard(c, prof.Favorites[i], i, me, calm))
                        return;  // (deleted: the list changed under the loop)
                }
                else if (i == count)
                    FavSaveSlot(c, me, prof, calm);
                else
                    LobbyKit.Key(c, "EMPTY SLOT", false, calm, size: 13, locked: true);
                y += cardH + 8f;
            }
            if (Time.unscaledTime < favNoteUntil)
                Ui.Label(new Rect(r.x + 16f, r.yMax - 26f, r.width - 32f, 20f), favNote, 12, Ui.Gold, TextAnchor.MiddleLeft, true);
        }

        /// One saved loadout. True when it was deleted.
        bool FavCard(Rect c, FavLoadout f, int index, LobbyEntry me, bool calm)
        {
            var col = ToColor(Defs.Get(f.Role).Color);
            bool on = f.Matches(me);
            bool locked = f.Role == Role.Wildcard && !WildcardOpen;
            LobbyKit.Key(c, "", on, calm, accent: col, enabled: false);
            Ui.Fill(new Rect(c.x, c.y, 3f, c.height), new Color(col.r, col.g, col.b, 0.85f));
            float isz = Mathf.Min(40f, c.height - 20f);
            Ui.Icon(new Rect(c.x + 14f, c.center.y - isz / 2f, isz, isz), PixelArt.Get(f.Role == Role.Wildcard ? "role_dps" : Classes.Get(f.Kit).Icon), Color.white);
            float tx = c.x + 66f, tw = c.width - 66f - 116f;
            float line = c.height / 3.4f;
            FavNameLine(new Rect(tx, c.y + 4f, tw, line + 4f), f, index, calm);
            Ui.Label(new Rect(tx, c.y + 4f + line, tw, line), FavKitText(f), 13, Color.Lerp(col, Color.white, 0.15f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(tx, c.y + 4f + line * 2f, tw, line), LobbyKit.Truncate(FavGearText(f), 11, tw, false), 11, new Color(0.78f, 0.82f, 0.94f), TextAnchor.MiddleLeft);
            var kr = new Rect(c.xMax - 104f, c.center.y - 17f, 92f, 34f);
            if (on)
            {
                LobbyKit.Key(kr, "EQUIPPED", true, calm, PixelArt.Get("ui_check"), size: 12, minSize: 9, accent: Ui.Good, enabled: false);
                return false;
            }
            if (favSure == index)
            {
                if (LobbyKit.Key(kr, "SURE?", true, calm, size: 13, minSize: 9, danger: true))
                {
                    favSure = -1;
                    var prof = session.Game.Profile;
                    favRenaming = -1;
                    if (Favorites.Delete(prof, index))
                        prof.Save();
                    return true;
                }
                return false;
            }
            if (LobbyKit.Key(new Rect(kr.x, kr.y, 62f, kr.height), "LOAD", false, calm, size: 14, minSize: 10, locked: locked))
                LoadFavorite(f);
            if (LobbyKit.Key(new Rect(kr.xMax - 26f, kr.y, 26f, kr.height), "X", false, calm, size: 12, minSize: 9, danger: true))
                favSure = index;
            return false;
        }

        /// The card's name with a pencil key after it; renaming, a text box in its place (Enter or the tick keeps it, Esc cancels).
        void FavNameLine(Rect r, FavLoadout f, int index, bool calm)
        {
            if (favRenaming != index)
            {
                string shown = LobbyKit.Truncate(f.Name, 18, r.width - 30f);
                Ui.Label(r, shown, 18, Color.white, TextAnchor.MiddleLeft, true);
                float nw = Mathf.Min(Ui.TextWidth(shown, 18, true), r.width - 30f);
                if (FavIconKey(new Rect(r.x + nw + 8f, r.center.y - 11f, 22f, 22f), "ui_pencil", Color.white, calm))
                {
                    favRenaming = index;
                    favRenameText = f.Name;
                    favNaming = false;
                    favSure = -1;
                }
                return;
            }
            var box = new Rect(r.x - 4f, r.y, r.width - 26f, r.height);
            Ui.Fill(box, new Color(1f, 1f, 1f, 0.08f));
            GUI.SetNextControlName("fav_rename");
            favRenameText = Ui.BareField(new Rect(box.x + 4f, box.y, box.width - 8f, box.height), favRenameText, Favorites.NameMax, 17);
            if (GUI.GetNameOfFocusedControl() != "fav_rename" && Event.current.type == EventType.Repaint)
                GUI.FocusControl("fav_rename");
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            bool esc = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            if (FavIconKey(new Rect(box.xMax + 4f, r.center.y - 11f, 22f, 22f), "ui_check", Ui.Good, calm) || enter)
            {
                var prof = session.Game.Profile;
                if (Favorites.Rename(prof, index, favRenameText))
                    prof.Save();
                favRenaming = -1;
                GUI.FocusControl(null);
                if (enter) Event.current.Use();
            }
            else if (esc)
            {
                favRenaming = -1;
                GUI.FocusControl(null);
                Event.current.Use();
            }
        }

        /// A small square key with just an icon, centred.
        static bool FavIconKey(Rect k, string icon, Color tint, bool calm)
        {
            bool hit = LobbyKit.Key(k, "", false, calm, size: 11);
            Ui.Icon(new Rect(k.center.x - 7f, k.center.y - 7f, 14f, 14f), PixelArt.Get(icon), new Color(tint.r, tint.g, tint.b, k.Contains(Event.current.mousePosition) ? 1f : 0.75f));
            return hit;
        }

        void FavSaveSlot(Rect c, LobbyEntry me, Profile prof, bool calm)
        {
            if (!favNaming)
            {
                if (LobbyKit.Key(c, "SAVE CURRENT LOADOUT", false, calm, PixelArt.Get("ui_star"), colorIcon: true, size: 15, minSize: 10, sub: FavKitText(me) + "  ·  " + Defs.Weapon(me.Role, me.Kit, me.Weapon).Name))
                {
                    favNaming = true;
                    favNameText = Favorites.DefaultName(me);
                    favSure = -1;
                    favRenaming = -1;
                }
                return;
            }
            // Naming it: a text box with the default name ready, SAVE and cancel.
            LobbyKit.Key(c, "", true, calm, enabled: false);
            Ui.Label(new Rect(c.x + 14f, c.y + 6f, 200f, 16f), "NAME THIS LOADOUT", 10, LobbyKit.HeadCol, TextAnchor.MiddleLeft, true);
            var box = new Rect(c.x + 14f, c.y + 24f, c.width - 150f, c.height - 32f);
            Ui.Fill(box, new Color(1f, 1f, 1f, 0.08f));
            GUI.SetNextControlName("fav_name");
            favNameText = Ui.BareField(new Rect(box.x + 6f, box.y, box.width - 12f, box.height), favNameText, Favorites.NameMax, 17);
            if (GUI.GetNameOfFocusedControl() != "fav_name" && Event.current.type == EventType.Repaint)
                GUI.FocusControl("fav_name");
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            bool esc = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            var kr = new Rect(c.xMax - 126f, c.center.y - 17f, 80f, 34f);
            if (LobbyKit.Key(kr, "SAVE", false, calm, PixelArt.Get("ui_check"), size: 13, minSize: 10, accent: Ui.Good) || enter)
            {
                if (Favorites.Save(prof, favNameText, me))
                {
                    prof.Save();
                    favNote = "Saved \"" + prof.Favorites[prof.Favorites.Count - 1].Name + "\"";
                    favNoteUntil = Time.unscaledTime + 2.5f;
                }
                favNaming = false;
                GUI.FocusControl(null);
                if (enter) Event.current.Use();
            }
            else if (LobbyKit.Key(new Rect(kr.xMax + 6f, kr.y, 34f, 34f), "X", false, calm, size: 12, minSize: 9, danger: true) || esc)
            {
                favNaming = false;
                GUI.FocusControl(null);
                if (esc) Event.current.Use();
            }
        }

        void LoadFavorite(FavLoadout f)
        {
            var prof = session.Game.Profile;
            if (Favorites.Load(session, prof, f))
            {
                prof.Save();
                session.ShareCosmetics();  // (the perks it was saved with: your seat shows them)
            }
            // The lobby's own picks follow, as if each key had been pressed (they seed the next Host / Join).
            hostRole = f.Role;
            joinRole = f.Role;
            if (f.Role != Role.Wildcard) kitChoice = f.Kit;
            weaponChoice = f.Weapon;
            abilityChoice = f.Ability;
            ultChoice = f.Ult;
            wildSecond = Defs.WildSecond(f.Wild);
            wildKey = Defs.WildKey(f.Wild);
            wildBrowseFor = -1;
            detailSel = 0;
            favNote = "Loaded \"" + f.Name + "\"";
            favNoteUntil = Time.unscaledTime + 2.5f;
        }

        static string FavKitText(FavLoadout f) => f.Role == Role.Wildcard ? "Wildcard" : Defs.KitName(f.Kit) + " " + Defs.Get(f.Role).Name;
        static string FavKitText(LobbyEntry e) => e.Role == Role.Wildcard ? "Wildcard" : Defs.KitName(e.Kit) + " " + Defs.Get(e.Role).Name;

        static string FavGearText(FavLoadout f)
        {
            string ab = Upgrades.Ability(Upgrades.RoleAbility(f.Role, f.Kit, f.Ability)).Name;
            string ult = Upgrades.Ability(Upgrades.Ultimate(f.Role, Upgrades.UltKitOf(f.Role, f.Kit, f.Ult), f.Ult)).Name;
            return Defs.Weapon(f.Role, f.Kit, f.Weapon).Name + "  ·  " + ab + "  ·  " + ult;
        }
    }
}
