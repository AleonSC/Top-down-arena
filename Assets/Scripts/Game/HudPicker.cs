using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The upgrade picker: trophy cards, upgrade cards and notes, the stat panel and the stats sidebar.
    public partial class ArenaApp
    {
        /// The level-up picker ("trophy cards", Docs/Mockups/upgrade-picker.html option C), opening out of your character
        /// (PickIntro.cs) and closing back into it (PickOutro.cs): a gold LEVEL UP! title, three
        /// tall cards with the icon big on a rarity backdrop (the picked one lifts and glows, the others step back; rare
        /// and better shine), one line saying what your pick changes, and the lobby's fancy buttons. Weapon mods (more than
        /// three cards) use a grid of smaller cards. Hold Tab for your full stats.
        void DrawUpgradePicker(Player me)
        {
            SyncSelection(me);
            float now = Time.unscaledTime;
            bool opening = PickIntroPlaying(me);  // the cards are still flying out of you: nothing can be chosen yet
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.82f * (introFull ? Seg(IntroElapsed, 0f, 0.3f) : 1f)));
            const float cardW = 270f, cardH = 384f, gap = 30f;
            float mid = W / 2f;

            // Heading: a big outlined title in the offer's colour, and a line under it.
            Color theme = me.OfferLegendary ? ToColor(Upgrades.RarityColor(Rarity.Legendary))
                : me.OfferMod ? ToColor(Upgrades.RarityColor(Rarity.Mod)) : me.OfferPrize ? ToColor(Upgrades.RarityColor(Rarity.VeryRare)) : Ui.Gold;
            string title = me.OfferLegendary ? (game.PrizeLegendary(me) ? "CHALLENGE PRIZE!" : "BOSS REWARD!") : me.OfferMod ? "WEAPON MOD" : me.OfferPrize ? "CHALLENGE PRIZE!" : "LEVEL UP!";
            float titleY, band;
            int titleSize;
            PickTitleMotion(72f, 54, out titleY, out titleSize, out band);  // a full opening pops it in the middle first
            if (band > 0f)
                for (int k = 0; k < 7; k++)  // a dark band behind it, soft at the edges
                    Ui.Fill(new Rect(0f, titleY - 70f + k * 20f, W, 20f), new Color(0f, 0f, 0f, 0.55f * band * Mathf.Sin((k + 0.5f) / 7f * Mathf.PI)));
            Ui.Fancy(new Rect(0f, titleY - 32f - (titleSize - 54) * 0.6f, W, 64f + (titleSize - 54) * 1.2f), title, titleSize, Color.white, theme, TextAnchor.MiddleCenter, new Color(theme.r, theme.g, theme.b, 0.55f));
            int more = me.PendingPicks + me.PendingLegendary + me.PendingMods + me.PrizePicks - 1;
            string sub = me.OfferLegendary ? "PICK A LEGENDARY UPGRADE" : me.OfferMod ? "PICK HOW YOUR SHOTS BEHAVE" : me.OfferPrize ? "RARE OR BETTER  ·  PICK ONE" : "LEVEL " + me.Level + "  ·  PICK ONE";
            if (more > 0)
                sub += "  ·  " + more + " MORE AFTER THIS";
            if (titleY <= 73f)
                Ui.Label(new Rect(0f, 102f, W, 20f), sub, 14, Ui.Muted, TextAnchor.MiddleCenter, true);

            bool grid = me.Offer.Length > 3;
            if (pickCardRects.Length != me.Offer.Length)
                pickCardRects = new Rect[me.Offer.Length];  // where each card is, for the outro when one is taken
            float by;
            V2 home = PickHome(me);
            var slots = new Rect[me.Offer.Length];  // where each card goes (the opening flies them there from you)
            if (grid)
            {
                const int perRow = 4;
                float areaW = cardW * 3 + gap * 2, gw = 18f, cw = (areaW - gw * (perRow - 1)) / perRow, ch = 150f, top = 140f;
                for (int i = 0; i < me.Offer.Length; i++)
                {
                    int row = i / perRow, col = i % perRow, inRow = Math.Min(perRow, me.Offer.Length - row * perRow);
                    float rowX = mid - (inRow * cw + (inRow - 1) * gw) / 2f;
                    slots[i] = new Rect(rowX + col * (cw + gw), top + row * (ch + gw), cw, ch);
                }
            }
            else
            {
                float x0 = mid - (me.Offer.Length * cardW + (me.Offer.Length - 1) * gap) / 2f;
                for (int i = 0; i < me.Offer.Length; i++)
                    slots[i] = new Rect(x0 + i * (cardW + gap), 150f, cardW, cardH);
            }
            DrawPickIntroUnder(me, slots, me.Offer.Length);  // the pillar out of you, and streaks behind the cards (PickIntro.cs)
            if (grid)
            {
                // Weapon mods: every one you can take, in a grid of smaller cards.
                const int perRow = 4;
                float areaW = cardW * 3 + gap * 2, gw = 18f, cw = (areaW - gw * (perRow - 1)) / perRow, ch = 150f, top = 140f;
                int rows = (me.Offer.Length + perRow - 1) / perRow;
                for (int i = 0; i < me.Offer.Length; i++)
                {
                    var u = Upgrades.All[me.Offer[i]];
                    int row = i / perRow, col = i % perRow, inRow = Math.Min(perRow, me.Offer.Length - row * perRow);
                    float rowX = mid - (inRow * cw + (inRow - 1) * gw) / 2f;
                    var slot = slots[i];
                    pickCardRects[i] = slot;
                    bool hover = !opening && slot.Contains(Event.current.mousePosition), selected = selectedCard == i;
                    float a = IntroCardsAt + Mathf.Min(i, 5) * IntroCardGap, f = EaseOut(Seg(IntroElapsed, a, a + IntroCardFly));
                    if (f > 0f)
                        DrawCardInFlight(home, slot, i, f, Mathf.Min(1f, f * 3f), () => DrawUpgradeCard(me, i, u, new Rect(slot.x, slot.y - (selected ? 8f : hover ? 3f : 0f), slot.width, slot.height), true, selected, hover, now));
                    if (!opening && Ui.HitArea(slot) && selectedCard != i)
                    {
                        selectedCard = i;
                        pickKey = 0;
                    }
                }
                by = top + rows * ch + (rows - 1) * gw + 20f;
            }
            else
            {
                const float top = 150f;
                float x0 = mid - (me.Offer.Length * cardW + (me.Offer.Length - 1) * gap) / 2f;
                for (int i = 0; i < me.Offer.Length; i++)
                {
                    var u = Upgrades.All[me.Offer[i]];
                    var slot = slots[i];
                    pickCardRects[i] = slot;
                    bool hover = !opening && slot.Contains(Event.current.mousePosition), selected = selectedCard == i;
                    float a = IntroCardsAt + Mathf.Min(i, 5) * IntroCardGap, f = EaseOut(Seg(IntroElapsed, a, a + IntroCardFly));
                    if (f > 0f)
                        DrawCardInFlight(home, slot, i, f, Mathf.Min(1f, f * 3f), () => DrawTrophyCard(me, i, u, slot, selected, selectedCard >= 0 && !selected, hover, now));
                    if (!opening && Ui.HitArea(slot) && selectedCard != i)
                    {
                        selectedCard = i;
                        pickKey = 0;
                    }
                }
                by = top + cardH + 14f;
            }
            DrawPickIntroOver(me, slots, me.Offer.Length);  // a ring as each card lands (PickIntro.cs)

            // The bottom row rises from below as the last cards land, like the HUD does when a run launches.
            float rowAlpha, rise = PickButtonsRise(me, out rowAlpha);
            var oldRowColor = GUI.color;
            Ui.Alpha = rowAlpha;
            GUI.color = new Color(oldRowColor.r, oldRowColor.g, oldRowColor.b, oldRowColor.a * rowAlpha);
            by += rise;
            if (rowAlpha <= 0.01f)
            {
                Ui.Alpha = 1f;
                GUI.color = oldRowColor;
                return;  // not risen yet
            }

            // What the pick changes, in one line (Hold Tab for everything).
            string change = PickChangeLine(me);
            if (change.Length > 0)
            {
                Ui.Label(new Rect(0f, by, W, 20f), change, 14, new Color(0.5f, 1f, 0.58f), TextAnchor.MiddleCenter, true);
                by += 28f;
            }

            // A card that grants a new ability: choose which key it goes on.
            if (selectedCard >= 0 && selectedCard < me.Offer.Length && Upgrades.All[me.Offer[selectedCard]].Grants != AbilityId.None)
            {
                var grant = Upgrades.All[me.Offer[selectedCard]].Grants;
                if (pickKey < 1 || pickKey > Upgrades.BonusSlots)
                {
                    pickKey = 1;  // default: the first free key
                    for (int k = 1; k <= Upgrades.BonusSlots; k++)
                        if (me.Slots[k].Id == AbilityId.None) { pickKey = k; break; }
                }
                const float keyW = 190f;
                float kx = mid - (keyW * 3 + 16f) / 2f;
                Ui.Label(new Rect(0f, by - 4f, W, 18f), "PUT " + Upgrades.Ability(grant).Name.ToUpperInvariant() + " ON", 12, Ui.Muted, TextAnchor.MiddleCenter, true);
                for (int k = 1; k <= Upgrades.BonusSlots; k++)
                {
                    var on = me.Slots[k].Id;
                    string label = SlotKeys[k] + (on != AbilityId.None ? "  ·  swap " + Upgrades.Ability(on).Name : "  ·  empty");
                    if (Ui.Choice(new Rect(kx + (k - 1) * (keyW + 8f), by + 16f, keyW, 32f), label, pickKey == k, ToColor(me.Def.Color)))
                        pickKey = k;
                }
                by += 60f;
            }

            // Reroll and take, the lobby's fancy buttons: the take button names your pick.
            by = Mathf.Min(by, H - 70f);
            bool wasEnabled = GUI.enabled;
            bool picked = selectedCard >= 0 && selectedCard < me.Offer.Length;
            bool canReroll = !me.OfferLegendary && !me.OfferMod && !me.OfferPrize;  // (nor a challenge room's prize)  // boss rewards can't be rerolled: no button
            const float rerollW = 240f, takeW = 320f, bgap = 18f;
            float bx = canReroll ? mid - (rerollW + bgap + takeW) / 2f : mid - takeW / 2f - rerollW - bgap;
            GUI.enabled = me.Rerolls > 0 && !game.RerollPending && !RerollLocked;  // (not off while opening: GUI.enabled draws at half opacity)
            if (canReroll && Ui.FancyButton(new Rect(bx, by, rerollW, 48f), "Reroll  " + me.Rerolls + "  [" + Keybinds.KeyName(Keybinds.Get(KeyAction.Reroll)) + "]", Ui.Neutral, PixelArt.Get("ui_arrow"), now) && !opening)
                game.RerollOffer();
            GUI.enabled = picked && !game.RerollPending;
            string take = picked ? "Take " + Upgrades.All[me.Offer[selectedCard]].Name : "Pick a card";
            if (Ui.FancyButton(new Rect(bx + rerollW + bgap, by, takeW, 48f), take, picked ? Ui.GoldButton : Ui.Neutral, PixelArt.Get("ui_check"), now) && !opening)
                ConfirmPick();
            SyncSelection(me);  // notes when these cards appeared, for the reroll lock (ArenaApp.cs)
            GUI.enabled = wasEnabled;
            string hint = me.OfferMod ? "Every weapon mod you can still take is shown. Each stacks up to 3 times."
                : me.OfferLegendary || me.OfferPrize ? (me.OfferPrize || game.PrizeLegendary(me) ? "Challenge prizes" : "Boss rewards") + " can't be rerolled  ·  Hold Tab for your build"
                : "1 / 2 / 3 or click a card, Enter to take it  ·  Rerolls: 2 to start, +2 a boss  ·  Hold Tab for your build";
            Ui.Label(new Rect(0f, by + 54f, W, 18f), hint, 12, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
            Ui.Alpha = 1f;
            GUI.color = oldRowColor;
        }

        /// "Regeneration 0.25 → 3.25 HP/s · Damage 100% → 110%": what the selected card changes (up to three stats),
        /// or the new ability and its key.
        string PickChangeLine(Player me)
        {
            if (selectedCard < 0 || me.Offer == null || selectedCard >= me.Offer.Length)
                return "";
            var u = Upgrades.All[me.Offer[selectedCard]];
            var now = StatSheet.Describe(me, me.Taken, 0, game.PassiveRegen);
            var after = StatSheet.Describe(me, new List<int>(me.Taken) { me.Offer[selectedCard] }, pickKey, game.PassiveRegen);
            var parts = new List<string>();
            string grant = u.Grants != AbilityId.None ? Upgrades.Ability(u.Grants).Name : null;
            foreach (var line in after)
            {
                if (line.Header || parts.Count >= 3 || grant != null && (line.Value.Contains(grant) || line.Label.Contains(grant)))
                    continue;  // (a new ability is named once, below)
                string old = null;
                foreach (var n in now)
                    if (!n.Header && n.Label == line.Label)
                        old = n.Value;
                if (old == line.Value)
                    continue;
                parts.Add(old != null ? line.Label + "  " + old + " → " + line.Value : line.Label + "  " + line.Value);
            }
            if (grant != null)
                parts.Insert(0, "New ability: " + grant + (pickKey >= 1 ? " on " + SlotKeys[pickKey] : ""));
            return parts.Count == 0 ? "" : "THIS PICK   " + string.Join("   ·   ", parts.ToArray());
        }

        static Texture2D hatchTex;

        /// A diagonal hatch for the cards' art (drawn tinted and stretched, so its stripes are chunky pixels).
        static Texture2D Hatch()
        {
            if (hatchTex != null)
                return hatchTex;
            const int w = 68, h = 44;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = (x + y) % 6 < 2 ? Color.white : new Color(1f, 1f, 1f, 0f);
            hatchTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            hatchTex.SetPixels(px);
            hatchTex.Apply();
            return hatchTex;
        }

        /// A tall "trophy" card: the icon big on a glowing, hatched rarity backdrop, its rarity and key, the name in
        /// outlined lettering, what it does, a note, and a rarity line along the bottom that fades at both ends.
        /// Picked: it lifts, glows and its icon bobs. Another one picked: it steps back and dims. Rare and better shine.
        void DrawTrophyCard(Player me, int index, UpgradeDef u, Rect slot, bool selected, bool behind, bool hover, float now)
        {
            Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
            float lift = selected ? -16f : behind ? 8f : hover ? -4f : 0f;
            var r = new Rect(slot.x, slot.y + lift, slot.width, slot.height);
            if (selected)  // a soft glow round the picked card
                for (int k = 3; k >= 1; k--)
                    Ui.Pill(new Rect(r.x - 5f * k, r.y - 5f * k, r.width + 10f * k, r.height + 10f * k),
                        new Color(rc.r, rc.g, rc.b, (0.07f + 0.03f * Mathf.Sin(now * 4f)) * (4 - k) / 3f));
            Ui.Card(r, rc, selected ? 1f : hover ? 0.7f : 0.4f);

            // The art: a glow of the rarity colour, a pixel hatch, then the icon big.
            var art = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, 176f);
            Ui.Pill(art, new Color(rc.r * 0.12f, rc.g * 0.12f, rc.b * 0.12f, 0.95f));
            for (int k = 0; k < 5; k++)
            {
                float s = 1f - k * 0.17f;
                var g = new Rect(art.center.x - art.width * s / 2f, art.center.y - art.height * s / 2f + 6f, art.width * s, art.height * s);
                Ui.Pill(g, new Color(rc.r, rc.g, rc.b, 0.06f + 0.02f * k + (selected ? 0.02f : 0f)));
            }
            Ui.Icon(art, Hatch(), new Color(rc.r, rc.g, rc.b, selected ? 0.1f : 0.07f));
            var icon = PixelArt.ForUpgrade(u.Name);
            float bob = selected ? 4f * Mathf.Sin(now * 3f) : 0f, isz = 108f;
            Ui.Icon(new Rect(art.center.x - isz / 2f, art.center.y - isz / 2f + 8f + bob, isz, isz), icon);
            if (u.Rarity >= Rarity.Rare)
            {
                // A slow shine across rare and better cards (quicker and brighter on legendaries).
                bool leg = u.Rarity == Rarity.Legendary;
                float sweep = MathX.Fmod(now * (leg ? 0.6f : 0.35f) + index * 0.3f, 1.6f) - 0.3f;
                GUI.BeginGroup(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f));
                Ui.Fill(new Rect(sweep * r.width, 0f, 22f, r.height), new Color(1f, 1f, 1f, leg ? 0.08f : 0.05f));
                Ui.Fill(new Rect(sweep * r.width + 28f, 0f, 7f, r.height), new Color(1f, 1f, 1f, leg ? 0.06f : 0.04f));
                GUI.EndGroup();
            }
            // Rarity chip and key, top corners.
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 22f + rarity.Length * 7.2f;
            var chipR = new Rect(r.x + 12f, r.y + 12f, rw, 20f);
            Ui.Pill(chipR, new Color(rc.r, rc.g, rc.b, 0.9f));
            Ui.Label(chipR, rarity, 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            DrawKeyCap(new Rect(r.xMax - 34f, r.y + 12f, 0f, 20f), (index + 1).ToString(), selected ? rc : new Color(1f, 1f, 1f, 0.6f));

            // Name, description (and what mastering a granted ability adds), a note, the fading rarity line.
            Color nameLow = selected ? Color.Lerp(rc, Color.white, 0.25f) : new Color(0.78f, 0.83f, 0.94f);
            Ui.Fancy(new Rect(r.x + 8f, r.y + 184f, r.width - 16f, 34f), u.Name, u.Name.Length > 16 ? 21 : 25, Color.white, nameLow, TextAnchor.MiddleCenter,
                selected ? (Color?)new Color(rc.r, rc.g, rc.b, 0.6f) : null);
            string desc = Upgrades.DescFor(u, me.Weapon);
            int ds = desc.Length > 110 ? 12 : 14;
            string note = UpgradeNote(me, index, u);
            var body = new Rect(r.x + 16f, r.y + 224f, r.width - 32f, r.height - 224f - 44f);
            if (u.Grants != AbilityId.None)
            {
                DrawDescAndMastery(body, desc, u.Grants, ds, Ui.Text, true);
                note = "";
            }
            else
                Ui.Label(body, desc, ds, selected ? Color.white : Ui.Text, TextAnchor.UpperCenter, false, true);
            if (note.Length > 0)
                Ui.Label(new Rect(r.x, r.yMax - 38f, r.width, 16f), note, 11, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);
            for (int k = 0; k < 24; k++)  // the line along the bottom, fading out at both ends
            {
                float f = Mathf.Sin(MathX.Pi * (k + 0.5f) / 24f);
                Ui.Fill(new Rect(r.x + 8f + (r.width - 16f) * k / 24f, r.yMax - 12f, (r.width - 16f) / 24f + 0.5f, selected ? 4f : 3f),
                    new Color(rc.r, rc.g, rc.b, f * (selected ? 0.95f : 0.55f)));
            }
            if (behind)  // another card is picked: this one steps back
                Ui.Pill(r, new Color(0.01f, 0.012f, 0.03f, 0.32f));
        }

        /// One upgrade card: rarity ribbon, pixel-art icon in a glowing frame, name, description and a note.
        void DrawUpgradeCard(Player me, int index, UpgradeDef u, Rect r, bool compact, bool selected, bool hover, float now)
        {
            Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
            if (selected)  // a soft glow under the selected card
                Ui.Pill(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), new Color(rc.r, rc.g, rc.b, 0.18f + 0.06f * Mathf.Sin(now * 4f)));
            Ui.Card(r, rc, selected ? 1f : hover ? 0.75f : 0.45f);
            // A wash of the rarity color across the top of the card.
            Ui.Pill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, compact ? 30f : 120f), new Color(rc.r, rc.g, rc.b, 0.08f));
            if (u.Rarity == Rarity.Legendary)
            {
                // A shimmer that sweeps across legendary cards.
                float sweep = MathX.Fmod(now * 0.6f + index * 0.3f, 1.6f) - 0.3f;
                GUI.BeginGroup(r);
                Ui.Fill(new Rect(sweep * r.width, 0f, 18f, r.height), new Color(1f, 0.9f, 0.6f, 0.07f));
                Ui.Fill(new Rect(sweep * r.width + 22f, 0f, 6f, r.height), new Color(1f, 0.9f, 0.6f, 0.05f));
                GUI.EndGroup();
            }
            var icon = PixelArt.ForUpgrade(u.Name);
            float bob = selected ? 3f * Mathf.Sin(now * 3f) : 0f;
            string note = UpgradeNote(me, index, u);
            if (compact)
            {
                // Weapon mod grid: icon on the left, name and stacks on the right, description under.
                var frame = new Rect(r.x + 12f, r.y + 14f, 52f, 52f);
                Ui.Pill(frame, new Color(rc.r * 0.3f, rc.g * 0.3f, rc.b * 0.3f, 0.9f));
                Ui.Icon(new Rect(frame.x + 2f, frame.y + 2f + bob * 0.5f, 48f, 48f), icon);
                Ui.Label(new Rect(r.x + 74f, r.y + 14f, r.width - 82f, 16f), note, 10, rc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 74f, r.y + 30f, r.width - 82f, 36f), u.Name, 15, Color.white, TextAnchor.UpperLeft, true, true);
                Ui.Label(new Rect(r.x + 12f, r.y + 74f, r.width - 24f, 52f), Upgrades.DescFor(u, me.Weapon), 11, Ui.Text, TextAnchor.UpperLeft, false, true);
                Ui.Label(new Rect(r.x, r.yMax - 22f, r.width - 12f, 16f), selected ? "SELECTED" : "[" + (index + 1) + "]", 11,
                    selected ? rc : new Color(1, 1, 1, 0.45f), TextAnchor.MiddleRight, true);
                return;
            }
            // Rarity ribbon.
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 24f + rarity.Length * 7.5f;
            var ribbon = new Rect(r.x + r.width / 2f - rw / 2f, r.y + 12f, rw, 20f);
            Ui.Pill(ribbon, new Color(rc.r, rc.g, rc.b, 0.85f));
            Ui.Label(ribbon, rarity, 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            // Icon in a framed, glowing tile.
            var tile = new Rect(r.x + r.width / 2f - 46f, r.y + 42f, 92f, 92f);
            Ui.Pill(new Rect(tile.x - 4f, tile.y - 4f, tile.width + 8f, tile.height + 8f), new Color(rc.r, rc.g, rc.b, selected ? 0.35f : 0.18f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            Ui.Icon(new Rect(tile.x + 10f, tile.y + 10f + bob, 72f, 72f), icon);
            Ui.Label(new Rect(r.x + 10f, r.y + 144f, r.width - 20f, 28f), u.Name, 21, Color.white, TextAnchor.MiddleCenter, true);
            Ui.Fill(new Rect(r.x + 40f, r.y + 176f, r.width - 80f, 1f), new Color(rc.r, rc.g, rc.b, 0.35f));
            string desc = Upgrades.DescFor(u, me.Weapon);
            int ds = desc.Length > 110 ? 12 : 14;
            if (u.Grants != AbilityId.None)
            {
                // A card that gives an ability also says what mastering it adds (it may take the note's line: the key
                // chooser below the cards says the same).
                DrawDescAndMastery(new Rect(r.x + 18f, r.y + 184f, r.width - 36f, r.height - 184f - 36f), desc, u.Grants, ds, Ui.Text, true);
                note = "";
            }
            else
                Ui.Label(new Rect(r.x + 18f, r.y + 184f, r.width - 36f, 66f), desc, ds, Ui.Text, TextAnchor.UpperCenter, false, true);
            if (note.Length > 0)
                Ui.Label(new Rect(r.x, r.yMax - 50f, r.width, 16f), note, 11, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);
            var key = new Rect(r.x + r.width / 2f - 50f, r.yMax - 30f, 100f, 20f);
            Ui.Pill(key, selected ? new Color(rc.r, rc.g, rc.b, 0.85f) : new Color(1f, 1f, 1f, 0.08f));
            Ui.Label(key, selected ? "SELECTED" : "PRESS " + (index + 1), 10, selected ? new Color(0.06f, 0.06f, 0.1f) : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, true);
        }

        /// The small line on a card: where a new ability goes, a role restriction, or how many you have.
        string UpgradeNote(Player me, int index, UpgradeDef u)
        {
            int have = Upgrades.Count(me.Taken, me.Offer[index]);
            if (u.Rarity == Rarity.Mod)
                return have > 0 ? "YOU HAVE " + have + " / " + u.MaxStacks : "NEW";
            if (u.Grants != AbilityId.None)
            {
                string cls = u.ClassAbility ? "Class ability  ·  " : "";
                if (index == selectedCard && pickKey >= 1)
                    return cls + "Goes on " + SlotKeys[pickKey] + " (choose below)";
                return cls + "You choose its key";
            }
            if (u.Role.HasValue)
                return Defs.Get(u.Role.Value).Name + " only";
            return have > 0 ? "You have " + have : "";
        }

        /// Your current stats; with a card selected, the ones it would change show their new value.
        /// shopPreview: an upgrade from the Bazaar's shelf to preview instead of the picker's selection.
        void DrawStatPanel(Player me, Rect r, int shopPreview = -1)
        {
            var now = StatSheet.Describe(me, me.Taken, 0, game.PassiveRegen);
            List<StatLine> after = null;
            if (shopPreview >= 0)
                after = StatSheet.Describe(me, new List<int>(me.Taken) { shopPreview }, 0, game.PassiveRegen);
            else if (selectedCard >= 0 && me.Offer != null && selectedCard < me.Offer.Length)
            {
                var preview = new List<int>(me.Taken) { me.Offer[selectedCard] };
                after = StatSheet.Describe(me, preview, pickKey, game.PassiveRegen);
            }
            var lines = after ?? now;
            var green = new Color(0.45f, 1f, 0.5f);
            float y = r.y;
            for (int i = 0; i < lines.Count && y < r.yMax - 20f; i++)
            {
                var line = lines[i];
                if (line.Header)
                {
                    y += 6f;
                    Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), line.Label.ToUpperInvariant(), 12, new Color(1f, 0.8f, 0.45f), TextAnchor.MiddleLeft, true);
                    y += 20f;
                    continue;
                }
                // Compare with the same row in the current stats to spot what the card changes.
                string old = null;
                foreach (var n in now)
                    if (n.Label == line.Label && !n.Header)
                        old = n.Value;
                bool changed = after != null && old != line.Value;
                Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), line.Label, 13, changed ? green : new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft);
                if (changed && old != null)
                {
                    // old value, an arrow, the new value (right-aligned)
                    float right = r.xMax - 12f, nw = Ui.TextWidth(line.Value, 13, true);
                    Label(new Rect(right - nw - 2f, y, nw + 4f, 18f), line.Value, 13, green, TextAnchor.MiddleRight, true);
                    var arrow = new Rect(right - nw - 18f, y + 3f, 12f, 12f);
                    Ui.Icon(arrow, PixelArt.Get("ui_arrow"), green);
                    Label(new Rect(r.x + 12f, y, arrow.x - 4f - (r.x + 12f), 18f), old, 13, new Color(green.r, green.g, green.b, 0.6f), TextAnchor.MiddleRight);
                }
                else
                    Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), changed ? "NEW " + line.Value : line.Value, 13, changed ? green : Color.white, TextAnchor.MiddleRight, changed);
                y += 20f;
            }
        }

        const float StatsSidebarW = 270f;

        /// YOUR STATS (Tab menu and upgrade picker): a see-through strip down the whole left edge of the screen,
        /// with the title and your weapon and ability at the top. Returns the space below for the stats.
        Rect StatsSidebar(Player me)
        {
            var r = new Rect(0f, 0f, StatsSidebarW, H);
            var rc = ToColor(me.Def.Color);
            Ui.Fill(r, new Color(0.05f, 0.07f, 0.13f, 0.6f));
            Ui.Fill(new Rect(r.xMax - 1f, 0f, 1f, H), new Color(1f, 1f, 1f, 0.22f));
            Ui.Fill(new Rect(r.xMax - 5f, 0f, 4f, H), new Color(rc.r, rc.g, rc.b, 0.18f));
            Ui.Label(new Rect(20f, 20f, r.width - 40f, 24f), "YOUR STATS", 16, Color.white, TextAnchor.MiddleLeft, true);
            Label(new Rect(20f, 44f, r.width - 40f, 18f), me.Weapon.Name + " · " + Upgrades.Ability(me.RoleAbility).Name, 12, rc, TextAnchor.MiddleLeft);
            Ui.Fill(new Rect(20f, 70f, r.width - 40f, 1f), new Color(1f, 1f, 1f, 0.1f));
            return new Rect(8f, 80f, r.width - 20f, H - 92f);
        }
    }
}
