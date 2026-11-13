using System;
using BackbeatDiner.Core;
using BackbeatDiner.Front;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Coop
{
    /// <summary>
    /// KG-3 C3 beat lexicon sender: pads map to Beat-B1..B4 or Beat-F1..F4.
    /// Solo: keys 1–4. Local 2P: Back A/S/D/F, Front ←↓→↑.
    /// CD: 8s any / 20s same-motif. Front blocked while <see cref="ServeRhythmWindow.FrontCommsSendLocked"/>.
    /// </summary>
    public sealed class BeatLexiconDemo : MonoBehaviour
    {
        /// <summary>Raised when a motif is sent (pad index 0–3, Japanese label).</summary>
        public static event Action<int, string> MotifSent;

        /// <summary>Raised with full definition after a successful send.</summary>
        public static event Action<BeatDefinition> BeatSent;

        [SerializeField] private BeatRole role = BeatRole.Front;
        [SerializeField] private BeatPadKeyScheme keyScheme = BeatPadKeyScheme.Digits;
        [SerializeField] private bool buildPadUi = true;
        [SerializeField] private AudioSource source;
        [SerializeField] private Text cooldownLabel;
        [SerializeField] private Image cooldownFill;

        private AudioClip[] _clips;
        private BeatDefinition[] _pads;
        private float _nextAnyReadyTime;
        private readonly float[] _nextMotifReadyTime = new float[8];
        private Text[] _padLabels;
        private Image[] _padBgs;
        private Text _statusLabel;
        private Coroutine _padPulseRoutine;

        public BeatRole Role => role;
        public BeatPadKeyScheme KeyScheme => keyScheme;
        public float RemainingAnyCooldown => Mathf.Max(0f, _nextAnyReadyTime - Time.unscaledTime);
        public bool IsFrontSendLocked =>
            role == BeatRole.Front && ServeRhythmWindow.FrontCommsSendLocked;
        public bool IsAnyReady => Time.unscaledTime >= _nextAnyReadyTime && !IsFrontSendLocked;

        public void Configure(
            BeatRole beatRole,
            Text cdLabel = null,
            Image cdFill = null,
            bool createPadUi = true,
            BeatPadKeyScheme padKeys = BeatPadKeyScheme.Digits)
        {
            role = beatRole;
            keyScheme = padKeys;
            cooldownLabel = cdLabel;
            cooldownFill = cdFill;
            buildPadUi = createPadUi;
            RebuildPads();
            if (buildPadUi)
            {
                EnsurePadUi();
            }
        }

        public void BindCooldownHud(Text cdLabel, Image cdFill)
        {
            cooldownLabel = cdLabel;
            cooldownFill = cdFill;
        }

        private void Awake()
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
            RebuildPads();
        }

        private void Start()
        {
            if (buildPadUi)
            {
                EnsurePadUi();
            }

            RefreshCooldownHud();
            RefreshPadUi();
        }

        private void RebuildPads()
        {
            _pads = BeatLexicon.PadsFor(role);
            _clips = new AudioClip[_pads.Length];
            for (var i = 0; i < _pads.Length; i++)
            {
                var def = _pads[i];
                _clips[i] = BuildMotif(def.Code, def.FrequenciesHz, def.NoteSeconds);
            }
        }

        private void Update()
        {
            var pad = ReadPadIndexThisFrame();
            if (pad >= 0)
            {
                TrySend(pad);
            }

            RefreshCooldownHud();
            RefreshPadUi();
        }

        private int ReadPadIndexThisFrame() => keyScheme switch
        {
            BeatPadKeyScheme.BackAsdf => LocalTwoPlayerBindings.ReadBackBeatPadIndex(),
            BeatPadKeyScheme.FrontArrows => LocalTwoPlayerBindings.ReadFrontBeatPadIndex(),
            _ => LocalTwoPlayerBindings.ReadDigitBeatPadIndex()
        };

        public bool TrySend(int padIndex)
        {
            if (_pads == null || padIndex < 0 || padIndex >= _pads.Length)
            {
                return false;
            }

            if (IsFrontSendLocked)
            {
                Debug.Log("[BeatLexicon] Send blocked — 配膳リズム中 (FrontCommsSendLocked)");
                SetStatus("送信ロック（配膳中）", FrontHudController.Gold);
                return false;
            }

            var now = Time.unscaledTime;
            var def = _pads[padIndex];
            var motifIdx = (int)def.Id;

            if (now < _nextAnyReadyTime)
            {
                Debug.Log($"[BeatLexicon] Any-motif CD {_nextAnyReadyTime - now:0.0}s left");
                SetStatus($"CD {RemainingAnyCooldown:0.0}s", FrontHudController.Gold);
                return false;
            }

            if (now < _nextMotifReadyTime[motifIdx])
            {
                var left = _nextMotifReadyTime[motifIdx] - now;
                Debug.Log($"[BeatLexicon] Same-motif CD {left:0.0}s left for {def.Code}");
                SetStatus($"同一モチーフ CD {left:0.0}s", FrontHudController.Gold);
                return false;
            }

            if (_clips != null && padIndex < _clips.Length && _clips[padIndex] != null)
            {
                source.PlayOneShot(_clips[padIndex], 0.85f);
            }

            _nextAnyReadyTime = now + BeatLexicon.AnyMotifCooldownSeconds;
            _nextMotifReadyTime[motifIdx] = now + BeatLexicon.SameMotifCooldownSeconds;

            Debug.Log($"[BeatLexicon] Send {def.DisplayName} (pad {padIndex + 1})");
            SetStatus($"送信: {def.JapaneseLabel}", FrontHudController.Teal);
            MotifSent?.Invoke(padIndex, def.JapaneseLabel);
            BeatSent?.Invoke(def);
            return true;
        }

        public float RemainingMotifCooldown(BeatId id)
        {
            return Mathf.Max(0f, _nextMotifReadyTime[(int)id] - Time.unscaledTime);
        }

        /// <summary>Chart-gap coop: clear any/same-motif CD so Beat-B3 / snaps can fire immediately.</summary>
        public void UnlockCooldowns()
        {
            _nextAnyReadyTime = 0f;
            for (var i = 0; i < _nextMotifReadyTime.Length; i++)
            {
                _nextMotifReadyTime[i] = 0f;
            }

            SetStatus("間隙ブースト: CD解除", FrontHudController.Teal);
            RefreshCooldownHud();
            RefreshPadUi();
        }

        /// <summary>Brief pad highlight (e.g. Beat-B3 during gap coop window).</summary>
        public void PulsePad(int padIndex, float durationSeconds = 2.5f)
        {
            if (padIndex < 0 || padIndex > 3)
            {
                return;
            }

            if (_padPulseRoutine != null)
            {
                StopCoroutine(_padPulseRoutine);
            }

            _padPulseRoutine = StartCoroutine(PulsePadRoutine(padIndex, Mathf.Max(0.2f, durationSeconds)));
        }

        private System.Collections.IEnumerator PulsePadRoutine(int padIndex, float duration)
        {
            var end = Time.unscaledTime + duration;
            while (Time.unscaledTime < end)
            {
                if (_padBgs != null && padIndex < _padBgs.Length && _padBgs[padIndex] != null)
                {
                    var t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
                    _padBgs[padIndex].color = Color.Lerp(
                        new Color(0.08f, 0.22f, 0.22f, 0.9f),
                        new Color(0.35f, 0.28f, 0.06f, 0.95f),
                        t);
                }

                if (_padLabels != null && padIndex < _padLabels.Length && _padLabels[padIndex] != null)
                {
                    _padLabels[padIndex].color = FrontHudController.Gold;
                }

                yield return null;
            }

            _padPulseRoutine = null;
            RefreshPadUi();
        }

        private void RefreshCooldownHud()
        {
            var locked = IsFrontSendLocked;
            var remain = RemainingAnyCooldown;
            var fill = locked
                ? 1f
                : (BeatLexicon.AnyMotifCooldownSeconds > 0f
                    ? Mathf.Clamp01(remain / BeatLexicon.AnyMotifCooldownSeconds)
                    : 0f);

            if (cooldownLabel != null)
            {
                if (locked)
                {
                    cooldownLabel.text = "CD: 配膳ロック";
                }
                else if (remain > 0f)
                {
                    cooldownLabel.text = $"CD: {remain:0.0}s";
                }
                else
                {
                    cooldownLabel.text = "CD: 準備OK";
                }
            }

            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = locked ? 1f : fill;
                cooldownFill.color = locked ? FrontHudController.Gold : FrontHudController.Teal;
            }
        }

        private string PadHudObjectName =>
            role == BeatRole.Back ? "BeatLexiconPadHUD_Back" : "BeatLexiconPadHUD_Front";

        private string PadRootName =>
            role == BeatRole.Back ? "PadRoot_Back" : "PadRoot_Front";

        private static readonly string[] DigitKeyLabels = { "1", "2", "3", "4" };
        private static readonly string[] AsdfKeyLabels = { "A", "S", "D", "F" };
        private static readonly string[] ArrowKeyLabels = { "←", "↓", "→", "↑" };

        private string[] KeyLabelsForScheme() => keyScheme switch
        {
            BeatPadKeyScheme.BackAsdf => AsdfKeyLabels,
            BeatPadKeyScheme.FrontArrows => ArrowKeyLabels,
            _ => DigitKeyLabels
        };

        private void EnsurePadUi()
        {
            var canvasName = PadHudObjectName;
            var canvasGo = GameObject.Find(canvasName);
            // Migrate legacy single canvas name used by FrontShell / older VerticalSlice.
            if (canvasGo == null && role == BeatRole.Front && keyScheme == BeatPadKeyScheme.Digits)
            {
                canvasGo = GameObject.Find("BeatLexiconPadHUD");
                if (canvasGo != null)
                {
                    canvasGo.name = canvasName;
                }
            }

            if (canvasGo == null)
            {
                canvasGo = new GameObject(canvasName);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = role == BeatRole.Back ? 24 : 25;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var rootName = PadRootName;
            var root = canvasGo.transform.Find(rootName) ?? canvasGo.transform.Find("PadRoot");
            RectTransform rootRt;
            if (root == null)
            {
                var go = new GameObject(rootName);
                go.transform.SetParent(canvasGo.transform, false);
                rootRt = go.AddComponent<RectTransform>();
                if (role == BeatRole.Back)
                {
                    rootRt.anchorMin = new Vector2(0f, 0f);
                    rootRt.anchorMax = new Vector2(0f, 0f);
                    rootRt.pivot = new Vector2(0f, 0f);
                    rootRt.anchoredPosition = new Vector2(24f, 72f);
                }
                else
                {
                    rootRt.anchorMin = new Vector2(1f, 0f);
                    rootRt.anchorMax = new Vector2(1f, 0f);
                    rootRt.pivot = new Vector2(1f, 0f);
                    rootRt.anchoredPosition = new Vector2(-24f, 72f);
                }

                rootRt.sizeDelta = new Vector2(460f, 132f);
            }
            else
            {
                root.name = rootName;
                rootRt = root.GetComponent<RectTransform>();
            }

            // Soft chrome behind pads
            if (rootRt.Find("PadChrome") == null)
            {
                var chromeGo = new GameObject("PadChrome");
                chromeGo.transform.SetParent(rootRt, false);
                chromeGo.transform.SetAsFirstSibling();
                var chrome = chromeGo.AddComponent<Image>();
                chrome.color = new Color(0.03f, 0.07f, 0.1f, 0.78f);
                var crt = chrome.rectTransform;
                crt.anchorMin = Vector2.zero;
                crt.anchorMax = Vector2.one;
                crt.offsetMin = new Vector2(-10f, -10f);
                crt.offsetMax = new Vector2(10f, 10f);
            }

            _padLabels = new Text[4];
            _padBgs = new Image[4];
            for (var i = 0; i < 4; i++)
            {
                var name = $"Pad_{i + 1}";
                var existing = rootRt.Find(name);
                Image bg;
                Text label;
                if (existing == null)
                {
                    var padGo = new GameObject(name);
                    padGo.transform.SetParent(rootRt, false);
                    var frame = padGo.AddComponent<Image>();
                    frame.color = new Color(FrontHudController.Gold.r, FrontHudController.Gold.g, FrontHudController.Gold.b, 0.55f);
                    var rt = frame.rectTransform;
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(104f, 88f);
                    rt.anchoredPosition = new Vector2(8f + i * 112f, 0f);

                    var insetGo = new GameObject("Inset");
                    insetGo.transform.SetParent(padGo.transform, false);
                    bg = insetGo.AddComponent<Image>();
                    bg.color = new Color(0.05f, 0.12f, 0.12f, 0.95f);
                    var irt = bg.rectTransform;
                    irt.anchorMin = Vector2.zero;
                    irt.anchorMax = Vector2.one;
                    irt.offsetMin = new Vector2(2f, 2f);
                    irt.offsetMax = new Vector2(-2f, -2f);

                    var labelGo = new GameObject("Label");
                    labelGo.transform.SetParent(padGo.transform, false);
                    label = labelGo.AddComponent<Text>();
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                    label.fontSize = 13;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = FrontHudController.Gold;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    var lrt = label.rectTransform;
                    lrt.anchorMin = Vector2.zero;
                    lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = new Vector2(4f, 4f);
                    lrt.offsetMax = new Vector2(-4f, -4f);
                }
                else
                {
                    bg = existing.Find("Inset")?.GetComponent<Image>() ?? existing.GetComponent<Image>();
                    label = existing.Find("Label")?.GetComponent<Text>();
                    if (label == null)
                    {
                        continue;
                    }
                }

                _padBgs[i] = bg;
                _padLabels[i] = label;
            }

            var statusName = role == BeatRole.Back ? "PadStatus_Back" : "PadStatus_Front";
            var status = canvasGo.transform.Find(statusName) ?? canvasGo.transform.Find("PadStatus");
            if (status == null)
            {
                var go = new GameObject(statusName);
                go.transform.SetParent(canvasGo.transform, false);
                _statusLabel = go.AddComponent<Text>();
                _statusLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                _statusLabel.fontSize = 16;
                _statusLabel.color = FrontHudController.Teal;
                var rt = _statusLabel.rectTransform;
                if (role == BeatRole.Back)
                {
                    _statusLabel.alignment = TextAnchor.MiddleLeft;
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.anchoredPosition = new Vector2(24f, 48f);
                }
                else
                {
                    _statusLabel.alignment = TextAnchor.MiddleRight;
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.anchoredPosition = new Vector2(-24f, 48f);
                }

                rt.sizeDelta = new Vector2(420f, 24f);
            }
            else
            {
                status.name = statusName;
                _statusLabel = status.GetComponent<Text>();
            }

            RefreshPadUi();
        }

        private void RefreshPadUi()
        {
            if (_padLabels == null || _pads == null)
            {
                return;
            }

            var locked = IsFrontSendLocked;
            var anyReady = Time.unscaledTime >= _nextAnyReadyTime;
            var keyLabels = KeyLabelsForScheme();
            for (var i = 0; i < 4; i++)
            {
                var def = _pads[i];
                var motifCd = RemainingMotifCooldown(def.Id);
                var ready = !locked && anyReady && motifCd <= 0f;
                if (_padLabels[i] != null)
                {
                    var key = keyLabels[i];
                    var cdNote = locked
                        ? "ロック"
                        : motifCd > 0f
                            ? $"{motifCd:0}s"
                            : !anyReady
                                ? $"{RemainingAnyCooldown:0}s"
                                : "OK";
                    _padLabels[i].text = $"{key}\n{def.JapaneseLabel}\n{cdNote}";
                    _padLabels[i].color = ready ? FrontHudController.Gold : new Color(0.55f, 0.55f, 0.5f);
                }

                if (_padBgs[i] != null)
                {
                    _padBgs[i].color = ready
                        ? new Color(0.08f, 0.22f, 0.22f, 0.9f)
                        : new Color(0.08f, 0.08f, 0.08f, 0.75f);
                }
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.text = text;
            _statusLabel.color = color;
        }

        private static AudioClip BuildMotif(string name, float[] freqs, float noteSeconds)
        {
            const int sampleRate = 44100;
            var gap = 0.02f;
            var total = noteSeconds * freqs.Length + gap * Mathf.Max(0, freqs.Length - 1);
            var samples = Mathf.CeilToInt(total * sampleRate);
            var data = new float[samples];

            var cursor = 0;
            for (var n = 0; n < freqs.Length; n++)
            {
                var len = Mathf.CeilToInt(noteSeconds * sampleRate);
                for (var i = 0; i < len && cursor + i < samples; i++)
                {
                    var t = i / (float)sampleRate;
                    var env = Mathf.Exp(-t * 14f) * (1f - t / Mathf.Max(0.001f, noteSeconds));
                    data[cursor + i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * env * 0.55f;
                }

                cursor += len + Mathf.CeilToInt(gap * sampleRate);
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
