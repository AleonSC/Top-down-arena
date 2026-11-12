using System;
using System.Collections.Generic;

namespace Arena
{
    /// One thing the first-run tutorial asks you to do (Tutorial.cs).
    public enum TutorialStep : byte { Move, Aim, Shoot, Dodge, PickCard, Ability, Parry, OpenBuild, DrawRoom, Vote, Gold, Shop }

    /// A group of tutorial steps shown together on the prompt.
    public class TutorialLesson
    {
        public string Title, When, Hint;
        public TutorialStep[] Steps;
    }

    /// The guided first run (Docs/QuickPlay.md step 3): a profile's first Quick Play shows prompts that tick off as you do each
    /// thing. Each player's own profile decides, on their own machine: nothing goes over the network and nothing waits for it
    /// (between waves the ready-up already waits for you). Steps can be done in any order; the prompt shows the first lesson
    /// with something left. Done, skipped, or once the first Quick Play is recorded: never again (Profile.TutorialDone).
    public class Tutorial
    {
        public static readonly TutorialLesson[] Lessons =
        {
            new TutorialLesson { Title = "THE BASICS", When = "Wave 1", Hint = "Enemies chase you. Keep moving and keep shooting.",
                Steps = new[] { TutorialStep.Move, TutorialStep.Aim, TutorialStep.Shoot, TutorialStep.Dodge } },
            new TutorialLesson { Title = "LEVEL UP", When = "Between waves", Hint = "Kills give XP. Every level offers three upgrade cards.",
                Steps = new[] { TutorialStep.PickCard } },
            new TutorialLesson { Title = "BUILD A ROOM", When = "Between waves: a build round is open", Hint = "Walls cost vectors. Build toward the sealed shop and vendor rooms.",
                Steps = new[] { TutorialStep.OpenBuild, TutorialStep.DrawRoom, TutorialStep.Vote } },
            new TutorialLesson { Title = "YOUR KIT", When = "During a wave", Hint = "Your ability recharges. The parry shield blocks shots.",
                Steps = new[] { TutorialStep.Ability, TutorialStep.Parry } },
            new TutorialLesson { Title = "GOLD AND THE SHOP", When = "Once a shop room is connected", Hint = "Kills drop gold once a shop or vendor room joins the map.",
                Steps = new[] { TutorialStep.Gold, TutorialStep.Shop } },
        };

        public const float MoveNeeded = 0.8f, AimNeeded = 400f, ShootNeeded = 0.5f, ParryNeeded = 0.25f;  // seconds held / px the aim travelled

        public bool Active;
        readonly HashSet<TutorialStep> done = new HashSet<TutorialStep>();
        float moveT, aimDist, shootT, parryT;
        V2 lastAim;
        bool aimSet;
        int takenAtStart, boughtAtStart, goldBefore;

        public bool Done(TutorialStep s) => done.Contains(s);
        public int StepsDone => done.Count;
        public static int StepCount { get { int n = 0; foreach (var l in Lessons) n += l.Steps.Length; return n; } }

        /// The first lesson with a step left (-1: all done).
        public int Current
        {
            get
            {
                for (int i = 0; i < Lessons.Length; i++)
                    if (!LessonDone(i))
                        return i;
                return -1;
            }
        }

        public bool LessonDone(int i)
        {
            foreach (var s in Lessons[i].Steps)
                if (!done.Contains(s))
                    return false;
            return true;
        }

        public const int BuildLesson = 2, KitLesson = 3;

        /// Can this lesson be done right now? Building only while a build round is open, the kit only in a fight.
        public static bool LessonNow(int i, Game g) =>
            i == BuildLesson ? g.ExpandOpen && g.State == MatchState.Intermission : i == KitLesson ? g.State == MatchState.Fighting : true;

        /// The lesson on the prompt: the first with a step left that can be done now (a build round right after the first
        /// upgrade pick shows BUILD A ROOM; the next wave shows YOUR KIT), else the first with a step left (-1: all done).
        public int CurrentFor(Game g)
        {
            for (int i = 0; i < Lessons.Length; i++)
                if (!LessonDone(i) && LessonNow(i, g))
                    return i;
            return Current;
        }

        /// Every game, as a match starts: on for a profile's first Quick Play (not for a bot, a replay or the lobby preview).
        public void Begin(Game g)
        {
            done.Clear();
            moveT = aimDist = shootT = parryT = 0f;
            aimSet = false;
            var me = g.Me;
            var prof = g.Profile;
            Active = g.Quick && me != null && prof != null && !prof.TutorialDone && !g.Bot && !g.Replaying && !g.ShowcaseMode && !g.Unrecorded;
            takenAtStart = me != null ? me.Taken.Count : 0;
            boughtAtStart = Expansion.BoughtCount(g.Expanded);
            goldBefore = me != null ? me.Gold : 0;
        }

        /// A step the front end saw (opening the build screen).
        public void Note(TutorialStep s)
        {
            if (Active)
                done.Add(s);
        }

        /// Every game, each fixed step, with this machine's own input.
        public void Tick(Game g, PlayerInput input, float dt, out bool finished)
        {
            finished = false;
            var me = g.Me;
            if (!Active || me == null || !g.Running)
                return;
            bool fighting = g.State == MatchState.Fighting && !me.Downed;
            if (fighting)
            {
                if (input.Move.Length > 0.3f && (moveT += dt) >= MoveNeeded)
                    done.Add(TutorialStep.Move);
                if (aimSet)
                    aimDist += (input.AimAt - lastAim).Length;
                lastAim = input.AimAt;
                aimSet = true;
                if (aimDist >= AimNeeded)
                    done.Add(TutorialStep.Aim);
                if (input.Fire && (shootT += dt) >= ShootNeeded)
                    done.Add(TutorialStep.Shoot);
                if (input.Dodge)
                    done.Add(TutorialStep.Dodge);
                if (input.Ability || input.AbilityQ || input.AbilityE || input.AbilityShift)
                    done.Add(TutorialStep.Ability);
                if (input.Parry && (parryT += dt) >= ParryNeeded)
                    done.Add(TutorialStep.Parry);
            }
            if (me.Taken.Count > takenAtStart)
                done.Add(TutorialStep.PickCard);
            if (g.ExpandProposals.Exists(q => q.Owner == me.Id))
                done.Add(TutorialStep.DrawRoom);
            if (done.Contains(TutorialStep.DrawRoom) && (g.ExpandVotes.ContainsKey(me.Id) || Expansion.BoughtCount(g.Expanded) > boughtAtStart))
                done.Add(TutorialStep.Vote);
            if (me.Gold > goldBefore)
                done.Add(TutorialStep.Gold);
            goldBefore = Math.Min(goldBefore, me.Gold);
            if (g.InServiceRoom(me))
                done.Add(TutorialStep.Shop);
            if (Current < 0)
            {
                Active = false;
                finished = true;
            }
        }

        /// Previews and tests: show lesson `lesson` with its first `stepsDone` steps ticked (the lessons before it done).
        public void ShowAt(int lesson, int stepsDone)
        {
            Active = true;
            done.Clear();
            for (int i = 0; i < Lessons.Length && i <= lesson; i++)
                for (int k = 0; k < Lessons[i].Steps.Length; k++)
                    if (i < lesson || k < stepsDone)
                        done.Add(Lessons[i].Steps[k]);
        }

        /// The player skipped it (the prompt's key): off for good.
        public void Skip(Game g)
        {
            if (!Active)
                return;
            Active = false;
            g.FinishTutorial();
        }
    }

    public partial class Game
    {
        /// The first-run tutorial on this machine (Tutorial.cs).
        public readonly Tutorial Tutorial = new Tutorial();

        /// This machine's tutorial is over (done or skipped): your profile won't show it again.
        public void FinishTutorial()
        {
            var prof = Profile;
            if (prof == null || prof.TutorialDone)
                return;
            prof.TutorialDone = true;
            prof.Save();
        }

        void TickTutorial(float dt, PlayerInput input)
        {
            bool finished;
            Tutorial.Tick(this, input, dt, out finished);
            if (finished)
                FinishTutorial();
        }
    }
}
