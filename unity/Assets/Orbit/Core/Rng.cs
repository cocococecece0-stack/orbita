using System;

namespace Orbit.Core
{
    /// <summary>Детерминированный xorshift64, общий для орбиты и тетро-движка.</summary>
    public sealed class Rng
    {
        private ulong _state;

        public Rng(int seed)
        {
            ulong value = (ulong)(long)seed;
            _state = value ^ 0x9E3779B97F4A7C15UL;
            if (_state == 0) _state = 0x6D2B79F5UL;
        }

        public float NextFloat()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            ulong result = _state * 0x2545F4914F6CDD1DUL;
            return (float)((result >> 11) / (double)(1UL << 53));
        }

        public int NextInt(int max)
        {
            if (max <= 0) return 0;
            return (int)(NextFloat() * max) % max;
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }
    }
}
