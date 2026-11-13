using BackbeatDiner.Coop;
using BackbeatDiner.Front;
using BackbeatDiner.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Vertical slice: left = back rhythm chart, right = front mini diner.
    /// Shared <see cref="DaySessionState"/> links Perfect/Good → 仕込み and Instant Cook → F1+ 配膳リズム → 提供.
    /// </summary>
    public sealed class VerticalSliceBootstrap : MonoBehaviour
    {
        private static readonly Color Gold = DaySessionState.Gold;
        private static readonly Color Teal = DaySessionState.Teal;

        [SerializeField] private AudioClip metronomeClick;
        [SerializeField] private float bpm = 120f;
        [SerializeField] private bool buildPlaceholders = true;
        [SerializeField] private Material goldMaterial;
        [SerializeField] private Material tealMaterial;
        [SerializeField] private int customerCount = 2;

        private void Start()
        {
            ResolveMaterials();
            if (buildPlaceholders)
            {
                BuildWorlds();
            }

            EnsureLights();
            var backCam = EnsureBackCamera();
            var frontCam = EnsureFrontCamera();
            _ = backCam;
            _ = frontCam;

            var systems = new GameObject("VerticalSliceSystems");
            var clock = systems.AddComponent<RhythmClock>();
            systems.AddComponent<AudioSource>();
            var highway = systems.AddComponent<NoteHighway>();
            var hitInput = systems.AddComponent<HitInput>();
            var session = systems.AddComponent<DaySessionState>();
            var queue = systems.AddComponent<DummyCustomerQueue>();
            var inventory = systems.AddComponent<InventoryBar>();
            var cook = systems.AddComponent<InstantCookInput>();
            var serveRhythm = systems.AddComponent<ServeRhythmWindow>();
            var backBeats = systems.AddComponent<BeatLexiconDemo>();
            var frontBeats = systems.AddComponent<BeatLexiconDemo>();

            if (metronomeClick == null)
            {
                metronomeClick = Resources.Load<AudioClip>("MetronomeClick") ?? BuildProceduralClick();
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

            var queueRoot = GameObject.Find("CustomerQueueRoot")?.transform;
            if (queueRoot == null)
            {
                var go = new GameObject("CustomerQueueRoot");
                go.transform.position = new Vector3(6f, 0f, 0f);
                queueRoot = go.transform;
            }

            queue.Configure(queueRoot, tealMaterial, goldMaterial, Mathf.Clamp(customerCount, 1, 3));

            BuildHud(out var judgement, out var combo, out var bpmLabel,
                out var prep, out var serve, out var timer, out var cookFeedback,
                out var invRow, out var resultRoot, out var resultTitle, out var resultBody);

            inventory.BuildUi(invRow);
            // Local 2P: Back WASD+Space hits stay live during Front serve (J·K).
            hitInput.Configure(clock, highway, judgement, combo, bpmLabel, twoPlayerSplit: true);
            hitInput.Window = JudgeWindow.Default;
            session.Bind(clock, hitInput, prep, serve, timer, resultRoot, resultTitle, resultBody);
            serveRhythm.Configure(clock, session, hitInput, cook, cookFeedback, twoPlayerSplit: true);
            cook.Configure(session, queue, cookFeedback, serveRhythm, twoPlayerSplit: true);
            // 裏: A/S/D/F = Beat-B1..B4　表: ←↓→↑ = Beat-F1..F4（配膳中は表のみロック）
            backBeats.Configure(BeatRole.Back, createPadUi: true, padKeys: BeatPadKeyScheme.BackAsdf);
            frontBeats.Configure(BeatRole.Front, createPadUi: true, padKeys: BeatPadKeyScheme.FrontArrows);

            session.Begin();
            clock.StartClock(0.35);
            Debug.Log("[BACKBEAT DINER] VerticalSlicePrototype ready — local 2P: Back WASD+Space hit / A·S·D·F=B1..B4; Front Enter cook / J·K serve / arrows=F1..F4; ~32拍 or 30s.");
        }

        private void ResolveMaterials()
        {
            if (goldMaterial == null)
            {
                goldMaterial = CreateRuntimeMat(Gold);
            }

            if (tealMaterial == null)
            {
                tealMaterial = CreateRuntimeMat(Teal);
            }
        }

        private void BuildWorlds()
        {
            if (GameObject.Find("BackKitchenRoot") != null)
            {
                return;
            }

            var back = new GameObject("BackKitchenRoot");
            back.transform.position = new Vector3(-6f, 0f, 0f);
            CreateCube("Floor", back.transform, new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 6f), Teal * 0.55f);
            CreateCube("KitchenCounter", back.transform, new Vector3(0f, 0.45f, 0.2f), new Vector3(3.2f, 0.9f, 1.1f), Teal);
            CreateCube("HitPad", back.transform, new Vector3(0f, 0.95f, -0.15f), new Vector3(0.7f, 0.12f, 0.7f), Gold);
            CreateCube("BackSplash", back.transform, new Vector3(0f, 1.4f, 0.7f), new Vector3(3.2f, 1.2f, 0.12f), Teal * 1.15f);
            CreateLaneAnchors(out _, out _);

            var front = new GameObject("FrontDinerRoot");
            front.transform.position = new Vector3(6f, 0f, 0f);
            CreateCube("DinerFloor", front.transform, new Vector3(0f, -0.05f, 1.2f), new Vector3(7f, 0.1f, 5f), Teal * 0.55f);
            CreateCube("ServiceCounter", front.transform, new Vector3(0f, 0.45f, -0.4f), new Vector3(3.6f, 0.9f, 0.9f), Teal);
            CreateCube("PassWindow", front.transform, new Vector3(0f, 1.35f, -0.85f), new Vector3(1.6f, 0.7f, 0.12f), Gold);
            CreateCube("BackWall", front.transform, new Vector3(0f, 1.5f, 3.2f), new Vector3(7f, 3f, 0.15f), Teal * 1.1f);

            if (GameObject.Find("CustomerQueueRoot") == null)
            {
                var q = new GameObject("CustomerQueueRoot");
                q.transform.SetParent(front.transform, false);
                q.transform.localPosition = Vector3.zero;
            }
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

            var back = GameObject.Find("BackKitchenRoot");
            var origin = back != null ? back.transform.position : new Vector3(-6f, 0f, 0f);

            var spawnGo = new GameObject("NoteSpawn");
            spawnGo.transform.position = origin + new Vector3(0f, 1.2f, 3.2f);
            var hitGo = new GameObject("HitLine");
            hitGo.transform.position = origin + new Vector3(0f, 1.0f, -0.15f);
            CreateCube("HitLineVisual", hitGo.transform, Vector3.zero, new Vector3(1.4f, 0.05f, 0.08f), Gold);

            spawn = spawnGo.transform;
            hit = hitGo.transform;
        }

        private static Camera EnsureBackCamera()
        {
            var go = GameObject.Find("BackCamera");
            Camera cam;
            if (go == null)
            {
                go = new GameObject("BackCamera");
                cam = go.AddComponent<Camera>();
            }
            else
            {
                cam = go.GetComponent<Camera>() ?? go.AddComponent<Camera>();
            }

            // Not MainCamera — front owns customer HUD projection.
            go.tag = "Untagged";
            cam.transform.position = new Vector3(-6f, 2.4f, -4.2f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.12f, 0.12f);
            cam.depth = 0;
            cam.rect = new Rect(0f, 0f, 0.5f, 1f);
            if (go.GetComponent<AudioListener>() == null)
            {
                go.AddComponent<AudioListener>();
            }

            return cam;
        }

        private static Camera EnsureFrontCamera()
        {
            // Retarget Main Camera if present; otherwise create FrontCamera as MainCamera.
            Camera cam;
            if (Camera.main != null)
            {
                cam = Camera.main;
                cam.gameObject.name = "FrontCamera";
            }
            else
            {
                var go = GameObject.Find("FrontCamera");
                if (go == null)
                {
                    go = new GameObject("FrontCamera");
                    go.tag = "MainCamera";
                    cam = go.AddComponent<Camera>();
                }
                else
                {
                    cam = go.GetComponent<Camera>() ?? go.AddComponent<Camera>();
                    go.tag = "MainCamera";
                }
            }

            // Strip duplicate listener — BackCamera owns audio.
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null)
            {
                Object.Destroy(listener);
            }

            cam.transform.position = new Vector3(6f, 3.1f, -5.2f);
            cam.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.1f, 0.1f);
            cam.depth = 1;
            cam.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            return cam;
        }

        private static void EnsureLights()
        {
            if (Object.FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -25f, 0f);
        }

        private static void BuildHud(
            out Text judgement,
            out Text combo,
            out Text bpmLabel,
            out Text prep,
            out Text serve,
            out Text timer,
            out Text cookFeedback,
            out RectTransform invRow,
            out GameObject resultRoot,
            out Text resultTitle,
            out Text resultBody)
        {
            var canvasGo = GameObject.Find("VerticalSliceHUD");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("VerticalSliceHUD");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Divider
            EnsurePanel(canvasGo.transform, "SplitDivider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 1080f), new Color(0.788f, 0.635f, 0.153f, 0.55f));

            EnsureLabel(canvasGo.transform, "BackTitle", "裏 · キッチン", new Vector2(24f, -18f), TextAnchor.UpperLeft, 22, Gold);
            EnsureLabel(canvasGo.transform, "FrontTitle", "表 · ダイナー", new Vector2(-24f, -18f), TextAnchor.UpperRight, 22, Gold);

            judgement = EnsureLabel(canvasGo.transform, "JudgementLabel", "判定: —", new Vector2(24f, -56f), TextAnchor.UpperLeft, 26, Gold);
            combo = EnsureLabel(canvasGo.transform, "ComboLabel", "コンボ: 0", new Vector2(24f, -96f), TextAnchor.UpperLeft, 22, Gold);
            bpmLabel = EnsureLabel(canvasGo.transform, "BpmLabel", "BPM: 120", new Vector2(24f, -132f), TextAnchor.UpperLeft, 20, Teal);
            SetLabelText(EnsureLabel(canvasGo.transform, "BackControlsHint", LocalTwoPlayerBindings.HudBackHint,
                new Vector2(24f, -168f), TextAnchor.UpperLeft, 15, Teal), LocalTwoPlayerBindings.HudBackHint);

            prep = EnsureLabel(canvasGo.transform, "PrepLabel", "仕込み: 0", new Vector2(-24f, -56f), TextAnchor.UpperRight, 28, Gold);
            serve = EnsureLabel(canvasGo.transform, "ServeLabel", "提供: 0", new Vector2(-24f, -100f), TextAnchor.UpperRight, 28, Teal);
            timer = EnsureLabel(canvasGo.transform, "TimerLabel", "残り 30.0s / ビート 0/32", new Vector2(0f, -18f), TextAnchor.UpperCenter, 22, Gold);
            cookFeedback = EnsureLabel(canvasGo.transform, "CookFeedback", LocalTwoPlayerBindings.HudFrontCookHint,
                new Vector2(-24f, -150f), TextAnchor.UpperRight, 18, Gold);
            SetLabelText(cookFeedback, LocalTwoPlayerBindings.HudFrontCookHint);

            SetLabelText(EnsureLabel(canvasGo.transform, "ServeRhythmHint",
                LocalTwoPlayerBindings.HudFrontServeHint,
                new Vector2(-24f, -186f), TextAnchor.UpperRight, 16, Teal), LocalTwoPlayerBindings.HudFrontServeHint);

            SetLabelText(EnsureLabel(canvasGo.transform, "FrontBeatHint",
                LocalTwoPlayerBindings.HudFrontBeatHint,
                new Vector2(-24f, -218f), TextAnchor.UpperRight, 15, Gold), LocalTwoPlayerBindings.HudFrontBeatHint);

            var help = EnsureLabel(canvasGo.transform, "HelpLabel",
                LocalTwoPlayerBindings.HudHelp,
                new Vector2(0f, 28f), TextAnchor.LowerCenter, 15, Gold);
            SetLabelText(help, LocalTwoPlayerBindings.HudHelp);
            help.rectTransform.sizeDelta = new Vector2(1800f, 48f);

            var rowGo = canvasGo.transform.Find("InventoryRow");
            if (rowGo == null)
            {
                var go = new GameObject("InventoryRow");
                go.transform.SetParent(canvasGo.transform, false);
                invRow = go.AddComponent<RectTransform>();
                invRow.anchorMin = new Vector2(0.75f, 1f);
                invRow.anchorMax = new Vector2(0.75f, 1f);
                invRow.pivot = new Vector2(0.5f, 1f);
                invRow.anchoredPosition = new Vector2(0f, -252f);
                invRow.sizeDelta = new Vector2(560f, 72f);
            }
            else
            {
                invRow = rowGo.GetComponent<RectTransform>();
            }

            resultRoot = canvasGo.transform.Find("ResultOverlay")?.gameObject;
            if (resultRoot == null)
            {
                resultRoot = EnsurePanel(canvasGo.transform, "ResultOverlay", new Vector2(0f, 0f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.06f, 0.06f, 0.88f));
                var rt = resultRoot.GetComponent<RectTransform>();
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            resultTitle = EnsureLabel(resultRoot.transform, "ResultTitle", "営業終了", Vector2.zero, TextAnchor.MiddleCenter, 48, Gold);
            var titleRt = resultTitle.rectTransform;
            titleRt.anchoredPosition = new Vector2(0f, 80f);
            titleRt.sizeDelta = new Vector2(900f, 60f);

            resultBody = EnsureLabel(resultRoot.transform, "ResultBody", "売上仮点数: 0", Vector2.zero, TextAnchor.MiddleCenter, 28, Teal);
            var bodyRt = resultBody.rectTransform;
            bodyRt.anchoredPosition = new Vector2(0f, -20f);
            bodyRt.sizeDelta = new Vector2(900f, 220f);
            resultBody.alignment = TextAnchor.UpperCenter;

            resultRoot.SetActive(false);
        }

        private static GameObject EnsurePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return go;
        }

        private static void SetLabelText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static Text EnsureLabel(Transform parent, string name, string text, Vector2 anchoredPos, TextAnchor corner, int fontSize, Color color)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var t = existing.GetComponent<Text>();
                if (t != null)
                {
                    t.text = text;
                    return t;
                }
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                         ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = label.rectTransform;
            switch (corner)
            {
                case TextAnchor.UpperLeft:
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    label.alignment = TextAnchor.UpperLeft;
                    break;
                case TextAnchor.UpperRight:
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    label.alignment = TextAnchor.UpperRight;
                    break;
                case TextAnchor.UpperCenter:
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    label.alignment = TextAnchor.UpperCenter;
                    break;
                case TextAnchor.LowerCenter:
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    label.alignment = TextAnchor.MiddleCenter;
                    break;
                case TextAnchor.MiddleCenter:
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    label.alignment = TextAnchor.MiddleCenter;
                    break;
                default:
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    label.alignment = TextAnchor.MiddleCenter;
                    break;
            }

            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(640f, 44f);
            return label;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = CreateRuntimeMat(color);
            return go;
        }

        private static Material CreateRuntimeMat(Color color)
        {
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

            return mat;
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
    }
}
