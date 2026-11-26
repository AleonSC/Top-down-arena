using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Tidal Tusker (TidalTusker.cs): the Tank Summoner's key 3 on the lobby's Ultimate row (the pick reaching the run), the rise
// (no spray), the elephant following behind the way you walk, the hose (40 a second and wet on the nearest enemy in reach,
// stopping on the first enemy in its line, nothing past its reach, a push for grunts and not heavies), the client seeing the
// stream, the taunt (regular enemies near it, not bosses), enemy shots stopped on it, its health running out or you going
// down starting the stomp, the stomp's damage, soak and shove, the end on time, mastered (16 s, two streams), Deep Trunk.
static partial class Program
{
    static void TidalTuskerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        var target = typeof(Game).GetMethod("TuskerTarget", flags);
        var stops = typeof(Game).GetMethod("TuskerStops", flags);
        var wear = typeof(Game).GetMethod("WearTusker", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.TidalTusker);
        Check(def.Name == "Tidal Tusker" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Kit == Kit.Summoner && def.Role == Role.Tank && def.Capstone != null,
            "Tidal Tusker is the Tank Summoner's ultimate on key 3, 75 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Summoner) == AbilityId.Titan && Upgrades.Ultimate(Role.Tank, Kit.Summoner, 3) == AbilityId.TidalTusker
            && Upgrades.AltUltimate(Role.Dps, Kit.Summoner, 3) != AbilityId.TidalTusker && Upgrades.AltUltimate(Role.Healer, Kit.Summoner, 3) != AbilityId.TidalTusker,
            "the Titan stays the default; key 3 is Tidal Tusker, only for the Tank Summoner");
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Deep Trunk");
        Check(card != null && card.RequiresAbility == AbilityId.TidalTusker && card.Rarity == Rarity.Rare, "its Rare card is Deep Trunk");

        // --- Into a run -----------------------------------------------------------------------------------------------------
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Mahout", Role.Tank, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Summoner);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); System.Threading.Thread.Sleep(1); } };
        host.RequestUlt(3);
        settle();
        Check(host.Lobby[1].Ult == 3 && client.Lobby[1].Ult == 3 && host.Lobby[1].UltId == AbilityId.TidalTusker, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.TidalTusker && client.Game.Players[1].Slots[U].Id == AbilityId.TidalTusker, "in the match the host's F is Tidal Tusker, on every game");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (where, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, where, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; spots[e] = where; return e;
        };
        V2 mePos = new V2(500f, 450f), aim = new V2(900f, 450f);
        bool pinned = true;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !spots.Keys.Any(e => e.Id == k)).ToList()) g.Enemies.Remove(id);
                if (pinned)
                    foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.KnockLeft = V2.Zero; }
                foreach (var e in spots.Keys) e.FireT = 99f;
                g.Minions.Clear();  // (the golems would hit the pinned enemies)
                g.Bullets.Clear();
                me.Pos = mePos;
                me.Hp = me.MaxHp;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        Func<Zone> tusker = () => g.Zones.Values.FirstOrDefault(zz => zz.Kind == ZoneKind.Tusker);
        Action reset = () =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); spots.Clear();
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            me.UltT = 0f; me.Downed = false; me.Pos = mePos = new V2(500f, 450f); me.Aim = 0f;
            me.Slots[U].Charges = 1; me.Slots[U].Timer = 0f;
            masteredField.SetValue(g, false);
            aim = new V2(900f, 450f); pinned = true;
            g.Players[2].Pos = new V2(300f, 950f);
            g.State = MatchState.Fighting;
        };

        // --- The cast and the rise -------------------------------------------------------------------------------------------
        reset();
        V2 elephantAt = mePos - new V2(Game.TkFollow, 0f);  // where it settles: behind you, aiming right
        var a = pin(elephantAt + new V2(0f, 150f), EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        var z = tusker();
        float total = Game.TkTotal(Game.TkLife);
        Check(z != null && Math.Abs(z.Duration - total) < 1e-3f && Math.Abs(me.UltT - total) < 1e-3f, "casting summons it: 0.8 s rise + 12 s + 0.7 s stomp");
        Check(z != null && Math.Abs(z.SoakMax - Game.TkHealth(g.Wave)) < 0.01f && Math.Abs(z.Radius - Game.TkReach) < 0.01f && !Game.TkMastered(z),
            "it has 600 + 30 a wave health (" + (z != null ? z.SoakMax : 0f) + " on wave " + g.Wave + "), a 260 px stream");
        step(Game.TkRise - 0.1f);
        Check(a.Hp == a.MaxHp && z.Side == 0 && !Game.TuskerUp(z), "nothing is sprayed while it rises");

        // --- The hose ---------------------------------------------------------------------------------------------------------
        step(1.2f);
        float before = a.Hp;
        step(1f);
        float lost = before - a.Hp;
        Console.WriteLine("  tidal tusker: the stream took {0:0} a second (x{1:0.00}), wet {2:0.0} s, elephant {3:0} px from its spot", lost, z.DmgMult, a.WetT, (z.Pos - elephantAt).Length);
        Check(z.Pos.CloserThan(elephantAt, 12f), "it settles 85 px behind you");
        Check(Math.Abs(lost - Game.TkDps * z.DmgMult) < 0.15f * Game.TkDps * z.DmgMult, "the stream hits the nearest enemy for 40 a second");
        Check(a.WetT > Game.TkWet - 0.3f && z.Side == a.Id, "and keeps it wet 3 s");
        settle();
        var cz = client.Game.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.Tusker);
        Check(cz != null && cz.Side == a.Id && cz.Origin.CloserThan(z.Origin, 4f) && Math.Abs(cz.GuardFrac - 1f) < 0.01f, "the client sees the stream and where it ends");
        Check(z.Origin.CloserThan(a.Pos, a.Radius + 2f), "the stream ends on the enemy's edge");

        // --- In line, out of reach, the push ----------------------------------------------------------------------------------
        reset();
        var front = pin(elephantAt + new V2(0f, 130f), EnemyType.Grunt);
        var behind = pin(elephantAt + new V2(0f, 210f), EnemyType.Grunt);
        var far = pin(elephantAt + new V2(0f, -360f), EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = tusker();
        step(Game.TkRise + 2f);
        Check(front.Hp < front.MaxHp && behind.Hp == behind.MaxHp, "the stream stops on the first enemy in its line");
        Check(far.Hp == far.MaxHp, "nothing past its reach is sprayed");
        pinned = false;
        V2 was = front.Pos;
        step(1f);
        Console.WriteLine("  tidal tusker: a grunt in the stream was pushed {0:0} px in a second", (front.Pos - was).Length);
        Check((front.Pos - z.Pos).Length > (was - z.Pos).Length + 20f, "a grunt in the stream is pushed back");
        reset();
        var heavyType = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < 250 && Defs.Get(t).Heavy && !Defs.Get(t).IsBoss && Defs.Get(t).Speed > 0f);
        var heavy = pin(elephantAt + new V2(0f, 140f), heavyType);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = tusker();
        step(Game.TkRise + 1f);
        Check(heavy.Hp < heavy.MaxHp && heavy.WetT > 0f && heavy.KnockLeft.Length < 0.01f, "a heavy is sprayed and soaked but not pushed");

        // --- Following you -----------------------------------------------------------------------------------------------------
        reset();
        pin(new V2(1500f, 150f), EnemyType.Grunt);  // (far out of reach: the wave isn't cleared)
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = tusker();
        step(Game.TkRise);
        for (int i = 0; i < 120; i++)
        {
            mePos = new V2(500f, 450f + i * 2.5f);  // you walk down 300 px
            step(dt * 2);
        }
        step(1f);
        V2 wantSpot = mePos - new V2(0f, Game.TkFollow);
        Console.WriteLine("  tidal tusker: after a walk down it is {0:0} px from the spot behind you", (z.Pos - wantSpot).Length);
        Check(z.Pos.CloserThan(wantSpot, 20f), "it follows behind you, the way you walk");

        // --- The taunt and the shots --------------------------------------------------------------------------------------------
        V2 p0 = me.Pos;
        var nearGrunt = pin(z.Pos + new V2(200f, 0f), EnemyType.Grunt);
        var farGrunt = pin(z.Pos + new V2(600f, -100f), EnemyType.Grunt);
        var bossType = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < 250 && Defs.Get(t).IsBoss);
        var boss = pin(z.Pos + new V2(-150f, 0f), bossType);
        Check(((V2)target.Invoke(g, new object[] { nearGrunt, p0 })).CloserThan(z.Pos, 0.01f), "a regular enemy within 300 px goes for the elephant");
        Check(((V2)target.Invoke(g, new object[] { farGrunt, p0 })).CloserThan(p0, 0.01f) && ((V2)target.Invoke(g, new object[] { boss, p0 })).CloserThan(p0, 0.01f), "one farther away, and a boss, still go for you");
        float hp0 = z.BlockHp;
        var shot = new Bullet { Pos = z.Pos + new V2(20f, 0f), Radius = 4f, Dmg = 10f };
        Check((bool)stops.Invoke(g, new object[] { shot }) && z.BlockHp < hp0, "an enemy shot reaching it stops there and wears it");
        Check(!(bool)stops.Invoke(g, new object[] { new Bullet { Pos = z.Pos + new V2(80f, 0f), Radius = 4f, Dmg = 10f } }), "a shot past its body flies on");

        // --- Broken: the stomp ---------------------------------------------------------------------------------------------------
        reset();
        V2 at = elephantAt;
        var close = pin(at + new V2(120f, -40f), EnemyType.Grunt);
        var splash = pin(at + new V2(-195f, 0f), EnemyType.Grunt);
        var outside = pin(at + new V2(0f, -320f), EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = tusker();
        step(Game.TkRise + 1f);
        float c0 = close.Hp, s0 = splash.Hp;
        wear.Invoke(g, new object[] { z, z.BlockHp + 1f, z.Pos });
        Check(Game.TkStomping(z) && z.T <= Game.TkStomp + 1e-3f, "at 0 health it rears for its stomp at once");
        pinned = false;
        step(Game.TkStompLand + dt * 2);
        float lostClose = c0 - close.Hp, lostSplash = s0 - splash.Hp;
        Console.WriteLine("  tidal tusker: the stomp took {0:0} close in, {1:0} at 195 px; wet {2:0.0} s; shoved {3:0} px", lostClose, lostSplash, splash.WetT, splash.KnockLeft.Length);
        Check(Math.Abs(lostClose - Game.TkStompDmg * z.DmgMult) < 0.3f * Game.TkStompDmg * z.DmgMult, "the stomp hits for 60 within 160 px");
        Check(lostSplash < 1f && splash.WetT > Game.TkWet - 0.3f && (splash.KnockLeft.Length > 1f || splash.Pos.FartherThan(at + new V2(-195f, 0f), 5f)), "its splash soaks and shoves out to 220 px, without hurting");
        Check(outside.Hp == outside.MaxHp && outside.WetT <= 0f, "nothing past the splash is touched");
        step(Game.TkStomp);
        Check(tusker() == null, "then it is gone");

        // --- On time, and going down -----------------------------------------------------------------------------------------------
        reset();
        pin(new V2(1500f, 150f), EnemyType.Grunt);  // (far out of reach: the wave isn't cleared)
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = tusker();
        step(Game.TkRise + Game.TkLife + 0.05f);
        Check(Game.TkStomping(z) && tusker() != null, "it stomps at the end of its 12 s");
        step(Game.TkStomp + 0.1f);
        Check(tusker() == null && me.UltT <= 0.01f, "and has gone on time");
        reset();
        pin(new V2(1500f, 150f), EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = tusker();
        step(Game.TkRise + 1f);
        me.Downed = true;
        step(dt * 2);
        Check(Game.TkStomping(z), "going down: it stomps at once");
        me.Downed = false;

        // --- Mastered, and Deep Trunk -------------------------------------------------------------------------------------------------
        reset();
        masteredField.SetValue(g, true);
        var one = pin(elephantAt + new V2(0f, 140f), EnemyType.Grunt);
        var two = pin(elephantAt + new V2(0f, -150f), EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = tusker();
        step(Game.TkRise + 1.5f);
        Check(z != null && Math.Abs(z.Duration - Game.TkTotal(Game.TkLifeMastered)) < 1e-3f && Game.TkMastered(z), "mastered: it stays 16 s");
        Check(z.Side != 0 && z.Rider != 0 && z.Side != z.Rider && one.Hp < one.MaxHp && two.Hp < two.MaxHp, "mastered: two streams on two enemies");
        reset();
        card.Apply(me.Stats);
        var edge = pin(elephantAt + new V2(0f, 320f), EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = tusker();
        step(Game.TkRise + 1.5f);
        Console.WriteLine("  tidal tusker, Deep Trunk: reach {0:0} px, the grunt 320 px out lost {1:0}, wet {2:0.0} s", z.Radius, edge.MaxHp - edge.Hp, edge.WetT);
        Check(Math.Abs(z.Radius - Game.TkReach * 1.25f) < 0.5f && edge.Hp < edge.MaxHp && edge.WetT > Game.TkWet + 0.7f, "Deep Trunk: the stream reaches 25% farther and soaks 1 s longer");
    }
}
