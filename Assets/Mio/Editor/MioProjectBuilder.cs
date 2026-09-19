using System.Collections.Generic;
using System.IO;
using System.Text;
using Mio.Unity.App;
using Mio.Unity.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mio.Editor
{
    /// <summary>
    /// One-click setup, so a human test needs no manual Unity configuration.
    ///
    /// Everything here is idempotent and repair-oriented: running it on a
    /// healthy project changes nothing, and running it on a broken one fixes
    /// only what is broken. It never overwrites tuning someone has already
    /// done.
    ///
    /// Scenes are generated rather than committed because a scene here is a
    /// camera, an EventSystem and one Bootstrap component; keeping that in
    /// source instead of YAML keeps it diffable and regenerable.
    /// </summary>
    public static class MioProjectBuilder
    {
        [MenuItem("PROJECT MIO/Setup Test Environment", priority = 0)]
        public static void SetupTestEnvironment()
        {
            var log = new StringBuilder();
            log.AppendLine("PROJECT MIO — SETUP TEST ENVIRONMENT");
            log.AppendLine("====================================");

            EnsureFolder(MioPaths.Settings);
            EnsureFolder(MioPaths.Scenes);

            var shared = CreateSharedAssets(log);
            var scenePaths = new List<string>();

            foreach (var ruleSet in MioRuleSetCatalog.All)
            {
                if (!ruleSet.IsImplemented)
                {
                    log.AppendLine($"  [skip] {ruleSet.DisplayName} — not implemented yet, no scene built");
                    continue;
                }

                var config = MioRuleSetCatalog.LoadOrCreateConfig(ruleSet);
                if (config == null) continue;

                WireDependencies(config, shared, ruleSet.DisplayName, log);
                scenePaths.Add(BuildOrRepairScene(ruleSet, config, log));
            }

            AddToBuildSettings(scenePaths, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(log.ToString());

            // Setup is not a green light on its own; the validator is the
            // authority on whether a human can start testing.
            var report = MioProjectValidator.Validate();
            Debug.Log(report.ToConsoleString());

            EditorUtility.DisplayDialog(
                "PROJECT MIO",
                report.Ready
                    ? "Setup complete.\n\nPROJECT MIO READY TO TEST\n\n" +
                      "Next: PROJECT MIO > Open Harness Test, then press Play."
                    : "Setup ran, but the project is NOT ready.\n\n" +
                      "See the Console for the exact corrective actions.",
                "OK");
        }

        private readonly struct SharedAssets
        {
            public readonly PrototypePalette Palette;
            public readonly FeedbackProfile Feedback;
            public readonly RewardTableAsset Rewards;

            public SharedAssets(PrototypePalette palette, FeedbackProfile feedback, RewardTableAsset rewards)
            {
                Palette = palette;
                Feedback = feedback;
                Rewards = rewards;
            }
        }

        private static SharedAssets CreateSharedAssets(StringBuilder log)
        {
            var palette = GetOrCreate<PrototypePalette>("Palette", log);
            var feedback = GetOrCreate<FeedbackProfile>("FeedbackProfile", log);
            var rewards = GetOrCreate<RewardTableAsset>("RewardTable", log);
            return new SharedAssets(palette, feedback, rewards);
        }

        private static T GetOrCreate<T>(string name, StringBuilder log) where T : ScriptableObject
        {
            var path = $"{MioPaths.Settings}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);

            if (existing != null)
            {
                log.AppendLine($"  [ok]   {name}.asset already present");
                return existing;
            }

            // A freshly created ScriptableObject carries the field initialisers
            // declared in code, which are the safe defaults.
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            log.AppendLine($"  [new]  {name}.asset created with safe defaults");
            return asset;
        }

        private static void WireDependencies(
            PrototypeConfigAsset config,
            SharedAssets shared,
            string label,
            StringBuilder log)
        {
            // Only fill blanks. Re-running setup must never stomp on tuning.
            var filled = new List<string>();

            if (config.Palette == null) { config.Palette = shared.Palette; filled.Add("Palette"); }
            if (config.Feedback == null) { config.Feedback = shared.Feedback; filled.Add("Feedback"); }
            if (config.Rewards == null) { config.Rewards = shared.Rewards; filled.Add("Rewards"); }

            if (filled.Count == 0)
            {
                log.AppendLine($"  [ok]   {label} config references already assigned");
                return;
            }

            EditorUtility.SetDirty(config);
            log.AppendLine($"  [fix]  {label} config: assigned {string.Join(", ", filled)}");
        }

        /// <summary>
        /// Creates the scene if missing, and repairs it if its Bootstrap or
        /// EventSystem has gone astray. An existing healthy scene is left alone
        /// so a tester's camera framing or added debug objects survive.
        /// </summary>
        private static string BuildOrRepairScene(
            MioRuleSet ruleSet,
            PrototypeConfigAsset config,
            StringBuilder log)
        {
            var path = ruleSet.ScenePath;
            var exists = File.Exists(path);

            var scene = exists
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var changed = !exists;

            if (EnsureCamera(scene, config)) { changed = true; log.AppendLine($"  [fix]  {ruleSet.SceneName}: added Main Camera"); }
            if (EnsureEventSystem(scene)) { changed = true; log.AppendLine($"  [fix]  {ruleSet.SceneName}: added EventSystem"); }
            if (EnsureBootstrap(scene, config)) { changed = true; log.AppendLine($"  [fix]  {ruleSet.SceneName}: added/repaired Bootstrap"); }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, path);
                log.AppendLine($"  [{(exists ? "fix" : "new")}]  {ruleSet.SceneName}.unity saved");
            }
            else
            {
                log.AppendLine($"  [ok]   {ruleSet.SceneName}.unity already healthy");
            }

            return path;
        }

        private static bool EnsureCamera(UnityEngine.SceneManagement.Scene scene, PrototypeConfigAsset config)
        {
            if (MioSceneProbe.FindComponent<Camera>(scene) != null) return false;

            // The UI renders Screen Space Overlay and needs no camera, but a
            // scene without one logs warnings and leaves nothing clearing the
            // buffer on some targets.
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";

            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config != null && config.Palette != null
                ? config.Palette.Background
                : Color.black;
            camera.orthographic = true;
            return true;
        }

        private static bool EnsureEventSystem(UnityEngine.SceneManagement.Scene scene)
        {
            if (MioSceneProbe.FindComponent<EventSystem>(scene) != null) return false;

            // Same factory the runtime uses, so the input module matches
            // whichever input backend this project is on.
            EventSystemFactory.Create();
            return true;
        }

        private static bool EnsureBootstrap(UnityEngine.SceneManagement.Scene scene, PrototypeConfigAsset config)
        {
            var bootstrap = MioSceneProbe.FindComponent<PrototypeBootstrap>(scene);
            var created = false;

            if (bootstrap == null)
            {
                var go = new GameObject("Bootstrap");
                bootstrap = go.AddComponent<PrototypeBootstrap>();
                created = true;
            }

            // The config field is private so the inspector stays tidy; reach it
            // through SerializedObject rather than loosening the runtime API
            // for the benefit of a tool.
            var serialized = new SerializedObject(bootstrap);
            var property = serialized.FindProperty("_config");

            if (property == null)
            {
                Debug.LogError("[MIO] PrototypeBootstrap has no _config field; setup cannot wire the scene.");
                return created;
            }

            if (property.objectReferenceValue == config) return created;

            property.objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void AddToBuildSettings(IList<string> scenePaths, StringBuilder log)
        {
            if (scenePaths.Count == 0) return;

            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var added = 0;

            foreach (var path in scenePaths)
            {
                var found = false;
                foreach (var entry in existing)
                {
                    if (entry.path != path) continue;

                    if (!entry.enabled) { entry.enabled = true; added++; }
                    found = true;
                    break;
                }

                if (found) continue;

                existing.Add(new EditorBuildSettingsScene(path, true));
                added++;
            }

            EditorBuildSettings.scenes = existing.ToArray();

            // In Unity 6 a Build Profile inherits this global scene list unless
            // it has been given its own override, so writing it here is the
            // portable way to configure both.
            log.AppendLine(added == 0
                ? "  [ok]   build scene list already correct"
                : $"  [fix]  build scene list updated ({added} scene(s))");
        }

        [MenuItem("PROJECT MIO/Advanced/Apply Mobile Player Settings", priority = 100)]
        public static void ApplyMobilePlayerSettings()
        {
            PlayerSettings.companyName = "Valorcia";
            PlayerSettings.productName = "PROJECT MIO";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

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
