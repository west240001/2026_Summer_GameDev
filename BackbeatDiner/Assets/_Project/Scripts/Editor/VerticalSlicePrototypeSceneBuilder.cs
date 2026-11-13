using System.IO;
using BackbeatDiner.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BackbeatDiner.Editor
{
    /// <summary>
    /// Builds VerticalSlicePrototype scene and registers it in Build Settings.
    /// Menu: BACKBEAT DINER / Build Vertical Slice Prototype Scene
    /// Batchmode: -executeMethod BackbeatDiner.Editor.VerticalSlicePrototypeSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class VerticalSlicePrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/VerticalSlicePrototype.unity";
        private const string GoldMatPath = "Assets/_Project/Art/Materials/BrandGold.mat";
        private const string TealMatPath = "Assets/_Project/Art/Materials/BrandTeal.mat";
        private const string ClickPath = "Assets/_Project/Audio/MetronomeClick.wav";

        [MenuItem("BACKBEAT DINER/Build Vertical Slice Prototype Scene")]
        public static void BuildFromMenu()
        {
            BuildScene(setInBuildSettings: true);
            EditorUtility.DisplayDialog(
                "BACKBEAT DINER",
                "VerticalSlicePrototype.unity を作成しました。\n左=裏チャート / 右=表ミニ。\nSpace=裏ヒット、F=インスタントクック→配膳リズム（Space/J/K）。\n配膳中は 1–4 コミュ送信ロック。",
                "OK");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildScene(setInBuildSettings: true);
                Debug.Log("[VerticalSlicePrototypeSceneBuilder] OK: " + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[VerticalSlicePrototypeSceneBuilder] FAILED: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildScene(bool setInBuildSettings)
        {
            EnsureBrandMaterials();
            Directory.CreateDirectory("Assets/_Project/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.name = "FrontCamera";
                cam.transform.position = new Vector3(6f, 3.1f, -5.2f);
                cam.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.1f, 0.1f);
                cam.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            }

            var root = new GameObject("VerticalSlicePrototypeRoot");
            var bootstrap = root.AddComponent<VerticalSliceBootstrap>();

            var gold = AssetDatabase.LoadAssetAtPath<Material>(GoldMatPath);
            var teal = AssetDatabase.LoadAssetAtPath<Material>(TealMatPath);
            var click = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickPath);
            var so = new SerializedObject(bootstrap);
            so.FindProperty("buildPlaceholders").boolValue = true;
            so.FindProperty("bpm").floatValue = 120f;
            so.FindProperty("customerCount").intValue = 2;
            so.FindProperty("goldMaterial").objectReferenceValue = gold;
            so.FindProperty("tealMaterial").objectReferenceValue = teal;
            if (click != null)
            {
                so.FindProperty("metronomeClick").objectReferenceValue = click;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (setInBuildSettings)
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(ScenePath, true),
                    new EditorBuildSettingsScene("Assets/_Project/Scenes/RhythmPrototype.unity", true),
                    new EditorBuildSettingsScene("Assets/_Project/Scenes/FrontShellPrototype.unity", true),
                    new EditorBuildSettingsScene("Assets/Scenes/Bootstrap.unity", true)
                };
            }

            Debug.Log($"[VerticalSlicePrototypeSceneBuilder] Saved {ScenePath}");
        }

        private static void EnsureBrandMaterials()
        {
            Directory.CreateDirectory("Assets/_Project/Art/Materials");
            EnsureUnlitMat(GoldMatPath, new Color(0.7882353f, 0.6352941f, 0.15294118f, 1f));
            EnsureUnlitMat(TealMatPath, new Color(0.101960784f, 0.419607843f, 0.419607843f, 1f));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureUnlitMat(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            if (existing == null)
            {
                var mat = new Material(shader);
                ApplyColor(mat, color);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                existing.shader = shader;
                ApplyColor(existing, color);
                EditorUtility.SetDirty(existing);
            }
        }

        private static void ApplyColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }
        }
    }
}
