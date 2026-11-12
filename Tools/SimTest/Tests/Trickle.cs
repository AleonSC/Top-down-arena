using System;
using System.Linq;
using Arena;

// The Long Campaign's handover (Approaches.cs): the last boss's faction fades over three shown waves (40/20/8%), its tricky
// members first to go (FactionMember.Fade), and a lesson's Trickle star only comes late in the wave.
static partial class Program
{
    static void TrickleTest()
    {
        Check(Approaches.FadeAt(6, 11) == 0.40f && Approaches.FadeAt(6, 12) == 0.20f && Approaches.FadeAt(7, 13) == 0.08f && Approaches.FadeAt(7, 14) == 0f && Approaches.FadeAt(8, 15) == 0f,
            "the fading faction's share: 40%, 20%, 8% on the three waves after the boss, then none");
        Check(Approaches.FadeAt(3, 5) == 0f && Approaches.FadeAt(6, 0) == Approaches.FadeShare[0], "no fade before the first boss; the plain track keeps its own curve");
        var warden = Approaches.For(EnemyType.Boss);
        Func<EnemyType, int> fade = t => warden.Members.First(m => m.Type == t).Fade;
        Check(fade(EnemyType.RiotShield) == 0 && fade(EnemyType.GasGrenadier) == 0 && fade(EnemyType.TaserGuard) == 0 && fade(EnemyType.SecurityGuard) == 1 && fade(EnemyType.Rioter) == 3 && fade(EnemyType.Guard) == 3,
            "the Warden's tricky enemies leave at the boss, the Security Guard after a wave, the Rioters and Guards last");
        var c = Approaches.Campaign(6, 11);
        var old = c.FindAll(x => !x.Current);
        Check(old.Count == 1 && old[0].Faction == warden && Math.Abs(old[0].Share - 0.40f) < 0.001f, "wave 11: the Warden's faction holds 40% of the wave (the new faction, once built, takes the rest at once)");
        Check(warden.Lessons.Count(l => l.Trickle) == 4 && warden.Lessons.Where(l => l.Trickle).All(l => l.Star.HasValue), "four of the Warden's lessons trickle their star in");
    }
}
