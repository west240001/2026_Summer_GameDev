using System.IO;
using BackbeatDiner.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BackbeatDiner.Editor
{
    /// <summary>
    /// Builds TitleMenu scene (logo / catchcopy / 営業開始) and puts it first in Build Settings.
    /// Menu: BACKBEAT DINER / Build Title Menu Scene
    /// Batchmode: -executeMethod BackbeatDiner.Editor.TitleMenuSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class TitleMenuSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/TitleMenu.unity";

        [MenuItem("BACKBEAT DINER/Build Title Menu Scene")]
        public static void BuildFromMenu()
        {
            BuildScene(setInBuildSettings: true);
            EditorUtility.DisplayDialog(
                "BACKBEAT DINER",
                "TitleMenu.unity を作成しました。\n" +
                "ロゴ BACKBEAT DINER / キャッチコピー / 営業開始 → VerticalSlicePrototype。\n" +
                "Build Settings 先頭に TitleMenu を登録済みです。",
                "OK");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildScene(setInBuildSettings: true);
                Debug.Log("[TitleMenuSceneBuilder] OK: " + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[TitleMenuSceneBuilder] FAILED: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildScene(bool setInBuildSettings)
        {
            Directory.CreateDirectory("Assets/_Project/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            RenderSettings.ambientLight = new Color(0.12f, 0.18f, 0.22f);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.name = "Main Camera";
                cam.transform.position = new Vector3(0f, 1.2f, -6f);
                cam.transform.rotation = Quaternion.identity;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = TitleMenuController.Navy;
            }

            var lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
            {
                var light = lightGo.GetComponent<Light>();
                if (light != null)
                {
                    light.color = new Color(1f, 0.94f, 0.84f);
                    light.intensity = 0.85f;
                    lightGo.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
                }
            }

            var root = new GameObject("TitleMenuRoot");
            var controller = root.AddComponent<TitleMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playSceneName").stringValue = "VerticalSlicePrototype";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (setInBuildSettings)
            {
                ApplyTitleFirstBuildSettings();
            }

            Debug.Log($"[TitleMenuSceneBuilder] Saved {ScenePath}");
        }

        /// <summary>TitleMenu first, then vertical slice and existing prototypes.</summary>
        public static void ApplyTitleFirstBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/VerticalSlicePrototype.unity", true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/RhythmPrototype.unity", true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/FrontShellPrototype.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Bootstrap.unity", true)
            };
        }
    }
}
