using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The build screen's motion (owner Nov 19, mockup "Build Screen Motion" A and C-H): the power-on open (the tube opens from a line, the
    /// rail and panel slide in, the map prints top-down under a scan bar, the wallet counts up), the gliding ghost, the card flying to its
    /// spot and the hand closing behind it, the shockwave and door ripple after the slam, the tool frame sliding and the title typing in,
    /// the wallet's roll, and the breathing corners. Reduced effects (FxCalm) keeps every screen still. Called from ExpansionUi.cs and
    /// GridRoomsUi.cs; buying a mystery room from the map is in ExpansionUi.cs.
    public partial class ArenaApp
    {
        static float Ease01(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }

        // --- A: opening ----------------------------------------------------------------------------------------------------------

        float buildOpenT0 = -99f, buildDrawnT = -99f;

        /// Seconds since the build screen opened (it counts as opened when it wasn't drawn a moment ago); large under Reduced effects.
        float BuildAge => FxCalm ? 99f : Time.unscaledTime - buildOpenT0;

        void NoteBuildDrawn()
        {
            if (Time.unscaledTime - buildDrawnT > 0.25f)
            {
                buildOpenT0 = Time.unscaledTime;
                walletLast = -1;
                railTool = -1;
            }
            buildDrawnT = Time.unscaledTime;
        }

        /// Draws `draw` sliding in from `dx` px to the side and fading up, `delay` s after the screen opened.
        void BuildSlide(float delay, float dx, System.Action draw)
        {
            float p = Mathf.Clamp01((BuildAge - delay) / 0.42f);
            if (p >= 1f)
            {
                draw();
                return;
            }
            var m0 = GUI.matrix;
            float a0 = Ui.Alpha;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + dx * (1f - EaseBack(p)) * scale, offY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));  // (the screen's own matrix, shifted: Hud.cs does the same)
            Ui.Alpha = a0 * Ease01(0f, 0.6f, p);
            draw();
            Ui.Alpha = a0;
            GUI.matrix = m0;
        }

        /// Inside the map's GL pass: the map prints top-down, everything below the scan bar still dark.
        void DrawBuildPrint(Rect map)
        {
            float k = Ease01(0.15f, 0.75f, BuildAge);
            if (k >= 1f)
                return;
            float y = Mathf.Lerp(map.y, map.yMax, k);
            var bg = CrtStyle.Bg;
            var under = new Rgba(0.7f * 0.01f + 0.3f * bg.r, 0.7f * 0.03f + 0.3f * bg.g, 0.7f * 0.08f + 0.3f * bg.b, 1f);  // (the map's own dark fill over the screen)
            RoomMapRect(map.x, y, map.width, map.yMax - y, under);
            RoomMapRect(map.x, y - 10f, map.width, 10f, GridInk.WithAlpha(0.12f));
            RoomMapRect(map.x, y - 1f, map.width, 2f, GridInk.WithAlpha(0.9f));
        }

        /// Over the whole window: the tube switching on (a bright line across the middle that opens up and down).
        void DrawBuildPowerOn()
        {
            float age = BuildAge;
            if (age >= 0.45f || Event.current.type != EventType.Repaint)
                return;
            var box = new Rect(20f, 6f, W - 40f, H - 12f);
            float hw = box.width / 2f * Ease01(0f, 0.2f, age), hh = Mathf.Max(1.5f, box.height / 2f * Ease01(0.16f, 0.45f, age));
            float cx = box.center.x, cy = box.center.y;
            var black = new Color(0f, 0f, 0f, 1f);
            Ui.Fill(new Rect(box.x, box.y, box.width, cy - hh - box.y), black);
            Ui.Fill(new Rect(box.x, cy + hh, box.width, box.yMax - cy - hh), black);
            Ui.Fill(new Rect(box.x, cy - hh, cx - hw - box.x, 2f * hh), black);
            Ui.Fill(new Rect(cx + hw, cy - hh, box.xMax - cx - hw, 2f * hh), black);
            float line = 1f - Ease01(0.3f, 0.45f, age);
            Ui.Fill(new Rect(cx - hw, cy - 6f, 2f * hw, 12f), CrtUi.T(new Color(0.37f, 0.94f, 1f, 0.25f * line)));
            Ui.Fill(new Rect(cx - hw, cy - 1.5f, 2f * hw, 3f), new Color(0.85f, 0.97f, 1f, line));
        }

        /// H: the window's corner brackets breathe.
        void DrawBuildCorners()
        {
            if (FxCalm || Event.current.type != EventType.Repaint)
                return;
            var box = new Rect(20f, 6f, W - 40f, H - 12f);
            var c = CrtStyle.Ink.WithAlpha(0.25f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f)));
            const float L = 26f, T = 3f;
            Ui.Fill(new Rect(box.x - 2f, box.y - 2f, L, T), c); Ui.Fill(new Rect(box.x - 2f, box.y - 2f, T, L), c);
            Ui.Fill(new Rect(box.xMax + 2f - L, box.y - 2f, L, T), c); Ui.Fill(new Rect(box.xMax - 1f, box.y - 2f, T, L), c);
            Ui.Fill(new Rect(box.x - 2f, box.yMax - 1f, L, T), c); Ui.Fill(new Rect(box.x - 2f, box.yMax + 2f - L, T, L), c);
            Ui.Fill(new Rect(box.xMax + 2f - L, box.yMax - 1f, L, T), c); Ui.Fill(new Rect(box.xMax - 1f, box.yMax + 2f - L, T, L), c);
        }

        // --- F: the tool frame and the title ------------------------------------------------------------------------------------

        int railTool = -1;
        float railT0 = -99f, railFromY, railY;

        /// The rail's selection frame for the tool at `targetY`: true while it is still sliding (the key itself shows off till it lands).
        bool RailSlide(int tool, float targetY)
        {
            if (railTool < 0 || FxCalm)
            {
                railTool = tool;
                railY = targetY;
                railT0 = -99f;
                return false;
            }
            if (tool != railTool)
            {
                railTool = tool;
                railFromY = railY;
                railT0 = Time.unscaledTime;
            }
            float p = Mathf.Clamp01((Time.unscaledTime - railT0) / 0.22f);
            railY = Mathf.Lerp(railFromY, targetY, EaseBack(p));
            return p < 1f;
        }

        void DrawRailFrame(float x, float w)
        {
            var r = new Rect(x, railY, w, 36f);
            Ui.Fill(r, CrtStyle.Ink.WithAlpha(0.12f));
            CrtKit.Frame(r, 2f, CrtStyle.Ink);
        }

        /// The panel title typing itself in after a tool change or the open.
        string TypedTitle(string title)
        {
            if (FxCalm)
                return title;
            float t0 = Mathf.Max(railT0, buildOpenT0 + 0.35f), age = Time.unscaledTime - t0;
            if (age >= 0.35f || age < 0f)
                return age < 0f ? "" : title;
            int n = Mathf.Clamp(Mathf.FloorToInt(age / 0.35f * title.Length), 0, title.Length);
            return title.Substring(0, n) + (Mathf.Sin(Time.unscaledTime * 40f) > 0f ? "_" : "");
        }

        // --- G: the wallet ------------------------------------------------------------------------------------------------------

        int walletLast = -1, walletDelta, walletTickN = int.MinValue;
        float walletT0 = -99f, walletFrom, walletTickT0 = -99f;

        /// The number the wallet shows this frame: counting up on the open, rolling to a new total after a spend or a gain.
        int WalletShown(int have)
        {
            if (walletLast < 0)
            {
                walletLast = have;
                walletFrom = have;
                walletT0 = -99f;
            }
            if (have != walletLast)
            {
                walletFrom = WalletRoll(walletLast);
                walletDelta = have - walletLast;
                walletLast = have;
                walletT0 = Time.unscaledTime;
            }
            int shown = Mathf.RoundToInt(WalletRoll(have));
            float open = Ease01(0.3f, 0.8f, BuildAge);
            if (open < 1f)
                shown = Mathf.RoundToInt(have * open);
            if (shown != walletTickN)
            {
                walletTickN = shown;
                walletTickT0 = Time.unscaledTime;
            }
            return shown;
        }

        float WalletRoll(int have) => FxCalm ? have : Mathf.Lerp(walletFrom, have, Ease01(0f, 0.5f, Time.unscaledTime - walletT0));

        /// The number's small drop as each step lands (px).
        float WalletTickDrop => FxCalm ? 0f : -3f * (1f - Mathf.Clamp01((Time.unscaledTime - walletTickT0) / 0.08f));

        /// After the bar: the slice just spent draining away, and the "-8" / "+5" floating up beside the number.
        void DrawWalletAfter(Rect bar, Rect number, int cap)
        {
            if (FxCalm)
                return;
            float age = Time.unscaledTime - walletT0;
            if (walletDelta < 0 && age < 1f)
            {
                float fNow = Mathf.Clamp01((float)walletLast / cap), fWas = Mathf.Clamp01((float)(walletLast - walletDelta) / cap);
                float fTail = Mathf.Lerp(fWas, fNow, Ease01(0.15f, 0.9f, age));
                if (fTail > fNow)
                    Ui.Fill(new Rect(bar.x + bar.width * fNow, bar.y, bar.width * (fTail - fNow), bar.height), new Color(1f, 0.94f, 0.75f, 0.7f * (1f - age)));
            }
            if (walletDelta != 0 && age < 1.2f)
            {
                float k = age / 1.2f;
                var ink = walletDelta < 0 ? new Color(1f, 0.38f, 0.44f) : new Color(0.45f, 1f, 0.6f);
                Ui.Label(new Rect(number.x, number.y + 18f - 34f * k, number.width - 70f, 22f), (walletDelta < 0 ? "−" : "+") + Mathf.Abs(walletDelta), 16, ink.WithAlpha(1f - k * k), TextAnchor.MiddleRight, true);
            }
        }

        // --- C: the ghost ---------------------------------------------------------------------------------------------------------

        Placement? ghostLast;
        V2 ghostShown, ghostFrom;
        float ghostT0 = -99f, ghostSpinT0 = -99f, gridDenyT0 = -99f;
        int ghostTurnLast;
        V2 gridDenyAt;

        /// How far (world px) the ghost still is from the place under the mouse as it glides there; starts a quarter-turn spin on R.
        V2 GhostOffset(Placement pick)
        {
            V2 at = new V2(pick.X, pick.Y);
            if (FxCalm)
                return V2.Zero;
            if (!ghostLast.HasValue || Time.unscaledTime - ghostT0 > 1f && ghostShown.DistanceTo(at) > Expansion.GridCell * 6f)
            {
                ghostLast = pick;
                ghostShown = ghostFrom = at;
                ghostT0 = -99f;
                ghostTurnLast = expGridTurn;
            }
            var last = ghostLast.Value;
            if (last.X != pick.X || last.Y != pick.Y || last.Orient != pick.Orient)
            {
                ghostFrom = ghostShown;
                ghostT0 = Time.unscaledTime;
                if (expGridTurn != ghostTurnLast)
                    ghostSpinT0 = Time.unscaledTime;
                ghostLast = pick;
            }
            ghostTurnLast = expGridTurn;
            float p = Ease01(0f, 0.14f, Time.unscaledTime - ghostT0);
            ghostShown = ghostFrom + (at - ghostFrom) * (1f - (1f - p) * (1f - p));
            return ghostShown - at;
        }

        /// The quarter-turn left to spin (radians, 0 when done).
        float GhostSpin => FxCalm ? 0f : -Mathf.PI / 2f * (1f - EaseBack(Mathf.Clamp01((Time.unscaledTime - ghostSpinT0) / 0.2f)));

        /// A frame of marching dashes (the ghost's edge).
        void DashFrame(float x, float y, float w, float h, float t, Rgba c)
        {
            float phase = (Time.unscaledTime * 28f) % 10f;
            DashLine(x, y, w, true, t, c, phase);
            DashLine(x, y + h - t, w, true, t, c, -phase);
            DashLine(x, y, h, false, t, c, -phase);
            DashLine(x + w - t, y, h, false, t, c, phase);
        }

        void DashLine(float x, float y, float len, bool across, float t, Rgba c, float phase)
        {
            for (float s = phase % 10f - 10f; s < len; s += 10f)
            {
                float a = Mathf.Max(0f, s), b = Mathf.Min(len, s + 6f);
                if (b <= a) continue;
                if (across) RoomMapRect(x + a, y, b - a, t, c);
                else RoomMapRect(x, y + a, t, b - a, c);
            }
        }

        /// A click on the map where the picked piece fits nowhere: a red cross that shakes there.
        void DrawGridDeny(System.Func<V2, V2> M)
        {
            float age = Time.unscaledTime - gridDenyT0;
            if (age > 0.45f || FxCalm)
                return;
            V2 c = M(gridDenyAt) + new V2(Mathf.Sin(age * 70f) * 5f * (1f - age / 0.45f), 0f);
            var red = new Rgba(1f, 0.3f, 0.36f, 1f - age / 0.45f);
            Line(c + new V2(-7f, -7f), c + new V2(7f, 7f), 2.5f, red);
            Line(c + new V2(-7f, 7f), c + new V2(7f, -7f), 2.5f, red);
        }

        // --- D: the hand ----------------------------------------------------------------------------------------------------------

        readonly Rect[] gridSlotRects = new Rect[Expansion.HandMax];
        int[] palLast;
        readonly Vector2[] palOff = new Vector2[Expansion.HandMax];
        float palT0 = -99f;
        PieceCard flyCard = PieceCard.None;
        Rect flyFrom;
        V2 flyTo;
        float flyT0 = -99f;

        /// A card leaves the hand for the map: it flies from its slot to the spot.
        void StartCardFly(int slot, V2 worldMid)
        {
            if (FxCalm || slot < 0 || slot >= gridSlotRects.Length)
                return;
            flyCard = PickedPieceCard;
            flyFrom = gridSlotRects[slot];
            flyTo = worldMid;
            flyT0 = Time.unscaledTime;
        }

        /// The hand closing up: each card slides from where it was last frame (by card, not slot); a new card rises in.
        void NoteHand(byte[] hand, int n, System.Func<int, Vector2> slotPos)
        {
            if (palLast != null && !FxCalm)
            {
                bool changed = palLast.Length != n;
                for (int i = 0; !changed && i < n; i++) changed = palLast[i] != hand[i];
                if (changed)
                {
                    var used = new bool[palLast.Length];
                    for (int i = 0; i < n && i < palOff.Length; i++)
                    {
                        int from = -1;
                        for (int j = i; j < palLast.Length && from < 0; j++) if (!used[j] && palLast[j] == hand[i]) from = j;
                        for (int j = 0; j < palLast.Length && from < 0; j++) if (!used[j] && palLast[j] == hand[i]) from = j;
                        if (from >= 0) used[from] = true;
                        palOff[i] = from >= 0 ? slotPos(from) - slotPos(i) : new Vector2(0f, 18f);
                    }
                    palT0 = Time.unscaledTime;
                }
            }
            if (palLast == null || palLast.Length != n) palLast = new int[n];
            for (int i = 0; i < n; i++) palLast[i] = hand[i];
        }

        /// Slot i's card this frame: where it slides from, and (on the open) how far it has faded up.
        Rect HandCardRect(int i, Rect r, out float alpha)
        {
            float k = 1f - Ease01(0f, 0.25f, Time.unscaledTime - palT0);
            var off = i < palOff.Length ? palOff[i] * k : Vector2.zero;
            float open = Ease01(0.3f + 0.05f * i, 0.6f + 0.05f * i, BuildAge);
            alpha = open;
            return new Rect(r.x + off.x, r.y + off.y + 12f * (1f - open), r.width, r.height);
        }

        /// The picked card: gold corner brackets and a sheen crossing it now and then.
        void DrawPickedCardLook(Rect r)
        {
            if (FxCalm || Event.current.type != EventType.Repaint)
                return;
            var gold = CrtStyle.Gold;
            const float L = 8f, T = 2f;
            Ui.Fill(new Rect(r.x - 3f, r.y - 3f, L, T), gold); Ui.Fill(new Rect(r.x - 3f, r.y - 3f, T, L), gold);
            Ui.Fill(new Rect(r.xMax + 3f - L, r.y - 3f, L, T), gold); Ui.Fill(new Rect(r.xMax + 1f, r.y - 3f, T, L), gold);
            Ui.Fill(new Rect(r.x - 3f, r.yMax + 1f, L, T), gold); Ui.Fill(new Rect(r.x - 3f, r.yMax + 3f - L, T, L), gold);
            Ui.Fill(new Rect(r.xMax + 3f - L, r.yMax + 1f, L, T), gold); Ui.Fill(new Rect(r.xMax + 1f, r.yMax + 3f - L, T, L), gold);
            float s = (Time.unscaledTime * 0.7f) % 1.6f;  // the sheen: a pale strip crossing left to right, then a pause
            if (s < 1f)
            {
                float x0 = Mathf.Max(r.x, r.x + s * (r.width + 16f) - 16f), x1 = Mathf.Min(r.xMax, x0 + 10f);
                if (x1 > x0)
                    Ui.Fill(new Rect(x0, r.y + 1f, x1 - x0, r.height - 2f), new Color(1f, 1f, 1f, 0.06f));
            }
        }

        /// After the map's GL pass: the card on its way to the map, shrinking, with a short gold trail.
        void DrawCardFly(System.Func<V2, V2> M)
        {
            float p = (Time.unscaledTime - flyT0) / 0.3f;
            if (p >= 1f || p < 0f || flyCard == PieceCard.None || Event.current.type != EventType.Repaint)
                return;
            V2 to = M(flyTo);
            System.Func<float, Vector2> at = q =>
            {
                float e = q * q * (3f - 2f * q);
                return new Vector2(Mathf.Lerp(flyFrom.center.x, to.X, e), Mathf.Lerp(flyFrom.center.y, to.Y, e) - Mathf.Sin(e * Mathf.PI) * 70f);
            };
            for (int i = 5; i >= 1; i--)
            {
                var tp = at(Mathf.Max(0f, p - i * 0.05f));
                Ui.Fill(new Rect(tp.x - 2f, tp.y - 2f, 4f, 4f), CrtStyle.Gold.WithAlpha(0.5f - i * 0.08f));
            }
            var c = at(p);
            float s = Mathf.Lerp(1f, 0.35f, p * p), a0 = Ui.Alpha;
            Ui.Alpha = a0 * (1f - 0.5f * p);
            var r = new Rect(c.x - flyFrom.width * s / 2f, c.y - flyFrom.height * s / 2f, flyFrom.width * s, flyFrom.height * s);
            Ui.Fill(r, new Color(0.07f, 0.14f, 0.29f, 0.95f));
            CrtKit.Frame(r, 2f, CrtStyle.Gold);
            DrawCardGlyph(flyCard, new Rect(r.x + 8f * s, r.y + 6f * s, r.width - 16f * s, 38f * s), Color.white);
            Ui.Alpha = a0;
        }

        // --- E: after the slam --------------------------------------------------------------------------------------------------

        /// A door spot by the piece just built pops in, one after another out from where it joined (size 0-1.1; 1 for every other spot).
        float DoorPop(V2 pos, out float k)
        {
            k = 1f;
            float t = Time.unscaledTime - placeFxT0;
            if (FxCalm || t > 2f || placeFxRooms.Count == 0)
                return 1f;
            bool near = false;
            foreach (var r in placeFxRooms)
                near |= pos.X >= r.X - 2f && pos.X <= r.X1 + 2f && pos.Y >= r.Y - 2f && pos.Y <= r.Y1 + 2f;
            if (!near)
                return 1f;
            float delay = 0.45f + pos.DistanceTo(placeFxDoor) / (Expansion.GridCell * 5f);
            k = Mathf.Clamp01((t - delay) / 0.35f);
            return k <= 0f ? 0f : EaseBack(Mathf.Min(1f, k / 0.7f));
        }

        /// A built door's mark, popping in after a build (DoorPop) with a ring flashing round it.
        void DrawDoorSpot(V2 c, V2 world, Rgba ink)
        {
            float k, s = DoorPop(world, out k);
            if (s <= 0f)
                return;
            RoomMapRect(c.X - 3f * s, c.Y - 3f * s, 6f * s, 6f * s, k < 1f ? new Rgba(1f, 1f, 1f, 0.95f) : ink);
            if (k < 1f)  // a square ring flying out from it
                RoomMapFrame(c.X - 5f - 9f * k, c.Y - 5f - 9f * k, 10f + 18f * k, 10f + 18f * k, 1.5f, ink.WithAlpha(0.9f * (1f - k)));
        }

        /// The ring rolling out from where the piece joined the map (inside DrawPlaceFx; kept within the map).
        void DrawPlaceRing(System.Func<V2, V2> M, float after)
        {
            if (FxCalm || after < 0f || after > 0.8f || !roomClip.HasValue)
                return;
            float e = 1f - (1f - after / 0.8f) * (1f - after / 0.8f);
            V2 c = M(placeFxDoor);
            var clip = roomClip.Value;
            float room = Mathf.Min(Mathf.Min(c.X - clip.x, clip.xMax - c.X), Mathf.Min(c.Y - clip.y, clip.yMax - c.Y));
            float r1 = Mathf.Min(18f + e * 160f, room), r2 = Mathf.Min(10f + e * 110f, room);
            if (r1 > 4f) Arc(c, r1, 3f * (1f - e) + 1f, new Rgba(1f, 0.78f, 0.29f, 0.9f * (1f - e)), 40);
            if (r2 > 4f) Arc(c, r2, 1f, GridInk.WithAlpha(0.5f * (1f - e)), 32);
        }

        /// After the GL pass: "+1 ROOM" rising over the piece just built.
        void DrawPlaceLabel(System.Func<V2, V2> M, Rect map)
        {
            float t = Time.unscaledTime - placeFxT0 - 0.5f;
            if (FxCalm || t < 0f || t > 1.2f || placeFxRooms.Count == 0)
                return;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue;
            foreach (var r in placeFxRooms) { x0 = Mathf.Min(x0, r.X); y0 = Mathf.Min(y0, r.Y); x1 = Mathf.Max(x1, r.X1); }
            V2 top = M(new V2((x0 + x1) / 2f, y0));
            float k = t / 1.2f;
            var lr = new Rect(top.X - 60f, top.Y - 26f - 22f * k, 120f, 18f);
            if (map.Contains(lr.center))
                Ui.Label(lr, "+1 ROOM", 14, CrtStyle.Gold.WithAlpha(1f - k * k), TextAnchor.MiddleCenter, true);
        }
    }
}
