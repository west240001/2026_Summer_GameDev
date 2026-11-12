using BackbeatDiner.Coop;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Rhythm
{
    /// <summary>
    /// Builds Week1 vertical-slice props (kitchen counter, hit pad, HUD, clock) at runtime.
    /// Safe to attach to an empty scene; also used by the Editor scene builder.
    /// </summary>
    public sealed class RhythmPrototypeBootstrap : MonoBehaviour
    {
        private static readonly Color Gold = new Color(0.788f, 0.635f, 0.153f); // #C9A227
        private static readonly Color Teal = new Color(0.102f, 0.420f, 0.420f); // #1A6B6B

        [SerializeField] private AudioClip metronomeClick;
        [SerializeField] private float bpm = 120f;
        [SerializeField] private bool buildPlaceholders = true;

        private void Start()
        {
            if (buildPlaceholders)
            {
                BuildWorld();
            }

            var systems = new GameObject("RhythmSystems");
            var clock = systems.AddComponent<RhythmClock>();
            systems.AddComponent<AudioSource>();
            var highway = systems.AddComponent<NoteHighway>();
            var hitInput = systems.AddComponent<HitInput>();
            systems.AddComponent<BeatLexiconDemo>();

            if (metronomeClick == null)
            {
                metronomeClick = LoadMetronomeClip() ?? BuildProceduralClick();
            }

            clock.AssignMetronomeClip(metronomeClick);
            clock.Bpm = bpm;

            var spawn = GameObject.Find("NoteSpawn")?.transform;
            var hit = GameObject.Find("HitLine")?.transform;
            if (spawn == null || hit == null)
            {
                CreateLaneAnchors(out spawn, out hit);
            }

            highway.Configure(clock, spawn, hit);

            var hud = EnsureHud(out var judgement, out var combo, out var bpmLabel);
            hitInput.Configure(clock, highway, judgement, combo, bpmLabel);
            hitInput.Window = JudgeWindow.Default;

            clock.StartClock(0.35);
            Debug.Log("[BACKBEAT DINER] Week1 RhythmPrototype ready — Space=hit, 1-4=beat lexicon.");
        }

        private static AudioClip LoadMetronomeClip()
        {
            // Prefer Resources stub if present; Editor scene builder also assigns the WAV asset.
            return Resources.Load<AudioClip>("MetronomeClick");
        }

        private static AudioClip BuildProceduralClick()
        {
            const int sampleRate = 44100;
            const float duration = 0.04f;
            const float freq = 1000f;
            var samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var env = Mathf.Exp(-t * 80f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
            }

            var clip = AudioClip.Create("MetronomeClick_Procedural", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void BuildWorld()
        {
            if (GameObject.Find("KitchenCounter") != null)
            {
                return;
            }

            var root = new GameObject("KitchenPlaceholders");

            var floor = CreateCube("Floor", root.transform, new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 6f), Teal * 0.55f);
            var counter = CreateCube("KitchenCounter", root.transform, new Vector3(0f, 0.45f, 0.2f), new Vector3(3.2f, 0.9f, 1.1f), Teal);
            var hitPad = CreateCube("HitPad", root.transform, new Vector3(0f, 0.95f, -0.15f), new Vector3(0.7f, 0.12f, 0.7f), Gold);
            CreateCube("BackSplash", root.transform, new Vector3(0f, 1.4f, 0.7f), new Vector3(3.2f, 1.2f, 0.12f), Teal * 1.15f);

            EnsureCamera();
            EnsureLight();
            CreateLaneAnchors(out _, out _);

            // Silence unused warnings in stripped builds.
            _ = floor;
            _ = counter;
            _ = hitPad;
        }

        private static void CreateLaneAnchors(out Transform spawn, out Transform hit)
        {
            var existingSpawn = GameObject.Find("NoteSpawn");
            var existingHit = GameObject.Find("HitLine");
            if (existingSpawn != null && existingHit != null)
            {
                spawn = existingSpawn.transform;
                hit = existingHit.transform;
                return;
            }

            var spawnGo = new GameObject("NoteSpawn");
            spawnGo.transform.position = new Vector3(0f, 1.2f, 3.2f);
            var hitGo = new GameObject("HitLine");
            hitGo.transform.position = new Vector3(0f, 1.0f, -0.15f);

            // Visual hit line bar
            var bar = CreateCube("HitLineVisual", hitGo.transform, Vector3.zero, new Vector3(1.4f, 0.05f, 0.08f), Gold);

            spawn = spawnGo.transform;
            hit = hitGo.transform;
            _ = bar;
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                var cam = Camera.main;
                cam.transform.position = new Vector3(0f, 2.4f, -4.2f);
                cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                cam.backgroundColor = new Color(0.05f, 0.12f, 0.12f);
                return;
            }

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0f, 2.4f, -4.2f);
            camera.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.12f, 0.12f);
        }

        private static void EnsureLight()
        {
            if (Object.FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static Canvas EnsureHud(out Text judgement, out Text combo, out Text bpm)
        {
            var canvasGo = GameObject.Find("RhythmHUD");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("RhythmHUD");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            judgement = EnsureLabel(canvasGo.transform, "JudgementLabel", "判定: —", new Vector2(0f, -40f), 36);
            combo = EnsureLabel(canvasGo.transform, "ComboLabel", "コンボ: 0", new Vector2(0f, -90f), 28);
            bpm = EnsureLabel(canvasGo.transform, "BpmLabel", "BPM: 120", new Vector2(0f, -130f), 24);
            EnsureLabel(canvasGo.transform, "HelpLabel", "Space / A = ヒット　1–4 = ビート送信", new Vector2(0f, 40f), 22);

            return canvasGo.GetComponent<Canvas>();
        }

        private static Text EnsureLabel(Transform parent, string name, string text, Vector2 anchoredPos, int fontSize)
        {
            var existing = parent.Find(name);
            Text label;
            if (existing != null)
            {
                label = existing.GetComponent<Text>();
                if (label != null)
                {
                    return label;
                }
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
            {
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Gold;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(900f, 50f);
            return label;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
