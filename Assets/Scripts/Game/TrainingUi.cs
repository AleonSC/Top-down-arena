using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Game Mode screen's **Training** page (Core/Training.cs, Docs/Training.md): the drills as compact cards under their
    /// group's heading (number, name, keys, medal), then a big scope with a schematic of the drill shown and its brief: goal,
    /// medal times, your best, its keys and START DRILL. Hovering a card shows it; a click picks it. A drill not built yet
    /// shows SOON and can't start. Preview: `TRAINING=1 shot.sh x lobby` (2: drill 1 built, with a gold).
    public partial class ArenaApp
    {
        const int TrainingTab = 13;
        static readonly Color TrainColor = new Color(0.45f, 0.9f, 1f);
        static readonly Color MedalSilver = new Color(0.78f, 0.85f, 0.94f), MedalBronze = new Color(0.88f, 0.56f, 0.32f);
        int trainPick = -1;  // the drill picked (-1: the first built one you haven't cleared)

        static string MedalName(int m) => m == 3 ? "GOLD" : m == 2 ? "SILVER" : m == 1 ? "BRONZE" : "NEW";
        static Color MedalCol(int m) => m == 3 ? CrtStyle.Gold : m == 2 ? MedalSilver : m == 1 ? MedalBronze : TrainColor;

        /// The medal you hold on drill i (0: none).
        int DrillMedal(int i)
        {
            var d = Training.Drills[i];
            return Training.Medal(d, (game.Profile ?? new Profile()).DrillBestTime(d.Key));
        }

        int DrillsCleared()
        {
            int n = 0;
            for (int i = 0; i < Training.Drills.Length; i++)
                if (DrillMedal(i) > 0)
                    n++;
            return n;
        }

        int DefaultDrill()
        {
            for (int i = 0; i < Training.Drills.Length; i++)
                if (Training.Drills[i].Built && DrillMedal(i) == 0)
                    return i;
            return 0;
        }

        void DrawTrainingTab(Rect r, float t)
        {
            var drills = Training.Drills;
            if (trainPick < 0 || trainPick >= drills.Length)
                trainPick = DefaultDrill();
            LobbyKit.Panel(r);
            DrawHoloGlass(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), Time.unscaledTime, TrainColor);
            int cleared = DrillsCleared();
            var head = new ModePage { Title = "TRAINING", Acc = TrainColor, Pct = cleared / (float)drills.Length, Ring = cleared + "/" + drills.Length,
                Tagline = Training.BuiltCount == 0 ? "Short drills, one mechanic each. They open here one at a time." : "Short drills, one mechanic each. Earn a medal, or just practise." };
            float top = ModeHeadBand(r, head) + 10f;
            float x = r.x + 20f, w = r.width - 40f, bottom = r.yMax - 18f;

            // The drills: each group under its heading, its cards in a row.
            const float gap = 22f, cgap = 8f, cardH = 46f;
            int ng = Training.Groups.Length;
            float gw = (w - (ng - 1) * gap) / ng;
            var mp = Event.current.mousePosition;
            int hover = -1;
            for (int g = 0; g < ng; g++)
            {
                float gx = x + g * (gw + gap);
                var inGroup = new List<int>();
                int done = 0;
                for (int i = 0; i < drills.Length; i++)
                    if (drills[i].Group == Training.Groups[g])
                    {
                        inGroup.Add(i);
                        if (DrillMedal(i) > 0)
                            done++;
                    }
                LobbyKit.Heading(gx, top, gw, Training.Groups[g], done + " / " + inGroup.Count, fxReduced, 14);
                float cw = (gw - (inGroup.Count - 1) * cgap) / inGroup.Count;
                for (int k = 0; k < inGroup.Count; k++)
                {
                    int i = inGroup[k];
                    var c = new Rect(gx + k * (cw + cgap), top + 34f, cw, cardH);
                    bool over = GUI.enabled && c.Contains(mp);
                    if (over)
                        hover = i;
                    if (Ui.HitArea(c))
                        trainPick = i;
                    DrillCard(c, i, i == trainPick, over);
                }
            }

            // The briefing: a big scope on the left, the brief on the right.
            float by = top + 34f + cardH + 16f;
            TrainRule(x, by, w);
            by += 14f;
            int s = hover >= 0 ? hover : trainPick;
            float sw = w * 0.56f;
            var scope = new Rect(x, by, sw, bottom - by);
            ModeScope(scope, drills[s].Group, "DRILL " + (s + 1).ToString("00"));
            DrillSchematic(new Rect(scope.x + 12f, scope.y + 26f, scope.width - 24f, scope.height - 40f), drills[s].Key);
            ModeScopeFinish(scope);
            DrillBrief(new Rect(scope.xMax + 22f, by, w - sw - 22f, bottom - by), s);
        }

        /// The rule between the cards and the briefing: a line with a scale of ticks.
        static void TrainRule(float x, float y, float w)
        {
            Ui.Fill(new Rect(x, y, w, 1f), TrainColor.WithAlpha(0.3f));
            for (float tx = x; tx < x + w; tx += 12f)
                Ui.Fill(new Rect(tx, y + 2f, 1f, 3f), TrainColor.WithAlpha(0.18f));
        }

        /// A drill card: number and name on top, its keys under them, its medal (or NEW, or SOON) on the right.
        void DrillCard(Rect c, int i, bool top, bool hover)
        {
            var d = Training.Drills[i];
            int medal = DrillMedal(i);
            if (d.Built || top || hover)
                CrtKit.SoftBox(c, top ? TrainColor : CrtStyle.Ink, top, hover, false);
            else
            {
                Ui.Fill(c, CrtStyle.Dim.WithAlpha(0.03f));
                CrtKit.Dashed(c, 1f, CrtStyle.Dim.WithAlpha(0.35f));
            }
            Color tc = d.Built || top ? CrtKit.SoftText(top ? TrainColor : CrtStyle.Ink, hover || top) : CrtStyle.Ink.WithAlpha(0.7f);
            Ui.Label(new Rect(c.x + 10f, c.y + 5f, 22f, 18f), (i + 1).ToString("00"), 10, tc.WithAlpha(0.6f), TextAnchor.MiddleLeft);
            CrtUi.Text(new Rect(c.x + 32f, c.y + 4f, c.width - 74f, 20f), d.Name, 13, tc, TextAnchor.MiddleLeft, true, false);
            KeyRow(c.x + 32f, c.yMax - 19f, d.Keys, 14f, top ? TrainColor : CrtStyle.Ink);
            if (!d.Built)
                LobbyKit.Spaced(new Rect(c.xMax - 46f, c.center.y - 7f, 40f, 14f), "SOON", 9, CrtStyle.Dim, TextAnchor.MiddleCenter, 1f);
            else if (medal > 0)
                MedalBadge(new Vector2(c.xMax - 20f, c.center.y), medal, 9f);
            else
                LobbyKit.Spaced(new Rect(c.xMax - 44f, c.center.y - 7f, 40f, 14f), "NEW", 9, TrainColor, TextAnchor.MiddleCenter, 1f);
        }

        /// The brief: name, goal, copy, the medal times (the ones you hold lit), your best, the keys and START DRILL.
        void DrillBrief(Rect b, int i)
        {
            var d = Training.Drills[i];
            int medal = DrillMedal(i);
            float best = (game.Profile ?? new Profile()).DrillBestTime(d.Key);
            DkSub(b);
            Ui.Fill(new Rect(b.x, b.y, 3f, b.height), TrainColor.WithAlpha(0.8f));
            float x = b.x + 18f, w = b.width - 36f, y = b.y + 12f;
            LobbyKit.Spaced(new Rect(x, y, w, 14f), "DRILL " + (i + 1).ToString("00") + "  ·  " + d.Group, 10, TrainColor, TextAnchor.MiddleLeft, 1.6f);
            Ui.Glitch(new Rect(x, y + 16f, w, 30f), d.Name.ToUpperInvariant(), 22, Color.white, TextAnchor.MiddleLeft);
            y += 52f;
            Ui.Label(new Rect(x, y, w, 18f), "GOAL  ·  " + d.Goal.ToUpperInvariant(), 12, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            y += 22f;
            Ui.Label(new Rect(x, y, w, 34f), d.Copy, 12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            y += 42f;
            float mw = (w - 16f) / 3f;
            for (int m = 3; m >= 1; m--)
            {
                var mr = new Rect(x + (3 - m) * (mw + 8f), y, mw, 46f);
                bool held = medal >= m;
                Ui.Fill(mr, MedalCol(m).WithAlpha(held ? 0.12f : 0.03f));
                HuntBox(mr, MedalCol(m).WithAlpha(held ? 0.7f : 0.25f));
                MedalBadge(new Vector2(mr.x + 18f, mr.center.y), held ? m : -m, 9f);
                LobbyKit.Spaced(new Rect(mr.x + 34f, mr.y + 6f, mw - 40f, 14f), MedalName(m), 10, MedalCol(m).WithAlpha(held ? 1f : 0.55f), TextAnchor.MiddleLeft, 1.4f);
                string need = m == 3 ? "UNDER " + ModeClock(d.Gold) : m == 2 ? "UNDER " + ModeClock(d.Silver) : "FINISH IT";
                Ui.Label(new Rect(mr.x + 34f, mr.y + 22f, mw - 40f, 18f), need, 11, CrtStyle.Ink.WithAlpha(held ? 0.9f : 0.5f), TextAnchor.MiddleLeft, true);
            }
            y += 58f;
            Ui.Label(new Rect(x, y, 80f, 20f), "YOUR BEST", 11, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 80f, y, 80f, 20f), best > 0f ? best.ToString("0.0") + " s" : "-", 15, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 170f, y, 50f, 20f), "KEYS", 11, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
            KeyRow(x + 214f, y + 1f, d.Keys, 18f, CrtStyle.Ink);

            var go = new Rect(b.xMax - 218f, b.yMax - 56f, 200f, 42f);
            bool can = d.Built && session.IsHost;
            LobbyGlowBox(go);
            string sub = !d.Built ? "COMING SOON" : !session.IsHost ? "THE HOST STARTS IT" : "R TO RETRY";
            if (DkKey(go, "START DRILL  >", can, fxReduced, size: 16, minSize: 12, accent: TrainColor, sub: sub, locked: !can) && can)
                session.StartDrill(i);  // DrillGame.cs
            Ui.Label(new Rect(x, b.yMax - 50f, w - 230f, 30f), "In a party, everyone runs the drill in their own lane.", 11, CrtStyle.Ink.WithAlpha(0.55f), TextAnchor.MiddleLeft, false, true);
        }

        /// Keycaps in a row from (x, y), `h` tall.
        static void KeyRow(float x, float y, string[] keys, float h, Color c)
        {
            int size = h > 15f ? 10 : 9;
            foreach (var k in keys)
            {
                float kw = Mathf.Max(h + 4f, LobbyKit.SpacedWidth(k, size, 1f) + 12f);
                var kr = new Rect(x, y, kw, h);
                Ui.Fill(kr, c.WithAlpha(0.08f));
                HuntBox(kr, c.WithAlpha(0.55f));
                Ui.Fill(new Rect(kr.x + 1f, kr.yMax - 3f, kr.width - 2f, 2f), c.WithAlpha(0.18f));
                LobbyKit.Spaced(new Rect(kr.x, kr.y - 1f, kr.width, kr.height), k, size, c, TextAnchor.MiddleCenter, 1f);
                x += kw + 5f;
            }
        }

        /// A medal: a filled disc in its colour (`m` 1-3), or a hollow ring in it when not held (`m` -1..-3).
        static void MedalBadge(Vector2 c, int m, float rad)
        {
            var r = new Rect(c.x - rad, c.y - rad, rad * 2f, rad * 2f);
            if (m > 0)
            {
                Ui.RadialGlow(new Rect(c.x - rad * 2f, c.y - rad * 2f, rad * 4f, rad * 4f), MedalCol(m).WithAlpha(0.35f));
                Ui.Dot(r, MedalCol(m));
                Ui.Dot(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Color.white.WithAlpha(0.25f), true);
            }
            else
                Ui.Dot(r, MedalCol(-m).WithAlpha(0.4f), true);
        }

        static Vector2 Lerp2(Vector2 a, Vector2 b, float k) => a + (b - a) * k;

        static void Disc(Vector2 p, float rad, Color c, bool ring = false) => Ui.Dot(new Rect(p.x - rad, p.y - rad, rad * 2f, rad * 2f), c, ring);

        static void Dots(Vector2 p0, Vector2 p1, Color c, float gap = 7f)
        {
            float len = (p1 - p0).magnitude;
            for (float s = 0; s < len; s += gap)
                Disc(Lerp2(p0, p1, s / len), 1.3f, c);
        }

        /// A schematic of the drill in a scope area: the arena floor, you (in your colour) and what the drill puts round you.
        void DrillSchematic(Rect a, string key)
        {
            var you = ScopeGlow();
            var red = CrtStyle.Danger;
            for (float gx = a.x + 20f; gx < a.xMax; gx += 32f)
                Ui.Fill(new Rect(gx, a.y + 4f, 1f, a.height - 8f), CrtStyle.Ink.WithAlpha(0.05f));
            for (float gy = a.y + 16f; gy < a.yMax; gy += 32f)
                Ui.Fill(new Rect(a.x + 4f, gy, a.width - 8f, 1f), CrtStyle.Ink.WithAlpha(0.05f));
            Vector2 C = a.center;
            float u = Mathf.Min(a.width, a.height) / 6f;
            System.Func<float, float, Vector2> P = (ux, uy) => C + new Vector2(ux * u, uy * u);
            switch (key)
            {
                case "move":  // beacons on a path
                    var pts = new[] { P(-2.2f, 1f), P(-1.2f, -1.4f), P(0.4f, 0.6f), P(1.6f, -1.2f), P(2.4f, 1f) };
                    for (int k = 0; k < pts.Length; k++)
                    {
                        if (k > 0) Dots(pts[k - 1], pts[k], you.WithAlpha(k <= 2 ? 0.7f : 0.2f));
                        Disc(pts[k], 11f, (k < 2 ? CrtStyle.Gold : TrainColor).WithAlpha(k < 2 ? 0.9f : 0.5f), true);
                        if (Training.MoveShootStep(k))
                            Disc(pts[k], 5f, red.WithAlpha(0.85f));  // a target to shoot
                    }
                    Disc(Lerp2(pts[2], pts[3], 0.35f), 8f, you);
                    break;
                case "dodge":  // walls of shots, a dash through one
                    for (int k = 0; k < 3; k++)
                        for (float y = a.y + 14f; y < a.yMax - 10f; y += 13f)
                            if (Mathf.Abs(y - C.y) > 9f || k != 1) Disc(new Vector2(C.x + (k - 1) * 1.4f * u, y), 3f, red);
                    for (int k = 0; k < 4; k++) Disc(P(-1.1f + k * 0.4f, 0f), 7f, you.WithAlpha(0.15f + k * 0.2f));
                    Disc(P(0.5f, 0f), 8f, you);
                    break;
                case "wallkick":  // bounce off a wall, gold ring
                    Ui.Fill(new Rect(P(2f, 0f).x, a.y + 10f, 8f, a.height - 20f), CrtStyle.Ink.WithAlpha(0.5f));
                    Dots(P(-2.2f, 1.2f), P(1.85f, -0.2f), you.WithAlpha(0.6f));
                    Dots(P(1.85f, -0.2f), P(-0.4f, -1.6f), CrtStyle.Gold.WithAlpha(0.8f));
                    Disc(P(1.85f, -0.2f), 14f, CrtStyle.Gold, true);
                    Disc(P(-0.4f, -1.6f), 8f, you);
                    break;
                case "shoot":  // targets and a stream of shots
                    for (int k = 0; k < 4; k++)
                    {
                        var tp = P(1.8f, -1.6f + k * 1.05f);
                        Disc(tp, 12f, red.WithAlpha(k == 1 ? 0.25f : 0.8f), true);
                        Disc(tp, 4f, red.WithAlpha(k == 1 ? 0.25f : 0.8f));
                    }
                    Disc(P(-2f, 0f), 8f, you);
                    for (int k = 1; k < 6; k++) Disc(Lerp2(P(-2f, 0f), P(1.8f, -0.55f), k / 6.5f), 2.5f, you);
                    break;
                case "parry":  // a shield arc taking shots
                    Disc(P(-0.6f, 0f), 8f, you);
                    for (int k = -5; k <= 5; k++)
                    {
                        float an = k * 0.13f;
                        Disc(P(-0.6f, 0f) + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * 26f, 2.2f, you);
                    }
                    for (int k = 0; k < 4; k++) Disc(P(0.6f + k * 0.55f, (k - 1.5f) * 0.25f), 3f, red);
                    Disc(P(2.6f, 0f), 10f, red.WithAlpha(0.7f), true);
                    break;
                default:  // "tells": an aim line before the shot
                    Disc(P(2f, -1.3f), 10f, red);
                    Dots(P(2f, -1.3f), P(-2.6f, 1.2f), red.WithAlpha(0.7f), 5f);
                    Disc(P(-1.2f, 0.45f), 8f, you.WithAlpha(0.25f));
                    Disc(P(-0.8f, 1.4f), 8f, you);
                    break;
            }
        }
    }
}
