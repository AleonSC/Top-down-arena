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

        void DrawHangout(Rect body, float t)
        {
            Ui.Label(new Rect(body.x + 4f, body.y, body.width - 8f, 26f), "Walk around with " + MoveKeysText() + ". Everyone sees your character, trail and frame. There's nothing to shoot here.", 13, Ui.Muted, TextAnchor.MiddleLeft);
            var room = new Rect(body.x + (body.width - Session.RoomW) / 2f, body.y + 32f, Session.RoomW, Session.RoomH);
            Avatar mine;
            session.Avatars.TryGetValue(session.MyId, out mine);

            if (Event.current.type == EventType.Repaint && hangoutFrame != Time.frameCount)
            {
                hangoutFrame = Time.frameCount;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                session.StepAvatars(dt);
                if (mine != null && HangoutKeysFree)
                    WalkMine(mine, dt);
                TickRoomTrails(t, dt);
            }

            // The floor: a faint grid and a rim, with the lobby's own background showing through.
            Ui.Fill(room, new Color(0.03f, 0.05f, 0.1f, 0.62f));
            for (float x = 50f; x < room.width; x += 50f)
                Ui.Fill(new Rect(room.x + x, room.y, 1f, room.height), new Color(1f, 1f, 1f, 0.035f));
            for (float y = 50f; y < room.height; y += 50f)
                Ui.Fill(new Rect(room.x, room.y + y, room.width, 1f), new Color(1f, 1f, 1f, 0.035f));
            Ui.Fill(new Rect(room.x, room.y, room.width, 2f), new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.5f));
            Ui.Fill(new Rect(room.x, room.yMax - 2f, room.width, 2f), new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.5f));
            Ui.Fill(new Rect(room.x, room.y, 2f, room.height), new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.5f));
            Ui.Fill(new Rect(room.xMax - 2f, room.y, 2f, room.height), new Color(HangoutColor.r, HangoutColor.g, HangoutColor.b, 0.5f));

            // Everyone, the ones further down the room in front.
            var order = new List<KeyValuePair<int, Avatar>>(session.Avatars);
            order.Sort((a, b) => a.Value.Disp.Y.CompareTo(b.Value.Disp.Y));
            if (BeginUiGl())
            {
                UiClip(room);
                foreach (var kv in order)
                    DrawAvatar(kv.Key, kv.Value, room, t);
                EndUiGl();
            }
            foreach (var kv in order)
                DrawAvatarTag(kv.Key, kv.Value, room, t);
            foreach (var kv in order)  // their emotes (EmoteBar.cs)
            {
                int index;
                float age;
                if (session.EmoteOf(kv.Key, out index, out age))
                    DrawEmote(index, new Vector2(room.x + kv.Value.Disp.X, room.y + kv.Value.Disp.Y - 30f), age, 1f);
            }
            TickEmoteBar(HangoutKeysFree);
            DrawEmoteBar(room.center.x, room.yMax - 14f);
        }

        static string MoveKeysText()
        {
            bool stock = true;
            foreach (var a in new[] { KeyAction.MoveUp, KeyAction.MoveDown, KeyAction.MoveLeft, KeyAction.MoveRight })
                stock &= Keybinds.IsDefault(a);
            return stock ? "WASD (or the arrow keys)" : "your movement keys";
        }

        /// Your own character: the movement keys walk it, and it turns the way it goes.
        void WalkMine(Avatar a, float dt)
        {
            V2 dir = new V2((Keybinds.Held(KeyAction.MoveRight) ? 1f : 0f) - (Keybinds.Held(KeyAction.MoveLeft) ? 1f : 0f),
                            (Keybinds.Held(KeyAction.MoveDown) ? 1f : 0f) - (Keybinds.Held(KeyAction.MoveUp) ? 1f : 0f));
            float aim = a.Aim;
            if (dir.LengthSq > 0f)
            {
                dir = dir.Normalized();
                float want = dir.Angle, d = want - aim;
                while (d > Mathf.PI) d -= 2f * Mathf.PI;
                while (d < -Mathf.PI) d += 2f * Mathf.PI;
                aim += Mathf.Clamp(d, -14f * dt, 14f * dt);
            }
            session.MoveMyAvatar(a.Pos + dir * (Session.AvatarSpeed * dt), aim);
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
        void DrawAvatar(int id, Avatar av, Rect room, float t)
        {
            LobbyEntry e;
            if (!session.Lobby.TryGetValue(id, out e))
                return;
            V2 at = new V2(room.x + av.Disp.X, room.y + av.Disp.Y);
            UiModelAt(at, AvatarZoom);
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
        void DrawAvatarTag(int id, Avatar av, Rect room, float t)
        {
            LobbyEntry e;
            if (!session.Lobby.TryGetValue(id, out e))
                return;
            bool mine = id == session.MyId;
            float w = Mathf.Max(92f, 50f + e.Name.Length * 7.6f), h = 24f;
            var tag = new Rect(room.x + av.Disp.X - w / 2f, room.y + av.Disp.Y + 40f, w, h);
            tag.x = Mathf.Clamp(tag.x, room.x + 4f, room.xMax - w - 4f);
            tag.y = Mathf.Min(tag.y, room.yMax - h - 4f);
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
