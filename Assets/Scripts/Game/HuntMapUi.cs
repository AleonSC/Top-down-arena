using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The hunt map screen (Docs/HuntMap.md, Core/HuntMap.cs): the map on the left, climbing from the first fight at the top to the
    /// boss at the bottom, with square nodes joined by right-angle lines (Core/HuntMapLayout.cs), and the information beside it:
    /// the hunt, the party's votes, the next sectors as keys, the picked one's detail and the lock-in key. Look of the
    /// lobby: bracket boxes, spaced headings, phosphor keys. Preview: `Tools/Preview/shot.sh huntmap huntmap`.
    /// Shown between waves of a hunt played as a map (Core/HuntMapGame.cs): the votes and the timer are the game's, sent over the network.
    public partial class ArenaApp
    {
        static readonly Color HmElite = new Color(1f, 0.42f, 0.42f), HmAmbush = new Color(1f, 0.6f, 0.3f), HmContra = new Color(0.72f, 0.5f, 1f),
            HmRest = new Color(0.36f, 0.85f, 0.54f), HmGamble = new Color(0.62f, 0.69f, 0.82f);

        static Color HuntNodeColor(HuntNodeType t)
        {
            switch (t)
            {
                case HuntNodeType.Elite: case HuntNodeType.Boss: return HmElite;
                case HuntNodeType.Ambush: return HmAmbush;
                case HuntNodeType.Contraband: return HmContra;
                case HuntNodeType.Rest: return HmRest;
                case HuntNodeType.Shop: return CrtStyle.Gold;
                case HuntNodeType.Gamble: return HmGamble;
                default: return CrtUi.Ink;
            }
        }

        // Glyphs wait here while the squares and text are drawn, then go on top in one GL pass.
        struct HmGlyph { public HuntNodeType Type; public V2 At; public float Half; public Color Col; public bool Tick; }
        readonly List<HmGlyph> hmGlyphs = new List<HmGlyph>();

        static Rgba HmRgba(Color c, float a = 1f) => new Rgba(c.r, c.g, c.b, c.a * a);

        void DrawHuntMap(HuntMap map, HuntDef hunt, float t)
        {
            if (map == null)
                return;
            Ui.Alpha = 1f;
            bool calm = CabCalm;
            var ink = CrtUi.Ink;
            Ui.Fill(new Rect(0f, 0f, W, H), new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.97f));

            // Title bar: the key to leave isn't here (the map is part of the match), the timer is.
            LobbyKit.Spaced(new Rect(28f, 16f, 280f, 28f), "HUNT MAP", 16, new Color(ink.r, ink.g, ink.b, 0.95f), TextAnchor.MiddleLeft, 5f);
            Ui.Fill(new Rect(190f, 30f, W - 190f - 220f, 1f), LobbyKit.Ink(0.25f));
            LobbyKit.Spaced(new Rect(W - 230f, 16f, 202f, 28f), "VOTE ENDS  " + Mathf.CeilToInt(game.HuntMapSeconds) + " S", 11, CrtUi.Dim, TextAnchor.MiddleRight, 2.4f);

            int step = map.Step;
            var path = map.Path;
            var choices = map.Choices();
            int me = game.Me != null ? game.Me.Id : 0;
            int myVote;
            if (!game.MapVotes.TryGetValue(me, out myVote) || myVote < 0 || myVote >= choices.Length)
                myVote = -1;
            int detail = myVote >= 0 ? myVote : 0;

            // ---- The map (left) ----
            var mapR = new Rect(24f, 58f, 484f, 644f);
            LobbyKit.Panel(mapR);
            LobbyKit.Spaced(new Rect(mapR.x + 16f, mapR.y + 8f, 200f, 16f), "SECTOR MAP", 10, CrtUi.Dim, TextAnchor.MiddleLeft, 2.4f);
            LobbyKit.Spaced(new Rect(mapR.xMax - 216f, mapR.y + 8f, 200f, 16f), "START AT TOP", 10, CrtUi.Dim, TextAnchor.MiddleRight, 2.4f);
            float[] laneX = { mapR.center.x - 120f, mapR.center.x, mapR.center.x + 120f };
            var rowY = new float[HuntMap.Steps];
            for (int k = 0; k < rowY.Length; k++)
                rowY[k] = mapR.y + 58f + k * 58f;
            const float half = 15f, bossHalf = 21f;
            var links = HuntMapLayout.Links(map, laneX, rowY, half, bossHalf);

            // The lines first (GL), the squares on top.
            if (BeginUiGl())
            {
                for (int pass = 0; pass < 3; pass++)   // the dim ones first, then the next choices' links, then the route you took
                    foreach (var l in links)
                    {
                        bool cleared = l.Step + 1 < path.Length && path[l.Step] == l.From && path[l.Step + 1] == l.To;
                        bool live = !cleared && step > 0 && l.Step == step - 1 && path[l.Step] == l.From;
                        if ((cleared ? 2 : live ? 1 : 0) != pass)
                            continue;
                        Color c = cleared ? ink : live ? CrtStyle.Gold : new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.6f);
                        float lw = cleared ? 3f : live ? 2.5f : 1.5f;
                        for (int k = 0; k < 6; k += 2)
                        {
                            if (l.Pts[k] == l.Pts[k + 2] && l.Pts[k + 1] == l.Pts[k + 3])
                                continue;
                            var a = new V2(l.Pts[k], l.Pts[k + 1]);
                            var b = new V2(l.Pts[k + 2], l.Pts[k + 3]);
                            Line(a - (b - a).Normalized() * (lw * 0.5f), b + (b - a).Normalized() * (lw * 0.5f), lw, HmRgba(c));  // (the ends overlap so the corners are square)
                        }
                    }
                EndUiGl();
            }

            hmGlyphs.Clear();
            for (int s = 0; s < HuntMap.Steps; s++)
            {
                var row = map.Rows[s];
                for (int i = 0; i < row.Length; i++)
                {
                    var n = row[i];
                    float h = n.Type == HuntNodeType.Boss ? bossHalf : half;
                    var c = new V2(laneX[n.Lane], rowY[s]);
                    var r = new Rect(c.X - h, c.Y - h, 2f * h, 2f * h);
                    bool isCleared = s < path.Length && path[s] == i;
                    int ci = s == step ? System.Array.IndexOf(choices, i) : -1;
                    bool pick = ci >= 0;
                    bool hover = pick && r.Contains(Event.current.mousePosition);
                    Color tc = HuntNodeColor(n.Type);
                    Color edge = isCleared ? ink : pick ? tc : new Color(tc.r, tc.g, tc.b, s <= step ? 0.6f : 0.4f);
                    Ui.Fill(r, isCleared ? new Color(ink.r, ink.g, ink.b, 0.28f) : hover ? new Color(ink.r, ink.g, ink.b, 0.18f) : new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 1f));
                    CrtKit.Frame(r, 2f, edge);
                    hmGlyphs.Add(new HmGlyph { Type = n.Type, At = c, Half = h, Col = isCleared ? ink : edge, Tick = isCleared });
                    if (pick)
                    {
                        var ring = new Rect(r.x - 7f, r.y - 7f, r.width + 14f, r.height + 14f);
                        CrtKit.Dashed(ring, ci == myVote ? 2f : 1f, ci == myVote ? CrtStyle.Gold : new Color(CrtStyle.Gold.r, CrtStyle.Gold.g, CrtStyle.Gold.b, 0.7f));
                        LobbyKit.Spaced(new Rect(r.x - 40f, r.y, 28f, r.height), "[" + (ci + 1) + "]", 12, CrtStyle.Gold, TextAnchor.MiddleRight, 0f);
                        string who = HuntVoters(ci, map);
                        if (who.Length > 0)
                            Ui.Label(new Rect(r.xMax + 12f, r.y, 80f, r.height), who, 11, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
                        if (Ui.HitArea(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f)))
                            game.SendHuntVote(ci, false);
                    }
                    else if (isCleared || n.Type == HuntNodeType.Boss)
                        Ui.Label(new Rect(r.xMax + 10f, r.y, 130f, r.height), HuntMap.Name(n.Type).ToUpperInvariant(), 10, n.Type == HuntNodeType.Boss ? HmElite : CrtUi.Ink, TextAnchor.MiddleLeft, true);
                    if (n.Type == HuntNodeType.Shop || n.Type == HuntNodeType.Gamble)
                        Ui.Label(r, n.Type == HuntNodeType.Shop ? "$" : "?", 16, isCleared ? ink : edge, TextAnchor.MiddleCenter, true);
                }
            }
            LobbyKit.Spaced(new Rect(laneX[1] - 60f, rowY[0] - 42f, 120f, 14f), "START", 10, CrtUi.Dim, TextAnchor.MiddleCenter, 3f);
            if (step < HuntMap.Steps)
                LobbyKit.Spaced(new Rect(mapR.x + 14f, rowY[step] - 8f, 54f, 16f), "NEXT", 10, CrtStyle.Gold, TextAnchor.MiddleLeft, 2f);

            // ---- The information (right) ----
            float x = 532f, w = W - x - 24f, y = 58f;
            LobbyKit.Heading(x, y, w, "HUNT", hunt != null ? hunt.Name : "Boss Hunt", calm);
            y += 36f;
            float tw = (w - 3f * 10f) / 4f;
            string bossName = hunt != null ? Defs.Get(hunt.Boss).Name : "Boss";
            LobbyKit.Tile(new Rect(x, y, tw, 50f), bossName.ToUpperInvariant(), "TARGET", calm);
            LobbyKit.Tile(new Rect(x + (tw + 10f), y, tw, 50f), System.Math.Min(step + 1, HuntMap.Steps) + " / " + HuntMap.Steps, "STEP", calm);
            LobbyKit.Tile(new Rect(x + 2f * (tw + 10f), y, tw, 50f), (game.Me != null ? game.Me.Gold : 0).ToString(), "GOLD", calm);
            LobbyKit.Tile(new Rect(x + 3f * (tw + 10f), y, tw, 50f), map.Clues + " / " + HuntMap.MaxClues, "BOSS CLUES", calm);
            y += 62f;

            LobbyKit.Heading(x, y, w, "PARTY", game.Players.Count + " / 3", calm);
            y += 36f;
            int seat = 0;
            float sw = (w - 2f * 10f) / 3f;
            foreach (var p in game.Players.Values)
            {
                if (seat >= 3)
                    break;
                var sr = new Rect(x + seat * (sw + 10f), y, sw, 58f);
                int v;
                bool voted = game.MapVotes.TryGetValue(p.Id, out v) && v >= 0 && v < choices.Length;
                DkSub(sr);
                CrtKit.Frame(sr, 1f, p.Id == me ? ink : LobbyKit.Ink(0.35f));
                Ui.Label(new Rect(sr.x + 10f, sr.y + 4f, sr.width - 80f, 20f), p.Name, 13, Color.white, TextAnchor.MiddleLeft, true);
                if (game.MapLocked.Contains(p.Id))
                    DkTag(new Rect(sr.xMax - 68f, sr.y + 6f, 60f, 16f), "LOCKED", PhosGreen);
                Ui.Label(new Rect(sr.x + 10f, sr.y + 22f, sr.width - 20f, 16f), Defs.KitName(p.Kit) + " " + p.Def.Name, 11, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(sr.x + 10f, sr.y + 38f, sr.width - 20f, 16f), voted ? "Vote: " + HuntMap.Name(map.Rows[step][choices[v]].Type) : "Choosing...", 11, voted ? CrtStyle.Gold : Ui.Muted, TextAnchor.MiddleLeft);
                seat++;
            }
            y += 70f;

            LobbyKit.Heading(x, y, w, "NEXT SECTOR", choices.Length > 1 ? "Choose one" : "", calm);
            y += 36f;
            for (int k = 0; k < choices.Length; k++)
            {
                var node = map.Rows[step][choices[k]];
                var kr = new Rect(x, y + k * 56f, w, 50f);
                Color tc = HuntNodeColor(node.Type);
                bool sel = k == myVote;
                bool hov = kr.Contains(Event.current.mousePosition);
                DkBox(kr, sel ? CrtStyle.Gold : tc, sel ? 1f : hov ? 0.6f : 0.2f);
                var gr = new Rect(kr.x + 10f, kr.y + 9f, 32f, 32f);
                CrtKit.Frame(gr, 2f, tc);
                hmGlyphs.Add(new HmGlyph { Type = node.Type, At = new V2(gr.center.x, gr.center.y), Half = 16f, Col = tc });
                if (node.Type == HuntNodeType.Shop || node.Type == HuntNodeType.Gamble)
                    Ui.Label(gr, node.Type == HuntNodeType.Shop ? "$" : "?", 18, tc, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(kr.x + 56f, kr.y + 5f, w - 140f, 22f), HuntMap.Name(node.Type).ToUpperInvariant(), 14, tc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(kr.x + 56f, kr.y + 26f, w - 140f, 18f), (HuntMap.Danger(node.Type) > 0 ? "Danger " + new string('■', HuntMap.Danger(node.Type)) : "No fight") + "  ·  " + HuntMap.Reward(node.Type), 11, CrtStyle.Desc, TextAnchor.MiddleLeft);
                string who = HuntVoters(k, map);
                if (who.Length > 0)
                    Ui.Label(new Rect(kr.xMax - 130f, kr.y + 5f, 70f, 22f), who, 12, CrtStyle.Gold, TextAnchor.MiddleRight, true);
                DkTag(new Rect(kr.xMax - 40f, kr.y + 17f, 26f, 16f), (k + 1).ToString(), CrtUi.Dim);
                if (Ui.HitArea(kr))
                    game.SendHuntVote(k, false);
            }
            y += 3 * 56f + 4f;

            if (choices.Length > 0)
            {
                var dn = map.Rows[step][choices[Mathf.Min(detail, choices.Length - 1)]];
                Color dc = HuntNodeColor(dn.Type);
                LobbyKit.Heading(x, y, w, "DETAIL", "", calm);
                y += 36f;
                var dr = new Rect(x, y, w, 96f);
                LobbyKit.Panel(dr);
                Ui.Label(new Rect(dr.x + 16f, dr.y + 10f, w - 32f, 22f), HuntMap.Name(dn.Type).ToUpperInvariant(), 16, dc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(dr.x + 16f, dr.y + 34f, w - 280f, 40f), HuntMap.Blurb(dn.Type), 12, CrtStyle.Body, TextAnchor.UpperLeft, false, true);
                LobbyKit.Tile(new Rect(dr.xMax - 250f, dr.y + 16f, 110f, 56f), HuntMap.Danger(dn.Type) > 0 ? new string('■', HuntMap.Danger(dn.Type)) : "-", "DANGER", calm);
                LobbyKit.Tile(new Rect(dr.xMax - 130f, dr.y + 16f, 116f, 56f), HuntMap.Reward(dn.Type), "REWARD", calm);
                y += 108f;
            }

            bool locked = game.MapLocked.Contains(me);
            var lr = new Rect(x, H - 24f - 46f - 22f, w, 40f);
            GUI.enabled = myVote >= 0;
            if (LobbyKit.Key(lr, locked ? "LOCKED  ·  TAP TO CHANGE" : "LOCK IN VOTE", locked, calm, accent: PhosGreen, size: 15))
            {
                game.SendHuntVote(myVote, !locked);
            }
            GUI.enabled = true;
            Ui.Label(new Rect(x, lr.yMax + 4f, w, 18f), myVote < 0 ? "Pick a sector first (1-3)" : locked ? "Waiting for the others" : "The next fight starts when everyone has locked in, or the timer ends", 11, Ui.Muted, TextAnchor.MiddleCenter);

            DrawHuntGlyphs();

            // Keys 1-3 vote.
            var ev = Event.current;
            if (ev.type == EventType.KeyDown && ev.keyCode >= KeyCode.Alpha1 && ev.keyCode <= KeyCode.Alpha3)
            {
                int k = ev.keyCode - KeyCode.Alpha1;
                if (k < choices.Length)
                    game.SendHuntVote(k, false);
            }
        }

        /// Who voted for a choice, as "P1 P3".
        string HuntVoters(int choice, HuntMap map)
        {
            string s = "";
            int seat = 1;
            foreach (var p in game.Players.Values)
            {
                int v;
                if (game.MapVotes.TryGetValue(p.Id, out v) && v == choice)
                    s += (s.Length > 0 ? " " : "") + "P" + seat;
                seat++;
            }
            return s;
        }

        /// The glyph of each node type, in thin lines: triangle skirmish, diamond elite, chevrons ambush, hexagon contraband,
        /// plus rest, a square with a plus for the boss (the shop's $ and the gamble's ? are text).
        void DrawHuntGlyphs()
        {
            if (hmGlyphs.Count == 0 || !BeginUiGl())
            {
                hmGlyphs.Clear();
                return;
            }
            foreach (var g in hmGlyphs)
            {
                var col = HmRgba(g.Col);
                float h = g.Half, q = h * 0.55f, w = 1.8f;
                V2 c = g.At;
                if (g.Tick)
                {
                    var dark = HmRgba(CrtUi.Ink);
                    Line(c + new V2(-q * 0.7f, 0f), c + new V2(-q * 0.1f, q * 0.6f), 2.6f, dark);
                    Line(c + new V2(-q * 0.1f, q * 0.6f), c + new V2(q * 0.8f, -q * 0.6f), 2.6f, dark);
                    continue;
                }
                switch (g.Type)
                {
                    case HuntNodeType.Skirmish:
                        HmPoly(c, col, w, new V2(0f, -q), new V2(q, q * 0.8f), new V2(-q, q * 0.8f));
                        break;
                    case HuntNodeType.Elite:
                        HmPoly(c, col, w, new V2(0f, -q * 1.3f), new V2(q * 1.3f, 0f), new V2(0f, q * 1.3f), new V2(-q * 1.3f, 0f));
                        HmPoly(c, col, w, new V2(0f, -q * 0.55f), new V2(q * 0.55f, 0f), new V2(0f, q * 0.55f), new V2(-q * 0.55f, 0f));
                        break;
                    case HuntNodeType.Ambush:
                        Line(c + new V2(-q, -q), c + new V2(q * 0.4f, 0f), w, col); Line(c + new V2(q * 0.4f, 0f), c + new V2(-q, q), w, col);
                        Line(c + new V2(0f, -q), c + new V2(q * 1.1f, 0f), w, col); Line(c + new V2(q * 1.1f, 0f), c + new V2(0f, q), w, col);
                        break;
                    case HuntNodeType.Contraband:
                    {
                        var pts = new V2[6];
                        for (int k = 0; k < 6; k++) pts[k] = V2.FromAngle(MathX.Tau * k / 6f) * (q * 1.25f);
                        HmPoly(c, col, w, pts);
                        break;
                    }
                    case HuntNodeType.Rest:
                        Line(c + new V2(0f, -q), c + new V2(0f, q), w * 1.4f, col); Line(c + new V2(-q, 0f), c + new V2(q, 0f), w * 1.4f, col);
                        break;
                    case HuntNodeType.Boss:
                        HmPoly(c, col, w, new V2(-h * 0.55f, -h * 0.55f), new V2(h * 0.55f, -h * 0.55f), new V2(h * 0.55f, h * 0.55f), new V2(-h * 0.55f, h * 0.55f));
                        Line(c + new V2(0f, -h * 0.28f), c + new V2(0f, h * 0.28f), w, col); Line(c + new V2(-h * 0.28f, 0f), c + new V2(h * 0.28f, 0f), w, col);
                        break;
                }
            }
            EndUiGl();
            hmGlyphs.Clear();
        }

        void HmPoly(V2 c, Rgba col, float w, params V2[] pts)
        {
            for (int k = 0; k < pts.Length; k++)
                Line(c + pts[k], c + pts[(k + 1) % pts.Length], w, col);
        }
    }
}
