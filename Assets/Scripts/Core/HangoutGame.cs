using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Hangout (Docs/Hangout.md): a hidden game mode (`GameMode.Hangout`) that is the real match, held for good in the between-waves
    /// state. Everyone has their whole loadout exactly as in a run (weapon, abilities, ultimate, class perks), because it is the same game
    /// code: nothing is re-implemented. No wave ever starts, the countdown never ends, nobody levels up, nothing is saved, recorded or
    /// unlocked, and nobody waits on a cooldown. Nobody goes down either: at 0 health a pillar of light takes you up and sets you
    /// down, full, somewhere else in the room (`HangoutRespawn`). Three target dummies stand ahead of the party (the loadout preview's:
    /// they never shoot back, and come back a second after they fall), and twelve enemy spawners stand round the edges
    /// (HangoutSpawners.cs). The way out is the pause menu's Back to lobby, like any run. Any player opens it from
    /// the lobby (`Session.StartHangout`; a client asks the host with `Msg.HangoutOpen`).
    public partial class Game
    {
        /// This run is the Hangout (the flag on `Msg.Start`, so every machine agrees).
        public bool HangoutOn;

        /// Host: the `Msg.Start` about to go out opens the Hangout (set by Session.StartHangout around HostStart).
        public bool HangoutStart;

        /// A run that keeps no progress: the lobby's loadout preview and the Hangout. No XP, mastery, records, recording or tutorial, and
        /// the target dummies stand still.
        public bool Quiet => ShowcaseMode || HangoutOn;

        public const float HangoutHold = 60f;  // the countdown is held here, so the "next wave in" clock never runs out

        bool hangoutReady;  // the host has set the room up (the dummies)

        /// The dummies' spots: a row ahead of where the party stands, in the middle of the arena.
        static V2 HangoutDummySpot(int k) => new V2(Width / 2f + k * 230f, Height / 2f - 140f);

        /// Host and clients, as the Start is read: take the Hangout's own setup over from the lobby's pick (a hunt is
        /// not part of it).
        void HangoutRead(bool on)
        {
            HangoutOn = on;
            hangoutReady = false;
            if (!on)
                return;
            Hunt = null;
            HuntMapOn = false;
            ResetSpawners();  // HangoutSpawners.cs
            hangoutBeams.Clear();
        }

        /// Instead of waves: hold the between-waves state, keep the dummies standing, run the spawners and the respawn beams.
        void TickHangout(float dt)
        {
            State = MatchState.Intermission;
            StateTimer = HangoutHold;
            Remaining = Enemies.Count;
            if (!IsHost)
                return;
            if (!hangoutReady)
            {
                hangoutReady = true;
                dummySpots.Clear();
                dummyIds.Clear();
                dummyRespawn.Clear();
                for (int k = -1; k <= 1; k++)
                {
                    dummySpots.Add(HangoutDummySpot(k));
                    dummyIds.Add(0);
                    dummyRespawn.Add(0.2f + 0.15f * (k + 1));
                }
            }
            TickDummies(dt);
            TickSpawners(dt);  // HangoutSpawners.cs
            TickHangoutBeams(dt);
            Orbs.Clear();
            foreach (var p in Players.Values)
            {
                p.Offer = null;  // (nothing to pick: no levelling up)
                p.PendingPicks = 0;
            }
        }
    }

    public partial class Game
    {
        sealed class HangoutBeam { public Player P; public V2 To; public float T; }

        readonly List<HangoutBeam> hangoutBeams = new List<HangoutBeam>();

        /// Host, from DownIfDead: in the Hangout nobody goes down. Back to full health at once, then the boss wave's beam (BossBeam.cs,
        /// FxKind.BossBeam): a pillar takes them up where they fell and sets them down at a spot clear of enemies. They can't move,
        /// shoot or be hurt while it plays (WarpT).
        void HangoutRespawn(Player p)
        {
            p.Wound = 0f;
            p.OverHp = 0f;
            p.Hp = p.MaxHp;
            p.Firing = false;
            p.WarpT = BeamTotal;
            p.InvulnT = Math.Max(p.InvulnT, BeamTotal + 1f);
            V2 to = HangoutRespawnSpot(p);
            BroadcastFx(FxKind.BossBeam, p.Pos, to, p.Def.Color, p.Id, BeamTotal, Sfx.None);
            BroadcastSound(Sfx.BossWarp);
            hangoutBeams.RemoveAll(b => b.P == p);
            hangoutBeams.Add(new HangoutBeam { P = p, To = to });
        }

        /// A few random spots in the middle of the room; the one farthest from any enemy.
        V2 HangoutRespawnSpot(Player p)
        {
            V2 best = new V2(Width / 2f, Height / 2f + 120f);
            float bestGap = -1f;
            for (int i = 0; i < 10; i++)
            {
                var at = new V2(220f + (float)rng.NextDouble() * (Width - 440f), 180f + (float)rng.NextDouble() * (Height - 360f));
                float gap = float.MaxValue;
                foreach (var e in Enemies.Values)
                    gap = Math.Min(gap, at.DistanceTo(e.Pos));
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = at;
                }
            }
            return PushOutOfProps(best, p.Def.BodyR);
        }

        /// Host: each beam moves its player at BeamMoveAt, like the boss wave's.
        void TickHangoutBeams(float dt)
        {
            for (int i = hangoutBeams.Count - 1; i >= 0; i--)
            {
                var b = hangoutBeams[i];
                float before = b.T;
                b.T += dt;
                if (before < BeamMoveAt && b.T >= BeamMoveAt)
                {
                    MovePlayer(b.P, b.To);
                    BroadcastSound(Sfx.Blink);
                }
                if (b.T >= BeamTotal)
                    hangoutBeams.RemoveAt(i);
            }
        }
    }

    public partial class Session
    {
        /// Open the Hangout from the lobby. The host starts it for everyone at once (nobody has to be ready); a client asks the host.
        public void StartHangout()
        {
            if (!IsHost)
            {
                if (!Game.Running && Screen == AppScreen.Lobby)
                    SendToHost(Proto.End(Proto.Begin(Msg.HangoutOpen)));
                return;
            }
            if (Game.Running || Screen != AppScreen.Lobby)
                return;
            Game.SettleRetry();  // a retry left untaken ends the old run now (Checkpoints.cs)
            foreach (var e in Lobby.Values)
                e.Ready = false;
            ApplyMatchSetup();
            Game.HangoutStart = true;
            Game.HostStart();
            Game.HangoutStart = false;
            foreach (var e in Lobby.Values)
                e.Seat = -1;
        }
    }
}
