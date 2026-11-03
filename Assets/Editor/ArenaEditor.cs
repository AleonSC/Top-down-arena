using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Arena.EditorTools
{
    /// Creates the (empty) main scene and build settings on first open, and adds
    /// an "Arena" menu with a one-click Windows build.
    [InitializeOnLoad]
    static class ArenaEditor
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string BuildPath = "Build/TopDownArena/TopDownArena.exe";

        static ArenaEditor()
        {
            EditorApplication.delayCall += EnsureSetup;
        }

        static void EnsureSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("Arena: created " + ScenePath + ". Press Play to run the game.");
            }
            if (EditorBuildSettings.scenes.Length == 0)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Top-Down Arena";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            // Gamma, not Linear (a new Unity 6 project's default): the game draws with raw vertex colours, which Unity
            // doesn't convert in Linear, so there every colour and see-through layer came out lighter and greyer and
            // glows and fades blended differently from the mockups (which blend like any browser: in gamma).
            if (PlayerSettings.colorSpace != ColorSpace.Gamma)
            {
                PlayerSettings.colorSpace = ColorSpace.Gamma;
                Debug.Log("Arena: switched the colour space to Gamma so colours, glows and fades look as designed (it was " + "Linear).");
            }
        }

        [MenuItem("Arena/Build Windows Game")]
        static void BuildWindows()
        {
            EnsureSetup();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Arena: built " + BuildPath + ". Zip the whole TopDownArena folder and send it to your friend.");
                EditorUtility.RevealInFinder(BuildPath);
            }
            else
            {
                Debug.LogError("Arena: build " + report.summary.result + ". Is Windows Build Support installed for this Unity version (Unity Hub → Installs → Add modules)?");
            }
        }

        [MenuItem("Arena/Open Main Scene")]
        static void OpenScene()
        {
            EnsureSetup();
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
