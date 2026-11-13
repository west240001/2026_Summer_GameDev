using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Cooking Simulator–style HUD chrome: large JP labels, semi-opaque dark panels,
    /// white/gold text, thick labeled bars, clear icon+text rows.
    /// </summary>
    public static class CookingSimHud
    {
        public static readonly Color Gold = new Color(0.7882353f, 0.6352941f, 0.15294118f); // #C9A227
        public static readonly Color GoldBright = new Color(0.93f, 0.78f, 0.32f, 1f);
        public static readonly Color Teal = new Color(0.101960784f, 0.419607843f, 0.419607843f); // #1A6B6B
        public static readonly Color Navy = new Color(0.05490196f, 0.12156863f, 0.2f); // #0E1F33
        public static readonly Color Cream = new Color(0.96f, 0.94f, 0.88f, 1f);
        public static readonly Color White = new Color(0.98f, 0.98f, 0.96f, 1f);
        public static readonly Color PanelBg = new Color(0.04f, 0.05f, 0.07f, 0.88f);
        public static readonly Color PanelBgSoft = new Color(0.05f, 0.06f, 0.09f, 0.78f);
        public static readonly Color BarTrack = new Color(0.08f, 0.1f, 0.12f, 0.95f);
        public static readonly Color BorderGold = new Color(0.7882353f, 0.6352941f, 0.15294118f, 0.75f);
        public static readonly Color BorderTeal = new Color(0.101960784f, 0.419607843f, 0.419607843f, 0.7f);

        public const int FontTitle = 28;
        public const int FontHeader = 24;
        public const int FontBody = 22;
        public const int FontRow = 20;
        public const int FontHint = 18;
        public const int FontSmall = 16;
        public const float BarHeight = 22f;
        public const float PanelPad = 16f;

        public static Font UiFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        public static Canvas EnsureOverlayCanvas(string name, int sortingOrder)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                go.AddComponent<GraphicRaycaster>();
            }
            else
            {
                var canvas = go.GetComponent<Canvas>() ?? go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
                var scaler = go.GetComponent<CanvasScaler>() ?? go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            return go.GetComponent<Canvas>();
        }

        public static GameObject EnsurePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color? color = null,
            bool goldBorder = true)
        {
            var t = parent.Find(name);
            GameObject go;
            Image img;
            if (t == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                img = go.AddComponent<Image>();
            }
            else
            {
                go = t.gameObject;
                img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            }

            img.color = color ?? PanelBg;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            outline.effectColor = goldBorder ? BorderGold : BorderTeal;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            return go;
        }

        public static Text EnsureLabel(
            Transform parent,
            string name,
            string text,
            Vector2 anchoredPos,
            TextAnchor corner,
            int fontSize,
            Color color,
            FontStyle style = FontStyle.Normal,
            float width = 520f,
            float height = 36f)
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

            label.font = UiFont();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = label.rectTransform;
            switch (corner)
            {
                case TextAnchor.UpperLeft:
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    label.alignment = TextAnchor.UpperLeft;
                    break;
                case TextAnchor.UpperRight:
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    label.alignment = TextAnchor.UpperRight;
                    break;
                case TextAnchor.UpperCenter:
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    label.alignment = TextAnchor.UpperCenter;
                    break;
                case TextAnchor.LowerCenter:
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    label.alignment = TextAnchor.MiddleCenter;
                    break;
                case TextAnchor.MiddleLeft:
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    label.alignment = TextAnchor.MiddleLeft;
                    break;
                case TextAnchor.MiddleRight:
                    rt.anchorMin = new Vector2(1f, 0.5f);
                    rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    label.alignment = TextAnchor.MiddleRight;
                    break;
                case TextAnchor.MiddleCenter:
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    label.alignment = TextAnchor.MiddleCenter;
                    break;
                default:
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    label.alignment = TextAnchor.MiddleCenter;
                    break;
            }

            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, height);
            return label;
        }

        /// <summary>Labeled progress bar (Cooking Sim ticket style). Returns fill Image.</summary>
        public static Image EnsureLabeledBar(
            Transform parent,
            string name,
            string labelText,
            Vector2 anchoredPos,
            TextAnchor corner,
            Color fillColor,
            float width,
            float height = BarHeight,
            int labelSize = FontSmall)
        {
            var rootT = parent.Find(name);
            GameObject root;
            if (rootT == null)
            {
                root = new GameObject(name);
                root.transform.SetParent(parent, false);
                root.AddComponent<RectTransform>();
            }
            else
            {
                root = rootT.gameObject;
            }

            var rt = root.GetComponent<RectTransform>();
            ApplyCorner(rt, corner);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, height + 22f);

            var caption = EnsureChildLabel(root.transform, "Caption", labelText, labelSize, Cream);
            var capRt = caption.rectTransform;
            capRt.anchorMin = new Vector2(0f, 1f);
            capRt.anchorMax = new Vector2(1f, 1f);
            capRt.pivot = new Vector2(0.5f, 1f);
            capRt.anchoredPosition = Vector2.zero;
            capRt.sizeDelta = new Vector2(0f, 20f);
            caption.alignment = corner == TextAnchor.UpperRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

            var trackT = root.transform.Find("Track");
            Image track;
            if (trackT == null)
            {
                var trackGo = new GameObject("Track");
                trackGo.transform.SetParent(root.transform, false);
                track = trackGo.AddComponent<Image>();
            }
            else
            {
                track = trackT.GetComponent<Image>() ?? trackT.gameObject.AddComponent<Image>();
            }

            track.color = BarTrack;
            track.raycastTarget = false;
            var trackRt = track.rectTransform;
            trackRt.anchorMin = new Vector2(0f, 0f);
            trackRt.anchorMax = new Vector2(1f, 0f);
            trackRt.pivot = new Vector2(0.5f, 0f);
            trackRt.anchoredPosition = Vector2.zero;
            trackRt.sizeDelta = new Vector2(0f, height);

            var fillT = track.transform.Find("Fill");
            Image fill;
            if (fillT == null)
            {
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(track.transform, false);
                fill = fillGo.AddComponent<Image>();
            }
            else
            {
                fill = fillT.GetComponent<Image>() ?? fillT.gameObject.AddComponent<Image>();
            }

            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.35f;
            fill.raycastTarget = false;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            return fill;
        }

        public static Image EnsureSimpleBar(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            TextAnchor corner,
            Color fillColor,
            float width,
            float height = BarHeight)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<Image>().color = BarTrack;
            }
            else
            {
                go = existing.gameObject;
                var bg = go.GetComponent<Image>() ?? go.AddComponent<Image>();
                bg.color = BarTrack;
            }

            var rt = go.GetComponent<RectTransform>();
            ApplyCorner(rt, corner);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, height);

            var fillT = go.transform.Find("Fill");
            Image fill;
            if (fillT == null)
            {
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(go.transform, false);
                fill = fillGo.AddComponent<Image>();
            }
            else
            {
                fill = fillT.GetComponent<Image>() ?? fillT.gameObject.AddComponent<Image>();
            }

            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            return fill;
        }

        public static Text EnsureChildLabel(
            Transform parent,
            string name,
            string text,
            int fontSize,
            Color color,
            FontStyle style = FontStyle.Normal)
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

            label.font = UiFont();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Image EnsureIconChip(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var t = parent.Find(name);
            Image img;
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                img = go.AddComponent<Image>();
            }
            else
            {
                img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            }

            img.color = color;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return img;
        }

        public static void ApplyCorner(RectTransform rt, TextAnchor corner)
        {
            switch (corner)
            {
                case TextAnchor.UpperRight:
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    break;
                case TextAnchor.UpperCenter:
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    break;
                case TextAnchor.UpperLeft:
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    break;
                case TextAnchor.LowerLeft:
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    break;
                case TextAnchor.LowerRight:
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    break;
                case TextAnchor.MiddleRight:
                    rt.anchorMin = new Vector2(1f, 0.5f);
                    rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    break;
                case TextAnchor.MiddleLeft:
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    break;
                default:
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }
        }
    }
}
