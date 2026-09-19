using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mio.Editor
{
    /// <summary>
    /// Shortcuts for opening a test scene, so a non-Unity tester never has to
    /// navigate the Project window.
    /// </summary>
    public static class MioMenus
    {
        [MenuItem("PROJECT MIO/Open Harness Test", priority = 20)]
        public static void OpenHarnessTest() => Open(MioRuleSetCatalog.Harness);

        [MenuItem("PROJECT MIO/Open FLOW Test", priority = 21)]
        public static void OpenFlowTest() => Open(MioRuleSetCatalog.Flow);

        /// <summary>
        /// Greys the FLOW entry out until FLOW exists, so the menu tells the
        /// truth about what this build contains rather than offering something
        /// that cannot work.
        /// </summary>
        [MenuItem("PROJECT MIO/Open FLOW Test", true)]
        public static bool OpenFlowTestValidate() => MioRuleSetCatalog.Flow.IsImplemented;

        private static void Open(MioRuleSet ruleSet)
        {
            if (!ruleSet.IsImplemented)
            {
                EditorUtility.DisplayDialog(
                    "PROJECT MIO",
                    $"{ruleSet.DisplayName} is not implemented yet.\n\n" +
                    "This build contains the shared foundation (M0.1) and the " +
                    "test harness only.\n\n" +
                    "Use PROJECT MIO > Open Harness Test instead.",
                    "OK");
                return;
            }

            if (!File.Exists(ruleSet.ScenePath))
            {
                if (EditorUtility.DisplayDialog(
                        "PROJECT MIO",
                        $"{ruleSet.SceneName}.unity does not exist yet.\n\n" +
                        "Create it now?",
                        "Set up the project", "Cancel"))
                {
                    MioProjectBuilder.SetupTestEnvironment();
                }

                if (!File.Exists(ruleSet.ScenePath)) return;
            }

            // Offers to save anything unsaved rather than discarding it.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ruleSet.ScenePath, OpenSceneMode.Single);
            Debug.Log($"[MIO] Opened {ruleSet.SceneName}. Press Play to start the session.");
        }

        [MenuItem("PROJECT MIO/Advanced/Open Metrics Folder", priority = 101)]
        public static void OpenMetricsFolder()
        {
            var path = Application.persistentDataPath;
            if (!Directory.Exists(path))
            {
                EditorUtility.DisplayDialog(
                    "PROJECT MIO",
                    "No data folder yet. Press Play once, finish a session, then try again.",
                    "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
            Debug.Log($"[MIO] Metrics folder: {path}");
        }

        [MenuItem("PROJECT MIO/Advanced/Reset Player Wallet", priority = 102)]
        public static void ResetWallet()
        {
            if (!EditorUtility.DisplayDialog(
                    "PROJECT MIO",
                    "Clear the saved wallet balance on this machine?\n\n" +
                    "Use this to test a first-run experience.",
                    "Clear", "Cancel"))
            {
                return;
            }

            new Mio.Unity.App.PlayerPrefsProfileStore().Clear();
            Debug.Log("[MIO] Player wallet cleared.");
        }
    }
}
