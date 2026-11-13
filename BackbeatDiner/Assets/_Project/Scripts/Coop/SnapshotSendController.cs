using System;
using BackbeatDiner.Core;
using BackbeatDiner.Front;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BackbeatDiner.Coop
{
    /// <summary>
    /// KG-3 C2 snapshot send (表→裏): hold Tab 0.8s → send anim 1.2s → CD 25s.
    /// Japanese HUD. Preferred during chart gap. Blocked while <see cref="ServeRhythmWindow.FrontCommsSendLocked"/>.
    /// </summary>
    public sealed class SnapshotSendController : MonoBehaviour
    {
        public const float HoldSeconds = 0.8f;
        public const float SendAnimSeconds = 1.2f;
        public const float CooldownSeconds = 25f;

        /// <summary>True while front is holding or mid-send (locks cook + front beats).</summary>
        public static bool FrontSnapshotBusy { get; private set; }

        /// <summary>Raised when send anim completes with a frozen inventory/customer payload.</summary>
        public static event Action<SnapshotPayload> SnapshotSent;

        [SerializeField] private InventoryBar inventory;
        [SerializeField] private DummyCustomerQueue queue;
        [SerializeField] private ChartGapCoopWindow gapWindow;

        private Text _status;
        private Image _holdFill;
        private Image _cdFill;
        private Text _hint;
        private float _holdProgress;
        private float _sendLeft;
        private float _nextReadyTime;
        private bool _sending;
        private bool _gapPreferred;
        private SnapshotPayload _pending;

        public float RemainingCooldown => Mathf.Max(0f, _nextReadyTime - Time.unscaledTime);
        public bool IsReady => !_sending && RemainingCooldown <= 0f && !ServeRhythmWindow.FrontCommsSendLocked;
        public bool IsSending => _sending;

        public void Configure(InventoryBar inv, DummyCustomerQueue customers, ChartGapCoopWindow gap = null)
        {
            inventory = inv;
            queue = customers;
            gapWindow = gap;
            EnsureUi();
            RefreshHud();
        }

        /// <summary>Chart-gap coop: show スナップ推奨 cue (CD stays independent).</summary>
        public void SetGapPreferred(bool preferred)
        {
            _gapPreferred = preferred;
            RefreshHud();
            if (preferred && _hint != null)
            {
                _hint.color = FrontHudController.Teal;
            }
        }

        /// <summary>Optional gap boost: clear C2 CD so snap can fire in the coop window.</summary>
        public void UnlockCooldown()
        {
            _nextReadyTime = 0f;
            RefreshHud();
        }

        private void Start()
        {
            EnsureUi();
            RefreshHud();
        }

        private void OnDisable()
        {
            FrontSnapshotBusy = false;
        }

        private void Update()
        {
            if (_sending)
            {
                FrontSnapshotBusy = true;
                _sendLeft -= Time.unscaledDeltaTime;
                if (_status != null)
                {
                    _status.text = $"送信中… {_sendLeft:0.0}s";
                    _status.color = FrontHudController.Teal;
                }

                if (_holdFill != null)
                {
                    _holdFill.fillAmount = Mathf.Clamp01(_sendLeft / SendAnimSeconds);
                    _holdFill.color = FrontHudController.Teal;
                }

                if (_sendLeft <= 0f)
                {
                    CompleteSend();
                }

                RefreshCdHud();
                return;
            }

            var locked = ServeRhythmWindow.FrontCommsSendLocked;
            var onCd = RemainingCooldown > 0f;
            var holding = !locked && !onCd && IsHoldPressed();

            if (holding)
            {
                _holdProgress += Time.unscaledDeltaTime;
                FrontSnapshotBusy = true;
                if (_status != null)
                {
                    _status.text = $"ホールド {_holdProgress:0.0}/{HoldSeconds:0.0}s";
                    _status.color = FrontHudController.Gold;
                }

                if (_holdFill != null)
                {
                    _holdFill.fillAmount = Mathf.Clamp01(_holdProgress / HoldSeconds);
                    _holdFill.color = FrontHudController.Gold;
                }

                if (_holdProgress >= HoldSeconds)
                {
                    BeginSend();
                }
            }
            else
            {
                if (_holdProgress > 0f && !_sending)
                {
                    _holdProgress = Mathf.MoveTowards(_holdProgress, 0f, Time.unscaledDeltaTime * 2.5f);
                }

                FrontSnapshotBusy = false;
                if (_holdFill != null)
                {
                    _holdFill.fillAmount = Mathf.Clamp01(_holdProgress / HoldSeconds);
                }

                RefreshHud();
            }

            if (gapWindow != null && gapWindow.IsActive != _gapPreferred)
            {
                SetGapPreferred(gapWindow.IsActive);
            }

            RefreshCdHud();
        }

        private static bool IsHoldPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.isPressed)
            {
                return true;
            }

            // Optional pad: Left Stick click as alternate hold (local 2P front).
            var pad = Gamepad.current;
            return pad != null && pad.leftStickButton.isPressed;
        }

        private void BeginSend()
        {
            _holdProgress = 0f;
            _sending = true;
            _sendLeft = SendAnimSeconds;
            FrontSnapshotBusy = true;
            _pending = SnapshotPayload.Capture(inventory, queue, _gapPreferred);
            Debug.Log("[SnapshotSend] Hold complete — send anim 1.2s");
            if (_status != null)
            {
                _status.text = $"送信中… {SendAnimSeconds:0.0}s";
                _status.color = FrontHudController.Teal;
            }
        }

        private void CompleteSend()
        {
            _sending = false;
            _sendLeft = 0f;
            FrontSnapshotBusy = false;
            _nextReadyTime = Time.unscaledTime + CooldownSeconds;
            var payload = _pending;
            Debug.Log(
                $"[SnapshotSend] C2 snapshot sent — customers={payload.ActiveCustomers} stock={payload.StockSummary}");
            SnapshotSent?.Invoke(payload);
            if (_status != null)
            {
                _status.text = "スナップ送信完了";
                _status.color = FrontHudController.Teal;
            }

            RefreshHud();
        }

        /// <summary>Netcode: deliver C2 payload to the other peer (Back Snap-In panel).</summary>
        public static void RaiseRemoteSnapshot(SnapshotPayload payload)
        {
            SnapshotSent?.Invoke(payload);
        }

        private void RefreshHud()
        {
            if (_status == null)
            {
                return;
            }

            if (_sending)
            {
                return;
            }

            if (ServeRhythmWindow.FrontCommsSendLocked)
            {
                _status.text = "スナップ: 配膳ロック";
                _status.color = FrontHudController.Gold;
            }
            else if (RemainingCooldown > 0f)
            {
                _status.text = $"スナップ CD {RemainingCooldown:0.0}s";
                _status.color = FrontHudController.Gold;
            }
            else if (_gapPreferred)
            {
                _status.text = "スナップ推奨（間隙）— Tab 長押し 0.8s";
                _status.color = FrontHudController.Teal;
            }
            else
            {
                _status.text = "Tab 長押し 0.8s → スナップ送信";
                _status.color = FrontHudController.Gold;
            }

            if (_hint != null)
            {
                _hint.text = _gapPreferred
                    ? "協力間隙 — C2 スナップショット好機"
                    : "C2 スナップショット（CD 25s）";
                _hint.color = _gapPreferred ? FrontHudController.Teal : FrontHudController.Gold;
            }
        }

        private void RefreshCdHud()
        {
            if (_cdFill == null)
            {
                return;
            }

            var remain = RemainingCooldown;
            _cdFill.fillAmount = _sending
                ? 1f
                : (CooldownSeconds > 0f ? Mathf.Clamp01(remain / CooldownSeconds) : 0f);
            _cdFill.color = _sending || remain > 0f
                ? FrontHudController.Gold
                : FrontHudController.Teal;
        }

        private void EnsureUi()
        {
            if (_status != null)
            {
                return;
            }

            var canvasGo = GameObject.Find("VerticalSliceHUD");
            if (canvasGo == null)
            {
                canvasGo = GameObject.Find("SnapshotSendHUD");
                if (canvasGo == null)
                {
                    canvasGo = new GameObject("SnapshotSendHUD");
                    var canvas = canvasGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 26;
                    canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            var root = canvasGo.transform.Find("SnapshotSendHud");
            GameObject rootGo;
            Image bg;
            if (root == null)
            {
                rootGo = new GameObject("SnapshotSendHud");
                rootGo.transform.SetParent(canvasGo.transform, false);
                bg = rootGo.AddComponent<Image>();
            }
            else
            {
                rootGo = root.gameObject;
                bg = rootGo.GetComponent<Image>() ?? rootGo.AddComponent<Image>();
            }

            bg.color = CookingSimHud.PanelBg;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-18f, -680f);
            rt.sizeDelta = new Vector2(460f, 120f);
            var outline = rootGo.GetComponent<Outline>() ?? rootGo.AddComponent<Outline>();
            outline.effectColor = CookingSimHud.BorderGold;
            outline.effectDistance = new Vector2(2f, -2f);

            _hint = EnsureLabel(rootGo.transform, "SnapHint", "◆ C2 スナップショット（CD 25s）", CookingSimHud.FontRow,
                CookingSimHud.GoldBright, new Vector2(0f, 36f));
            _status = EnsureLabel(rootGo.transform, "SnapStatus", "Tab 長押し 0.8s → スナップ送信", CookingSimHud.FontBody,
                CookingSimHud.Cream, new Vector2(0f, 6f));

            _holdFill = EnsureBar(rootGo.transform, "HoldBar", new Vector2(-12f, -28f), CookingSimHud.Gold, 400f);
            _cdFill = EnsureBar(rootGo.transform, "CdBar", new Vector2(-12f, -52f), CookingSimHud.Teal, 400f);
        }

        private static Text EnsureLabel(Transform parent, string name, string text, int size, Color color, Vector2 pos)
        {
            var t = parent.Find(name);
            Text label;
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                label = go.AddComponent<Text>();
            }
            else
            {
                label = t.GetComponent<Text>() ?? t.gameObject.AddComponent<Text>();
            }

            label.font = CookingSimHud.UiFont();
            label.alignment = TextAnchor.MiddleRight;
            label.fontStyle = FontStyle.Bold;
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(12f, -14f);
            rt.offsetMax = new Vector2(-12f, 14f);
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.rectTransform.anchoredPosition = pos;
            return label;
        }

        private static Image EnsureBar(Transform parent, string name, Vector2 pos, Color color, float width)
        {
            var existing = parent.Find(name);
            Image fill;
            if (existing != null)
            {
                var fillExisting = existing.Find("Fill");
                if (fillExisting != null)
                {
                    fill = fillExisting.GetComponent<Image>();
                    var existingRt = existing.GetComponent<RectTransform>();
                    existingRt.anchoredPosition = pos;
                    existingRt.sizeDelta = new Vector2(width, CookingSimHud.BarHeight);
                    fill.color = color;
                    return fill;
                }
            }

            var go = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null)
            {
                go.transform.SetParent(parent, false);
            }

            var bg = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            bg.color = CookingSimHud.BarTrack;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, CookingSimHud.BarHeight);

            var fillGo = go.transform.Find("Fill")?.gameObject;
            if (fillGo == null)
            {
                fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(go.transform, false);
            }

            fill = fillGo.GetComponent<Image>() ?? fillGo.AddComponent<Image>();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            return fill;
        }
    }

    /// <summary>Frozen C2 snapshot of front inventory + customer queue.</summary>
    public readonly struct SnapshotPayload
    {
        public int ActiveCustomers { get; }
        public string CustomerLine { get; }
        public string StockSummary { get; }
        public string[] CategoryNames { get; }
        public float[] StockLevels { get; }
        public bool GapPreferred { get; }

        public SnapshotPayload(
            int activeCustomers,
            string customerLine,
            string stockSummary,
            string[] categoryNames,
            float[] stockLevels,
            bool gapPreferred)
        {
            ActiveCustomers = activeCustomers;
            CustomerLine = customerLine;
            StockSummary = stockSummary;
            CategoryNames = categoryNames;
            StockLevels = stockLevels;
            GapPreferred = gapPreferred;
        }

        public static SnapshotPayload Capture(
            InventoryBar inventory,
            DummyCustomerQueue queue,
            bool gapPreferred = false)
        {
            var customers = queue != null ? queue.ActiveCount : 0;
            var customerLine = queue != null
                ? queue.BuildCustomerSummaryLine()
                : $"待ち客: {customers}";
            var stockSummary = inventory != null ? inventory.SummaryText : "在庫: —";
            string[] names = Array.Empty<string>();
            float[] levels = Array.Empty<float>();
            if (inventory != null)
            {
                inventory.CopyStockSnapshot(out names, out levels);
            }

            return new SnapshotPayload(customers, customerLine, stockSummary, names, levels, gapPreferred);
        }
    }
}
