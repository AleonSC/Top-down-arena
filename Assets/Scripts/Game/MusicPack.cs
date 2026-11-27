using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public enum MusicMood { Chill, Action, Dark, Epic, Custom }

    /// Your own music, assigned in the Unity Inspector. Make one with Assets > Create > Arena > Music Pack and save it as
    /// Assets/Resources/MusicPack (that exact name and folder). Everything here is optional:
    ///  - Main Menu / Lobby / Wave Battle / Bosses: play your clip instead of the generated track.
    ///  - Factions: a battle track for the regular waves of each boss's faction (the waves leading up to that boss).
    ///  - Tracks: extra songs for the music player (Settings > Audio > Music player), with your own title and mood.
    /// Even simpler: drop audio files into Assets/Resources/Music and they appear in the music player on their own.
    [CreateAssetMenu(fileName = "MusicPack", menuName = "Arena/Music Pack")]
    public class MusicPack : ScriptableObject
    {
        [Serializable]
        public class Slot
        {
            public AudioClip Clip;
            [Range(0f, 1f)] public float Volume = 0.6f;
        }

        [Serializable]
        public class BossSlot
        {
            [Tooltip("The Twin Reapers share one slot: pick ReaperOfDusk.")] public EnemyType Boss = EnemyType.Warden;
            public Slot Music = new Slot();
        }

        [Serializable]
        public class Extra
        {
            [Tooltip("Shown in the player. Leave empty to use the clip's name.")] public string Title;
            public AudioClip Clip;
            public MusicMood Mood = MusicMood.Custom;
            [Range(0f, 1f)] public float Volume = 0.6f;
        }

        [Tooltip("Replaces the main menu theme.")] public Slot MainMenu = new Slot();
        [Tooltip("Replaces the lobby theme.")] public Slot Lobby = new Slot();
        [Tooltip("Replaces the soft track under normal waves.")] public Slot WaveBattle = new Slot();
        [Tooltip("Plays instead of Wave Battle during the regular waves before this boss (its faction's waves).")] public List<BossSlot> Factions = new List<BossSlot>();
        [Tooltip("Replaces a boss's theme while it is on the field.")] public List<BossSlot> Bosses = new List<BossSlot>();
        [Tooltip("Extra songs for the music player.")] public List<Extra> Tracks = new List<Extra>();
    }
}
