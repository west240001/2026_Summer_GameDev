using BackbeatDiner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Coop
{
    /// <summary>
    /// KG-3 C2 receive (裏): inventory/customer panel + Snap-In beat SE on arrival.
    /// Panel stays ~5s (or until dismissed / next gap end). Japanese HUD.
    /// </summary>
    public sealed class SnapshotReceivePanel : MonoBehaviour
    {
        private static readonly Color Gold = CookingSimHud.Gold;
        private static readonly Color Teal = CookingSimHud.Teal;

        [SerializeField] private float panelSeconds = 5f;
        [SerializeField] private AudioSource source;

        private GameObject _root;
        private CanvasGroup _group;
        private Text _title;
        private Text _customerLine;
        private Text _stockLine;
        private Text[] _stockLines;
        private Image _pulseEdge;
        private float _hideAt;
        private AudioClip _snapInClip;
        private bool _visible;

        public void Configure(AudioSource audio = null)
        {
            if (audio != null)
            {
                source = audio;
            }

            EnsureAudio();
            EnsureUi();
            SetVisible(false);
        }

        private void OnEnable()
        {
            SnapshotSendController.SnapshotSent += OnSnapshotSent;
        }

        private void OnDisable()
        {
            SnapshotSendController.SnapshotSent -= OnSnapshotSent;
        }

        private void Start()
        {
            EnsureAudio();
            EnsureUi();
            SetVisible(false);
        }

        private void Update()
        {
            if (!_visible)
            {
                return;
            }

            if (_group != null)
            {
                var pulse = 0.78f + 0.22f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.4f));
                _group.alpha = pulse;
            }

            if (_pulseEdge != null)
            {
                var a = 0.15f + 0.2f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f));
                _pulseEdge.color = new Color(Teal.r, Teal.g, Teal.b, a);
            }

            if (Time.unscaledTime >= _hideAt)
            {
                SetVisible(false);
            }
        }

        private void OnSnapshotSent(SnapshotPayload payload)
        {
            Show(payload);
        }

        public void Show(SnapshotPayload payload)
        {
            EnsureUi();
            EnsureAudio();

            if (_title != null)
            {
                _title.text = "スナップ着（Snap-In）";
            }

            if (_customerLine != null)
            {
                _customerLine.text = payload.CustomerLine;
            }

            if (_stockLine != null)
            {
                _stockLine.text = payload.StockSummary;
            }

            if (_stockLines != null && payload.CategoryNames != null && payload.StockLevels != null)
            {
                for (var i = 0; i < _stockLines.Length; i++)
                {
                    if (_stockLines[i] == null)
                    {
                        continue;
                    }

                    if (i < payload.CategoryNames.Length && i < payload.StockLevels.Length)
                    {
                        var pct = Mathf.RoundToInt(Mathf.Clamp01(payload.StockLevels[i]) * 100f);
                        _stockLines[i].text = $"{payload.CategoryNames[i]}  {pct}%";
                        _stockLines[i].color = payload.StockLevels[i] < 0.35f ? Gold : Teal;
                    }
                    else
                    {
                        _stockLines[i].text = "—";
                    }
                }
            }

            if (source != null && _snapInClip != null)
            {
                source.PlayOneShot(_snapInClip, 0.9f);
            }

            _hideAt = Time.unscaledTime + panelSeconds;
            SetVisible(true);
            Debug.Log(
                $"[SnapshotReceive] Snap-In — {payload.CustomerLine} / {payload.StockSummary}");
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool on)
        {
            _visible = on;
            if (_root != null)
            {
                _root.SetActive(on);
            }

            if (_group != null)
            {
                _group.alpha = on ? 1f : 0f;
            }
        }

        private void EnsureAudio()
        {
            if (source == null)
            {
                source = gameObject.GetComponent<AudioSource>();
                if (source == null)
                {
                    source = gameObject.AddComponent<AudioSource>();
                }
            }

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            if (_snapInClip == null)
            {
                _snapInClip = BuildSnapInClip();
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
                canvasGo = GameObject.Find("SnapshotReceiveHUD");
                if (canvasGo == null)
                {
                    canvasGo = new GameObject("SnapshotReceiveHUD");
                    var canvas = canvasGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 27;
                    canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            var existing = canvasGo.transform.Find("SnapshotReceivePanel");
            if (existing != null)
            {
                _root = existing.gameObject;
            }
            else
            {
                _root = new GameObject("SnapshotReceivePanel");
                _root.transform.SetParent(canvasGo.transform, false);
            }

            _group = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
            var bg = _root.GetComponent<Image>() ?? _root.AddComponent<Image>();
            bg.color = CookingSimHud.PanelBg;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(18f, -380f);
            rt.sizeDelta = new Vector2(420f, 250f);
            var outline = _root.GetComponent<Outline>() ?? _root.AddComponent<Outline>();
            outline.effectColor = CookingSimHud.BorderTeal;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            _pulseEdge = EnsureEdge(canvasGo.transform);
            _title = EnsureLine(_root.transform, "SnapTitle", "スナップ着（Snap-In）", CookingSimHud.FontHeader, Gold, new Vector2(0f, 96f));
            _customerLine = EnsureLine(_root.transform, "SnapCustomers", "待ち客: —", CookingSimHud.FontRow, CookingSimHud.Cream, new Vector2(0f, 60f));
            _stockLine = EnsureLine(_root.transform, "SnapStockSummary", "在庫: —", CookingSimHud.FontRow, Gold, new Vector2(0f, 30f));

            _stockLines = new Text[5];
            for (var i = 0; i < 5; i++)
            {
                _stockLines[i] = EnsureLine(
                    _root.transform,
                    $"SnapStock_{i}",
                    "—",
                    CookingSimHud.FontHint,
                    Teal,
                    new Vector2(0f, 0f - i * 24f));
            }
        }

        private static Image EnsureEdge(Transform canvas)
        {
            var edge = canvas.Find("SnapshotEdgePulse");
            Image img;
            if (edge == null)
            {
                var go = new GameObject("SnapshotEdgePulse");
                go.transform.SetParent(canvas, false);
                img = go.AddComponent<Image>();
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                img = edge.GetComponent<Image>();
            }

            img.color = new Color(Teal.r, Teal.g, Teal.b, 0f);
            img.raycastTarget = false;
            return img;
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
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = FontStyle.Bold;
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(16f, -14f);
            rt.offsetMax = new Vector2(-16f, 14f);
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.rectTransform.anchoredPosition = pos;
            return label;
        }

        /// <summary>Procedural Snap-In motif (~1 bar): rising click + settle.</summary>
        private static AudioClip BuildSnapInClip()
        {
            const int sampleRate = 44100;
            var freqs = new[] { 196f, 247f, 330f, 392f, 523f };
            var noteSeconds = 0.09f;
            var gap = 0.025f;
            var total = noteSeconds * freqs.Length + gap * (freqs.Length - 1) + 0.12f;
            var samples = Mathf.CeilToInt(total * sampleRate);
            var data = new float[samples];
            var cursor = 0;
            for (var n = 0; n < freqs.Length; n++)
            {
                var len = Mathf.CeilToInt(noteSeconds * sampleRate);
                for (var i = 0; i < len && cursor + i < samples; i++)
                {
                    var t = i / (float)sampleRate;
                    var env = Mathf.Exp(-t * 12f) * (1f - t / Mathf.Max(0.001f, noteSeconds));
                    data[cursor + i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * env * 0.58f;
                }

                cursor += len + Mathf.CeilToInt(gap * sampleRate);
            }

            // Soft settle tail
            var tail = Mathf.CeilToInt(0.12f * sampleRate);
            for (var i = 0; i < tail && cursor + i < samples; i++)
            {
                var t = i / (float)sampleRate;
                data[cursor + i] += Mathf.Sin(2f * Mathf.PI * 164.81f * t) * Mathf.Exp(-t * 18f) * 0.28f;
            }

            var clip = AudioClip.Create("SnapIn_Beat", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
