using Mio.Core.Economy;
using Mio.Core.Harness;
using UnityEngine;

namespace Mio.Unity.Config
{
    /// <summary>
    /// Base for the per-rule-set tuning assets.
    ///
    /// Config assets exist so every number a designer might want to change
    /// lives in an inspector, not in code. Each asset hands the Core a plain
    /// clone of its data, so editing the asset mid-play cannot mutate a session
    /// that is already in flight.
    /// </summary>
    public abstract class PrototypeConfigAsset : ScriptableObject
    {
        [Header("Economy")]
        [Tooltip("Resources this rule set pays out. Shared assets are fine.")]
        public RewardTableAsset Rewards;

        [Header("Presentation")]
        public PrototypePalette Palette;

        public FeedbackProfile Feedback;

        [Header("Determinism")]
        [Tooltip("Leave on for playtests. Turn off and fix the seed to compare " +
                 "two tunings on an identical session.")]
        public bool RandomiseSeed = true;

        public int FixedSeed = 1;

        public int NextSeed()
        {
            return RandomiseSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : FixedSeed;
        }

        public RewardTable BuildRewardTable()
        {
            return Rewards != null ? Rewards.Build() : RewardTable.Empty;
        }
    }

    /// <summary>
    /// Tuning for the architecture validation rule set.
    /// Not a game; see <see cref="TestRuleset"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "MIO/Config/Test Ruleset", fileName = "TestRulesetConfig")]
    public sealed class TestRulesetConfigAsset : PrototypeConfigAsset
    {
        [SerializeField] private TestRulesetConfig _config = new TestRulesetConfig();

        public TestRulesetConfig Build() => _config.Clone();
    }
}
