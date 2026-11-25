using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The gadget bag's front end (Core/Gadgets.cs): hold the Gadget key with gadgets in the bag and a wheel opens at the cursor, one slice
    /// for the gadget in hand and one for each in the bag. Move to a slice and let go to swap it into your hand; let go in the middle
    /// to throw what's in hand where the cursor was. With a Gadget wheel key bound, holding that key opens it instead (its middle cancels)
    /// and holding the Gadget key always throws. A small chip beside the keys shows the gadget in hand and an icon for each bag slot. With an empty
    /// bag the key works as it always did (tap: on / off, hold: throw).
    public partial class ArenaApp
    {
        bool gadgetWheelOpen;
        float gadgetWheelK;
        Vector2 gadgetAnchor;   // where the wheel is centred on the HUD canvas
        int gadgetHover = -1;   // the hovered slice (an index into gadgetWheelSlots), -1: the middle
        readonly List<int> gadgetWheelSlots = new List<int>();  // each slice's source: -1 the hand, else a bag slot

        static int BagCount(Player p)
        {
            int n = 0;
            for (int i = 0; i < p.BagKind.Length; i++)
                if (p.BagKind[i] != GadgetId.None)
                    n++;
            return n;
        }

        static Color GadgetInk(GadgetId g) => GadgetTone(g).ToColor();  // (GadgetShapes.cs)

        /// "MAGNET 42s", "GRENADE x3", with "+2" for what's in the bag; "—" with nothing.
        static string GadgetSummary(Player me)
        {
            int bag = BagCount(me);
            string hand = me.Gadget == GadgetId.None ? "—" : ChipName(me.Gadget) + (Game.GadgetTimed(me.Gadget) ? " " + Mathf.CeilToInt(me.GadgetLeft) + "s" : "") + UsesTag(me.Gadget, me.GadgetUses);
            return bag > 0 ? hand + "  +" + bag : hand;
        }

        /// A gadget's name on the wheel's small tiles.
        static string WheelName(GadgetId g) => g == GadgetId.EnergyDrink ? "ENERGY" : g == GadgetId.ShieldGenerator ? "SHIELD" : g == GadgetId.Medkit ? "MEDKIT"
            : g == GadgetId.SmokeBomb ? "SMOKE" : g == GadgetId.GasGrenade ? "GAS" : g == GadgetId.OverclockChip ? "OVERCLOCK" : g == GadgetId.LandMine ? "MINE"
            : Game.GadgetName(g).ToUpperInvariant();

        /// A gadget's name on the chip and the Inventory page.
        static string ChipName(GadgetId g) => g == GadgetId.ShieldGenerator ? "SHIELD GEN" : g == GadgetId.OverclockChip ? "OVERCLOCK" : Game.GadgetName(g).ToUpperInvariant();

        /// " x3" for a gadget with uses (the Lodestone, the Medkit, the throwables), "" otherwise (a timed one).
        static string UsesTag(GadgetId g, byte uses) => Game.GadgetUses(g) > 1 ? " x" + uses : "";

        void OpenGadgetWheel(Player me)
        {
            gadgetWheelSlots.Clear();
            if (me.Gadget != GadgetId.None)
                gadgetWheelSlots.Add(-1);
            for (int i = 0; i < me.BagKind.Length; i++)
                if (me.BagKind[i] != GadgetId.None)
                    gadgetWheelSlots.Add(i);
            var m = MouseUi();
            gadgetAnchor = new Vector2(Mathf.Clamp(m.x, 130f, W - 130f), Mathf.Clamp(m.y, 130f, H - 130f));
            gadgetHover = -1;
            gadgetWheelOpen = true;
        }

        void TickGadgetWheelHover()
        {
            Vector2 d = MouseUi() - gadgetAnchor;
            int n = gadgetWheelSlots.Count;
            if (d.magnitude < 30f || n == 0)
            {
                gadgetHover = -1;
                return;
            }
            float step = 360f / n, a = (Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f + step / 2f + 720f) % 360f;  // 0 = the top slice's left edge
            gadgetHover = Mathf.FloorToInt(a / step) % n;
        }

        GadgetId GadgetOfSlot(Player me, int slot) => slot < 0 ? me.Gadget : me.BagKind[slot];
        float GadgetLeftOfSlot(Player me, int slot) => slot < 0 ? me.GadgetLeft : me.BagLeft[slot];

        /// A gadget's glyph centred on c (HUD pixels, from fills and dots like the ping glyphs): the Magnet's horseshoe, the Lodestone's house in a
        /// ring, the Energy Drink's can with a bolt.
        void DrawGadgetGlyph(GadgetId g, Vector2 c, float r, Color col)
        {
            float t = Mathf.Max(2f, r * 0.16f), k = r * 0.55f;
            if (DrawNewGadgetGlyph(g, c, r, col))  // GadgetShapes.cs
                return;
            if (g == GadgetId.EnergyDrink)
            {
                var can = new Rect(c.x - k * 0.62f, c.y - k * 0.95f, k * 1.24f, k * 1.9f);
                Ui.Fill(can, col);
                Ui.Fill(new Rect(can.x, can.y, can.width, t), new Color(0.86f, 0.9f, 0.96f));  // rim
                Ui.Fill(new Rect(can.x, can.yMax - t * 0.7f, can.width, t * 0.7f), new Color(0.86f, 0.9f, 0.96f, 0.8f));
                var dark = new Color(0.08f, 0.2f, 0.04f);
                float bw = Mathf.Max(1.5f, t * 0.8f);  // the bolt: two offset bars
                Ui.Fill(new Rect(c.x - bw * 0.2f, c.y - k * 0.55f, bw, k * 0.6f), dark);
                Ui.Fill(new Rect(c.x - bw * 1.2f, c.y - bw * 0.2f, bw * 2.4f, bw), dark);
                Ui.Fill(new Rect(c.x - bw * 0.8f, c.y, bw, k * 0.6f), dark);
                return;
            }
            if (g == GadgetId.Lodestone)
            {
                Ui.Dot(new Rect(c.x - r * 0.85f, c.y - r * 0.85f, r * 1.7f, r * 1.7f), col, true);
                Ui.Fill(new Rect(c.x - k * 0.5f, c.y - k * 0.1f, k, k * 0.7f), col);  // walls
                Ui.Fill(new Rect(c.x - k * 0.7f, c.y - k * 0.5f, k * 1.4f, t), col);  // roof
                Ui.Fill(new Rect(c.x - k * 0.3f, c.y - k * 0.8f, k * 0.6f, t), col);
                return;
            }
            Ui.Fill(new Rect(c.x - k, c.y - k * 0.9f, t * 1.6f, k * 1.5f), col);   // left leg
            Ui.Fill(new Rect(c.x + k - t * 1.6f, c.y - k * 0.9f, t * 1.6f, k * 1.5f), col);  // right leg
            Ui.Fill(new Rect(c.x - k, c.y + k * 0.6f - t * 1.6f, k * 2f, t * 1.6f), col);  // the bend
            Ui.Fill(new Rect(c.x - k, c.y - k * 0.9f, t * 1.6f, t * 1.2f), new Color(0.86f, 0.9f, 0.96f));  // steel tips
            Ui.Fill(new Rect(c.x + k - t * 1.6f, c.y - k * 0.9f, t * 1.6f, t * 1.2f), new Color(0.86f, 0.9f, 0.96f));
        }

        /// The wheel, centred on gadgetAnchor (HUD pass).
        void DrawGadgetWheel()
        {
            var me = game.Me;
            if (gadgetWheelK <= 0.01f || me == null || gadgetWheelSlots.Count == 0)
                return;
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * gadgetWheelK;
            Ui.Fill(new Rect(0f, 0f, W, H), new Color(0.02f, 0.03f, 0.04f, 0.4f));
            int n = gadgetWheelSlots.Count;
            float mid = n > 3 ? 112f : 92f;  // (the tiles are 70 wide: far enough out to clear the middle)
            for (int i = 0; i < n; i++)
            {
                int slot = gadgetWheelSlots[i];
                GadgetId g = GadgetOfSlot(me, slot);
                float a = (-90f + i * 360f / n) * Mathf.Deg2Rad;
                var c = gadgetAnchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (mid + (gadgetHover == i ? 6f : 0f)) * (0.7f + 0.3f * gadgetWheelK);
                bool hot = gadgetHover == i;
                Color tone = GadgetInk(g);
                var tile = new Rect(c.x - 35f, c.y - 29f, 70f, 58f);
                Ui.Fill(tile, hot ? tone.WithAlpha(0.35f) : new Color(0.07f, 0.09f, 0.11f, 0.92f));
                Ui.Fill(new Rect(tile.x, tile.y, tile.width, 2f), hot ? tone : slot < 0 ? tone.WithAlpha(0.8f) : new Color(1f, 1f, 1f, 0.15f));
                DrawGadgetGlyph(g, new Vector2(c.x, c.y - 7f), 22f, hot ? Color.white : tone);
                string label = WheelName(g) + (Game.GadgetTimed(g) ? " " + Mathf.CeilToInt(GadgetLeftOfSlot(me, slot)) + "s" : "");
                if (slot < 0)
                    label += UsesTag(g, me.GadgetUses);  // (the hand's tile has IN HAND across its top)
                else if (Game.GadgetUses(g) > 1)  // uses left, in the tile's corner
                    Ui.Label(new Rect(tile.x, tile.y + 3f, tile.width - 4f, 11f), "x" + (slot < 0 ? me.GadgetUses : me.BagUses[slot]), 8, hot ? Color.white : tone, TextAnchor.MiddleRight, true);
                Ui.Label(new Rect(tile.x, tile.yMax - 17f, tile.width, 14f), label, 9, hot ? Color.white : new Color(0.57f, 0.63f, 0.68f), TextAnchor.MiddleCenter, true);
                if (slot < 0)
                    Ui.Label(new Rect(tile.x, tile.y + 3f, tile.width, 11f), "IN HAND", 7, tone.WithAlpha(0.9f), TextAnchor.MiddleCenter, true);
            }
            var hub = new Rect(gadgetAnchor.x - 26f, gadgetAnchor.y - 26f, 52f, 52f);
            Ui.Dot(hub, new Color(0.03f, 0.04f, 0.07f, 0.9f));
            Ui.Dot(hub, new Color(0.35f, 0.4f, 0.45f), true);
            Ui.Label(hub, gadgetHover >= 0 ? (gadgetWheelSlots[gadgetHover] < 0 ? "KEEP" : "SWAP") : me.Gadget != GadgetId.None && !gadgetWheelByKey ? "THROW" : "CANCEL", 8, new Color(0.57f, 0.63f, 0.68f), TextAnchor.MiddleCenter, true);
            Ui.Alpha = oldAlpha;
        }

        const float BagTile = 22f, BagGap = 3f;  // the chip's bag row: one small tile per bag slot

        /// The chip beside the keys: the gadget in hand (glyph, name, use left), then a row of small tiles, one per bag slot, each
        /// with its gadget's glyph and its uses or seconds left, so the bag reads at a glance without opening the wheel.
        void DrawGadgetChip(Player me)
        {
            if (me == null || me.Downed || (me.Gadget == GadgetId.None && BagCount(me) == 0))
                return;
            int slots = me.BagKind.Length;
            float row = slots * (BagTile + BagGap) + 4f, w = 150f + row;
            var r = new Rect(W / 2f - w / 2f, HudChestHint.y - 30f, w, 26f);  // (just over the chest hint, centred)
            Ui.Fill(r, new Color(0.03f, 0.04f, 0.07f, 0.85f));
            Color tone = me.Gadget == GadgetId.None ? new Color(0.57f, 0.63f, 0.68f) : GadgetInk(me.Gadget);
            Ui.Fill(new Rect(r.x, r.y, 2f, r.height), tone);
            if (me.Gadget != GadgetId.None)
                DrawGadgetGlyph(me.Gadget, new Vector2(r.x + 16f, r.center.y), 10f, tone);
            string key = Keybinds.NameOrNull(KeyAction.GadgetWheel) ?? Keybinds.NameOrNull(KeyAction.Gadget) ?? "";
            string text = me.Gadget == GadgetId.None ? "HOLD " + key + " TO EQUIP" : me.Gadget == GadgetId.Lodestone && me.GadgetOn ? "CASTING " + Mathf.CeilToInt(me.GadgetLeft) + "s" + UsesTag(me.Gadget, me.GadgetUses)
                : ChipName(me.Gadget) + (me.GadgetOn ? " ON" : "") + UsesTag(me.Gadget, me.GadgetUses);
            Ui.Label(new Rect(r.x + 30f, r.y, 150f - 34f, r.height), text, 10, Color.white, TextAnchor.MiddleLeft, true);
            float x0 = r.xMax - row;
            Ui.Fill(new Rect(x0 - 1f, r.y + 4f, 1f, r.height - 8f), new Color(1f, 1f, 1f, 0.12f));  // the divider before the bag
            for (int i = 0; i < slots; i++)
                DrawBagTile(me, i, new Rect(x0 + 3f + i * (BagTile + BagGap), r.center.y - BagTile / 2f, BagTile, BagTile));
        }

        /// One bag slot on the chip: an empty outline, or the gadget's glyph in its ink with its uses (x2) or seconds (18) in the corner.
        void DrawBagTile(Player me, int i, Rect t)
        {
            GadgetId g = me.BagKind[i];
            if (g == GadgetId.None)
            {
                Ui.Fill(t, new Color(1f, 1f, 1f, 0.03f));
                Ui.RectEdge(t, 1f, new Color(1f, 1f, 1f, 0.1f));
                return;
            }
            Color ink = GadgetInk(g);
            Ui.Fill(t, ink.WithAlpha(0.14f));
            Ui.Fill(new Rect(t.x, t.yMax - 2f, t.width, 2f), ink.WithAlpha(0.8f));
            DrawGadgetGlyph(g, new Vector2(t.center.x - 2f, t.center.y - 3f), 7f, ink);
            string tag = Game.GadgetTimed(g) ? Mathf.CeilToInt(me.BagLeft[i]).ToString() : Game.GadgetUses(g) > 1 ? "x" + me.BagUses[i] : "";
            if (tag != "")
                Ui.Label(new Rect(t.x, t.yMax - 10f, t.width - 1f, 9f), tag, 7, Color.white, TextAnchor.MiddleRight, true);
        }
    }
}
