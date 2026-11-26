using UnityEngine;

namespace Arena
{
    /// A short directional blur of the world (Visual effects on; off with Screen shake off or Reduced effects): the frame's
    /// triangles from the arena's contents up (recorded as they are drawn, by the hit-pause recorder in Juice.cs), drawn again as two faint copies pushed either
    /// way along a direction, fading out. No post-processing: it is the GL picture itself, smeared. Used by your own Twin Rifle
    /// grenade launch (TwinNadeLook.cs `NadeLaunchFeel`).
    public partial class ArenaApp
    {
        float blurT, blurMax, blurPx;  // time left, its full length, the copies' offset at the start (arena units)
        V2 blurDir;
        bool blurRec;                  // recording this frame for the blur only (the hit-pause recorder isn't)
        int blurFrom;                  // the first recorded vertex it copies: the arena's contents, not the floor and backdrop under them

        /// Smear the picture `px` arena units each way along `dir`, fading over `time` seconds.
        void ScreenBlur(V2 dir, float px, float time)
        {
            if (fxShake == 0 || fxReduced || !Juicy)
                return;
            blurT = blurMax = time;
            blurDir = dir.IsZero ? new V2(1f, 0f) : dir.Normalized();
            blurPx = px;
        }

        /// Once a frame (real time).
        void BlurStep(float realDt) => blurT = Mathf.Max(0f, blurT - realDt);

        /// BeginJuiceFrame: record this frame's triangles if a blur is running (unless the hit-pause recorder already is).
        void BeginBlurFrame()
        {
            blurRec = blurT > 0f && !recording && !fpView;
            blurFrom = 0;
            if (blurRec)
            {
                recVerts.Clear(); recCols.Clear(); recGlowFrom.Clear(); recGlowTo.Clear();
            }
        }

        /// DrawWorldFrame, after the floor: the copies start here (a dimmed backdrop drawn again would darken the picture).
        void BlurFromHere() => blurFrom = recVerts.Count;

        /// After the world (before the screen overlays): the two faint copies of what was drawn so far.
        void DrawBlurCopies()
        {
            if (blurT <= 0f || !(blurRec || recording) || fpView || blurMax <= 0f)
                return;
            float k = blurT / blurMax, a = 0.32f * k;
            float px = blurPx * k * drawScale;
            int n = recVerts.Count;
            for (int side = -1; side <= 1; side += 2)
            {
                var off = new Vector3(blurDir.X * px * side, -blurDir.Y * px * side, 0f);  // (GL's y runs up the screen)
                int glow = 0;
                while (glow < recGlowTo.Count && recGlowTo[glow] <= blurFrom)
                    glow++;  // (glow passes before the start: none in practice)
                bool inGlow = false;
                for (int i = blurFrom; i < n; i++)
                {
                    if (glow < recGlowFrom.Count && i == recGlowFrom[glow])
                    {
                        SwitchToGlow(true);
                        inGlow = true;
                    }
                    if (glow < recGlowTo.Count && i == recGlowTo[glow])
                    {
                        SwitchToGlow(false);
                        inGlow = false;
                        glow++;
                    }
                    var c = recCols[i];
                    c.a *= a;
                    GL.Color(c);
                    GL.Vertex(recVerts[i] + off);
                }
                if (inGlow)
                    SwitchToGlow(false);
            }
            blurRec = false;
        }
    }
}
