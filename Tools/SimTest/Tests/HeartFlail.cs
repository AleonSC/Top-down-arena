using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Healer Hemomancer's fourth weapon, the Heart Flail (HeartFlail.cs).
static partial class Program
{
    /// A click swings the heart: it hits and bleeds the enemy on its arc (not one behind), quickens the beat and its blood
    /// drops heal the hurt ally; holding spins it round you, hitting all round and healing the ally near you each beat; at 180
    /// beats the next beat is a Cardiac Burst; Alt throws the heart to the cursor, where it lands, roots, heals the ally near it
    /// through its veins and reels back; the state reaches the other screen; Pacemaker is its card.
    static void HeartFlailTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Heart", Role.Healer, 3, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.HeartFlail && Defs.WeaponCount(Role.Healer, Kit.Hemomancer) == 4, "the Healer Hemomancer's fourth key is the Heart Flail (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(500f, 450f), right = from + new V2(400f, 0f);
        Func<EnemyType, V2, Enemy> Dummy = (type, at) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        V2 allyPos = from + new V2(-60f, 120f);
        V2 aim = right;
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; ally.Pos = allyPos; client.Game.Players[2].Pos = allyPos; queue.Clear();
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                foreach (var x in s) { x.Poll(); ally.Pos = allyPos; x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aim, Fire = fire, WeaponAction = alt } : new PlayerInput()); }
            }
        };
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        float unit = me.Weapon.Damage * me.Stats.DmgMult;
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Step(0.3f, false, false);

        // a click: the Swing
        var ahead = Dummy(EnemyType.Grunt, from + new V2(100f, 0f));
        var behind = Dummy(EnemyType.Grunt, from + new V2(-105f, 0f));
        ally.Hp = ally.MaxHp * 0.5f;
        float allyHp0 = ally.Hp;
        int bpm0 = me.HfBpm;
        Step(dt * 3f, true, false);
        Step(0.4f, false, false);
        Check(took(ahead) >= unit * 0.9f, "a click swings the heart through the enemy ahead (" + took(ahead).ToString("0.#") + ")");
        Check(took(behind) == 0f, "and not the one behind you");
        Check(ahead.BleedT > 0f, "the hit makes it bleed");
        Check(me.HfBpm > bpm0, "the hit quickens the beat (" + bpm0 + " -> " + me.HfBpm + ")");
        Step(Game.HfDropFlight + 0.1f, false, false);
        Check(ally.Hp >= allyHp0 + Game.HfDrops * Game.HfDropHeal - 0.5f, "its blood drops heal the hurt ally 3 x 4 (" + (ally.Hp - allyHp0).ToString("0.#") + ")");
        Check(client.Game.Players[1].HfBpm == me.HfBpm, "the beat reaches the other screen (" + client.Game.Players[1].HfBpm + ")");

        // held: the Spin
        g.Enemies.Clear();
        var left = Dummy(EnemyType.Grunt, from + new V2(-110f, 0f));
        var up = Dummy(EnemyType.Grunt, from + new V2(0f, -112f));
        allyPos = from + new V2(-40f, 100f);
        ally.Hp = ally.MaxHp * 0.4f;
        float allyHp1 = ally.Hp;
        bool spinSeen = false, pulse = false;
        for (int f = 0; f < 2.5f / dt; f++)
        {
            Step(dt, true, false);
            spinSeen |= me.HfMode == Game.HfSpin && client.Game.Players[1].HfMode == Game.HfSpin;
            pulse |= client.Game.Effects.Any(e => e.Kind == FxKind.HeartPulse);
        }
        Check(spinSeen, "holding Fire spins the heart, on both screens");
        Check(took(left) > 0f && took(up) > 0f, "the spin hits all round you (" + took(left).ToString("0") + ", " + took(up).ToString("0") + ")");
        Check(pulse && ally.Hp > allyHp1 + Game.HfPulseHeal - 0.5f, "each beat while spinning heals the ally near you (" + (ally.Hp - allyHp1).ToString("0.#") + ")");
        Step(0.2f, false, false);
        Check(me.HfMode == Game.HfRest, "letting go brings the heart back to your side");

        // the Cardiac Burst
        g.Enemies.Clear();
        var near = Dummy(EnemyType.Grunt, from + new V2(150f, 0f));
        ally.Hp = ally.MaxHp * 0.4f;
        float allyHp2 = ally.Hp;
        var hf = typeof(Game).GetField("hfHost", flags).GetValue(g) as System.Collections.IDictionary;
        var h = hf[1];
        h.GetType().GetField("BpmF").SetValue(h, Game.HfBpmMax);
        V2 n0 = near.Pos;
        bool burst = false;
        for (int f = 0; f < 0.6f / dt; f++)
        {
            Step(dt, false, false);
            burst |= client.Game.Effects.Any(e => e.Kind == FxKind.HeartBurst);
        }
        Check(burst && ally.Hp >= allyHp2 + Game.HfBurstHeal - 0.5f, "at 180 beats the next beat is a Cardiac Burst that heals 30 (" + (ally.Hp - allyHp2).ToString("0.#") + ")");
        Check(near.Pos.X - n0.X > 40f, "and shoves the enemies round you (" + (near.Pos.X - n0.X).ToString("0") + " px)");
        Check(me.HfBpm <= Game.HfBpmMin + 1f, "and the beat starts over at 60 (" + me.HfBpm + ")");

        // Alt: Open Heart
        g.Enemies.Clear();
        V2 spot = from + new V2(250f, 0f);
        var there = Dummy(EnemyType.Grunt, spot + new V2(30f, 0f));
        allyPos = spot + new V2(0f, 120f);
        ally.Hp = ally.MaxHp * 0.3f;
        float allyHp3 = ally.Hp;
        aim = spot;
        Step(dt * 2f, false, true);
        Step(dt * 2f, false, false);
        Check(me.HfMode == Game.HfFly && me.HfCd > Game.HfThrowCd - 0.3f, "Alt throws the heart and starts the 12 s cooldown (" + me.HfCd.ToString("0.0") + ")");
        Step(Game.HfThrowFlight + 0.05f, false, false);
        Check(me.HfMode == Game.HfRoot && me.HfPos.DistanceTo(spot) < 5f, "it lands at the cursor and roots (" + me.HfPos.DistanceTo(spot).ToString("0") + " px off)");
        Check(took(there) >= unit * Game.HfLandMult * 0.9f, "the landing hits the enemy by it (" + took(there).ToString("0.#") + ")");
        Step(Game.HfRootTime - 0.1f, true, false);
        Check(ally.Hp >= allyHp3 + Game.HfVeinHps * 2.5f, "its veins heal the ally near it about 8 a second (" + (ally.Hp - allyHp3).ToString("0.#") + ")");
        Check(client.Game.Players[1].HfMode == Game.HfRoot && client.Game.Players[1].HfPos.DistanceTo(spot) < 10f, "the other screen sees it rooted there");
        Step(Game.HfReelTime + 0.4f, false, false);
        Check(me.HfMode == Game.HfRest, "then it reels back to you");
        Step(dt * 2f, false, true);
        Step(dt * 2f, false, false);
        Check(me.HfMode != Game.HfFly, "Alt does nothing while it cools down");

        // Pacemaker
        var card = Upgrades.All.First(u => u.Name == "Pacemaker");
        Check(card.Weapons != null && card.Weapons.Contains(WeaponId.HeartFlail), "Pacemaker is the Heart Flail's card");
        card.Apply(me.Stats);
        Check(me.Stats.HfBpmMult > 1.4f && me.Stats.HfPulseMult > 1.4f, "Pacemaker: the beat quickens faster and pulses heal more");
    }
}
