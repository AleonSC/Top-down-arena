using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Field guide's in-game databases, opened from the pause menu (ReferencePages.cs): the Bestiary (every enemy and
    /// boss: live model, stats from Defs, health at your wave, attacks and tips from BestiaryData) and the Armory
    /// (classes, weapons, abilities and upgrade cards, read straight from the game's definitions). A searchable list on
    /// the left, the selected entry on the right; names in an entry link to their own entries.
    public partial class ArenaApp
    {
        // Pages: the Bestiary, then the Armory's four tabs.
        const int GuideBestiary = 0, GuideClasses = 1, GuideWeapons = 2, GuideAbilities = 3, GuideUpgrades = 4, GuidePages = 5;
        static readonly string[] GuideTabNames = { "Bestiary", "Classes", "Weapons", "Abilities", "Upgrades" };
        static readonly Color BestiaryColor = new Color(1f, 0.45f, 0.5f), ArmoryColor = new Color(0.45f, 0.75f, 1f);

        bool fieldGuideOpen;
        int guidePage;
        int guideArmoryPage = GuideClasses;     // the Armory tab to come back to
        string guideSearch = "";
        readonly string[] guideSel = new string[GuidePages];
        readonly float[] guideListScroll = new float[GuidePages];
        float guideDetailScroll, guideDetailHeight;
        bool guideScrollToSel;                  // bring the selected row into view (after a jump or the arrow keys)
        List<GuideItem>[] guideItems;

        /// One row of a Field guide list.
        class GuideItem
        {
            public string Key, Name, Sub, Group, Search;
            public Color Accent;
            public Texture2D Icon;              // abilities and upgrades: their pixel-art icon
            public bool HasEnemy;               // the Bestiary: a live enemy
            public EnemyType Enemy;
            public bool HasPlayer;              // classes and weapons: a live character
            public Role Role;
            public Kit Kit;
            public int Weapon;
            public BestiaryEntry Beast;
            public AbilityDef Ability;
            public int Upgrade = -1;            // index in Upgrades.All
        }

        void OpenFieldGuide(bool armory)
        {
            fieldGuideOpen = true;
            guidePage = armory ? guideArmoryPage : GuideBestiary;
            guideDetailScroll = 0f;
            guideScrollToSel = true;
            GUIUtility.keyboardControl = 0;
        }

        void CloseFieldGuide()
        {
            fieldGuideOpen = false;
            GUIUtility.keyboardControl = 0;
        }

        void SetGuidePage(int page)
        {
            if (page == guidePage)
                return;
            guidePage = page;
            if (page != GuideBestiary)
                guideArmoryPage = page;
            guideDetailScroll = 0f;
            guideScrollToSel = true;
        }

        /// Open another entry (a weapon named on a class's page, an upgrade's ability...).
        void GuideJump(int page, string key)
        {
            SetGuidePage(page);
            guideSel[page] = key;
            guideSearch = "";
            guideDetailScroll = 0f;
            guideScrollToSel = true;
            GUIUtility.keyboardControl = 0;
        }

        /// Keys while the guide is open (from Update): the arrows walk the list.
        void FieldGuideKeys()
        {
            int step = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 0;
            if (step == 0)
                return;
            var items = GuideFiltered(guidePage);
            if (items.Count == 0)
                return;
            int i = items.FindIndex(x => x.Key == guideSel[guidePage]);
            i = Mathf.Clamp(i + step, 0, items.Count - 1);
            guideSel[guidePage] = items[i].Key;
            guideDetailScroll = 0f;
            guideScrollToSel = true;
        }

        // --- The lists ----------------------------------------------------------------

        List<GuideItem> GuideList(int page)
        {
            if (guideItems == null)
            {
                guideItems = new List<GuideItem>[GuidePages];
                guideItems[GuideBestiary] = BuildBestiaryItems();
                guideItems[GuideClasses] = BuildClassItems();
                guideItems[GuideWeapons] = BuildWeaponItems();
                guideItems[GuideAbilities] = BuildAbilityItems();
                guideItems[GuideUpgrades] = BuildUpgradeItems();
                foreach (var list in guideItems)
                    foreach (var it in list)
                        it.Search = (it.Name + " " + it.Sub + " " + it.Group + " " + it.Search).ToLowerInvariant();
            }
            return guideItems[page];
        }

        List<GuideItem> GuideFiltered(int page)
        {
            var all = GuideList(page);
            string q = guideSearch.Trim().ToLowerInvariant();
            if (q.Length == 0)
                return all;
            var words = q.Split(' ');
            return all.FindAll(it =>
            {
                foreach (var w in words)
                    if (w.Length > 0 && it.Search.IndexOf(w, System.StringComparison.Ordinal) < 0)
                        return false;
                return true;
            });
        }

        static List<GuideItem> BuildBestiaryItems()
        {
            var list = new List<GuideItem>();
            foreach (var e in BestiaryData.All)
            {
                var d = Defs.Get(e.Type);
                var sb = new System.Text.StringBuilder(e.Appears + " " + e.Lead + " " + d.Blurb);
                foreach (var a in e.Attacks)
                    sb.Append(' ').Append(a.Name).Append(' ').Append(a.Pattern).Append(' ').Append(a.Shot);
                list.Add(new GuideItem
                {
                    Key = e.Type.ToString(), Name = e.Name, Group = e.Group, Beast = e, HasEnemy = true, Enemy = e.Type,
                    Sub = (d.IsBoss && e.Group == "Bosses" ? "Boss · wave " : "From wave ") + e.Wave,
                    Accent = ToColor(d.Color), Search = sb.ToString(),
                });
            }
            return list;
        }

        static List<GuideItem> BuildClassItems()
        {
            var list = new List<GuideItem>();
            for (int k = 0; k < Defs.KitCount; k++)
                for (int r = 0; r < 3; r++)
                {
                    var role = (Role)r;
                    var kit = (Kit)k;
                    var def = Defs.Get(role);
                    list.Add(new GuideItem
                    {
                        Key = r + "/" + k, Name = Defs.KitName(kit) + " " + def.Name, Group = Defs.KitName(kit),
                        Sub = (int)(def.Hp * Defs.KitHpMult(kit)) + " HP · " + Defs.Weapon(role, kit, 0).Name + ", " + Defs.Weapon(role, kit, 1).Name + "…",
                        Accent = ToColor(def.Color), HasPlayer = true, Role = role, Kit = kit, Weapon = 0,
                        Search = def.Desc + " " + KitBlurb(kit),
                    });
                }
            return list;
        }

        static List<GuideItem> BuildWeaponItems()
        {
            var list = new List<GuideItem>();
            for (int k = 0; k < Defs.KitCount; k++)
                for (int r = 0; r < 3; r++)
                    for (int i = 0; i < Defs.WeaponsPerRole; i++)
                    {
                        var role = (Role)r;
                        var kit = (Kit)k;
                        var w = Defs.Weapon(role, kit, i);
                        list.Add(new GuideItem
                        {
                            Key = w.Id.ToString(), Name = w.Name, Group = Defs.KitName(kit) + " weapons",
                            Sub = Defs.Get(role).Name + (w.Melee ? " · melee" : "") + (w.Companion != MinionKind.None ? " · companions" : ""),
                            Accent = ToColor(Defs.Get(role).Color), HasPlayer = true, Role = role, Kit = kit, Weapon = i,
                            Search = w.Desc + " " + w.ExtraDesc,
                        });
                    }
            return list;
        }

        static List<GuideItem> BuildAbilityItems()
        {
            var list = new List<GuideItem>();
            // Space abilities by class, then the ultimates, then the ones only very rare cards give.
            for (int k = 0; k < Defs.KitCount; k++)
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < Upgrades.RoleAbilityChoices; c++)
                        list.Add(AbilityItem(Upgrades.Ability(Upgrades.RoleAbility((Role)r, (Kit)k, c)), Defs.KitName((Kit)k) + " · Space",
                            Defs.Get((Role)r).Name + " · " + CooldownText(Upgrades.Ability(Upgrades.RoleAbility((Role)r, (Kit)k, c)))));
            for (int k = 0; k < Defs.KitCount; k++)
                for (int r = 0; r < 3; r++)
                {
                    var a = Upgrades.Ability(Upgrades.Ultimate((Role)r, (Kit)k));
                    list.Add(AbilityItem(a, "Ultimates · F", Defs.KitName((Kit)k) + " " + Defs.Get((Role)r).Name + " · " + CooldownText(a)));
                }
            foreach (var a in Upgrades.Abilities)
                if (a.Choice == AbilityDef.CardOnly)
                    list.Add(AbilityItem(a, "Bonus abilities · Q / E / Shift", "From a card · " + CooldownText(a)));
            return list;
        }

        static GuideItem AbilityItem(AbilityDef a, string group, string sub)
        {
            return new GuideItem
            {
                Key = a.Id.ToString(), Name = a.Name, Group = group, Sub = sub, Ability = a, Icon = PixelArt.ForUpgrade(a.Name),
                Accent = a.Choice == AbilityDef.CardOnly ? Ui.Gold : a.Choice == AbilityDef.Ult ? new Color(1f, 0.6f, 0.3f) : ToColor(Defs.Get(a.Role).Color),
                Search = a.Desc + " " + a.Capstone,
            };
        }

        static string CooldownText(AbilityDef a) => a.Cooldown > 0f ? Num(a.Cooldown) + " s" : "no cooldown";

        static List<GuideItem> BuildUpgradeItems()
        {
            var list = new List<GuideItem>();
            for (int rar = 0; rar <= (int)Rarity.Mod; rar++)
                for (int i = 0; i < Upgrades.All.Length; i++)
                {
                    var u = Upgrades.All[i];
                    if ((int)u.Rarity != rar || u.Unoffered || u.ClassAbility)
                        continue;  // class ability cards are your class's other Space abilities: see Abilities
                    string sub = u.Desc.Length > 46 ? u.Desc.Substring(0, 44).TrimEnd() + "…" : u.Desc;
                    list.Add(new GuideItem
                    {
                        Key = i.ToString(), Name = u.Name, Group = Upgrades.RarityName(u.Rarity).Substring(0, 1) + Upgrades.RarityName(u.Rarity).Substring(1).ToLowerInvariant(),
                        Sub = sub, Accent = ToColor(Upgrades.RarityColor(u.Rarity)), Icon = PixelArt.ForUpgrade(u.Name), Upgrade = i,
                        Search = u.Desc + (u.Role.HasValue ? " " + Defs.Get(u.Role.Value).Name : "") + (u.Boss.HasValue ? " " + BossName(u.Boss.Value) : ""),
                    });
                }
            return list;
        }

        /// Is this entry yours this match (your class, weapon, abilities, upgrade cards)?
        bool GuideMine(GuideItem it)
        {
            var me = game.Me;
            if (me == null)
                return false;
            if (it.HasPlayer && guidePage == GuideClasses)
                return it.Role == me.Role && it.Kit == me.Kit;
            if (it.HasPlayer && guidePage == GuideWeapons)
                return me.Weapon != null && me.Weapon.Name == it.Name;
            if (it.Ability != null)
            {
                foreach (var s in me.Slots)
                    if (s.Id == it.Ability.Id)
                        return true;
                return false;
            }
            return it.Upgrade >= 0 && me.Taken.Contains(it.Upgrade);
        }

        static string Num(float v) => Mathf.Abs(v - Mathf.Round(v)) < 0.01f ? Mathf.RoundToInt(v).ToString() : v.ToString(v < 10f ? "0.##" : "0.#");

        // --- The screen ------------------------------------------------------------------

        void DrawFieldGuideScreen()
        {
            float t = Time.unscaledTime;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.82f));
            var panel = new Rect(28f, 18f, W - 56f, H - 36f);
            Ui.Panel(panel, true);
            bool armory = guidePage != GuideBestiary;
            Color accent = armory ? ArmoryColor : BestiaryColor;

            // Header: title, the two books, search, back.
            float hx = panel.x + 22f, hy = panel.y + 14f;
            Ui.Fancy(new Rect(hx, hy, 230f, 36f), "FIELD GUIDE", 26, Color.white, Color.Lerp(accent, Color.white, 0.45f), TextAnchor.MiddleLeft, accent);
            if (Ui.Choice(new Rect(hx + 220f, hy + 2f, 140f, 34f), "Bestiary", !armory, BestiaryColor))
                SetGuidePage(GuideBestiary);
            if (Ui.Choice(new Rect(hx + 368f, hy + 2f, 140f, 34f), "Armory", armory, ArmoryColor))
                SetGuidePage(guideArmoryPage);
            var back = new Rect(panel.xMax - 152f, hy + 2f, 130f, 34f);
            if (Ui.Button(back, "Back  [Esc]"))
                CloseFieldGuide();
            var search = new Rect(back.x - 262f, hy + 2f, 250f, 34f);
            Ui.Label(new Rect(search.x - 64f, search.y, 60f, 34f), "SEARCH", 10, Ui.Muted, TextAnchor.MiddleRight, true);
            string q = Ui.Field(search, guideSearch, 30);
            if (q != guideSearch)
            {
                guideSearch = q;
                guideScrollToSel = true;
                guideListScroll[guidePage] = 0f;
            }
            if (guideSearch.Length > 0 && Ui.SmallButton(new Rect(search.xMax - 30f, search.y + 6f, 24f, 22f), "×", 14))
            {
                guideSearch = "";
                GUIUtility.keyboardControl = 0;
            }
            float y = hy + 46f;

            // The Armory's tabs, with how many entries each has.
            if (armory)
            {
                for (int p = GuideClasses; p < GuidePages; p++)
                {
                    int n = GuideFiltered(p).Count;
                    if (Ui.Choice(new Rect(hx + (p - 1) * 136f, y, 128f, 28f), GuideTabNames[p] + "  " + n, guidePage == p, ArmoryColor, null, null, 13))
                        SetGuidePage(p);
                }
                y += 36f;
            }
            else
            {
                Ui.Label(new Rect(hx, y, 700f, 28f), "Every enemy and boss: how it fights, its attacks, and its health at your wave.", 13, Ui.Muted);
                y += 36f;
            }
            Ui.Label(new Rect(panel.xMax - 420f, y - 36f, 398f, 28f), "↑ ↓ to browse · click a name in an entry to open it", 11, Ui.Muted, TextAnchor.MiddleRight);

            var list = new Rect(panel.x + 16f, y, 330f, panel.yMax - 16f - y);
            var detail = new Rect(list.xMax + 14f, y, panel.xMax - 16f - list.xMax - 14f, list.height);
            var shown = GuideFiltered(guidePage);
            var sel = shown.Find(x => x.Key == guideSel[guidePage]);
            if (sel == null && shown.Count > 0)
            {
                sel = shown[0];
                guideSel[guidePage] = sel.Key;
                guideDetailScroll = 0f;
            }
            DrawGuideList(list, shown, sel, t);
            Ui.Pill(detail, new Color(0f, 0f, 0f, 0.28f));
            if (sel == null)
            {
                Ui.Label(detail, "Nothing matches \"" + guideSearch.Trim() + "\".", 15, Ui.Muted, TextAnchor.MiddleCenter);
                return;
            }
            switch (guidePage)
            {
                case GuideBestiary: DrawBeastEntry(detail, sel, t); break;
                case GuideClasses: DrawClassEntry(detail, sel, t); break;
                case GuideWeapons: DrawWeaponEntry(detail, sel, t); break;
                case GuideAbilities: DrawAbilityEntry(detail, sel, t); break;
                default: DrawUpgradeEntry(detail, sel, t); break;
            }
        }

        const float GuideRow = 46f, GuideHeader = 26f;

        void DrawGuideList(Rect list, List<GuideItem> items, GuideItem sel, float t)
        {
            Ui.Pill(list, new Color(0f, 0f, 0f, 0.3f));
            // Lay out: a header before each group, then its rows.
            var rowY = new float[items.Count];
            var headers = new List<KeyValuePair<float, string>>();
            float h = 6f;
            string group = null;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Group != group)
                {
                    group = items[i].Group;
                    headers.Add(new KeyValuePair<float, string>(h, group));
                    h += GuideHeader;
                }
                rowY[i] = h;
                h += GuideRow;
            }
            h += 6f;
            int page = guidePage;
            float maxScroll = Mathf.Max(0f, h - list.height);
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && list.Contains(ev.mousePosition))
            {
                guideListScroll[page] += ev.delta.y * 30f;
                ev.Use();
            }
            int selIndex = sel == null ? -1 : items.IndexOf(sel);
            if (guideScrollToSel && selIndex >= 0)
            {
                float top = rowY[selIndex] - GuideHeader - 4f, bottom = rowY[selIndex] + GuideRow + 4f;
                if (top < guideListScroll[page])
                    guideListScroll[page] = top;
                else if (bottom > guideListScroll[page] + list.height)
                    guideListScroll[page] = bottom - list.height;
                guideScrollToSel = false;
            }
            float scroll = guideListScroll[page] = Mathf.Clamp(guideListScroll[page], 0f, maxScroll);

            GUI.BeginGroup(list);
            foreach (var hd in headers)
            {
                float gy = hd.Key - scroll;
                if (gy < -GuideHeader || gy > list.height)
                    continue;
                Ui.Label(new Rect(12f, gy + 4f, list.width - 24f, 18f), hd.Value.ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Fill(new Rect(12f, gy + 22f, list.width - 24f, 1f), new Color(1f, 1f, 1f, 0.07f));
            }
            var mouse = ev.mousePosition;
            for (int i = 0; i < items.Count; i++)
            {
                float ry = rowY[i] - scroll;
                if (ry < -GuideRow || ry > list.height)
                    continue;
                var it = items[i];
                var r = new Rect(6f, ry, list.width - 18f, GuideRow - 4f);
                bool on = it == sel, hover = r.Contains(mouse);
                if (on || hover)
                    Ui.Pill(r, on ? new Color(it.Accent.r * 0.35f, it.Accent.g * 0.35f, it.Accent.b * 0.35f, 0.95f) : new Color(1f, 1f, 1f, 0.05f));
                Ui.Fill(new Rect(r.x, r.y + 6f, 3f, r.height - 12f), new Color(it.Accent.r, it.Accent.g, it.Accent.b, on ? 1f : 0.55f));
                var thumb = new Rect(r.x + 8f, r.y + 2f, 38f, 38f);
                if (it.Icon != null)
                    Ui.Icon(new Rect(thumb.x + 5f, thumb.y + 5f, 28f, 28f), it.Icon);
                bool mine = GuideMine(it);
                float nameW = r.width - 60f - (mine ? 40f : 0f);
                Ui.Label(new Rect(r.x + 54f, r.y + 3f, nameW, 20f), it.Name, 14, on ? Color.white : Color.Lerp(it.Accent, Color.white, 0.55f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 54f, r.y + 21f, r.width - 60f, 16f), it.Sub, 11, on ? new Color(1f, 1f, 1f, 0.75f) : Ui.Muted);
                if (mine)
                    Ui.Badge(new Rect(r.xMax - 42f, r.y + 5f, 36f, 16f), "YOU", Ui.Good);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !on)
                {
                    guideSel[page] = it.Key;
                    guideDetailScroll = 0f;
                }
            }
            GUI.EndGroup();
            if (maxScroll > 0f)
            {
                float th = list.height * list.height / h, ty = list.y + (list.height - th) * (scroll / maxScroll);
                Ui.Fill(new Rect(list.xMax - 5f, ty, 3f, th), new Color(1f, 1f, 1f, 0.3f));
            }

            // The live models in the rows, clipped to the list.
            if (BeginUiGl())
            {
                UiClip(new Rect(list.x, list.y + 2f, list.width, list.height - 4f));
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    float ry = rowY[i] - scroll;
                    if (ry < -GuideRow || ry > list.height || !(it.HasEnemy || it.HasPlayer))
                        continue;
                    var c = new V2(list.x + 33f, list.y + ry + 21f);
                    if (it.HasEnemy)
                        DrawEnemyPreview(it.Enemy, c, 36f, t, it == sel);
                    else
                    {
                        UiModelAt(c, 0.6f);
                        DrawPreviewPlayer(it.Role, it.Kit, it.Weapon, t + i * 0.7f);
                    }
                }
                EndUiGl();
            }
        }

        // --- Entries -------------------------------------------------------------------------
        // Each entry: a stage on the left (a live model or a big icon), the name and numbers beside it, then the
        // words below, which scroll.

        Rect GuideStage(Rect r, Color accent)
        {
            var stage = new Rect(r.x + 14f, r.y + 14f, 250f, 196f);
            Ui.Pill(stage, new Color(accent.r * 0.12f + 0.02f, accent.g * 0.12f + 0.02f, accent.b * 0.12f + 0.03f, 0.95f));
            // A faint floor grid, like the arena's.
            for (int k = 1; k < 5; k++)
                Ui.Fill(new Rect(stage.x + stage.width * k / 5f, stage.y + 6f, 1f, stage.height - 12f), new Color(1f, 1f, 1f, 0.03f));
            for (int k = 1; k < 4; k++)
                Ui.Fill(new Rect(stage.x + 6f, stage.y + stage.height * k / 4f, stage.width - 12f, 1f), new Color(1f, 1f, 1f, 0.03f));
            return stage;
        }

        /// The entry's name and chips beside the stage; returns where the stat tiles go.
        float GuideTitle(Rect r, Rect stage, string name, Color accent, params KeyValuePair<string, Color>[] chips)
        {
            float x = stage.xMax + 18f, w = r.xMax - 14f - x;
            Ui.Fancy(new Rect(x, r.y + 12f, w, 36f), name.ToUpperInvariant(), 26, Color.white, Color.Lerp(accent, Color.white, 0.35f), TextAnchor.MiddleLeft, accent);
            float cx = x;
            foreach (var c in chips)
            {
                float cw = Ui.TextWidth(c.Key, 11, true) - 4f;
                if (cx + cw > r.xMax - 14f)
                    break;
                Ui.Badge(new Rect(cx, r.y + 52f, cw, 20f), c.Key, c.Value);
                cx += cw + 6f;
            }
            return r.y + 82f;
        }

        static KeyValuePair<string, Color> Chip(string text, Color c) => new KeyValuePair<string, Color>(text.ToUpperInvariant(), c);

        /// A small stat tile: label above, value below (and an optional note under that).
        void GuideStat(float x, float y, float w, string label, string value, Color c, string note = null)
        {
            Ui.Pill(new Rect(x, y, w, 56f), new Color(1f, 1f, 1f, 0.04f));
            Ui.Label(new Rect(x + 10f, y + 4f, w - 16f, 16f), label.ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 10f, y + 19f, w - 16f, 22f), value, 17, c, TextAnchor.MiddleLeft, true);
            if (note != null)
                Ui.Label(new Rect(x + 10f, y + 39f, w - 16f, 14f), note, 10, Ui.Muted);
        }

        /// Up to six stat tiles in rows of three beside the stage.
        void GuideStats(Rect r, Rect stage, float y, params string[] cells)
        {
            // cells: label, value, note (null for none), repeated
            float x0 = stage.xMax + 18f, w = (r.xMax - 14f - x0 - 16f) / 3f;
            for (int i = 0; i * 3 < cells.Length; i++)
            {
                int col = i % 3, row = i / 3;
                GuideStat(x0 + col * (w + 8f), y + row * 62f, w, cells[i * 3], cells[i * 3 + 1], Color.white, cells[i * 3 + 2]);
            }
        }

        // The scrolling body: call GuideBodyBegin, draw with the helpers (y runs from 0 down), then GuideBodyEnd.
        Rect guideBody;
        float gy, gw;

        void GuideBodyBegin(Rect r)
        {
            guideBody = new Rect(r.x + 14f, r.y + 222f, r.width - 28f, r.height - 230f);
            Ui.Fill(new Rect(guideBody.x, guideBody.y - 6f, guideBody.width, 1f), new Color(1f, 1f, 1f, 0.07f));
            var ev = Event.current;
            float maxScroll = Mathf.Max(0f, guideDetailHeight - guideBody.height);
            if (ev.type == EventType.ScrollWheel && guideBody.Contains(ev.mousePosition))
            {
                guideDetailScroll += ev.delta.y * 30f;
                ev.Use();
            }
            guideDetailScroll = Mathf.Clamp(guideDetailScroll, 0f, maxScroll);
            GUI.BeginGroup(guideBody);
            gy = 4f - guideDetailScroll;
            gw = guideBody.width - 14f;
        }

        void GuideBodyEnd()
        {
            GUI.EndGroup();
            guideDetailHeight = gy + guideDetailScroll + 8f;
            float maxScroll = guideDetailHeight - guideBody.height;
            if (maxScroll > 0f)
            {
                float th = guideBody.height * guideBody.height / guideDetailHeight;
                float ty = guideBody.y + (guideBody.height - th) * (guideDetailScroll / maxScroll);
                Ui.Fill(new Rect(guideBody.xMax - 4f, ty, 3f, th), new Color(1f, 1f, 1f, 0.3f));
                if (guideDetailScroll < maxScroll - 1f)
                    Ui.Label(new Rect(guideBody.x, guideBody.yMax - 16f, guideBody.width - 12f, 16f), "scroll for more", 10, Ui.Muted, TextAnchor.MiddleRight);
            }
        }

        void GHeading(string text)
        {
            gy += 6f;
            Ui.Label(new Rect(0f, gy, gw, 18f), text.ToUpperInvariant(), 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Fill(new Rect(0f, gy + 19f, gw, 1f), new Color(1f, 1f, 1f, 0.07f));
            gy += 26f;
        }

        void GText(string text, int size, Color c, float indent = 0f, bool bold = false)
        {
            if (string.IsNullOrEmpty(text))
                return;
            float h = Ui.TextHeight(text, size, gw - indent, bold);
            Ui.Label(new Rect(indent, gy, gw - indent, h), text, size, c, TextAnchor.UpperLeft, bold, true);
            gy += h + 4f;
        }

        /// A clickable row naming another entry: icon, name, a line of text, and an arrow. Returns true when clicked.
        bool GLink(Texture2D icon, string name, string text, Color accent, bool mine = false)
        {
            float th = Ui.TextHeight(text, 12, gw - 70f);
            var r = new Rect(0f, gy, gw, Mathf.Max(44f, 26f + th));
            bool hover = r.Contains(Event.current.mousePosition);
            Ui.Pill(r, hover ? new Color(accent.r * 0.3f, accent.g * 0.3f, accent.b * 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.04f));
            Ui.Fill(new Rect(r.x, r.y + 6f, 3f, r.height - 12f), accent);
            if (icon != null)
                Ui.Icon(new Rect(r.x + 12f, r.y + 8f, 26f, 26f), icon);
            float x = icon != null ? 48f : 14f;
            Ui.Label(new Rect(x, r.y + 4f, gw - x - 60f, 20f), name, 14, Color.Lerp(accent, Color.white, 0.5f), TextAnchor.MiddleLeft, true);
            if (mine)
                Ui.Badge(new Rect(x + Ui.TextWidth(name, 14, true) - 10f, r.y + 6f, 36f, 16f), "YOU", Ui.Good);
            Ui.Label(new Rect(x, r.y + 23f, gw - x - 30f, th), text, 12, Ui.Text, TextAnchor.UpperLeft, false, true);
            Ui.Label(new Rect(r.xMax - 28f, r.y, 20f, r.height), "›", 20, hover ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
            gy += r.height + 6f;
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // --- Bestiary ----------------------------------------------------------------------

        void DrawBeastEntry(Rect r, GuideItem it, float t)
        {
            var e = it.Beast;
            var d = Defs.Get(e.Type);
            var stage = GuideStage(r, it.Accent);
            if (BeginUiGl())
            {
                UiClip(stage);
                DrawEnemyPreview(e.Type, new V2(stage.center.x, stage.center.y), d.IsBoss ? 176f : Mathf.Min(150f, 40f + d.Radius * 4.5f), t, true);
                EndUiGl();
            }
            var chips = new List<KeyValuePair<string, Color>> { Chip(e.Group, it.Accent) };
            if (d.IsBoss) chips.Add(Chip("Boss", new Color(1f, 0.3f, 0.4f)));
            if (d.Melee) chips.Add(Chip("Melee", new Color(0.9f, 0.55f, 0.3f)));
            if (d.Heavy || d.IsBoss) chips.Add(Chip("Can't be shoved", Ui.Neutral * 1.6f));
            chips.Add(Chip("From wave " + e.Wave, Ui.Neutral * 1.6f));
            float sy = GuideTitle(r, stage, e.Name, it.Accent, chips.ToArray());

            // Health at your wave (bosses: at the wave they come), for your party.
            int players = Mathf.Max(1, game.Players.Count);
            int wave = Mathf.Max(1, game.Wave);
            int atWave = d.IsBoss ? Mathf.Max(e.Wave, wave) : wave;
            string party = players == 1 ? "solo" : players + " players";
            GuideStats(r, stage, sy,
                "Health", Num(d.Hp), "base",
                "At wave " + atWave, Mathf.RoundToInt(game.SpawnHp(e.Type, atWave)).ToString("N0"), party + (game.Has(RunMod.ToughCrowd) ? ", Tough Crowd" : ""),
                "Speed", d.Speed > 0f ? Num(d.Speed) + " px/s" : "Doesn't walk", null,
                "Size", Num(d.Radius * 2f) + " px", "across",
                "XP · Score", d.Xp + " · " + d.Score, d.Xp == 0 ? "gives no XP" : null,
                "Touch", d.Contact > 0f ? Num(d.Contact) + " damage" : d.IsBoss ? "Hurts, shoves" : d.Melee ? "Hurts" : "Harmless", d.IsBoss ? "once a second" : null);

            GuideBodyBegin(r);
            GText(e.Lead, 14, Ui.Text);
            GText("Appears:  " + e.Appears, 12, Ui.Gold);
            if (e.Stages != null && e.Stages.Length > 0)
            {
                GHeading("Stages");
                float cx = 0f;
                for (int i = 0; i < e.Stages.Length; i++)
                {
                    string s = (i + 1) + "  " + e.Stages[i];
                    float cw = Ui.TextWidth(s, 12, true) + 6f;
                    if (cx + cw > gw) { cx = 0f; gy += 30f; }
                    Ui.Pill(new Rect(cx, gy, cw, 24f), new Color(it.Accent.r * 0.3f, it.Accent.g * 0.3f, it.Accent.b * 0.3f, 0.9f));
                    Ui.Label(new Rect(cx, gy, cw, 24f), s, 12, Color.white, TextAnchor.MiddleCenter, true);
                    cx += cw + 8f;
                }
                gy += 34f;
            }
            if (e.Attacks.Length > 0)
            {
                GHeading("Attacks");
                float rate = Game.WaveFireRate(wave) * (game.Has(RunMod.Frenzy) ? Modifiers.FrenzyFire : 1f);
                foreach (var a in e.Attacks)
                    DrawBeastAttack(a, rate, wave);
            }
            if (e.Notes.Length > 0)
            {
                GHeading("Tips");
                foreach (var n in e.Notes)
                {
                    Ui.Label(new Rect(4f, gy, 12f, 18f), "•", 14, it.Accent, TextAnchor.UpperLeft, true);
                    GText(n, 13, Ui.Text, 18f);
                }
            }
            if (!string.IsNullOrEmpty(d.Blurb))
            {
                GHeading("In short");
                GText(d.Blurb, 13, Ui.Muted);
            }
            GuideBodyEnd();
        }

        /// "23" → "23 damage", "23 each" → "23 damage each"; anything wordier ("10 contact", "8 per second") as written.
        static string DamageText(string d)
        {
            float v;
            if (float.TryParse(d, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                return d + " damage";
            if (d.EndsWith(" each") && float.TryParse(d.Substring(0, d.Length - 5), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                return d.Substring(0, d.Length - 5) + " damage each";
            return d;
        }

        void DrawBeastAttack(BestiaryAttack a, float rate, int wave)
        {
            // The numbers as chips: shot, count, speed, damage, timing.
            var chips = new List<string>();
            if (a.Count != null) chips.Add("× " + a.Count);
            if (a.Speed != null) chips.Add(a.Speed);
            if (a.Damage != null && a.Damage != "—") chips.Add(DamageText(a.Damage));
            if (a.Every > 0f)
                chips.Add(a.Fixed || rate <= 1.001f ? "every " + Num(a.Every) + " s" : "every " + Num(a.Every) + " s → " + (a.Every / rate).ToString(a.Every / rate < 0.2f ? "0.000" : "0.00") + " s at wave " + wave);
            else if (a.EveryText != null)
                chips.Add(a.EveryText);
            float ph = Ui.TextHeight(a.Pattern, 12, gw - 24f), nh = a.Notes != null ? Ui.TextHeight(a.Notes, 11, gw - 24f) : 0f;
            // Chips wrap onto extra lines when they don't fit.
            float cx = 12f, cyRows = 1f;
            float shotW = a.Shot != null ? Ui.TextWidth(a.Shot, 11, true) + 14f : 0f;
            cx += shotW + (shotW > 0f ? 6f : 0f);
            foreach (var c in chips)
            {
                float cw = Ui.TextWidth(c, 11, true) - 6f;
                if (cx + cw > gw - 12f) { cyRows++; cx = 12f; }
                cx += cw + 6f;
            }
            float h = 28f + cyRows * 24f + ph + (nh > 0f ? nh + 2f : 0f) + 6f;
            var card = new Rect(0f, gy, gw, h);
            Ui.Pill(card, new Color(1f, 1f, 1f, 0.04f));
            Ui.Label(new Rect(12f, gy + 5f, gw - 24f, 20f), a.Name, 14, Color.white, TextAnchor.MiddleLeft, true);
            float y = gy + 28f;
            cx = 12f;
            if (a.Shot != null)
            {
                var sc = ToColor(a.ShotColor);
                var sr = new Rect(cx, y, shotW, 20f);
                Ui.Pill(sr, new Color(sc.r * 0.35f, sc.g * 0.35f, sc.b * 0.35f, 0.95f));
                Ui.Pill(new Rect(sr.x + 6f, sr.y + 6f, 8f, 8f), sc);
                Ui.Label(new Rect(sr.x + 16f, sr.y, sr.width - 16f, 20f), a.Shot, 11, Color.Lerp(sc, Color.white, 0.5f), TextAnchor.MiddleLeft, true);
                cx += shotW + 6f;
            }
            foreach (var c in chips)
            {
                float cw = Ui.TextWidth(c, 11, true) - 6f;
                if (cx + cw > gw - 12f) { y += 24f; cx = 12f; }
                Ui.Pill(new Rect(cx, y, cw, 20f), new Color(1f, 1f, 1f, 0.08f));
                Ui.Label(new Rect(cx, y, cw, 20f), c, 11, Ui.Text, TextAnchor.MiddleCenter, true);
                cx += cw + 6f;
            }
            y += 24f;
            Ui.Label(new Rect(12f, y, gw - 24f, ph), a.Pattern, 12, Ui.Text, TextAnchor.UpperLeft, false, true);
            y += ph;
            if (a.Notes != null)
                Ui.Label(new Rect(12f, y + 2f, gw - 24f, nh), a.Notes, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
            gy += h + 6f;
        }

        // --- Armory: classes ---------------------------------------------------------------

        void DrawClassEntry(Rect r, GuideItem it, float t)
        {
            var def = Defs.Get(it.Role);
            var stage = GuideStage(r, it.Accent);
            int shownWeapon = (int)(t / 4f) % Defs.WeaponsPerRole;  // shows off each weapon in turn
            if (BeginUiGl())
            {
                UiClip(stage);
                UiModelAt(new V2(stage.center.x, stage.center.y), 2.6f);
                DrawPreviewPlayer(it.Role, it.Kit, shownWeapon, t);
                EndUiGl();
            }
            Ui.Label(new Rect(stage.x, stage.yMax - 22f, stage.width, 18f), Defs.Weapon(it.Role, it.Kit, shownWeapon).Name, 11, Ui.Muted, TextAnchor.MiddleCenter);
            float sy = GuideTitle(r, stage, it.Name, it.Accent, Chip(def.Name, it.Accent), Chip(Defs.KitName(it.Kit), Ui.Neutral * 1.6f));
            float speed = def.Speed * Defs.KitSpeedMult(it.Kit);
            GuideStats(r, stage, sy,
                "Health", ((int)(def.Hp * Defs.KitHpMult(it.Kit))).ToString(), Defs.KitHpMult(it.Kit) != 1f ? Mathf.RoundToInt(Defs.KitHpMult(it.Kit) * 100f) + "% of " + Num(def.Hp) : null,
                "Speed", Mathf.RoundToInt(speed) + " px/s", null,
                "Hitbox", Num(def.HitR * 2f) + " px", "across");

            GuideBodyBegin(r);
            GText(def.Desc, 14, Ui.Text);
            GText(KitBlurb(it.Kit), 13, Ui.Muted);
            GHeading("Weapons (pick one in the lobby)");
            for (int i = 0; i < Defs.WeaponsPerRole; i++)
            {
                var w = Defs.Weapon(it.Role, it.Kit, i);
                var me = game.Me;
                if (GLink(null, w.Name, w.Desc, it.Accent, me != null && me.Weapon != null && me.Weapon.Name == w.Name))
                    GuideJump(GuideWeapons, w.Id.ToString());
            }
            GHeading("Space abilities (pick one in the lobby)");
            for (int c = 0; c < Upgrades.RoleAbilityChoices; c++)
                if (AbilityLink(Upgrades.Ability(Upgrades.RoleAbility(it.Role, it.Kit, c)), it.Accent))
                    break;
            GHeading("Ultimate (F)");
            AbilityLink(Upgrades.Ability(Upgrades.Ultimate(it.Role, it.Kit)), new Color(1f, 0.6f, 0.3f));
            GuideBodyEnd();
        }

        bool AbilityLink(AbilityDef a, Color accent)
        {
            bool mine = false;
            if (game.Me != null)
                foreach (var s in game.Me.Slots)
                    mine |= s.Id == a.Id;
            if (!GLink(PixelArt.ForUpgrade(a.Name), a.Name + "   ·   " + CooldownText(a), a.Desc, accent, mine))
                return false;
            GuideJump(GuideAbilities, a.Id.ToString());
            return true;
        }

        // --- Armory: weapons -------------------------------------------------------------------

        void DrawWeaponEntry(Rect r, GuideItem it, float t)
        {
            var w = Defs.Weapon(it.Role, it.Kit, it.Weapon);
            var stage = GuideStage(r, it.Accent);
            if (BeginUiGl())
            {
                UiClip(stage);
                UiModelAt(new V2(stage.center.x, stage.center.y), 2.6f);
                DrawPreviewPlayer(it.Role, it.Kit, it.Weapon, t);
                EndUiGl();
            }
            var chips = new List<KeyValuePair<string, Color>> { Chip(Defs.KitName(it.Kit) + " " + Defs.Get(it.Role).Name, it.Accent) };
            chips.Add(Chip(w.Melee ? "Melee" : "Ranged", Ui.Neutral * 1.6f));
            if (w.Companion != MinionKind.None)
                chips.Add(Chip("Companions", new Color(0.4f, 0.8f, 0.5f)));
            float sy = GuideTitle(r, stage, w.Name, it.Accent, chips.ToArray());
            // Its reach as the aim line shows it, without upgrades.
            var p = previewPlayer;
            p.Role = it.Role;
            p.Def = Defs.Get(it.Role);
            p.Kit = it.Kit;
            p.Weapon = w;
            float reach = game.WeaponRange(p);
            var cells = new List<string>
            {
                "Damage", Num(w.Damage), w.Melee ? "a swing" : "a hit, before upgrades",
                "Attacks", (1f / w.FireInterval).ToString("0.0") + " / s", "every " + Num(w.FireInterval) + " s",
                w.Melee ? "Reach" : "Range", Mathf.RoundToInt(reach) + " px", "as the aim line shows",
            };
            if (w.Companion != MinionKind.None)
                cells.AddRange(new[] { "Companions", w.CompanionCount + " × " + Spaced(w.Companion.ToString()), w.CompanionRespawn != 3f ? "back in " + Num(w.CompanionRespawn) + " s" : null });
            GuideStats(r, stage, sy, cells.ToArray());

            GuideBodyBegin(r);
            GText(w.Desc, 14, Ui.Text);
            if (!string.IsNullOrEmpty(w.ExtraDesc))
            {
                GHeading("Each extra projectile (Multishot, Bullet Storm…)");
                GText(w.ExtraDesc, 13, Ui.Text);
            }
            GHeading("Used by");
            var role = Defs.Get(it.Role);
            if (GLink(null, Defs.KitName(it.Kit) + " " + role.Name, role.Desc + " " + KitBlurb(it.Kit), it.Accent))
                GuideJump(GuideClasses, (int)it.Role + "/" + (int)it.Kit);
            GuideBodyEnd();
        }

        // --- Armory: abilities ------------------------------------------------------------------

        Rect GuideIconStage(Rect r, Color accent, Texture2D icon, float t)
        {
            var stage = GuideStage(r, accent);
            float s = 112f + 6f * Mathf.Sin(t * 2f);
            Ui.Pill(new Rect(stage.center.x - 70f, stage.center.y - 70f, 140f, 140f), new Color(accent.r, accent.g, accent.b, 0.12f));
            Ui.Icon(new Rect(stage.center.x - s / 2f, stage.center.y - s / 2f, s, s), icon);
            return stage;
        }

        void DrawAbilityEntry(Rect r, GuideItem it, float t)
        {
            var a = it.Ability;
            var stage = GuideIconStage(r, it.Accent, it.Icon, t);
            var chips = new List<KeyValuePair<string, Color>>();
            if (a.Choice == AbilityDef.Ult)
                chips.Add(Chip("Ultimate · F", it.Accent));
            else if (a.Choice == AbilityDef.CardOnly)
                chips.Add(Chip("Bonus · Q / E / Shift", it.Accent));
            else
                chips.Add(Chip("Space", it.Accent));
            if (a.Choice != AbilityDef.CardOnly)
                chips.Add(Chip(Defs.KitName(a.Kit) + " " + Defs.Get(a.Role).Name, ToColor(Defs.Get(a.Role).Color)));
            if (a.Instant)
                chips.Add(Chip("Instant", Ui.Neutral * 1.6f));
            float sy = GuideTitle(r, stage, a.Name, it.Accent, chips.ToArray());
            var cells = new List<string>
            {
                "Cooldown", a.Cooldown > 0f ? Num(a.Cooldown) + " s" : "None", "before upgrades",
                "Charges", (1 + a.ExtraCharges).ToString(), a.CapstoneCharge ? "+1 when mastered" : null,
            };
            if (a.HealthCost > 0f)
                cells.AddRange(new[] { "Costs", Mathf.RoundToInt(a.HealthCost * 100f) + "% health", a.CostsCurrent ? "of your current health" : "of your max health" });
            int level = game.Me != null && game.Me.MasteryXp.ContainsKey(a.Id) ? Game.MasteryLevel(game.Me, a.Id) : 0;
            if (level > 0)
                cells.AddRange(new[] { "Your mastery", "Level " + level, level >= Game.MasteryMax ? "mastered" : null });
            GuideStats(r, stage, sy, cells.ToArray());

            GuideBodyBegin(r);
            GText(a.Desc, 14, Ui.Text);
            if (!string.IsNullOrEmpty(a.Capstone))
            {
                GHeading("Mastered (level " + Game.MasteryMax + ")");
                GText(a.Capstone, 13, Ui.Gold);
                GText("Abilities level up as you use them. Instant ones earn more the more they hit.", 12, Ui.Muted);
            }
            if (a.Choice != AbilityDef.CardOnly)
            {
                GHeading(a.Choice == AbilityDef.Ult ? "The ultimate of" : "Picked in the lobby by");
                var role = Defs.Get(a.Role);
                if (GLink(null, Defs.KitName(a.Kit) + " " + role.Name, role.Desc + " " + KitBlurb(a.Kit), ToColor(role.Color)))
                    GuideJump(GuideClasses, (int)a.Role + "/" + (int)a.Kit);
                if (a.Choice != AbilityDef.Ult)
                    GText("It's also a very rare card (for Q, E or Shift) for players of this class who picked one of its other two Space abilities.", 12, Ui.Muted);
            }
            else
            {
                GHeading("Comes from");
                for (int i = 0; i < Upgrades.All.Length; i++)
                {
                    var u = Upgrades.All[i];
                    if (u.Grants != a.Id || u.ClassAbility || u.Unoffered)
                        continue;
                    if (GLink(PixelArt.ForUpgrade(u.Name), u.Name + "   ·   " + Upgrades.RarityName(u.Rarity), u.Desc, ToColor(Upgrades.RarityColor(u.Rarity))))
                        GuideJump(GuideUpgrades, i.ToString());
                }
            }
            var cards = UpgradesNeeding(a.Id);
            if (cards.Count > 0)
            {
                GHeading("Cards that improve it");
                foreach (int i in cards)
                {
                    var u = Upgrades.All[i];
                    if (GLink(PixelArt.ForUpgrade(u.Name), u.Name + "   ·   " + Upgrades.RarityName(u.Rarity), u.Desc, ToColor(Upgrades.RarityColor(u.Rarity))))
                        GuideJump(GuideUpgrades, i.ToString());
                }
            }
            GuideBodyEnd();
        }

        static List<int> UpgradesNeeding(AbilityId id)
        {
            var list = new List<int>();
            for (int i = 0; i < Upgrades.All.Length; i++)
                if (Upgrades.All[i].RequiresAbility == id && !Upgrades.All[i].Unoffered)
                    list.Add(i);
            return list;
        }

        // --- Armory: upgrades ----------------------------------------------------------------------

        void DrawUpgradeEntry(Rect r, GuideItem it, float t)
        {
            var u = Upgrades.All[it.Upgrade];
            var stage = GuideIconStage(r, it.Accent, it.Icon, t);
            var chips = new List<KeyValuePair<string, Color>> { Chip(Upgrades.RarityName(u.Rarity), it.Accent) };
            if (u.Role.HasValue)
                chips.Add(Chip(Defs.Get(u.Role.Value).Name + " only", ToColor(Defs.Get(u.Role.Value).Color)));
            if (u.Boss.HasValue)
                chips.Add(Chip("From " + BossName(u.Boss.Value), new Color(1f, 0.35f, 0.45f)));
            float sy = GuideTitle(r, stage, u.Name, it.Accent, chips.ToArray());
            int have = game.Me != null ? Upgrades.Count(game.Me.Taken, it.Upgrade) : 0;
            int max = u.Grants != AbilityId.None ? 1 : u.MaxStacks;
            GuideStats(r, stage, sy,
                "Stacks", max >= 99 ? "No limit" : "Up to " + max, null,
                "You have", have.ToString(), have > 0 && have >= max ? "maxed" : null,
                "Offered to", u.Role.HasValue ? Defs.Get(u.Role.Value).Name + "s" : "Everyone", u.RequiresAbility != AbilityId.None ? "with its ability" : u.NeedsExtraAbility ? "with a bonus ability" : null);

            GuideBodyBegin(r);
            GText(u.Desc, 15, Ui.Text);
            GText(RarityNote(u.Rarity), 12, Ui.Muted);
            if (u.Grants != AbilityId.None)
            {
                GHeading("Gives you");
                AbilityLink(Upgrades.Ability(u.Grants), Ui.Gold);
            }
            if (u.RequiresAbility != AbilityId.None)
            {
                GHeading("Only offered if you have");
                AbilityLink(Upgrades.Ability(u.RequiresAbility), it.Accent);
            }
            if (u.NeedsExtraAbility)
                GText("Only offered once you have a bonus ability on Q, E or Shift.", 12, Ui.Muted);
            GuideBodyEnd();
        }

        static string RarityNote(Rarity r)
        {
            switch (r)
            {
                case Rarity.Legendary: return "Legendary cards are a boss's reward: beat it and pick one of its cards.";
                case Rarity.Mod: return "Weapon mods are picked after each boss, alongside its legendary card.";
                case Rarity.VeryRare: return "Very rare cards give a new ability for Q, E or Shift (up to three).";
                default: return Upgrades.RarityName(r).Substring(0, 1) + Upgrades.RarityName(r).Substring(1).ToLowerInvariant() + " card: offered when you level up.";
            }
        }
    }
}
