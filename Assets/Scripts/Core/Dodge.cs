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
        float hostClock;  // host: seconds of ticks since the game was made
        readonly System.Collections.Generic.Dictionary<int, float> hostDodgeAt = new System.Collections.Generic.Dictionary<int, float>();  // host: each client's last dodge (hostClock)

        /// Your own game, every tick. `pressed` is a fresh Dodge press this tick.
        void LocalDodge(Player me, V2 move, bool pressed, bool held, float dt)
        {
            dodgeCd -= dt;
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
            if (!pressed || dodgeCd > 0f || me.Downed || me.StunT > 0f || me.StandOn || DashT > 0f || HoldControls || Paused || KatanaStriking)
                return;
            V2 dir = !move.IsZero ? move.Normalized() : -V2.FromAngle(me.Aim);  // no direction held: step back from your aim
            DashDir = dir;
            dashSpeed = DodgeDist / DodgeTime;
            DashT = DodgeTime;
            dodgeCd = DodgeCd;
            Dodges++;
            DodgeFrom = me.Pos;
            dodgeHoldT = 0f;
            SprintT = 0f;
            me.InvulnT = Math.Max(me.InvulnT, DodgeInvuln);
            if (IsHost)
                me.GhostT = me.Stats.GhostTime;  // Ghost Step (the host resolves damage; a client's comes in OnDodge)
            AddFx(FxKind.Ring, me.Pos, new Rgba(0.75f, 0.85f, 1f), 22f, 0.25f);
            Sounds.Add(Sfx.DodgeSwish);  // your own screen only
            if (IsHost)
                return;  // (the host's own player is the one simulated here)
            var w = Proto.Begin(Msg.Dodge);
            w.Write(me.Pos);
            session.SendToHost(Proto.End(w));
        }

        /// Host: a client dodged. They are invulnerable from now on for a moment.
        public void OnDodge(int peer, BinaryReader r)
        {
            r.ReadV2();
            Player p;
            if (!Players.TryGetValue(peer, out p) || p.Downed)
                return;
            float last;
            if (hostDodgeAt.TryGetValue(peer, out last) && hostClock - last < DodgeCd * 0.75f)
                return;  // faster than the cooldown allows (with slack for network jitter): a resent or tampered message
            hostDodgeAt[peer] = hostClock;
            p.InvulnT = Math.Max(p.InvulnT, DodgeInvuln);
            p.GhostT = p.Stats.GhostTime;  // Ghost Step
        }
    }
}
