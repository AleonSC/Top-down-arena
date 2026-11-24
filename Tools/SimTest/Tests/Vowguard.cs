using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Healer Knight's fourth weapon, the Vowguard (Vowguard.cs, VowguardMotion.cs).
static partial class Program
{
    /// The chains: packed, saved and sent with the run's start to every game; each stance plays its own chain in order and
    /// round again; Valor links light Faith and a finisher spends it; Alt switches stance and both keep their place; Sanctuary
    /// is slower and its shield stops small shots at the front only; the heals, ward and dome; Cover Ally's guard; the parry
    /// starts both chains over; the other screen sees the stance and the places.
    static void VowguardTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);

        // the pack, its line in the profile, and what is refused
        var V = new[] { VgMove.EdgeCut, VgMove.LungeThrust, VgMove.HallowedSlam };
        var S = new[] { VgMove.RallyTap, VgMove.GuardedJab, VgMove.SanctifiedWard };
        var pack = VowguardMotion.Pack(V, S);
        Check(VowguardMotion.Valid(pack) && VowguardMotion.Chain(pack, 0).SequenceEqual(V) && VowguardMotion.Chain(pack, 1).SequenceEqual(S), "a chain pair packs and unpacks");
        Check(VowguardMotion.SamePack(VowguardMotion.FromLine(VowguardMotion.ToLine(pack)), pack), "and goes through the profile's line unchanged (" + VowguardMotion.ToLine(pack) + ")");
        Check(!VowguardMotion.Valid(VowguardMotion.Pack(new[] { VgMove.RaiseGuard }, S)), "a Sanctuary move in the Valor chain is refused");
        Check(!VowguardMotion.Valid(VowguardMotion.Pack(Enumerable.Repeat(VgMove.EdgeCut, 11).ToArray(), S)), "and so is a chain of 11");
        Check(VowguardMotion.Valid(VowguardMotion.Pack(Enumerable.Repeat(VgMove.EdgeCut, 10).ToArray(), S)), "10 is fine");
        Check(VowguardMotion.SamePack(VowguardMotion.FromLine("Nonsense;Junk"), VowguardMotion.DefaultPack), "a broken line loads as the default chains");
        // the drawn motion (Radiant): same ends as the host's poses, a hold on each hit, still done on time
        foreach (var mv in VowguardMotion.Moves)
        {
            VgPose hostA = VowguardMotion.PoseAt(mv, 0f), drawnA = VowguardMotion.ShownPoseAt(mv, 0f), hostB = VowguardMotion.PoseAt(mv, 1f), drawnB = VowguardMotion.ShownPoseAt(mv, 1f);
            bool same = Math.Abs(hostA.Sword - drawnA.Sword) < 0.01f && Math.Abs(hostB.Sword - drawnB.Sword) < 0.01f && Math.Abs(hostB.ShieldD - drawnB.ShieldD) < 0.01f;
            bool stop;
            bool onTime = Math.Abs(VowguardMotion.ShownT(mv, mv.Dur, out stop) - mv.Dur) < 0.001f && VowguardMotion.ShownT(mv, 0f, out stop) == 0f;
            if (!same || !onTime)
                Check(false, mv.Name + "'s drawn motion starts and ends with the host's");
        }
        var cut = VowguardMotion.Of(VgMove.EdgeCut);
        bool held;
        float hitAt = cut.Hits[0].T * cut.Dur, shownMid = VowguardMotion.ShownT(cut, hitAt + 0.03f, out held);
        Check(held && Math.Abs(shownMid - hitAt) < 0.001f, "Edge Cut's drawing holds on its hit (" + shownMid.ToString("0.000") + " s shown at " + (hitAt + 0.03f).ToString("0.000") + ")");
        Check(VowguardMotion.ShownPoseAt(cut, 0.12f).Sword > VowguardMotion.PoseAt(cut, 0.12f).Sword, "and pulls the sword back before the cut");
        foreach (var preset in VowguardMotion.Presets)
            Check(VowguardMotion.Valid(VowguardMotion.Pack(preset[0], preset[1])), "every preset is a valid pair");

        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        var prof = new Profile { VgChain = pack };
        host.Game.ProfileOverride = prof;
        HostAs(host, port, false, "Shield", Role.Healer, 3, 0, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.Vowguard && me.Weapon.Melee && Defs.WeaponCount(Role.Healer, Kit.Knight) == 4, "the Healer Knight's fourth key is the Vowguard (" + me.Weapon.Id + ")");
        Check(VowguardMotion.SamePack(me.VgChain, pack) && VowguardMotion.SamePack(client.Game.Players[1].VgChain, pack), "your chains reach the run on both screens");

        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), far = from + new V2(400f, 0f);
        var dummies = new HashSet<int>();
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            dummies.Add(d.Id);
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        V2 allyAt = from + new V2(-80f, 40f);
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; ally.Pos = allyAt; client.Game.Players[2].Pos = allyAt; queue.Clear();
                foreach (var id in g.Enemies.Keys.Where(id => !dummies.Contains(id)).ToList()) g.Enemies.Remove(id);
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = far, Fire = fire && f < 2, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        float mult = me.Stats.DmgMult;
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // Valor: Edge Cut, Lunge Thrust, Hallowed Slam, in order; the links light Faith, the finisher spends it
        var d1 = Dummy(from + new V2(90f, 0f));
        float[] want = { 16f, 20f, 28f + Game.VgFaithDmg * 2f };
        for (int k = 0; k < 3; k++)
        {
            float before = d1.Hp;
            me.FireCd = 0f;
            Step(dt * 2f, true, false);
            Check(me.VgMoveId == (byte)V[k] && me.VgAtV == (k + 1) % 3, "press " + (k + 1) + " is " + VowguardMotion.Of(V[k]).Name + " (next " + me.VgAtV + ")");
            Step(VowguardMotion.Of(V[k]).Dur + 0.1f, false, false);
            float took = before - d1.Hp;
            Check(Math.Abs(took - want[k] * mult) < 1f, VowguardMotion.Of(V[k]).Name + " deals " + want[k] + " to an enemy 90 px ahead (" + took.ToString("0.#") + ")");
            if (k < 2)
                Check(me.VgFaith == k + 1, "and lights a Faith (" + me.VgFaith + ")");
        }
        Check(me.VgFaith == 0, "the finisher spent the Faith");
        Check(d1.StunT > 0f, "Hallowed Slam stuns");
        Check(g.Effects.Any(e => e.Kind == FxKind.VgStep && Math.Round(e.Radius) == 100 + (int)VgMove.HallowedSlam), "each step is sent as FxKind.VgStep (id 1, the move)");

        // the chain's window: a long wait starts it over; a short one keeps the place
        me.FireCd = 0f; Step(dt * 2f, true, false); Step(0.5f, false, false);
        Check(me.VgAtV == 1, "after a step the chain waits at step 2");
        Step(Game.VgChainWindow + 0.2f, false, false);
        Check(me.VgAtV == 0, "but " + Game.VgChainWindow + " s idle and it starts over");

        // Valor's shield only guards during shield moves
        g.Bullets.Clear();
        var b0 = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(60f, 0f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        Step(0.25f, false, false);
        Check(!g.Bullets.ContainsKey(b0.Id) && Game.VgGuardHalf(me) == 0f, "in Valor at rest the shield doesn't guard (the shot reached you)");

        // Alt: Sanctuary, slower, both places kept
        me.VgAtV = 2;
        float speed = me.Speed;
        Step(dt * 4f, false, true);
        Step(0.3f, false, false);
        Check(me.VgStance == 1, "Alt switches to Sanctuary");
        Check(Math.Abs(me.Speed - speed * Game.VgSanctSlow) < 0.5f, "you walk at 80% in it (" + me.Speed.ToString("0") + " of " + speed.ToString("0") + ")");
        Check(client.Game.Players[1].VgStance == 1 && client.Game.Players[1].VgAtV == 2, "the other screen sees the stance and the Valor place");

        // Sanctuary's guard: a small shot in front stops and lights Faith; one from behind doesn't
        g.Bullets.Clear();
        byte faith = me.VgFaith;
        var b1 = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(60f, 5f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        Step(0.2f, false, false);
        Check(!g.Bullets.ContainsKey(b1.Id) && me.VgFaith == faith + 1, "a small shot at the front stops on the shield and lights a Faith (" + me.VgFaith + ")");
        shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(-60f, 0f), new V2(300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        Step(0.2f, false, false);
        Check(me.VgFaith == faith + 1, "one from behind isn't stopped (Faith " + me.VgFaith + ")");

        // Rally Tap heals allies near you
        ally.Hp = ally.MaxHp * 0.5f;
        float allyHp = ally.Hp;
        me.FireCd = 0f; Step(dt * 2f, true, false);
        Check(me.VgMoveId == (byte)VgMove.RallyTap && me.VgAtS == 1, "Fire plays Sanctuary's own first step, Rally Tap, and Valor keeps its place (" + me.VgAtV + ")");
        Step(0.4f, false, false);
        Check(ally.Hp - allyHp >= 3.9f, "Rally Tap heals an ally 90 px away (+" + (ally.Hp - allyHp).ToString("0.#") + ")");

        // Sanctified Ward wards everyone near
        me.VgAtS = 2; me.FireCd = 0f; Step(dt * 2f, true, false); Step(0.6f, false, false);
        Check(ally.ShieldHp >= VowguardMotion.WardAmt - 0.01f, "Sanctified Ward puts a 15 HP ward on an ally near you (" + ally.ShieldHp.ToString("0") + ")");

        // Aegis Dome stops shots at its rim; Cover Ally's guard cuts damage
        me.VgChain = VowguardMotion.Pack(V, new[] { VgMove.CoverAlly, VgMove.AegisDome });
        me.VgAtS = 1; me.FireCd = 0f; Step(dt * 2f, true, false); Step(0.6f, false, false);
        g.Bullets.Clear();
        var b3 = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(0f, -150f), new V2(0f, 300f), 0f, 2f, 5f, 0, false, 0f });
        Step(0.25f, false, false);
        Check(!g.Bullets.ContainsKey(b3.Id), "an Aegis Dome stops a shot at its rim");
        ally.VgCoverT = 1f;
        Check(Math.Abs(Game.VgCover(ally) - VowguardMotion.CoverCut) < 0.001f, "Cover Ally's guard takes 30% off");

        // the parry starts both chains over; Alt back keeps Valor's place
        me.VgAtV = 2; me.VgAtS = 1;
        typeof(Game).GetMethod("ResetAttackChains", flags).Invoke(g, new object[] { me });
        Check(me.VgAtV == 0 && me.VgAtS == 0, "raising the parry sends both chains back to their first step");
        Step(dt * 4f, false, true);
        Step(0.3f, false, false);
        Check(me.VgStance == 0, "Alt goes back to Valor");
    }
}
