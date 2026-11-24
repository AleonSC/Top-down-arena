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
            string key = Training.Drills[game.Drill].Key;
            if (d.HitT < 0.45f && !FxCalm)  // a hit (Dodge, Parry): a red flash over the screen
                Ui.Fill(new Rect(0f, 0f, W, H), CrtStyle.Danger.WithAlpha(0.22f * (1f - d.HitT / 0.45f)));
            if (key == "parry")
                DrawDrillShot(d);
            else if (key == "dodge")
                DrawDrillWall(d);
            else if (key == "wallkick")
                DrawDrillKick(d);
            else if (key == "shoot")
                DrawDrillTargets(d);
            else
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

        /// Parry: the shot coming in (a red bolt with a short trail), and an arc on the ring round you on the side it's coming from.
        void DrawDrillShot(DrillRunner d)
        {
            var me = game.Me;
            if (d.Done || !d.ShotUp || me == null)
                return;
            V2 s = ToUi(d.Shot), c = ToUi(me.Disp);
            V2 way = c - s;
            float len = Mathf.Max(1f, way.Length);
            V2 dir = way * (1f / len);
            for (int k = 1; k <= 6; k++)  // the trail
            {
                V2 q = s - dir * (k * 7f);
                float r = 5f - k * 0.6f;
                Ui.Dot(new Rect(q.X - r, q.Y - r, r * 2f, r * 2f), CrtStyle.Danger.WithAlpha(0.6f - k * 0.08f));
            }
            Ui.RadialGlow(new Rect(s.X - 18f, s.Y - 18f, 36f, 36f), CrtStyle.Danger.WithAlpha(0.6f));
            Ui.Dot(new Rect(s.X - 7f, s.Y - 7f, 14f, 14f), CrtStyle.Danger);
            Ui.Dot(new Rect(s.X - 3f, s.Y - 3f, 6f, 6f), new Color(1f, 0.9f, 0.9f));
            // The warning arc: dots round you, brightest straight at the shot, closer as it nears.
            float near = Mathf.Clamp01(1f - len / 300f), ang = Mathf.Atan2(-dir.Y, -dir.X), ring = 66f;  // (outside the player's own rings)
            for (int k = -4; k <= 4; k++)
            {
                float a = ang + k * 0.12f, al = (1f - Mathf.Abs(k) / 5f) * (0.35f + 0.65f * near);
                V2 q = c + new V2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
                Ui.Dot(new Rect(q.X - 3.5f, q.Y - 3.5f, 7f, 7f), CrtStyle.Danger.WithAlpha(al));
            }
            if (len > 120f)  // a quiet guide line while it's far
                for (float t = 20f; t < len - ring - 10f; t += 16f)
                {
                    V2 q = s + dir * t;
                    Ui.Fill(new Rect(q.X - 1f, q.Y - 1f, 2f, 2f), CrtStyle.Danger.WithAlpha(0.25f));
                }
        }

        /// Shoot: the target's lane (a faint rail with end caps once it slides) and a pulsing ring round the target.
        void DrawDrillTargets(DrillRunner d)
        {
            if (d.Done)
                return;
            float pulse = FxCalm ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            V2 c = Training.ShootSpot(d.Step, Game.Width, Game.Height);
            if (Training.ShootSpeedAt(d.Step) > 0f)
            {
                V2 a = ToUi(c - new V2(Training.ShootSlide, 0f)), b = ToUi(c + new V2(Training.ShootSlide, 0f));
                Ui.Fill(new Rect(a.X, a.Y - 1f, b.X - a.X, 2f), TrainColor.WithAlpha(0.25f));
                Ui.Fill(new Rect(a.X - 1f, a.Y - 8f, 2f, 16f), TrainColor.WithAlpha(0.45f));
                Ui.Fill(new Rect(b.X - 1f, b.Y - 8f, 2f, 16f), TrainColor.WithAlpha(0.45f));
            }
            if (d.TargetId == 0 || !game.Enemies.TryGetValue(d.TargetId, out var e))
                return;
            V2 p = ToUi(e.Disp);
            float rad = ToUi(e.Disp + new V2(Training.BeaconRadius, 0f)).X - p.X;
            Ui.Dot(new Rect(p.X - rad, p.Y - rad, rad * 2f, rad * 2f), TrainColor.WithAlpha(pulse), true);
            for (int k = 0; k < 4; k++)  // crosshair ticks
            {
                V2 dir = V2.FromAngle(k * Mathf.PI / 2f);
                V2 q = p + dir * (rad + 6f);
                Ui.Fill(new Rect(q.X - 2f + dir.X * 4f, q.Y - 2f + dir.Y * 4f, 4f, 4f), TrainColor.WithAlpha(0.8f * pulse));
            }
            LobbyKit.Spaced(new Rect(p.X - 80f, p.Y + rad + 16f, 160f, 14f), "TARGET " + (d.Step + 1) + " / " + Training.ShootSteps, 10, TrainColor, TextAnchor.MiddleCenter, 2f);
        }

        /// Wall Kick: the lit panel glowing with KICK and arrows at its faces, kicked ones gold, then the flag (lit once all three
        /// are kicked) with a dotted line from you.
        void DrawDrillKick(DrillRunner d)
        {
            float pulse = FxCalm ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < Training.KickWalls; i++)
            {
                var s = Training.KickPanel(i, Game.Width, Game.Height);
                V2 a = ToUi(s.Pos - new V2(s.HW, s.HH)), b = ToUi(s.Pos + new V2(s.HW, s.HH));
                var r = new Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
                bool done = i < d.Step, lit = i == d.Step && !d.Done;
                if (done)
                {
                    Ui.Fill(r, CrtStyle.Gold.WithAlpha(0.25f));
                    CrtKit.Frame(r, 2f, CrtStyle.Gold.WithAlpha(0.8f));
                    continue;
                }
                if (!lit)
                {
                    Ui.Label(new Rect(r.center.x - 20f, r.center.y - 10f, 40f, 20f), (i + 1).ToString(), 14, CrtStyle.Ink.WithAlpha(0.35f), TextAnchor.MiddleCenter, true);
                    continue;
                }
                var g = new Rect(r.x - 18f, r.y - 18f, r.width + 36f, r.height + 36f);
                Ui.RadialGlow(g, TrainColor.WithAlpha(0.35f * pulse));
                Ui.Fill(r, TrainColor.WithAlpha(0.35f * pulse));
                CrtKit.Frame(r, 2f, TrainColor.WithAlpha(pulse));
                bool tall = r.height > r.width;
                LobbyKit.Spaced(new Rect(r.center.x - 40f, r.yMax + 8f, 80f, 14f), "KICK", 11, TrainColor, TextAnchor.MiddleCenter, 3f);
                if (tall)  // arrows into both faces
                    for (int side = -1; side <= 1; side += 2)
                        Chevron(new Vector2(side < 0 ? r.x - 14f : r.xMax + 14f, r.center.y), -side, TrainColor.WithAlpha(pulse));
            }
            // The flag.
            V2 fw = Training.KickFlag(Game.Width, Game.Height), fc = ToUi(fw);
            bool flagLit = d.Step >= Training.KickWalls && !d.Done;
            float rad = ToUi(fw + new V2(Training.BeaconRadius, 0f)).X - fc.X;
            Color fcol = d.Done ? CrtStyle.Gold : flagLit ? TrainColor : CrtStyle.Ink.WithAlpha(0.3f);
            if (flagLit)
                Ui.RadialGlow(new Rect(fc.X - rad * 2.4f, fc.Y - rad * 2.4f, rad * 4.8f, rad * 4.8f), TrainColor.WithAlpha(0.3f * pulse));
            Ui.Dot(new Rect(fc.X - rad, fc.Y - rad, rad * 2f, rad * 2f), fcol.WithAlpha(flagLit ? pulse : 1f), true);
            Ui.Fill(new Rect(fc.X - 1f, fc.Y - rad * 0.8f, 3f, rad * 1.4f), fcol);  // the pole
            Ui.Fill(new Rect(fc.X + 2f, fc.Y - rad * 0.8f, rad * 0.7f, rad * 0.45f), fcol);  // the cloth
            if (flagLit)
            {
                LobbyKit.Spaced(new Rect(fc.X - 80f, fc.Y + rad + 8f, 160f, 14f), "FLAG", 10, TrainColor, TextAnchor.MiddleCenter, 3f);
                if (game.Me != null)
                {
                    V2 from = ToUi(game.Me.Disp), dir = fc - from;
                    float len = dir.Length;
                    for (float s = 30f; s < len - rad - 6f; s += 14f)
                    {
                        V2 q = from + dir * (s / len);
                        Ui.Fill(new Rect(q.X - 1.5f, q.Y - 1.5f, 3f, 3f), TrainColor.WithAlpha(0.5f));
                    }
                }
            }
        }

        /// Dodge: your wall of shots sweeping across, with chevrons the way it runs; a red flash over the screen when one catches you.
        void DrawDrillWall(DrillRunner d)
        {
            if (d.Done || float.IsNaN(d.Wall))
                return;
            int dir = Training.WallDir(d.Step);
            V2 top = ToUi(new V2(d.Wall, 0f)), bot = ToUi(new V2(d.Wall, Game.Height));
            float x = top.X, rad = Mathf.Max(4f, (ToUi(new V2(d.Wall + 7f, 0f)).X - x));
            Ui.Fill(new Rect(x - rad * 2.2f, top.Y, rad * 4.4f, bot.Y - top.Y), CrtStyle.Danger.WithAlpha(0.08f));
            int k = 0;
            for (float y = top.Y + 10f; y < bot.Y - 4f; y += rad * 3.2f, k++)
            {
                Ui.Dot(new Rect(x - rad, y - rad, rad * 2f, rad * 2f), CrtStyle.Danger);
                Ui.Dot(new Rect(x - rad * 0.45f, y - rad * 0.45f, rad * 0.9f, rad * 0.9f), new Color(1f, 0.85f, 0.85f));
                if (k % 6 == 3)  // a chevron the way it runs
                    Chevron(new Vector2(x + dir * rad * 3.4f, y), dir, CrtStyle.Danger.WithAlpha(0.7f));
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
            bool dodge = dr.Key == "dodge";
            string dodgeKey = Keybinds.KeyName(Keybinds.Get(KeyAction.Dodge));
            string line = d.Done ? "Done!" : dr.Key == "parry" ? "Hold " + Keybinds.KeyName(Keybinds.Get(KeyAction.Parry)) + " and turn the shield to face shot " + (d.Step + 1) + "."
                : dr.Key == "shoot" ? (Training.ShootSpeedAt(d.Step) > 0f
                    ? "Target " + (d.Step + 1) + " slides: lead it a little and keep firing."
                    : "Hold " + Keybinds.KeyName(Keybinds.Get(KeyAction.Fire)) + " on target " + (d.Step + 1) + " to break it.")
                : dr.Key == "wallkick" ? (d.Step < Training.KickWalls
                    ? "Dodge (" + dodgeKey + ") into lit wall " + (d.Step + 1) + " to kick off it."
                    : "Hold " + dodgeKey + " after a dodge to sprint to the flag.")
                : dodge ? "Dodge (" + Keybinds.KeyName(Keybinds.Get(KeyAction.Dodge)) + ") through wall " + (d.Step + 1) + " just as it reaches you."
                : Training.MoveShootStep(d.Step) ? "Aim at the target on beacon " + (d.Step + 1) + " and bring it down."
                : "Walk onto beacon " + (d.Step + 1) + ".";
            Ui.Label(new Rect(r.x + 14f, r.y + 50f, r.width - 28f, 32f), line, 12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            // Pips: one per beacon.
            float px = r.x + 14f, py = r.y + 88f;
            float pipStep = Mathf.Min(22f, (r.width - 28f - 124f) / dr.Steps), pipSz = Mathf.Min(16f, pipStep - 4f);  // (twelve targets fit beside the clock)
            for (int i = 0; i < dr.Steps; i++)
            {
                var pr = new Rect(px + i * pipStep, py + (16f - pipSz) / 2f, pipSz, pipSz);
                bool done = i < d.Step;
                Ui.Fill(pr, (done ? CrtStyle.Gold : i == d.Step ? TrainColor : CrtStyle.Dim).WithAlpha(done ? 0.85f : i == d.Step ? 0.4f : 0.15f));
                HuntBox(pr, (done ? CrtStyle.Gold : i == d.Step ? TrainColor : CrtStyle.Dim).WithAlpha(0.7f));
            }
            // The clock and the medal it's on course for.
            int medal = Training.Medal(dr, Mathf.Max(0.01f, d.Time));
            Ui.Label(new Rect(r.xMax - 120f, py - 6f, 106f, 28f), DrillClock(d.Time), 22, Color.white, TextAnchor.MiddleRight, true);
            string next = medal == 3 ? "GOLD UNDER " + ModeClock(dr.Gold) : medal == 2 ? "SILVER UNDER " + ModeClock(dr.Silver) : "BRONZE: FINISH IT";
            LobbyKit.Spaced(new Rect(r.x + 14f, r.y + 112f, r.width - 28f, 12f), next, 9, MedalCol(medal).WithAlpha(0.85f), TextAnchor.MiddleLeft, 1.4f);
            if (d.Hits > 0)
                LobbyKit.Spaced(new Rect(r.x + 14f, r.y + 112f, r.width - 28f, 12f), "HIT x" + d.Hits, 9, CrtStyle.Danger.WithAlpha(d.HitT < 0.6f ? 1f : 0.75f), TextAnchor.MiddleRight, 1.4f);
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
            string extra = dr.Key == "dodge" || dr.Key == "parry" ? (d.Hits == 0 ? "  ·  NO HITS" : "  ·  HIT x" + d.Hits) : "";
            Ui.Label(new Rect(r.x, r.y + 130f, r.width, 18f), (newBest ? "NEW BEST" : "BEST  " + DrillClock(best)) + extra, 12, newBest ? CrtStyle.Gold : CrtStyle.Dim, TextAnchor.MiddleCenter, true);
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
                    Ui.Label(new Rect(r.xMax - 184f, y, 160f, 20f), o.Done ? DrillClock(o.Time) + "  " + MedalName(Training.Medal(dr, o.Time)) : o.Step + " / " + dr.Steps,
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
