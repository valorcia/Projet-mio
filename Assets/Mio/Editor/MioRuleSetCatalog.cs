using System;
using System.Collections.Generic;
using Mio.Unity.Config;
using UnityEditor;
using UnityEngine;

namespace Mio.Editor
{
    /// <summary>
    /// One rule set the tooling knows how to build a scene for.
    /// </summary>
    public sealed class MioRuleSet
    {
        /// <summary>Human-facing name, e.g. "FLOW".</summary>
        public string DisplayName;

        /// <summary>Scene file name without extension.</summary>
        public string SceneName;

        /// <summary>Config asset file name without extension.</summary>
        public string ConfigAssetName;

        /// <summary>Assembly-qualified config ScriptableObject type.</summary>
        public string ConfigTypeName;

        /// <summary>Assembly-qualified rules type, used to detect implementation.</summary>
        public string RulesTypeName;

        /// <summary>False when this rule set is not required at the current milestone.</summary>
        public bool RequiredNow;

        /// <summary>Resolved config type, or null when the rule set does not exist yet.</summary>
        public Type ConfigType => Type.GetType(ConfigTypeName);

        public Type RulesType => Type.GetType(RulesTypeName);

        /// <summary>
        /// A rule set counts as implemented only when BOTH its rules and its
        /// config asset type exist. Building a scene from a config the
        /// bootstrap cannot dispatch on would produce a scene that throws on
        /// Play, which is worse than no scene at all.
        /// </summary>
        public bool IsImplemented => RulesType != null && ConfigType != null;

        public string ScenePath => $"{MioPaths.Scenes}/{SceneName}.unity";
        public string ConfigPath => $"{MioPaths.Settings}/{ConfigAssetName}.asset";
    }

    public static class MioPaths
    {
        public const string Root = "Assets/Mio";
        public const string Settings = Root + "/Settings";
        public const string Scenes = Root + "/Scenes";
    }

    /// <summary>
    /// Everything the setup and validation tools iterate over.
    ///
    /// Rule sets are discovered by type name rather than referenced directly,
    /// so this file does not need editing when M0.2 lands: implement
    /// Mio.Core.Flow.FlowRules and Mio.Unity.Config.FlowConfigAsset and the
    /// FLOW scene starts being created automatically.
    /// </summary>
    public static class MioRuleSetCatalog
    {
        public static readonly MioRuleSet Harness = new MioRuleSet
        {
            DisplayName = "Test Harness",
            SceneName = "M0_TestHarness",
            ConfigAssetName = "TestRulesetConfig",
            ConfigTypeName = "Mio.Unity.Config.TestRulesetConfigAsset, Mio.Unity",
            RulesTypeName = "Mio.Core.Harness.TestRuleset, Mio.Core",
            RequiredNow = true
        };

        public static readonly MioRuleSet Stack = new MioRuleSet
        {
            DisplayName = "STACK",
            SceneName = "MioStack",
            ConfigAssetName = "StackConfig",
            ConfigTypeName = "Mio.Unity.Config.StackConfigAsset, Mio.Unity",
            RulesTypeName = "Mio.Core.Prototypes.StackRules, Mio.Core",
            RequiredNow = true
        };

        public static readonly MioRuleSet MergeFactory = new MioRuleSet
        {
            DisplayName = "MERGE FACTORY",
            SceneName = "MioMergeFactory",
            ConfigAssetName = "MergeFactoryConfig",
            ConfigTypeName = "Mio.Unity.Config.MergeFactoryConfigAsset, Mio.Unity",
            RulesTypeName = "Mio.Core.Prototypes.MergeFactoryRules, Mio.Core",
            RequiredNow = true
        };

        public static readonly MioRuleSet Pop = new MioRuleSet
        {
            DisplayName = "POP",
            SceneName = "MioPop",
            ConfigAssetName = "PopConfig",
            ConfigTypeName = "Mio.Unity.Config.PopConfigAsset, Mio.Unity",
            RulesTypeName = "Mio.Core.Prototypes.PopRules, Mio.Core",
            RequiredNow = true
        };

        public static readonly MioRuleSet MioMix = new MioRuleSet
        {
            DisplayName = "MIO MIX",
            SceneName = "MioMix",
            ConfigAssetName = "MioMixConfig",
            ConfigTypeName = "Mio.Unity.Config.MioMixConfigAsset, Mio.Unity",
            RulesTypeName = "Mio.Core.Prototypes.MioMixRules, Mio.Core",
            RequiredNow = true
        };

        public static IEnumerable<MioRuleSet> All
        {
            get
            {
                yield return Harness;
                yield return Stack;
                yield return MergeFactory;
                yield return Pop;
                yield return MioMix;
            }
        }

        /// <summary>Rule sets that actually exist in the current build.</summary>
        public static IEnumerable<MioRuleSet> Implemented
        {
            get
            {
                foreach (var ruleSet in All)
                {
                    if (ruleSet.IsImplemented) yield return ruleSet;
                }
            }
        }

        /// <summary>
        /// Loads the config asset for a rule set, creating it with the
        /// inspector defaults if it is missing.
        /// </summary>
        public static PrototypeConfigAsset LoadOrCreateConfig(MioRuleSet ruleSet)
        {
            if (!ruleSet.IsImplemented) return null;

            var existing = AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ruleSet.ConfigPath);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance(ruleSet.ConfigType) as PrototypeConfigAsset;
            if (created == null)
            {
                Debug.LogError($"[MIO] {ruleSet.ConfigTypeName} is not a PrototypeConfigAsset.");
                return null;
            }

            AssetDatabase.CreateAsset(created, ruleSet.ConfigPath);
            return created;
        }
    }
}
