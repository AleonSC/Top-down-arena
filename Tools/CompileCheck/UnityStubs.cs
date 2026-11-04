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
  public struct Color { public static Color operator*(Color c, float f){return c;} public static Color Lerp(Color a, Color b, float t){return a;} public static Color HSVToRGB(float h,float s,float v){ float r=v,g=v,b=v; if(s>0f){ h=(h-(float)System.Math.Floor(h))*6f; int i=(int)h; float f=h-i,p=v*(1-s),q=v*(1-s*f),t=v*(1-s*(1-f)); switch(i){case 0:r=v;g=t;b=p;break;case 1:r=q;g=v;b=p;break;case 2:r=p;g=v;b=t;break;case 3:r=p;g=q;b=v;break;case 4:r=t;g=p;b=v;break;default:r=v;g=p;b=q;break;} } return new Color(r,g,b,1f); } public static Color white; public float r,g,b,a; public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;} public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} }
  public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
  public struct Quaternion { public static Quaternion identity; }
  public struct Matrix4x4 { public float m00, m03, m13; public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s){return new Matrix4x4 { m00 = s.x, m03 = p.x, m13 = p.y };} }
  public struct Rect { public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;} public float x,y,width,height; public float xMax{get{return x+width;}} public float yMax{get{return y+height;}} public Vector2 center{get{return new Vector2(x+width/2f,y+height/2f);}} public bool Contains(Vector2 p){return false;} }
  public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} public static Vector2 zero => new Vector2(0f,0f); public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y); public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y); public float magnitude=>(float)System.Math.Sqrt(x*x+y*y); public static float Distance(Vector2 a,Vector2 b)=>(float)System.Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); public static Vector2 operator *(Vector2 v,float k)=>new Vector2(v.x*k,v.y*k); public static bool operator ==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y; public static bool operator !=(Vector2 a,Vector2 b)=>!(a==b); public override bool Equals(object o)=>o is Vector2 v&&v==this; public override int GetHashCode()=>x.GetHashCode()^y.GetHashCode(); }
  public static class Random { public static int Range(int a,int b){return a;} }
  public enum EventType { MouseDown, MouseUp, ScrollWheel, Repaint, Layout, Used, KeyDown, MouseDrag }
  public class Event { public static Event current; public Vector2 mousePosition; public EventType type; public Vector2 delta; public int button; public KeyCode keyCode; public bool shift; public void Use(){} }
  public class GUIContent { public string text; public GUIContent(){} public GUIContent(string t){ text = t; } public static GUIContent none; }
  public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
  public enum FontStyle { Normal, Bold }
  public class GUIStyleState { public Color textColor; public Texture2D background; }
  public class RectOffset { public int left, right; public int horizontal { get { return left + right; } } public RectOffset(int l,int r,int t,int b){ left=l; right=r; } }
  public enum TextureFormat { RGBA32, RGB24 } public enum FilterMode { Point, Bilinear } public enum TextureWrapMode { Clamp, Repeat }
  public class GUIStyle { public Vector2 CalcSize(GUIContent c){ return new Vector2((c.text ?? "").Length * fontSize * 0.55f + 20f, fontSize + 8f); } public float CalcHeight(GUIContent c, float w){ float cw = (c.text ?? "").Length * fontSize * 0.5f; int lines = 1 + (int)(cw / System.Math.Max(1f, w - 8f)); return lines * fontSize * 1.18f + 4f; } public static GUIStyle none; public GUIStyle(){} public GUIStyle(GUIStyle o){} public int fontSize; public bool wordWrap, richText; public FontStyle fontStyle; public TextAnchor alignment; public float fixedHeight, fixedWidth; public GUIStyleState normal, hover, active, focused; public RectOffset border, padding, margin; }
  public class GUISkin { public GUIStyle label, button, textField, box; }
  public sealed class GUILayoutOption {}
  public class GUI { public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect uv){} public static void SetNextControlName(string n){} public static void FocusControl(string n){} public static string TextArea(Rect r, string t, GUIStyle s){return t;} public static string GetNameOfFocusedControl(){return "";} public static void BeginGroup(Rect r){} public static void EndGroup(){} public static Color backgroundColor; public static void Box(Rect r, GUIContent c, GUIStyle s){} public static string TextField(Rect r, string t, int max, GUIStyle s){return t;} public static float HorizontalSlider(Rect r, float v, float a, float b, GUIStyle s, GUIStyle t){return v;} public static bool Toggle(Rect r, bool v, GUIContent c, GUIStyle s){return v;} public static float HorizontalSlider(Rect r, float v, float a, float b){return v;} public static bool Toggle(Rect r, bool v, string t){return v;} public static bool enabled; public static bool Button(Rect r, string t, GUIStyle s){return false;} public static bool Button(Rect r, GUIContent c, GUIStyle s){return false;} public static GUISkin skin; public static Matrix4x4 matrix; public static Color color; public static void Label(Rect r, string t, GUIStyle s){} public static void DrawTexture(Rect r, Texture t){} }
  public class GUILayout {
    public static void BeginArea(Rect r, GUIStyle s){} public static void EndArea(){} public static void Space(float p){} public static void FlexibleSpace(){}
    public static void Label(string t, GUIStyle s, params GUILayoutOption[] o){} public static bool Button(string t, GUIStyle s, params GUILayoutOption[] o){return false;}
    public static string TextField(string t, int max, GUIStyle s, params GUILayoutOption[] o){return t;}
    public static void BeginHorizontal(params GUILayoutOption[] o){} public static void EndHorizontal(){} public static GUILayoutOption MinHeight(float h){return null;} }
  public class Texture : Object {} public class Texture2D : Texture { public Texture2D(int w,int h,TextureFormat f,bool m){} public static Texture2D whiteTexture; public FilterMode filterMode; public TextureWrapMode wrapMode; public void SetPixels(Color[] c){} public void SetPixels32(Color32[] c){} public void Apply(){} public void Apply(bool m){} public void ReadPixels(Rect r,int x,int y,bool m){} public int width, height; }
  public class Shader : Object { public static Shader Find(string n){return null;} }
  public class Material : Object { public Material(Shader s){} public void SetInt(string n, int v){} public bool SetPass(int p){return true;} }
  public sealed class GL { public const int TRIANGLES=4; public static void PushMatrix(){} public static void PopMatrix(){} public static void LoadPixelMatrix(){} public static void Begin(int m){} public static void End(){} public static void Vertex(Vector3 v){} public static void Color(Color c){} }
  public enum KeyCode { None, T, Y, C, H, LeftShift, RightShift, Return, KeypadEnter, Q, E, M, Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, Keypad0, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9, Mouse0, Mouse1, Mouse2, Mouse3, Mouse4, Mouse5, Mouse6, Space, Escape, R, L, W, A, S, D, UpArrow, DownArrow, LeftArrow, RightArrow, Tab, F, F1, F2, F3, F4, V, P, G, X, Delete, LeftAlt, RightAlt, LeftControl, RightControl, LeftCommand, BackQuote, Minus, Equals, LeftBracket, RightBracket, Semicolon, Quote, Comma, Period, Slash, Backslash, Backspace, CapsLock, Home, End }
  public class Input { public static bool anyKeyDown; public static bool GetKey(KeyCode k){return false;} public static bool GetKeyDown(KeyCode k){return false;} public static bool GetMouseButton(int b){return false;} public static bool GetMouseButtonDown(int b){return false;} public static Vector3 mousePosition; }
  public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 } public sealed class QualitySettings { public static int antiAliasing; public static ColorSpace activeColorSpace; } public sealed class Screen { public static int width, height; }
  public class Time { public static float deltaTime, fixedDeltaTime, unscaledTime, unscaledDeltaTime, time, timeScale = 1f; public static int frameCount; }
  public class Application { public static string persistentDataPath = "/tmp"; public static string dataPath = "/tmp/Assets"; public static bool isEditor; public static void OpenURL(string u){} public static bool runInBackground; public static int targetFrameRate; public static void Quit(){} }
  public class Cursor { public static bool visible; }
  public struct Mathf { public static float MoveTowards(float c,float t,float d){ return System.Math.Abs(t-c)<=d?t:c+(t>c?d:-d); } public const float Rad2Deg = 57.29578f; public const float Deg2Rad = 0.0174532924f; public static float Repeat(float t,float l){ return t-(float)System.Math.Floor(t/l)*l; } public static float Atan2(float y,float x){return (float)System.Math.Atan2(y,x);} public static float Ceil(float v){return (float)System.Math.Ceiling(v);} public static float Lerp(float a,float b,float t){ t=t<0f?0f:t>1f?1f:t; return a+(b-a)*t; } public static float Round(float v){return v;} public static float Clamp(float v, float a, float b){return v<a?a:v>b?b:v;} public static int FloorToInt(float a){return 0;} public static float Min(float a,float b){return a;} public static float Max(float a,float b){return a;} public static int Max(int a,int b){return a>b?a:b;} public static int Min(int a,int b){return a<b?a:b;} public static float Sin(float a){return a;} public static float Clamp01(float a){return a;} public static int Clamp(int v,int a,int b){return v;} public static int CeilToInt(float a){return 0;} public static int RoundToInt(float a){return 0;} public static float Pow(float a,float b){return a;} public static float Sqrt(float a){return a;} public static float Cos(float a){return a;} public static float Abs(float a){return a;} public static float Exp(float a){return a;} public const float PI=3.14f; }
  public class Debug { public static void Log(object o){} public static void LogError(object o){} }
  public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, SubsystemRegistration }
  public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
  public class AudioListener : Behaviour {}
  public class AudioClip : Object { public int channels = 1, frequency = 44100, samples; public float length; public bool GetData(float[] d, int off){return false;} public static AudioClip Create(string n, int len, int ch, int freq, bool stream){return null;} public bool SetData(float[] d, int off){return true;} }
  public class AudioSource : Behaviour { public bool playOnAwake, loop, isPlaying; public float spatialBlend, pitch, volume, time; public AudioClip clip; public void Stop(){} public void PlayOneShot(AudioClip c, float v){} public void Play(){} public void Pause(){} public void UnPause(){} }
  public class PlayerPrefs { public static string GetString(string k, string d){return d;} public static void SetString(string k, string v){} public static float GetFloat(string k, float d){return d;} public static int GetInt(string k, int d){return d;} public static void SetFloat(string k, float v){} public static void SetInt(string k, int v){} public static void Save(){} }
  public class GUIUtility { public static void RotateAroundPivot(float angle, Vector2 pivot){} public static void ScaleAroundPivot(Vector2 scale, Vector2 pivot){} public static int keyboardControl; public static Vector2 GUIToScreenPoint(Vector2 p) => p; }
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
