using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Warden's faction (Docs/Approaches.md), beside the Rioters and Guards of Warden.cs:
    ///   Riot Shield: a guard behind a tall shield. It blocks shots and swings from the front (it wears down in four crack
    ///     stages, like a Shielder's, but much stronger: RiotShieldShare times its health). It walks up, flashes its
    ///     shield (RiotWindup), lunges and bashes: RiotBashDamage and a knockback of RiotKnock px. After a bash it holds
    ///     the shield low for RiotLowTime (hits from the front land). A player can't be bashed again for RiotKnockGrace s,
    ///     so two shields bounce you without pinning you. Broken, it staggers (RiotStagger), then flails with its baton,
    ///     taking RiotBrokenMult damage. In the Warden's searchlight turn he sends two (SendRiotShields), faster, and they
    ///     work round to your side away from the light so their bash knocks you into it.
    ///   Gas Grenadier: keeps its distance and lobs tear gas near you (the Warden's gas patch, smaller); with no Warden
    ///     about, TickLooseGas makes the gas hurt.
    /// The knockback happens on the player's own game (Msg.Knock, like Substitution's Msg.Teleport), the damage on the host.
    public partial class Game
    {
        // --- Numbers -----------------------------------------------------------------------------------------------
        public const float RiotShieldShare = 4f;     // the shield holds 4x the guard's health
        public const float RiotBrokenMult = 1.5f;    // damage taken once the shield's broken
        const float RiotShieldArc = 1.2f;            // blocks within ~70 degrees of where it faces
        const float RiotTurn = 2.2f;                 // rad/s: slow enough to get round
        const float RiotBashReach = 42f;             // starts its wind-up this far beyond touching you
        public const float RiotWindup = 0.4f, RiotLunge = 0.18f, RiotLungeSpeed = 430f, RiotLowTime = 1f, RiotStagger = 1f;
        public const float RiotBashDamage = 10f, RiotKnock = 160f, RiotKnockGrace = 0.8f;
        const float RiotWardenSpeed = 1.25f;         // the Warden's own pair (searchlight turn) move faster
        const int RiotWardenPair = 2;
        const float GrenadierRange = 330f, GrenadierEvery = 3.6f, GrenadierGasRadius = 70f, GrenadierGasLife = 4f;
        public const float KnockSpeed = 900f;        // px/s a knockback moves you at (160 px in about 0.18 s)

        // A Riot Shield's Phase: the low three bits are the shield's cracks (0-3, RiotCracked = broken), the rest its state.
        public const byte RiotWalk = 0, RiotWinding = 1, RiotLunging = 2, RiotLow = 3, RiotStaggered = 4, RiotFlail = 5;
        public const byte RiotCracked = 4;
        public static byte RiotCrack(Enemy e) => (byte)(e.Phase & 7);
        public static byte RiotState(Enemy e) => (byte)(e.Phase >> 3);
        static void SetRiot(Enemy e, byte state, byte crack) => e.Phase = (byte)(state << 3 | crack);
        static void SetRiotState(Enemy e, byte state, float time)
        {
            SetRiot(e, state, RiotCrack(e));
            e.PhaseT = time;
        }

        public static bool RiotShieldBroken(Enemy e) => e.Type == EnemyType.RiotShield && RiotCrack(e) >= RiotCracked;

        /// Shots and swings from the front stop on an unbroken shield, unless it's held low after a bash.
        bool RiotShieldBlocks(Enemy e, V2 from)
        {
            if (RiotCrack(e) >= RiotCracked || RiotState(e) == RiotLow)
                return false;
            float diff = Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
            return diff < RiotShieldArc;
        }

        /// A blocked hit wears the shield; it cracks in three steps, then shatters and the guard staggers.
        void WearRiotShield(Enemy e, float amount)
        {
            if (RiotCrack(e) >= RiotCracked || e.ShieldMax <= 0f)
                return;
            e.ShieldHp -= amount;
            float frac = e.ShieldHp / e.ShieldMax;
            byte crack = (byte)(frac <= 0f ? RiotCracked : frac <= 0.25f ? 3 : frac <= 0.5f ? 2 : frac <= 0.75f ? 1 : 0);
            if (crack == RiotCrack(e))
                return;
            V2 front = e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 6f);
            if (crack == RiotCracked)
            {
                SetRiot(e, RiotStaggered, RiotCracked);
                e.PhaseT = RiotStagger;
                BroadcastFx(FxKind.Shatter, e.Pos, front, new Rgba(0.7f, 0.82f, 1f), RiotShieldArc, 0.6f, Sfx.Clang);
            }
            else
            {
                SetRiot(e, RiotState(e), crack);
                BroadcastFx(FxKind.Spark, front, new Rgba(0.8f, 0.9f, 1f), 18f, 0.25f, Sfx.Hit);
            }
        }

        /// Turns slowly while walking; locked in while winding up and lunging; a staggered guard stays put.
        void RiotShieldFace(Enemy e, float want, float dt)
        {
            byte st = RiotState(e);
            if (st == RiotWinding || st == RiotLunging || st == RiotStaggered)
                return;
            if (st == RiotFlail)
            {
                e.Facing = want;
                return;
            }
            float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
            e.Facing += Math.Max(-RiotTurn * dt, Math.Min(RiotTurn * dt, diff));
        }

        V2 WardenFactionMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            if (e.Type == EnemyType.GasGrenadier)
                return e.Winding ? V2.Zero : KeepDistance(dir, dist, GrenadierRange) + dir.Orthogonal() * e.Strafe * 0.5f;  // plants its feet for the lob
            if (e.Type == EnemyType.SecurityGuard)
                return SecurityMove(e, dir, dist, dt);  // SecurityGuard.cs
            if (e.Type == EnemyType.TaserGuard)
                return e.Winding ? V2.Zero : KeepDistance(dir, dist, TaserRange) + dir.Orthogonal() * e.Strafe * 0.5f;  // plants its feet while it charges
            switch (RiotState(e))
            {
                case RiotWinding:
                case RiotStaggered:
                    return V2.Zero;
                case RiotLunging:
                    speed = RiotLungeSpeed;
                    return V2.FromAngle(e.Facing);
                case RiotLow:
                    speed *= 0.3f;
                    return dir;
                case RiotFlail:
                    return dir;
            }
            V2 goal = HerdSpot(e, tpos);
            if (goal.DistanceTo(e.Pos) < 8f)
                return V2.Zero;
            return RouteTo(e, goal);  // round walls and crates (LayoutRules.cs)
        }

        // --- The Riot Line (the Warden's approach, its last wave: Approaches.cs) --------------------------------------

        // Riot Shields march at you shoulder to shoulder (RiotLineGap apart, at RiotLineSpeed of their pace), Guards
        // close behind (RiotGuardBehind back, RiotGuardGap apart). The Guards break out and rush you once the wall is
        // within RiotLineRelease of its target, half its shields are down, or it's been marching RiotLineMaxMarch s.
        public const float RiotLineGap = 58f, RiotGuardGap = 48f, RiotGuardBehind = 80f, RiotLineSpeed = 0.55f, RiotLineRelease = 190f, RiotLineMaxMarch = 18f;
        readonly Dictionary<int, int> riotLines = new Dictionary<int, int>();   // host: a marching Riot Line's squad id -> its shields at the start
        readonly HashSet<int> riotLineBroke = new HashSet<int>();                // host: lines whose guards have broken out

        /// Each member's place in its own row (shields counted among shields, guards among guards).
        static int RiotLineSlot(SquadDef s, int i)
        {
            int k = 0;
            for (int j = 0; j < i; j++)
                if ((s.Members[j] == EnemyType.RiotShield) == (s.Members[i] == EnemyType.RiotShield))
                    k++;
            return k;
        }

        static int RiotLineCount(SquadDef s, EnemyType type)
        {
            int n = 0;
            foreach (var t in s.Members)
                if ((t == EnemyType.RiotShield) == (type == EnemyType.RiotShield))
                    n++;
            return n;
        }

        /// How a Riot Line's members move (from SquadMove): the shields keep their wall and advance on you; the guards
        /// keep behind it until it breaks, then they're ordinary chasers (`v`). A shield winding up, bashing or broken
        /// does its own thing.
        V2 RiotLineMove(Enemy e, List<Enemy> mates, V2 tpos, V2 v)
        {
            int start;
            if (!riotLines.TryGetValue(e.SquadId, out start) || riotLineBroke.Contains(e.SquadId))
                return v;
            var shields = new List<Enemy>();
            foreach (var m in mates)
                if (m.Type == EnemyType.RiotShield && RiotCrack(m) < RiotCracked)
                    shields.Add(m);
            shields.Sort((a, b) => a.SquadSlot.CompareTo(b.SquadSlot));
            V2 c = V2.Zero;
            foreach (var m in shields)
                c = c + m.Pos;
            if (shields.Count > 0)
                c = c / shields.Count;
            if (shields.Count * 2 < start || shields.Count == 0 || c.DistanceTo(tpos) < RiotLineRelease || e.T > RiotLineMaxMarch)
            {
                riotLineBroke.Add(e.SquadId);  // the wall's done: the guards charge
                BroadcastFx(FxKind.Ring, shields.Count > 0 ? c : e.Pos, Defs.Get(EnemyType.Guard).Color, 160f, 0.5f, Sfx.BossWhoosh);
                DoLog("a riot line breaks: the guards charge");
                return v;
            }
            V2 fwd = (tpos - c).Normalized(), across = fwd.Orthogonal();
            if (e.Type == EnemyType.RiotShield)
            {
                if (RiotState(e) != RiotWalk || RiotCrack(e) >= RiotCracked)
                    return v;
                int k = shields.IndexOf(e);
                V2 goal = c + fwd * 40f + across * ((k - (shields.Count - 1) / 2f) * RiotLineGap);
                return RouteVel(e, goal, ((goal - e.Pos) / 40f).LimitLength(RiotLineSpeed));  // round walls and crates
            }
            var guards = mates.FindAll(m => m.Type != EnemyType.RiotShield);
            guards.Sort((a, b) => a.SquadSlot.CompareTo(b.SquadSlot));
            int g = guards.IndexOf(e);
            V2 spot = c - fwd * RiotGuardBehind + across * ((g - (guards.Count - 1) / 2f) * RiotGuardGap);
            return RouteVel(e, spot, ((spot - e.Pos) / 40f).LimitLength(1f));
        }

        /// Where a Riot Shield walks to: straight at you, or, sent by the Warden while his searchlight hunts, round to
        /// your side away from the light, so a bash knocks you into it.
        V2 HerdSpot(Enemy e, V2 tpos)
        {
            var warden = e.ParentId != 0 ? TheWarden() : null;
            var w = warden != null && warden.Id == e.ParentId ? warden.Warden : null;
            var p = PlayerOrNull(e.TargetId);
            if (w == null || w.LightMode == WardenLight.Off || p == null || p.Downed)
                return tpos;
            V2 fromLight = p.Pos - w.Light;
            if (fromLight.Length < 20f)
                return tpos;  // you're in the light already: just go for you
            return p.Pos + fromLight.Normalized() * (e.Radius + p.Def.BodyR + 24f);
        }

        void RiotShieldSpecial(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            switch (RiotState(e))
            {
                case RiotWalk:
                {
                    if (RiotCrack(e) >= RiotCracked)
                        break;
                    var p = PlayerOrNull(e.TargetId);
                    if (p == null || p.Downed || p.BashGraceT > 0f)
                        break;
                    float reach = e.Radius + p.Def.BodyR + RiotBashReach;
                    float off = Math.Abs(MathX.Fmod((p.Pos - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                    if (p.Pos.DistanceTo(e.Pos) < reach && off < 0.5f)
                    {
                        SetRiotState(e, RiotWinding, RiotWindup);
                        BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 6f), new Rgba(1f, 0.95f, 0.7f), 20f, RiotWindup, Sfx.None);
                    }
                    break;
                }
                case RiotWinding:
                    if (e.PhaseT <= 0f)
                        SetRiotState(e, RiotLunging, RiotLunge);
                    break;
                case RiotLunging:
                    RiotBash(e);
                    if (e.PhaseT <= 0f)
                        SetRiotState(e, RiotLow, RiotLowTime);
                    break;
                case RiotLow:
                    if (e.PhaseT <= 0f)
                        SetRiotState(e, RiotWalk, 0f);
                    break;
                case RiotStaggered:
                    if (e.PhaseT <= 0f)
                        SetRiotState(e, RiotFlail, 0f);
                    break;
            }
        }

        /// The lunge: anyone in front of the shield is hit and knocked back the way it faces (once per grace period).
        void RiotBash(Enemy e)
        {
            V2 fwd = V2.FromAngle(e.Facing);
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.SwallowT > 0f || p.BashGraceT > 0f)
                    continue;
                V2 to = p.Pos - e.Pos;
                if (to.Length > e.Radius + p.Def.BodyR + 14f || to.Normalized().Dot(fwd) < 0.45f)
                    continue;
                p.BashGraceT = RiotKnockGrace;
                if (p.ParryActive && ParryContact(p, e, RiotBashDamage))
                    continue;  // a raised guard takes it, and you keep your feet
                if (p.InvulnT <= 0f)
                {
                    Damage(p, RiotBashDamage, e.Def.Name + " (bash)");
                    RodTouched(p, e);  // LightningRod.cs
                }
                KnockPlayer(p, fwd, RiotKnock);
                BroadcastFx(FxKind.Spark, p.Pos, new Rgba(0.85f, 0.9f, 1f), 24f, 0.25f, Sfx.Clang);
            }
        }

        // --- Knockback ------------------------------------------------------------------------------------------------

        V2 knockDir;
        float knockLeft;  // px still to go (your own game)

        /// Host: knock a player back. Players move on their own game, so it's sent to theirs (or done here for the host's own).
        void KnockPlayer(Player p, V2 dir, float dist)
        {
            if (p.Id == MyId)
                StartKnock(dir, dist);
            var w = Proto.Begin(Msg.Knock);
            w.Write(p.Id);
            w.Write(dir);
            w.Write(dist);
            session.Broadcast(Proto.End(w));
        }

        void StartKnock(V2 dir, float dist)
        {
            knockDir = dir.IsZero ? V2.FromAngle(0f) : dir.Normalized();
            knockLeft = dist;
        }

        /// Your own game, each frame (LocalControl): carry the knockback out.
        void ApplyKnock(Player me, float dt)
        {
            if (knockLeft <= 0f)
                return;
            if (me.Downed)
            {
                knockLeft = 0f;
                return;
            }
            float step = Math.Min(knockLeft, KnockSpeed * dt);
            me.Pos += knockDir * step;
            knockLeft -= step;
        }

        // --- The Warden's pair -----------------------------------------------------------------------------------------

        /// The searchlight turn: two Riot Shields through the side doors (fewer if some are still up).
        void SendRiotShields(Enemy warden)
        {
            int have = 0;
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.RiotShield && o.ParentId == warden.Id && o.Hp > 0f)
                    have++;
            for (int i = have; i < RiotWardenPair; i++)
            {
                V2 door = RiotDoors[i % 2 == 0 ? 0 : 3];  // left top, right bottom
                V2 inward = door.X < Width / 2f ? new V2(1f, 0f) : new V2(-1f, 0f);
                var s = SpawnEnemyAt(EnemyType.RiotShield, door + inward * 50f, 0.6f, warden.Id);
                s.Speed *= RiotWardenSpeed;
                s.Facing = inward.Angle;
                BroadcastFx(FxKind.Boom, door, new Rgba(0.45f, 0.6f, 1f), 70f, 0.4f, Sfx.None);
            }
        }

        // The Warden fires no stun shots himself: Taser Guards do. WardenTaserLight come through the side doors for the
        // searchlight turn, WardenTaserStand for the last stand (lit up in the dark); they're cleared when the turn changes.
        public const int WardenTaserLight = 2, WardenTaserStand = 1;

        void SendTaserGuards(Enemy warden, int count)
        {
            int[] doors = count > 1 ? new[] { 1, 2 } : new[] { 4 };  // left lower and right upper (the shields use the other two); bottom left
            for (int i = 0; i < count; i++)
            {
                V2 door = RiotDoors[doors[i % doors.Length]];
                V2 inward = door.Y >= Height - 1f ? new V2(0f, -1f) : door.X < Width / 2f ? new V2(1f, 0f) : new V2(-1f, 0f);
                var g = SpawnEnemyAt(EnemyType.TaserGuard, door + inward * 50f, 0.6f, warden.Id);
                g.Facing = inward.Angle;
                g.FireT = 1f + 0.9f * i;  // staggered, so the bolts don't come together
                BroadcastFx(FxKind.Boom, door, new Rgba(0.75f, 0.91f, 1f), 70f, 0.4f, Sfx.None);
            }
        }

        void ClearTaserGuards(Enemy warden)
        {
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.TaserGuard && o.ParentId == warden.Id)
                {
                    o.LastHitBy = 0;  // no kill credit
                    o.Hp = 0f;
                }
        }

        /// Gone with the Warden, or when he changes stage.
        void ClearWardenShields(Enemy warden)
        {
            ClearTaserGuards(warden);
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.RiotShield && o.ParentId == warden.Id)
                {
                    o.LastHitBy = 0;  // no kill credit
                    o.Hp = 0f;
                }
        }

        // --- Gas Grenadier ---------------------------------------------------------------------------------------------

        // An overhand lob (Docs/Mockups/gas-grenadier-throw.html option A, Game/GasLobLook.cs): when the wind-up starts it picks the spot near you and shows
        // the ring (FxKind.GasLob: the arm goes back, the canister rises, the ring fills); GrenadierSwing before the throw is the
        // swing; the cloud lands GasFlight later on that spot, so you can walk out of it. It plants its feet meanwhile.
        public const float GrenadierWindUp = 0.85f, GrenadierSwing = 0.22f;
        static readonly Rgba GasLobColor = new Rgba(0.6f, 0.95f, 0.4f);

        /// Host, once a tick while it's firing normally: locks the landing spot as the wind-up begins (Winding is true for the last
        /// GrenadierWindUp + GrenadierSwing s before the throw) and tells everyone how long is left.
        void GrenadierWind(Enemy e, V2 tpos, float rateNow)
        {
            if (!e.Winding)
            {
                e.GasLocked = false;
                return;
            }
            if (e.GasLocked)
                return;
            e.GasLocked = true;
            e.GasAt = ClampToArena(tpos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(0f, 50f), 60f);
            float left = Math.Max(0f, e.FireT / Math.Max(0.01f, rateNow));
            BroadcastFx(FxKind.GasLob, e.GasAt, new V2(e.Id, GasFlight), GasLobColor, GrenadierGasRadius, left + GasFlight, Sfx.None);
        }

        void GrenadierFire(Enemy e, V2 tpos)
        {
            e.FireT = GrenadierEvery;
            if (!e.GasLocked)  // no wind-up seen (it fired the moment it spawned): an instant lob
            {
                e.GasAt = ClampToArena(tpos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(0f, 50f), 60f);
                BroadcastFx(FxKind.GasLob, e.GasAt, new V2(e.Id, GasFlight), GasLobColor, GrenadierGasRadius, GasFlight, Sfx.None);
            }
            e.GasLocked = false;
            SpawnPatchAt(PatchKind.Gas, e.GasAt, GasFlight, GasFlight + GrenadierGasLife, GrenadierGasRadius, GrenadierGasRadius);
            BroadcastFx(FxKind.GasLob, e.Pos, new V2(e.Id, -1f), GasLobColor, GrenadierGasRadius, 0.3f, Sfx.Throw);  // the release cue
        }

        // --- Taser Guard ---------------------------------------------------------------------------------------------
        // Keeps TaserRange away, plants its feet and charges for TaserWindUp s (prongs crackling, a dashed aim line: Game/TaserLook.cs),
        // then fires one Fork Bolt (Defs.TaserBolt) straight at you. A hit stuns you for StunTime (Combat.cs); a perfect parry
        // sends it back as a needle like any shot. It doesn't lead its target: strafe across the line.
        public const float TaserWindUp = 0.7f, TaserEvery = 3.2f, TaserRange = 320f, TaserBoltSpeed = 260f;

        void TaserFire(Enemy e, float aim)
        {
            e.FireT = TaserEvery;
            EnemyShot(e, aim, TaserBoltSpeed, Defs.TaserBolt);
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(aim) * (e.Radius + 12f), Defs.BulletColor(Defs.TaserBolt), 22f, 0.2f, Sfx.SpellZap);
        }

        float looseGasT;

        /// Host, every tick: tear gas hurts with no Warden about (the Warden's fight ticks its own, TickWardenGas), and
        /// the bash grace runs down.
        void TickWardenFaction(float dt)
        {
            foreach (var p in Players.Values)
                p.BashGraceT = Math.Max(0f, p.BashGraceT - dt);
            looseGasT -= dt;
            if (looseGasT > 0f)
                return;
            looseGasT = 0.25f;
            if (TheWarden() != null)
                return;
            foreach (var pt in Patches.Values)
            {
                if (pt.Kind != PatchKind.Gas || !pt.Active)
                    continue;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                        DamageOverTime(p, GasDps * 0.25f, "Tear gas");
            }
        }
    }
}
