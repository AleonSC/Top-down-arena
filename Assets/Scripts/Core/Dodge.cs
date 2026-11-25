using System;
using System.IO;

namespace Arena
{
    /// The Dodge: a tiny dash everyone has, with a short window where nothing can hurt you. Ctrl (Keybinds, "Dodge") or a
    /// double tap of a movement key. Your own game moves you (like Lunge and the weapon dashes) and tells the host
    /// (Msg.Dodge), which grants the invulnerability (Player.InvulnT, already replicated). Standing still, it steps
    /// you back from your aim.
    /// Sprint: keep holding the key that started the dodge for SprintHold s and you burst into a sprint, SprintBoost faster,
    /// fading back to walking pace over SprintTime. A tap doesn't. Your own game only (it moves you), so nothing is sent.
    /// Wall kick: a Dodge that runs into a wall or a solid prop bounces off it (the dash reflects and carries on) and the
    /// dodge cooldown comes back at once, for one free dodge within DodgeCd. That free dodge can't kick again. Msg.Dodge
    /// says when a dodge is the free one, so the host lets it in under the cooldown.
    public partial class Game
    {
        public const float DodgeDist = 95f, DodgeTime = 0.11f, DodgeInvuln = 0.22f, DodgeCd = 1.1f;
        public const float SprintHold = 0.2f, SprintBoost = 0.5f, SprintTime = 2.2f;  // hold this long after the dodge; +50% speed fading over 2.2 s

        public float SprintT;          // your own game: seconds of sprint left
        float dodgeHoldT = -1f;        // seconds the dodge key has stayed down since the dodge; -1 = let go (no sprint)
        /// Your own move speed multiplier from the sprint: 1 + SprintBoost, easing back to 1 as it runs out.
        public float SprintMult => SprintT > 0f ? 1f + SprintBoost * SprintFade : 1f;
        /// 1 at the start of a sprint, 0 at its end (slow at first, then quicker: an ease-in fade).
        public float SprintFade => SprintT <= 0f ? 0f : 1f - (1f - SprintT / SprintTime) * (1f - SprintT / SprintTime);

        float dodgeCd;
        public int Dodges;      // your own game: dodges made, counted up (the floor's burst ripple watches it; FloorGridLook.cs)
        public V2 DodgeFrom;    // your own game: where the last dodge started
        public const float KickMinPush = 0.5f, KickFacing = 0.35f;  // a push-out this many px, this square against the dash (dot), is a wall hit
        public const float KickReach = 70f;  // a Dodge ending this close short of a wall stretches on to it and kicks
        public const float KickDist = 150f, KickTime = 0.13f;  // the kickoff: a fresh dash this far, this fast (the Dodge is 95 px in 0.11 s)
        bool kickReached;       // your own game: this Dodge already stretched on to a wall (once a dodge)
        bool kickDash;          // your own game: the dash running now is a Dodge that can still kick off a wall
        V2 kickDir;             // the Dodge's DashDir (another dash taking over changes it)
        float kickFreeT;        // your own game: seconds left to use the free dodge a wall kick gave back
        public int WallKicks;   // your own game: wall kicks made, counted up
        public V2 WallKickAt;   // your own game: where the last one hit
        readonly System.Collections.Generic.Dictionary<int, bool> hostKickUsed = new System.Collections.Generic.Dictionary<int, bool>();  // host: each client's last dodge was a free one
        float hostClock;  // host: seconds of ticks since the game was made
        readonly System.Collections.Generic.Dictionary<int, float> hostDodgeAt = new System.Collections.Generic.Dictionary<int, float>();  // host: each client's last dodge (hostClock)

        /// Your own game, every tick. `pressed` is a fresh Dodge press this tick.
        void LocalDodge(Player me, V2 move, bool pressed, bool held, float dt)
        {
            dodgeCd -= dt;
            kickFreeT = Math.Max(0f, kickFreeT - dt);
            if (DashT <= 0f)
                kickDash = false;
            if (me.Downed || me.StunT > 0f || HoldControls)
            {
                SprintT = 0f;
                dodgeHoldT = -1f;
            }
            SprintT = Math.Max(0f, SprintT - dt);
            if (dodgeHoldT >= 0f)
            {
                if (!held)
                    dodgeHoldT = -1f;  // a tap: no sprint
                else if ((dodgeHoldT += dt) >= SprintHold)
                {
                    SprintT = SprintTime;
                    dodgeHoldT = -1f;
                    AddFx(FxKind.Ring, me.Pos, new Rgba(0.75f, 0.85f, 1f), 30f, 0.3f);
                }
            }
            if (!pressed || dodgeCd > 0f || me.Downed || me.StunT > 0f || me.StandOn || Seated(me) || TankOn(me) || ArmOn(me) || Riding(me) || ApAway(me) || DashT > 0f || HoldControls || Paused || KatanaStriking)
                return;
            V2 dir = !move.IsZero ? move.Normalized() : -V2.FromAngle(me.Aim);  // no direction held: step back from your aim
            DashDir = dir;
            dashSpeed = DodgeDist / DodgeTime;
            DashT = DodgeTime;
            dodgeCd = DodgeCd;
            bool free = kickFreeT > 0f;  // the dodge a wall kick gave back: it can't kick again
            kickFreeT = 0f;
            kickDash = !free;
            kickReached = false;
            kickDir = dir;
            Dodges++;
            DodgeFrom = me.Pos;
            dodgeHoldT = 0f;
            SprintT = 0f;
            me.InvulnT = Math.Max(me.InvulnT, DodgeInvuln);
            if (IsHost)
            {
                me.GhostT = me.Stats.GhostTime;  // Ghost Step (the host resolves damage; a client's comes in OnDodge)
                DrillDodged(me.Id);  // the Dodge drill (DrillGame.cs)
            }
            AddFx(FxKind.Ring, me.Pos, new Rgba(0.75f, 0.85f, 1f), 22f, 0.25f);
            Sounds.Add(Sfx.DodgeSwish);  // your own screen only
            if (IsHost)
                return;  // (the host's own player is the one simulated here)
            var w = Proto.Begin(Msg.Dodge);
            w.Write(me.Pos);
            w.Write(free);
            session.SendToHost(Proto.End(w));
        }

        /// Your own game, after a push out of walls or props: `want` is where you were before it. A Dodge pushed back
        /// square against its way bounces off (one bounce a dodge) and gives the cooldown back.
        void WallKick(Player me, V2 want)
        {
            if (!kickDash || DashT <= 0f || me.Downed || DashDir.X != kickDir.X || DashDir.Y != kickDir.Y)
                return;
            V2 push = me.Pos - want;
            if (push.Length < KickMinPush)
                return;
            V2 n = push.Normalized();
            float d = DashDir.Dot(n);
            if (d > -KickFacing)
                return;  // glancing along the wall: slide on
            DashDir = (DashDir - n * (2f * d)).Normalized();
            DashT = KickTime;  // kick off harder than the dodge came in
            dashSpeed = KickDist / KickTime;
            kickDash = false;
            dodgeCd = 0f;
            kickFreeT = DodgeCd;
            WallKicks++;
            WallKickAt = me.Pos - n * me.Def.HitR;
            Dodges++;  // the floor ripples again (FloorGridLook.cs)
            DodgeFrom = me.Pos;
            AddFx(FxKind.Ring, WallKickAt, new Rgba(1f, 0.85f, 0.45f), 26f, 0.25f);
            Sounds.Add(Sfx.DodgeSwish);
            DrillNoteKick();  // the Wall Kick drill (DrillGame.cs)
        }

        /// Your own game, each dash tick before it moves: a kick-ready Dodge about to end with a wall within KickReach
        /// straight ahead runs on until it reaches it (then WallKick bounces it off).
        void KickReachOn(Player me, float dt)
        {
            if (!kickDash || kickReached || DashT > dt || DashDir.X != kickDir.X || DashDir.Y != kickDir.Y)
                return;
            float r = me.Def.HitR + PropPad, step = 6f;
            for (float s = step; s <= KickReach + step * 0.5f; s += step)
            {
                V2 p = me.Pos + DashDir * (dashSpeed * DashT + s);
                V2 o = p;
                if (Props.Count > 0 || LayoutOn)
                    o = PushOutOfProps(o, r);
                if (Walled)
                    o = KeepInArena(o, me.Def.BodyR);
                V2 push = o - p;
                if (push.Length < KickMinPush || DashDir.Dot(push.Normalized()) > -KickFacing)
                    continue;
                kickReached = true;
                DashT += (s + step) / dashSpeed;  // a little past it, so the push-out registers the hit
                return;
            }
        }

        /// Host: a client dodged. They are invulnerable from now on for a moment.
        public void OnDodge(int peer, BinaryReader r)
        {
            r.ReadV2();
            bool free = r.ReadBoolean();  // the free dodge from a wall kick
            Player p;
            if (!Players.TryGetValue(peer, out p) || p.Downed)
                return;
            float last;
            bool used;
            hostKickUsed.TryGetValue(peer, out used);
            bool early = hostDodgeAt.TryGetValue(peer, out last) && hostClock - last < DodgeCd * 0.75f;
            if (early && (!free || used))
                return;  // faster than the cooldown allows (with slack for network jitter): a resent or tampered message
            hostKickUsed[peer] = early;  // one free dodge under the cooldown, then a full one again
            hostDodgeAt[peer] = hostClock;
            p.InvulnT = Math.Max(p.InvulnT, DodgeInvuln);
            p.GhostT = p.Stats.GhostTime;  // Ghost Step
            DrillDodged(p.Id);  // the Dodge drill (DrillGame.cs)
        }
    }
}
