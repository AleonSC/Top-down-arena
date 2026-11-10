// Signature-only stubs of the Unity APIs used by the project, for compile checks.
using System;
namespace UnityEngine {
  public class Object { public string name; public HideFlags hideFlags; public static T FindFirstObjectByType<T>() where T: Object { return null; } public static void DontDestroyOnLoad(Object o){} public static void Destroy(Object o){} public static implicit operator bool(Object o){return o!=null;} }
  public enum HideFlags { None=0, HideAndDontSave=61 }
  public class Component : Object { public GameObject gameObject; public string tag; }
  public class Behaviour : Component {}
  public class MonoBehaviour : Behaviour {}
  public class GameObject : Object { public GameObject(string n){} public string tag; public T AddComponent<T>() where T: Component { return null; } }
  public enum CameraClearFlags { Skybox=1, SolidColor=2 }
  public class Camera : Behaviour { public static Camera main; public CameraClearFlags clearFlags; public Color backgroundColor; public bool orthographic; }
  public struct Color32 { public byte r,g,b,a; public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;} }
  public struct Color { public static Color black => new Color(0,0,0,1); public static Color operator*(Color c, float f){return new Color(c.r*f,c.g*f,c.b*f,c.a*f);} public static Color operator*(Color a, Color b){return new Color(a.r*b.r,a.g*b.g,a.b*b.b,a.a*b.a);} public static Color Lerp(Color a, Color b, float t){t=t<0?0:t>1?1:t; return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);} public static Color HSVToRGB(float h,float s,float v){ float r=v,g=v,b=v; if(s>0f){ h=(h-(float)System.Math.Floor(h))*6f; int i=(int)h; float f=h-i,p=v*(1-s),q=v*(1-s*f),t=v*(1-s*(1-f)); switch(i){case 0:r=v;g=t;b=p;break;case 1:r=q;g=v;b=p;break;case 2:r=p;g=v;b=t;break;case 3:r=p;g=q;b=v;break;case 4:r=t;g=p;b=v;break;default:r=v;g=p;b=q;break;} } return new Color(r,g,b,1f); } public static void RGBToHSV(Color c,out float h,out float s,out float v){ float mx=System.Math.Max(c.r,System.Math.Max(c.g,c.b)),mn=System.Math.Min(c.r,System.Math.Min(c.g,c.b)),d=mx-mn; v=mx; s=mx<=0f?0f:d/mx; h=0f; if(d>0f){ if(mx==c.r)h=((c.g-c.b)/d)/6f; else if(mx==c.g)h=(2f+(c.b-c.r)/d)/6f; else h=(4f+(c.r-c.g)/d)/6f; if(h<0f)h+=1f; } } public static Color white = new Color(1,1,1,1); public float r,g,b,a; public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;} public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} }
  public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
  public struct Quaternion { public static Quaternion identity; }
  public struct Matrix4x4 { public static Matrix4x4 identity => default(Matrix4x4); public float tx, ty, sx, sy; public bool Set; public float m00 => Set ? sx : 1f; public float m03 => tx; public float m13 => ty; public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s){return new Matrix4x4 { tx = p.x, ty = p.y, sx = s.x, sy = s.y, Set = true };} }  // only translation and scale; GUI applies it to what it logs (m00/m03/m13 for HudTiles)
  public struct Rect { public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;} public float x,y,width,height; public float xMax{get{return x+width;}} public float yMax{get{return y+height;}} public Vector2 center{get{return new Vector2(x+width/2f,y+height/2f);}} public bool Contains(Vector2 p){return p.x>=x&&p.x<x+width&&p.y>=y&&p.y<y+height;} }
  public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} public static Vector2 zero => new Vector2(0f,0f); public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y); public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y); public float magnitude=>(float)System.Math.Sqrt(x*x+y*y); public static float Distance(Vector2 a,Vector2 b)=>(float)System.Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); public static Vector2 operator *(Vector2 v,float k)=>new Vector2(v.x*k,v.y*k); public static bool operator ==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y; public static bool operator !=(Vector2 a,Vector2 b)=>!(a==b); public override bool Equals(object o)=>o is Vector2 v&&v==this; public override int GetHashCode()=>x.GetHashCode()^y.GetHashCode(); }
  public static class Random { public static int Range(int a,int b){return a;} public static float value => 0.5f; }
  public enum EventType { MouseDown, MouseUp, ScrollWheel, Repaint, Layout, Used, KeyDown, MouseDrag }
  public class Event { public static Event current; public Vector2 mousePosition; public EventType type; public Vector2 delta; public int button, clickCount; public KeyCode keyCode; public bool shift; public void Use(){} }
  public class GUIContent { public string text; public GUIContent(){} public GUIContent(string t){ text = t; } public static GUIContent none; }
  public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
  public enum FontStyle { Normal, Bold }
  public class Font : Object { }
  public class GUIStyleState { public Color textColor; public Texture2D background; }
  public class RectOffset { public int left,right,top,bottom; public int horizontal { get { return left + right; } } public RectOffset(int l,int r,int t,int b){left=l;right=r;top=t;bottom=b;} }
  public enum TextureFormat { RGBA32, RGB24 } public enum FilterMode { Point, Bilinear } public enum TextureWrapMode { Clamp, Repeat }
  // Text widths like Arial (the game's default font; the preview draws with Liberation Sans, which has the same metrics), so TextWidth / TextHeight in the preview match what Unity measures.
  public static class TextW {
    static readonly short[] reg = { 278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278, 556,556,556,556,556,556,556,556,556,556, 278,278,584,584,584,556, 1015, 667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611, 278,278,278,469,556,333, 556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500, 334,260,334,584 };
    static readonly short[] bold = { 278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278, 556,556,556,556,556,556,556,556,556,556, 333,333,584,584,584,611, 975, 722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611, 333,278,333,584,556,333, 556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500, 389,280,389,584 };
    public static float Width(string t, int size, bool b) { var w = b ? bold : reg; float x = 0; foreach (char ch in t) { int i = ch - 32; x += (i >= 0 && i < w.Length ? w[i] : 600) * size / 1000f; } return x; }
    public static int Lines(string t, int size, bool b, float width) {
      int lines = 0;
      foreach (var para in t.Split('\n')) {
        lines++; float x = 0, sp = Width(" ", size, b);
        foreach (var word in para.Split(' ')) { float ww = Width(word, size, b); if (x > 0 && x + ww > width) { lines++; x = 0; } x += ww + sp; }
      }
      return lines;
    }
  }
  public class GUIStyle { public Vector2 CalcSize(GUIContent c){ return new Vector2(TextW.Width(c.text ?? "", fontSize, fontStyle == FontStyle.Bold) + 6f, fontSize + 8f); } public float CalcHeight(GUIContent c, float w){ int lines = TextW.Lines(c.text ?? "", fontSize, fontStyle == FontStyle.Bold, System.Math.Max(1f, w - 6f)); return lines * fontSize * 1.18f + 4f; } public static GUIStyle none = new GUIStyle(); public GUIStyle(){} public GUIStyle(GUIStyle o){ if(o!=null){fontSize=o.fontSize; wordWrap=o.wordWrap; fontStyle=o.fontStyle; alignment=o.alignment; normal.textColor=o.normal.textColor; normal.background=o.normal.background; border=o.border;} } public Font font; public int fontSize; public bool wordWrap, richText; public FontStyle fontStyle; public TextAnchor alignment; public float fixedHeight, fixedWidth; public GUIStyleState normal=new GUIStyleState(), hover=new GUIStyleState(), active=new GUIStyleState(), focused=new GUIStyleState(); public RectOffset border=new RectOffset(0,0,0,0), padding, margin; }
  public sealed class GUILayoutOption {}
  public class GUISkin { public GUIStyle label = new GUIStyle(); }
  public class GUI { public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect uv){} public static void SetNextControlName(string n){} public static void FocusControl(string n){} public static string TextArea(Rect r, string t, GUIStyle s){return t;} public static string GetNameOfFocusedControl(){return "";}
    public static System.Collections.Generic.List<string> Log = new System.Collections.Generic.List<string>();
    static System.Collections.Generic.List<Rect> groups = new System.Collections.Generic.List<Rect>();
    static float ox, oy; static Rect clip = new Rect(-10000,-10000,40000,40000);
    static string C(Color c){ return c.r+","+c.g+","+c.b+","+c.a; }
    static string R(Rect r){ if (matrix.Set && !(matrix.sx == 1f && matrix.sy == 1f && matrix.tx == 0f && matrix.ty == 0f)) return (matrix.tx+(r.x+ox)*matrix.sx)+","+(matrix.ty+(r.y+oy)*matrix.sy)+","+(r.width*matrix.sx)+","+(r.height*matrix.sy); return (r.x+ox)+","+(r.y+oy)+","+r.width+","+r.height; }
    static int Fs(int size){ return matrix.Set && matrix.sy != 1f ? System.Math.Max(1, (int)System.Math.Round(size * matrix.sy)) : size; }
    static string Clip(){ return clip.x+","+clip.y+","+clip.width+","+clip.height; }
    public static void BeginGroup(Rect r){ groups.Add(clip); var a = new Rect(r.x+ox, r.y+oy, r.width, r.height); float x0=System.Math.Max(a.x,clip.x), y0=System.Math.Max(a.y,clip.y), x1=System.Math.Min(a.xMax,clip.xMax), y1=System.Math.Min(a.yMax,clip.yMax); clip=new Rect(x0,y0,System.Math.Max(0,x1-x0),System.Math.Max(0,y1-y0)); ox=a.x; oy=a.y; offs.Add(new float[]{r.x, r.y}); }
    static System.Collections.Generic.List<float[]> offs = new System.Collections.Generic.List<float[]>();
    public static void EndGroup(){ clip = groups[groups.Count-1]; groups.RemoveAt(groups.Count-1); var o = offs[offs.Count-1]; offs.RemoveAt(offs.Count-1); ox -= o[0]; oy -= o[1]; }
    public static Color backgroundColor = new Color(1,1,1,1); public static Color contentColor = new Color(1,1,1,1);
    public static void Box(Rect r, GUIContent c, GUIStyle s){ Styled(r, s, backgroundColor); }
    static string Hex(Texture2D t){ var sb = new System.Text.StringBuilder(); if (t.px32 != null) foreach (var c in t.px32) sb.Append(c.r.ToString("x2")+c.g.ToString("x2")+c.b.ToString("x2")+c.a.ToString("x2")); else foreach (var c in t.px) sb.Append(((int)System.Math.Round(c.r*255f)).ToString("x2")+((int)System.Math.Round(c.g*255f)).ToString("x2")+((int)System.Math.Round(c.b*255f)).ToString("x2")+((int)System.Math.Round(c.a*255f)).ToString("x2")); return sb.ToString(); }
    // A styled box: the style's texture 9-sliced into the rect (its border on every side), tinted like Unity does.
    static void Styled(Rect r, GUIStyle s, Color tint){ var t = s == null ? null : s.normal.background; if (t == null || t.px == null) return; Log.Add("box9\t"+R(r)+"\t"+Clip()+"\t"+C(tint)+"\t"+t.w+"\t"+t.h+"\t"+s.border.left+"\t"+Hex(t)+"\t"+C(color)); }
    public static string TextField(Rect r, string t, int max, GUIStyle s){return t;} public static float HorizontalSlider(Rect r, float v, float a, float b, GUIStyle s, GUIStyle t){return v;} public static bool Toggle(Rect r, bool v, GUIContent c, GUIStyle s){return v;} public static float HorizontalSlider(Rect r, float v, float a, float b){return v;} public static bool Toggle(Rect r, bool v, string t){return v;}
    public static bool enabled = true;
    public static bool Button(Rect r, string t, GUIStyle s){ Styled(r, s, backgroundColor); if (t != null) Label(r, t, s); return false;} public static bool Button(Rect r, GUIContent c, GUIStyle s){return false;}
    public static GUISkin skin = new GUISkin(); public static Matrix4x4 matrix; public static Color color = new Color(1,1,1,1);
    public static void Label(Rect r, string t, GUIStyle s){ Log.Add("label\t"+R(r)+"\t"+Clip()+"\t"+C(s.normal.textColor)+"\t"+Fs(s.fontSize)+"\t"+(int)s.alignment+"\t"+(s.fontStyle==FontStyle.Bold?1:0)+"\t"+(s.wordWrap?1:0)+"\t"+t.Replace("\n","\\n")); }
    public static void DrawTexture(Rect r, Texture t){ var t2 = t as Texture2D; if (t2 != null && (t2.px32 != null || t2.px != null)) { Log.Add("img\t"+R(r)+"\t"+Clip()+"\t"+t2.w+"\t"+Hex(t2)+"\t"+C(color)+"\t"+(t2.filterMode==FilterMode.Bilinear?1:0)); return; } Log.Add("fill\t"+R(r)+"\t"+Clip()+"\t"+C(color)); } }
  public class GUILayout {
    public static void BeginArea(Rect r, GUIStyle s){} public static void EndArea(){} public static void Space(float p){} public static void FlexibleSpace(){}
    public static void Label(string t, GUIStyle s, params GUILayoutOption[] o){} public static bool Button(string t, GUIStyle s, params GUILayoutOption[] o){return false;}
    public static string TextField(string t, int max, GUIStyle s, params GUILayoutOption[] o){return t;}
    public static void BeginHorizontal(params GUILayoutOption[] o){} public static void EndHorizontal(){} public static GUILayoutOption MinHeight(float h){return null;} }
  public class Texture : Object {} public class Texture2D : Texture { public int w,h; public Color[] px; public Texture2D(int w,int h,TextureFormat f,bool m){this.w=w;this.h=h;} public static Texture2D whiteTexture = new Texture2D(1,1,default(TextureFormat),false); public FilterMode filterMode; public TextureWrapMode wrapMode; public void SetPixels(Color[] c){px=c;} public Color32[] px32; public void SetPixels32(Color32[] c){px32=c;} public void Apply(){} public void Apply(bool m){} public void ReadPixels(Rect r,int x,int y,bool m){} public int width, height; }
  public class Shader : Object { public static Shader Find(string n){return null;} }
  public class Material : Object { int dst = -1; public Material(Shader s){} public void SetInt(string n, int v){ if (n == "_DstBlend") dst = v; } public bool SetPass(int p){ GL.Additive = dst == 1; return true; } } /* the glow material (SrcAlpha, One) adds light: its vertices are marked for shot.py */
  public sealed class GL { public const int TRIANGLES=4; public static void PushMatrix(){} public static void PopMatrix(){} public static void LoadPixelMatrix(){} public static bool ToUi; static System.Collections.Generic.List<float> buf = new System.Collections.Generic.List<float>(); public static void Begin(int m){ buf.Clear(); } public static void End(){ if (ToUi && buf.Count > 0) GUI.Log.Add("tris\t0,0,1280,720\t0,0,1280,720\t" + string.Join(",", buf.ConvertAll(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)))); buf.Clear(); } public static System.Collections.Generic.List<float> Tris = new System.Collections.Generic.List<float>(); public static Color Cur; public static bool Additive; public static void Vertex(Vector3 v){ var l = ToUi ? buf : Tris; l.Add(v.x); l.Add(v.y); l.Add(Cur.r); l.Add(Cur.g); l.Add(Cur.b); l.Add(Additive ? -1f - Cur.a : Cur.a);} /* alpha below 0: drawn with the additive glow material (-1 - alpha) */ public static void Color(Color c){ Cur = c; } }
  public enum KeyCode { None, F9, T, Y, C, H, LeftShift, RightShift, Return, KeypadEnter, Q, E, M, Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, Keypad0, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9, Mouse0, Mouse1, Mouse2, Mouse3, Mouse4, Mouse5, Mouse6, Space, Escape, R, L, W, A, S, D, UpArrow, DownArrow, LeftArrow, RightArrow, Tab, F, F1, F2, F3, F4, V, P, G, X, Delete, LeftAlt, RightAlt, LeftControl, RightControl, LeftCommand, RightCommand, Z, BackQuote, Minus, Equals, LeftBracket, RightBracket, Semicolon, Quote, Comma, Period, Slash, Backslash, Backspace, CapsLock, Home, End, F5 }
  public class Input { public static bool anyKeyDown; public static bool GetKey(KeyCode k){return false;} public static bool GetKeyDown(KeyCode k){return false;} public static bool GetMouseButton(int b){return false;} public static bool GetMouseButtonDown(int b){return false;} public static Vector3 mousePosition; public static Vector2 mouseScrollDelta; }
  public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 } public sealed class QualitySettings { public static int antiAliasing; public static ColorSpace activeColorSpace; } public sealed class Screen { public static int width, height; }
  public class Time { public static float deltaTime, fixedDeltaTime, unscaledTime, unscaledDeltaTime, time, timeScale = 1f; public static int frameCount; }
  public class Application { public static string persistentDataPath = "/tmp"; public static string consoleLogPath = "/tmp/Player.log"; public static string dataPath = "/tmp/Assets"; public static bool isEditor; public static void OpenURL(string u){} public static bool runInBackground; public static int targetFrameRate; public static void Quit(){} }
  public class Cursor { public static bool visible; }
  public struct Mathf { public static float MoveTowards(float c,float t,float d){ return System.Math.Abs(t-c)<=d?t:c+(t>c?d:-d); } public const float Rad2Deg = 57.29578f; public const float Deg2Rad = 0.0174532924f; public static float Repeat(float t,float l){ return t-(float)System.Math.Floor(t/l)*l; } public static float Ceil(float v){return (float)System.Math.Ceiling(v);} public const float PI=(float)System.Math.PI; public static int FloorToInt(float a){return (int)System.Math.Floor(a);} public static float Min(float a,float b){return System.Math.Min(a,b);} public static float Max(float a,float b){return System.Math.Max(a,b);} public static int Max(int a,int b){return a>b?a:b;} public static int Min(int a,int b){return a<b?a:b;} public static float Sin(float a){return (float)System.Math.Sin(a);} public static float Cos(float a){return (float)System.Math.Cos(a);} public static float Clamp01(float a){return a<0?0:a>1?1:a;} public static int Clamp(int v,int a,int b){return v<a?a:v>b?b:v;} public static float Clamp(float v,float a,float b){return v<a?a:v>b?b:v;} public static int CeilToInt(float a){return (int)System.Math.Ceiling(a);} public static int RoundToInt(float a){return (int)System.Math.Round(a);} public static float Abs(float a){return System.Math.Abs(a);} public static float Sign(float a){return a>=0f?1f:-1f;} public static float Sqrt(float a){return (float)System.Math.Sqrt(a);} public static float Lerp(float a,float b,float t){return a+(b-a)*t;} public static float Atan2(float y,float x){return (float)System.Math.Atan2(y,x);} public static float Tan(float a){return (float)System.Math.Tan(a);} public static float Pow(float a,float b){return (float)System.Math.Pow(a,b);} public static float Exp(float a){return (float)System.Math.Exp(a);} public static float Log10(float a){return (float)System.Math.Log10(a);} public static float Round(float a){return (float)System.Math.Round(a);} public static float Floor(float a){return (float)System.Math.Floor(a);} }
  public class Debug { public static void Log(object o){} public static void LogError(object o){} }
  public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, SubsystemRegistration }
  public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
  public class AudioListener : Behaviour {}
  public class AudioClip : Object { public int channels = 1, frequency = 44100, samples; public float length; public bool GetData(float[] d, int off){return false;} public static AudioClip Create(string n, int len, int ch, int freq, bool stream){return null;} public bool SetData(float[] d, int off){return true;} }
  public class AudioSource : Behaviour { public bool playOnAwake, loop, isPlaying; public float spatialBlend, pitch, volume, time; public AudioClip clip; public void Stop(){} public void PlayOneShot(AudioClip c, float v){} public void Play(){} public void Pause(){} public void UnPause(){} }
  public class PlayerPrefs { public static string GetString(string k, string d){return d;} public static void SetString(string k, string v){} public static float GetFloat(string k, float d){return d;} public static int GetInt(string k, int d){return d;} public static void SetFloat(string k, float v){} public static void SetInt(string k, int v){} public static void Save(){} }
  public class GUIUtility { public static void RotateAroundPivot(float angle, Vector2 pivot){} public static void ScaleAroundPivot(Vector2 scale, Vector2 pivot){} public static int keyboardControl, hotControl; public static string systemCopyBuffer = ""; public static Vector2 GUIToScreenPoint(Vector2 p) => p; }
  public enum FullScreenMode { ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed }
}
namespace UnityEngine {
  public class ScriptableObject : Object {}
  public class Resources { public static T Load<T>(string p) where T: Object {return null;} public static T[] LoadAll<T>(string p) where T: Object {return new T[0];} }
  public class CreateAssetMenuAttribute : System.Attribute { public string menuName, fileName; public int order; }
  public class TooltipAttribute : System.Attribute { public TooltipAttribute(string s){} }
  public class RangeAttribute : System.Attribute { public RangeAttribute(float a, float b){} }
}
namespace UnityEngine.Networking {
  public enum AudioType { UNKNOWN = 0, MPEG = 13, OGGVORBIS = 14, WAV = 20 }
  public class UnityWebRequestAsyncOperation { }
  public class UnityWebRequest : System.IDisposable { public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError } public bool isDone; public Result result; public string error;
    public UnityWebRequestAsyncOperation SendWebRequest(){return null;} public void Dispose(){} }
  public static class UnityWebRequestMultimedia { public static UnityWebRequest GetAudioClip(string uri, AudioType t){return new UnityWebRequest();} }
  public static class DownloadHandlerAudioClip { public static AudioClip GetContent(UnityWebRequest r){return null;} }
}
namespace UnityEngine.Rendering {
  public enum BlendMode { One=1, SrcAlpha=5, OneMinusSrcAlpha=10 } public enum CullMode { Off=0 } public enum CompareFunction { Always=8 }
}
namespace UnityEditor {
  public class InitializeOnLoadAttribute : Attribute {} public class MenuItem : Attribute { public MenuItem(string p){} }
  public class EditorApplication { public delegate void CallbackFunction(); public static CallbackFunction delayCall; public static bool isPlayingOrWillChangePlaymode; }
  public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e){} }
  public class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
  public class PlayerSettings { public static UnityEngine.ColorSpace colorSpace; public static string productName; public static bool runInBackground, resizableWindow; public static UnityEngine.FullScreenMode fullScreenMode; public static int defaultScreenWidth, defaultScreenHeight; }
  public enum BuildTarget { StandaloneWindows64 } [Flags] public enum BuildOptions { None=0 }
  public struct BuildPlayerOptions { public string[] scenes; public string locationPathName; public BuildTarget target; public BuildOptions options; }
  public class BuildPipeline { public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o){return null;} }
  public class EditorUtility { public static void RevealInFinder(string p){} }
}
namespace UnityEditor.Build.Reporting { public enum BuildResult { Succeeded, Failed } public struct BuildSummary { public BuildResult result; } public class BuildReport { public BuildSummary summary; } }
namespace UnityEditor.SceneManagement {
  public enum NewSceneSetup { EmptyScene } public enum NewSceneMode { Single }
  public struct Scene {}
  public class EditorSceneManager { public static Scene NewScene(NewSceneSetup s, NewSceneMode m){return default(Scene);} public static bool SaveScene(Scene s, string p){return true;} public static Scene OpenScene(string p){return default(Scene);} }
}
