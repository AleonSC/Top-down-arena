using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// The newest files in a folder, for the saved-file lists (Practice setups, wave sets). The list is read from disk
    /// at most once a second (OnGUI runs every frame), and at once after Forget (a save).
    public static class FileListCache
    {
        sealed class Entry
        {
            public string[] Files = new string[0];
            public float ReadAt = float.NegativeInfinity;
        }

        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        const float Refresh = 1f;

        /// Up to take files matching pattern in folder, newest first (empty when the folder is missing or unreadable).
        public static string[] Newest(string folder, string pattern, int take)
        {
            string key = folder + "|" + pattern + "|" + take;
            Entry e;
            if (!cache.TryGetValue(key, out e))
                cache[key] = e = new Entry();
            float now = Time.unscaledTime;
            if (now - e.ReadAt < Refresh)
                return e.Files;
            e.ReadAt = now;
            try
            {
                e.Files = Directory.Exists(folder)
                    ? Directory.GetFiles(folder, pattern).OrderByDescending(File.GetLastWriteTime).Take(take).ToArray()
                    : new string[0];
            }
            catch (Exception)
            {
                e.Files = new string[0];
            }
            return e.Files;
        }

        /// After writing or deleting a file: the next list reads the disk again.
        public static void Forget() => cache.Clear();
    }
}
