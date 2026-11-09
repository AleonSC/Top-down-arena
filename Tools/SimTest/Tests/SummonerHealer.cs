using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Sprite Staff's firefly (seeds, then hops to enemies that aren't seeded) and the Treant Rod's root lash (a line that
// hurts enemies and mends allies), both in KitWeapons.cs.
static partial class Program
{
    static void FireflyRootTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        V2 from = new V2(300f, 450f), allyAt = new V2(300f, 150f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; keep.Add(e); pins[e] = at; return e; };
        Action<float> play = secs =>
        {
            for (int f = 0; f < (int)Math.Round(secs * 60); f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { e.Pos = pins[e]; e.FireT = 99f; }
                me.Pos = from;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); g.Minions.Clear(); g.Zones.Clear(); };
        Action fire = () => volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.3f);

        // --- Sprite Staff: the firefly.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.SpriteStaff);
        float shot = me.Weapon.Damage * me.Stats.DmgMult;
        var a = dummy(from + new V2(200f, 0f)); var b = dummy(a.Pos + new V2(0f, 110f)); var c = dummy(b.Pos + new V2(0f, 110f));
        fire();
        play(1.3f);
        Func<Enemy, bool> seeded = e => e.SeededT > 0f && e.SeededBy == 1;
        Console.WriteLine("  firefly: seeded {0}/{1}/{2}; damage {3:0.0}/{4:0.0}/{5:0.0} (a shot is {6:0.0}); bullets left {7}", seeded(a), seeded(b), seeded(c),
            a.MaxHp - a.Hp, b.MaxHp - b.Hp, c.MaxHp - c.Hp, shot, g.Bullets.Count);
        Check(seeded(a) && seeded(b) && seeded(c), "one firefly seeds three enemies in a row (hit, hop, hop)");
        Check(Math.Abs(a.MaxHp - a.Hp - shot) < 0.5f && Math.Abs(b.MaxHp - b.Hp - shot) < 0.5f && Math.Abs(c.MaxHp - c.Hp - shot) < 0.5f, "each enemy a firefly lands on takes one full hit");
        Check(g.Bullets.Count == 0, "a firefly is spent after its last hop");
        reset();

        // It looks for an enemy that isn't seeded, so a seeded one in the way is passed over.
        a = dummy(from + new V2(200f, 0f)); b = dummy(a.Pos + new V2(0f, 110f)); c = dummy(a.Pos + new V2(0f, -170f));
        b.SeededBy = 1; b.SeededT = 3f;
        fire();
        play(1.2f);
        Check(seeded(a) && seeded(c) && Math.Abs(b.MaxHp - b.Hp) < 0.01f, "a firefly skips enemies that are already seeded and hops to the next one (nearest 110 px away was passed over)");
        reset();

        // Nothing within 300 px to hop to: it ends there.
        a = dummy(from + new V2(200f, 0f)); b = dummy(a.Pos + new V2(0f, 340f));
        fire();
        play(1.2f);
        Check(seeded(a) && !seeded(b) && Math.Abs(b.MaxHp - b.Hp) < 0.01f && g.Bullets.Count == 0, "a firefly doesn't hop further than " + (int)Game.FireflyReach + " px");
        reset();

        // A lone target: seeded once, hit once.
        a = dummy(from + new V2(200f, 0f));
        fire();
        play(1.0f);
        Check(seeded(a) && Math.Abs(a.MaxHp - a.Hp - shot) < 0.5f, "a firefly on a lone enemy seeds it and hits it once (a boss gets no extra)");
        // The seeds still free sprites: kill a seeded enemy.
        a.MaxHp = a.Hp = 3f;
        fire();
        play(0.9f);
        int freed = g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Sprite && !m.Companion);
        Check(freed >= 1, "killing a seeded enemy still frees a sprite");
        reset();

        // The client sees the firefly fly (its kind is in the spawn message).
        a = dummy(from + new V2(500f, 0f));
        fire();
        play(0.15f);
        Check(client.Game.Bullets.Values.Any(x => x.Kind == Defs.FireflyShot), "the other player sees the firefly in flight");
        reset();

        // --- Treant Rod: the root lash.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.TreantRod);
        play(0.3f);
        reset();
        float lash = me.Weapon.Damage * me.Stats.DmgMult;
        float reach = g.WeaponRange(me);
        Check(Math.Abs(reach - me.Weapon.Reach) < 1f, "the Treant Rod's range (aim line, bots) is the lash's reach (" + reach.ToString("0") + ")");
        var near = dummy(from + new V2(100f, 0f)); var mid = dummy(from + new V2(200f, 0f));
        var far = dummy(from + new V2(330f, 0f)); var wide = dummy(from + new V2(150f, 90f));
        allyAt = from + new V2(150f, 0f);
        ally.Hp = ally.MaxHp * 0.3f;
        play(0.1f);
        float n0 = near.Hp, m0 = mid.Hp, f0 = far.Hp, w0 = wide.Hp, a0 = ally.Hp;
        fire();
        play(1f / 60f);
        Console.WriteLine("  root lash: near {0:0.0}, mid {1:0.0}, beyond the reach {2:0.0}, off to the side {3:0.0} (a lash is {4:0.0}); ally on the line healed {5:0.0}",
            n0 - near.Hp, m0 - mid.Hp, f0 - far.Hp, w0 - wide.Hp, lash, ally.Hp - a0);
        Check(Math.Abs(n0 - near.Hp - lash) < 1f && Math.Abs(m0 - mid.Hp - lash) < 1f, "the root lash hits everything on its line, once each");
        Check(f0 - far.Hp < 0.01f && w0 - wide.Hp < 0.01f, "it misses an enemy past its reach and one off to the side");
        Check(ally.Hp - a0 >= Game.RootHeal - 0.5f, "an ally standing on the lash is healed " + (int)Game.RootHeal);
        play(0.1f);
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.RootLash), "the other player sees the root lash");

        // An ally off the line gets nothing from it.
        allyAt = from + new V2(150f, 150f);
        ally.Hp = ally.MaxHp * 0.3f;
        play(0.1f);
        a0 = ally.Hp;
        fire();
        play(1f / 60f);
        Check(ally.Hp - a0 < 1f, "an ally off the lash's line isn't healed by it");
        // Timing: the weapon fires about every 0.55 s.
        Check(Math.Abs(me.Weapon.FireInterval - 0.55f) < 0.001f && me.Weapon.Damage * (1f / me.Weapon.FireInterval) > 25f && me.Weapon.Damage * (1f / me.Weapon.FireInterval) < 30f, "the lash is sized to about 27 damage a second on one target");
        reset();

        // --- Treant Rod: the Treant's seeds (tiny, 3 damage, every 0.4 s, nearest enemy within 320 px).
        play(1.5f);  // the Treant is back
        var treant = g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.Treant && m.Owner == 1);
        Check(treant != null, "the Treant Rod's Treant is out");
        var foe = dummy(treant.Pos + new V2(200f, 0f));
        float h0 = foe.Hp, seedDmg = Game.SeedDamage * me.Stats.DmgMult;
        bool sawSeed = false, clientSawSeed = false;
        for (int f = 0; f < 60; f++)
        {
            play(1f / 60f);
            sawSeed |= g.Bullets.Values.Any(x => x.Kind == Defs.TreantSeed && x.Owner == 1);
            clientSawSeed |= client.Game.Bullets.Values.Any(x => x.Kind == Defs.TreantSeed);
        }
        float dealt = h0 - foe.Hp;
        Console.WriteLine("  treant seeds: {0:0.0} damage to an enemy 200 px away in 1 s (a seed is {1:0.0})", dealt, seedDmg);
        Check(sawSeed && clientSawSeed, "the Treant flicks seeds at an enemy in range, and the other player sees them");
        Check(dealt >= seedDmg * 2f - 0.01f && dealt <= seedDmg * 3f + 0.01f && Math.Abs(dealt / seedDmg - Math.Round(dealt / seedDmg)) < 0.01f,
            "about 2 to 3 seeds land in a second, each for exactly one seed's damage (" + (int)Game.SeedDamage + " x your damage)");
        Check(Game.SeedDamage * 4f < me.Weapon.Damage, "a seed is small next to the lash");

        treant.StaggerT = 5f;
        play(0.6f);  // the seeds already flying land first
        float h1 = foe.Hp;
        play(1f);
        Check(h1 - foe.Hp < 0.01f, "a staggered Treant holds its seeds");
        treant.StaggerT = 0f;
        reset();

        play(1.5f);
        treant = g.Minions.Values.First(m => m.Kind == MinionKind.Treant && m.Owner == 1);
        var distant = dummy(treant.Pos + new V2(Game.SeedRange + 150f, 0f));
        float d0 = distant.Hp;
        play(1f);
        Check(d0 - distant.Hp < 0.01f && !g.Bullets.Values.Any(x => x.Kind == Defs.TreantSeed), "an enemy past the seed range gets no seeds");
        reset();

        // The seeds belong to the Treant Rod: another weapon's Treant (none) and a lone Treant with nobody holding the rod fire nothing.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.SpriteStaff);
        play(1.5f);
        dummy(from + new V2(150f, 0f));
        play(1f);
        Check(!g.Bullets.Values.Any(x => x.Kind == Defs.TreantSeed), "no seeds without the Treant Rod");
        reset();
        host.Leave(""); client.Leave("");
    }
}
