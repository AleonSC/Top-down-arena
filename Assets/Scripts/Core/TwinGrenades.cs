using System;

namespace Arena
{
    /// The Twin Rifle's grenade tubes (Docs/Mockups/twin-rifle-grenade-rework.html: Twin Tubes, loaded like the Cluster Mag). A launcher
    /// sits under each rifle. Rifle hits load them: every TwinNadeHits (40) hits (a crossfire hit counts twice) loads a grenade, two at most
    /// (Player.TwinNades). The weapon-action key (Left Alt) fires one grenade at a time and the tubes take turns (Player.TwinNext,
    /// 0 = left, 1 = right): with two loaded it fires the left, and the next press fires the right, even if you reloaded the left in
    /// between. With one loaded, that one is in the tube TwinNext names; reloading fills the other. The grenade leaves its own tube and
    /// flies down that rifle's stream line to the crossing point (your cursor's distance, as the streams cross), then bursts like the
    /// old Cluster Grenade: TwinNadeDamage in GrenadeRadius and 8 shards (Combat.cs ExplodeGrenade).
    /// Your game sends Msg.WeaponDash (DashNade) with your aim; the host checks the stock and the short cooldown and fires it.
    /// Replicated: TwinNades, TwinNext, TwinCharge (hits toward the next grenade), drawn as tubes on the guns and pips behind you.
    public partial class Game
    {
        public const int TwinNadeMax = 2, TwinNadeHits = 40;
        public const float TwinNadeDamage = 60f, TwinNadeFlight = 0.45f, TwinNadeCd = 0.25f;

        /// Which tube a loaded grenade sits in, for the screen: with two, both; with one, the one TwinNext names.
        public static bool TwinTubeLoaded(Player p, int tube) => p.TwinNades >= 2 || p.TwinNades == 1 && p.TwinNext == tube;

        /// Where tube `tube` (0 left, 1 right) fires from: the mouth of the tube under that rifle, just behind and outside its
        /// muzzle (the guns are held out wide, Weapons.cs TwinMuzzleOut). Against a wall, from your edge.
        public V2 TwinTubeMuzzle(Player p, float aim, int tube)
        {
            V2 v = V2.FromAngle(aim), left = v.Orthogonal();  // (arena y runs down the screen, so Orthogonal is to your left)
            V2 lat = left * ((tube == 0 ? 1f : -1f) * (TwinBarrelGap * 0.5f + TwinTubeOut));
            V2 at = p.Pos + v * (p.Def.BodyR + TwinMuzzleOut - 2f) + lat;
            if (ActiveLayout != null && SegBlocked(ActiveLayout, p.Pos, at, 0f))
                at = p.Pos + v * p.Def.BodyR + lat;
            return at;
        }

        /// How far outside its rifle's barrel a tube's mouth sits.
        const float TwinTubeOut = 4f;

        /// Host: a Twin Rifle round hit an enemy (Combat.cs, after the crossfire check). Loads the tubes.
        void TwinRifleHit(Bullet b, Enemy e)
        {
            Player p;
            if (!b.Crossfire || !Players.TryGetValue(b.Owner, out p) || p.Weapon.Id != WeaponId.TwinRifle || p.TwinNades >= TwinNadeMax)
                return;
            int charge = p.TwinCharge + (CrossfireMult(b, e) > 1f ? 2 : 1);
            if (charge >= TwinNadeHits)
            {
                charge -= TwinNadeHits;
                p.TwinNades++;
                if (p.TwinNades >= TwinNadeMax)
                    charge = 0;
                BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.GrenadeShot), 24f, 0.25f, Sfx.Pick);
            }
            p.TwinCharge = (byte)charge;
        }

        /// Host: a player pressed the weapon-action key with the Twin Rifle, aiming at `aimAt`.
        void HostTwinGrenade(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.TwinRifle || p.Downed || p.StunT > 0f || p.TwinNadeCd > 0f || p.TwinNades == 0 || State == MatchState.GameOver)
                return;
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            float dist = Math.Max(TwinCrossMin, Math.Min(p.Weapon.Speed * p.Weapon.Life * p.Stats.ShotSpeedMult, toward.Length));
            V2 cross = ClampToArena(p.Pos + V2.FromAngle(aim) * dist, 10f);
            int tube = p.TwinNext;
            V2 from = TwinTubeMuzzle(p, aim, tube);
            var g = SpawnBullet(Defs.GrenadeShot, from, (cross - from) / TwinNadeFlight, 0f, TwinNadeFlight, TwinNadeDamage * p.Stats.DmgMult, p.Id);
            g.Splash = GrenadeRadius;
            g.Burst = 8;
            p.TwinNades--;
            p.TwinNext = (byte)(1 - tube);  // the tubes take turns, however they were refilled
            p.TwinNadeCd = TwinNadeCd;
            BroadcastFx(FxKind.Ring, from, Defs.BulletColor(Defs.GrenadeShot), 16f, 0.2f, Sfx.Throw);
        }

        /// Host, every tick: the tubes' cooldown runs down, and they empty when the rifle is put away.
        void TickTwinRifle(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.TwinNadeCd = Math.Max(0f, p.TwinNadeCd - dt);
                if (p.Weapon.Id != WeaponId.TwinRifle)
                {
                    p.TwinNades = 0;
                    p.TwinCharge = 0;
                    p.TwinNext = 0;
                }
            }
        }
    }
}
