using System;
using System.IO;
using System.Reflection;
using Arena;

// Renders the synthesized sound effects to WAV files so they can be heard without Unity:
//   Tools/SoundCheck/render.sh [prefix]   (default prefix "Boss": the bosses' sounds) -> Tools/SoundCheck/out/*.wav
static class SoundCheck
{
    static void Main(string[] args)
    {
        string prefix = args.Length > 0 ? args[0] : "Boss";
        var bank = new SoundBank(new UnityEngine.GameObject("sound"));
        var clips = (UnityEngine.AudioClip[][])typeof(SoundBank).GetField("clips", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bank);
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "out");
        Directory.CreateDirectory(dir);
        int written = 0;
        foreach (Sfx s in Enum.GetValues(typeof(Sfx)))
        {
            if (!s.ToString().StartsWith(prefix) || clips[(int)s] == null)
                continue;
            var data = clips[(int)s][0].Data;
            Write(Path.Combine(dir, s + ".wav"), data, 44100);
            Console.WriteLine("  {0,-14} {1:0.00} s", s, data.Length / 44100f);
            written++;
        }
        Console.WriteLine("{0} sounds -> {1}", written, dir);
    }

    static void Write(string path, float[] data, int rate)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write("RIFF".ToCharArray()); w.Write(36 + data.Length * 2); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(data.Length * 2);
            foreach (float v in data)
                w.Write((short)(Math.Max(-1f, Math.Min(1f, v)) * 32767));
        }
    }
}
