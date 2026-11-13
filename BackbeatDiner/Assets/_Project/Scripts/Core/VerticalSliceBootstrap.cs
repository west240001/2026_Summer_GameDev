using BackbeatDiner.Coop;
using BackbeatDiner.Front;
using BackbeatDiner.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Vertical slice: left = back rhythm station, right = front low-poly diner.
    /// Shared <see cref="DaySessionState"/> links Perfect/Good → 仕込み and Instant Cook → F1+ 配膳リズム → 提供.
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

        [SerializeField] private AudioClip metronomeClick;
        [SerializeField] private ChartDefinition chart;
        [SerializeField] private float bpm = 120f;
        [SerializeField] private bool buildPlaceholders = true;
        [SerializeField] private Material goldMaterial;
        [SerializeField] private Material tealMaterial;
        [SerializeField] private Material navyMaterial;
        [SerializeField] private int customerCount = 2;

        private void Start()
        {
            ResolveMaterials();
            if (buildPlaceholders)
            {
                BuildWorlds();
            }

            EnsureLightsAndFog();
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

            if (chart == null)
            {
                chart = Resources.Load<ChartDefinition>("Charts/Chart_DemoA");
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
                out var retryButton, out var titleButton);

            inventory.BuildUi(invRow);
            // Local 2P: Back WASD+Space hits stay live during Front serve (J·K).
            hitInput.Configure(clock, highway, judgement, combo, bpmLabel, twoPlayerSplit: true);
            hitInput.Window = JudgeWindow.Default;
            session.Bind(clock, hitInput, prep, serve, timer, resultRoot, resultTitle, resultBody,
                sessionFill, prepFill, serveFill);
            var resultOverlay = systems.AddComponent<ResultOverlayController>();
            resultOverlay.Bind(session, resultRoot, retryButton, titleButton);
            serveRhythm.Configure(clock, session, hitInput, cook, cookFeedback, twoPlayerSplit: true, serveMeter);
            cook.Configure(session, queue, cookFeedback, serveRhythm, twoPlayerSplit: true);
            // 裏: A/S/D/F = Beat-B1..B4　表: ←↓→↑ = Beat-F1..F4（配膳中は表のみロック）
            backBeats.Configure(BeatRole.Back, createPadUi: true, padKeys: BeatPadKeyScheme.BackAsdf);
            frontBeats.Configure(BeatRole.Front, createPadUi: true, padKeys: BeatPadKeyScheme.FrontArrows);

            session.Begin();
            clock.StartClock(0.35);
            var chartLabel = chart != null ? chart.name : "(fallback every-beat)";
            Debug.Log($"[BACKBEAT DINER] VerticalSlicePrototype ready — chart={chartLabel}; kitchen BACK + diner FRONT; local 2P.");
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
            cam.rect = new Rect(0f, 0f, 0.5f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            if (go.GetComponent<AudioListener>() == null)
            {
                go.AddComponent<AudioListener>();
            }

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

            var listener = cam.GetComponent<AudioListener>();
            if (listener != null)
            {
                Object.Destroy(listener);
            }

            cam.transform.position = new Vector3(6f, 3.25f, -5.4f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Navy;
            cam.depth = 1;
            cam.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
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
            out Text resultBody,
            out Image sessionFill,
            out Image prepFill,
            out Image serveFill,
            out Image serveMeter,
            out Button retryButton,
            out Button titleButton)
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

            // Center brand logo
            var logo = EnsureLabel(canvasGo.transform, "BrandLogo", "BACKBEAT DINER", new Vector2(0f, -10f),
                TextAnchor.UpperCenter, 30, Gold);
            logo.fontStyle = FontStyle.Bold;
            logo.rectTransform.sizeDelta = new Vector2(640f, 40f);
            EnsureLabel(canvasGo.transform, "BrandSub", "バックビート・ダイナー", new Vector2(0f, -42f),
                TextAnchor.UpperCenter, 14, Teal).rectTransform.sizeDelta = new Vector2(480f, 24f);

            EnsurePanel(canvasGo.transform, "SplitDivider", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 0f), new Color(Gold.r, Gold.g, Gold.b, 0.65f));

            // Left / right chrome panels
            EnsurePanel(canvasGo.transform, "BackPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -70f), new Vector2(430f, 210f), new Color(0.03f, 0.07f, 0.1f, 0.72f));
            EnsurePanel(canvasGo.transform, "FrontPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -70f), new Vector2(450f, 250f), new Color(0.03f, 0.07f, 0.1f, 0.72f));
            EnsurePanel(canvasGo.transform, "SessionPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(420f, 86f), new Color(0.03f, 0.07f, 0.1f, 0.75f));

            EnsureLabel(canvasGo.transform, "BackTitle", "裏 · リズムステーション", new Vector2(28f, -78f), TextAnchor.UpperLeft, 20, Gold);
            EnsureLabel(canvasGo.transform, "FrontTitle", "表 · ダイナーフロア", new Vector2(-28f, -78f), TextAnchor.UpperRight, 20, Gold);

            judgement = EnsureLabel(canvasGo.transform, "JudgementLabel", "判定: —", new Vector2(28f, -112f), TextAnchor.UpperLeft, 24, Gold);
            combo = EnsureLabel(canvasGo.transform, "ComboLabel", "コンボ: 0", new Vector2(28f, -148f), TextAnchor.UpperLeft, 20, Gold);
            bpmLabel = EnsureLabel(canvasGo.transform, "BpmLabel", "BPM: 120", new Vector2(28f, -180f), TextAnchor.UpperLeft, 18, Teal);
            SetLabelText(EnsureLabel(canvasGo.transform, "BackControlsHint", LocalTwoPlayerBindings.HudBackHint,
                new Vector2(28f, -212f), TextAnchor.UpperLeft, 14, Teal), LocalTwoPlayerBindings.HudBackHint);

            prep = EnsureLabel(canvasGo.transform, "PrepLabel", "仕込み: 0", new Vector2(-28f, -112f), TextAnchor.UpperRight, 24, Gold);
            serve = EnsureLabel(canvasGo.transform, "ServeLabel", "提供: 0", new Vector2(-28f, -146f), TextAnchor.UpperRight, 24, Teal);
            timer = EnsureLabel(canvasGo.transform, "TimerLabel", "残り 30.0s / ビート 0/32", new Vector2(0f, -78f), TextAnchor.UpperCenter, 18, Gold);
            cookFeedback = EnsureLabel(canvasGo.transform, "CookFeedback", LocalTwoPlayerBindings.HudFrontCookHint,
                new Vector2(-28f, -210f), TextAnchor.UpperRight, 15, Gold);
            SetLabelText(cookFeedback, LocalTwoPlayerBindings.HudFrontCookHint);

            SetLabelText(EnsureLabel(canvasGo.transform, "ServeRhythmHint",
                LocalTwoPlayerBindings.HudFrontServeHint,
                new Vector2(-28f, -238f), TextAnchor.UpperRight, 14, Teal), LocalTwoPlayerBindings.HudFrontServeHint);

            SetLabelText(EnsureLabel(canvasGo.transform, "FrontBeatHint",
                LocalTwoPlayerBindings.HudFrontBeatHint,
                new Vector2(-28f, -264f), TextAnchor.UpperRight, 13, Gold), LocalTwoPlayerBindings.HudFrontBeatHint);

            sessionFill = EnsureBar(canvasGo.transform, "SessionBar", new Vector2(0f, -112f), TextAnchor.UpperCenter, Teal, 360f);
            prepFill = EnsureBar(canvasGo.transform, "PrepBar", new Vector2(-28f, -178f), TextAnchor.UpperRight, Gold, 240f);
            serveFill = EnsureBar(canvasGo.transform, "ServeBar", new Vector2(-28f, -198f), TextAnchor.UpperRight, Teal, 240f);
            serveMeter = EnsureBar(canvasGo.transform, "ServeRhythmMeter", new Vector2(-28f, -292f), TextAnchor.UpperRight, Gold, 280f);
            serveMeter.fillAmount = 0f;

            var helpPanel = EnsurePanel(canvasGo.transform, "HelpPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1760f, 52f), new Color(0.03f, 0.07f, 0.1f, 0.8f));
            var help = EnsureLabel(helpPanel.transform, "HelpLabel",
                LocalTwoPlayerBindings.HudHelp,
                new Vector2(0f, 0f), TextAnchor.MiddleCenter, 14, Gold);
            SetLabelText(help, LocalTwoPlayerBindings.HudHelp);
            help.rectTransform.anchorMin = Vector2.zero;
            help.rectTransform.anchorMax = Vector2.one;
            help.rectTransform.offsetMin = new Vector2(12f, 4f);
            help.rectTransform.offsetMax = new Vector2(-12f, -4f);

            var rowGo = canvasGo.transform.Find("InventoryRow");
            if (rowGo == null)
            {
                var go = new GameObject("InventoryRow");
                go.transform.SetParent(canvasGo.transform, false);
                invRow = go.AddComponent<RectTransform>();
                invRow.anchorMin = new Vector2(0.78f, 1f);
                invRow.anchorMax = new Vector2(0.78f, 1f);
                invRow.pivot = new Vector2(0.5f, 1f);
                invRow.anchoredPosition = new Vector2(0f, -320f);
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
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.08f, 0.92f));
                var rt = resultRoot.GetComponent<RectTransform>();
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                var dim = resultRoot.GetComponent<Image>();
                if (dim != null)
                {
                    dim.color = new Color(0.02f, 0.05f, 0.08f, 0.92f);
                }
            }

            // Raise above gameplay HUD chrome so buttons stay clickable.
            resultRoot.transform.SetAsLastSibling();
            ResultOverlayController.BuildPolishedUi(resultRoot, out resultTitle, out resultBody, out retryButton, out titleButton);
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

        private static Image EnsureBar(Transform parent, string name, Vector2 anchoredPos, TextAnchor corner, Color color, float width)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var fillExisting = existing.Find("Fill");
                if (fillExisting != null)
                {
                    return fillExisting.GetComponent<Image>();
                }
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.1f, 0.12f, 0.85f);
            var rt = bg.rectTransform;
            switch (corner)
            {
                case TextAnchor.UpperRight:
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    break;
                case TextAnchor.UpperCenter:
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    break;
                default:
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    break;
            }

            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, 14f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.AddComponent<Image>();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.35f;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            return fill;
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
