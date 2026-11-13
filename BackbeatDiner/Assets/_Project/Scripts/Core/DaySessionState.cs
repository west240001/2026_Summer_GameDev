using System;
using BackbeatDiner.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Shared day-session resource counter for the vertical slice:
    /// back Perfect/Good → 仕込み (prepUnits); front Instant Cook/serve → 提供.
    /// Ends after ~32 beats or 30 seconds (or setlist Chart B); shows 営業終了 with 売上 / 提供
    /// and fires <see cref="OnSessionEnded"/> for ResultOverlay → TitleMenu.
    /// </summary>
    public sealed class DaySessionState : MonoBehaviour
    {
        public static readonly Color Gold = new Color(0.7882353f, 0.6352941f, 0.15294118f);
        public static readonly Color Teal = new Color(0.101960784f, 0.419607843f, 0.419607843f);

        [SerializeField] private int maxBeats = 32;
        [SerializeField] private float maxSeconds = 30f;
        [SerializeField] private int perfectPrep = 2;
        [SerializeField] private int goodPrep = 1;
        [SerializeField] private bool endSessionOnChartComplete = true;

        private RhythmClock _clock;
        private HitInput _hitInput;
        private Text _prepLabel;
        private Text _serveLabel;
        private Text _timerLabel;
        private Text _resultTitle;
        private Text _resultBody;
        private GameObject _resultRoot;
        private Image _sessionFill;
        private Image _prepFill;
        private Image _serveFill;

        private int _prepUnits;
        private int _servedCount;
        private int _salesScore;
        private float _elapsed;
        private bool _ended;
        private bool _running;
        private bool _chartCompleteLatched;
        private bool _networkFollower;
        private string _endReason = string.Empty;

        public int PrepUnits => _prepUnits;
        public int ServedCount => _servedCount;
        public int SalesScore => _salesScore;
        public bool IsEnded => _ended;
        public bool IsRunning => _running && !_ended;
        public int MaxBeats => maxBeats;
        public string EndReason => _endReason;

        public event Action OnSessionEnded;

        /// <summary>
        /// Fired when the current chart's beat budget is exhausted.
        /// When <see cref="endSessionOnChartComplete"/> is false (setlist mode),
        /// the day continues until <see cref="EndDay"/>.
        /// </summary>
        public event Action OnChartCompleted;

        /// <summary>Apply chart length (beats) from a <see cref="ChartDefinition"/>.</summary>
        public void ApplyChart(ChartDefinition chart)
        {
            if (chart == null)
            {
                return;
            }

            maxBeats = Mathf.Max(1, Mathf.RoundToInt(chart.LengthBeats));
            _chartCompleteLatched = false;
        }

        /// <summary>
        /// When false, chart completion only stops the clock / hit input and raises
        /// <see cref="OnChartCompleted"/> (used by <see cref="DaySetlistRunner"/>).
        /// </summary>
        public void SetEndSessionOnChartComplete(bool value)
        {
            endSessionOnChartComplete = value;
        }

        /// <summary>
        /// Netcode client: skip local beat-budget chart completion (host drives chart/gap via sync).
        /// </summary>
        public void SetNetworkFollower(bool follower)
        {
            _networkFollower = follower;
        }

        /// <summary>Expand day time budget for a multi-chart setlist (+ buffer).</summary>
        public void ApplySetlistBudget(SetlistDefinition setlist)
        {
            if (setlist == null)
            {
                return;
            }

            var charts = setlist.Charts;
            var total = 0f;
            var valid = 0;
            for (var i = 0; i < charts.Length; i++)
            {
                var c = charts[i];
                if (c == null)
                {
                    continue;
                }

                total += (c.LengthBeats / c.Bpm) * 60f;
                valid++;
            }

            if (valid > 1)
            {
                total += setlist.GapSeconds * (valid - 1);
            }

            maxSeconds = Mathf.Max(maxSeconds, total + 20f);
        }

        /// <summary>End the day from setlist / external flow (shows 営業終了 overlay).</summary>
        public void EndDay(string reason)
        {
            EndSession(string.IsNullOrWhiteSpace(reason) ? "セットリスト終了" : reason);
        }

        public void Bind(
            RhythmClock clock,
            HitInput hitInput,
            Text prepLabel,
            Text serveLabel,
            Text timerLabel,
            GameObject resultRoot,
            Text resultTitle,
            Text resultBody,
            Image sessionFill = null,
            Image prepFill = null,
            Image serveFill = null)
        {
            Unhook();
            _clock = clock;
            _hitInput = hitInput;
            _prepLabel = prepLabel;
            _serveLabel = serveLabel;
            _timerLabel = timerLabel;
            _resultRoot = resultRoot;
            _resultTitle = resultTitle;
            _resultBody = resultBody;
            _sessionFill = sessionFill;
            _prepFill = prepFill;
            _serveFill = serveFill;

            if (_hitInput != null)
            {
                _hitInput.OnJudged += HandleJudged;
            }

            if (_clock != null)
            {
                _clock.OnBeat += HandleBeat;
            }

            if (_resultRoot != null)
            {
                _resultRoot.SetActive(false);
            }

            RefreshHud();
        }

        public void Begin()
        {
            _prepUnits = 0;
            _servedCount = 0;
            _salesScore = 0;
            _elapsed = 0f;
            _ended = false;
            _running = true;
            _chartCompleteLatched = false;
            _endReason = string.Empty;
            if (_resultRoot != null)
            {
                _resultRoot.SetActive(false);
            }

            if (_hitInput != null)
            {
                _hitInput.InputEnabled = true;
            }

            RefreshHud();
        }

        private void Update()
        {
            if (!_running || _ended)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            RefreshHud();

            if (_elapsed >= maxSeconds)
            {
                EndSession("時間切れ");
            }
        }

        /// <summary>
        /// Instant Cook path without rhythm (legacy / FrontShell). Base 100 + patience tip.
        /// </summary>
        public bool TryConsumePrepForServe(float patienceRatio)
        {
            return TryConsumePrepForServe(patienceRatio, tipMultiplier: 1f, HitJudgement.Good);
        }

        /// <summary>Reserve 1 prep unit at Instant Cook start; tip applied in <see cref="CompleteServeWithRhythm"/>.</summary>
        public bool TryReservePrepForServe()
        {
            if (_ended || !_running || _prepUnits <= 0)
            {
                return false;
            }

            _prepUnits--;
            RefreshHud();
            return true;
        }

        /// <summary>
        /// Finish a reserved serve after F1+ S1. Base sales always kept; tip = patience×50×mult
        /// (Miss → mult 0 = tip only missing per KG-2).
        /// </summary>
        public void CompleteServeWithRhythm(float patienceRatio, float tipMultiplier, HitJudgement serveJudgement)
        {
            if (_ended)
            {
                return;
            }

            _servedCount++;
            var tip = Mathf.RoundToInt(Mathf.Clamp01(patienceRatio) * 50f * Mathf.Max(0f, tipMultiplier));
            _salesScore += 100 + tip;
            RefreshHud();
            Debug.Log($"[DaySessionState] Serve+rhythm {serveJudgement} tip={tip} mult={tipMultiplier:0.##} sales={_salesScore}");
        }

        public bool TryConsumePrepForServe(float patienceRatio, float tipMultiplier, HitJudgement serveJudgement)
        {
            if (_ended || !_running || _prepUnits <= 0)
            {
                return false;
            }

            _prepUnits--;
            _servedCount++;
            // Base 100 always; tip scaled by rhythm (Miss → 0 tip).
            var tip = Mathf.RoundToInt(Mathf.Clamp01(patienceRatio) * 50f * Mathf.Max(0f, tipMultiplier));
            _salesScore += 100 + tip;
            RefreshHud();
            _ = serveJudgement;
            return true;
        }

        public void AddPrepFromJudgement(HitJudgement judgement)
        {
            if (_ended || !_running)
            {
                return;
            }

            if (judgement == HitJudgement.Perfect)
            {
                _prepUnits += perfectPrep;
            }
            else if (judgement == HitJudgement.Good)
            {
                _prepUnits += goodPrep;
            }

            RefreshHud();
        }

        /// <summary>Netcode client mirror: absolute prep from host NetworkVariable.</summary>
        public void ApplyNetworkPrepUnits(int prepUnits)
        {
            _prepUnits = Mathf.Max(0, prepUnits);
            RefreshHud();
        }

        /// <summary>Netcode client mirror: absolute sales from host NetworkVariable.</summary>
        public void ApplyNetworkSalesScore(int salesScore)
        {
            _salesScore = Mathf.Max(0, salesScore);
            RefreshHud();
        }

        /// <summary>Netcode client mirror: absolute serve count from host NetworkVariable.</summary>
        public void ApplyNetworkServedCount(int servedCount)
        {
            _servedCount = Mathf.Max(0, servedCount);
            RefreshHud();
        }

        private void HandleJudged(HitJudgement judgement, double delta, int combo, int laneScore)
        {
            AddPrepFromJudgement(judgement);
            _ = delta;
            _ = combo;
            _ = laneScore;
        }

        private void HandleBeat(int beatIndex, double scheduledDsp)
        {
            _ = scheduledDsp;
            if (_networkFollower)
            {
                return;
            }

            // BeatIndex after FireBeat has already advanced; fire uses the index being fired.
            // Complete chart when the fired beat reaches maxBeats-1 (0-based → 32nd beat is index 31).
            if (!_ended && beatIndex + 1 >= maxBeats)
            {
                CompleteCurrentChart();
            }
        }

        private void CompleteCurrentChart()
        {
            if (_ended || _chartCompleteLatched)
            {
                return;
            }

            _chartCompleteLatched = true;
            if (_clock != null)
            {
                _clock.StopClock();
            }

            if (_hitInput != null)
            {
                _hitInput.InputEnabled = false;
            }

            Debug.Log($"[DaySessionState] Chart complete (beats={maxBeats}) endSession={endSessionOnChartComplete}");
            OnChartCompleted?.Invoke();

            if (endSessionOnChartComplete)
            {
                EndSession("チャート終了");
            }
        }

        private void EndSession(string reason)
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            _running = false;
            _endReason = string.IsNullOrWhiteSpace(reason) ? "営業終了" : reason.Trim();
            if (_clock != null)
            {
                _clock.StopClock();
            }

            if (_hitInput != null)
            {
                _hitInput.InputEnabled = false;
            }

            if (_resultRoot != null)
            {
                _resultRoot.SetActive(true);
            }

            if (_resultTitle != null)
            {
                _resultTitle.text = "営業終了";
            }

            // Fallback body when ResultOverlayController is absent; polished overlay overwrites stats.
            if (_resultBody != null)
            {
                _resultBody.text =
                    $"売上 {_salesScore}　·　提供 {_servedCount}\n" +
                    $"残仕込み {_prepUnits}　·　{reason}";
            }

            Debug.Log($"[DaySessionState] End ({reason}) sales={_salesScore} served={_servedCount} prepLeft={_prepUnits}");
            OnSessionEnded?.Invoke();
        }

        private void RefreshHud()
        {
            if (_prepLabel != null)
            {
                _prepLabel.text = $"仕込み: {_prepUnits}";
            }

            if (_serveLabel != null)
            {
                _serveLabel.text = $"提供: {_servedCount}";
            }

            if (_timerLabel != null)
            {
                var remain = Mathf.Max(0f, maxSeconds - _elapsed);
                var beat = _clock != null ? _clock.BeatIndex : 0;
                _timerLabel.text = $"残り {remain:0.0}s / ビート {beat}/{maxBeats}";
            }

            if (_sessionFill != null)
            {
                var timeRatio = maxSeconds > 0f ? Mathf.Clamp01(1f - _elapsed / maxSeconds) : 0f;
                var beat = _clock != null ? _clock.BeatIndex : 0;
                var beatRatio = maxBeats > 0 ? Mathf.Clamp01(1f - beat / (float)maxBeats) : 0f;
                _sessionFill.fillAmount = Mathf.Min(timeRatio, beatRatio);
                _sessionFill.color = _sessionFill.fillAmount < 0.25f ? Gold : Teal;
            }

            if (_prepFill != null)
            {
                _prepFill.fillAmount = Mathf.Clamp01(_prepUnits / 8f);
                _prepFill.color = _prepUnits > 0 ? Gold : Teal * 0.55f;
            }

            if (_serveFill != null)
            {
                _serveFill.fillAmount = Mathf.Clamp01(_servedCount / 6f);
                _serveFill.color = Teal;
            }
        }

        private void OnDestroy()
        {
            Unhook();
        }

        private void Unhook()
        {
            if (_hitInput != null)
            {
                _hitInput.OnJudged -= HandleJudged;
            }

            if (_clock != null)
            {
                _clock.OnBeat -= HandleBeat;
            }
        }
    }
}
