// Signature-only stubs of the Unity APIs used by the project, for compile checks.
using System;
namespace UnityEngine {
  public class Object { public HideFlags hideFlags; public static T FindFirstObjectByType<T>() where T: Object { return null; } public static void DontDestroyOnLoad(Object o){} public static void Destroy(Object o){} public static implicit operator bool(Object o){return o!=null;} }
  public enum HideFlags { None=0, HideAndDontSave=61 }
  public class Component : Object { public GameObject gameObject; public string tag; }
  public class Behaviour : Component {}
  public class MonoBehaviour : Behaviour {}
  public class GameObject : Object { public GameObject(string n){} public string tag; public T AddComponent<T>() where T: Component { return null; } }
  public enum CameraClearFlags { Skybox=1, SolidColor=2 }
  public class Camera : Behaviour { public static Camera main; public CameraClearFlags clearFlags; public Color backgroundColor; public bool orthographic; }
  public struct Color32 { public byte r,g,b,a; public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;} }
  public struct Color { public static Color operator*(Color c, float f){return new Color(c.r*f,c.g*f,c.b*f,c.a*f);} public static Color operator*(Color a, Color b){return new Color(a.r*b.r,a.g*b.g,a.b*b.b,a.a*b.a);} public static Color Lerp(Color a, Color b, float t){t=t<0?0:t>1?1:t; return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);} public static Color white = new Color(1,1,1,1); public float r,g,b,a; public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;} public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} }
  public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
  public struct Quaternion { public static Quaternion identity; }
  public struct Matrix4x4 { public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s){return default(Matrix4x4);} }
  public struct Rect { public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;} public float x,y,width,height; public float xMax{get{return x+width;}} public float yMax{get{return y+height;}} public Vector2 center{get{return new Vector2(x+width/2f,y+height/2f);}} public bool Contains(Vector2 p){return p.x>=x&&p.x<x+width&&p.y>=y&&p.y<y+height;} }
  public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
  public static class Random { public static int Range(int a,int b){return a;} }
  public enum EventType { MouseDown, MouseUp, ScrollWheel, Repaint, Layout, Used }
  public class Event { public static Event current; public Vector2 mousePosition; public EventType type; public Vector2 delta; public int button; public void Use(){} }
  public class GUIContent { public static GUIContent none; }
  public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
  public enum FontStyle { Normal, Bold }
  public class GUIStyleState { public Color textColor; public Texture2D background; }
  public class RectOffset { public int left,right,top,bottom; public RectOffset(int l,int r,int t,int b){left=l;right=r;top=t;bottom=b;} }
  public enum TextureFormat { RGBA32 } public enum FilterMode { Point, Bilinear } public enum TextureWrapMode { Clamp, Repeat }
  public class GUIStyle { public static GUIStyle none = new GUIStyle(); public GUIStyle(){} public GUIStyle(GUIStyle o){ if(o!=null){fontSize=o.fontSize; wordWrap=o.wordWrap; fontStyle=o.fontStyle; alignment=o.alignment; normal.textColor=o.normal.textColor; normal.background=o.normal.background; border=o.border;} } public int fontSize; public bool wordWrap, richText; public FontStyle fontStyle; public TextAnchor alignment; public float fixedHeight, fixedWidth; public GUIStyleState normal=new GUIStyleState(), hover=new GUIStyleState(), active=new GUIStyleState(), focused=new GUIStyleState(); public RectOffset border=new RectOffset(0,0,0,0), padding, margin; }
  public sealed class GUILayoutOption {}
  public class GUISkin { public GUIStyle label = new GUIStyle(); }
  public class GUI { public static void SetNextControlName(string n){} public static string GetNameOfFocusedControl(){return "";}
    public static System.Collections.Generic.List<string> Log = new System.Collections.Generic.List<string>();
    static System.Collections.Generic.List<Rect> groups = new System.Collections.Generic.List<Rect>();
    static float ox, oy; static Rect clip = new Rect(-10000,-10000,40000,40000);
    static string C(Color c){ return c.r+","+c.g+","+c.b+","+c.a; }
    static string R(Rect r){ return (r.x+ox)+","+(r.y+oy)+","+r.width+","+r.height; }
    static string Clip(){ return clip.x+","+clip.y+","+clip.width+","+clip.height; }
    public static void BeginGroup(Rect r){ groups.Add(clip); var a = new Rect(r.x+ox, r.y+oy, r.width, r.height); float x0=System.Math.Max(a.x,clip.x), y0=System.Math.Max(a.y,clip.y), x1=System.Math.Min(a.xMax,clip.xMax), y1=System.Math.Min(a.yMax,clip.yMax); clip=new Rect(x0,y0,System.Math.Max(0,x1-x0),System.Math.Max(0,y1-y0)); ox=a.x; oy=a.y; offs.Add(new float[]{r.x, r.y}); }
    static System.Collections.Generic.List<float[]> offs = new System.Collections.Generic.List<float[]>();
    public static void EndGroup(){ clip = groups[groups.Count-1]; groups.RemoveAt(groups.Count-1); var o = offs[offs.Count-1]; offs.RemoveAt(offs.Count-1); ox -= o[0]; oy -= o[1]; }
    public static Color backgroundColor = new Color(1,1,1,1);
    public static void Box(Rect r, GUIContent c, GUIStyle s){ Styled(r, s, backgroundColor); }
    static void Styled(Rect r, GUIStyle s, Color tint){ var t = s == null ? null : s.normal.background; if (t == null || t.px == null) return; int n=t.w; Color mid=t.px[(n/2)*n+n/2], top=t.px[(n-n/2-1)*n+n/2], edge=t.px[(n/2)*n+1]; Log.Add("box\t"+R(r)+"\t"+Clip()+"\t"+C(tint)+"\t"+C(mid)+"\t"+C(edge)+"\t"+s.border.left+"\t"+C(t.px[(n-3)*n+n/2])+"\t"+C(t.px[2*n+n/2])); }
    public static string TextField(Rect r, string t, int max, GUIStyle s){return t;} public static float HorizontalSlider(Rect r, float v, float a, float b, GUIStyle s, GUIStyle t){return v;} public static bool Toggle(Rect r, bool v, GUIContent c, GUIStyle s){return v;} public static float HorizontalSlider(Rect r, float v, float a, float b){return v;} public static bool Toggle(Rect r, bool v, string t){return v;}
    public static bool enabled = true;
    public static bool Button(Rect r, string t, GUIStyle s){ Styled(r, s, backgroundColor); if (t != null) Label(r, t, s); return false;} public static bool Button(Rect r, GUIContent c, GUIStyle s){return false;}
    public static GUISkin skin = new GUISkin(); public static Matrix4x4 matrix; public static Color color = new Color(1,1,1,1);
    public static void Label(Rect r, string t, GUIStyle s){ Log.Add("label\t"+R(r)+"\t"+Clip()+"\t"+C(s.normal.textColor)+"\t"+s.fontSize+"\t"+(int)s.alignment+"\t"+(s.fontStyle==FontStyle.Bold?1:0)+"\t"+(s.wordWrap?1:0)+"\t"+t.Replace("\n","\n")); }
    public static void DrawTexture(Rect r, Texture t){ var t2 = t as Texture2D; if (t2 != null && t2.px32 != null) { var sb = new System.Text.StringBuilder(); foreach (var c in t2.px32) sb.Append(c.r.ToString("x2")+c.g.ToString("x2")+c.b.ToString("x2")+c.a.ToString("x2")); Log.Add("img\t"+R(r)+"\t"+Clip()+"\t"+t2.w+"\t"+sb+"\t"+C(color)); return; } Log.Add("fill\t"+R(r)+"\t"+Clip()+"\t"+C(color)); } }
  public class GUILayout {
    public static void BeginArea(Rect r, GUIStyle s){} public static void EndArea(){} public static void Space(float p){} public static void FlexibleSpace(){}
    public static void Label(string t, GUIStyle s, params GUILayoutOption[] o){} public static bool Button(string t, GUIStyle s, params GUILayoutOption[] o){return false;}
    public static string TextField(string t, int max, GUIStyle s, params GUILayoutOption[] o){return t;}
    public static void BeginHorizontal(params GUILayoutOption[] o){} public static void EndHorizontal(){} public static GUILayoutOption MinHeight(float h){return null;} }
  public class Texture : Object {} public class Texture2D : Texture { public int w,h; public Color[] px; public Texture2D(int w,int h,TextureFormat f,bool m){this.w=w;this.h=h;} public static Texture2D whiteTexture = new Texture2D(1,1,default(TextureFormat),false); public FilterMode filterMode; public TextureWrapMode wrapMode; public void SetPixels(Color[] c){px=c;} public Color32[] px32; public void SetPixels32(Color32[] c){px32=c;} public void Apply(){} }
  public class Shader : Object { public static Shader Find(string n){return null;} }
  public class Material : Object { public Material(Shader s){} public void SetInt(string n, int v){} public bool SetPass(int p){return true;} }
  public sealed class GL { public const int TRIANGLES=4; public static void PushMatrix(){} public static void PopMatrix(){} public static void LoadPixelMatrix(){} public static bool ToUi; static System.Collections.Generic.List<float> buf = new System.Collections.Generic.List<float>(); public static void Begin(int m){ buf.Clear(); } public static void End(){ if (ToUi && buf.Count > 0) GUI.Log.Add("tris\t0,0,1280,720\t0,0,1280,720\t" + string.Join(",", buf.ConvertAll(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)))); buf.Clear(); } public static System.Collections.Generic.List<float> Tris = new System.Collections.Generic.List<float>(); public static Color Cur; public static void Vertex(Vector3 v){ var l = ToUi ? buf : Tris; l.Add(v.x); l.Add(v.y); l.Add(Cur.r); l.Add(Cur.g); l.Add(Cur.b); l.Add(Cur.a);} public static void Color(Color c){ Cur = c; } }
  public enum KeyCode { C, LeftShift, RightShift, Return, KeypadEnter, Q, E, M, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Space, Escape, R, L, W, A, S, D, UpArrow, DownArrow, LeftArrow, RightArrow, Tab, F, F1, F3, V, P }
  public class Input { public static bool GetKey(KeyCode k){return false;} public static bool GetKeyDown(KeyCode k){return false;} public static bool GetMouseButton(int b){return false;} public static bool GetMouseButtonDown(int b){return false;} public static Vector3 mousePosition; }
  public sealed class Screen { public static int width, height; }
  public class Time { public static float deltaTime, fixedDeltaTime, unscaledTime, unscaledDeltaTime, time, timeScale = 1f; }
  public class Application { public static string persistentDataPath = "/tmp"; public static void OpenURL(string u){} public static bool runInBackground; public static int targetFrameRate; public static void Quit(){} }
  public class Cursor { public static bool visible; }
  public struct Mathf { public const float PI=(float)System.Math.PI; public static int FloorToInt(float a){return (int)System.Math.Floor(a);} public static float Min(float a,float b){return System.Math.Min(a,b);} public static float Max(float a,float b){return System.Math.Max(a,b);} public static int Max(int a,int b){return a>b?a:b;} public static int Min(int a,int b){return a<b?a:b;} public static float Sin(float a){return (float)System.Math.Sin(a);} public static float Cos(float a){return (float)System.Math.Cos(a);} public static float Clamp01(float a){return a<0?0:a>1?1:a;} public static int Clamp(int v,int a,int b){return v<a?a:v>b?b:v;} public static float Clamp(float v,float a,float b){return v<a?a:v>b?b:v;} public static int CeilToInt(float a){return (int)System.Math.Ceiling(a);} public static int RoundToInt(float a){return (int)System.Math.Round(a);} public static float Abs(float a){return System.Math.Abs(a);} public static float Sqrt(float a){return (float)System.Math.Sqrt(a);} public static float Lerp(float a,float b,float t){return a+(b-a)*t;} public static float Atan2(float y,float x){return (float)System.Math.Atan2(y,x);} public static float Pow(float a,float b){return (float)System.Math.Pow(a,b);} public static float Exp(float a){return (float)System.Math.Exp(a);} public static float Log10(float a){return (float)System.Math.Log10(a);} public static float Round(float a){return (float)System.Math.Round(a);} public static float Floor(float a){return (float)System.Math.Floor(a);} }
  public class Debug { public static void Log(object o){} public static void LogError(object o){} }
  public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, SubsystemRegistration }
  public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
  public class AudioListener : Behaviour {}
  public class AudioClip : Object { public static AudioClip Create(string n, int len, int ch, int freq, bool stream){return null;} public bool SetData(float[] d, int off){return true;} }
  public class AudioSource : Behaviour { public bool playOnAwake, loop, isPlaying; public float spatialBlend, pitch, volume, time; public AudioClip clip; public void Stop(){} public void PlayOneShot(AudioClip c, float v){} public void Play(){} public void Pause(){} }
  public class PlayerPrefs { public static float GetFloat(string k, float d){return d;} public static int GetInt(string k, int d){return d;} public static void SetFloat(string k, float v){} public static void SetInt(string k, int v){} public static void Save(){} }
  public class GUIUtility { public static int keyboardControl; }
  public enum FullScreenMode { ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed }
}
namespace UnityEngine.Rendering {
  public enum BlendMode { SrcAlpha=5, OneMinusSrcAlpha=10 } public enum CullMode { Off=0 } public enum CompareFunction { Always=8 }
}
namespace UnityEditor {
  public class InitializeOnLoadAttribute : Attribute {} public class MenuItem : Attribute { public MenuItem(string p){} }
  public class EditorApplication { public delegate void CallbackFunction(); public static CallbackFunction delayCall; public static bool isPlayingOrWillChangePlaymode; }
  public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e){} }
  public class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
  public class PlayerSettings { public static string productName; public static bool runInBackground, resizableWindow; public static UnityEngine.FullScreenMode fullScreenMode; public static int defaultScreenWidth, defaultScreenHeight; }
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
