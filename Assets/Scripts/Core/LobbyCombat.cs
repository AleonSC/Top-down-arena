using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A shot flying through the Hangout room. Every screen flies its own copy for drawing; only the host applies damage.
    public class LobbyShot
    {
        public int Owner;
        public V2 Pos, Vel;
        public float Life, Dmg;
    }

    /// A shot that landed: a short ring to draw where it hit.
    public struct LobbyHit
    {
        public V2 Pos;
        public float Age;
    }

    /// Shooting in the Hangout (Docs/Hangout.md). A deliberately light fight: everyone has RoomHp health and each weapon fires one plain
    /// shot at its own rate, speed and reach (the loadout's numbers, nothing special: no abilities, pellets, beams or swings). The host
    /// decides every hit. PvP is a per-player switch that starts off: with it off you can't fire and shots pass through you.
    /// Nobody dies for real: at 0 health a character is knocked out for DownSeconds, then returns to a spawn spot at full health.
    public partial class Session
    {
        public const float RoomHp = 100f, DownSeconds = 2.5f, RegenDelay = 3f, RegenPerSecond = 15f;
        public const float ShotRadius = 5f, AvatarRadius = 20f;

        public readonly List<LobbyShot> Shots = new List<LobbyShot>();
        public readonly List<LobbyHit> Hits = new List<LobbyHit>();
        double lastCombatStep = -1;

        /// What a weapon's Hangout shot is like, from its stats: the fire interval, the shot's speed and lifetime (so its reach), and the damage
        /// one hit does (about 20 a second of fire whatever the weapon, so every loadout fights evenly).
        public static void ShotStats(LobbyEntry e, out float interval, out float speed, out float life, out float dmg)
        {
            var w = Defs.Weapon(e.Role, e.Kit, e.Weapon);
            interval = w != null && w.FireInterval > 0f ? Math.Max(0.08f, Math.Min(1.2f, w.FireInterval)) : 0.25f;
            speed = w != null && w.Speed > 1f && !w.Melee ? Math.Max(350f, Math.Min(1100f, w.Speed)) : 650f;
            life = w != null && w.Life > 0f && !w.Melee ? Math.Max(0.25f, Math.Min(1f, w.Life)) : 0.3f;  // (a melee weapon's "shot" is a short lash)
            dmg = Math.Max(3f, Math.Min(20f, 20f * interval));
        }

        /// Your own PvP switch. Turning it off also heals you, so nobody is stuck hurt and unable to fight back.
        public void SetMyPvp(bool on)
        {
            Avatar a;
            if (Avatars.TryGetValue(MyId, out a))
                SetPvp(a, on);
        }

        void SetPvp(Avatar a, bool on)
        {
            if (a.Pvp == on)
                return;
            a.Pvp = on;
            if (!on)
            {
                a.Hp = RoomHp;
                a.Down = 0f;
                a.SentHp = (byte)RoomHp;
            }
            avatarsDirty = true;
        }

        /// Fire your own weapon at this angle (the Hangout calls it while the button is held). False when it can't: PvP off, knocked out, not ready yet.
        public bool FireMyShot(float aim)
        {
            Avatar a;
            LobbyEntry e;
            if (Game.Running || !Avatars.TryGetValue(MyId, out a) || !Lobby.TryGetValue(MyId, out e) || !a.Pvp || a.Hp <= 0f)
                return false;
            double now = avatarClock.Elapsed.TotalSeconds;
            if (now < a.NextFire || float.IsNaN(aim) || float.IsInfinity(aim))
                return false;
            float interval, speed, life, dmg;
            ShotStats(e, out interval, out speed, out life, out dmg);
            a.NextFire = now + interval;
            if (IsHost)
                HostFire(MyId, aim, false);
            else
            {
                AddShot(MyId, a.Pos, aim, e);  // (at once on our own screen; the host's echo of it is skipped)
                var w = Proto.Begin(Msg.LobbyFire);
                w.Write(aim);
                SendToHost(Proto.End(w));
            }
            return true;
        }

        void AddShot(int owner, V2 from, float aim, LobbyEntry e)
        {
            float interval, speed, life, dmg;
            ShotStats(e, out interval, out speed, out life, out dmg);
            var dir = V2.FromAngle(aim);
            Shots.Add(new LobbyShot { Owner = owner, Pos = from + dir * 24f, Vel = dir * speed, Life = life, Dmg = dmg });
        }

        /// The host: a player fired (a client's message, checked here, or the host's own, already checked).
        void HostFire(int id, float aim, bool check)
        {
            Avatar a;
            LobbyEntry e;
            if (Game.Running || !Avatars.TryGetValue(id, out a) || !Lobby.TryGetValue(id, out e) || !a.Pvp || a.Hp <= 0f || float.IsNaN(aim) || float.IsInfinity(aim))
                return;
            if (check)
            {
                float interval, speed, life, dmg;
                ShotStats(e, out interval, out speed, out life, out dmg);
                double now = avatarClock.Elapsed.TotalSeconds;
                if (now < a.NextFire - 0.06)
                    return;  // faster than its weapon allows
                a.NextFire = Math.Max(a.NextFire, now + interval * 0.9);
            }
            AddShot(id, a.Pos, aim, e);
            if (Net != null)
            {
                var w = Proto.Begin(Msg.LobbyShot);
                w.Write(id);
                w.Write(a.Pos);
                w.Write(aim);
                Broadcast(Proto.End(w));
            }
        }

        /// A client: the host says someone fired.
        void ClientLobbyShot(BinaryReader r)
        {
            int owner = r.ReadInt32();
            var pos = r.ReadV2();
            float aim = r.ReadSingle();
            LobbyEntry e;
            if (owner == MyId || !Lobby.TryGetValue(owner, out e) || float.IsNaN(aim) || float.IsInfinity(aim) || float.IsNaN(pos.X) || float.IsNaN(pos.Y))
                return;
            AddShot(owner, pos, aim, e);
        }

        /// Everyone: fly the shots and check what they hit; the host also deals the damage, regrows health and respawns the knocked out.
        /// Called from Poll, so it runs whatever tab a screen is on.
        void StepLobbyCombat()
        {
            double now = avatarClock.Elapsed.TotalSeconds;
            float dt = lastCombatStep < 0 ? 0f : (float)Math.Min(0.1, now - lastCombatStep);
            lastCombatStep = now;
            if (Screen != AppScreen.Lobby || (Shots.Count == 0 && Hits.Count == 0 && !IsHost))
                return;
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                s.Pos += s.Vel * dt;
                s.Life -= dt;
                bool gone = s.Life <= 0f || s.Pos.X < 0f || s.Pos.Y < 0f || s.Pos.X > RoomW || s.Pos.Y > RoomH;
                if (!gone)
                    foreach (var kv in Avatars)
                    {
                        var t = kv.Value;
                        if (kv.Key == s.Owner || !t.Pvp || t.Hp <= 0f || ((IsHost ? t.Pos : t.Disp) - s.Pos).LengthSq > (AvatarRadius + ShotRadius) * (AvatarRadius + ShotRadius))
                            continue;
                        Hits.Add(new LobbyHit { Pos = s.Pos });
                        if (IsHost)
                            HurtAvatar(t, s.Dmg, now);
                        gone = true;
                        break;
                    }
                if (gone)
                    Shots.RemoveAt(i);
            }
            for (int i = Hits.Count - 1; i >= 0; i--)
            {
                var h = Hits[i];
                h.Age += dt;
                if (h.Age > 0.3f)
                    Hits.RemoveAt(i);
                else
                    Hits[i] = h;
            }
            if (!IsHost)
                return;
            int slot = 0;
            foreach (var kv in Avatars)
            {
                var a = kv.Value;
                if (a.Pvp && a.Hp <= 0f)
                {
                    a.Down -= dt;
                    if (a.Down <= 0f)
                    {
                        a.Hp = RoomHp;
                        a.Pos = a.Disp = SpawnSpot(slot);
                        a.Respawns++;
                        a.SentHp = 0;
                        avatarsDirty = true;
                    }
                }
                else if (a.Pvp && a.Hp < RoomHp && now - a.LastHit > RegenDelay)
                {
                    a.Hp = Math.Min(RoomHp, a.Hp + RegenPerSecond * dt);
                    if ((byte)Math.Round(a.Hp) != a.SentHp)
                        avatarsDirty = true;
                }
                slot++;
            }
        }

        void HurtAvatar(Avatar a, float dmg, double now)
        {
            a.Hp = Math.Max(0f, a.Hp - dmg);
            a.LastHit = now;
            if (a.Hp <= 0f)
                a.Down = DownSeconds;
            avatarsDirty = true;
        }
    }
}
