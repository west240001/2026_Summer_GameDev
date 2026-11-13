using BackbeatDiner.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BackbeatDiner.Front
{
    /// <summary>
    /// v0 front mini: one-button Instant Cook — consumes 1 仕込み and serves a dummy customer.
    /// </summary>
    public sealed class InstantCookInput : MonoBehaviour
    {
        [SerializeField] private DaySessionState session;
        [SerializeField] private DummyCustomerQueue queue;
        [SerializeField] private Text feedbackLabel;
        [SerializeField] private bool inputEnabled = true;

        private float _flashUntil;

        public bool InputEnabled
        {
            get => inputEnabled;
            set => inputEnabled = value;
        }

        public void Configure(DaySessionState daySession, DummyCustomerQueue customerQueue, Text feedback)
        {
            session = daySession;
            queue = customerQueue;
            feedbackLabel = feedback;
        }

        private void Update()
        {
            if (session != null && session.IsEnded)
            {
                inputEnabled = false;
            }

            if (inputEnabled && WasCookPressed())
            {
                TryInstantCook();
            }

            if (feedbackLabel != null && Time.time > _flashUntil && session != null && !session.IsEnded)
            {
                feedbackLabel.text = "F / Enter = インスタントクック（提供）";
                feedbackLabel.color = FrontHudController.Gold;
            }
        }

        private static bool WasCookPressed()
        {
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

            if (queue == null || !queue.TryServeOldest(out var patience))
            {
                Flash("客待ちなし", false);
                return;
            }

            if (!session.TryConsumePrepForServe(patience))
            {
                Flash("仕込み不足", false);
                return;
            }

            Flash($"提供成功 +{100 + Mathf.RoundToInt(patience * 50f)}", true);
            Debug.Log($"[InstantCook] Serve ok patience={patience:0.00} prepLeft={session.PrepUnits}");
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
