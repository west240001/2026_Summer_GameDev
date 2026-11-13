using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BackbeatDiner.Coop
{
    /// <summary>
    /// Front-side receive feedback for KG-3 beats: screen-edge pulse + optional a11y waveform strip.
    /// Listens to <see cref="BeatLexiconDemo.MotifSent"/> (Beat-B*/F* pads) and a simulated back-beat (B / auto).
    /// Japanese toast is tutorial-only (first pulse).
    /// </summary>
    public sealed class BeatReceivePulse : MonoBehaviour
    {
        private static readonly Color Gold = new Color(0.7882353f, 0.6352941f, 0.15294118f);
        private static readonly Color Teal = new Color(0.101960784f, 0.419607843f, 0.419607843f);

        [SerializeField] private bool showWaveformStrip = true;
        [SerializeField] private bool showTutorialToast = true;
        [SerializeField] private bool autoSimulateBackBeat = true;
        [SerializeField] private float autoIntervalSeconds = 6.5f;
        [SerializeField] private float pulseDuration = 0.45f;

        private Image _edge;
        private Image[] _waveBars;
        private Text _toast;
        private float _pulseT;
        private float _autoTimer;
        private bool _toastShown;
        private int _lastMotif = -1;

        private void OnEnable()
        {
            BeatLexiconDemo.MotifSent += OnMotifSent;
        }

        private void OnDisable()
        {
            BeatLexiconDemo.MotifSent -= OnMotifSent;
        }

        public void Configure(bool autoSimulate = false, bool tutorialToast = true, bool waveform = true)
        {
            autoSimulateBackBeat = autoSimulate;
            showTutorialToast = tutorialToast;
            showWaveformStrip = waveform;
        }

        private void Start()
        {
            EnsureUi();
            _autoTimer = autoIntervalSeconds * 0.6f;
            SetPulseVisible(0f);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame)
            {
                TriggerPulse(-1, "シミュレート裏ビート");
            }

            if (autoSimulateBackBeat)
            {
                _autoTimer -= Time.deltaTime;
                if (_autoTimer <= 0f)
                {
                    _autoTimer = autoIntervalSeconds;
                    TriggerPulse(-2, "シミュレート裏ビート");
                }
            }

            if (_pulseT > 0f)
            {
                _pulseT -= Time.deltaTime;
                var t = Mathf.Clamp01(_pulseT / pulseDuration);
                SetPulseVisible(t);
                AnimateWave(t);
            }
        }

        private void OnMotifSent(int motifIndex, string label)
        {
            TriggerPulse(motifIndex, label);
        }

        public void TriggerPulse(int motifIndex, string label)
        {
            _lastMotif = motifIndex;
            _pulseT = pulseDuration;
            SetPulseVisible(1f);

            if (showTutorialToast && !_toastShown && _toast != null)
            {
                _toastShown = true;
                _toast.text = "裏からのビートを受信しました（チュートリアル）";
                _toast.gameObject.SetActive(true);
                CancelInvoke(nameof(HideToast));
                Invoke(nameof(HideToast), 2.4f);
            }

            Debug.Log($"[BeatReceivePulse] Receive motif={motifIndex} ({label})");
            _ = _lastMotif;
        }

        private void HideToast()
        {
            if (_toast != null)
            {
                _toast.gameObject.SetActive(false);
            }
        }

        private void EnsureUi()
        {
            var canvasGo = GameObject.Find("BeatReceiveHUD");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("BeatReceiveHUD");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var edgeGo = canvasGo.transform.Find("EdgePulse");
            if (edgeGo == null)
            {
                var go = new GameObject("EdgePulse");
                go.transform.SetParent(canvasGo.transform, false);
                _edge = go.AddComponent<Image>();
                _edge.color = new Color(Gold.r, Gold.g, Gold.b, 0f);
                var rt = _edge.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                _edge = edgeGo.GetComponent<Image>();
            }

            if (showWaveformStrip)
            {
                BuildWaveStrip(canvasGo.transform);
            }

            if (showTutorialToast)
            {
                var toastGo = canvasGo.transform.Find("TutorialToast");
                if (toastGo == null)
                {
                    var go = new GameObject("TutorialToast");
                    go.transform.SetParent(canvasGo.transform, false);
                    _toast = go.AddComponent<Text>();
                    _toast.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                    _toast.fontSize = 22;
                    _toast.alignment = TextAnchor.MiddleCenter;
                    _toast.color = Gold;
                    var rt = _toast.rectTransform;
                    rt.anchorMin = new Vector2(0.5f, 0.12f);
                    rt.anchorMax = new Vector2(0.5f, 0.12f);
                    rt.sizeDelta = new Vector2(720f, 40f);
                    go.SetActive(false);
                }
                else
                {
                    _toast = toastGo.GetComponent<Text>();
                }
            }
        }

        private void BuildWaveStrip(Transform parent)
        {
            var strip = parent.Find("WaveformStrip");
            Transform stripT;
            if (strip == null)
            {
                var go = new GameObject("WaveformStrip");
                go.transform.SetParent(parent, false);
                stripT = go.transform;
                var bg = go.AddComponent<Image>();
                bg.color = new Color(0.02f, 0.06f, 0.06f, 0.7f);
                var rt = bg.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 18f);
                rt.sizeDelta = new Vector2(280f, 36f);
            }
            else
            {
                stripT = strip;
            }

            const int bars = 12;
            _waveBars = new Image[bars];
            for (var i = 0; i < bars; i++)
            {
                var existing = stripT.Find($"Bar_{i}");
                Image img;
                if (existing == null)
                {
                    var bar = new GameObject($"Bar_{i}");
                    bar.transform.SetParent(stripT, false);
                    img = bar.AddComponent<Image>();
                    var rt = img.rectTransform;
                    rt.anchorMin = new Vector2(0f, 0.15f);
                    rt.anchorMax = new Vector2(0f, 0.15f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.sizeDelta = new Vector2(14f, 8f);
                    rt.anchoredPosition = new Vector2(20f + i * 22f, 4f);
                }
                else
                {
                    img = existing.GetComponent<Image>();
                }

                img.color = Teal;
                _waveBars[i] = img;
            }
        }

        private void SetPulseVisible(float intensity)
        {
            if (_edge == null)
            {
                return;
            }

            var a = intensity * 0.35f;
            _edge.color = new Color(Gold.r, Gold.g, Gold.b, a);
        }

        private void AnimateWave(float t)
        {
            if (_waveBars == null)
            {
                return;
            }

            for (var i = 0; i < _waveBars.Length; i++)
            {
                if (_waveBars[i] == null)
                {
                    continue;
                }

                var h = 8f + Mathf.Abs(Mathf.Sin(Time.time * 18f + i * 0.55f)) * 22f * t;
                var rt = _waveBars[i].rectTransform;
                rt.sizeDelta = new Vector2(14f, h);
                _waveBars[i].color = Color.Lerp(Teal, Gold, t * (0.4f + (i % 3) * 0.2f));
            }
        }
    }
}
