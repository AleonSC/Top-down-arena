using UnityEngine;

namespace Arena
{
    /// The upgrade picker's sounds, on your own screen only (SoundBank.cs recipes): played as the opening (PickIntro.cs)
    /// and the close (PickOutro.cs) pass each moment, so they follow the same timings as what's drawn.
    ///   opening: the pillar rising (a full opening only), each card flying out and landing (a sparkle for very rare and up)
    ///   close:   the cards flying back into you, then yours landing in you
    public partial class ArenaApp
    {
        string pickSoundOffer = "";
        float pickSoundIntro = -1f, pickSoundOutro = -1f, pickSoundOutroAt = -10f;

        /// Update: while the picker is up (or closing), play each sound as its moment passes.
        void TickPickSounds(Player me, bool choosing)
        {
            float now = Time.unscaledTime;
            if (choosing && !tabHeld && !PickOutroPlaying && game.State == MatchState.Intermission && me.Offer != null)
            {
                if (pickSoundOffer != shownOffer)
                {
                    pickSoundOffer = shownOffer;  // new cards: their opening starts
                    pickSoundIntro = -1f;
                }
                float e = IntroElapsed;
                if (introFull && Passed(pickSoundIntro, e, 0f))
                    sounds.Play(Sfx.PickRise, 0.8f, now);
                for (int i = 0; i < me.Offer.Length; i++)
                {
                    float a = IntroCardsAt + Mathf.Min(i, 5) * IntroCardGap;
                    if (Passed(pickSoundIntro, e, a))
                        sounds.Play(Sfx.PickCardOut, 0.7f, now);
                    if (Passed(pickSoundIntro, e, IntroLanded(i)))
                    {
                        sounds.Play(Sfx.PickCardLand, 0.7f, now);
                        if (Upgrades.All[me.Offer[i]].Rarity >= Rarity.VeryRare)
                            sounds.Play(Sfx.PickRare, 0.7f, now);
                    }
                }
                pickSoundIntro = e;
            }
            if (pickOutroAt != pickSoundOutroAt)
            {
                pickSoundOutroAt = pickOutroAt;  // a card was just taken: its close starts
                pickSoundOutro = -1f;
            }
            float o = now - pickOutroAt;
            if (o >= 0f && o < PickOutroTime + 0.2f)
            {
                if (Passed(pickSoundOutro, o, 0.02f))
                    sounds.Play(Sfx.PickReturn, 0.6f, now);  // the others fly home
                if (Passed(pickSoundOutro, o, OutroPickFrom + 0.05f))
                    sounds.Play(Sfx.PickReturn, 0.8f, now);  // then yours
                if (Passed(pickSoundOutro, o, OutroPickLand))
                    sounds.Play(Sfx.PickAbsorb, 0.85f, now);
                pickSoundOutro = o;
            }
        }

        /// Did the clock just pass `at` (it was before it last frame, and is at or past it now)?
        static bool Passed(float last, float now, float at) => last < at && now >= at;
    }
}
