using System.Collections;
using BackbeatDiner.Back;
using BackbeatDiner.Coop;
using BackbeatDiner.Front;
using BackbeatDiner.Net;
using BackbeatDiner.Rhythm;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Vertical slice — single-role fullscreen for two devices (one screen each).
    /// Back: kitchen + back HUD. Front: diner + front HUD.
    /// Shared <see cref="DaySessionState"/> / setlist always runs (host logic on this scene).
    /// When NGO Host/Client: <see cref="DaySessionNetSync"/> mirrors beat phase + role inputs.
    /// Solo: F8 toggles role view.
    /// </summary>
    public sealed class VerticalSliceBootstrap : MonoBehaviour
    {
        private static readonly Color Gold = DaySessionState.Gold;
        private static readonly Color Teal = DaySessionState.Teal;
        private static readonly Color Navy = new Color(0.05490196f, 0.12156863f, 0.2f, 1f);
        private static readonly Color Cream = new Color(0.92f, 0.88f, 0.78f, 1f);
        private static readonly Color WarmWood = new Color(0.35f, 0.22f, 0.12f, 1f);
        private static readonly Color SoftWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color Stainless = new Color(0.72f, 0.76f, 0.8f, 1f);
        private static readonly Color StainlessDark = new Color(0.42f, 0.46f, 0.5f, 1f);
        private static readonly Color CastIron = new Color(0.12f, 0.12f, 0.14f, 1f);
        private static readonly Color TileGrout = new Color(0.78f, 0.82f, 0.84f, 1f);
        private static readonly Color WarmAmber = new Color(1f, 0.72f, 0.35f, 1f);

        private const KeyCode DebugRoleToggleKey = KeyCode.F8;

        [SerializeField] private AudioClip metronomeClick;
        [SerializeField] private ChartDefinition chart;
        [SerializeField] private SetlistDefinition setlist;
        [SerializeField] private float bpm = 120f;
        [SerializeField] private bool buildPlaceholders = true;
        [SerializeField] private Material goldMaterial;
        [SerializeField] private Material tealMaterial;
        [SerializeField] private Material navyMaterial;
        [SerializeField] private int customerCount = 2;

        private Camera _backCam;
        private Camera _frontCam;
        private BeatLexiconDemo _backBeats;
        private BeatLexiconDemo _frontBeats;
        private SurveillanceThumbnail _surveillance;
        private Transform _hudRoot;
        private Text _helpLabel;
        private Text _roleBadge;
        private GameObject _rolePickOverlay;
        private bool _sessionStarted;

        private IEnumerator Start()
        {
            ResolveMaterials();
            if (buildPlaceholders)
            {
                BuildWorlds();
            }

            EnsureLightsAndFog();
            _backCam = EnsureBackCamera();
            _frontCam = EnsureFrontCamera();
            var surveillanceCam = EnsureSurveillanceCamera(_frontCam);

            if (!VerticalSliceRoleSelection.HasUserSelection)
            {
                VerticalSliceRoleSelection.LoadSavedOrDefault();
                yield return WaitForRolePick();
            }

            ApplyRolePresentation();

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
            _backBeats = systems.AddComponent<BeatLexiconDemo>();
            _frontBeats = systems.AddComponent<BeatLexiconDemo>();
            var receivePulse = systems.AddComponent<BeatReceivePulse>();
            var gapCoop = systems.AddComponent<ChartGapCoopWindow>();
            var snapshotSend = systems.AddComponent<SnapshotSendController>();
            var snapshotReceive = systems.AddComponent<SnapshotReceivePanel>();
            var questRequest = systems.AddComponent<QuestRequestController>();
            var questReceive = systems.AddComponent<QuestReceiveToast>();
            _surveillance = systems.AddComponent<SurveillanceThumbnail>();

            if (metronomeClick == null)
            {
                metronomeClick = Resources.Load<AudioClip>("MetronomeClick") ?? BuildProceduralClick();
            }

            if (setlist == null)
            {
                setlist = Resources.Load<SetlistDefinition>("Setlists/Setlist_DemoDay");
            }

            if (chart == null)
            {
                chart = Resources.Load<ChartDefinition>("Charts/Chart_DemoA");
            }

            if (setlist != null && setlist.Charts.Length > 0 && setlist.Charts[0] != null)
            {
                chart = setlist.Charts[0];
            }

            clock.AssignMetronomeClip(metronomeClick);
            if (chart != null)
            {
                clock.ApplyChart(chart);
                session.ApplyChart(chart);
                bpm = chart.Bpm;
            }
            else
            {
                clock.Bpm = bpm;
            }

            var spawn = GameObject.Find("NoteSpawn")?.transform;
            var hit = GameObject.Find("HitLine")?.transform;
            if (spawn == null || hit == null)
            {
                CreateLaneAnchors(out spawn, out hit);
            }

            highway.Configure(clock, spawn, hit, chart);

            var queueRoot = GameObject.Find("CustomerQueueRoot")?.transform;
            if (queueRoot == null)
            {
                var go = new GameObject("CustomerQueueRoot");
                go.transform.position = new Vector3(6f, 0f, 0f);
                queueRoot = go.transform;
            }

            queue.Configure(queueRoot, tealMaterial, goldMaterial, Mathf.Clamp(customerCount, 1, 3));

            BuildHud(
                out var judgement, out var combo, out var bpmLabel,
                out var prep, out var serve, out var timer, out var cookFeedback,
                out var invRow, out var resultRoot, out var resultTitle, out var resultBody,
                out var sessionFill, out var prepFill, out var serveFill, out var serveMeter,
                out var retryButton, out var titleButton, out var chartNameLabel,
                out var resultSalesValue, out var resultServesValue, out var resultCountdown);

            inventory.BuildUi(invRow);
            // Role bindings stay split-style so solo F8 can drive either side's keys.
            // Hit judge: JP popup + procedural SFX on back lane (Cooking-sim ticket kept).
            // try/catch so HitJudgePopup CanvasGroup fake-null cannot abort Start before StartSetlist.
            try
            {
                var hitFeedback = systems.AddComponent<HitJudgeFeedback>();
                hitInput.Configure(clock, highway, judgement, combo, bpmLabel, twoPlayerSplit: true,
                    hitFeedback, _hudRoot);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HitJudge] Configure failed (notes still start): {ex.Message}");
                hitInput.Configure(clock, highway, judgement, combo, bpmLabel, twoPlayerSplit: true);
            }

            hitInput.Window = JudgeWindow.Default;
            session.Bind(clock, hitInput, prep, serve, timer, resultRoot, resultTitle, resultBody,
                sessionFill, prepFill, serveFill);
            var resultOverlay = systems.AddComponent<ResultOverlayController>();
            resultOverlay.Bind(session, resultRoot, retryButton, titleButton,
                resultSalesValue, resultServesValue, resultBody, resultCountdown);
            serveRhythm.Configure(clock, session, hitInput, cook, cookFeedback, twoPlayerSplit: true, serveMeter);
            cook.Configure(session, queue, cookFeedback, serveRhythm, twoPlayerSplit: true);
            // 裏: A/S/D/F = Beat-B1..B4　表: ←↓→↑ = Beat-F1..F4（配膳中は表のみロック）
            _backBeats.Configure(BeatRole.Back, createPadUi: true, padKeys: BeatPadKeyScheme.BackAsdf);
            _frontBeats.Configure(BeatRole.Front, createPadUi: true, padKeys: BeatPadKeyScheme.FrontArrows);
            // Gap coop: スナップ推奨 / Beat-B3 hint + brief CD unlock + pulse
            receivePulse.Configure(autoSimulate: false, tutorialToast: false);
            gapCoop.Configure(_backBeats, _frontBeats, receivePulse, snapshotSend, questRequest);
            // C2: Tab hold 0.8s → send 1.2s → Back Snap-In panel + SE (CD 25s; gap preferred)
            snapshotReceive.Configure();
            snapshotSend.Configure(inventory, queue, gapCoop);
            // C1: Back Q → Quest SE + Front toast/icon (CD 15s; blocked while notes active)
            questReceive.Configure();
            questRequest.Configure(highway);
            // KG-4: Back surveillance of FrontCamera (~2Hz); only when viewing Back.
            // Scene owns SurveillanceCamera; try/catch keeps StartSetlist reliable if HUD setup fails.
            try
            {
                _surveillance.Configure(highway, _frontCam, surveillanceCam);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Surveillance] Configure failed (notes still start): {ex.Message}");
            }

            ApplyRolePresentation();
            _sessionStarted = true;

            // NGO client: do not start the local setlist/clock — host publishes beat phase + chart.
            var netClientFollower = IsNetcodeClientFollower();

            DaySetlistRunner runner = null;
            var useSetlist = setlist != null && setlist.ChartCount >= 1;
            if (useSetlist)
            {
                runner = systems.AddComponent<DaySetlistRunner>();
                runner.Configure(setlist, clock, highway, session, hitInput, chartNameLabel, gapCoop);
                if (netClientFollower)
                {
                    runner.SetNetworkFollower(true);
                    session.SetNetworkFollower(true);
                    session.Begin();
                    session.SetEndSessionOnChartComplete(false);
                    session.ApplySetlistBudget(setlist);
                    Debug.Log(
                        $"[BACKBEAT DINER] VerticalSlicePrototype ready — CLIENT follower; " +
                        $"role={VerticalSliceRoleSelection.DisplayName}; waiting host beat/chart sync; F8 toggles view.");
                }
                else
                {
                    runner.StartSetlist(0.35);
                    Debug.Log(
                        $"[BACKBEAT DINER] VerticalSlicePrototype ready — role={VerticalSliceRoleSelection.DisplayName} fullscreen; " +
                        $"setlist={setlist.name} charts={setlist.ChartCount} gap={setlist.GapSeconds:0.#}s; shared DaySessionState; F8 toggles role.");
                }
            }
            else
            {
                if (chartNameLabel != null)
                {
                    chartNameLabel.text = chart != null
                        ? $"チャート名: {chart.DisplayName}"
                        : "チャート名: —";
                }

                session.Begin();
                if (netClientFollower)
                {
                    session.SetNetworkFollower(true);
                }
                else
                {
                    clock.StartClock(0.35);
                }

                var chartLabel = chart != null ? chart.DisplayName : "(fallback every-beat)";
                Debug.Log(
                    $"[BACKBEAT DINER] VerticalSlicePrototype ready — role={VerticalSliceRoleSelection.DisplayName} fullscreen; " +
                    $"chart={chartLabel}; shared DaySessionState; F8 toggles role" +
                    (netClientFollower ? "; CLIENT follower (clock from host)." : "."));
            }

            TryBindNetSessionSync(session, runner, clock, hitInput, serveRhythm, judgement);
        }

        private static bool IsNetcodeClientFollower()
        {
            if (!NetSessionConfig.IsNetworked)
            {
                return false;
            }

            var nm = NetworkManager.Singleton;
            // Require a live connected client — stale Client mode without NGO must not skip StartSetlist.
            return nm != null && nm.IsListening && nm.IsConnectedClient && nm.IsClient && !nm.IsServer;
        }

        private void TryBindNetSessionSync(
            DaySessionState session,
            DaySetlistRunner runner,
            RhythmClock clock,
            HitInput hitInput,
            ServeRhythmWindow serveRhythm,
            Text judgementLabel)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                if (NetSessionConfig.IsNetworked)
                {
                    Debug.LogWarning("[Netcode] Session marked networked but NetworkManager is not listening.");
                }

                return;
            }

            var status = EnsureNetStatusLabel();
            if (nm.IsServer)
            {
                var sync = NetcodeSessionBootstrap.SpawnSyncOnServerIfNeeded();
                sync?.BindLocalSources(session, runner, clock, hitInput, serveRhythm, status, judgementLabel);
            }
            else if (DaySessionNetSync.Instance != null)
            {
                DaySessionNetSync.Instance.BindLocalSources(
                    session, runner, clock, hitInput, serveRhythm, status, judgementLabel);
            }
            else
            {
                StartCoroutine(WaitForClientNetSync(
                    session, runner, clock, hitInput, serveRhythm, status, judgementLabel));
            }

            Debug.Log(
                $"[Netcode] VerticalSlice bound — IsHost={nm.IsHost} IsClient={nm.IsClient} " +
                $"role={VerticalSliceRoleSelection.DisplayName} fullscreen (beat+input sync)");
        }

        private IEnumerator WaitForClientNetSync(
            DaySessionState session,
            DaySetlistRunner runner,
            RhythmClock clock,
            HitInput hitInput,
            ServeRhythmWindow serveRhythm,
            Text status,
            Text judgementLabel)
        {
            var timeout = 5f;
            while (timeout > 0f && DaySessionNetSync.Instance == null)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            DaySessionNetSync.Instance?.BindLocalSources(
                session, runner, clock, hitInput, serveRhythm, status, judgementLabel);
        }

        private Text EnsureNetStatusLabel()
        {
            if (_helpLabel == null)
            {
                return null;
            }

            var existing = GameObject.Find("NetSyncStatus");
            if (existing != null)
            {
                return existing.GetComponent<Text>();
            }

            var parent = _helpLabel.transform.parent;
            if (parent == null)
            {
                return null;
            }

            var go = new GameObject("NetSyncStatus");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(720f, 28f);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.alignment = TextAnchor.UpperCenter;
            text.color = Gold;
            text.raycastTarget = false;
            text.text = "NET …";
            return text;
        }

        private void Update()
        {
            if (!_sessionStarted)
            {
                return;
            }

            if (Input.GetKeyDown(DebugRoleToggleKey))
            {
                VerticalSliceRoleSelection.Toggle();
                ApplyRolePresentation();
                Debug.Log($"[BACKBEAT DINER] Debug role switch → {VerticalSliceRoleSelection.DisplayName} fullscreen");
            }
        }

        private IEnumerator WaitForRolePick()
        {
            EnsureRolePickOverlay();
            if (_rolePickOverlay != null)
            {
                _rolePickOverlay.SetActive(true);
            }

            while (!VerticalSliceRoleSelection.HasUserSelection)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                {
                    VerticalSliceRoleSelection.Select(VerticalSlicePlayRole.Back);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                {
                    VerticalSliceRoleSelection.Select(VerticalSlicePlayRole.Front);
                }

                yield return null;
            }

            if (_rolePickOverlay != null)
            {
                _rolePickOverlay.SetActive(false);
            }
        }

        private void ApplyRolePresentation()
        {
            var back = VerticalSliceRoleSelection.IsBack;
            ApplyRoleCameras(back);
            ApplyRoleHud(back);
            _backBeats?.SetPadUiVisible(back);
            _frontBeats?.SetPadUiVisible(!back);

            if (_surveillance != null)
            {
                _surveillance.enabled = back;
                SetNamedActive("SurveillanceHud", back);
            }

            SetNamedActive("QuestRequestHud", back);
            SetNamedActive("SnapshotReceivePanel", back);
            SetNamedActive("SnapshotEdgePulse", back);
            SetNamedActive("SnapshotSendHud", !back);
            SetNamedActive("QuestReceiveToast", !back);

            if (_roleBadge != null)
            {
                _roleBadge.text = back ? "役: 裏（キッチン全画面）" : "役: 表（ダイナー全画面）";
            }

            if (_helpLabel != null)
            {
                _helpLabel.text = back
                    ? LocalTwoPlayerBindings.HudHelpBack
                    : LocalTwoPlayerBindings.HudHelpFront;
            }
        }

        private void ApplyRoleCameras(bool back)
        {
            if (_backCam == null)
            {
                _backCam = EnsureBackCamera();
            }

            if (_frontCam == null)
            {
                _frontCam = EnsureFrontCamera();
            }

            _backCam.rect = new Rect(0f, 0f, 1f, 1f);
            _frontCam.rect = new Rect(0f, 0f, 1f, 1f);
            _backCam.enabled = back;
            _frontCam.enabled = !back;

            EnsureSingleAudioListener(back ? _backCam.gameObject : _frontCam.gameObject);
        }

        private static void EnsureSingleAudioListener(GameObject host)
        {
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener != null && listener.gameObject != host)
                {
                    Object.Destroy(listener);
                }
            }

            if (host.GetComponent<AudioListener>() == null)
            {
                host.AddComponent<AudioListener>();
            }
        }

        private void ApplyRoleHud(bool back)
        {
            if (_hudRoot == null)
            {
                var canvas = GameObject.Find("VerticalSliceHUD");
                _hudRoot = canvas != null ? canvas.transform : null;
            }

            if (_hudRoot == null)
            {
                return;
            }

            SetChildActive(_hudRoot, "BackPanel", back);
            SetChildActive(_hudRoot, "BackTitle", back);
            SetChildActive(_hudRoot, "JudgementLabel", back);
            SetChildActive(_hudRoot, "ComboLabel", back);
            SetChildActive(_hudRoot, "BpmLabel", back);
            SetChildActive(_hudRoot, "BackControlsHint", back);
            SetChildActive(_hudRoot, "BackQuestPrompt", back);

            SetChildActive(_hudRoot, "FrontPanel", !back);
            SetChildActive(_hudRoot, "FrontTitle", !back);
            SetChildActive(_hudRoot, "PrepLabel", !back);
            SetChildActive(_hudRoot, "ServeLabel", !back);
            SetChildActive(_hudRoot, "PrepBar", !back);
            SetChildActive(_hudRoot, "ServeBar", !back);
            SetChildActive(_hudRoot, "CookFeedback", !back);
            SetChildActive(_hudRoot, "ServeRhythmHint", !back);
            SetChildActive(_hudRoot, "FrontBeatHint", !back);
            SetChildActive(_hudRoot, "FrontSnapHint", !back);
            SetChildActive(_hudRoot, "ServeMeterPanel", !back);
            SetChildActive(_hudRoot, "ServeRhythmMeter", !back);
            SetChildActive(_hudRoot, "InventoryRow", !back);
        }

        private static void SetChildActive(Transform root, string name, bool active)
        {
            var t = root.Find(name);
            if (t != null)
            {
                t.gameObject.SetActive(active);
            }
        }

        private void SetNamedActive(string name, bool active)
        {
            if (_hudRoot == null)
            {
                var canvas = GameObject.Find("VerticalSliceHUD");
                _hudRoot = canvas != null ? canvas.transform : null;
            }

            if (_hudRoot != null)
            {
                var underHud = _hudRoot.Find(name);
                if (underHud != null)
                {
                    underHud.gameObject.SetActive(active);
                    return;
                }
            }

            var go = GameObject.Find(name);
            if (go != null)
            {
                go.SetActive(active);
            }
        }

        private void EnsureRolePickOverlay()
        {
            if (_rolePickOverlay != null)
            {
                return;
            }

            EnsureEventSystem();
            var canvas = CookingSimHud.EnsureOverlayCanvas("VerticalSliceHUD", 20);
            _hudRoot = canvas.transform;
            var panel = CookingSimHud.EnsurePanel(
                canvas.transform,
                "RolePickOverlay",
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.04f, 0.07f, 0.94f),
                goldBorder: false);
            var rt = panel.GetComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            panel.transform.SetAsLastSibling();

            CookingSimHud.EnsureLabel(panel.transform, "RolePickTitle", "BACKBEAT DINER",
                new Vector2(0f, 120f), TextAnchor.MiddleCenter, 42, CookingSimHud.Gold, FontStyle.Bold, 800f, 52f);
            CookingSimHud.EnsureLabel(panel.transform, "RolePickSub", "端末ごとに役を選んでください（2デバイス・各1画面）",
                new Vector2(0f, 60f), TextAnchor.MiddleCenter, CookingSimHud.FontBody, CookingSimHud.Cream, FontStyle.Normal, 900f, 36f);
            CookingSimHud.EnsureLabel(panel.transform, "RolePickHint", LocalTwoPlayerBindings.RolePickHint,
                new Vector2(0f, -120f), TextAnchor.MiddleCenter, CookingSimHud.FontRow, CookingSimHud.Teal, FontStyle.Normal, 800f, 32f);

            BuildRolePickButton(panel.transform, "PickBackButton", "裏で始める", new Vector2(-220f, -20f), () =>
            {
                VerticalSliceRoleSelection.Select(VerticalSlicePlayRole.Back);
            });
            BuildRolePickButton(panel.transform, "PickFrontButton", "表で始める", new Vector2(220f, -20f), () =>
            {
                VerticalSliceRoleSelection.Select(VerticalSlicePlayRole.Front);
            });

            _rolePickOverlay = panel;
        }

        private static void BuildRolePickButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = existing.gameObject;
            }

            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(360f, 72f);

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = CookingSimHud.Gold;
            var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);

            var text = go.GetComponentInChildren<Text>();
            if (text == null)
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);
                text = labelGo.AddComponent<Text>();
                var lrt = text.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
            }

            text.font = CookingSimHud.UiFont();
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.color = Navy;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void ResolveMaterials()
        {
            if (goldMaterial == null)
            {
                goldMaterial = CreateRuntimeMat(Gold, lit: true);
            }

            if (tealMaterial == null)
            {
                tealMaterial = CreateRuntimeMat(Teal, lit: true);
            }

            if (navyMaterial == null)
            {
                navyMaterial = CreateRuntimeMat(Navy, lit: true);
            }
        }

        private void BuildWorlds()
        {
            if (GameObject.Find("BackKitchenRoot") != null)
            {
                return;
            }

            BuildBackKitchen();
            BuildFrontDinerRoom();
            CreateLaneAnchors(out _, out _);
        }

        private void BuildBackKitchen()
        {
            var back = new GameObject("BackKitchenRoot");
            back.transform.position = new Vector3(-6f, 0f, 0f);

            // Kitchen floor tiles (teal / stainless checker)
            BuildFloorTiles(back.transform, 8, 6, 0.95f, Teal * 0.4f, StainlessDark * 0.85f);

            CreatePrim(PrimitiveType.Cube, "BackWall", back.transform, new Vector3(0f, 1.6f, 2.8f), new Vector3(8f, 3.2f, 0.18f), Navy);
            CreatePrim(PrimitiveType.Cube, "SideWallL", back.transform, new Vector3(-3.9f, 1.6f, 0.4f), new Vector3(0.18f, 3.2f, 5.2f), Navy * 1.05f);
            CreatePrim(PrimitiveType.Cube, "SideWallR", back.transform, new Vector3(3.9f, 1.6f, 0.4f), new Vector3(0.18f, 3.2f, 5.2f), Navy * 1.05f);

            BuildKitchenBacksplash(back.transform);
            BuildStainlessPrepLine(back.transform);
            BuildStoveOven(back.transform);
            BuildPotShelf(back.transform);
            BuildPassWindowStation(back.transform);
            BuildKitchenWarmerLights(back.transform);

            // Rhythm pads row (A S D F visual) on prep counter
            var padColors = new[] { Gold, Teal, Gold * 0.85f, Teal * 1.2f };
            var padLabels = new[] { "A", "S", "D", "F" };
            for (var i = 0; i < 4; i++)
            {
                var x = -1.35f + i * 0.9f;
                CreatePrim(PrimitiveType.Cube, $"BeatPad_{padLabels[i]}", back.transform,
                    new Vector3(x, 1.02f, -0.05f), new Vector3(0.62f, 0.1f, 0.62f), padColors[i]);
                CreatePrim(PrimitiveType.Cylinder, $"PadStem_{padLabels[i]}", back.transform,
                    new Vector3(x, 0.96f, -0.05f), new Vector3(0.18f, 0.04f, 0.18f), Navy);
            }

            CreatePrim(PrimitiveType.Cube, "HitPad", back.transform, new Vector3(0f, 0.98f, -0.85f), new Vector3(1.1f, 0.12f, 0.45f), Gold);
            CreatePrim(PrimitiveType.Cube, "LaneRailL", back.transform, new Vector3(-0.85f, 1.05f, 1.2f), new Vector3(0.08f, 0.06f, 3.6f), Teal * 0.8f);
            CreatePrim(PrimitiveType.Cube, "LaneRailR", back.transform, new Vector3(0.85f, 1.05f, 1.2f), new Vector3(0.08f, 0.06f, 3.6f), Teal * 0.8f);
            CreatePrim(PrimitiveType.Quad, "LaneDeck", back.transform, new Vector3(0f, 1.01f, 1.2f), new Vector3(1.55f, 3.5f, 1f), Navy * 1.25f);
            var deck = back.transform.Find("LaneDeck");
            if (deck != null)
            {
                deck.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            CreatePendantLamp(back.transform, new Vector3(-1.6f, 2.55f, 0.2f), "BackLampL");
            CreatePendantLamp(back.transform, new Vector3(1.6f, 2.55f, 0.2f), "BackLampR");
        }

        private void BuildKitchenBacksplash(Transform parent)
        {
            var root = new GameObject("BacksplashTiles");
            root.transform.SetParent(parent, false);
            const int cols = 10;
            const int rows = 3;
            const float size = 0.55f;
            var originX = -(cols - 1) * size * 0.5f;
            var originY = 1.05f;
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < cols; x++)
                {
                    var color = ((x + y) & 1) == 0 ? SoftWhite : TileGrout;
                    CreatePrim(PrimitiveType.Cube, $"Splash_{x}_{y}", root.transform,
                        new Vector3(originX + x * size, originY + y * size, 2.68f),
                        new Vector3(size * 0.92f, size * 0.92f, 0.04f),
                        color);
                }
            }

            // Gold / teal accent strip under splash
            CreatePrim(PrimitiveType.Cube, "SplashAccentGold", parent, new Vector3(0f, 0.88f, 2.66f), new Vector3(5.6f, 0.06f, 0.05f), Gold);
            CreatePrim(PrimitiveType.Cube, "SplashAccentTeal", parent, new Vector3(0f, 0.8f, 2.66f), new Vector3(5.6f, 0.05f, 0.05f), Teal);
        }

        private void BuildStainlessPrepLine(Transform parent)
        {
            // Main stainless prep counter (replaces flat teal block)
            CreatePrim(PrimitiveType.Cube, "KitchenCounter", parent, new Vector3(0f, 0.45f, 0.35f), new Vector3(3.6f, 0.9f, 1.2f), StainlessDark);
            CreatePrim(PrimitiveType.Cube, "CounterTop", parent, new Vector3(0f, 0.92f, 0.35f), new Vector3(3.75f, 0.07f, 1.3f), Stainless);
            CreatePrim(PrimitiveType.Cube, "CounterToeKick", parent, new Vector3(0f, 0.06f, 0.35f), new Vector3(3.55f, 0.12f, 1.15f), CastIron);
            CreatePrim(PrimitiveType.Cube, "CounterTealTrim", parent, new Vector3(0f, 0.72f, -0.24f), new Vector3(3.62f, 0.08f, 0.04f), Teal);
            CreatePrim(PrimitiveType.Cube, "CounterGoldHandle", parent, new Vector3(-1.1f, 0.55f, -0.26f), new Vector3(0.55f, 0.04f, 0.05f), Gold);
            CreatePrim(PrimitiveType.Cube, "CounterGoldHandleR", parent, new Vector3(1.1f, 0.55f, -0.26f), new Vector3(0.55f, 0.04f, 0.05f), Gold);

            // Cutting / prep board on left of pads
            CreatePrim(PrimitiveType.Cube, "PrepBoard", parent, new Vector3(-2.05f, 0.98f, 0.15f), new Vector3(0.85f, 0.05f, 0.7f), WarmWood);
            CreatePrim(PrimitiveType.Cube, "PrepBoardGroove", parent, new Vector3(-2.05f, 1.01f, 0.15f), new Vector3(0.7f, 0.01f, 0.04f), WarmWood * 0.7f);
            CreatePrim(PrimitiveType.Cube, "ChefKnife", parent, new Vector3(-1.75f, 1.02f, 0.35f), new Vector3(0.35f, 0.02f, 0.06f), Stainless);
            CreatePrim(PrimitiveType.Cube, "KnifeHandle", parent, new Vector3(-1.55f, 1.02f, 0.35f), new Vector3(0.12f, 0.03f, 0.05f), CastIron);

            // Side stainless wing (dish landing)
            CreatePrim(PrimitiveType.Cube, "SideCounter", parent, new Vector3(2.55f, 0.4f, 0.9f), new Vector3(1.1f, 0.8f, 1.6f), StainlessDark);
            CreatePrim(PrimitiveType.Cube, "SideCounterTop", parent, new Vector3(2.55f, 0.82f, 0.9f), new Vector3(1.15f, 0.06f, 1.65f), Stainless);
            CreatePrim(PrimitiveType.Cylinder, "PlateStack", parent, new Vector3(2.55f, 0.95f, 1.2f), new Vector3(0.35f, 0.08f, 0.35f), SoftWhite);
            CreatePrim(PrimitiveType.Cylinder, "PlateStack2", parent, new Vector3(2.55f, 1.05f, 1.2f), new Vector3(0.32f, 0.06f, 0.32f), SoftWhite);
        }

        private void BuildStoveOven(Transform parent)
        {
            var root = new GameObject("StoveOven");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-2.55f, 0f, 1.15f);

            CreatePrim(PrimitiveType.Cube, "RangeBody", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.35f, 0.9f, 0.95f), StainlessDark);
            CreatePrim(PrimitiveType.Cube, "OvenDoor", root.transform, new Vector3(0f, 0.35f, -0.48f), new Vector3(1.05f, 0.55f, 0.06f), CastIron);
            CreatePrim(PrimitiveType.Cube, "OvenWindow", root.transform, new Vector3(0f, 0.38f, -0.52f), new Vector3(0.7f, 0.32f, 0.03f), Navy * 1.4f);
            CreatePrim(PrimitiveType.Cube, "OvenHandle", root.transform, new Vector3(0f, 0.58f, -0.56f), new Vector3(0.55f, 0.04f, 0.05f), Gold);
            CreatePrim(PrimitiveType.Cube, "Cooktop", root.transform, new Vector3(0f, 0.92f, 0f), new Vector3(1.4f, 0.06f, 1.0f), CastIron);

            // Four burners
            var burnerOffsets = new[]
            {
                new Vector3(-0.32f, 0.97f, 0.18f),
                new Vector3(0.32f, 0.97f, 0.18f),
                new Vector3(-0.32f, 0.97f, -0.22f),
                new Vector3(0.32f, 0.97f, -0.22f)
            };
            for (var i = 0; i < burnerOffsets.Length; i++)
            {
                CreatePrim(PrimitiveType.Cylinder, $"Burner_{i}", root.transform, burnerOffsets[i], new Vector3(0.28f, 0.02f, 0.28f), Stainless);
                CreatePrim(PrimitiveType.Cylinder, $"BurnerCore_{i}", root.transform,
                    burnerOffsets[i] + new Vector3(0f, 0.02f, 0f), new Vector3(0.12f, 0.015f, 0.12f), WarmAmber);
            }

            CreatePrim(PrimitiveType.Cube, "ControlPanel", root.transform, new Vector3(0f, 1.05f, 0.42f), new Vector3(1.2f, 0.22f, 0.08f), Stainless);
            CreatePrim(PrimitiveType.Cylinder, "KnobL", root.transform, new Vector3(-0.35f, 1.05f, 0.48f), new Vector3(0.08f, 0.04f, 0.08f), Gold);
            CreatePrim(PrimitiveType.Cylinder, "KnobR", root.transform, new Vector3(0.35f, 1.05f, 0.48f), new Vector3(0.08f, 0.04f, 0.08f), Teal);
            CreatePrim(PrimitiveType.Cylinder, "SaucePot", root.transform, new Vector3(-0.32f, 1.12f, 0.18f), new Vector3(0.3f, 0.14f, 0.3f), Stainless);
            CreatePrim(PrimitiveType.Cylinder, "SaucePotLid", root.transform, new Vector3(-0.32f, 1.26f, 0.18f), new Vector3(0.28f, 0.03f, 0.28f), StainlessDark);
        }

        private void BuildPotShelf(Transform parent)
        {
            var root = new GameObject("PotShelf");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(2.2f, 2.05f, 2.55f);

            CreatePrim(PrimitiveType.Cube, "ShelfBoard", root.transform, new Vector3(0f, 0f, 0f), new Vector3(2.2f, 0.07f, 0.45f), WarmWood);
            CreatePrim(PrimitiveType.Cube, "ShelfBracketL", root.transform, new Vector3(-0.95f, -0.2f, 0.05f), new Vector3(0.08f, 0.35f, 0.3f), StainlessDark);
            CreatePrim(PrimitiveType.Cube, "ShelfBracketR", root.transform, new Vector3(0.95f, -0.2f, 0.05f), new Vector3(0.08f, 0.35f, 0.3f), StainlessDark);
            CreatePrim(PrimitiveType.Cube, "ShelfRail", root.transform, new Vector3(0f, 0.18f, -0.18f), new Vector3(2.0f, 0.04f, 0.04f), Teal);

            CreatePrim(PrimitiveType.Cylinder, "PotA", root.transform, new Vector3(-0.7f, 0.22f, 0.05f), new Vector3(0.38f, 0.2f, 0.38f), Stainless);
            CreatePrim(PrimitiveType.Cylinder, "PotALid", root.transform, new Vector3(-0.7f, 0.42f, 0.05f), new Vector3(0.36f, 0.04f, 0.36f), Gold);
            CreatePrim(PrimitiveType.Cylinder, "PotB", root.transform, new Vector3(0f, 0.18f, 0.05f), new Vector3(0.32f, 0.16f, 0.32f), StainlessDark);
            CreatePrim(PrimitiveType.Cylinder, "PotBHandle", root.transform, new Vector3(0.22f, 0.22f, 0.05f), new Vector3(0.18f, 0.03f, 0.05f), CastIron);
            CreatePrim(PrimitiveType.Cylinder, "PotC", root.transform, new Vector3(0.7f, 0.2f, 0.05f), new Vector3(0.34f, 0.18f, 0.34f), Stainless);
            CreatePrim(PrimitiveType.Sphere, "PotCLidKnob", root.transform, new Vector3(0.7f, 0.4f, 0.05f), new Vector3(0.08f, 0.08f, 0.08f), Teal);
        }

        private void BuildPassWindowStation(Transform parent)
        {
            CreatePrim(PrimitiveType.Cube, "PassWall", parent, new Vector3(0f, 1.55f, 1.18f), new Vector3(3.2f, 2.0f, 0.14f), StainlessDark);
            CreatePrim(PrimitiveType.Cube, "PassFrame", parent, new Vector3(0f, 1.5f, 1.1f), new Vector3(2.0f, 1.05f, 0.1f), Teal);
            CreatePrim(PrimitiveType.Cube, "PassWindow", parent, new Vector3(0f, 1.5f, 1.05f), new Vector3(1.65f, 0.78f, 0.06f), Gold);
            CreatePrim(PrimitiveType.Cube, "PassShelf", parent, new Vector3(0f, 1.08f, 0.85f), new Vector3(2.5f, 0.07f, 0.55f), Stainless);
            CreatePrim(PrimitiveType.Cube, "PassShelfLip", parent, new Vector3(0f, 1.14f, 0.6f), new Vector3(2.5f, 0.05f, 0.05f), Gold);
            CreatePrim(PrimitiveType.Cube, "TicketRail", parent, new Vector3(0f, 2.15f, 1.05f), new Vector3(1.8f, 0.04f, 0.04f), Teal);
            CreatePrim(PrimitiveType.Cube, "TicketA", parent, new Vector3(-0.4f, 1.95f, 1.02f), new Vector3(0.22f, 0.28f, 0.02f), SoftWhite);
            CreatePrim(PrimitiveType.Cube, "TicketB", parent, new Vector3(0.15f, 1.95f, 1.02f), new Vector3(0.22f, 0.28f, 0.02f), SoftWhite);
            CreatePrim(PrimitiveType.Cylinder, "PassPlate", parent, new Vector3(-0.45f, 1.15f, 0.85f), new Vector3(0.35f, 0.03f, 0.35f), SoftWhite);
            CreatePrim(PrimitiveType.Cylinder, "PassBowl", parent, new Vector3(0.4f, 1.18f, 0.85f), new Vector3(0.28f, 0.08f, 0.28f), Teal * 0.9f);
        }

        private void BuildKitchenWarmerLights(Transform parent)
        {
            var root = new GameObject("WarmerLights");
            root.transform.SetParent(parent, false);

            CreateWarmerLamp(root.transform, new Vector3(-0.55f, 2.35f, 0.85f), "WarmerL");
            CreateWarmerLamp(root.transform, new Vector3(0.55f, 2.35f, 0.85f), "WarmerR");
            CreateWarmerLamp(root.transform, new Vector3(0f, 2.35f, 0.85f), "WarmerC");
        }

        private void CreateWarmerLamp(Transform parent, Vector3 localPos, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            CreatePrim(PrimitiveType.Cube, "Arm", root.transform, new Vector3(0f, 0.15f, -0.1f), new Vector3(0.06f, 0.06f, 0.35f), StainlessDark);
            CreatePrim(PrimitiveType.Cylinder, "Canopy", root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.32f, 0.08f, 0.32f), Stainless);
            CreatePrim(PrimitiveType.Sphere, "Bulb", root.transform, new Vector3(0f, -0.08f, 0f), new Vector3(0.14f, 0.1f, 0.14f), WarmAmber);

            var lightGo = new GameObject("WarmLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.78f, 0.45f);
            light.intensity = 1.8f;
            light.range = 3.2f;
            light.spotAngle = 55f;
            lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void BuildFrontDinerRoom()
        {
            var front = new GameObject("FrontDinerRoot");
            front.transform.position = new Vector3(6f, 0f, 0f);

            BuildFloorTiles(front.transform, 7, 5, 0.95f, Cream * 0.85f, Teal * 0.35f);

            CreatePrim(PrimitiveType.Cube, "BackWall", front.transform, new Vector3(0f, 1.7f, 3.35f), new Vector3(7.4f, 3.4f, 0.16f), Navy);
            CreatePrim(PrimitiveType.Cube, "SideWallL", front.transform, new Vector3(-3.6f, 1.7f, 1.0f), new Vector3(0.16f, 3.4f, 5.0f), Navy * 1.08f);
            CreatePrim(PrimitiveType.Cube, "SideWallR", front.transform, new Vector3(3.6f, 1.7f, 1.0f), new Vector3(0.16f, 3.4f, 5.0f), Navy * 1.08f);
            CreatePrim(PrimitiveType.Cube, "CeilingTrim", front.transform, new Vector3(0f, 3.25f, 1.0f), new Vector3(7.2f, 0.12f, 4.8f), Teal * 0.7f);

            // Kitchen pass wall separating back/front feel on the right stage
            CreatePrim(PrimitiveType.Cube, "KitchenPassWall", front.transform, new Vector3(0f, 1.5f, -1.15f), new Vector3(4.2f, 3f, 0.2f), Teal);
            CreatePrim(PrimitiveType.Cube, "PassWindow", front.transform, new Vector3(0f, 1.45f, -1.05f), new Vector3(1.8f, 0.85f, 0.1f), Gold);
            CreatePrim(PrimitiveType.Cube, "ServiceCounter", front.transform, new Vector3(0f, 0.45f, -0.35f), new Vector3(3.8f, 0.9f, 0.95f), Teal);
            CreatePrim(PrimitiveType.Cube, "ServiceTop", front.transform, new Vector3(0f, 0.92f, -0.35f), new Vector3(3.9f, 0.08f, 1.05f), SoftWhite);
            CreatePrim(PrimitiveType.Cube, "MenuBoard", front.transform, new Vector3(0f, 2.35f, -1.05f), new Vector3(2.2f, 0.55f, 0.08f), Navy);
            CreatePrim(PrimitiveType.Cube, "MenuAccent", front.transform, new Vector3(0f, 2.35f, -1.0f), new Vector3(1.9f, 0.08f, 0.04f), Gold);

            // Booths + tables
            BuildBooth(front.transform, new Vector3(-2.2f, 0f, 1.6f), "BoothL");
            BuildBooth(front.transform, new Vector3(2.2f, 0f, 1.6f), "BoothR");
            BuildTableSet(front.transform, new Vector3(-2.2f, 0f, 0.35f), "TableL");
            BuildTableSet(front.transform, new Vector3(2.2f, 0f, 0.35f), "TableR");
            BuildTableSet(front.transform, new Vector3(0f, 0f, 2.1f), "TableC");

            CreatePendantLamp(front.transform, new Vector3(-2.2f, 2.7f, 1.1f), "FrontLampL");
            CreatePendantLamp(front.transform, new Vector3(2.2f, 2.7f, 1.1f), "FrontLampR");
            CreatePendantLamp(front.transform, new Vector3(0f, 2.85f, 0.2f), "FrontLampC");

            // Brand sign above pass
            CreatePrim(PrimitiveType.Cube, "LogoSign", front.transform, new Vector3(0f, 2.85f, 3.2f), new Vector3(3.2f, 0.55f, 0.1f), Navy);
            CreatePrim(PrimitiveType.Cube, "LogoBar", front.transform, new Vector3(0f, 2.55f, 3.15f), new Vector3(2.6f, 0.06f, 0.06f), Gold);

            if (GameObject.Find("CustomerQueueRoot") == null)
            {
                var q = new GameObject("CustomerQueueRoot");
                q.transform.SetParent(front.transform, false);
                q.transform.localPosition = new Vector3(0f, 0f, 0.9f);
            }
        }

        private void BuildFloorTiles(Transform parent, int cols, int rows, float size, Color a, Color b)
        {
            var root = new GameObject("FloorTiles");
            root.transform.SetParent(parent, false);
            var originX = -(cols - 1) * size * 0.5f;
            var originZ = -(rows - 1) * size * 0.5f + 0.8f;
            for (var z = 0; z < rows; z++)
            {
                for (var x = 0; x < cols; x++)
                {
                    var color = ((x + z) & 1) == 0 ? a : b;
                    CreatePrim(PrimitiveType.Cube, $"Tile_{x}_{z}", root.transform,
                        new Vector3(originX + x * size, -0.05f, originZ + z * size),
                        new Vector3(size * 0.96f, 0.08f, size * 0.96f),
                        color);
                }
            }
        }

        private void BuildBooth(Transform parent, Vector3 localPos, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            CreatePrim(PrimitiveType.Cube, "Seat", root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(1.5f, 0.35f, 0.7f), Teal);
            CreatePrim(PrimitiveType.Cube, "Backrest", root.transform, new Vector3(0f, 0.7f, 0.32f), new Vector3(1.5f, 0.85f, 0.18f), Teal * 1.15f);
            CreatePrim(PrimitiveType.Cube, "Cushion", root.transform, new Vector3(0f, 0.48f, -0.05f), new Vector3(1.35f, 0.1f, 0.55f), Gold * 0.75f);
        }

        private void BuildTableSet(Transform parent, Vector3 localPos, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            CreatePrim(PrimitiveType.Cylinder, "Leg", root.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.18f, 0.35f, 0.18f), WarmWood);
            CreatePrim(PrimitiveType.Cylinder, "Top", root.transform, new Vector3(0f, 0.72f, 0f), new Vector3(1.05f, 0.05f, 1.05f), WarmWood * 1.15f);
            CreatePrim(PrimitiveType.Cylinder, "Plate", root.transform, new Vector3(0.15f, 0.8f, 0.1f), new Vector3(0.22f, 0.02f, 0.22f), SoftWhite);
            CreatePrim(PrimitiveType.Cube, "Cup", root.transform, new Vector3(-0.2f, 0.82f, -0.1f), new Vector3(0.1f, 0.12f, 0.1f), Gold);
        }

        private void CreatePendantLamp(Transform parent, Vector3 localPos, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            CreatePrim(PrimitiveType.Cylinder, "Cord", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.04f, 0.25f, 0.04f), Navy);
            CreatePrim(PrimitiveType.Sphere, "Shade", root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.45f, 0.28f, 0.45f), Gold);
            CreatePrim(PrimitiveType.Sphere, "Bulb", root.transform, new Vector3(0f, -0.12f, 0f), new Vector3(0.16f, 0.16f, 0.16f), SoftWhite);

            var lightGo = new GameObject("PointLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.9f, 0.7f);
            light.intensity = 1.35f;
            light.range = 4.5f;
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
            spawnGo.transform.position = origin + new Vector3(0f, 1.15f, 3.0f);
            var hitGo = new GameObject("HitLine");
            hitGo.transform.position = origin + new Vector3(0f, 1.05f, -0.85f);
            CreatePrimStatic(PrimitiveType.Cube, "HitLineVisual", hitGo.transform, Vector3.zero, new Vector3(1.5f, 0.06f, 0.1f), Gold);
            CreatePrimStatic(PrimitiveType.Quad, "HitGlow", hitGo.transform, new Vector3(0f, 0.02f, 0f), new Vector3(1.7f, 0.35f, 1f),
                new Color(Gold.r, Gold.g, Gold.b, 0.35f));
            var glow = hitGo.transform.Find("HitGlow");
            if (glow != null)
            {
                glow.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

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

            go.tag = "Untagged";
            cam.transform.position = new Vector3(-6f, 2.55f, -4.4f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Navy;
            cam.depth = 0;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            return cam;
        }

        private static Camera EnsureFrontCamera()
        {
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

            cam.transform.position = new Vector3(6f, 3.25f, -5.4f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Navy;
            cam.depth = 1;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            return cam;
        }

        /// <summary>
        /// Scene-authored RT capture cam for Back surveillance. Never MainCamera / no AudioListener.
        /// </summary>
        private static Camera EnsureSurveillanceCamera(Camera front)
        {
            var go = GameObject.Find("SurveillanceCamera");
            Camera cam = null;
            if (go != null)
            {
                cam = go.GetComponent<Camera>();
            }

            if (cam == null)
            {
                if (go != null)
                {
                    go.name = "SurveillanceCamera_Stale";
                    Object.Destroy(go);
                }

                go = new GameObject("SurveillanceCamera");
                cam = go.AddComponent<Camera>();
            }

            go.tag = "Untagged";
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Navy;
            cam.depth = -10;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.allowHDR = false;
            cam.allowMSAA = false;

            var listener = go.GetComponent<AudioListener>();
            if (listener != null)
            {
                Object.Destroy(listener);
            }

            if (front != null)
            {
                go.transform.SetPositionAndRotation(front.transform.position, front.transform.rotation);
                cam.fieldOfView = front.fieldOfView;
                cam.orthographic = front.orthographic;
                cam.orthographicSize = front.orthographicSize;
                cam.cullingMask = front.cullingMask;
            }
            else
            {
                go.transform.position = new Vector3(6f, 3.25f, -5.4f);
                go.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            }

            return cam;
        }

        private static void EnsureLightsAndFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.06f, 0.12f, 0.16f, 1f);
            RenderSettings.fogDensity = 0.028f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.24f, 0.28f);

            var existing = Object.FindFirstObjectByType<Light>();
            Light key;
            if (existing != null && existing.type == LightType.Directional)
            {
                key = existing;
            }
            else
            {
                var lightGo = new GameObject("KeyLight");
                key = lightGo.AddComponent<Light>();
                key.type = LightType.Directional;
            }

            key.name = "KeyLight";
            key.color = new Color(1f, 0.94f, 0.84f);
            key.intensity = 0.95f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            if (GameObject.Find("FillLight") == null)
            {
                var fillGo = new GameObject("FillLight");
                var fill = fillGo.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(0.45f, 0.7f, 0.75f);
                fill.intensity = 0.35f;
                fill.shadows = LightShadows.None;
                fillGo.transform.rotation = Quaternion.Euler(25f, 140f, 0f);
            }
        }

        private void BuildHud(
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
            out Text resultBody,
            out Image sessionFill,
            out Image prepFill,
            out Image serveFill,
            out Image serveMeter,
            out Button retryButton,
            out Button titleButton,
            out Text chartNameLabel,
            out Text resultSalesValue,
            out Text resultServesValue,
            out Text resultCountdown)
        {
            var canvas = CookingSimHud.EnsureOverlayCanvas("VerticalSliceHUD", 20);
            var root = canvas.transform;
            _hudRoot = root;

            // Remove legacy split divider if a previous Play left one in the hierarchy.
            var legacySplit = root.Find("SplitDivider");
            if (legacySplit != null)
            {
                Object.Destroy(legacySplit.gameObject);
            }

            // Center brand (compact — does not compete with order tickets)
            var logo = CookingSimHud.EnsureLabel(root, "BrandLogo", "BACKBEAT DINER", new Vector2(0f, -8f),
                TextAnchor.UpperCenter, 34, CookingSimHud.Gold, FontStyle.Bold, 720f, 42f);
            logo.fontStyle = FontStyle.Bold;
            CookingSimHud.EnsureLabel(root, "BrandSub", "バックビート・ダイナー", new Vector2(0f, -46f),
                TextAnchor.UpperCenter, CookingSimHud.FontHint, CookingSimHud.Teal, FontStyle.Normal, 480f, 26f);
            chartNameLabel = CookingSimHud.EnsureLabel(root, "ChartNameLabel", "チャート名: —",
                new Vector2(0f, -72f), TextAnchor.UpperCenter, CookingSimHud.FontRow, CookingSimHud.Cream,
                FontStyle.Normal, 560f, 28f);
            _roleBadge = CookingSimHud.EnsureLabel(root, "RoleBadge", "役: —",
                new Vector2(0f, -100f), TextAnchor.UpperCenter, CookingSimHud.FontHint, CookingSimHud.GoldBright,
                FontStyle.Bold, 520f, 26f);

            // Back order ticket (fullscreen when 裏)
            CookingSimHud.EnsurePanel(root, "BackPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(24f, -120f), new Vector2(520f, 300f), CookingSimHud.PanelBg);
            CookingSimHud.EnsureLabel(root, "BackTitle", "【注文票】裏 · リズムステーション",
                new Vector2(40f, -130f), TextAnchor.UpperLeft, CookingSimHud.FontHeader, CookingSimHud.Gold,
                FontStyle.Bold, 480f, 34f);
            judgement = CookingSimHud.EnsureLabel(root, "JudgementLabel", "判定: —",
                new Vector2(40f, -172f), TextAnchor.UpperLeft, 30, CookingSimHud.White, FontStyle.Bold, 480f, 38f);
            combo = CookingSimHud.EnsureLabel(root, "ComboLabel", "コンボ: 0",
                new Vector2(40f, -214f), TextAnchor.UpperLeft, CookingSimHud.FontBody, CookingSimHud.Gold,
                FontStyle.Normal, 480f, 32f);
            bpmLabel = CookingSimHud.EnsureLabel(root, "BpmLabel", "BPM: 120",
                new Vector2(40f, -250f), TextAnchor.UpperLeft, CookingSimHud.FontRow, CookingSimHud.Cream,
                FontStyle.Normal, 480f, 30f);
            CookingSimHud.EnsureLabel(root, "BackControlsHint", LocalTwoPlayerBindings.HudBackHint,
                new Vector2(40f, -288f), TextAnchor.UpperLeft, CookingSimHud.FontHint, CookingSimHud.Cream,
                FontStyle.Normal, 500f, 56f);
            CookingSimHud.EnsureLabel(root, "BackQuestPrompt", "◆ C1 クエスト  Qキー",
                new Vector2(40f, -350f), TextAnchor.UpperLeft, CookingSimHud.FontRow, CookingSimHud.GoldBright,
                FontStyle.Bold, 480f, 32f);

            // Front service board (fullscreen when 表)
            CookingSimHud.EnsurePanel(root, "FrontPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -120f), new Vector2(520f, 380f), CookingSimHud.PanelBg);
            CookingSimHud.EnsureLabel(root, "FrontTitle", "【サービス】表 · ダイナーフロア",
                new Vector2(-40f, -130f), TextAnchor.UpperRight, CookingSimHud.FontHeader, CookingSimHud.Gold,
                FontStyle.Bold, 480f, 34f);
            prep = CookingSimHud.EnsureLabel(root, "PrepLabel", "仕込み: 0",
                new Vector2(-40f, -172f), TextAnchor.UpperRight, 28, CookingSimHud.White, FontStyle.Bold, 460f, 36f);
            serve = CookingSimHud.EnsureLabel(root, "ServeLabel", "提供: 0",
                new Vector2(-40f, -210f), TextAnchor.UpperRight, 28, CookingSimHud.Cream, FontStyle.Bold, 460f, 36f);
            prepFill = CookingSimHud.EnsureLabeledBar(root, "PrepBar", "仕込みゲージ",
                new Vector2(-40f, -252f), TextAnchor.UpperRight, CookingSimHud.Gold, 440f, 24f);
            serveFill = CookingSimHud.EnsureLabeledBar(root, "ServeBar", "提供ゲージ",
                new Vector2(-40f, -300f), TextAnchor.UpperRight, CookingSimHud.Teal, 440f, 24f);
            cookFeedback = CookingSimHud.EnsureLabel(root, "CookFeedback", LocalTwoPlayerBindings.HudFrontCookHint,
                new Vector2(-40f, -352f), TextAnchor.UpperRight, CookingSimHud.FontRow, CookingSimHud.GoldBright,
                FontStyle.Bold, 480f, 34f);
            CookingSimHud.EnsureLabel(root, "ServeRhythmHint", LocalTwoPlayerBindings.HudFrontServeHint,
                new Vector2(-40f, -386f), TextAnchor.UpperRight, CookingSimHud.FontHint, CookingSimHud.Cream,
                FontStyle.Normal, 480f, 30f);
            CookingSimHud.EnsureLabel(root, "FrontBeatHint", LocalTwoPlayerBindings.HudFrontBeatHint,
                new Vector2(-40f, -416f), TextAnchor.UpperRight, CookingSimHud.FontHint, CookingSimHud.Gold,
                FontStyle.Normal, 480f, 28f);
            CookingSimHud.EnsureLabel(root, "FrontSnapHint", "◆ C2 スナップ  Tab長押し",
                new Vector2(-40f, -446f), TextAnchor.UpperRight, CookingSimHud.FontRow, CookingSimHud.GoldBright,
                FontStyle.Bold, 480f, 30f);

            // Center session ticket (shared)
            CookingSimHud.EnsurePanel(root, "SessionPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(460f, 110f), CookingSimHud.PanelBg);
            timer = CookingSimHud.EnsureLabel(root, "TimerLabel", "残り 30.0s / ビート 0/32",
                new Vector2(0f, -144f), TextAnchor.UpperCenter, CookingSimHud.FontBody, CookingSimHud.White,
                FontStyle.Bold, 440f, 34f);
            sessionFill = CookingSimHud.EnsureLabeledBar(root, "SessionBar", "セッション進行",
                new Vector2(0f, -184f), TextAnchor.UpperCenter, CookingSimHud.Teal, 400f, 22f);

            // Big serve rhythm meter (Front only)
            CookingSimHud.EnsurePanel(root, "ServeMeterPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -520f), new Vector2(520f, 88f), CookingSimHud.PanelBgSoft);
            serveMeter = CookingSimHud.EnsureLabeledBar(root, "ServeRhythmMeter", "◆ 配膳リズムメーター  J·K",
                new Vector2(-40f, -534f), TextAnchor.UpperRight, CookingSimHud.GoldBright, 480f, 32f, CookingSimHud.FontRow);
            serveMeter.fillAmount = 0f;

            // Inventory slots row (Front)
            var rowGo = root.Find("InventoryRow");
            if (rowGo == null)
            {
                var go = new GameObject("InventoryRow");
                go.transform.SetParent(root, false);
                invRow = go.AddComponent<RectTransform>();
            }
            else
            {
                invRow = rowGo.GetComponent<RectTransform>() ?? rowGo.gameObject.AddComponent<RectTransform>();
            }

            invRow.anchorMin = new Vector2(1f, 1f);
            invRow.anchorMax = new Vector2(1f, 1f);
            invRow.pivot = new Vector2(1f, 1f);
            invRow.anchoredPosition = new Vector2(-28f, -630f);
            invRow.sizeDelta = new Vector2(700f, 96f);

            // Bottom help strip
            var helpPanel = CookingSimHud.EnsurePanel(root, "HelpPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1840f, 64f), CookingSimHud.PanelBg);
            _helpLabel = CookingSimHud.EnsureChildLabel(helpPanel.transform, "HelpLabel",
                LocalTwoPlayerBindings.HudHelp, CookingSimHud.FontHint, CookingSimHud.Cream);
            _helpLabel.alignment = TextAnchor.MiddleCenter;
            var helpRt = _helpLabel.rectTransform;
            helpRt.anchorMin = Vector2.zero;
            helpRt.anchorMax = Vector2.one;
            helpRt.offsetMin = new Vector2(16f, 6f);
            helpRt.offsetMax = new Vector2(-16f, -6f);

            resultRoot = root.Find("ResultOverlay")?.gameObject;
            if (resultRoot == null)
            {
                resultRoot = CookingSimHud.EnsurePanel(root, "ResultOverlay", new Vector2(0f, 0f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                    new Color(0.02f, 0.04f, 0.06f, 0.94f), goldBorder: false);
                var rt = resultRoot.GetComponent<RectTransform>();
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                var dim = resultRoot.GetComponent<Image>();
                if (dim != null)
                {
                    dim.color = new Color(0.02f, 0.04f, 0.06f, 0.94f);
                }
            }

            resultRoot.transform.SetAsLastSibling();
            ResultOverlayController.BuildPolishedUi(
                resultRoot,
                out resultTitle,
                out resultBody,
                out retryButton,
                out titleButton,
                out resultSalesValue,
                out resultServesValue,
                out resultCountdown);
            resultRoot.SetActive(false);
        }

        private GameObject CreatePrim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            ApplyMat(go, color);
            return go;
        }

        private static GameObject CreatePrimStatic(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = CreateRuntimeMat(color, lit: true);
            return go;
        }

        private void ApplyMat(GameObject go, Color color)
        {
            Material mat = null;
            if (Approximately(color, Gold) && goldMaterial != null)
            {
                mat = goldMaterial;
            }
            else if (Approximately(color, Teal) && tealMaterial != null)
            {
                mat = tealMaterial;
            }
            else if (Approximately(color, Navy) && navyMaterial != null)
            {
                mat = navyMaterial;
            }

            var metallic = IsStainlessTone(color);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat != null
                ? mat
                : CreateRuntimeMat(color, lit: true, smoothness: metallic ? 0.82f : 0.35f, metallic: metallic ? 0.65f : 0f);
        }

        private static bool IsStainlessTone(Color color)
        {
            return Approximately(color, Stainless)
                   || Approximately(color, StainlessDark)
                   || Approximately(color, CastIron);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f
                   && Mathf.Abs(a.g - b.g) < 0.02f
                   && Mathf.Abs(a.b - b.b) < 0.02f;
        }

        private static Material CreateRuntimeMat(Color color, bool lit, float smoothness = 0.35f, float metallic = 0f)
        {
            Shader shader = null;
            if (lit)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            shader ??= Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Unlit/Color")
                       ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
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
