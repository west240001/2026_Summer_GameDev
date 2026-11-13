using System.IO;
using BackbeatDiner.Front;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BackbeatDiner.Editor
{
    /// <summary>
    /// Builds FrontShellPrototype scene and registers it in Build Settings.
    /// Menu: BACKBEAT DINER / Build Front Shell Prototype Scene
    /// Batchmode: -executeMethod BackbeatDiner.Editor.FrontShellPrototypeSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class FrontShellPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/FrontShellPrototype.unity";
        private const string GoldMatPath = "Assets/_Project/Art/Materials/BrandGold.mat";
        private const string TealMatPath = "Assets/_Project/Art/Materials/BrandTeal.mat";

        [MenuItem("BACKBEAT DINER/Build Front Shell Prototype Scene")]
        public static void BuildFromMenu()
        {
            BuildScene(setInBuildSettings: true);
            EditorUtility.DisplayDialog(
                "BACKBEAT DINER",
                "FrontShellPrototype.unity を作成しました。\nPlay で表HUD（客待ち/在庫/クォータ/CD）と 1-4 / B のビート受信を試せます。",
                "OK");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildScene(setInBuildSettings: true);
                Debug.Log("[FrontShellPrototypeSceneBuilder] OK: " + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[FrontShellPrototypeSceneBuilder] FAILED: " + ex);
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
                cam.transform.position = new Vector3(0f, 3.1f, -5.2f);
                cam.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.1f, 0.1f);
            }

            var root = new GameObject("FrontShellPrototypeRoot");
            var bootstrap = root.AddComponent<FrontShellBootstrap>();

            var gold = AssetDatabase.LoadAssetAtPath<Material>(GoldMatPath);
            var teal = AssetDatabase.LoadAssetAtPath<Material>(TealMatPath);
            var so = new SerializedObject(bootstrap);
            so.FindProperty("buildPlaceholders").boolValue = true;
            so.FindProperty("goldMaterial").objectReferenceValue = gold;
            so.FindProperty("tealMaterial").objectReferenceValue = teal;
            so.FindProperty("customerCount").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (setInBuildSettings)
            {
                var rhythm = "Assets/_Project/Scenes/RhythmPrototype.unity";
                var front = ScenePath;
                var bootstrapScene = "Assets/Scenes/Bootstrap.unity";
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(rhythm, true),
                    new EditorBuildSettingsScene(front, true),
                    new EditorBuildSettingsScene(bootstrapScene, true)
                };
            }

            Debug.Log($"[FrontShellPrototypeSceneBuilder] Saved {ScenePath}");
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
