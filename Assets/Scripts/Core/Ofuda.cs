using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Healer Ninja's Ofuda (mockup: the "Ofuda Rework" artifact).
    ///   Fire: a Healing Talisman (Defs.OfudaShot, Bullet.Talisman) flies straight, TalismanRange out. Over the last
    ///     TalismanHomeLate of its flight it bends (TalismanTurn) toward the most hurt teammate within TalismanHomeR
    ///     (SteerTalisman; clients get the course through Bullet.Pulled). It bursts on a teammate (+TalismanHeal to them),
    ///     on an enemy, or where its flight ends: TalismanBurstR round it, teammates (you too) heal TalismanBurstHeal,
    ///     enemies are shoved TalismanShove (not heavies) and small enemy shots are erased. It deals no damage.
    ///   Alt (Msg.WeaponDash WeaponAlt.Ofuda): a Blast Ofuda (Defs.BlastOfudaShot), straight, no homing, BlastRange out, its own
    ///     BlastCd cooldown. It sticks to the first enemy it touches and blows BlastFuse later; one that hits nothing lands
    ///     and blows BlastGroundFuse later. The stuck or landed charm is a ZoneKind.BlastOfuda (Zone.Side: the enemy it
    ///     rides, replicated; 0 on the ground). The blast: BlastR round it, the weapon's Damage, BlastEdge of it at the rim,
    ///     weapon mods ride it. An enemy that dies under a charm drops it where it stood, still lit.
    ///   Extra projectiles: one more talisman and one more blast charm, fanned. Homing Rounds and Blessed Paper make the
    ///     burst and the blast wider; Blessed Paper heals more.
    /// Look: WorldShots.cs (both charms in flight), WorldEnemies.cs (the stuck and landed charm), TankAltLook.cs (the cooldown).
    public partial class Game
    {
        public const float TalismanRange = 420f, TalismanHomeLate = 0.35f, TalismanHomeR = 150f, TalismanTurn = 7f;
        public const float TalismanHeal = 10f, TalismanBurstR = 55f, TalismanBurstHeal = 5f, TalismanShove = 90f;
        public const float BlastSpeed = 460f, BlastRange = 380f, BlastFuse = 0.8f, BlastGroundFuse = 1.5f, BlastR = 75f, BlastEdge = 0.6f, BlastCd = 1.1f;
        public const float OfudaFan = 0.4f;  // radians between fanned charms (extra projectiles)
        public static readonly Rgba TalismanGreen = new Rgba(0.44f, 0.94f, 0.63f), OfudaRed = new Rgba(1f, 0.35f, 0.3f);

        public static float TalismanBurstOf(Player p) => TalismanBurstR * AreaMult(p) * p.Stats.CharmBurstMult;
        public static float BlastROf(Player p) => BlastR * AreaMult(p) * p.Stats.CharmBurstMult;

        /// Fire with the Ofuda: n Healing Talismans, fanned.
        void FireTalismans(Player p, float aim, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            int n = 1 + extra;
            for (int i = 0; i < n; i++)
            {
                var b = SpawnBullet(Defs.OfudaShot, muzzle, V2.FromAngle(Fanned(aim, i, n, OfudaFan)) * w.Speed, 0f, TalismanRange * PierceReach(p) / w.Speed, w.Damage * dmg, p.Id);
                b.Talisman = true;
                b.Burst = 1;  // bursts where its flight ends (MoveBullets' landed list)
            }
        }

        /// Host, every tick: a talisman late in its flight bends toward the most hurt teammate near it.
        void SteerTalisman(Bullet b, float dt)
        {
            if (b.Life > b.Life0 * TalismanHomeLate)
                return;
            Player best = null;
            float bestV = float.MaxValue;
            foreach (var q in Players.Values)
            {
                if (q.Id == b.Owner || q.Downed || q.Hp >= q.MaxHp)
                    continue;
                float d = q.Pos.DistanceTo(b.Pos);
                if (d > TalismanHomeR)
                    continue;
                float v = q.Hp / q.MaxHp * 200f + d;  // the most hurt first, the nearer of two alike
                if (v < bestV)
                {
                    bestV = v;
                    best = q;
                }
            }
            if (best == null)
                return;
            float diff = MathX.WrapPi((best.Pos - b.Pos).Angle - b.Vel.Angle);
            float turn = Math.Max(-TalismanTurn * dt, Math.Min(TalismanTurn * dt, diff));
            if (Math.Abs(turn) < 1e-4f)
                return;
            b.Vel = b.Vel.Rotated(turn);
            b.Pulled = true;  // the host sends its new course
        }

        readonly List<KeyValuePair<Bullet, Player>> talismanBursts = new List<KeyValuePair<Bullet, Player>>();

        /// Host, in the shot loop: a talisman touching a teammate or an enemy bursts (after the loop: SpendTalismans).
        /// True when it is spent.
        bool TalismanTouches(Bullet b)
        {
            foreach (var q in Players.Values)
                if (q.Id != b.Owner && !q.Downed && b.Pos.WithinReach(q.Pos, q.Def.BodyR + b.Radius))
                {
                    talismanBursts.Add(new KeyValuePair<Bullet, Player>(b, q));
                    return true;
                }
            foreach (var e in AllFoes(by: b.Owner))
                if (Targetable(e) && b.Pos.WithinReach(e.Pos, e.Radius + b.Radius))
                {
                    talismanBursts.Add(new KeyValuePair<Bullet, Player>(b, null));
                    return true;
                }
            return false;
        }

        /// After the shot loop: the talismans that touched something burst.
        void SpendTalismans()
        {
            foreach (var kv in talismanBursts)
                TalismanBurst(kv.Key, kv.Value);
            talismanBursts.Clear();
        }

        /// A talisman bursts at b.Pos: `hit` (the teammate it touched, if any) gets TalismanHeal, everyone in the burst
        /// TalismanBurstHeal; enemies in it are shoved, small enemy shots erased.
        void TalismanBurst(Bullet b, Player hit)
        {
            var owner = PlayerOrNull(b.Owner);
            float r = owner != null ? TalismanBurstOf(owner) : TalismanBurstR;
            float heal = (owner != null ? owner.Stats.CharmHealMult : 1f) * b.Dmg / Math.Max(1f, Defs.WeaponOf(WeaponId.Ofuda).Damage);
            if (hit != null)
                Heal(hit, TalismanHeal * heal, owner);
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.WithinReach(b.Pos, r + q.Def.BodyR))
                    Heal(q, TalismanBurstHeal * heal, owner);
            foreach (var e in AllFoes(by: b.Owner))
                if (Targetable(e) && !Heavy(e) && e.Pos.WithinReach(b.Pos, r + e.Radius))
                    ShoveFrom(e, b.Pos, TalismanShove, b.Vel.Normalized());  // Knock.cs
            CutShotsNear(b.Pos, r);  // small enemy shots (TwinBlades.cs)
            BroadcastFx(FxKind.TalismanBurst, b.Pos, TalismanGreen, r, 0.45f, Sfx.SpellHoly);
            if (owner != null && hit != null)
                owner.Run.ShotsHit++;
        }

        // --- Alt: the Blast Ofuda ---------------------------------------------------------------

        float ofudaLocalCd;  // your own game: until the Alt can go again (the cooldown ring under your feet)
        public float OfudaLocalCd => ofudaLocalCd;

        /// Your own game: Alt with the Ofuda. True when the weapon is out (the Alt is its own).
        bool LocalOfudaBlast(Player me, bool pressed, V2 aimAt, float dt)
        {
            ofudaLocalCd = Math.Max(0f, ofudaLocalCd - dt);
            if (me.Weapon.Id != WeaponId.Ofuda)
                return false;
            if (pressed && ofudaLocalCd <= 0f)
            {
                ofudaLocalCd = BlastCd;
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.Ofuda, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.Ofuda);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        /// Host: Alt pressed with the Ofuda: Blast Ofudas fly at the cursor, if the cooldown is done.
        void HostOfudaBlast(Player p, V2 target)
        {
            if (p.Weapon.Id != WeaponId.Ofuda || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.OfudaCd > 0f || State == MatchState.GameOver)
                return;
            p.OfudaCd = BlastCd;
            V2 to = target - p.Pos;
            float aim = to.Length > 1f ? to.Angle : p.Aim;
            V2 muzzle = p.Pos + V2.FromAngle(aim) * (p.Def.BodyR + 4f);
            int n = 1 + ExtraShots(p);
            float dmg = p.Weapon.Damage * p.Stats.DmgMult;
            bool was = firingWeapon;
            firingWeapon = true;  // counted as the weapon's shots (accuracy, mods)
            for (int i = 0; i < n; i++)
            {
                var b = SpawnBullet(Defs.BlastOfudaShot, muzzle, V2.FromAngle(Fanned(aim, i, n, OfudaFan)) * BlastSpeed, 0f, BlastRange * PierceReach(p) / BlastSpeed, dmg, p.Id);
                b.Burst = 1;  // lands where its flight ends (MoveBullets' landed list)
            }
            firingWeapon = was;
            BroadcastSound(Sfx.ShotKunai);
        }

        /// Host, in the shot loop: a Blast Ofuda touching an enemy sticks to it. True when it is spent.
        bool BlastOfudaTouches(Bullet b)
        {
            foreach (var e in AllFoes(by: b.Owner))
                if (Targetable(e) && b.Pos.WithinReach(e.Pos, e.Radius + b.Radius))
                {
                    PlaceBlastOfuda(b, e);
                    return true;
                }
            return false;
        }

        /// A Blast Ofuda stops: stuck to `on`, or on the ground (null). It blows when its fuse runs out (TickBlastOfudas).
        void PlaceBlastOfuda(Bullet b, Enemy on)
        {
            var p = PlayerOrNull(b.Owner);
            if (p == null)
                return;
            V2 at = on != null ? on.Pos + (b.Pos - on.Pos).Normalized() * on.Radius * 0.8f : ClampToArena(b.Pos, 10f);
            var z = AddZone(ZoneKind.BlastOfuda, p, at, BlastROf(p), on != null ? BlastFuse : BlastGroundFuse);
            z.DmgMult = b.Dmg / Math.Max(1f, Defs.WeaponOf(WeaponId.Ofuda).Damage);  // the charm's damage, upgrades and all
            z.Aim = b.Vel.Angle;
            z.Side = on != null ? on.Id : 0;
            if (on != null)
                z.Origin = at - on.Pos;  // where on the enemy it rides
            BroadcastFx(FxKind.Ring, at, OfudaRed, 12f, 0.25f, Sfx.None);
        }

        readonly List<Zone> blownOfudas = new List<Zone>();

        /// Host, every tick (before zones age): stuck charms ride their enemy; charms whose fuse runs out this tick blow.
        void TickBlastOfudas(float dt)
        {
            foreach (var p in Players.Values)
                p.OfudaCd = Math.Max(0f, p.OfudaCd - dt);
            blownOfudas.Clear();
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.BlastOfuda)
                    continue;
                if (z.Side != 0)
                {
                    Enemy e;
                    if (Enemies.TryGetValue(z.Side, out e) && e.Hp > 0f)
                        z.Pos = e.Pos + z.Origin;
                    else
                        z.Side = 0;  // its enemy died: the charm drops where it was, still lit
                }
                if (z.T <= dt)
                    blownOfudas.Add(z);
            }
            foreach (var z in blownOfudas)
            {
                BlowOfuda(z);
                Zones.Remove(z.Id);
            }
        }

        /// A Blast Ofuda blows: the weapon's damage round it, BlastEdge of it at the rim.
        void BlowOfuda(Zone z)
        {
            float dmg = Defs.WeaponOf(WeaponId.Ofuda).Damage * z.DmgMult;
            foreach (var e in FoesInCircle(z.Pos, z.Radius, by: z.Owner))
            {
                float f = e.Id == z.Side ? 1f : 1f - (1f - BlastEdge) * Math.Min(1f, e.Pos.DistanceTo(z.Pos) / Math.Max(1f, z.Radius));
                WeaponHurt(e, dmg * f, z.Owner);
                ModHit(e, z.Owner, dmg * f, Defs.BlastOfudaShot, default(V2));  // weapon mods ride the blast
            }
            V2 at = z.Pos;
            float r = z.Radius;
            MeleeHitRoomTurrets(dmg, (q, lr) => q.WithinReach(at, r + lr));  // room turrets in the blast (RoomTraps.cs)
            PropBlast(z.Pos, z.Radius, z.Owner);
            var owner = PlayerOrNull(z.Owner);
            if (owner != null)
                owner.Run.ShotsHit++;
            BroadcastFx(FxKind.Boom, z.Pos, OfudaRed, z.Radius, 0.45f, Sfx.Explode);
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): a Blast Ofuda at what it shoots, in reach
        static bool OfudaBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            return !late && v.ShootAt != null && v.ShootD < BlastRange && g.Tap(g.OfudaLocalCd <= 0f, 1f);
        }
    }
}
