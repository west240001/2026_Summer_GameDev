using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// End-of-day result overlay: 営業終了 / 売上仮点数 / 仕込み・提供サマリ,
    /// with もう一度 (reload VerticalSlice) and タイトルへ (TitleMenu).
    /// Enter = retry, Esc = title (optional shortcuts).
    /// </summary>
    public sealed class ResultOverlayController : MonoBehaviour
    {
        public static readonly Color Gold = DaySessionState.Gold;
        public static readonly Color GoldBright = new Color(0.93f, 0.78f, 0.32f, 1f);
        public static readonly Color Teal = DaySessionState.Teal;
        public static readonly Color Navy = new Color(0.05490196f, 0.12156863f, 0.2f);

        private const string VerticalSliceScene = "VerticalSlicePrototype";
        private const string TitleMenuScene = "TitleMenu";

        [SerializeField] private string retrySceneName = VerticalSliceScene;
        [SerializeField] private string titleSceneName = TitleMenuScene;

        private DaySessionState _session;
        private GameObject _resultRoot;
        private bool _active;
        private bool _navigating;

        public void Bind(DaySessionState session, GameObject resultRoot, Button retryButton, Button titleButton)
        {
            if (_session != null)
            {
                _session.OnSessionEnded -= HandleSessionEnded;
            }

            _session = session;
            _resultRoot = resultRoot;
            _active = false;
            _navigating = false;

            if (_session != null)
            {
                _session.OnSessionEnded += HandleSessionEnded;
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(RetryDay);
                retryButton.onClick.AddListener(RetryDay);
            }

            if (titleButton != null)
            {
                titleButton.onClick.RemoveListener(GoToTitle);
                titleButton.onClick.AddListener(GoToTitle);
            }

            EnsureEventSystem();
            if (_resultRoot != null)
            {
                _resultRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_active || _navigating)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                RetryDay();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                GoToTitle();
            }
        }

        public void RetryDay()
        {
            if (_navigating)
            {
                return;
            }

            _navigating = true;
            var target = string.IsNullOrWhiteSpace(retrySceneName) ? VerticalSliceScene : retrySceneName.Trim();
            Debug.Log("[BACKBEAT DINER] もう一度 — loading " + target);
            SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        public void GoToTitle()
        {
            if (_navigating)
            {
                return;
            }

            _navigating = true;
            var target = string.IsNullOrWhiteSpace(titleSceneName) ? TitleMenuScene : titleSceneName.Trim();
            Debug.Log("[BACKBEAT DINER] タイトルへ — loading " + target);
            SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        private void HandleSessionEnded()
        {
            _active = true;
            if (_resultRoot != null && !_resultRoot.activeSelf)
            {
                _resultRoot.SetActive(true);
            }

            EnsureEventSystem();
            Debug.Log("[ResultOverlay] 営業終了 overlay armed — Enter=もう一度 Esc=タイトルへ");
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnSessionEnded -= HandleSessionEnded;
            }
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

        /// <summary>Build polished result card + buttons under an existing fullscreen dim root.</summary>
        public static void BuildPolishedUi(
            GameObject resultRoot,
            out Text resultTitle,
            out Text resultBody,
            out Button retryButton,
            out Button titleButton)
        {
            // Clear leftover children from an older minimal overlay so rebuild stays clean.
            for (var i = resultRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(resultRoot.transform.GetChild(i).gameObject);
            }

            var card = CreatePanel(
                resultRoot.transform,
                "ResultCard",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(720f, 520f),
                new Color(0.03f, 0.07f, 0.12f, 0.96f));

            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            CreatePanel(
                card.transform,
                "GoldRule",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 150f),
                new Vector2(180f, 3f),
                new Color(Gold.r, Gold.g, Gold.b, 0.9f));

            resultTitle = CreateLabel(card.transform, "ResultTitle", "営業終了",
                new Vector2(0f, 190f), 52, Gold, FontStyle.Bold);
            resultTitle.alignment = TextAnchor.MiddleCenter;
            resultTitle.rectTransform.sizeDelta = new Vector2(640f, 64f);

            resultBody = CreateLabel(card.transform, "ResultBody",
                "売上仮点数: 0\n\n仕込み・提供サマリ\n提供: 0\n残仕込み: 0",
                new Vector2(0f, 20f), 26, new Color(0.86f, 0.92f, 0.9f, 1f), FontStyle.Normal);
            resultBody.alignment = TextAnchor.UpperCenter;
            resultBody.rectTransform.sizeDelta = new Vector2(600f, 220f);
            resultBody.lineSpacing = 1.15f;

            retryButton = CreateActionButton(
                card.transform,
                "RetryButton",
                "もう一度",
                new Vector2(-150f, -170f),
                faceGold: true);

            titleButton = CreateActionButton(
                card.transform,
                "TitleButton",
                "タイトルへ",
                new Vector2(150f, -170f),
                faceGold: false);

            var hint = CreateLabel(card.transform, "ResultHint", "Enter = もう一度　/　Esc = タイトルへ",
                new Vector2(0f, -230f), 15, new Color(Teal.r, Teal.g, Teal.b, 0.85f), FontStyle.Normal);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.rectTransform.sizeDelta = new Vector2(560f, 28f);
        }

        private static Button CreateActionButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos,
            bool faceGold)
        {
            var face = faceGold ? Gold : Teal;
            var labelColor = faceGold ? Navy : new Color(0.94f, 0.96f, 0.95f, 1f);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(240f, 64f);

            var img = go.AddComponent<Image>();
            img.color = face;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.92f, 0.55f, faceGold ? 0.4f : 0.2f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = face;
            colors.highlightedColor = faceGold
                ? GoldBright
                : new Color(Teal.r * 1.25f, Teal.g * 1.25f, Teal.b * 1.15f, 1f);
            colors.pressedColor = Navy;
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.targetGraphic = img;

            var text = CreateLabel(go.transform, "Label", label, Vector2.zero, 26, labelColor, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            var textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return button;
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
