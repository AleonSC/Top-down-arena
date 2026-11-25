using UnityEngine;

namespace Arena
{
    /// The Storm Herald's poses: what its body does for each turn, worked out from the replicated turn and its clock
    /// (HeraldState.Turn/TurnT, the rods' Fall, TransT, CondId), so clients see the same with no extra network data.
    public enum HeraldPose : byte
    {
        Idle,       // staff out at its target; a jab with each volley in the breathers
        Call,       // Strikes, Strike rain, the last stand: the staff thrust up at the sky, a bolt answering each call
        Plant,      // rods falling: the staff driven into the floor, a crack ring
        Conduct,    // rods standing: arms wide, the staff held level, the spires live
        Slam,       // Thunderclap: the staff lifted, then slammed down as each ring goes out
        Sweep,      // Crosswind: the staff swept upwind, cloak streaming
        Gather,     // Ball lightning: both hands on the staff, an orb swelling at its head, then let go
        Downburst,  // the staff raised, then swung down the wind
        Front,      // Storm front: the staff held up, swinging slowly across
        Mark,       // the Supercell's Conductor: the staff pointed at the marked player
        Hunch,      // changing phase: wrapped in its cloak, crackling, rising at the end
    }

    /// The Storm Herald's body, the Tempest Herald: a hooded caller facing its target, a torn storm cloak behind it
    /// (streaming downwind in the Gale), two lightning-rod spires on its shoulders with an arc snapping between them,
    /// a staff with a crackling head, and two eyes glowing out of the cowl, posed for each turn (HeraldPose). Blue in the
    /// Gathering Storm and the Gale, dark violet in the Supercell, blazing in the last stand. Its storm glow and wind
    /// sock are DrawHeraldUnderlay (HeraldLook.cs).
    public partial class ArenaApp
    {
        static Rgba HeraldInk(int stage) => stage >= 2 ? new Rgba(0.13f, 0.09f, 0.22f) : new Rgba(0.16f, 0.2f, 0.3f);
        static Rgba HeraldLit(int stage) => stage >= 2 ? new Rgba(0.55f, 0.42f, 0.85f) : new Rgba(0.55f, 0.68f, 0.9f);
        static Rgba HeraldCore(int stage) => stage >= 3 ? new Rgba(0.95f, 0.75f, 1f) : stage == 2 ? new Rgba(0.82f, 0.7f, 1f) : StormWhite;
        static readonly Rgba HeraldIron = new Rgba(0.3f, 0.36f, 0.48f);
        static readonly Rgba HeraldStaff = new Rgba(0.42f, 0.46f, 0.55f);

        /// Seconds since the turn's last beat and until its next: beats at `first`, then every `period` seconds
        /// (the host's ActT/FireT clock in Herald.cs: 0.3 s into a turn, then the turn's own period).
        static void HeraldBeat(float turnT, float first, float period, out float since, out float until)
        {
            if (turnT < first)
            {
                since = 99f;
                until = first - turnT;
                return;
            }
            since = MathX.Fmod(turnT - first, period);
            until = period - since;
        }

        /// The pose for the Herald's current turn, with `k` its strength (0-1) and `flash` a beat just landed (0-1).
        public static HeraldPose HeraldPoseOf(HeraldState s, out float k, out float flash)
        {
            k = 1f;
            flash = 0f;
            if (s == null)
                return HeraldPose.Idle;
            if (s.TransT > 0f)
            {
                k = Mathf.Clamp01(s.TransT / 0.4f);  // 1 while hunched, to 0 as it rises in the last 0.4 s
                return HeraldPose.Hunch;
            }
            float since, until;
            switch (s.Turn)
            {
                case HeraldTurn.Strikes:
                case HeraldTurn.StrikeRain:
                case HeraldTurn.Eye:
                {
                    float period = s.Turn == HeraldTurn.Strikes ? 1.8f : s.Turn == HeraldTurn.StrikeRain ? 0.6f : 2f;
                    HeraldBeat(s.TurnT, 0.3f, period, out since, out until);
                    k = s.Turn == HeraldTurn.Strikes ? Mathf.Max(Mathf.Clamp01(1f - (until - 0.1f) / 0.45f), Mathf.Clamp01(1f - (since - 0.3f) / 0.4f)) : 1f;
                    flash = Mathf.Clamp01(1f - since / 0.25f);
                    return HeraldPose.Call;
                }
                case HeraldTurn.Rods:
                case HeraldTurn.RodCage:
                    foreach (var r in s.Rods)
                        if (r.Fall > 0f)
                        {
                            flash = Mathf.Clamp01((r.Fall - (Game.RodFall - 0.3f)) / 0.3f);  // the slam as the spots are marked
                            return HeraldPose.Plant;
                        }
                    if (s.Rods.Count == 0)
                        return HeraldPose.Idle;
                    foreach (var a in s.Arcs)
                        if (Game.ArcLiveNow(a))
                            flash = 1f;
                    return HeraldPose.Conduct;
                case HeraldTurn.Thunderclap:
                    HeraldBeat(s.TurnT, 0.3f, 1.5f, out since, out until);
                    k = Mathf.Clamp01(1f - until / 0.5f);  // lifting before the slam
                    flash = Mathf.Clamp01(1f - since / 0.35f);  // the slam
                    return HeraldPose.Slam;
                case HeraldTurn.Crosswind:
                    HeraldBeat(s.TurnT, 0.3f, 0.8f, out since, out until);
                    flash = Mathf.Clamp01(1f - since / 0.2f);
                    return HeraldPose.Sweep;
                case HeraldTurn.BallLightning:
                    HeraldBeat(s.TurnT, 0.3f, 3.5f, out since, out until);
                    k = since < 0.4f ? 0f : Mathf.Clamp01(1f - (until - 0.1f) / 1.6f);  // the orb swelling
                    flash = Mathf.Clamp01(1f - since / 0.4f);  // let go
                    return HeraldPose.Gather;
                case HeraldTurn.Downburst:
                    HeraldBeat(s.TurnT, 0.3f, 2f, out since, out until);
                    k = Mathf.Clamp01(since / 0.5f);  // 0 raised, 1 swung down the wind
                    if (since > 1.2f)
                        k = Mathf.Clamp01((until - 0.1f) / 0.7f);  // back up for the next
                    flash = Mathf.Clamp01(1f - since / 0.3f);
                    return HeraldPose.Downburst;
                case HeraldTurn.StormFront:
                    HeraldBeat(s.TurnT, 0.3f, 0.5f, out since, out until);
                    k = Mathf.Clamp01(s.TurnT / 7f);  // the swing across, over the turn
                    flash = Mathf.Clamp01(1f - since / 0.15f);
                    return HeraldPose.Front;
            }
            if (s.CondId != 0 && s.CondT > 0f)
            {
                k = 1f - Mathf.Clamp01(s.CondT / Game.ConductorTime);
                return HeraldPose.Mark;
            }
            HeraldBeat(s.TurnT, 0.3f, 1f, out since, out until);  // its volley in the breathers
            flash = Mathf.Clamp01(1f - since / 0.2f);
            return HeraldPose.Idle;
        }

        void DrawHeraldBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var s = e.Herald;
            int stage = s == null ? 0 : (int)s.Stage;
            float wind = s == null || s.TransT > 0f ? 0f : s.Wind;
            float hit = e.Flash > 0f || JuiceFlash(e) ? HitFlashMix(e) : 0f;  // the hit flash, as DrawEnemy mixes it
            Rgba ink = HeraldInk(stage).Mix(Rgba.White, hit), lit = HeraldLit(stage).Mix(Rgba.White, hit), core = HeraldCore(stage);
            int seed = (int)(t * 14f);
            float k, flash;
            var pose = HeraldPoseOf(s, out k, out flash);
            V2 fwd = V2.FromAngle(e.Facing), side = fwd.Orthogonal();

            // What the pose does to the body: the cloak's spread and length, the body's size, the eyes, the staff.
            float spread = 1f, cloakLen = 1f, scale = 1f, eyes = 1f;
            float staffAng = e.Facing, staffUp = 0f, staffLen = 1f;  // staffUp: 0 held level, 1 straight up (foreshortened)
            V2 handR = pos - side * r * 0.7f + fwd * r * 0.35f, handL = V2.Zero;  // handL: only when bothHands (its free hand stays under the cloak)
            bool bothHands = false, skyBolt = false, crack = false, orb = false;
            switch (pose)
            {
                case HeraldPose.Idle:
                    handR += fwd * 6f * flash;  // a jab with the volley
                    break;
                case HeraldPose.Call:
                    staffUp = k;
                    handR = V2.Lerp(handR, pos + fwd * r * 0.55f - side * r * 0.25f, k);
                    spread = 1f + 0.2f * k;
                    eyes = 1f + 0.5f * k;
                    skyBolt = flash > 0f && k > 0.5f;
                    break;
                case HeraldPose.Plant:
                    staffUp = 0.85f;
                    handR = pos + fwd * r * 0.95f;
                    handL = pos + fwd * r * 0.8f + side * r * 0.25f;
                    bothHands = true;
                    scale = 0.95f;
                    crack = true;
                    break;
                case HeraldPose.Conduct:
                    staffAng = e.Facing + MathX.Pi / 2f;  // held level across its chest
                    handR = pos + fwd * r * 0.55f - side * r * 0.75f;
                    handL = pos + fwd * r * 0.55f + side * r * 0.75f;
                    bothHands = true;
                    staffLen = 0f;  // (drawn hand to hand below)
                    spread = 1.25f;
                    eyes = 1.3f;
                    break;
                case HeraldPose.Slam:
                    staffUp = flash > 0f ? 0.85f : Mathf.Lerp(0f, 1f, k);
                    handR = flash > 0f ? pos + fwd * r * 0.95f : V2.Lerp(handR, pos + fwd * r * 0.4f, k);
                    handL = pos + fwd * r * 0.8f + side * r * 0.25f;
                    bothHands = flash > 0f;
                    spread = 1f + 0.35f * flash;
                    scale = 1f + 0.06f * k - 0.04f * flash;
                    crack = flash > 0f;
                    break;
                case HeraldPose.Sweep:
                    staffAng = wind > 0f ? MathX.Pi : 0f;  // upwind
                    handR = pos + V2.FromAngle(staffAng) * r * 0.7f + fwd * r * 0.2f;
                    handR += V2.FromAngle(staffAng) * 6f * flash;
                    cloakLen = 1.15f;
                    break;
                case HeraldPose.Gather:
                    handR = pos + fwd * r * 0.75f - side * r * 0.3f;
                    handL = pos + fwd * r * 0.75f + side * r * 0.3f;
                    bothHands = true;
                    staffLen = 0.75f;
                    orb = true;
                    eyes = 1f + 0.4f * k;
                    break;
                case HeraldPose.Downburst:
                {
                    float down = wind >= 0f ? 0f : MathX.Pi;  // the wind's way
                    staffUp = 1f - k;
                    staffAng = e.Facing + (MathX.Fmod(down - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) * k;  // the short way round
                    handR = V2.Lerp(pos + fwd * r * 0.55f - side * r * 0.25f, pos + V2.FromAngle(down) * r * 0.75f, k);
                    skyBolt = flash > 0f;
                    spread = 1.1f;
                    break;
                }
                case HeraldPose.Front:
                    staffUp = 0.6f;
                    staffAng = e.Facing + Mathf.Lerp(-1f, 1f, k);
                    handR = pos + V2.FromAngle(staffAng) * r * 0.6f;
                    skyBolt = flash > 0f;
                    break;
                case HeraldPose.Mark:
                {
                    Player c;
                    if (s != null && game.Players.TryGetValue(s.CondId, out c))
                        staffAng = (c.Disp - pos).Angle;
                    handR = pos + V2.FromAngle(staffAng) * r * 0.7f;
                    staffLen = 1.15f;
                    eyes = 1.2f + 0.3f * k;
                    break;
                }
                case HeraldPose.Hunch:
                    spread = Mathf.Lerp(1.2f, 0.65f, k);
                    cloakLen = Mathf.Lerp(1.1f, 0.85f, k);
                    scale = Mathf.Lerp(1.05f, 0.88f, k);
                    eyes = Mathf.Lerp(1.8f, 0.4f, k);
                    staffUp = 1f - k;
                    handR = V2.Lerp(pos + fwd * r * 0.55f - side * r * 0.25f, pos + fwd * r * 0.3f - side * r * 0.45f, k);
                    break;
            }
            r *= scale;
            V2 back = (-fwd + new V2(wind * 0.9f, 0f)).Normalized(), bside = back.Orthogonal();

            // The cloak: a full fan from the shoulders back, its hem torn into points that flutter; longer each phase.
            const int pts = 7;
            V2 prevTip = V2.Zero;
            for (int i = 0; i < pts; i++)
            {
                float u0 = -1f + 2f * i / pts, u1 = -1f + 2f * (i + 1) / pts, um = (u0 + u1) / 2f;
                float len = (r * (1.2f + 0.18f * stage) * (1f - 0.22f * um * um) + 5f * Mathf.Sin(t * 7f + i * 1.3f) + Mathf.Abs(wind) * 14f) * cloakLen;
                float w = r * 1.08f * spread;
                V2 h0 = pos + bside * u0 * w + back * len * 0.8f;
                V2 h1 = pos + bside * u1 * w + back * len * 0.8f;
                V2 tip = pos + bside * um * w * 1.04f + back * len;
                V2 r0 = pos + bside * u0 * r * 0.7f, r1 = pos + bside * u1 * r * 0.7f;
                Rgba fold = ink.Mix(lit, i % 2 == 0 ? 0.12f : 0.22f);  // alternate folds catch the light
                VTri(r0, ink, h0, fold, h1, fold);
                VTri(r0, ink, h1, fold, r1, ink);
                VTri(h0, fold, tip, fold.WithAlpha(0.55f), h1, fold);
                if (i > 0)
                    Line(prevTip, h0, 1.5f, lit.WithAlpha(0.3f));  // the hem's lit edge
                Line(h0, tip, 1.5f, lit.WithAlpha(0.3f));
                prevTip = tip;
                if ((stage >= 2 || pose == HeraldPose.Hunch) && HHash(seed + i * 5) > 0.75f)  // charge crawling off the hem
                    DrawBolt(tip, tip + back * (10f + 8f * stage) + bside * (HHash(seed + i) - 0.5f) * 20f, 1.2f, core, seed + i, 2);
            }

            // Mantle and pauldrons, lit on the side facing its target.
            V2 shoulderL = pos + side * r * 0.62f, shoulderR = pos - side * r * 0.62f;
            Circle(pos + new V2(0f, 5f), r * 0.85f, new Rgba(0f, 0f, 0f, 0.35f), 22);
            Circle(pos, r * 0.8f, ink.Mix(lit, 0.2f), 24);
            Circle(shoulderL, r * 0.32f, ink.Mix(lit, 0.35f), 14);
            Circle(shoulderR, r * 0.32f, ink.Mix(lit, 0.35f), 14);
            Arc(shoulderL, r * 0.32f, 1.5f, lit.WithAlpha(0.7f), 10, e.Facing - 1.2f, 2.4f);
            Arc(shoulderR, r * 0.32f, 1.5f, lit.WithAlpha(0.7f), 10, e.Facing - 1.2f, 2.4f);
            Arc(pos, r * 0.8f, 2f, lit.WithAlpha(0.8f), 24, e.Facing - 1.6f, 3.2f);

            // Two rod spires rising off the shoulders, an arc snapping between their tips (always on from the
            // Supercell, and while it conducts or hunches).
            V2 tipL = shoulderL + back * r * 0.15f + side * r * 0.12f, tipR = shoulderR + back * r * 0.15f - side * r * 0.12f;
            DrawHeraldSpire(tipL, back, r, t, core);
            DrawHeraldSpire(tipR, back, r, t, core);
            if (Mathf.Sin(t * 5f) > -0.3f || stage >= 2 || pose == HeraldPose.Conduct || pose == HeraldPose.Hunch)
                DrawBolt(tipL, tipR, 2f, core, seed, 5);

            // The hood: a dark cowl, two eyes glowing out of it (blazing in the last stand); under the staff,
            // which is nearer the camera.
            V2 hood = pos + fwd * r * 0.15f;
            Circle(hood, r * 0.5f, ink.Mix(lit, 0.45f), 20);
            Arc(hood, r * 0.5f, 1.5f, lit.WithAlpha(0.6f), 16, e.Facing - 1.3f, 2.6f);
            Circle(hood + fwd * r * 0.06f, r * 0.38f, new Rgba(0.04f, 0.04f, 0.08f), 18);
            for (int i = -1; i <= 1; i += 2)
            {
                V2 ep = hood + fwd * r * 0.2f + side * i * r * 0.13f;
                GlowDisc(ep, (10f + 3f * stage) * eyes, core, Mathf.Min(1f, 0.7f * eyes));
                Circle(ep, (2.6f + (stage >= 3 ? 0.8f : 0f)) * Mathf.Max(0.6f, Mathf.Min(1.4f, eyes)), Rgba.White, 8);
            }

            // Sleeves out to its hands.
            Rgba sleeve = ink.Mix(lit, 0.5f);
            Line(shoulderR, handR, 9f * scale, sleeve);
            Circle(handR, 5f, ink.Mix(lit, 0.45f), 10);
            if (bothHands)
            {
                Line(shoulderL, handL, 9f * scale, sleeve);
                Circle(handL, 5f, ink.Mix(lit, 0.45f), 10);
            }

            // The staff, then its head: level, raised (foreshortened, its head bigger and nearer the camera) or planted.
            V2 head;
            if (pose == HeraldPose.Conduct)
            {
                V2 a = handR - V2.FromAngle(staffAng) * r * 0.2f, b = handL + V2.FromAngle(staffAng) * r * 0.2f;
                Line(a, b, 4f, HeraldStaff.Mix(Rgba.White, hit));
                head = a;
                DrawBolt(a, tipR, 1.5f, core, seed + 3, 3);
                DrawBolt(b, tipL, 1.5f, core, seed + 4, 3);
                GlowDisc(b, 18f, core, 0.4f + 0.4f * flash);
            }
            else
            {
                V2 dir = V2.FromAngle(staffAng);
                float reach = r * 0.95f * staffLen * (1f - 0.75f * staffUp);
                head = handR + dir * reach;
                Line(handR - dir * r * 0.5f * (1f - staffUp), head, 4f + 2f * staffUp, HeraldStaff.Mix(Rgba.White, hit));
            }
            float big = 1f + 0.6f * staffUp;
            if (crack)  // the floor cracking round the planted staff
            {
                Arc(head, 14f + 22f * (1f - flash), 2f, core.WithAlpha(0.3f + 0.5f * flash), 16);
                for (int i = 0; i < 5; i++)
                    DrawBolt(head, head + V2.FromAngle(i * 1.26f + 0.4f) * (18f + 10f * flash), 1.2f, core.WithAlpha(0.7f), seed + i * 3, 2);
            }
            if (orb)  // the ball lightning swelling at the staff's head, gone in a flash when it lets go
            {
                float or = 4f + 14f * k;
                GlowDisc(head, or * 2.6f, core, 0.4f + 0.4f * k);
                Circle(head, or, core.WithAlpha(0.8f), 16);
                Circle(head, or * 0.5f, Rgba.White, 12);
                if (flash > 0f)
                    Arc(head, 20f + 40f * (1f - flash), 3f, core.WithAlpha(flash), 20);
            }
            GlowDisc(head, (22f + 6f * stage) * big * (1f + 0.5f * flash), core, 0.5f + 0.25f * Mathf.Sin(t * 9f) + 0.3f * flash);
            Polygon(head, 7f * big, 4, t * 2f, lit);
            Circle(head, 3f * big, Rgba.White, 8);
            for (int i = 0; i < 2 + stage + (flash > 0f ? 2 : 0); i++)
                DrawBolt(head, head + V2.FromAngle(t * 4f + i * 2.3f) * (14f + 4f * stage) * big, 1.5f, core, seed + i * 5, 3);
            if (skyBolt)  // the sky answering: a bolt straight up off the screen's top from the staff
            {
                V2 sky = head + new V2(Mathf.Sin(seed) * 30f, -420f);
                DrawBolt(sky, head, 1.5f + 2f * flash, StormWhite, seed + 17, 9);
                GlowDisc(head, 44f * flash, StormWhite, 0.45f * flash);
            }

        }

        /// A lightning-rod spire on the Herald's shoulder: an iron rod with a glowing tip.
        void DrawHeraldSpire(V2 tip, V2 back, float r, float t, Rgba core)
        {
            Line(tip - back * r * 0.25f, tip, 5f, HeraldIron);
            GlowDisc(tip, 16f, core, 0.6f);
            Circle(tip, 4f + 1f * Mathf.Sin(t * 14f + tip.X), core, 10);
        }

        static float HHash(int a) => Mathf.Abs(Mathf.Sin(a * 12.9898f + 4.1f) * 43758.5453f) % 1f;
    }
}
