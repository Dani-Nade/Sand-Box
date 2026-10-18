using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SandHouse.UI
{
    /// <summary>
    /// Helpers for building placeholder UI from code (canvases, panels, text, buttons, sliders, toggles)
    /// so every screen shares one look. Swap for designed prefabs later.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.85f);
        public static readonly Color ButtonColor = new Color(0.24f, 0.19f, 0.14f, 1f);
        public static readonly Color ButtonHoverColor = new Color(0.4f, 0.3f, 0.18f, 1f);
        public static readonly Color ButtonPressedColor = new Color(0.55f, 0.4f, 0.2f, 1f);
        public static readonly Color DisabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        public static readonly Color TrackColor = new Color(0.15f, 0.13f, 0.11f, 1f);
        public static readonly Color AccentColor = new Color(1f, 0.8f, 0.35f, 1f);
        public static readonly Color MutedTextColor = new Color(1f, 1f, 1f, 0.6f);

        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ---------------------------------------------------------------- scaffolding

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder, bool interactive)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (interactive) go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Clickable UI needs one EventSystem in the scene (using the new Input System).</summary>
        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Positions a rect relative to an anchor point on its parent.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Fills the parent, with optional inset.</summary>
        public static RectTransform Stretch(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Sets the size a layout group gives this element.</summary>
        public static T Size<T>(this T component, float width = -1f, float height = -1f, float flexibleWidth = -1f) where T : Component
        {
            var element = component.GetComponent<LayoutElement>();
            if (element == null) element = component.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) element.preferredWidth = element.minWidth = width;
            if (height >= 0f) element.preferredHeight = element.minHeight = height;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            return component;
        }

        public static VerticalLayoutGroup VerticalLayout(GameObject go, float spacing, int padding)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup HorizontalLayout(GameObject go, float spacing)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return layout;
        }

        /// <summary>An empty row for a vertical layout, laid out horizontally.</summary>
        public static RectTransform CreateRow(Transform parent, string name, float height, float spacing = 10f)
        {
            var row = CreateRect(parent, name);
            HorizontalLayout(row.gameObject, spacing);
            row.Size(height: height);
            return row;
        }

        // ---------------------------------------------------------------- widgets

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreatePanel(Transform parent, string name) => CreateImage(parent, name, PanelColor);

        public static Text CreateText(Transform parent, string name, string text, int size,
            TextAnchor alignment = TextAnchor.MiddleLeft, Color? color = null, bool shadow = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color ?? Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (shadow) go.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            return label;
        }

        public static Button CreateButton(Transform parent, string text, Action onClick, int fontSize = 26)
        {
            var image = CreateImage(parent, $"Button {text}", Color.white);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = Colors(ButtonColor);
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var label = CreateText(image.transform, "Label", text, fontSize, TextAnchor.MiddleCenter, shadow: false);
            label.rectTransform.Stretch();
            return button;
        }

        public static Slider CreateSlider(Transform parent, float min, float max, float value, Action<float> onChanged)
        {
            var root = CreateRect(parent, "Slider");
            var slider = root.gameObject.AddComponent<Slider>();

            CreateImage(root, "Track", TrackColor).rectTransform.Stretch(6f);

            var fillArea = CreateRect(root, "Fill Area").Stretch(6f);
            var fill = CreateImage(fillArea, "Fill", AccentColor * new Color(1f, 1f, 1f, 0.7f));
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = CreateRect(root, "Handle Area").Stretch();
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);
            var handle = CreateImage(handleArea, "Handle", Color.white);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(16f, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.colors = Colors(AccentColor);
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, bool value, Action<bool> onChanged)
        {
            var background = CreateImage(parent, "Toggle", Color.white);
            background.raycastTarget = true;
            var toggle = background.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.colors = Colors(TrackColor);

            var check = CreateImage(background.transform, "Check", AccentColor);
            check.rectTransform.Stretch(7f);
            toggle.graphic = check;

            toggle.SetIsOnWithoutNotify(value);
            if (onChanged != null) toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        static ColorBlock Colors(Color normal)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.selectedColor = normal; // don't stay highlighted after a mouse click
            colors.highlightedColor = normal == ButtonColor ? ButtonHoverColor : Color.Lerp(normal, Color.white, 0.25f);
            colors.pressedColor = normal == ButtonColor ? ButtonPressedColor : Color.Lerp(normal, Color.white, 0.4f);
            colors.disabledColor = DisabledColor;
            colors.fadeDuration = 0.05f;
            return colors;
        }
    }
}
