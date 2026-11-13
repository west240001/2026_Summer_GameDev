using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Front
{
    /// <summary>
    /// Week2 front HUD shell: 客待ち / 在庫 / クォータ / CD (Japanese labels).
    /// </summary>
    public sealed class FrontHudController : MonoBehaviour
    {
        public static readonly Color Gold = new Color(0.7882353f, 0.6352941f, 0.15294118f); // #C9A227
        public static readonly Color Teal = new Color(0.101960784f, 0.419607843f, 0.419607843f); // #1A6B6B

        [SerializeField] private Text waitingLabel;
        [SerializeField] private Text inventoryLabel;
        [SerializeField] private Text quotaLabel;
        [SerializeField] private Text cooldownLabel;
        [SerializeField] private Image quotaFill;
        [SerializeField] private Image cooldownFill;

        [SerializeField] private float quotaTarget = 8f;
        [SerializeField] private float quotaCurrent = 3f;
        [SerializeField] private float cooldownSeconds = 4.5f;

        private float _cooldownRemaining;
        private InventoryBar _inventory;
        private DummyCustomerQueue _queue;

        public void Configure(InventoryBar inventory, DummyCustomerQueue queue)
        {
            _inventory = inventory;
            _queue = queue;
            _cooldownRemaining = cooldownSeconds;
            Refresh();
        }

        private void Update()
        {
            _cooldownRemaining -= Time.deltaTime;
            if (_cooldownRemaining <= 0f)
            {
                _cooldownRemaining = cooldownSeconds;
                quotaCurrent = Mathf.Min(quotaTarget, quotaCurrent + 1f);
            }

            Refresh();
        }

        public void Refresh()
        {
            var waiting = _queue != null ? _queue.ActiveCount : 0;
            if (waitingLabel != null)
            {
                waitingLabel.text = $"客待ち: {waiting}";
            }

            if (inventoryLabel != null)
            {
                inventoryLabel.text = _inventory != null ? _inventory.SummaryText : "在庫: —";
            }

            if (quotaLabel != null)
            {
                quotaLabel.text = $"クォータ: {quotaCurrent:0}/{quotaTarget:0}";
            }

            if (quotaFill != null)
            {
                quotaFill.fillAmount = quotaTarget > 0f ? Mathf.Clamp01(quotaCurrent / quotaTarget) : 0f;
            }

            if (cooldownLabel != null)
            {
                cooldownLabel.text = $"CD: {_cooldownRemaining:0.0}s";
            }

            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = cooldownSeconds > 0f
                    ? Mathf.Clamp01(_cooldownRemaining / cooldownSeconds)
                    : 0f;
            }
        }

        public void BindLabels(Text waiting, Text inventory, Text quota, Text cooldown, Image quotaBar, Image cdBar)
        {
            waitingLabel = waiting;
            inventoryLabel = inventory;
            quotaLabel = quota;
            cooldownLabel = cooldown;
            quotaFill = quotaBar;
            cooldownFill = cdBar;
        }
    }
}
