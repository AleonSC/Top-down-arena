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
            pickParts = PickChangeParts(me, selectedCard);  // what the picked card changes, drawn on that card
            if (me.OfferMilestone && me.Offer != null)  // milestone cards always show theirs
            {
                if (milestoneParts == null || milestoneParts.Length != me.Offer.Length)
                    milestoneParts = new List<string>[me.Offer.Length];
                for (int i = 0; i < me.Offer.Length; i++)
                    milestoneParts[i] = PickChangeParts(me, i);
            }
            else
                milestoneParts = null;
            bool opening = PickIntroPlaying(me);  // the cards are still flying out of you: nothing can be chosen yet
            RoundBackdrop(PickerDark * (introFull ? Seg(IntroElapsed, 0f, 0.3f) : 1f));  // (RoundWindows.cs)
            const float cardW = 270f, cardH = 384f, gap = 30f;
            float mid = W / 2f;

            // Heading: a big outlined title in the offer's colour, and a line under it.
            Color theme = me.OfferLegendary ? ToColor(Upgrades.RarityColor(Rarity.Legendary))
                : me.OfferMilestone ? ToColor(Upgrades.RarityColor(Rarity.Milestone))
                : me.OfferMod ? ToColor(Upgrades.RarityColor(Rarity.Mod)) : me.OfferPrize ? ToColor(Upgrades.RarityColor(Rarity.VeryRare)) : Ui.Gold;
            string title = me.OfferLegendary ? (game.PrizeLegendary(me) ? "CHALLENGE PRIZE!" : "BOSS REWARD!") : me.OfferMod ? "WEAPON MOD" : me.OfferMilestone ? "MILESTONE!" : me.OfferPrize ? "CHALLENGE PRIZE!" : "LEVEL UP!";
            float titleY, band;
            int titleSize;
            PickTitleMotion(72f, 54, out titleY, out titleSize, out band);  // a full opening pops it in the middle first
            if (band > 0f)
                for (int k = 0; k < 7; k++)  // a dark band behind it, soft at the edges
                    Ui.Fill(new Rect(0f, titleY - 70f + k * 20f, W, 20f), new Color(0f, 0f, 0f, 0.55f * band * Mathf.Sin((k + 0.5f) / 7f * Mathf.PI)));
            // The CRT title: VT323, glowing in the offer's colour (the lobby's blue for a plain level-up).
            var titleCol = me.OfferLegendary || me.OfferMod || me.OfferMilestone || me.OfferPrize ? theme : CrtUi.Ink;
            var titleR = new Rect(0f, titleY - 32f - (titleSize - 54) * 0.6f, W, 64f + (titleSize - 54) * 1.2f);
            if (Event.current.type == EventType.Repaint && !fxReduced)
                Ui.Glow(new Rect(W / 2f - CrtUi.TitleWidth(title, (int)(titleSize * 1.3f)) / 2f, titleR.center.y - titleSize * 0.35f, CrtUi.TitleWidth(title, (int)(titleSize * 1.3f)), titleSize * 0.7f), titleCol, 0.16f);
            CrtUi.Title(titleR, title, (int)(titleSize * 1.3f), Color.Lerp(titleCol, Color.white, 0.8f), TextAnchor.MiddleCenter, fxReduced);
            int more = me.PendingPicks + me.PendingLegendary + me.PendingMods + me.PendingMilestones + me.PrizePicks - 1;
            string sub = me.OfferLegendary ? "PICK A LEGENDARY UPGRADE" : me.OfferMod ? "PICK HOW YOUR SHOTS BEHAVE" : me.OfferMilestone ? "LEVEL " + me.Level + "  ·  PICK ONE MILESTONE UPGRADE" : me.OfferPrize ? "RARE OR BETTER  ·  PICK ONE" : "LEVEL " + me.Level + "  ·  PICK ONE";
            if (more > 0)
                sub += "  ·  " + more + " MORE AFTER THIS";
            if (titleY <= 73f)
                LobbyKit.Spaced(new Rect(0f, 102f, W, 20f), sub, 14, LobbyKit.HeadCol, TextAnchor.MiddleCenter, 3f);

            bool grid = me.OfferMod;  // weapon mods: a grid of small cards (Wide Choice's 4th and 5th level-up cards are tall ones, narrower)
            float tw = me.Offer.Length <= 4 ? cardW : 224f, tg = me.Offer.Length <= 4 ? 40f : 30f;  // (the mod grid keeps `gap`)
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
                float x0 = mid - (me.Offer.Length * tw + (me.Offer.Length - 1) * tg) / 2f;
                for (int i = 0; i < me.Offer.Length; i++)
                    slots[i] = new Rect(x0 + i * (tw + tg), 150f, tw, cardH);
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
                by = top + cardH + 28f;
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

            // A card that grants a new ability: choose which key it goes on. The key switch sits between Reroll and Choose;
            // the two buttons spring apart to make room for it (and back) so nothing below the cards moves down.
            bool grantCard = selectedCard >= 0 && selectedCard < me.Offer.Length && Upgrades.All[me.Offer[selectedCard]].Grants != AbilityId.None;
            if (grantCard)
            {
                if (pickKey < 1 || pickKey > Upgrades.BonusSlots)
                {
                    pickKey = 1;  // default: the first free key
                    for (int k = 1; k <= Upgrades.BonusSlots; k++)
                        if (me.Slots[k].Id == AbilityId.None) { pickKey = k; break; }
                }
            }
            StepKeySwitch(grantCard);

            // Reroll and choose, the lobby's fancy buttons. Reroll takes a second click to confirm: the first arms it (the
            // button turns orange and a bar drains over RerollArmTime), and the button never changes size, so both stay put.
            bool goldOffer = game.CanGoldInstead(me);  // level-up offers where a shop will take gold: a slim key under the row
            by = Mathf.Min(by, H - (goldOffer ? 120f : 70f));
            bool wasEnabled = GUI.enabled;
            bool picked = selectedCard >= 0 && selectedCard < me.Offer.Length;
            bool canReroll = !me.OfferLegendary && !me.OfferMod && !me.OfferMilestone && !me.OfferPrize;  // (nor a challenge room's prize)  // boss rewards can't be rerolled: no button
            const float rerollW = 300f, takeW = 300f, bgap = 18f, keysW = 230f;
            float bx = canReroll ? mid - (rerollW + bgap + takeW) / 2f : mid - takeW / 2f - rerollW - bgap;
            float spread = keySwitch * (keysW + bgap * 2f) / 2f;  // with Reroll: each button moves half the switch and its gaps outward
            float chooseX = bx + rerollW + bgap + (canReroll ? spread : keySwitch * (keysW + bgap) / 2f);  // (with no Reroll, Choose just moves right of the switch)
            bx -= spread;
            GUI.enabled = me.Rerolls > 0 && !game.RerollPending && !RerollLocked;  // (not off while opening: GUI.enabled draws at half opacity)
            if (!GUI.enabled)
                rerollArmedAt = -99f;
            bool armed = RerollArmed;
            var rerollR = new Rect(bx, by, rerollW, 48f);
            if (canReroll)
            {
                // The lobby's digital keys: Reroll is a plain key (orange and lit once armed), Choose is the dashed locked key
                // until a card is picked, then lit with a lamp.
                if (LobbyKit.Key(rerollR, armed ? "CLICK AGAIN TO REROLL" : "REROLL", armed, fxReduced, PixelArt.Get("ui_arrow"), size: 22, minSize: 14, accent: armed ? RerollArmedTint : (Color?)null,
                    locked: me.Rerolls <= 0 && !armed) && !opening)
                    RerollPress();
                if (armed)
                    Ui.Fill(new Rect(rerollR.x + 12f, rerollR.yMax - 7f, (rerollR.width - 24f) * (1f - (now - rerollArmedAt) / RerollArmTime), 3f), RerollArmedTint);
                else
                    Ui.Label(new Rect(rerollR.xMax - 66f, rerollR.y, 50f, rerollR.height), "×" + me.Rerolls, 15, new Color(1f, 1f, 1f, GUI.enabled ? 0.85f : 0.4f), TextAnchor.MiddleRight, true);
            }
            GUI.enabled = picked && !game.RerollPending;
            var chooseR = new Rect(chooseX, by, takeW, 48f);
            if (LobbyKit.Key(chooseR, "CHOOSE UPGRADE", false, fxReduced, PixelArt.Get("ui_check"), size: 22, minSize: 14, locked: !picked) && !opening)
                ConfirmPick();
            if (picked)  // the lamp: lit once there is something to choose
            {
                var lamp = new Rect(chooseR.x + 12f, chooseR.center.y - 5f, 10f, 10f);
                Ui.Glow(lamp, new Color(0.24f, 1f, 0.54f), 0.35f);
                Ui.Fill(lamp, new Color(0.24f, 1f, 0.54f));
            }
            if (goldOffer)
            {
                GUI.enabled = !game.RerollPending;
                var goldR = new Rect(mid - takeW / 2f, by + 48f + 10f, takeW, 38f);
                int amount = Game.GoldInsteadAmount(game.Wave);
                if (LobbyKit.Key(goldR, "GOLD INSTEAD  +" + amount, false, fxReduced, PixelArt.Get("ui_coin"), size: 17, minSize: 12, accent: Ui.Gold) && !opening)
                    game.TakeGoldInstead();
            }
            DrawKeySwitch(me, by, canReroll ? mid : chooseX - bgap - keysW / 2f, keysW, rowAlpha, opening);
            SyncSelection(me);  // notes when these cards appeared, for the reroll lock (ArenaApp.cs)
            GUI.enabled = wasEnabled;
            Ui.Alpha = 1f;
            GUI.color = oldRowColor;
        }

        float keySwitch, keySwitchVel;              // 0 = no key switch, 1 = out between the buttons (a spring, so it overshoots a little)
        float keySwitchAt = -1f;                    // unscaled time of the last step

        /// Moves the key switch's spring toward out (1) or away (0). A spring keeps its speed when the target flips, so
        /// clicking between cards mid-slide turns the buttons round without a jump. Steps once per frame (repaint only).
        void StepKeySwitch(bool out_)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            float now = Time.unscaledTime, dt = keySwitchAt < 0f ? 0f : Mathf.Min(now - keySwitchAt, 0.033f);
            keySwitchAt = now;
            float target = out_ ? 1f : 0f;
            keySwitchVel += (target - keySwitch) * 900f * dt;
            keySwitchVel *= Mathf.Exp(-33f * dt);
            keySwitch += keySwitchVel * dt;
            if (Mathf.Abs(target - keySwitch) < 0.001f && Mathf.Abs(keySwitchVel) < 0.01f)
            {
                keySwitch = target;
                keySwitchVel = 0f;
            }
        }

        /// Q / E / SHIFT between Reroll and Choose: it fades and scales up as the buttons spring apart.
        void DrawKeySwitch(Player me, float by, float mid, float w, float rowAlpha, bool opening)
        {
            if (keySwitch < 0.02f || selectedCard < 0 || selectedCard >= me.Offer.Length)
                return;
            var grant = Upgrades.All[me.Offer[selectedCard]].Grants;
            if (grant == AbilityId.None)
                return;  // (still sliding away after the card changed: nothing to name)
            float a = Mathf.Clamp01(keySwitch * 1.5f) * rowAlpha;
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = a;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, a);
            float sc = Mathf.Lerp(0.6f, 1f, keySwitch);
            GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), new Vector2(mid, by + 24f));
            Ui.Label(new Rect(mid - w / 2f - 20f, by - 22f, w + 40f, 18f), Upgrades.Ability(grant).Name.ToUpperInvariant() + " ON", 11, Ui.Gold, TextAnchor.MiddleCenter, true);
            float kw = w / Upgrades.BonusSlots;
            for (int k = 1; k <= Upgrades.BonusSlots; k++)
            {
                var r = new Rect(mid - w / 2f + (k - 1) * kw, by, kw, 48f);
                string hint = me.Slots[k].Id != AbilityId.None ? "swap" : "empty";
                if (DkChoice(new Rect(r.x, r.y, r.width, r.height - 14f), SlotKeys[k], pickKey == k, ToColor(me.Def.Color)) && !opening)
                    pickKey = k;
                Ui.Label(new Rect(r.x, r.yMax - 14f, r.width, 14f), hint, 10, CrtStyle.Desc, TextAnchor.MiddleCenter);
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
            Ui.Alpha = oldAlpha;
        }

        float rerollArmedAt = -99f;                 // when Reroll was clicked once (unscaled time), -99 = not armed
        const float RerollArmTime = 3f;             // seconds the second click has to come in
        static readonly Color RerollArmedTint = new Color(0.92f, 0.5f, 0.14f);
        bool RerollArmed => Time.unscaledTime - rerollArmedAt < RerollArmTime;
        List<string> pickParts;                     // what the selected card changes, set each frame by the picker
        List<string>[] milestoneParts;              // what each milestone card changes (they always show it)

        /// Reroll (button or R): the first press arms it, a second press within RerollArmTime rerolls.
        void RerollPress()
        {
            var me = game.Me;
            if (me == null || me.Rerolls <= 0 || game.RerollPending || RerollLocked)
                return;
            if (RerollArmed)
            {
                rerollArmedAt = -99f;
                game.RerollOffer();
            }
            else
                rerollArmedAt = Time.unscaledTime;
        }

        /// "Regeneration  0.25 → 3.25 HP/s": what the selected card changes (up to three stats), or the new ability and
        /// its key. Null with nothing selected. Drawn on the picked card itself.
        List<string> PickChangeParts(Player me, int index)
        {
            if (index < 0 || me.Offer == null || index >= me.Offer.Length)
                return null;
            return ChangeParts(me, me.Offer[index], pickKey, pickKey >= 1 && index == selectedCard);
        }

        /// Your stat sheet (StatSheet.Describe), with one more upgrade taken when extra >= 0. The picker, the shop and the
        /// Tab screen ask for the same sheets every frame (twice per card), so they are kept for a quarter second.
        /// The lists are shared: read them, don't change them.
        List<StatLine> Stats(Player me, int extra, int key)
        {
            float t = Time.unscaledTime;
            if (t - statsAt > 0.25f)
            {
                statsCache.Clear();
                statsAt = t;
            }
            long k = (((long)me.Id * 4096 + me.Taken.Count) * 4096 + (extra + 1)) * 64 + key;
            List<StatLine> lines;
            if (statsCache.TryGetValue(k, out lines))
                return lines;
            List<int> taken = me.Taken;
            if (extra >= 0)
            {
                statsTaken.Clear();
                statsTaken.AddRange(me.Taken);
                statsTaken.Add(extra);
                taken = statsTaken;
            }
            lines = StatSheet.Describe(me, taken, key, game.PassiveRegen);
            statsCache[k] = lines;
            return lines;
        }
        readonly Dictionary<long, List<StatLine>> statsCache = new Dictionary<long, List<StatLine>>();
        readonly List<int> statsTaken = new List<int>();
        float statsAt = float.NegativeInfinity;

        /// The same for any upgrade (the Bazaar's shelf cards use it too).
        List<string> ChangeParts(Player me, int upgrade, int key, bool showKey)
        {
            var u = Upgrades.All[upgrade];
            var now = Stats(me, -1, 0);
            var after = Stats(me, upgrade, key);
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
                if (old == null)
                    old = StatSheet.Default(line.Label);  // a milestone stat you have none of yet
                if (old == line.Value)
                    continue;
                parts.Add(old != null ? line.Label + "  " + old + " → " + line.Value : line.Label + "  " + line.Value);
            }
            if (grant != null)
                parts.Insert(0, "New ability: " + grant + (showKey ? " on " + SlotKeys[key] : ""));
            return parts.Count == 0 ? null : parts;
        }

        /// The colour a card is drawn in: its rarity colour (Common is gray and never takes the CRT colour setting).
        static Color CrtCol(UpgradeDef u) => ToColor(Upgrades.RarityColor(u.Rarity));

        /// A thin outline, w px thick.
        static void CrtEdge(Rect r, float w, Color c)
        {
            Ui.Fill(new Rect(r.x, r.y, r.width, w), c);
            Ui.Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Ui.Fill(new Rect(r.x, r.y, w, r.height), c);
            Ui.Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        /// An upgrade card in the lobby's digital CRT look (Docs/Mockups/crt-level-up-blue.html): a holographic screen in the
        /// card's rarity colour (DrawHolo), a thin outline (brighter picked or hovered), blue corner brackets and a soft glow that grows on
        /// the picked card.
        void DrawCrtCardFrame(Rect r, Color c, bool selected, bool hover, bool behind, float now)
        {
            float glow = selected ? 0.55f + 0.1f * Mathf.Sin(now * 4f) : behind ? 0.08f : hover ? 0.32f : 0.2f;
            Ui.Glow(r, c, glow);
            if (selected)
                Ui.Glow(r, c, 0.3f);
            DrawHolo(r, c, now);
            var edge = selected ? Color.Lerp(c, Color.white, 0.55f) : new Color(c.r, c.g, c.b, hover ? 0.9f : 0.5f);
            CrtEdge(r, selected ? 2f : 1f, edge);
            const float arm = 18f, th = 3f;
            var b = new Color(c.r, c.g, c.b, behind ? 0.35f : 1f);
            Ui.Fill(new Rect(r.x, r.y, arm, th), b); Ui.Fill(new Rect(r.x, r.y, th, arm), b);
            Ui.Fill(new Rect(r.xMax - arm, r.yMax - th, arm, th), b); Ui.Fill(new Rect(r.xMax - th, r.yMax - arm, th, arm), b);
        }

        /// The card's holographic body (Docs/Mockups/holo-level-up-cards.html, "3 Projection"): rarity-coloured glass fading to
        /// the dark screen colour (CardArt.Holo), a scanline every 4 px that dips now and then, a bright band drifting down
        /// every 3.4 s, and a magenta fringe on the left edge and a cyan one on the right. Reduced effects keeps the glass and
        /// the lines, still.
        void DrawHolo(Rect r, Color c, float now)
        {
            Ui.Picture(r, CardArt.Holo(c));
            float t = MathX.Fmod(now, 5f) / 5f;
            bool dip = !fxReduced && ((t >= 0.94f && t < 0.96f) || t >= 0.98f);  // the mockup's flicker: two short dips every 5 s
            var line = new Color(c.r, c.g, c.b, (dip ? 0.32f : 0.5f) * 0.4f);
            for (float y = Mathf.Round(r.yMax) - 1f; y >= r.y; y -= 4f)  // counted up from the bottom edge, like the mockup
                Ui.Fill(new Rect(r.x, y, r.width, 1f), line);
            if (!fxReduced)
            {
                float h = r.height, p = MathX.Fmod(now, 3.4f) / 3.4f;
                float by = Mathf.Lerp(-0.288f * h, 1.152f * h, p);
                var bc = Color.Lerp(c, Color.white, 0.3f);
                var old = GUI.color;
                GUI.BeginGroup(r);
                GUI.color = Ui.Faded(new Color(bc.r, bc.g, bc.b, 0.5f));
                GUI.DrawTexture(new Rect(0f, by, r.width, 0.28f * h), CardArt.Band());
                GUI.EndGroup();
                GUI.color = old;
            }
            Ui.Fill(new Rect(r.x, r.y, 2f, r.height), new Color(1f, 60f / 255f, 200f / 255f, 0.35f));
            Ui.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), new Color(60f / 255f, 240f / 255f, 1f, 0.35f));
        }

        /// A small key-like label (the rarity): outlined and faintly lit, inverse video on the picked card.
        void DrawCrtChip(Rect r, string text, Color c, bool inverse)
        {
            Ui.Fill(r, inverse ? c : new Color(c.r, c.g, c.b, 0.08f));
            CrtEdge(r, 1f, c);
            if (inverse)
                Ui.Label(r, text, 10, CrtUi.Bg, TextAnchor.MiddleCenter, true);
            else
                CrtUi.Text(r, text, 10, c, TextAnchor.MiddleCenter, true, fxReduced);
        }

        /// The outlined, faintly lit square an icon sits in (the icon itself keeps its colours).
        void DrawCrtTile(Rect r, Color c, bool selected)
        {
            if (selected)
                Ui.Glow(r, c, 0.4f);
            Ui.Fill(r, new Color(0.01f, 0.02f, 0.05f, 1f));
            Ui.Fill(r, new Color(c.r, c.g, c.b, 0.07f));
            CrtEdge(r, 2f, new Color(c.r, c.g, c.b, selected ? 1f : 0.6f));
        }

        /// Gold corner brackets round a card (the picked milestone).
        static void DrawBrackets(Rect r, Color c)
        {
            const float len = 24f, th = 3f, o = 7f;
            float x0 = r.x - o, y0 = r.y - o, x1 = r.xMax + o, y1 = r.yMax + o;
            Ui.Fill(new Rect(x0, y0, len, th), c); Ui.Fill(new Rect(x0, y0, th, len), c);
            Ui.Fill(new Rect(x1 - len, y0, len, th), c); Ui.Fill(new Rect(x1 - th, y0, th, len), c);
            Ui.Fill(new Rect(x0, y1 - th, len, th), c); Ui.Fill(new Rect(x0, y1 - len, th, len), c);
            Ui.Fill(new Rect(x1 - len, y1 - th, len, th), c); Ui.Fill(new Rect(x1 - th, y1 - len, th, len), c);
        }

        static readonly Color PickGo = new Color(125f / 255f, 1f, 160f / 255f);  // #7dffa0
        static readonly Color PickDim = new Color(63f / 255f, 122f / 255f, 82f / 255f);  // #3f7a52

        /// The mockup's blink (steps(2) between 100% and 15%): four steps of a quarter cycle each.
        static float PickBlink(float now, float cycle)
        {
            switch ((int)(MathX.Fmod(now, cycle) / (cycle / 4f)))
            {
                case 0: return 1f;
                case 2: return 0.15f;
                default: return 0.575f;
            }
        }

        /// A small pixel triangle pointing right (the mockup's ▸): three columns of 2 px, 6, 4 and 2 px tall.
        static void PickArrow(float x, float cy, Color c)
        {
            for (int i = 0; i < 3; i++)
                Ui.Fill(new Rect(x + i * 2f, cy - (3 - i), 2f, (3 - i) * 2f), c);
        }

        /// The picked card's change lines (up to two) as a green phosphor readout (Docs/Mockups/holo-level-up-cards.html, "A
        /// readout"): THIS PICK with a blinking square, then per line the stat in the CRT font on the left and old ▸▸ new on
        /// the right, and a row of 8 segments for the stacks you hold with the next one blinking. The panel sits with its
        /// bottom on the strip's bottom and grows upward. stacks: how many you hold (-1: all segments off).
        void DrawPickStrip(Rect strip, int size, bool label, List<string> parts = null, int stacks = -1, bool segments = true)
        {
            if (parts == null)
                parts = pickParts;
            int lines = Math.Min(2, parts.Count);
            float now = Time.unscaledTime;
            const float pad = 9f, rowH = 17f;
            float h = (segments ? 25.5f : 14f) + (label ? 11f : 0f) + lines * (rowH + 3.2f);  // top 6.4, label, gap 3.2, rows, gap 5, segments 6.4, bottom 7.7
            var r = new Rect(strip.x, strip.yMax - h, strip.width, h);
            var go = PickGo;
            Ui.Fill(r, new Color(2f / 255f, 10f / 255f, 8f / 255f, 0.82f));
            for (int k = 0; k < 4; k++)  // the faint inner glow
                CrtEdge(new Rect(r.x + k * 3f, r.y + k * 3f, r.width - k * 6f, r.height - k * 6f), 3f, new Color(go.r, go.g, go.b, 0.045f - k * 0.011f));
            CrtEdge(r, 1f, new Color(go.r, go.g, go.b, 0.55f));
            const float arm = 6.4f;  // brackets: top left and bottom right
            Ui.Fill(new Rect(r.x, r.y, arm, 2f), go); Ui.Fill(new Rect(r.x, r.y, 2f, arm), go);
            Ui.Fill(new Rect(r.xMax - arm, r.yMax - 2f, arm, 2f), go); Ui.Fill(new Rect(r.xMax - 2f, r.yMax - arm, 2f, arm), go);
            float y = r.y + 6.4f;
            if (label)
            {
                PickArrow(r.x + pad, y + 5.5f, new Color(0.31f, 0.65f, 0.416f));
                Ui.Label(new Rect(r.x + pad + 14f, y, 90f, 11f), "THIS PICK", 9, new Color(0.31f, 0.65f, 0.416f), TextAnchor.MiddleLeft, false);
                float bl = fxReduced ? 1f : PickBlink(now, 1.1f);
                Ui.Fill(new Rect(r.xMax - pad - 6f, y + 2.5f, 6f, 6f), new Color(go.r, go.g, go.b, bl));
                y += 11f;
            }
            y += 3.2f;
            float avail = r.width - pad * 2f;
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            for (int i = 0; i < lines; i++, y += rowH + 3.2f)
            {
                string part = parts[i], left = part, oldV = null, newV = null;
                int sp = part.IndexOf("  ", StringComparison.Ordinal);
                if (sp > 0)
                {
                    left = part.Substring(0, sp).ToUpperInvariant();
                    string rest = part.Substring(sp + 2).Trim();
                    int arrow = rest.IndexOf(" → ", StringComparison.Ordinal);
                    if (arrow >= 0) { oldV = rest.Substring(0, arrow); newV = rest.Substring(arrow + 3); }
                    else newV = rest;
                }
                int fs = 17;
                float aw = oldV != null ? 16f : 0f;
                while (fs > 11 && Ui.TextWidth(left, fs) + 12f + (oldV != null ? Ui.TextWidth(oldV, fs) + aw : 0f) + (newV != null ? Ui.TextWidth(newV, fs) : 0f) > avail)
                    fs--;
                var row = new Rect(r.x + pad, y, avail, rowH);
                CrtUi.Text(row, left, fs, go, TextAnchor.MiddleLeft, false, fxReduced);
                if (newV != null)
                {
                    float nw = Ui.TextWidth(newV, fs), x = row.xMax - nw;
                    CrtUi.Text(new Rect(x, y, nw + 2f, rowH), newV, fs, go, TextAnchor.MiddleLeft, false, fxReduced);
                    if (oldV != null)
                    {
                        var arr = new Color(go.r, go.g, go.b, 1f);
                        PickArrow(x - 7f, y + rowH / 2f, arr);
                        PickArrow(x - 14f, y + rowH / 2f, arr);
                        float ow = Ui.TextWidth(oldV, fs);
                        CrtUi.Text(new Rect(x - 20f - ow, y, ow + 2f, rowH), oldV, fs, PickDim, TextAnchor.MiddleLeft, false, true);
                    }
                }
            }
            Ui.CrtFont = was;
            if (!segments)
                return;
            y += 5f - 3.2f;
            const int segs = 8;
            const float gap = 2.56f, sh = 6.4f;
            float sw = (avail - gap * (segs - 1)) / segs;
            int have = Math.Max(0, stacks), first = have / segs * segs;  // a window of 8: the stacks held, then the next one
            for (int k = 0; k < segs; k++)
            {
                var seg = new Rect(r.x + pad + k * (sw + gap), y, sw, sh);
                int n = first + k;
                if (stacks >= 0 && n < have)
                {
                    Ui.Fill(new Rect(seg.x - 2f, seg.y - 2f, seg.width + 4f, seg.height + 4f), new Color(go.r, go.g, go.b, 0.18f));
                    Ui.Fill(seg, go);
                }
                else if (stacks >= 0 && n == have)
                    Ui.Fill(seg, new Color(go.r, go.g, go.b, fxReduced ? 1f : PickBlink(now, 0.8f)));
                else
                    Ui.Fill(seg, new Color(16f / 255f, 48f / 255f, 28f / 255f));
            }
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

        /// A tall card in the milestone cards' layout: the rarity chip centred on top, the icon in a cutout tile, the name in
        /// outlined lettering and what it does. Upgrades with a stack cap get a row of circles that fill with how many you
        /// hold (the next one is ringed on the picked card, with what the pick changes under it); the others show the change
        /// in a strip on the card. A soft glow in the rarity colour and a slow bob. Picked: it lifts and glows about twice
        /// as strong. Another one picked: it steps back and dims. Rare and better shine.
        void DrawTrophyCard(Player me, int index, UpgradeDef u, Rect slot, bool selected, bool behind, bool hover, float now)
        {
            Color rc = CrtCol(u);
            float lift = selected ? -16f : behind ? 8f : hover ? -4f : 0f;
            float bob = behind ? 0f : Mathf.Sin(now * 1.85f + index * 2.1f) * 2.5f;  // 5 px top to bottom, each card out of step
            var r = new Rect(slot.x, slot.y + lift + bob, slot.width, slot.height);
            bool ms = u.Rarity == Rarity.Milestone;  // the milestone pool: gold, its change line always shown, brackets on the picked one
            DrawCrtCardFrame(r, rc, selected, hover, behind, now);  // the lobby's boxed look: dark screen, thin outline, corner brackets, a glow

            // Rarity chip, centred on top, above the icon's cutout tile.
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 22f + rarity.Length * 7.2f;
            var chipR = new Rect(r.center.x - rw / 2f, r.y + 14f, rw, 20f);
            DrawCrtChip(chipR, rarity, rc, selected);
            var tile = new Rect(r.center.x - 52f, r.y + 42f, 104f, 104f);
            DrawCrtTile(tile, rc, selected);  // the icon keeps its own colours
            Ui.Icon(new Rect(tile.center.x - 38f, tile.center.y - 38f, 76f, 76f), PixelArt.ForUpgrade(u.Name));
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

            // Name, description (and what mastering a granted ability adds), a note.
            Color nameLow = selected ? Color.Lerp(rc, Color.white, 0.25f) : new Color(0.78f, 0.83f, 0.94f);
            CrtUi.Title(new Rect(r.x + 8f, r.y + 148f, r.width - 16f, 36f), u.Name, u.Name.Length > 16 ? 27 : 32, Color.Lerp(nameLow, Color.white, 0.6f), TextAnchor.MiddleCenter, fxReduced);
            string desc = Upgrades.DescFor(u, me.Weapon);
            int ds = desc.Length > 110 ? 12 : 14;
            bool capped = u.MaxStacks < 99, grants = u.Grants != AbilityId.None;
            string note = UpgradeNote(me, index, u);
            if (capped && !grants && !u.Role.HasValue)
                note = "";  // (the circles say how many you have)
            if (grants)
            {
                DrawDescAndMastery(new Rect(r.x + 16f, r.y + 188f, r.width - 32f, r.height - 188f - 50f), desc, u.Grants, ds, Ui.Text, true);
                note = "";
            }
            else
                Ui.Label(new Rect(r.x + 16f, r.y + 188f, r.width - 32f, 76f), desc, ds, selected ? Color.white : LobbyKit.Soft, TextAnchor.UpperCenter, false, true);
            if (note.Length > 0)
                Ui.Label(new Rect(r.x, r.y + 266f, r.width, 16f), note, 11, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);

            // Stack circles: filled for what you hold; on the picked card the next one is ringed. The change goes under them.
            var shown = ms ? (milestoneParts != null && index < milestoneParts.Length ? milestoneParts[index] : null) : selected ? pickParts : null;
            bool showParts = shown != null;
            if (capped)
            {
                int total = Math.Min(u.MaxStacks, 8), have = me.Offer != null && index < me.Offer.Length ? Upgrades.Count(me.Taken, me.Offer[index]) : 0;
                const float d = 15f, pg = 9f;
                float px = r.center.x - (total * d + (total - 1) * pg) / 2f, py = r.yMax - 98f;
                for (int k = 0; k < total; k++)
                {
                    var c = new Rect(px + k * (d + pg), py, d, d);
                    if (k < have)  // squares, like the lobby's stat pips
                    {
                        Ui.Glow(c, rc, 0.2f);
                        Ui.Fill(c, rc);
                    }
                    else if ((selected || ms) && k == have)
                    {
                        Ui.Fill(c, new Color(rc.r, rc.g, rc.b, 0.22f + 0.14f * Mathf.Sin(now * 5f)));
                        CrtEdge(c, 2f, rc);
                    }
                    else
                    {
                        Ui.Fill(c, new Color(0.01f, 0.02f, 0.05f, 1f));
                        CrtEdge(c, 1f, new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.9f));
                    }
                }
                if (showParts && !grants)
                    for (int i = 0; i < Math.Min(2, shown.Count); i++)
                        Ui.Label(new Rect(r.x + 6f, r.yMax - 76f + i * 15f, r.width - 12f, 16f), shown[i], shown[i].Length > 34 ? 11 : 12, ms && !selected ? new Color(0.45f, 0.85f, 0.52f) : new Color(0.5f, 1f, 0.58f), TextAnchor.MiddleCenter, true);
            }
            else if (showParts && !grants)
                DrawPickStrip(new Rect(r.x + 15.4f, r.yMax - 100f, r.width - 30.8f, 56f), 12, true, shown, Upgrades.Count(me.Taken, me.Offer != null && index < me.Offer.Length ? me.Offer[index] : 0));  // (the mockup's 1.2cqw side margin; the panel's bottom sits 44 px up, over the key)

            // The key, bottom centre, over the fading rarity line.
            string keyText = (index + 1).ToString();
            DrawKeyCap(new Rect(r.center.x - (12f + keyText.Length * 6.5f) / 2f, r.yMax - 40f, 0f, 18f), keyText, selected ? rc : new Color(1f, 1f, 1f, 0.6f));
            for (float dx = 0f; dx < r.width - 20f; dx += 8f)  // the dashed line along the bottom
                Ui.Fill(new Rect(r.x + 10f + dx, r.yMax - 12f, 5f, 2f), new Color(rc.r, rc.g, rc.b, selected ? 0.95f : hover ? 0.8f : 0.5f));
            if (selected && ms)
                DrawBrackets(r, rc);
            if (behind)  // another card is picked: this one steps back
                Ui.Fill(r, new Color(0.01f, 0.012f, 0.03f, 0.32f));
        }

        /// One upgrade card: rarity ribbon, pixel-art icon in a glowing frame, name, description and a note.
        void DrawUpgradeCard(Player me, int index, UpgradeDef u, Rect r, bool compact, bool selected, bool hover, float now)
        {
            Color rc = CrtCol(u);
            if (!selected)
                r.y += Mathf.Sin(now * 1.85f + index * 2.1f) * 2.5f;  // the same slow bob as the tall cards
            DrawCrtCardFrame(r, rc, selected, hover, false, now);
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
                DrawCrtTile(frame, rc, selected);
                Ui.Icon(new Rect(frame.x + 2f, frame.y + 2f + bob * 0.5f, 48f, 48f), icon);
                Ui.Label(new Rect(r.x + 74f, r.y + 14f, r.width - 82f, 16f), note, 10, rc, TextAnchor.MiddleLeft, true);
                CrtUi.Title(new Rect(r.x + 74f, r.y + 28f, r.width - 82f, 36f), u.Name, u.Name.Length > 14 ? 19 : 22, Color.white, TextAnchor.UpperLeft, fxReduced);
                Ui.Label(new Rect(r.x + 12f, r.y + 74f, r.width - 24f, 52f), Upgrades.DescFor(u, me.Weapon), 11, Ui.Text, TextAnchor.UpperLeft, false, true);
                if (selected && pickParts != null)  // (a strip would cover the text on these small cards: one green line instead)
                    Ui.Label(new Rect(r.x + 10f, r.yMax - 22f, r.width - 20f, 16f), pickParts[0], 10, new Color(0.5f, 1f, 0.58f), TextAnchor.MiddleRight, true);
                else
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
            Ui.Glitch(new Rect(r.x + 10f, r.y + 144f, r.width - 20f, 28f), u.Name, 21, Color.white, TextAnchor.MiddleCenter);
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
            int have = me.Offer != null && index < me.Offer.Length ? Upgrades.Count(me.Taken, me.Offer[index]) : 0;  // (the offer is cleared the moment you pick, while the card is still flying out)
            if (u.Rarity == Rarity.Mod)
                return have > 0 ? "YOU HAVE " + have + " / " + u.MaxStacks : "NEW";
            if (u.Grants != AbilityId.None)
            {
                string cls = u.ClassAbility ? "Class ability  ·  " : "";
                if (index == selectedCard && pickKey >= 1)
                    return cls + "Goes on " + SlotKeys[pickKey] + " (choose with the keys)";
                return cls + "You choose its key";
            }
            if (u.Role.HasValue)
                return Defs.Get(u.Role.Value).Name + " only";
            if (have > 0 && Upgrades.Diminishes(u))  // diminishing returns (Upgrades.CopyStrength): say what this copy is worth
                return "You have " + have + "  ·  this copy " + Mathf.RoundToInt(Upgrades.CopyStrength(u, have + 1) * 100f) + "% strength";
            return have > 0 ? "You have " + have : "";
        }

        /// Your current stats; with a card selected, the ones it would change show their new value.
        /// shopPreview: an upgrade from the Bazaar's shelf to preview instead of the picker's selection.
        void DrawStatPanel(Player me, Rect r, int shopPreview = -1)
        {
            var now = Stats(me, -1, 0);
            List<StatLine> after = null;
            if (shopPreview >= 0)
                after = Stats(me, shopPreview, 0);
            else if (selectedCard >= 0 && me.Offer != null && selectedCard < me.Offer.Length)
                after = Stats(me, me.Offer[selectedCard], pickKey);
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
