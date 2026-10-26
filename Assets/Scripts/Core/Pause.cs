using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Pausing. Either player can pause the shared match; the host freezes the
    /// simulation and sends every bullet's exact state when pausing and resuming,
    /// so clients (which move bullets themselves) stop and restart in step.
    public partial class Game
    {
        public bool Paused;
        public int PausedBy;

        /// Called by the front end when the local player opens or closes the pause menu.
        public void RequestPause(bool pause)
        {
            if (!Running)
                return;
            if (IsHost)
            {
                SetPaused(MyId, pause);
            }
            else
            {
                var w = Proto.Begin(Msg.Pause);
                w.Write(pause);
                session.SendToHost(Proto.End(w));
            }
        }

        public void OnPause(int peer, BinaryReader r)
        {
            SetPaused(peer, r.ReadBoolean());
        }

        void SetPaused(int id, bool pause)
        {
            if (!Running || Paused == pause)
                return;
            Paused = pause;
            PausedBy = pause ? id : 0;
            var w = Proto.Begin(Msg.BulletSync);
            w.Write(Paused);
            w.Write(PausedBy);
            w.Write(Bullets.Count);
            foreach (var b in Bullets.Values)
            {
                w.Write(b.Id);
                w.Write(b.Kind);
                w.Write(b.Owner);
                w.Write(b.Pos);
                w.Write(b.Vel);
                w.Write(b.AngVel);
                w.Write(b.Life);
            }
            session.Broadcast(Proto.End(w));
            DoLog(pause ? "paused by " + id : "resumed");
        }

        /// Client: adopt the host's pause state and bullet positions exactly.
        void ReadBulletSync(BinaryReader r)
        {
            Paused = r.ReadBoolean();
            PausedBy = r.ReadInt32();
            Bullets.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var b = new Bullet { Id = r.ReadInt32(), Kind = r.ReadByte(), Owner = r.ReadInt32(), Pos = r.ReadV2(), Vel = r.ReadV2() };
                b.AngVel = r.ReadSingle();
                b.Life = r.ReadSingle();
                b.Radius = Defs.BulletRadius(b.Kind);
                Bullets[b.Id] = b;
            }
        }
    }
}
