using System;

namespace Mio.Core.PopChain
{
    [Serializable]
    public sealed class PopChainConfig
    {
        public float Duration = 45f;

        public int Width = 7;
        public int Height = 9;

        /// <summary>Distinct colours. Fewer colours means bigger, easier groups.</summary>
        public int ColorCount = 4;

        /// <summary>Smallest group that can be popped.</summary>
        public int MinGroupSize = 2;

        public int ScorePerCell = 12;

        /// <summary>Score needed to win before the clock runs out.</summary>
        public int TargetScore = 1200;

        /// <summary>
        /// Pop again inside this window and the multiplier steps up. This is the
        /// entire reason the prototype is called POP CHAIN: it rewards reading
        /// the board ahead rather than tapping at random.
        /// </summary>
        public float ChainWindow = 1.35f;

        public int MaxChainMultiplier = 8;

        /// <summary>
        /// Play-field rectangle the board is drawn into. The view fills this
        /// rect exactly so that what you see is what a tap hits, which means
        /// the rect's proportions decide the cells': these defaults give square
        /// cells for a 7x9 board on a 9:16 field.
        /// </summary>
        public float FieldMinX = 0.06f;
        public float FieldMaxX = 0.94f;
        public float FieldMinY = 0.10f;
        public float FieldMaxY = 0.74f;

        public PopChainConfig Clone() => (PopChainConfig)MemberwiseClone();
    }
}
