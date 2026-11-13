using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BackbeatDiner.Editor
{
    /// <summary>
    /// Batch: -executeMethod BackbeatDiner.Editor.SurveillanceCameraWiringCheck.RunFromCommandLine
    /// Refresh + authored-scene assert for SurveillanceCamera wiring.
    /// </summary>
    public static class SurveillanceCameraWiringCheck
    {
        private const string ScenePath = "Assets/_Project/Scenes/VerticalSlicePrototype.unity";

        [MenuItem("BACKBEAT DINER/Check SurveillanceCamera Wiring")]
        public static void RunFromMenu()
        {
            if (RunChecks(exitBatch: false))
            {
                EditorUtility.DisplayDialog("BACKBEAT DINER", "SurveillanceCamera wiring OK", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("BACKBEAT DINER", "SurveillanceCamera wiring FAILED — see Console", "OK");
            }
        }

        public static void RunFromCommandLine()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var ok = RunChecks(exitBatch: true);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool RunChecks(bool exitBatch)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var survGo = GameObject.Find("SurveillanceCamera");
            if (survGo == null)
            {
                Debug.LogError("[SurveillanceWiringCheck] FAIL: SurveillanceCamera missing in authored scene");
                return false;
            }

            var cam = survGo.GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogError("[SurveillanceWiringCheck] FAIL: SurveillanceCamera has no Camera component");
                return false;
            }

            if (survGo.GetComponent<AudioListener>() != null)
            {
                Debug.LogError("[SurveillanceWiringCheck] FAIL: SurveillanceCamera must not have AudioListener");
                return false;
            }

            if (GameObject.Find("FrontCamera") == null)
            {
                Debug.LogError("[SurveillanceWiringCheck] FAIL: FrontCamera missing");
                return false;
            }

            Debug.Log(
                $"[SurveillanceWiringCheck] PASS: SurveillanceCamera Camera " +
                $"enabled={cam.enabled} depth={cam.depth} hdr={cam.allowHDR} msaa={cam.allowMSAA} " +
                $"active={survGo.activeInHierarchy} roots={scene.rootCount} batch={exitBatch}");
            return true;
        }
    }
}
