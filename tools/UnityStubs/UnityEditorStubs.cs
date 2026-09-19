// Compile-only stubs for the UnityEditor surface used by Mio.Editor.
// See UnityStubs.cs for why these exist and what they do and do not prove.

using System;
using UnityEngine;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
        public int priority { get; set; }
    }

    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static bool IsValidFolder(string path) => false;
        public static string CreateFolder(string parent, string newFolderName) => string.Empty;
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object target) { }
        public static bool DisplayDialog(string title, string message, string ok) => true;
    }

    public class SerializedProperty
    {
        public UnityEngine.Object objectReferenceValue { get; set; }
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object target) { }
        public SerializedProperty FindProperty(string path) => null;
        public bool ApplyModifiedProperties() => true;
        public void ApplyModifiedPropertiesWithoutUndo() { }
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
        public string path { get; set; }
        public bool enabled { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; } = new EditorBuildSettingsScene[0];
    }

    public enum UIOrientation
    {
        Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation
    }

    public enum AndroidSdkVersions
    {
        AndroidApiLevel24 = 24,
        AndroidApiLevel26 = 26,
        AndroidApiLevel29 = 29
    }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }

        public static class Android
        {
            public static AndroidSdkVersions minSdkVersion { get; set; }
        }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get; set; }
        public string path { get; set; }
    }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;

    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool MarkSceneDirty(Scene scene) => true;
        public static bool SaveScene(Scene scene, string path) => true;
    }
}
