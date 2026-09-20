using System;
using Mio.Core.Economy;
using UnityEngine;

namespace Mio.Unity.Config
{
    /// <summary>
    /// The payout curve as an inspector asset. Nothing in gameplay knows these
    /// numbers, so the whole economy can be re-balanced without a recompile.
    /// </summary>
    [CreateAssetMenu(menuName = "MIO/Config/Reward Table", fileName = "RewardTable")]
    public sealed class RewardTableAsset : ScriptableObject
    {
        [Serializable]
        public struct Bundle
        {
            public int Cotton;
            public int Wood;
            public int Metal;

            public ResourceBundle ToCore() => new ResourceBundle(Cotton, Wood, Metal);
        }

        [Serializable]
        public struct Tier
        {
            [Tooltip("Score at or above which this bundle is paid.")]
            public int MinScore;

            public Bundle Payout;
        }

        [Tooltip("Paid for finishing a run at all, win or lose.")]
        [SerializeField]
        private Bundle _participation = new Bundle { Cotton = 2 };

        [Tooltip("Added on top when the run is won.")]
        [SerializeField]
        private Bundle _completionBonus = new Bundle { Metal = 5 };

        [Tooltip("Only the highest tier the player reaches is paid.")]
        [SerializeField]
        private Tier[] _tiers =
        {
            new Tier { MinScore = 100, Payout = new Bundle { Wood = 1 } },
            new Tier { MinScore = 400, Payout = new Bundle { Wood = 3 } },
            new Tier { MinScore = 900, Payout = new Bundle { Wood = 6, Metal = 2 } }
        };

        public RewardTable Build()
        {
            var tiers = new RewardTier[_tiers == null ? 0 : _tiers.Length];
            for (var i = 0; i < tiers.Length; i++)
            {
                tiers[i] = new RewardTier(_tiers[i].MinScore, _tiers[i].Payout.ToCore());
            }

            return new RewardTable(_participation.ToCore(), _completionBonus.ToCore(), tiers);
        }
    }
}
