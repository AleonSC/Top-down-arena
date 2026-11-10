using System;
using System.Collections.Generic;

namespace Arena
{
    /// The boss wave's beam-in (Docs/Mockups/boss-teleport.html, option A). When the boss is next and the rooms are up,
    /// the wave doesn't start until everyone outside the hub has been beamed into it: a pillar of light takes them up from
    /// where they stand, they are moved at BeamMoveAt and a second pillar sets them down in the hub. Nobody outside
    /// the hub is left behind when the rooms come down (PullIntoHub stays as the fallback for anything that gets past this).
    ///   0.0 s    the call: a landing ring in the hub for each, the pillar comes down on them, they can't move or shoot
    ///   0.8 s    they go up it with a ring and a flash
    ///   1.0 s    the host moves them (Msg.Teleport); every screen cuts to the hub
    ///   1.1 s    a pillar comes down on the landing ring, 1.35 s: they re-form, 1.9 s: the wave starts
    /// The look is Game/BossBeamLook.cs (FxKind.BossBeam: Pos = where they stood, Pos2 = where they land, Radius = their id).
    public partial class Game
    {
        public const float BeamMoveAt = 1.0f, BeamTotal = 1.9f;

        sealed class BeamJob { public Player P; public V2 To; }

        readonly List<BeamJob> beams = new List<BeamJob>();
        float beamClock = -1f;  // host: seconds since the beam-in began; below 0 when there's none

        /// Host, as the intermission runs out: true while the boss wave is held for the beam-in (Waves.cs).
        bool BossBeamHold(float dt)
        {
            if (beamClock < 0f)
            {
                if (!BossStartsNext || !RoomsOn)
                    return false;
                var hub = Rooms.Areas[Rooms.Hub];
                scratchPts.Clear();
                foreach (var p in Players.Values)
                {
                    if (hub.Contains(p.Pos, -1f))
                        continue;
                    V2 at = p.Pos.Clamp(hub.Min + new V2(110f, 110f), hub.Max - new V2(110f, 110f));
                    for (int tries = 0; tries < 6; tries++)  // (not on top of another's landing ring)
                    {
                        bool near = false;
                        foreach (var q in scratchPts)
                            near |= q.DistanceTo(at) < 60f;
                        if (!near)
                            break;
                        at += (hub.Min + (hub.Max - hub.Min) * 0.5f - at).Normalized() * 60f;
                    }
                    at = PushOutOfProps(at, p.Def.BodyR);
                    scratchPts.Add(at);
                    beams.Add(new BeamJob { P = p, To = at });
                }
                if (beams.Count == 0)
                    return false;
                beamClock = 0f;
                foreach (var j in beams)
                {
                    j.P.WarpT = BeamTotal;
                    BroadcastFx(FxKind.BossBeam, j.P.Pos, j.To, j.P.Def.Color, j.P.Id, BeamTotal, Sfx.None);
                }
                BroadcastSound(Sfx.BossWarp);
                DoLog("boss beam: " + beams.Count + " outside the hub");
                return true;
            }
            float before = beamClock;
            beamClock += dt;
            if (before < BeamMoveAt && beamClock >= BeamMoveAt)
            {
                foreach (var j in beams)
                    MovePlayer(j.P, j.To);
                BroadcastSound(Sfx.Blink);
            }
            if (beamClock < BeamTotal)
                return true;
            foreach (var j in beams)
                j.P.WarpT = 0f;
            beams.Clear();
            beamClock = -1f;
            return false;
        }

        readonly List<V2> scratchPts = new List<V2>();
    }
}
