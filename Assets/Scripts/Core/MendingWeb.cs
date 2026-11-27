using System;

namespace Arena
{
    /// A point on a wall a Mending Web is tied to: where, the wall face's outward normal, and which wall (-1..-4 the arena's
    /// edges, otherwise the index in ActiveLayout.Solids), so both ends can't go on the same wall.
    public struct WebAnchor
    {
        public V2 At, Normal;
        public int Wall;
    }

    /// Mending Web, the Healer Summoner's 1st choice (`AbilityId.MendingWeb`, took Spirit Swarm's place; mockup claude.ai
    /// artifact "Weaver Web Ability"). Pressing it starts a pick (`WebPickSlot`, like Octopus Familiar): click a point on a wall
    /// (within WebSnapR of a wall face, WebReach of you), then a point on another wall. The two must be WebMinLen..WebMaxLen apart
    /// (Silk Choir adds Stats.WebLenBonus) with an open line between them (`WebPair`). The key again cancels; nothing is spent
    /// until the second click. The first anchor rides in Msg.Ability's cast-from (`StepFrom`), the second is the target; the
    /// host checks the pair again and, failing that (and for bots and the lobby preview), finds one round the target itself
    /// (`WebAuto`), or strings a free web WebFreeLen across the caster's aim there.
    /// A spirit spider runs from the first anchor to the second laying a line (WebLaySpeed px/s), then weaves back (WebWeaveT s);
    /// from then on the web (`ZoneKind.MendingWeb`: Pos its middle, Aim the way from the first anchor, Radius half its length,
    /// all replicated, so it costs nothing on the wire) is a band WebBand px either side of the line for WebLife s:
    ///   allies in it heal WebHeal HP/s; enemies in it are slowed WebSlow (heavies half, bosses not at all) and poisoned
    ///   (Enemy.PoisonT/PoisonDps, the Senbon Seekers' poison: WebPoisonDps a second, for WebPoisonT s after they leave).
    /// Up to WebMax webs per caster (a new one unravels the oldest). Mastered: it heals and poisons WebMasteredMult times as
    /// much. Its Rare card Silk Choir adds Stats.WebLenBonus and WebLifeBonus. The look: Game/MendingWebLook.cs.
    public partial class Game
    {
        public const float WebCooldown = 8f, WebLife = 10f, WebBand = 25f, WebSnapR = 44f, WebReach = 560f;
        public const float WebMinLen = 60f, WebMaxLen = 420f, WebFreeLen = 200f;
        public const float WebLaySpeed = 320f, WebWeaveT = 0.9f;
        public const float WebHeal = 7f, WebSlow = 0.45f, WebPoisonDps = 5f, WebPoisonT = 3f, WebMasteredMult = 1.4f;
        public const float WebChoirLen = 140f, WebChoirLife = 3f;  // Silk Choir, a stack
        public const int WebCharges = 2, WebMax = 3;
        public static readonly Rgba WebColor = new Rgba(0.87f, 0.91f, 1f);    // the silk
        public static readonly Rgba WebGreen = new Rgba(0.35f, 0.95f, 0.5f);  // the healing dew
        public static readonly Rgba WebVenom = new Rgba(0.72f, 0.49f, 1f);    // the spider, the poison

        /// How long a web of half length `half` takes to build: the line across, then the weave back.
        public static float WebBuildT(float half) => 2f * half / WebLaySpeed + WebWeaveT;

        /// A web's two ends (A where the spider started).
        public static void WebEnds(Zone z, out V2 a, out V2 b)
        {
            V2 u = V2.FromAngle(z.Aim) * z.Radius;
            a = z.Pos - u;
            b = z.Pos + u;
        }

        /// How far `p` is from a web's line.
        public static float WebDist(Zone z, V2 p)
        {
            V2 u = V2.FromAngle(z.Aim);
            float along = Math.Max(-z.Radius, Math.Min(z.Radius, (p - z.Pos).Dot(u)));
            return p.DistanceTo(z.Pos + u * along);
        }

        /// Is a web built (the spider done weaving)?
        public static bool WebReady(Zone z) => z.Duration - z.T >= WebBuildT(z.Radius);

        public float WebMaxLenFor(Player p) => WebMaxLen + p.Stats.WebLenBonus;

        /// The wall face nearest `p` within `reach`: the arena's edges and the layout's walls and pillars.
        public bool WebSnap(V2 p, float reach, out WebAnchor a)
        {
            a = default(WebAnchor);
            float best = reach;
            bool found = false;
            V2 lo = ArenaMin, hi = ArenaMax;
            if (p.X >= lo.X && p.X <= hi.X && p.Y >= lo.Y && p.Y <= hi.Y)
            {
                float cx = Math.Max(lo.X, Math.Min(hi.X, p.X)), cy = Math.Max(lo.Y, Math.Min(hi.Y, p.Y));
                TrySnap(p.Y - lo.Y, new V2(cx, lo.Y), new V2(0f, 1f), -1, ref best, ref a, ref found);
                TrySnap(hi.Y - p.Y, new V2(cx, hi.Y), new V2(0f, -1f), -2, ref best, ref a, ref found);
                TrySnap(p.X - lo.X, new V2(lo.X, cy), new V2(1f, 0f), -3, ref best, ref a, ref found);
                TrySnap(hi.X - p.X, new V2(hi.X, cy), new V2(-1f, 0f), -4, ref best, ref a, ref found);
            }
            var d = ActiveLayout;
            if (d != null)
                for (int i = 0; i < d.Solids.Length; i++)
                {
                    var s = d.Solids[i];
                    if (s.Edge || !Counts(s))
                        continue;  // (the edge wall is the arena's edge, done above)
                    if (s.Round)
                    {
                        V2 off = p - s.Pos;
                        float len = off.Length;
                        V2 n = len > 0.01f ? off / len : new V2(0f, -1f);
                        TrySnap(Math.Abs(len - s.R), s.Pos + n * s.R, n, i, ref best, ref a, ref found);
                        continue;
                    }
                    float x0 = s.Pos.X - s.HW, x1 = s.Pos.X + s.HW, y0 = s.Pos.Y - s.HH, y1 = s.Pos.Y + s.HH;
                    bool inside = p.X > x0 && p.X < x1 && p.Y > y0 && p.Y < y1;
                    if (inside)
                    {
                        float l = p.X - x0, r = x1 - p.X, t = p.Y - y0, b = y1 - p.Y, m = Math.Min(Math.Min(l, r), Math.Min(t, b));
                        if (m == l) TrySnap(0f, new V2(x0, p.Y), new V2(-1f, 0f), i, ref best, ref a, ref found);
                        else if (m == r) TrySnap(0f, new V2(x1, p.Y), new V2(1f, 0f), i, ref best, ref a, ref found);
                        else if (m == t) TrySnap(0f, new V2(p.X, y0), new V2(0f, -1f), i, ref best, ref a, ref found);
                        else TrySnap(0f, new V2(p.X, y1), new V2(0f, 1f), i, ref best, ref a, ref found);
                        continue;
                    }
                    V2 q = new V2(Math.Max(x0, Math.Min(x1, p.X)), Math.Max(y0, Math.Min(y1, p.Y)));
                    V2 nn = q.X == x0 && p.X < x0 ? new V2(-1f, 0f) : q.X == x1 && p.X > x1 ? new V2(1f, 0f) : q.Y == y0 && p.Y < y0 ? new V2(0f, -1f) : new V2(0f, 1f);
                    TrySnap(p.DistanceTo(q), q, nn, i, ref best, ref a, ref found);
                }
            return found;
        }

        static void TrySnap(float dist, V2 at, V2 n, int wall, ref float best, ref WebAnchor a, ref bool found)
        {
            if (dist > best)
                return;
            best = dist;
            a = new WebAnchor { At = at, Normal = n, Wall = wall };
            found = true;
        }

        /// Can a web join these two anchors? Returns null if so, else why not (for the pick's hint).
        public string WebPair(WebAnchor a, WebAnchor b, float maxLen)
        {
            if (a.Wall == b.Wall)
                return "another wall";
            V2 ab = b.At - a.At;
            float len = ab.Length;
            if (len > maxLen)
                return "too far";
            if (len < WebMinLen)
                return "too close";
            V2 u = ab / len;
            if (u.Dot(a.Normal) < 0.1f || -u.Dot(b.Normal) < 0.1f)
                return "no line";  // (an end on the far side of its wall)
            RailCast(a.At + a.Normal * 1.5f, u, len + 8f, out float hit, out _);  // the first wall along the line (RailBeam.cs)
            return hit < len - 6f ? "no line" : null;
        }

        /// A pair round `around` for when there was no pick (bots, the lobby): the shortest line through it, wall to wall,
        /// that fits in `maxLen`, preferring one across `aim` (unit; zero: any).
        public bool WebAuto(V2 around, V2 aim, float maxLen, out V2 a, out V2 b)
        {
            a = b = around;
            float bestScore = float.MaxValue;
            bool found = false;
            const int Tries = 12;
            for (int i = 0; i < Tries; i++)
            {
                V2 d = V2.FromAngle(MathX.Pi * i / Tries);
                RailCast(around, d, maxLen, out float fwd, out _);
                RailCast(around, -d, maxLen, out float back, out _);
                float len = fwd + back;
                if (fwd >= maxLen || back >= maxLen || len > maxLen || len < WebMinLen)
                    continue;
                float score = len * (1f + Math.Abs(d.Dot(aim)));  // (across the aim scores better)
                if (score < bestScore)
                {
                    bestScore = score;
                    a = around - d * back;
                    b = around + d * fwd;
                    found = true;
                }
            }
            return found;
        }

        /// Host: the cast. `target` the second anchor, StepFrom the first (see the summary).
        void CastWeb(Player p, V2 target, bool cap)
        {
            float maxLen = WebMaxLenFor(p);
            V2 a, b;
            WebAnchor wa, wb;
            if (StepFrom.HasValue && WebSnap(StepFrom.Value, 6f, out wa) && WebSnap(target, 6f, out wb) && WebPair(wa, wb, maxLen) == null)
            {
                a = wa.At;
                b = wb.At;
            }
            else
            {
                V2 aim = target - p.Pos;
                aim = aim.Length > 1f ? aim.Normalized() : V2.FromAngle(p.Aim);
                if (!WebAuto(target, aim, maxLen, out a, out b))
                {
                    V2 side = new V2(-aim.Y, aim.X) * (WebFreeLen * 0.5f);  // nothing to tie to: a free web across your aim
                    a = ClampToArena(target - side, 4f);
                    b = ClampToArena(target + side, 4f);
                }
            }
            Zone oldest = null;
            int mine = 0;
            foreach (var z0 in Zones.Values)
                if (z0.Owner == p.Id && z0.Kind == ZoneKind.MendingWeb)
                {
                    mine++;
                    if (oldest == null || z0.Id < oldest.Id)
                        oldest = z0;
                }
            if (mine >= WebMax && oldest != null)
                oldest.T = Math.Min(oldest.T, 0.01f);  // unravels this tick
            V2 ab = b - a;
            var z = AddZone(ZoneKind.MendingWeb, p, (a + b) * 0.5f, Math.Max(1f, ab.Length * 0.5f), WebLife + p.Stats.WebLifeBonus);
            z.Aim = (float)Math.Atan2(ab.Y, ab.X);
            z.Cap = cap;
            BroadcastFx(FxKind.Ring, a, WebColor, 16f, 0.3f, Sfx.Summon);
        }

        /// Host, every tick, for each web once it's built: heal allies in the band, slow and poison enemies in it.
        void TickWeb(Zone z, float dt)
        {
            if (!WebReady(z))
                return;
            float mult = z.Cap ? WebMasteredMult : 1f;
            Player owner = PlayerOrNull(z.Owner);
            foreach (var q in Players.Values)
                if (!q.Downed && WebDist(z, q.Pos) < WebBand + q.Def.BodyR * 0.5f)
                    Heal(q, WebHeal * mult * z.HealMult * dt, owner);
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || WebDist(z, e.Pos) > WebBand + e.Radius * 0.5f)
                    continue;
                e.PoisonDps = Math.Max(e.PoisonDps, WebPoisonDps * mult * z.DmgMult);
                e.PoisonT = Math.Max(e.PoisonT, WebPoisonT);
                e.PoisonBy = z.Owner;
                if (!e.Def.IsBoss)
                    Slow(e, 0.3f, 1f - WebSlow * (e.Def.Heavy ? 0.5f : 1f));
            }
        }

        // --- the pick, on your own game ------------------------------------------------------------

        /// Your own game: the slot of a Mending Web waiting for its anchors (-1: not picking), and the first one once clicked.
        public int WebPickSlot = -1;
        public WebAnchor? WebFirst;
        WebAnchor webSecond;
        bool webFireWas, webFireHold;

        /// While picking: the anchor under the cursor (within WebSnapR of a wall, WebReach of you), if any.
        public bool WebPickAnchor(Player me, V2 aimAt, out WebAnchor a)
        {
            return WebSnap(aimAt, WebSnapR, out a) && a.At.CloserThan(me.Pos, WebReach);
        }

        /// While picking with the first anchor down: why the anchor under the cursor won't do (null: it will; "" no wall there).
        public string WebPickWhy(Player me, V2 aimAt, out WebAnchor b)
        {
            if (!WebPickAnchor(me, aimAt, out b))
                return "";
            return WebFirst.HasValue ? WebPair(WebFirst.Value, b, WebMaxLenFor(me)) : null;
        }

        /// Your own game, each tick before the ability keys: while picking, Fire sets the anchors instead of shooting. Returns
        /// true on the tick the second anchor lands (the cast); cancels the pick when you go down or the slot changes.
        bool LocalWebPick(Player me, V2 aimAt, bool fireDown, ref bool firing)
        {
            bool click = fireDown && !webFireWas;
            webFireWas = fireDown;
            if (webFireHold && !fireDown)
                webFireHold = false;
            if (WebPickSlot >= 0 && (me.Downed || HoldControls || State == MatchState.GameOver || me.Slots[WebPickSlot].Id != AbilityId.MendingWeb))
            {
                WebPickSlot = -1;
                WebFirst = null;
            }
            if (WebPickSlot < 0)
            {
                if (webFireHold)
                    firing = false;
                return false;
            }
            firing = false;
            if (!click)
                return false;
            webFireHold = true;
            WebAnchor a;
            if (!WebFirst.HasValue)
            {
                if (WebPickAnchor(me, aimAt, out a))
                    WebFirst = a;
                return false;
            }
            if (WebPickWhy(me, aimAt, out a) != null)
                return false;
            webSecond = a;
            return true;
        }
    }
}
