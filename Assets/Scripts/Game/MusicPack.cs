using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public enum MusicMood { Chill, Action, Dark, Epic, Custom }

    /// Your own music, assigned in the Unity Inspector. Make one with Assets > Create > Arena > Music Pack and save it in
    /// any Resources folder (Assets/Resources/MusicPack is the usual name). Everything here is optional:
    ///  - Main Menu / Wave Battle / Bosses: play your clip instead of the generated track.
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
            [Tooltip("The Twin Reapers share one slot: pick TwinA.")] public EnemyType Boss = EnemyType.Boss;
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

        [Tooltip("Replaces the menu and lobby theme.")] public Slot MainMenu = new Slot();
        [Tooltip("Replaces the soft track under normal waves.")] public Slot WaveBattle = new Slot();
        [Tooltip("Replaces a boss's theme while it is on the field.")] public List<BossSlot> Bosses = new List<BossSlot>();
        [Tooltip("Extra songs for the music player.")] public List<Extra> Tracks = new List<Extra>();
    }
}
