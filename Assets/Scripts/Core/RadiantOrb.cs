using System;

namespace Arena
{
    /// Radiant Orb (Elementalist Healer): hold fire and an orb of light flies out from you to your cursor and stays on
    /// it for as long as you hold, so you can drag it through enemies (it burns them) and over allies (it heals them).
    /// Let go and it fades. The host moves it (TickRadiantOrb); everyone gets it in the snapshot (Player.RadiantOn/RadiantPos).
    public partial class Game
    {
        public const float RadiantRadius = 20f, RadiantSpeed = 750f;
        const float RadiantTick = 0.2f, RadiantHeal = 4f;  // a hit (the weapon's Damage) and a heal on everything it touches, 5 times a second

        /// How far from you the orb can go: the weapon's Reach (Velocity Rounds stretch it).
        public float RadiantReach(Player p) => p.Weapon.Reach * p.Stats.ShotSpeedMult;

        /// Where the orb is headed: your cursor, held within its reach of you.
        V2 RadiantGoal(Player p, V2 aimAt) => p.Pos + (aimAt - p.Pos).LimitLength(RadiantReach(p));

        /// Host, from TickPlayers: casting the orb, moving it toward the cursor, and its touch.
        void TickRadiantOrb(Player p, float dt)
        {
            bool hold = p.Firing && !p.Downed && !p.ParryActive && State != MatchState.GameOver;
            if (!hold)
            {
                if (p.RadiantOn)
                    BroadcastFx(FxKind.Spark, p.RadiantPos, Defs.BulletColor(Defs.HolyShot), RadiantRadius + 10f, 0.3f, Sfx.None);  // it fades
                p.RadiantOn = false;
                return;
            }
            if (!p.RadiantOn)
            {
                p.RadiantOn = true;
                p.RadiantPos = p.Pos + V2.FromAngle(p.Aim) * p.Def.BodyR;  // cast from you
                p.FireCd = 0f;
                Sounds.Add(new SoundCue { Sfx = Sfx.SpellHoly, Ally = p.Id != MyId });
            }
            p.RadiantPos += (RadiantGoal(p, p.AimAt) - p.RadiantPos).LimitLength(RadiantSpeed * p.Stats.ShotSpeedMult * dt);
            p.RadiantPos = p.Pos + (p.RadiantPos - p.Pos).LimitLength(RadiantReach(p));  // it never strays past its reach, even as you move
            if (p.FireCd > 0f)
                return;
            p.FireCd = RadiantTick / FireRate(p);
            float r = RadiantRadius * AreaMult(p), dmg = p.Weapon.Damage * p.Stats.DmgMult;
            bool hit = false;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f || e.Pos.DistanceTo(p.RadiantPos) > r + e.Radius)
                    continue;
                hit = true;
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = (e.Pos - p.RadiantPos).Normalized(), Owner = p.Id, Dmg = dmg, Kind = Defs.HolyShot }, e);  // weapon mods ride it
            }
            foreach (var q in Players.Values)
                if (q != p && !q.Downed && q.Pos.DistanceTo(p.RadiantPos) <= r + q.Def.BodyR)
                    Heal(q, RadiantHeal * p.Stats.DmgMult, p);
            p.Run.ShotsFired++;
            if (hit)
                p.Run.ShotsHit++;
        }

        /// Every screen: where to draw each orb. Your own follows your cursor here as the host's does (so it doesn't
        /// wait on snapshots); others chase where the host has them.
        public void StepRadiantDisplay(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (IsHost || !p.RadiantOn)
                {
                    p.RadiantDisp = p.RadiantOn ? p.RadiantPos : p.Pos;  // the host has the real one; a new orb starts from its caster
                    continue;
                }
                V2 goal = p.Id == MyId ? RadiantGoal(p, p.AimAt) : p.RadiantPos;
                p.RadiantDisp += (goal - p.RadiantDisp).LimitLength(RadiantSpeed * p.Stats.ShotSpeedMult * dt);
            }
        }
    }
}
