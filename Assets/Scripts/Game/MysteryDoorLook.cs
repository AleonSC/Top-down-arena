using UnityEngine;

namespace Arena
{
    /// Mystery doors (Core/MysteryDoors.cs, Docs/MysteryDoors.md): a first look until step 3. In the world, a sealed amber shutter in the
    /// wall (slats and a pulsing diamond; a teal rim once someone saved it); on the HUD, a price tag over each door and, standing by one,
    /// the keys to open or save it.
    public partial class ArenaApp
    {
        static readonly Rgba MysteryAmber = new Rgba(1f, 0.74f, 0.25f), MysteryDark = new Rgba(0.09f, 0.07f, 0.05f), MysterySaved = new Rgba(0.35f, 0.9f, 0.85f);

        /// Every sealed door, over the walls (after DrawGates).
        void DrawMysteryDoors()
        {
            if (!game.ExpandingOn)
                return;
            float t = game.AnimT;
            for (int i = 0; i < game.MysteryDoors.Count; i++)
            {
                var d = game.MysteryDoors[i];
                V2 c = d.Pos, along = d.Vertical ? new V2(0f, 1f) : new V2(1f, 0f), across = d.Vertical ? new V2(1f, 0f) : new V2(0f, 1f);
                float half = Rooms.DoorW / 2f, wt = Rooms.WallT / 2f + 4f;
                float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3f + i * 1.7f);
                var rim = d.Saver != 0 ? MysterySaved : MysteryAmber;
                SetColor(rim.WithAlpha(0.12f * pulse));
                Quad(c - along * (half + 10f) - across * (wt + 10f), c + along * (half + 10f) - across * (wt + 10f), c + along * (half + 10f) + across * (wt + 10f), c - along * (half + 10f) + across * (wt + 10f));
                SetColor(MysteryDark);
                Quad(c - along * half - across * wt, c + along * half - across * wt, c + along * half + across * wt, c - along * half + across * wt);
                for (int k = 1; k < 6; k++)  // the shutter's slats
                {
                    float u = -half + k * (2f * half / 6f);
                    Line(c + along * u - across * (wt - 3f), c + along * u + across * (wt - 3f), 2f, MysteryAmber.WithAlpha(0.35f));
                }
                Line(c - along * half - across * wt, c + along * half - across * wt, 2f, rim.WithAlpha(0.9f));
                Line(c - along * half + across * wt, c + along * half + across * wt, 2f, rim.WithAlpha(0.9f));
                float s = 9f + 2f * pulse;  // the diamond: something behind
                SetColor(rim.WithAlpha(0.95f));
                Quad(c - along * s, c - across * s, c + along * s, c + across * s);
                SetColor(MysteryDark);
                Quad(c - along * (s - 4f), c - across * (s - 4f), c + along * (s - 4f), c + across * (s - 4f));
            }
        }

        /// DrawHud: a price tag over every door, and by one the keys to open (G) or save (X) it.
        void DrawMysteryHint()
        {
            var me = game.Me;
            if (me == null || !game.ExpandingOn || game.MysteryDoors.Count == 0)
                return;
            int near = me.Downed ? -1 : game.MysteryNear(me.Pos);
            for (int i = 0; i < game.MysteryDoors.Count; i++)
            {
                var d = game.MysteryDoors[i];
                V2 at = ToUi(d.Pos);
                bool saved = d.Saver != 0, mine = d.Saver == game.MyId;
                bool afford = game.SpendableVectors(me) >= d.Price;
                if (i == near)
                {
                    string open = Keybinds.KeyName(Keybinds.Get(KeyAction.TakeItem)).ToUpperInvariant(), save = Keybinds.KeyName(Keybinds.Get(KeyAction.DiscardItem)).ToUpperInvariant();
                    string text = open + "  OPEN  " + d.Price + " VECTORS   ·   " + save + (mine ? "  UNSAVE" : saved ? "  SAVED" : "  SAVE");
                    var r = new Rect(at.X - 130f, at.Y - 58f, 260f, 22f);
                    Ui.Pill(r, afford ? new Color(1f, 0.74f, 0.25f, 0.9f) : new Color(0f, 0f, 0f, 0.65f));
                    Ui.Label(r, text, 11, afford ? new Color(0.08f, 0.06f, 0.03f) : new Color(1f, 0.45f, 0.42f), TextAnchor.MiddleCenter, true);
                }
                else
                {
                    string text = "?  " + d.Price + (saved ? "  · SAVED" : "");
                    var r = new Rect(at.X - 42f, at.Y - 40f, 84f, 18f);
                    Ui.Pill(r, new Color(0f, 0f, 0f, 0.6f));
                    Ui.Label(r, text, 10, saved ? new Color(0.35f, 0.9f, 0.85f) : new Color(1f, 0.74f, 0.25f), TextAnchor.MiddleCenter, true);
                }
            }
        }
    }
}
