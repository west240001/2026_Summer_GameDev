using System.IO;
using BackbeatDiner.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BackbeatDiner.Editor
{
    /// <summary>
    /// Builds TitleMenu scene (art backdrop / logo / catchcopy / 営業開始) and puts it first in Build Settings.
    /// Menu: BACKBEAT DINER / Build Title Menu Scene
    /// Batchmode: -executeMethod BackbeatDiner.Editor.TitleMenuSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class TitleMenuSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/TitleMenu.unity";
        private const string TitleBackgroundPath = "Assets/_Project/Art/UI/TitleBackground.jpg";

        [MenuItem("BACKBEAT DINER/Build Title Menu Scene")]
        public static void BuildFromMenu()
        {
            BuildScene(setInBuildSettings: true);
            EditorUtility.DisplayDialog(
                "BACKBEAT DINER",
                "TitleMenu.unity を作成しました。\n" +
                "背景アート + BACKBEAT DINER / キャッチコピー。\n" +
                "役選択 → ホスト作成 / 参加 localhost（1/2, H/J）→ VerticalSlice 全画面。\n" +
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
            Directory.CreateDirectory("Assets/_Project/Art/UI");

            EnsureTitleBackgroundImportSettings();

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

            var background = AssetDatabase.LoadAssetAtPath<Sprite>(TitleBackgroundPath);
            if (background == null)
            {
                Debug.LogWarning("[TitleMenuSceneBuilder] Missing sprite at " + TitleBackgroundPath);
            }

            var root = new GameObject("TitleMenuRoot");
            var controller = root.AddComponent<TitleMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playSceneName").stringValue = "VerticalSlicePrototype";
            var bgProp = so.FindProperty("titleBackground");
            if (bgProp != null && background != null)
            {
                bgProp.objectReferenceValue = background;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (setInBuildSettings)
            {
                ApplyTitleFirstBuildSettings();
            }

            Debug.Log($"[TitleMenuSceneBuilder] Saved {ScenePath} (bg={(background != null ? "ok" : "missing")})");
        }

        private static void EnsureTitleBackgroundImportSettings()
        {
            if (!File.Exists(TitleBackgroundPath))
            {
                Debug.LogWarning("[TitleMenuSceneBuilder] TitleBackground.jpg not found at " + TitleBackgroundPath);
                return;
            }

            AssetDatabase.ImportAsset(TitleBackgroundPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(TitleBackgroundPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
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
