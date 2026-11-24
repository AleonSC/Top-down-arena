using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// MOCKUP: the Game Mode screen's **Training** page (drills that teach one mechanic each). Layout only: the drills, their
    /// medals and progress are placeholders until each drill is built. Three layouts to choose from (`trainLayout`):
    /// 1 the mode page (list, scope, sidebar), 2 a board of drill cards by group, 3 a course of nodes over a big briefing.
    /// Preview: `TRAINING=1|2|3 LOBBYTAB=13 shot.sh x lobby`.
    public partial class ArenaApp
    {
        const int TrainingTab = 13;
        static readonly Color TrainColor = new Color(0.45f, 0.9f, 1f);
        static readonly Color MedalSilver = new Color(0.78f, 0.85f, 0.94f), MedalBronze = new Color(0.88f, 0.56f, 0.32f);
        int trainLayout;   // 0: off (mockup layouts 1-3)
        int trainPick = 3; // the drill picked

        class Drill
        {
            public string Name, Group, Goal, Copy;
            public string[] Keys;
            public int Medal;   // mock progress: 3 gold, 2 silver, 1 bronze, 0 new, -1 not tried
            public string Best;
        }

        static readonly string[] DrillGroups = { "MOVEMENT", "COMBAT" };

        static readonly Drill[] Drills =
        {
            new Drill { Group = "MOVEMENT", Name = "Move and Aim", Keys = new[] { "WASD", "MOUSE" }, Medal = 3, Best = "0:14",
                Goal = "Touch the five beacons", Copy = "Move with WASD, aim with the mouse. Beacons light up one at a time." },
            new Drill { Group = "MOVEMENT", Name = "Dodge", Keys = new[] { "CTRL" }, Medal = 2, Best = "0:31",
                Goal = "Dash through six bullet walls", Copy = "A dodge is a short dash with a blink of invulnerability. Time it into the wall." },
            new Drill { Group = "MOVEMENT", Name = "Wall Kick", Keys = new[] { "HOLD CTRL" }, Medal = 1, Best = "0:48",
                Goal = "Kick off three walls, reach the flag", Copy = "Hold after a dodge to sprint. Dodge into a wall to bounce off it with a free dodge." },
            new Drill { Group = "COMBAT", Name = "Shoot", Keys = new[] { "LMB" }, Medal = 0,
                Goal = "Break twelve targets", Copy = "Hold left click to fire. Targets slide faster as the drill goes on." },
            new Drill { Group = "COMBAT", Name = "Parry", Keys = new[] { "RMB" }, Medal = -1,
                Goal = "Block ten shots", Copy = "Hold right click to raise the parry shield. It blocks shots from the front." },
            new Drill { Group = "COMBAT", Name = "Read the Tells", Keys = new[] { "CTRL" }, Medal = -1,
                Goal = "Slip eight wind-ups", Copy = "A Sniper's line, a Lunger's crouch: every big hit is shown first. Move when you see it." },
        };

        static string MedalName(int m) => m == 3 ? "GOLD" : m == 2 ? "SILVER" : m == 1 ? "BRONZE" : m == 0 ? "NEW" : "NOT TRIED";
        static Color MedalCol(int m) => m == 3 ? CrtStyle.Gold : m == 2 ? MedalSilver : m == 1 ? MedalBronze : m == 0 ? TrainColor : CrtStyle.Ink.WithAlpha(0.55f);
        static int DrillsDone { get { int n = 0; foreach (var d in Drills) if (d.Medal > 0) n++; return n; } }

        void DrawTrainingTab(Rect r, float t)
        {
            if (trainLayout == 2)
                DrawTrainBoard(r, t);
            else if (trainLayout == 3)
                DrawTrainCourse(r, t);
            else
                DrawModePage(r, t, TrainPage());
        }

        // ---------- Layout 1: the mode page ----------

        ModePage TrainPage()
        {
            var p = new ModePage { Title = "TRAINING", Acc = TrainColor, Tagline = "Short drills, one mechanic each. Earn a medal, or just practise.",
                Rules = new[] { "YOUR LOADOUT", "RETRY ANY TIME", "DUMMIES DON'T HIT BACK", "NO XP", "NO RECORDS" },
                RecHead = "THIS DRILL", RulesHead = "IN TRAINING", ListHead = "DRILLS", ListCap = DrillsDone + " of " + Drills.Length + " cleared",
                Pct = DrillsDone / (float)Drills.Length, Ring = DrillsDone + "/" + Drills.Length, Default = trainPick };
            for (int i = 0; i < Drills.Length; i++)
            {
                var d = Drills[i];
                p.Rows.Add(new ModeRowData { Num = (i + 1).ToString("00"), Name = d.Name, Known = true, Lit = d.Medal >= 0, Model = EnemyType.Grunt,
                    Chip = MedalName(d.Medal), ChipCol = MedalCol(d.Medal) });
            }
            p.OnPick = i => trainPick = i;
            p.ScopeDraw = (g, shown) => DrillSchematic(g, shown, Time.unscaledTime);
            p.Focus = i => new ModeFocus { ScopeL = Drills[i].Group, ScopeR = "DRILL " + (i + 1).ToString("00"), Name = Drills[i].Name.ToUpperInvariant(),
                Copy = Drills[i].Goal + ". " + Drills[i].Copy,
                RecLabels = new[] { "BEST", "GOLD", "SILVER", "BRONZE" },
                RecVals = new[] { Drills[i].Best ?? "-", "0:20", "0:35", "CLEAR" } };
            p.NoModels = true;
            return p;
        }

        // ---------- Layout 2: the board ----------

        void DrawTrainBoard(Rect r, float t)
        {
            LobbyKit.Panel(r);
            DrawHoloGlass(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), Time.unscaledTime, TrainColor);
            float top = ModeHeadBand(r, TrainHead()) + 6f;
            float x = r.x + 20f, w = r.width - 40f, bottom = r.yMax - 18f;
            // Four groups side by side, three cards each.
            const float gap = 16f, cardH = 62f;
            float gw = (w - 3f * gap) / 4f;
            var mp = Event.current.mousePosition;
            int hover = -1;
            for (int g = 0; g < DrillGroups.Length; g++)
            {
                float gx = x + g * (gw + gap);
                LobbyKit.Heading(gx, top, gw, DrillGroups[g], GroupCap(g), fxReduced, 14);
                int k = 0;
                for (int i = 0; i < Drills.Length; i++)
                {
                    if (Drills[i].Group != DrillGroups[g]) continue;
                    var c = new Rect(gx, top + 36f + k * (cardH + 8f), gw, cardH);
                    if (c.Contains(mp)) hover = i;
                    if (Ui.HitArea(c)) trainPick = i;
                    DrillCard(c, i, i == trainPick, c.Contains(mp));
                    k++;
                }
            }
            // The briefing strip under the board: schematic, then the goal, medals and START DRILL.
            float by = top + 36f + 3f * (cardH + 8f) + 10f;
            ModeSeparatorH(x, by, w, TrainColor);
            by += 14f;
            int s = hover >= 0 ? hover : trainPick;
            var scope = new Rect(x, by, 360f, bottom - by);
            ModeScope(scope, Drills[s].Group, "DRILL " + (s + 1).ToString("00"));
            DrillSchematic(new Rect(scope.x + 12f, scope.y + 26f, scope.width - 24f, scope.height - 40f), s, t);
            ModeScopeFinish(scope);
            DrillBrief(new Rect(scope.xMax + 22f, by, w - scope.width - 22f, bottom - by), s, true);
        }

        string GroupCap(int g)
        {
            int n = 0, done = 0;
            foreach (var d in Drills)
                if (d.Group == DrillGroups[g]) { n++; if (d.Medal > 0) done++; }
            return done + " / " + n;
        }

        /// A drill card: number, name, keys, and its medal in the corner.
        void DrillCard(Rect c, int i, bool top, bool hover)
        {
            var d = Drills[i];
            bool lit = d.Medal >= 0;
            if (lit || top || hover)
                CrtKit.SoftBox(c, top ? TrainColor : CrtStyle.Ink, top, hover, false);
            else
            {
                Ui.Fill(c, CrtStyle.Dim.WithAlpha(0.03f));
                CrtKit.Dashed(c, 1f, CrtStyle.Dim.WithAlpha(0.35f));
            }
            Color tc = lit || top ? CrtKit.SoftText(top ? TrainColor : CrtStyle.Ink, hover || top) : CrtStyle.Ink.WithAlpha(0.7f);
            Ui.Label(new Rect(c.x + 10f, c.y + 6f, 30f, 16f), (i + 1).ToString("00"), 10, tc.WithAlpha(0.6f), TextAnchor.MiddleLeft);
            CrtUi.Text(new Rect(c.x + 10f, c.y + 20f, c.width - 60f, 20f), d.Name, 14, tc, TextAnchor.MiddleLeft, true, false);
            KeyRow(c.x + 10f, c.yMax - 19f, d.Keys, 14f, top ? TrainColor : CrtStyle.Ink);
            MedalBadge(new Vector2(c.xMax - 22f, c.y + 22f), d.Medal, 12f);
        }

        /// A compact drill card: number and name on top, its keys under them, its medal on the right.
        void DrillChip(Rect c, int i, bool top, bool hover)
        {
            var d = Drills[i];
            bool lit = d.Medal >= 0;
            if (lit || top || hover)
                CrtKit.SoftBox(c, top ? TrainColor : CrtStyle.Ink, top, hover, false);
            else
            {
                Ui.Fill(c, CrtStyle.Dim.WithAlpha(0.03f));
                CrtKit.Dashed(c, 1f, CrtStyle.Dim.WithAlpha(0.35f));
            }
            Color tc = lit || top ? CrtKit.SoftText(top ? TrainColor : CrtStyle.Ink, hover || top) : CrtStyle.Ink.WithAlpha(0.7f);
            Ui.Label(new Rect(c.x + 10f, c.y + 5f, 22f, 18f), (i + 1).ToString("00"), 10, tc.WithAlpha(0.6f), TextAnchor.MiddleLeft);
            CrtUi.Text(new Rect(c.x + 32f, c.y + 4f, c.width - 64f, 20f), d.Name, 13, tc, TextAnchor.MiddleLeft, true, false);
            KeyRow(c.x + 32f, c.yMax - 19f, d.Keys, 14f, top ? TrainColor : CrtStyle.Ink);
            MedalBadge(new Vector2(c.xMax - 20f, c.center.y), d.Medal, 9f);
        }

        // ---------- Layout 3: the course ----------

        void DrawTrainCourse(Rect r, float t)
        {
            LobbyKit.Panel(r);
            DrawHoloGlass(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), Time.unscaledTime, TrainColor);
            float top = ModeHeadBand(r, TrainHead()) + 10f;
            float x = r.x + 20f, w = r.width - 40f, bottom = r.yMax - 18f;
            // The drills: each group under its heading, its cards in a row.
            const float gap = 22f, cgap = 8f, cardH = 46f;
            int ng = DrillGroups.Length;
            float gw = (w - (ng - 1) * gap) / ng;
            var mp = Event.current.mousePosition;
            int hover = -1;
            for (int g = 0; g < ng; g++)
            {
                float gx = x + g * (gw + gap);
                LobbyKit.Heading(gx, top, gw, DrillGroups[g], GroupCap(g), fxReduced, 14);
                var inGroup = new List<int>();
                for (int i = 0; i < Drills.Length; i++)
                    if (Drills[i].Group == DrillGroups[g]) inGroup.Add(i);
                float cw = (gw - (inGroup.Count - 1) * cgap) / inGroup.Count;
                for (int k = 0; k < inGroup.Count; k++)
                {
                    int i = inGroup[k];
                    var c = new Rect(gx + k * (cw + cgap), top + 34f, cw, cardH);
                    if (c.Contains(mp)) hover = i;
                    if (Ui.HitArea(c)) trainPick = i;
                    DrillChip(c, i, i == trainPick, c.Contains(mp));
                }
            }
            // The briefing: a big scope on the left, the brief on the right.
            float by = top + 34f + cardH + 16f;
            ModeSeparatorH(x, by, w, TrainColor);
            by += 14f;
            int s = hover >= 0 ? hover : trainPick;
            float sw = w * 0.56f;
            var scope = new Rect(x, by, sw, bottom - by);
            ModeScope(scope, Drills[s].Group, "DRILL " + (s + 1).ToString("00"));
            DrillSchematic(new Rect(scope.x + 12f, scope.y + 26f, scope.width - 24f, scope.height - 40f), s, t);
            ModeScopeFinish(scope);
            DrillBrief(new Rect(scope.xMax + 22f, by, w - sw - 22f, bottom - by), s, true);
        }

        // ---------- Shared pieces ----------

        ModePage TrainHead() => new ModePage { Title = "TRAINING", Acc = TrainColor, Pct = DrillsDone / (float)Drills.Length,
            Ring = DrillsDone + "/" + Drills.Length, Tagline = "Short drills, one mechanic each. Earn a medal, or just practise." };

        static void ModeSeparatorH(float x, float y, float w, Color acc)
        {
            Ui.Fill(new Rect(x, y, w, 1f), acc.WithAlpha(0.3f));
            for (float tx = x; tx < x + w; tx += 12f)
                Ui.Fill(new Rect(tx, y + 2f, 1f, 3f), acc.WithAlpha(0.18f));
        }

        /// The brief: name, goal, copy, the medal times and your best, the keys, and START DRILL. Wide (the board's strip):
        /// two columns, the medals stacked on the right over START DRILL; narrow: one column.
        void DrillBrief(Rect b, int i, bool startKey)
        {
            var d = Drills[i];
            DkSub(b);
            Ui.Fill(new Rect(b.x, b.y, 3f, b.height), TrainColor.WithAlpha(0.8f));
            bool wide = b.width > 600f;
            float x = b.x + 18f, w = (wide ? b.width * 0.58f : b.width) - 36f, y = b.y + 12f;
            LobbyKit.Spaced(new Rect(x, y, w, 14f), "DRILL " + (i + 1).ToString("00") + "  ·  " + d.Group, 10, TrainColor, TextAnchor.MiddleLeft, 1.6f);
            Ui.Glitch(new Rect(x, y + 16f, w, 30f), d.Name.ToUpperInvariant(), 22, Color.white, TextAnchor.MiddleLeft);
            y += 52f;
            Ui.Label(new Rect(x, y, w, 18f), "GOAL  ·  " + d.Goal.ToUpperInvariant(), 12, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            y += 22f;
            Ui.Label(new Rect(x, y, w, 34f), d.Copy, 12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            y += 42f;
            // Medals: a row of three (narrow) or a stack (wide), the ones you hold lit.
            float mx = wide ? b.x + b.width * 0.58f + 4f : x, mw = wide ? b.xMax - 18f - mx : (w - 16f) / 3f, my = wide ? b.y + 12f : y;
            float mh = wide ? 34f : 46f;
            for (int m = 3; m >= 1; m--)
            {
                int k = 3 - m;
                var mr = wide ? new Rect(mx, my + k * (mh + 6f), mw, mh) : new Rect(mx + k * (mw + 8f), my, mw, mh);
                bool held = d.Medal >= m;
                Ui.Fill(mr, MedalCol(m).WithAlpha(held ? 0.12f : 0.03f));
                HuntBox(mr, MedalCol(m).WithAlpha(held ? 0.7f : 0.25f));
                MedalBadge(new Vector2(mr.x + 18f, mr.center.y), held ? m : -2, 9f, m);
                string need = m == 3 ? "UNDER 0:20" : m == 2 ? "UNDER 0:35" : "FINISH IT";
                if (wide)
                {
                    LobbyKit.Spaced(new Rect(mr.x + 36f, mr.y, 90f, mh), MedalName(m), 10, MedalCol(m).WithAlpha(held ? 1f : 0.55f), TextAnchor.MiddleLeft, 1.4f);
                    Ui.Label(new Rect(mr.x + 120f, mr.y, mw - 130f, mh), need, 11, CrtStyle.Ink.WithAlpha(held ? 0.9f : 0.5f), TextAnchor.MiddleRight, true);
                }
                else
                {
                    LobbyKit.Spaced(new Rect(mr.x + 34f, mr.y + 6f, mw - 40f, 14f), MedalName(m), 10, MedalCol(m).WithAlpha(held ? 1f : 0.55f), TextAnchor.MiddleLeft, 1.4f);
                    Ui.Label(new Rect(mr.x + 34f, mr.y + 22f, mw - 40f, 18f), need, 11, CrtStyle.Ink.WithAlpha(held ? 0.9f : 0.5f), TextAnchor.MiddleLeft, true);
                }
            }
            if (!wide)
                y += 58f;
            else
                y = b.yMax - 32f;
            Ui.Label(new Rect(x, y, 80f, 20f), "YOUR BEST", 11, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 80f, y, 80f, 20f), d.Best ?? "-", 15, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 170f, y, 50f, 20f), "KEYS", 11, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
            KeyRow(x + 214f, y + 1f, d.Keys, 18f, CrtStyle.Ink);
            if (startKey)
            {
                var go = wide ? new Rect(mx, b.yMax - 54f, mw, 42f) : new Rect(b.xMax - 218f, b.yMax - 56f, 200f, 42f);
                LobbyGlowBox(go);
                DkKey(go, "START DRILL  >", true, fxReduced, size: 16, minSize: 12, accent: TrainColor, sub: "SOLO  ·  R TO RETRY");
                if (!wide)
                    Ui.Label(new Rect(x, b.yMax - 50f, w - 230f, 30f), "Party: everyone runs the drill in their own lane.", 11, CrtStyle.Ink.WithAlpha(0.55f), TextAnchor.MiddleLeft, false, true);
            }
        }

        /// Keycaps in a row from (x, y), `h` tall.
        static void KeyRow(float x, float y, string[] keys, float h, Color c)
        {
            foreach (var k in keys)
            {
                float kw = Mathf.Max(h + 4f, LobbyKit.SpacedWidth(k, h > 15f ? 10 : 9, 1f) + 12f);
                var kr = new Rect(x, y, kw, h);
                Ui.Fill(kr, c.WithAlpha(0.08f));
                HuntBox(kr, c.WithAlpha(0.55f));
                Ui.Fill(new Rect(kr.x + 1f, kr.yMax - 3f, kr.width - 2f, 2f), c.WithAlpha(0.18f));
                LobbyKit.Spaced(new Rect(kr.x, kr.y - 1f, kr.width, kr.height), k, h > 15f ? 10 : 9, c, TextAnchor.MiddleCenter, 1f);
                x += kw + 5f;
            }
        }

        /// A medal: a filled disc with a ring in its colour (`m` 1-3), a hollow ring when not held (-2, `ghost` its colour).
        static void MedalBadge(Vector2 c, int m, float rad, int ghost = 0)
        {
            var r = new Rect(c.x - rad, c.y - rad, rad * 2f, rad * 2f);
            if (m >= 1)
            {
                Ui.RadialGlow(new Rect(c.x - rad * 2f, c.y - rad * 2f, rad * 4f, rad * 4f), MedalCol(m).WithAlpha(0.35f));
                Ui.Dot(r, MedalCol(m));
                Ui.Dot(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Color.white.WithAlpha(0.25f), true);
            }
            else if (m == 0)
                LobbyKit.Spaced(new Rect(c.x - 20f, c.y - 7f, 40f, 14f), "NEW", 9, TrainColor, TextAnchor.MiddleCenter, 1f);
            else
                Ui.Dot(r, (ghost > 0 ? MedalCol(ghost) : CrtStyle.Dim).WithAlpha(0.4f), true);
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
        void DrillSchematic(Rect a, int i, float t)
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
            switch (i)
            {
                case 0:  // beacons on a path
                    var pts = new[] { P(-2.2f, 1f), P(-1.2f, -1.4f), P(0.4f, 0.6f), P(1.6f, -1.2f), P(2.4f, 1f) };
                    for (int k = 0; k < pts.Length; k++)
                    {
                        if (k > 0) Dots(pts[k - 1], pts[k], you.WithAlpha(k <= 2 ? 0.7f : 0.2f));
                        Disc(pts[k], 11f, (k < 2 ? CrtStyle.Gold : TrainColor).WithAlpha(k < 2 ? 0.9f : 0.5f), true);
                    }
                    Disc(Lerp2(pts[2], pts[3], 0.35f), 8f, you);
                    break;
                case 1:  // walls of shots, a dash through one
                    for (int k = 0; k < 3; k++)
                        for (float y = a.y + 14f; y < a.yMax - 10f; y += 13f)
                            if (Mathf.Abs(y - C.y) > 9f || k != 1) Disc(new Vector2(C.x + (k - 1) * 1.4f * u, y), 3f, red);
                    for (int k = 0; k < 4; k++) Disc(P(-1.1f + k * 0.4f, 0f), 7f, you.WithAlpha(0.15f + k * 0.2f));
                    Disc(P(0.5f, 0f), 8f, you);
                    break;
                case 2:  // bounce off a wall, gold ring
                    Ui.Fill(new Rect(P(2f, 0f).x, a.y + 10f, 8f, a.height - 20f), CrtStyle.Ink.WithAlpha(0.5f));
                    Dots(P(-2.2f, 1.2f), P(1.85f, -0.2f), you.WithAlpha(0.6f));
                    Dots(P(1.85f, -0.2f), P(-0.4f, -1.6f), CrtStyle.Gold.WithAlpha(0.8f));
                    Disc(P(1.85f, -0.2f), 14f, CrtStyle.Gold, true);
                    Disc(P(-0.4f, -1.6f), 8f, you);
                    break;
                case 3:  // targets and a stream of shots
                    for (int k = 0; k < 4; k++)
                    {
                        var tp = P(1.8f, -1.6f + k * 1.05f);
                        Disc(tp, 12f, red.WithAlpha(k == 1 ? 0.25f : 0.8f), true);
                        Disc(tp, 4f, red.WithAlpha(k == 1 ? 0.25f : 0.8f));
                    }
                    Disc(P(-2f, 0f), 8f, you);
                    for (int k = 1; k < 6; k++) Disc(Lerp2(P(-2f, 0f), P(1.8f, -0.55f), k / 6.5f), 2.5f, you);
                    break;
                case 4:  // a shield arc taking shots
                    Disc(P(-0.6f, 0f), 8f, you);
                    for (int k = -5; k <= 5; k++)
                    {
                        float an = k * 0.13f;
                        Disc(P(-0.6f, 0f) + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * 26f, 2.2f, you);
                    }
                    for (int k = 0; k < 4; k++) Disc(P(0.6f + k * 0.55f, (k - 1.5f) * 0.25f), 3f, red);
                    Disc(P(2.6f, 0f), 10f, red.WithAlpha(0.7f), true);
                    break;
                case 5:  // an aim line before the shot
                    Disc(P(2f, -1.3f), 10f, red);
                    Dots(P(2f, -1.3f), P(-2.6f, 1.2f), red.WithAlpha(0.7f), 5f);
                    Disc(P(-1.2f, 0.45f), 8f, you.WithAlpha(0.25f));
                    Disc(P(-0.8f, 1.4f), 8f, you);
                    break;
                case 6:  // a cast catching three dummies
                    Disc(P(0.8f, 0f), 44f, you.WithAlpha(0.12f));
                    Disc(P(0.8f, 0f), 44f, you.WithAlpha(0.7f), true);
                    foreach (var dp in new[] { P(0.5f, -0.4f), P(1.2f, 0.2f), P(0.6f, 0.5f) }) Disc(dp, 7f, red);
                    Disc(P(-2f, 0.3f), 8f, you);
                    break;
                case 7:  // the charge filling
                    Disc(C, 50f, CrtStyle.Gold.WithAlpha(0.6f), true);
                    Disc(C, 8f, you);
                    Ui.Bar(new Rect(C.x - 60f, a.yMax - 20f, 120f, 6f), 0.7f, CrtStyle.Gold);
                    break;
                case 8:  // a thrown gadget's arc
                    Disc(P(-2f, 0.8f), 8f, you);
                    for (int k = 1; k < 10; k++) { float s = k / 10f; Disc(Lerp2(P(-2f, 0.8f), P(1.6f, 0.4f), s) + new Vector2(0f, -Mathf.Sin(s * Mathf.PI) * 1.4f * u), 1.6f, you.WithAlpha(0.7f)); }
                    Disc(P(1.6f, 0.4f), 18f, TrainColor.WithAlpha(0.7f), true);
                    break;
                case 9:  // three cards
                    for (int k = -1; k <= 1; k++)
                    {
                        var cr = new Rect(C.x + k * 70f - 28f, C.y - 40f + (k == 0 ? -6f : 0f), 56f, 80f);
                        Ui.Fill(cr, (k == 0 ? CrtStyle.Gold : CrtStyle.Ink).WithAlpha(0.08f));
                        HuntBox(cr, (k == 0 ? CrtStyle.Gold : CrtStyle.Ink).WithAlpha(0.6f));
                    }
                    break;
                case 10:  // a room drawn off the map
                    HuntBox(new Rect(C.x - 2.4f * u, C.y - u, 1.8f * u, 2f * u), CrtStyle.Ink.WithAlpha(0.6f));
                    CrtKit.Dashed(new Rect(C.x - 0.6f * u, C.y - 0.7f * u, 1.6f * u, 1.4f * u), 1f, TrainColor);
                    HuntBox(new Rect(C.x + 1f * u, C.y - 0.5f * u, 1.3f * u, 1f * u), CrtStyle.Gold.WithAlpha(0.6f));
                    Disc(P(-1.5f, 0f), 7f, you);
                    break;
                default:  // coins to a counter
                    for (int k = 0; k < 6; k++) Disc(P(-2f + k * 0.35f, Mathf.Sin(k) * 0.6f), 4f, CrtStyle.Gold);
                    HuntBox(new Rect(P(1.2f, -0.7f).x, P(1.2f, -0.7f).y, 1.2f * u, 1.4f * u), CrtStyle.Gold.WithAlpha(0.7f));
                    Disc(P(0.6f, 0f), 8f, you);
                    break;
            }
        }
    }
}
