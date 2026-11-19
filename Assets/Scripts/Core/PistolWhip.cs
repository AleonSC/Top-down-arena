using System;

namespace Arena
{
    /// The Revolver's Alt (Healer Soldier): the Pistol Whip. The weapon-action key swings the gun in a wide arc in front of
    /// you: it destroys enemy bullets in the arc (big orbs too, never a boss's red aimed shots), hurts what it hits and
    /// shoves it back. Your game sends Msg.WeaponDash (DashWhip); the host checks the cooldown, sends the look
    /// (FxKind.PistolWhip, drawn from its age by Game/PistolWhipLook.cs) and lands the swing WhipWind later (a Knight
    /// swing, Knight.cs, with its own look instead of the sword's).
    public partial class Game
    {
        const byte DashWhip = 42;
        public const float WhipReach = 135f, WhipArc = 190f;  // the arc's radius (px) and width (degrees)
        public const float WhipMult = 1.75f;                  // damage against a round's
        public const float WhipKnock = 180f;
        public const float WhipCd = 4f;                       // seconds before the key works again
        public const float WhipWind = 0.07f;                  // from the press to the hit (the gun is halfway across)
        public const float WhipSweep = 0.16f, WhipFxTime = 0.5f;  // the gun's sweep, and the look with its fade (the effect itself lasts the cooldown)
        const float WhipHold = 0.3f;                          // no shots for this long after the press
        public static readonly Rgba WhipColor = new Rgba(1f, 0.86f, 0.55f);

        float whipLocalCd;

        /// Your game: the weapon-action key with the Revolver. True when the key belongs to the whip (pressed or not).
        bool LocalPistolWhip(Player me, bool pressed, V2 aimAt, float dt)
        {
            whipLocalCd -= dt;
            if (me.Weapon.Id != WeaponId.Revolver)
                return false;
            if (pressed && whipLocalCd <= 0f)
            {
                whipLocalCd = WhipCd;
                if (IsHost)
                    HostWeaponDash(MyId, DashWhip, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(DashWhip);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        /// Host: a player swung the Revolver, aiming at `aimAt`.
        void HostPistolWhip(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Revolver || p.Downed || p.StunT > 0f || p.WhipCd > 0f || p.SwallowT > 0f || State == MatchState.GameOver)
                return;
            p.WhipCd = WhipCd;
            p.FireCd = Math.Max(p.FireCd, WhipHold);
            p.SwingCount++;
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            var s = NewSwing(p, aim, WhipReach, WhipArc, p.Weapon.Damage * WhipMult * p.Stats.DmgMult);
            s.Knock = WhipKnock;
            s.CutAll = true;          // destroys big orbs too
            s.NoFx = true;            // its look is FxKind.PistolWhip, sent now
            s.Sound = Sfx.SwingHeavy;
            s.Delay = WhipWind;
            QueueSwing(s);
            BroadcastFx(FxKind.PistolWhip, p.Pos, p.Pos + V2.FromAngle(aim) * s.Reach, WhipColor, p.Id, WhipCd, Sfx.None);  // (Radius = their id; it lives the cooldown, so every game can draw it)
        }

        /// Host, every tick: the whips cool down.
        void TickPistolWhip(float dt)
        {
            foreach (var p in Players.Values)
                p.WhipCd = Math.Max(0f, p.WhipCd - dt);
        }

        /// How long until `p` can whip again, from the newest look their whip sent (every game sees it; nothing replicated).
        public float WhipCooldown(Player p)
        {
            float best = 0f;
            foreach (var fx in Effects)
                if (fx.Kind == FxKind.PistolWhip && (int)fx.Radius == p.Id)
                    best = Math.Max(best, WhipCd - fx.T);
            return best;
        }
    }
}
