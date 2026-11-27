using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Holy Lance Rain (HolyLanceRain.cs): the Healer Knight's key 2 on the lobby's Ultimate row (the pick reaching the run on F);
// 24 spears over 3 s, each landing inside the 340 px circle round the knight as they walk; a landing hurts (30 times your
// damage), stuns and shoves what stands within 55 px, a boss only hurt; each glow heals a player standing in it 5 HP/s, glows
// not stacking, and stops when the glows fade; mastered, 32 spears and 6 s glows.
static partial class Program
{
    static void HolyLanceRainTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        var lanceTick = typeof(Game).GetMethod("TickLanceRain", flags);

        var def = Upgrades.Ability(AbilityId.HolyLanceRain);
        Check(def.Name == "Holy Lance Rain" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Kit == Kit.Knight && def.Role == Role.Healer && def.Capstone != null,
            "Holy Lance Rain is the Healer Knight's ultimate on key 2, 80 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Knight) == AbilityId.HallowedBlade && Upgrades.Ultimate(Role.Healer, Kit.Knight, 1) == AbilityId.PilgrimsStandard
            && Upgrades.Ultimate(Role.Healer, Kit.Knight, 2) == AbilityId.HolyLanceRain,
            "Hallowed Blade is the default and Pilgrim's Standard key 1; key 2 is Holy Lance Rain");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Paladin", Role.Healer, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(2);
        settle();
        Check(host.Lobby[1].Ult == 2 && client.Lobby[1].Ult == 2 && host.Lobby[1].UltId == AbilityId.HolyLanceRain, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[client.MyId];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.HolyLanceRain && client.Game.Players[1].Slots[U].Id == AbilityId.HolyLanceRain, "in the match the host's F is Holy Lance Rain, on every game");
        g.DevSetGod(1, true);

        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, float, Enemy> pin = (at, type, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FireT = 99f; spots[e] = at; return e;
        };
        V2 mePos = new V2(500f, 450f), walk = new V2(120f, 0f);  // the knight walks right at 120 px/s while it rains
        V2? allyAt = null;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var kv in spots) if (g.Enemies.ContainsKey(kv.Key.Id)) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; }
                foreach (var id in g.Enemies.Keys.Where(k => !spots.Keys.Any(e => e.Id == k)).ToList()) g.Enemies.Remove(id);
                mePos += walk * dt;
                me.Pos = mePos;
                ally.Pos = allyAt ?? new V2(1500f, 150f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(200f, 0f) });
            }
        };
        Action reset = () =>
        {
            g.Enemies.Clear(); spots.Clear(); g.Effects.Clear(); g.Bullets.Clear();
            me.Slots[U].Timer = 0f; me.Slots[U].Charges = 1; mePos = new V2(500f, 450f); me.Pos = mePos; allyAt = null;
            ally.Hp = ally.MaxHp; ally.Downed = false; ally.InvulnT = 0f;
        };
        Func<FxKind, int> count = k => g.Effects.Count(e => e.Kind == k);

        // --- The rain ------------------------------------------------------------------------------------------------------
        reset();
        masteredField.SetValue(g, false);
        cast.Invoke(g, new object[] { me, U, mePos + new V2(200f, 0f) });
        var rainFx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.HolyLanceRain);
        Check(rainFx != null && (int)rainFx.Radius == me.Id && Math.Abs(rainFx.Color.A * 1000f - Game.LrRadius) < 0.5f && g.Sounds.Any(c => c.Sfx == Sfx.SpellHoly),
            "casting it sends the circle to every screen (the caster's id, 340 px) with a holy sound");

        // the first spear: pin a grunt, a boss and a far grunt round its spot
        step(dt * 2);
        var first = g.Effects.First(e => e.Kind == FxKind.HolyLanceSpear);
        V2 spot = first.Pos;
        Check(Math.Abs(first.Color.A * 10f - Game.LrGlowT) < 0.01f && Math.Abs(first.Dur - (Game.LrWarn + Game.LrGlowT + Game.LrFade)) < 0.01f,
            "each spear is one effect, sent as its warning starts (the glow's 4 s in it)");
        var grunt = pin(spot + new V2(30f, 0f), EnemyType.Grunt, 5000f);
        var boss = pin(spot + new V2(-30f, 0f), EnemyType.Grunt, 5000f);
        boss.Def = Defs.Get(EnemyType.Colossus);
        var far = pin(spot + new V2(0f, 600f), EnemyType.Grunt, 5000f);
        step(Game.LrWarn * 0.5f);
        Check(grunt.Hp == grunt.MaxHp, "nothing is hit during the warning");
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.HolyLanceSpear && e.Pos.DistanceTo(spot) < 1f), "the other screen draws the spear");
        step(Game.LrWarn * 0.5f + dt * 2);
        float want = Game.LrDamage * me.Stats.DmgMult, took = grunt.MaxHp - grunt.Hp;
        Console.WriteLine("  lancerain: grunt took {0:0.0}, boss {1:0.0}, far {2:0}", took, boss.MaxHp - boss.Hp, far.MaxHp - far.Hp);
        Check(took >= want - 0.5f && grunt.StunT > 0f,
            "a landing hurts what stands within 55 px (" + took.ToString("0") + ", 30 times your damage) and stuns it");
        Check(boss.MaxHp - boss.Hp >= want - 0.5f && boss.StunT <= 0f, "a boss is hurt but not stunned");

        // the glow: the ally stands in it, hurt
        // (only the rain's own tick, so the ally's other healing doesn't count)
        Func<float> rainHeal = () =>
        {
            float hp0 = ally.Hp;
            for (int f = 0; f < 1f / dt; f++) lanceTick.Invoke(g, new object[] { dt });
            return ally.Hp - hp0;
        };
        allyAt = spot;
        ally.Pos = spot;
        ally.Hp = ally.MaxHp * 0.3f;
        float healed = rainHeal();
        Check(healed > Game.LrGlowHeal * 0.6f && healed < Game.LrGlowHeal * 1.6f,
            "standing in a glow heals 5 HP/s, glows not stacking (" + healed.ToString("0.0") + " in 1 s)");

        step(Game.LrRain);
        var spears = g.Effects.Where(e => e.Kind == FxKind.HolyLanceSpear).ToList();
        Check(spears.Count == Game.LrCount, "24 spears fall over 3 s (" + spears.Count + ")");
        Check(far.Hp == far.MaxHp || far.MaxHp - far.Hp >= want - 0.5f, "an enemy far away is only hit if a spear lands by it");

        // every spear lands inside the circle round where the knight was as it fell (the knight walked 360 px)
        float maxX = spears.Max(e => e.Pos.X), minX = spears.Min(e => e.Pos.X);
        Check(maxX > 500f + Game.LrRadius * 0.6f && minX < 500f + 360f - Game.LrRadius * 0.4f, "the circle follows the knight as they walk");

        step(Game.LrGlowT + 1f);
        ally.Pos = spot; ally.Hp = ally.MaxHp * 0.3f;
        Check(rainHeal() == 0f, "once the glows fade, nothing heals");

        // --- Mastered ------------------------------------------------------------------------------------------------------
        reset();
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, U, mePos });
        masteredField.SetValue(g, false);
        step(Game.LrRain + 0.2f);
        var ms = g.Effects.Where(e => e.Kind == FxKind.HolyLanceSpear).ToList();
        Check(ms.Count == Game.LrCountMastered && ms.All(e => Math.Abs(e.Color.A * 10f - Game.LrGlowMastered) < 0.01f),
            "mastered, 32 spears and 6 s glows (" + ms.Count + ")");
        step(Game.LrGlowMastered + 1f);
    }
}
