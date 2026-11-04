using UnityEngine;

namespace Arena
{
    /// The move between "Who's playing?" and the main menu (look C, "Plot points", of Docs/Mockups/profile-transitions.html),
    /// at half speed: about 4.5 s. Picking a profile plays it forward; the menu's "Switch profile" plays it backwards.
    /// Its clock `profTrT` runs in the mockup's seconds (0 to ProfTrLen) at ProfTrSpeed of real time:
    ///   0.1 s   WHO'S PLAYING? falls into the axis; each box shrinks into a point on it (one after another, with a ping each)
    ///   0.8 s   the points you didn't pick shoot off the ends of the axis; yours slides to the origin
    ///   0.7 s   the menu's title is written (the menu's own intro, MenuIntro.cs, run from a remapped clock: ProfTrToIntro)
    ///   1.1 s   a sweep as your point opens into the menu's box; the buttons drop in; the bar slides up
    /// There are always four boxes (three slots and New profile, ProfilesUi.cs), so every move is the same. Nothing can be
    /// clicked meanwhile (the menu counts as still in its intro), and any key skips to the end. Reduced effects or Visual
    /// effects off: no move, the screens just change (and the menu's short fade plays).
    public partial class ArenaApp
    {
        const float ProfTrLen = 2.26f, ProfTrSpeed = 0.5f;
        const float ProfTrPenAt = 0.7f, ProfTrPenRate = 0.65f;  // menu intro second 0.35 happens at 0.7; each intro second takes 0.65 of the clock

        bool profTr;               // the move is playing
        bool profTrFwd;            // true: picker to menu; false: menu to picker
        float profTrT, profTrPrev, profTrAge;
        int profTrSlot;            // the box that becomes (or came from) the point at the origin

        bool ProfTrFull => fxOn && !fxReduced;

        /// The menu's intro clock for the move's clock: nothing before the pen starts, then the intro's own timings, squeezed.
        static float ProfTrToIntro(float t) => 0.35f + Mathf.Max(0f, t - ProfTrPenAt) / ProfTrPenRate;

        /// The axis is already full during the move.
        float MenuAxisK => profTr ? 1f : MenuIntroK(0f, 0.45f);

        /// A profile was picked (ChooseProfile): play the move in, or just show the menu.
        void StartProfTrIn(string id)
        {
            whoOpen = false;
            WhoRefresh();
            int slot = whoList.FindIndex(p => p.Id == id);
            if (!ProfTrFull || slot < 0 || slot >= WhoSlots)
                return;
            profTr = true;
            profTrFwd = true;
            profTrT = 0f;
            profTrPrev = -0.0001f;
            profTrAge = 0f;
            profTrSlot = slot;
            menuIntroDone = true;  // this is the menu's intro now
            menuIntroT = ProfTrToIntro(0f);
        }

        /// The menu's "Switch profile": play the move backwards, or just show the picker.
        void StartProfTrOut()
        {
            WhoRefresh();
            int slot = whoList.FindIndex(p => p.Id == ProfileStore.ActiveId);
            if (!ProfTrFull || slot < 0 || slot >= WhoSlots)
            {
                OpenWho();
                return;
            }
            whoManage = false;
            whoEdit = null;
            whoError = "";
            whoArmed = null;
            profTr = true;
            profTrFwd = false;
            profTrT = ProfTrLen;
            profTrPrev = ProfTrLen + 0.0001f;
            profTrAge = 0f;
            profTrSlot = slot;
            menuIntroT = MenuIntroLen - 0.001f;
            sounds.Play(Sfx.ProfSelect, 0.7f, Time.unscaledTime);
        }

        /// Update (from TickMenuIntro): run the clock and its sounds, and hand the menu its place in its intro.
        void TickProfTr(float dt)
        {
            profTrAge += dt;
            if (Input.anyKeyDown && profTrAge > 0.25f)
                profTrT = profTrFwd ? ProfTrLen : 0f;  // skipped: straight to the end, silently
            else
            {
                profTrT = Mathf.Clamp(profTrT + (profTrFwd ? 1f : -1f) * dt * ProfTrSpeed, 0f, ProfTrLen);
                PlayProfTrCues(profTrPrev, profTrT);
            }
            profTrPrev = profTrT;
            menuIntroT = Mathf.Min(MenuIntroLen - 0.001f, ProfTrToIntro(profTrT));
            if (profTrFwd && profTrT >= ProfTrLen)
            {
                profTr = false;
                menuIntroT = MenuIntroLen;
            }
            else if (!profTrFwd && profTrT <= 0f)
            {
                profTr = false;
                menuIntroT = MenuIntroLen;
                OpenWho();
            }
        }

        // t: when it plays going forward; d: how long it lasts (going backwards it plays that much later in the clock);
        // back: what plays going backwards (None: nothing).
        struct ProfCue
        {
            public float t, d, vol;
            public Sfx fwd, back;
            public ProfCue(float t, float d, Sfx fwd, Sfx back, float vol) { this.t = t; this.d = d; this.fwd = fwd; this.back = back; this.vol = vol; }
        }

        static readonly ProfCue[] ProfCues =
        {
            new ProfCue(0.00f, 0.15f, Sfx.ProfSelect, Sfx.None, 0.7f),     // the click on your box
            new ProfCue(0.46f, 0.30f, Sfx.ProfPing, Sfx.ProfPing, 0.7f),   // the four boxes become points, a ping each (rising)
            new ProfCue(0.53f, 0.30f, Sfx.ProfPing, Sfx.ProfPing, 0.7f),
            new ProfCue(0.60f, 0.30f, Sfx.ProfPing, Sfx.ProfPing, 0.7f),
            new ProfCue(0.67f, 0.30f, Sfx.ProfPing, Sfx.ProfPing, 0.7f),
            new ProfCue(0.70f, 0.60f, Sfx.ProfScratch, Sfx.ProfScratch, 0.7f),  // the pen writes the title
            new ProfCue(0.85f, 0.25f, Sfx.ProfZip, Sfx.ProfZip, 0.8f),     // the others shoot off the ends
            new ProfCue(1.10f, 0.45f, Sfx.ProfSweep, Sfx.ProfSweepBack, 0.7f),  // your point opens into the box
            new ProfCue(1.66f, 0.05f, Sfx.PauseType, Sfx.PauseType, 0.5f),  // Host, Join and Continue drop in
            new ProfCue(1.70f, 0.30f, Sfx.PauseSettle, Sfx.PauseClose, 0.5f),  // the box settles
            new ProfCue(1.78f, 0.05f, Sfx.PauseType, Sfx.PauseType, 0.5f),
            new ProfCue(1.84f, 0.26f, Sfx.ProfBar, Sfx.ProfBar, 0.8f),     // the bar slides up
            new ProfCue(1.86f, 0.05f, Sfx.PauseType, Sfx.PauseType, 0.5f),
            new ProfCue(1.95f, 0.20f, Sfx.ProfChord, Sfx.ProfChord, 0.6f), // a low chord as the menu settles
        };

        /// Plays each cue the clock passed between a and b (forward: its start; backwards: its end).
        void PlayProfTrCues(float a, float b)
        {
            float now = Time.unscaledTime;
            foreach (var c in ProfCues)
            {
                if (b > a && a < c.t && c.t <= b)
                    sounds.Play(c.fwd, c.vol, now);
                else if (b < a && b <= c.t + c.d && c.t + c.d < a && c.back != Sfx.None)
                    sounds.Play(c.back, c.vol, now);
            }
        }

        /// The whole screen during the move: the menu at its place in its intro (its title, axis, box and bar), with the
        /// picker's title and boxes on top.
        void DrawProfTr()
        {
            DrawMenu();
            DrawProfTrWho(profTrT);
        }

        /// A box as it sits on the picker (nothing in it can be clicked).
        void DrawWhoBoxStatic(int i, Rect r, float t)
        {
            if (i == WhoSlots)
                DrawWhoNewCard(r, false);
            else if (WhoInSlot(i) != null)
                DrawWhoCard(r, WhoInSlot(i), t, false);
            else
                DrawWhoEmptyCard(r);
        }

        void DrawProfTrWho(float T)
        {
            float t = Time.unscaledTime;
            float tk = Seg(T, 0.1f, 0.5f);
            if (tk < 1f)
                WithAlpha(1f - tk, () => Ui.Glitch(new Rect(0f, MenuTitleY - 56f + 78f * tk * tk, W, 80f), "WHO'S PLAYING?", 58, Color.white, TextAnchor.MiddleCenter, Ui.Accent));

            int boxes = WhoSlots + 1;
            var dotAt = new Vector2[boxes];
            var dotCol = new Color[boxes];
            var solid = new float[boxes];
            var trail = new float[boxes];   // 0 none; else its length, signed by the way it flies
            var label = new float[boxes];
            for (int i = 0; i < boxes; i++)
            {
                var R = WhoBox(i);
                var info = i < WhoSlots ? WhoInSlot(i) : null;
                var col = i == WhoSlots ? Ui.Muted : WhoColor(info);
                float k1 = Seg(T, 0.1f + 0.07f * i, 0.5f + 0.07f * i), e1 = EaseOut(k1), k2 = Seg(T, 0.85f, 1.15f);
                bool pick = i == profTrSlot;
                float cx = R.center.x, side = cx >= W / 2f ? 1f : -1f;
                float dx = pick ? (W / 2f - cx) * EaseOut(Seg(T, 0.8f, 1.1f)) : side * 1000f * k2 * k2;
                if (pick ? T >= 1.1f : k2 >= 1f)
                    continue;
                var r = new Rect(Mathf.Lerp(R.x, cx - 7f, e1) + dx, Mathf.Lerp(R.y, MenuAxisY - 7f, e1), Mathf.Lerp(R.width, 14f, e1), Mathf.Lerp(R.height, 14f, e1));
                if (k1 <= 0f)
                {
                    DrawWhoBoxStatic(i, R, t);
                    continue;
                }
                Ui.Card(r, col, 0.9f);
                float ca = 1f - Mathf.Clamp01(k1 * 2.5f);
                if (ca > 0f)  // what was in the box fades out as the box closes up
                {
                    GUI.BeginGroup(r);
                    var inner = new Rect(0f, 0f, R.width, R.height);
                    int ii = i;
                    WithAlpha(ca, () => DrawWhoBoxStatic(ii, inner, t));
                    GUI.EndGroup();
                }
                dotAt[i] = r.center;
                dotCol[i] = col;
                solid[i] = Mathf.Clamp01((k1 - 0.6f) / 0.4f);
                trail[i] = pick ? 0f : side * (30f + 140f * k2) * (k2 > 0f ? 1f : 0f);
                label[i] = Seg(k1, 0.7f, 1f) * (1f - Seg(T, pick ? 0.9f : 0.8f, pick ? 1.1f : 1.0f));
            }

            if (BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                for (int i = 0; i < boxes; i++)
                {
                    if (solid[i] <= 0f && trail[i] == 0f)
                        continue;
                    var c = dotCol[i];
                    var p = new V2(dotAt[i].x, dotAt[i].y);
                    if (trail[i] != 0f)
                        for (int s = 0; s < 8; s++)
                            Line(new V2(p.X - trail[i] * s / 8f, p.Y), new V2(p.X - trail[i] * (s + 1) / 8f, p.Y), 3f, new Rgba(c.r, c.g, c.b, 0.8f * (1f - s / 8f)));
                    if (solid[i] > 0f)
                    {
                        Circle(p, 11f, new Rgba(c.r, c.g, c.b, 0.22f * solid[i]), 18);
                        Circle(p, 7f, new Rgba(c.r, c.g, c.b, solid[i]), 16);
                    }
                }
                var pickInfo = WhoInSlot(profTrSlot);
                float kk = Seg(T, 1.1f, 1.55f);
                if (T >= 1.1f && kk < 1f)  // yours, at the origin, giving way to the box
                {
                    var c = WhoColor(pickInfo);
                    var o = new V2(W / 2f, MenuAxisY);
                    Circle(o, 7f + 16f * kk, new Rgba(c.r, c.g, c.b, 0.25f * (1f - kk)), 20);
                    Circle(o, 7f * (1f - kk), new Rgba(c.r, c.g, c.b, 1f), 16);
                }
                EndUiGl();
            }
            for (int i = 0; i < boxes; i++)
            {
                if (label[i] <= 0f)
                    continue;
                var info = i < WhoSlots ? WhoInSlot(i) : null;
                string text = i == WhoSlots ? "New" : info != null ? info.Name : "Empty";
                var c = Color.Lerp(dotCol[i], Color.white, 0.4f);
                Ui.Label(new Rect(dotAt[i].x - 60f, MenuAxisY + 14f, 120f, 18f), text, 12, new Color(c.r, c.g, c.b, label[i]), TextAnchor.MiddleCenter);
            }
        }
    }
}
