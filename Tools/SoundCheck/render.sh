#!/bin/bash
# Usage: Tools/SoundCheck/render.sh [prefix]   Renders the synthesized sounds whose names start with prefix
# (default "Boss") to Tools/SoundCheck/out/*.wav, from the game's own recipes (SoundBank.cs). Needs mono.
set -e
cd "$(dirname "$0")"
# The preview stubs, but audio clips keep their samples and components exist (SoundBank builds voices).
sed -e 's|public class AudioClip : Object {.*$|public class AudioClip : Object { public int channels = 1, frequency = 44100, samples; public float length; public float[] Data; public bool GetData(float[] d, int off){return false;} public static AudioClip Create(string n, int len, int ch, int freq, bool stream){return new AudioClip { Data = new float[len], samples = len };} public bool SetData(float[] d, int off){ System.Array.Copy(d, 0, Data, off, d.Length); return true;} }|' \
    -e 's|public T AddComponent<T>() where T: Component { return null; }|public T AddComponent<T>() where T: Component { return (T)System.Activator.CreateInstance(typeof(T)); }|' \
    ../Preview/Stubs.cs > /tmp/soundcheck-stubs.cs
mcs -langversion:7.2 -nologo -nowarn:414,169,649,219,618,67 -out:SoundCheck.exe SoundCheck.cs /tmp/soundcheck-stubs.cs ../../Assets/Scripts/Core/*.cs ../../Assets/Scripts/Game/*SoundBank*.cs ../../Assets/Scripts/Game/MenuMusic.cs ../../Assets/Scripts/Game/GameMusic.cs ../../Assets/Scripts/Game/MusicLibrary.cs ../../Assets/Scripts/Game/CustomMusic.cs
mono SoundCheck.exe "$@"
