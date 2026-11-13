using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BackbeatDiner.Rhythm
{
    /// <summary>
    /// Space / gamepad South hit input with judge feedback (Debug.Log + on-screen text).
    /// </summary>
    public sealed class HitInput : MonoBehaviour
    {
        [SerializeField] private RhythmClock clock;
        [SerializeField] private NoteHighway highway;
        [SerializeField] private JudgeWindow window = JudgeWindow.Default;
        [SerializeField] private Text judgementText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text bpmText;
        [SerializeField] private bool inputEnabled = true;

        private int _combo;
        private int _score;

        /// <summary>Args: judgement, signed delta seconds, combo, lane score.</summary>
        public event Action<HitJudgement, double, int, int> OnJudged;

        public JudgeWindow Window
        {
            get => window;
            set => window = value;
        }

        public bool InputEnabled
        {
            get => inputEnabled;
            set => inputEnabled = value;
        }

        public int Combo => _combo;

        public int Score => _score;

        public void Configure(RhythmClock rhythmClock, NoteHighway noteHighway, Text judgement, Text combo, Text bpm)
        {
            clock = rhythmClock;
            highway = noteHighway;
            judgementText = judgement;
            comboText = combo;
            bpmText = bpm;
        }

        private void Update()
        {
            if (inputEnabled && WasHitPressed())
            {
                ResolveHit();
            }

            RefreshHud();
        }

        private static bool WasHitPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private void ResolveHit()
        {
            if (clock == null || !clock.IsRunning)
            {
                return;
            }

            var now = AudioSettings.dspTime;
            HitJudgement judgement;
            double delta;

            if (highway != null &&
                highway.TryConsumeNearest(now, window.GoodMs, out var targetDsp, out _))
            {
                delta = now - targetDsp;
                judgement = window.Evaluate(delta);
            }
            else
            {
                delta = clock.GetDeltaToNearestBeat(now);
                judgement = window.Evaluate(delta);
                // Empty lane miss if far from beat.
                if (judgement == HitJudgement.Miss)
                {
                    // keep Miss
                }
            }

            if (judgement == HitJudgement.Miss)
            {
                _combo = 0;
            }
            else
            {
                _combo++;
                _score += judgement == HitJudgement.Perfect ? 100 : 50;
            }

            var msg = $"[HitInput] {judgement} Δ={delta * 1000.0:F1}ms combo={_combo} score={_score}";
            Debug.Log(msg);
            if (judgementText != null)
            {
                judgementText.text = $"判定: {ToJapanese(judgement)} ({delta * 1000.0:F0}ms)";
            }

            OnJudged?.Invoke(judgement, delta, _combo, _score);
        }

        private void RefreshHud()
        {
            if (comboText != null)
            {
                comboText.text = $"コンボ: {_combo}";
            }

            if (bpmText != null && clock != null)
            {
                bpmText.text = $"BPM: {clock.Bpm:0}";
            }
        }

        private static string ToJapanese(HitJudgement j) => j switch
        {
            HitJudgement.Perfect => "Perfect",
            HitJudgement.Good => "Good",
            _ => "Miss"
        };
    }
}
