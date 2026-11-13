using BackbeatDiner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Coop
{
    /// <summary>
    /// Chart-gap coop window (KG-2/3): Japanese prompts for スナップ推奨 + Beat-B3,
    /// brief CD unlock/boost on lexicon pads, and a pulsing banner UI.
    /// Driven by <see cref="DaySetlistRunner"/> during inter-chart kitchen gaps.
    /// </summary>
    public sealed class ChartGapCoopWindow : MonoBehaviour
    {
        private static readonly Color Gold = CookingSimHud.Gold;
        private static readonly Color Teal = CookingSimHud.Teal;

        private const string PromptTitle = "協力ウィンドウ";
        private const string PromptSnap = "スナップ推奨 — 表は Tab 長押し0.8sで C2 送信";
        private const string PromptB3 = "裏: Beat-B3「スナップ前」／Qで C1 クエスト";

        [SerializeField] private float pulseHz = 2.2f;
        [SerializeField] private float boostHoldSeconds = 2.5f;

        private BeatLexiconDemo _backBeats;
        private BeatLexiconDemo _frontBeats;
        private BeatReceivePulse _receivePulse;
        private SnapshotSendController _snapshotSend;
        private QuestRequestController _questRequest;

        private GameObject _root;
        private CanvasGroup _group;
        private Image _bannerBg;
        private Text _title;
        private Text _snapLine;
        private Text _b3Line;
        private Text _cdLine;

        private bool _active;
        private float _boostUntil;
        private Coroutine _boostRoutine;

        public bool IsActive => _active;

        public void Configure(
            BeatLexiconDemo backBeats,
            BeatLexiconDemo frontBeats,
            BeatReceivePulse receivePulse = null,
            SnapshotSendController snapshotSend = null,
            QuestRequestController questRequest = null)
        {
            _backBeats = backBeats;
            _frontBeats = frontBeats;
            _receivePulse = receivePulse;
            _snapshotSend = snapshotSend;
            _questRequest = questRequest;
            EnsureUi();
            SetVisible(false);
        }

        /// <summary>Open gap coop prompts, unlock CD briefly, pulse UI.</summary>
        public void BeginGap(float gapSeconds, string nextChartName = null)
        {
            EnsureUi();
            _active = true;

            if (_title != null)
            {
                var next = string.IsNullOrEmpty(nextChartName) ? "次チャート" : nextChartName;
                _title.text = $"{PromptTitle}（{Mathf.Max(0f, gapSeconds):0.#}s → {next}）";
            }

            if (_snapLine != null)
            {
                _snapLine.text = PromptSnap;
            }

            if (_b3Line != null)
            {
                _b3Line.text = PromptB3;
            }

            UnlockAndBoost();
            _snapshotSend?.SetGapPreferred(true);
            _snapshotSend?.UnlockCooldown();
            _questRequest?.UnlockCooldown();
            _receivePulse?.TriggerPulse(2, "スナップ前");
            _backBeats?.PulsePad(2, boostHoldSeconds);
            SetVisible(true);

            Debug.Log(
                $"[ChartGapCoopWindow] Gap coop open {gapSeconds:0.#}s — スナップ推奨 / Beat-B3 / C1 Q; CD unlock");
        }

        /// <summary>Hide prompts when the next chart starts.</summary>
        public void EndGap()
        {
            _active = false;
            _snapshotSend?.SetGapPreferred(false);
            if (_boostRoutine != null)
            {
                StopCoroutine(_boostRoutine);
                _boostRoutine = null;
            }

            SetVisible(false);
            Debug.Log("[ChartGapCoopWindow] Gap coop closed");
        }

        private void UnlockAndBoost()
        {
            _backBeats?.UnlockCooldowns();
            _frontBeats?.UnlockCooldowns();
            _snapshotSend?.UnlockCooldown();
            _questRequest?.UnlockCooldown();
            _boostUntil = Time.unscaledTime + boostHoldSeconds;

            if (_cdLine != null)
            {
                _cdLine.text = "CD解除ブースト中 — いま送れ";
                _cdLine.color = Teal;
            }

            if (_boostRoutine != null)
            {
                StopCoroutine(_boostRoutine);
            }

            _boostRoutine = StartCoroutine(BoostHudTick());
        }

        private System.Collections.IEnumerator BoostHudTick()
        {
            while (Time.unscaledTime < _boostUntil && _active)
            {
                var left = _boostUntil - Time.unscaledTime;
                if (_cdLine != null)
                {
                    _cdLine.text = $"CD解除ブースト中 {left:0.0}s — いま送れ";
                }

                yield return null;
            }

            if (_cdLine != null && _active)
            {
                _cdLine.text = "ビート送信OK（通常CD）";
                _cdLine.color = Gold;
            }

            _boostRoutine = null;
        }

        private void Update()
        {
            if (!_active || _group == null)
            {
                return;
            }

            var pulse = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * pulseHz));
            _group.alpha = pulse;

            if (_bannerBg != null)
            {
                var a = 0.55f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * pulseHz * 1.15f));
                _bannerBg.color = new Color(0.03f, 0.1f, 0.12f, a);
            }

            if (_snapLine != null)
            {
                _snapLine.color = Color.Lerp(Teal, Gold, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseHz * 3f));
            }
        }

        private void SetVisible(bool on)
        {
            if (_root != null)
            {
                _root.SetActive(on);
            }

            if (_group != null)
            {
                _group.alpha = on ? 1f : 0f;
            }
        }

        private void EnsureUi()
        {
            if (_root != null)
            {
                return;
            }

            var canvasGo = GameObject.Find("VerticalSliceHUD");
            if (canvasGo == null)
            {
                canvasGo = GameObject.Find("ChartGapCoopHUD");
                if (canvasGo == null)
                {
                    canvasGo = new GameObject("ChartGapCoopHUD");
                    var canvas = canvasGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 28;
                    canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            var existing = canvasGo.transform.Find("GapCoopBanner");
            if (existing != null)
            {
                _root = existing.gameObject;
            }
            else
            {
                _root = new GameObject("GapCoopBanner");
                _root.transform.SetParent(canvasGo.transform, false);
            }

            _group = _root.GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = _root.AddComponent<CanvasGroup>();
            }

            _bannerBg = _root.GetComponent<Image>();
            if (_bannerBg == null)
            {
                _bannerBg = _root.AddComponent<Image>();
            }

            _bannerBg.color = CookingSimHud.PanelBg;
            var rt = _bannerBg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 36f);
            rt.sizeDelta = new Vector2(860f, 200f);

            var outline = _root.GetComponent<Outline>() ?? _root.AddComponent<Outline>();
            outline.effectColor = CookingSimHud.BorderGold;
            outline.effectDistance = new Vector2(3f, -3f);

            _title = EnsureLine(_root.transform, "GapTitle", PromptTitle, CookingSimHud.FontTitle, Gold, new Vector2(0f, 68f));
            _snapLine = EnsureLine(_root.transform, "GapSnap", PromptSnap, CookingSimHud.FontBody, CookingSimHud.Cream, new Vector2(0f, 24f));
            _b3Line = EnsureLine(_root.transform, "GapB3", PromptB3, CookingSimHud.FontBody, Gold, new Vector2(0f, -16f));
            _cdLine = EnsureLine(_root.transform, "GapCd", "CD解除ブースト中", CookingSimHud.FontRow, Teal, new Vector2(0f, -60f));
        }

        private static Text EnsureLine(Transform parent, string name, string text, int size, Color color, Vector2 pos)
        {
            var t = parent.Find(name);
            Text label;
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                label = go.AddComponent<Text>();
            }
            else
            {
                label = t.GetComponent<Text>() ?? t.gameObject.AddComponent<Text>();
            }

            label.font = CookingSimHud.UiFont();
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(820f, 36f);
            rt.anchoredPosition = pos;
            return label;
        }

        private void OnDestroy()
        {
            if (_boostRoutine != null)
            {
                StopCoroutine(_boostRoutine);
                _boostRoutine = null;
            }
        }
    }
}
