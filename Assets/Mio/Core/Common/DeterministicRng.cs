namespace Mio.Core.Common
{
    /// <summary>
    /// xorshift128 generator. We do not use System.Random because its internal
    /// algorithm differs between runtimes, which would make a seed produce
    /// different boards in the editor, on device and in tests.
    /// </summary>
    public sealed class DeterministicRng
    {
        private uint _x, _y, _z, _w;

        public DeterministicRng(int seed)
        {
            // SplitMix-style scramble so that neighbouring seeds (0, 1, 2...)
            // do not produce visibly similar sequences.
            var s = (uint)seed;
            _x = Scramble(ref s);
            _y = Scramble(ref s);
            _z = Scramble(ref s);
            _w = Scramble(ref s);
            if ((_x | _y | _z | _w) == 0u) _x = 0x9E3779B9u;
        }

        public int Seed { get; private set; }

        private static uint Scramble(ref uint state)
        {
            state += 0x9E3779B9u;
            var z = state;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            return z ^ (z >> 16);
        }

        public uint NextUInt()
        {
            var t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            return _w;
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var span = (uint)(maxExclusive - minInclusive);

            // Rejection sampling keeps the distribution uniform; a plain modulo
            // would bias the low end of the range.
            var limit = uint.MaxValue - (uint.MaxValue % span);
            uint value;
            do { value = NextUInt(); } while (value >= limit);
            return minInclusive + (int)(value % span);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Next01()
        {
            // 24 bits of mantissa is all a float can represent exactly.
            return (NextUInt() >> 8) * (1.0f / 16777216.0f);
        }

        public float Range(float minInclusive, float maxExclusive)
        {
            return minInclusive + (maxExclusive - minInclusive) * Next01();
        }

        public bool Chance(float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            return Next01() < probability;
        }
    }
}
