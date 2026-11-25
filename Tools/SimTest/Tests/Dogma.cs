using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Dragon's Dogma (DragonsDogma.cs): the Tank Ninja's key 1 on the lobby's Ultimate row, the pick reaching the run; the dragon
// pours out of the Ninja and devours regular enemies whole, bites heavies and bosses for 150 (times damage) at most once per
// 1.2 s each, swallows enemy shots round its head, comes home and ends; the other screen sees it and keeps a trail; mastered
// it lasts 7 s and heals 3% per enemy eaten; it ends on going down and between waves.
static partial class Program
{
    static void DogmaTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var tick = typeof(Game).GetMethod("TickDogmas", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby ---------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.DragonsDogma);
        Check(def.Name == "Dragon's Dogma" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Tank && def.Kit == Kit.Ninja && def.Capstone != null,
            "Dragon's Dogma is the Tank Ninja's ultimate on key 1, 70 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Ninja) == AbilityId.ThousandClones && Upgrades.Ultimate(Role.Tank, Kit.Ninja, 1) == AbilityId.DragonsDogma
            && Upgrades.AltUltimate(Role.Tank, Kit.Ninja, 2) == AbilityId.SpiritAvatar && Upgrades.Ultimate(Role.Dps, Kit.Ninja, 1) == AbilityId.ForbiddenPath,
            "Thousand Clones stays the default; key 1 is Dragon's Dogma; key 2 is Spirit Avatar; the DPS Ninja's key 1 is its own (Forbidden Path)");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9 }, port, false, "Ninja", Role.Tank, 0, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Ninja);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(1); } };
        host.RequestUlt(1);
        settle();
        Check(host.Lobby[1].Ult == 1 && client.Lobby[1].Ult == 1 && host.Lobby[1].UltId == AbilityId.DragonsDogma, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        int cid = client.MyId;
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.DragonsDogma && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.DragonsDogma,
            "in the match the host's F is Dragon's Dogma, on every game");

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        V2 mePos = new V2(800f, 450f);
        var mine = new HashSet<int>();
        Func<EnemyType, V2, float, Enemy> make = (type, at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FrozenT = 999f; e.Pos = at; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                me.Pos = mePos; me.InvulnT = 1f; ally.Pos = new V2(200f, 800f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(200f, 100f) });
                g.Update(dt); client.Game.Update(dt);
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.DogmaOn = false; me.DogmaEaten = me.DogmaBites = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });

        // --- The hunt ----------------------------------------------------------------------------------------------------
        reset();
        var grunts = new List<Enemy>();
        for (int k = 0; k < 5; k++)
            grunts.Add(make(EnemyType.Grunt, mePos + V2.FromAngle(k * 1.25f) * (180f + k * 40f), 5000f));
        var heavy = make(EnemyType.Thunderhead, mePos + new V2(-420f, 260f), 1e6f);
        cast();
        Check(me.DogmaOn && me.UltT == Game.DogmaTime && me.DogmaHead.DistanceTo(mePos) < 1f, "the cast: the dragon comes out of you, 5 s");
        Check(g.Sounds.Any(c => c.Sfx == Sfx.DogmaRoar), "the cast roars (Sfx.DogmaRoar)");
        step(0.1f);
        Check(client.Game.Players[1].DogmaOn && client.Game.Players[1].DogmaTrail.Count > 0, "the other screen sees it and lays its trail");
        step(3.6f);
        int eaten = grunts.Count(e => !g.Enemies.ContainsKey(e.Id) || e.Hp <= 0f);
        Console.WriteLine("  dogma: eaten {0}/5, big bites {1}, heavy lost {2:0}", eaten, me.DogmaBites, 1e6f - heavy.Hp);
        Check(eaten == 5 && me.DogmaEaten >= 5, "it devours every regular enemy it reaches, whatever its health (" + eaten + " of 5)");
        Check(g.Enemies.ContainsKey(heavy.Id) && heavy.Hp > 0f, "a heavy is never devoured");
        float bite = Game.DogmaBigBite * me.Stats.DmgMult;
        Check(me.DogmaBites >= 1 && Math.Abs((1e6f - heavy.Hp) - me.DogmaBites * bite) < 1f, "a heavy takes 150 (times your damage) a bite");
        int b0 = me.DogmaBites;
        step(0.5f);
        Check(me.DogmaBites - b0 <= 1, "at most one bite a heavy every 1.2 s");

        // Shots round its head are swallowed.
        g.Bullets.Clear();
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        shoot.Invoke(g, new object[] { (byte)10, me.DogmaHead, new V2(1f, 0f), 0f, 9f, 8f, -1, false, 0f });
        int before = g.Bullets.Count;
        tick.Invoke(g, new object[] { dt });
        Check(before == 1 && g.Bullets.Count == 0, "it swallows enemy shots round its head");

        bool heardHome = false;
        for (int f = 0; f < Game.DogmaTime / dt && !heardHome; f++) { step(dt); heardHome = g.Sounds.Any(c => c.Sfx == Sfx.DogmaHome); }
        Check(heardHome, "it coils home with its own sound (Sfx.DogmaHome)");
        step(0.2f);
        Check(!me.DogmaOn && me.UltT == 0f && !client.Game.Players[1].DogmaOn, "it comes home and ends");
        step(1f);
        Check(me.DogmaTrail.Count == 0 && client.Game.Players[1].DogmaTrail.Count == 0, "its body drains back in");

        // Mastered: 7 s and heals.
        reset();
        me.MasteryXp[AbilityId.DragonsDogma] = 1e6f;
        me.Hp = me.MaxHp * 0.5f;
        make(EnemyType.Grunt, mePos + new V2(160f, 0f), 50f);
        cast();
        Check(me.UltT == Game.DogmaTimeMastered, "mastered: 7 s");
        step(1.5f);
        Check(me.DogmaEaten == 1 && Math.Abs(me.Hp - (me.MaxHp * 0.5f + me.MaxHp * Game.DogmaHeal)) < 1f, "mastered: each enemy devoured heals 3%");
        me.MasteryXp.Remove(AbilityId.DragonsDogma);

        // Going down, and between waves.
        reset();
        make(EnemyType.Grunt, mePos + new V2(400f, 0f), 1e6f);
        cast();
        step(0.2f);
        me.InvulnT = 0f; me.Hp = 1f;
        damage.Invoke(g, new object[] { me, 500f, "test" });
        tick.Invoke(g, new object[] { dt });
        Check(me.Downed && !me.DogmaOn, "it ends if you go down");
        reset();
        cast();
        g.State = MatchState.Intermission;
        tick.Invoke(g, new object[] { dt });
        Check(!me.DogmaOn && me.UltT == 0f, "it ends when the wave is over");
        g.State = MatchState.Fighting;
        host.Leave(""); client.Leave("");
    }
}
