using System.Linq;
using System;
using System.Reflection;
using Arena;

// Sanctum: the dome heals in seven pulses (the last bigger), each ally as the ring reaches them, and nobody outside the dome.
static partial class Program
{
    static void SanctumTest(float dt)
    {
        var s = StartBuffed(dt, 3, 0);
        Run(s, dt, 2, null);
        var g = s[0].Game;
        g.Bot = false; s[1].Game.Bot = false;
        var me = g.Players[1];
        var ally = g.Players.Values.First(p => p != me);
        var out2 = g.Players.Values.FirstOrDefault(p => p != me && p != ally);
        V2 c = new V2(Game.Width / 2f, Game.Height / 2f);
        foreach (var q in g.Players.Values)
        {
            g.DevSetGod(q.Id, true);
            q.MaxHp = 1000f; q.Hp = 100f; q.Downed = false;
        }
        me.Pos = c; ally.Pos = c + new V2(180f, 0f);  // a pulse takes 0.5 s to reach them
        if (out2 != null) out2.Pos = c + new V2(0f, 380f);  // outside the 200 px dome
        var add = typeof(Game).GetMethod("AddZone", BindingFlags.NonPublic | BindingFlags.Instance);
        var dome = (Zone)add.Invoke(g, new object[] { ZoneKind.Sanctum, me, c, 200f, 7f });
        var meBeats = new System.Collections.Generic.List<float>(); var allyBeats = new System.Collections.Generic.List<float>();
        float myHp = me.Hp, allyHp = ally.Hp, outHp = out2 != null ? out2.Hp : 0f, myGain = 0f;
        Run(s, dt, 7.5f, null, () =>
        {
            foreach (var q in g.Players.Values) { q.Pos = q == me ? c : q == ally ? c + new V2(180f, 0f) : q.Pos; }
            foreach (var q in s[1].Game.Players.Values) if (q.Id == ally.Id) q.Pos = c + new V2(180f, 0f);  // the friend's own screen decides where they stand
            if (me.Hp - myHp > 5f) { meBeats.Add(dome.Duration - dome.T); myGain += me.Hp - myHp; }
            if (ally.Hp - allyHp > 5f) allyBeats.Add(dome.Duration - dome.T);
            myHp = me.Hp; allyHp = ally.Hp;
        });
        Console.WriteLine("  sanctum: {0} pulses on the caster (gain {1:0}), {2} on the ally; first at {3:0.00}/{4:0.00} s", meBeats.Count, myGain, allyBeats.Count,
            meBeats.Count > 0 ? meBeats[0] : -1f, allyBeats.Count > 0 ? allyBeats[0] : -1f);
        Check(meBeats.Count == Game.SanctumPulses, "seven healing pulses over the dome's 7 s");
        Check(Math.Abs(myGain - (6 * Game.SanctumPulseHeal + Game.SanctumFinaleHeal)) < 8f, "six pulses of 20 and a finale of 30");
        Check(allyBeats.Count == Game.SanctumPulses && allyBeats[0] > meBeats[0] + 0.3f, "an ally further out is healed later, as the ring reaches them");
        if (out2 != null) Check(out2.Hp == outHp, "someone outside the dome gets nothing");
        Check(g.Zones.Values.All(z => z.Kind != ZoneKind.Sanctum), "the dome is gone after 7 s");
        s[0].Leave(""); s[1].Leave("");
    }
}
