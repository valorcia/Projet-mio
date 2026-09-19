using System;
using Mio.Core.Economy;
using Mio.Core.Flow;
using Mio.Core.Pack;
using Mio.Core.PopChain;
using UnityEngine;

namespace Mio.Unity.Config
{
    /// <summary>
    /// Base for the per-prototype tuning assets.
    ///
    /// Config assets exist so every number a designer might want to change
    /// lives in an inspector, not in code. Each asset hands the Core a plain
    /// clone of its data, so editing the asset mid-play cannot mutate a run
    /// that is already in flight.
    /// </summary>
    public abstract class PrototypeConfigAsset : ScriptableObject
    {
        [Header("Economy")]
        [Tooltip("Resources this prototype pays out. Shared assets are fine.")]
        public RewardTableAsset Rewards;

        [Header("Presentation")]
        public PrototypePalette Palette;

        public FeedbackProfile Feedback;

        /// <summary>
        /// Seed for the next run. Leave Randomise on for playtests; turn it off
        /// and fix the seed to compare two tunings on an identical layout.
        /// </summary>
        [Header("Determinism")]
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

    [CreateAssetMenu(menuName = "MIO/Config/Flow", fileName = "FlowConfig")]
    public sealed class FlowConfigAsset : PrototypeConfigAsset
    {
        [SerializeField] private FlowConfig _config = new FlowConfig();

        public FlowConfig Build() => _config.Clone();
    }

    [CreateAssetMenu(menuName = "MIO/Config/Pop Chain", fileName = "PopChainConfig")]
    public sealed class PopChainConfigAsset : PrototypeConfigAsset
    {
        [SerializeField] private PopChainConfig _config = new PopChainConfig();

        public PopChainConfig Build() => _config.Clone();
    }

    [CreateAssetMenu(menuName = "MIO/Config/Pack", fileName = "PackConfig")]
    public sealed class PackConfigAsset : PrototypeConfigAsset
    {
        [SerializeField] private PackConfig _config = new PackConfig();

        [Tooltip("Leave empty to use the built-in shape catalogue.")]
        [SerializeField] private AuthoredShape[] _shapes = new AuthoredShape[0];

        [Serializable]
        public struct AuthoredShape
        {
            public string Name;

            [Tooltip("Cell offsets from the shape's bottom-left corner.")]
            public Vector2Int[] Cells;
        }

        public PackConfig Build()
        {
            var config = _config.Clone();
            config.Shapes = BuildShapes();
            return config;
        }

        private PackShape[] BuildShapes()
        {
            if (_shapes == null || _shapes.Length == 0) return null;

            var result = new PackShape[_shapes.Length];
            for (var i = 0; i < _shapes.Length; i++)
            {
                var source = _shapes[i].Cells ?? new Vector2Int[0];
                var cells = new GridOffset[source.Length];
                for (var c = 0; c < source.Length; c++)
                {
                    cells[c] = new GridOffset(source[c].x, source[c].y);
                }

                result[i] = new PackShape(_shapes[i].Name, cells);
            }

            return result;
        }
    }
}
