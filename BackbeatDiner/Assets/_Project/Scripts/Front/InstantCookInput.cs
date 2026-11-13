using BackbeatDiner.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BackbeatDiner.Front
{
    /// <summary>
    /// v0 front mini: Instant Cook press → opens F1+ <see cref="ServeRhythmWindow"/> (S1),
    /// then consumes 1 仕込み and serves a dummy customer with tip from rhythm judgement.
    /// Solo: F / Enter. Local 2P: Enter only (F reserved for Back Beat-B4).
    /// </summary>
    public sealed class InstantCookInput : MonoBehaviour
    {
        [SerializeField] private DaySessionState session;
        [SerializeField] private DummyCustomerQueue queue;
        [SerializeField] private ServeRhythmWindow serveRhythm;
        [SerializeField] private Text feedbackLabel;
        [SerializeField] private bool inputEnabled = true;
        [SerializeField] private bool localTwoPlayerSplit;

        private float _flashUntil;

        public bool InputEnabled
        {
            get => inputEnabled;
            set => inputEnabled = value;
        }

        public bool LocalTwoPlayerSplit
        {
            get => localTwoPlayerSplit;
            set => localTwoPlayerSplit = value;
        }

        public void Configure(
            DaySessionState daySession,
            DummyCustomerQueue customerQueue,
            Text feedback,
            ServeRhythmWindow rhythmWindow = null,
            bool twoPlayerSplit = false)
        {
            session = daySession;
            queue = customerQueue;
            feedbackLabel = feedback;
            serveRhythm = rhythmWindow;
            localTwoPlayerSplit = twoPlayerSplit;
        }

        private void Update()
        {
            if (session != null && session.IsEnded)
            {
                inputEnabled = false;
            }

            if (serveRhythm != null && serveRhythm.IsOpen)
            {
                return;
            }

            if (inputEnabled && WasCookPressed())
            {
                TryInstantCook();
            }

            if (feedbackLabel != null && Time.time > _flashUntil && session != null && !session.IsEnded)
            {
                feedbackLabel.text = localTwoPlayerSplit
                    ? LocalTwoPlayerBindings.HudFrontCookHint
                    : "F / Enter = インスタントクック → 配膳リズム";
                feedbackLabel.color = FrontHudController.Gold;
            }
        }

        private bool WasCookPressed()
        {
            if (localTwoPlayerSplit)
            {
                if (LocalTwoPlayerBindings.WasFrontCookPressed())
                {
                    return true;
                }

                var padSplit = Gamepad.current;
                return padSplit != null && padSplit.buttonWest.wasPressedThisFrame;
            }

            var kb = Keyboard.current;
            if (kb != null && (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }

            var pad = Gamepad.current;
            // West (X/□) — distinct from South hit on the back lane.
            if (pad != null && pad.buttonWest.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private void TryInstantCook()
        {
            if (session == null || !session.IsRunning)
            {
                Flash("営業外", false);
                return;
            }

            if (session.PrepUnits <= 0)
            {
                Flash("仕込み不足", false);
                return;
            }

            if (queue == null || queue.ActiveCount <= 0)
            {
                Flash("客待ちなし", false);
                return;
            }

            if (serveRhythm != null)
            {
                // Peek patience by serving only after rhythm window accepts the reservation.
                if (!queue.TryServeOldest(out var patience))
                {
                    Flash("客待ちなし", false);
                    return;
                }

                if (!serveRhythm.TryOpen(patience))
                {
                    Flash("配膳リズム開始失敗", false);
                    return;
                }

                Flash(localTwoPlayerSplit ? "配膳リズム！ J·K" : "配膳リズム！ Space / J·K", true);
                return;
            }

            // Fallback without rhythm window (e.g. FrontShell-only scenes).
            if (!queue.TryServeOldest(out var patienceFallback))
            {
                Flash("客待ちなし", false);
                return;
            }

            if (!session.TryConsumePrepForServe(patienceFallback))
            {
                Flash("仕込み不足", false);
                return;
            }

            Flash($"提供成功 +{100 + Mathf.RoundToInt(patienceFallback * 50f)}", true);
            Debug.Log($"[InstantCook] Serve ok patience={patienceFallback:0.00} prepLeft={session.PrepUnits}");
        }

        private void Flash(string msg, bool ok)
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.text = msg;
            feedbackLabel.color = ok ? FrontHudController.Teal : FrontHudController.Gold;
            _flashUntil = Time.time + 1.1f;
        }
    }
}
