namespace OpenWC3;

public static class RandomExtensions
{
    /// <summary>客户端 Random.NextUnit：返回 [0,1) 单精度随机数。</summary>
    public static float NextUnit(this System.Random rng) => (float)rng.NextDouble();

    public static int NextRange(this System.Random rng, int minInclusive, int maxExclusive)
        => minInclusive >= maxExclusive ? minInclusive : rng.Next(minInclusive, maxExclusive);
}
