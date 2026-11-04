using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The lobby's Hangout tab (Docs/Hangout.md): a room where everyone's character walks about with WASD, wearing what their
    /// rank has earned (the trail behind them, the frame round their name, the rank badge). Nothing to shoot and nothing
    /// that matters: it's somewhere to stand while the party gets ready. Positions travel through LobbyAvatars.cs.
    public partial class ArenaApp
    {
        const int HangoutTab = 6;
        static readonly Color HangoutColor = new Color(0.3f, 0.85f, 0.7f);
        const float AvatarZoom = 1.5f;

        readonly Player roomPlayer = new Player { Id = -101 };
        readonly Dictionary<int, List<TrailPoint>> roomTrails = new Dictionary<int, List<TrailPoint>>();
        readonly Dictionary<int, CycleRider> roomCycles = new Dictionary<int, CycleRider>();  // the Light Cycle's walls (LightCycleTrail.cs)
        readonly Dictionary<int, float> roomAim = new Dictionary<int, float>();
        int hangoutFrame = -1;

        /// Whether the keyboard is ours: no text box, window or menu is taking it.
        bool HangoutKeysFree => GUIUtility.keyboardControl == 0 && !settingsOpen && !lobbyMenuOpen && !connInfoOpen && !LobbyEntering && !LobbyLeaving && !LobbyReturning;

        // --- The window: the Hangout's view of the big room, which scrolls with you and stops at the room's border.
        const float MiniW = 150f, MiniH = 100f;       // the minimap, top left of the window (the room is 2400 x 1600: 1/16 scale)
        const float ShowcaseShipX = 0.5f, ShowcaseShipY = 0.76f, ShowcaseZoom = 1.1f;  // where your ship sits in the loadout's preview window, so the grow starts from it
        float hangoutAnim;       // 0 = the Loadout, 1 = the Hangout; moves over HangoutAnimLen when you switch (the preview window grows into the room)
        int hangoutLastTab;
        const float HangoutAnimLen = 0.85f;

        /// The Loadout's preview window (the live mini-arena on the hero card): the Hangout grows out of it. DrawLoadout uses this too.
        static Rect ShowcaseRect(Rect body)
        {
            var hero = new Rect(body.x + 14f, body.y + 14f, 232f, body.height - 28f);
            return new Rect(hero.x + 8f, hero.y + 8f, hero.width - 16f, 180f);
        }

        static Rect HangoutView(Rect body) => new Rect(body.x + 4f, body.y + 30f, body.width - 8f, body.height - 34f);

        /// Once a frame: slide hangoutAnim towards where the tab says. Only Loadout <-> Hangout animates; any other switch, and
        /// reduced effects, snap.
        void TickHangoutAnim()
        {
            float target = lobbyTab == HangoutTab ? 1f : 0f;
            if (lobbyTab != hangoutLastTab)
            {
                bool fromLoadout = (lobbyTab == HangoutTab && hangoutLastTab == 0) || (lobbyTab == 0 && hangoutLastTab == HangoutTab);
                if (!fromLoadout || session.Resume != null)
                    hangoutAnim = target;
                hangoutLastTab = lobbyTab;
            }
            if (hangoutAnim == target)
                return;
            hangoutAnim = fxOn && !fxReduced ? Mathf.MoveTowards(hangoutAnim, target, Time.unscaledDeltaTime / HangoutAnimLen) : target;
        }

        /// Mid-switch: the Loadout fades away over the first 50% (the room fades in over the first 35%) while the preview window stretches into the room and your ship
        /// grows to its size there; the rest of the room (the others, name plates, minimap, PvP switch) arrives over the last 40%.
        void DrawHangoutTransition(LobbyEntry me, Rect body, float t)
        {
            float fade = 1f - Mathf.Clamp01(hangoutAnim / 0.5f);
            if (fade > 0f)
            {
                var old = GUI.color;
                GUI.color = new Color(old.r, old.g, old.b, old.a * fade);
                DrawLoadout(me, body, t);
                GUI.color = old;
            }
            // The room fades in over the preview (and out over it going back), so the ship's small jump between the two
            // renderings is a crossfade instead of a hop.
            float inAlpha = EaseInOut(Mathf.Clamp01(hangoutAnim / 0.35f));
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * inAlpha;
            DrawHangout(body, t);
            Ui.Alpha = oldAlpha;
        }

        void DrawHangout(Rect body, float t)
        {
            float k = Mathf.Clamp01(hangoutAnim);
            if (lobbyTab == HangoutTab && hangoutAnim <= 0f)
                k = 1f;  // (nothing has ticked the animation yet)
            bool live = k >= 1f;
            float e = EaseInOut(k);
            float roomAlpha = Mathf.Clamp01((k - 0.6f) / 0.4f);
            if (live)
                Ui.Label(new Rect(body.x + 4f, body.y, body.width - 8f, 26f), "Walk with " + MoveKeysText() + ". PvP on (under the minimap): aim with the mouse, hold click to shoot.", 13, Ui.Muted, TextAnchor.MiddleLeft);
            var full = HangoutView(body);
            var view = live ? full : LerpRect(ShowcaseRect(body), full, e);
            float ws = live ? 1f : Mathf.Lerp(ShowcaseZoom / AvatarZoom, 1f, e);  // the room's scale on screen
            Avatar mine;
            session.Avatars.TryGetValue(session.MyId, out mine);
            V2 me2 = mine != null ? mine.Disp : new V2(Session.RoomW / 2f, Session.RoomH / 2f);

            // The camera follows you and stops at the room's edge. Mid-switch it keeps your ship at its place in the window and
            // eases it to where it will really stand.
            V2 camFull = new V2(Mathf.Clamp(me2.X - full.width / 2f, 0f, Mathf.Max(0f, Session.RoomW - full.width)), Mathf.Clamp(me2.Y - full.height / 2f, 0f, Mathf.Max(0f, Session.RoomH - full.height)));
            V2 cam = camFull;
            if (!live)
            {
                float fx = Mathf.Lerp(ShowcaseShipX, (me2.X - camFull.X) / full.width, e), fy = Mathf.Lerp(ShowcaseShipY, (me2.Y - camFull.Y) / full.height, e);
                cam = new V2(me2.X - fx * view.width / ws, me2.Y - fy * view.height / ws);
            }
            V2 origin = new V2(view.x - cam.X * ws, view.y - cam.Y * ws);  // where the room's corner is on screen

            if (Event.current.type == EventType.Repaint && hangoutFrame != Time.frameCount)
            {
                hangoutFrame = Time.frameCount;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                session.StepAvatars(dt);
                if (mine != null && live && HangoutKeysFree)
                    WalkMine(mine, dt, view, cam);
                TickRoomTrails(t, dt);
            }

            // The floor: a faint grid over the lobby's own background, and a glowing rim at the room's edge.
            Ui.Fill(view, new Color(0.02f, 0.03f, 0.06f, 0.9f));
            var world = new Rect(origin.X, origin.Y, Session.RoomW * ws, Session.RoomH * ws);
            float fx0 = Mathf.Max(view.x, world.x), fy0 = Mathf.Max(view.y, world.y), fx1 = Mathf.Min(view.xMax, world.xMax), fy1 = Mathf.Min(view.yMax, world.yMax);
            if (fx1 > fx0 && fy1 > fy0)
            {
                var floor = new Rect(fx0, fy0, fx1 - fx0, fy1 - fy0);
                Ui.Fill(floor, new Color(0.03f, 0.05f, 0.1f, 0.62f));
                float step = 50f * ws;
                for (float x = world.x + Mathf.Ceil((fx0 - world.x) / step) * step; x < fx1; x += step)
                    Ui.Fill(new Rect(x, fy0, 1f, fy1 - fy0), new Color(1f, 1f, 1f, 0.035f));
                for (float y = world.y + Mathf.Ceil((fy0 - world.y) / step) * step; y < fy1; y += step)
                    Ui.Fill(new Rect(fx0, y, fx1 - fx0, 1f), new Color(1f, 1f, 1f, 0.035f));
            }
            var rim = new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.9f);
            var glow = new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.14f);
            DrawRimLine(view, new Rect(world.x - 2f, world.y - 2f, 4f, world.height + 4f), rim, glow);
            DrawRimLine(view, new Rect(world.xMax - 2f, world.y - 2f, 4f, world.height + 4f), rim, glow);
            DrawRimLine(view, new Rect(world.x - 2f, world.y - 2f, world.width + 4f, 4f), rim, glow);
            DrawRimLine(view, new Rect(world.x - 2f, world.yMax - 2f, world.width + 4f, 4f), rim, glow);
            Ui.Fill(new Rect(view.x, view.y, view.width, 1f), new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.35f));
            Ui.Fill(new Rect(view.x, view.yMax - 1f, view.width, 1f), new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.35f));
            Ui.Fill(new Rect(view.x, view.y, 1f, view.height), new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.35f));
            Ui.Fill(new Rect(view.xMax - 1f, view.y, 1f, view.height), new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.35f));

            // Everyone, the ones further down the room in front, and the shots between them.
            var order = new List<KeyValuePair<int, Avatar>>(session.Avatars);
            order.Sort((a, b) => a.Value.Disp.Y.CompareTo(b.Value.Disp.Y));
            if (BeginUiGl())
            {
                UiClip(view);
                if (roomAlpha > 0f)
                    DrawLobbyShots(origin, ws);
                foreach (var kv in order)
                {
                    if (roomAlpha <= 0f && kv.Key != session.MyId)
                        continue;  // (the others walk in as the window finishes growing)
                    var sp = new V2(origin.X + kv.Value.Disp.X * ws, origin.Y + kv.Value.Disp.Y * ws);
                    if (sp.X < view.x - 60f || sp.X > view.xMax + 60f || sp.Y < view.y - 60f || sp.Y > view.yMax + 60f)
                        continue;
                    float keep = glAlpha;
                    if (kv.Value.Hp <= 0f)
                        glAlpha = keep * 0.3f;  // knocked out: a ghost until it respawns
                    DrawAvatar(kv.Key, kv.Value, origin, ws, t);
                    glAlpha = keep;
                }
                EndUiGl();
            }
            var oldCol = GUI.color;
            GUI.color = new Color(oldCol.r, oldCol.g, oldCol.b, oldCol.a * roomAlpha);
            if (roomAlpha > 0f)
            {
                foreach (var kv in order)
                    DrawAvatarTag(kv.Key, kv.Value, view, origin, ws, t);
                foreach (var kv in order)  // their emotes (EmoteBar.cs)
                {
                    int index;
                    float age;
                    if (session.EmoteOf(kv.Key, out index, out age))
                        DrawEmote(index, new Vector2(origin.X + kv.Value.Disp.X * ws, origin.Y + kv.Value.Disp.Y * ws - 30f), age, 1f);
                }
                DrawHangoutHud(view, cam, mine, live, t);
            }
            GUI.color = oldCol;
            if (live)
            {
                TickEmoteBar(HangoutKeysFree);
                DrawEmoteBar(view.center.x, view.yMax - 14f);
            }
        }

        static void DrawRimLine(Rect view, Rect line, Color rim, Color glow)
        {
            var g = new Rect(line.x - 8f, line.y - 8f, line.width + 16f, line.height + 16f);
            if (g.xMax < view.x || g.x > view.xMax || g.yMax < view.y || g.y > view.yMax)
                return;
            Ui.Fill(ClipTo(view, g), glow);
            Ui.Fill(ClipTo(view, line), rim);
        }

        static Rect ClipTo(Rect view, Rect r)
        {
            float x0 = Mathf.Max(view.x, r.x), y0 = Mathf.Max(view.y, r.y), x1 = Mathf.Min(view.xMax, r.xMax), y1 = Mathf.Min(view.yMax, r.yMax);
            return new Rect(x0, y0, Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
        }

        /// GL: every shot in flight (a short bright dash in its owner's class colour) and the rings where shots landed.
        void DrawLobbyShots(V2 origin, float ws)
        {
            UiModelAt(origin, ws);
            foreach (var s in session.Shots)
            {
                LobbyEntry e;
                var c = session.Lobby.TryGetValue(s.Owner, out e) ? ToColor(Defs.Get(e.Role).Color) : Color.white;
                var dir = s.Vel.Normalized();
                Line(s.Pos - dir * 16f, s.Pos, 6f, new Rgba(c.r, c.g, c.b, 0.35f));
                Line(s.Pos - dir * 12f, s.Pos, 3f, new Rgba(Mathf.Lerp(c.r, 1f, 0.6f), Mathf.Lerp(c.g, 1f, 0.6f), Mathf.Lerp(c.b, 1f, 0.6f), 1f));
            }
            foreach (var kv in session.Avatars)  // a Tank's barrier: a bubble round them while it lasts (it flickers out in its last second)
            {
                var b = kv.Value;
                if (b.BarrierLeft <= 0f || b.Hp <= 0f)
                    continue;
                float flick = b.BarrierLeft < 1f ? (Mathf.Sin(b.BarrierLeft * 40f) > 0f ? 1f : 0.35f) : 1f;
                var bc = new Rgba(0.45f, 0.75f, 1f, 1f);
                Circle(b.Disp, Session.BarrierRadius, bc.WithAlpha(0.1f * flick), 40);
                RingLine(b.Disp, Session.BarrierRadius, 3f, bc.WithAlpha(0.8f * flick));
            }
            foreach (var fx in session.Fx)
            {
                float f = fx.Age / Session.FxLife;
                if (fx.Kind == LobbyAbilityKind.Dash)
                {
                    Line(fx.Pos, fx.Pos - V2.FromAngle(fx.Angle) * Session.DashDist, 18f * (1f - f), new Rgba(1f, 0.85f, 0.45f, 0.35f * (1f - f)));  // the streak it left
                    Circle(fx.Pos, 24f * (1f - f), new Rgba(1f, 0.9f, 0.6f, 0.4f * (1f - f)), 20);
                }
                else if (fx.Kind == LobbyAbilityKind.BurstHeal)
                {
                    float rr = Session.HealRadius * Mathf.Sqrt(Mathf.Clamp01(f * 1.6f));
                    Circle(fx.Pos, rr, new Rgba(0.35f, 1f, 0.55f, 0.16f * (1f - f)), 40);
                    RingLine(fx.Pos, rr, 4f, new Rgba(0.5f, 1f, 0.65f, 0.9f * (1f - f)));
                }
                else
                    RingLine(fx.Pos, Session.BarrierRadius * (1f + 0.5f * f), 3f, new Rgba(0.6f, 0.85f, 1f, 0.9f * (1f - f)));  // the barrier's pop
            }
            foreach (var h in session.Hits)
            {
                float f = h.Age / 0.3f;
                Circle(h.Pos, 8f + 18f * f, new Rgba(1f, 0.4f, 0.35f, 0.5f * (1f - f)), 16);
            }
        }

        /// GL: a ring drawn as short lines (Circle is filled).
        void RingLine(V2 c, float r, float width, Rgba col)
        {
            const int n = 36;
            V2 prev = c + new V2(r, 0f);
            for (int i = 1; i <= n; i++)
            {
                float ang = Mathf.PI * 2f * i / n;
                V2 cur = c + new V2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                Line(prev, cur, width, col);
                prev = cur;
            }
        }

        /// UI: the minimap (top left of the window), the PvP switch under it and each fighter's health bar.
        void DrawHangoutHud(Rect view, V2 cam, Avatar mine, bool live, float t)
        {
            // Health bars over everyone who's fighting.
            foreach (var kv in session.Avatars)
            {
                var a = kv.Value;
                if (!a.Pvp)
                    continue;
                var sp = new V2(view.x + a.Disp.X - cam.X, view.y + a.Disp.Y - cam.Y);
                if (!live || sp.X < view.x - 30f || sp.X > view.xMax + 30f || sp.Y < view.y - 30f || sp.Y > view.yMax + 30f)
                    continue;
                var bar = new Rect(sp.X - 25f, sp.Y - 46f, 50f, 6f);
                float f = Mathf.Clamp01(a.Hp / Session.RoomHp);
                Ui.Fill(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), new Color(0f, 0f, 0f, 0.7f));
                Ui.Fill(new Rect(bar.x, bar.y, bar.width * f, bar.height), Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(0.35f, 0.95f, 0.5f), f));
                if (a.Hp <= 0f)
                    Ui.Label(new Rect(sp.X - 50f, sp.Y - 70f, 100f, 18f), "KNOCKED OUT", 10, new Color(1f, 0.55f, 0.55f), TextAnchor.MiddleCenter, true);
            }

            var mini = new Rect(view.x + 10f, view.y + 10f, MiniW, MiniH);
            Ui.Fill(mini, new Color(0.02f, 0.04f, 0.08f, 0.82f));
            var edge = new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.7f);
            Ui.Fill(new Rect(mini.x, mini.y, mini.width, 1f), edge);
            Ui.Fill(new Rect(mini.x, mini.yMax - 1f, mini.width, 1f), edge);
            Ui.Fill(new Rect(mini.x, mini.y, 1f, mini.height), edge);
            Ui.Fill(new Rect(mini.xMax - 1f, mini.y, 1f, mini.height), edge);
            float mk = MiniW / Session.RoomW;
            var vp = new Rect(mini.x + cam.X * mk, mini.y + cam.Y * mk, view.width * mk, view.height * mk);
            var vc = new Color(1f, 1f, 1f, 0.85f);
            Ui.Fill(new Rect(vp.x, vp.y, vp.width, 1f), vc);
            Ui.Fill(new Rect(vp.x, vp.yMax - 1f, vp.width, 1f), vc);
            Ui.Fill(new Rect(vp.x, vp.y, 1f, vp.height), vc);
            Ui.Fill(new Rect(vp.xMax - 1f, vp.y, 1f, vp.height), vc);
            foreach (var kv in session.Avatars)
            {
                LobbyEntry e;
                if (!session.Lobby.TryGetValue(kv.Key, out e))
                    continue;
                var c = ToColor(Defs.Get(e.Role).Color);
                float d = kv.Key == session.MyId ? 6f : 5f;
                var dot = new Rect(mini.x + kv.Value.Disp.X * mk - d / 2f, mini.y + kv.Value.Disp.Y * mk - d / 2f, d, d);
                if (kv.Key == session.MyId)
                    Ui.Fill(new Rect(dot.x - 1f, dot.y - 1f, d + 2f, d + 2f), Color.white);
                Ui.Fill(dot, c);
            }

            // The PvP switch: yours alone. Off, you can neither shoot nor be shot.
            var box = new Rect(mini.x, mini.yMax + 6f, MiniW, 46f);
            Ui.Fill(box, new Color(0.03f, 0.05f, 0.09f, 0.85f));
            bool on = mine != null && mine.Pvp;
            var tog = new Rect(box.x + 8f, box.y + 2f, box.width - 12f, 28f);
            if (live)
            {
                bool now = Ui.Toggle(tog, on, "PVP");
                if (now != on)
                    session.SetMyPvp(now);
            }
            else
                Ui.Label(tog, "PVP", 14, Ui.Text, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(box.x + 8f, box.y + 28f, box.width - 12f, 16f), on ? "You can shoot and be shot" : "Off: no shooting either way", 10, on ? new Color(1f, 0.6f, 0.55f) : Ui.Muted, TextAnchor.MiddleLeft);
            // Your ability, under the switch (only while PvP is on): what it is, its keys and its cooldown.
            var abox = new Rect(box.x, box.yMax + 6f, MiniW, 44f);
            if (on && live && mine != null)
            {
                LobbyEntry me;
                if (session.Lobby.TryGetValue(session.MyId, out me))
                {
                    var kind = Session.AbilityFor(me.Role);
                    float total, left = session.MyAbilityCooldownLeft(out total);
                    Ui.Fill(abox, new Color(0.03f, 0.05f, 0.09f, 0.85f));
                    Ui.Label(new Rect(abox.x + 8f, abox.y + 2f, abox.width - 12f, 20f), Session.AbilityName(kind).ToUpperInvariant(), 13, left > 0f ? Ui.Muted : Color.white, TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(abox.x + 8f, abox.y + 20f, abox.width - 12f, 14f), Keybinds.SlotLabels[0] + " or right click", 10, Ui.Muted, TextAnchor.MiddleLeft);
                    Ui.Meter(new Rect(abox.x + 8f, abox.yMax - 8f, abox.width - 16f, 4f), 1f - left / total, left > 0f ? new Color(0.6f, 0.65f, 0.8f) : new Color(0.4f, 0.95f, 0.7f));
                }
            }
            hangoutHudRects[0] = mini;
            hangoutHudRects[1] = on && live ? new Rect(box.x, box.y, box.width, abox.yMax - box.y) : box;
        }

        readonly Rect[] hangoutHudRects = new Rect[2];

        static string MoveKeysText()
        {
            bool stock = true;
            foreach (var a in new[] { KeyAction.MoveUp, KeyAction.MoveDown, KeyAction.MoveLeft, KeyAction.MoveRight })
                stock &= Keybinds.IsDefault(a);
            return stock ? "WASD (or the arrow keys)" : "your movement keys";
        }

        /// Your own character: the movement keys walk it, and it turns the way it goes. With PvP on it faces the mouse instead, and the
        /// held button fires. (view and cam: the window and where it's looking, to turn the mouse into a point in the room.)
        void WalkMine(Avatar a, float dt, Rect view, V2 cam)
        {
            if (a.Hp <= 0f)
                return;  // knocked out: it waits for the host to respawn it
            V2 dir = new V2((Keybinds.Held(KeyAction.MoveRight) ? 1f : 0f) - (Keybinds.Held(KeyAction.MoveLeft) ? 1f : 0f),
                            (Keybinds.Held(KeyAction.MoveDown) ? 1f : 0f) - (Keybinds.Held(KeyAction.MoveUp) ? 1f : 0f));
            float aim = a.Aim;
            var mouse = Event.current.mousePosition;
            bool aiming = a.Pvp && view.Contains(mouse);
            if (aiming)
            {
                V2 target = new V2(mouse.x - view.x + cam.X, mouse.y - view.y + cam.Y);
                aim = (target - a.Pos).Angle;
            }
            else if (dir.LengthSq > 0f)
            {
                float want = dir.Normalized().Angle, d = want - aim;
                while (d > Mathf.PI) d -= 2f * Mathf.PI;
                while (d < -Mathf.PI) d += 2f * Mathf.PI;
                aim += Mathf.Clamp(d, -14f * dt, 14f * dt);
            }
            float castAim = dir.LengthSq > 0f ? dir.Angle : aim;  // a dash goes the way you walk, or the way you face when standing still
            if (dir.LengthSq > 0f)
                dir = dir.Normalized();
            session.MoveMyAvatar(a.Pos + dir * (Session.AvatarSpeed * dt) + session.TakeDashStep(a, dt), aim);
            // Ability: Space (or whatever it's bound to) or the right button.
            if (a.Pvp && (Keybinds.Down(KeyAction.Ability) || (Input.GetMouseButtonDown(1) && view.Contains(mouse))))
                session.CastMyAbility(castAim);
            // Fire: PvP on, the button held over the window (not over the minimap or switch, nor while the emote bar is picking).
            if (aiming && Input.GetMouseButton(0) && !emoteBarOpen && !hangoutHudRects[0].Contains(mouse) && !hangoutHudRects[1].Contains(mouse))
                session.FireMyShot(aim);
        }

        /// Each character's path for its trail, the last second or so (as the match does it: RankTrails.cs).
        void TickRoomTrails(float t, float dt)
        {
            foreach (var kv in session.Avatars)
            {
                List<TrailPoint> pts;
                if (!roomTrails.TryGetValue(kv.Key, out pts))
                    roomTrails[kv.Key] = pts = new List<TrailPoint>();
                LobbyEntry e;
                bool has = session.Lobby.TryGetValue(kv.Key, out e) && e.Trail != 0;
                V2 at = kv.Value.Disp;
                if (has && Rank.TrailId(e.Trail) == CycleId)  // (LightCycleTrail.cs)
                {
                    CycleRider cr;
                    if (!roomCycles.TryGetValue(kv.Key, out cr))
                        roomCycles[kv.Key] = cr = new CycleRider();
                    cr.Step(at, t);
                    pts.Clear();
                }
                else
                    roomCycles.Remove(kv.Key);
                if (has && Rank.TrailId(e.Trail) != CycleId && (pts.Count == 0 || (pts[pts.Count - 1].At - at).Length > 7f))
                    pts.Add(new TrailPoint { At = at, T = t });
                while (pts.Count > 0 && (!has || t - pts[0].T > TrailLife))
                    pts.RemoveAt(0);
                float shown;
                roomAim.TryGetValue(kv.Key, out shown);
                if (!roomAim.ContainsKey(kv.Key) || kv.Key == session.MyId)
                    shown = kv.Value.Aim;
                float d = kv.Value.Aim - shown;
                while (d > Mathf.PI) d -= 2f * Mathf.PI;
                while (d < -Mathf.PI) d += 2f * Mathf.PI;
                roomAim[kv.Key] = shown + d * (1f - Mathf.Exp(-16f * dt));
            }
        }

        /// GL: one character, with its trail behind it. Models sit around the origin, so each gets its own spot on the canvas.
        void DrawAvatar(int id, Avatar av, V2 origin, float ws, float t)
        {
            LobbyEntry e;
            if (!session.Lobby.TryGetValue(id, out e))
                return;
            V2 at = new V2(origin.X + av.Disp.X * ws, origin.Y + av.Disp.Y * ws);
            UiModelAt(at, AvatarZoom * ws);
            List<TrailPoint> pts;
            CycleRider cycle;
            if (e.Trail != 0 && Rank.TrailId(e.Trail) == CycleId && roomCycles.TryGetValue(id, out cycle))
                DrawCycleTrail(cycle, t, av.Disp);
            else if (e.Trail != 0 && roomTrails.TryGetValue(id, out pts) && pts.Count > 1)
            {
                string tid = Rank.TrailId(e.Trail);
                for (int i = 1; i < pts.Count; i++)
                {
                    if ((pts[i].At - pts[i - 1].At).Length > 120f)
                        continue;
                    TrailPiece(tid, pts[i].At - av.Disp, pts[i - 1].At - av.Disp, i, Mathf.Clamp01((t - pts[i].T) / TrailLife), t, V2.Zero);
                }
            }
            var p = roomPlayer;
            p.Role = e.Role;
            p.Def = Defs.Get(e.Role);
            p.Kit = e.Kit;
            p.Weapon = Defs.Weapon(e.Role, e.Kit, e.Weapon);
            p.Hp = p.MaxHp = 1f;
            p.Disp = p.Pos = V2.Zero;
            float aim;
            p.Aim = roomAim.TryGetValue(id, out aim) ? aim : av.Aim;
            emoteBodyFor = id;  // (so an emote moves this character, EmoteLooks.cs)
            DrawPlayer(p, t, true);
            emoteBodyFor = -1;
        }

        /// UI: the name plate under a character: rank badge, name, their frame (animated, as on the party card), and a green mark when ready.
        void DrawAvatarTag(int id, Avatar av, Rect view, V2 origin, float ws, float t)
        {
            LobbyEntry e;
            if (!session.Lobby.TryGetValue(id, out e))
                return;
            bool mine = id == session.MyId;
            float w = Mathf.Max(92f, 50f + e.Name.Length * 7.6f), h = 24f;
            float sx = origin.X + av.Disp.X * ws, sy = origin.Y + av.Disp.Y * ws;
            if (sx < view.x - 40f || sx > view.xMax + 40f || sy < view.y - 40f || sy > view.yMax + 40f)
                return;  // (off the window)
            var tag = new Rect(sx - w / 2f, sy + 40f * ws, w, h);
            tag.x = Mathf.Clamp(tag.x, view.x + 4f, view.xMax - w - 4f);
            tag.y = Mathf.Clamp(tag.y, view.y + 4f, view.yMax - h - 4f);
            if (e.Frame > 0)
                DrawFrameCard(tag, e.Frame, t);  // their equipped frame, animated like on the party card (FrameLooks.cs)
            else
            {
                Color rim = mine ? Ui.Accent : new Color(1f, 1f, 1f, 0.25f);
                Ui.Pill(new Rect(tag.x - 2f, tag.y - 2f, tag.width + 4f, tag.height + 4f), new Color(rim.r, rim.g, rim.b, 0.7f));
                Ui.Pill(tag, new Color(0.04f, 0.06f, 0.12f, 0.94f));
            }
            RankLook.Draw(new Rect(tag.x + 3f, tag.y + 1f, 22f, 22f), e.Rank);
            DrawName(new Rect(tag.x + 28f, tag.y, tag.width - 40f, tag.height), e.Name, 12, e.NameFx, t, mine ? Color.white : Ui.Text);  // NameEffects.cs
            if (e.Ready)
                Ui.Pill(new Rect(tag.xMax - 14f, tag.y + 8f, 8f, 8f), new Color(0.3f, 1f, 0.45f, 0.8f + 0.2f * Mathf.Sin(t * 4f)));
        }
    }
}
