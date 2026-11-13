using System;
using UnityEngine;

namespace BackbeatDiner.Rhythm
{
    /// <summary>
    /// BPM clock scheduled against <see cref="AudioSettings.dspTime"/>.
    /// Fires <see cref="OnBeat"/> when each beat becomes due.
    /// </summary>
    public sealed class RhythmClock : MonoBehaviour
    {
        [SerializeField] private float bpm = 120f;
        [SerializeField] private AudioSource metronomeSource;
        [SerializeField] private AudioSource songSource;
        [SerializeField] private AudioClip metronomeClick;
        [SerializeField] private bool playMetronomeClick = true;
        [SerializeField] private double scheduleLookAhead = 0.08;

        private double _nextBeatDspTime;
        private double _startDspTime;
        private int _beatIndex;
        private bool _running;
        private ChartDefinition _chart;
        private AudioClip _songClip;

        public float Bpm
        {
            get => bpm;
            set
            {
                bpm = Mathf.Max(1f, value);
                if (_running)
                {
                    // Keep schedule continuous; next interval uses new BPM.
                }
            }
        }

        public double SecondsPerBeat => 60.0 / Mathf.Max(1f, bpm);

        public int BeatIndex => _beatIndex;

        public double NextBeatDspTime => _nextBeatDspTime;

        /// <summary>DSP time of beat 0 when the clock was last started.</summary>
        public double StartDspTime => _startDspTime;

        public bool IsRunning => _running;

        public ChartDefinition Chart => _chart;

        /// <summary>Apply BPM / optional song clip (and retain reference) from a <see cref="ChartDefinition"/>.</summary>
        public void ApplyChart(ChartDefinition chart)
        {
            StopSong();
            _chart = chart;
            _songClip = chart != null ? chart.SongClip : null;
            if (chart == null)
            {
                return;
            }

            Bpm = chart.Bpm;
        }

        /// <summary>Absolute DSP time for a beat index relative to <see cref="StartDspTime"/>.</summary>
        public double BeatToDspTime(float beatTime)
        {
            return _startDspTime + beatTime * SecondsPerBeat;
        }

        /// <summary>Args: beatIndex, scheduledDspTime.</summary>
        public event Action<int, double> OnBeat;

        private void Awake()
        {
            EnsureAudioSources();

            if (metronomeClick == null)
            {
                metronomeClick = Resources.Load<AudioClip>("MetronomeClick");
            }
        }

        private void EnsureAudioSources()
        {
            if (metronomeSource == null)
            {
                metronomeSource = gameObject.GetComponent<AudioSource>();
                if (metronomeSource == null)
                {
                    metronomeSource = gameObject.AddComponent<AudioSource>();
                }
            }

            metronomeSource.playOnAwake = false;
            metronomeSource.spatialBlend = 0f;

            if (songSource == null)
            {
                songSource = gameObject.AddComponent<AudioSource>();
            }

            songSource.playOnAwake = false;
            songSource.spatialBlend = 0f;
            songSource.loop = false;
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            var now = AudioSettings.dspTime;
            while (_running && _nextBeatDspTime <= now + scheduleLookAhead)
            {
                FireBeat(_beatIndex, _nextBeatDspTime);
                if (!_running)
                {
                    break;
                }

                _beatIndex++;
                _nextBeatDspTime += SecondsPerBeat;
            }
        }

        public void StartClock(double startDelaySeconds = 0.25)
        {
            EnsureAudioSources();
            _beatIndex = 0;
            _startDspTime = AudioSettings.dspTime + Math.Max(0.05, startDelaySeconds);
            _nextBeatDspTime = _startDspTime;
            _running = true;
            ScheduleSongAtStart();
            var chartName = _chart != null ? _chart.name : "(no chart)";
            var songName = _songClip != null ? _songClip.name : "(no song)";
            Debug.Log(
                $"[RhythmClock] Started @ {bpm} BPM, chart={chartName}, song={songName}, " +
                $"first beat dsp={_nextBeatDspTime:F3}");
        }

        public void StopClock()
        {
            _running = false;
            StopSong();
        }

        public double GetNearestBeatDspTime(double dspTime)
        {
            if (!_running)
            {
                return dspTime;
            }

            var spb = SecondsPerBeat;
            var first = _nextBeatDspTime - (_beatIndex > 0 ? spb : 0.0);
            // Walk relative to upcoming beat.
            var prev = _nextBeatDspTime - spb;
            var next = _nextBeatDspTime;
            return Math.Abs(dspTime - prev) <= Math.Abs(dspTime - next) ? prev : next;
        }

        /// <summary>Signed delta (input − beat) in seconds for the nearest scheduled beat.</summary>
        public double GetDeltaToNearestBeat(double inputDspTime)
        {
            var beat = GetNearestBeatDspTime(inputDspTime);
            return inputDspTime - beat;
        }

        private void FireBeat(int index, double scheduledDsp)
        {
            // Prefer chart song bed over metronome clicks when a clip is assigned.
            if (playMetronomeClick && _songClip == null && metronomeSource != null && metronomeClick != null)
            {
                metronomeSource.PlayOneShot(metronomeClick, 0.7f);
            }

            OnBeat?.Invoke(index, scheduledDsp);
        }

        private void ScheduleSongAtStart()
        {
            if (songSource == null || _songClip == null)
            {
                return;
            }

            songSource.Stop();
            songSource.clip = _songClip;
            songSource.volume = 0.85f;
            songSource.PlayScheduled(_startDspTime);
        }

        private void StopSong()
        {
            if (songSource == null)
            {
                return;
            }

            songSource.Stop();
            songSource.clip = null;
        }

        public void AssignMetronomeClip(AudioClip clip)
        {
            metronomeClick = clip;
        }
    }
}
