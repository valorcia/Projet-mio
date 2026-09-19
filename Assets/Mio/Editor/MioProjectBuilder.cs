using System.Collections.Generic;
using System.IO;
using Mio.Unity.App;
using Mio.Unity.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mio.Editor
{
    /// <summary>
    /// One-click project setup.
    ///
    /// The three prototypes live in code, not in scene files: a scene here is a
    /// camera and a single Bootstrap component. Generating them rather than
    /// committing hand-written YAML keeps the repository diffable, avoids merge
    /// conflicts in binary-ish assets, and means the scenes can be regenerated
    /// after any refactor.
    /// </summary>
    public static class MioProjectBuilder
    {
        private const string SettingsFolder = "Assets/Mio/Settings";
        private const string ScenesFolder = "Assets/Mio/Scenes";

        [MenuItem("Tools/MIO/Set Up Project", priority = 0)]
        public static void SetUpProject()
        {
            CreateDefaultAssets();
            GenerateScenes();
            ApplyMobilePlayerSettings();

            EditorUtility.DisplayDialog(
                "MIO",
                "Prototype scenes, tuning assets and mobile player settings are ready.\n\n" +
                "Open Assets/Mio/Scenes and press Play.",
                "OK");
        }

        [MenuItem("Tools/MIO/Create Default Tuning Assets", priority = 20)]
        public static void CreateDefaultAssets()
        {
            EnsureFolder(SettingsFolder);

            var palette = GetOrCreate<PrototypePalette>("Palette");
            var feedback = GetOrCreate<FeedbackProfile>("FeedbackProfile");
            var rewards = GetOrCreate<RewardTableAsset>("RewardTable");

            Wire(GetOrCreate<FlowConfigAsset>("FlowConfig"), palette, feedback, rewards);
            Wire(GetOrCreate<PopChainConfigAsset>("PopChainConfig"), palette, feedback, rewards);
            Wire(GetOrCreate<PackConfigAsset>("PackConfig"), palette, feedback, rewards);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Wire(
            PrototypeConfigAsset config,
            PrototypePalette palette,
            FeedbackProfile feedback,
            RewardTableAsset rewards)
        {
            // Only fill in blanks, so re-running setup never stomps on tuning
            // someone has already done.
            var dirty = false;

            if (config.Palette == null) { config.Palette = palette; dirty = true; }
            if (config.Feedback == null) { config.Feedback = feedback; dirty = true; }
            if (config.Rewards == null) { config.Rewards = rewards; dirty = true; }

            if (dirty) EditorUtility.SetDirty(config);
        }

        private static T GetOrCreate<T>(string name) where T : ScriptableObject
        {
            var path = $"{SettingsFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        [MenuItem("Tools/MIO/Generate Prototype Scenes", priority = 21)]
        public static void GenerateScenes()
        {
            CreateDefaultAssets();
            EnsureFolder(ScenesFolder);

            var paths = new List<string>
            {
                BuildScene("A_Flow", LoadConfig<FlowConfigAsset>("FlowConfig")),
                BuildScene("B_PopChain", LoadConfig<PopChainConfigAsset>("PopChainConfig")),
                BuildScene("C_Pack", LoadConfig<PackConfigAsset>("PackConfig"))
            };

            AddToBuildSettings(paths);
            AssetDatabase.Refresh();
        }

        private static T LoadConfig<T>(string name) where T : PrototypeConfigAsset
        {
            return AssetDatabase.LoadAssetAtPath<T>($"{SettingsFolder}/{name}.asset");
        }

        private static string BuildScene(string sceneName, PrototypeConfigAsset config)
        {
            var path = $"{ScenesFolder}/{sceneName}.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The UI is Screen Space Overlay so it renders without a camera, but
            // a scene with no camera logs warnings and leaves nothing to clear
            // the buffer on some targets.
            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config != null && config.Palette != null
                ? config.Palette.Background
                : Color.black;
            camera.orthographic = true;

            var bootstrapGo = new GameObject("Bootstrap");
            var bootstrap = bootstrapGo.AddComponent<PrototypeBootstrap>();

            // The config field is private so the inspector stays tidy; reach it
            // through SerializedObject rather than loosening the API for a tool.
            var serialized = new SerializedObject(bootstrap);
            var property = serialized.FindProperty("_config");
            if (property != null)
            {
                property.objectReferenceValue = config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError("[MIO] Could not find the _config field on PrototypeBootstrap.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void AddToBuildSettings(IList<string> scenePaths)
        {
            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var path in scenePaths)
            {
                var found = false;
                foreach (var entry in existing)
                {
                    if (entry.path != path) continue;
                    found = true;
                    break;
                }

                if (!found) existing.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = existing.ToArray();
        }

        /// <summary>
        /// Portrait-first mobile defaults. Kept to the settings the brief
        /// actually pins down; anything else stays at Unity's default so we are
        /// not silently deciding things for the project.
        /// </summary>
        [MenuItem("Tools/MIO/Apply Mobile Player Settings", priority = 22)]
        public static void ApplyMobilePlayerSettings()
        {
            PlayerSettings.companyName = "Valorcia";
            PlayerSettings.productName = "PROJECT MIO";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // A casual game that dims mid-session reads as broken.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            AssetDatabase.SaveAssets();
            Debug.Log("[MIO] Applied portrait-first mobile player settings.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
