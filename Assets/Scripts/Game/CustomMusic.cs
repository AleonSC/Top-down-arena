using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Your own tracks for the music player: any .wav, .mp3 or .ogg dropped in the music folder (shown in Settings >
    /// Audio > Music player, with an Open folder button). Plain C# (no Unity), so SimTest covers it. WAV files are
    /// decoded here on a background thread; mp3 and ogg are decoded by Unity (MusicPlayerUi.cs) and handed over
    /// through Decoded.
    public static class CustomMusic
    {
        public static readonly string[] Extensions = { ".wav", ".mp3", ".ogg" };
        public const string IdPrefix = "file:";

        public static bool IsSupported(string path) => Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;
        public static bool NeedsUnity(string path) => Path.GetExtension(path).ToLowerInvariant() != ".wav";

        /// "my_cool-song.mp3" -> "my cool-song"
        public static string TitleOf(string path)
        {
            string t = Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Trim();
            return t.Length == 0 ? "Untitled" : t;
        }

        /// The supported files in the folder (made if it doesn't exist), sorted by name. Never throws.
        public static List<string> Scan(string folder)
        {
            var found = new List<string>();
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var f in Directory.GetFiles(folder))
                    if (IsSupported(f))
                        found.Add(f);
                found.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception) { }
            return found;
        }

        /// Library entries for those files (appended to the music player's list after the built-in tracks).
        public static List<MusicLibrary.Entry> Entries(List<string> files)
        {
            var list = new List<MusicLibrary.Entry>();
            foreach (var f in files)
            {
                string path = f, id = IdPrefix + Path.GetFileName(f);
                list.Add(new MusicLibrary.Entry
                {
                    Id = id, Title = TitleOf(f), Mood = "Custom", Gain = 0.6f, Path = path, NeedsUnity = NeedsUnity(f),
                    Blurb = Path.GetFileName(f),
                    Render = NeedsUnity(f) ? (Func<float[]>)(() => TakeDecoded(id)) : () => LoadWav(path, id),
                });
            }
            return list;
        }

        // --- Tracks Unity decoded (mp3, ogg), waiting to be turned into clips ------------------------------------------

        static readonly Dictionary<string, float[]> decoded = new Dictionary<string, float[]>();
        static readonly HashSet<string> failedIds = new HashSet<string>();   // ids that couldn't be loaded (shown in the list)
        public static bool IsFailed(string id) { lock (failedIds) return id != null && failedIds.Contains(id); }
        public static void MarkFailed(string id) { lock (failedIds) failedIds.Add(id); }
        public static void ClearFailed() { lock (failedIds) failedIds.Clear(); }

        public static bool HasDecoded(string id) { lock (decoded) return decoded.ContainsKey(id); }
        public static void Give(string id, float[] mono) { lock (decoded) decoded[id] = mono; }
        static float[] TakeDecoded(string id)
        {
            lock (decoded)
            {
                decoded.TryGetValue(id, out var s);
                decoded.Remove(id);  // (the clip keeps the audio; this frees the copy)
                return s ?? new float[MenuMusic.Rate];
            }
        }

        // --- Decoding ------------------------------------------------------------------------------------------------------

        public static float[] LoadWav(string path, string id = null)
        {
            float[] s = null;
            try { s = DecodeWav(File.ReadAllBytes(path)); }
            catch (Exception) { }
            if (s == null)
            {
                if (id != null)
                    MarkFailed(id);   // (e.g. a compressed WAV)
                return new float[MenuMusic.Rate];
            }
            return s;
        }

        /// 8 / 16 / 24 / 32-bit PCM or 32-bit float WAV, any rate and channel count, to mono at the game's rate. Null if it isn't one.
        public static float[] DecodeWav(byte[] b)
        {
            if (b == null || b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F' || b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E')
                return null;
            int format = 0, channels = 0, rate = 0, bits = 0, dataAt = -1, dataLen = 0;
            int p = 12;
            while (p + 8 <= b.Length)
            {
                int size = BitConverter.ToInt32(b, p + 4);
                bool fmt = b[p] == 'f' && b[p + 1] == 'm' && b[p + 2] == 't' && b[p + 3] == ' ';
                bool data = b[p] == 'd' && b[p + 1] == 'a' && b[p + 2] == 't' && b[p + 3] == 'a';
                if (fmt && p + 8 + 16 <= b.Length)
                {
                    format = BitConverter.ToUInt16(b, p + 8);
                    channels = BitConverter.ToUInt16(b, p + 10);
                    rate = BitConverter.ToInt32(b, p + 12);
                    bits = BitConverter.ToUInt16(b, p + 22);
                    if (format == 0xFFFE && size >= 26 && p + 8 + 26 <= b.Length)
                        format = BitConverter.ToUInt16(b, p + 8 + 24);  // WAVE_FORMAT_EXTENSIBLE: the real format is in the sub-format
                }
                else if (data)
                {
                    dataAt = p + 8;
                    dataLen = size < 0 || dataAt + size > b.Length ? b.Length - dataAt : size;  // (streamed files can claim too much)
                    break;
                }
                if (size < 0)
                    return null;
                p += 8 + size + (size & 1);
            }
            if (dataAt < 0 || channels < 1 || channels > 8 || rate < 1000 || rate > 400000)
                return null;
            bool isFloat = format == 3 && bits == 32;
            if (!(format == 1 && (bits == 8 || bits == 16 || bits == 24 || bits == 32)) && !isFloat)
                return null;
            int bytes = bits / 8, frames = dataLen / (bytes * channels);
            if (frames < 1)
                return null;
            var all = new float[frames * channels];
            for (int i = 0; i < all.Length; i++)
            {
                int o = dataAt + i * bytes;
                float v;
                switch (bits)
                {
                    case 8: v = (b[o] - 128) / 128f; break;
                    case 16: v = BitConverter.ToInt16(b, o) / 32768f; break;
                    case 24: v = ((b[o] << 8 | b[o + 1] << 16 | b[o + 2] << 24) >> 8) / 8388608f; break;
                    default: v = isFloat ? BitConverter.ToSingle(b, o) : BitConverter.ToInt32(b, o) / 2147483648f; break;
                }
                all[i] = v;
            }
            return Finish(all, channels, rate);
        }

        /// Interleaved samples at any rate -> mono at MenuMusic.Rate, quiet ends so it loops without a click, and the
        /// peak set like the built-in tracks (0.8) so tracks sit at a similar loudness.
        public static float[] Finish(float[] interleaved, int channels, int rate)
        {
            int frames = interleaved.Length / Math.Max(1, channels);
            var mono = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += interleaved[f * channels + c];
                mono[f] = sum / channels;
            }
            float[] outp = mono;
            if (rate != MenuMusic.Rate)
            {
                int n = (int)((long)frames * MenuMusic.Rate / rate);
                outp = new float[Math.Max(1, n)];
                double step = (double)rate / MenuMusic.Rate;
                for (int i = 0; i < outp.Length; i++)
                {
                    double src = i * step;
                    int i0 = (int)src;
                    float fr = (float)(src - i0);
                    float a = mono[Math.Min(i0, frames - 1)], c2 = mono[Math.Min(i0 + 1, frames - 1)];
                    outp[i] = a + (c2 - a) * fr;
                }
            }
            float peak = 0f;
            foreach (float v in outp)
                peak = Math.Max(peak, Math.Abs(v));
            float gain = peak > 0.0001f ? 0.8f / peak : 1f;
            int fade = Math.Min(outp.Length / 2, MenuMusic.Rate / 50);  // 20 ms
            for (int i = 0; i < outp.Length; i++)
            {
                float edge = i < fade ? (float)i / fade : outp.Length - 1 - i < fade ? (float)(outp.Length - 1 - i) / fade : 1f;
                outp[i] = float.IsNaN(outp[i]) ? 0f : outp[i] * gain * edge;
            }
            return outp;
        }
    }
}
