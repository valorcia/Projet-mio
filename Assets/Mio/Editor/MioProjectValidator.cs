using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mio.Unity.App;
using Mio.Unity.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Mio.Editor
{
    public enum MioCheckState
    {
        Pass,
        Fail,

        /// <summary>Not required at this milestone. Never blocks readiness.</summary>
        Skipped
    }

    public sealed class MioCheck
    {
        public string Section;
        public string Name;
        public MioCheckState State;
        public string Detail;

        /// <summary>Exactly what a human should do. Required when State is Fail.</summary>
        public string Fix;
    }

    public sealed class MioValidationReport
    {
        public readonly List<MioCheck> Checks = new List<MioCheck>();

        public bool Ready
        {
            get
            {
                foreach (var check in Checks)
                {
                    if (check.State == MioCheckState.Fail) return false;
                }

                return true;
            }
        }

        public int FailureCount
        {
            get
            {
                var n = 0;
                foreach (var check in Checks)
                {
                    if (check.State == MioCheckState.Fail) n++;
                }

                return n;
            }
        }

        public string ToConsoleString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PROJECT MIO — PREFLIGHT CHECK");
            sb.AppendLine("=============================");

            var section = string.Empty;
            foreach (var check in Checks)
            {
                if (check.Section != section)
                {
                    section = check.Section;
                    sb.AppendLine();
                    sb.AppendLine(section.ToUpperInvariant());
                }

                var mark = check.State == MioCheckState.Pass ? "[ OK ]"
                         : check.State == MioCheckState.Fail ? "[FAIL]"
                         : "[skip]";

                sb.Append("  ").Append(mark).Append(' ').Append(check.Name);
                if (!string.IsNullOrEmpty(check.Detail)) sb.Append(" — ").Append(check.Detail);
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("-----------------------------");

            if (Ready)
            {
                sb.AppendLine("PROJECT MIO READY TO TEST");
            }
            else
            {
                sb.AppendLine("PROJECT MIO NOT READY");
                sb.AppendLine();
                sb.AppendLine($"{FailureCount} problem(s). Corrective actions:");

                var n = 0;
                foreach (var check in Checks)
                {
                    if (check.State != MioCheckState.Fail) continue;
                    n++;
                    sb.AppendLine($"  {n}. {check.Name}");
                    sb.AppendLine($"     -> {check.Fix}");
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Preflight check run before handing the project to a human tester.
    ///
    /// Every check is a static or asset-level fact. Runtime wiring — which
    /// objects the bootstrap builds when Play is pressed — is verified by
    /// confirming the participating types and serialised references exist; the
    /// report says so rather than implying it watched the game run.
    /// </summary>
    public static class MioProjectValidator
    {
        [MenuItem("PROJECT MIO/Validate Project", priority = 1)]
        public static void ValidateMenu()
        {
            var report = Validate();
            Debug.Log(report.ToConsoleString());

            EditorUtility.DisplayDialog(
                "PROJECT MIO",
                report.Ready
                    ? "PROJECT MIO READY TO TEST\n\nFull report is in the Console."
                    : $"PROJECT MIO NOT READY\n\n{report.FailureCount} problem(s).\n\n" +
                      "The Console lists the exact corrective actions.\n\n" +
                      "Most problems are fixed by:\nPROJECT MIO > Setup Test Environment",
                "OK");
        }

        public static MioValidationReport Validate()
        {
            var report = new MioValidationReport();

            CheckCoreArchitecture(report);
            CheckScriptableObjects(report);
            CheckScenes(report);
            CheckSubsystems(report);
            CheckBuildScenes(report);

            return report;
        }

        private static void Add(
            MioValidationReport report,
            string section,
            string name,
            bool pass,
            string detail,
            string fix)
        {
            report.Checks.Add(new MioCheck
            {
                Section = section,
                Name = name,
                State = pass ? MioCheckState.Pass : MioCheckState.Fail,
                Detail = detail,
                Fix = fix
            });
        }

        private static void Skip(MioValidationReport report, string section, string name, string detail)
        {
            report.Checks.Add(new MioCheck
            {
                Section = section,
                Name = name,
                State = MioCheckState.Skipped,
                Detail = detail
            });
        }

        private static bool TypeExists(string assemblyQualifiedName)
        {
            return Type.GetType(assemblyQualifiedName) != null;
        }

        // ---- sections ----

        private static void CheckCoreArchitecture(MioValidationReport report)
        {
            const string S = "Core architecture";

            var required = new (string Label, string TypeName)[]
            {
                ("IPrototypeRules", "Mio.Core.Session.IPrototypeRules, Mio.Core"),
                ("PrototypeRunner", "Mio.Core.Session.PrototypeRunner, Mio.Core"),
                ("InputCommand", "Mio.Core.Session.InputCommand, Mio.Core"),
                ("PlayFieldSpace", "Mio.Core.Session.PlayFieldSpace, Mio.Core"),
                ("FeedbackCue", "Mio.Core.Session.FeedbackCue, Mio.Core"),
                ("MetricReport", "Mio.Core.Metrics.MetricReport, Mio.Core"),
                ("RewardTable", "Mio.Core.Economy.RewardTable, Mio.Core"),
                ("PlayerWallet", "Mio.Core.Profile.PlayerWallet, Mio.Core"),
                ("DeterministicRng", "Mio.Core.Common.DeterministicRng, Mio.Core")
            };

            foreach (var entry in required)
            {
                Add(report, S, entry.Label, TypeExists(entry.TypeName),
                    TypeExists(entry.TypeName) ? "present" : "type not found",
                    "Mio.Core failed to compile. Open the Console, fix the compile " +
                    "errors, then re-run PROJECT MIO > Validate Project.");
            }

            // The rule the whole architecture rests on. Mio.Core is marked
            // noEngineReferences, so this is enforced by the compiler; the
            // check guards against someone editing the asmdef.
            var asmdefPath = $"{MioPaths.Root}/Core/Mio.Core.asmdef";
            var asmdefOk = File.Exists(asmdefPath) &&
                           File.ReadAllText(asmdefPath).Contains("\"noEngineReferences\": true");

            Add(report, S, "Core does not reference UnityEngine", asmdefOk,
                asmdefOk ? "noEngineReferences: true" : "asmdef missing or flag cleared",
                $"Set \"noEngineReferences\": true in {asmdefPath}. Gameplay logic " +
                "must stay engine-free so it can be tested without Unity.");
        }

        private static void CheckScriptableObjects(MioValidationReport report)
        {
            const string S = "ScriptableObjects";

            CheckSharedAsset<PrototypePalette>(report, S, "Palette");
            CheckSharedAsset<FeedbackProfile>(report, S, "FeedbackProfile");
            CheckSharedAsset<RewardTableAsset>(report, S, "RewardTable");

            foreach (var ruleSet in MioRuleSetCatalog.All)
            {
                if (!ruleSet.IsImplemented)
                {
                    Skip(report, S, $"{ruleSet.DisplayName} config",
                        $"{ruleSet.DisplayName} is not implemented yet");
                    continue;
                }

                var config = AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ruleSet.ConfigPath);

                Add(report, S, $"{ruleSet.DisplayName} config", config != null,
                    config != null ? ruleSet.ConfigPath : "missing",
                    "Run PROJECT MIO > Setup Test Environment.");

                if (config == null) continue;

                var missing = new List<string>();
                if (config.Palette == null) missing.Add("Palette");
                if (config.Feedback == null) missing.Add("Feedback");
                if (config.Rewards == null) missing.Add("Rewards");

                Add(report, "Missing references",
                    $"{ruleSet.DisplayName} config references",
                    missing.Count == 0,
                    missing.Count == 0 ? "all assigned" : "unassigned: " + string.Join(", ", missing),
                    "Run PROJECT MIO > Setup Test Environment, which fills blank " +
                    "references without touching tuning you have already done.");
            }
        }

        private static void CheckSharedAsset<T>(MioValidationReport report, string section, string name)
            where T : ScriptableObject
        {
            var path = $"{MioPaths.Settings}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            Add(report, section, name, asset != null,
                asset != null ? path : "missing",
                "Run PROJECT MIO > Setup Test Environment.");
        }

        private static void CheckScenes(MioValidationReport report)
        {
            const string S = "Scenes";

            foreach (var ruleSet in MioRuleSetCatalog.All)
            {
                if (!ruleSet.IsImplemented)
                {
                    Skip(report, S, $"{ruleSet.SceneName}.unity",
                        $"{ruleSet.DisplayName} is not implemented yet — nothing to build");
                    continue;
                }

                if (!File.Exists(ruleSet.ScenePath))
                {
                    Add(report, S, $"{ruleSet.SceneName}.unity", false, "missing",
                        "Run PROJECT MIO > Setup Test Environment.");
                    continue;
                }

                InspectScene(report, ruleSet);
            }
        }

        /// <summary>
        /// Opens the scene additively to inspect it, then closes it without
        /// saving. Additive so validation never disturbs whatever the user
        /// currently has open or unsaved.
        /// </summary>
        private static void InspectScene(MioValidationReport report, MioRuleSet ruleSet)
        {
            const string S = "Scenes";
            Scene scene = default;
            var opened = false;

            try
            {
                scene = EditorSceneManager.OpenScene(ruleSet.ScenePath, OpenSceneMode.Additive);
                opened = scene.IsValid();

                if (!opened)
                {
                    Add(report, S, $"{ruleSet.SceneName}.unity", false, "could not be opened",
                        $"Delete {ruleSet.ScenePath} and run " +
                        "PROJECT MIO > Setup Test Environment to regenerate it.");
                    return;
                }

                Add(report, S, $"{ruleSet.SceneName}.unity", true, "present", null);

                var bootstrap = MioSceneProbe.FindComponent<PrototypeBootstrap>(scene);

                Add(report, S, $"{ruleSet.SceneName}: Bootstrap", bootstrap != null,
                    bootstrap != null ? "present" : "no PrototypeBootstrap in scene",
                    "Run PROJECT MIO > Setup Test Environment to repair the scene.");

                if (bootstrap != null)
                {
                    var serialized = new SerializedObject(bootstrap);
                    var property = serialized.FindProperty("_config");
                    var assigned = property != null && property.objectReferenceValue != null;

                    Add(report, "Missing references",
                        $"{ruleSet.SceneName}: Bootstrap config",
                        assigned,
                        assigned ? property.objectReferenceValue.name : "not assigned",
                        "Run PROJECT MIO > Setup Test Environment. Without a config " +
                        "the scene logs an error and nothing runs.");
                }

                var hasEventSystem = MioSceneProbe.Has<EventSystem>(scene);

                Add(report, "EventSystem", $"{ruleSet.SceneName}: EventSystem",
                    hasEventSystem,
                    hasEventSystem
                        ? "present in scene"
                        : "absent from scene (runtime factory would still create one)",
                    "Run PROJECT MIO > Setup Test Environment. Without an EventSystem " +
                    "no touch or click reaches the game.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CheckSubsystems(MioValidationReport report)
        {
            // These subsystems are assembled by PrototypeBootstrap at Play
            // time, so what is verifiable ahead of Play is that every
            // participating type exists and is reachable.
            CheckTypeGroup(report, "Input routing", new (string, string)[]
            {
                ("PlayFieldInput (EventSystem routing)", "Mio.Unity.Input.PlayFieldInput, Mio.Unity"),
                ("PlayFieldSpace (normalised coordinates)", "Mio.Core.Session.PlayFieldSpace, Mio.Core"),
                ("EventSystemFactory", "Mio.Unity.App.EventSystemFactory, Mio.Unity")
            });

            CheckTypeGroup(report, "Feedback routing", new (string, string)[]
            {
                ("FeedbackRouter", "Mio.Unity.Feedback.FeedbackRouter, Mio.Unity"),
                ("FeedbackProfile", "Mio.Unity.Config.FeedbackProfile, Mio.Unity"),
                ("BurstPool (particles)", "Mio.Unity.Feedback.BurstPool, Mio.Unity"),
                ("PunchAnimator (punch)", "Mio.Unity.Feedback.PunchAnimator, Mio.Unity"),
                ("DeviceHapticChannel (haptics)", "Mio.Unity.Feedback.DeviceHapticChannel, Mio.Unity")
            });

            CheckTypeGroup(report, "Metrics", new (string, string)[]
            {
                ("IMetricsSink", "Mio.Core.Metrics.IMetricsSink, Mio.Core"),
                ("ConsoleMetricsSink", "Mio.Unity.App.ConsoleMetricsSink, Mio.Unity"),
                ("JsonlMetricsSink", "Mio.Unity.App.JsonlMetricsSink, Mio.Unity"),
                ("CompositeMetricsSink", "Mio.Core.Metrics.CompositeMetricsSink, Mio.Core")
            });

            CheckTypeGroup(report, "Profile store", new (string, string)[]
            {
                ("IProfileStore", "Mio.Core.Profile.IProfileStore, Mio.Core"),
                ("PlayerPrefsProfileStore", "Mio.Unity.App.PlayerPrefsProfileStore, Mio.Unity"),
                ("PlayerProfile", "Mio.Core.Profile.PlayerProfile, Mio.Core")
            });
        }

        private static void CheckTypeGroup(
            MioValidationReport report,
            string section,
            (string Label, string TypeName)[] entries)
        {
            foreach (var entry in entries)
            {
                var ok = TypeExists(entry.TypeName);

                Add(report, section, entry.Label, ok,
                    ok ? "present" : "type not found",
                    "The assembly containing this type failed to compile. Fix the " +
                    "Console errors, then re-run PROJECT MIO > Validate Project.");
            }
        }

        private static void CheckBuildScenes(MioValidationReport report)
        {
            const string S = "Build scenes";

            var listed = new HashSet<string>();
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (entry.enabled) listed.Add(entry.path);
            }

            foreach (var ruleSet in MioRuleSetCatalog.All)
            {
                if (!ruleSet.IsImplemented)
                {
                    Skip(report, S, ruleSet.SceneName, $"{ruleSet.DisplayName} is not implemented yet");
                    continue;
                }

                var included = listed.Contains(ruleSet.ScenePath);

                Add(report, S, ruleSet.SceneName, included,
                    included ? "enabled in build list" : "not in the build list",
                    "Run PROJECT MIO > Setup Test Environment. A scene missing from " +
                    "the list cannot be loaded in a device build.");
            }

            report.Checks.Add(new MioCheck
            {
                Section = S,
                Name = "Build Profiles",
                State = MioCheckState.Pass,
                Detail = "a Build Profile inherits this global scene list unless it overrides it"
            });
        }
    }
}
