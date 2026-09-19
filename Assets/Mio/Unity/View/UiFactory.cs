using UnityEngine;
using UnityEngine.UI;

namespace Mio.Unity.View
{
    /// <summary>
    /// Builds the placeholder visuals from flat rectangles at runtime.
    ///
    /// M0 ships no art, so every prototype draws itself from Images. Positions
    /// and sizes are expressed as anchors in play-field space, which means the
    /// layout rescales itself on any screen without a single pixel constant.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>
        /// Play-field aspect (width / height). The field is a fixed 9:16 box
        /// letterboxed inside the canvas, so gameplay is identical on a tall
        /// phone and a squat tablet.
        /// </summary>
        public const float FieldAspect = 1080f / 1920f;

        /// <summary>Converts a length in field-width units to field-height units.</summary>
        public static float ToHeightUnits(float widthUnits) => widthUnits * FieldAspect;

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Rect(Transform parent, string name, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>Stretches a RectTransform to fill its parent.</summary>
        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Positions and sizes a RectTransform purely with anchors, in
        /// play-field space: (0,0) bottom-left, (1,1) top-right.
        /// </summary>
        public static void Place(RectTransform rt, float centerX, float centerY, float width, float height)
        {
            var hw = width * 0.5f;
            var hh = height * 0.5f;

            rt.anchorMin = new Vector2(centerX - hw, centerY - hh);
            rt.anchorMax = new Vector2(centerX + hw, centerY + hh);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>Places a visually square box, sized in field-width units.</summary>
        public static void PlaceSquare(RectTransform rt, float centerX, float centerY, float size)
        {
            Place(rt, centerX, centerY, size, ToHeightUnits(size));
        }

        private static Font _font;

        /// <summary>
        /// The built-in font, so the project needs no font asset and no
        /// TextMeshPro import step. Returns null if the engine has none, and
        /// every caller treats that as "skip the label".
        /// </summary>
        public static Font BuiltinFont()
        {
            if (_font != null) return _font;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }

        public static Text Label(
            Transform parent,
            string name,
            string value,
            int fontSize,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var font = BuiltinFont();
            if (font == null) return null;

            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static void SetText(Text label, string value)
        {
            if (label != null) label.text = value;
        }

        public static void SetActive(Component component, bool active)
        {
            if (component != null && component.gameObject.activeSelf != active)
            {
                component.gameObject.SetActive(active);
            }
        }
    }
}
