using UnityEngine;

namespace Arena
{
    /// Pings (Docs/Mockups/ping-system.html, Core/Pings.cs). Tap the Ping key (middle mouse by default) to drop the last kind you
    /// picked (Here to begin with) at your cursor; hold it for 0.2 s and a six-slice wheel opens there: move to a slice and let
    /// go to send it, let go in the middle to cancel. Each ping is a ripple, a beam of light and a glyph ring in its owner's
    /// colour with their name for a second, and lasts 4-6 s. A Focus ping rings the enemy it follows; a Help ping rings the
    /// player who sent it. While the wheel is open a click doesn't fire.
    public partial class ArenaApp
    {
        const float PingHoldTime = 0.2f, PingWheelR0 = 34f, PingWheelR1 = 96f;
        static readonly string[] PingNames = { "HERE", "FOCUS", "DANGER", "HELP", "LOOT", "GO" };
        static readonly Color[] PingColors =
        {
            new Color(0.44f, 0.89f, 0.77f), new Color(0.88f, 0.42f, 1f), new Color(1f, 0.35f, 0.3f),
            new Color(1f, 0.71f, 0.28f), new Color(1f, 0.8f, 0.35f), new Color(0.48f, 0.85f, 0.42f),
        };
        static readonly Color[] PingOwnerColors = { new Color(0.44f, 0.89f, 0.77f), new Color(1f, 0.71f, 0.28f), new Color(0.72f, 0.55f, 1f) };

        bool pingHeld, pingWheelOpen;
        float pingDownAt, pingWheelK;
        V2 pingAim;            // the arena spot under the cursor when the key went down
        Vector2 pingAnchor;    // where the wheel is centred on the HUD canvas
        int pingHover = -1;
        int pingTapKind = -1;  // the last kind you sent (the tap), loaded lazily

        int PingTapKind
        {
            get
            {
                if (pingTapKind < 0)
                    pingTapKind = Mathf.Clamp(PlayerPrefs.GetInt("ping_tap", 0), 0, 5);
                return pingTapKind;
            }
        }

        Vector2 MouseUi()
        {
            Vector3 m = Input.mousePosition;
            return new Vector2((m.x - offX) / scale, (UnityEngine.Screen.height - m.y - offY) / scale);
        }

        /// Each frame from Update: tap or hold the Ping key. `allowed`: a match with you up and no menu over it.
        void TickPing(bool allowed)
        {
            if (!allowed)
            {
                pingHeld = pingWheelOpen = false;
            }
            else if (Keybinds.Down(KeyAction.Ping) && !pingHeld)
            {
                pingHeld = true;
                pingDownAt = Time.unscaledTime;
                pingAim = MouseArena();
            }
            if (pingHeld && !pingWheelOpen && Time.unscaledTime - pingDownAt >= PingHoldTime && Keybinds.Held(KeyAction.Ping))
            {
                pingWheelOpen = true;
                var m = MouseUi();
                pingAnchor = new Vector2(Mathf.Clamp(m.x, 120f, W - 120f), Mathf.Clamp(m.y, 120f, H - 120f));
                pingHover = -1;
            }
            if (pingWheelOpen)
            {
                Vector2 d = MouseUi() - pingAnchor;
                if (d.magnitude < 30f)
                    pingHover = -1;
                else
                {
                    float a = (Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f + 30f + 720f) % 360f;  // 0 = the top slice's left edge
                    pingHover = Mathf.FloorToInt(a / 60f) % 6;
                }
            }
            pingWheelK = Mathf.MoveTowards(pingWheelK, pingWheelOpen ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (pingHeld && !Keybinds.Held(KeyAction.Ping))
            {
                int kind = pingWheelOpen ? pingHover : PingTapKind;
                if (kind >= 0 && allowed && session.SendPing((PingKind)kind, pingAim) && pingWheelOpen)
                {
                    pingTapKind = kind;
                    PlayerPrefs.SetInt("ping_tap", kind);
                }
                pingHeld = pingWheelOpen = false;
            }
        }

        static Color PingOwnerColor(Game g, int id)
        {
            int i = 0;
            foreach (int pid in g.Players.Keys)
            {
                if (pid == id)
                    break;
                i++;
            }
            return PingOwnerColors[i % PingOwnerColors.Length];
        }

        /// A kind's glyph, centred on c, drawn from fills and dots so it needs no special font.
        void DrawPingGlyph(PingKind kind, Vector2 c, float r, Color col)
        {
            float t = Mathf.Max(2f, r * 0.14f), k = r * 0.5f;
            switch (kind)
            {
                case PingKind.Here:
                    Ui.Dot(new Rect(c.x - k, c.y - k, 2f * k, 2f * k), col, true);
                    Ui.Dot(new Rect(c.x - k * 0.25f, c.y - k * 0.25f, k * 0.5f, k * 0.5f), col);
                    break;
                case PingKind.Focus:
                    Ui.Dot(new Rect(c.x - k * 0.8f, c.y - k * 0.8f, k * 1.6f, k * 1.6f), col, true);
                    Ui.Fill(new Rect(c.x - t / 2f, c.y - k * 1.15f, t, k * 0.7f), col);
                    Ui.Fill(new Rect(c.x - t / 2f, c.y + k * 0.45f, t, k * 0.7f), col);
                    Ui.Fill(new Rect(c.x - k * 1.15f, c.y - t / 2f, k * 0.7f, t), col);
                    Ui.Fill(new Rect(c.x + k * 0.45f, c.y - t / 2f, k * 0.7f, t), col);
                    break;
                case PingKind.Danger:
                    Ui.Dot(new Rect(c.x - k * 1.1f, c.y - k * 1.1f, k * 2.2f, k * 2.2f), col, true);
                    Ui.Fill(new Rect(c.x - t / 2f, c.y - k * 0.6f, t, k * 0.8f), col);
                    Ui.Fill(new Rect(c.x - t / 2f, c.y + k * 0.4f, t, t), col);
                    break;
                case PingKind.Help:
                    Ui.Fill(new Rect(c.x - t, c.y - k, 2f * t, 2f * k), col);
                    Ui.Fill(new Rect(c.x - k, c.y - t, 2f * k, 2f * t), col);
                    break;
                case PingKind.Loot:
                {
                    var old = GUI.matrix;
                    GUIUtility.RotateAroundPivot(45f, c);
                    Ui.Fill(new Rect(c.x - k * 0.7f, c.y - k * 0.7f, k * 1.4f, k * 1.4f), new Color(col.r, col.g, col.b, col.a * 0.4f));
                    Ui.Fill(new Rect(c.x - k * 0.7f, c.y - k * 0.7f, k * 1.4f, t), col);
                    Ui.Fill(new Rect(c.x - k * 0.7f, c.y + k * 0.7f - t, k * 1.4f, t), col);
                    Ui.Fill(new Rect(c.x - k * 0.7f, c.y - k * 0.7f, t, k * 1.4f), col);
                    Ui.Fill(new Rect(c.x + k * 0.7f - t, c.y - k * 0.7f, t, k * 1.4f), col);
                    GUI.matrix = old;
                    break;
                }
                default:  // Go: two chevrons pointing down the screen
                    Ui.Label(new Rect(c.x - r, c.y - r, 2f * r, 2f * r), "»", Mathf.RoundToInt(r * 1.5f), col, TextAnchor.MiddleCenter, true);
                    break;
            }
        }

        /// The HUD pass: every ping in the match, and the Help rings on players.
        void DrawMatchPings()
        {
            if (session.Pings.Count == 0)
                return;
            float now = Time.unscaledTime;
            for (int i = 0; i < session.Pings.Count; i++)
            {
                V2 pos;
                float age;
                if (!session.PingNow(i, out pos, out age))
                    continue;
                var ping = session.Pings[i];
                Color tone = PingColors[(int)ping.Kind], who = PingOwnerColor(game, ping.Owner);
                float life = Session.PingLife(ping.Kind), fade = Mathf.Clamp01(life - age);
                float oldAlpha = Ui.Alpha;
                V2 at = ToUi(pos);
                float pulse = ping.Kind == PingKind.Danger ? 2.4f : 1.4f;
                for (int k = 0; k < 3; k++)  // three ripples, each a third of a beat behind
                {
                    if (age * pulse < k * 0.33f)
                        continue;
                    float u = Mathf.Repeat(age * pulse - k * 0.33f, 1f), rr = 8f + u * 34f;
                    Ui.Alpha = oldAlpha * fade * (1f - u) * 0.8f;
                    Ui.Dot(new Rect(at.X - rr, at.Y - rr, 2f * rr, 2f * rr), tone, true);
                }
                for (int s = 0; s < 8; s++)  // the beam: fades out as it climbs
                {
                    Ui.Alpha = oldAlpha * fade * 0.45f * (1f - s / 8f);
                    Ui.Fill(new Rect(at.X - 1.5f, at.Y - (s + 1) * 14f, 3f, 14f), tone);
                }
                Ui.Alpha = oldAlpha * fade;
                var badge = new Rect(at.X - 16f, at.Y - 54f, 32f, 32f);
                Ui.Dot(badge, new Color(0.03f, 0.04f, 0.07f, 0.88f));
                Ui.Dot(badge, who, true);
                DrawPingGlyph(ping.Kind, badge.center, 24f, tone);
                string name = age < 1f && game.Players.ContainsKey(ping.Owner) ? game.Players[ping.Owner].Name + "  " + PingNames[(int)ping.Kind] : null;
                if (name != null)
                {
                    float w = Ui.TextWidth(name, 10, true) + 14f;
                    var chip = new Rect(at.X - w / 2f, at.Y - 78f, w, 18f);
                    Ui.Fill(chip, new Color(0.03f, 0.04f, 0.07f, 0.85f));
                    Ui.Label(chip, name, 10, who, TextAnchor.MiddleCenter, true);
                }
                if (ping.Kind == PingKind.Focus && ping.Target >= 0)  // ring the enemy it follows
                {
                    Enemy e;
                    if (game.Enemies.TryGetValue(ping.Target, out e))
                    {
                        float rr = (e.Radius + 5f) * UiWorldScale;
                        Ui.Dot(new Rect(at.X - rr, at.Y - rr, 2f * rr, 2f * rr), who, true);
                    }
                }
                if (ping.Kind == PingKind.Help)  // a pulsing ring round whoever is asking
                {
                    Player p;
                    if (game.Players.TryGetValue(ping.Owner, out p) && !p.Downed)
                    {
                        V2 pa = ToUi(p.Disp);
                        float rr = p.Def.BodyR * UiWorldScale + 8f + Mathf.Sin(now * 8f) * 2f;
                        Ui.Dot(new Rect(pa.X - rr, pa.Y - rr, 2f * rr, 2f * rr), tone, true);
                    }
                }
                Ui.Alpha = oldAlpha;
            }
        }

        /// The wheel, centred on pingAnchor (HUD pass, over everything else in the match).
        void DrawPingWheel()
        {
            if (pingWheelK <= 0.01f)
                return;
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * pingWheelK;
            Ui.Fill(new Rect(0f, 0f, W, H), new Color(0.02f, 0.03f, 0.04f, 0.4f));
            float mid = (PingWheelR0 + PingWheelR1) / 2f;
            for (int i = 0; i < 6; i++)
            {
                float a = (-90f + i * 60f) * Mathf.Deg2Rad;
                var c = pingAnchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (mid + (pingHover == i ? 6f : 0f)) * (0.7f + 0.3f * pingWheelK);
                bool hot = pingHover == i;
                Color tone = PingColors[i];
                var tile = new Rect(c.x - 31f, c.y - 27f, 62f, 54f);
                Ui.Fill(tile, hot ? new Color(tone.r, tone.g, tone.b, 0.35f) : new Color(0.07f, 0.09f, 0.11f, 0.92f));
                Ui.Fill(new Rect(tile.x, tile.y, tile.width, 2f), hot ? tone : new Color(1f, 1f, 1f, 0.15f));
                DrawPingGlyph((PingKind)i, new Vector2(c.x, c.y - 6f), 24f, hot ? Color.white : tone);
                Ui.Label(new Rect(tile.x, tile.yMax - 17f, tile.width, 14f), PingNames[i], 9, hot ? Color.white : new Color(0.57f, 0.63f, 0.68f), TextAnchor.MiddleCenter, true);
            }
            var hub = new Rect(pingAnchor.x - 26f, pingAnchor.y - 26f, 52f, 52f);
            Ui.Dot(hub, new Color(0.03f, 0.04f, 0.07f, 0.9f));
            Ui.Dot(hub, new Color(0.35f, 0.4f, 0.45f), true);
            Ui.Label(hub, pingHover < 0 ? "CANCEL" : "LET GO", 8, new Color(0.57f, 0.63f, 0.68f), TextAnchor.MiddleCenter, true);
            Ui.Alpha = oldAlpha;
        }
    }
}
