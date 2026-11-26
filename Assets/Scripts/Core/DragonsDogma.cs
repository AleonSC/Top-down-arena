using System;
using System.Collections.Generic;

namespace Arena
{
    /// Dragon's Dogma: the Tank Ninja's second ultimate (`AbilityId.DragonsDogma`, key 1 of the lobby's Ultimate row; Thousand
    /// Clones stays the default). A great spirit dragon pours out of the Ninja and hunts across the arena for DogmaTime (5 s,
    /// 7 s mastered), then coils back into them.
    ///
    /// Host: the head flies at DogmaSpeed and turns at most DogmaTurn radians a second (so it swoops and loops rather than
    /// darting; harder within DogmaCloseIn of its goal, so it never circles it), after the enemy nearest it (`Player.DogmaTgt`); with nobody about it circles its Ninja. Any enemy its head
    /// reaches (DogmaBiteR beyond the enemy's edge) is bitten:
    ///   - a regular enemy is devoured: killed outright, whatever its health;
    ///   - a boss or a heavy enemy takes DogmaBigBite (150, times the Ninja's damage multiplier), at most once every
    ///     DogmaBiteEvery per enemy, and the dragon goes after someone else meanwhile.
    /// Its head also swallows enemy bullets within DogmaGulpR. In its last DogmaReturn seconds it turns for home and ends when
    /// it reaches its Ninja; it also ends if the Ninja goes down or the wave is over. Mastered: 7 s, and every enemy devoured
    /// heals the Ninja for DogmaHeal (3%) of their max health.
    ///
    /// Replicated: `Player.DogmaOn`, and while it's on, `DogmaHead` and `DogmaTgt`; each bite sends FxKind.DragonBite. Every game
    /// keeps the body's trail itself (UpdateDogmaView: the head eased on screen and the points it has passed), so the look
    /// (Game/DragonsDogmaLook.cs) lays the body along where the head has really been.
    public partial class Game
    {
        public const float DogmaTime = 5f, DogmaTimeMastered = 7f;
        public const float DogmaSpeed = 760f, DogmaTurn = 6.5f;      // px a second; radians a second at most
        public const float DogmaCloseIn = 240f;                       // nearer its goal than this it turns harder (DogmaTurn * this / distance)
        public const float DogmaEmerge = 0.3f;                        // it pours out at half speed this long
        public const float DogmaReturn = 0.7f;                        // its last seconds: back to the Ninja
        public const float DogmaBiteR = 30f, DogmaGulpR = 40f;        // reach past an enemy's edge; bullets swallowed round the head
        public const float DogmaBigBite = 150f, DogmaBiteEvery = 1.2f;  // a boss or heavy: its bite, and how often per enemy (it hunts others meanwhile)
        public const float DogmaHeal = 0.03f;                         // mastered: each enemy devoured heals this share of max health
        public const float DogmaHomeR = 36f;                          // back this close to the Ninja: it's gone in
        public const float DogmaLen = 680f;                           // the body's length on screen (the look; the trail keeps this much)
        public const float DogmaBiteFx = 0.9f;
        public const int DogmaBotCrowd = 4;                           // a bot casts it with this many enemies near
        public static readonly Rgba DogmaBlue = new Rgba(0.32f, 0.66f, 1f), DogmaGold = new Rgba(1f, 0.83f, 0.44f);

        /// Its entry: key 1 of the tank Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] DogmaUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.DragonsDogma, Kit = Kit.Ninja, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Dragon's Dogma", Cooldown = 70f,
                Desc = "A spirit dragon hunts across the arena, devouring enemies and their shots.",
                Capstone = "Hunts for 7 s, and every enemy it devours heals you 3%.",
                Bot = b => b.NearEnemies >= DogmaBotCrowd,
            },
        };

        readonly Dictionary<int, float> dogmaBit = new Dictionary<int, float>();  // host: (player id * 100000 + enemy id) -> seconds until it can bite that big one again
        readonly List<int> dogmaBitKeys = new List<int>();

        void CastDogma(Player p, bool mastered)
        {
            float time = mastered ? DogmaTimeMastered : DogmaTime;
            p.UltT = time;
            p.DogmaOn = true;
            p.DogmaAge = 0f;
            p.DogmaHead = p.Pos;
            p.DogmaAng = p.Aim;
            p.DogmaTgt = 0;
            p.DogmaHealOn = mastered;
            BroadcastFx(FxKind.Ring, p.Pos, DogmaGold, 70f, 0.5f, Sfx.DogmaRoar);
        }

        void EndDogma(Player p)
        {
            p.DogmaOn = false;
            p.DogmaTgt = 0;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.DragonsDogma)
                p.UltT = 0f;
        }

        /// Host, every tick (after the shots have moved and hit).
        void TickDogmas(float dt)
        {
            if (dogmaBit.Count > 0)
            {
                dogmaBitKeys.Clear();
                dogmaBitKeys.AddRange(dogmaBit.Keys);
                foreach (int k in dogmaBitKeys)
                {
                    float left = dogmaBit[k] - dt;
                    if (left <= 0f) dogmaBit.Remove(k);
                    else dogmaBit[k] = left;
                }
            }
            foreach (var p in Players.Values)
            {
                if (!p.DogmaOn)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.DragonsDogma)
                {
                    EndDogma(p);
                    continue;
                }
                p.DogmaAge += dt;
                bool home = p.UltT <= DogmaReturn;
                V2 goal;
                Enemy tgt = null;
                if (home)
                {
                    goal = p.Pos;
                    p.DogmaTgt = 0;
                    if (p.DogmaHead.CloserThan(p.Pos, DogmaHomeR) || p.UltT <= 0f)
                    {
                        BroadcastSound(Sfx.DogmaHome);  // (DogmaSoundBank.cs)
                        EndDogma(p);
                        continue;
                    }
                }
                else
                {
                    if (p.DogmaTgt == 0 || !TryFoe(p.DogmaTgt, out tgt) || !Targetable(tgt) || dogmaBit.ContainsKey(p.Id * 100000 + tgt.Id))
                        tgt = DogmaPick(p);
                    p.DogmaTgt = tgt != null ? tgt.Id : 0;
                    goal = tgt != null ? tgt.Pos : p.Pos + V2.FromAngle(p.DogmaAge * 2.2f) * 170f;  // nobody about: circle the Ninja
                }
                // Steer: turn toward the goal at most DogmaTurn a second, fly on at speed (faster home, so it makes it).
                float want = (goal - p.DogmaHead).Angle;
                float diff = want - p.DogmaAng;
                diff = MathX.WrapPi(diff);
                float near = Math.Min(4f, Math.Max(1f, DogmaCloseIn / Math.Max(1f, (goal - p.DogmaHead).Length)));  // close in: turn harder, so it never circles its prey
                float turn = DogmaTurn * (home ? 1.8f : 1f) * near * dt;
                p.DogmaAng += Math.Max(-turn, Math.Min(turn, diff));
                float speed = DogmaSpeed * (p.DogmaAge < DogmaEmerge ? 0.5f : 1f) * (home ? 1.3f : 1f);
                float step = speed * dt;
                if (home && step >= p.DogmaHead.DistanceTo(p.Pos))
                    p.DogmaHead = p.Pos;
                else
                    p.DogmaHead = p.DogmaHead + V2.FromAngle(p.DogmaAng) * step;
                p.DogmaHead = p.DogmaHead.Clamp(new V2(-200f, -200f), new V2(ArenaMax.X + 200f, ArenaMax.Y + 200f));
                EraseEnemyBullets(p.DogmaHead, DogmaGulpR);
                DogmaBites(p);
            }
        }

        /// The enemy nearest the head, other than a big one it has just bitten (it moves on, and comes back for it).
        Enemy DogmaPick(Player p)
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || dogmaBit.ContainsKey(p.Id * 100000 + e.Id))
                    continue;
                float d = e.Pos.DistanceSq(p.DogmaHead);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        readonly List<Enemy> dogmaReach = new List<Enemy>();

        /// Everything the head reaches this tick: devoured, or bitten if it's big.
        void DogmaBites(Player p)
        {
            dogmaReach.Clear();
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.CloserThan(p.DogmaHead, DogmaBiteR + e.Radius))
                    dogmaReach.Add(e);
            foreach (var e in dogmaReach)
            {
                bool big = e.Def.IsBoss || e.Def.Heavy;
                if (big)
                {
                    int key = p.Id * 100000 + e.Id;
                    if (dogmaBit.ContainsKey(key))
                        continue;
                    dogmaBit[key] = DogmaBiteEvery;
                    HurtEnemy(e, DogmaBigBite * p.Stats.DmgMult, p.Id);
                    p.DogmaBites++;
                }
                else
                {
                    HurtEnemy(e, Math.Max(e.Hp, 1f) * 50f + 1000f, p.Id);  // (enough through any armour)
                    if (e.Hp <= 0f)
                    {
                        p.DogmaEaten++;
                        if (p.DogmaHealOn)
                            Heal(p, p.MaxHp * DogmaHeal, p);
                    }
                }
                BroadcastFx(FxKind.DragonBite, e.Pos, new V2(p.Id, big ? 0f : 1f), e.Def.Color, e.Radius, DogmaBiteFx, big ? Sfx.DogmaBite : Sfx.DogmaDevour);
                if (p.DogmaTgt == e.Id && !big)
                    p.DogmaTgt = 0;
            }
        }

        /// Every game, every frame: the head eased on screen, and the trail of points it has passed (the body is laid
        /// along it). Once it's gone in, the trail drains into the Ninja.
        void UpdateDogmaView(float dt)
        {
            foreach (var p in Players.Values)
            {
                var tr = p.DogmaTrail;
                if (p.DogmaOn)
                {
                    if (tr.Count == 0)
                    {
                        p.DogmaDisp = p.Pos;
                        p.DogmaSince = 0f;
                        p.DogmaSink = 0f;
                        tr.Add(p.DogmaDisp);
                    }
                    p.DogmaSince += dt;
                    V2 to = p.DogmaHead;
                    p.DogmaDisp = IsHost ? to : p.DogmaDisp + (to - p.DogmaDisp) * Math.Min(1f, dt * 18f);
                    if (p.DogmaDisp.FartherThan(tr[tr.Count - 1], 4f))
                        tr.Add(p.DogmaDisp);
                }
                else if (tr.Count > 0)
                {
                    p.DogmaSince += dt;
                    p.DogmaSink += DogmaSpeed * 1.6f * dt;
                    if (p.DogmaSink >= DogmaLen + 40f)
                    {
                        tr.Clear();
                        continue;
                    }
                }
                else
                    continue;
                // keep only what the body covers
                float len = 0f;
                for (int i = tr.Count - 1; i > 0; i--)
                {
                    len += tr[i].DistanceTo(tr[i - 1]);
                    if (len > DogmaLen + 60f)
                    {
                        tr.RemoveRange(0, i - 1);
                        break;
                    }
                }
            }
        }
    }
}
