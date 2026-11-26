using System;
using System.IO;

namespace Arena
{
    /// Pits (Docs/CellVariants.md): voids in a grid cell's floor, from its pit variant (CellVariants.cs; `World.Pits`). Not walls:
    /// shots fly over them. A body falls once its middle is over the void.
    /// - An enemy (host): dies at once, the kill going to whoever hit it last; it is put on the rim first, so its coins land there.
    ///   Bosses, mini-bosses and spawn portals hover. Walking never takes an enemy in (`KeepEnemyOffPits`, and steering routes
    ///   round pits: `PitSteerLayout`); a push does (knockback, charges, pulls).
    /// - A player (their own game, which owns their movement): drops in (a step past the lip, controls held and the body hidden for
    ///   `PitDrop` s: `PitFallT`), then climbs out onto the rim beside the spot (`PitRim`), and the host takes `PitHurt` of their max health, never below 1 HP, with `PitShield` seconds of invulnerability
    ///   (`Msg.PitFall`, `HostPitFall`). A dash flies over; one that ends over the void falls. Bots keep off them.
    /// Every screen draws the fall from `FxKind.PitFall` (Game/PitLook.cs).
    public partial class Game
    {
        public const float PitHurt = 0.15f;    // a player's fall: this share of max health
        public const float PitShield = 1.5f;   // invulnerable this long from the drop (1 s once out on the rim)
        public const float PitFxDur = 0.9f;    // the fall's look
        public const float PitGrace = 0.6f;    // an enemy this new (just spawned) is put on the rim, not killed
        public const float PitRimGap = 6f;     // how far past the edge a body comes back out
        public const float PitDrop = 0.5f;     // a player's time down in the pit before climbing out (the fall's look ends then)

        /// Your own game: seconds left down in a pit (controls held, body hidden), where you are down there, and where you climb out.
        public float PitFallT;
        V2 pitFallAt, pitFallOut;

        static readonly Solid[] noPits = new Solid[0];
        public int PitKills, PlayerFalls;      // counters (tests, session reports)

        /// The pits in play: the Expanding map's, while its walls are up.
        public Solid[] ActivePits => RoomsOn ? CurrentWorld.Pits ?? noPits : noPits;

        /// The pit holding p (grown by pad), or -1.
        public int PitAt(V2 p, float pad = 0f)
        {
            var pits = ActivePits;
            for (int i = 0; i < pits.Length; i++)
                if (Layouts.Inside(pits[i], p, pad))
                    return i;
            return -1;
        }

        /// The nearest spot just outside pit s from p, for a body r wide.
        static V2 RimOf(Solid s, V2 p, float r)
        {
            float gap = r + PitRimGap;
            if (s.Round)
            {
                V2 d = p - s.Pos;
                float len = d.Length;
                return s.Pos + (len > 0.5f ? d / len : new V2(1f, 0f)) * (s.R + gap);
            }
            float dl = p.X - (s.Pos.X - s.HW), dr = s.Pos.X + s.HW - p.X, du = p.Y - (s.Pos.Y - s.HH), dd = s.Pos.Y + s.HH - p.Y;
            float m = Math.Min(Math.Min(dl, dr), Math.Min(du, dd));
            if (m == dl) return new V2(s.Pos.X - s.HW - gap, p.Y);
            if (m == dr) return new V2(s.Pos.X + s.HW + gap, p.Y);
            if (m == du) return new V2(p.X, s.Pos.Y - s.HH - gap);
            return new V2(p.X, s.Pos.Y + s.HH + gap);
        }

        /// Where a body r wide that fell at p climbs out: the rim nearest the spot, clear of walls and other pits; failing that,
        /// the rim further round, then `fallback`.
        public V2 PitRim(V2 p, float r, V2 fallback)
        {
            var pits = ActivePits;
            int i = PitAt(p);
            if (i < 0)
                return p;
            var s = pits[i];
            for (int k = 0; k < 9; k++)  // the rim nearest the spot, then eight ways out from the pit's middle
            {
                V2 rim = RimOf(s, k == 0 ? p : ClampInto(s, s.Pos + V2.FromAngle(k * MathX.Pi / 4f) * 4000f), r);
                V2 at = PushOutOfSolids(rim, r);
                if (PitAt(at, r * 0.5f) < 0 && at.CloserThan(rim, r + 2f))
                    return at;
            }
            return PitAt(fallback) < 0 ? fallback : p;
        }

        static V2 ClampInto(Solid s, V2 p)
        {
            if (s.Round)
                return s.Pos + (p - s.Pos).LimitLength(s.R * 0.99f);
            return new V2(Math.Max(s.Pos.X - s.HW + 0.5f, Math.Min(s.Pos.X + s.HW - 0.5f, p.X)), Math.Max(s.Pos.Y - s.HH + 0.5f, Math.Min(s.Pos.Y + s.HH - 0.5f, p.Y)));
        }

        /// Does this enemy fly over pits?
        static bool HoversOverPits(Enemy e) => e.Def.IsBoss || e.Def.Mini || e.Type == EnemyType.SpawnPortal || e.Dormant;

        /// Host, each tick after its push slides: an enemy over a pit falls and dies (true), or, just spawned there, steps onto the rim.
        bool PitEnemy(Enemy e)
        {
            if (ActivePits.Length == 0 || HoversOverPits(e) || e.Hp <= 0f || PitAt(e.Pos) < 0)
                return false;
            V2 from = e.Pos;
            e.Pos = PitRim(from, e.Radius, from);
            e.KnockLeft = V2.Zero;
            e.KnockSpeed = 0f;
            if (e.SpawnT > 0f || e.T < PitGrace)
                return false;  // (came in over it: no fall)
            var c = e.Def.Color;
            BroadcastFx(FxKind.PitFall, from, e.Pos, new Rgba(c.R, c.G, c.B, 0.5f), e.Radius, PitFxDur, Sfx.None);
            e.Hp = 0f;  // (the kill goes to e.LastHitBy, whoever shoved it)
            PitKills++;
            return true;
        }

        /// Host, after an enemy's own step: walking doesn't take it into a pit (it slides along the edge instead).
        void KeepEnemyOffPits(Enemy e, V2 before)
        {
            if (ActivePits.Length == 0 || HoversOverPits(e))
                return;
            e.Pos = OffPits(before, e.Pos, 4f);
        }

        /// A step from `before` to `after` that would put a middle over a pit: kept on the ground (slides along one axis, or stays).
        V2 OffPits(V2 before, V2 after, float pad)
        {
            if (PitAt(after, pad) < 0 || PitAt(before, pad) >= 0)
                return after;
            var a = new V2(after.X, before.Y);
            if (PitAt(a, pad) < 0)
                return a;
            var b = new V2(before.X, after.Y);
            return PitAt(b, pad) < 0 ? b : before;
        }

        /// LayoutDef for steering with the pits as walls (cached per floor plan).
        LayoutDef pitPlanBase, pitPlan;
        LayoutDef PitSteerLayout(LayoutDef d)
        {
            if (d == null || !RoomsOn)
                return d;
            var pits = CurrentWorld.Pits;
            if (pits == null || pits.Length == 0)
                return d;
            if (pitPlanBase != d)
            {
                if (pitPlan != null)
                {
                    wayGraphs.Remove(pitPlan);
                    plainGraphs.Remove(pitPlan);
                }
                pitPlanBase = d;
                pitPlan = d.WithSolids(pits);
            }
            return pitPlan;
        }

        /// Your own game, at the end of your step (LocalControl): a bot keeps off pits; anyone else whose middle is over one
        /// (and not dashing) drops in: a step past the lip toward the middle, held there `PitDrop` s, then out on the rim. The host
        /// takes the health as you go in.
        void PitLocal(Player me, V2 before, float dt)
        {
            if (PitFallT > 0f)
            {
                PitFallT -= dt;
                bool lost = me.Downed || PitAt(pitFallAt) < 0 || !me.Pos.CloserThan(pitFallAt, 40f);  // (downed, or moved away: a reset)
                if (!lost)
                    me.Pos = pitFallAt;
                if (PitFallT <= 0f || lost)
                {
                    PitFallT = 0f;
                    if (!lost)
                        me.Pos = PitRim(pitFallAt, me.Def.HitR, pitFallOut);
                }
                DashT = 0f;
                knockLeft = 0f;
                shoveT = 0f;
                return;
            }
            if (ActivePits.Length == 0 || me.Downed)
                return;
            if (Bot && BotLevel != BotSkill.Off && DashT <= 0f)
                me.Pos = OffPits(before, me.Pos, 2f);
            int pit = PitAt(me.Pos);
            if (DashT > 0f || pit < 0)
                return;
            var s = ActivePits[pit];
            V2 from = ClampInto(s, me.Pos + (s.Pos - me.Pos).LimitLength(me.Def.HitR));  // (in past the lip, toward the middle)
            pitFallOut = PitRim(me.Pos, me.Def.HitR, PitAt(before) < 0 ? before : me.Pos);
            pitFallAt = from;
            PitFallT = PitDrop;
            me.Pos = from;
            DashT = 0f;
            knockLeft = 0f;
            shoveT = 0f;
            if (IsHost)
            {
                HostPitFall(me.Id, from, pitFallOut);
                return;
            }
            var w = Proto.Begin(Msg.PitFall);
            w.Write(from);
            w.Write(pitFallOut);
            session.SendToHost(Proto.End(w));
        }

        public void OnPitFall(int peer, BinaryReader r)
        {
            V2 from = r.ReadV2(), to = r.ReadV2();
            HostPitFall(peer, from, to);
        }

        /// Host: player `id` fell into the pit at `from` and climbed out at `to`.
        void HostPitFall(int id, V2 from, V2 to)
        {
            if (!Players.TryGetValue(id, out var p) || p.Downed || PitAt(from, 40f) < 0)
                return;
            if (!p.DevGod)
                p.Hp = Math.Max(Math.Min(p.Hp, 1f), p.Hp - PitHurt * p.MaxHp);  // (never below 1 HP)
            p.InvulnT = Math.Max(p.InvulnT, PitShield);
            var c = p.Def.Color;
            BroadcastFx(FxKind.PitFall, from, to, new Rgba(c.R, c.G, c.B, 1f), p.Def.HitR, PitFxDur, Sfx.None);
            PlayerFalls++;
        }
    }
}
