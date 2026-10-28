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
  public struct Color { public static Color operator*(Color c, float f){return c;} public static Color Lerp(Color a, Color b, float t){return a;} public static Color white; public float r,g,b,a; public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;} public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} }
  public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
  public struct Quaternion { public static Quaternion identity; }
  public struct Matrix4x4 { public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s){return default(Matrix4x4);} }
  public struct Rect { public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;} public float x,y,width,height; public float xMax{get{return x+width;}} public float yMax{get{return y+height;}} public Vector2 center{get{return new Vector2(x+width/2f,y+height/2f);}} public bool Contains(Vector2 p){return false;} }
  public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
  public static class Random { public static int Range(int a,int b){return a;} }
  public enum EventType { MouseDown, MouseUp, ScrollWheel, Repaint, Layout, Used }
  public class Event { public static Event current; public Vector2 mousePosition; public EventType type; public Vector2 delta; public int button; public void Use(){} }
  public class GUIContent { public static GUIContent none; }
  public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
  public enum FontStyle { Normal, Bold }
  public class GUIStyleState { public Color textColor; public Texture2D background; }
  public class RectOffset { public RectOffset(int l,int r,int t,int b){} }
  public enum TextureFormat { RGBA32 } public enum FilterMode { Point, Bilinear } public enum TextureWrapMode { Clamp, Repeat }
  public class GUIStyle { public static GUIStyle none; public GUIStyle(){} public GUIStyle(GUIStyle o){} public int fontSize; public bool wordWrap, richText; public FontStyle fontStyle; public TextAnchor alignment; public float fixedHeight, fixedWidth; public GUIStyleState normal, hover, active, focused; public RectOffset border, padding, margin; }
  public class GUISkin { public GUIStyle label, button, textField, box; }
  public sealed class GUILayoutOption {}
  public class GUI { public static void SetNextControlName(string n){} public static string GetNameOfFocusedControl(){return "";} public static void BeginGroup(Rect r){} public static void EndGroup(){} public static Color backgroundColor; public static void Box(Rect r, GUIContent c, GUIStyle s){} public static string TextField(Rect r, string t, int max, GUIStyle s){return t;} public static float HorizontalSlider(Rect r, float v, float a, float b, GUIStyle s, GUIStyle t){return v;} public static bool Toggle(Rect r, bool v, GUIContent c, GUIStyle s){return v;} public static float HorizontalSlider(Rect r, float v, float a, float b){return v;} public static bool Toggle(Rect r, bool v, string t){return v;} public static bool enabled; public static bool Button(Rect r, string t, GUIStyle s){return false;} public static bool Button(Rect r, GUIContent c, GUIStyle s){return false;} public static GUISkin skin; public static Matrix4x4 matrix; public static Color color; public static void Label(Rect r, string t, GUIStyle s){} public static void DrawTexture(Rect r, Texture t){} }
  public class GUILayout {
    public static void BeginArea(Rect r, GUIStyle s){} public static void EndArea(){} public static void Space(float p){} public static void FlexibleSpace(){}
    public static void Label(string t, GUIStyle s, params GUILayoutOption[] o){} public static bool Button(string t, GUIStyle s, params GUILayoutOption[] o){return false;}
    public static string TextField(string t, int max, GUIStyle s, params GUILayoutOption[] o){return t;}
    public static void BeginHorizontal(params GUILayoutOption[] o){} public static void EndHorizontal(){} public static GUILayoutOption MinHeight(float h){return null;} }
  public class Texture : Object {} public class Texture2D : Texture { public Texture2D(int w,int h,TextureFormat f,bool m){} public static Texture2D whiteTexture; public FilterMode filterMode; public TextureWrapMode wrapMode; public void SetPixels(Color[] c){} public void SetPixels32(Color32[] c){} public void Apply(){} }
  public class Shader : Object { public static Shader Find(string n){return null;} }
  public class Material : Object { public Material(Shader s){} public void SetInt(string n, int v){} public bool SetPass(int p){return true;} }
  public sealed class GL { public const int TRIANGLES=4; public static void PushMatrix(){} public static void PopMatrix(){} public static void LoadPixelMatrix(){} public static void Begin(int m){} public static void End(){} public static void Vertex(Vector3 v){} public static void Color(Color c){} }
  public enum KeyCode { C, LeftShift, RightShift, Return, KeypadEnter, Q, E, M, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Space, Escape, R, L, W, A, S, D, UpArrow, DownArrow, LeftArrow, RightArrow, Tab, F, F1, F3, V, P }
  public class Input { public static bool GetKey(KeyCode k){return false;} public static bool GetKeyDown(KeyCode k){return false;} public static bool GetMouseButton(int b){return false;} public static bool GetMouseButtonDown(int b){return false;} public static Vector3 mousePosition; }
  public sealed class Screen { public static int width, height; }
  public class Time { public static float deltaTime, fixedDeltaTime, unscaledTime, unscaledDeltaTime, time, timeScale = 1f; }
  public class Application { public static string persistentDataPath = "/tmp"; public static void OpenURL(string u){} public static bool runInBackground; public static int targetFrameRate; public static void Quit(){} }
  public class Cursor { public static bool visible; }
  public struct Mathf { public static float Round(float v){return v;} public static float Clamp(float v, float a, float b){return v<a?a:v>b?b:v;} public static int FloorToInt(float a){return 0;} public static float Min(float a,float b){return a;} public static float Max(float a,float b){return a;} public static int Max(int a,int b){return a>b?a:b;} public static int Min(int a,int b){return a<b?a:b;} public static float Sin(float a){return a;} public static float Clamp01(float a){return a;} public static int Clamp(int v,int a,int b){return v;} public static int CeilToInt(float a){return 0;} public static int RoundToInt(float a){return 0;} public static float Pow(float a,float b){return a;} public static float Sqrt(float a){return a;} public static float Cos(float a){return a;} public static float Abs(float a){return a;} public const float PI=3.14f; }
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
