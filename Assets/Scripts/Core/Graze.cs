using System;

namespace Arena
{
    /// Graze: an enemy shot that skims past a player (within GrazeReach of their edge, without touching) charges
    /// their ultimate by GrazeUltCut seconds. Each shot grazes once, while it passes (moving away), and at most one
    /// graze counts every GrazeGapTime per player, so weaving through a dense wall tops out near 1.5 s of ultimate a
    /// second. Not while invulnerable (a dodge's i-frames or the grace after a hit), downed, or out of a fight.
    /// The host finds it (from the enemy-bullet loop in Combat.cs) and sends Msg.Graze: everyone draws the streak
    /// (FxKind.Graze, Game/GrazeLook.cs), that player's own game takes the time off its ultimate and plays Sfx.Graze,
    /// and its HUD flashes the ultimate's tile (Game/HudTiles.cs).
    public partial class Game
    {
        public const float GrazeReach = 16f;      // px past the hit circle (player HitR + shot radius) that still counts
        public const float GrazeUltCut = 0.3f;    // seconds off the ultimate's recharge per graze
        public const float GrazeGapTime = 0.2f;   // at most one graze every this long, per player
        public const float GrazeFxTime = 0.32f;   // how long the streak shows

        /// Host, from the enemy-bullet loop: shot b missed player p; did it skim past?
        void TryGraze(Player p, Bullet b, float hitR)
        {
            if (b.Grazed || p.GrazeGap > 0f || p.InvulnT > 0f || !Battle || b.Vel.LengthSq < 900f)
                return;
            V2 off = b.Pos - p.Pos;
            float reach = hitR + GrazeReach;
            if (off.LengthSq > reach * reach)
                return;
            if (off.X * b.Vel.X + off.Y * b.Vel.Y < 0f)
                return;  // still closing in: wait until it's going past
            b.Grazed = true;
            p.GrazeGap = GrazeGapTime;
            V2 at = b.Pos;  // where it went by
            LandGraze(p, at, b.Vel);
            var w = Proto.Begin(Msg.Graze);
            w.Write(p.Id);
            w.Write(at);
            w.Write(b.Vel);
            session.Broadcast(Proto.End(w));
        }

        /// Everyone: player p was grazed at `at` by a shot flying along vel.
        void LandGraze(Player p, V2 at, V2 vel)
        {
            // Pos: where the shot went by; Pos2: their middle then; Radius: how far apart, negative when the shot went
            // clockwise round them (the streak runs the way it went).
            V2 n = at - p.Pos;
            bool cw = n.X * vel.Y - n.Y * vel.X < 0f;
            float d = Math.Max(1f, n.Length);
            AddFx(FxKind.Graze, at, new Rgba(0.75f, 0.95f, 1f), cw ? -d : d, GrazeFxTime);
            Effects[Effects.Count - 1].Pos2 = p.Pos;
            if (p == Me || IsHost)
            {
                p.Grazes++;  // (the host keeps a copy for remote players too; their own game takes the time off itself)
                ApplyGrazeCut(p);
            }
            if (p == Me)
                Sounds.Add(Sfx.Graze);
        }

        static void ApplyGrazeCut(Player p)
        {
            var s = p.Slots[Upgrades.UltSlot];
            if (s.Id != AbilityId.None && s.Charges < s.MaxCharges && s.Timer > 0f)
                s.Timer = Math.Max(0f, s.Timer - GrazeUltCut);
        }

        /// Host, each step: the gap between grazes runs down.
        void TickGraze(Player p, float dt)
        {
            if (p.GrazeGap > 0f)
                p.GrazeGap -= dt;
        }
    }
}
