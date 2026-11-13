using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Title menu: brand logo, catchcopy, and 営業開始 → VerticalSlicePrototype (day start).
    /// Gold / teal UI on navy backdrop.
    /// </summary>
    public sealed class TitleMenuController : MonoBehaviour
    {
        public static readonly Color Gold = new Color(0.7882353f, 0.6352941f, 0.15294118f); // #C9A227
        public static readonly Color Teal = new Color(0.101960784f, 0.419607843f, 0.419607843f); // #1A6B6B
        public static readonly Color Navy = new Color(0.05490196f, 0.12156863f, 0.2f); // #0E1F33

        private const string VerticalSliceScene = "VerticalSlicePrototype";
        private const string Catchcopy = "見えない相棒と、最高の一皿を。";

        [SerializeField] private string playSceneName = VerticalSliceScene;

        private void Start()
        {
            EnsureCamera();
            EnsureEventSystem();
            BuildUi();
            Debug.Log("[BACKBEAT DINER] TitleMenu ready — 営業開始 → " + playSceneName);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                StartDay();
            }
        }

        public void StartDay()
        {
            var target = string.IsNullOrWhiteSpace(playSceneName) ? VerticalSliceScene : playSceneName.Trim();
            Debug.Log("[BACKBEAT DINER] 営業開始 — loading " + target);
            SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        private static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                go.tag = "MainCamera";
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Navy;
            cam.transform.position = new Vector3(0f, 1.2f, -6f);
            cam.transform.rotation = Quaternion.identity;
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

        private void BuildUi()
        {
            if (GameObject.Find("TitleMenuHUD") != null)
            {
                return;
            }

            var canvasGo = new GameObject("TitleMenuHUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Full-bleed navy panel
            var bg = CreatePanel(
                canvasGo.transform,
                "Backdrop",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(Navy.r, Navy.g, Navy.b, 1f));
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Soft teal accent band behind logo
            CreatePanel(
                canvasGo.transform,
                "AccentBand",
                new Vector2(0.5f, 0.55f),
                new Vector2(0.5f, 0.55f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f),
                new Vector2(920f, 220f),
                new Color(Teal.r, Teal.g, Teal.b, 0.22f));

            var logo = CreateLabel(canvasGo.transform, "BrandLogo", "BACKBEAT DINER",
                new Vector2(0f, 120f), 64, Gold, FontStyle.Bold);
            logo.alignment = TextAnchor.MiddleCenter;
            logo.rectTransform.sizeDelta = new Vector2(1100f, 80f);

            var sub = CreateLabel(canvasGo.transform, "BrandSub", "バックビート・ダイナー",
                new Vector2(0f, 52f), 22, Teal, FontStyle.Normal);
            sub.alignment = TextAnchor.MiddleCenter;
            sub.rectTransform.sizeDelta = new Vector2(720f, 36f);

            var catchcopy = CreateLabel(canvasGo.transform, "Catchcopy", Catchcopy,
                new Vector2(0f, -20f), 28, Gold, FontStyle.Normal);
            catchcopy.alignment = TextAnchor.MiddleCenter;
            catchcopy.rectTransform.sizeDelta = new Vector2(900f, 48f);

            // Gold CTA button
            var buttonGo = new GameObject("StartDayButton");
            buttonGo.transform.SetParent(canvasGo.transform, false);
            var buttonRt = buttonGo.AddComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRt.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRt.pivot = new Vector2(0.5f, 0.5f);
            buttonRt.anchoredPosition = new Vector2(0f, -140f);
            buttonRt.sizeDelta = new Vector2(360f, 72f);

            var buttonImg = buttonGo.AddComponent<Image>();
            buttonImg.color = Gold;

            var button = buttonGo.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Gold;
            colors.highlightedColor = new Color(0.9f, 0.75f, 0.28f, 1f);
            colors.pressedColor = Teal;
            colors.selectedColor = Gold;
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            button.colors = colors;
            button.targetGraphic = buttonImg;
            button.onClick.AddListener(StartDay);

            var label = CreateLabel(buttonGo.transform, "Label", "営業開始", Vector2.zero, 28, Navy, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var hint = CreateLabel(canvasGo.transform, "Hint", "Enter / Space — 営業開始",
                new Vector2(0f, -220f), 16, Teal, FontStyle.Normal);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.rectTransform.sizeDelta = new Vector2(480f, 28f);
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            // Overload for stretch vs fixed — size zero + stretch anchors = full bleed handled by caller offsets
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            string content,
            Vector2 anchoredPos,
            int fontSize,
            Color color,
            FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400f, 40f);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }
    }
}
