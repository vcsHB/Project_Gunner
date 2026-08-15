using System;

/// <summary>
/// System.Random 기반 시드 난수원.
///
/// UnityEngine.Random을 쓰지 않는 이유는 그것이 프로세스 전역 상태이기 때문이다.
/// 인스턴스마다 상태를 따로 들고 있어야 "전투 판정용"과 "맵 생성용"을 분리할 수 있다.
/// </summary>
public class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    public int Seed { get; }

    public float Value => (float)_random.NextDouble();

    public float Range(float min, float max)
    {
        if (max <= min) return min;

        return min + (float)_random.NextDouble() * (max - min);
    }

    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;

        return _random.Next(minInclusive, maxExclusive);
    }
}
