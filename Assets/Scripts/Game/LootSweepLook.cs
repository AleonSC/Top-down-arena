using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The floor-loot sweep as the next wave starts (Docs/RoundFlow.md step 4; host side: Core/Coins.cs SweepFloorLoot): every coin of
    /// yours still lying about flies to you in a quick gold stream and "+86 GOLD" pops over you. Vector pickups show through the usual
    /// "+n VECTORS" (VectorGainLook.cs). The app watches its own coins through the break and, on the frame the wave starts, counts the
    /// ones that went.
    public partial class ArenaApp
    {
        struct SweptCoin { public V2 From; public float Delay; }

        readonly Dictionary<int, V2> sweepSeen = new Dictionary<int, V2>();     // your gold coins on the floor during the break: id -> where
        readonly Dictionary<int, int> sweepValue = new Dictionary<int, int>();  // ...and what each is worth
        readonly List<SweptCoin> sweptCoins = new List<SweptCoin>();
        readonly List<int> sweepScratch = new List<int>();
        MatchState sweepLastState;
        float sweepAge = 99f;
        int sweepGold;
        const float SweepSpan = 1.7f, SweepFly = 0.42f, SweepStagger = 0.025f;
        const int SweepMaxCoins = 18;

        void TickLootSweep(float dt)
        {
            sweepAge += dt;
            var me = game.Me;
            if (me == null || session.Screen != AppScreen.Game)
            {
                sweepSeen.Clear();
                sweepValue.Clear();
                sweepLastState = game.State;
                return;
            }
            if (game.State == MatchState.Intermission)
            {
                sweepSeen.Clear();
                sweepValue.Clear();
                foreach (var c in game.Coins.Values)
                    if (!c.Vec && c.Owner == game.MyId)
                    {
                        sweepSeen[c.Id] = c.Disp;
                        sweepValue[c.Id] = c.Value;
                    }
            }
            else if (sweepLastState == MatchState.Intermission && game.State == MatchState.Fighting)
            {
                // The wave started: the coins of yours that went with it were swept to you.
                sweepScratch.Clear();
                foreach (var kv in sweepSeen)
                    if (!game.Coins.ContainsKey(kv.Key))
                        sweepScratch.Add(kv.Key);
                int gold = 0;
                sweptCoins.Clear();
                for (int i = 0; i < sweepScratch.Count; i++)
                {
                    int id = sweepScratch[i];
                    gold += sweepValue[id];
                    if (sweptCoins.Count < SweepMaxCoins)
                        sweptCoins.Add(new SweptCoin { From = sweepSeen[id], Delay = sweptCoins.Count * SweepStagger });
                }
                if (gold > 0)
                {
                    sweepGold = gold;
                    sweepAge = 0f;
                    game.Sounds.Add(Sfx.PickupBig);
                }
                sweepSeen.Clear();
                sweepValue.Clear();
            }
            sweepLastState = game.State;
        }

        /// Tools/Preview (SWEEP=<age>): the sweep `age` seconds in, these coins worth `gold` in all.
        void PreviewLootSweep(float age, int gold, V2[] from)
        {
            sweptCoins.Clear();
            foreach (var f in from)
                sweptCoins.Add(new SweptCoin { From = f, Delay = sweptCoins.Count * SweepStagger });
            sweepGold = gold;
            sweepAge = age;
        }

        /// HUD: the coins streaming in, then "+n GOLD" over you, rising and fading.
        void DrawLootSweepHud()
        {
            var me = game.Me;
            if (me == null || sweepAge >= SweepSpan)
                return;
            V2 to = ToUi(me.Disp);
            var gold = CrtStyle.Gold;
            foreach (var c in sweptCoins)
            {
                float k = (sweepAge - c.Delay) / SweepFly;
                if (k <= 0f || k >= 1f)
                    continue;
                V2 from = ToUi(c.From);
                float e = k * k * (3f - 2f * k);
                V2 ctrl = new V2((from.X + to.X) / 2f, Mathf.Min(from.Y, to.Y) - 60f);  // (a little arc)
                V2 a = V2.Lerp(from, ctrl, e), b = V2.Lerp(ctrl, to, e), at = V2.Lerp(a, b, e);
                float s = 9f - 3f * k;
                Ui.Fill(new Rect(at.X - s / 2f, at.Y - s / 2f, s, s), gold.WithAlpha(1f - 0.3f * k));
            }
            float t = sweepAge - SweepFly;  // the label as the first coins land
            if (t <= 0f)
                return;
            float pop = t < 0.12f ? 1f + 0.4f * (1f - t / 0.12f) : 1f;
            float al = Mathf.Clamp01((SweepSpan - sweepAge) / 0.45f) * Mathf.Clamp01(t / 0.08f);
            V2 at2 = to + new V2(0f, -86f - 30f * Mathf.Clamp01(t / SweepSpan));
            string text = "+" + sweepGold + " GOLD";
            int size = Mathf.RoundToInt(20f * pop);
            var r = new Rect(at2.X - 150f, at2.Y - 16f, 300f, 32f);
            Ui.Label(new Rect(r.x + 1.5f, r.y + 2f, r.width, r.height), text, size, new Color(0.1f, 0.05f, 0f, 0.85f * al), TextAnchor.MiddleCenter, true);
            Ui.Label(r, text, size, gold.WithAlpha(al), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + 30f, r.width, 16f), "COLLECTED", 11, gold.WithAlpha(0.8f * al), TextAnchor.MiddleCenter, true);
        }
    }
}
