using System;

namespace Arena
{
    /// The boss gate: in the break before a boss (Quick Play, the Campaign, a Boss Hunt), a summoning circle opens in the
    /// middle of the hub, and the boss fight starts once every player still standing is in it. It takes the place of
    /// Ready and Auto-ready for that break: the team goes when it's done shopping, building and picking. Downed players
    /// don't count. Bots walk into it (BotGate). Host-side; GateUp, GatePos and GateFill go out in every snapshot
    /// (SyncBossGate). The look is Game/BossGateLook.cs.
    public partial class Game
    {
        public const float GateR = 80f;          // stand this close to its middle to be in it
        public const float GateFillTime = 1.2f;  // everyone in for this long and the boss comes

        public static bool GatesOn = true;  // Tools/SimTest turns it off (its players don't walk), except in the bossgate suite

        public bool GateUp;    // the gate is open (everyone's screen)
        public V2 GatePos;
        public float GateFill; // 0-1: the fight starts at 1

        /// The break before a boss, after a wave's results (not the run's opening, a dev jump or the Practice Range).
        bool GateWanted => GatesOn && State == MatchState.Intermission && BossStartsNext && Practice == null && !DevArena && !ShowcaseMode
            && WaveReport != null && WaveReport.Wave == Wave;

        /// Where it opens: the middle of the hub, clear of props.
        V2 GateSpot()
        {
            V2 at = RoomsOn ? Rooms.Areas[Rooms.Hub].Mid : new V2(Width / 2f, Height / 2f);
            return PushOutOfProps(at, GateR * 0.5f);
        }

        /// How many players are standing (alive) and how many of them are in the gate.
        public void GateCount(out int alive, out int inside)
        {
            alive = 0;
            inside = 0;
            foreach (var p in Players.Values)
                if (!p.Downed)
                {
                    alive++;
                    if (p.Pos.DistanceTo(GatePos) < GateR)
                        inside++;
                }
        }

        /// Host, each tick of the break: opens the gate when the boss is next. Returns true while it holds the wave
        /// (instead of the Ready countdown); once everyone is in long enough, the countdown is cut to nothing.
        bool TickBossGate(ref float timer, float dt)
        {
            if (!GateWanted)
            {
                GateUp = false;
                GateFill = 0f;
                return false;
            }
            if (!GateUp)
            {
                GateUp = true;
                GatePos = GateSpot();
                GateFill = 0f;
                BroadcastFx(FxKind.Ring, GatePos, new Rgba(1f, 0.22f, 0.25f), 160f, 0.8f, Sfx.BossWarp);
                DoLog("the boss gate opens");
            }
            int alive, inside;
            GateCount(out alive, out inside);
            if (alive > 0 && inside == alive)
                GateFill = Math.Min(1f, GateFill + dt / GateFillTime);
            else
                GateFill = Math.Max(0f, GateFill - dt * 1.5f);
            if (GateFill >= 1f)
                timer = 0f;
            return true;
        }

        /// The fight is starting: the gate goes (Waves.cs).
        void CloseBossGate()
        {
            if (!GateUp)
                return;
            BroadcastFx(FxKind.Implode, GatePos, new Rgba(1f, 0.22f, 0.25f), 140f, 0.5f, Sfx.BossGong);  // a deep gong as it takes them
            GateUp = false;
            GateFill = 0f;
        }

        /// Bots: into the gate once nothing else is left to do.
        void BotGate(Player me, ref V2 move)
        {
            if (!GateUp || me.Downed || me.Offer != null)
                return;
            V2 to = GatePos - me.Pos;
            move = to.Length > GateR * 0.4f ? to.Normalized() : V2.Zero;
        }

        /// The snapshot's gate block.
        void SyncBossGate(NetSync s)
        {
            s.Bool(ref GateUp);
            if (!GateUp)
                return;
            s.Vec(ref GatePos);
            s.Float(ref GateFill);
        }
    }
}
