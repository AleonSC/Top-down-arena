using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Expanding arena on screen (ExpansionGame.cs, Docs/ExpandingArena.md): the vote on rooms to buy after a boss, and
    /// the team purse chip under the wave plate. The map and the minimap are drawn by RoomsLook.cs.
    public partial class ArenaApp
    {
        static readonly Rgba CombatInk = new Rgba(1f, 0.3f, 0.36f), ShopInk = new Rgba(1f, 0.82f, 0.4f);

        static Rgba KindInk(RoomKind k, PathId floor) => k == RoomKind.Shop ? ShopInk : k == RoomKind.Floor ? Paths.Get(floor).Accent : CombatInk;

        static string KindName(ExpansionRoom r) => r.Kind == RoomKind.Shop ? "Shop" : r.Kind == RoomKind.Floor ? Paths.Get(r.Floor).Name : "Combat";

        // --- The vote ----------------------------------------------------------------------------------------

        void DrawExpansionChoice(float t)
        {
            var offers = game.ExpandOffer;
            if (offers == null)
                return;
            Ui.Alpha = 1f;
            bool calm = CabCalm;
            RoundBackdrop(PathDim);  // (RoundWindows.cs)
            CrtUi.Title(new Rect(0, 22f, W, 56f), "EXPAND THE ARENA", 52, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            int mine;
            bool voted = game.ExpandVotes.TryGetValue(game.MyId, out mine);
            Ui.Label(new Rect(0, 80f, W, 20f), "Team purse " + game.Purse + " gold  ·  " + game.Expanded.Count + " room" + (game.Expanded.Count == 1 ? "" : "s") + " so far  ·  the boss fights stay in the middle", 13, Ui.Muted, TextAnchor.MiddleCenter);
            float cw = 370f, ch = 452f, gap = 30f, y = 118f;
            float x0 = W / 2f - (offers.Count * cw + (offers.Count - 1) * gap) / 2f;
            for (int i = 0; i < offers.Count; i++)
            {
                var r = new Rect(x0 + i * (cw + gap), y, cw, ch);
                if (DrawExpansionCard(r, offers[i], i, voted && mine == i, game.Purse, t, calm))
                    game.CastExpandVote(i);
            }
            float by = y + ch + 12f;
            bool skip = voted && mine == offers.Count;
            if (CrtUi.SoftKey(new Rect(W / 2f - 250f, by, 500f, 34f), "BUY NOTHING  ·  KEEP THE PURSE" + (skip ? "  ✓" : ""), null, 22, calm, lit: skip))
                game.CastExpandVote(offers.Count);
            int waiting = 0;
            foreach (int id in game.Players.Keys)
                if (!game.ExpandVotes.ContainsKey(id))
                    waiting++;
            string msg = !voted ? "Vote for a room: most votes wins" : waiting > 0 ? "Voted. Waiting for " + waiting + " more player" + (waiting == 1 ? "" : "s") : "Counting the votes";
            Ui.Label(new Rect(0, by + 40f, W, 22f), msg, 14, !voted ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        /// One room on offer as a CRT tube: a little map with the room lit up where it would go, what it is and what it costs.
        bool DrawExpansionCard(Rect r, ExpansionRoom o, int index, bool picked, int purse, float t, bool calm)
        {
            bool afford = o.Price <= purse;
            bool hover = afford && r.Contains(Event.current.mousePosition);
            Color ink = afford ? CrtUi.Ink : CrtUi.Dim, dim = CrtUi.Dim;
            bool taken = false;
            CrtUi.Begin(r);
            if (hover || picked)
                CrtKit.Frame(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), picked ? 3f : 2f, picked ? Ui.Gold : ink);
            var stage = new Rect(r.x + 22f, r.y + 22f, r.width - 44f, 210f);
            if (BeginUiGl())
            {
                DrawExpansionMini(stage, o, afford, t);
                EndUiGl();
            }
            var accent = ToColor(KindInk(o.Kind, o.Floor));
            float ty = stage.yMax + 14f;
            CrtUi.Text(new Rect(r.x + 22f, ty, r.width - 44f, 34f), (o.Shape + " room").ToUpperInvariant(), 28, ink, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(r.x + 22f, ty + 34f, r.width - 44f, 22f), KindName(o).ToUpperInvariant() + "  ·  " + o.W / 100 + " x " + o.H / 100 + " tiles", 15, afford ? accent : dim, TextAnchor.MiddleLeft, false, calm);
            string blurb = o.Kind == RoomKind.Shop ? "A room to shop in. (Its stock arrives with the room features.)"
                : o.Kind == RoomKind.Floor ? "Fights here are on " + Paths.Get(o.Floor).Name + " ground. (Its hazards arrive with the room features.)"
                : "More floor to fight on: and more enemies come with it.";
            CrtUi.Text(new Rect(r.x + 22f, ty + 62f, r.width - 44f, 70f), blurb, 14, dim, TextAnchor.UpperLeft, false, calm, true);
            int votes = 0;
            foreach (var v in game.ExpandVotes.Values)
                votes += v == index ? 1 : 0;
            string price = o.Price + " GOLD" + (afford ? "" : "  ·  NEED " + (o.Price - purse) + " MORE");
            CrtUi.Text(new Rect(r.x + 22f, r.yMax - 82f, r.width - 44f, 30f), price, 22, afford ? Ui.Gold : dim, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(r.x + 22f, r.yMax - 50f, r.width - 44f, 24f), votes == 0 ? "NO VOTES" : votes + " VOTE" + (votes == 1 ? "" : "S") + (picked ? "  ·  YOURS" : ""), 15, votes > 0 ? CrtUi.Ink : dim, TextAnchor.MiddleLeft, false, calm);
            CrtUi.End(r, t, calm);
            taken = hover && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (taken)
                Event.current.Use();
            return taken;
        }

        /// The map so far (dim), with the offered room lit in its colour and a marker on its door.
        void DrawExpansionMini(Rect stage, ExpansionRoom o, bool afford, float t)
        {
            var all = new List<ExpansionRoom>(game.Expanded) { o };
            var wd = Expansion.Build(all);
            V2 lo = wd.Min, hi = wd.Max;
            float pad = 14f, k = Mathf.Min((stage.width - 2f * pad) / (hi.X - lo.X), (stage.height - 2f * pad) / (hi.Y - lo.Y));
            float ox = stage.center.x - (hi.X - lo.X) * k / 2f, oy = stage.center.y - (hi.Y - lo.Y) * k / 2f;
            System.Func<V2, V2> M = p => new V2(ox + (p.X - lo.X) * k, oy + (p.Y - lo.Y) * k);
            RoomMapRect(stage.x, stage.y, stage.width, stage.height, new Rgba(0.01f, 0.03f, 0.08f, 0.6f));
            var ink = KindInk(o.Kind, o.Floor);
            float pulse = 0.65f + 0.35f * Mathf.Sin(t * 3f);
            for (int i = 0; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                bool isNew = i == wd.Areas.Length - 1;
                V2 p0 = M(a.Min), p1 = M(a.Max);
                Rgba fill, line;
                if (isNew) { fill = ink.WithAlpha((afford ? 0.3f : 0.12f) * pulse); line = ink.WithAlpha(afford ? 1f : 0.45f); }
                else if (a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.16f); line = new Rgba(0.4f, 0.64f, 1f, 0.8f); }
                else { fill = KindInk(a.Kind, a.Floor).WithAlpha(a.Kind == RoomKind.Combat ? 0.08f : 0.16f); line = KindInk(a.Kind, a.Floor).WithAlpha(0.5f); }
                RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, fill);
                RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, isNew ? 2f : 1f, line);
            }
            foreach (var d in wd.Doors)
            {
                V2 c = M(d.Pos);
                RoomMapRect(c.X - 3f, c.Y - 3f, 6f, 6f, d.Joins(wd.Areas.Length - 1) ? Rgba.White.WithAlpha(0.9f) : new Rgba(0.37f, 0.94f, 1f, 0.7f));
            }
        }

        // --- HUD chip ----------------------------------------------------------------------------------------

        /// Under the wave plate: the team purse. Returns true if it drew (the next chip goes a line up).
        bool DrawPurseChip(float x, float y)
        {
            if (!game.ExpandingOn)
                return false;
            string text = "PURSE  " + game.Purse + " GOLD  ·  " + game.Expanded.Count + "/" + Expansion.MaxRooms + " ROOMS";
            float w = 30f + text.Length * 6.4f;
            var chip = new Rect(x, y, w, 20f);
            var gold = new Color(1f, 0.82f, 0.4f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), gold);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, gold, TextAnchor.MiddleLeft, true);
            return true;
        }
    }
}
