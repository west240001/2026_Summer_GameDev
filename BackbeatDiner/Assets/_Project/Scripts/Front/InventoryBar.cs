using BackbeatDiner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Front
{
    /// <summary>
    /// Fake front stock categories (ING A–E style) tinted teal/gold for Week2 shell.
    /// </summary>
    public sealed class InventoryBar : MonoBehaviour
    {
        private static readonly string[] CategoryNames = { "A 丼", "B グリル", "C 副菜", "D ソース", "E 仕上げ" };

        [SerializeField] private RectTransform rowRoot;
        [SerializeField] private float[] stock = { 0.82f, 0.55f, 0.7f, 0.4f, 0.9f };

        private Image[] _fills;
        private Text[] _labels;

        public string SummaryText
        {
            get
            {
                if (stock == null || stock.Length == 0)
                {
                    return "在庫: —";
                }

                var low = 0;
                for (var i = 0; i < stock.Length; i++)
                {
                    if (stock[i] < 0.35f)
                    {
                        low++;
                    }
                }

                return low > 0 ? $"在庫: 低在庫 {low}種" : "在庫: 安定";
            }
        }

        /// <summary>KG-3 C2: copy frozen category names + levels for a snapshot payload.</summary>
        public void CopyStockSnapshot(out string[] names, out float[] levels)
        {
            EnsureStock();
            names = new string[CategoryNames.Length];
            levels = new float[stock.Length];
            for (var i = 0; i < CategoryNames.Length; i++)
            {
                names[i] = CategoryNames[i];
            }

            for (var i = 0; i < stock.Length; i++)
            {
                levels[i] = stock[i];
            }
        }

        public void BuildUi(RectTransform parent)
        {
            rowRoot = parent;
            EnsureStock();
            Rebuild();
        }

        private void Start()
        {
            if (_fills == null && rowRoot != null)
            {
                Rebuild();
            }
        }

        private void Update()
        {
            if (stock == null || _fills == null)
            {
                return;
            }

            // Gentle fake drain / restock so the bar feels alive.
            for (var i = 0; i < stock.Length && i < _fills.Length; i++)
            {
                stock[i] = Mathf.Clamp01(stock[i] + Mathf.Sin(Time.time * (0.35f + i * 0.07f) + i) * Time.deltaTime * 0.08f);
                if (_fills[i] != null)
                {
                    _fills[i].fillAmount = stock[i];
                    _fills[i].color = stock[i] < 0.35f ? FrontHudController.Gold : FrontHudController.Teal;
                }

                if (_labels != null && i < _labels.Length && _labels[i] != null)
                {
                    _labels[i].text = $"{CategoryNames[i % CategoryNames.Length]}\n{Mathf.RoundToInt(stock[i] * 100f)}%";
                }
            }
        }

        private void Rebuild()
        {
            EnsureStock();
            if (rowRoot == null)
            {
                return;
            }

            for (var c = rowRoot.childCount - 1; c >= 0; c--)
            {
                Destroy(rowRoot.GetChild(c).gameObject);
            }

            _fills = new Image[stock.Length];
            _labels = new Text[stock.Length];

            // Cooking Sim inventory slots — large, labeled, high contrast
            var width = 128f;
            var height = 92f;
            var gap = 10f;
            var total = stock.Length * width + (stock.Length - 1) * gap;
            var startX = -total * 0.5f + width * 0.5f;

            for (var i = 0; i < stock.Length; i++)
            {
                var cell = new GameObject($"Stock_{i}");
                cell.transform.SetParent(rowRoot, false);
                var cellRt = cell.AddComponent<RectTransform>();
                cellRt.sizeDelta = new Vector2(width, height);
                cellRt.anchoredPosition = new Vector2(startX + i * (width + gap), 0f);

                var bg = cell.AddComponent<Image>();
                bg.color = CookingSimHud.PanelBg;
                var outline = cell.AddComponent<Outline>();
                outline.effectColor = CookingSimHud.BorderGold;
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(cell.transform, false);
                var fill = fillGo.AddComponent<Image>();
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                fill.fillAmount = stock[i];
                fill.color = stock[i] < 0.35f ? CookingSimHud.Gold : CookingSimHud.Teal;
                var frt = fill.rectTransform;
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = new Vector2(5f, 26f);
                frt.offsetMax = new Vector2(-5f, -5f);
                _fills[i] = fill;

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(cell.transform, false);
                var label = labelGo.AddComponent<Text>();
                label.font = CookingSimHud.UiFont();
                label.fontSize = 15;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.LowerCenter;
                label.color = CookingSimHud.Cream;
                label.text = $"{CategoryNames[i % CategoryNames.Length]}\n{Mathf.RoundToInt(stock[i] * 100f)}%";
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 0f);
                lrt.sizeDelta = new Vector2(0f, 28f);
                lrt.anchoredPosition = Vector2.zero;
                _labels[i] = label;
            }
        }

        private void EnsureStock()
        {
            if (stock == null || stock.Length != 5)
            {
                stock = new[] { 0.82f, 0.55f, 0.7f, 0.4f, 0.9f };
            }
        }
    }
}
