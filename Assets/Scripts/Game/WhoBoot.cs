using UnityEngine;

namespace Arena
{
    /// Who's playing? booting up when the game opens (option C of Docs/Mockups/whos-playing-load-in.html, with a fade from black first):
    ///   0.0-0.6 s   the whole screen fades up from black, the monitor's glass still off;
    ///   0.55 s      the glass powers on: a bright line widens across it, then opens top and bottom (CrtPower);
    ///   0.85-1.8 s  boot lines type on the glass in VT323 (BIOS, memory, drive A:, READING PROFILES ### n FOUND) while the drive
    ///               reads (its light blinking amber, the cells filling; DiskRead);
    ///   1.9 s       a white flash and WHO'S PLAYING? snaps in with a red / cyan split that settles (CrtReady);
    ///   2.0 s       the disks rise from below, the middle one first, then each pair outwards 0.08 s apart (DiskSlide);
    ///   2.75 s      a soft slanted glint sweeps the middle disk (kept to its outline); 2.9 s the ‹ › keys, PROFILE n OF m and the two buttons rise in.
    /// Only at launch, and once. Any key or click skips to the end under the channel flip (SkipStatic.cs). Nothing takes a click until
    /// it's over. Reduced effects or Visual effects off: a 0.45 s fade from black instead.
    public partial class ArenaApp
    {
        const float WhoBootLenFull = 3.45f, WhoBootLenFade = 0.45f;
        const float WbFadeLen = 0.6f, WbPowerAt = 0.55f, WbPowerLen = 0.3f, WbTypeAt = 0.85f, WbTypeEnd = 1.8f, WbReadAt = 1.15f,
            WbTitleAt = 1.9f, WbDiskAt = 2.0f, WbDiskStep = 0.08f, WbDiskLen = 0.55f, WbGlintAt = 2.75f, WbGlintLen = 0.45f, WbUiAt = 2.9f;
        float whoBootT = -1f;      // seconds into the boot; below 0 when none has started
        bool whoBootDone;          // it has started once this launch
        int whoBootTyped;          // boot-line letters whose key sound has played
        int whoBootSkipFrame = -1;  // the frame a key or click skipped the boot: that press does nothing else

        bool WhoBootFull => fxOn && !fxReduced;
        float WhoBootLen => WhoBootFull ? WhoBootLenFull : WhoBootLenFade;
        bool WhoBootPlaying => whoBootT >= 0f && whoBootT < WhoBootLen;

        /// How far a piece of the screen is into its part of the boot (0 to 1), or 1 when the full boot isn't playing.
        float WhoBootK(float a, float b) => WhoBootPlaying && WhoBootFull ? Seg(whoBootT, a, b) : 1f;

        string[] WhoBootLines => new[]
        {
            "ARENA CRT-14    BIOS v1.06",
            "MEMORY TEST ......... 640K OK",
            "DRIVE A: ............ READY",
            "READING PROFILES .... ",
        };

        /// Update: start the boot the first time Who's playing? shows, run its clock and its sounds, and skip it on any key or click.
        void TickWhoBoot(float dt)
        {
            bool on = session.Screen == AppScreen.Menu && whoOpen && !profTr && !pacOpen;
            if (on && !whoBootDone)
            {
                whoBootDone = true;
                whoBootT = 0f;
                whoBootTyped = 0;
                return;
            }
            if (!WhoBootPlaying)
                return;
            if (!on || Input.anyKeyDown)
            {
                if (on && WhoBootFull && whoBootT < WbUiAt)
                    StartSkipStatic();  // the channel flip (SkipStatic.cs)
                if (on)
                    whoBootSkipFrame = Time.frameCount;
                whoBootT = WhoBootLen;
                return;
            }
            float before = whoBootT;
            whoBootT += dt;
            if (WhoBootFull)
                PlayWhoBootCues(before, whoBootT);
        }

        void PlayWhoBootCues(float a, float b)
        {
            if (sounds == null)
                return;
            float now = Time.unscaledTime;
            System.Func<float, bool> Hit = at => a < at && b >= at;
            if (Hit(WbPowerAt)) sounds.Play(Sfx.CrtPower, 0.6f, now);
            if (Hit(WbReadAt)) sounds.Play(Sfx.DiskRead, 0.5f, now);
            if (Hit(WbTitleAt)) sounds.Play(Sfx.CrtReady, 0.55f, now);
            if (Hit(WbDiskAt)) sounds.Play(Sfx.DiskSlide, 0.5f, now);
            // A key click every few letters of the boot lines, a line feed at each line's end.
            string all = string.Join("\n", WhoBootLines);
            int n = Mathf.FloorToInt(Seg(b, WbTypeAt, WbTypeEnd - 0.25f) * all.Length);
            while (whoBootTyped < n)
            {
                char c = all[whoBootTyped++];
                if (c == '\n')
                    sounds.Play(Sfx.CrtLine, 0.35f, now);
                else if (c != ' ' && c != '.' && whoBootTyped % 3 == 0)
                    sounds.Play(Sfx.CrtType, 0.4f, now);
            }
        }

        /// The monitor's title while booting: nothing until the flash, then all of it.
        MonTitle WhoBootTitle(MonTitle title) => WhoBootPlaying && WhoBootFull && whoBootT < WbTitleAt ? new MonTitle { text = title.text, typed = 0, flat = true } : title;

        /// The drive while booting: empty, then reading (light blinking, cells filling) until the title comes up.
        DriveState WhoBootDrive(DriveState drive)
        {
            if (!WhoBootPlaying || !WhoBootFull || whoBootT >= WbTitleAt)
                return drive;
            var s = DriveEmpty(whoBootT < WbReadAt ? "DRIVE A:" : "READING PROFILES…");
            if (whoBootT >= WbReadAt)
            {
                s.ledOn = Mathf.FloorToInt(whoBootT * 14f) % 2 == 0;
                s.prog = Seg(whoBootT, WbReadAt, WbTitleAt - 0.1f);
            }
            return s;
        }

        /// Over the monitor's glass while booting: off (black), the power-on line, the boot lines, then the flash and the title's colour
        /// split as it settles.
        void DrawWhoBootGlass()
        {
            if (!WhoBootPlaying || !WhoBootFull)
                return;
            var box = CrtBox;
            float T = whoBootT, t = Time.unscaledTime;
            if (T < WbTitleAt)
            {
                DrawCrtBack();  // a clean glass (no title cursor) for the boot lines
                if (T >= WbTypeAt)
                {
                    string all = string.Join("\n", WhoBootLines);
                    int n = Mathf.FloorToInt(Seg(T, WbTypeAt, WbTypeEnd - 0.25f) * all.Length);
                    var lines = all.Substring(0, n).Split('\n');
                    if (n >= all.Length)
                    {
                        int k = Mathf.Min(3, Mathf.FloorToInt(Seg(T, WbTypeEnd - 0.25f, WbTypeEnd) * 4f));
                        lines[lines.Length - 1] += k < 3 ? new string('#', k + 1) : "### " + whoList.Count + " FOUND";
                    }
                    else if ((int)(t * 8f) % 2 == 0)
                        lines[lines.Length - 1] += "_";
                    bool was = Ui.CrtFont;
                    Ui.CrtFont = true;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var r = new Rect(box.x + 28f, box.y + 12f + i * 26f, box.width - 56f, 26f);
                        Ui.Label(new Rect(r.x - 1.5f, r.y, r.width, r.height), lines[i], 24, new Color(1f, 0.25f, 0.4f, 0.25f));
                        Ui.Label(new Rect(r.x + 1.5f, r.y, r.width, r.height), lines[i], 24, new Color(0.2f, 0.9f, 1f, 0.25f));
                        Ui.Label(r, lines[i], 24, new Color(0.62f, 0.91f, 1f));
                    }
                    Ui.CrtFont = was;
                }
                GUI.BeginGroup(box);
                float roll = (t * 18f) % 4f;
                for (float y = -4f + roll; y < box.height; y += 4f)
                    Ui.Fill(new Rect(0f, y, box.width, 2f), new Color(0f, 0f, 0f, 0.28f));
                GUI.EndGroup();
            }
            // Power-on: a bright line widens from the middle, then the black opens up and down from it.
            if (T < WbPowerAt + WbPowerLen)
            {
                float p = Seg(T, WbPowerAt, WbPowerAt + WbPowerLen);
                float open = EaseOut3(Seg(p, 0.45f, 1f)) * box.height / 2f, mid = box.center.y;
                var black = new Color(0.008f, 0.01f, 0.02f);
                Ui.Fill(new Rect(box.x, box.y, box.width, mid - open - box.y), black);
                Ui.Fill(new Rect(box.x, mid + open, box.width, box.yMax - mid - open), black);
                if (p > 0f)
                {
                    float lw = box.width * EaseOut3(Seg(p, 0f, 0.45f)), la = 1f - Seg(p, 0.6f, 1f);
                    Ui.RadialGlow(new Rect(box.center.x - lw / 2f, mid - 14f - open, lw, 28f + open * 2f), new Color(0.6f, 0.8f, 1f, 0.5f * la));
                    Ui.Fill(new Rect(box.center.x - lw / 2f, mid - 1.5f - open, lw, 3f + open * 2f), new Color(0.85f, 0.93f, 1f, 0.9f * la));
                }
            }
            // The flash, and the title's red / cyan split settling.
            if (T >= WbTitleAt - 0.05f)
            {
                float f = 1f - Seg(T, WbTitleAt - 0.05f, WbTitleAt + 0.12f);
                if (f > 0f)
                    Ui.Fill(box, new Color(0.87f, 0.91f, 1f, 0.85f * f));
                float sp = 12f * Mathf.Exp(-(T - WbTitleAt) * 6f);
                if (sp > 0.4f)
                {
                    float jit = Mathf.Sin(t * 97f) * sp * 0.3f;
                    var r = new Rect(jit, MenuTitleY - MenuTitleSize * 0.6f, W, MenuTitleSize * 1.2f);
                    Ui.Label(new Rect(r.x - sp, r.y, r.width, r.height), WhoTitleText, (int)MenuTitleSize, new Color(1f, 0.18f, 0.42f, 0.5f), TextAnchor.MiddleCenter, true);
                    Ui.Label(new Rect(r.x + sp, r.y, r.width, r.height), WhoTitleText, (int)MenuTitleSize, new Color(0.18f, 0.88f, 1f, 0.5f), TextAnchor.MiddleCenter, true);
                }
            }
        }

        /// How far below its place a carousel disk off the middle by `off` still is, and how visible, while booting.
        float WhoBootDiskRise(float off, out float alpha)
        {
            alpha = 1f;
            if (!WhoBootPlaying || !WhoBootFull)
                return 0f;
            float k = Mathf.Abs(Mathf.Round(off)), at = WbDiskAt + k * WbDiskStep + (off > 0.5f ? 0.03f : 0f);
            float u = Seg(whoBootT, at, at + WbDiskLen);
            alpha = Mathf.Clamp01(u * 3f);
            return (1f - EaseOutBack(u)) * 360f;
        }

        /// A glint sweeping across the middle disk (r) once it has landed.
        void DrawWhoBootGlint(Rect r)
        {
            if (!WhoBootPlaying || !WhoBootFull || whoBootT < WbGlintAt || whoBootT > WbGlintAt + WbGlintLen)
                return;
            // A soft slanted band of light, drawn as 2 px rows so it keeps to the disk's own outline (the chamfered top-right
            // corner included) with no rotation, and fades off at its edges instead of ending in a hard line.
            float u = Seg(whoBootT, WbGlintAt, WbGlintAt + WbGlintLen), a = Mathf.Sin(u * Mathf.PI);
            float k = r.width / 200f, ch = 24f * k, slant = 0.36f, x0 = Mathf.Lerp(-0.4f, 1.4f, u * u * (3f - 2f * u)) * r.width;
            for (float y = 0f; y < r.height; y += 2f)
            {
                float right = y < ch ? r.width - ch + y : r.width, cx = x0 + (r.height / 2f - y) * slant;
                for (int j = 0; j < 4; j++)
                {
                    float hw = (4f + j * 7f) * k, l = Mathf.Max(0f, cx - hw), rr = Mathf.Min(right, cx + hw);
                    if (rr > l)
                        Ui.Fill(new Rect(r.x + l, r.y + y, rr - l, 2f), new Color(1f, 1f, 1f, 0.08f * a));
                }
            }
        }

        /// The keys and buttons under the carousel rising in (delay: their place in the stagger): the y shift, and the alpha to draw at.
        float WhoBootUiRise(float delay, out float alpha)
        {
            float u = EaseOut3(WhoBootK(WbUiAt + delay, WbUiAt + delay + 0.32f));
            alpha = u;
            return (1f - u) * 18f;
        }

        /// Draws a piece under the carousel rising into place (delay: its place in the stagger), or just draws it when not booting.
        void WhoBootRise(float delay, System.Action draw)
        {
            float dy = WhoBootUiRise(delay, out float a);
            if (dy <= 0f && a >= 1f)
            {
                draw();
                return;
            }
            if (a <= 0.01f)
                return;
            GUI.BeginGroup(new Rect(0f, dy, W, H));
            WithAlphaS(a, draw);
            GUI.EndGroup();
        }

        /// A press that the boot keeps from the screen: any while it plays, and the one that skipped it.
        bool WhoBootEatsEvent(Event e)
        {
            bool input = e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag || e.type == EventType.KeyDown || e.type == EventType.ScrollWheel;
            return input && (WhoBootPlaying || Time.frameCount == whoBootSkipFrame);
        }

        /// Over everything: the fade from black (the full boot's first 0.6 s, or the short version's whole 0.45 s).
        void DrawWhoBootFade()
        {
            if (!WhoBootPlaying)
                return;
            float a = WhoBootFull ? 1f - EaseOut3(Seg(whoBootT, 0f, WbFadeLen)) : 1f - Seg(whoBootT, 0f, WhoBootLenFade - 0.05f);
            if (a > 0f)
                Ui.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, a));
        }
    }
}
