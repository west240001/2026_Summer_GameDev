using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Title menu: full-bleed art, brand logo, catchcopy, gold CTA → VerticalSlicePrototype.
    /// </summary>
    public sealed class TitleMenuController : MonoBehaviour
    {
        public static readonly Color Gold = new Color(0.7882353f, 0.6352941f, 0.15294118f); // #C9A227
        public static readonly Color GoldBright = new Color(0.93f, 0.78f, 0.32f, 1f);
        public static readonly Color Teal = new Color(0.101960784f, 0.419607843f, 0.419607843f); // #1A6B6B
        public static readonly Color Navy = new Color(0.05490196f, 0.12156863f, 0.2f); // #0E1F33
        public static readonly Color Cream = new Color(0.94f, 0.90f, 0.82f, 0.92f);

        private const string VerticalSliceScene = "VerticalSlicePrototype";
        private const string CatchcopyPrimary = "見えない相棒と、最高の一皿を。";
        private const string CatchcopySecondary = "音で支える、非対称協力レストランゲーム。";
        private const string CreditsLine = "BACKBEAT DINER  ·  prototype build";

        [SerializeField] private string playSceneName = VerticalSliceScene;
        [SerializeField] private Sprite titleBackground;

        private Image _ctaImage;
        private RectTransform _ctaRt;
        private Vector2 _ctaBaseSize;
        private bool _ctaHovered;

        private void Start()
        {
            EnsureCamera();
            EnsureEventSystem();
            ResolveBackgroundSprite();
            BuildUi();
            Debug.Log("[BACKBEAT DINER] TitleMenu ready — 営業開始 → " + playSceneName);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                StartDay();
            }

            AnimateCtaHover();
        }

        public void StartDay()
        {
            var target = string.IsNullOrWhiteSpace(playSceneName) ? VerticalSliceScene : playSceneName.Trim();
            Debug.Log("[BACKBEAT DINER] 営業開始 — loading " + target);
            SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        private void ResolveBackgroundSprite()
        {
            if (titleBackground != null)
            {
                return;
            }

            // Optional Resources fallback if the scene was not rebuilt with TitleMenuSceneBuilder.
            titleBackground = Resources.Load<Sprite>("TitleBackground");
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
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildFullBleedBackground(canvasGo.transform);
            BuildBottomGradient(canvasGo.transform);
            BuildVignetteEdges(canvasGo.transform);

            // Content stack anchored lower-center over the dark wash
            var stack = new GameObject("ContentStack");
            stack.transform.SetParent(canvasGo.transform, false);
            var stackRt = stack.AddComponent<RectTransform>();
            stackRt.anchorMin = new Vector2(0.5f, 0f);
            stackRt.anchorMax = new Vector2(0.5f, 0f);
            stackRt.pivot = new Vector2(0.5f, 0f);
            stackRt.anchoredPosition = new Vector2(0f, 96f);
            stackRt.sizeDelta = new Vector2(1100f, 520f);

            var brand = CreateLabel(stack.transform, "BrandLogo", "BACKBEAT DINER",
                new Vector2(0f, 390f), 78, Gold, FontStyle.Bold);
            brand.alignment = TextAnchor.MiddleCenter;
            brand.horizontalOverflow = HorizontalWrapMode.Overflow;
            brand.rectTransform.sizeDelta = new Vector2(1100f, 96f);

            var rule = CreatePanel(
                stack.transform,
                "GoldRule",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 332f),
                new Vector2(220f, 3f),
                new Color(Gold.r, Gold.g, Gold.b, 0.85f));
            rule.GetComponent<Image>().raycastTarget = false;

            var sub = CreateLabel(stack.transform, "BrandSub", "バックビート・ダイナー",
                new Vector2(0f, 292f), 22, new Color(Teal.r, Teal.g, Teal.b, 0.95f), FontStyle.Normal);
            sub.alignment = TextAnchor.MiddleCenter;
            sub.rectTransform.sizeDelta = new Vector2(720f, 34f);

            var catch1 = CreateLabel(stack.transform, "CatchcopyPrimary", CatchcopyPrimary,
                new Vector2(0f, 230f), 30, Cream, FontStyle.Normal);
            catch1.alignment = TextAnchor.MiddleCenter;
            catch1.rectTransform.sizeDelta = new Vector2(980f, 44f);

            var catch2 = CreateLabel(stack.transform, "CatchcopySecondary", CatchcopySecondary,
                new Vector2(0f, 186f), 22, new Color(Cream.r, Cream.g, Cream.b, 0.72f), FontStyle.Normal);
            catch2.alignment = TextAnchor.MiddleCenter;
            catch2.rectTransform.sizeDelta = new Vector2(980f, 36f);

            BuildCtaButton(stack.transform);

            var hint = CreateLabel(stack.transform, "Hint", "Enter / Space",
                new Vector2(0f, 48f), 15, new Color(Teal.r, Teal.g, Teal.b, 0.75f), FontStyle.Normal);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.rectTransform.sizeDelta = new Vector2(360f, 24f);

            var credits = CreateLabel(canvasGo.transform, "Credits", CreditsLine,
                new Vector2(0f, 28f), 14, new Color(1f, 1f, 1f, 0.38f), FontStyle.Normal);
            credits.alignment = TextAnchor.MiddleCenter;
            var creditsRt = credits.rectTransform;
            creditsRt.anchorMin = new Vector2(0.5f, 0f);
            creditsRt.anchorMax = new Vector2(0.5f, 0f);
            creditsRt.pivot = new Vector2(0.5f, 0f);
            creditsRt.anchoredPosition = new Vector2(0f, 18f);
            creditsRt.sizeDelta = new Vector2(720f, 22f);
        }

        private void BuildFullBleedBackground(Transform parent)
        {
            var go = new GameObject("TitleBackground");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            if (titleBackground != null)
            {
                img.sprite = titleBackground;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = Color.white;
            }
            else
            {
                img.color = Navy;
                Debug.LogWarning("[TitleMenu] TitleBackground sprite missing — navy fallback.");
            }
        }

        private static void BuildBottomGradient(Transform parent)
        {
            var go = new GameObject("BottomGradient");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.62f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<RawImage>();
            img.texture = CreateVerticalGradientTexture(
                new Color(0.02f, 0.05f, 0.09f, 0f),
                new Color(0.02f, 0.04f, 0.07f, 0.92f));
            img.raycastTarget = false;
            img.color = Color.white;
        }

        private static void BuildVignetteEdges(Transform parent)
        {
            // Soft side wash so the brand stack reads clearly without flattening the art.
            CreatePanel(
                parent,
                "LeftWash",
                new Vector2(0f, 0f),
                new Vector2(0.22f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.05f, 0.09f, 0.28f));

            CreatePanel(
                parent,
                "RightWash",
                new Vector2(0.78f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.05f, 0.09f, 0.22f));

            CreatePanel(
                parent,
                "TopShade",
                new Vector2(0f, 0.82f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.04f, 0.07f, 0.35f));
        }

        private void BuildCtaButton(Transform parent)
        {
            var buttonGo = new GameObject("StartDayButton");
            buttonGo.transform.SetParent(parent, false);
            _ctaRt = buttonGo.AddComponent<RectTransform>();
            _ctaRt.anchorMin = new Vector2(0.5f, 0.5f);
            _ctaRt.anchorMax = new Vector2(0.5f, 0.5f);
            _ctaRt.pivot = new Vector2(0.5f, 0.5f);
            _ctaRt.anchoredPosition = new Vector2(0f, 110f);
            _ctaBaseSize = new Vector2(380f, 78f);
            _ctaRt.sizeDelta = _ctaBaseSize;

            // Soft gold glow plate behind the CTA
            var glow = CreatePanel(
                buttonGo.transform,
                "CtaGlow",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(420f, 100f),
                new Color(Gold.r, Gold.g, Gold.b, 0.18f));
            glow.transform.SetAsFirstSibling();

            _ctaImage = buttonGo.AddComponent<Image>();
            _ctaImage.color = Gold;

            var outline = buttonGo.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.92f, 0.55f, 0.45f);
            outline.effectDistance = new Vector2(2f, -2f);

            var button = buttonGo.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Gold;
            colors.highlightedColor = GoldBright;
            colors.pressedColor = Teal;
            colors.selectedColor = GoldBright;
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            button.targetGraphic = _ctaImage;
            button.onClick.AddListener(StartDay);

            var trigger = buttonGo.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => _ctaHovered = true);
            AddTrigger(trigger, EventTriggerType.PointerExit, _ => _ctaHovered = false);

            var label = CreateLabel(buttonGo.transform, "Label", "営業開始", Vector2.zero, 30, Navy, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
        }

        private void AnimateCtaHover()
        {
            if (_ctaRt == null)
            {
                return;
            }

            var target = _ctaHovered ? _ctaBaseSize * 1.045f : _ctaBaseSize;
            _ctaRt.sizeDelta = Vector2.Lerp(_ctaRt.sizeDelta, target, Time.unscaledDeltaTime * 12f);
            if (_ctaImage != null && !_ctaHovered)
            {
                // Gentle idle shimmer toward gold-bright
                var pulse = (Mathf.Sin(Time.unscaledTime * 2.2f) + 1f) * 0.5f;
                _ctaImage.color = Color.Lerp(Gold, GoldBright, pulse * 0.12f);
            }
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private static Texture2D CreateVerticalGradientTexture(Color top, Color bottom)
        {
            const int height = 64;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "TitleBottomGradient"
            };

            for (var y = 0; y < height; y++)
            {
                var t = y / (float)(height - 1);
                // y=0 is bottom of RawImage UV in Unity — keep dark at bottom.
                tex.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }

            tex.Apply(false, true);
            return tex;
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
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            if (Mathf.Approximately(size.x, 0f) && Mathf.Approximately(size.y, 0f) &&
                (anchorMin != anchorMax))
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

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
