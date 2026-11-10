using System;
using System.IO;

namespace Arena
{
    /// The Dodge: a tiny dash everyone has, with a short window where nothing can hurt you. Ctrl (Keybinds, "Dodge") or a
    /// double tap of a movement key. Your own game moves you (like Lunge and the weapon dashes) and tells the host
    /// (Msg.Dodge), which grants the invulnerability (Player.InvulnT, already replicated). Standing still, it steps
    /// you back from your aim.
    public partial class Game
    {
        public const float DodgeDist = 95f, DodgeTime = 0.11f, DodgeInvuln = 0.22f, DodgeCd = 1.1f;

        float dodgeCd;

        /// Your own game, every tick. `pressed` is a fresh Dodge press this tick.
        void LocalDodge(Player me, V2 move, bool pressed, float dt)
        {
            dodgeCd -= dt;
            if (!pressed || dodgeCd > 0f || me.Downed || me.StunT > 0f || me.StandOn || DashT > 0f || HoldControls || Paused || KatanaStriking)
                return;
            V2 dir = !move.IsZero ? move.Normalized() : -V2.FromAngle(me.Aim);  // no direction held: step back from your aim
            DashDir = dir;
            dashSpeed = DodgeDist / DodgeTime;
            DashT = DodgeTime;
            dodgeCd = DodgeCd;
            me.InvulnT = Math.Max(me.InvulnT, DodgeInvuln);
            AddFx(FxKind.Ring, me.Pos, new Rgba(0.75f, 0.85f, 1f), 22f, 0.25f);
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
            if (Players.TryGetValue(peer, out p) && !p.Downed)
                p.InvulnT = Math.Max(p.InvulnT, DodgeInvuln);
        }
    }
}
