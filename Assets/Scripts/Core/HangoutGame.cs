using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Practice (the Hangout in code; Docs/Hangout.md): a hidden game mode (`GameMode.Hangout`) that is the real match, held for good in the
    /// between-waves state, played inside the lobby's loadout window. The screen stays the lobby (`Session.Screen` never turns to Game), so
    /// the party column, the Ready key and everyone's classes stay in view. Everyone has their whole loadout exactly as in a run (weapon,
    /// abilities, ultimate, class perks), because it is the same game code: nothing is re-implemented. No wave ever starts, nobody levels
    /// up, nothing is saved, recorded or unlocked, and nobody waits on a cooldown. Players' attacks hurt each other (Pvp.cs). Nobody goes
    /// down: at 0 health a pillar of light takes you up and sets you down, full, somewhere else in the room (`HangoutRespawn`). Twelve
    /// enemy spawners stand round the edges (HangoutSpawners.cs). Any player opens the room from the lobby's Practice key
    /// (`Session.StartHangout`; a client asks the host with `Msg.HangoutOpen`); each player chooses for themselves whether they look at it
    /// or at their loadout (`LobbyUi`, `HangoutUi.cs`). A loadout changed in the lobby swaps just that player's character
    /// (`Msg.PracticePlayer`, `SyncPractice`); someone joining starts the room over with them in it. The real run's start replaces it.
    public partial class Game
    {
        /// This run is the Hangout (the flag on `Msg.Start`, so every machine agrees).
        public bool HangoutOn;

        /// The Practice room is up: running behind the lobby (the screen stays the lobby).
        public bool Practicing => Running && HangoutOn;

        /// A real run is going (anything but the Practice room): the lobby's own requests wait for it to end.
        public bool InRun => Running && !HangoutOn;

        /// Host: the `Msg.Start` about to go out opens the Hangout (set by Session.StartHangout around HostStart).
        public bool HangoutStart;

        /// A run that keeps no progress: the lobby's loadout preview and the Hangout. No XP, mastery, records, recording or tutorial, and
        /// the target dummies stand still.
        public bool Quiet => ShowcaseMode || HangoutOn || DrillOn;

        public const float HangoutHold = 60f;  // the countdown is held here, so the "next wave in" clock never runs out

        /// Host and clients, as the Start is read: take the Hangout's own setup over from the lobby's pick (a hunt is
        /// not part of it).
        void HangoutRead(bool on)
        {
            HangoutOn = on;
            practiceSent.Clear();
            practiceRev = -1;
            if (!on)
                return;
            Hunt = null;
            HuntMapOn = false;
            ResetSpawners();  // HangoutSpawners.cs
            hangoutBeams.Clear();
        }

        /// Instead of waves: hold the between-waves state, run the spawners and the respawn beams. (No target dummies: the
        /// loadout preview has those.)
        void TickHangout(float dt)
        {
            State = MatchState.Intermission;
            StateTimer = HangoutHold;
            Remaining = Enemies.Count;
            HoldControls = false;  // control from the first frame: there is no countdown to wait for
            if (!IsHost)
                return;
            TickSpawners(dt);  // HangoutSpawners.cs
            TickPvp(dt);  // what other players' attacks left on each player's stand-in (Pvp.cs)
            TickHangoutBeams(dt);
            Orbs.Clear();
            foreach (var p in Players.Values)
            {
                p.Offer = null;  // (nothing to pick: no levelling up)
                p.PendingPicks = 0;
            }
        }
    }

    /// Practice: one player's character swapped in place when their loadout changes in the lobby (`Msg.PracticePlayer`).
    public partial class Game
    {
        readonly Dictionary<int, string> practiceSent = new Dictionary<int, string>();  // host: each player's loadout as the room last had it
        int practiceRev = -1;  // the lobby broadcast those were checked against (Session.LobbyRev): unchanged, nothing to check

        /// A lobby entry's loadout as one string, to notice a change (class, weapon, abilities, ultimate, perks, chains).
        string PracticeKey(LobbyEntry e)
        {
            var w = new BinaryWriter(new MemoryStream());
            WriteStartPlayer(w, e);
            w.Flush();
            return Convert.ToBase64String(((MemoryStream)w.BaseStream).ToArray());
        }

        /// Host, every lobby tick while the room is up: someone new in the lobby starts the room over with them in it; a loadout
        /// changed swaps just that player's character (everyone else plays on).
        public void SyncPractice()
        {
            if (!IsHost || !Practicing)
                return;
            foreach (var e in session.Lobby.Values)
                if (!Players.ContainsKey(e.Id))
                {
                    HangoutStart = true;  // a newcomer has no room yet: a fresh start for everyone (Session.StartHangout)
                    try { HostStart(); }
                    finally { HangoutStart = false; }  // (never left on: the next START would open as Practice)
                    return;
                }
            if (practiceRev == session.LobbyRev)
                return;  // (no lobby change since the last check: no loadout to re-pack)
            practiceRev = session.LobbyRev;
            foreach (var e in session.Lobby.Values)
            {
                string key = PracticeKey(e);
                string had;
                if (!practiceSent.TryGetValue(e.Id, out had))
                {
                    practiceSent[e.Id] = key;  // (as the room started)
                    continue;
                }
                if (had == key)
                    continue;
                practiceSent[e.Id] = key;
                var w = Proto.Begin(Msg.PracticePlayer);
                WriteStartPlayer(w, e);
                var msg = Proto.End(w);
                session.Broadcast(msg);
                var r = new BinaryReader(new MemoryStream(msg));
                r.ReadByte();
                OnPracticePlayer(r);
            }
        }

        /// Host and clients: put the new character in where the old one stood, facing the same way, at full health.
        internal void OnPracticePlayer(BinaryReader r)
        {
            var p = ReadStartPlayer(r);
            Player old;
            if (!HangoutOn || !Running || !Players.TryGetValue(p.Id, out old))
                return;
            p.Pos = old.Pos;
            p.Disp = old.Disp;
            p.Aim = old.Aim;
            Players[p.Id] = p;
            stands.Remove(p.Id);  // (its PvP stand-in is made afresh for the new body: Pvp.cs)
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
        /// Open the Practice room from the lobby, if it isn't up yet. The host starts it for the whole party (nobody has to be ready, and
        /// Ready marks stay as they are); each player's own screen decides whether it looks at it. A client asks the host.
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
            ApplyMatchSetup();
            Game.HangoutStart = true;
            try { Game.HostStart(); }
            finally { Game.HangoutStart = false; }  // (never left on: the next START would open as Practice)
        }
    }
}
