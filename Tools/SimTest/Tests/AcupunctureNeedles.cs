using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Healer Ninja's Acupuncture Needles (AcupunctureNeedles.cs).
static partial class Program
{
    /// Fire: needles fly through enemies and stick in a hurt teammate, healing over time (the other screen sees them); the
    /// fifth opens the meridian; they fall out after NeedleTime. Alt: Bloodletting hits, sticks and bleeds, on a cooldown;
    /// the fifth in one enemy bursts at the pressure point; a dying enemy's needles fly back to heal the most hurt player.
    static void NeedlesTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Needle", Role.Healer, 0, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var mate = g.Players[2];
        g.DevSetGod(1, true);
        var w = Defs.WeaponOf(WeaponId.AcupunctureNeedles);
        Check(me.Weapon.Id == WeaponId.AcupunctureNeedles && w.Name == "Acupuncture Needles" && w.Desc.Contains("Alternative Fire"),
            "the Healer Ninja's first weapon is the Acupuncture Needles, with an Alt (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f);
        V2 mateAt = from + new V2(300f, 0f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action Clear = () => { queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Zones.Clear(); g.Effects.Clear(); };
        Action<float, V2, bool, bool> Step = (secs, aim, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; mate.Pos = mateAt; client.Game.Players[2].Pos = mateAt; queue.Clear();
                foreach (var x in s)
                {
                    x.Poll();
                    var input = x == host ? new PlayerInput { AimAt = aim, Fire = fire && f == 0, WeaponAction = alt && f < 3 } : new PlayerInput { AimAt = mateAt + new V2(300f, 0f) };
                    x.Game.FixedStep(dt, input);
                }
            }
        };
        Clear();
        Step(0.5f, from + new V2(0f, -300f), false, false);

        // Fire through an enemy into a hurt teammate: the enemy is untouched, the teammate gets a needle
        var between = Dummy(from + new V2(150f, 0f));
        mate.Hp = mate.MaxHp * 0.3f;
        Step(0.4f, mateAt, true, false);
        Check(between.Hp >= 1e6f, "a healing needle flies through an enemy without hurting it (" + (1e6f - between.Hp) + ")");
        Check(mate.NeedleN == 1 && mate.NeedleT > 0f, "and sticks in the hurt teammate behind it (" + mate.NeedleN + ")");
        Step(0.4f, mateAt, true, false);
        Step(0.4f, mateAt, true, false);
        Check(mate.NeedleN == 3 && client.Game.Players[2].NeedleN == 3, "three needles; the other screen sees them (" + client.Game.Players[2].NeedleN + ")");
        float h0 = mate.Hp;
        Step(1f, mateAt, false, false);
        float perSec = mate.Hp - h0;
        float want = 3f * Game.NeedleHeal * me.Stats.DmgMult;
        Check(Math.Abs(perSec - want) < want * 0.15f, "three needles heal " + want.ToString("0.0") + " a second (" + perSec.ToString("0.0") + ")");

        // the fifth opens the meridian: a burst, the needles out
        mate.Hp = mate.MaxHp * 0.3f;
        g.Effects.Clear();
        Step(0.4f, mateAt, true, false);
        float before = mate.Hp;
        Step(0.4f, mateAt, true, false);
        Check(mate.NeedleN == 0 && mate.Hp - before >= Game.MeridianHeal * me.Stats.DmgMult - 0.5f, "the fifth needle opens the meridian: +" + (mate.Hp - before).ToString("0.0") + ", the needles out");
        Check(g.Effects.Any(fx => fx.Kind == FxKind.NeedleBurst && fx.Pos2.X == 0f), "with the jade burst (FxKind.NeedleBurst)");

        // they fall out NeedleTime after the last one
        Step(0.4f, mateAt, true, false);
        Check(mate.NeedleN == 1, "a new needle after the burst starts over");
        Step(Game.NeedleTime + 0.2f, mateAt, false, false);
        Check(mate.NeedleN == 0 && client.Game.Players[2].NeedleN == 0, "needles fall out after " + Game.NeedleTime + " s");

        // a teammate at full health: the needle flies past
        mate.Hp = mate.MaxHp;
        Step(0.4f, mateAt, true, false);
        Check(mate.NeedleN == 0, "a needle doesn't stick in a teammate at full health");

        // Alt: Bloodletting, three needles that hit, stick and bleed; a cooldown
        Clear();
        mateAt = new V2(400f, 900f);
        var target = Dummy(from + new V2(200f, 0f));
        Step(0.5f, target.Pos, false, true);
        float hit = 1e6f - target.Hp;
        Check(target.NeedlesIn == 3 && target.BleedStacks == 3, "Bloodletting: three needles stick and bleed (" + target.NeedlesIn + " in, " + target.BleedStacks + " stacks)");
        Check(hit >= 3f * w.Damage * me.Stats.DmgMult - 0.5f, "each hits for the weapon's damage (" + hit.ToString("0.0") + ")");
        Check(client.Game.Enemies.ContainsKey(target.Id) && client.Game.Enemies[target.Id].NeedlesIn == 3, "the other screen sees the needles in it");
        Step(dt, target.Pos, false, true);
        Step(0.2f, target.Pos, false, false);
        Check(target.NeedlesIn == 3, "Alt again during its " + Game.BloodletCd + " s cooldown throws nothing");

        // the fifth in one enemy bursts at the pressure point
        Step(Game.BloodletCd, target.Pos, false, false);
        g.Effects.Clear();
        float pre = target.Hp;
        Step(0.5f, target.Pos, false, true);
        float second = pre - target.Hp;
        Check(target.NeedlesIn == 1 && g.Effects.Any(fx => fx.Kind == FxKind.NeedleBurst && fx.Pos2.X == 1f), "the fifth needle bursts at the pressure point (the sixth sticks: " + target.NeedlesIn + " in)");
        Check(second >= 3f * w.Damage * me.Stats.DmgMult + Game.PressureDmg * me.Stats.DmgMult - 0.5f, "for " + Game.PressureDmg + " more (" + second.ToString("0.0") + " that volley)");
        Step(Game.BloodletCd + 3.5f, target.Pos, false, false);
        Check(target.NeedlesIn == 0, "Bloodletting needles fall out with the bleed");

        // Needles Return: a dying enemy's needles fly back to the most hurt player
        Clear();
        mateAt = from + new V2(0f, 250f);
        var victim = Dummy(from + new V2(200f, 0f));
        Step(0.5f, victim.Pos, false, true);
        Check(victim.NeedlesIn == 3, "three needles in the enemy before it dies");
        mate.Hp = mate.MaxHp * 0.3f;
        victim.Hp = 1f;
        float mh = mate.Hp;
        Step(Game.BloodletCd, victim.Pos, false, false);
        Step(0.3f, victim.Pos, false, true);
        Check(!g.Enemies.ContainsKey(victim.Id), "the next volley kills it");
        Step(1.5f, victim.Pos, false, false);
        float back = mate.Hp - mh;
        Check(back >= 3f * Game.ReturnHeal - 0.5f, "its three needles fly back to the hurt teammate, healing " + Game.ReturnHeal + " each (" + back.ToString("0.0") + ")");
    }
}
