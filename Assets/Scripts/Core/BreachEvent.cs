using System;
using System.Collections.Generic;

namespace Arena
{
    /// The breach event (Docs/SurpriseRooms.md step 3): on some breach waves (Breaches.IsBreachWave) one of the breaches flashes gold, and when the wave has run
    /// BreachEvent.Delay seconds (or ends first) the wall behind it opens onto a free surprise room.
    public static class BreachEvent
    {
        public const float Delay = 20f;   // seconds into the wave the room is revealed (sooner if the wave ends)
        public const int FromWave = 7;    // the first wave that can have one (breach waves are 3, 7, 11, 15, ...)
        public const float Chance = 0.5f; // about half the breach waves from then on
        public const float Behind = 30f;  // the room has floor this far behind the breach's wall

        /// Does this wave have one? A breach wave from FromWave on, about half of them, never the wave after a boss (that intermission gets a boss's
        /// gift). Seeded, so it is the same for a replay.
        public static bool IsEventWave(int seed, int wave) => Breaches.IsBreachWave(wave) && wave >= FromWave && wave % Expansion.Milestone != 1 && new DetRandom(seed * 977 + wave * 131 + 5).Float() < Chance;
    }

    /// An event's plan: the breach that flashes gold and the room that opens behind it.
    public struct BreachEventPlan
    {
        public Breach At;
        public SurpriseOption Option;
    }

    public partial class Game
    {
        /// Every game, for the look: the gold breach's position while an event is running (synced in Msg.ExpandState).
        public V2? EventMark;

        /// Vectors every player gets from each Stash that opens (Docs/BreachRooms.md).
        public const int StashVectors = 6;
        int breachRoomsWave = -1;

        BreachEventPlan? eventPlan;
        int eventPlanWave = -1;

        /// Host: this wave's event, planned once from the wave's breaches (a gold one, and a surprise room that fits behind its wall).
        /// Null when it is not an event wave or nothing fits.
        BreachEventPlan? EventPlan()
        {
            if (eventPlanWave == Wave)
                return eventPlan;
            if (!IsHost || !BreachesOn || Practice != null || Hunt != null || !BreachEvent.IsEventWave(expandSeed, Wave) || Expansion.BoughtCount(Expanded) > Expansion.MaxRooms - 2)
                return null;
            eventPlanWave = Wave;
            eventPlan = null;
            var bs = ActiveBreaches;
            var order = new List<int>();
            for (int i = 0; i < bs.Length; i++)
                order.Add(i);
            var rg = new DetRandom(expandSeed * 53 + Wave);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rg.Int(i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }
            foreach (int i in order)
            {
                string why;
                var pick = Expansion.Surprise(expandSeed, Wave, Expanded, NextSurpriseShape(), out why, bs[i].Pos - bs[i].In * BreachEvent.Behind);
                if (pick.HasValue)
                {
                    eventPlan = new BreachEventPlan { At = bs[i], Option = pick.Value };
                    break;
                }
            }
            if (eventPlan.HasValue)
            {
                EventMark = eventPlan.Value.At.Pos;
                DoLog("breach event: " + Expansion.SurpriseName(eventPlan.Value.Option.Shape) + " behind the breach at " + (int)EventMark.Value.X + "," + (int)EventMark.Value.Y);
                BroadcastExpansion();
            }
            return eventPlan;
        }

        /// Host, every tick of a wave: plans the event once the breaches are up, and reveals the room when the delay is up.
        void TickBreachEvent()
        {
            if (!IsHost || !ExpandingOn || State != MatchState.Fighting || BreachAge < Breaches.Telegraph)
                return;
            var plan = EventPlan();
            if (plan.HasValue && EventMark.HasValue && BreachAge >= BreachEvent.Delay)
                RevealBreachEvent();
            if (BreachAge >= BreachEvent.Delay)
                RevealBreachRooms();  // the Stashes open 20 s in too (or when the wave is cleared, if that comes first)
        }

        /// Host: the wall opens. The planned room is built (free), the breach goes, the gold clears. Also run as the wave ends.
        void RevealBreachEvent()
        {
            if (!IsHost || !EventMark.HasValue || !eventPlan.HasValue || eventPlanWave != Wave)
                return;
            var rooms = eventPlan.Value.Option.Rooms;
            eventPlan = null;
            EventMark = null;
            Expanded.AddRange(rooms);
            PinBreaches(rooms);
            expWorld = null;
            LayoutAge = 0f;
            NoteSurprise(rooms[0].Surprise);
            Sounds.Add(Sfx.Pick);
            DoLog("breach event: the wall opens onto a " + Expansion.SurpriseName((SurpriseShape)(rooms[0].Surprise - 1)));
            BroadcastExpansion();
        }

        /// Host, 20 s into a breach wave (or as it is cleared, if that comes first): each of its breaches (the gold one already went) opens onto a free Stash behind its wall
        /// (Docs/BreachRooms.md). Skips a breach with no legal spot, and stops within 2 rooms of the cap.
        void RevealBreachRooms()
        {
            if (BreachesOn)
                _ = ActiveBreaches;  // (make sure this wave's breaches are cached)
            if (!IsHost || !ExpandingOn || Practice != null || Hunt != null || breachCache == null || breachWave != Wave || !Breaches.IsBreachWave(Wave) || breachRoomsWave == Wave)
                return;
            breachRoomsWave = Wave;  // once a wave
            var made = new List<ExpansionRoom>();
            foreach (var b in breachCache)
            {
                if (Expansion.BoughtCount(Expanded) > Expansion.MaxRooms - 2)
                    break;
                var opts = Expansion.SurpriseOptions(Expanded, SurpriseShape.Stash, b.Pos - b.In * BreachEvent.Behind);
                if (opts.Count == 0)
                    continue;
                Expanded.AddRange(opts[0].Rooms);
                made.AddRange(opts[0].Rooms);
            }
            if (made.Count == 0)
                return;
            PinBreaches(made);
            foreach (var p in Players.Values)
                AddVectors(p, StashVectors * made.Count);  // each Stash holds vectors for the whole team
            expWorld = null;
            LayoutAge = 0f;
            NoteSurprise(made[0].Surprise);
            Sounds.Add(Sfx.Pick);
            DoLog("breach rooms: " + made.Count + " Stash(es) behind the wave's breaches");
            BroadcastExpansion();
        }
    }
}
