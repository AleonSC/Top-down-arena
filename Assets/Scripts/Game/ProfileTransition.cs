using UnityEngine;

namespace Arena
{
    /// The disk boot sequence (Docs/Mockups/disk-boot-sequence.html): from Who's playing? to the main menu, 4.8 s. Picking a profile plays
    /// it; the menu's eject key has the way back (DiskEject.cs). Its clock `profTrT` (also the menu's machine clock, MachT) runs in seconds:
    ///   0.00 s  Pick: the middle disk lifts; the rest of the carousel, its keys and its count fade away (0.15 to 0.5 s)
    ///   0.35 s  Carry: the disk rises toward the drive in the monitor's chin, shrinking to the slit's width, with a small wobble as it lines up
    ///   1.05 s  Insert: it tips back and slides up in through the slit, the slit's lower lip over it; the slit flashes as it seats (1.72 s)
    ///   1.80 s  Read: DISK DETECTED, then READING <name>… with the light blinking amber and the cells filling; WHO'S PLAYING? backspaces away
    ///   2.60 s  Retype: VECTOR COLOSSEUM types on; the drive says <name> READY in green (2.85 s)
    ///   2.90 s  Drop: the menu's case falls out from under the monitor, lands with a squash, a bounce, a neon flare, the monitor jolting
    ///           and dust off its feet (3.35 s; MenuMachine.cs)
    ///   3.75 s  Power on: POWER, DISK, the key lamps one by one, the profile screen switching on (to 4.45 s); the shutter rattles (4.4 s)
    /// Nothing can be clicked meanwhile (the menu counts as still in its intro), and any key fast-forwards the rest of it, silently, in
    /// 0.5 s (the other intro skips use the channel flip, SkipStatic.cs). Reduced effects or Visual effects off: no move, the screens
    /// just change (and the menu's short fade plays).
    public partial class ArenaApp
    {
        const float ProfTrLen = MachEnd;

        bool profTr;               // the move is playing
        float profTrT, profTrPrev, profTrAge;
        float profTrFf;            // skipped: the clock's speed while it fast-forwards to the end (0: playing normally)
        const float ProfTrFfLen = 0.5f;  // a skip plays whatever is left in this long
        int profTrSlot;            // the profile whose disk goes in (or comes out): its place in whoList

        bool ProfTrFull => fxOn && !fxReduced;

        /// A profile was picked (ChooseProfile): play the boot sequence, or just show the menu.
        void StartProfTrIn(string id)
        {
            whoOpen = false;
            WhoRefresh();
            int slot = whoList.FindIndex(p => p.Id == id);
            if (!ProfTrFull || slot < 0)
                return;
            whoManage = false;
            profTr = true;
            profTrT = 0f;
            profTrPrev = -0.0001f;
            profTrAge = 0f;
            profTrFf = 0f;
            profTrSlot = slot;
            whoCarPos = whoCarTarget = slot;  // the picked disk is the one in the middle
            menuIntroDone = true;  // this is the menu's intro now
            menuIntroT = 0f;
            menuPlayOpen = menuJoinOpen = menuSetOpen = menuWikiOpen = false;
            menuPlayK = 0f;
        }

        /// Update (from TickMenuIntro): run the clock and its sounds, and hand the menu its place in its intro.
        void TickProfTr(float dt)
        {
            if (profEj)
            {
                TickProfEj(dt);  // the eject key's move (DiskEject.cs)
                return;
            }
            profTrAge += dt;
            if (profTrFf <= 0f && Input.anyKeyDown && profTrAge > 0.25f)
                profTrFf = Mathf.Max(1f, (ProfTrLen - profTrT) / ProfTrFfLen);  // skipped: fast-forward the rest
            if (profTrFf > 0f)
                profTrT = Mathf.Min(ProfTrLen, profTrT + dt * profTrFf);  // silently
            else
            {
                profTrT = Mathf.Min(ProfTrLen, profTrT + dt);
                PlayProfTrCues(profTrPrev, profTrT);
                PlayMachineCues(profTrPrev, profTrT);
            }
            profTrPrev = profTrT;
            menuIntroT = Mathf.Clamp(profTrT - 2.6f, 0f, MenuIntroLen - 0.001f);
            if (profTrT >= ProfTrLen)
            {
                profTr = false;
                menuIntroT = MenuIntroLen;
            }
        }

        static readonly Sfx[] ProfCueSfx = { Sfx.ProfSelect, Sfx.DiskFly, Sfx.DiskTip, Sfx.DiskSlide, Sfx.DiskClick, Sfx.DiskRead, Sfx.DiskReady };
        static readonly float[] ProfCueAt = { 0f, 0.35f, 1.05f, 1.3f, 1.75f, 2.0f, 2.85f };
        static readonly float[] ProfCueVol = { 0.7f, 0.6f, 0.7f, 0.7f, 0.8f, 0.6f, 0.6f };

        /// Plays each cue the clock passed between a and b, and the title's keys as WHO'S PLAYING? is erased (the menu's title then
        /// types with its own sounds, TickTitleTyping).
        void PlayProfTrCues(float a, float b)
        {
            if (b <= a || sounds == null)
                return;
            float now = Time.unscaledTime;
            TitleFlapCues(WhoTitleText, a, b, 1.95f, 2.45f, false);
            for (int i = 0; i < ProfCueAt.Length; i++)
                if (a < ProfCueAt[i] && ProfCueAt[i] <= b)
                    sounds.Play(ProfCueSfx[i], ProfCueVol[i], now);
        }

        /// The whole screen during the move: the menu at its place in the sequence (the monitor, then the case), with the carousel
        /// fading and the picked disk on its way into the drive.
        void DrawProfTr()
        {
            if (profEj)
            {
                DrawProfEj();  // DiskEject.cs
                return;
            }
            DrawMenu();
            DrawProfTrWho(profTrT);
        }

        readonly System.Collections.Generic.List<int> whoOrder = new System.Collections.Generic.List<int>();
        readonly CarFarFirst whoFarFirst = new CarFarFirst();
        /// DrawWhoStill's draw order: the disks farthest from the middle first.
        sealed class CarFarFirst : System.Collections.Generic.IComparer<int>
        {
            public ArenaApp App;
            public int Compare(int x, int y) => Mathf.Abs(App.CarOffset(y)).CompareTo(Mathf.Abs(App.CarOffset(x)));
        }

        /// The carousel without its middle disk, and its keys and count, drawn only (for the moves).
        void DrawWhoStill(float a)
        {
            if (a <= 0f)
                return;
            int n = CarCount, mid = CarMiddle;
            var order = whoOrder;  // (its own list: the disks' drawing may use DrawScratch)
            order.Clear();
            for (int i = 0; i < n; i++)
                if (i != mid && Mathf.Abs(CarOffset(i)) < 3.2f)
                    order.Add(i);
            whoFarFirst.App = this;
            order.Sort(whoFarFirst);
            foreach (int i in order)
            {
                var r = CarRect(CarOffset(i), out float da);
                if (da > 0.01f)
                    WithAlpha(a * da, () => DrawCarDisk(r, i));
            }
            WithAlpha(a, () =>
            {
                RoundKey(CarLeftKey, "‹", false);
                RoundKey(CarRightKey, "›", false);
                DrawCarCount();
                MachKey(WhoManageKey, "MANAGE PROFILES", 15, false, -1, PixelArt.Get("ui_gear"));
                MachKey(WhoNewKey, "+  NEW PROFILE", 15, false, -1, null, false, WhoFull ? 0.45f : 1f);
            });
        }

        /// Where the picked disk is at clock T going in: its top edge's centre, its size, and how far it has tipped back (0 to 62 degrees).
        static void DiskPathIn(float T, out float cx, out float top, out float size, out float tilt, out float wob)
        {
            float pick = Seg(T, 0f, 0.35f), carry = EaseInOut(Seg(T, 0.35f, 1.05f)), ins = Seg(T, 1.05f, 1.8f);
            float s = Mathf.Lerp(1f, 0.77f, carry) + 0.03f * Mathf.Sin(Mathf.PI * pick) * (1f - carry);
            size = CarSize * s;
            cx = Mathf.Lerp(CarCx, DriveSlitCx, carry);
            top = Mathf.Lerp(CarCy - CarSize / 2f - 14f * EaseOut(pick), 232f, carry);
            tilt = 0f;
            float w = Seg(T, 0.95f, 1.2f);
            wob = Mathf.Sin(w * Mathf.PI * 2f) * 2f * (1f - w);
            if (ins > 0f)
            {
                top = Mathf.Lerp(232f, DriveY - size * 0.6f, ins * ins * ins);
                tilt = Mathf.Lerp(0f, 62f, EaseOut(ins));
            }
        }

        /// The picker's side of the move, over the menu: the carousel fading, and the picked disk lifting, carried up, and slid into the slit.
        void DrawProfTrWho(float T)
        {
            DrawWhoStill(1f - Seg(T, 0.15f, 0.5f));
            var info = WhoAt(profTrSlot);
            if (info == null || T >= 1.8f)
                return;
            DiskPathIn(T, out float cx, out float top, out float size, out float tilt, out float wob);
            DrawTiltedDisk(info, cx, top, size, tilt, wob);
            if (T >= 1.05f)  // going in: the chin above the slit hides what's already in
                DrawMonitorAbove(DriveY - 4f, MenuTitleAt(T), DriveForMenu());
            DrawDriveLip(1f, DriveForMenu());
        }

        /// A profile's disk with its top edge centred at (cx, top), `size` across, tipped back by `tilt` degrees about its top edge (it
        /// foreshortens, and its far end widens a little) and swayed sideways by `wob` (the wobble as it lines up with the slit).
        void DrawTiltedDisk(ProfileInfo info, float cx, float top, float size, float tilt, float wob)
        {
            float rad = tilt * Mathf.Deg2Rad;
            float w = size * (1f + 0.12f * Mathf.Sin(rad)), h = size * Mathf.Max(0.02f, Mathf.Cos(rad));
            float x0 = cx - w / 2f + wob * 3f;
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + x0 * scale, offY + top * scale, 0f), Quaternion.identity, new Vector3(scale * w / 200f, scale * h / 200f, 1f));
            DrawWhoDisk(new Rect(0f, 0f, 200f, 200f), info, info.Id == ProfileStore.ActiveId);
            GUI.matrix = old;
        }
    }
}
