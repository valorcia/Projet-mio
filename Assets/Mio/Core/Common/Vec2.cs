using System;

namespace Mio.Core.Common
{
    /// <summary>
    /// Minimal 2D vector. Core must not reference UnityEngine, so it cannot use
    /// Vector2. The Unity layer converts at the boundary.
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }

    public static class MathK
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        /// <summary>Moves <paramref name="current"/> toward target by at most maxDelta.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            var diff = target - current;
            if (diff > maxDelta) return current + maxDelta;
            if (diff < -maxDelta) return current - maxDelta;
            return target;
        }
    }
}
