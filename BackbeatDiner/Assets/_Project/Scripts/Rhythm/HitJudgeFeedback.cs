using UnityEngine;
using UnityEngine.UI;
using BackbeatDiner.Core;

namespace BackbeatDiner.Rhythm
{
    /// <summary>
    /// Clear Perfect / Good / Miss feedback for the back rhythm lane:
    /// JP popup label (scale punch + fade) + short procedural SFX.
    /// Keeps Cooking-sim ticket <c>JudgementLabel</c> in sync when assigned.
    /// </summary>
    public sealed class HitJudgeFeedback : MonoBehaviour
    {
        private static readonly Color PerfectColor = CookingSimHud.GoldBright;
        private static readonly Color GoodColor = new Color(0.55f, 0.92f, 0.88f, 1f);
        private static readonly Color MissColor = new Color(0.92f, 0.42f, 0.38f, 1f);

        [SerializeField] private Text ticketLabel;
        [SerializeField] private AudioSource source;
        [SerializeField] private float popupSeconds = 0.85f;

        private Text _popup;
        private Outline _popupOutline;
        private CanvasGroup _popupGroup;
        private RectTransform _popupRt;
        private AudioClip _perfectClip;
        private AudioClip _goodClip;
        private AudioClip _missClip;
        private float _popupUntil;
        private float _punch;

        public void Configure(Text judgementTicketLabel, AudioSource audio = null, Transform hudRoot = null)
        {
            ticketLabel = judgementTicketLabel;
            if (audio != null)
            {
                source = audio;
            }

            EnsureAudio();
            EnsurePopup(hudRoot != null ? hudRoot : ticketLabel != null ? ticketLabel.transform.parent : null);
        }

        public void Play(HitJudgement judgement, double deltaSeconds, int combo)
        {
            EnsureAudio();
            EnsurePopup(ticketLabel != null ? ticketLabel.transform.parent : null);

            var jp = ToJapanese(judgement);
            var color = ColourFor(judgement);
            var deltaMs = deltaSeconds * 1000.0;

            if (ticketLabel != null)
            {
                ticketLabel.text = combo > 0
                    ? $"判定: {jp} ({deltaMs:F0}ms)"
                    : $"判定: {jp}";
                ticketLabel.color = color;
            }

            if (_popup != null)
            {
                _popup.text = jp;
                _popup.color = color;
                if (_popupOutline != null)
                {
                    _popupOutline.effectColor = new Color(color.r, color.g, color.b, 0.85f);
                }

                _popupUntil = Time.unscaledTime + popupSeconds;
                _punch = 1f;
                if (_popupGroup != null)
                {
                    _popupGroup.alpha = 1f;
                }

                if (_popupRt != null)
                {
                    _popupRt.localScale = Vector3.one * 1.35f;
                }

                _popup.gameObject.SetActive(true);
            }

            PlaySfx(judgement);
        }

        private void Update()
        {
            if (_popup == null || !_popup.gameObject.activeSelf)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now >= _popupUntil)
            {
                if (_popupGroup != null)
                {
                    _popupGroup.alpha = 0f;
                }

                _popup.gameObject.SetActive(false);
                return;
            }

            var remain = _popupUntil - now;
            var t = 1f - Mathf.Clamp01(remain / Mathf.Max(0.01f, popupSeconds));
            if (_punch > 0f)
            {
                _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 4.5f);
            }

            if (_popupRt != null)
            {
                var scale = Mathf.Lerp(1.35f, 1f, 1f - _punch) * (1f + 0.04f * Mathf.Sin(now * 28f));
                _popupRt.localScale = Vector3.one * scale;
                // Drift slightly upward for presence.
                var baseY = 40f;
                _popupRt.anchoredPosition = new Vector2(0f, baseY + t * 36f);
            }

            if (_popupGroup != null)
            {
                _popupGroup.alpha = t < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
            }
        }

        private void PlaySfx(HitJudgement judgement)
        {
            if (source == null)
            {
                return;
            }

            AudioClip clip;
            float vol;
            switch (judgement)
            {
                case HitJudgement.Perfect:
                    clip = _perfectClip;
                    vol = 0.9f;
                    break;
                case HitJudgement.Good:
                    clip = _goodClip;
                    vol = 0.8f;
                    break;
                default:
                    clip = _missClip;
                    vol = 0.75f;
                    break;
            }

            if (clip != null)
            {
                source.PlayOneShot(clip, vol);
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

            if (_perfectClip == null)
            {
                _perfectClip = BuildToneStack("HitJudge_Perfect", new[] { 880f, 1320f }, 0.055f, 18f, 0.55f);
            }

            if (_goodClip == null)
            {
                _goodClip = BuildToneStack("HitJudge_Good", new[] { 660f }, 0.07f, 22f, 0.5f);
            }

            if (_missClip == null)
            {
                _missClip = BuildNoiseThud("HitJudge_Miss", 0.12f, 140f, 0.45f);
            }
        }

        private void EnsurePopup(Transform hudRoot)
        {
            if (_popup != null)
            {
                return;
            }

            if (hudRoot == null)
            {
                var canvas = CookingSimHud.EnsureOverlayCanvas("HitJudgeFeedbackHUD", 40);
                hudRoot = canvas.transform;
            }

            var existing = hudRoot.Find("HitJudgePopup");
            GameObject go;
            if (existing == null)
            {
                go = new GameObject("HitJudgePopup");
                go.transform.SetParent(hudRoot, false);
                _popup = go.AddComponent<Text>();
            }
            else
            {
                go = existing.gameObject;
                _popup = go.GetComponent<Text>() ?? go.AddComponent<Text>();
            }

            _popup.font = CookingSimHud.UiFont();
            _popup.fontSize = 72;
            _popup.fontStyle = FontStyle.Bold;
            _popup.alignment = TextAnchor.MiddleCenter;
            _popup.horizontalOverflow = HorizontalWrapMode.Overflow;
            _popup.verticalOverflow = VerticalWrapMode.Overflow;
            _popup.raycastTarget = false;
            _popup.color = PerfectColor;

            _popupRt = _popup.rectTransform;
            _popupRt.anchorMin = new Vector2(0.5f, 0.5f);
            _popupRt.anchorMax = new Vector2(0.5f, 0.5f);
            _popupRt.pivot = new Vector2(0.5f, 0.5f);
            _popupRt.anchoredPosition = new Vector2(0f, 40f);
            _popupRt.sizeDelta = new Vector2(720f, 120f);

            _popupOutline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            _popupOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            _popupOutline.effectDistance = new Vector2(3f, -3f);
            _popupOutline.useGraphicAlpha = true;

            _popupGroup = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            _popupGroup.alpha = 0f;
            _popupGroup.blocksRaycasts = false;
            _popupGroup.interactable = false;
            go.SetActive(false);
        }

        public static string ToJapanese(HitJudgement j) => j switch
        {
            HitJudgement.Perfect => "パーフェクト",
            HitJudgement.Good => "グッド",
            _ => "ミス"
        };

        private static Color ColourFor(HitJudgement j) => j switch
        {
            HitJudgement.Perfect => PerfectColor,
            HitJudgement.Good => GoodColor,
            _ => MissColor
        };

        private static AudioClip BuildToneStack(string name, float[] freqs, float noteSeconds, float decay, float gain)
        {
            const int sampleRate = 44100;
            var gap = 0.012f;
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
                    var env = Mathf.Exp(-t * decay) * (1f - t / Mathf.Max(0.001f, noteSeconds));
                    data[cursor + i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * env * gain;
                }

                cursor += len + Mathf.CeilToInt(gap * sampleRate);
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildNoiseThud(string name, float duration, float toneHz, float gain)
        {
            const int sampleRate = 44100;
            var samples = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var env = Mathf.Exp(-t * 28f);
                var tone = Mathf.Sin(2f * Mathf.PI * toneHz * t);
                var noise = (Random.value * 2f - 1f) * 0.35f;
                data[i] = (tone * 0.65f + noise) * env * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
