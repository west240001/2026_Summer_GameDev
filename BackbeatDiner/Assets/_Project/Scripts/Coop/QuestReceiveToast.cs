using BackbeatDiner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Coop
{
    /// <summary>
    /// KG-3 C1 receive (表): Quest beat SE + Japanese toast + icon badge.
    /// Front answers with C2 Tab (<see cref="SnapshotSendController"/>) or Beat-F1「ビジー」.
    /// </summary>
    public sealed class QuestReceiveToast : MonoBehaviour
    {
        private static readonly Color Gold = CookingSimHud.Gold;
        private static readonly Color Teal = CookingSimHud.Teal;

        [SerializeField] private float toastSeconds = 4.5f;
        [SerializeField] private AudioSource source;

        private GameObject _root;
        private CanvasGroup _group;
        private Image _icon;
        private Text _toast;
        private Text _hint;
        private float _hideAt;
        private bool _pending;
        private AudioClip _questClip;

        public bool HasPendingQuest => _pending;

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
            QuestRequestController.QuestRequested += OnQuestRequested;
            SnapshotSendController.SnapshotSent += OnSnapshotAnswered;
            BeatLexiconDemo.BeatSent += OnBeatAnswered;
        }

        private void OnDisable()
        {
            QuestRequestController.QuestRequested -= OnQuestRequested;
            SnapshotSendController.SnapshotSent -= OnSnapshotAnswered;
            BeatLexiconDemo.BeatSent -= OnBeatAnswered;
        }

        private void Start()
        {
            EnsureAudio();
            EnsureUi();
            SetVisible(false);
        }

        private void Update()
        {
            if (!_pending || _root == null || !_root.activeSelf)
            {
                return;
            }

            if (_group != null)
            {
                var pulse = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.6f));
                _group.alpha = pulse;
            }

            if (_icon != null)
            {
                var a = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f));
                _icon.color = new Color(Gold.r, Gold.g, Gold.b, a);
            }

            if (Time.unscaledTime >= _hideAt)
            {
                // Toast fades; pending stays until Front answers with C2 / F1.
                if (_root != null)
                {
                    _root.SetActive(false);
                }
            }
        }

        private void OnQuestRequested(QuestRequestPayload payload)
        {
            Show(payload);
        }

        private void OnSnapshotAnswered(SnapshotPayload _)
        {
            if (!_pending)
            {
                return;
            }

            Acknowledge("スナップで応答しました");
        }

        private void OnBeatAnswered(BeatDefinition def)
        {
            if (!_pending || def.Id != BeatId.F1)
            {
                return;
            }

            Acknowledge("ビジーで応答しました");
        }

        public void Show(QuestRequestPayload payload)
        {
            EnsureUi();
            EnsureAudio();

            _pending = true;
            if (_toast != null)
            {
                _toast.text = $"クエスト要求（{payload.JapaneseSlotLabel}）";
            }

            if (_hint != null)
            {
                _hint.text = "応答: Tab長押し=C2 ／ ←=Beat-F1 ビジー";
            }

            if (source != null && _questClip != null)
            {
                source.PlayOneShot(_questClip, 0.92f);
            }

            _hideAt = Time.unscaledTime + toastSeconds;
            SetVisible(true);
            Debug.Log($"[QuestReceive] Quest SE — {payload.JapaneseSlotLabel}");
        }

        private void Acknowledge(string message)
        {
            _pending = false;
            if (_toast != null)
            {
                _toast.text = message;
            }

            if (_hint != null)
            {
                _hint.text = "要求クリア";
            }

            _hideAt = Time.unscaledTime + 1.6f;
            SetVisible(true);
            Debug.Log($"[QuestReceive] {message}");
            CancelInvoke(nameof(ClearPendingUi));
            Invoke(nameof(ClearPendingUi), 1.65f);
        }

        private void ClearPendingUi()
        {
            SetVisible(false);
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
            if (_questClip == null)
            {
                _questClip = BuildQuestClip();
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
                canvasGo = GameObject.Find("QuestReceiveHUD");
                if (canvasGo == null)
                {
                    canvasGo = new GameObject("QuestReceiveHUD");
                    var canvas = canvasGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 28;
                    canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            var existing = canvasGo.transform.Find("QuestReceiveToast");
            if (existing != null)
            {
                _root = existing.gameObject;
            }
            else
            {
                _root = new GameObject("QuestReceiveToast");
                _root.transform.SetParent(canvasGo.transform, false);
            }

            _group = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
            var bg = _root.GetComponent<Image>() ?? _root.AddComponent<Image>();
            bg.color = CookingSimHud.PanelBg;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-24f, 80f);
            rt.sizeDelta = new Vector2(480f, 110f);
            var outline = _root.GetComponent<Outline>() ?? _root.AddComponent<Outline>();
            outline.effectColor = CookingSimHud.BorderGold;
            outline.effectDistance = new Vector2(3f, -3f);

            _icon = EnsureIcon(_root.transform);
            _toast = EnsureLine(_root.transform, "QuestToast", "クエスト要求", CookingSimHud.FontHeader, Gold, new Vector2(40f, 22f));
            _hint = EnsureLine(_root.transform, "QuestHint", "応答: Tab長押し=C2 ／ ←=Beat-F1 ビジー", CookingSimHud.FontRow, CookingSimHud.Cream, new Vector2(40f, -18f));
        }

        private static Image EnsureIcon(Transform parent)
        {
            var t = parent.Find("QuestIcon");
            Image img;
            if (t == null)
            {
                var go = new GameObject("QuestIcon");
                go.transform.SetParent(parent, false);
                img = go.AddComponent<Image>();
            }
            else
            {
                img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            }

            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(36f, 0f);
            rt.sizeDelta = new Vector2(40f, 40f);
            img.color = new Color(Gold.r, Gold.g, Gold.b, 0.95f);
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
            rt.offsetMin = new Vector2(64f, -16f);
            rt.offsetMax = new Vector2(-14f, 16f);
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.rectTransform.anchoredPosition = pos;
            return label;
        }

        /// <summary>Procedural Quest motif (~1 bar): rising ask + soft tail.</summary>
        private static AudioClip BuildQuestClip()
        {
            const int sampleRate = 44100;
            var freqs = new[] { 293.66f, 349.23f, 440f, 523.25f, 659.25f };
            var noteSeconds = 0.1f;
            var gap = 0.03f;
            var total = noteSeconds * freqs.Length + gap * (freqs.Length - 1) + 0.14f;
            var samples = Mathf.CeilToInt(total * sampleRate);
            var data = new float[samples];
            var cursor = 0;
            for (var n = 0; n < freqs.Length; n++)
            {
                var len = Mathf.CeilToInt(noteSeconds * sampleRate);
                for (var i = 0; i < len && cursor + i < samples; i++)
                {
                    var t = i / (float)sampleRate;
                    var env = Mathf.Exp(-t * 10f) * (1f - t / Mathf.Max(0.001f, noteSeconds));
                    data[cursor + i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * env * 0.55f;
                }

                cursor += len + Mathf.CeilToInt(gap * sampleRate);
            }

            var tail = Mathf.CeilToInt(0.14f * sampleRate);
            for (var i = 0; i < tail && cursor + i < samples; i++)
            {
                var t = i / (float)sampleRate;
                data[cursor + i] += Mathf.Sin(2f * Mathf.PI * 369.99f * t) * Mathf.Exp(-t * 14f) * 0.32f;
            }

            var clip = AudioClip.Create("Quest_Beat", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
