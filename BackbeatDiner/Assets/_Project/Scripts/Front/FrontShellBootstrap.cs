using BackbeatDiner.Coop;
using BackbeatDiner.Core;
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
            var beatLexicon = systems.AddComponent<BeatLexiconDemo>();
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
            // 表: Beat-F1..F4 日本語パッド + 8s / 同一モチーフ 20s CD → HUD CD バー
            beatLexicon.Configure(BeatRole.Front, cd, cdFill, createPadUi: true);
            hud.Configure(inventory, queue, beatLexicon);

            _ = canvas;
            Debug.Log("[BACKBEAT DINER] Week2 FrontShellPrototype ready — 客待ち/在庫/クォータ/Beat CD(8s), 1-4=Beat-F1..F4, B=シミュレート受信.");
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
            var canvas = CookingSimHud.EnsureOverlayCanvas("FrontHUD", 10);
            var root = canvas.transform;

            var shellPanel = CookingSimHud.EnsurePanel(root, "FrontShellPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(1760f, 210f), CookingSimHud.PanelBg);
            _ = shellPanel;

            waiting = CookingSimHud.EnsureLabel(root, "WaitingLabel", "客待ち: 0",
                new Vector2(36f, -36f), TextAnchor.UpperLeft, CookingSimHud.FontTitle, CookingSimHud.White, FontStyle.Bold, 480f, 40f);
            inventory = CookingSimHud.EnsureLabel(root, "InventoryLabel", "在庫: —",
                new Vector2(36f, -84f), TextAnchor.UpperLeft, CookingSimHud.FontBody, CookingSimHud.Cream, FontStyle.Normal, 480f, 34f);
            quota = CookingSimHud.EnsureLabel(root, "QuotaLabel", "クォータ: 0/8",
                new Vector2(-36f, -36f), TextAnchor.UpperRight, CookingSimHud.FontHeader, CookingSimHud.Gold, FontStyle.Bold, 420f, 36f);
            cooldown = CookingSimHud.EnsureLabel(root, "CooldownLabel", "CD: —",
                new Vector2(-36f, -80f), TextAnchor.UpperRight, CookingSimHud.FontBody, CookingSimHud.Cream, FontStyle.Normal, 420f, 32f);

            quotaFill = CookingSimHud.EnsureLabeledBar(root, "QuotaBar", "クォータ",
                new Vector2(-36f, -120f), TextAnchor.UpperRight, CookingSimHud.Gold, 360f, 22f);
            cooldownFill = CookingSimHud.EnsureLabeledBar(root, "CooldownBar", "ビート CD",
                new Vector2(-36f, -168f), TextAnchor.UpperRight, CookingSimHud.Teal, 360f, 22f);

            var helpPanel = CookingSimHud.EnsurePanel(root, "HelpPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1600f, 56f), CookingSimHud.PanelBg);
            var help = CookingSimHud.EnsureChildLabel(helpPanel.transform, "HelpLabel",
                "1–4 = Beat-F（ビジー/大口/在庫赤/ラスト客）　B = 裏ビート受信シミュレート　CD 8s / 同一20s",
                CookingSimHud.FontHint, CookingSimHud.Cream);
            help.alignment = TextAnchor.MiddleCenter;
            var helpRt = help.rectTransform;
            helpRt.anchorMin = Vector2.zero;
            helpRt.anchorMax = Vector2.one;
            helpRt.offsetMin = new Vector2(12f, 4f);
            helpRt.offsetMax = new Vector2(-12f, -4f);

            var rowGo = root.Find("InventoryRow");
            if (rowGo == null)
            {
                var go = new GameObject("InventoryRow");
                go.transform.SetParent(root, false);
                inventoryRow = go.AddComponent<RectTransform>();
            }
            else
            {
                inventoryRow = rowGo.GetComponent<RectTransform>() ?? rowGo.gameObject.AddComponent<RectTransform>();
            }

            inventoryRow.anchorMin = new Vector2(0.5f, 1f);
            inventoryRow.anchorMax = new Vector2(0.5f, 1f);
            inventoryRow.pivot = new Vector2(0.5f, 1f);
            inventoryRow.anchoredPosition = new Vector2(0f, -250f);
            inventoryRow.sizeDelta = new Vector2(720f, 100f);

            return canvas;
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
