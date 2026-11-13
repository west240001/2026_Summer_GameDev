using System;
using BackbeatDiner.Front;
using BackbeatDiner.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BackbeatDiner.Coop
{
    /// <summary>
    /// KG-3 C1 quest request (裏→表): Q key → Quest beat SE + toast on Front.
    /// CD 15s. Blocked while <see cref="NoteHighway.HasActiveNotes"/> (ノーツ入力中).
    /// </summary>
    public sealed class QuestRequestController : MonoBehaviour
    {
        public const float CooldownSeconds = 15f;

        /// <summary>True while Back cannot send C1 (notes on highway).</summary>
        public static bool BackNoteInputLocked { get; private set; }

        /// <summary>Raised when C1 Quest request is accepted (Front listens).</summary>
        public static event Action<QuestRequestPayload> QuestRequested;

        [SerializeField] private NoteHighway highway;

        private Text _status;
        private Image _cdFill;
        private float _nextReadyTime;

        public float RemainingCooldown => Mathf.Max(0f, _nextReadyTime - Time.unscaledTime);
        public bool IsReady => RemainingCooldown <= 0f && !IsNoteInputBlocked();

        public void Configure(NoteHighway noteHighway)
        {
            highway = noteHighway;
            EnsureUi();
            RefreshHud();
        }

        /// <summary>Chart-gap coop: clear C1 CD so Quest can fire in the window.</summary>
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

        private void Update()
        {
            BackNoteInputLocked = IsNoteInputBlocked();

            if (WasQuestPressed())
            {
                TrySend();
            }

            RefreshHud();
        }

        private bool IsNoteInputBlocked() => highway != null && highway.HasActiveNotes;

        private static bool WasQuestPressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.qKey.wasPressedThisFrame;
        }

        public bool TrySend()
        {
            if (IsNoteInputBlocked())
            {
                Debug.Log("[QuestRequest] Blocked — 譜面中（ノーツ入力）");
                SetStatus("要求: 譜面中は不可", FrontHudController.Gold);
                return false;
            }

            if (RemainingCooldown > 0f)
            {
                Debug.Log($"[QuestRequest] CD {RemainingCooldown:0.0}s left");
                SetStatus($"要求 CD {RemainingCooldown:0.0}s", FrontHudController.Gold);
                return false;
            }

            var payload = new QuestRequestPayload(QuestSlot.InventoryAll, Time.unscaledTime);
            _nextReadyTime = Time.unscaledTime + CooldownSeconds;
            Debug.Log("[QuestRequest] C1 Quest sent → Front（在庫全件）");
            SetStatus("クエスト送信完了", FrontHudController.Teal);
            QuestRequested?.Invoke(payload);
            RefreshHud();
            return true;
        }

        private void RefreshHud()
        {
            if (_status == null)
            {
                return;
            }

            if (IsNoteInputBlocked())
            {
                SetStatus("要求: 譜面中は不可", FrontHudController.Gold);
            }
            else if (RemainingCooldown > 0f)
            {
                SetStatus($"要求 CD {RemainingCooldown:0.0}s", FrontHudController.Gold);
            }
            else
            {
                SetStatus("Q → C1 クエスト要求", FrontHudController.Teal);
            }

            if (_cdFill != null)
            {
                var remain = RemainingCooldown;
                _cdFill.fillAmount = CooldownSeconds > 0f
                    ? Mathf.Clamp01(remain / CooldownSeconds)
                    : 0f;
                _cdFill.color = IsNoteInputBlocked() || remain > 0f
                    ? FrontHudController.Gold
                    : FrontHudController.Teal;
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (_status == null)
            {
                return;
            }

            _status.text = text;
            _status.color = color;
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
                canvasGo = GameObject.Find("QuestRequestHUD");
                if (canvasGo == null)
                {
                    canvasGo = new GameObject("QuestRequestHUD");
                    var canvas = canvasGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 26;
                    canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            var root = canvasGo.transform.Find("QuestRequestHud");
            GameObject rootGo;
            if (root == null)
            {
                rootGo = new GameObject("QuestRequestHud");
                rootGo.transform.SetParent(canvasGo.transform, false);
                var bg = rootGo.AddComponent<Image>();
                bg.color = new Color(0.03f, 0.07f, 0.1f, 0.82f);
                var rt = bg.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(16f, -510f);
                rt.sizeDelta = new Vector2(320f, 56f);
            }
            else
            {
                rootGo = root.gameObject;
            }

            _status = EnsureLabel(rootGo.transform, "QuestStatus", "Q → C1 クエスト要求", 16,
                FrontHudController.Teal, new Vector2(0f, 6f));
            _cdFill = EnsureBar(rootGo.transform, "QuestCdBar", new Vector2(8f, -16f), FrontHudController.Teal);
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
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                             ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.alignment = TextAnchor.MiddleLeft;
                var rt = label.rectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(10f, -12f);
                rt.offsetMax = new Vector2(-10f, 12f);
            }
            else
            {
                label = t.GetComponent<Text>();
            }

            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.rectTransform.anchoredPosition = pos;
            return label;
        }

        private static Image EnsureBar(Transform parent, string name, Vector2 pos, Color color)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var fillExisting = existing.Find("Fill");
                if (fillExisting != null)
                {
                    return fillExisting.GetComponent<Image>();
                }
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.1f, 0.12f, 0.9f);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280f, 10f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.AddComponent<Image>();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(1f, 1f);
            frt.offsetMax = new Vector2(-1f, -1f);
            return fill;
        }
    }

    /// <summary>C1 request slot (v1: inventory / queue / next orders).</summary>
    public enum QuestSlot
    {
        InventoryAll = 0,
        CustomerQueue = 1,
        NextOrders = 2
    }

    /// <summary>Frozen C1 quest request payload.</summary>
    public readonly struct QuestRequestPayload
    {
        public QuestSlot Slot { get; }
        public float SentAtUnscaled { get; }

        public QuestRequestPayload(QuestSlot slot, float sentAtUnscaled)
        {
            Slot = slot;
            SentAtUnscaled = sentAtUnscaled;
        }

        public string JapaneseSlotLabel => Slot switch
        {
            QuestSlot.CustomerQueue => "客列",
            QuestSlot.NextOrders => "次3注文",
            _ => "在庫全件"
        };
    }
}
