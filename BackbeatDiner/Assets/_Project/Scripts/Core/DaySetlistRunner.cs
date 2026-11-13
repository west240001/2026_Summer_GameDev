using System.Collections;
using BackbeatDiner.Coop;
using BackbeatDiner.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Plays a <see cref="SetlistDefinition"/> on the vertical slice:
    /// Chart A → kitchen gap → Chart B → … then ends the day session.
    /// Japanese HUD shows チャート名 (and 間隙 during gaps).
    /// Gap opens <see cref="ChartGapCoopWindow"/> (スナップ推奨 / Beat-B3).
    /// </summary>
    public sealed class DaySetlistRunner : MonoBehaviour
    {
        [SerializeField] private SetlistDefinition setlist;

        private RhythmClock _clock;
        private NoteHighway _highway;
        private DaySessionState _session;
        private HitInput _hitInput;
        private Text _chartNameLabel;
        private ChartGapCoopWindow _coopWindow;

        private int _index;
        private bool _inGap;
        private bool _started;
        private Coroutine _gapRoutine;

        public SetlistDefinition Setlist => setlist;

        public int CurrentIndex => _index;

        public bool IsInGap => _inGap;

        public void Configure(
            SetlistDefinition definition,
            RhythmClock rhythmClock,
            NoteHighway noteHighway,
            DaySessionState daySession,
            HitInput hit,
            Text chartNameHud,
            ChartGapCoopWindow coopWindow = null)
        {
            setlist = definition;
            _clock = rhythmClock;
            _highway = noteHighway;
            _session = daySession;
            _hitInput = hit;
            _chartNameLabel = chartNameHud;
            _coopWindow = coopWindow;

            if (_session != null)
            {
                _session.SetEndSessionOnChartComplete(false);
                _session.OnChartCompleted -= HandleChartCompleted;
                _session.OnChartCompleted += HandleChartCompleted;
                _session.ApplySetlistBudget(setlist);
            }
        }

        /// <summary>Begin day: reset session counters and play first chart.</summary>
        public void StartSetlist(double startDelaySeconds = 0.35)
        {
            if (setlist == null || setlist.Charts.Length == 0)
            {
                Debug.LogWarning("[DaySetlistRunner] No setlist charts — skip.");
                return;
            }

            if (_gapRoutine != null)
            {
                StopCoroutine(_gapRoutine);
                _gapRoutine = null;
            }

            _coopWindow?.EndGap();

            _index = FirstValidIndex(0);
            if (_index < 0)
            {
                Debug.LogWarning("[DaySetlistRunner] Setlist has no valid charts.");
                return;
            }

            _inGap = false;
            _started = true;
            _session?.Begin();
            BeginCurrentChart(startDelaySeconds);
        }

        private void BeginCurrentChart(double startDelaySeconds)
        {
            var chart = setlist.Charts[_index];
            RefreshChartHud(chart, inGap: false);

            if (_clock != null)
            {
                _clock.StopClock();
                _clock.ApplyChart(chart);
            }

            _session?.ApplyChart(chart);

            if (_highway != null)
            {
                _highway.ClearActiveNotes();
                _highway.LoadChart(chart);
            }

            if (_hitInput != null)
            {
                _hitInput.InputEnabled = true;
            }

            _clock?.StartClock(startDelaySeconds);
            var total = CountValidCharts();
            var ordinal = OrdinalAmongValid(_index);
            Debug.Log(
                $"[DaySetlistRunner] Chart {ordinal}/{total}: {chart.DisplayName} " +
                $"(bpm={chart.Bpm}, beats={chart.LengthBeats})");
        }

        private void HandleChartCompleted()
        {
            if (!_started || _inGap || _session == null || _session.IsEnded)
            {
                return;
            }

            var next = FirstValidIndex(_index + 1);
            if (next < 0)
            {
                _session.EndDay("セットリスト終了");
                return;
            }

            _gapRoutine = StartCoroutine(GapThenNext(next));
        }

        private IEnumerator GapThenNext(int nextIndex)
        {
            _inGap = true;
            var nextChart = setlist.Charts[nextIndex];
            RefreshChartHud(nextChart, inGap: true);

            if (_hitInput != null)
            {
                _hitInput.InputEnabled = false;
            }

            _highway?.ClearActiveNotes();

            var gap = setlist.GapSeconds;
            Debug.Log($"[DaySetlistRunner] Gap {gap:0.#}s → next {nextChart.DisplayName}");
            _coopWindow?.BeginGap(gap, nextChart != null ? nextChart.DisplayName : null);
            if (gap > 0f)
            {
                yield return new WaitForSeconds(gap);
            }

            _coopWindow?.EndGap();
            _index = nextIndex;
            _inGap = false;
            _gapRoutine = null;
            BeginCurrentChart(0.25);
        }

        private void RefreshChartHud(ChartDefinition chart, bool inGap)
        {
            if (_chartNameLabel == null)
            {
                return;
            }

            var name = chart != null ? chart.DisplayName : "—";
            _chartNameLabel.text = inGap
                ? $"チャート名: 間隙 → {name}"
                : $"チャート名: {name}";
        }

        private int FirstValidIndex(int from)
        {
            var charts = setlist != null ? setlist.Charts : System.Array.Empty<ChartDefinition>();
            for (var i = Mathf.Max(0, from); i < charts.Length; i++)
            {
                if (charts[i] != null)
                {
                    return i;
                }
            }

            return -1;
        }

        private int CountValidCharts()
        {
            return setlist != null ? setlist.ChartCount : 0;
        }

        private int OrdinalAmongValid(int absoluteIndex)
        {
            var charts = setlist.Charts;
            var n = 0;
            for (var i = 0; i <= absoluteIndex && i < charts.Length; i++)
            {
                if (charts[i] != null)
                {
                    n++;
                }
            }

            return n;
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnChartCompleted -= HandleChartCompleted;
            }
        }
    }
}
