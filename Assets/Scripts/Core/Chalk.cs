using System;
using System.Collections.Generic;

namespace Arena
{
    /// One dab of spirit ink. A stroke is a run of dots with the same Stroke id, laid ChalkStep px apart.
    public class InkDot
    {
        public V2 Pos;
        public float T;          // seconds of life left (shots that cross it wear it down)
        public int Owner, Stroke;
        public int Pen;          // > 0: this dot is part of a closed loop (an InkPen)
    }

    /// A closed loop of ink: the enemies inside it when it closed can't get out until it fades or breaks.
    public class InkPen
    {
        public int Id, Owner;
        public float T;
        public List<V2> Poly = new List<V2>();
        public int Dots;         // how many dots make up its ring
    }

    /// Spirit Chalk (Healer Summoner): hold and drag to draw a line of spirit ink. It burns and slows enemies touching it, heals
    /// allies standing on it and (with the Warding Ink card) destroys enemy bullets that cross it, each wearing it down fast. Draw a loop round enemies and they
    /// are penned in until it fades (bosses aren't held). Clients see it through the snapshot (SyncInk). Looks: Game/ChalkLook.cs.
    public partial class Game
    {
        public const float ChalkSpeed = 700f, ChalkStep = 10f, ChalkLife = 10f, ChalkInk = 600f, ChalkDotR = 11f;
        public const float ChalkBurn = 26f, ChalkSlow = 0.3f, ChalkHeal = 10f, ChalkWear = 1.5f;  // ink fades out over ChalkLife s; with Warding Ink each bullet it stops takes ChalkWear s off the dot it hit
        public const float ChalkErase = 0.3f;  // a dot with less life than this is fading out (erased, or about to expire) and no longer counts as ink down
        public const float ChalkSeal = 3.5f, ChalkCloseDist = 20f, ChalkMinLoopArea = 3600f;  // a loop closes within this of where it began; it holds this long; it must enclose at least about 60 x 60 px
        public const int ChalkMinLoopDots = 12;  // a loop is at least 120 px of line (so a scribble in place doesn't count)

        /// How far from you you can draw (Piercing Rounds stretch it); the aim line ends here too.
        public static float ChalkReach(Player p) => 450f * PierceReach(p);

        /// Everyone's ink, oldest first. The host runs it; clients rebuild it from each snapshot.
        public readonly List<InkDot> Ink = new List<InkDot>();
        readonly Dictionary<int, InkPen> pens = new Dictionary<int, InkPen>();  // host
        readonly Dictionary<int, ChalkHand> hands = new Dictionary<int, ChalkHand>();  // host: each drawer's pen tip
        int nextStroke = 1, nextPen = 1;

        /// Where each drawer's chalk is right now (while they hold the button), sent with the ink so the head of a line moves
        /// smoothly instead of in dab-sized steps. InkTipDisp is what's drawn: it eases toward the tip every frame (UpdateInkView).
        public readonly Dictionary<int, V2> InkTip = new Dictionary<int, V2>();
        public readonly Dictionary<int, V2> InkTipDisp = new Dictionary<int, V2>();

        class ChalkHand
        {
            public V2 Tip;
            public bool Down;
            public int Stroke, LoopFrom;
            public readonly List<InkDot> Dots = new List<InkDot>();
        }

        static float InkWidth(Player p) => p != null ? AreaMult(p) : 1f;

        /// Host, every tick: drawing, then what the ink does to enemies, allies and bullets.
        void TickChalk(float dt)
        {
            foreach (var p in Players.Values)
                DrawChalk(p, dt);
            if (Ink.Count == 0 && pens.Count == 0)
                return;
            // Wear: dots fade, a broken ring frees its pen.
            for (int i = Ink.Count - 1; i >= 0; i--)
            {
                var d = Ink[i];
                d.T -= dt;
                if (d.T > 0f)
                    continue;
                InkPen pen;
                if (d.Pen != 0 && pens.TryGetValue(d.Pen, out pen) && pen.T > 0.05f)
                    pen.T = 0f;  // a gap in the ring: they're out
                Ink.RemoveAt(i);
            }
            foreach (var pen in new List<InkPen>(pens.Values))
            {
                pen.T -= dt;
                if (pen.T > 0f)
                    continue;
                pens.Remove(pen.Id);
                foreach (var d in Ink)
                    if (d.Pen == pen.Id)
                        d.Pen = 0;
            }
            if (Ink.Count == 0)
                return;
            InkTouches(dt);
        }

        /// Hold to draw: the tip follows your cursor (within reach, at most ChalkSpeed px a second) laying dots, while there's ink.
        void DrawChalk(Player p, float dt)
        {
            ChalkHand h;
            if (!hands.TryGetValue(p.Id, out h))
                hands[p.Id] = h = new ChalkHand();
            if (p.Weapon.Id != WeaponId.SpiritChalk || p.Downed || !p.Firing)
            {
                h.Down = false;
                InkTip.Remove(p.Id);
                return;
            }
            V2 target = ClampToArena(p.Pos + (p.AimAt - p.Pos).LimitLength(ChalkReach(p)), 4f);
            if (!h.Down)
            {
                h.Down = true;
                h.Tip = target;
                BeginStroke(p, h);
                InkTip[p.Id] = h.Tip;
                return;
            }
            V2 to = target - h.Tip;
            float len = to.Length, move = Math.Min(len, ChalkSpeed * dt);
            V2 end = len > 0.001f ? h.Tip + to / len * move : h.Tip;
            if (move > 0f)
            {
                V2 last = h.Dots.Count > 0 ? h.Dots[h.Dots.Count - 1].Pos : h.Tip;
                float gap = last.DistanceTo(end);
                if (gap >= ChalkStep)
                {
                    int n = (int)(gap / ChalkStep);
                    for (int k = 1; k <= n; k++)
                        LayDot(p, h, last + (end - last) / gap * (ChalkStep * k));
                }
            }
            h.Tip = end;
            InkTip[p.Id] = h.Tip;
        }

        void BeginStroke(Player p, ChalkHand h)
        {
            h.Stroke = nextStroke++;
            h.LoopFrom = 0;
            h.Dots.Clear();
            LayDot(p, h, h.Tip);
        }

        int InkCap(Player p) => (int)(ChalkInk * (1f + 0.2f * ExtraShots(p)) / ChalkStep);

        /// How much of your ink is down (a dot being erased, or about to fade anyway, no longer counts).
        int InkCount(int owner)
        {
            int n = 0;
            foreach (var d in Ink)
                if (d.Owner == owner && d.T > ChalkErase)
                    n++;
            return n;
        }

        /// At the cap, drawing more rubs out the start of your line: the oldest of your dots fades away to make room. If it was
        /// part of a sealed loop, that ring is broken and what it held goes free.
        void EraseOldest(Player p)
        {
            int cap = InkCap(p);
            while (InkCount(p.Id) >= cap)
            {
                InkDot oldest = null;
                foreach (var d in Ink)
                    if (d.Owner == p.Id && d.T > ChalkErase)
                    {
                        oldest = d;
                        break;
                    }
                if (oldest == null)
                    return;
                oldest.T = ChalkErase * 0.8f;
                InkPen pen;
                if (oldest.Pen != 0 && pens.TryGetValue(oldest.Pen, out pen))
                    pen.T = 0f;
            }
        }

        void LayDot(Player p, ChalkHand h, V2 at)
        {
            EraseOldest(p);
            var dot = new InkDot { Pos = at, T = ChalkLife, Owner = p.Id, Stroke = h.Stroke };
            Ink.Add(dot);
            h.Dots.Add(dot);
            TryClose(p, h);
        }

        /// The newest dot meets an older one of this stroke: a loop. If it holds enough ground, seal it and trap what's inside.
        void TryClose(Player p, ChalkHand h)
        {
            int last = h.Dots.Count - 1;
            var tail = h.Dots[last];
            for (int j = h.LoopFrom; j <= last - ChalkMinLoopDots; j++)
            {
                var head = h.Dots[j];
                if (head.T <= ChalkErase || head.Pos.DistanceTo(tail.Pos) > ChalkCloseDist)
                    continue;
                var pen = new InkPen { Id = nextPen++, Owner = p.Id, T = ChalkSeal, Dots = last - j + 1 };
                for (int k = j; k <= last; k++)
                    pen.Poly.Add(h.Dots[k].Pos);
                float area = PolyArea(pen.Poly);
                if (area < ChalkMinLoopArea)
                    continue;
                for (int k = j; k <= last; k++)
                {
                    h.Dots[k].Pen = pen.Id;
                    h.Dots[k].T = Math.Max(h.Dots[k].T, ChalkSeal);  // the ring holds as one
                }
                pens[pen.Id] = pen;
                h.LoopFrom = last;  // the next loop starts from here
                int caught = 0;
                foreach (var e in Enemies.Values)
                    if (e.Hp > 0f && e.SpawnT <= 0f && e.PenId == 0 && !e.Def.IsBoss && !e.Def.Mini && InsidePoly(pen.Poly, e.Pos))
                    {
                        e.PenId = pen.Id;
                        e.PenPos = e.Pos;
                        e.Penned = true;
                        caught++;
                    }
                V2 c = V2.Zero;
                foreach (var q in pen.Poly)
                    c += q;
                c = c / pen.Poly.Count;
                BroadcastFx(FxKind.Ring, c, new Rgba(0.8f, 0.92f, 1f), (float)Math.Sqrt(area / Math.PI) + 20f, 0.45f, caught > 0 ? Sfx.SpellHoly : Sfx.None);
                return;
            }
        }

        public static float PolyArea(List<V2> poly)
        {
            float a = 0f;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                a += poly[j].X * poly[i].Y - poly[i].X * poly[j].Y;
            return Math.Abs(a) * 0.5f;
        }

        /// Ray-casting point-in-polygon.
        public static bool InsidePoly(List<V2> poly, V2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    inside = !inside;
            return inside;
        }

        /// What the ink does: burns and slows enemies on it, heals allies on it, and eats enemy bullets (which wears it).
        void InkTouches(float dt)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.Hp <= 0f || e.SpawnT > 0f)
                    continue;
                foreach (var d in Ink)
                {
                    Player owner;
                    if (!Players.TryGetValue(d.Owner, out owner) || e.Pos.DistanceTo(d.Pos) > ChalkDotR * InkWidth(owner) + e.Radius)
                        continue;
                    DotDamage(e, ChalkBurn * owner.Stats.DmgMult * dt, owner.Id);
                    if (!Heavy(e))
                    {
                        e.SlowT = Math.Max(e.SlowT, 0.3f);
                        e.SlowMult = Math.Min(e.SlowMult, 1f - ChalkSlow);
                    }
                    break;  // once a tick, however many dots it's on
                }
            }
            foreach (var q in Players.Values)
            {
                if (q.Downed)
                    continue;
                foreach (var d in Ink)
                {
                    Player owner;
                    if (!Players.TryGetValue(d.Owner, out owner) || q.Pos.DistanceTo(d.Pos) > ChalkDotR * InkWidth(owner) + q.Def.HitR)
                        continue;
                    Heal(q, ChalkHeal * dt, owner);
                    break;
                }
            }
            var gone = scratchInkShots;
            gone.Clear();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                foreach (var d in Ink)
                {
                    Player owner;
                    Players.TryGetValue(d.Owner, out owner);
                    if (owner == null || !owner.Stats.ChalkWard)
                        continue;  // plain ink lets bullets through; Warding Ink makes it a wall that wears away
                    if (b.Pos.DistanceTo(d.Pos) > ChalkDotR * InkWidth(owner) + b.Radius)
                        continue;
                    gone.Add(b.Id);
                    d.T -= ChalkWear;
                    BroadcastFx(FxKind.Spark, d.Pos, new Rgba(0.8f, 0.9f, 1f), 12f, 0.25f, Sfx.None);  // a puff of dust where it hit
                    break;
                }
            }
            foreach (int id in gone)
            {
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }
        }

        readonly List<int> scratchInkShots = new List<int>();

        /// Host, after everything has moved: enemies penned in by a loop can't be outside it. A step that would leave slides along
        /// the ring instead, or stops; a pen that has faded, broken or lost them lets them go.
        void EnforcePens()
        {
            foreach (var e in Enemies.Values)
            {
                e.Penned = e.PenId != 0;  // (the host draws the ring from this too; clients get it in the snapshot)
                if (e.PenId == 0)
                    continue;
                InkPen pen;
                if (e.Hp <= 0f || !pens.TryGetValue(e.PenId, out pen))
                {
                    e.PenId = 0;
                    e.Penned = false;
                    continue;
                }
                if (InsidePoly(pen.Poly, e.Pos))
                {
                    e.PenPos = e.Pos;
                    continue;
                }
                V2 slideX = new V2(e.Pos.X, e.PenPos.Y), slideY = new V2(e.PenPos.X, e.Pos.Y);
                if (InsidePoly(pen.Poly, slideX))
                    e.Pos = slideX;
                else if (InsidePoly(pen.Poly, slideY))
                    e.Pos = slideY;
                else
                    e.Pos = e.PenPos;
                e.PenPos = e.Pos;
                e.KnockLeft = V2.Zero;
            }
        }

        /// A pen's ring, for drawing: the dots with this pen's id, in order (clients rebuild it from the snapshot's ink).
        public void PenRing(int pen, List<V2> into)
        {
            into.Clear();
            foreach (var d in Ink)
                if (d.Pen == pen)
                    into.Add(d.Pos);
        }

        /// Every frame (everyone's game): the drawn chalk tips ease toward where the host says they are, and a client ages its ink
        /// between snapshots (the host sends each dot's life only 20 times a second).
        void UpdateInkView(float dt)
        {
            float k = 1f - (float)Math.Exp(-dt * 30f);
            foreach (var kv in InkTip)
            {
                V2 shown;
                InkTipDisp[kv.Key] = InkTipDisp.TryGetValue(kv.Key, out shown) && shown.DistanceTo(kv.Value) < 120f ? V2.Lerp(shown, kv.Value, k) : kv.Value;
            }
            if (InkTipDisp.Count > InkTip.Count)
                foreach (int id in new List<int>(InkTipDisp.Keys))
                    if (!InkTip.ContainsKey(id))
                        InkTipDisp.Remove(id);
            if (!IsHost)
                foreach (var d in Ink)
                    d.T -= dt;
        }

        /// Snapshot: everyone's ink (position, life, owner, stroke, pen) and each drawer's live tip, small enough to send whole each time.
        void SyncInk(NetSync s)
        {
            int count = Ink.Count;
            s.Short(ref count);
            if (s.Reading)
            {
                Ink.Clear();
                for (int i = 0; i < count; i++)
                {
                    int owner = 0, stroke = 0, pen = 0;
                    V2 pos = default(V2);
                    float t = 0f;
                    s.Byte(ref owner); s.Short(ref stroke); s.Short(ref pen); s.Vec(ref pos); s.Float(ref t);
                    Ink.Add(new InkDot { Owner = owner, Stroke = stroke, Pen = pen, Pos = pos, T = t });
                }
            }
            else
                foreach (var d in Ink)
                {
                    int owner = d.Owner & 0xff, stroke = d.Stroke & 0x7fff, pen = d.Pen & 0x7fff;
                    V2 pos = d.Pos;
                    float t = d.T;
                    s.Byte(ref owner); s.Short(ref stroke); s.Short(ref pen); s.Vec(ref pos); s.Float(ref t);
                }
            int tips = InkTip.Count;
            s.Byte(ref tips);
            if (s.Reading)
            {
                InkTip.Clear();
                for (int i = 0; i < tips; i++)
                {
                    int owner = 0;
                    V2 pos = default(V2);
                    s.Byte(ref owner); s.Vec(ref pos);
                    InkTip[owner] = pos;
                }
            }
            else
                foreach (var kv in InkTip)
                {
                    int owner = kv.Key & 0xff;
                    V2 pos = kv.Value;
                    s.Byte(ref owner); s.Vec(ref pos);
                }
        }
    }
}
