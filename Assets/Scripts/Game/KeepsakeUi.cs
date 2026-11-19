using UnityEngine;

namespace Arena
{
    /// The keepsake vote on screen (Docs/MapReset.md step 2, Core/MapReset.cs): after a boss in the Campaign the map resets, and
    /// the team picks one room to take to the new map. A CRT window of room cards (the room's outline, its size and kind, who
    /// picked it) with the vote's clock; click KEEP on a card to vote. Preview `KEEPSAKE=1 shot.sh x hud`.
    public partial class ArenaApp
    {
        bool KeepsakeWindowUp => game.KeepsakeOpen && game.Me != null && !BeamShowing;

        void DrawKeepsakeVote(float t)
        {
            var choices = game.KeepsakeChoices;
            if (choices.Count == 0)
                return;
            bool calm = FxCalm;
            RoundBackdrop(PathDim);  // (RoundWindows.cs)
            DkPanel(new Rect(30f, 12f, W - 60f, H - 24f));
            var S = new Rect(42f, 24f, W - 84f, H - 48f);
            CrtUi.Begin(S);
            int secs = Mathf.CeilToInt(game.KeepsakeLeft);
            var dim = CrtUi.Dim;
            float sy = S.y + 8f;
            bool lampOn = calm || Mathf.Repeat(t, 1.2f) < 0.6f;
            CrtUi.Text(new Rect(S.x + 20f, sy, 240f, 20f), (lampOn ? "● " : "  ") + "MAP RESET", 15, lampOn ? Ui.Gold : dim, TextAnchor.MiddleLeft, false, calm);
            CrtUi.Text(new Rect(S.x, sy, S.width, 20f), "MAP " + game.MapNumber + " DONE  ·  ROOM KITS KEPT  ·  HALF YOUR SPENT VECTORS BACK", 15, dim, TextAnchor.MiddleCenter, false, calm);
            CrtUi.Text(new Rect(S.xMax - 220f, sy, 200f, 20f), "VOTE ENDS " + secs, 15, secs <= 5 ? Ui.Gold : dim, TextAnchor.MiddleRight, false, calm);
            DkBar(new Rect(S.x + 20f, sy + 24f, S.width - 40f, 2f), game.KeepsakeLeft / Game.KeepsakeTime, Ui.Gold);
            DkTitle(new Rect(S.x, S.y + 38f, S.width, 54f), "KEEP ONE ROOM", 46, CrtUi.Ink, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(S.x, S.y + 92f, S.width, 20f), "It's rebuilt next to the hub on the new map. Most votes wins; a tie goes to the host, no votes to the biggest", 13, Ui.Muted, TextAnchor.MiddleCenter);

            int n = choices.Count, cols = Mathf.Min(n, n <= 4 ? 4 : n <= 12 ? 6 : 8), rows = (n + cols - 1) / cols;
            float gap = 16f, top = S.y + 122f, bottom = S.yMax - 56f;
            float cw = Mathf.Min(260f, (S.width - 60f - (cols - 1) * gap) / cols);
            float ch = Mathf.Min(300f, (bottom - top - (rows - 1) * gap) / rows);
            top += Mathf.Max(0f, (bottom - top - rows * ch - (rows - 1) * gap) / 2f);  // (the cards sit in the middle of the screen)
            // One scale for every card's outline, so the sizes compare.
            int spanX = 1, spanY = 1;
            foreach (int h in choices)
            {
                RoomBounds(h, out int bx0, out int by0, out int bx1, out int by1);
                spanX = Mathf.Max(spanX, bx1 - bx0); spanY = Mathf.Max(spanY, by1 - by0);
            }
            float scale = Mathf.Min((cw - 28f) / spanX, (ch - 104f) / spanY);
            int mine = game.KeepsakeVotes.TryGetValue(game.MyId, out int v) ? v : -1;
            int lead = game.KeepsakeWinner();
            for (int i = 0; i < n; i++)
            {
                int c = i % cols, row = i / cols;
                int inRow = Mathf.Min(cols, n - row * cols);
                float x0 = W / 2f - (inRow * cw + (inRow - 1) * gap) / 2f;
                var r = new Rect(x0 + c * (cw + gap), top + row * (ch + gap), cw, ch);
                if (DrawKeepsakeCard(r, choices[i], choices[i] == mine, choices[i] == lead && game.KeepsakeVotes.Count > 0, scale))
                    game.CastKeepsakeVote(choices[i]);
            }
            int waiting = 0;
            foreach (int id in game.Players.Keys)
                if (!game.KeepsakeVotes.ContainsKey(id))
                    waiting++;
            string msg = mine < 0 ? "Pick the room to keep" : waiting > 0 ? "Voted. Waiting for " + waiting + " more player" + (waiting == 1 ? "" : "s") : "Counting the votes";
            Ui.Label(new Rect(0, S.yMax - 40f, W, 22f), msg, 14, mine < 0 ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
            CrtUi.End(S, t, calm);
        }

        /// One room to keep: its outline to scale, its size and kind, the players who picked it, and the KEEP key. True when clicked.
        void RoomBounds(int head, out int x0, out int y0, out int x1, out int y1)
        {
            var rooms = game.Expanded;
            x0 = int.MaxValue; y0 = int.MaxValue; x1 = int.MinValue; y1 = int.MinValue;
            foreach (int i in Expansion.RoomGroup(rooms, head))
            {
                x0 = Mathf.Min(x0, rooms[i].X); y0 = Mathf.Min(y0, rooms[i].Y); x1 = Mathf.Max(x1, rooms[i].X1); y1 = Mathf.Max(y1, rooms[i].Y1);
            }
        }

        bool DrawKeepsakeCard(Rect r, int head, bool mine, bool lead, float s)
        {
            var rooms = game.Expanded;
            var room = rooms[head];
            var ink = KindInk(room.Kind, room.Floor).ToColor();
            DkBox(r, ink, mine ? 1f : lead ? 0.6f : 0.25f);
            var group = Expansion.RoomGroup(rooms, head);
            RoomBounds(head, out int x0, out int y0, out int x1, out int y1);
            // The outline at the window's common scale, in the card's top.
            var stage = new Rect(r.x + 14f, r.y + 12f, r.width - 28f, r.height - 104f);
            float ox = stage.center.x - (x1 - x0) * s / 2f, oy = stage.center.y - (y1 - y0) * s / 2f;
            foreach (int i in group)
            {
                var b = rooms[i];
                var br = new Rect(ox + (b.X - x0) * s, oy + (b.Y - y0) * s, b.W * s, b.H * s);
                Ui.Fill(br, ink.WithAlpha(mine ? 0.42f : 0.24f));
            }
            foreach (int i in group)  // (rims after the fills, so inner seams stay faint)
            {
                var b = rooms[i];
                CrtKit.Frame(new Rect(ox + (b.X - x0) * s, oy + (b.Y - y0) * s, b.W * s, b.H * s), 1f, ink.WithAlpha(0.8f));
            }
            int tiles = Expansion.RoomTiles(rooms, head);
            string kind = room.Kind == RoomKind.Floor ? Paths.Get(room.Floor).Name : room.Surprise > 0 ? "Surprise" : room.Kind.ToString();
            float ty = r.yMax - 88f;
            DkTitle(new Rect(r.x + 12f, ty, r.width - 24f, 20f), kind.ToUpperInvariant(), 14, ink, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(r.x + 12f, ty + 20f, r.width - 24f, 16f), (x1 - x0) / Expansion.Step + " x " + (y1 - y0) / Expansion.Step + "  ·  " + tiles + " tiles", 11, CrtStyle.Desc, TextAnchor.MiddleCenter);
            // Who picked it: a pip in each voter's class colour, centred.
            int votes = 0;
            foreach (var kv in game.KeepsakeVotes)
                if (kv.Value == head && game.Players.ContainsKey(kv.Key))
                    votes++;
            int pip = 0;
            foreach (var kv in game.KeepsakeVotes)
                if (kv.Value == head && game.Players.TryGetValue(kv.Key, out var p))
                    Ui.Fill(new Rect(r.center.x - (votes * 14f - 4f) / 2f + pip++ * 14f, ty + 40f, 10f, 6f), p.Def.Color.ToColor());
            if (lead)
                DkTag(new Rect(r.xMax - 74f, r.y + 8f, 64f, 18f), "LEADING", Ui.Gold);
            return DkKey(new Rect(r.x + 12f, r.yMax - 34f, r.width - 24f, 26f), mine ? "KEEPING  ✓" : "KEEP", ink, mine, 13);
        }
    }
}
