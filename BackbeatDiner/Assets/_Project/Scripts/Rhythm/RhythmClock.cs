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
        [SerializeField] private AudioClip metronomeClick;
        [SerializeField] private bool playMetronomeClick = true;
        [SerializeField] private double scheduleLookAhead = 0.08;

        private double _nextBeatDspTime;
        private int _beatIndex;
        private bool _running;

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

        public bool IsRunning => _running;

        /// <summary>Args: beatIndex, scheduledDspTime.</summary>
        public event Action<int, double> OnBeat;

        private void Awake()
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

            if (metronomeClick == null)
            {
                metronomeClick = Resources.Load<AudioClip>("MetronomeClick");
            }
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            var now = AudioSettings.dspTime;
            while (_nextBeatDspTime <= now + scheduleLookAhead)
            {
                FireBeat(_beatIndex, _nextBeatDspTime);
                _beatIndex++;
                _nextBeatDspTime += SecondsPerBeat;
            }
        }

        public void StartClock(double startDelaySeconds = 0.25)
        {
            _beatIndex = 0;
            _nextBeatDspTime = AudioSettings.dspTime + Math.Max(0.05, startDelaySeconds);
            _running = true;
            Debug.Log($"[RhythmClock] Started @ {bpm} BPM, first beat dsp={_nextBeatDspTime:F3}");
        }

        public void StopClock()
        {
            _running = false;
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
            if (playMetronomeClick && metronomeSource != null && metronomeClick != null)
            {
                metronomeSource.PlayOneShot(metronomeClick, 0.7f);
            }

            OnBeat?.Invoke(index, scheduledDsp);
        }

        public void AssignMetronomeClip(AudioClip clip)
        {
            metronomeClick = clip;
        }
    }
}
