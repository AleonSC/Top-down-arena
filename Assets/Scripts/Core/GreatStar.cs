using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// The DPS Ninja's Shuriken, Great Star stance (Docs/GreatStar.md; Docs/Mockups/shuriken-great-star.html, concept B "Trace the Path").
    ///   - The weapon-action key swaps between the fan (StarStance 0, today's stars) and the Great Star (1). Going into the Great Star
    ///     recalls every star you have stuck in enemies, as the key used to.
    ///   - In the Great Star stance, holding Fire draws a route of ink from your Ninja through the cursor (your own game:
    ///     LocalGreatStar, up to GreatStarInkMax of ink). Letting go throws one huge star along it (Msg.StarThrow carries the points);
    ///     a click without drawing throws it straight at the cursor, GreatStarStraight far.
    ///   - The host flies it as a bullet (Defs.GreatStar) along the route, cutting every enemy it passes (GreatStarMult x the fan
    ///     star's damage), then home to your hand, cutting them again for GreatStarBackShare. It can't be thrown again until it's caught.
    ///     The host sends everyone the route (Msg.StarThrow, host -> all) and each screen flies its copy along it the same way
    ///     (TickGreatStars); Msg.Steer (SendSteer) keeps them in step. A shield or cover that stops it puts it straight back in your hand.
    ///   - The look is Game/GreatStarLook.cs (the held star, the ink, the thrown star).
    public partial class Game
    {
        public const float GreatStarInkMax = 900f, GreatStarStraight = 600f, GreatStarMinInk = 30f, GreatStarPointGap = 16f;  // the most ink a route can use; a click's straight throw; less ink than this is a click; the gap between the route's points
        public const int GreatStarMaxPoints = 64;
        public const float GreatStarSpeed = 1150f, GreatStarBackSpeed = 1100f, GreatStarCatchR = 30f, GreatStarCatchCd = 0.25f, GreatStarLife = 6f;  // out along the route, back home; caught this close; the wait before the next throw
        public const float GreatStarMult = 60f / 9f, GreatStarBackShare = 0.5f, GreatStarExtra = 0.15f;  // damage against a fan star's (9 -> 60); on the way back; +15% an extra projectile
        public const float StarSwitchCd = 0.3f;
        public static readonly Rgba GreatStarInk = new Rgba(0.7f, 0.64f, 1f);

        class StarFlight
        {
            public int Owner, Next;
            public bool Back, Seen;  // flying home; (a client) its bullet has arrived
            public float Age;
            public readonly List<V2> Route = new List<V2>();
        }

        readonly Dictionary<int, StarFlight> starFlights = new Dictionary<int, StarFlight>();  // bullet id -> its route (every game)
        readonly List<int> starGone = new List<int>();

        /// Is this player's Great Star out of their hand (a thrown star of theirs is in the air)? Every game knows (the bullet is everywhere).
        public bool GreatStarOut(int owner)
        {
            foreach (var b in Bullets.Values)
                if (b.Kind == Defs.GreatStar && b.Owner == owner)
                    return true;
            return false;
        }

        public static bool InGreatStar(Player p) => p.Weapon != null && p.Weapon.Id == WeaponId.Shuriken && p.StarStance == 1;

        /// How much ink a route from `from` through these points uses.
        public static float InkLength(V2 from, List<V2> pts)
        {
            float len = 0f;
            foreach (var q in pts)
            {
                len += from.DistanceTo(q);
                from = q;
            }
            return len;
        }

        // --- Your own game -------------------------------------------------------------------------------------------

        /// The route being drawn on this machine (arena points; the Ninja is its start). Drawn by GreatStarLook.cs.
        public readonly List<V2> GreatStarRoute = new List<V2>();
        bool starWasFiring;
        float starThrowLocalCd, starSwitchLocalCd;

        /// Your own game, every tick. In the Great Star stance Fire belongs to the stance: holding it draws the route and letting go
        /// throws. True when it does (then nothing else fires).
        bool LocalGreatStar(Player me, bool firing, V2 aimAt, float dt)
        {
            starThrowLocalCd -= dt;
            if (!InGreatStar(me))
            {
                GreatStarRoute.Clear();
                starWasFiring = false;
                return false;
            }
            bool ready = !me.Downed && me.StunT <= 0f && !me.ParryActive && starThrowLocalCd <= 0f && !GreatStarOut(me.Id) && State != MatchState.GameOver;
            if (!ready)
                GreatStarRoute.Clear();
            else if (firing)
                AddInk(me.Pos, ClampToArena(aimAt, 12f));
            else if (starWasFiring && GreatStarRoute.Count > 0)
            {
                SendStarThrow(GreatStarRoute);
                starThrowLocalCd = 0.3f;
                GreatStarRoute.Clear();
            }
            else
                GreatStarRoute.Clear();
            starWasFiring = firing && ready;
            return true;
        }

        /// The route goes on to the cursor, a point every GreatStarPointGap, until the ink runs out.
        void AddInk(V2 from, V2 at)
        {
            var r = GreatStarRoute;
            if (r.Count >= GreatStarMaxPoints)
                return;
            if (r.Count > 0 && r[r.Count - 1].CloserThan(at, GreatStarPointGap))
                return;
            float left = GreatStarInkMax - InkLength(from, r);
            V2 last = r.Count > 0 ? r[r.Count - 1] : from;
            float d = last.DistanceTo(at);
            if (left <= 1f || d < 0.5f)
                return;
            r.Add(d > left ? last + (at - last) * (left / d) : at);
        }

        void SendStarThrow(List<V2> pts)
        {
            if (IsHost)
            {
                Player me;
                if (Players.TryGetValue(MyId, out me))
                    HostGreatStarThrow(me, pts);
                return;
            }
            var w = Proto.Begin(Msg.StarThrow);
            int n = Math.Min(pts.Count, GreatStarMaxPoints);
            w.Write((byte)n);
            for (int i = 0; i < n; i++)
                w.Write(pts[i]);
            session.SendToHost(Proto.End(w));
        }

        // --- Host ----------------------------------------------------------------------------------------------------

        readonly List<V2> starPtsScratch = new List<V2>();

        /// Host: a guest let go of a drawn route (Msg.StarThrow).
        public void OnStarThrow(int peer, BinaryReader r)
        {
            int n = r.ReadByte();
            starPtsScratch.Clear();
            for (int i = 0; i < n; i++)
            {
                V2 q = r.ReadV2();
                if (q.IsFinite && i < GreatStarMaxPoints)
                    starPtsScratch.Add(q);
            }
            Player p;
            if (Players.TryGetValue(peer, out p))
                HostGreatStarThrow(p, starPtsScratch);
        }

        /// A client: the host threw a Great Star along this route (Msg.StarThrow, host -> all). The bullet itself comes with the next Spawn.
        public void ReadStarThrow(BinaryReader r)
        {
            int id = r.ReadInt32(), owner = r.ReadInt32(), n = r.ReadByte();
            var f = new StarFlight { Owner = owner };
            for (int i = 0; i < n; i++)
                f.Route.Add(r.ReadV2());
            if (f.Route.Count > 0)
                starFlights[id] = f;
        }

        /// The weapon-action key with the Shuriken: the fan to the Great Star (pulling stuck stars out) and back.
        void HostStarSwitch(Player p)
        {
            if (p.Weapon.Id != WeaponId.Shuriken || p.Downed || p.StunT > 0f || p.StarSwitchCd > 0f)
                return;
            p.StarSwitchCd = StarSwitchCd - 0.05f;  // (slack for a guest's trip, which keeps the real cooldown)
            if (p.StarStance == 0)
            {
                p.StarStance = 1;
                RecallStars(p);
                BroadcastFx(FxKind.Ring, p.Pos, GreatStarInk, 30f, 0.3f, Sfx.SwingLight);
                return;
            }
            p.StarStance = 0;
            p.FireCd = Math.Max(p.FireCd, 0.15f);
            BroadcastSound(Sfx.SwingLight);
        }

        /// Throws the Great Star along a route (arena points; it starts at the thrower). Nothing happens unless it's in the hand.
        void HostGreatStarThrow(Player p, List<V2> pts)
        {
            if (!InGreatStar(p) || p.Downed || p.StunT > 0f || p.ParryActive || p.StarCatchCd > 0f || pts.Count == 0 || State == MatchState.GameOver || GreatStarOut(p.Id))
                return;
            var f = new StarFlight { Owner = p.Id };
            V2 from = p.Pos;
            if (InkLength(from, pts) < GreatStarMinInk || pts.Count == 1)
            {
                V2 d = pts[pts.Count - 1] - from;  // a click: straight out toward it
                f.Route.Add(ClampToArena(from + (d.LengthSq < 1f ? V2.FromAngle(p.Aim) : d.Normalized()) * GreatStarStraight, 12f));
            }
            else
            {
                float left = GreatStarInkMax;
                foreach (var raw in pts)
                {
                    V2 q = ClampToArena(raw, 12f);
                    float d = from.DistanceTo(q);
                    if (d < 0.5f)
                        continue;
                    if (d >= left)
                    {
                        f.Route.Add(from + (q - from) * (left / d));
                        break;
                    }
                    f.Route.Add(q);
                    left -= d;
                    from = q;
                }
            }
            if (f.Route.Count == 0)
                return;
            V2 dir = (f.Route[0] - p.Pos).Normalized();
            if (dir.IsZero)
                dir = V2.FromAngle(p.Aim);
            float dmg = p.Weapon.Damage * p.Stats.DmgMult * GreatStarMult * (1f + GreatStarExtra * ExtraShots(p));
            bool was = firingWeapon;
            firingWeapon = true;  // (a weapon shot: accuracy, weapon mods)
            var b = SpawnBullet(Defs.GreatStar, p.Pos, dir * GreatStarSpeed, 0f, GreatStarLife, dmg, p.Id);
            firingWeapon = was;
            b.Pierce += 99;
            b.Homing = 0f;  // (it goes where you drew it, Homing Rounds or not)
            if (b.Sent != null)
                b.Sent.Homing = 0f;
            starFlights[b.Id] = f;
            f.Seen = true;
            var w = Proto.Begin(Msg.StarThrow);  // everyone flies it along the same route
            w.Write(b.Id);
            w.Write(p.Id);
            w.Write((byte)f.Route.Count);
            foreach (var q in f.Route)
                w.Write(q);
            session.Broadcast(Proto.End(w));
            p.Aim = dir.Angle;
            BroadcastFx(FxKind.Ring, p.Pos, GreatStarInk, 34f, 0.22f, Sfx.GreatStarThrow);
        }

        /// Every game, every tick before bullets move (PrepareNinjaBullets): each Great Star follows its route, then flies home. The
        /// host catches it (and runs the cooldowns); a client's copy waits at the hand for the host's Remove.
        void TickGreatStars(float dt)
        {
            if (IsHost)
                foreach (var p in Players.Values)
                {
                    p.StarSwitchCd -= dt;
                    p.StarCatchCd -= dt;
                    if (p.StarStance != 0 && (p.Weapon == null || p.Weapon.Id != WeaponId.Shuriken))
                        p.StarStance = 0;
                }
            if (starFlights.Count == 0 || dt <= 0f)
                return;
            starGone.Clear();
            foreach (var kv in starFlights)
            {
                var f = kv.Value;
                Bullet b;
                Player p;
                Players.TryGetValue(f.Owner, out p);
                f.Age += dt;
                if (!Bullets.TryGetValue(kv.Key, out b))
                {
                    if (f.Seen || f.Age > 2f)
                    {
                        starGone.Add(kv.Key);  // stopped by a shield or cover: straight back in the hand
                        if (p != null && IsHost)
                            p.StarCatchCd = GreatStarCatchCd;
                    }
                    continue;
                }
                f.Seen = true;
                if (p == null || p.Downed)
                {
                    if (IsHost)
                    {
                        KillBullet(b.Id);
                        starGone.Add(kv.Key);
                    }
                    continue;
                }
                b.Life = GreatStarLife;  // (it never runs out while it flies)
                float fast = p.Stats.ShotSpeedMult;
                if (!f.Back)
                {
                    float step = GreatStarSpeed * fast * dt;
                    V2 at = b.Pos;
                    while (step > 0f && f.Next < f.Route.Count)
                    {
                        V2 to = f.Route[f.Next];
                        float d = at.DistanceTo(to);
                        if (d <= step)
                        {
                            at = to;
                            step -= d;
                            f.Next++;
                        }
                        else
                        {
                            at += (to - at) * (step / d);
                            step = 0f;
                        }
                    }
                    b.Vel = (at - b.Pos) / dt;  // (MoveBullets moves it there this tick)
                    if (f.Next >= f.Route.Count)
                    {
                        f.Back = true;  // the end of the route: home, cutting everything again
                        if (IsHost)
                        {
                            b.LastHit = b.PrevHit = 0;
                            b.Dmg *= GreatStarBackShare;
                        }
                    }
                    continue;
                }
                V2 home = p.Pos - b.Pos;
                float dist = home.Length;
                if (dist < p.Def.BodyR + GreatStarCatchR)
                {
                    if (!IsHost)
                    {
                        b.Vel = home / dt;  // (a client's copy waits in the hand for the host's Remove)
                        continue;
                    }
                    KillBullet(b.Id);
                    starGone.Add(kv.Key);
                    p.StarCatchCd = GreatStarCatchCd;
                    BroadcastFx(FxKind.Ring, p.Pos, GreatStarInk, 22f, 0.18f, Sfx.GreatStarCatch);
                    continue;
                }
                b.Vel = home / dist * Math.Min(GreatStarBackSpeed * fast, dist / dt);
            }
            foreach (int id in starGone)
                starFlights.Remove(id);
        }
    }
}
