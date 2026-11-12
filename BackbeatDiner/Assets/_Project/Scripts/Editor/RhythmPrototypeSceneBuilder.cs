using System.IO;
using BackbeatDiner.Rhythm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BackbeatDiner.Editor
{
    /// <summary>
    /// Builds RhythmPrototype scene and registers it first in Build Settings.
    /// Menu: BACKBEAT DINER / Build Rhythm Prototype Scene
    /// Batchmode: -executeMethod BackbeatDiner.Editor.RhythmPrototypeSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class RhythmPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/RhythmPrototype.unity";
        private const string ClickPath = "Assets/_Project/Audio/MetronomeClick.wav";

        [MenuItem("BACKBEAT DINER/Build Rhythm Prototype Scene")]
        public static void BuildFromMenu()
        {
            BuildScene(setAsFirstBuildScene: true);
            EditorUtility.DisplayDialog(
                "BACKBEAT DINER",
                "RhythmPrototype.unity を作成し Build Settings 先頭に登録しました。\nPlay で Space ヒット / 1-4 ビート送信を試せます。",
                "OK");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildScene(setAsFirstBuildScene: true);
                Debug.Log("[RhythmPrototypeSceneBuilder] OK: " + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[RhythmPrototypeSceneBuilder] FAILED: " + ex);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Compile / import smoke check without requiring a full scene bake.</summary>
        public static void CompileCheck()
        {
            Debug.Log("[RhythmPrototypeSceneBuilder] CompileCheck OK — assemblies loaded.");
            EditorApplication.Exit(0);
        }

        private static void BuildScene(bool setAsFirstBuildScene)
        {
            Directory.CreateDirectory("Assets/_Project/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Tint main camera
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 2.4f, -4.2f);
                cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.12f, 0.12f);
            }

            var root = new GameObject("RhythmPrototypeRoot");
            var bootstrap = root.AddComponent<RhythmPrototypeBootstrap>();

            var click = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickPath);
            if (click != null)
            {
                var so = new SerializedObject(bootstrap);
                so.FindProperty("metronomeClick").objectReferenceValue = click;
                so.FindProperty("bpm").floatValue = 120f;
                so.FindProperty("buildPlaceholders").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (setAsFirstBuildScene)
            {
                var bootstrapScene = "Assets/Scenes/Bootstrap.unity";
                var list = new[]
                {
                    new EditorBuildSettingsScene(ScenePath, true),
                    new EditorBuildSettingsScene(bootstrapScene, true)
                };
                EditorBuildSettings.scenes = list;
            }

            Debug.Log($"[RhythmPrototypeSceneBuilder] Saved {ScenePath}");
        }
    }
}
