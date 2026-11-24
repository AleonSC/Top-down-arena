using UnityEngine;

namespace Arena
{
    /// A Training drill on screen (Core/DrillGame.cs, Docs/Training.md): your beacons in the arena (done ones dim with a tick, the
    /// lit one pulsing with a line from you to it, the rest faint and numbered), the drill card at the top left (name, the step's
    /// instruction, five pips, the clock and the medal it is on course for), and the result card once you're done (time, medal,
    /// best, the party's progress, RETRY and BACK for the host). Preview: `DRILL=<step>,<seconds>[,done] shot.sh x hud`.
    public partial class ArenaApp
    {
        static readonly Rect DrillRect = new Rect(16f, 40f, 300f, 0f);

        /// The drill's world marks and its HUD, from DrawHud.
        void DrawDrill()
        {
            var d = game.MyDrill;
            if (d == null)
                return;
            DrawDrillBeacons(d);
            DrawDrillCard(d);
            if (d.Done)
                DrawDrillResult(d);
        }

        /// Your five beacons: done (dim, a tick), the lit one (pulsing, numbered, with a dotted line from you), the rest faint.
        void DrawDrillBeacons(DrillRunner d)
        {
            var me = game.Me;
            float pulse = FxCalm ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < Training.MoveSteps; i++)
            {
                V2 w = Training.MoveBeacon(i, Game.Width, Game.Height);
                V2 c = ToUi(w);
                float rad = ToUi(w + new V2(Training.BeaconRadius, 0f)).X - c.X;
                var rr = new Rect(c.X - rad, c.Y - rad, rad * 2f, rad * 2f);
                bool done = i < d.Step, lit = i == d.Step && !d.Done;
                bool shoot = Training.MoveShootStep(i);
                if (done)
                {
                    Ui.Dot(rr, CrtStyle.Gold.WithAlpha(0.35f), true);
                    float dr = rad * 0.32f;
                    Ui.Dot(new Rect(c.X - dr, c.Y - dr, dr * 2f, dr * 2f), CrtStyle.Gold.WithAlpha(0.5f));
                    if (i == d.Step - 1 && d.Flash < 0.5f && !FxCalm)  // the pop as it's reached
                    {
                        float k = d.Flash / 0.5f, g = rad * (1f + k * 1.2f);
                        Ui.Dot(new Rect(c.X - g, c.Y - g, g * 2f, g * 2f), CrtStyle.Gold.WithAlpha(0.8f * (1f - k)), true);
                    }
                    continue;
                }
                if (lit)
                {
                    Ui.RadialGlow(new Rect(c.X - rad * 2.4f, c.Y - rad * 2.4f, rad * 4.8f, rad * 4.8f), TrainColor.WithAlpha(0.3f * pulse));
                    Ui.Dot(rr, TrainColor.WithAlpha(0.12f));
                    Ui.Dot(rr, TrainColor.WithAlpha(pulse), true);
                    float g = rad * (1.15f + 0.15f * pulse);
                    Ui.Dot(new Rect(c.X - g, c.Y - g, g * 2f, g * 2f), TrainColor.WithAlpha(0.35f * pulse), true);
                    if (!shoot)
                        Ui.Label(rr, (i + 1).ToString(), Mathf.RoundToInt(rad * 0.75f), Color.white, TextAnchor.MiddleCenter, true);
                    LobbyKit.Spaced(new Rect(c.X - 80f, c.Y + rad + 8f, 160f, 14f), shoot ? "SHOOT IT" : "GO HERE", 10, TrainColor, TextAnchor.MiddleCenter, 2f);
                    if (me != null)
                    {
                        V2 from = ToUi(me.Disp);
                        V2 dir = c - from;
                        float len = dir.Length;
                        if (len > rad + 40f)
                            for (float s = 30f; s < len - rad - 6f; s += 14f)
                            {
                                V2 q = from + dir * (s / len);
                                Ui.Fill(new Rect(q.X - 1.5f, q.Y - 1.5f, 3f, 3f), TrainColor.WithAlpha(0.5f));
                            }
                    }
                    continue;
                }
                Ui.Dot(rr, CrtStyle.Ink.WithAlpha(0.22f), true);
                Ui.Label(rr, (i + 1).ToString(), Mathf.RoundToInt(rad * 0.6f), CrtStyle.Ink.WithAlpha(0.3f), TextAnchor.MiddleCenter, true);
            }
        }

        /// The drill card, top left: name, what to do now, the pips, the clock and the medal you're on course for.
        void DrawDrillCard(DrillRunner d)
        {
            var dr = Training.Drills[game.Drill];
            var r = new Rect(DrillRect.x, DrillRect.y, DrillRect.width, 132f);
            DkHud(r, TrainColor);
            if (!FxCalm)
                Ui.Fill(new Rect(r.x, r.y, 3f, r.height), TrainColor.WithAlpha(0.85f));
            HudCrt(new Rect(r.x + 14f, r.y + 6f, 220f, 16f), "TRAINING  ·  DRILL " + (game.Drill + 1).ToString("00"), 15, CrtStyle.Dim);
            DkTitle(new Rect(r.x + 14f, r.y + 24f, r.width - 28f, 24f), dr.Name.ToUpperInvariant(), 20, TrainColor);
            string line = d.Done ? "Done!" : Training.MoveShootStep(d.Step) ? "Aim at the target on beacon " + (d.Step + 1) + " and bring it down."
                : "Walk onto beacon " + (d.Step + 1) + ".";
            Ui.Label(new Rect(r.x + 14f, r.y + 50f, r.width - 28f, 32f), line, 12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            // Pips: one per beacon.
            float px = r.x + 14f, py = r.y + 88f;
            for (int i = 0; i < Training.MoveSteps; i++)
            {
                var pr = new Rect(px + i * 22f, py, 16f, 16f);
                bool done = i < d.Step;
                Ui.Fill(pr, (done ? CrtStyle.Gold : i == d.Step ? TrainColor : CrtStyle.Dim).WithAlpha(done ? 0.85f : i == d.Step ? 0.4f : 0.15f));
                HuntBox(pr, (done ? CrtStyle.Gold : i == d.Step ? TrainColor : CrtStyle.Dim).WithAlpha(0.7f));
            }
            // The clock and the medal it's on course for.
            int medal = Training.Medal(dr, Mathf.Max(0.01f, d.Time));
            Ui.Label(new Rect(r.xMax - 120f, py - 6f, 106f, 28f), DrillClock(d.Time), 22, Color.white, TextAnchor.MiddleRight, true);
            string next = medal == 3 ? "GOLD UNDER " + ModeClock(dr.Gold) : medal == 2 ? "SILVER UNDER " + ModeClock(dr.Silver) : "BRONZE: FINISH IT";
            LobbyKit.Spaced(new Rect(r.x + 14f, r.y + 112f, r.width - 28f, 12f), next, 9, MedalCol(medal).WithAlpha(0.85f), TextAnchor.MiddleLeft, 1.4f);
        }

        static string DrillClock(float s) => (int)(s / 60f) + ":" + (s % 60f).ToString("00.0");

        /// The result card in the middle once you're done: CLEAR, the time and medal, your best, the party, RETRY and BACK.
        void DrawDrillResult(DrillRunner d)
        {
            var dr = Training.Drills[game.Drill];
            int medal = Training.Medal(dr, d.Time);
            float best = (game.Profile ?? new Profile()).DrillBestTime(dr.Key);
            int others = game.DrillRunners.Count - 1;
            float h = 230f + (others > 0 ? 26f + others * 22f : 0f);
            var r = new Rect(W / 2f - 210f, H / 2f - h / 2f, 420f, h);
            DkHud(r, MedalCol(medal));
            Ui.Fill(new Rect(r.x, r.y, r.width, 3f), MedalCol(medal).WithAlpha(0.85f));
            LobbyKit.Spaced(new Rect(r.x, r.y + 14f, r.width, 16f), "DRILL CLEAR", 12, TrainColor, TextAnchor.MiddleCenter, 3f);
            DkTitle(new Rect(r.x, r.y + 34f, r.width, 28f), dr.Name.ToUpperInvariant(), 22, Color.white, TextAnchor.MiddleCenter);
            MedalBadge(new Vector2(r.center.x - 90f, r.y + 96f), medal, 18f);
            LobbyKit.Spaced(new Rect(r.center.x - 60f, r.y + 78f, 160f, 16f), MedalName(medal), 13, MedalCol(medal), TextAnchor.MiddleLeft, 2f);
            Ui.Label(new Rect(r.center.x - 60f, r.y + 94f, 180f, 26f), DrillClock(d.Time), 24, Color.white, TextAnchor.MiddleLeft, true);
            bool newBest = best > 0f && Mathf.Abs(best - d.Time) < 0.01f;
            Ui.Label(new Rect(r.x, r.y + 130f, r.width, 18f), newBest ? "NEW BEST" : "BEST  " + DrillClock(best), 12, newBest ? CrtStyle.Gold : CrtStyle.Dim, TextAnchor.MiddleCenter, true);
            float y = r.y + 158f;
            if (others > 0)
            {
                LobbyKit.Spaced(new Rect(r.x + 24f, y, 200f, 14f), "PARTY", 9, CrtStyle.Dim, TextAnchor.MiddleLeft, 2f);
                y += 20f;
                foreach (var o in game.DrillRunners.Values)
                {
                    if (o.PlayerId == game.MyId || !game.Players.TryGetValue(o.PlayerId, out var op))
                        continue;
                    Ui.Label(new Rect(r.x + 24f, y, 200f, 20f), op.Name, 12, CrtStyle.Ink, TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(r.xMax - 184f, y, 160f, 20f), o.Done ? DrillClock(o.Time) + "  " + MedalName(Training.Medal(dr, o.Time)) : o.Step + " / " + Training.MoveSteps,
                        12, o.Done ? MedalCol(Training.Medal(dr, o.Time)) : CrtStyle.Dim, TextAnchor.MiddleRight, true);
                    y += 22f;
                }
                y += 6f;
            }
            var retry = new Rect(r.x + 24f, r.yMax - 58f, 176f, 40f);
            var back = new Rect(r.xMax - 200f, r.yMax - 58f, 176f, 40f);
            bool host = session.IsHost;
            var ev = Event.current;
            bool keyRetry = host && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.R;
            bool keyBack = host && ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.B || ev.keyCode == KeyCode.Return);
            if (keyRetry || keyBack)
                ev.Use();
            if (DkKey(retry, "RETRY", true, FxCalm, size: 15, minSize: 11, accent: TrainColor, sub: host ? "R" : "THE HOST", locked: !host) && host || keyRetry)
                session.StartDrill(game.Drill);
            else if (DkKey(back, "BACK", false, FxCalm, size: 15, minSize: 11, sub: host ? "B  ·  TO TRAINING" : "THE HOST", locked: !host) && host || keyBack)
                session.ReturnToLobby();
        }
    }
}
