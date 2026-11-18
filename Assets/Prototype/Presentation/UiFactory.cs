using System;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonsNShops
{
    internal sealed class UiFactory
    {
        public static readonly Color Background = new(0.057f, 0.039f, 0.026f, 0.94f);
        public static readonly Color Panel = new(0.32f, 0.23f, 0.15f);
        public static readonly Color Accent = new(0.48f, 0.33f, 0.17f);
        public static readonly Color TextColor = new(0.98f, 0.89f, 0.72f);
        private readonly Font customFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 22);
        private readonly Texture2D[] icons = new Texture2D[Catalog.Count];
        private Sprite frame;
        private Texture2D frameTexture;

        public RawImage ItemIcon(RectTransform parent, Vector2 min, Vector2 max)
        {
            RectTransform bounds = Rect(parent, "Icon bounds");
            Stretch(bounds, min, max);
            RectTransform art = Rect(bounds, "Item portrait");
            RawImage image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            art.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            return image;
        }

        public void SetIcon(RawImage image, int item)
        {
            image.gameObject.SetActive(Catalog.Valid(item));
            if (!Catalog.Valid(item)) return;
            var texture = icons[item] ??= Resources.Load<Texture2D>("InventoryArt/" + item);
            if (image.texture == texture) return;
            image.texture = texture;
            image.GetComponent<AspectRatioFitter>().aspectRatio = texture.width / (float)texture.height;
        }

        public RectTransform Rect(Transform parent, string name)
        {
            GameObject obj = new(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public RectTransform Box(Transform parent, string name, Color color)
        {
            RectTransform rect = Rect(parent, name);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            if (name is "Screen" or "Vitals" or "Hotbar" or "Button" or "Input" || name.StartsWith("Inventory cell"))
            {
                image.sprite = Frame();
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = name == "Screen" ? 1 : 2;
            }
            return rect;
        }

        private Sprite Frame()
        {
            if (frame != null) return frame;
            const int size = 48;
            frameTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Carved brass UI frame", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int a = Mathf.Min(x, size - 1 - x), b = Mathf.Min(y, size - 1 - y);
                    int edge = Mathf.Min(a, b);
                    Color color;
                    if (a + b < 5) color = Color.clear;
                    else if (edge < 2) color = new Color(0.24f, 0.17f, 0.11f);
                    else if (edge < 4 || a + b < 9) color = new Color(1.48f, 1.40f, 1.22f);
                    else if (edge < 6) color = new Color(0.51f, 0.39f, 0.26f);
                    else if (edge == 6) color = new Color(1.17f, 1.06f, 0.86f);
                    else color = Color.Lerp(new Color(0.60f, 0.51f, 0.41f), Color.white,
                        (x * 17 + y * 23) % 21 / 210f + Mathf.Clamp01(edge / 32f) * 0.43f);
                    pixels[y * size + x] = color;
                }
            frameTexture.SetPixels32(pixels);
            frameTexture.Apply(false, true);
            frame = Sprite.Create(frameTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100, 0, SpriteMeshType.FullRect, new Vector4(11, 11, 11, 11));
            return frame;
        }

        public void Dispose()
        {
            if (frame != null) UnityEngine.Object.Destroy(frame);
            if (frameTexture != null) UnityEngine.Object.Destroy(frameTexture);
            if (customFont != null) UnityEngine.Object.Destroy(customFont);
        }

        public UnityEngine.UI.Text Text(Transform parent, string value, int size = 19, TextAnchor align = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(parent, "Text");
            UnityEngine.UI.Text text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = align;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        public UnityEngine.UI.Text Label(Transform parent, string value, int height = 33, int size = 19)
        {
            UnityEngine.UI.Text text = Text(parent, value, size);
            Height(text.gameObject, height);
            return text;
        }

        public UnityEngine.UI.Button Button(Transform parent, string title, Action onClick, int height = 38)
        {
            RectTransform rect = Box(parent, "Button", Accent);
            Height(rect.gameObject, height);
            UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.7f, 0.85f, 0.85f);
            button.colors = colors;
            UnityEngine.UI.Text label = Text(rect, title, 18, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, new Vector2(5, 0), new Vector2(-5, 0));
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public UnityEngine.UI.InputField Field(Transform parent, string value, string hint, bool number)
        {
            RectTransform rect = Box(parent, "Input", new Color(0.13f, 0.17f, 0.22f));
            Height(rect.gameObject, 42);
            UnityEngine.UI.InputField field = rect.gameObject.AddComponent<UnityEngine.UI.InputField>();
            field.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Text placeholder = Text(rect, hint);
            placeholder.color = new Color(0.66f, 0.72f, 0.76f);
            UnityEngine.UI.Text entry = Text(rect, value);
            placeholder.horizontalOverflow = entry.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(placeholder.rectTransform, new Vector2(12, 0), new Vector2(-12, 0));
            Stretch(entry.rectTransform, new Vector2(12, 0), new Vector2(-12, 0));
            field.textComponent = entry;
            field.placeholder = placeholder;
            field.contentType = number ? UnityEngine.UI.InputField.ContentType.IntegerNumber : UnityEngine.UI.InputField.ContentType.Standard;
            field.characterLimit = number ? 3 : 70;
            field.text = value;
            return field;
        }

        public void Slider(Transform parent, float min, float max, float value, Action<float> changed)
        {
            RectTransform root = Rect(parent, "Slider");
            Height(root.gameObject, 30);
            RectTransform track = Box(root, "Track", new Color(0.19f, 0.24f, 0.29f));
            Anchor(track, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(11, -3), new Vector2(-11, 3));
            RectTransform area = Rect(root, "Handle area");
            Stretch(area, new Vector2(11, 0), new Vector2(-11, 0));
            RectTransform handle = Box(area, "Handle", new Color(0.29f, 0.75f, 0.67f));
            handle.sizeDelta = new Vector2(22, 23);
            UnityEngine.UI.Slider slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v => changed(v));
        }

        public RectTransform Row(Transform parent, int height)
        {
            RectTransform row = Rect(parent, "Row");
            Height(row.gameObject, height);
            UnityEngine.UI.HorizontalLayoutGroup group = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            group.spacing = 8;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = true;
            return row;
        }

        public RectTransform Scroll(Transform parent, int height, out UnityEngine.UI.ScrollRect scroll)
        {
            RectTransform root = Rect(parent, "Scroll");
            Height(root.gameObject, height);
            scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            RectTransform viewport = Box(root, "Viewport", new Color(0, 0, 0, 0.15f));
            Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            RectTransform content = Rect(viewport, "Content");
            Anchor(content, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1);
            Column(content, 6, 8);
            UnityEngine.UI.ContentSizeFitter fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static void Column(RectTransform rect, int spacing, int padding)
        {
            UnityEngine.UI.VerticalLayoutGroup group = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.spacing = spacing;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
        }

        public static void Height(GameObject obj, float height)
        {
            UnityEngine.UI.LayoutElement layout = obj.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        public static void Stretch(RectTransform rect, Vector2 min, Vector2 max) =>
            Anchor(rect, Vector2.zero, Vector2.one, min, max);

        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 min, Vector2 max)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }
}
