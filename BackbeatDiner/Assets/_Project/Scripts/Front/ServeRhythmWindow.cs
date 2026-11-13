using System;
using BackbeatDiner.Core;
using BackbeatDiner.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BackbeatDiner.Front
{
    /// <summary>
    /// F1+ / S1 serve rhythm (KG-5): after Instant Cook, open a short 1–2 bar
    /// judge window timed to <see cref="AudioSettings.dspTime"/> / <see cref="RhythmClock"/>.
    /// Perfect / Good / Miss scales tip (base sales always kept). While open, front
    /// comms send is locked via <see cref="FrontCommsSendLocked"/> (KG-3).
    /// </summary>
    public sealed class ServeRhythmWindow : MonoBehaviour
    {
        /// <summary>KG-3: table must not send beat lexicon / C2 while serving.</summary>
        public static bool FrontCommsSendLocked { get; private set; }

        [SerializeField] private RhythmClock clock;
        [SerializeField] private DaySessionState session;
        [SerializeField] private HitInput hitInput;
        [SerializeField] private InstantCookInput cookInput;
        [SerializeField] private Text hudLabel;
        [SerializeField] private JudgeWindow window = JudgeWindow.Default;
        [SerializeField] private int barCount = 2;
        [SerializeField] private float perfectTipMult = 1.5f;
        [SerializeField] private float goodTipMult = 1.0f;
        [SerializeField] private float missTipMult = 0f;

        private bool _open;
        private float _patienceRatio;
        private double _deadlineDsp;
        private double _openedDsp;
        private double _inputArmedDsp;
        private bool _hitResolved;
        private HitJudgement _best = HitJudgement.Miss;
        private double _bestAbsDelta = double.MaxValue;

        public bool IsOpen => _open;

        public event Action<HitJudgement, float> OnServeResolved;

        public void Configure(
            RhythmClock rhythmClock,
            DaySessionState daySession,
            HitInput backHit,
            InstantCookInput cook,
            Text label)
        {
            if (session != null)
            {
                session.OnSessionEnded -= HandleSessionEnded;
            }

            clock = rhythmClock;
            session = daySession;
            hitInput = backHit;
            cookInput = cook;
            hudLabel = label;

            if (session != null)
            {
                session.OnSessionEnded += HandleSessionEnded;
            }
        }

        private void HandleSessionEnded()
        {
            if (_open)
            {
                // Drop pending tip; prep already reserved. Clear KG-3 lock.
                _open = false;
                FrontCommsSendLocked = false;
            }
        }

        /// <summary>
        /// Begin S1 after Instant Cook validated prep + customer. Consumes prep immediately;
        /// tip portion is applied when the window resolves.
        /// </summary>
        public bool TryOpen(float patienceRatio)
        {
            if (_open || session == null || !session.IsRunning)
            {
                return false;
            }

            if (clock == null || !clock.IsRunning)
            {
                return false;
            }

            if (!session.TryReservePrepForServe())
            {
                return false;
            }

            _patienceRatio = Mathf.Clamp01(patienceRatio);
            _openedDsp = AudioSettings.dspTime;
            var beats = Mathf.Clamp(barCount, 1, 2) * 4;
            _deadlineDsp = _openedDsp + beats * clock.SecondsPerBeat;
            _hitResolved = false;
            _best = HitJudgement.Miss;
            _bestAbsDelta = double.MaxValue;
            _open = true;
            // Skip the Instant Cook press frame (F / West) so it does not count as S1.
            _inputArmedDsp = _openedDsp + 0.08;
            FrontCommsSendLocked = true;

            if (hitInput != null)
            {
                hitInput.InputEnabled = false;
            }

            if (cookInput != null)
            {
                cookInput.InputEnabled = false;
            }

            SetHud($"配膳リズム — Space / J·K で拍合わせ（{barCount}小節）", FrontHudController.Gold);
            Debug.Log($"[ServeRhythmWindow] Open bars={barCount} deadlineDsp={_deadlineDsp:F3} patience={_patienceRatio:0.00}");
            return true;
        }

        private void Update()
        {
            if (!_open)
            {
                return;
            }

            var now = AudioSettings.dspTime;
            if (!_hitResolved && now >= _inputArmedDsp && WasServeHitPressed())
            {
                ResolveHit(now);
            }

            if (now >= _deadlineDsp)
            {
                CloseWindow(_hitResolved ? _best : HitJudgement.Miss);
                return;
            }

            RefreshProgressHud(now);
        }

        private static bool WasServeHitPressed()
        {
            var kb = Keyboard.current;
            if (kb != null &&
                (kb.spaceKey.wasPressedThisFrame ||
                 kb.jKey.wasPressedThisFrame ||
                 kb.kKey.wasPressedThisFrame))
            {
                return true;
            }

            var pad = Gamepad.current;
            // East / West as 2-key stand-ins (South reserved for back lane when unlocked).
            if (pad != null &&
                (pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame))
            {
                return true;
            }

            return false;
        }

        private void ResolveHit(double now)
        {
            if (clock == null)
            {
                return;
            }

            var delta = clock.GetDeltaToNearestBeat(now);
            var judgement = window.Evaluate(delta);
            var abs = Math.Abs(delta);
            if (!_hitResolved || abs < _bestAbsDelta)
            {
                _bestAbsDelta = abs;
                _best = judgement;
            }

            _hitResolved = true;
            SetHud(
                $"配膳リズム: {ToJapanese(judgement)} ({delta * 1000.0:F0}ms) — 小節終了待ち",
                judgement == HitJudgement.Miss ? FrontHudController.Gold : FrontHudController.Teal);
            Debug.Log($"[ServeRhythmWindow] Hit {judgement} Δ={delta * 1000.0:F1}ms");

            // First non-miss can close early for snappy feel; Miss keeps the window open for a retry.
            if (judgement != HitJudgement.Miss)
            {
                CloseWindow(judgement);
            }
        }

        private void CloseWindow(HitJudgement judgement)
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            FrontCommsSendLocked = false;

            var tipMult = judgement switch
            {
                HitJudgement.Perfect => perfectTipMult,
                HitJudgement.Good => goodTipMult,
                _ => missTipMult
            };

            if (session != null)
            {
                session.CompleteServeWithRhythm(_patienceRatio, tipMult, judgement);
            }

            if (hitInput != null && session != null && session.IsRunning)
            {
                hitInput.InputEnabled = true;
            }

            if (cookInput != null && session != null && session.IsRunning)
            {
                cookInput.InputEnabled = true;
            }

            var tipNote = judgement == HitJudgement.Miss ? "チップなし（基本売上のみ）" : $"チップ×{tipMult:0.##}";
            SetHud($"配膳 {ToJapanese(judgement)} — {tipNote}", FrontHudController.Teal);
            Debug.Log($"[ServeRhythmWindow] Close {judgement} tipMult={tipMult:0.##} patience={_patienceRatio:0.00}");
            OnServeResolved?.Invoke(judgement, tipMult);
        }

        private void RefreshProgressHud(double now)
        {
            if (hudLabel == null || clock == null)
            {
                return;
            }

            var remain = Math.Max(0.0, _deadlineDsp - now);
            var beatsLeft = remain / clock.SecondsPerBeat;
            if (_hitResolved)
            {
                return;
            }

            hudLabel.text = $"配膳リズム — Space / J·K  残り {beatsLeft:0.0}拍";
            hudLabel.color = FrontHudController.Gold;
        }

        private void SetHud(string text, Color color)
        {
            if (hudLabel == null)
            {
                return;
            }

            hudLabel.text = text;
            hudLabel.color = color;
        }

        private static string ToJapanese(HitJudgement j) => j switch
        {
            HitJudgement.Perfect => "Perfect",
            HitJudgement.Good => "Good",
            _ => "Miss"
        };

        private void OnDisable()
        {
            if (_open)
            {
                FrontCommsSendLocked = false;
                _open = false;
            }

            if (session != null)
            {
                session.OnSessionEnded -= HandleSessionEnded;
            }
        }
    }
}
