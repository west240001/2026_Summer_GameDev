using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// End-of-day result overlay (Cooking-sim JP): 営業終了 with clear 売上 / 提供,
    /// then return to TitleMenu (Esc / タイトルへ / auto countdown). もう一度 reloads the slice.
    /// </summary>
    public sealed class ResultOverlayController : MonoBehaviour
    {
        public static readonly Color Gold = DaySessionState.Gold;
        public static readonly Color GoldBright = new Color(0.93f, 0.78f, 0.32f, 1f);
        public static readonly Color Teal = DaySessionState.Teal;
        public static readonly Color Navy = new Color(0.05490196f, 0.12156863f, 0.2f);

        private const string VerticalSliceScene = "VerticalSlicePrototype";
        private const string TitleMenuScene = "TitleMenu";
        private const float AutoTitleSeconds = 8f;

        [SerializeField] private string retrySceneName = VerticalSliceScene;
        [SerializeField] private string titleSceneName = TitleMenuScene;
        [SerializeField] private float autoReturnSeconds = AutoTitleSeconds;

        private DaySessionState _session;
        private GameObject _resultRoot;
        private Text _salesValue;
        private Text _servesValue;
        private Text _detailLabel;
        private Text _countdownLabel;
        private bool _active;
        private bool _navigating;
        private Coroutine _autoReturnRoutine;

        public void Bind(
            DaySessionState session,
            GameObject resultRoot,
            Button retryButton,
            Button titleButton,
            Text salesValue = null,
            Text servesValue = null,
            Text detailLabel = null,
            Text countdownLabel = null)
        {
            if (_session != null)
            {
                _session.OnSessionEnded -= HandleSessionEnded;
            }

            _session = session;
            _resultRoot = resultRoot;
            _salesValue = salesValue;
            _servesValue = servesValue;
            _detailLabel = detailLabel;
            _countdownLabel = countdownLabel;
            _active = false;
            _navigating = false;
            StopAutoReturn();

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
            StopAutoReturn();
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
            StopAutoReturn();
            var target = string.IsNullOrWhiteSpace(titleSceneName) ? TitleMenuScene : titleSceneName.Trim();
            Debug.Log("[BACKBEAT DINER] タイトルへ — loading " + target);
            SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        private void HandleSessionEnded()
        {
            _active = true;
            RefreshStatsFromSession();

            if (_resultRoot != null)
            {
                _resultRoot.SetActive(true);
                _resultRoot.transform.SetAsLastSibling();
            }

            EnsureEventSystem();
            StopAutoReturn();
            _autoReturnRoutine = StartCoroutine(AutoReturnToTitle());
            Debug.Log("[ResultOverlay] 営業終了 — 売上/提供表示 · Esc=タイトルへ Enter=もう一度");
        }

        private void RefreshStatsFromSession()
        {
            if (_session == null)
            {
                return;
            }

            if (_salesValue != null)
            {
                _salesValue.text = _session.SalesScore.ToString("N0");
            }

            if (_servesValue != null)
            {
                _servesValue.text = _session.ServedCount.ToString("N0");
            }

            if (_detailLabel != null)
            {
                var reason = string.IsNullOrWhiteSpace(_session.EndReason) ? "営業終了" : _session.EndReason;
                _detailLabel.text = $"残仕込み {_session.PrepUnits}　·　{reason}";
            }
        }

        private IEnumerator AutoReturnToTitle()
        {
            var remain = Mathf.Max(1f, autoReturnSeconds);
            while (remain > 0f && !_navigating)
            {
                if (_countdownLabel != null)
                {
                    _countdownLabel.text = $"{Mathf.CeilToInt(remain)}秒後にタイトルへ";
                }

                yield return null;
                remain -= Time.unscaledDeltaTime;
            }

            _autoReturnRoutine = null;
            if (!_navigating)
            {
                GoToTitle();
            }
        }

        private void StopAutoReturn()
        {
            if (_autoReturnRoutine == null)
            {
                return;
            }

            StopCoroutine(_autoReturnRoutine);
            _autoReturnRoutine = null;
        }

        private void OnDestroy()
        {
            StopAutoReturn();
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

        /// <summary>
        /// Build Cooking-sim result card: big 売上 / 提供, detail line, タイトルへ (primary) + もう一度.
        /// </summary>
        public static void BuildPolishedUi(
            GameObject resultRoot,
            out Text resultTitle,
            out Text resultBody,
            out Button retryButton,
            out Button titleButton,
            out Text salesValue,
            out Text servesValue,
            out Text countdownLabel)
        {
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
                new Vector2(820f, 600f),
                CookingSimHud.PanelBg);

            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = CookingSimHud.BorderGold;
            cardOutline.effectDistance = new Vector2(3f, -3f);

            CreatePanel(
                card.transform,
                "GoldRule",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 196f),
                new Vector2(200f, 3f),
                new Color(Gold.r, Gold.g, Gold.b, 0.9f));

            resultTitle = CreateLabel(card.transform, "ResultTitle", "営業終了",
                new Vector2(0f, 236f), 54, CookingSimHud.Gold, FontStyle.Bold);
            resultTitle.alignment = TextAnchor.MiddleCenter;
            resultTitle.rectTransform.sizeDelta = new Vector2(740f, 64f);

            var salesPanel = CreatePanel(
                card.transform,
                "SalesStat",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-190f, 40f),
                new Vector2(320f, 200f),
                CookingSimHud.PanelBgSoft);
            var salesOutline = salesPanel.AddComponent<Outline>();
            salesOutline.effectColor = CookingSimHud.BorderGold;
            salesOutline.effectDistance = new Vector2(2f, -2f);

            var salesCaption = CreateLabel(salesPanel.transform, "SalesCaption", "売上",
                new Vector2(0f, 58f), 26, CookingSimHud.Gold, FontStyle.Bold);
            salesCaption.alignment = TextAnchor.MiddleCenter;
            salesCaption.rectTransform.sizeDelta = new Vector2(280f, 36f);

            salesValue = CreateLabel(salesPanel.transform, "SalesValue", "0",
                new Vector2(0f, -8f), 64, CookingSimHud.White, FontStyle.Bold);
            salesValue.alignment = TextAnchor.MiddleCenter;
            salesValue.rectTransform.sizeDelta = new Vector2(300f, 80f);

            var salesUnit = CreateLabel(salesPanel.transform, "SalesUnit", "仮点数",
                new Vector2(0f, -72f), 18, CookingSimHud.Cream, FontStyle.Normal);
            salesUnit.alignment = TextAnchor.MiddleCenter;
            salesUnit.rectTransform.sizeDelta = new Vector2(280f, 28f);

            var servesPanel = CreatePanel(
                card.transform,
                "ServesStat",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(190f, 40f),
                new Vector2(320f, 200f),
                CookingSimHud.PanelBgSoft);
            var servesOutline = servesPanel.AddComponent<Outline>();
            servesOutline.effectColor = CookingSimHud.BorderTeal;
            servesOutline.effectDistance = new Vector2(2f, -2f);

            var servesCaption = CreateLabel(servesPanel.transform, "ServesCaption", "提供",
                new Vector2(0f, 58f), 26, CookingSimHud.Teal, FontStyle.Bold);
            servesCaption.alignment = TextAnchor.MiddleCenter;
            servesCaption.rectTransform.sizeDelta = new Vector2(280f, 36f);

            servesValue = CreateLabel(servesPanel.transform, "ServesValue", "0",
                new Vector2(0f, -8f), 64, CookingSimHud.White, FontStyle.Bold);
            servesValue.alignment = TextAnchor.MiddleCenter;
            servesValue.rectTransform.sizeDelta = new Vector2(300f, 80f);

            var servesUnit = CreateLabel(servesPanel.transform, "ServesUnit", "杯",
                new Vector2(0f, -72f), 18, CookingSimHud.Cream, FontStyle.Normal);
            servesUnit.alignment = TextAnchor.MiddleCenter;
            servesUnit.rectTransform.sizeDelta = new Vector2(280f, 28f);

            resultBody = CreateLabel(card.transform, "ResultBody", "残仕込み 0",
                new Vector2(0f, -100f), 22, CookingSimHud.Cream, FontStyle.Normal);
            resultBody.alignment = TextAnchor.MiddleCenter;
            resultBody.rectTransform.sizeDelta = new Vector2(700f, 36f);

            // Primary: return to TitleMenu
            titleButton = CreateActionButton(
                card.transform,
                "TitleButton",
                "タイトルへ",
                new Vector2(-150f, -178f),
                faceGold: true);

            retryButton = CreateActionButton(
                card.transform,
                "RetryButton",
                "もう一度",
                new Vector2(150f, -178f),
                faceGold: false);

            countdownLabel = CreateLabel(card.transform, "ResultCountdown",
                $"{Mathf.CeilToInt(AutoTitleSeconds)}秒後にタイトルへ",
                new Vector2(0f, -236f), 16, new Color(Teal.r, Teal.g, Teal.b, 0.9f), FontStyle.Normal);
            countdownLabel.alignment = TextAnchor.MiddleCenter;
            countdownLabel.rectTransform.sizeDelta = new Vector2(560f, 28f);

            var hint = CreateLabel(card.transform, "ResultHint", "Esc = タイトルへ　/　Enter = もう一度",
                new Vector2(0f, -266f), 14, new Color(CookingSimHud.Cream.r, CookingSimHud.Cream.g, CookingSimHud.Cream.b, 0.7f), FontStyle.Normal);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.rectTransform.sizeDelta = new Vector2(560f, 24f);
        }

        /// <summary>Legacy signature used by older callers — builds polished UI and discards extra outs.</summary>
        public static void BuildPolishedUi(
            GameObject resultRoot,
            out Text resultTitle,
            out Text resultBody,
            out Button retryButton,
            out Button titleButton)
        {
            BuildPolishedUi(resultRoot, out resultTitle, out resultBody, out retryButton, out titleButton,
                out _, out _, out _);
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
            outline.effectColor = new Color(1f, 0.92f, 0.55f, faceGold ? 0.45f : 0.2f);
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
            text.font = CookingSimHud.UiFont();
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
