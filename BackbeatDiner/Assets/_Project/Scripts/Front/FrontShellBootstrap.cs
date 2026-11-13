using BackbeatDiner.Coop;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Front
{
    /// <summary>
    /// Builds Week2 asymmetric front HUD shell at runtime (diner floor, dummy guests, Japanese HUD).
    /// </summary>
    public sealed class FrontShellBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildPlaceholders = true;
        [SerializeField] private Material goldMaterial;
        [SerializeField] private Material tealMaterial;
        [SerializeField] private int customerCount = 2;

        private void Start()
        {
            ResolveMaterials();
            if (buildPlaceholders)
            {
                BuildWorld();
            }

            EnsureCamera();
            EnsureLight();

            var systems = new GameObject("FrontShellSystems");
            var queue = systems.AddComponent<DummyCustomerQueue>();
            var inventory = systems.AddComponent<InventoryBar>();
            var hud = systems.AddComponent<FrontHudController>();
            systems.AddComponent<BeatLexiconDemo>();
            systems.AddComponent<BeatReceivePulse>();

            var queueRoot = GameObject.Find("CustomerQueueRoot")?.transform;
            if (queueRoot == null)
            {
                var go = new GameObject("CustomerQueueRoot");
                queueRoot = go.transform;
            }

            queue.Configure(queueRoot, tealMaterial, goldMaterial, Mathf.Clamp(customerCount, 1, 3));

            var canvas = EnsureFrontHud(out var waiting, out var invLabel, out var quota, out var cd,
                out var quotaFill, out var cdFill, out var invRow);
            inventory.BuildUi(invRow);
            hud.BindLabels(waiting, invLabel, quota, cd, quotaFill, cdFill);
            hud.Configure(inventory, queue);

            _ = canvas;
            Debug.Log("[BACKBEAT DINER] Week2 FrontShellPrototype ready — 客待ち/在庫/クォータ/CD, 1-4=ビート, B=シミュレート受信.");
        }

        private void ResolveMaterials()
        {
            // Prefer scene-assigned BrandGold / BrandTeal assets; runtime tint keeps Play Mode usable.
            if (goldMaterial == null)
            {
                goldMaterial = CreateRuntimeMat(FrontHudController.Gold);
            }

            if (tealMaterial == null)
            {
                tealMaterial = CreateRuntimeMat(FrontHudController.Teal);
            }
        }

        private void BuildWorld()
        {
            if (GameObject.Find("DinerFloor") != null)
            {
                return;
            }

            var root = new GameObject("FrontPlaceholders");
            CreateCube("DinerFloor", root.transform, new Vector3(0f, -0.05f, 1.2f), new Vector3(7f, 0.1f, 5f), tealMaterial, FrontHudController.Teal * 0.55f);
            CreateCube("ServiceCounter", root.transform, new Vector3(0f, 0.45f, -0.4f), new Vector3(3.6f, 0.9f, 0.9f), tealMaterial, FrontHudController.Teal);
            CreateCube("PassWindow", root.transform, new Vector3(0f, 1.35f, -0.85f), new Vector3(1.6f, 0.7f, 0.12f), goldMaterial, FrontHudController.Gold);
            CreateCube("BackWall", root.transform, new Vector3(0f, 1.5f, 3.2f), new Vector3(7f, 3f, 0.15f), tealMaterial, FrontHudController.Teal * 1.1f);

            if (GameObject.Find("CustomerQueueRoot") == null)
            {
                new GameObject("CustomerQueueRoot");
            }
        }

        private static Canvas EnsureFrontHud(
            out Text waiting,
            out Text inventory,
            out Text quota,
            out Text cooldown,
            out Image quotaFill,
            out Image cooldownFill,
            out RectTransform inventoryRow)
        {
            var canvasGo = GameObject.Find("FrontHUD");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("FrontHUD");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            waiting = EnsureLabel(canvasGo.transform, "WaitingLabel", "客待ち: 0", new Vector2(24f, -24f), TextAnchor.UpperLeft, 26);
            inventory = EnsureLabel(canvasGo.transform, "InventoryLabel", "在庫: —", new Vector2(24f, -64f), TextAnchor.UpperLeft, 22);
            quota = EnsureLabel(canvasGo.transform, "QuotaLabel", "クォータ: 0/8", new Vector2(-24f, -24f), TextAnchor.UpperRight, 24);
            cooldown = EnsureLabel(canvasGo.transform, "CooldownLabel", "CD: —", new Vector2(-24f, -64f), TextAnchor.UpperRight, 22);
            EnsureLabel(canvasGo.transform, "HelpLabel", "1–4 = ビート送信　B = 裏ビート受信シミュレート", new Vector2(0f, 28f), TextAnchor.LowerCenter, 20);

            quotaFill = EnsureBar(canvasGo.transform, "QuotaBar", new Vector2(-24f, -100f), TextAnchor.UpperRight, FrontHudController.Gold);
            cooldownFill = EnsureBar(canvasGo.transform, "CooldownBar", new Vector2(-24f, -130f), TextAnchor.UpperRight, FrontHudController.Teal);

            var rowGo = canvasGo.transform.Find("InventoryRow");
            if (rowGo == null)
            {
                var go = new GameObject("InventoryRow");
                go.transform.SetParent(canvasGo.transform, false);
                inventoryRow = go.AddComponent<RectTransform>();
                inventoryRow.anchorMin = new Vector2(0.5f, 1f);
                inventoryRow.anchorMax = new Vector2(0.5f, 1f);
                inventoryRow.pivot = new Vector2(0.5f, 1f);
                inventoryRow.anchoredPosition = new Vector2(0f, -160f);
                inventoryRow.sizeDelta = new Vector2(640f, 72f);
            }
            else
            {
                inventoryRow = rowGo.GetComponent<RectTransform>();
            }

            return canvasGo.GetComponent<Canvas>();
        }

        private static Text EnsureLabel(Transform parent, string name, string text, Vector2 anchoredPos, TextAnchor corner, int fontSize)
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
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                         ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = FrontHudController.Gold;
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
                case TextAnchor.LowerCenter:
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    break;
                default:
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    break;
            }

            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(520f, 40f);
            return label;
        }

        private static Image EnsureBar(Transform parent, string name, Vector2 anchoredPos, TextAnchor corner, Color color)
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
            bg.color = new Color(0.05f, 0.1f, 0.1f, 0.8f);
            var rt = bg.rectTransform;
            if (corner == TextAnchor.UpperRight)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
            }

            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(220f, 16f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.AddComponent<Image>();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.4f;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            return fill;
        }

        private static void EnsureCamera()
        {
            Camera cam;
            if (Camera.main != null)
            {
                cam = Camera.main;
            }
            else
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.transform.position = new Vector3(0f, 3.1f, -5.2f);
            cam.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.1f, 0.1f);
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
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -25f, 0f);
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Color fallback)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }
            else
            {
                renderer.sharedMaterial = CreateRuntimeMat(fallback);
            }

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
    }
}
