using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneTapDemolition
{
    /// <summary>
    /// 実行時にUGUIを組み立てるための小さなヘルパー。Canvas/RectTransform/アンカーだけで配置し、固定ピクセル座標には依存しない。
    /// </summary>
    public static class UiKit
    {
        private static Font cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return cachedFont;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// Canvas直下にSafe Area+バナー領域を避けるルート矩形を作る。
        /// </summary>
        public static RectTransform CreateSafeRoot(Canvas canvas)
        {
            RectTransform root = NewRect("SafeRoot", canvas.transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<SafeAreaFitter>();
            return root;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void SetAnchored(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = true)
        {
            RectTransform rt = NewRect(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (rounded)
            {
                image.sprite = UiSprites.RoundedRect;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        public static Text Label(Transform parent, string name, string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            RectTransform rt = NewRect(name, parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        public static Button MakeButton(Transform parent, string name, string label, Color color, Vector2 size, UnityAction onClick)
        {
            RectTransform rt = NewRect(name, parent);
            rt.sizeDelta = size;
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.RoundedRect;
            image.type = Image.Type.Sliced;
            image.color = color;

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => JuiceManager.Instance?.PlayUiClick());
            button.onClick.AddListener(onClick);

            Text text = Label(rt, "Label", label, 46, Color.white);
            RectTransform tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;
            return button;
        }
    }
}
