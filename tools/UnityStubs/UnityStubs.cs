// Compile-only stubs of the Unity API surface this project touches.
//
// Unity is not installable in CI, so without these the ~1600 lines of the
// Mio.Unity and Mio.Editor assemblies would ship completely unverified. They
// catch syntax errors, typos, wrong argument counts and internal API drift
// across our own classes.
//
// They do NOT prove the real Unity API is used correctly; only the editor does
// that. They are never compiled into the game: this folder lives outside
// Assets/, so Unity ignores it entirely.

using System;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object target) { }
        public static void DestroyImmediate(Object target) { }
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => 0;
        public static implicit operator bool(Object target) => !ReferenceEquals(target, null);
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a;
        public static Vector2 operator +(Vector2 a, Vector2 b) => a;
        public static Vector2 operator -(Vector2 a, Vector2 b) => a;
        public static Vector2 operator *(Vector2 a, float s) => a;
        public static bool operator ==(Vector2 a, Vector2 b) => true;
        public static bool operator !=(Vector2 a, Vector2 b) => false;
        public override bool Equals(object o) => true;
        public override int GetHashCode() => 0;
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 zero => new Vector3(0, 0, 0);
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }

    public struct Rect
    {
        public float width, height, xMin, yMin, xMax, yMax;
        public Vector2 size => new Vector2(width, height);
        public Vector2 min => new Vector2(xMin, yMin);
        public Vector2 max => new Vector2(xMax, yMax);
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color black => new Color(0, 0, 0);
        public static Color clear => new Color(0, 0, 0, 0);
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static float Clamp01(float v) => v;
        public static float Clamp(float v, float a, float b) => v;
        public static int Clamp(int v, int a, int b) => v;
        public static float Max(float a, float b) => a;
        public static int Max(int a, int b) => a;
        public static float Min(float a, float b) => a;
        public static float Lerp(float a, float b, float t) => a;
        public static float MoveTowards(float a, float b, float d) => a;
        public static float Abs(float v) => v;
        public static float Sin(float v) => v;
        public static float Cos(float v) => v;
        public static float Exp(float v) => v;
        public static int RoundToInt(float v) => (int)v;
        public static int FloorToInt(float v) => (int)v;
    }

    public static class Time
    {
        public static float deltaTime => 0.016f;
        public static float unscaledTime => 0f;
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogWarning(object message, Object context) { }
        public static void LogError(object message) { }
        public static void LogError(object message, Object context) { }
    }

    public static class Application
    {
        public static int targetFrameRate { get; set; }
        public static string persistentDataPath => ".";
        public static bool isMobilePlatform => false;
    }

    // Not an enum in Unity: a static class of int constants, because
    // Screen.sleepTimeout is a plain int.
    public static class SleepTimeout
    {
        public const int NeverSleep = -2;
        public const int SystemSetting = -1;
    }

    public static class Screen
    {
        public static int sleepTimeout { get; set; }
    }

    public static class PlayerPrefs
    {
        public static bool HasKey(string key) => false;
        public static int GetInt(string key, int def) => def;
        public static void SetInt(string key, int value) { }
        public static void DeleteKey(string key) { }
        public static void Save() { }
    }

    public static class Random
    {
        public static float Range(float min, float max) => min;
        public static int Range(int min, int max) => min;
    }

    public static class Handheld
    {
        public static void Vibrate() { }
    }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() where T : Component => null;
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        public Vector3 localScale { get; set; }
        public Transform parent { get; set; }
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public void SetSiblingIndex(int index) { }
        public void SetAsLastSibling() { }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Rect rect => default;
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform => null;
        public string tag { get; set; }
        public bool activeSelf => true;
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component => null;
        public Component AddComponent(Type type) => null;
        public T GetComponent<T>() where T : Component => null;
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => null;
        public static ScriptableObject CreateInstance(Type type) => null;
    }

    public class AudioClip : Object { }

    public class AudioSource : Behaviour
    {
        public bool playOnAwake { get; set; }
        public float spatialBlend { get; set; }
        public float pitch { get; set; }
        public void PlayOneShot(AudioClip clip, float volume) { }
    }

    public class Font : Object { }
    public class Sprite : Object { }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }

    public class Camera : Behaviour
    {
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public bool orthographic { get; set; }
    }

    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(
            RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName { get; set; }
        public string fileName { get; set; }
        public int order { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public RequireComponent(Type type) { }
    }
}

namespace UnityEngine.UI
{
    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Graphic : Behaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
    }

    public class Image : Graphic
    {
        public Sprite sprite { get; set; }
    }

    public class Text : Graphic
    {
        public Font font { get; set; }
        public string text { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
    }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
    }

    public class CanvasScaler : Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }

        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : Behaviour { }

    public class AspectRatioFitter : Behaviour
    {
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent }
        public AspectMode aspectMode { get; set; }
        public float aspectRatio { get; set; }
    }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }

    public class EventSystem : UIBehaviour
    {
        public static EventSystem current => null;
    }

    public class BaseInputModule : UIBehaviour { }
    public class PointerInputModule : BaseInputModule { }
    public class StandaloneInputModule : PointerInputModule { }

    public class BaseEventData { }

    public class PointerEventData : BaseEventData
    {
        public Vector2 position { get; set; }
        public int pointerId { get; set; }
    }

    public interface IEventSystemHandler { }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerMoveHandler : IEventSystemHandler { void OnPointerMove(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
}
